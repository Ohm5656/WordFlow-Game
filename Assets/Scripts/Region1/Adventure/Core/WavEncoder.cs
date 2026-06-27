using System;

namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// 16-bit PCM mono/stereo WAV encoder. Byte layout extracted from
    /// MagicStonePuzzleController.EncodeWav so it is reusable and unit-testable.
    /// </summary>
    public static class WavEncoder
    {
        private const int HeaderSize = 44;
        private const int BytesPerSample = 2;

        /// <summary>Encode PCM samples to a 16-bit WAV byte array. Returns null if samples is null/empty.</summary>
        public static byte[] Encode(float[] samples, int channels, int frequency)
        {
            if (samples == null || samples.Length == 0) return null;
            channels = Math.Max(1, channels);
            frequency = Math.Max(1, frequency);

            byte[] wav = new byte[HeaderSize + samples.Length * BytesPerSample];

            WriteAscii(wav, 0, "RIFF");
            WriteInt(wav, 4, wav.Length - 8);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt(wav, 16, 16);
            WriteShort(wav, 20, 1); // PCM
            WriteShort(wav, 22, (short)channels);
            WriteInt(wav, 24, frequency);
            WriteInt(wav, 28, frequency * channels * BytesPerSample);
            WriteShort(wav, 32, (short)(channels * BytesPerSample));
            WriteShort(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt(wav, 40, samples.Length * BytesPerSample);

            int offset = HeaderSize;
            for (int i = 0; i < samples.Length; i++)
            {
                int v = (int)Math.Round(samples[i] * short.MaxValue);
                if (v > short.MaxValue) v = short.MaxValue;
                if (v < short.MinValue) v = short.MinValue;
                WriteShort(wav, offset, (short)v);
                offset += BytesPerSample;
            }
            return wav;
        }

        private static void WriteAscii(byte[] t, int o, string v)
        {
            for (int i = 0; i < v.Length; i++) t[o + i] = (byte)v[i];
        }

        private static void WriteInt(byte[] t, int o, int v) =>
            Buffer.BlockCopy(BitConverter.GetBytes(v), 0, t, o, 4);

        private static void WriteShort(byte[] t, int o, short v) =>
            Buffer.BlockCopy(BitConverter.GetBytes(v), 0, t, o, 2);
    }
}
