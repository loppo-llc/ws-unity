using UnityEngine;
using WsUnity;

public class WsUnityServerSample : MonoBehaviour, IWsServerCallback
{
    WsServer m_Server;

    System.Collections.IEnumerator Start()
    {
        Debug.Log("Server Start");
        m_Server = new WsServer(WsServerOption.Default, this);
        m_Server.Start();

        yield return new WaitForSeconds(10f);
        Destroy(this);
    }

    void OnDestroy()
    {
        Debug.Log("Server Stop");
        m_Server?.Stop();
        m_Server = null;
    }

    async void IWsServerCallback.OnConnect(WsServer _, WsSocket socket)
    {
        Debug.Log($"Open: {socket.Address}");
        await socket.SendAsync("Hello world!");
        await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure);
        Debug.Log($"Close: {socket.Address}");
    }

    void IWsServerCallback.OnError(WsServer _, System.Exception exception)
    {
        Debug.LogException(exception);
    }
}
