using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    /// <summary>
    /// Checks a transaction conforms to BIP68 Final (Time and Blockheight checks) - see https://github.com/bitcoin/bips/blob/master/bip-0068.mediawiki
    /// </summary>
    public class TransactionFinalRule : TransactionConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            if (this.Transaction.IsCoinBase)
                return Task.CompletedTask;

            ChainedBlock index = context.BlockValidationContext.ChainedBlock;
            DeploymentFlags flags = context.Flags;
            UnspentOutputSet view = context.Set;

            if (!view.HaveInputs(this.Transaction))
            {
                this.Logger.LogTrace("(-)[BAD_TX_NO_INPUT]");
                ConsensusErrors.BadTransactionMissingInput.Throw();
            }

            var prevheights = new int[this.Transaction.Inputs.Count];
            // Check that transaction is BIP68 final.
            // BIP68 lock checks (as opposed to nLockTime checks) must
            // be in ConnectBlock because they require the UTXO set.
            for (int j = 0; j < this.Transaction.Inputs.Count; j++)
            {
                prevheights[j] = (int)view.AccessCoins(this.Transaction.Inputs[j].PrevOut.Hash).Height;
            }

            if (!this.Transaction.CheckSequenceLocks(prevheights, index, flags.LockTimeFlags))
            {
                this.Logger.LogTrace("(-)[BAD_TX_NON_FINAL]");
                ConsensusErrors.BadTransactionNonFinal.Throw();
            }

            return Task.CompletedTask;
        }
    }
}