using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks signature operation costs
    /// Then updates the coinview with each transaction
    /// </summary>
    [ExecutionRule]
    public class TransactionRulesRunner : ConsensusRule
    {
        /// <summary>The current transaction in the loop of transactions being validated - allowing rules at transaction level without looping again.</summary>
        public const string CurrentTransactionContextKey = "CurrentTransaction";

        public const string CheckInputsContextKey = "CheckInputs";

        public const string SigOpsCostContextKey = "SigOpsCost";

        private readonly IEnumerable<ConsensusRule> transactionConsensusRules;

        /// <summary>Consensus options.</summary>
        public PowConsensusOptions ConsensusOptions { get; private set; }

        public TransactionRulesRunner(params ConsensusRule[] transactionConsensusRules)
        {
            this.transactionConsensusRules = transactionConsensusRules;
        }

        public override void Initialize()
        {
            //TODO before PR - this shouldn't be hardcoded as Pow - this will probably go away since this is now the runner of transaction rules
            this.ConsensusOptions = this.Parent.Network.Consensus.Option<PowConsensusOptions>();
        }

        public override Task RunAsync(RuleContext context)
        {
            this.Parent.PerformanceCounter.AddProcessedBlocks(1);
            context.SetItem(CheckInputsContextKey, new List<Task<bool>>());
            context.SetItem(SigOpsCostContextKey, (long)0);

            //TODO before PR merge - won't this never get reached if validation skipped?
            if (context.SkipValidation)
            { 
                this.Logger.LogTrace("BIP68, SigOp cost, and block reward validation skipped for block at height {0}.", context.BlockValidationContext.ChainedHeader.Height);
                return Task.CompletedTask;
            }

            foreach (Transaction transaction in context.BlockValidationContext.Block.Transactions)
            {
                this.Parent.PerformanceCounter.AddProcessedTransactions(1);

                foreach (var rule in this.transactionConsensusRules)
                {
                    rule.Logger = this.Logger;
                    rule.Parent = this.Parent;
                    context.SetItem(CurrentTransactionContextKey, transaction);
                    rule.Initialize();
                    rule.RunAsync(context);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Calculates signature operation cost for single transaction input.
        /// </summary>
        /// <param name="scriptSig">Signature script.</param>
        /// <param name="scriptPubKey">Script public key.</param>
        /// <param name="witness">Witness script.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for single transaction input.</returns>
        private long CountWitnessSignatureOperation(Script scriptSig, Script scriptPubKey, WitScript witness, DeploymentFlags flags)
        {
            witness = witness ?? WitScript.Empty;
            if (!flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                return 0;

            WitProgramParameters witParams = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(this.Parent.Network, scriptPubKey);

            if (witParams?.Version == 0)
            {
                if (witParams.Program.Length == 20)
                    return 1;

                if (witParams.Program.Length == 32 && witness.PushCount > 0)
                {
                    Script subscript = Script.FromBytesUnsafe(witness.GetUnsafePush(witness.PushCount - 1));
                    return subscript.GetSigOpCount(true);
                }
            }

            return 0;
        }

        /// <summary>
        /// Updates context's UTXO set.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="transaction">Transaction which outputs will be added to the context's <see cref="UnspentOutputSet"/> and which inputs will be removed from it.</param>
        protected virtual void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            this.Logger.LogTrace("()");

            ChainedHeader index = context.BlockValidationContext.ChainedHeader;
            UnspentOutputSet view = context.Set;

            view.Update(transaction, index.Height);

            this.Logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks the maturity of UTXOs.
        /// </summary>
        /// <param name="coins">UTXOs to check the maturity of.</param>
        /// <param name="spendHeight">Height at which coins are attempted to be spent.</param>
        /// <exception cref="ConsensusErrors.BadTransactionPrematureCoinbaseSpending">Thrown if transaction tries to spend coins that are not mature.</exception>
        protected virtual void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            this.Logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

            // If prev is coinbase, check that it's matured
            if (coins.IsCoinbase)
            {
                if ((spendHeight - coins.Height) < this.ConsensusOptions.CoinbaseMaturity)
                {
                    this.Logger.LogTrace("Coinbase transaction height {0} spent at height {1}, but maturity is set to {2}.", coins.Height, spendHeight, this.ConsensusOptions.CoinbaseMaturity);
                    this.Logger.LogTrace("(-)[COINBASE_PREMATURE_SPENDING]");
                    ConsensusErrors.BadTransactionPrematureCoinbaseSpending.Throw();
                }
            }

            this.Logger.LogTrace("(-)");
        }

        protected virtual Task CheckInputs(Transaction transaction, RuleContext context)
        {
            if (transaction.IsCoinBase)
                return Task.CompletedTask;

            UnspentOutputSet inputs = context.Set;
            int spendHeight = context.BlockValidationContext.ChainedHeader.Height;

            //TODO before Merge - share this code between the rules and remove the call inside MempoolValidator
            this.Logger.LogTrace("({0}:{1})", nameof(spendHeight), spendHeight);

            if (!inputs.HaveInputs(transaction))
                ConsensusErrors.BadTransactionMissingInput.Throw();

            Money valueIn = Money.Zero;
            Money fees = Money.Zero;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                OutPoint prevout = transaction.Inputs[i].PrevOut;
                UnspentOutputs coins = inputs.AccessCoins(prevout.Hash);

                this.CheckMaturity(coins, spendHeight);

                // Check for negative or overflow input values.
                valueIn += coins.TryGetOutput(prevout.N).Value;
                if (!this.MoneyRange(coins.TryGetOutput(prevout.N).Value) || !this.MoneyRange(valueIn))
                {
                    this.Logger.LogTrace("(-)[BAD_TX_INPUT_VALUE]");
                    ConsensusErrors.BadTransactionInputValueOutOfRange.Throw();
                }
            }

            if (valueIn < transaction.TotalOut)
            {
                this.Logger.LogTrace("(-)[TX_IN_BELOW_OUT]");
                ConsensusErrors.BadTransactionInBelowOut.Throw();
            }

            // Tally transaction fees.
            Money txFee = valueIn - transaction.TotalOut;
            if (txFee < 0)
            {
                this.Logger.LogTrace("(-)[NEGATIVE_FEE]");
                ConsensusErrors.BadTransactionNegativeFee.Throw();
            }

            fees += txFee;
            if (!this.MoneyRange(fees))
            {
                this.Logger.LogTrace("(-)[BAD_FEE]");
                ConsensusErrors.BadTransactionFeeOutOfRange.Throw();
            }

            this.Logger.LogTrace("(-)");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if value is in range from 0 to <see cref="ConsensusOptions.MaxMoney"/>.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns><c>true</c> if the value is in range. Otherwise <c>false</c>.</returns>
        private bool MoneyRange(long value)
        {
            return ((value >= 0) && (value <= this.ConsensusOptions.MaxMoney));
        }

        /// <summary>
        /// Calculates legacy transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <returns>Legacy signature operation cost for transaction.</returns>
        private long GetLegacySignatureOperationsCount(Transaction transaction)
        {
            long sigOps = 0;
            foreach (TxIn txin in transaction.Inputs)
                sigOps += txin.ScriptSig.GetSigOpCount(false);

            foreach (TxOut txout in transaction.Outputs)
                sigOps += txout.ScriptPubKey.GetSigOpCount(false);

            return sigOps;
        }

        /// <summary>
        /// Calculates pay-to-script-hash (BIP16) transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Signature operation cost for transaction.</returns>
        private uint GetP2SHSignatureOperationsCount(Transaction transaction, UnspentOutputSet inputs)
        {
            if (transaction.IsCoinBase)
                return 0;

            uint sigOps = 0;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                if (prevout.ScriptPubKey.IsPayToScriptHash(this.Parent.Network))
                    sigOps += prevout.ScriptPubKey.GetSigOpCount(this.Parent.Network, transaction.Inputs[i].ScriptSig);
            }

            return sigOps;
        }

        /// <inheritdoc />
        public long GetTransactionSignatureOperationCost(Transaction transaction, UnspentOutputSet inputs, DeploymentFlags flags)
        {
            long signatureOperationCost = this.GetLegacySignatureOperationsCount(transaction) * this.ConsensusOptions.WitnessScaleFactor;

            if (transaction.IsCoinBase)
                return signatureOperationCost;

            if (flags.ScriptFlags.HasFlag(ScriptVerify.P2SH))
            {
                signatureOperationCost += this.GetP2SHSignatureOperationsCount(transaction, inputs) * this.ConsensusOptions.WitnessScaleFactor;
            }

            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                signatureOperationCost += this.CountWitnessSignatureOperation(transaction.Inputs[i].ScriptSig, prevout.ScriptPubKey, transaction.Inputs[i].WitScript, flags);
            }

            return signatureOperationCost;
        }
    }
}