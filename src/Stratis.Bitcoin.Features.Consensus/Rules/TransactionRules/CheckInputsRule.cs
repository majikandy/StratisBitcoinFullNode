﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public abstract class CheckInputsRule : ConsensusRule, ICheckInputs
    {
        public PowConsensusOptions ConsensusOptions { get; set; }

        public override Task RunAsync(RuleContext context)
        {
            var transaction = context.Get<Transaction>(TransactionRulesRunner.CurrentTransactionContextKey);

            if (transaction.IsCoinBase)
                return Task.CompletedTask;

            UnspentOutputSet inputs = context.Set;
            int spendHeight = context.BlockValidationContext.ChainedHeader.Height;

            this.CheckInputs(transaction, inputs, spendHeight);

            this.Logger.LogTrace("(-)");

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void CheckInputs(Transaction transaction, UnspentOutputSet inputs, int spendHeight)
        {
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
        }

        /// <summary>
        /// Checks the maturity of UTXOs.
        /// </summary>
        /// <param name="coins">UTXOs to check the maturity of.</param>
        /// <param name="spendHeight">Height at which coins are attempted to be spent.</param>
        /// <exception cref="ConsensusErrors.BadTransactionPrematureCoinbaseSpending">Thrown if transaction tries to spend coins that are not mature.</exception>
        private void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            this.Logger.LogTrace("({0}:'{1}/{2}',{3}:{4})", nameof(coins), coins.TransactionId, coins.Height, nameof(spendHeight), spendHeight);

            // If prev is coinbase, check that it's matured
            if (CoinsShouldBeChecked(coins))
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

        protected abstract bool CoinsShouldBeChecked(UnspentOutputs coins);
        
        /// <summary>
        /// Checks if value is in range from 0 to <see cref="ConsensusOptions.MaxMoney"/>.
        /// </summary>
        /// <param name="value">The value to be checked.</param>
        /// <returns><c>true</c> if the value is in range. Otherwise <c>false</c>.</returns>
        private bool MoneyRange(long value)
        {
            return ((value >= 0) && (value <= this.ConsensusOptions.MaxMoney));
        }
    }

    public class PosCheckInputsRule : CheckInputsRule
    {
        public override void Initialize()
        {
            this.ConsensusOptions = this.Parent.Network.Consensus.Option<PosConsensusOptions>();
        }

        protected override bool CoinsShouldBeChecked(UnspentOutputs coins)
        {
            return coins.IsCoinstake;
        }
    }

    public class PowCheckInputsRule : CheckInputsRule
    {
        public override void Initialize()
        {
            this.ConsensusOptions = this.Parent.Network.Consensus.Option<PowConsensusOptions>();
        }

        protected override bool CoinsShouldBeChecked(UnspentOutputs coins)
        {
            return coins.IsCoinbase;
        }
    }
}