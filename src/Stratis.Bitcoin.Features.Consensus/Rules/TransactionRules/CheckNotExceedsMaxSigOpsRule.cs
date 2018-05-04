using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public class CheckNotExceedsMaxSigOpsRule : TransactionConsensusRule
    {
        public PowConsensusOptions ConsensusOptions { get; set; }

        public override void Initialize()
        {
            this.ConsensusOptions = this.Parent.Network.Consensus.Option<PowConsensusOptions>();
        }

        public override Task RunAsync(RuleContext context)
        {
            // GetTransactionSignatureOperationCost counts 3 types of sigops:
            // * legacy (always),
            // * p2sh (when P2SH enabled in flags and excludes coinbase),
            // * witness (when witness enabled in flags and excludes coinbase).
            context.SigOpsCost += this.GetTransactionSignatureOperationCost(this.Transaction, context.Set, context.Flags);
            if (context.SigOpsCost > this.ConsensusOptions.MaxBlockSigopsCost)
                ConsensusErrors.BadBlockSigOps.Throw();

            return Task.CompletedTask;
        }

        private long GetTransactionSignatureOperationCost(Transaction transaction, UnspentOutputSet inputs, DeploymentFlags flags)
        {
            long signatureOperationCost = this.GetLegacySignatureOperationsCount(transaction) * this.ConsensusOptions.WitnessScaleFactor;

            if (transaction.IsCoinBase)
                return signatureOperationCost;

            if (flags.ScriptFlags.HasFlag(ScriptVerify.P2SH))
            {
                signatureOperationCost += this.GetP2SHSignatureOperationsCount(transaction, inputs) * this.ConsensusOptions.WitnessScaleFactor;
            }

            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                signatureOperationCost += this.CountWitnessSignatureOperation(transaction.Inputs[i].ScriptSig, prevout.ScriptPubKey, transaction.Inputs[i].WitScript, flags);
            }

            return signatureOperationCost;
        }


        /// <summary>
        /// Calculates legacy transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <returns>Legacy signature operation cost for transaction.</returns>
        private long GetLegacySignatureOperationsCount(Transaction transaction)
        {
            long sigOps = 0;
            foreach (TxIn txin in transaction.Inputs)
                sigOps += txin.ScriptSig.GetSigOpCount(false);

            foreach (TxOut txout in transaction.Outputs)
                sigOps += txout.ScriptPubKey.GetSigOpCount(false);

            return sigOps;
        }

        /// <summary>
        /// Calculates pay-to-script-hash (BIP16) transaction signature operation cost.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <returns>Signature operation cost for transaction.</returns>
        private uint GetP2SHSignatureOperationsCount(Transaction transaction, UnspentOutputSet inputs)
        {
            if (transaction.IsCoinBase)
                return 0;

            uint sigOps = 0;
            for (int i = 0; i < transaction.Inputs.Count; i++)
            {
                TxOut prevout = inputs.GetOutputFor(transaction.Inputs[i]);
                if (prevout.ScriptPubKey.IsPayToScriptHash(this.Parent.Network))
                    sigOps += prevout.ScriptPubKey.GetSigOpCount(this.Parent.Network, transaction.Inputs[i].ScriptSig);
            }

            return sigOps;
        }

        /// <summary>
        /// Calculates signature operation cost for single transaction input.
        /// </summary>
        /// <param name="scriptSig">Signature script.</param>
        /// <param name="scriptPubKey">Script public key.</param>
        /// <param name="witness">Witness script.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for single transaction input.</returns>
        private long CountWitnessSignatureOperation(Script scriptSig, Script scriptPubKey, WitScript witness, DeploymentFlags flags)
        {
            witness = witness ?? WitScript.Empty;
            if (!flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                return 0;

            WitProgramParameters witParams = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(this.Parent.Network, scriptPubKey);

            if (witParams?.Version == 0)
            {
                if (witParams.Program.Length == 20)
                    return 1;

                if (witParams.Program.Length == 32 && witness.PushCount > 0)
                {
                    Script subscript = Script.FromBytesUnsafe(witness.GetUnsafePush(witness.PushCount - 1));
                    return subscript.GetSigOpCount(true);
                }
            }

            return 0;
        }


    }
}