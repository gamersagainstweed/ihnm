using Microsoft.VisualBasic;
using ihnm.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ihnm.Managers
{
    public class SongsManager
    {

        public List<string> songs = new List<string>();

        private AudioPlaybackEngine engine1;
        private AudioPlaybackEngine engine2;

        private string songsDir = "sounds/songs/";

        public bool isSongPlaying = false;
        public string currentSong = "";

        static HttpClient client = new HttpClient();

        public SongsManager(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2)
        {
            this.loadSongs();
            this.engine1 = engine1;
            this.engine2 = engine2;
        }

        private void loadSongs()
        {
            this.songs = new List<string>();

            if (!Directory.Exists(songsDir))
                Directory.CreateDirectory(songsDir);

            foreach (string directory in Directory.GetDirectories(this.songsDir))
            {
                string songname = Path.GetFileNameWithoutExtension(directory);
                this.songs.Add(songname);
            }
        }

        private string getInstrumentalFile(string song)
        {
            return Directory.GetFiles(this.songsDir + song, "instrumental.mp3")[0];
        }

        private string getVoiceFile(string song, string voice)
        {
            if (File.Exists(this.songsDir + song + "/" + voice + ".mp3"))
                return Directory.GetFiles(this.songsDir + song, voice + ".mp3")[0];
            else
                return this.songsDir + song + "/" + "original.mp3";
        }


        public class OutputEventArgs : EventArgs
        {
            public string text { get; set; }
        }

        protected void CallOutput(string text)
        {
            OutputEventArgs e = new OutputEventArgs();
            e.text = text;
            Output?.Invoke(null, e);
        }

        public delegate void OutputEventHandler(object myObject, OutputEventArgs myArgs);

        public event OutputEventHandler Output;

        private void playSongEngine(string filename1, string filename2,
            float volume = 1, float pitch = 1, float tempo = 1, bool sync = false, int ping=0)
        {
            engine1.cacheSound(filename1);
            engine1.cacheSound(filename2);
            engine2.cacheSound(filename1);
            engine2.cacheSound(filename2);

            if (sync)
            {
                engine1.PlaySong(filename1, filename2, volume, pitch, tempo, sync, ping);
                //Thread.Sleep(10000);
                engine2.PlaySong(filename1, filename2, volume, pitch, tempo, sync, ping);

            }
            else
            {

                new Thread(() => engine1.PlaySong(filename1, filename2, volume, pitch, tempo)).Start();
                engine2.PlaySong(filename1, filename2, volume, pitch, tempo);
            }
        }
        public void Play(string song, string voice, EnumSongPlayMode songPlayMode, bool sync = false, int ping=0)
        {
            Common.sentence = "";
            Common.sentence2 = "";
            this.isSongPlaying = true;
            this.currentSong = song;

            if (File.Exists(this.songsDir + song + "/" +"lipsyncThreshold.txt"))
            {
                StreamReader sr = new StreamReader(this.songsDir + song + "/" + "lipsyncThreshold.txt");
                string line = sr.ReadLine();
                double threshold = double.Parse(line);
                Common.lipsyncThreshold = threshold;
            }

            if (File.Exists(this.songsDir + song + "/" + "lipsyncDelay.txt"))
            {
                StreamReader sr = new StreamReader(this.songsDir + song + "/" + "lipsyncDelay.txt");
                string line = sr.ReadLine();
                int delay = int.Parse(line);
                Common.lipsyncDelay = TimeSpan.FromMilliseconds(delay);
            }

            new Thread(() =>
            {
                if (songPlayMode == EnumSongPlayMode.DEFAULT)
                {
                    this.playSongEngine(getInstrumentalFile(song), getVoiceFile(song, voice),
                        (float)Common.volume, (float)Common.pitch, (float)Common.tempo, sync, ping);
                }
                else if (songPlayMode == EnumSongPlayMode.VOICEONLY)
                {
                    this.playSongEngine("", getVoiceFile(song, voice),
                        (float)Common.volume, (float)Common.pitch, (float)Common.tempo, sync, ping);
                }
                else if (songPlayMode == EnumSongPlayMode.INSTRUMENTALONLY)
                {
                    this.playSongEngine(getInstrumentalFile(song), "",
                        (float)Common.volume, (float)Common.pitch, (float)Common.tempo, sync, ping);
                }
                if (!sync)
                    this.startPlayTimer((int)engine2.currentSoundLength.TotalMilliseconds);
                else
                    this.startEndlessTimer();
                this.isSongPlaying = false;
                this.currentSong = "";
                engine1.StopAllSounds();
                engine2.StopAllSounds();
                Common.forceStop = false;
            }).Start();
        }



        public string getPlayingSong()
        {
            return currentSong;
        }

        public void startPlayTimer(int ms)
        {
            int msFromStart = 0;
            while (msFromStart < ms)
            {

                if (Common.forceStop)
                    break;

                Thread.Sleep(100);
                msFromStart += 100;
            }
            return;
        }

        private void startEndlessTimer()
        {
            while (true)
            {

                if (Common.forceStop)
                    break;

                Thread.Sleep(500);
            }
            return;
        }




    }
}
