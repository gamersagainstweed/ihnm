using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Markdown.Avalonia;
using SherpaOnnx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static ihnm.Managers.HotkeysManager;
using static ihnm.Managers.SampledVoiceManager;
using static System.Net.WebRequestMethods;


namespace ihnm.Managers
{
    public class SampledVoiceManager:IVoiceManager
    {
        public string voicesDir;

        public List<string> voices = new List<string>() { "cassie" };

        public List<string> words = new List<string>();

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

        public string[] sentenceArray;
        public int currentWord;
        public string formattedSentence = "";
        public string formattedSentence2 = "";
        public string highlightedSentence = "";

        public bool isTalking = false;
        public bool isTyping = false;

        List<string> suggestions = new List<string>();
        public int tabOffset = 0;

        public string uncompleteWord;
        public string selectedSuggestion;

        public bool insideAlias;

        public double delayRectPos = 0;
        public bool isCorrect;

        public int cursorOffset = 1;

        ProfanityFilter.ProfanityFilter filter;

        OfflineTts offlineTTS;

        System.Timers.Timer closemouthtimer;

        public SampledVoiceManager(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2,
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


            if (Common.isLipsyncOn)
            {
                closemouthtimer = new System.Timers.Timer();
                closemouthtimer.Interval = 500;
                closemouthtimer.Elapsed += Closemouthtimer_Elapsed;
                closemouthtimer.Start();
            }


        }

        private void Closemouthtimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            CallCloseMouth();
        }

        public void Dispose()
        {
            Dispatcher.UIThread.InvokeAsync(() => this.hideSuggestions());
            hotkeysManager.HotkeyTyped -= HotkeysManager_HotkeyTyped;
            closemouthtimer.Dispose();   
        }


        public class UpdateTextBlocksEventArgs : EventArgs
        {
            public MarkdownScrollViewer ttsBlock;
            public MarkdownScrollViewer ttsHighlight;
        }

        protected virtual void CallUpdateTextBlocks()
        {
            UpdateTextBlocksEventArgs e = new UpdateTextBlocksEventArgs();
            e.ttsBlock = ttsBlock;
            e.ttsHighlight = ttsHighlight;
            UpdateTextBlocks?.Invoke(this, e);
        }

        public delegate void UpdateTextBlocksEventHandler(object myObject, UpdateTextBlocksEventArgs myArgs);

        public event UpdateTextBlocksEventHandler UpdateTextBlocks;


        public void setupVoice()
        {
            this.voicesDir = "sounds/voices/" + Common.voice + "/";

            if (System.IO.File.Exists(this.voicesDir + "config/default.txt"))
            {
                this.parseConfig(this.voicesDir + "config/default.txt");
                Debug.WriteLine("config found");
            }

            aliasManager.loadAliasesForVoice();


            this.setupWords();


            setupFallbackVoice();
            
        }

        public void setupFallbackVoice()
        {
            string modeldir = "sherpa/tts-models/vits-piper-en_US-cassie-medium";

            var config = new OfflineTtsConfig();

            config.Model.Vits.Model = "./" + modeldir + "/model.onnx";
            config.Model.Vits.Tokens = "./" + modeldir + "/tokens.txt";
            config.Model.Vits.DataDir = "./" + modeldir + "/espeak-ng-data";

            config.Model.NumThreads = 4;
            config.Model.Debug = 1;
            config.Model.Provider = "cuda";
            offlineTTS = new OfflineTts(config);

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

        private void MoveItemAtIndexToFront<T>(List<T> list, int index)
        {
            T item = list[index];
            list.RemoveAt(index);
            list.Insert(0, item);
        }

        private void setupWords()
        {
            this.words = new List<string>();

            foreach (string file in Directory.GetFiles(voicesDir, "*.mp3", SearchOption.AllDirectories))
            {
                string soundname = System.IO.Path.GetFileNameWithoutExtension(file);
                this.words.Add(soundname);
            }

            foreach (string favorite in this.favoritesManager.favorites)
            {
                if (words.Contains(favorite))
                {
                    this.MoveItemAtIndexToFront(this.words, this.words.FindIndex(a => a == favorite));
                }
            }

        }

        private void HotkeysManager_HotkeyTyped(object? sender, HotkeyTypedEventArgs e)
        {
            if (!isTyping /*&& engine1.sourcesCount < 6*/)
            {
                Common.sentence = e.action+" ";
                Common.sentence2 = "";

                Common.sentence = filter.CensorString(Common.sentence.ToLower(), ' ');
                Common.sentence2 = filter.CensorString(Common.sentence2.ToLower(), ' ');

                this.currentWord = 0;

                new Thread(() =>
                {
                    this.isTalking = true; this.startTalking();
                }).Start();
              
            }
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
                        this.formattedSentence = this.formattedSentence.Substring(0, this.formattedSentence.Length - 1);
                    else
                        this.formattedSentence = this.formattedSentence + '_';
                    this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                    //this.CallUpdateTextBlocks();
                }
            }
            );
        }

        public void moveCursorLeft()
        {
            this.sentenceArray = Regex.Split(Common.sentenceFull, " ");
            foreach (var sentence in sentenceArray)
            if (this.sentenceArray.Length > this.cursorOffset)
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
            if (this.cursorOffset> 1)
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
            if (this.sentenceArray!=null && this.sentenceArray.Length>0 && this.cursorOffset>0)
            {
                Common.sentence = String.Join(" ", this.sentenceArray[..^(this.cursorOffset-1)]);
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

        private string getCoreWord(string word)
        {
            string coreWord = "";

            if (words.Contains(word))
                return word;

            if (Regex.Match(word, "^un.*").Success)
            {
                coreWord = word.Substring(2, word.Length - 2);
            }
            else if (Regex.Match(word, "^post.*").Success)
            {
                coreWord = word.Substring(4, word.Length - 4);
            }
            else if (Regex.Match(word, "^pre.*").Success)
            {
                coreWord = word.Substring(3, word.Length - 3);
            }
            else if (Regex.Match(word, "^pro.*").Success)
            {
                coreWord = word.Substring(3, word.Length - 3);
            }


            if (words.Contains(coreWord))
            {

                return coreWord;
            }

            if (coreWord != "")
                word = coreWord;

            if (Regex.Match(word, ".*ed$").Success)
            {
                coreWord = word.Substring(0, word.Length - 2);
                if (!this.words.Contains(coreWord))
                {
                    coreWord = word.Substring(0, word.Length - 1);
                }
            }
            else if (Regex.Match(word, ".*ed.$").Success)
            {
                coreWord = word.Substring(0, word.Length - 3);
                if (!this.words.Contains(coreWord))
                {
                    coreWord = word.Substring(0, word.Length - 2);
                }
            }
            else if (Regex.Match(word, ".*ing$").Success)
            {
                coreWord = word.Substring(0, word.Length - 3);
            }
            else if (Regex.Match(word, ".*ing.$").Success)
            {
                coreWord = word.Substring(0, word.Length - 4);
            }
            else if (Regex.Match(word, ".*ish$").Success)
            {
                coreWord = word.Substring(0, word.Length - 3);
            }
            else if (Regex.Match(word, ".*ish.$").Success)
            {
                coreWord = word.Substring(0, word.Length - 4);
            }
            else if (Regex.Match(word, ".*s$").Success)
            {
                coreWord = word.Substring(0, word.Length - 1);
            }
            else if (Regex.Match(word, ".*s.$").Success)
            {
                coreWord = word.Substring(0, word.Length - 2);
            }
            else if (Regex.Match(word, ".*-like$").Success)
            {
                coreWord = word.Substring(0, word.Length - 5);
            }
            else if (Regex.Match(word, ".*-like.$").Success)
            {
                coreWord = word.Substring(0, word.Length - 6);
            }

            return coreWord;
        }

        private bool isValidConcat(string word)
        {
            string[] concatWords;
            if (word.Contains('-') && !word.Contains("-like"))
            {
                concatWords = word.Split('-');
                if (concatWords.Length == 2 && this.words.Contains(getCoreWord(concatWords[0]))
                    && this.words.Contains(getCoreWord(concatWords[1])))
                {
                    return true;
                }
            }
            return false;
        }

        private bool isPartlyValidConcat(string word)
        {
            string[] concatWords;
            if (word.Contains('-') && !word.Contains("-like"))
            {
                concatWords = word.Split('-');
                if (concatWords.Length == 2 && this.words.Contains(getCoreWord(concatWords[0])) && concatWords[1].Length > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private string[] getConcatArray(string word)
        {
            string[] concatWords = word.Split('-');
            return concatWords;
        }

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
                this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                //this.CallUpdateTextBlocks();
            });
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.cursorRect.IsVisible = false;
            });

        }

        public void updateFormattedSentence()
        {
            if (Common.forceStop)
                return;



            this.formattedSentence = Common.sentence;

            if (songsManager.isSongPlaying)
            {
                this.formattedSentence = "%{color:lime}" + songsManager.currentSong + "%";
                return;
            }

            if (this.formattedSentence.StartsWith("/"))
            {
                Dispatcher.UIThread.InvokeAsync(() => this.cursorRect.IsVisible = false);
                this.formattedSentence2 = Common.sentence2;
                return;
            }

            Dispatcher.UIThread.InvokeAsync(() => this.cursorRect.IsVisible = true);
            this.CallClearOutput();


            string[] sentenceWords = Regex.Split(this.formattedSentence, " ");
            this.isCorrect = true;

            this.hideSuggestions();

            foreach (string word in sentenceWords)
            {
                if (word.Length > 0)
                {
                    string wordcopy = word;

                    string coreWord = getCoreWord(wordcopy);


                    if (aliasManager.oneLiners.ContainsKey(wordcopy))
                    {
                        this.uncompleteWord = wordcopy;
                        this.suggestAliasExpansion();
                    }
                    else
                    if (wordcopy.Length == 1 && sentenceWords[^1] == wordcopy)
                    {
                        this.uncompleteWord = wordcopy;
                        this.showSuggestions();
                    }
                    else
                    if (wordcopy[^1] != '.')
                    {




                        if (!this.words.Contains(wordcopy) && !this.words.Contains(coreWord) && !this.isValidConcat(wordcopy)
                            && !this.Soundboard.sounds.Contains(word) && !this.musicManager.music.Contains(word) &&
                            !this.loopManager.loops.Contains(word) && !aliasManager.aliasNames.ContainsKey(word))
                        {
                            //this.hideSuggestions();
                            this.formattedSentence = ReplaceLastOccurrence(formattedSentence, wordcopy, "%{color:orangered}" + wordcopy + "%");
                            this.isCorrect = false;

                            if (sentenceWords[^1] == wordcopy)
                            {
                                if (this.isPartlyValidConcat(wordcopy))
                                {
                                    this.uncompleteWord = getConcatArray(wordcopy)[1];
                                    this.showSuggestions();
                                }
                                else
                                {
                                    this.uncompleteWord = wordcopy;
                                    this.showSuggestions();
                                }
                            }


                        }

                    }
                    else
                    {
                        string wordWithoutDot = wordcopy.Substring(0, wordcopy.Length - 1);

                        if (!this.words.Contains(wordWithoutDot) && !this.words.Contains(coreWord) &&
                            !this.Soundboard.sounds.Contains(wordWithoutDot) && !this.musicManager.music.Contains(wordWithoutDot)
                            && !aliasManager.aliasNames.ContainsKey(wordWithoutDot))
                        {
                            //this.hideSuggestions();
                            this.formattedSentence = ReplaceLastOccurrence(formattedSentence, wordcopy, "%{color:orangered}" + wordcopy + "%");
                            this.isCorrect = false;

                        }
                    }
                    //this.showSuggestions();



                }
            }
            this.updateFormattedSentence2();
        }

        public void updateFormattedSentence2()
        {
            if (Common.forceStop)
                return;



            this.formattedSentence2 = Common.sentence2;

            Dispatcher.UIThread.InvokeAsync(() => this.cursorRect.IsVisible = true);
            this.CallClearOutput();


            string[] sentenceWords = Regex.Split(this.formattedSentence2, " ");
            this.isCorrect = true;



            foreach (string word in sentenceWords)
            {
                if (word.Length > 0)
                {
                    string wordcopy = word;

                    string coreWord = getCoreWord(wordcopy);


                    if (wordcopy[^1] != '.')
                    {




                        if (!this.words.Contains(wordcopy) && !this.words.Contains(coreWord) && !this.isValidConcat(wordcopy)
                            && !this.Soundboard.sounds.Contains(word) && !this.musicManager.music.Contains(word) &&
                            !this.loopManager.loops.Contains(word) && !aliasManager.aliasNames.ContainsKey(word))
                        {
                            //this.hideSuggestions();
                            this.formattedSentence2 = ReplaceLastOccurrence(formattedSentence2, wordcopy, "%{color:red}" + wordcopy + "%");
                            this.isCorrect = false;

                            if (sentenceWords[^1] == wordcopy)
                            {
                                if (this.isPartlyValidConcat(wordcopy))
                                {
                                    this.uncompleteWord = getConcatArray(wordcopy)[1];
                                }
                                else
                                {
                                    this.uncompleteWord = wordcopy;
                                }
                            }


                        }

                    }
                    else
                    {
                        string wordWithoutDot = wordcopy.Substring(0, wordcopy.Length - 1);

                        if (!this.words.Contains(wordWithoutDot) && !this.words.Contains(coreWord) &&
                            !this.Soundboard.sounds.Contains(wordWithoutDot) && !this.musicManager.music.Contains(wordWithoutDot)
                            && !aliasManager.aliasNames.ContainsKey(wordWithoutDot))
                        {
                            //this.hideSuggestions();
                            this.formattedSentence2 = ReplaceLastOccurrence(formattedSentence2, wordcopy, "%{color:red}" + wordcopy + "%");
                            this.isCorrect = false;

                        }
                    }
                    //this.showSuggestions();



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
                if (Regex.Match(word, "^%{color:red}").Success)
                {
                    word = this.extractWordFromRed(word);

                    if (this.isPartlyValidConcat(word))
                    {
                        lastword = this.getConcatArray(word)[1];
                    }
                    else
                    {
                        lastword = word;
                    }

                    if (this.selectedSuggestion != "" && removePrefix(lastword).Length < this.selectedSuggestion.Length)
                    {

                        this.formattedSentence = ReplaceLastOccurrence(formattedSentence,
                            "%{color:red}" + word + "%", "%{color:white}" + word + "%" + "%{color:gray}"
                            + this.selectedSuggestion.Substring(removePrefix(lastword).Length) + "%");
                    }
                }
                else if (Regex.Match(word, "^%{color:white}").Success)
                {
                    string justWord = extractWordFromWhite(word);

                    if (this.isPartlyValidConcat(justWord))
                    {
                        lastword = this.getConcatArray(justWord)[1];
                    }
                    else
                    {
                        lastword = justWord;
                    }

                    string greyWord = this.getGrayWord(word);

                    this.formattedSentence = ReplaceLastOccurrence(formattedSentence,
                            "%{color:white}" + justWord + "%" + greyWord, "%{color:white}" + justWord + "%" + "%{color:gray}"
                            + this.selectedSuggestion.Substring(removePrefix(lastword).Length) + "%");
                }
                else
                {

                    if (this.isPartlyValidConcat(word))
                    {
                        lastword = this.getConcatArray(word)[1];
                    }
                    else
                    {
                        lastword = word;
                    }

                    if (word.Length < this.selectedSuggestion.Length && this.selectedSuggestion.Contains(removePrefix(word)))
                    {
                        this.formattedSentence = ReplaceLastOccurrence(formattedSentence,
                                 word, "%{color:white}" + word + "%" + "%{color:gray}"
                                + this.selectedSuggestion.Substring(removePrefix(lastword).Length) + "%");
                    }
                }
            }
        }

        private string extractWordFromRed(string word)
        {
            if (word.Length > 0)
                return word.Substring(12, word.Length - 12 - 1);
            else
                return "";
        }

        private string removeGrayWord(string word)
        {
            word = Regex.Match(word, "^.*[%]{2}").Value;
            word = word.Substring(0, word.Length - 1);
            return word;
        }

        private string getGrayWord(string word)
        {
            word = Regex.Match(word, "[%]{2}.*$").Value;
            word = word.Substring(1, word.Length - 1);
            return word;
        }

        private string extractWordFromWhite(string word)
        {
            word = removeGrayWord(word);

            if (word.Length > 0)
                return word.Substring(14, word.Length - 14 - 1);
            else
                return "";
        }

        public string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);

            if (place == -1)
                return source;

            return source.Remove(place, find.Length).Insert(place, replace);
        }

        public List<string> getSuggestions(int offset = 0)
        {
            suggestions = new List<string>();

            Thread.Sleep(10);

            int curWord = 0;

            while (suggestions.Count < 12 && curWord < aliasManager.aliasNames.Count)
            {

                if (aliasManager.aliasNamesList[curWord].StartsWith((this.uncompleteWord)))
                {
                    suggestions.Add(aliasManager.aliasNamesList[curWord]);
                }

                curWord++;
            }

            curWord = 0;

            while (suggestions.Count < 12 && curWord < this.words.Count)
            {

                if (this.words[curWord].StartsWith(this.removePrefix(this.uncompleteWord)))
                {
                    suggestions.Add(this.words[curWord]);
                }

                curWord++;
            }

            curWord = 0;

            while (suggestions.Count < 12 && curWord < musicManager.music.Count)
            {

                if (musicManager.music[curWord].StartsWith((this.uncompleteWord)))
                {
                    suggestions.Add(musicManager.music[curWord]);
                }

                curWord++;
            }


            curWord = 0;

            while (suggestions.Count < 12 && curWord < loopManager.loops.Count)
            {

                if (loopManager.loops[curWord].StartsWith((this.uncompleteWord)))
                {
                    suggestions.Add(loopManager.loops[curWord]);
                }

                curWord++;
            }

            curWord = 0;

            while (suggestions.Count < 12 && curWord < Soundboard.sounds.Count)
            {

                if (Soundboard.sounds[curWord].StartsWith((this.uncompleteWord)))
                {
                    suggestions.Add(Soundboard.sounds[curWord]);
                }

                curWord++;
            }

            if (suggestions.Count > offset)
                this.selectedSuggestion = suggestions[offset];


            return suggestions;

        }

        private string removePrefix(string word)
        {
            if (word.Length > 2 && word.Substring(0, 2) == "un")
            {
                return word.Substring(2, word.Length - 2);
            }

            if (word.Length > 3 && word.Substring(0, 3) == "pre")
            {
                return word.Substring(3, word.Length - 3);
            }

            if (word.Length > 3 && word.Substring(0, 3) == "pro")
            {
                return word.Substring(3, word.Length - 3);
            }

            if (word.Length > 4 && word.Substring(0, 4) == "post")
            {
                return word.Substring(4, word.Length - 4);
            }
            return word;
        }

        public void showSuggestions(int offset = 0, bool updateSuggestions = true)
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
                    curKey = (curColumn + 1).ToString();
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

                suggestionKey = new TextBlock()
                {
                    Text = curKey,
                    Opacity = 1,
                    FontSize = 12,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };

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

        public void autoComplete()
        {

            if (this.suggestions.Count > 0 && this.selectedSuggestion != "")
            {

                if (Common.sentence[^1] == ' ')
                    return;

                Common.sentence = Regex.Match(Common.sentence, "^.*" + this.uncompleteWord).Value;


                if (aliasManager.oneLiners.ContainsKey(this.uncompleteWord))
                    Common.sentence = ReplaceLastOccurrence(Common.sentence, this.uncompleteWord, aliasManager.oneLiners[this.uncompleteWord] + " ");
                else
                    Common.sentence = ReplaceLastOccurrence(Common.sentence, this.uncompleteWord, this.selectedSuggestion + " ");

                this.tabOffset = 0;

                Dispatcher.UIThread.InvokeAsync(() => {
                    this.updateFormattedSentence();
                    //this.showSuggestions(this.tabOffset,false);
                    Dispatcher.UIThread.InvokeAsync(() => this.updateSuggestionColor(this.getPrevOffset()));
                    this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                    //this.CallUpdateTextBlocks();
                });


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
                    this.updateFormattedSentence();
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
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                    });
                }
                else
                {
                    this.selectedSuggestion = this.suggestions[index + 1];
                    Dispatcher.UIThread.InvokeAsync(() => {
                        this.updateSuggestionColor(index + 1);
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
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                        //this.CallUpdateTextBlocks();
                    });
                }
                else
                {
                    Dispatcher.UIThread.InvokeAsync(() => {
                        this.selectedSuggestion = this.suggestions[index - 1];
                        this.updateSuggestionColor(index - 1);
                        this.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
                        //this.CallUpdateTextBlocks();
                    });
                }

            }
        }

        public void suggestAliasExpansion()
        {
            string suggestStr = aliasManager.oneLiners[this.uncompleteWord];

            this.suggestionsGrid.Children.Clear();

            TextBlock from = new TextBlock() { Text = this.uncompleteWord, FontSize = 20 };
            TextBlock arrow = new TextBlock() { Text = " ⟶ ", FontSize = 20, Foreground = new SolidColorBrush(Color.Parse("DodgerBlue")) };
            TextBlock to = new TextBlock() { Text = suggestStr, FontSize = 20 };

            this.suggestionsGrid.Children.Add(from);
            this.suggestionsGrid.Children.Add(arrow);
            Grid.SetColumn(arrow, 1);
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

            HslColor newColor = new HslColor(1, hslColor.H, hslColor.S, hslColor.L + 0.3);
            return newColor.ToRgb();

        }

        public void hideSuggestions()
        {
            //this.tabOffset = 0;

            this.suggestionsGrid.Children.Clear();
        }
        public void ReadSentence()
        {

            Common.sentence = filter.CensorString(Common.sentence.ToLower(), ' ');
            Common.sentence2 = filter.CensorString(Common.sentence2.ToLower(), ' ');

            if (Common.sentenceFull.Length == 0)
                return;

            if (this.formattedSentence.Length > 0 && this.formattedSentence[^1] == '_')
                this.formattedSentence = this.formattedSentence.Substring(0, this.formattedSentence.Length - 1);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.ttsBlock.Opacity = 0.5;
                this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2;
            });

            Dispatcher.UIThread.InvokeAsync(() => { this.delayRect.IsVisible = true; this.delayRect.Opacity = 1; });

            this.tabOffset = 0;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.hideSuggestions();
            });

            
            
            System.Timers.Timer delayTimer = new System.Timers.Timer(10);

            delayTimer.Elapsed += DelayTimer_Elapsed;
            delayTimer.AutoReset = true;
            delayTimer.Enabled = true;
            delayTimer.Start();




        }

        private void DelayTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (!this.isTalking)
            {

                if ((int)this.delayRectPos == 25)
                {
                    this.isTalking = true;

                    (sender as System.Timers.Timer).Stop();

                    this.startTalking();


                }


                this.delayRectPos += 1;

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Canvas.SetLeft(this.delayRect, this.delayRectPos);

                    if (this.delayRect.Opacity > 0)
                        this.delayRect.Opacity -= 0.039;
                    if (this.delayRect.Opacity < 0.1)
                        this.delayRect.Opacity = 0;
                }
                );
            }
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
                return;
                

            string text2=RemoveSpecialCharacters(text);

            if (text2[0]==' ')
                text2=text2.Substring(1);

            if (text2.Contains("blankaudio"))
                return;

            if (Common.sentence.Length>0)
                Common.sentence += " "+text2;
            else
                Common.sentence = text2;
            Common.sentence2 = "";
            this.updateFormattedSentence();
            Dispatcher.UIThread.InvokeAsync(() => this.ttsBlock.Markdown = this.formattedSentence + this.formattedSentence2);
            //Dispatcher.UIThread.InvokeAsync(() => this.ttsBlock.Markdown = Global.sentence);
            if (Common.isSttRealtime)
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.ttsBlock.Opacity = 0.5; this.cursorRect.IsVisible = false; });

                //this.isTalking = true;
                //this.startTalking();
                this.delayRectPos = 0;
                this.ReadSentence();
            }
               
        }

        private void startTalking()
        {



                this.currentWord = 0;

                if (Common.sentenceFull.Length > 0)
                {

                    this.sentenceArray = Regex.Split(Common.sentenceFull, " ");

                    foreach (string word in sentenceArray)
                    {
                        if (Common.forceStop)
                        {
                            this.stopTalking();
                            return;
                        }

                        if (word.Length > 0)
                        {
                            this.initPlayWord(word);
                        }

                    }


                }

                if (!this.insideAlias)
                    this.stopTalking();
           
        }

        private void initPlayWord(string word)
        {
            if (word[^1] == '.')
            {
                string justWord = word.Substring(0, word.Length - 1);
                if (words.Contains(justWord))
                {
                    playWord(justWord);
                    Thread.Sleep((int)(Common.dotdelay / Common.tempo));
                }
                else if (aliasManager.aliasNames.ContainsKey(justWord))
                {
                    this.playAlias(justWord);
                }
                else if (musicManager.music.Contains(justWord))
                {
                    this.playMusic(justWord);
                }
                else if (Soundboard.sounds.Contains(justWord))
                {
                    this.playSound(justWord);
                }
                else if (loopManager.loops.Contains(justWord))
                {
                    this.playLoop(justWord);
                }
                else
                {
                    this.tryPlayComposableWord(word);
                    Thread.Sleep((int)(Common.dotdelay / Common.tempo));
                }

            }
            else
            {
                if (words.Contains(word))
                {
                    playWord(word);
                }
                else if (aliasManager.aliasNames.ContainsKey(word))
                {
                    this.playAlias(word);
                }
                else if (musicManager.music.Contains(word))
                {
                    this.playMusic(word);
                }
                else if (Soundboard.sounds.Contains(word))
                {
                    this.playSound(word);
                }
                else if (loopManager.loops.Contains(word))
                {
                    this.playLoop(word);
                }
                else
                    this.tryPlayComposableWord(word);
            }
        }

        private void playSound(string sound)
        {
            Soundboard.Play(sound);
            this.highlightSound();
            this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds) / Common.tempo));
            this.currentWord += 1;
        }

        private void playMusic(string sound)
        {
            musicManager.Play(sound);
            this.highlightMusic();
            this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds) / Common.tempo));
            this.currentWord += 1;
        }

        private void playLoop(string sound)
        {

            this.highlightLoop();
            while (true)
            {
                if (Common.forceStop)
                    break;
                loopManager.Play(sound);
                this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds) / Common.tempo));
            }

            this.currentWord += 1;
        }

        private void playAlias(string alias)
        {
            Alias al = aliasManager.aliasNames[alias];

            string prevSentence;
            string prevSentence2;
            string prevHighlightedSentence;
            int prevCurrentWord;

            this.highlightAlias();

            this.insideAlias = true;

            foreach (string line in al.aliasLines)
            {
                if (!line.StartsWith("/"))
                {
                    prevSentence = Common.sentence;
                    prevSentence2 = Common.sentence2;
                    prevHighlightedSentence = this.highlightedSentence;
                    prevCurrentWord = this.currentWord;
                    Common.sentence = line;
                    Common.sentence2 = "";
                    this.startTalking();
                    Common.sentence = prevSentence+prevSentence2;
                    Common.sentence2 = "";
                    this.currentWord = prevCurrentWord;
                    this.sentenceArray = Regex.Split(Common.sentenceFull, " ");
                }
                else
                {
                    this.CallCommandExecute(line);
                }
            }

            Thread.Sleep(20);

            this.insideAlias = false;
            this.currentWord++;


        }


        private void tryPlayComposableWord(string word)
        {

            string[] concatWord;

            if (this.isValidConcat(word))
            {
                concatWord = this.getConcatArray(word);
                this.playConcat1(concatWord[0]);
                this.playConcat2(concatWord[1]);
                return;
            }

            string coreWord = getCoreWord(word);
            if (this.words.Contains(coreWord))
            {

                if (word.Substring(0, 2) == "un")
                {
                    this.playWordNoDelay("un-");
                }
                else if (word.Length > 4 && word.Substring(0, 4) == "post")
                {
                    this.playWordNoDelay("post-");
                }
                else if (word.Substring(0, 3) == "pre")
                {
                    this.playWordNoDelay("pre-");
                }
                else if (word.Substring(0, 3) == "pro")
                {
                    this.playWordNoDelay("pro-");
                }

                this.playWordNoDelay(coreWord);

                if (coreWord.Length > 5)
                    if (coreWord.Substring(coreWord.Length - 2, 2) == "ed" ||
                    coreWord.Substring(coreWord.Length - 3, 3) == "ing" ||
                    coreWord.Substring(coreWord.Length - 1, 1) == "s" ||
                    coreWord.Substring(coreWord.Length - 3, 3) == "ish" ||
                    coreWord.Substring(coreWord.Length - 5, 5) == "-like")
                    { this.currentWord += 1; return; }

                if (coreWord.Length > 3)
                    if (coreWord.Substring(coreWord.Length - 2, 2) == "ed" ||
                     coreWord.Substring(coreWord.Length - 3, 3) == "ing" ||
                     coreWord.Substring(coreWord.Length - 1, 1) == "s" ||
                     coreWord.Substring(coreWord.Length - 3, 3) == "ish")
                    { this.currentWord += 1; return; }

                if (coreWord.Length > 2)
                    if (coreWord.Substring(coreWord.Length - 2, 2) == "ed" ||
                     coreWord.Substring(coreWord.Length - 1, 1) == "s")
                    { this.currentWord += 1; return; }

                if (coreWord.Length > 1)
                    if (coreWord.Substring(coreWord.Length - 1, 1) == "s")
                    { this.currentWord += 1; return; }

                if ((word.Length > 2 && word.Substring(word.Length - 2, 2) == "ed") || word.Substring(word.Length - 3, 3) == "ed.")
                {
                    if (coreWord[^1] == 't' || coreWord[^1] == 'd' || coreWord[^1] == 'r')
                    {
                        //Thread.Sleep(5);
                        this.playWordNoDelay("t-ed");
                    }
                    else
                    {
                        this.playWordNoDelay("-ed");
                    }
                }
                else if ((word.Length > 3 && word.Substring(word.Length - 3, 3) == "ing") || (word.Length > 4 && word.Substring(word.Length - 4, 4) == "ing."))
                {
                    this.playWordNoDelay("-ing");
                }
                else if ((word.Length > 1 && word.Substring(word.Length - 1, 1) == "s") || (word.Length > 2 && word.Substring(word.Length - 2, 2) == "s."))
                {
                    this.playWordNoDelay("-s");
                }
                else if ((word.Length > 3 && word.Substring(word.Length - 3, 3) == "ish") || (word.Length > 4 && word.Substring(word.Length - 4, 4) == "ish."))
                {
                    this.playWordNoDelay("-ish");
                }
                else if ((word.Length > 5 && word.Substring(word.Length - 5, 5) == "-like") || (word.Length > 6 && word.Substring(word.Length - 6, 6) == "-like."))
                {
                    this.playWordNoDelay("-like");
                }




                this.currentWord += 1;
            }
            else
            {
                OfflineTtsGeneratedAudio audio;

                float[] samples;

                audio = offlineTTS.Generate(word,1,0);

                try { samples = audio.Samples; }
                catch { return; }

                if (samples.Length > 0)
                {
                    new Thread(() => engine1.PlayFloatArray(samples, offlineTTS.SampleRate,
                        (float)(Common.volume * Common.playbackVolume), (float)Common.pitch, (float)Common.tempo)).Start();
                    engine2.PlayFloatArray(samples, offlineTTS.SampleRate,
                        (float)(Common.volume ), (float)Common.pitch, (float)Common.tempo);
                }

                this.highlightWord();
                this.startTalkTimer((int)((engine2.currentSoundLength.TotalMilliseconds + Common.offset) / Common.tempo));
                engine1.StopAllSounds();
                engine2.StopAllSounds();
                this.currentWord += 1;
            }
        }

        private string getWordFile(string word)
        {
            return Directory.GetFiles(this.voicesDir, word + ".mp3", SearchOption.AllDirectories)[0];
        }

        private void playWordNoDelay(string word)
        {
            engine1.PlaySound(this.getWordFile(word), (float)(Common.playbackVolume*Common.volume), (float)Common.pitch,
                (float)Common.tempo, Common.voicethreshold, true);
            engine2.PlaySound(this.getWordFile(word), (float)Common.volume, (float)Common.pitch,
                (float)Common.tempo, Common.voicethreshold, true);

            if (!word.StartsWith("-") && !word.StartsWith("t-")
                && !(word == "un-") && !(word == "post-") && !(word == "pre-") && !(word == "pro-"))
            {

                int silenceDuration = (int)engine1.endSilenceDuration.TotalMilliseconds;

                this.highlightWord();
                this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds + Common.concatoffset) / Common.tempo));
            }
            else
            {
                this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds) / Common.tempo));
            }
        }

        private void playConcat1(string word)
        {

            if (!this.words.Contains(word))
            {
                this.tryPlayComposableConcatPart(word);
                return;
            }

            engine1.PlaySound(this.getWordFile(word), (float)(Common.playbackVolume * Common.volume), (float)Common.pitch,
                (float)Common.tempo, Common.voicethreshold, true);
            engine2.PlaySound(this.getWordFile(word), (float)Common.volume, (float)Common.pitch,
                (float)Common.tempo, Common.voicethreshold, true);

            int silenceDuration = (int)engine1.endSilenceDuration.TotalMilliseconds;

            this.highlightWord();
            this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds + Common.concatoffset - 150) / Common.tempo));

        }

        private void playConcat2(string word)
        {

            if (!this.words.Contains(word))
            {
                this.tryPlayComposableWord(word);
                return;
            }

            engine1.PlaySound(this.getWordFile(word), (float)(Common.playbackVolume * Common.volume), (float)Common.pitch, (float)Common.tempo, Common.voicethreshold);
            engine2.PlaySound(this.getWordFile(word), (float)Common.volume, (float)Common.pitch, (float)Common.tempo, Common.voicethreshold);

            int silenceDuration = (int)engine1.endSilenceDuration.TotalMilliseconds;

            this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds + Common.offset) / Common.tempo));
            this.currentWord += 1;
        }

        private void tryPlayComposableConcatPart(string word)
        {

            string[] concatWord;

            string coreWord = getCoreWord(word);
            if (this.words.Contains(coreWord))
            {

                if (word.Substring(0, 2) == "un")
                {
                    this.playConcatPartNoDelay("un-");
                }
                else if (word.Substring(0, 4) == "post")
                {
                    this.playConcatPartNoDelay("post-");
                }
                else if (word.Substring(0, 3) == "pre")
                {
                    this.playConcatPartNoDelay("pre-");
                }
                else if (word.Substring(0, 3) == "pro")
                {
                    this.playConcatPartNoDelay("pro-");
                }

                this.playConcatPartNoDelay(coreWord);

                if (coreWord.Length > 5)
                    if (coreWord.Substring(coreWord.Length - 2, 2) == "ed" ||
                    coreWord.Substring(coreWord.Length - 3, 3) == "ing" ||
                    coreWord.Substring(coreWord.Length - 1, 1) == "s" ||
                    coreWord.Substring(coreWord.Length - 3, 3) == "ish" ||
                    coreWord.Substring(coreWord.Length - 5, 5) == "-like")
                    { this.currentWord += 1; return; }

                if (coreWord.Length > 3)
                    if (coreWord.Substring(coreWord.Length - 2, 2) == "ed" ||
                     coreWord.Substring(coreWord.Length - 3, 3) == "ing" ||
                     coreWord.Substring(coreWord.Length - 1, 1) == "s" ||
                     coreWord.Substring(coreWord.Length - 3, 3) == "ish")
                    { this.currentWord += 1; return; }

                if (coreWord.Length > 2)
                    if (coreWord.Substring(coreWord.Length - 2, 2) == "ed" ||
                     coreWord.Substring(coreWord.Length - 1, 1) == "s")
                    { this.currentWord += 1; return; }

                if (coreWord.Length > 1)
                    if (coreWord.Substring(coreWord.Length - 1, 1) == "s")
                    { this.currentWord += 1; return; }

                if (word.Substring(word.Length - 2, 2) == "ed" || (word.Length > 3 && word.Substring(word.Length - 3, 3) == "ed."))
                {
                    if (coreWord[^1] == 't' || coreWord[^1] == 'd' || coreWord[^1] == 'r')
                    {
                        //Thread.Sleep(5);
                        this.playConcatPartNoDelay("t-ed");
                    }
                    else
                    {
                        this.playConcatPartNoDelay("-ed");
                    }
                }
                else if (word.Substring(word.Length - 3, 3) == "ing" || word.Substring(word.Length - 4, 4) == "ing.")
                {
                    this.playConcatPartNoDelay("-ing");
                }
                else if (word.Substring(word.Length - 1, 1) == "s" || word.Substring(word.Length - 2, 2) == "s.")
                {
                    this.playConcatPartNoDelay("-s");
                }
                else if (word.Substring(word.Length - 3, 3) == "ish" || word.Substring(word.Length - 4, 4) == "ish.")
                {
                    this.playConcatPartNoDelay("-ish");
                }
                else if (word.Substring(word.Length - 5, 5) == "-like" || word.Substring(word.Length - 6, 6) == "-like.")
                {
                    this.playConcatPartNoDelay("-like");
                }

            }
        }

        private void playConcatPartNoDelay(string word)
        {
            engine1.PlaySound(this.getWordFile(word), (float)(Common.playbackVolume * Common.volume), (float)Common.pitch,
                (float)Common.tempo, Common.voicethreshold, true);
            engine2.PlaySound(this.getWordFile(word), (float)Common.volume, (float)Common.pitch,
                (float)Common.tempo, Common.voicethreshold, true);

            if (!word.StartsWith("-") && !word.StartsWith("t-")
                && !(word == "un-") && !(word == "post-") && !(word == "pre-") && !(word == "pro-"))
            {

                int silenceDuration = (int)engine1.endSilenceDuration.TotalMilliseconds;

                //this.highlightWord();
                this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds + Common.concatoffset) / Common.tempo));
            }
            else
            {
                this.startTalkTimer((int)((engine1.currentSoundLength.TotalMilliseconds + Common.concatoffset) / Common.tempo));
            }
        }

        private void playWord(string word)
        {
            new Thread(()=>engine1.PlayVoiceSound(this.getWordFile(word),
                (float)(Common.playbackVolume * Common.volume), (float)Common.pitch, (float)Common.tempo, Common.voicethreshold)).Start();
            engine2.PlayVoiceSound(this.getWordFile(word), (float)Common.volume, (float)Common.pitch, (float)Common.tempo, Common.voicethreshold);

            int silenceDuration = (int)engine1.endSilenceDuration.TotalMilliseconds;

            this.highlightWord();
            this.startTalkTimer((int)((engine2.currentSoundLength.TotalMilliseconds + Common.offset) / Common.tempo));
            this.currentWord += 1;
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

            Thread.Sleep(ms);

        }

        private void highlightWord()
        {

            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.sentenceArray.Length <= this.currentWord)
                return;

            

            int delay = (int)((int)this.engine1.currentSoundLength.TotalMilliseconds / this.sentenceArray[this.currentWord].Length * 0.5);




            string word = this.sentenceArray[this.currentWord];

            int index = 0;


            if (this.engine1.currentSoundLength.TotalMilliseconds < 350)
            {
                text += word;
                this.highlightedSentence = text;

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

                    if (text != null)
                    {

                        text += word[index];




                        if (word[index] == ' ')
                            this.delayRectPos += 15;
                        else if (word[index] == '.')
                            this.delayRectPos += 5;
                        else
                            this.delayRectPos += 17;

                        index += 1;

                        this.highlightedSentence = text;


                        Dispatcher.UIThread.InvokeAsync(() => {
                            this.ttsHighlight.Markdown = this.highlightedSentence;
                            Canvas.SetLeft(this.delayRect, this.delayRectPos);
                        });

                        Thread.Sleep((int)delay);

                    }

                }
            }).Start();

        }

        private void highlightSound()
        {
            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.sentenceArray.Length < this.currentWord ||
                this.sentenceArray.Length < this.currentWord && this.sentenceArray[this.currentWord].Length == 0)
                return;

            //int delay = (int)((int)this.engine1.currentSoundLength.TotalMilliseconds / this.sentenceArray[this.currentWord].Length * 0.5);
            string word = this.sentenceArray[this.currentWord];

            int index = 0;

            text += "%{color:lightgreen}" + word + "%" + " "; 
            this.highlightedSentence = text;

            Dispatcher.UIThread.InvokeAsync(() => {
                this.ttsHighlight.Markdown = this.highlightedSentence;
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
            });
        }

        private void highlightAlias()
        {
            if (this.insideAlias)
                return;

            string spacestring = "";

            if (this.currentWord > 0)
                spacestring = " ";

            string text = this.highlightedSentence + spacestring;

            if (this.sentenceArray.Length < this.currentWord || this.sentenceArray[this.currentWord].Length == 0)
                return;


            string word = this.sentenceArray[this.currentWord];

            int index = 0;

            text += word;
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

            if (this.sentenceArray.Length < this.currentWord || this.sentenceArray[this.currentWord].Length == 0)
                return;

            int delay = (int)((int)this.engine1.currentSoundLength.TotalMilliseconds / this.sentenceArray[this.currentWord].Length * 0.5);
            string word = this.sentenceArray[this.currentWord];

            int index = 0;

            text += "%{color:yellow}" + word + "%" + " ";
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

            if (this.sentenceArray.Length < this.currentWord || this.sentenceArray[this.currentWord].Length == 0)
                return;

            int delay = (int)((int)this.engine1.currentSoundLength.TotalMilliseconds / this.sentenceArray[this.currentWord].Length * 0.5);
            string word = this.sentenceArray[this.currentWord];

            int index = 0;

            text += "%{color:purple}" + word + "%" + " ";
            this.highlightedSentence = text;

            Dispatcher.UIThread.InvokeAsync(() => {
                this.ttsHighlight.Markdown = this.highlightedSentence;
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
            });
        }


        public void stopTalking()
        {

            if (engine1.sourcesCount < 2)
                this.isTalking = false;


            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.ttsBlock.Opacity = 1;
                Common.sentence = "";
                Common.sentence2 = "";
                this.formattedSentence = "";
                this.formattedSentence2 = "";
                this.ttsBlock.Markdown = "";
            });
            this.delayRectPos = 0;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Thread.Sleep(5);
                Canvas.SetLeft(this.delayRect, this.delayRectPos);
                this.delayRect.IsVisible = false;
                this.highlightedSentence = "";
                this.ttsHighlight.Markdown = this.highlightedSentence;
                this.cursorRect.IsVisible = false;
            }
            );
            this.selectedSuggestion = "";
            Dispatcher.UIThread.InvokeAsync(() => { this.hideSuggestions(); });
            Common.forceStop = false;

            if (Common.isLipsyncOn)
            {
                new Thread(() =>
                {
                    Thread.Sleep(500);
                    this.CallCloseMouth();
                    Common.lipsyncThreshold = 0.07;
                    Common.lipsyncDelay = TimeSpan.FromMilliseconds(70);

                });

            }
        }


        protected virtual void CallCloseMouth()
        {
            CloseMouth?.Invoke(this, null);
        }

        public event EventHandler CloseMouth;



    }
}
