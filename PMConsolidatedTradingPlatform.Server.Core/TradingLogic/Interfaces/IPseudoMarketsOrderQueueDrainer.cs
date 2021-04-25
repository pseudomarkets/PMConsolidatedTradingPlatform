using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingLogic.Interfaces
{
    public interface IPseudoMarketsOrderQueueDrainer
    {
        Task DrainOpenOrders(IEnumerable<int> orderIds, DateTime date);

        Task CancelOpenOrders(IEnumerable<int> orderIds, DateTime date);
    }
}
