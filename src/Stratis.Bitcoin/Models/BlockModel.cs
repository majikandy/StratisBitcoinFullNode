﻿using System;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Models
{
    public class BlockModel
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("bits")]
        public string Bits { get; set; }

        [JsonProperty("time")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset Time { get; set; }

        [JsonProperty("tx")]
        public string[] Transactions { get; set; }

        [JsonProperty("difficulty")]
        public double Difficulty { get; set; }

        [JsonProperty("merkleroot")]
        public string MerkleRoot { get; set; }

        [JsonProperty("previousblockhash")]
        public string PreviousBlockHash { get; set; }

        [JsonProperty("nonce")]
        public uint Nonce { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        public BlockModel(Block block, ChainBase chain)
        {
            this.Hash = block.GetHash().ToString();
            this.Size = block.ToBytes().Length;
            this.Version = block.Header.Version;
            this.Bits = block.Header.Bits.ToCompact().ToString("x8");
            this.Time = block.Header.BlockTime;
            this.Nonce = block.Header.Nonce;
            this.PreviousBlockHash = block.Header.HashPrevBlock.ToString();
            this.MerkleRoot = block.Header.HashMerkleRoot.ToString();
            this.Difficulty = block.Header.Bits.Difficulty;
            this.Transactions = block.Transactions.Select(t => t.GetHash().ToString()).ToArray();
            this.Height = chain.GetBlock(block.GetHash()).Height;
        }

        /// <summary>
        /// Creates a block model
        /// Used for deserializing from Json
        /// </summary>
        public BlockModel()
        {
        }
    }
}
