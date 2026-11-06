using System;

public class EcoEyePcmConverter
{
    private bool hasLeftSample = false; // indica se è stato ricevuto il primo valore di una coppia stereo
    private float leftSample = 0f; // conserva il primo campione di una coppia stereo

    private float sum = 0f; // accumula tre campioni mono consecutivi
    private int monoCount = 0; // conta quanti campioni mono abbiamo accumulato, da 0 a 3


    public byte[] Convert(float[] samples, int count)
    {
        // Spazio sufficiente per i byte prodotti.
        byte[] pcm = new byte[(count / 6 + 2) * 2];

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

            // 2. Accumuliamo tre campioni mono.
            sum += mono;
            monoCount++;

            if (monoCount == 3)
            {
                // cambio della frequenza di campionamento f_s da 48 a 16 kHz | 16 000 = 48.000 / 3
                float average = sum / 3f; // stiamo prendendo un campione ogni tre campioni originali

                // 3. QUANTIZZAZINE: Float -> PCM signed 16 bit.
                int value = (int)Math.Round(average * 32768f);

                // Limitiamo il valore all'intervallo PCM16.
                value = Math.Max(-32768, Math.Min(32767, value));

                // Trasformiamo il valore intero in due byte (il nostro server si aspetta PCM16_le)
                pcm[bytesWritten++] = (byte)value; // byte meno significativo
                pcm[bytesWritten++] = (byte)(value >> 8); // byte più significativo

                // Prepariamo il gruppo successivo.
                sum = 0f;
                monoCount = 0;
            }
        }

        // Eliminiamo gli eventuali byte inutilizzati.
        Array.Resize(ref pcm, bytesWritten);

        return pcm;
    }


    public void Reset()
    {
        hasLeftSample = false;
        leftSample = 0f;

        sum = 0f;
        monoCount = 0;
    }
}
