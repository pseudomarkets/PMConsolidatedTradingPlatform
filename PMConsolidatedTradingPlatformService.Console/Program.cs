using System;
using PMCommonEntities.Models.TradingPlatform;
using PMConsolidatedTradingPlatform.Client.Core.Implementation;

namespace PMConsolidatedTradingPlatformService.Console
{
    public class Program
    {
        public static void Main(string[] args)
        {
            System.Console.WriteLine("Pseudo Markets Consolidated Trading Service - Console");
            System.Console.Write("Enter Pseudo Markets MQ Connection String: ");
            var mqConnectionString = System.Console.ReadLine();

            TradingPlatformClient tradingClient = new TradingPlatformClient(mqConnectionString);

            System.Console.WriteLine("Connected to Trading Platform");

            System.Console.WriteLine("1. Send order");
            System.Console.Write("Enter selection: ");

            var option = System.Console.ReadLine();

            switch (option)
            {

                case "1":
                    SendTradeRequestMenu(tradingClient);
                    break;
                default:
                    break;
            }
        }

        private static void SendTradeRequestMenu(TradingPlatformClient client)
        {
            System.Console.Write("Enter symbol: ");
            var symbol = System.Console.ReadLine();

            System.Console.Write("Enter quantity: ");
            var quantity = System.Console.ReadLine();

            System.Console.Write("Enter order action (BUY/SELL/SELLSHORT): ");
            var action = System.Console.ReadLine();

            System.Console.Write("Enter Account ID: ");
            var accountId = System.Console.ReadLine();

            ConsolidatedTradeRequest tradeRequest = new ConsolidatedTradeRequest()
            {
                AccountId = int.Parse(accountId),
                OrderAction = action,
                OrderOrigin = ConsolidatedTradeEnums.ConsolidatedOrderOrigin.PseudoMarkets,
                OrderTiming = ConsolidatedTradeEnums.ConsolidatedOrderTiming.DayOnly,
                OrderType = ConsolidatedTradeEnums.ConsolidatedOrderType.Market,
                Quantity = int.Parse(quantity),
                Symbol = symbol
            };

            client.SendTradeRequest(tradeRequest);

            var response = client.GetTradeResponse();

            System.Console.WriteLine("Received trade response");
            System.Console.WriteLine("Status Code: " + response?.StatusCode);
            System.Console.WriteLine("Status Message: " + response?.StatusMessage);

            System.Console.ReadKey();
        }
    }
}
