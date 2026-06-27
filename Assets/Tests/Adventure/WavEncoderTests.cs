using System;
using NUnit.Framework;
using WordFlow.Adventure.Core;

namespace WordFlow.Adventure.Tests
{
    public sealed class WavEncoderTests
    {
        [Test]
        public void Header_HasRiffWaveDataMarkers_AndCorrectLength()
        {
            byte[] wav = WavEncoder.Encode(new float[] { 0f, 0f, 0f, 0f }, channels: 1, frequency: 16000);
            Assert.AreEqual(44 + 4 * 2, wav.Length);
            Assert.AreEqual("RIFF", Ascii(wav, 0, 4));
            Assert.AreEqual("WAVE", Ascii(wav, 8, 4));
            Assert.AreEqual("data", Ascii(wav, 36, 4));
            Assert.AreEqual(16000, BitConverter.ToInt32(wav, 24)); // sample rate
            Assert.AreEqual(1, BitConverter.ToInt16(wav, 22));     // channels
            Assert.AreEqual(16, BitConverter.ToInt16(wav, 34));    // bits per sample
            Assert.AreEqual(4 * 2, BitConverter.ToInt32(wav, 40)); // data chunk size
        }

        [Test]
        public void FullScaleSample_EncodesToInt16Max()
        {
            byte[] wav = WavEncoder.Encode(new float[] { 1f }, channels: 1, frequency: 16000);
            Assert.AreEqual(short.MaxValue, BitConverter.ToInt16(wav, 44));
        }

        [Test]
        public void NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(WavEncoder.Encode(null, 1, 16000));
            Assert.IsNull(WavEncoder.Encode(Array.Empty<float>(), 1, 16000));
        }

        private static string Ascii(byte[] b, int offset, int len)
        {
            var c = new char[len];
            for (int i = 0; i < len; i++) c[i] = (char)b[offset + i];
            return new string(c);
        }
    }
}
