using System;
using System.Collections.Generic;
using System.Text;

namespace PMConsolidatedTradingPlatform.Server.Core.Helpers
{
    public static class MarketOpenCheckHelper
    {
        public static bool IsMarketOpen(bool isMarketHoliday)
        {
            // Regular market hours are between 9:30 AM and 4:00 PM EST, Monday through Friday
            if (DateTime.Now.ToUniversalTime() >=
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 8, 30, 0).ToUniversalTime() &&
                DateTime.Now.ToUniversalTime() <=
                new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 15, 0, 0).ToUniversalTime() &&
                (DateTime.Now.DayOfWeek != DayOfWeek.Saturday || DateTime.Now.DayOfWeek != DayOfWeek.Sunday) && !isMarketHoliday)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
