
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class EcoEyeWebSocketClient : MonoBehaviour
{
    [SerializeField, Tooltip("Con l'Hotspot personale attivo l'iPhone ha sempre 172.20.10.1")]
    private string serverIpAddress = "172.20.10.1";

    [SerializeField]
    private int port = 8080;

    [SerializeField]
    private SubtitlesDisplay subtitlesDisplay;

    [SerializeField, Min(1f), Tooltip("Secondi massimi di attesa per ogni tentativo di connessione")]
    private float connectTimeoutSeconds = 5f;

    [SerializeField, Min(1), Tooltip("Numero massimo di tentativi prima di arrendersi")]
    private int maxConnectAttempts = 10;

    private EcoEyeViewModel viewModel;

    private EcoEyeWebSocketService service;

    [SerializeField]
    private EcoEyeRecordController recordController;

    [SerializeField]
    private Button startAcquisitionButton;

    [SerializeField]
    private TMP_Text startAcquisitionButtonText;

    private AcquisitionButtonState buttonState = AcquisitionButtonState.NotAvailable;

    private Task lastAudioSend = Task.CompletedTask;



    // Stato bottone UI
    private enum AcquisitionButtonState
    {
        Idle,
        Starting,
        Recording,
        Stopping,
        NotAvailable,
    }

    private void SetButtonBehaviour(AcquisitionButtonState nuovoStato)
    {
        buttonState = nuovoStato;

        if(startAcquisitionButton == null || startAcquisitionButtonText == null)
        {
            Debug.Log("[WebSocketClient] ERRORE: riferimento al bottone e/o al testo mancanti");
            return;
        }

        if(nuovoStato == AcquisitionButtonState.Idle)
        {
           
            startAcquisitionButton.interactable = service.isConnected;
            startAcquisitionButtonText.text = "Avvia trascrizione";

        }else if(nuovoStato == AcquisitionButtonState.Starting)
        {
            startAcquisitionButton.interactable = false;
            startAcquisitionButtonText.text = "Avvio in corso...";
        }else if(nuovoStato == AcquisitionButtonState.Recording)
        {
            startAcquisitionButton.interactable = service.isConnected;
            startAcquisitionButtonText.text = "Termina tracrizione";
        }else if(nuovoStato == AcquisitionButtonState.Stopping)
        {
            startAcquisitionButton.interactable = false;
            startAcquisitionButtonText.text = "Arresto in corso...";
        }else if(nuovoStato == AcquisitionButtonState.NotAvailable)
        {
            startAcquisitionButton.interactable = false;
        }
    }

    private void Awake()
    {
        if(subtitlesDisplay == null 
        || startAcquisitionButton == null 
        || startAcquisitionButtonText == null 
        ||recordController == null)

        {
            Debug.LogError("[EcoEyeWebSocketClient] Mancano i riferimenti nell'ispector");

            enabled = false;

            return;
        }

        service = new EcoEyeWebSocketService();

        viewModel = new EcoEyeViewModel(service);

        service.MessageReceived += onSessionMessageReceived;

        viewModel.SubtitleChanged += subtitlesDisplay.ShowSubtitle;

        recordController.RecordingStarted += OnRecordingStarted;

        SetButtonBehaviour(AcquisitionButtonState.NotAvailable);
        
        recordController.PcmConversionAvailable += OnPcmConversionAvailable;
    }

    private async void Start()
    {

        Debug.Log("[EcoEye] WebSocketClient avviato.");

        if(subtitlesDisplay == null)
        {
            Debug.LogError("[WebSocket] Riferimento a subTitleDisplay non assegnato");
            return;
        }

        // LOG 2: verifica il riferimento ai sottotitoli.
        Debug.Log("[EcoEye WS] SubtitlesDisplay assegnato: " +
                  subtitlesDisplay.name);

        try
        {
        
            await ConnectWithRetryAsync(destroyCancellationToken);

        }
        catch (OperationCanceledException)
        {
            Debug.Log("[EcoEye WS] Tentativi di connessione interrotti (oggetto distrutto).");

            SetButtonBehaviour(AcquisitionButtonState.NotAvailable);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[EcoEye WS] Errore durante la connessione: " + e);
            
            SetButtonBehaviour(AcquisitionButtonState.NotAvailable);
        }
    }

    private async Task ConnectWithRetryAsync(CancellationToken token)
    {
        TimeSpan timeout = TimeSpan.FromSeconds(connectTimeoutSeconds);

        for (int attempt = 1; attempt <= maxConnectAttempts; attempt++)
        {
            // LOG 3: indirizzo verso cui stiamo tentando la connessione.
            Debug.Log($"[EcoEye WS] Tentativo {attempt}/{maxConnectAttempts} verso ws://{serverIpAddress}:{port}/ws");

            bool connected = await viewModel.ConnectAsync(serverIpAddress, port, timeout);

            if (connected)
            {
                // LOG 4: connessione riuscita.
                Debug.Log("[EcoEye WS] Connesso. isConnected = " + service.isConnected);
                SetButtonBehaviour(AcquisitionButtonState.Idle);
                return;
            }

            if (attempt < maxConnectAttempts)
            {
                float delaySeconds = GetRetryDelaySeconds(attempt);
                Debug.Log($"[EcoEye WS] Nuovo tentativo tra {delaySeconds:0.#} s");
                await Awaitable.WaitForSecondsAsync(delaySeconds, token);
            }
        }

        Debug.LogError($"[EcoEye WS] Server non raggiungibile dopo {maxConnectAttempts} tentativi.");
        viewModel.SetSubtitle($"Server {serverIpAddress}:{port} non raggiungibile. Controlla IP e server sull'iPhone.");
        SetButtonBehaviour(AcquisitionButtonState.NotAvailable);
    }

    private float GetRetryDelaySeconds(int attempt)
    {
        return 2f;
    }

    public async Task StartStreaming()
    {
        lastAudioSend = Task.CompletedTask; // otteniamo l'ultima attività completata correttamente prima dell'avvio della trasmissione

        EcoEyeMessage audio_start = new EcoEyeMessage(
            type: EcoEyeMessageType.AudioStart,
            content: "pcm_f32le;sample_rate=48000;channels=1"
           );

        await service.SendMessageAsync(
            audio_start,
            destroyCancellationToken
        );

    }

    private void onSessionMessageReceived(EcoEyeMessage messaggio)
    {
        if(messaggio == null)
        {
            return;
        }

        if (
            messaggio.type == EcoEyeMessageType.Ack &&
            messaggio.content == "audio_start" &&
            buttonState == AcquisitionButtonState.Starting
        )
        {
            recordController.StartAcquisition();
        }else if(messaggio.type == EcoEyeMessageType.TranscriptionFinal && buttonState == AcquisitionButtonState.Stopping)
        {
            SetButtonBehaviour(AcquisitionButtonState.Idle);
        }
    }

    private async Task StopStreaming()
    {

        recordController.StopRecording();

        await lastAudioSend;

        EcoEyeMessage audio_end =  new EcoEyeMessage(
            EcoEyeMessageType.AudioEnd,
            ""
        );

        await service.SendMessageAsync(
            audio_end, 
            destroyCancellationToken
        );
    }

    private void OnRecordingStarted()
    {
        if (buttonState != AcquisitionButtonState.Starting)
        {
            return;
        }

        SetButtonBehaviour(AcquisitionButtonState.Recording);
    }

    public async void onButtonClick()
    {
        try
        {
            if (buttonState == AcquisitionButtonState.Recording)
            {
                SetButtonBehaviour(AcquisitionButtonState.Stopping);
                await StopStreaming();
                return;
            }

            if (service == null || !service.isConnected)
            {
                SetButtonBehaviour(AcquisitionButtonState.NotAvailable);
                return;
            }

            if (buttonState == AcquisitionButtonState.Idle)
            {
                SetButtonBehaviour(AcquisitionButtonState.Starting);
                await StartStreaming();
            }
        }
        catch (Exception error)
        {
            Debug.LogError(
                "[EcoEye] Operazione interrotta: " + error.Message
            );

            SetButtonBehaviour(AcquisitionButtonState.NotAvailable);
        }
    }

    private async void OnPcmConversionAvailable(byte[] pcmChunk)
    {
        lastAudioSend = SendAudioAfterAsync(
            lastAudioSend,
            pcmChunk,
            destroyCancellationToken
        );

        try
        {
            await lastAudioSend;


        }
        catch(OperationCanceledException)
        {
            // L'operazione è stata annullata
        }
        catch(Exception e)
        {
            Debug.LogError("[EcoEyeWebSocketClient] Errore nella trasmissione dell'audio PCM16" + e);
        }   
    }

    private async Task SendAudioAfterAsync(
    Task previousSend,
    byte[] pcmChunk,
    CancellationToken cancellationToken)
    {
        await previousSend;

        await service.SendAudioPcmAsync(
            pcmChunk,
            cancellationToken
        );
    }

    private void Update()
    {
        
    }


    private void OnDestroy()
    {

        if(recordController != null)
        {
            recordController.PcmConversionAvailable -= OnPcmConversionAvailable;
            recordController.RecordingStarted -= OnRecordingStarted;
        }

        if(viewModel != null)
        {
            if(subtitlesDisplay != null)
            {
                viewModel.SubtitleChanged -= subtitlesDisplay.ShowSubtitle;
            }

            viewModel.Dispose();
        }

        if(service != null)
        {
            service.MessageReceived -= onSessionMessageReceived;
            service.Dispose();
        }

    }
}
