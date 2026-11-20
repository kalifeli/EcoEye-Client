using System;

public class EcoEyeAudioConverter
{
    private bool hasLeftSample = false; // indica se è stato ricevuto il primo valore di una coppia stereo
    private float leftSample = 0f; // conserva il primo campione di una coppia stereo


    public byte[] Convert(float[] samples, int count)
    {
        byte[] pcm = new byte[samples.Length * 4];

        int bytesWritten = 0;

        for (int i = 0; i < count; i++)
        {
            // Aspettiamo il primo valore della coppia stereo.
            if (!hasLeftSample)
            {
                leftSample = samples[i];
                hasLeftSample = true;
                continue;
            }

            // 1. Stereo -> mono.
            float mono = (leftSample + samples[i]) / 2f; // Media di L_i e R_i
            hasLeftSample = false;
            byte[] sampleBytes = BitConverter.GetBytes(mono);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(sampleBytes);
            }

            Buffer.BlockCopy(sampleBytes,0, pcm, bytesWritten, 4);

            bytesWritten += 4;
        }

        Array.Resize(ref pcm, bytesWritten);

        return pcm;
    }


    public void Reset()
    {
        hasLeftSample = false;
        leftSample = 0f;
    }
}
