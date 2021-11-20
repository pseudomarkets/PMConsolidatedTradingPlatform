using System;
using System.Collections.Generic;
using System.Text;
using PMCommonEntities.Models.PseudoMarkets;

namespace PMConsolidatedTradingPlatform.Server.Core.RealTimeDataStore.Repository.Interfaces
{
    public interface IExtendedTransactionsRepository
    {
        ExtendedTransaction GetTransaction(string transactionId);

        void UpsertTransaction(ExtendedTransaction transaction);
    }
}
