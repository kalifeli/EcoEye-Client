using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class EcoEyeViewModel: IDisposable
{
    private readonly EcoEyeWebSocketService service;
    private CancellationTokenSource cancellation;
    private Task ricezione;
    public string CurrentSubtitle { get; private set; } = "In attesa di una conversazione ...";
    public event Action<string> SubtitleChanged;

    public EcoEyeViewModel(EcoEyeWebSocketService service)
    {
        this.service = service;
        this.service.MessageReceived += OnMessageReceived;
    }

    public async Task ConnectAsync(string ipAddress, int port)
    {
        cancellation = new CancellationTokenSource();

        SetSubtitle("Connessione in corso...");

        await service.ConnectAsync(ipAddress,port,cancellation.Token);

        SetSubtitle("Connesso. In attesa di una conversazione");

        ricezione = ReceiveAsync(); // chiamato senza await perchè è un'operazione che dura per tutta la durante della connessione

    }

    public async Task SendPingAsync()
    {
        try
        {
            await service.SendMessageAsync(
            new EcoEyeMessage(
                EcoEyeMessageType.Ping,
                "ciao"
            ),
            cancellation.Token
            );

        Debug.Log("[WebSocket] Tx: messaggio di Ping inviato correttamente");
        }catch(Exception e)
        {
            Debug.LogError("[WebSocket] Errore nella trasmissione del messaggio di Ping:" + e.Message);
        }
        
    }

    public async Task ReceiveAsync()
    {
        try
        {
            await service.ReceiveMessageAsync(cancellation.Token);
            SetSubtitle("Connessione chiusa dal server");
            
        }catch(Exception e)
        {
            Debug.LogError("[ViewModel] Errore in ricezione: " + e.Message);
            SetSubtitle("Connessione persa.");
        }
    }

    private async void OnMessageReceived(EcoEyeMessage messaggio)
    {
        if(messaggio == null)
        {
            Debug.LogWarning("[ViewModel] Rx: messaggio non valido");
            return;
        }else if(messaggio.type == EcoEyeMessageType.Transcription)
        {
            SetSubtitle(messaggio.content);
        }else if(messaggio.type == EcoEyeMessageType.Ping)
        {
            Debug.Log("[ViewModel] Pong ricevuto: il server risponde");

            // Prepariamo un messaggio PONG
            EcoEyeMessage pong = new EcoEyeMessage(EcoEyeMessageType.Pong, messaggio.content);

            // Inviamo il messaggio
            await service.SendMessageAsync(pong, cancellation.Token);

        }else if(messaggio.type == EcoEyeMessageType.Pong){
            SetSubtitle(messaggio.content);
        }
        else if(messaggio.type == EcoEyeMessageType.Error)
        {
            Debug.LogWarning("[ViewModel] Errore dal server");
        }
        else
        {
            Debug.Log("[ViewModel] Tipo non gestito " + messaggio.content);
        }
    }
    public void SetSubtitle(string subtitle)
    {
        string newSubtitle = subtitle ?? string.Empty;

        if(newSubtitle == CurrentSubtitle)
        {
            return;
        }

        CurrentSubtitle = newSubtitle;

        SubtitleChanged?.Invoke(CurrentSubtitle);
    }

    public void Dispose()
    {
        service.MessageReceived -= OnMessageReceived; // disicrizione dall'evento MessageReceived
        cancellation?.Cancel(); // ferma il lavoro di tutte le operazioni che hanno ricevuto il token
        cancellation?.Dispose(); // libera le risorse che usa internamente
        
    }
}
