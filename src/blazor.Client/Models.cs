using System;
using System.Collections.Generic;

namespace blazor.Client
{
    public class WeatherForecast
    {
        public DateTime Date { get; set; }
        public int TemperatureC { get; set; }
        public string Summary { get; set; }
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }

    public class WalletFileModel
    {
        public string WalletsPath { get; set; }

        public IEnumerable<string> WalletsFiles { get; set; }
    }

    public class WalletCreationRequest 
    {
        public string Mnemonic { get; set; }

        public string Password { get; set; }

        public string Network { get; set; }

        public string FolderPath { get; set; }

        public string Name { get; set; }
    }

    public class StatusModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StatusModel"/> class.
        /// </summary>
        public StatusModel()
        {
            this.InboundPeers = new List<ConnectedPeerModel>();
            this.OutboundPeers = new List<ConnectedPeerModel>();
            this.EnabledFeatures = new List<string>();
        }

        /// <summary>The node's user agent that will be shared with peers in the version handshake.</summary>
        public string Agent { get; set; }

        /// <summary>The node's version.</summary>
        public string Version { get; set; }

        /// <summary>The network the current node is running on.</summary>
        public string Network { get; set; }

        /// <summary>The coin ticker to use with external applications.</summary>
        public string CoinTicker { get; set; }

        /// <summary>System identifier of the node's process.</summary>
        public int ProcessId { get; set; }

        /// <summary>The height of the consensus.</summary>
        public int ConsensusHeight { get; set; }

        /// <summary>Height of the most recent block in persistent storage.</summary>
        /// <seealso cref="Stratis.Bitcoin.Features.BlockRepository.HighestPersistedBlock.Height"/>
        public int BlockStoreHeight { get; set; }

        /// <summary>A collection of inbound peers.</summary>
        public List<ConnectedPeerModel> InboundPeers { get; set; }

        /// <summary>A collection of outbound peers.</summary>
        public List<ConnectedPeerModel> OutboundPeers { get; set; }

        /// <summary>A collection of all the features enabled by this node.</summary>
        public List<string> EnabledFeatures { get; set; }

        /// <summary>The path to the directory where the data is saved.</summary>
        public string DataDirectoryPath { get; set; }

        /// <summary>Time this node has been running.</summary>
        public TimeSpan RunningTime { get; set; }

        /// <summary>The current network difficulty target.</summary>
        public double Difficulty { get; set; }

        /// <summary>The node's protocol version</summary>
        public uint ProtocolVersion { get; set; }

        /// <summary>Is the node on the testnet.</summary>
        public bool Testnet { get; set; }

        /// <summary>The current transaction relay fee.</summary>
        public decimal RelayFee { get; set; }
    }

    public class ConnectedPeerModel
    {
        /// <summary>A value indicating whether this peer is connected via an inbound or outbound connection.</summary>
        public bool IsInbound { get; set; }

        /// <summary>The version this peer is running.</summary>
        public string Version { get; set; }

        /// <summary>The endpoint where this peer is located.</summary>
        public string RemoteSocketEndpoint { get; set; }

        /// <summary>The height of this connected peer's tip.</summary>
        public int TipHeight { get; set; }
    }

    public class WalletBalanceModel
    {
        public WalletBalanceModel()
        {
            this.Balances = new List<AccountBalanceModel>();
        }

        public List<AccountBalanceModel> Balances { get; set; }
    }

    public class AccountBalanceModel
    {
        public string Name { get; set; }

        public string HdPath { get; set; }

        public int CoinType { get; set; }

        public long AmountConfirmed { get; set; }

        public long AmountUnconfirmed { get; set; }
    }

    public class WalletHistoryModel
    {
        public WalletHistoryModel()
        {
            this.History = new List<AccountHistoryModel>();
        }

        public ICollection<AccountHistoryModel> History { get; set; }
    }

    public class AccountHistoryModel
    {
        public AccountHistoryModel()
        {
            this.TransactionsHistory = new List<TransactionItemModel>();
        }

        public string AccountName { get; set; }

        public string HdPath { get; set; }

        public int CoinType { get; set; }

        public ICollection<TransactionItemModel> TransactionsHistory { get; set; }
    }

    public class TransactionItemModel
    {
        public TransactionItemModel()
        {
            this.Payments = new List<PaymentDetailModel>();
        }

        public string Type { get; set; }

        /// <summary>
        /// The Base58 representation of this address.
        /// </summary>
        public string ToAddress { get; set; }

        public string Id { get; set; }

        public long Amount { get; set; }

        /// <summary>
        /// A list of payments made out in this transaction.
        /// </summary>
        public ICollection<PaymentDetailModel> Payments { get; set; }

        public long Fee { get; set; }

        /// <summary>
        /// The height of the block in which this transaction was confirmed.
        /// </summary>
        public int? ConfirmedInBlock { get; set; }

        //public DateTimeOffset Timestamp { get; set; }
    }

    public class PaymentDetailModel
    {
        /// <summary>
        /// The Base58 representation of the destination  address.
        /// </summary>
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The transaction amount.
        /// </summary>
        public long Amount { get; set; }
    }

    public enum TransactionItemType
    {
        Received,
        Send,
        Staked
    }

}
