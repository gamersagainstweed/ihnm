using ihnm.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ihnm.Managers
{
    public class LoopManager
    {

        public List<string> loops = new List<string>();

        private AudioPlaybackEngine engine1;
        private AudioPlaybackEngine engine2;

        private string loopsDir = "sounds/loops/";

        public LoopManager(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2)
        {
            this.loadLoops();
            this.engine1 = engine1;
            this.engine2 = engine2;
        }

        private void loadLoops()
        {
            this.loops = new List<string>();

            if (!Directory.Exists(this.loopsDir))
                Directory.CreateDirectory(this.loopsDir);

            foreach (string file in Directory.GetFiles(loopsDir, "*.mp3", SearchOption.AllDirectories))
            {
                string loopname = Path.GetFileNameWithoutExtension(file);
                this.loops.Add(loopname);
            }
        }

        private string getLoopFile(string loop)
        {
            return Directory.GetFiles(this.loopsDir, loop + ".mp3", SearchOption.AllDirectories)[0];
        }

        public void Play(string music)
        {
            EngineHelper.PlayLoop(engine1, engine2, this.getLoopFile(music), (float)Common.volume);
        }

    }
}
