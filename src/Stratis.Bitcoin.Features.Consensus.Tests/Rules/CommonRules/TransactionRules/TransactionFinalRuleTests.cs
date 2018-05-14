using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules.TransactionRules
{
    public class TransactionFinalRuleTests : ConsensusRuleTestBase
    {
        private const int HeightOfBlockchain = 1;
        private RuleContext ruleContext;
        private UnspentOutputSet coinView;
        private Transaction transactionWithCoinbaseFromPreviousBlock;
        private readonly TransactionFinalRule rule;

        public TransactionFinalRuleTests()
        {
            this.rule = new TransactionFinalRule();
        }

        [Fact]
        public void RunAsync_ValidatingATransactionThatIsNotCoinBaseButStillHasUnspentOutputsWithoutInput_ThrowsBadTransactionMissingInput()
        {
            this.GivenACoinbaseTransactionFromAPreviousBlock();
            this.AndARuleContext();
            this.AndSomeUnspentOutputs();
            this.AndATransactionWithNoUnspentOutputsAsInput();
            this.WhenExecutingTheRule(this.rule, this.ruleContext);
            this.ThenExceptionThrownIs(ConsensusErrors.BadTransactionMissingInput);
        }
          
        [Fact]
        public void RunAsync_ValidatingABlockHeightLowerThanBIP86Allows_ThrowsBadTransactionNonFinal()
        {
            this.GivenACoinbaseTransactionFromAPreviousBlock();
            this.AndARuleContext();
            this.AndSomeUnspentOutputs();
            this.AndATransactionBlockHeightLowerThanBip68Allows();
            this.WhenExecutingTheRule(this.rule, this.ruleContext);
            this.ThenExceptionThrownIs(ConsensusErrors.BadTransactionNonFinal);
        }

        //[Fact] 
        //TODO before PR complete - Similar test to the block height but considering the time not the height.
        public void RunAsync_AttemptingABlockEarlierThanBIP86Allows_ThrowsBadTransactionNonFinal()
        {
        }

        private void AndSomeUnspentOutputs()
        {
            this.coinView = new UnspentOutputSet();
            this.coinView.SetCoins(new UnspentOutputs[0]);
            this.ruleContext.Set = this.coinView;
            this.coinView.Update(this.transactionWithCoinbaseFromPreviousBlock, 0);
        }

        private void AndARuleContext()
        { 
            this.ruleContext = new RuleContext { };
            this.ruleContext.BlockValidationContext = new BlockValidationContext();
            this.ruleContext.BlockValidationContext.ChainedHeader = new ChainedHeader(new BlockHeader(), new uint256("bcd7d5de8d3bcc7b15e7c8e5fe77c0227cdfa6c682ca13dcf4910616f10fdd06"), HeightOfBlockchain);
            this.ruleContext.SetItem(TransactionRulesRunner.CheckInputsContextKey, new List<Task<bool>>());
            this.ruleContext.SetItem(TransactionRulesRunner.SigOpsCostContextKey, (long)0);
        }

        private void AndATransactionBlockHeightLowerThanBip68Allows()
        {
            var transaction = new Transaction
            {
                Inputs = { new TxIn()
                {
                    PrevOut = new OutPoint(this.transactionWithCoinbaseFromPreviousBlock, 0),
                    Sequence = HeightOfBlockchain + 1, //this sequence being higher triggers the ThrowsBadTransactionNonFinal
                } },
                Outputs = { new TxOut()},
                Version = 2, // So that sequence locks considered (BIP68)
            };

            this.ruleContext.Flags = new DeploymentFlags() { LockTimeFlags = Transaction.LockTimeFlags.VerifySequence };
            this.ruleContext.SetItem(TransactionRulesRunner.CurrentTransactionContextKey, transaction);
        }

        private void GivenACoinbaseTransactionFromAPreviousBlock()
        {
            this.transactionWithCoinbaseFromPreviousBlock = new Transaction();
            var txIn = new TxIn { PrevOut = new OutPoint() };
            this.transactionWithCoinbaseFromPreviousBlock.AddInput(txIn);
            this.transactionWithCoinbaseFromPreviousBlock.AddOutput(new TxOut());
        }

        private void AndATransactionWithNoUnspentOutputsAsInput()
        {
            this.ruleContext.SetItem(TransactionRulesRunner.CurrentTransactionContextKey, new Transaction { Inputs = { new TxIn() } });
        }
    }
}