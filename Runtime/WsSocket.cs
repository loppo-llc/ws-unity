using Cysharp.Threading.Tasks;
using System;
using System.Buffers;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace WsUnity
{
    public class WsSocket : IWsSocketCallback
    {
        internal readonly ArraySegment<byte> m_Buffer;
        internal readonly HttpListenerRequest m_Request;
        internal readonly WebSocket m_Socket;
        internal readonly CancellationToken m_Token;

        #region Public
        public event Action<WebSocketCloseStatus> OnClose;

        public event Action<Exception> OnError;

        public event Action<WsMessage> OnMessage;

        public int BufferSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Buffer.Count;
        }

        public IPAddress Address => m_Request.RemoteEndPoint.Address;

        public IWsSocketCallback Callback
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        public async UniTask CloseAsync(WebSocketCloseStatus status, string statusDesc = null)
        {
            if (m_Socket.State != WebSocketState.Open) return;

            await m_Socket.CloseAsync(status, statusDesc, m_Token).AsUniTask();
        }

        public async UniTask SendAsync(NativeArray<byte> data, WebSocketMessageType type = WebSocketMessageType.Binary)
        {
            if (!data.IsCreated || data.Length == 0)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (m_Socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException();
            }
            m_Token.ThrowIfCancellationRequested();

            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                var offset = 0;
                var remain = data.Length;
                while (remain > 0)
                {
                    var count = math.min(remain, BufferSize);
                    unsafe
                    {
                        var src = (byte*)data.GetUnsafeReadOnlyPtr();
                        var dst = UnsafeUtility.PinGCArrayAndGetDataAddress(buffer, out var handleDst);
                        try
                        {
                            UnsafeUtility.MemCpy(dst, src + offset, count);
                        }
                        finally
                        {
                            UnsafeUtility.ReleaseGCObject(handleDst);
                        }
                    }
                    remain -= count;
                    var segment = new ArraySegment<byte>(buffer, 0, count);
                    var eom = (remain == 0);
                    await m_Socket.SendAsync(segment, type, eom, m_Token).AsUniTask();
                    offset += count;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async UniTask SendAsync(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (m_Socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException();
            }
            m_Token.ThrowIfCancellationRequested();

            await SendAsyncInternal(data, WebSocketMessageType.Binary);
        }

        public async UniTask SendAsync(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (m_Socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException();
            }
            m_Token.ThrowIfCancellationRequested();

            var data = Encoding.UTF8.GetBytes(text);
            await SendAsyncInternal(data, WebSocketMessageType.Text);
        }
        #endregion

        #region Internal
        internal WsSocket(in HttpListenerRequest request, in WebSocket socket, in int bufferSize, in CancellationToken token)
        {
            m_Request = request;
            m_Socket = socket;
            m_Buffer = new ArraySegment<byte>(new byte[bufferSize]);
            m_Token = token;
        }

        internal async UniTask ReceiveAsync()
        {
            WsMessage? message = null;

            while (m_Socket.State == WebSocketState.Open)
            {
                try
                {
                    var ret = await m_Socket.ReceiveAsync(m_Buffer, m_Token).AsUniTask(false);
                    if (m_Token.IsCancellationRequested) return;

                    switch (ret.MessageType)
                    {
                        case WebSocketMessageType.Binary:
                        case WebSocketMessageType.Text:
                            if (!message.HasValue)
                            {
                                message = new WsMessage(ret.MessageType);
                            }
                            var msg = message.Value;
                            msg.Append(m_Buffer, ret.Count);
                            if (ret.EndOfMessage)
                            {
                                ((IWsSocketCallback)this).OnMessage(this, msg);
                                ((IDisposable)msg).Dispose();
                                message = null;
                            }
                            break;

                        case WebSocketMessageType.Close:
                            ((IWsSocketCallback)this).OnClose(this, ret.CloseStatus ?? WebSocketCloseStatus.Empty);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[{nameof(WsServer)}] ws client ({Address}) is abort.\n{e}\n");
                    ((IWsSocketCallback)this).OnError(this, e);
                    ((IWsSocketCallback)this).OnClose(this, m_Socket.CloseStatus ?? WebSocketCloseStatus.InternalServerError);
                }
            }

            if (message.HasValue)
            {
                ((IDisposable)message.Value).Dispose();
                message = null;
            }
        }
        #endregion

        #region Private
        async UniTask SendAsyncInternal(byte[] data, WebSocketMessageType type)
        {
            var offset = 0;
            var remain = data.Length;
            while (remain > 0)
            {
                var count = math.min(remain, BufferSize);
                var buffer = new ArraySegment<byte>(data, offset, count);
                remain -= count;
                var eom = (remain == 0);
                await m_Socket.SendAsync(buffer, type, eom, m_Token).AsUniTask(false);
                offset += count;
            }
        }
        #endregion

        #region IWsSocketCallback
        void IWsSocketCallback.OnClose(WsSocket socket, WebSocketCloseStatus status)
        {
            Callback?.OnClose(socket, status);
            OnClose?.Invoke(status);
        }

        void IWsSocketCallback.OnError(WsSocket socket, Exception exception)
        {
            Callback?.OnError(socket, exception);
            OnError?.Invoke(exception);
        }

        void IWsSocketCallback.OnMessage(WsSocket socket, WsMessage message)
        {
            Callback?.OnMessage(socket, message);
            OnMessage?.Invoke(message);
        }
        #endregion
    }
}
