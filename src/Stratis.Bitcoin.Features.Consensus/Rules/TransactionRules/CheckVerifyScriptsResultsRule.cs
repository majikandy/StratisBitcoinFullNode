﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public abstract class CheckVerifyScriptsResultsRule : ConsensusRule
    {
        public PowConsensusOptions ConsensusOptions { get; set; }

        public override Task RunAsync(RuleContext context)
        {
            ChainedHeader index = context.BlockValidationContext.ChainedHeader;
            if (!context.SkipValidation)
            {
                this.CheckBlockReward(context.Get<Money>(TransactionRulesRunner.TotalBlockFeesContextKey), index.Height, context.BlockValidationContext.Block);

                bool passed = context.Get<List<Task<bool>>>(TransactionRulesRunner.CheckInputsContextKey).All(c => c.GetAwaiter().GetResult());
                if (!passed)
                {
                    this.Logger.LogTrace("(-)[BAD_TX_SCRIPT]");
                    ConsensusErrors.BadTransactionScriptError.Throw();
                }
            }
            else
            { 
                this.Logger.LogTrace("BIP68, SigOp cost, and block reward validation skipped for block at height {0}.", index.Height);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Verifies that block has correct coinbase transaction with appropriate reward and fees summ.
        /// </summary>
        /// <param name="fees">Total amount of fees from transactions that are included in that block.</param>
        /// <param name="height">Block's height.</param>
        /// <param name="block">Block for which reward amount is checked.</param>
        /// <exception cref="ConsensusErrors.BadCoinbaseAmount">Thrown if coinbase transaction output value is larger than expected.</exception>
        private void CheckBlockReward(Money fees, int height, Block block)
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

        private Money GetProofOfWorkReward(int height)
        {
            int halvings = height / this.Parent.Network.Consensus.SubsidyHalvingInterval;
            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return 0;

            Money subsidy = this.ConsensusOptions.ProofOfWorkReward;
            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            subsidy >>= halvings;
            return subsidy;
        }
    }

    public class PosCheckVerifyScriptsResultsRule : CheckVerifyScriptsResultsRule
    {
        public override void Initialize()
        {
            this.ConsensusOptions = this.Parent.Network.Consensus.Option<PosConsensusOptions>();
        }
    }

    public class PowCheckVerifyScriptsResultsRule : CheckVerifyScriptsResultsRule
    {
        public override void Initialize()
        {
            this.ConsensusOptions = this.Parent.Network.Consensus.Option<PowConsensusOptions>();
        }
    }
}