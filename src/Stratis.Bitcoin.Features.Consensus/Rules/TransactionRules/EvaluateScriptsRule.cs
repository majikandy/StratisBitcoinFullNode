using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    [ExecutionRule]
    public class EvaluateScriptsRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            if (context.CurrentTransaction.IsCoinBase)
                return Task.CompletedTask;

            var txData = new PrecomputedTransactionData(context.CurrentTransaction);
            for (int inputIndex = 0; inputIndex < context.CurrentTransaction.Inputs.Count; inputIndex++)
            {
                this.Parent.PerformanceCounter.AddProcessedInputs(1);
                TxIn input = context.CurrentTransaction.Inputs[inputIndex];
                int inputIndexCopy = inputIndex;
                TxOut txout = context.Set.GetOutputFor(input);
                var checkInput = new Task<bool>(() =>
                {
                    var checker = new TransactionChecker(context.CurrentTransaction, inputIndexCopy, txout.Value, txData);
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