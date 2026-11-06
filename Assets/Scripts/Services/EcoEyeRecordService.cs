using System;
using UnityEngine.XR.MagicLeap;

public sealed class EcoEyeRecordService : IDisposable
{
    private const int ReadBufferSize = 4096;
    private const int BufferLenghtSeconds = 10;

    private readonly MLAudioInput.MicCaptureType captureType = MLAudioInput.MicCaptureType.WorldCapture;
    
    private MLAudioInput.BufferClip audioBuffer;

    private readonly float[] readBuffer = new float[ReadBufferSize];

    private int readPosition = 0;

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

        readPosition = 0;

        audioBuffer = new MLAudioInput.BufferClip(
            captureType,
            BufferLenghtSeconds,
            SampleRate
        );
    }

    public int ReadAvailableSamples()
    {
        if (!IsRecording)
        {
            return 0;
        }

        int totalSamplesRead = 0;

        while(IsRecording)
        {
            int count = audioBuffer.GetData(
            readBuffer, 
            readPosition, 
            out int nextReadPosition
            );

            if(count <= 0)
            {
                break;
            }

            readPosition = nextReadPosition;

            totalSamplesRead += count;

            SamplesAvailable?.Invoke(readBuffer, count);
        }

        return totalSamplesRead;
    }

    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        audioBuffer.Dispose();
        audioBuffer = null;

        readPosition = 0;
    }
    public void Dispose()
    {
        StopRecording();
    }
}
