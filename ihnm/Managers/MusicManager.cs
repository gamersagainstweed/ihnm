using ihnm.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ihnm.Managers
{
    public class MusicManager
    {

        public List<string> music = new List<string>();

        private AudioPlaybackEngine engine1;
        private AudioPlaybackEngine engine2;

        private static string musicDir = "sounds/music/";

        public MusicManager(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2)
        { 
            this.loadMusic();
            this.engine1= engine1;
            this.engine2= engine2;
        }

        private void loadMusic()
        {
            this.music = new List<string>();

            if (!Directory.Exists(musicDir))
                Directory.CreateDirectory(musicDir);

            foreach (string file in Directory.GetFiles(musicDir, "*.mp3", SearchOption.AllDirectories))
            {
                string musicname = Path.GetFileNameWithoutExtension(file);
                this.music.Add(musicname);
            }
        }

        public static string getMusicFile(string music)
        {
            return Directory.GetFiles(musicDir, music + ".mp3", SearchOption.AllDirectories)[0];
        }

        public void Play(string music)
        {
            EngineHelper.PlaySound(engine1, engine2, getMusicFile(music), (float)Common.volume);
        }

    }
}
