using System;
using System.Net.WebSockets;

namespace WsUnity
{
    public interface IWsServerCallback
    {
        void OnConnect(WsServer server, WsSocket socket);
        void OnError(WsServer server, Exception exception);
    }
}
