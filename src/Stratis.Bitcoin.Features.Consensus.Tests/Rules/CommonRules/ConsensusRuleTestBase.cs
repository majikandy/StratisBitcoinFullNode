using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class ConsensusRuleTestBase
    {
        protected Exception CaughtExecption;

        protected void WhenExecutingTheRule(ConsensusRule rule, RuleContext ruleContext)
        {
            try
            {
                rule.Logger = new Mock<ILogger>().Object;
                rule.Parent = new PowConsensusRules(
                    Network.RegTest, 
                    new Mock<ILoggerFactory>().Object, 
                    new Mock<IDateTimeProvider>().Object, 
                    new ConcurrentChain(), 
                    new NodeDeployments(Network.RegTest, new ConcurrentChain()), 
                    new ConsensusSettings(), new Mock<ICheckpoints>().Object, new Mock<CoinView>().Object, null);

                rule.Initialize();

                rule.Parent.PerformanceCounter.ProcessedTransactions.Should().Be(0);

                rule.RunAsync(ruleContext).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                this.CaughtExecption = e;
            }
        }

        protected void ThenExceptionThrownIs(ConsensusError consensusErrorType)
        {
            this.CaughtExecption.Should().NotBeNull();
            this.CaughtExecption.Should().BeOfType<ConsensusErrorException>();
            var consensusErrorException = (ConsensusErrorException)this.CaughtExecption;
            consensusErrorException.ConsensusError.Should().Be(consensusErrorType);
        }
    }
}