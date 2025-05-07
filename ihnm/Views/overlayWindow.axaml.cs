using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.TextFormatting.Unicode;
using Avalonia.Threading;
using NAudio.Wave;
using SharpHook;
using SharpHook.Native;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
using NAudio;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using static System.Net.Mime.MediaTypeNames;
using AvaloniaEdit;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ihnm.Helpers;
using ihnm.Managers;
using static ihnm.Managers.HotkeysManager;
using static System.Collections.Specialized.BitVector32;
using static System.Net.WebRequestMethods;
using System.Windows.Media.Media3D;

namespace ihnm;


public partial class overlayWindow : Window
{

    public TaskPoolGlobalHook globalHook;
    EventSimulator simulator = new EventSimulator();

    public IntPtr hwnd;
    public IntPtr prevHwnd;

    AudioPlaybackEngine audioPlaybackEngine1;
    AudioPlaybackEngine audioPlaybackEngine2;

    AudioPlaybackEngine audioPlaybackEngine3;

    SampledVoiceManager sampledVoiceManager;
    SherpaVoiceManager sherpaVoiceManager;

    IVoiceManager voiceManager;

    SoundboardManager Soundboard;
    MusicManager musicManager;
    SongsManager songsManager;
    LoopManager loopManager;
    AliasManager aliasManager;

    HotkeysManager hotkeysManager;

    MicToCableManager micToCable;

    SpeechToTextManager sttManager;

    MMDevice microphone;

    FavoritesWindow favoritesWindow;
    FavoritesManager favoritesMgr;

    LipsyncManager lipsyncManager;


    private bool ctrlPressed = false;
    private bool shiftPressed = false;
    private bool altPressed=false;


    private bool radioPlaying=false;

    private bool qPressed = false;

    private System.Timers.Timer cursorRectUpdateTimer;


    private ProfanityFilter.ProfanityFilter filter = new ProfanityFilter.ProfanityFilter();

    public overlayWindow(MMDevice virtualCableOut, MMDevice playbackDevice, MMDevice microphone=null)
    {
        InitializeComponent();
        if (Design.IsDesignMode)
            return;

        this.Loaded += OverlayWindow_Loaded;

        this.CanResize = false;
        this.Height = 80;
        this.Width = 1000;
        this.SystemDecorations = SystemDecorations.None;
        this.ShowInTaskbar = false;

        this.Topmost = true;

        this.Position = new PixelPoint(400,650);

        filter.AddProfanity(BadWords.badwords);
        filter.RemoveProfanity(BadWords.notbadwords);


        audioPlaybackEngine1 = new AudioPlaybackEngine();
        audioPlaybackEngine1.Init(playbackDevice);


        audioPlaybackEngine2 = new AudioPlaybackEngine(isLastEngine:true);
        audioPlaybackEngine2.Init(virtualCableOut);


        Soundboard = new SoundboardManager(audioPlaybackEngine1, audioPlaybackEngine2);
        musicManager = new MusicManager(audioPlaybackEngine1, audioPlaybackEngine2);
        songsManager = new SongsManager(audioPlaybackEngine1, audioPlaybackEngine2);
        songsManager.Output += SongsManager_Output;
        loopManager = new LoopManager(audioPlaybackEngine1, audioPlaybackEngine2);
        aliasManager = new AliasManager();





        //Dispatcher.UIThread.InvokeAsync(() => this.debugText.Text=this.hwnd.ToString());

        this.globalHook = new TaskPoolGlobalHook();
        globalHook.KeyTyped += GlobalHook_KeyTyped;
        globalHook.MouseWheel += GlobalHook_MouseWheel;
        globalHook.KeyPressed += GlobalHook_KeyPressed;
        globalHook.KeyReleased += GlobalHook_KeyReleased;
        globalHook.RunAsync();

        hotkeysManager = new HotkeysManager(globalHook);

        sherpaVoiceManager = new SherpaVoiceManager();
        sherpaVoiceManager.Output += KokoroVoiceManager_Output;
        sherpaVoiceManager = null;

        this.favoritesWindow = new FavoritesWindow();
        this.favoritesMgr = new FavoritesManager(hotkeysManager, this.favoritesWindow, 
            this.musicManager, this.loopManager, this.Soundboard, this.songsManager);
        this.favoritesMgr.setContext(false);
        this.favoritesWindow.Show();

        WordCountersManager.initWordCounters();

        this.initVoices();

        this.microphone = microphone;
             
    }

    private void SongsManager_Output(object myObject, SongsManager.OutputEventArgs myArgs)
    {
        this.outputMessage(myArgs.text);
    }

    private void KokoroVoiceManager_Output(object myObject, SherpaVoiceManager.OutputEventArgs myArgs)
    {
        this.outputMessage(myArgs.text);
    }

    private void SttManager_Output(object myObject, SpeechToTextManager.OutputEventArgs myArgs)
    {
        this.outputMessage(myArgs.text);
    }

    private void OverlayWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        
        if (this.microphone != null && Common.sttEnabled)
        {

                sttManager = new SpeechToTextManager(this.microphone);
                sttManager.Output += SttManager_Output;
                sttManager.SegmentRecognized += SttManager_SegmentRecognized;
           
        }



        if (this.microphone!=null && Common.enableMicToCable)
        {

                this.micToCable = new MicToCableManager(this.audioPlaybackEngine2, this.microphone);

        }

        this.setupVoice();


        cursorRectUpdateTimer = new System.Timers.Timer();
        cursorRectUpdateTimer.Interval = 70;
        cursorRectUpdateTimer.Elapsed += CursorRectUpdateTimer_Elapsed;
        cursorRectUpdateTimer.Start();
        

        this.hwnd = this.TryGetPlatformHandle().Handle;
    }

    private void CursorRectUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (GetForegroundWindow()==this.hwnd)
        {
            Dispatcher.UIThread.InvokeAsync(() => this.cursorRect.IsVisible = true);
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(() => this.cursorRect.IsVisible = false);
            if (sampledVoiceManager != null)
                sampledVoiceManager.isTyping = false;
            else if (sherpaVoiceManager != null)
                sherpaVoiceManager.isTyping = false;
        }
    }

    private void SampledVoiceManager_CloseMouth(object? sender, EventArgs e)
    {
        this.lipsyncManager.CloseMouth();
    }

    private void KokoroVoiceManager_CloseMouth(object? sender, EventArgs e)
    {
        this.lipsyncManager.CloseMouth();
    }

    private void LipsyncManager_Output(object myObject, LipsyncManager.OutputEventArgs myArgs)
    {
        this.outputMessage(myArgs.text);
    }

    private void SttManager_SegmentRecognized(object myObject, SpeechToTextManager.SegmentRecognizedEventArgs myArgs)
    {
        if (sampledVoiceManager != null)
            sampledVoiceManager.playSTT(myArgs.SegmentText.ToLower());
        else if (sherpaVoiceManager != null)
            sherpaVoiceManager.playSTT(myArgs.SegmentText.ToLower());
    }

    public void initVoices()
    {
        if (!Directory.Exists("sounds/voices/"))
            Directory.CreateDirectory("sounds/voices/");

        List<string> voiceDirs = Directory.GetDirectories("sounds/voices/").ToList();

        foreach (string voiceDir in voiceDirs)
        {
            string filename = System.IO.Path.GetFileNameWithoutExtension(voiceDir);
            Common.voices.Add(filename);
        }
    }

    public void setupVoice()
    {
        if (sampledVoiceManager != null)
        {
            sampledVoiceManager.cursorTimer.Stop();
            sampledVoiceManager.Dispose();
        }
        if (sherpaVoiceManager != null)
        {
            sherpaVoiceManager.cursorTimer.Stop();
            sherpaVoiceManager.Dispose();
        }
        if (lipsyncManager!=null)
        {
            lipsyncManager.Dispose();
        }

        Common.volume = 1;
        Common.pitch = 1;
        Common.tempo = 1;

        sampledVoiceManager = null;
        sherpaVoiceManager = null;
        lipsyncManager = null;
        if (Common.voices.Contains(Common.voice))
        {

            sampledVoiceManager = new SampledVoiceManager(audioPlaybackEngine1, audioPlaybackEngine2,
            this.ttsBlock, this.ttsHighlight, this.suggestionsGrid, this.delayRect, this.cursorRect,
            songsManager, musicManager, Soundboard, loopManager, aliasManager, hotkeysManager, favoritesMgr, filter);

            sampledVoiceManager.setCursorTimer();
            sampledVoiceManager.CommandExecute += SampledVoiceManager_CommandExecute;
            sampledVoiceManager.ClearOutput += SampledVoiceManager_ClearOutput;


            sampledVoiceManager.setupVoice();
        }
        else if (Common.sherpaVoices.ContainsKey(Common.voice)) 
        {
            sherpaVoiceManager = new SherpaVoiceManager(audioPlaybackEngine1, audioPlaybackEngine2,
            this.ttsBlock, this.ttsHighlight, this.suggestionsGrid, this.delayRect, this.cursorRect,
            songsManager, musicManager, Soundboard, loopManager, aliasManager, hotkeysManager, favoritesMgr, filter);

            sherpaVoiceManager.setCursorTimer();
            sherpaVoiceManager.Output += KokoroVoiceManager_Output;
            sherpaVoiceManager.CommandExecute += KokoroVoiceManager_CommandExecute;
            sherpaVoiceManager.ClearOutput += SampledVoiceManager_ClearOutput;

            sherpaVoiceManager.setupVoice();
        }
        else
        {
            sherpaVoiceManager = new SherpaVoiceManager(audioPlaybackEngine1, audioPlaybackEngine2,
            this.ttsBlock, this.ttsHighlight, this.suggestionsGrid, this.delayRect, this.cursorRect,
            songsManager, musicManager, Soundboard, loopManager, aliasManager, hotkeysManager, favoritesMgr, filter);

            sherpaVoiceManager.setCursorTimer();
            sherpaVoiceManager.Output += KokoroVoiceManager_Output;
            sherpaVoiceManager.CommandExecute += KokoroVoiceManager_CommandExecute;
            sherpaVoiceManager.ClearOutput += SampledVoiceManager_ClearOutput;

            sherpaVoiceManager.setupNoneVoice();
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Common.isLipsyncOn)
            {

                    this.lipsyncManager = new LipsyncManager(audioPlaybackEngine1, audioPlaybackEngine2, this.micToCable);
                    this.lipsyncManager.Output += LipsyncManager_Output;
                    if (this.sherpaVoiceManager != null)
                        this.sherpaVoiceManager.CloseMouth += KokoroVoiceManager_CloseMouth;
                    else if (this.sampledVoiceManager != null)
                        this.sampledVoiceManager.CloseMouth += SampledVoiceManager_CloseMouth;

            
            }
            if (this.micToCable != null)
            {
                if (micToCable.Sound == null)
                    micToCable.startMicToCable();
                else
                    micToCable.renewLipsync();

            }
        });

    }

    private void KokoroVoiceManager_CommandExecute(object myObject, SherpaVoiceManager.CommandExecuteEventArgs myArgs)
    {
        this.executeCommand(myArgs.cmdString);
    }

    private void SampledVoiceManager_UpdateTextBlocks(object myObject, SampledVoiceManager.UpdateTextBlocksEventArgs myArgs)
    {
            this.ttsBlock = myArgs.ttsBlock;
            this.ttsHighlight = myArgs.ttsHighlight;
    }

    private void SampledVoiceManager_ClearOutput(object? sender, EventArgs e)
    {
        this.clearOutput();
    }

    private void SampledVoiceManager_CommandExecute(object myObject, SampledVoiceManager.CommandExecuteEventArgs myArgs)
    {
        this.executeCommand(myArgs.cmdString);
    }

    private List<KeyCode> digitKeys = new List<KeyCode>()
    { 
        KeyCode.VcNumPad0,
        KeyCode.VcNumPad1,
        KeyCode.VcNumPad2,
        KeyCode.VcNumPad3,
        KeyCode.VcNumPad4,
        KeyCode.VcNumPad5,
        KeyCode.VcNumPad6,
        KeyCode.VcNumPad7,
        KeyCode.VcNumPad8,
        KeyCode.VcNumPad9,
        KeyCode.Vc0,
        KeyCode.Vc1,
        KeyCode.Vc2,
        KeyCode.Vc3,
        KeyCode.Vc4,
        KeyCode.Vc5,
        KeyCode.Vc6,
        KeyCode.Vc7,
        KeyCode.Vc8,
        KeyCode.Vc9
    };

    public bool invokeSpecialCondition()
    {
        if (Common.invokeSpecialKey == KeyCode.VcUndefined)
            return true;
        else if (Common.invokeSpecialKey == KeyCode.VcLeftControl)
            return this.ctrlPressed;
        else if (Common.invokeSpecialKey == KeyCode.VcLeftShift)
            return this.shiftPressed;
        else if (Common.invokeSpecialKey == KeyCode.VcLeftAlt)
            return this.altPressed;

        return false;
    }

    private void GlobalHook_KeyTyped(object? sender, KeyboardHookEventArgs e)
    {

        if (e.Data.KeyCode==KeyCode.VcEscape)
        {
            if (GetForegroundWindow()==this.hwnd)
                SetForegroundWindow(prevHwnd);

        }

        if (digitKeys.Contains(e.Data.KeyCode) && this.altPressed == false && !Regex.Match(Common.sentenceFull, "^/.*").Success)
        {
            return;
        }

        if (e.Data.KeyCode==Common.invokeKey && invokeSpecialCondition())
        {

            if (sampledVoiceManager != null)
            {
                
                if (sampledVoiceManager.isTalking == false && sampledVoiceManager.isTyping == false)
                {
                    if (!(this.hwnd == GetForegroundWindow()))
                    {
                        this.prevHwnd = GetForegroundWindow();
                        //this.outputInfoMessage(this.prevHwnd.ToString());
                        setForeground(hwnd);

                        this.favoritesMgr.setContext(true);
                        //if (!success)
                        //{
                        //    this.raiseError("cannot set interface window to foreground");
                        //    return;
                        //}
                        //this.outputInfoMessage(this.prevHwnd.ToString() +" "+ GetForegroundWindow().ToString()+
                        //    " " +this.hwnd.ToString());
                        sampledVoiceManager.isTyping = true;
                        sampledVoiceManager.cursorTimer.Start();

                        return;
                    }
                }
                else if (sampledVoiceManager.isTyping == false)
                {
                    this.prevHwnd = GetForegroundWindow();
                    setForeground(hwnd);

                    return;
                }
            }
            else if (sherpaVoiceManager != null) 
            {
                if (sherpaVoiceManager.isTalking == false && sherpaVoiceManager.isTyping == false)
                {
                    if (!(this.hwnd == GetForegroundWindow()))
                    {
                        this.prevHwnd = GetForegroundWindow();

                        setForeground(hwnd);
                        this.favoritesMgr.setContext(true);

                        sherpaVoiceManager.isTyping = true;
                        sherpaVoiceManager.cursorTimer.Start();

                        return;
                    }
                }
                else if (sherpaVoiceManager.isTyping == false)
                {
                    this.prevHwnd = GetForegroundWindow();
                    setForeground(hwnd);

                    return;
                }
            }
        }

        if (e.Data.KeyCode == KeyCode.VcEnter && (this.hwnd == GetForegroundWindow()))
        {


            this.favoritesMgr.setContext(false);

            audioPlaybackEngine1.StopAllSounds();
            audioPlaybackEngine2.StopAllSounds();


            if (this.hwnd == GetForegroundWindow())
                SetForegroundWindow(prevHwnd);

            if (sampledVoiceManager != null)
            {

                if (Common.sentenceFull == "")
                {
                    sampledVoiceManager.stopTalking();
                }

                if (Common.sentenceFull.StartsWith("/"))
                {
                    sampledVoiceManager.cursorTimer.Stop();
                    this.executeCommand();
                    Common.sentence = "";
                    Common.sentence2 = "";
                    
                    return;
                }

                if (sampledVoiceManager.isTalking)
                {
                    audioPlaybackEngine1.StopAllSounds();
                    audioPlaybackEngine2.StopAllSounds();
                    Common.forceStop = true;
                }
                else if (songsManager.isSongPlaying)
                {
                    audioPlaybackEngine1.StopAllSounds();
                    audioPlaybackEngine2.StopAllSounds();
                    Common.forceStop = true;
                }
                else if (this.radioPlaying)
                {
                    audioPlaybackEngine1.StopAllSounds();
                    audioPlaybackEngine2.StopAllSounds();
                    Common.forceStop = true;
                    this.radioPlaying = false;
                }
                else
                {
                    sampledVoiceManager.isTyping = false;

                    sampledVoiceManager.cursorTimer.Stop();


                    sampledVoiceManager.cursorOffset = 1;


                    if (sampledVoiceManager.isCorrect)
                        sampledVoiceManager.ReadSentence();
                    else
                        sampledVoiceManager.stopTalking();
                }
            }
            else if (sherpaVoiceManager!=null)
            {
                if (Common.sentenceFull == "")
                {
                    sherpaVoiceManager.stopTalking();
                }

                if (Common.sentence.StartsWith("/"))
                {
                    sherpaVoiceManager.cursorTimer.Stop();
                    this.executeCommand();
                    Common.sentence = "";
                    Common.sentence2 = "";
                   
                    return;
                }

                if (sherpaVoiceManager.isTalking)
                {
                    audioPlaybackEngine1.StopAllSounds();
                    audioPlaybackEngine2.StopAllSounds();
                    Common.forceStop = true;
                }
                else if (songsManager.isSongPlaying)
                {
                    Common.forceStop = true;
                }
                else
                {

                    sherpaVoiceManager.isTyping = false;

                    sherpaVoiceManager.cursorTimer.Stop();
                    sherpaVoiceManager.cursorOffset = 1;

                    sherpaVoiceManager.ReadSentence();
                }
            }
            return;
        }



        if (sampledVoiceManager != null && sampledVoiceManager.isTyping)
        {
            if (Char.IsLetterOrDigit(e.Data.KeyChar) || e.Data.KeyChar == '.' ||
                e.Data.KeyCode == KeyCode.VcSpace || e.Data.KeyCode == KeyCode.VcSlash
                || e.Data.KeyCode == KeyCode.VcMinus)
            {

                if (Regex.Match(Common.sentenceFull, "^/h new.*").Success || Regex.Match(Common.sentenceFull, "^/hotkey new.*").Success)
                {
                    string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                    if (sentenceArray.Length == 4)
                    {
                        //Global.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else if (sentenceArray.Length == 5)
                    {
                        //Global.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else
                    {
                        if (e.Data.KeyCode == KeyCode.VcSpace && Common.sentence.Length > 0 && Common.sentence[^1] == ' ')
                        {

                        }
                        else
                            Common.sentence += e.Data.KeyChar;
                    }
                }
                else if (Regex.Match(Common.sentenceFull, "^/bind.*").Success)
                {
                    string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                    if (sentenceArray.Length == 3)
                    {
                        //Global.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else if (sentenceArray.Length == 4)
                    {
                        //Global.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else
                    {
                        if (e.Data.KeyCode == KeyCode.VcSpace && Common.sentence.Length > 0 && Common.sentence[^1] == ' ')
                        {

                        }
                        else
                            Common.sentence += e.Data.KeyChar;
                    }

                }
                else
                {
                    if (e.Data.KeyCode == KeyCode.VcSpace && Common.sentence.Length > 0 && Common.sentence[^1] == ' ')
                    {

                    }
                    else
                        Common.sentence += e.Data.KeyChar;
                }

                sampledVoiceManager.sentenceArray = Regex.Split(Common.sentenceFull, " ");
                sampledVoiceManager.updateSentenceSplit();

                if (e.Data.KeyCode == KeyCode.VcSpace)
                {
                    sampledVoiceManager.tabOffset = 0;
                }


                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    sampledVoiceManager.updateFormattedSentence();
                    sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                    this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence+sampledVoiceManager.formattedSentence2;
                });
            }
            else if (e.Data.KeyCode == KeyCode.VcBackspace && Common.sentence.Length > 0)
            {
                Common.sentence = Common.sentence.Remove(Common.sentence.Length - 1,1);
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    sampledVoiceManager.sentenceArray = Regex.Split(Common.sentenceFull, " ");
                    sampledVoiceManager.updateSentenceSplit();
                    sampledVoiceManager.updateFormattedSentence();
                    sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                    this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence+sampledVoiceManager.formattedSentence2;
                });
            }
            else if (e.Data.KeyCode == KeyCode.VcTab && Common.sentence.Length > 0)
            {
                sampledVoiceManager.autoComplete();
            }

        }

        else if (sherpaVoiceManager != null && sherpaVoiceManager.isTyping) 
        {

            if (Char.IsLetterOrDigit(e.Data.KeyChar) || e.Data.KeyChar == '.' ||
            e.Data.KeyCode == KeyCode.VcSpace || e.Data.KeyCode == KeyCode.VcSlash
            || e.Data.KeyCode == KeyCode.VcMinus)
            {


                if (Regex.Match(Common.sentenceFull, "^/h new.*").Success || Regex.Match(Common.sentenceFull, "^/hotkey new.*").Success)
                {
                    string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                    if (sentenceArray.Length == 4)
                    {
                        //Global.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else if (sentenceArray.Length == 5)
                    {
                        //Global.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else
                    {
                        if (e.Data.KeyCode == KeyCode.VcSpace && Common.sentence.Length > 0 && Common.sentence[^1] == ' ')
                        {

                        }
                        else
                            Common.sentence += e.Data.KeyChar;
                    }
                }
                else if (Regex.Match(Common.sentenceFull, "^/bind.*").Success)
                {
                    string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                    if (sentenceArray.Length == 3)
                    {
                        //Global.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else if (sentenceArray.Length == 4)
                    {
                        //Global.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    }
                    else
                    {
                        if (e.Data.KeyCode == KeyCode.VcSpace && Common.sentence.Length > 0 && Common.sentence[^1] == ' ')
                        {

                        }
                        else
                            Common.sentence += e.Data.KeyChar;
                    }

                }
                else
                {
                    if (e.Data.KeyCode == KeyCode.VcSpace && Common.sentence.Length > 0 && Common.sentence[^1] == ' ')
                    {

                    }
                    else
                        Common.sentence += e.Data.KeyChar;
                }

                if (e.Data.KeyCode == KeyCode.VcSpace)
                {
                    sherpaVoiceManager.tabOffset = 0;

                }

                sherpaVoiceManager.sentenceArray = Regex.Split(Common.sentenceFull, " ");
                sherpaVoiceManager.updateSentenceSplit();

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    sherpaVoiceManager.updateFormattedSentence();
                    sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                    this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                });
            }
            else if (e.Data.KeyCode == KeyCode.VcBackspace && Common.sentence.Length > 0)
            {
                Common.sentence = Common.sentence.Remove(Common.sentence.Length - 1);

                //sampledVoiceManager.sentenceArray = Regex.Split(Global.sentence, " ");


                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    sherpaVoiceManager.updateFormattedSentence();
                    sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                    this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                });
            }
            else if (e.Data.KeyCode == KeyCode.VcTab && Common.sentence.Length > 0)
            {
                sherpaVoiceManager.autoComplete();
            }


        }
    }

    private void GlobalHook_KeyPressed(object? sender, KeyboardHookEventArgs e)
    {


        if (e.Data.KeyCode==KeyCode.VcLeftAlt)
            this.altPressed = true;

        if (e.Data.KeyCode == KeyCode.VcLeftShift)
            this.shiftPressed = true;

        if (e.Data.KeyCode == KeyCode.VcLeftControl)
            this.ctrlPressed = true;

        if (sampledVoiceManager != null && sampledVoiceManager.isTyping)
        {
            if (e.Data.KeyCode == KeyCode.VcLeft)
            {
                sampledVoiceManager.moveCursorLeft();
                return;
            }
            if (e.Data.KeyCode == KeyCode.VcRight)
            {
                sampledVoiceManager.moveCursorRight();
                return;
            }
            else
            if (e.Data.KeyCode == KeyCode.VcUp)
            {
                sampledVoiceManager.selectNextSuggestion();
                return;
            }
            else
            if (e.Data.KeyCode == KeyCode.VcDown)
            {
                sampledVoiceManager.selectPrevSuggestion();
                return;
            }

            if (!this.altPressed && !Regex.Match(Common.sentenceFull, "^/.*").Success)
            {
                if (e.Data.KeyCode == KeyCode.VcNumPad0 || e.Data.KeyCode == KeyCode.Vc0)
                {
                    sampledVoiceManager.selectSuggestion(9);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad1 || e.Data.KeyCode == KeyCode.Vc1)
                {
                    sampledVoiceManager.selectSuggestion(0);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad2 || e.Data.KeyCode == KeyCode.Vc2)
                {
                    sampledVoiceManager.selectSuggestion(1);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad3 || e.Data.KeyCode == KeyCode.Vc3)
                {
                    sampledVoiceManager.selectSuggestion(2);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad4 || e.Data.KeyCode == KeyCode.Vc4)
                {
                    sampledVoiceManager.selectSuggestion(3);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad5 || e.Data.KeyCode == KeyCode.Vc5)
                {
                    sampledVoiceManager.selectSuggestion(4);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad6 || e.Data.KeyCode == KeyCode.Vc6)
                {
                    sampledVoiceManager.selectSuggestion(5);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad7 || e.Data.KeyCode == KeyCode.Vc7)
                {
                    sampledVoiceManager.selectSuggestion(6);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad8 || e.Data.KeyCode == KeyCode.Vc8)
                {
                    sampledVoiceManager.selectSuggestion(7);
                    sampledVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad9 || e.Data.KeyCode == KeyCode.Vc9)
                {
                    sampledVoiceManager.selectSuggestion(8);
                    sampledVoiceManager.autoComplete();
                }
            }

            if ( (Regex.Match(Common.sentenceFull, "^/h new.*").Success || Regex.Match(Common.sentenceFull, "^/hotkey new.*").Success)
                && e.Data.KeyCode!=KeyCode.VcBackspace && e.Data.KeyCode != KeyCode.VcEnter)
            {
                string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                if (sentenceArray.Length == 4)
                {
                    Common.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sampledVoiceManager.updateFormattedSentence();
                        sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence + sampledVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 5 && sentenceArray[^1] == "")
                {
                    Common.sentence += e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sampledVoiceManager.updateFormattedSentence();
                        sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence + sampledVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 5)
                {
                    Common.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sampledVoiceManager.updateFormattedSentence();
                        sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence + sampledVoiceManager.formattedSentence2;
                    });
                }
            }
            else if ((Regex.Match(Common.sentenceFull, "^/bind.*").Success)
                && e.Data.KeyCode != KeyCode.VcBackspace && e.Data.KeyCode != KeyCode.VcEnter)
            {
                string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                if (sentenceArray.Length == 3)
                {
                    Common.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sampledVoiceManager.updateFormattedSentence();
                        sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence + sampledVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 4 && sentenceArray[^1] == "")
                {
                    Common.sentence += e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sampledVoiceManager.updateFormattedSentence();
                        sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence + sampledVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 4)
                {
                    Common.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sampledVoiceManager.updateFormattedSentence();
                        sampledVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sampledVoiceManager.formattedSentence + sampledVoiceManager.formattedSentence2;
                    });
                }
            }

        }

        if (sherpaVoiceManager != null && sherpaVoiceManager.isTyping)
        {
            
            if (e.Data.KeyCode == KeyCode.VcLeft)
            {
                sherpaVoiceManager.moveCursorLeft();
                return;
            } else
            if (e.Data.KeyCode == KeyCode.VcRight)
            {
                sherpaVoiceManager.moveCursorRight();
                return;
            }
            else
            if (e.Data.KeyCode == KeyCode.VcUp)
            {
                sherpaVoiceManager.selectNextSuggestion();
                return;
            }
            else
            if (e.Data.KeyCode == KeyCode.VcDown)
            {
                sherpaVoiceManager.selectPrevSuggestion();
                return;
            }

            if (!this.altPressed && !Regex.Match(Common.sentenceFull, "^/.*").Success)
            {
                if (e.Data.KeyCode == KeyCode.VcNumPad0 || e.Data.KeyCode == KeyCode.Vc0)
                {
                    sherpaVoiceManager.selectSuggestion(9);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad1 || e.Data.KeyCode == KeyCode.Vc1)
                {
                    sherpaVoiceManager.selectSuggestion(0);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad2 || e.Data.KeyCode == KeyCode.Vc2)
                {
                    sherpaVoiceManager.selectSuggestion(1);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad3 || e.Data.KeyCode == KeyCode.Vc3)
                {
                    sherpaVoiceManager.selectSuggestion(2);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad4 || e.Data.KeyCode == KeyCode.Vc4)
                {
                    sherpaVoiceManager.selectSuggestion(3);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad5 || e.Data.KeyCode == KeyCode.Vc5)
                {
                    sherpaVoiceManager.selectSuggestion(4);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad6 || e.Data.KeyCode == KeyCode.Vc6)
                {
                    sherpaVoiceManager.selectSuggestion(5);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad7 || e.Data.KeyCode == KeyCode.Vc7)
                {
                    sherpaVoiceManager.selectSuggestion(6);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad8 || e.Data.KeyCode == KeyCode.Vc8)
                {
                    sherpaVoiceManager.selectSuggestion(7);
                    sherpaVoiceManager.autoComplete();
                }
                else
                if (e.Data.KeyCode == KeyCode.VcNumPad9 || e.Data.KeyCode == KeyCode.Vc9)
                {
                    sherpaVoiceManager.selectSuggestion(8);
                    sherpaVoiceManager.autoComplete();
                }
            }

            if ((Regex.Match(Common.sentenceFull, "^/h new.*").Success || Regex.Match(Common.sentenceFull, "^/hotkey new.*").Success)
    && e.Data.KeyCode != KeyCode.VcBackspace && e.Data.KeyCode != KeyCode.VcEnter)
            {
                string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                if (sentenceArray.Length == 4)
                {
                    Common.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sherpaVoiceManager.updateFormattedSentence();
                        sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 5 && sentenceArray[^1] == "")
                {
                    Common.sentence += e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sherpaVoiceManager.updateFormattedSentence();
                        sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 5)
                {
                    Common.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sherpaVoiceManager.updateFormattedSentence();
                        sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                    });
                }
            }
            else if ((Regex.Match(Common.sentenceFull, "^/bind.*").Success)
                && e.Data.KeyCode != KeyCode.VcBackspace && e.Data.KeyCode != KeyCode.VcEnter)
            {
                string[] sentenceArray = Regex.Split(Common.sentenceFull, " ");
                if (sentenceArray.Length == 3)
                {
                    Common.sentence += " " + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sherpaVoiceManager.updateFormattedSentence();
                        sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 4 && sentenceArray[^1] == "")
                {
                    Common.sentence += e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sherpaVoiceManager.updateFormattedSentence();
                        sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                    });
                }
                else if (sentenceArray.Length == 4)
                {
                    Common.sentence += "+" + e.Data.KeyCode.ToString().Substring(2);
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        sherpaVoiceManager.updateFormattedSentence();
                        sherpaVoiceManager.updateFormattedSentenceForAutoComplete();
                        this.ttsBlock.Markdown = sherpaVoiceManager.formattedSentence + sherpaVoiceManager.formattedSentence2;
                    });
                }
            }


        }

            if (e.Data.KeyCode == KeyCode.VcPageUp)
        {
            this.hotkeysManager.NextPage();
            Dispatcher.UIThread.InvokeAsync(() => this.favoritesMgr.updateHotkeysGrid());
            return;
        }

        if (e.Data.KeyCode == KeyCode.VcPageDown)
        {
            this.hotkeysManager.PrevPage();
            Dispatcher.UIThread.InvokeAsync(() => this.favoritesMgr.updateHotkeysGrid());
            return;
        }
    }

    private void GlobalHook_KeyReleased(object? sender, KeyboardHookEventArgs e)
    {

        if (Common.hodlvtt == true && e.Data.KeyCode == Common.vttKey)
        {
            if (qPressed == false)
            {
                simulator.SimulateKeyPress(Common.vttKey);
                qPressed = true;
            }
            else
            {
                qPressed = false;
            }
        }

        if (e.Data.KeyCode == KeyCode.VcLeftAlt)
            this.altPressed = false;

        if (e.Data.KeyCode == KeyCode.VcLeftShift)
            this.shiftPressed = false;

        if (e.Data.KeyCode == KeyCode.VcLeftControl)
            this.ctrlPressed = false;
    }

    private void GlobalHook_MouseWheel(object? sender, MouseWheelHookEventArgs e)
    {
        if (sampledVoiceManager != null && sampledVoiceManager.selectedSuggestion != null)
        {
            if (e.Data.Rotation > 0)
            {
                sampledVoiceManager.selectNextSuggestion();
            }
            else if (e.Data.Rotation < 0)
            {
                sampledVoiceManager.selectPrevSuggestion();
            }
        }
        else if (sherpaVoiceManager != null && sherpaVoiceManager.selectedSuggestion != null)
        {
            if (e.Data.Rotation > 0)
            {
                sherpaVoiceManager.selectNextSuggestion();
            }
            else if (e.Data.Rotation < 0)
            {
                sherpaVoiceManager.selectPrevSuggestion();
            }
        }
        }


    private void executeCommand()
    {
        string cmdString = Common.sentenceFull;
        executeCommand(cmdString);
    }

    private void executeCommand(string cmdString)
    {
        this.clearOutput();
        if (cmdString.StartsWith("/"))
        {
            List<string> cmdArray = Regex.Split(cmdString, " ").Where(s => s != String.Empty).ToList();

            if (sampledVoiceManager!=null && !sampledVoiceManager.insideAlias)
                sampledVoiceManager.clearInput();

            if (sherpaVoiceManager != null && !sherpaVoiceManager.insideAlias)
                sherpaVoiceManager.clearInput();

            cmdArray[0] = cmdArray[0].Substring(1, cmdArray[0].Length - 1);


            switch (cmdArray[0])
            {
                case "set":
                    this.executeSetCmd(cmdArray);
                    break;
                case "get":
                    this.executeGetCmd(cmdArray);
                    break;
                case "voice":
                    this.executeVoiceCmd(cmdArray);
                    break;
                case "v":
                    this.executeVoiceCmd(cmdArray);
                    break;
                case "pitch":
                    this.executePitchCmd(cmdArray);
                    break;
                case "tempo":
                    this.executeTempoCmd(cmdArray);
                    break;
                case "vol":
                    this.executeVolumeCmd(cmdArray);
                    break;
                case "volume":
                    this.executeVolumeCmd(cmdArray);
                    break;
                case "pvol":
                    this.executePlaybackVolumeCmd(cmdArray);
                    break;
                case "playbackvolume":
                    this.executePlaybackVolumeCmd(cmdArray);
                    break;
                case "mvol":
                    this.executeMicrophoneVolumeCmd(cmdArray);
                    break;
                case "microphonevolume":
                    this.executeMicrophoneVolumeCmd(cmdArray);
                    break;
                case "playsong":
                    this.executePlaySongCmd(cmdArray);
                    break;
                case "playsongsync":
                    this.executePlaySongSyncCmd(cmdArray);
                    break;
                case "playmusicsync":
                    this.executePlayMusicSyncCmd(cmdArray);
                    break;
                case "scatman":
                    this.executeScatmanCmd(cmdArray);
                    break;
                case "numberone":
                    this.executeNumberOneCmd(cmdArray);
                    break;
                case "radio":
                    this.executeRadioCmd(cmdArray);
                    break;
                case "h":
                    this.executeHotkeyCmd(cmdArray);
                    break;
                case "hotkey":
                    this.executeHotkeyCmd(cmdArray);
                    break;
                case "bind":
                    this.executeBindCmd(cmdArray);
                    break;
                case "unbind":
                    this.executeUnbindCmd(cmdArray);
                    break;
                case "lipsync":
                    this.executeLipsyncCmd(cmdArray);
                    break;
                case "ping":
                    this.executePingCmd(cmdArray);
                    break;
                case "mic":
                    this.executeMicCmd(cmdArray);
                    break;
                case "holdvtt":
                    this.executeHoldVTTCmd(cmdArray);
                    break;
                case "vtt":
                    this.executeVttCmd(cmdArray);
                    break;
                default:
                    break;

            }

        }
    }

    private void executeVttCmd(List<string> cmdArray)
    {
        if (cmdArray.Count < 2 || cmdArray.Count > 2)
        {
            this.raiseError("Syntax: /vtt (vttkey)");
            return;
        }
        else
        {
            Common.vttKey = (KeyCode)Enum.Parse(typeof(KeyCode),"Vc"+cmdArray[1].ToUpper());
            if (System.IO.File.Exists("cfg/config.txt"))
            {
                string path = "cfg/config.txt";
                var fileContent = System.IO.File.ReadLines(path).ToList();

                fileContent[fileContent.Count - 1] = "vttkey:" + Common.vttKey;
                System.IO.File.WriteAllLines(path, fileContent);

            }
            this.outputInfoMessage("set vtt key to: " + Common.vttKey.ToString());
        }
    }

    private void executeHoldVTTCmd(List<string> cmdArray)
    {
        if (Common.enableMicToCable==true)
        {
            this.raiseError("disable mic first: /mic off. privacy matters");
            return;
        }
        else if (cmdArray.Count<2)
        {
            this.raiseError("Syntax: /holdvtt on (off)");
            return;
        }
        else
        {
            if (cmdArray[1] == "on")
            {
                Common.hodlvtt = true;
            }
            else if (cmdArray[1] == "off")
            {
                Common.hodlvtt = false;
            }  
        }
    }

    private void clearOutput()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.outputText.Markdown = "";
        });

    }

    private void raiseError(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.outputText.Markdown = "%{color:red}" + message + "%";
        });
    }

    private void outputMessage(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.outputText.Markdown = "%{color:green}" + message + "%";
            Console.WriteLine(message);
        });
    }

    private void outputInfoMessage(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.outputText.Markdown = "%{color:lightskyblue}" + message + "%";
            Console.WriteLine(message);
        });
    }

    private void outputSetMessage(string property, string value)
    {
        this.outputMessage("The " + property + " has been set to " + value);
    }

    private void outputGetMessage(string property, string value)
    {
        this.outputInfoMessage("The " + property + " is set to " + value);
    }

    private void executeSetCmd(List<string> cmdArray)
    {

        double fl;
        int it;
        bool parsed;

        if (cmdArray.Count < 3 || cmdArray.Count > 3)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /set cmd is following: /set (property) (value)");
            });
        }
        else if (cmdArray.Count == 3)
        {
            switch (cmdArray[1])
            {
                case "pitch":
                    parsed = double.TryParse(cmdArray[2], out fl);

                    if (parsed && fl > 0)
                    {
                        Common.pitch = fl;
                        this.outputSetMessage("pitch", Common.pitch.ToString());
                    }
                    else
                        this.raiseError("pitch is a positive floating point number (e.g 5.2312)");
                    break;
                case "volume":

                    parsed = double.TryParse(cmdArray[2], out fl);

                    if (parsed && fl > 0)
                    {
                        Common.volume = fl;
                        this.outputSetMessage("volume", Common.volume.ToString());
                    }
                    else
                        this.raiseError("volume is a positive floating point number (e.g 5.2312)");
                    break;
                case "tempo":

                    parsed = double.TryParse(cmdArray[2], out fl);

                    if (parsed && fl > 0)
                    {
                        Common.tempo = fl;
                        this.outputSetMessage("tempo", Common.tempo.ToString());
                    }
                    else
                        this.raiseError("tempo is a positive floating point number (e.g 5.2312)");
                    break;
                case "offset":
                    parsed = int.TryParse(cmdArray[2], out it);

                    if (parsed)
                    {
                        Common.offset = it;
                        this.outputSetMessage("offset", Common.offset.ToString() + " milliseconds");
                    }
                    else
                        this.raiseError("offset is an integer number (e.g 100) which represents milliseconds");
                    break;
                case "concatoffset":
                    parsed = int.TryParse(cmdArray[2], out it);

                    if (parsed)
                    {
                        Common.concatoffset = it;
                        this.outputSetMessage("concatoffset", Common.concatoffset.ToString() + " milliseconds");
                    }
                    else
                        this.raiseError("concatoffset is an integer number (e.g 500) which represents milliseconds");
                    break;
                case "voicethreshold":
                    parsed = double.TryParse(cmdArray[2], out fl);

                    if (parsed && fl > 0)
                    {
                        Common.voicethreshold = fl;
                        this.outputSetMessage("voicethreshold", Common.voicethreshold.ToString());
                    }
                    else
                        this.raiseError("silencethreshold is a floating point number (e.g -5.2312)");
                    break;
                case "voice":
                    if (Common.voices.Contains(cmdArray[2]) || 
                        Common.sherpaVoices.ContainsKey(cmdArray[2]) || cmdArray[2]=="none")
                    {
                        Common.voice = cmdArray[2];
                        this.setupVoice();
                        this.outputSetMessage("voice", Common.voice);
                    }
                    else
                        this.raiseError("this voice doesn't exist. each voice has its own subdir with the corresponding name in sounds/");
                    break;
                case "playbackvolume":

                    parsed = double.TryParse(cmdArray[2], out fl);

                    if (parsed && fl > 0)
                    {
                        Common.playbackVolume = fl;
                        this.outputSetMessage("playbackvolume", Common.playbackVolume.ToString());
                    }
                    else
                        this.raiseError("playbackvolume is a positive floating point number (e.g 5.2312)");
                    break;
                case "micvolume":

                    parsed = double.TryParse(cmdArray[2], out fl);

                    if (parsed && fl > 0)
                    {
                        Common.micVolume = fl;
                        this.outputSetMessage("micvolume", Common.micVolume.ToString());
                    }
                    else
                        this.raiseError("micvolume is a positive floating point number (e.g 5.2312)");
                    break;
                case "ping":
                    parsed = int.TryParse(cmdArray[2], out it);

                    if (parsed)
                    {
                        Common.offset = it;
                        this.outputSetMessage("ping", Common.offset.ToString() + " milliseconds");
                    }
                    else
                        this.raiseError("ping is an integer number (e.g 100) which represents milliseconds");
                    break;
            }
        }
    }

    private void executeGetCmd(List<string> cmdArray)
    {

        if (cmdArray.Count < 2 || cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /get cmd is following: /get (property)");
            });
        }
        else if (cmdArray.Count == 2)
        {
            switch (cmdArray[1])
            {
                case "pitch":
                    this.outputGetMessage("pitch", Common.pitch.ToString());
                    break;
                case "volume":
                    this.outputGetMessage("volume", Common.volume.ToString());
                    break;
                case "tempo":
                    this.outputGetMessage("tempo", Common.tempo.ToString());
                    break;
                case "offset":
                    this.outputGetMessage("offset", Common.offset.ToString() + " milliseconds");
                    break;
                case "concatoffset":
                    this.outputGetMessage("concatoffset", Common.concatoffset.ToString() + " milliseconds");
                    break;
                case "voicethreshold":
                    this.outputGetMessage("voicethreshold", Common.voicethreshold.ToString() + " dB");
                    break;
                case "voice":
                    this.outputGetMessage("voice", Common.voice);
                    break;
                case "playbackvolume":
                    this.outputGetMessage("playbackvolume", Common.playbackVolume.ToString());
                    break;
                case "micvolume":
                    this.outputGetMessage("micvolume", Common.micVolume.ToString());
                    break;
                case "ping":
                    this.outputGetMessage("ping", Common.ping.ToString());
                    break;
                default:
                    break;
            }
        }
    }

    private void executeVoiceCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /voice cmd is following: /voice (voice) or /voice");
            });
        }
        else if (cmdArray.Count == 1)
        {
            Common.sentence = "";
            Common.sentence2 = "";
            Thread.Sleep(50);
            Dispatcher.UIThread.InvokeAsync((Action)(() =>
            {
                Voices voices = new Voices(this.audioPlaybackEngine1);
                voices.CommandExecute += Voices_CommandExecute;
                voices.ClearInput += this.Voices_ClearInput;
                voices.Show();
            }));
        }
        else if (cmdArray.Count==2 && cmdArray[1]=="list")
        {
            Common.sentence = "";
            Common.sentence2 = "";
            Thread.Sleep(50);
            Dispatcher.UIThread.InvokeAsync((Action)(() =>
            {
                Voices voices = new Voices(this.audioPlaybackEngine1);
                voices.CommandExecute += Voices_CommandExecute;
                voices.ClearInput += this.Voices_ClearInput;
                voices.Show();
            }));
        }
        else
        {
            executeSetCmd(new List<string>() { "set", "voice", cmdArray[1] });
            if (System.IO.File.Exists("sounds/soundboard/scp/.bell.mp3"))
                audioPlaybackEngine1.PlaySound("sounds/soundboard/scp/.bell.mp3", 1, 1, 1);
        }
    }

    private void executeLipsyncCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /lipsync cmd is following: /lipsync on (off)");
            });
        }
        else if (cmdArray.Count < 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /lipsync cmd is following: /lipsync on (off)");
            });
        }
        else
        {
            if (cmdArray[1] == "on")
            {
                Common.isLipsyncOn = true;
                if (lipsyncManager==null)
                {
                    Dispatcher.UIThread.InvokeAsync(() => lipsyncManager =
                    new LipsyncManager(audioPlaybackEngine1, audioPlaybackEngine2, micToCable));
                }
            }
            else if (cmdArray[1] == "off")
            {
                Common.isLipsyncOn = false;
                if (lipsyncManager!=null)
                {
                    lipsyncManager.Dispose();
                    lipsyncManager = null;
                }
            }
        }

    }

    private void executeMicCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /mic cmd is following: /mic on (off)");
            });
        }
        else if (cmdArray.Count < 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /mic cmd is following: /mic on (off)");
            });
        }
        else
        {
            if (cmdArray[1] == "on")
            {
                Common.enableMicToCable = true;
                if (this.micToCable != null)
                    this.micToCable.startMicToCable();
            }
            else if (cmdArray[1] == "off")
            {
                Common.enableMicToCable = false;
                if (this.micToCable != null)
                    this.micToCable.stopMicToCable();

            }
        }

    }


    private void Voices_ClearInput(object? sender, EventArgs e)
    {
        if (sampledVoiceManager != null && !sampledVoiceManager.insideAlias)
            sampledVoiceManager.clearInput();

        if (sherpaVoiceManager != null && !sherpaVoiceManager.insideAlias)
            sherpaVoiceManager.clearInput();

    }


    private void Voices_CommandExecute(object myObject, Voices.CommandExecuteEventArgs myArgs)
    {
        this.executeCommand(myArgs.cmdString);
    }

    private void executePingCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /ping cmd is following: /ping (ping) or /ping");
            });
        }
        else if (cmdArray.Count == 1)
        {
            executeGetCmd(new List<string>() { "get", "ping" });
        }
        else
        {
            executeSetCmd(new List<string>() { "set", "ping", cmdArray[1] });
        }
    }


    private void executePitchCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /pitch cmd is following: /pitch (pitch) or /pitch");
            });
        }
        else if (cmdArray.Count == 1)
        {
            executeGetCmd(new List<string>() { "get", "pitch" });
        }
        else
        {
            executeSetCmd(new List<string>() { "set", "pitch", cmdArray[1] });
        }
    }



    private void executeTempoCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /tempo cmd is following: /tempo (tempo) or /tempo");
            });
        }
        else if (cmdArray.Count == 1)
        {
            executeGetCmd(new List<string>() { "get", "tempo" });
        }
        else
        {
            executeSetCmd(new List<string>() { "set", "tempo", cmdArray[1] });
        }
    }

    private void executeVolumeCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /vol cmd is following: /vol (volume) or /vol");
            });
        }
        else if (cmdArray.Count == 1)
        {
            executeGetCmd(new List<string>() { "get", "volume" });
        }
        else
        {
            executeSetCmd(new List<string>() { "set", "volume", cmdArray[1] });
        }
    }

    private void executePlaybackVolumeCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /pvol cmd is following: /pvol (playbackvolume) or /pvol");
            });
        }
        else if (cmdArray.Count == 1)
        {
            executeGetCmd(new List<string>() { "get", "playbackvolume" });
        }
        else
        {
            executeSetCmd(new List<string>() { "set", "playbackvolume", cmdArray[1] });
        }
    }

    private void executeMicrophoneVolumeCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /mvol cmd is following: /mvol (volume) or /mvol");
            });
        }
        else if (cmdArray.Count == 1)
        {
            executeGetCmd(new List<string>() { "get", "micvolume" });
        }
        else
        {
            executeSetCmd(new List<string>() { "set", "micvolume", cmdArray[1] });
            if (this.micToCable!=null)
            {
                this.micToCable.updateMicVolume();
            }
        }
    }

    private void executePlaySongCmd(List<string> cmdArray)
    {
        bool v = false;
        bool i = false;
        bool o = false;

        EnumSongPlayMode songPlayMode = EnumSongPlayMode.DEFAULT;

        if (cmdArray.Count < 2)
        {
            string songs = "";
            foreach (string song in songsManager.songs)
            {
                songs += song + " ";
            }
            this.outputInfoMessage(songs);
        }
        else
        {
            string song = cmdArray[^1];

            if (!songsManager.songs.Contains(song))
            {
                this.raiseError("there's no such song. try /playsong"); return;
            }

            List<string> args = cmdArray.Slice(1, cmdArray.Count - 2);

            string voicetrack = "";

            if (args.Count > 0)
            {
                foreach (string arg in args)
                {
                    if (arg == "-v")
                    {
                        v = true;
                    }
                    else if (arg == "-i")
                    {
                        i = true;
                    }
                    else if (arg == "-o")
                    {
                        o = true;
                    }
                    else if (arg.StartsWith('-'))
                    {
                        voicetrack = arg[1..];
                    }

                }
            }

            if (v == true && i == true) { this.raiseError("you cannot do -v and -i at the same time XD"); return; }

            if (v == true)
            {
                songPlayMode = EnumSongPlayMode.VOICEONLY;
            }

            if (i == true)
            {
                songPlayMode = EnumSongPlayMode.INSTRUMENTALONLY;
            }


                if (o == true)
                {
                    songsManager.Play(song, "original", songPlayMode);
                }
                else
                {
                    if (voicetrack!="")
                        songsManager.Play(song, voicetrack, songPlayMode);
                    else
                        songsManager.Play(song, Common.voice, songPlayMode);
                }

                Thread.Sleep(100);

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (sampledVoiceManager != null)
                    {
                        //sampledVoiceManager.updateFormattedSentence();
                        this.ttsBlock.Markdown = "%{color:lime}" + song +"%";//sampledVoiceManager.formattedSentence;
                    }
                    else if (sherpaVoiceManager != null)
                    { 
                        //kokoroVoiceManager.updateFormattedSentence();
                        this.ttsBlock.Markdown = "%{color:lime}" + song +"%"; //kokoroVoiceManager.formattedSentence;
                    }
                });

                Common.sentence = "";
                Common.sentence2 = "";


        }
    }

    private void executePlaySongSyncCmd(List<string> cmdArray)
    {
        bool v = false;
        bool i = false;
        bool o = false;

        EnumSongPlayMode songPlayMode = EnumSongPlayMode.DEFAULT;

        if (cmdArray.Count < 2)
        {
            string songs = "";
            foreach (string song in songsManager.songs)
            {
                songs += song + " ";
            }
            this.outputInfoMessage(songs);
        }
        else
        {
            string song="";

            List<string> args = cmdArray.Slice(1, cmdArray.Count-1);

            int ping = 0;

            string voicetrack="";

            if (args.Count > 0)
            {
                foreach (string arg in args)
                {
                    if (arg == "-v")
                    {
                        v = true;
                    }
                    else if (arg == "-i")
                    {
                        i = true;
                    }
                    else if (arg == "-o")
                    {
                        o = true;
                    }
                    else if (arg.StartsWith('-'))
                    {
                        voicetrack = arg[1..];
                    }
                    else if (int.TryParse(arg,out ping))
                    {

                    }
                    else
                    {
                        song = arg;
                    }

                }
            }

            Common.ping = ping;

            if (!songsManager.songs.Contains(song))
            {
                this.raiseError("there's no such song. try /playsong"); return;
            }

            if (v == true && i == true) { this.raiseError("you cannot do -v and -i at the same time XD"); return; }

            if (v == true)
            {
                songPlayMode = EnumSongPlayMode.VOICEONLY;
            }

            if (i == true)
            {
                songPlayMode = EnumSongPlayMode.INSTRUMENTALONLY;
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {

                this.ttsBlock.Markdown = "%{color:lime}" + "please wait" + "%";//sampledVoiceManager.formattedSentence;
            });


                audioPlaybackEngine1.PlaySongSync(song,ping,onlyvoice:v,onlyins:i, original:o, vtrack:voicetrack);
                audioPlaybackEngine2.PlaySongSync(song,ping,onlyvoice: v, onlyins: i, original: o, vtrack: voicetrack);


            Thread.Sleep(100);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (sampledVoiceManager != null)
                {
                    //sampledVoiceManager.updateFormattedSentence();
                    this.ttsBlock.Markdown = "%{color:lime}" + song + "%";//sampledVoiceManager.formattedSentence;
                }
                else if (sherpaVoiceManager != null)
                {
                    //kokoroVoiceManager.updateFormattedSentence();
                    this.ttsBlock.Markdown = "%{color:lime}" + song + "%"; //kokoroVoiceManager.formattedSentence;
                }
            });

            Common.sentence = "";
            Common.sentence2 = "";


        }
    }

    private void executeScatmanCmd(List<string> cmdArray)
    {
        bool v = false;
        bool i = false;
        bool o = false;

        EnumSongPlayMode songPlayMode = EnumSongPlayMode.DEFAULT;


            string song = "";

            List<string> args = cmdArray.Slice(1, cmdArray.Count - 1);

            int ping = 0;

            string voicetrack = "";

            song = "_scatman";

            if (args.Count > 0)
            {
                foreach (string arg in args)
                {
                    if (arg == "-v")
                    {
                        v = true;
                    }
                    else if (arg == "-i")
                    {
                        i = true;
                    }
                    else if (arg == "-o")
                    {
                        o = true;
                    }
                    else if (arg.StartsWith('-'))
                    {
                        voicetrack = arg[1..];
                    }
                    else if (int.TryParse(arg, out ping))
                    {

                    }

                }
            }

            Common.ping = ping;

            if (!songsManager.songs.Contains(song))
            {
                this.raiseError("there's no such song. try /playsong"); return;
            }

            if (v == true && i == true) { this.raiseError("you cannot do -v and -i at the same time XD"); return; }

            if (v == true)
            {
                songPlayMode = EnumSongPlayMode.VOICEONLY;
            }

            if (i == true)
            {
                songPlayMode = EnumSongPlayMode.INSTRUMENTALONLY;
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {

                this.ttsBlock.Markdown = "%{color:lime}" + "please wait" + "%";//sampledVoiceManager.formattedSentence;
            });



            audioPlaybackEngine1.PlaySongSync(song, ping, onlyvoice: v, onlyins: i, original: o, vtrack: voicetrack);
            audioPlaybackEngine2.PlaySongSync(song, ping, onlyvoice: v, onlyins: i, original: o, vtrack: voicetrack);


            Thread.Sleep(100);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (sampledVoiceManager != null)
                {
                    //sampledVoiceManager.updateFormattedSentence();
                    this.ttsBlock.Markdown = "%{color:lime}" + song + "%";//sampledVoiceManager.formattedSentence;
                }
                else if (sherpaVoiceManager != null)
                {
                    //kokoroVoiceManager.updateFormattedSentence();
                    this.ttsBlock.Markdown = "%{color:lime}" + song + "%"; //kokoroVoiceManager.formattedSentence;
                }
            });

            Common.sentence = "";
            Common.sentence2 = "";


        
    }

    private void executeNumberOneCmd(List<string> cmdArray)
    {
        bool v = false;
        bool i = false;
        bool o = false;

        EnumSongPlayMode songPlayMode = EnumSongPlayMode.DEFAULT;


        string song = "";

        List<string> args = cmdArray.Slice(1, cmdArray.Count - 1);

        int ping = 0;

        string voicetrack = "";

        song = "_numberone";

        if (args.Count > 0)
        {
            foreach (string arg in args)
            {
                if (arg == "-v")
                {
                    v = true;
                }
                else if (arg == "-i")
                {
                    i = true;
                }
                else if (arg == "-o")
                {
                    o = true;
                }
                else if (arg.StartsWith('-'))
                {
                    voicetrack = arg[1..];
                }
                else if (int.TryParse(arg, out ping))
                {

                }

            }
        }

        Common.ping = ping;

        if (!songsManager.songs.Contains(song))
        {
            this.raiseError("there's no such song. try /playsong"); return;
        }

        if (v == true && i == true) { this.raiseError("you cannot do -v and -i at the same time XD"); return; }

        if (v == true)
        {
            songPlayMode = EnumSongPlayMode.VOICEONLY;
        }

        if (i == true)
        {
            songPlayMode = EnumSongPlayMode.INSTRUMENTALONLY;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {

            this.ttsBlock.Markdown = "%{color:lime}" + "please wait" + "%";//sampledVoiceManager.formattedSentence;
        });



        audioPlaybackEngine1.PlaySongSync(song, ping, onlyvoice: v, onlyins: i, original: o, vtrack: voicetrack);
        audioPlaybackEngine2.PlaySongSync(song, ping, onlyvoice: v, onlyins: i, original: o, vtrack: voicetrack);


        Thread.Sleep(100);

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (sampledVoiceManager != null)
            {
                //sampledVoiceManager.updateFormattedSentence();
                this.ttsBlock.Markdown = "%{color:lime}" + song + "%";//sampledVoiceManager.formattedSentence;
            }
            else if (sherpaVoiceManager != null)
            {
                //kokoroVoiceManager.updateFormattedSentence();
                this.ttsBlock.Markdown = "%{color:lime}" + song + "%"; //kokoroVoiceManager.formattedSentence;
            }
        });

        Common.sentence = "";
        Common.sentence2 = "";



    }



    private void executePlayMusicSyncCmd(List<string> cmdArray)
    {


        if (cmdArray.Count < 2)
        {
            string songs = "";
            foreach (string song in musicManager.music)
            {
                songs += song + " ";
            }
            this.outputInfoMessage(songs);
        }
        else
        {
            string song = "";

            List<string> args = cmdArray.Slice(1, cmdArray.Count - 1);

            int ping = 0;

            string voicetrack = "";

            if (args.Count > 0)
            {
                foreach (string arg in args)
                {

                     if (int.TryParse(arg, out ping))
                    {

                    }
                    else
                    {
                        song = arg;
                    }

                }
            }

            Common.ping = ping;

            if (!musicManager.music.Contains(song))
            {
                this.raiseError("there's no such music. try /playmusicsync"); return;
            }


            Dispatcher.UIThread.InvokeAsync(() =>
            {

                this.ttsBlock.Markdown = "%{color:yellow}" + "please wait" + "%";//sampledVoiceManager.formattedSentence;
            });



            audioPlaybackEngine1.PlayMusicSync(song, ping);
            audioPlaybackEngine2.PlayMusicSync(song, ping);


            Thread.Sleep(100);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (sampledVoiceManager != null)
                {
                    //sampledVoiceManager.updateFormattedSentence();
                    this.ttsBlock.Markdown = "%{color:yellow}" + song + "%";//sampledVoiceManager.formattedSentence;
                }
                else if (sherpaVoiceManager != null)
                {
                    //kokoroVoiceManager.updateFormattedSentence();
                    this.ttsBlock.Markdown = "%{color:yellow}" + song + "%"; //kokoroVoiceManager.formattedSentence;
                }
            });

            Common.sentence = "";
            Common.sentence2 = "";


        }
    }




    private void executeRadioCmd(List<string> cmdArray)
    {

        bool v = false;
        bool i = false;
        string voicetrack = "";

        List<string> args = cmdArray.Slice(1, cmdArray.Count - 1);

            int ping = 0;

            if (args.Count > 0)
            {

            foreach (string arg in args)
            {
                if (arg == "-v")
                {
                    v = true;
                }
                else if (arg == "-i")
                {
                    i = true;
                }
                else if (arg.StartsWith('-'))
                {
                    voicetrack = arg[1..];
                }
                else if (int.TryParse(arg, out ping))
                {

                }

            }

        }

        Common.ping = ping;

        Dispatcher.UIThread.InvokeAsync(() =>
            {

                this.ttsBlock.Markdown = "%{color:dodgerblue}" + "please wait" + "%";//sampledVoiceManager.formattedSentence;
            });

            this.radioPlaying = true;



                audioPlaybackEngine1.PlayRadio(ping,onlyins:i,onlyvoice:v,vtrack:voicetrack);
                audioPlaybackEngine2.PlayRadio(ping);
   

            Thread.Sleep(100);

            Dispatcher.UIThread.InvokeAsync(() =>
            {

                    this.ttsBlock.Markdown = "%{color:dodgerblue}" + "radio" + "%";//sampledVoiceManager.formattedSentence;
            });

            Common.sentence = "";
            Common.sentence2 = "";


        
    }


    private void executeBindCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 2)
            executeHotkeyCmd(new List<string>() { "/h", "new", cmdArray[1], cmdArray[2] });
        else
            this.raiseError("The syntax of /bind  cmd is following: /bind (action) (hotkey) to add a new hotkey");
    }

    private void executeUnbindCmd(List<string> cmdArray)
    {
        if (cmdArray.Count > 1)
            executeHotkeyCmd(new List<string>() { "/h", "remove", cmdArray[1]});
        else
            this.raiseError("The syntax of /unbind cmd is following: /bind (action) to remove a hotkey");
    }

    private void executeHotkeyCmd(List<string> cmdArray)
    {
        if (cmdArray.Count < 2)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.raiseError("The syntax of /h (/hotkey) cmd is following: /h (number) to switch hotkeys, /h new (action) (hotkey) to add a new hotkey, /h remove (action)");
            });

        }

        else if (cmdArray.Count==2)
        {
            int page;
            if (int.TryParse(cmdArray[1], out page))
            {
                if (this.hotkeysManager.SetPage(page))
                {
                    Dispatcher.UIThread.InvokeAsync(() => this.favoritesMgr.updateHotkeysGrid());
                    this.outputMessage("Hotkeys page has been set to " + page.ToString());
                }
                else
                {
                    this.raiseError("there's no hotkeys page with such a number :(");
                }
            }
            else
            {
                this.raiseError("The syntax of /h (/hotkey) cmd is following: /h (number) to switch hotkeys, /h new (action) (hotkey) to add a new hotkey, /h remove (action)");
            }
        }
        else if (cmdArray.Count==3)
        {
            if (cmdArray[1] == "remove")
            {
                this.hotkeysManager.remove(cmdArray[2]);
                Dispatcher.UIThread.InvokeAsync(() => this.favoritesMgr.updateHotkeysGrid());
            }
        }

        else if (cmdArray.Count==4)
        {
            string action;
            string hkeyStr;

            if (cmdArray[1] =="new")
            {
                action = cmdArray[2];
                hkeyStr= cmdArray[3];
                this.hotkeysManager.addSave(action, hkeyStr);
                Dispatcher.UIThread.InvokeAsync(() => this.favoritesMgr.updateHotkeysGrid());
            }

        }
    }

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern void AttachThreadInput(int windowThreadProcessId, int currentThreadId, bool fAttach=true);

    [DllImport("user32.dll")]
    static extern int GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("kernel32.dll")]
    static extern int GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private void setForeground(IntPtr hWnd)
    {
        int windowThreadProcessId = GetWindowThreadProcessId(GetForegroundWindow(),0);
        int currentThreadId = GetCurrentThreadId();
        int CONST_SW_SHOW = 5;
        AttachThreadInput(windowThreadProcessId, currentThreadId, true);
        BringWindowToTop(hwnd);
        ShowWindow(hwnd, CONST_SW_SHOW);
        AttachThreadInput(windowThreadProcessId, currentThreadId, false);
    }

}
