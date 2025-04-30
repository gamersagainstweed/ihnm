using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Markdown.Avalonia;
using System.Timers;
using static System.Net.Mime.MediaTypeNames;
using NAudio.Wave;
//using KokoroSharp;
//using KokoroSharp.Core;
//using KokoroSharp.Utilities;
using Microsoft.ML.OnnxRuntime;
using System.Net.Http;
using NAudio.Dsp;
using System.Numerics;
using static ihnm.Managers.HotkeysManager;
using AvaloniaEdit;
using SharpCompress;
using SherpaOnnx;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Archives.Tar;
using SharpCompress.Readers;
using NAudio.CoreAudioApi;
using static ihnm.Managers.DownloadManager;
using ihnm.Enums;
using Microsoft.International.Converters.PinYinConverter;

namespace ihnm.Managers
{
    public class SherpaVoiceManager:IVoiceManager
    {

        //KokoroWavSynthesizer ttsSynth;
        //KokoroVoice voice;

        string sherpafolder = "sherpa/tts-models/";

        string highlightedSentence;

        public System.Timers.Timer cursorTimer;

        AudioPlaybackEngine engine1;
        AudioPlaybackEngine engine2;
        MarkdownScrollViewer ttsBlock;
        MarkdownScrollViewer ttsHighlight;
        Grid suggestionsGrid;
        Rectangle delayRect;
        Rectangle cursorRect;

        SongsManager songsManager;
        MusicManager musicManager;
        SoundboardManager Soundboard;
        LoopManager loopManager;
        AliasManager aliasManager;
        HotkeysManager hotkeysManager;
        FavoritesManager favoritesManager;

        public string voicesDir;

        public bool isTalking=false;
        public bool isTyping=false;
        public int delayRectPos = 0;


        public int characterCounter = 0;

        private int currentWord=0;
        private string currentHighlightedSentence;
        private string currentSentence = "";
        public string formattedSentence="";
        public string formattedSentence2="";
        string uncompleteWord;

        public string[] sentenceArray;
        public int cursorOffset = 1;

        List<string> suggestions = new List<string>();
        public string selectedSuggestion = "";
        public int tabOffset = 0;

        public bool insideAlias=false;


        private static readonly Lazy<HttpClient> httpClient = new Lazy<HttpClient>(() => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });

        private List<string> processedSentence;

        public List<string> nonWords = new List<string>();

        private MemoryStream voiceStream;

        public List<string> wordlist;

        private Dictionary<string,float[]> cachedSounds = new Dictionary<string, float[]>();

        OfflineTts offlineTTS;
        int sid;

        private static Random random = new Random();

        private string curBasevoice;

        private ProfanityFilter.ProfanityFilter filter;



        public SherpaVoiceManager(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2,
            MarkdownScrollViewer ttsBlock, MarkdownScrollViewer ttsHighlight, Grid suggestionsGrid,
            Rectangle delayRect, Rectangle cursorRect, SongsManager songsManager,
            MusicManager musicManager, SoundboardManager Soundboard, LoopManager loopManager,
            AliasManager aliasManager, HotkeysManager hotkeysManager, FavoritesManager favoritesMgr, ProfanityFilter.ProfanityFilter filter)
        {
            this.engine1 = engine1;
            this.engine2 = engine2;
            this.ttsBlock = ttsBlock;
            this.ttsHighlight = ttsHighlight;
            this.suggestionsGrid = suggestionsGrid;
            this.delayRect = delayRect;
            this.cursorRect = cursorRect;

            this.songsManager = songsManager;
            this.musicManager = musicManager;
            this.Soundboard = Soundboard;
            this.loopManager = loopManager;
            this.aliasManager = aliasManager;
            this.hotkeysManager = hotkeysManager;
            this.favoritesManager = favoritesMgr;

            this.filter = filter;

            hotkeysManager.HotkeyTyped += HotkeysManager_HotkeyTyped;


            //this.initEngDict();

        }

        public SherpaVoiceManager() {

            this.initVoiceNames();
            //this.initEngDict();


        }

        public void Dispose()
        {
            Dispatcher.UIThread.InvokeAsync(() => this.hideSuggestions());
            hotkeysManager.HotkeyTyped -= HotkeysManager_HotkeyTyped;
        }

        private void HotkeysManager_HotkeyTyped(object? sender, HotkeyTypedEventArgs e)
        {

            if (!isTyping/* && engine1.sourcesCount < 9*/)
                {
                Common.sentence = e.action;
                Common.sentence2 = "";



                new Thread(() =>
                {
                    this.isTalking = true; this.Read(true);
                }).Start();
            }
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

        private static string URL()
        {
            return "https://github.com/taylorchu/kokoro-onnx/releases/download/v0.2.0/kokoro-quant-gpu.onnx";
        }




        public static async Task<Stream> GetFileAsync(string requestUri, CancellationToken cancellationToken = default(CancellationToken))
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpResponseMessage obj = await httpClient.Value.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            obj.EnsureSuccessStatusCode();
            return await obj.Content.ReadAsStreamAsync(cancellationToken);
        }

        public async void LoadSherpa()
        {


            //ttsSynth = KokoroTTS.LoadModel(fileName);

            //ttsSynth = new KokoroWavSynthesizer(fileName);

            //KokoroVoiceManager.LoadVoicesFromPath("kokoroVoices/");

            this.initVoiceNames();
            this.initEngDict();




        }

        public string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == ' ')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public void playSTT(string text)
        {

            if (isTalking)
            {
                return;
            }
               


            string text2 = RemoveSpecialCharacters(text);

            if (text2[0] == ' ')
                text2 = text2.Substring(1);

            if (text2.Contains("blankaudio"))
                return;

            if (Common.sentence.Length > 0)
                Common.sentence += " " + text2;
            else
                Common.sentence = text2;
            Common.sentence2 = "";

            this.sentenceArray = Regex.Split(Common.sentenceFull, " ");

            Dispatcher.UIThread.InvokeAsync(() => this.updateFormattedSentence());
            Dispatcher.UIThread.InvokeAsync(() => this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2);
            //Dispatcher.UIThread.InvokeAsync(() => this.ttsBlock.Markdown = Global.sentence);
            if (Common.isSttRealtime)
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.ttsBlock.Opacity = 0.5; 
                    this.cursorRect.IsVisible = false; });

                //this.isTalking = true;
                //this.startTalking();
                this.delayRectPos = 0;
                this.ReadSentence();
            }

        }

        private void highlightWord(string word)
        {

            if (this.insideAlias)
            {
                return;
            }
               

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            if (this.currentWord >= this.processedSentence.Count)
            {
                return;
            }



            if (this.processedSentence.Count < currentWord)
                return;


            int delay = (int)((int)(this.engine2.currentSoundLength.TotalMilliseconds) / this.processedSentence[currentWord].Length * 0.4);

            int index = 0;


            if (this.engine2.currentSoundLength.TotalMilliseconds < 350)
            {
                this.highlightedSentence += word;

                Dispatcher.UIThread.InvokeAsync(() => {
                    this.ttsHighlight.Markdown = this.highlightedSentence;
                    Canvas.SetLeft(this.delayRect, this.delayRectPos);
                });
      
                return;
            }


            new Thread(() =>
            {

                while (index < word.Length)
                {

                    if (Common.forceStop)
                    {
                        break;
                    }


                    this.highlightedSentence += word[index];

                    index += 1;

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        this.ttsHighlight.Markdown = this.highlightedSentence;
                        Canvas.SetLeft(this.delayRect, this.delayRectPos);
                    });

                    Thread.Sleep((int)delay);



                }
            }).Start();


        }

        //public static KokoroVoice GetVoice(string name)
        //{
        //    if (KokoroVoiceManager.Voices.Count == 0)
        //    {
        //        KokoroVoiceManager.LoadVoicesFromPath("kokoroVoices/");
        //    }

        //    return KokoroVoiceManager.Voices.First((KokoroVoice x) => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        //}

        public void initVoiceNames()
        {

            Common.sherpaVoices = new Dictionary<string, (string, int, double, EnumLanguage, EnumGender)>();
            Common.sherpaModels = new List<string>();
            Common.sherpaModelsDict = new Dictionary<string, sherpaTTSmodel>();

            if (!Directory.Exists("sherpaVoices/"))
                Directory.CreateDirectory("sherpaVoices/");

            foreach (string dir in Directory.EnumerateDirectories(sherpafolder))
            {
                string modelname = System.IO.Path.GetFileNameWithoutExtension(dir);
                if (!File.Exists("sherpaVoices/"+modelname+".txt"))
                {
                    sherpaTTSmodel mdl = getModelById(modelname);

                    string voicebasename = RandomString(4);

                    StreamWriter sw = new StreamWriter("sherpaVoices/" + modelname+".txt");

                    for (int i=0; i<mdl.speakers;i++)
                    {
                        sw.WriteLine(voicebasename + i.ToString() + " " +i.ToString()+" "+1.ToString()+" "
                            + mdl.languages[0].ToString() +" "+"unspecified");
                    }

                    sw.Close();

                }
            }



            foreach (string file in Directory.EnumerateFiles("sherpaVoices/"))
            {
                StreamReader sr = new StreamReader(file);
                string model = System.IO.Path.GetFileNameWithoutExtension(file);
                if (!Directory.Exists(sherpafolder + model))
                    continue;
                Common.sherpaModels.Add(model);
                Common.sherpaModelsDict.Add(model, getModelById(model));
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();

                    string[] data = line.Split(new char[] { ' ' });

                    string voice = data[0]; int sid = int.Parse(data[1]);

                    double volMultiplier = double.Parse(data[2]);

                    EnumLanguage lang =(EnumLanguage)Enum.Parse(typeof(EnumLanguage), data[3]);

                    EnumGender gender = (EnumGender)Enum.Parse(typeof(EnumGender),data[4]);

                    if (!Common.sherpaVoices.ContainsKey(voice))
                    {
                        Common.sherpaVoices.Add(voice, (model, sid, volMultiplier, lang, gender));
                    }
                }
            }

        }

        public static sherpaTTSmodel getModelById(string id)
        {
            sherpaTTSmodel mdl = null;
            foreach (var ttsmdl in DownloadManager.sherpaTTSmodels)
            {
                if (ttsmdl.id == id)
                {
                    mdl = ttsmdl;
                    break;
                }
            }
            return mdl;
        }

        public static string RandomString(int length)
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToLower();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public void initEngDict()
        {
            if (this.wordlist==null)
            {
                this.wordlist = new List<string> ();
                if (File.Exists("englishdictionary/words_alpha.txt"))
                {
                    StreamReader sr = new StreamReader("englishdictionary/words_alpha.txt");
                    while (!sr.EndOfStream)
                    {
                        this.wordlist.Add(sr.ReadLine());
                    }
                }
                
            }



            if (favoritesManager != null)
            {
                foreach (string favorite in this.favoritesManager.favorites)
                {
                    if (this.wordlist.Contains(favorite))
                    {
                        this.MoveItemAtIndexToFront(this.wordlist, this.wordlist.FindIndex(a => a == favorite));
                    }
                }
            }

        }


        public void initWordlist(string lang)
        {
            if (this.wordlist == null)
            {
                this.wordlist = new List<string>();
                if (File.Exists("wordlists/"+lang+".txt"))
                {
                    StreamReader sr = new StreamReader("wordlists/" + lang+".txt");
                    while (!sr.EndOfStream)
                    {
                        this.wordlist.Add(sr.ReadLine().Split(new char[] {' ' })[0]);
                    }
                }

            }

            foreach (string alias in aliasManager.aliasNamesList)
            {
                this.wordlist.Add(alias);
            }

            foreach ( string sound in Soundboard.sounds)
            {
                this.wordlist.Add(sound);
            }

            foreach (string music in musicManager.music)
            {
                this.wordlist.Add(music);
            }

            foreach (string song in songsManager.songs)
            {
                this.wordlist.Add(song);
            }

            foreach (string loop in loopManager.loops)
            {
                this.wordlist.Add(loop);
            }

            EnumLanguage langEnum = (EnumLanguage)Enum.Parse(typeof(EnumLanguage), lang);

            if (WordCountersManager.sortedWords.ContainsKey(langEnum))
            {
                foreach (string wrd in WordCountersManager.sortedWords[langEnum])
                {
                    if (this.wordlist.Contains(wrd))
                    {
                        this.MoveItemAtIndexToFront(this.wordlist, this.wordlist.FindIndex(a => a == wrd));
                    }
                }
            }


            if (favoritesManager != null)
            {
                foreach (string favorite in this.favoritesManager.favorites)
                {
                    if (this.wordlist.Contains(favorite))
                    {
                        this.MoveItemAtIndexToFront(this.wordlist, this.wordlist.FindIndex(a => a == favorite));
                    }
                }
            }

        }

        private void MoveItemAtIndexToFront<T>(List<T> list, int index)
        {
            T item = list[index];
            list.RemoveAt(index);
            list.Insert(0, item);
        }

        //public KokoroVoice findVoice(string name)
        //{
        //    foreach(KokoroVoice voice in KokoroVoiceManager.Voices)
        //    {
        //        string voicename = voice.Name.Split(new char[] { ' ' })[^1];
        //        if (voicename == name)
        //            return voice;
        //    }
        //    return null;
        //}


        public void setupVoice()
        {
            setupVoice(Common.voice);
        }

        public void setupNoneVoice()
        {
            aliasManager.loadAliasesForVoice();

            this.setupNonWords();

            wordlist = new List<string>();

        }

        public void setupVoice(string voice)
        {
            curBasevoice = voice;

            string modeldir = sherpafolder + Common.sherpaVoices[voice].Item1;

            aliasManager.loadAliasesForVoice();

            this.setupNonWords();

            var config = new OfflineTtsConfig();

            string type="";
            if (Common.sherpaVoices[voice].Item1.Contains("kokoro"))
            {
                type = "kokoro";
            }
            else 
            if (Common.sherpaVoices[voice].Item1.Contains("vits"))
            {
                type = "vits";
            }

            if (type == "kokoro")
            {
                config.Model.Kokoro.Model = "./" + modeldir + "/model.onnx";
                config.Model.Kokoro.Voices = "./" + modeldir + "/voices.bin";
                config.Model.Kokoro.Tokens = "./" + modeldir + "/tokens.txt";
                config.Model.Kokoro.DataDir = "./" + modeldir + "/espeak-ng-data";
                config.Model.Kokoro.DictDir = "./" + modeldir + "/dict";
                config.Model.Kokoro.Lexicon = "./" + modeldir + "/lexicon-us-en.txt"+
                    ","+ "./" + modeldir + "/lexicon-zh.txt";


                config.Model.NumThreads = 4;
                config.Model.Debug = 1;
                config.Model.Provider = "gpu";

                offlineTTS = new OfflineTts(config);


                sid = Common.sherpaVoices[voice].Item2;

            }
            else
            if (type == "vits")
            {

                List<string> onnxfiles = System.IO.Directory.GetFiles(modeldir, "*.onnx").ToList();

                string onnxfile = onnxfiles[0];

                if (onnxfiles.Count>1)
                {
                    if (onnxfiles[0].Contains("int8"))
                        onnxfile = onnxfiles[1];
                    else
                        onnxfile = onnxfiles[0];
                }

                config.Model.Vits.Model = "./"+ modeldir + "/"+System.IO.Path.GetFileName(onnxfile);
                
                config.Model.Vits.Tokens = "./" + modeldir + "/tokens.txt";

                if (Directory.Exists("./" + modeldir + "/espeak-ng-data"))
                    config.Model.Vits.DataDir = "./" + modeldir + "/espeak-ng-data";

                if (Directory.Exists("./" + modeldir + "/dict"))
                    config.Model.Vits.DataDir = "./" + modeldir + "/dict";

                if (File.Exists(modeldir + "/lexicon.txt"))
                    config.Model.Vits.Lexicon = "./" + modeldir + "/lexicon.txt";

                config.Model.NumThreads = 4;
                config.Model.Debug = 1;
                config.Model.Provider = "cuda";

                offlineTTS = new OfflineTts(config);


                sid = Common.sherpaVoices[voice].Item2;

            }


            Common.voiceLanguage = Common.sherpaVoices[voice].Item4;

            this.initWordlist(Common.voiceLanguage.ToString());

            WordCountersManager.runUpdateTimer(Common.voiceLanguage);

            string voiceFolder = "sherpaVoices/" + Common.voice;


        }

        public void setupNonWords()
        {
            foreach (string alias in aliasManager.aliasNamesList)
            {
                nonWords.Add(alias);
            }
            foreach (string sound in Soundboard.sounds)
            {
                nonWords.Add(sound);
            }
            foreach (string music in musicManager.music)
            {
                nonWords.Add(music);
            }
            foreach (string loop in loopManager.loops)
            {
                nonWords.Add(loop);
            }
            foreach (string song in songsManager.songs)
            {
                nonWords.Add(song);
            }


        }

        public class CommandExecuteEventArgs : EventArgs
        {
            public string cmdString { get; set; }
        }

        protected virtual void CallCommandExecute(string cmdString)
        {
            CommandExecuteEventArgs e = new CommandExecuteEventArgs();
            e.cmdString = cmdString;
            CommandExecute?.Invoke(this, e);
        }

        public delegate void CommandExecuteEventHandler(object myObject, CommandExecuteEventArgs myArgs);

        public event CommandExecuteEventHandler CommandExecute;


        protected virtual void CallClearOutput()
        {
            ClearOutput?.Invoke(this, null);
        }

        public event EventHandler ClearOutput;

        private void parseConfig(string configFilename)
        {
            StreamReader sr = new StreamReader(configFilename);

            string rawConfig = sr.ReadToEnd();

            string[] cmdStrings = Regex.Split(rawConfig, "\r\n|\r|\n");

            foreach (string cmdString in cmdStrings)
            {
                this.CallCommandExecute(cmdString);
            }

            this.CallClearOutput();

        }

        public void ReadSentence()
        {

            if (Common.sentenceFull.Length == 0)
                return;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Common.voice != "none")
                    this.ttsBlock.Opacity = 0.5;
                else
                    this.ttsBlock.Opacity = 0;
            });

            if (this.formattedSentence.Length > 0 && this.formattedSentence[^1] == '_')
                this.formattedSentence = this.formattedSentence.Substring(0, this.formattedSentence.Length - 1);



            Dispatcher.UIThread.InvokeAsync(() => { this.delayRect.IsVisible = true; this.delayRect.Opacity = 1; 
                this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2; });


            this.tabOffset = 0;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.hideSuggestions();
            });


            System.Timers.Timer delayTimer = new System.Timers.Timer(1);

            delayTimer.Elapsed += DelayTimer_Elapsed;
            delayTimer.AutoReset = true;
            delayTimer.Enabled = true;
            delayTimer.Start();

        }

        private void DelayTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {

                if (this.delayRectPos == 25)
                {
                    this.isTalking = true;

                    (sender as System.Timers.Timer).Stop();
                    //Dispatcher.UIThread.InvokeAsync(() => this.delayRect.IsVisible = false);

                    this.startTalking();


                }


                this.delayRectPos += 1;

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Canvas.SetLeft(this.delayRect, this.delayRectPos);

                    if (this.delayRect.Opacity > 0)
                        this.delayRect.Opacity -= 0.038;
                    if (this.delayRect.Opacity < 0.1)
                        this.delayRect.Opacity = 0;
                }
                );
            
        }

        public void startTalking()
        {
            
            new Thread(() => 
            {
                this.Read();
            }
            ).Start();
        }

        public void Read(bool cache = false)
        {

            Common.sentence = filter.CensorString(Common.sentence.ToLower(),' ');
            Common.sentence2 = filter.CensorString(Common.sentence2.ToLower(), ' ');

            List<string> sentenceList = Common.sentenceFull.Split(new char[] { ' ' }).ToList();

            WordCountersManager.parseSentence(sentenceList, this.wordlist);

            processedSentence = new List<string>();

            string foundSentence = "";

            foreach (string str in sentenceList)
            {
                if (!nonWords.Contains(str))
                {
                    foundSentence += " "+str;
                }
                else
                {
                    if (foundSentence!="")
                        processedSentence.Add(foundSentence);
                    foundSentence = "";
                    processedSentence.Add(str);
                }
            }

            if (foundSentence!="" && foundSentence!=" ")
                processedSentence.Add(foundSentence);

            //if (processedSentence.Count >0 && processedSentence[^1]=="")
            //    processedSentence.Remove(processedSentence[^1]);

           

            foreach (string str in processedSentence)
            {
                if (Common.forceStop)
                {
                    break;
                }

                

                this.currentSentence = "";


                if(aliasManager.aliasNamesList.Contains(str))
                {
                    playAlias(str,cache);
                }
                else if (Soundboard.sounds.Contains(str))
                {
                    this.highlightSound();
                    Soundboard.Play(str);
                    
              
                        this.startTalkTimer((int)(engine1.currentSoundLength.TotalMilliseconds-50));
                }
                else if (musicManager.music.Contains(str))
                {
                    this.highlightMusic();
                    musicManager.Play(str);
                
                    this.startTalkTimer((int)(engine1.currentSoundLength.TotalMilliseconds - 50));
                }
                else if (loopManager.loops.Contains(str))
                {
                    this.highlightLoop();
                    loopManager.Play(str);

                    this.startEndlessTimer();
                }
                else if (songsManager.songs.Contains(str))
                {
                    this.highlightSong();
                    songsManager.Play(str,Common.voice,EnumSongPlayMode.DEFAULT);
                    return;
                    //this.startTalkTimer((int)(engine1.currentSoundLength.TotalMilliseconds - 50));
                }
                else
                {
                    if (str != "" && Common.voice!="none")
                    {


                        OfflineTtsGeneratedAudio audio;

                        float[] samples;

                        double volMult = Common.sherpaVoices[curBasevoice].Item3;

                        if (cache)
                        {
                            if (!this.cachedSounds.ContainsKey(str))
                            {

                                audio = offlineTTS.Generate(str, (float)Common.tempo, sid);

                                try { samples = audio.Samples; }
                                catch { continue; }

                                this.cachedSounds.Add(str, audio.Samples);
                            }

                            if (this.cachedSounds[str].Length > 0)
                            {
                                new Thread(() => engine1.PlayFloatArray(this.cachedSounds[str], offlineTTS.SampleRate,
                                (float)(Common.volume * volMult * Common.playbackVolume), (float)Common.pitch, 1)).Start();
                                engine2.PlayFloatArray(this.cachedSounds[str], offlineTTS.SampleRate,
                                    (float)(Common.volume * volMult), (float)Common.pitch, 1);

                            }
                        }
                        else
                        {
                            audio = offlineTTS.Generate(str, (float)Common.tempo, sid);

                            try { samples = audio.Samples; }
                            catch{ continue; }

                            
                            if (samples.Length > 0)
                            {
                                new Thread(() => engine1.PlayFloatArray(samples, offlineTTS.SampleRate,
                                    (float)(Common.volume * volMult * Common.playbackVolume), (float)Common.pitch, 1)).Start();
                                engine2.PlayFloatArray(samples, offlineTTS.SampleRate,
                                    (float)(Common.volume * volMult), (float)Common.pitch, 1);
                            }
                        }

                        this.highlightWord(str.Substring(1));

                        this.startTalkTimer((int)(engine2.currentSoundLength.TotalMilliseconds - 50));
                    }

                }

                if (!this.insideAlias)
                    this.currentWord += 1;

            }
            this.stopTalking();

        }

        public void setCursorTimer()
        {
            this.cursorTimer = new System.Timers.Timer(500);

            this.cursorTimer.Elapsed += CursorTimer_Elapsed;
            this.cursorTimer.AutoReset = true;
        }

        private void CursorTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {


            Dispatcher.UIThread.InvokeAsync(() =>
            {

                if (Common.forceStop)
                    return;

                    if (this.formattedSentence.Length > 0)
                    {
                        if (this.formattedSentence[^1] == '_')
                        {
                            this.formattedSentence = this.formattedSentence.Substring(0, this.formattedSentence.Length - 1);
                        }
                        else
                        {
                            this.formattedSentence = this.formattedSentence + '_';
                        }
                    }
                    else
                    {
                        this.formattedSentence = "_";
                    }

                    this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;

                
            }
            );
        }

        public void moveCursorLeft()
        {
            this.sentenceArray = Regex.Split(Common.sentenceFull, " ");
            if (this.sentenceArray.Length >= this.cursorOffset && this.sentenceArray[0]!="")
            {
                this.cursorOffset += 1;
                this.updateSentenceSplit();
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.updateFormattedSentence();
                    this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                });
            }
        }

        public void moveCursorRight()
        {
            this.sentenceArray = Regex.Split(Common.sentenceFull, " ");
            if (this.cursorOffset > 1)
            {
                this.cursorOffset -= 1;
                this.updateSentenceSplit();
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.updateFormattedSentence();
                    this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                });
            }
        }

        public void updateSentenceSplit()
        {
            if (this.sentenceArray != null && this.sentenceArray.Length > 0 && this.cursorOffset > 0)
            {
                Common.sentence = String.Join(" ", this.sentenceArray[..^(this.cursorOffset - 1)]);
                if (this.sentenceArray[^(this.cursorOffset - 1)..].Length > 0)
                {
                    Common.sentence2 = " " + String.Join(" ", this.sentenceArray[^(this.cursorOffset - 1)..]);
                }
                else
                {
                    Common.sentence2 = "";
                }
            }
            else
            {
                Common.sentence = Common.sentence;
                Common.sentence2 = "";
            }
        }

        private void getNextTabOffset()
        {
            if (this.tabOffset == this.suggestions.Count - 1)
            {
                this.tabOffset = 0;
            }
            else
            {
                this.tabOffset++;
            }
        }

        private void getPrevTabOffset()
        {
            if (this.tabOffset == 0)
            {
                this.tabOffset = this.suggestions.Count - 1;
            }
            else
            {
                this.tabOffset--;
            }
        }

        public void selectSuggestion(int index)
        {
            if (this.suggestions.Count > 0 && this.selectedSuggestion != "")
            {
                if (this.suggestions.Count <= index)
                    return;

                this.tabOffset = index;

                
                    this.selectedSuggestion = this.suggestions[index];
                    Dispatcher.UIThread.InvokeAsync(() => {
                        this.updateSuggestionColor(index);
                        this.updateFormattedSentence(false);
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                    });

            }
        }

        public void selectNextSuggestion()
        {
            if (this.suggestions.Count > 0 && this.selectedSuggestion != "")
            {


                int index = this.suggestions.IndexOf(this.selectedSuggestion);

                this.getNextTabOffset();

                

                if (index == suggestions.Count - 1)
                {
                    this.selectedSuggestion = this.suggestions[0];
                    Dispatcher.UIThread.InvokeAsync(() => {
                        this.updateSuggestionColor(0);
                        this.updateFormattedSentence(false);
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                    });
                }
                else
                {
                    this.selectedSuggestion = this.suggestions[index + 1];
                    Dispatcher.UIThread.InvokeAsync(() => {
                        this.updateSuggestionColor(index + 1);
                        this.updateFormattedSentence(false);
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                    });
                }

               

            }
        }

        public void selectPrevSuggestion()
        {
            if (this.suggestions.Count > 0 && this.selectedSuggestion != "")
            {
                int index = this.suggestions.IndexOf(this.selectedSuggestion);

                this.getPrevTabOffset();

                if (index == 0)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        this.selectedSuggestion = this.suggestions[suggestions.Count - 1];
                        this.updateSuggestionColor(suggestions.Count - 1);
                        this.updateFormattedSentence(false);
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                        
                    });
                }
                else
                {
                    Dispatcher.UIThread.InvokeAsync(() => {
                        this.selectedSuggestion = this.suggestions[index - 1];
                        this.updateSuggestionColor(index - 1);
                        this.updateFormattedSentence(false);
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                        
                    });
                }

            }
        }

        public void hideSuggestions()
        {
            //this.tabOffset = 0;

            this.suggestionsGrid.Children.Clear();
        }



        public List<string> getSuggestions(int offset = 0)
        {
            suggestions = new List<string>();

            Thread.Sleep(10);

            int curWord = 0;

            this.tabOffset = 0;


            curWord = 0;

            if (Common.voiceLanguage != EnumLanguage.Chinese && Common.voiceLanguage!=EnumLanguage.Japanese)
            {


                while (suggestions.Count < 12 && curWord < this.wordlist.Count)
                {

                    if (this.wordlist[curWord].StartsWith(this.uncompleteWord) 
                        && this.wordlist[curWord].Length > 2 && !filter.IsProfanity(this.wordlist[curWord].ToLower()))
                    {
                        suggestions.Add(this.wordlist[curWord]);
                    }

                    curWord++;
                }
            }
            else
            {
                //PLEASE HELP ME IMPLEMENT PINYIN
            }

            return suggestions;

        }


        public void showSuggestions(int offset = 0, bool updateSuggestions=true)
        {
            this.suggestionsGrid.Children.Clear();


            TextBlock suggestionBlock;
            TextBlock suggestionKey;
            Grid suggestionEntry;
            

            if (updateSuggestions)
                suggestions = getSuggestions(offset);

            int curColumn = 0;
            int curRow = 0;
            string curKey;

            foreach (string suggestion in suggestions)
            {


                    if (curColumn < 9)
                        curKey = (curColumn+1).ToString();
                    else if (curColumn == 9)
                        curKey = (0).ToString();
                    else
                        curKey = "";

                    suggestionEntry = new Grid();
                    suggestionEntry.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                    suggestionEntry.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                    suggestionBlock = new TextBlock() { Text = suggestion + " ", FontSize = 20 };

                    suggestionEntry.Children.Add(suggestionBlock);
                    Grid.SetRow(suggestionBlock, 0);

                    suggestionKey = new TextBlock() { Text = curKey , Opacity=1, FontSize=12,
                        HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center};

                    suggestionEntry.Children.Add(suggestionKey);
                    Grid.SetRow(suggestionKey, 1);

                    this.suggestionsGrid.Children.Add(suggestionEntry);
                    Grid.SetColumn(suggestionEntry, curColumn);
                    curColumn++;

            }
                
            if (suggestions.Count > offset)
                this.selectedSuggestion = suggestions[offset];

            if (updateSuggestions)
            {
                this.tabOffset = 0;
            }

            //if (updateSuggestions)
            this.updateSuggestionColor(offset);

        }

        public void suggestAliasExpansion()
        {
            string suggestStr = aliasManager.oneLiners[this.uncompleteWord];

            this.suggestionsGrid.Children.Clear();

            TextBlock from = new TextBlock() { Text = this.uncompleteWord, FontSize = 20 };
            TextBlock arrow = new TextBlock() { Text = " ⟶ ", FontSize=20, Foreground=new SolidColorBrush(Color.Parse("DodgerBlue")) };
            TextBlock  to = new TextBlock() { Text = suggestStr, FontSize = 20 };

            this.suggestionsGrid.Children.Add(from);
            this.suggestionsGrid.Children.Add(arrow);
            Grid.SetColumn(arrow,1);
            this.suggestionsGrid.Children.Add(to);
            Grid.SetColumn(to, 2);

        }

        private void updateSuggestionColor(int index)
        {
            for (int i = 0; i < this.suggestionsGrid.Children.Count; i++)
            {
                Grid suggestionEntry = (Grid)this.suggestionsGrid.Children[i];
                TextBlock suggestion = (TextBlock)suggestionEntry.Children[0];
                if (suggestions.Count <= i || suggestions.Count <= index)
                    return;
                string selSuggestion = this.suggestions[index];
                string curSuggestion = this.suggestions[i];
                //this.suggestionsGrid.Children.RemoveAt(i);
                if (i == index)
                {
                    if (Soundboard.sounds.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = Common.soundColor };
                    else if (musicManager.music.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = Common.musicColor };
                    else if (loopManager.loops.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = Common.loopColor };
                    else if (songsManager.songs.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = Common.songColor };
                    else
                        suggestion.Foreground = new SolidColorBrush() { Color = Avalonia.Media.Color.Parse("DodgerBlue") };
                }
                else
                {
                    if (Soundboard.sounds.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = getLighterColor(Common.soundColor) };
                    else if (musicManager.music.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = getLighterColor(Common.musicColor) };
                    else if (loopManager.loops.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = getLighterColor(Common.loopColor) };
                    else if (songsManager.songs.Contains(curSuggestion))
                        suggestion.Foreground = new SolidColorBrush() { Color = getLighterColor(Common.songColor) };
                    else
                        suggestion.Foreground = new SolidColorBrush() { Color = Avalonia.Media.Color.Parse("White") };


                }
                //this.suggestionsGrid.Children.Insert(i, suggestion);
            }

        }

        public Color getLighterColor(Color color)
        {
            HslColor hslColor = color.ToHsl();

            double newL = hslColor.L + 0.3;

            if (newL>0.9)
            {
                newL = 0.9;
            }

            HslColor newColor = new HslColor(1,hslColor.H,hslColor.S,newL);


            return newColor.ToRgb();
            
        }


        public void updateFormattedSentence(bool updateSuggestions=true)
        {
            if (Common.forceStop)
                return;



            this.formattedSentence = filter.CensorString(Common.sentence,'?');

            if (songsManager.isSongPlaying)
            {
                this.formattedSentence = "%{color:lime}" + songsManager.currentSong + "%";
                return;
            }

            if (this.formattedSentence.StartsWith("/"))
            {
                //Dispatcher.UIThread.InvokeAsync(() => this.cursorRect.IsVisible = false);
                //this.formattedSentence2 = Global.sentence2;
                return;
            }

            //Dispatcher.UIThread.InvokeAsync(() => this.cursorRect.IsVisible = true);
            this.CallClearOutput();


            string[] sentenceWords = Regex.Split(this.formattedSentence, " ");     

            this.hideSuggestions();

            this.formatNonWords(sentenceWords, ref formattedSentence);

            Debug.WriteLine(formattedSentence);


            string word = sentenceWords[^1];
                if (word.Length > 0)
                {
                    string wordcopy = word;

                    if (aliasManager.oneLiners.ContainsKey(wordcopy))
                    {
                        this.uncompleteWord = wordcopy;
                        this.suggestAliasExpansion();
                    }
                    else
                    if (wordcopy.Length == 1 && sentenceWords[^1] == wordcopy)
                    {
                        this.uncompleteWord = wordcopy;
                        this.showSuggestions(tabOffset,updateSuggestions);
                    }
                    else
                    if (wordcopy[^1] != '.')
                    {

                        this.uncompleteWord = wordcopy;
                        this.showSuggestions(tabOffset,updateSuggestions);

                    }




                }
            this.formattedSentence2 = filter.CensorString(Common.sentence2, '?');

            sentenceWords = Regex.Split(this.formattedSentence2, " ");

            this.formatNonWords(sentenceWords, ref this.formattedSentence2);
        }

        public void formatNonWords(string[] sentenceWords, ref string sentence)
        {
            foreach (string wrd in sentenceWords
             .GroupBy(c => c)
             .Select(group => group.First())
             .ToList())
            {

                if (Soundboard.sounds.Contains(wrd))
                {
                    sentence = sentence.Replace(wrd, "%{color:" + Common.soundColor + "}"
                        + wrd + "%");
                }
                else if (musicManager.music.Contains(wrd))
                {
                    sentence = sentence.Replace(wrd, "%{color:" + Common.musicColor + "}"
                        + wrd + "%");
                }
                else if (loopManager.loops.Contains(wrd))
                {
                    sentence = sentence.Replace(wrd, "%{color:" + Common.loopColor + "}"
                     + wrd + "%");
                }
                else if (songsManager.songs.Contains(wrd))
                {
                    sentence = sentence.Replace(wrd, "%{color:" + Common.songColor + "}"
                       + wrd + "%");
                }
            }
        }

        public void updateFormattedSentenceForAutoComplete()
        {
            if (Common.forceStop)
                return;

            if (this.formattedSentence.StartsWith("/"))
                return;

            if (this.suggestions.Count == 0)
                return;

            string[] sentenceWords = Regex.Split(this.formattedSentence, " ");

            string word = sentenceWords[^1];


            string lastword;



            if (word.Length > 0 && this.selectedSuggestion != "")
            {
                

                    lastword = word;

                    if (this.selectedSuggestion != "" && lastword.Length < this.selectedSuggestion.Length)
                    {
                        this.formattedSentence = ReplaceLastOccurrence(formattedSentence,
                            word,  word  + "%{color:gray}"
                            + this.selectedSuggestion.Substring(lastword.Length) + "%");
                    }
                

            }
        }

        public void autoComplete()
        {


            if (this.suggestions.Count > 0 && this.selectedSuggestion != "")
            {

                if (Common.sentence.Length==0 || Common.sentence[^1] == ' ')
                    return;

                Common.sentence = Regex.Match(Common.sentence, "^.*" + this.uncompleteWord).Value;
                

                if (aliasManager.oneLiners.ContainsKey(this.uncompleteWord) )
                    Common.sentence = ReplaceLastOccurrence(Common.sentence, this.uncompleteWord, aliasManager.oneLiners[this.uncompleteWord] + " ");
                else
                    Common.sentence = ReplaceLastOccurrence(Common.sentence, this.uncompleteWord, this.selectedSuggestion+" ");


                this.tabOffset = 0;

                Dispatcher.UIThread.InvokeAsync(() => {
                    this.updateFormattedSentence(false);
                    Dispatcher.UIThread.InvokeAsync(() => this.updateSuggestionColor(this.getPrevOffset()));
                    this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                });


            }

        }

        private int getPrevOffset()
        {
            if (this.tabOffset == 0)
            {
                return this.suggestions.Count - 1;
            }
            else
            {
                return this.tabOffset - 1;
            }
        }

        public string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);

            if (place == -1)
                return source;

            return source.Remove(place, find.Length).Insert(place, replace);
        }


        private void playAlias(string alias, bool cache=false)
        {
            Alias al = aliasManager.aliasNames[alias];

            string prevSentence;
            string prevSentence2;
            string prevHighlightedSentence;
            int prevCurrentWord;

            string[] prevProcessedSentence = new string[processedSentence.Count];

            this.highlightAlias();

            bool insideInsideAlias=false;

            if (this.insideAlias==true)
                insideInsideAlias = true;

            this.insideAlias = true;

            foreach (string line in al.aliasLines)
            {
                if (!line.StartsWith("/"))
                {
                    prevSentence = Common.sentence;
                    prevHighlightedSentence = this.highlightedSentence;
                    prevSentence2 = Common.sentence2;
                    prevProcessedSentence = processedSentence.ToArray();
                    Common.sentence = line;
                    Common.sentence2 = "";
                    this.Read(cache);
                    Common.sentence = prevSentence + prevSentence2;
                    Common.sentence2 = "";

                    this.sentenceArray = Regex.Split(Common.sentenceFull, " ");
                    processedSentence = prevProcessedSentence.ToList();
 
                }
                else
                {
                    this.CallCommandExecute(line);
                }
            }

            if (insideInsideAlias==false)
                this.insideAlias = false;


        }


        private void highlightAlias()
        {
            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.processedSentence.Count <= this.currentWord || this.processedSentence[this.currentWord].Length == 0)
                return;

            string word = this.processedSentence[this.currentWord];

            text += word+" ";
            this.highlightedSentence = text;

            Dispatcher.UIThread.InvokeAsync(() => {
                this.ttsHighlight.Markdown = this.highlightedSentence;
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
            });
        }

        private void highlightSound()
        {

            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.processedSentence.Count <= this.currentWord || this.processedSentence[this.currentWord].Length == 0)
                return;

            string word = this.processedSentence[this.currentWord];

                text += "%{color:" + Common.soundColor.ToString() + "}" + word + "%" +" ";
                this.highlightedSentence = text;

                Dispatcher.UIThread.InvokeAsync(() => {
                    this.ttsHighlight.Markdown = this.highlightedSentence;
                    Canvas.SetLeft(this.delayRect, this.delayRectPos);
                });
        }

        private void highlightMusic()
        {

            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.processedSentence.Count <= this.currentWord || this.processedSentence[this.currentWord].Length == 0)
                return;

            string word = this.processedSentence[this.currentWord];

            text += "%{color:" + Common.musicColor.ToString() + "}" + word + "%"+ " ";
            this.highlightedSentence = text;

            Dispatcher.UIThread.InvokeAsync(() => {
                this.ttsHighlight.Markdown = this.highlightedSentence;
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
            });
        }

        private void highlightLoop()
        {

            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.processedSentence.Count <= this.currentWord || this.processedSentence[this.currentWord].Length == 0)
                return;

            string word = this.processedSentence[this.currentWord];

            text += "%{color:"+Common.loopColor.ToString()+"}" + word + "%" + " ";
            this.highlightedSentence = text;

            Dispatcher.UIThread.InvokeAsync(() => {
                this.ttsHighlight.Markdown = this.highlightedSentence;
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
            });
        }

        private void highlightSong()
        {

            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.processedSentence.Count <= this.currentWord || this.processedSentence[this.currentWord].Length == 0)
                return;

            string word = this.processedSentence[this.currentWord];

            text += "%{color:" + Common.songColor.ToString() + "}" + word + "%" + " ";
            this.highlightedSentence = text;

            Dispatcher.UIThread.InvokeAsync(() => {
                this.ttsHighlight.Markdown = this.highlightedSentence;
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
            });
        }


        private void startTalkTimer(int ms)
        {
            int msFromStart = 0;

            if (ms > 4000)
            {
                while (msFromStart < ms)
                {

                    if (Common.forceStop)
                        break;

                    Thread.Sleep(500);
                    msFromStart += 500;
                }
                return;
            }

            if (ms < 10)
                ms = 10;

            Thread.Sleep(ms);
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

        public void stopTalking()
        {


            if (this.insideAlias)
                return;

            //if (engine1.sourcesCount < 2)
            this.isTalking = false;

            this.currentWord = 0;

            

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.ttsBlock.Opacity = 1;
                Common.sentence = "";
                Common.sentence2 = "";
                this.formattedSentence = "";
                this.formattedSentence2 = "";
                this.ttsBlock.Markdown = Common.sentence;
                //this.cursorRect.IsVisible = false;
                //this.CallUpdateTextBlocks();
            });
            this.delayRectPos = 0;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
                this.delayRect.IsVisible = false;
                this.highlightedSentence = "";
                this.ttsHighlight.Markdown = this.highlightedSentence;
                //this.CallUpdateTextBlocks();
            }
            );

            Common.forceStop = false;

            if (Common.isLipsyncOn)
            {
                this.CallCloseMouth();
                Common.lipsyncThreshold = 0.07;
                Common.lipsyncDelay = TimeSpan.FromMilliseconds(70);
            }


        }


        protected virtual void CallCloseMouth()
        {
            CloseMouth?.Invoke(this, null);
        }

        public event EventHandler CloseMouth;


        public void clearInput()
        {
            this.isTyping = false;
            Common.sentence = "";
            Common.sentence2 = "";
            this.updateSentenceSplit();
            this.formattedSentence = "";
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.ttsBlock.Opacity = 1;
                this.ttsBlock.Markdown = "";
                //this.CallUpdateTextBlocks();
            });
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                //this.cursorRect.IsVisible = false;
            });



        }



    }



}
