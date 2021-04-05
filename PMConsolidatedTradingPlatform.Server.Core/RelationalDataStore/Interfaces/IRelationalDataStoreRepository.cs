﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PMCommonApiModels.RequestModels;
using PMCommonEntities.Models;
using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.RelationalDataStore.Interfaces
{
    public interface IRelationalDataStoreRepository
    {
        Task CreateAndSaveOrder(string symbol, string type, double price, int quantity, DateTime date,
            string transactionId, RDSEnums.EnvironmentId environmentId, RDSEnums.OriginId originId,
            RDSEnums.SecurityType securityType);
        Task SaveTransaction(Transactions transaction);
        Task SavePosition(Positions position, Accounts account);
        Task UpdatePosition(Positions existingPosition, double newValue, int newQuantity, Accounts account);
        Task CreateQueuedOrder(TradeExecInput tradeInput, Accounts account);
        Task<Accounts> GetAccountUsingId(int accountId);
    }
}