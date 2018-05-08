using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    [ExecutionRule]
    public class PosTransactionRelativeLocktimeAndSignatureOperationCostRule : TransactionRulesRunner
    {
        /// <summary>Consensus options.</summary>
        private PosConsensusOptions consensusOptions;

        public override void Initialize()
        {
            this.consensusOptions = this.Parent.Network.Consensus.Option<PosConsensusOptions>();
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            this.Parent.PerformanceCounter.AddProcessedBlocks(1);

            long sigOpsCost = 0;
            context.Fees = Money.Zero;

            context.CheckInputs = new List<Task<bool>>();
            foreach (Transaction tx in block.Transactions)
            {
                this.Parent.PerformanceCounter.AddProcessedTransactions(1);

                if (!tx.IsCoinBase && !tx.IsCoinStake) 
                {
                    this.TransactionFinalityCheck(tx, context);
                }

                this.MaxSigOpsCostCheck(sigOpsCost, tx, view, flags);

                if (!tx.IsCoinBase && !tx.IsCoinStake)
                {
                    this.CalculateFees(context, tx, view);
                    this.AddCheckInputsToContext(context, tx, view, flags);
                }

                this.UpdateCoinView(context, tx);
            }

            return Task.CompletedTask;
        }

        private Task TransactionFinalityCheck(Transaction tx, RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            //TODO before PR - this logic can be pulled out in the Pow Base and just called here
            int[] prevheights;

            if (!view.HaveInputs(tx))
            {
                this.Logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                ConsensusErrors.BadTransactionMissingInput.Throw();
            }

            prevheights = new int[tx.Inputs.Count];
            // Check that transaction is BIP68 final.
            // BIP68 lock checks (as opposed to nLockTime checks) must
            // be in ConnectBlock because they require the UTXO set.
            for (int j = 0; j < tx.Inputs.Count; j++)
            {
                prevheights[j] = (int) view.AccessCoins(tx.Inputs[j].PrevOut.Hash).Height;
            }

            if (!tx.CheckSequenceLocks(prevheights, index, flags.LockTimeFlags))
            {
                this.Logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
                ConsensusErrors.BadTransactionNonFinal.Throw();
            }

            return Task.CompletedTask;
        }

        private void AddCheckInputsToContext(RuleContext context, Transaction tx, UnspentOutputSet view, DeploymentFlags flags)
        {
            var txData = new PrecomputedTransactionData(tx);
            for (int inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
            {
                this.Parent.PerformanceCounter.AddProcessedInputs(1);
                TxIn input = tx.Inputs[inputIndex];
                int inputIndexCopy = inputIndex;
                TxOut txout = view.GetOutputFor(input);
                var checkInput = new Task<bool>(() =>
                {
                    var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
                    var ctx = new ScriptEvaluationContext(this.Parent.Network)
                    {
                        ScriptVerify = flags.ScriptFlags
                    };
                    return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                });
                checkInput.Start(context.TaskScheduler);
                context.CheckInputs.Add(checkInput);
            }
        }

        private void CalculateFees(RuleContext context, Transaction tx, UnspentOutputSet view)
        {
            //TODO before PR - this logic can be pulled out in the Pow Base and just called here
            this.CheckInputs(tx, context);
            context.Fees += view.GetValueIn(tx) - tx.TotalOut;
        }

        private void MaxSigOpsCostCheck(long sigOpsCost, Transaction tx, UnspentOutputSet view, DeploymentFlags flags)
        {
            //TODO before PR - this logic can be pulled out in the Pow Base and just called here
            // GetTransactionSignatureOperationCost counts 3 types of sigops:
            // * legacy (always),
            // * p2sh (when P2SH enabled in flags and excludes coinbase),
            // * witness (when witness enabled in flags and excludes coinbase).
            sigOpsCost += this.GetTransactionSignatureOperationCost(tx, view, flags);

            if (sigOpsCost > this.consensusOptions.MaxBlockSigopsCost)
                ConsensusErrors.BadBlockSigOps.Throw();
        }

        /// <inheritdoc />
        protected override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            this.Logger.LogTrace("()");

            UnspentOutputSet view = context.Set;

            if (transaction.IsCoinStake)
                context.Stake.TotalCoinStakeValueIn = view.GetValueIn(transaction);

            base.UpdateCoinView(context, transaction);

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            this.Logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

            base.CheckMaturity(coins, spendHeight);

            if (coins.IsCoinstake)
            {
                if ((spendHeight - coins.Height) < this.consensusOptions.CoinbaseMaturity)
                {
                    this.Logger.LogTrace("Coinstake transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, spendHeight, this.consensusOptions.CoinbaseMaturity);
                    this.Logger.LogTrace("(-)[COINSTAKE_PREMATURE_SPENDING]");
                    ConsensusErrors.BadTransactionPrematureCoinstakeSpending.Throw();
                }
            }

            this.Logger.LogTrace("(-)");
        }
    }
}