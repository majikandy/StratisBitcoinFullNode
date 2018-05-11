using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.TransactionRules
{
    /// <summary>
    /// Checks that transaction's inputs are valid.
    /// </summary>
    /// <param name="transaction">Transaction to check.</param>
    /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
    /// <param name="spendHeight">Height at which we are spending coins.</param>
    /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction's inputs are missing.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionInputValueOutOfRange">Thrown if input value is out of range.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionInBelowOut">Thrown if transaction inputs are less then outputs.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionNegativeFee">Thrown if fees sum is negative.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionFeeOutOfRange">Thrown if fees value is out of range.</exception>
    public interface ICheckInputs
    {
        void CheckInputs(Transaction transaction, UnspentOutputSet inputs, int spendHeight);
    }
}