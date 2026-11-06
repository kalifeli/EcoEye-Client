using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public class EcoEyeWebSocketService : IDisposable
{
    private const int BufferSize = 4096;
    private ClientWebSocket socket;

    public event Action<EcoEyeMessage> MessageReceived;

    public bool isConnected
    {
        get
        {
            return socket != null && socket.State == WebSocketState.Open;
        }
    }

    public async Task ConnectAsync(
    string serverIpAddress,
    int port,
    CancellationToken cancellationToken)
    {
        if (socket != null)
        {
            throw new InvalidOperationException(
                "Servizio già inizializzato."
            );
        }

        string address = $"ws://{serverIpAddress}:{port}/ws";

        ClientWebSocket newSocket = new ClientWebSocket();
        socket = newSocket;

        try
        {
            await newSocket.ConnectAsync(
                new Uri(address),
                cancellationToken
            );
        }
        catch
        {
            // Un ClientWebSocket fallito non può essere riutilizzato:
            // lo eliminiamo così un nuovo tentativo può crearne un altro.
            newSocket.Dispose();
            if (socket == newSocket)
            {
                socket = null;
            }
            throw;
        }

        Debug.Log("[EcoEye WS] Handshake completato. Stato socket: " +
          newSocket.State);
    }

    public async Task SendMessageAsync(
        EcoEyeMessage messaggio,
        CancellationToken cancellationToken
    )
    {

        if(messaggio == null)
        {
            throw new ArgumentException(nameof(messaggio));
        }


        try
        {
            if (!isConnected)
            {
                throw new InvalidOperationException("WebSocket non connesso!");
            }

            string messaggio_json = JsonUtility.ToJson(messaggio);

            byte[] dati = Encoding.UTF8.GetBytes(messaggio_json);

            await socket.SendAsync(
            new ArraySegment<byte>(dati),
            WebSocketMessageType.Text,                
            true,
            cancellationToken
            );

             Debug.Log("[WebSocket] Tx: " + JsonUtility.ToJson(messaggio));
        }
        catch (Exception e)
        {
            Debug.Log("Errore nell'inivio del messaggio" + e.Message);
        }
    }

    public async Task SendAudioPcmAsync(
        byte[] audio_pcm,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (!isConnected)
            {
                throw new InvalidOperationException("WebSocket non connesso!");
                
            }

            await socket.SendAsync(
                new ArraySegment<byte>(audio_pcm),
                WebSocketMessageType.Binary,
                true,
                cancellationToken
            );
            
        }catch(Exception e)
        {
            Debug.LogError("Errore nell invio dell' audio pcm " + e.Message);
        }
        
    }



    public async Task ReceiveMessageAsync(
        CancellationToken cancellationToken
    )
    {

        if (!isConnected)
        {
            throw new InvalidOperationException("WebSocket non connesso");
        }

        byte[] buffer = new byte[BufferSize];


        while(socket.State == WebSocketState.Open)
        {
            StringBuilder risposta = new StringBuilder();

            WebSocketReceiveResult risultato;

            do
            {
                risultato = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken
                );

                if(risultato.MessageType != WebSocketMessageType.Text)
                {
                    Debug.LogWarning("[WebSocket] Messaggio non testuale");
                    return;
                }

                risposta.Append(Encoding.UTF8.GetString(buffer, 0, risultato.Count));

            } while (!risultato.EndOfMessage);

            string json = risposta.ToString();

            Debug.Log("[WebSocket] RX: " + json);

            EcoEyeMessage messaggio = JsonUtility.FromJson<EcoEyeMessage>(json);

            MessageReceived?.Invoke(messaggio);

        }
        
    }



    public void Dispose()
    {
        socket?.Abort();
        socket?.Dispose();
        socket = null;
    }
}
