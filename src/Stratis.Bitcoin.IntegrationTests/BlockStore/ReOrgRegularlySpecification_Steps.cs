﻿using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Common;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReOrgRegularlySpecification
    {
        private NodeBuilder nodeBuilder;
        private CoreNode selfishMiner;
        private CoreNode secondNode;
        private CoreNode thirdNode;
        private CoreNode fourthNode;
        private SharedSteps sharedSteps;
        private Transaction secondNodeTransaction;
        private Money secondNodeTransationFee;
        private int selfishBlockHeight;
        private IDictionary<string, CoreNode> nodes;
        private const string AccountZero = "account 0";
        private const string WalletZero = "wallet 0";
        private const string WalletPassword = "123456";

        public ReOrgRegularlySpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.sharedSteps = new SharedSteps();
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void four_nodes()
        {
            this.nodeBuilder = NodeBuilder.Create();

            var nodeNetworkBuilder = new NodeNetworkBuilder();

            this.nodes = nodeNetworkBuilder
                .StratisPowNode("Selfish", true).WhichIsNotInInitialBlockDownload()
                .WithWallet(WalletZero, WalletPassword)
                .ConnectedTo()
                .StratisPowNode("B", true).WhichIsNotInInitialBlockDownload()
                .WithWallet(WalletZero, WalletPassword)
                .ConnectedTo()
                .StratisPowNode("C", true).WhichIsNotInInitialBlockDownload()
                .WithWallet(WalletZero, WalletPassword)
                .StratisPowNode("D", true).WhichIsNotInInitialBlockDownload()
                .WithWallet(WalletZero, WalletPassword)
                .Build();

            this.selfishMiner = this.nodes.Values.First();
            this.secondNode = this.nodes.Values.Skip(1).First();
            this.thirdNode = this.nodes.Values.Skip(2).First();
            this.fourthNode = this.nodes.Values.Skip(3).First();
            //this.selfishMiner = this.nodeBuilder.CreateStratisPowNode();
            //this.secondNode = this.nodeBuilder.CreateStratisPowNode();
            //this.thirdNode = this.nodeBuilder.CreateStratisPowNode();
            //this.fourthNode = this.nodeBuilder.CreateStratisPowNode();

            //this.nodeBuilder.StartAll();

            //this.selfishMiner.NotInIBD();
            //this.secondNode.NotInIBD();
            //this.thirdNode.NotInIBD();
            //this.fourthNode.NotInIBD();

            //this.selfishMiner.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);
            //this.secondNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);
            //this.thirdNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);
            //this.fourthNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletZero);

            //this.selfishMiner.CreateRPCClient().AddNode(this.secondNode.Endpoint, true);
            //TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.secondNode, this.selfishMiner));

            //this.secondNode.CreateRPCClient().AddNode(this.thirdNode.Endpoint, true);
            //TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.thirdNode, this.secondNode));

            //this.thirdNode.CreateRPCClient().AddNode(this.fourthNode.Endpoint, true);
            //TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(this.fourthNode, this.thirdNode));
        }

        private void each_mine_a_block()
        {
            this.sharedSteps.MineBlocks(1, this.selfishMiner, AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.secondNode, AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.thirdNode, AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.MineBlocks(1, this.fourthNode, AccountZero, WalletZero, WalletPassword);
        }

        private void selfish_miner_disconnects_and_mines_10_blocks()
        {
            this.selfishMiner.FullNode.ConnectionManager.RemoveNodeAddress(this.secondNode.Endpoint);
            this.selfishMiner.FullNode.ConnectionManager.RemoveNodeAddress(this.thirdNode.Endpoint);
            this.selfishMiner.FullNode.ConnectionManager.RemoveNodeAddress(this.fourthNode.Endpoint);
            TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(this.selfishMiner));

            this.sharedSteps.MineBlocks(10, this.selfishMiner, AccountZero, WalletZero, WalletPassword);

            this.selfishBlockHeight = this.selfishMiner.FullNode.Chain.Height;
        }

        private void second_node_creates_a_transaction_and_broadcasts()
        {
            var thirdNodeReceivingAddress = this.GetSecondUnusedAddressToAvoidClashWithMiningAddress(this.thirdNode);

            var transactionBuildContext = SharedSteps.CreateTransactionBuildContext(WalletZero, AccountZero, WalletPassword, thirdNodeReceivingAddress.ScriptPubKey, Money.COIN * 1, FeeType.Medium, minConfirmations: 1);

            this.secondNodeTransaction = this.secondNode.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
            this.secondNodeTransationFee = this.secondNode.FullNode.WalletTransactionHandler().EstimateFee(transactionBuildContext);

            this.secondNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.secondNodeTransaction.ToHex()));
        }

        private HdAddress GetSecondUnusedAddressToAvoidClashWithMiningAddress(CoreNode node)
        {
            var thirdNodeReceivingAddress = node.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletZero, AccountZero), 2)
                .Skip(1).First();
            return thirdNodeReceivingAddress;
        }

        private void third_node_mines_this_block()
        {
            this.sharedSteps.MineBlocks(1, this.thirdNode, AccountZero, WalletZero, WalletPassword, this.secondNodeTransationFee.Satoshi);
        }

        private void fouth_node_confirms_it_ensures_tx_present()
        {
            this.sharedSteps.WaitForBlockStoreToSync(this.secondNode, this.thirdNode, this.fourthNode);

            var transaction = this.fourthNode.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.secondNodeTransaction.GetHash()).Result;
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.secondNodeTransaction.GetHash());
        }

        private void selfish_node_reconnects_and_broadcasts()
        {
            this.selfishMiner.CreateRPCClient().AddNode(this.secondNode.Endpoint);
            this.sharedSteps.WaitForBlockStoreToSync(this.selfishMiner, this.secondNode, this.thirdNode, this.fourthNode);
        }

        private void second_third_and_fourth_node_reorg_to_longest_chain()
        {
            TestHelper.WaitLoop(() => this.secondNode.FullNode.Chain.Height == this.selfishBlockHeight);
            this.secondNode.FullNode.Chain.Height.Should().Be(this.selfishBlockHeight);

            TestHelper.WaitLoop(() => this.thirdNode.FullNode.Chain.Height == this.selfishBlockHeight);
            TestHelper.WaitLoop(() => this.fourthNode.FullNode.Chain.Height == this.selfishBlockHeight);
        }

        private void transaction_from_shorter_chain_is_missing()
        {
            this.secondNode.FullNode.BlockStoreManager().BlockRepository.GetTrxAsync(this.secondNodeTransaction.GetHash()).Result
                .Should().BeNull("longest chain comes from selfish miner and shouldn't contain the transaction made on the chain with the other 3 nodes.");
        }

        private void transaction_is_NOT_YET_returned_to_the_mem_pool()
        {
            this.fourthNode.CreateRPCClient().GetRawMempool()
                .Should().NotContain(x => x == this.secondNodeTransaction.GetHash(), "it is not implemented yet.");
        }

        private void mining_continues_to_maturity_to_allow_spend()
        {
            var coinbaseMaturity = (int)this.secondNode.FullNode
                .Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;

            this.sharedSteps.MineBlocks(coinbaseMaturity, this.secondNode, AccountZero, WalletZero, WalletPassword);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.selfishMiner));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.secondNode));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.thirdNode));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.fourthNode));
        }
    }

    internal class NodeNetworkBuilder
    {
        private readonly NodeBuilder nodeBuilder;
        private Dictionary<string, CoreNode> nodes;
        private bool sync;

        public NodeNetworkBuilder()
        {
            this.nodeBuilder = NodeBuilder.Create();
            this.nodes = new Dictionary<string, CoreNode>();
        }

        public IDictionary<string, CoreNode> Build()
        {
            CoreNode previousNode = null;
            foreach (var nextNode in this.nodes.Values)
            {
                nextNode.Start();
                if (previousNode == null)
                {
                    previousNode = nextNode;
                    continue;
                }

                previousNode.CreateRPCClient().AddNode(nextNode.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(previousNode, nextNode));
            }

            return this.nodes;
        }

        public NodeNetworkBuilder StratisPowNode(string nodeName, bool started)
        {
            this.nodes.Add(nodeName, this.nodeBuilder.CreateStratisPowNode(started));
            
            return this;
        }

        public NodeNetworkBuilder WhichIsNotInInitialBlockDownload()
        {
            this.nodes.Last().Value.NotInIBD();
            return this;
        }

        public NodeNetworkBuilder WithWallet(string walletName, string walletPassword)
        {
            this.nodes.Last().Value.FullNode.WalletManager().CreateWallet(walletPassword, walletName);
            return this;
        }

        public NodeNetworkBuilder ConnectedTo()
        {
            return this;
        }
    }
}
