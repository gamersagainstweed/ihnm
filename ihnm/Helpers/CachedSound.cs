using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

// https://mark-dot-net.blogspot.de/2014/02/fire-and-forget-audio-playback-with.html
// Mark Heath 2014

namespace ihnm
{
    class CachedSound
    {
        public byte[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }


        public CachedSound(string audioFileName)
        {
            using (var audioFileReader = new AudioFileReader(audioFileName))
            {
                WaveFormat = audioFileReader.WaveFormat;
                var wholeFile = new List<byte>((int)(audioFileReader.Length / 4));
                var readBuffer = new byte[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                int samplesRead;

                while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeFile.AddRange(readBuffer.Take(samplesRead));
                }

                AudioData = wholeFile.ToArray();
            }
        }

        public void Dispose()
        {
            AudioData = null;
        }


    }
}
