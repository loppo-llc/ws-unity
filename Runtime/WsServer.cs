using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
#if ENABLE_UNITASK
using Cysharp.Threading.Tasks;
#endif // ENABLE_UNITASK

namespace WsUnity
{
    public class WsServer : IWsServerCallback, IDisposable
    {
#pragma warning disable IDE0032
        readonly CancellationTokenSource m_CTS;
        readonly SemaphoreSlim m_Lock;
        readonly WsServerOption m_Options;
        readonly List<WsSocket> m_SocketList;
#pragma warning restore IDE0032

        bool m_IsDisposed;

        #region Public
        public WsServer(in WsServerOption options, IWsServerCallback callback = null)
        {
            m_SocketList = new List<WsSocket>();
            m_CTS = new CancellationTokenSource();
            m_Lock = new SemaphoreSlim(1, 1);

            m_Options = options;
            Callback = callback;
        }

        public event Action<WsSocket> OnConnect;

        public event Action<Exception> OnError;

        public IWsServerCallback Callback
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        public WsServerOption Options
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Options;
        }

        public async UniTask CloseAsync()
        {
            await CloseAsyncInternal();
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            CloseAsyncInternal().Forget();
            GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start() => ListenAsync().Forget();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Stop() => CloseAsync().Forget();

#if ENABLE_UNITASK
        public async UniTaskVoid ListenAsync()
        {
            var token = m_CTS.Token;
            if (token.IsCancellationRequested) return;

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{m_Options.Host}:{m_Options.Port}{m_Options.Path}");
            listener.Start();

            await UniTask.SwitchToThreadPool();
            if (token.IsCancellationRequested) return;

            try
            {
                while (true)
                {
                    var ctx = await listener.GetContextAsync()
                        .AsUniTask(false)
                        .WithCancellation(token);

                    if (token.IsCancellationRequested)
                    {
                        listener.Abort();
                        listener = null;
                        break;
                    }

                    if (ctx.Request.IsWebSocketRequest)
                    {
                        ReceiveAsync(ctx, token).Forget();
                    }
                    else
                    {
                        var res = ctx.Response;
                        res.StatusCode = (int)HttpStatusCode.BadRequest;
                        res.Close();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                ((IWsServerCallback)this).OnError(this, e);
            }
            finally
            {
                listener?.Abort();
            }
        }
#else
        public async void ListenAsync()
        {
            throw new NotImplementedException();
        }
#endif // ENABLE_UNITASK
        #endregion

        #region Internal
        ~WsServer()
        {
            CloseAsyncInternal().Forget();
        }

#if ENABLE_UNITASK
        async UniTask CloseAsyncInternal()
        {
            if (m_IsDisposed || m_CTS.IsCancellationRequested) return;
            m_IsDisposed = true;

            await m_Lock.WaitAsync();
            try
            {
                if (m_CTS.IsCancellationRequested) return;

                foreach (var socket in m_SocketList)
                {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure).Forget();
                }
                m_SocketList.Clear();
                m_CTS.Cancel();
            }
            finally
            {
                m_Lock.Release();
                m_Lock.Dispose();
            }
        }

        async UniTaskVoid ReceiveAsync(HttpListenerContext ctx, CancellationToken token)
        {
            var wsctx = await ctx.AcceptWebSocketAsync(null, m_Options.ReceiveBufferSize, m_Options.KeepAliveInterval).AsUniTask(false).WithCancellation(token);
            if (token.IsCancellationRequested) return;

            using (var ws = wsctx.WebSocket)
            {
                var socket = new WsSocket(ctx.Request, ws, m_Options.ReceiveBufferSize, token);

                await m_Lock.WaitAsync();
                try
                {
                    if (token.IsCancellationRequested) return;
                    m_SocketList.Add(socket);
                }
                finally
                {
                    m_Lock.Release();
                }

                ((IWsServerCallback)this).OnConnect(this, socket);

                await socket.ReceiveAsync();

                if (token.IsCancellationRequested) return;

                await m_Lock.WaitAsync();
                try
                {
                    if (token.IsCancellationRequested) return;
                    m_SocketList.Remove(socket);
                }
                finally
                {
                    m_Lock.Release();
                }
            }
        }
#else
        async void ReceiveAsync(HttpListenerContext ctx, CancellationToken token)
        {
            throw new NotImplementedException();
        }
#endif // ENABLE_UNITASK
        #endregion

        #region IWsServerCallback
        void IWsServerCallback.OnConnect(WsServer server, WsSocket socket)
        {
            Callback?.OnConnect(server, socket);
            OnConnect?.Invoke(socket);
        }

        void IWsServerCallback.OnError(WsServer server, Exception e)
        {
            Callback?.OnError(server, e);
            OnError?.Invoke(e);
        }
        #endregion
    }
}
