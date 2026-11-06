using System;
using UnityEngine;
using UnityEngine.Android;

public class EcoEyeRecordController : MonoBehaviour
{

    private EcoEyeRecordService recordService;

    private EcoEyePcmConverter pcmConverter;

    private PermissionCallbacks permissionCallbacks;

    private bool acquisitionRequested = false;

    public event Action<byte[]> PcmConversionAvailable;

    void Awake()
    {
        recordService = new EcoEyeRecordService();
        pcmConverter = new EcoEyePcmConverter();
        permissionCallbacks = new PermissionCallbacks(); // oggetto che contiene degli eventi

        recordService.SamplesAvailable += OnSamplesAvailable;

        // Indichiamo i metodi di callback quando l'utente accetta o rifiuta il permesso
        permissionCallbacks.PermissionGranted += OnPermissionGranted;
        permissionCallbacks.PermissionDenied += OnPermissionDenied;

    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    
    }

    public void StartAcquisition()
    {
        Debug.Log("[Record] Richiesta di avvio acquisizione ricevuta.");

        // Se l'acquisizione è già richiesta o in corso, non facciamo nulla.
        if (acquisitionRequested || recordService.IsRecording)
        {
            Debug.Log("[Record] Acquisizione già richiesta o in corso: ignoro il click.");
            return;
        }

        

    #if UNITY_ANDROID && !UNITY_EDITOR

        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            acquisitionRequested = true;
        }
        else
        {
            Permission.RequestUserPermission(
                Permission.Microphone,
                permissionCallbacks
                );
        }
    #else
        acquisitionRequested = false;
    #endif
    }


    private void OnPermissionGranted(string permission)
    {
        if (permission == Permission.Microphone)
        {
            acquisitionRequested = true;
        }
    }

    private void OnPermissionDenied(string permission)
    {
        if (permission == Permission.Microphone)
        {
            acquisitionRequested = false;

            Debug.LogWarning("[Record] Permesso microfono negato.");
        }
    }
    private void OnSamplesAvailable(float[] samples, int count)
    {
        byte[] audio_pcm = pcmConverter.Convert(samples,count);

        if(audio_pcm.Length > 0)
        {
            PcmConversionAvailable?.Invoke(audio_pcm);
        }
        
    }


    // Update is called once per frame
    void Update()
    {

        if (acquisitionRequested && !recordService.IsRecording)
        {

            try
            {
                
                acquisitionRequested = false; // questo permette ad Update di chiamare solo una volta StartRecording() e non una volta ogni frame

                pcmConverter.Reset();

                recordService.StartRecording();
            
                Debug.Log("[EcoEyeRecordController] Registrazione avviata.");
            }
            catch (Exception e)
            {
                Debug.LogError("[EcoEyeRecordController] Errore durante l'avvio: " + e.Message);
            }
        }

        if (recordService.IsRecording)
        {
            recordService.ReadAvailableSamples();
        }
    }

    public void OnDestroy()
    {
        acquisitionRequested = false;

        recordService.SamplesAvailable -= OnSamplesAvailable;

        // Disiscrizione degli eventi per la scelta del permesso microfono
        permissionCallbacks.PermissionGranted -= OnPermissionGranted;
        permissionCallbacks.PermissionDenied -= OnPermissionDenied;

        recordService.Dispose();

    }
}
