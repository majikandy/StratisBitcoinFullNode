using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public class AddCalculatedFeesRule : TransactionConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            //TODO before Merge - do we need to consider the IsCoinStake here?  I believe the original code didn't
            if (this.Transaction.IsCoinBase)
                return Task.CompletedTask;

            context.TotalBlockFees += context.Set.GetValueIn(this.Transaction) - this.Transaction.TotalOut;

            return Task.CompletedTask;
        }
    }
}