using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    public class BuildCheckInputsRule : TransactionConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            if (this.Transaction.IsCoinBase)
                return Task.CompletedTask;

            var txData = new PrecomputedTransactionData(this.Transaction);
            for (int inputIndex = 0; inputIndex < this.Transaction.Inputs.Count; inputIndex++)
            {
                this.Parent.PerformanceCounter.AddProcessedInputs(1);
                TxIn input = this.Transaction.Inputs[inputIndex];
                int inputIndexCopy = inputIndex;
                TxOut txout = context.Set.GetOutputFor(input);
                var checkInput = new Task<bool>(() =>
                {
                    var checker = new TransactionChecker(this.Transaction, inputIndexCopy, txout.Value, txData);
                    var scriptEvaluationContext = new ScriptEvaluationContext(this.Parent.Network)
                    {
                        ScriptVerify = context.Flags.ScriptFlags
                    };
                    return scriptEvaluationContext.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                });
                checkInput.Start(context.TaskScheduler);
                context.CheckInputs.Add(checkInput);
            }

            return Task.CompletedTask;
        }
    }
}