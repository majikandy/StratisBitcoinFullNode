using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

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

        public const string TotalBlockFeesContextKey = "TotalBlockFees";

        private readonly IEnumerable<ConsensusRule> transactionConsensusRules;

        public TransactionRulesRunner(params ConsensusRule[] transactionConsensusRules)
        {
            this.transactionConsensusRules = transactionConsensusRules;
        }

        public override void Initialize()
        {
        }

        public override Task RunAsync(RuleContext context)
        {
            //TODO before PR merge - should this be at the higher level in the Consensus Rules Engine ?
            this.Parent.PerformanceCounter.AddProcessedBlocks(1);

            context.SetItem(CheckInputsContextKey, new List<Task<bool>>());
            context.SetItem(SigOpsCostContextKey, (long)0);
            context.SetItem(TotalBlockFeesContextKey, Money.Zero);

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
    }
}