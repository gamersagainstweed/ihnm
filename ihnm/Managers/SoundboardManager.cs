using ihnm.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ihnm.Managers
{
    public class SoundboardManager
    {

        public List<string> sounds = new List<string>();

        private AudioPlaybackEngine engine1;
        private AudioPlaybackEngine engine2;

        private string soundsDir = "sounds/soundboard/";

        public SoundboardManager(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2)
        { 
            this.loadSounds();
            this.engine1= engine1;
            this.engine2 = engine2;
        }

        private void loadSounds()
        {
            this.sounds = new List<string>();

            if (!Directory.Exists(soundsDir))
                Directory.CreateDirectory(soundsDir);

            foreach (string file in Directory.GetFiles(soundsDir, "*.mp3", SearchOption.AllDirectories))
            {
                string soundname = Path.GetFileNameWithoutExtension(file);
                this.sounds.Add(soundname);
            }
        }

        private string getSoundFile(string sound)
        {
            return Directory.GetFiles(this.soundsDir, sound + ".mp3", SearchOption.AllDirectories)[0];
        }

        public void Play(string sound)
        {
            EngineHelper.PlaySound(engine1, engine2, this.getSoundFile(sound), (float)Common.volume);
        }

    }
}
