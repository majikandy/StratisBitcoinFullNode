﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <inheritdoc />
    [ExecutionRule]
    public sealed class SmartContractCoinviewRule : CoinViewRule
    {
        private List<Transaction> blockTxsProcessed;
        private readonly CoinView coinview;
        /// <summary>Consensus parameters.</summary>
        private NBitcoin.Consensus consensusParams;
        private readonly ISmartContractExecutorFactory executorFactory;
        private Transaction generatedTransaction;
        private readonly ContractStateRepositoryRoot originalStateRoot;
        private uint refundCounter;

        public SmartContractCoinviewRule(CoinView coinview, ISmartContractExecutorFactory executorFactory, ContractStateRepositoryRoot stateRoot)
        {
            this.coinview = coinview;
            this.executorFactory = executorFactory;
            this.generatedTransaction = null;
            this.originalStateRoot = stateRoot;
            this.refundCounter = 1;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            this.Logger.LogTrace("()");

            base.Initialize();

            this.consensusParams = this.Parent.Network.Consensus;

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async override Task RunAsync(RuleContext context)
        {
            this.Logger.LogTrace("()");

            this.blockTxsProcessed = new List<Transaction>();
            NBitcoin.Block block = context.BlockValidationContext.Block;
            ChainedHeader index = context.BlockValidationContext.ChainedHeader;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            this.Parent.PerformanceCounter.AddProcessedBlocks(1);

            // Start state from previous block's root
            this.originalStateRoot.SyncToRoot(((SmartContractBlockHeader)context.ConsensusTip.Header).HashStateRoot.ToBytes());
            IContractStateRepository trackedState = this.originalStateRoot.StartTracking();

            this.refundCounter = 1;
            long sigOpsCost = 0;
            Money fees = Money.Zero;
            var checkInputs = new List<Task<bool>>();

            for (int txIndex = 0; txIndex < block.Transactions.Count; txIndex++)
            {
                this.Parent.PerformanceCounter.AddProcessedTransactions(1);
                Transaction tx = block.Transactions[txIndex];
                if (!context.SkipValidation)
                {
                    // TODO: Simplify this condition.
                    if (!tx.IsCoinBase && (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        if (!view.HaveInputs(tx))
                        {
                            this.Logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                            ConsensusErrors.BadTransactionMissingInput.Throw();
                        }

                        var prevheights = new int[tx.Inputs.Count];
                        // Check that transaction is BIP68 final.
                        // BIP68 lock checks (as opposed to nLockTime checks) must
                        // be in ConnectBlock because they require the UTXO set.
                        for (int j = 0; j < tx.Inputs.Count; j++)
                        {
                            prevheights[j] = (int)view.AccessCoins(tx.Inputs[j].PrevOut.Hash).Height;
                        }

                        if (!tx.CheckSequenceLocks(prevheights, index, flags.LockTimeFlags))
                        {
                            this.Logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
                            ConsensusErrors.BadTransactionNonFinal.Throw();
                        }
                    }

                    // GetTransactionSignatureOperationCost counts 3 types of sigops:
                    // * legacy (always),
                    // * p2sh (when P2SH enabled in flags and excludes coinbase),
                    // * witness (when witness enabled in flags and excludes coinbase).
                    sigOpsCost += this.GetTransactionSignatureOperationCost(tx, view, flags);
                    if (sigOpsCost > this.PowConsensusOptions.MaxBlockSigopsCost)
                        ConsensusErrors.BadBlockSigOps.Throw();

                    // TODO: Simplify this condition.
                    if (!tx.IsCoinBase && (!context.IsPoS || (context.IsPoS && !tx.IsCoinStake)))
                    {
                        this.CheckInputs(tx, view, index.Height);
                        fees += view.GetValueIn(tx) - tx.TotalOut;
                        var txData = new PrecomputedTransactionData(tx);
                        for (int inputIndex = 0; inputIndex < tx.Inputs.Count; inputIndex++)
                        {
                            this.Parent.PerformanceCounter.AddProcessedInputs(1);
                            TxIn input = tx.Inputs[inputIndex];
                            int inputIndexCopy = inputIndex;
                            TxOut txout = view.GetOutputFor(input);
                            var checkInput = new Task<bool>(() =>
                            {
                                if (txout.ScriptPubKey.IsSmartContractExec || txout.ScriptPubKey.IsSmartContractInternalCall)
                                {
                                    return input.ScriptSig.IsSmartContractSpend;
                                }

                                var checker = new TransactionChecker(tx, inputIndexCopy, txout.Value, txData);
                                var ctx = new ScriptEvaluationContext(this.Parent.Network);
                                ctx.ScriptVerify = flags.ScriptFlags;
                                return ctx.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                            });

                            checkInput.Start();
                            checkInputs.Add(checkInput);
                        }
                    }
                }

                this.UpdateCoinView(context, tx);

                this.blockTxsProcessed.Add(tx);
            }

            if (!context.SkipValidation)
            {
                this.CheckBlockReward(context, fees, index.Height, block);

                foreach (Task<bool> checkInput in checkInputs)
                {
                    if (await checkInput.ConfigureAwait(false))
                        continue;

                    this.Logger.LogTrace("(-)[BAD_TX_SCRIPT]");
                    ConsensusErrors.BadTransactionScriptError.Throw();
                }
            }
            else this.Logger.LogTrace("BIP68, SigOp cost, and block reward validation skipped for block at height {0}.", index.Height);

            if (new uint256(this.originalStateRoot.Root) != ((SmartContractBlockHeader)block.Header).HashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();

            this.originalStateRoot.Commit();

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, NBitcoin.Block block)
        {
            this.Logger.LogTrace("()");

            Money blockReward = fees + this.GetProofOfWorkReward(height);
            if (block.Transactions[0].TotalOut > blockReward)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            base.CheckCoinbaseMaturity(coins, spendHeight);
        }

        /// <inheritdoc />
        public override Money GetProofOfWorkReward(int height)
        {
            int halvings = height / this.consensusParams.SubsidyHalvingInterval;

            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return 0;

            Money subsidy = this.PowConsensusOptions.ProofOfWorkReward;

            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            subsidy >>= halvings;

            return subsidy;
        }

        /// <summary>
        /// Executes contracts as necessary and updates the coinview / UTXOset after execution.
        /// </summary>
        /// <inheritdoc/>
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            if (this.generatedTransaction != null)
            {
                ValidateGeneratedTransaction(transaction);
                base.UpdateUTXOSet(context, transaction);
                return;
            }

            // If we are here, was definitely submitted by someone
            ValidateSubmittedTransaction(transaction);

            TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsSmartContractExec);
            if (smartContractTxOut == null)
            {
                // Someone submitted a standard transaction - no smart contract opcodes.
                base.UpdateUTXOSet(context, transaction);
                return;
            }

            // Someone submitted a smart contract transaction.
            ExecuteContractTransaction(context, transaction);

            base.UpdateUTXOSet(context, transaction);
        }

        /// <summary>
        /// Validates that any condensing transaction matches the transaction generated during execution
        /// </summary>
        /// <param name="transaction"></param>
        private void ValidateGeneratedTransaction(Transaction transaction)
        {
            if (this.generatedTransaction.GetHash() != transaction.GetHash())
                SmartContractConsensusErrors.UnequalCondensingTx.Throw();

            this.generatedTransaction = null;

            return;
        }

        /// <summary>
        /// Validates that a submitted transacction doesn't contain illegal operations
        /// </summary>
        /// <param name="transaction"></param>
        private void ValidateSubmittedTransaction(Transaction transaction)
        {
            if (transaction.Inputs.Any(x => x.ScriptSig.IsSmartContractSpend))
                SmartContractConsensusErrors.UserOpSpend.Throw();

            if (transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractInternalCall))
                SmartContractConsensusErrors.UserInternalCall.Throw();
        }

        /// <summary>
        /// Executes the smart contract part of a transaction
        /// </summary>
        private void ExecuteContractTransaction(RuleContext context, Transaction transaction)
        {
            ISmartContractTransactionContext txContext = GetSmartContractTransactionContext(context, transaction);
            ISmartContractExecutor executor = this.executorFactory.CreateExecutor(this.originalStateRoot, txContext);

            ISmartContractExecutionResult result = executor.Execute();

            ValidateRefunds(result.Refunds, context.BlockValidationContext.Block.Transactions[0]);

            if (result.InternalTransaction != null)
                this.generatedTransaction = result.InternalTransaction;
        }

        /// <summary>
        /// Retrieves the context object to be given to the contract executor.
        /// </summary>
        private ISmartContractTransactionContext GetSmartContractTransactionContext(RuleContext context, Transaction transaction)
        {
            ulong blockHeight = Convert.ToUInt64(context.BlockValidationContext.ChainedHeader.Height);

            GetSenderUtil.GetSenderResult getSenderResult = GetSenderUtil.GetSender(this.Parent.Network, transaction, this.coinview, this.blockTxsProcessed);

            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));

            Script coinbaseScriptPubKey = context.BlockValidationContext.Block.Transactions[0].Outputs[0].ScriptPubKey;
            GetSenderUtil.GetSenderResult getCoinbaseResult = GetSenderUtil.GetAddressFromScript(this.Parent.Network, coinbaseScriptPubKey);
            if (!getCoinbaseResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-coinbase", getCoinbaseResult.Error));

            Money mempoolFee = transaction.GetFee(context.Set);

            return new SmartContractTransactionContext(blockHeight, getCoinbaseResult.Sender, mempoolFee, getSenderResult.Sender, transaction);
        }

        /// <summary>
        /// Throws a consensus exception if the gas refund inside the block is different to what this node calculated during execution.
        /// </summary>
        private void ValidateRefunds(List<TxOut> refunds, Transaction coinbaseTransaction)
        {
            foreach (TxOut refund in refunds)
            {
                TxOut refundToMatch = coinbaseTransaction.Outputs[this.refundCounter];
                if (refund.Value != refundToMatch.Value || refund.ScriptPubKey != refundToMatch.ScriptPubKey)
                    SmartContractConsensusErrors.UnequalRefundAmounts.Throw();

                this.refundCounter++;
            }
        }
    }
}