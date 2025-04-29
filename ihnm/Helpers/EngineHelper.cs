using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ihnm.Managers;

namespace ihnm.Helpers
{
    public static class EngineHelper
    {

        public static void PlaySound(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2, string fileName, float volume = 1, float pitch = 1,
     float tempo = 1, double silenceThreshold = -10, bool stop = false)
        {
            engine1.PlaySound(fileName, (float)(volume*Common.playbackVolume), pitch, tempo, silenceThreshold, stop);
            engine2.PlaySound(fileName, volume, pitch, tempo, silenceThreshold, stop);
        }

        public static void PlayLoop(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2, string fileName, float volume = 1, float pitch = 1,
float tempo = 1, double silenceThreshold = -10, bool stop = false)
        {
            engine1.PlayLoop(fileName, (float)(volume * Common.playbackVolume), pitch, tempo);
            engine2.PlayLoop(fileName, volume, pitch, tempo);
        }

    }
}
