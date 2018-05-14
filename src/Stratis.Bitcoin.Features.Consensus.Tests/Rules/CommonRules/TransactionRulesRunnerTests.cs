using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class TransactionRulesRunnerTests : ConsensusRuleTestBase
    {
        private List<Transaction> transactions;
        private RuleContext ruleContext;
        private Mock<ConsensusRule> mockTransactionRule1;
        private Mock<ConsensusRule> mockTransactionRule2;
        private ConsensusRule rule;

        [Fact]
        public void RunAsync_WithTransactionLevelSubRulesAndBlockContains2Transactions_ExecutesSubRulesForEachTransaction()
        {
            this.GivenARuleContext();
            this.AndATransactionRuleRunnerWithTransactionSubRules();
            this.And2TransactionsInTheBlock();
            this.WhenExecutingTheRule(this.rule, this.ruleContext);
            this.ThenItExcutesTheRulesInsideForEachTransactionInTheBlock();
            this.AndTransactionsAddedInPerformanceCounter();

        }

        private void AndTransactionsAddedInPerformanceCounter()
        {
            this.rule.Parent.PerformanceCounter.ProcessedTransactions.Should().Be(2);
        }

        private void AndATransactionRuleRunnerWithTransactionSubRules()
        {
            this.mockTransactionRule1 = new Mock<ConsensusRule>();
            this.mockTransactionRule2 = new Mock<ConsensusRule>();
            this.rule = new TransactionRulesRunner(this.mockTransactionRule1.Object, this.mockTransactionRule2.Object);
        }

        private void ThenItExcutesTheRulesInsideForEachTransactionInTheBlock()
        {
            this.mockTransactionRule1.Verify(x => x.RunAsync(this.ruleContext), Times.Exactly(2));
            this.mockTransactionRule2.Verify(x => x.RunAsync(this.ruleContext), Times.Exactly(2));
        }

        private void GivenARuleContext()
        {
            this.ruleContext = new RuleContext { };
            this.ruleContext.BlockValidationContext = new BlockValidationContext();
            this.transactions = new List<Transaction>();
            this.ruleContext.BlockValidationContext.Block = new Block() { Transactions = this.transactions };
        }

        private void And2TransactionsInTheBlock()
        {
            this.transactions.Add(new Transaction { Inputs = { new TxIn() } });
            this.transactions.Add(new Transaction { Inputs = { new TxIn() } });
        }
    }
}