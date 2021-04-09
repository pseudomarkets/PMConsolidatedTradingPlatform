using System;
using System.Collections.Generic;
using System.Text;
using PMConsolidatedTradingPlatform.Server.Core.Models;

namespace PMConsolidatedTradingPlatform.Server.Core.NetMq.Interfaces
{
    public interface INetMqService
    {
        T GetMessage<T>();
        void SendMessage<T>(T message);
        void Disconnect();
        void Reconnect();
    }
}
