using System;
using System.Net.WebSockets;

namespace WsUnity
{
    public interface IWsSocketCallback
    {
        void OnClose(WsSocket socket, WebSocketCloseStatus status);

        void OnError(WsSocket socket, Exception exception);

        void OnMessage(WsSocket socket, WsMessage message);
    }
}
