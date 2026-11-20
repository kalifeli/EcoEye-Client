using System;
using System.Collections.Concurrent;
using UnityEngine.XR.MagicLeap;

public sealed class EcoEyeRecordService : IDisposable
{
    private const int BufferLenghtSeconds = 10;

    private readonly MLAudioInput.MicCaptureType captureType = MLAudioInput.MicCaptureType.WorldCapture;
    
    private MLAudioInput.BufferClip audioBuffer;

    // Coda FIFO che mantiene i blocchi audio in attesa di elaborazione.
    private ConcurrentQueue<float[]> pendingSamples = new ConcurrentQueue<float[]>();

    // Evento che mantiene la funzione di callback da eseguire a seguito dell'aggiunta di un nuovo blocco audio
    private Action<float[]> samplesCallback;


    public event Action<float[], int> SamplesAvailable;

    public bool IsRecording
    {
        get
        {
            return audioBuffer != null;
        }
    }

    public int SampleRate { get; private set; }

    public int Channels { get; private set; }


    public void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }

        SampleRate = MLAudioInput.GetSampleRate(captureType);
        Channels = MLAudioInput.GetChannels(captureType);

        var sessionQueue = new ConcurrentQueue<float[]>();
        pendingSamples = sessionQueue;

        samplesCallback = samples =>
        {
            float[] copy = (float[])samples.Clone();
            sessionQueue.Enqueue(copy);
        };

        audioBuffer = new MLAudioInput.BufferClip(
            captureType,
            BufferLenghtSeconds,
            SampleRate
        );

        audioBuffer.OnReceivedSamples += samplesCallback;
    }

    public int ReadAvailableSamples()
    {
       int totalSamplesRead = 0;

       while(pendingSamples.TryDequeue(out float[] samples))
        {
            totalSamplesRead += samples.Length;

            SamplesAvailable?.Invoke(samples, samples.Length);
        }

        return totalSamplesRead;
    }

    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }


        audioBuffer.OnReceivedSamples -= samplesCallback;

        try
        {
            audioBuffer.Dispose();
        }
        finally
        {
            audioBuffer = null;
            samplesCallback = null;
        }

        ReadAvailableSamples();
    }

    public void Dispose()
    {
        StopRecording();
    }
}
