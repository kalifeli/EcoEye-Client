using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.IO;

public class EcoEyeWebSocketService : IDisposable
{
    
    private ClientWebSocket socket;

    private const int BufferSize = 4096;

    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1,1);

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

        string messaggio_json = JsonUtility.ToJson(messaggio);

        byte[] dati = Encoding.UTF8.GetBytes(messaggio_json);

        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (!isConnected)
            {
                throw new InvalidOperationException("WebSocket non connesso!");
            }

            await socket.SendAsync(
            new ArraySegment<byte>(dati),
            WebSocketMessageType.Text,                
            true,
            cancellationToken
            );
            
            Debug.Log("[WebSocket] Tx: " + JsonUtility.ToJson(messaggio));
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task SendAudioPcmAsync(
        byte[] audio_pcm,
        CancellationToken cancellationToken
    )
    {

        await semaphore.WaitAsync(cancellationToken);
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
            
        }
        finally
        {
            semaphore.Release();
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
            using(var messageBytes = new MemoryStream()){

                WebSocketReceiveResult risultato;

                do
                {
                    risultato = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), // da riferimento allo stesso butter. Non ne copia quindi il contenuto.
                        cancellationToken
                    );

                    if(risultato.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if(risultato.MessageType != WebSocketMessageType.Text)
                    {
                        throw new InvalidOperationException("Il server ha inviato un messaggio non testuale.");
                    }

                    messageBytes.Write(
                        buffer,
                        0,
                        risultato.Count
                    );

                } while (!risultato.EndOfMessage);

                string json_message = Encoding.UTF8.GetString(messageBytes.ToArray());

                EcoEyeMessage messaggio = JsonUtility.FromJson<EcoEyeMessage>(json_message);
                
                MessageReceived?.Invoke(messaggio);
                
            }

        }
        
    }



    public void Dispose()
    {
        socket?.Abort();
        socket?.Dispose();
        socket = null;
    }
}
