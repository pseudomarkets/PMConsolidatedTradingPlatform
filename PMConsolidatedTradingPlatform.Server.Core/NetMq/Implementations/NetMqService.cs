using MessagePack;
using NetMQ;
using NetMQ.Sockets;
using PMConsolidatedTradingPlatform.Server.Core.NetMq.Interfaces;

namespace PMConsolidatedTradingPlatform.Server.Core.NetMq.Implementations
{
    public class NetMqService : INetMqService
    {
        private readonly ResponseSocket _netMqConnection;
        private readonly string _netMqConnectionString;
        
        public NetMqService(string netMqConnectionString)
        {
            _netMqConnectionString = netMqConnectionString;
            _netMqConnection = new ResponseSocket(_netMqConnectionString);
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

        public void Disconnect()
        {
            _netMqConnection.Disconnect(_netMqConnectionString);
        }

        public void Reconnect()
        {
            _netMqConnection.Connect(_netMqConnectionString);
        }
    }
}
