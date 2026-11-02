
using System;
using UnityEngine;


public class EcoEyeWebSocketClient : MonoBehaviour
{
    [SerializeField]
    private string serverIpAddress = "192.168.1.35";

    [SerializeField]
    private int port = 8080;

    [SerializeField]
    private SubtitlesDisplay subtitlesDisplay;

    private EcoEyeViewModel viewModel;

    private EcoEyeWebSocketService service;

    private async void Start()
    {
        if(subtitlesDisplay == null)
        {
            Debug.LogError("[WebSocket] Riferimento a subTitleDisplay non assegnato");
            return;
        }
        service = new EcoEyeWebSocketService();

        viewModel = new EcoEyeViewModel(service);

        viewModel.SubtitleChanged += subtitlesDisplay.ShowSubtitle;

        try
        {
            await viewModel.ConnectAsync(serverIpAddress, port);
        }
        catch (Exception e)
        {
            Debug.LogError("[WebSocket] Errore: " + e);
        }
    }

    private void OnDestroy()
    {
        if(viewModel != null && service != null){
        viewModel.SubtitleChanged -= subtitlesDisplay.ShowSubtitle;

        viewModel.Dispose();
        service.Dispose();
        }
    }
}