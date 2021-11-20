using System;
using System.Collections.Generic;
using System.Text;
using Aerospike.Client;
using PMCommonEntities.Models;
using PMCommonEntities.Models.PseudoMarkets;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.DbContext.Interfaces;
using PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.Repository.Interfaces;

namespace PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.Repository.Implementations
{
    public class ExtendedTransactionsRepository : IExtendedTransactionsRepository
    {
        private readonly IRealTimeDbContext _dbContext;

        public ExtendedTransactionsRepository(IRealTimeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ExtendedTransaction GetTransaction(string transactionId)
        {
            var record = _dbContext.Get(PseudoMarketsSharedNamespace.PseudoMarketsNamespace,
                PseudoMarketsSharedNamespace.SetExtendedTransactions.Set, transactionId);

            var extendedTran = new ExtendedTransaction();

            if (record != null)
            {
                extendedTran.TransactionId = transactionId;
                extendedTran.AccountId =
                    record.GetInt(PseudoMarketsSharedNamespace.SetExtendedTransactions.AccountIdBin);
                extendedTran.Symbol = record.GetString(PseudoMarketsSharedNamespace.SetExtendedTransactions.SymbolBin);
                extendedTran.TradeSide =
                    (RDSEnums.TradeSide)record.GetInt(PseudoMarketsSharedNamespace.SetExtendedTransactions
                        .OrderSideBin);
                extendedTran.Quantity =
                    record.GetDouble(PseudoMarketsSharedNamespace.SetExtendedTransactions.QuantityBin);
                extendedTran.ExecutionPrice =
                    record.GetDouble(PseudoMarketsSharedNamespace.SetExtendedTransactions.ExecutionPriceBin);
                extendedTran.SecurityType =
                    (RDSEnums.SecurityType)record.GetInt(PseudoMarketsSharedNamespace.SetExtendedTransactions
                        .SecurityTypeBin);
                extendedTran.OriginId = (RDSEnums.OriginId)record.GetInt("bOrigId");
                extendedTran.ExecutionTimestamp =
                    new DateTime(record.GetLong(PseudoMarketsSharedNamespace.SetExtendedTransactions
                        .ExecutionTimestampBin));
                extendedTran.ServiceUser =
                    record.GetString(PseudoMarketsSharedNamespace.SetExtendedTransactions.ServiceUserBin);
                extendedTran.CreditOrDebit =
                    record.GetString(PseudoMarketsSharedNamespace.SetExtendedTransactions.CreditOrDebitBin);
                extendedTran.TransactionType =
                    record.GetString(PseudoMarketsSharedNamespace.SetExtendedTransactions.TransactionTypeBin);
                extendedTran.TransactionDescription =
                    record.GetString(PseudoMarketsSharedNamespace.SetExtendedTransactions.TransactionDescriptionBin);
                extendedTran.AccountStartingBalance =
                    record.GetDouble(PseudoMarketsSharedNamespace.SetExtendedTransactions.AccountStartingBalanceBin);
                extendedTran.AccountEndingBalance =
                    record.GetDouble(PseudoMarketsSharedNamespace.SetExtendedTransactions.AccountEndingBalanceBin);

            }

            return extendedTran;
        }

        public void UpsertTransaction(ExtendedTransaction transaction)
        {
            var symbolBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.SymbolBin, transaction.Symbol);
            var tradeSideBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.OrderSideBin,
                (int)transaction.TradeSide);
            var quantityBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.QuantityBin,
                transaction.Quantity);
            var executionPriceBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.ExecutionPriceBin,
                transaction.ExecutionPrice);
            var secTypeBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.SecurityTypeBin,
                (int)transaction.SecurityType);
            var originIdBin = new Bin("bOrigId", (int)transaction.OriginId);
            var executionTsBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.ExecutionTimestampBin,
                transaction.ExecutionTimestamp.Ticks);
            var serviceUserBin =
                new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.ServiceUserBin,
                    transaction.ServiceUser);
            var accountIdBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.AccountIdBin,
                transaction.AccountId);
            var creditOrDebitBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.CreditOrDebitBin,
                transaction.CreditOrDebit);
            var tranTypeBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.TransactionTypeBin,
                transaction.TransactionType);
            var tranDescBin = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.TransactionDescriptionBin,
                transaction.TransactionDescription);
            var accountStartingBalanceBin =
                new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.AccountStartingBalanceBin,
                    transaction.AccountStartingBalance);
            var accountEndingBalanceBin =
                new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.AccountEndingBalanceBin,
                    transaction.AccountEndingBalance);
            var isTradingTran = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.IsTradingTransactionBin,
                transaction.IsTradingTransaction);
            var marketDataSource = new Bin(PseudoMarketsSharedNamespace.SetExtendedTransactions.MarketDataSourceBin,
                transaction.MarketDataSource);

            _dbContext.Insert(PseudoMarketsSharedNamespace.PseudoMarketsNamespace,
                PseudoMarketsSharedNamespace.SetExtendedTransactions.Set, transaction.TransactionId, symbolBin,
                tradeSideBin, quantityBin, executionPriceBin, secTypeBin, originIdBin, executionPriceBin,
                executionTsBin, serviceUserBin, accountIdBin, creditOrDebitBin, tranTypeBin, tranDescBin, accountStartingBalanceBin, accountEndingBalanceBin, isTradingTran, marketDataSource);
        }
    }
}
