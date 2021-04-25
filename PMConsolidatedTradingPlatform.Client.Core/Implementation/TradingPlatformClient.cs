using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using NetMQ;
using NetMQ.Sockets;
using PMCommonEntities.Models.TradingPlatform;
using PMConsolidatedTradingPlatform.Client.Core.Interfaces;
using PMConsolidatedTradingPlatform.Client.Core.Models;

namespace PMConsolidatedTradingPlatform.Client.Core.Implementation
{
    public class TradingPlatformClient : ITradingPlatformClient, IOrderDrainerClient
    {
        private RequestSocket _netMqConnection;
        private readonly string _netMqConnectionString;

        public TradingPlatformClient(string netMqConnectionString)
        {
            _netMqConnectionString = netMqConnectionString;
            _netMqConnection = new RequestSocket(_netMqConnectionString);
        }

        public void SendTradeRequest(ConsolidatedTradeRequest tradeRequest)
        {
            SendMessage(tradeRequest);
        }

        public ConsolidatedTradeResponse GetTradeResponse()
        {
            var response = GetMessage<ConsolidatedTradeResponse>();

            return response;
        }

        private T GetMessage<T>()
        {
            var incomingMessage = _netMqConnection.ReceiveFrameBytes();

            var deserializedMessage = MessagePackSerializer.Deserialize<T>(incomingMessage);

            return deserializedMessage;
        }

        private void SendMessage<T>(T message)
        {
            var outboundMessage = MessagePackSerializer.Serialize<T>(message);

            _netMqConnection.SendFrame(outboundMessage);
        }

        public void Disconnect()
        {
            _netMqConnection.Disconnect(_netMqConnectionString);
        }

        public void DrainOpenOrders(IEnumerable<int> orderIds, DateTime date)
        {
            OrderDrainerMessage drainerMessage = new OrderDrainerMessage()
            {
                Date = date,
                IsOrderCanceled = false,
                OrderIds = orderIds
            };

            SendMessage(drainerMessage);
        }

        public void CancelOpenOrders(IEnumerable<int> orderIds, DateTime date)
        {
            OrderDrainerMessage drainerMessage = new OrderDrainerMessage()
            {
                Date = date,
                IsOrderCanceled = true,
                OrderIds = orderIds
            };

            SendMessage(drainerMessage);
        }
    }
}
