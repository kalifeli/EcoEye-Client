
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;


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

    private bool IsStreaming = false;

    private void Awake()
    {
        if(subtitlesDisplay == null || startAcquisitionButton == null || recordController == null)
        {
            Debug.LogError("[EcoEyeWebSocketClient] Mancano i riferimenti nell'ispector");

            enabled = false;

            return;
        }

        service = new EcoEyeWebSocketService();

        viewModel = new EcoEyeViewModel(service);

        viewModel.SubtitleChanged += subtitlesDisplay.ShowSubtitle;

        startAcquisitionButton.interactable = false;
        
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
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[EcoEye WS] Errore durante la connessione: " + e);
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
    }

    /// <summary>
    /// Quanti secondi aspettare prima del tentativo successivo.
    /// attempt = numero del tentativo appena fallito (1, 2, 3, ...).
    /// </summary>
    private float GetRetryDelaySeconds(int attempt)
    {
        // TODO(human): scegli la strategia di attesa tra un tentativo e l'altro.
        return 2f;
    }

    public void StartStreaming()
    {
        if(service.isConnected && !IsStreaming)
        {
            recordController.StartAcquisition();
            Debug.Log("[EcoEyeWebSocketClient] Acquisizione audio inizita!");
        }
        
    }

    private async void OnPcmConversionAvailable(byte[] pcmChunk)
    {
        try
        {
            await service.SendAudioPcmAsync(pcmChunk, destroyCancellationToken);
            Debug.Log("[EcoEyeWebSocketClient] Invio dell'audio PCM16 iniziato");
            
        }catch(Exception e)
        {
            Debug.LogError("[EcoEyeWebSocketClient] Errore nella trasmissione dell'audio PCM16" + e);
        }   
    }

    private void Update()
    {
        if(startAcquisitionButton != null)
        {
            startAcquisitionButton.interactable = service != null && service.isConnected;
        }
    }


    private void OnDestroy()
    {

        if(recordController != null)
        {
            recordController.PcmConversionAvailable -= OnPcmConversionAvailable;
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
            service.Dispose();
        }

    }
}
