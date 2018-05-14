using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    [ExecutionRule]
    public class EvaluateScriptsRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var transaction = context.Get<Transaction>(TransactionRulesRunner.CurrentTransactionContextKey);

            if (transaction.IsCoinBase)
                return Task.CompletedTask;

            var txData = new PrecomputedTransactionData(transaction);
            for (int inputIndex = 0; inputIndex < transaction.Inputs.Count; inputIndex++)
            {
                this.Parent.PerformanceCounter.AddProcessedInputs(1);
                TxIn input = transaction.Inputs[inputIndex];
                int inputIndexCopy = inputIndex;
                TxOut txout = context.Set.GetOutputFor(input);
                var checkInput = new Task<bool>(() =>
                {
                    var checker = new TransactionChecker(transaction, inputIndexCopy, txout.Value, txData);
                    var scriptEvaluationContext = new ScriptEvaluationContext(this.Parent.Network)
                    {
                        ScriptVerify = context.Flags.ScriptFlags
                    };
                    return scriptEvaluationContext.VerifyScript(input.ScriptSig, txout.ScriptPubKey, checker);
                });
                checkInput.Start(context.TaskScheduler);
                context.Get<List<Task<bool>>>(TransactionRulesRunner.CheckInputsContextKey).Add(checkInput);
            }



            return Task.CompletedTask;
        }
    }
}