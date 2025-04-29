using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Linq;
using SoundTouch.Net.NAudioSupport;
using System.Threading;
using System.Diagnostics;
using Avalonia.Input;
using ihnm.Helpers;
using NAudio.WaveFormRenderer;
using System.Runtime.CompilerServices;
using System.IO;
using NAudio.CoreAudioApi;
using ihnm.Helpers;
using System.ComponentModel.DataAnnotations;
using Avalonia.Threading;
using ihnm.Managers;
using System.Collections;
using NAudio.Mixer;
using SharpCompress.Common;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using Microsoft.WindowsAPICodePack.Shell;
using static NAudio.Wave.SyncWasapiOut;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using System.Net;

namespace ihnm
{
    public class AudioPlaybackEngine : IDisposable
    {
        private MMDevice outputDevice;
        private MMDevice outDev;
        //private readonly MixingSampleProvider mixer;
        private static IDictionary<string, CachedSound> cachedSounds = new Dictionary<string, CachedSound>();

        public TimeSpan currentSoundLength;
        public OffsetSampleProvider currentSongSample;

        public TimeSpan endSilenceDuration;

        bool onlyvoice = false;
        bool onlyins = false;
        string vtrack = "";

        private MixingSampleProvider mixer;
        private MixingSampleProvider micMixer;

        public WasapiOut wasapiOut;
        public WasapiOut micWasapiOut;

        WasapiOut wOut2;
        SyncWasapiOut syncwout;
        LoopWasapiOut loopwout;

        List<ISampleProvider> currentVoiceTracks;

        private MeteringSampleProvider voiceSample;

        private List<TimeSpan> radioLipsyncDelays = new List<TimeSpan>();
        private List<double> radioLipsyncThresholds = new List<double>();

        private List<WaveInfo> currentWaves = new List<WaveInfo>();

        private int ping=0;
        private string currentSyncSong = "";
        private string currentSyncMusic = "";

        public ISampleProvider microphoneSample = null;


        public int sourcesCount { get { return mixer.MixerInputs.Count(); } }

        public bool isLastEngine = false;

        public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2, bool isLastEngine=false)
        {
            this.isLastEngine = isLastEngine;
            this.mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
            mixer.ReadFully = true;
            mixer.MixerInputEnded += OnMixerInputEnded;


        }

        public event EventHandler AllInputEnded;

        private void OnMixerInputEnded(object sender, SampleProviderEventArgs e)
        {
            // check if there are any inputs left
            // OnMixerInputEnded gets invoked before the corresponding source is removed from the List so there should be exactly one source left
            if (mixer.MixerInputs.Count() == 1)
            {
                AllInputEnded?.Invoke(this, EventArgs.Empty);
                
            }
            
        }

        public void Init(int deviceNumber)
        {
            if (outputDevice != null) outputDevice.Dispose();

            var output = new WaveOutEvent();
            output.DeviceNumber = deviceNumber;
            output.Init(mixer);
            output.Play();

            //outputDevice = output;
        }

        public void Init(MMDevice device)
        {
            if (outputDevice != null) outputDevice.Dispose();

            wasapiOut = new WasapiOut(device,AudioClientShareMode.Shared,false,1);
            wasapiOut.Init(mixer);
            wasapiOut.Play();

            outputDevice = device;
            outDev = device;
        }
        public void InitMic()
        {

            this.micMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            micMixer.ReadFully = true;
            //mixer.MixerInputEnded += OnMixerInputEnded;

            this.micWasapiOut = new WasapiOut(this.outDev, AudioClientShareMode.Shared, false, 1);
            this.micWasapiOut.Init(micMixer); 
            this.micWasapiOut.Play();



        }

        public class LipsyncEventArgs : EventArgs
        {
            public MeteringSampleProvider sample { get; set; }
        }
        protected virtual void CallLipsync(MeteringSampleProvider sample)
        {
            LipsyncEventArgs e = new LipsyncEventArgs();
            e.sample = sample;
            Lipsync?.Invoke(this, e);
        }
        public delegate void LipsyncEventHandler(object myObject, LipsyncEventArgs myArgs);
        public event LipsyncEventHandler Lipsync;

        public void PlaySound(string fileName, float volume = 1, float pitch = 1, float tempo=1, double silenceThreshold = -10, bool stop = false)
        {
            AudioFileReader input = new AudioFileReader(fileName);

            this.currentSoundLength = input.TotalTime;

            this.endSilenceDuration = AudioFileReaderExt.GetSilenceDuration(input, AudioFileReaderExt.SilenceLocation.End,(silenceThreshold));

            CachedSound cachedSound = null;

            if (!cachedSounds.TryGetValue(fileName, out cachedSound))
            {
                cachedSound = new CachedSound(fileName);
                
                if (!cachedSounds.ContainsKey(fileName))
                {
                    cachedSounds.Add(fileName, cachedSound);
                }
            }

            var resultingSampleProvider = new VolumeSampleProvider(
                new SoundTouchWaveProvider(
                new CachedSoundWaveProvider(cachedSound)
                )
                { Pitch = pitch, Tempo = tempo }
                .ToSampleProvider())
            { Volume=volume};

            if (stop) 
                this.StopAllSounds();
            AddMixerInput(resultingSampleProvider);

        }

        public void PlayVoiceSound(string fileName, float volume = 1, float pitch = 1, float tempo = 1, double silenceThreshold = -10, bool stop = false)
        {
            AudioFileReader input = new AudioFileReader(fileName);

            this.currentSoundLength = input.TotalTime;

            this.endSilenceDuration = AudioFileReaderExt.GetSilenceDuration(input, AudioFileReaderExt.SilenceLocation.End, (silenceThreshold));

            CachedSound cachedSound = null;

            if (!cachedSounds.TryGetValue(fileName, out cachedSound))
            {
                cachedSound = new CachedSound(fileName);
                if (!cachedSounds.ContainsKey(fileName))
                    cachedSounds.Add(fileName, cachedSound);
            }

            var resultingSampleProvider = new MeteringSampleProvider(new VolumeSampleProvider(
                new SoundTouchWaveProvider(
                new CachedSoundWaveProvider(cachedSound)
                )
                { Pitch = pitch, Tempo = tempo }
                .ToSampleProvider())
            { Volume = volume })
            { SamplesPerNotification=50};


            if (Common.isLipsyncOn)
                this.CallLipsync(resultingSampleProvider);

            if (stop)
                this.StopAllSounds();
            AddMixerInput(resultingSampleProvider);

        }

        public static byte[] GetSamplesWaveData(float[] samples, int samplesCount)
        {
            var pcm = new byte[samplesCount * 2];
            int sampleIndex = 0,
                pcmIndex = 0;

            while (sampleIndex < samplesCount)
            {
                var outsample = (short)(samples[sampleIndex] * short.MaxValue);
                pcm[pcmIndex] = (byte)(outsample & 0xff);
                pcm[pcmIndex + 1] = (byte)((outsample >> 8) & 0xff);

                sampleIndex++;
                pcmIndex += 2;
            }

            return pcm;
        }

        public void PlayFloatArray(float[] floatarray, int sampleRate, float volume = 1, float pitch = 1, float tempo = 1, double silenceThreshold = -10, bool stop = false)
        {
            byte[] array = GetSamplesWaveData(floatarray, floatarray.Length);
            WaveFormat waveFormat = new WaveFormat(sampleRate, 1);
            this.PlayArray(array, waveFormat, volume, pitch, tempo);
        }

        public void PlayArray(byte[] array, WaveFormat waveFormat, float volume = 1, float pitch = 1, float tempo = 1, double silenceThreshold = -10, bool stop = false)
        {

            BufferedWaveProvider bufferWave = new BufferedWaveProvider(waveFormat);

            bufferWave.AddSamples(array, 0, array.Length);

            Stream stream = new MemoryStream(array);
            var rs = new RawSourceWaveStream(stream, waveFormat);

            this.currentSoundLength = (rs).TotalTime;

            var newSample = bufferWave.ToSampleProvider();


            var resultingSampleProvider = new MeteringSampleProvider(
                new VolumeSampleProvider(
                new SoundTouchWaveProvider(
                new SampleToWaveProvider(newSample
                ))
                { Pitch = pitch, Tempo = tempo, }
                .ToSampleProvider())
                { Volume = volume })
            { SamplesPerNotification = 50 };

            

            if (Common.isLipsyncOn)
                this.CallLipsync(resultingSampleProvider);


            if (stop)
                this.StopAllSounds();
            AddMixerInput(resultingSampleProvider);

        }


        public void PlaySong(string fileName1, string fileName2,
            float volume = 1, float pitch = 1, float tempo = 1, bool sync = false, int ping = 0)
        {

            if (fileName1 != "")
            {
                using (AudioFileReader input1 = new AudioFileReader(fileName1))
                    this.currentSoundLength = input1.TotalTime;
            }
            else
            {
                using (AudioFileReader input2 = new AudioFileReader(fileName2))
                    this.currentSoundLength = input2.TotalTime;
            }



            CachedSound cachedSound1 = null;

            if (fileName1 != "")
            {
                if (!cachedSounds.TryGetValue(fileName1, out cachedSound1))
                {
                    cachedSound1 = new CachedSound(fileName1);
                    if (!cachedSounds.ContainsKey(fileName1))
                        cachedSounds.Add(fileName1, cachedSound1);
                }
            }

            CachedSound cachedSound2 = null;

            if (fileName2 != "")
            {
                if (!cachedSounds.TryGetValue(fileName2, out cachedSound2))
                {
                    cachedSound2 = new CachedSound(fileName2);
                    if (!cachedSounds.ContainsKey(fileName2))
                        cachedSounds.Add(fileName2, cachedSound2);
                }
            }

            VolumeSampleProvider resultingSampleProvider1 = null;
            MeteringSampleProvider resultingSampleProvider2 = null;

            if (fileName1 != "")
            {
                resultingSampleProvider1 =
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSound1)
                    )
                    { Pitch = pitch, Tempo = tempo }
                    .ToSampleProvider())
                { Volume = volume };



            }

            if (fileName2 != "")
            {
                resultingSampleProvider2 =
                    new MeteringSampleProvider(
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSound2)
                    )
                    { Pitch = pitch, Tempo = tempo }
                    .ToSampleProvider())
                { Volume = volume }
                )
                    { SamplesPerNotification = 50 };
            }




            {
                if (fileName1 != "")
                {
                    AddMixerInput(resultingSampleProvider1);
                }
                if (fileName2 != "")
                {
                    AddMixerInput(resultingSampleProvider2);
                    if (Common.isLipsyncOn)
                        this.CallLipsync(resultingSampleProvider2);
                }
            }

        }

        public void cacheSound(string filename)
        {
            CachedSound cachedSound1 = null;

            if (filename != "")
            {
                if (!cachedSounds.TryGetValue(filename, out cachedSound1))
                {
                    cachedSound1 = new CachedSound(filename);
                    if (!cachedSounds.ContainsKey(filename))
                        cachedSounds.Add(filename, cachedSound1);
                }
            }
        }

        public void PlaySongSync(string song,
            float volume = 1, float pitch = 1, float tempo = 1, int ping =0, bool onlyvoice=false, 
            bool onlyins=false, bool original=false, string vtrack="")
        {
            List<string> songs = new List<string>()
            {
              song
            };

            ping = Common.ping;

            string voicetrack;
            if (original == true)
                voicetrack = "original.mp3";
            else
            {
                if (vtrack != "")
                {
                    voicetrack = vtrack + ".mp3";
                    this.vtrack = vtrack;
                }  
                else
                    voicetrack = Common.voice + ".mp3";
            }

            this.onlyvoice = onlyvoice;
            this.onlyins = onlyins;

            radioLipsyncDelays = new List<TimeSpan>();
            radioLipsyncThresholds = new List<double>();

            foreach (string s in songs)
            {
                string path = Common.songsFolder + s + "/";

                if (File.Exists(path + "lipsyncDelay.txt"))
                {
                    StreamReader sr = new StreamReader(path + "lipsyncDelay.txt");
                    //Global.lipsyncDelay =
                    radioLipsyncDelays.Add(TimeSpan.FromMilliseconds(int.Parse(sr.ReadLine())));
                    sr.Close();
                }
                else
                {
                    radioLipsyncDelays.Add(TimeSpan.FromMilliseconds(70));
                }


                if (File.Exists(path + "lipsyncThreshold.txt"))
                {
                    StreamReader sr = new StreamReader(path + "lipsyncThreshold.txt");
                    radioLipsyncThresholds.Add(double.Parse(sr.ReadLine()));
                    sr.Close();
                }
                else
                {
                    radioLipsyncThresholds.Add(0.07);
                }

            }

            List<string> instrumentals = new List<string>();
            List<string> voicetracks = new List<string>();

            foreach (string s in songs)
            {
                string path = Common.songsFolder + s + "/";
                if (File.Exists(path + voicetrack))
                    voicetracks.Add(path + voicetrack);
                else
                    voicetracks.Add(path + "original.mp3");
                instrumentals.Add(path + "instrumental.mp3");
            }

            List<TimeSpan> lengths = new List<TimeSpan>();

            foreach (string ins in instrumentals)
            {
                TimeSpan length = GetPreciseDuration(ins);
                lengths.Add(length);
            }

            foreach (string s in instrumentals)
            {
                if (Common.forceStop)
                    return;
                this.cacheSound(s);

            }

            foreach (string s in voicetracks)
            {
                if (Common.forceStop)
                    return;
                this.cacheSound(s);
            }

            List<ISampleProvider> instrumentalSamples = new List<ISampleProvider>();
            List<ISampleProvider> voiceSamples = new List<ISampleProvider>();

            foreach (string s in instrumentals)
            {
                var instrumentalSampleProvider =
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSounds[s])
                    )
                    { Pitch = 1, Tempo = 1 }
                    .ToSampleProvider())
                { Volume = (float)Common.volume };
                instrumentalSamples.Add(instrumentalSampleProvider);
            }

            foreach (string s in voicetracks)
            {
                var voiceSampleProvider =
                new MeteringSampleProvider(
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSounds[s])
                    )
                    { Pitch = 1, Tempo = 1 }
                    .ToSampleProvider())
                { Volume = (float)Common.volume }
                )
                { SamplesPerNotification = 50 };
                voiceSamples.Add(voiceSampleProvider);
            }

            TimeSpan TotalTime = TimeSpan.Zero;

            foreach (TimeSpan span in lengths)
            {
                TotalTime += span;
            }

            if (Common.forceStop)
                return;

            this.currentSyncSong = song;

            this.AddMixerInputsListSync(instrumentalSamples, voiceSamples, TotalTime, lengths, ping, onlyvoice, onlyins);

        }

        public void PlayMusicSync(string song,
    float volume = 1, float pitch = 1, float tempo = 1, int ping = 0)
        {
            List<string> files = new List<string>()
            {
              song
            };

            for (int i = 0; i < 100; i++)
            {
                files.Add(song);
            }



            this.currentSyncMusic = song;

            radioLipsyncDelays = null;
            radioLipsyncThresholds = null;


            List<string> musics = new List<string>();

            foreach (string s in files)
            {
                musics.Add(s);
            }


            this.cacheSound(MusicManager.getMusicFile(musics[0]));

            TimeSpan TotalTime = GetPreciseDuration(MusicManager.getMusicFile(musics[0]));

            List<ISampleProvider> loopSamples = new List<ISampleProvider>();

            foreach (string s in musics)
            {
                var instrumentalSampleProvider =
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSounds[MusicManager.getMusicFile(musics[0])])
                    )
                    { Pitch = 1, Tempo = 1 }
                    .ToSampleProvider())
                { Volume = (float)Common.volume };
                loopSamples.Add(instrumentalSampleProvider);
            }



            if (Common.forceStop)
                return;


            this.AddMixerInputMusic(loopSamples, TotalTime);
        }







        public void PlayRadio(int ping=0, bool onlyvoice=false, bool onlyins=false, string vtrack="")
        {
            List<string> songs = new List<string>()
            {  
                "_wabaduba",
                "_scatmanworld",
                "_therace",
                "_numberone",
                "_scatman"
            };
            ping = Common.ping;

            radioLipsyncDelays = new List<TimeSpan>();
            radioLipsyncThresholds = new List<double>();

            this.onlyvoice = onlyvoice;
            this.onlyins = onlyins;
            this.vtrack = vtrack;


            foreach (string s in songs)
            {
                string path = Common.songsFolder + s + "/";

                if (File.Exists(path + "lipsyncDelay.txt"))
                {
                    StreamReader sr = new StreamReader(path + "lipsyncDelay.txt");
                    //Global.lipsyncDelay =
                    radioLipsyncDelays.Add(TimeSpan.FromMilliseconds(int.Parse(sr.ReadLine())));
                    sr.Close();
                }
                else
                {
                    radioLipsyncDelays.Add(TimeSpan.FromMilliseconds(70));
                }


                if (File.Exists(path + "lipsyncThreshold.txt"))
                {
                    StreamReader sr = new StreamReader(path + "lipsyncThreshold.txt");
                    radioLipsyncThresholds.Add(double.Parse(sr.ReadLine()));
                    sr.Close();
                }
                else
                {
                    radioLipsyncThresholds.Add(0.07);
                }

            }

            List<string> instrumentals = new List<string>();
            List<string> voicetracks = new List<string>();

            foreach (string s in songs)
            {
                string path = Common.songsFolder + s + "/";
                if (File.Exists(path + vtrack + ".mp3"))
                    voicetracks.Add(path + vtrack+".mp3");
                else
                    voicetracks.Add(path + "original.mp3");
                instrumentals.Add(path + "instrumental.mp3");
            }

            List<TimeSpan> lengths = new List<TimeSpan>();

            foreach (string ins in instrumentals)
            {
                TimeSpan length = GetPreciseDuration(ins);
                lengths.Add(length);
            }

            foreach (string s in instrumentals)
            {
                if (Common.forceStop)
                    return;
                this.cacheSound(s);

            }

            foreach (string s in voicetracks)
            {
                if (Common.forceStop)
                    return;
                this.cacheSound(s);
            }

            List<ISampleProvider> instrumentalSamples = new List<ISampleProvider>();
            List<ISampleProvider> voiceSamples = new List<ISampleProvider>();

            foreach (string s in instrumentals)
            {
                var instrumentalSampleProvider =
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSounds[s])
                    )
                    { Pitch = 1, Tempo = 1 }
                    .ToSampleProvider())
                    { Volume = (float) Common.volume };
                instrumentalSamples.Add(instrumentalSampleProvider);
            }

            foreach (string s in voicetracks)
            {
                var voiceSampleProvider =
                new MeteringSampleProvider(
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSounds[s])
                    )
                    { Pitch = 1, Tempo = 1 }
                    .ToSampleProvider())
                { Volume = (float)Common.volume }
                )
                    { SamplesPerNotification = 50 };
                voiceSamples.Add(voiceSampleProvider);
            }

            TimeSpan TotalTime = TimeSpan.Zero;

            foreach (TimeSpan span in lengths)
            {
                TotalTime += span;
            }

            if (Common.forceStop)
                return;

            this.currentSyncSong = "radio";

            this.AddMixerInputsListSync(instrumentalSamples, voiceSamples, TotalTime, lengths, ping,onlyvoice:onlyvoice,onlyins:onlyins);

        }

        public void PlayLoop(string loop,
    float volume = 1, float pitch = 1, float tempo = 1)
        {
            List<string> files = new List<string>()
            {
              loop
            };

            for (int i =0; i<200;i++)
            {
                files.Add(loop);
            }



            radioLipsyncDelays = null;
            radioLipsyncThresholds = null;


            List<string> loops = new List<string>();

            foreach (string s in files)
            {
                loops.Add(s);
            }


            this.cacheSound(loops[0]);


            List<ISampleProvider> loopSamples = new List<ISampleProvider>();

            foreach (string s in loops)
            {
                var instrumentalSampleProvider =
                new VolumeSampleProvider(
                    new SoundTouchWaveProvider(
                    new CachedSoundWaveProvider(cachedSounds[s])
                    )
                    { Pitch = 1, Tempo = 1 }
                    .ToSampleProvider())
                { Volume = (float)Common.volume };
                loopSamples.Add(instrumentalSampleProvider);
            }



            if (Common.forceStop)
                return;


            this.AddMixerInputLoop(loopSamples);

        }


        public int getDuration(string filename)
        {
            AudioFileReader input = new AudioFileReader(filename);

            return (int)input.TotalTime.TotalMilliseconds;
        }

        private static TimeSpan GetPreciseDuration(string filePath)
        {
            using (var shell = ShellObject.FromParsingName(Path.GetFullPath(filePath)))
            {
                IShellProperty prop = shell.Properties.System.Media.Duration;
                var t = (ulong)prop.ValueAsObject;
                return TimeSpan.FromTicks((long)t);
            }
        }


        public void StopAllSounds()
        {


            mixer.RemoveAllMixerInputs();
            if (syncwout!=null)
            {
                syncwout.PlaybackStopped -= Syncwout_PlaybackStopped;
                syncwout.PlaybackStopped -= Syncwout_PlaybackStopped1;
                syncwout.Dispose();
            }
            if (loopwout!=null)
            {
                loopwout.Dispose();
            }
            this.currentVoiceTracks = null;

            if (isLastEngine)
            {
                DisposeLargeSounds();
            }

        }

        public void DisposeLargeSounds()
        {
            foreach (KeyValuePair<string, CachedSound> kv in cachedSounds)
            {
                if (kv.Value.AudioData.Length > 5000000)
                {
  
                    cachedSounds[kv.Key].Dispose();
                    cachedSounds.Remove(kv.Key);
                }
            }

            GC.Collect();


        }


        public void stopDirOut()
        {
            if (syncwout != null)
            { syncwout.Dispose(); }
        }

        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
            {
                return input;
            }

            if (input.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }

            throw new NotImplementedException("Not yet implemented this channel count conversion");
        }

        public void AddMixerInput(ISampleProvider input)
        {
            var resampled = new WdlResamplingSampleProvider(input, mixer.WaveFormat.SampleRate);
            var convertedToRightChannelCount = ConvertToRightChannelCount(resampled);
            mixer.AddMixerInput(convertedToRightChannelCount);
        }

        public void AddMicrophoneInput(ISampleProvider input)
        {
            var resampled = new WdlResamplingSampleProvider(input, mixer.WaveFormat.SampleRate);
            var convertedToRightChannelCount = ConvertToRightChannelCount(resampled);

            micMixer.RemoveAllMixerInputs();
            micMixer.AddMixerInput(convertedToRightChannelCount);

        }

        private void AddMixerInputSync(ISampleProvider input, int ping=0)
        {
            var resampled = new WdlResamplingSampleProvider(input, mixer.WaveFormat.SampleRate);

            var convertedToRightChannelCount = ConvertToRightChannelCount(resampled);

            var mixed = new MixingSampleProvider(mixer.WaveFormat);
            mixed.AddMixerInput(convertedToRightChannelCount);


            List<ISampleProvider> samples = new List<ISampleProvider>();

            for (int i = 0; i < 15; i++)
            {
                samples.Add(mixed);
            }

            ConcatenatingSampleProvider concat = new ConcatenatingSampleProvider(samples);


            Dispatcher.UIThread.InvokeAsync(() =>
            {
                syncwout = new SyncWasapiOut(this.outDev, AudioClientShareMode.Shared, false, 1);

                syncwout.Init(concat);

                syncwout.SyncwoutLipsync += Syncwout_Lipsync;

                syncwout.Play(this.currentSoundLength, ping);
            });

        }


        private void AddMixerInputsListSync(List<ISampleProvider> instrumentals, List<ISampleProvider> voicetracks, 
            TimeSpan time, List<TimeSpan> lengths, int ping = 0, bool onlyvoice=false, bool onlyins=false)
        {
            this.currentVoiceTracks = voicetracks;

            currentWaves = new List<WaveInfo>();

            this.ping = Common.ping;


            for (int i = 0; i < instrumentals.Count; i++)
            {

                if (Common.forceStop)
                    return;

                var mixed = new MixingSampleProvider(mixer.WaveFormat);

                if (onlyins == false)
                {
                    var resampled2 = new WdlResamplingSampleProvider(voicetracks[i], mixer.WaveFormat.SampleRate);
                    var convertedToRightChannelCount2 = ConvertToRightChannelCount(resampled2);
                    mixed.AddMixerInput(convertedToRightChannelCount2);
                }
                if (onlyvoice == false)
                {
                    var resampled = new WdlResamplingSampleProvider(instrumentals[i], mixer.WaveFormat.SampleRate);
                    var convertedToRightChannelCount = ConvertToRightChannelCount(resampled);
                    mixed.AddMixerInput(convertedToRightChannelCount);

                }

                if (radioLipsyncDelays!=null)
                    currentWaves.Add(new WaveInfo(mixed.ToWaveProvider(), lengths[i], radioLipsyncDelays[i], radioLipsyncThresholds[i]));
                else
                { currentWaves.Add(new WaveInfo(mixed.ToWaveProvider(), TimeSpan.Zero, Common.lipsyncDelay, Common.lipsyncThreshold)); }

            }



            Dispatcher.UIThread.InvokeAsync(() =>
            {
                syncwout = new SyncWasapiOut(this.outDev, AudioClientShareMode.Shared, false, 0);

                syncwout.Init(currentWaves);

                syncwout.SyncwoutLipsync += Syncwout_Lipsync;

                syncwout.PlaybackStopped += Syncwout_PlaybackStopped;

                syncwout.Play(time, ping);
            });
        }


        private void AddMixerInputMusic(List<ISampleProvider> files, TimeSpan TotalTime)
        {

            currentWaves = new List<WaveInfo>();

            int ping = Common.ping;

            for (int i = 0; i < files.Count; i++)
            {

                if (Common.forceStop)
                    return;

                var mixed = new MixingSampleProvider(mixer.WaveFormat);


                var resampled = new WdlResamplingSampleProvider(files[i], mixer.WaveFormat.SampleRate);
                var convertedToRightChannelCount = ConvertToRightChannelCount(resampled);
                mixed.AddMixerInput(convertedToRightChannelCount);


                { currentWaves.Add(new WaveInfo(mixed.ToWaveProvider(), TotalTime, Common.lipsyncDelay, Common.lipsyncThreshold)); }

            }



            Dispatcher.UIThread.InvokeAsync(() =>
            {
                syncwout = new SyncWasapiOut(this.outDev, AudioClientShareMode.Shared, false, 0);

                syncwout.Init(currentWaves);

                syncwout.PlaybackStopped += Syncwout_PlaybackStopped1;

                syncwout.Play(TotalTime, ping);
            });
        }

        private void Syncwout_PlaybackStopped1(object? sender, StoppedEventArgs e)
        {
            this.PlayMusicSync(this.currentSyncMusic);
        }

        private void AddMixerInputLoop(List<ISampleProvider> files)
        {

            currentWaves = new List<WaveInfo>();


            for (int i = 0; i < files.Count; i++)
            {

                if (Common.forceStop)
                    return;

                var mixed = new MixingSampleProvider(mixer.WaveFormat);


                    var resampled = new WdlResamplingSampleProvider(files[i], mixer.WaveFormat.SampleRate);
                    var convertedToRightChannelCount = ConvertToRightChannelCount(resampled);
                    mixed.AddMixerInput(convertedToRightChannelCount);


                { currentWaves.Add(new WaveInfo(mixed.ToWaveProvider(), TimeSpan.Zero, Common.lipsyncDelay, Common.lipsyncThreshold)); }

            }



            Dispatcher.UIThread.InvokeAsync(() =>
            {
                loopwout = new LoopWasapiOut(this.outDev, AudioClientShareMode.Shared, false, 0);

                loopwout.Init(currentWaves);

                loopwout.Play();
            });
        }


        private void Syncwout_PlaybackStopped(object? sender, StoppedEventArgs e)
        {

            if (Common.forceStop)
                return;

            if (this.currentSyncSong != "radio")
            {
                this.PlaySongSync(this.currentSyncSong,onlyins:this.onlyins,onlyvoice:onlyvoice,vtrack:vtrack);
            }
            else
            {
                this.PlayRadio();
            }
        }

        private void Syncwout_Lipsync(object? sender, SyncwoutLipsyncEventArgs e)
        {
            if (Common.isLipsyncOn)
            {
                if (this.currentVoiceTracks != null)
                {
                    this.voiceSample = (MeteringSampleProvider)this.currentVoiceTracks[e.skipped];
                    Common.lipsyncDelay = currentWaves[e.skipped].lipsyncDelay;
                    Common.lipsyncThreshold = currentWaves[e.skipped].lipsyncThreshold;
                }
                this.CallLipsync(this.voiceSample);
            }
        }

        public DirectSoundDeviceInfo getDirSoundDevice(string deviceId)
        {
            List<DirectSoundDeviceInfo> devices = DirectSoundOut.Devices.ToList();

            foreach (var dev in devices)
            {
                if (dev.ModuleName == deviceId)
                {
                    return dev;
                }
            }
            return null;
        }

        public void Dispose()
        {
            if (outputDevice != null)
            {
                outputDevice.Dispose();
                outputDevice = null;
            }
        }
    }

    public class SyncMaster
    {
        public TimeSpan currentSoundLength;

        public SyncMaster(TimeSpan currentSoundLength) 
        {
            this.currentSoundLength = currentSoundLength;
        }

        public virtual void CallSyncSong()
        {
            SyncSong?.Invoke(this, null);
        }

        public event EventHandler SyncSong;


    }

}
