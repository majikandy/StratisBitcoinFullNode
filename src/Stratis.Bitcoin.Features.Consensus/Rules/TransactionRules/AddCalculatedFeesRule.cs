using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public class AddCalculatedFeesRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            //TODO before Merge - do we need to consider the IsCoinStake here?  I believe the original code didn't
            if (context.CurrentTransaction.IsCoinBase)
                return Task.CompletedTask;

            context.TotalBlockFees += context.Set.GetValueIn(context.CurrentTransaction) - context.CurrentTransaction.TotalOut;

            return Task.CompletedTask;
        }
    }
}