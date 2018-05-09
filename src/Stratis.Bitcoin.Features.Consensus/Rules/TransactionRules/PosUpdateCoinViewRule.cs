﻿using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    /// <summary>
    /// Updates context's UTXO set.
    /// Transaction outputs will be added to the context's <see cref="UnspentOutputSet"/> and which inputs will be removed from it.
    /// </summary>
    public class PosUpdateCoinViewRule : TransactionConsensusRule
    {
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        public override Task RunAsync(RuleContext context)
        {
            context.Set.Update(this.Transaction, context.BlockValidationContext.ChainedHeader.Height);

            return Task.CompletedTask;
        }
    }
}