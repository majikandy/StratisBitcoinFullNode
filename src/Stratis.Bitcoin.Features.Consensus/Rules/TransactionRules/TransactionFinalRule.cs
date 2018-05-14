using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    /// <summary>
    /// Checks a transaction conforms to BIP68 Final (Time and Blockheight checks) - <see cref="https://github.com/bitcoin/bips/blob/master/bip-0068.mediawiki"/>
    /// </summary>
    public class TransactionFinalRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var transaction = context.Get<Transaction>(TransactionRulesRunner.CurrentTransactionContextKey);

            if (transaction.IsCoinBase || transaction.IsCoinStake)
                return Task.CompletedTask;

            ChainedHeader index = context.BlockValidationContext.ChainedHeader;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            if (!view.HaveInputs(transaction))
            {
                this.Logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                ConsensusErrors.BadTransactionMissingInput.Throw();
            }

            var prevheights = new int[transaction.Inputs.Count];
            
            // Check that transaction is BIP68 final.
            // BIP68 lock checks (as opposed to nLockTime checks) must
            // be in ConnectBlock because they require the UTXO set.
            for (int j = 0; j < transaction.Inputs.Count; j++)
            {
                prevheights[j] = (int) view.AccessCoins(transaction.Inputs[j].PrevOut.Hash).Height;
            }

            if (!transaction.CheckSequenceLocks(prevheights, index, flags.LockTimeFlags))
            {
                this.Logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
                ConsensusErrors.BadTransactionNonFinal.Throw();
            }

            return Task.CompletedTask;
        }
    }
}