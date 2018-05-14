using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public class AddCalculatedFeesRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var transaction = context.Get<Transaction>(TransactionRulesRunner.CurrentTransactionContextKey);

            //TODO before Merge - do we need to consider the IsCoinStake here? The original code didn't
            if (transaction.IsCoinBase)
                return Task.CompletedTask;

            var blockFeesRunningTotal = context.Get<Money>(TransactionRulesRunner.TotalBlockFeesContextKey) + (context.Set.GetValueIn(transaction) - transaction.TotalOut);

            context.SetItem(TransactionRulesRunner.TotalBlockFeesContextKey, blockFeesRunningTotal);

            return Task.CompletedTask;
        }
    }
}