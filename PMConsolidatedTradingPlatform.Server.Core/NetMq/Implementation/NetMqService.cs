using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using NetMQ;
using NetMQ.Sockets;
using PMConsolidatedTradingPlatform.Server.Core.NetMq.Interfaces;

namespace PMConsolidatedTradingPlatform.Server.Core.NetMq.Implementation
{
    public class NetMqService : INetMqService
    {
        private readonly ResponseSocket _netMqConnection;

        public NetMqService(string netMqConnectionString)
        {
            _netMqConnection = new ResponseSocket(netMqConnectionString);
        }

        public T GetMessage<T>()
        {
            var incomingMessage = _netMqConnection.ReceiveFrameBytes();

            var deserializedMessage = MessagePackSerializer.Deserialize<T>(incomingMessage);

            return deserializedMessage;
        }

        public void SendMessage<T>(T message)
        {
            var outboundMessage = MessagePackSerializer.Serialize<T>(message);

            _netMqConnection.SendFrame(outboundMessage);
        }
    }
}
