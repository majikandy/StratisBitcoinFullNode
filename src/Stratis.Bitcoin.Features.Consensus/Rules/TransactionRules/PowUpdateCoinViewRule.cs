using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    /// <summary>
    /// Updates context's UTXO set.
    /// Transaction outputs will be added to the context's <see cref="UnspentOutputSet"/> and which inputs will be removed from it.
    /// </summary>
    public class PowUpdateCoinViewRule : ConsensusRule
    {
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        public override Task RunAsync(RuleContext context)
        {
            var transaction = context.Get<Transaction>(TransactionRulesRunner.CurrentTransactionContextKey);

            context.Set.Update(transaction, context.BlockValidationContext.ChainedHeader.Height);

            return Task.CompletedTask;
        }
    }
}