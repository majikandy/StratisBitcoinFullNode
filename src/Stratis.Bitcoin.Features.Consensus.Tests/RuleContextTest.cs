using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class RuleContextTest
    {
        [Fact]
        public void SetItem_WithEmptyContext_AddsNewItem()
        {
            var ruleContext = new RuleContext();
            ruleContext.SetItem("TestKey", "TestItem");
            ruleContext.Get<string>("TestKey").Should().Be("TestItem");
        }
         
        [Fact]
        public void SetItem_SameAsExistingKey_ReplacesItem()
        {
            var ruleContext = new RuleContext();
            ruleContext.SetItem("TestKey", "TestItem");
            ruleContext.SetItem("TestKey", "TestItem2");
            ruleContext.Get<string>("TestKey").Should().Be("TestItem2");
        }

        [Fact]
        public void SetItem_SameAsExistingKeyButDifferentType_ReplacesItem()
        {
            var ruleContext = new RuleContext();
            ruleContext.SetItem("TestKey", "TestItem");
            ruleContext.SetItem("TestKey", 1);
            ruleContext.Get<int>("TestKey").Should().Be(1);
        }

        [Fact]
        public void SetItem_ExistingKeyButDifferentType_ThrowsInvalidCastException()
        {
            var ruleContext = new RuleContext();
            ruleContext.SetItem("TestKey", "TestItem");
            Action action = () => ruleContext.Get<int>("TestKey");
            action.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public void Get_NonExistantKey_ThrowsKeyNotFoundException()
        {
            var ruleContext = new RuleContext();
            Action action = () => ruleContext.Get<int>("TestKey");
            action.Should().Throw<KeyNotFoundException>();
        }
    }
}
