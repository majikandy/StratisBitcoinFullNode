using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public class AddCalculatedFeesRule : TransactionConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            if (this.Transaction.IsCoinBase)
                return Task.CompletedTask;

            context.Fees += context.Set.GetValueIn(this.Transaction) - this.Transaction.TotalOut;

            return Task.CompletedTask;
        }
    }
}