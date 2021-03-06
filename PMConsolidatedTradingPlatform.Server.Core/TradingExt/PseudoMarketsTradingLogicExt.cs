using PMUnifiedAPI.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.TradingExt
{
    public static class PseudoMarketsTradingLogicExt
    {
        public static bool IsPositionLong(this Positions position)
        {
            return position.Quantity > 0;
        }

        public static bool IsPositionShort(this Positions position)
        {
            return position.Quantity < 0;
        }

        public static bool IsLiquidatingPosition(this Positions existingPosition, int orderQuantity)
        {
            return existingPosition.Quantity - orderQuantity == 0;
        }
        
        public static double CalculateOrderMarketValue(this Orders order)
        {
            return order.Quantity * order.Price;
        }
    }
}
