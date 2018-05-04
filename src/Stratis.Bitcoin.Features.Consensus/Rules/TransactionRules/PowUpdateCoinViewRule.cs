using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public class PowUpdateCoinViewRule : TransactionConsensusRule
    {
        /// <summary>
        /// Updates context's UTXO set.
        /// Transaction outputs will be added to the context's <see cref="UnspentOutputSet"/> and which inputs will be removed from it.
        /// 
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        public override Task RunAsync(RuleContext context)
        {
            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            UnspentOutputSet view = context.Set;

            view.Update(this.Transaction, index.Height);

            return Task.CompletedTask;
        }
    }
}