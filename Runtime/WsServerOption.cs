using System;
using System.Net.WebSockets;

namespace WsUnity
{
    public struct WsServerOption
    {
        public string Host;
        public int Port;
        public string Path;
        public int ReceiveBufferSize;
        public TimeSpan KeepAliveInterval;

        public static WsServerOption Default => new WsServerOption
        {
            Host = "localhost",
            Port = 8080,
            Path = "/",
            ReceiveBufferSize = 16385,
            KeepAliveInterval = WebSocket.DefaultKeepAliveInterval
        };
    }
}
