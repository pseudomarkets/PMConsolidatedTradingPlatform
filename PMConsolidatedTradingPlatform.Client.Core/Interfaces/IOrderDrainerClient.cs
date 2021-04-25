using System;
using System.Collections.Generic;
using System.Text;

namespace PMConsolidatedTradingPlatform.Client.Core.Interfaces
{
    public interface IOrderDrainerClient
    {
        void DrainOpenOrders(IEnumerable<int> orderIds, DateTime date);

        void CancelOpenOrders(IEnumerable<int> orderIds, DateTime date);

    }
}
