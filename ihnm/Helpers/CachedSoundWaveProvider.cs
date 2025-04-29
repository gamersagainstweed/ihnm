using System;
using NAudio.Wave;

namespace ihnm
{
    class CachedSoundWaveProvider : IWaveProvider
    {
        private readonly CachedSound cachedSound;
        private long position;

        public CachedSoundWaveProvider(CachedSound cachedSound)
        {
            this.cachedSound = cachedSound;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (cachedSound.AudioData!=null)
            {
                var availableSamples = cachedSound.AudioData.Length - position;
                var samplesToCopy = Math.Min(availableSamples, count);

                Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);

                position += samplesToCopy;

                return (int)samplesToCopy;
            }
            return 0;
        }

        public WaveFormat WaveFormat { get { return cachedSound.WaveFormat; } }
    }
}
