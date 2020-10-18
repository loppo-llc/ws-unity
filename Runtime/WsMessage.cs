using System;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace WsUnity
{
    public readonly struct WsMessage : IDisposable
    {
        readonly NativeList<byte> m_Data;
#pragma warning disable IDE0032
        readonly WebSocketMessageType m_Type;
#pragma warning restore IDE0032

        #region Public
        public byte[] Binary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (m_Type != WebSocketMessageType.Binary) throw new InvalidOperationException();
                TryGetBinary(out byte[] ret);
                return ret;
            }
        }

        public string Text
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (m_Type != WebSocketMessageType.Text) throw new InvalidOperationException();
                TryGetText(out var ret);
                return ret;
            }
        }

        public WebSocketMessageType Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Type;
        }

        public bool TryGetBinary(out byte[] data)
        {
            if (m_Type == WebSocketMessageType.Binary && m_Data.IsCreated)
            {
                data = m_Data.ToArray();
                return (data != null);
            }
            data = null;
            return false;
        }

        public bool TryGetBinary(out NativeArray<byte> data, in Allocator alloc)
        {
            if (m_Type == WebSocketMessageType.Binary && m_Data.IsCreated)
            {
                data = m_Data.ToArray(alloc);
                return true;
            }
            data = default;
            return false;
        }

        public bool TryGetText(out string text)
        {
            if (m_Type == WebSocketMessageType.Text && m_Data.IsCreated && m_Data.Length != 0)
            {
                unsafe
                {
                    text = Encoding.UTF8.GetString((byte*)m_Data.GetUnsafeReadOnlyPtr(), m_Data.Length);
                }
                return !string.IsNullOrEmpty(text);
            }
            text = null;
            return false;
        }
        #endregion

        #region Internal
        internal WsMessage(in WebSocketMessageType type)
        {
            m_Data = new NativeList<byte>(Allocator.Persistent);
            m_Type = type;
        }

        void IDisposable.Dispose()
        {
            if (m_Data.IsCreated)
            {
                m_Data.Dispose();
            }
        }

        internal void Append(in ArraySegment<byte> segment, in int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

            unsafe
            {
                fixed (byte* pArray = segment.Array)
                {
                    m_Data.AddRange(pArray + segment.Offset, count);
                }
            }
        }
        #endregion
    }
}
