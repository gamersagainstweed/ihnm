using Fizzler;
using NAudio.Wave;
using NAudio.WaveFormRenderer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ihnm.Helpers
{


        static class AudioFileReaderExt
        {
            public enum SilenceLocation { Start, End }

            public static double toDB(float num)
            {
                double dB = 20 * Math.Log10(Math.Abs(num));
                return dB;
            }

            private static bool IsSilence(float amplitude, double threshold)
            {
                double dB = 20 * Math.Log10(Math.Abs(amplitude));
                return dB < threshold;
            }

            public static TimeSpan GetSilenceDuration(this AudioFileReader reader,
                                                      SilenceLocation location,
                                                      double silenceThreshold = -40)
            {
                int counter = 0;
                bool volumeFound = false;
                bool eof = false;
                long oldPosition = reader.Position;

                var buffer = new float[reader.WaveFormat.SampleRate * 4];
                while (!volumeFound && !eof)
                {
                    int samplesRead = reader.Read(buffer, 0, buffer.Length);
                    if (samplesRead == 0)
                        eof = true;

                    for (int n = 0; n < samplesRead; n++)
                    {
                        if (IsSilence(buffer[n], silenceThreshold))
                        {
                            counter++;
                        }
                        else
                        {
                            if (location == SilenceLocation.Start)
                            {
                                volumeFound = true;
                                break;
                            }
                            else if (location == SilenceLocation.End)
                            {
                                counter = 0;
                            }
                        }
                    }
                }

                // reset position
                reader.Position = oldPosition;

                double silenceSamples = (double)counter / reader.WaveFormat.Channels;
                double silenceDuration = (silenceSamples / reader.WaveFormat.SampleRate) * 1000;
                return TimeSpan.FromMilliseconds(silenceDuration);
            }


            public static List<float> getPeaks(this AudioFileReader reader)
            {
                var samples = reader.Length / (reader.WaveFormat.Channels * reader.WaveFormat.BitsPerSample / 8);
                var max = 0.0f;
                // waveform will be a maximum of 4000 pixels wide:
                var batch = (int)Math.Max(40, samples / 4000);

                float[] buffer = new float[batch];
                int read;

                List<float> peaks = new List<float>();

                while ((read = reader.Read(buffer, 0, batch)) == batch)
                {
                    for (int n = 0; n < read; n++)
                    {
                        max = Math.Max(Math.Abs(buffer[n]), max);
                    };

                    peaks.Add(max);

                    max = 0;

                }
                return peaks;
            }


    }

    
}
