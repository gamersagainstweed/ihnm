using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ihnm.Helpers;
using static ihnm.AudioPlaybackEngine;
using NAudio.Wave.SampleProviders;
using System.Diagnostics;
using Avalonia.Threading;
using System.Threading;
using System.Data.SqlTypes;
using System.IO;
using SharpHook;

namespace ihnm.Managers
{
    public class LipsyncManager
    {

        AudioPlaybackEngine engine1;
        AudioPlaybackEngine engine2;
        MicToCableManager micToCable;

        Image classd;
        Image classd2;


        MeteringSampleProvider curSample1;
        MeteringSampleProvider curSample2;
        MeteringSampleProvider micSample;

        Bitmap neutral = new Bitmap(AssetLoader.Open(new Uri("avares://ihnm/Assets/classd_neutral.png")));
        Bitmap happy = new Bitmap(AssetLoader.Open(new Uri("avares://ihnm/Assets/classd_happy.png")));
        Bitmap scared = new Bitmap(AssetLoader.Open(new Uri("avares://ihnm/Assets/classd_scared.png")));

        EventSimulator simulator = new EventSimulator();

        public EnumEmotion curEmotion;
        private EnumEmotion curUiEmotion;

        DateTime lastMove=DateTime.Now;
        DateTime lastMoveUi=DateTime.Now;

        LipsyncWindow lipWnd;

        public LipsyncManager(AudioPlaybackEngine engine1, AudioPlaybackEngine engine2, MicToCableManager micToCable = null)
        {
            this.setupCmdBindings();

            this.engine1 = engine1;
            this.engine2 = engine2;
            this.micToCable = micToCable;

            lipWnd = new LipsyncWindow();
            lipWnd.Show();
            
            classd= new Image() { Width=100, Height=100 };
            classd.Source = neutral;

            classd2 = new Image() { Width = 50, Height = 50};
            classd2.Source = neutral;
            Canvas.SetBottom(classd2, 0);

            lipWnd.root.Children.Add(classd);
            //lipWnd.root.Children.Add(classd2);

            this.engine1.Lipsync += Engine_Lipsync1;
            this.engine2.Lipsync += Engine_Lipsync2;

            if (this.micToCable != null)
            {
                this.micToCable.Lipsync += MicToCable_Lipsync;
            }
            else
            {
                Debug.WriteLine("micToCable is null");
            }


        }

        public void Dispose()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                lipWnd.Close();

                this.engine1.Lipsync -= Engine_Lipsync1;
                this.engine2.Lipsync -= Engine_Lipsync2;
                if (this.micToCable != null)
                {
                    this.micToCable.Lipsync -= MicToCable_Lipsync;
                }
            });
        }

        public void MicToCable_Lipsync(object myObject, MicToCableManager.LipsyncEventArgs myArgs)
        {
            this.micSample = myArgs.sample;

            this.micSample.StreamVolume += MicSample_StreamVolume;
        }

        private void MicSample_StreamVolume(object? sender, StreamVolumeEventArgs e)
        {
            float vol = e.MaxSampleValues.Sum() / e.MaxSampleValues.Length;
            if (vol > Common.lipsyncThreshold)
            {
                this.emotionScared();
                this.uiEmotionScared();
            }
            else if (vol > Common.lipsyncThreshold - 0.02)
            {
                this.emotionHappy();
                this.uiEmotionHappy();
            }
            else
            {
                this.emotionNeutral();
                this.uiEmotionNeutral();
            }
        }

        public void setupCmdBindings()
        {
            string userprofile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string cmdPath = userprofile + "\\AppData\\Roaming\\SCP Secret Laboratory\\cmdbinding.txt";


            StreamReader cmdStream = new StreamReader(cmdPath);
            string cmdBindings = cmdStream.ReadToEnd();

            cmdStream.Close();

            StreamWriter cmdWriteStream;

            if (!cmdBindings.Contains("294:emotion neutral"))
            {

                cmdWriteStream = new StreamWriter(cmdPath, append: true);

                cmdWriteStream.WriteLine();

                cmdWriteStream.WriteLine("294:emotion neutral");
                cmdWriteStream.WriteLine("295:emotion happy");
                cmdWriteStream.WriteLine("296:emotion scared");

                cmdWriteStream.Close();

                CallOutput("SCP:SL's cmdbinds have been changed. Pls restart the game.");

            }


        }


        private void Engine_Lipsync1(object? sender, LipsyncEventArgs e)
        {
            if (this.curSample1!=null)
            {
                this.curSample1.StreamVolume -= Sample_StreamVolume1;
            }

            this.curSample1 =  e.sample;

            TimeSpan soundLength = engine1.currentSoundLength;

      
                curSample1.StreamVolume += Sample_StreamVolume1;

        }

        private void Engine_Lipsync2(object? sender, LipsyncEventArgs e)
        {
            if (this.curSample2 != null)
            {
                this.curSample2.StreamVolume -= Sample_StreamVolume2;
            }

            this.curSample2 = e.sample;

            TimeSpan soundLength = engine2.currentSoundLength;

 
                curSample2.StreamVolume += Sample_StreamVolume2;

        }

        private void Sample_StreamVolume1(object? sender, StreamVolumeEventArgs e)
        {
            float vol = e.MaxSampleValues.Sum() / e.MaxSampleValues.Length;
            if (vol > Common.lipsyncThreshold)
            {
                this.uiEmotionScared();
            }
            else if (vol > Common.lipsyncThreshold-0.02)
            {
                this.uiEmotionHappy();
            }
            else
            {
                this.uiEmotionNeutral();
            }

        }

        private void Sample_StreamVolume2(object? sender, StreamVolumeEventArgs e)
        {
            float vol = e.MaxSampleValues.Sum() / e.MaxSampleValues.Length;
            if (vol > Common.lipsyncThreshold)
            {
                this.emotionScared();
            }
            else if (vol > Common.lipsyncThreshold-0.02)
            {
                this.emotionHappy();
            }
            else
            {
                 this.emotionNeutral();
            }

        }

        public void CloseMouth()
        {
            lastMove = DateTime.MinValue;
            lastMoveUi = DateTime.MinValue;
            this.emotionNeutral();
            this.uiEmotionNeutral();
        }

        public void emotionNeutral()
        {
            DateTime now = DateTime.Now;
            if ((now - lastMove > Common.lipsyncDelay) && curEmotion !=EnumEmotion.neutral)
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.classd2.Source = neutral; });
                simulator.SimulateKeyPress(SharpHook.Native.KeyCode.VcF13);
                simulator.SimulateKeyRelease(SharpHook.Native.KeyCode.VcF13);
                curEmotion = EnumEmotion.neutral;
                lastMove = now;
            }

        }

        public void emotionHappy()
        {
            DateTime now = DateTime.Now;
            if ((now - lastMove > Common.lipsyncDelay) && curEmotion != EnumEmotion.happy)
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.classd2.Source = happy; });
                simulator.SimulateKeyPress(SharpHook.Native.KeyCode.VcF14);
                simulator.SimulateKeyRelease(SharpHook.Native.KeyCode.VcF14);
                curEmotion = EnumEmotion.happy;
                lastMove = now;
            }
        }

        public void emotionScared()
        {
            DateTime now = DateTime.Now;
            if ((now - lastMove > Common.lipsyncDelay) && curEmotion != EnumEmotion.scared)
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.classd2.Source = scared; });
                simulator.SimulateKeyPress(SharpHook.Native.KeyCode.VcF15);
                simulator.SimulateKeyRelease(SharpHook.Native.KeyCode.VcF15);
                curEmotion = EnumEmotion.scared;
                lastMove = now;
            }
        }

        private void uiEmotionNeutral()
        {
            DateTime now = DateTime.Now;
            if ((now-lastMoveUi> Common.lipsyncDelay) && (curUiEmotion != EnumEmotion.neutral))
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.classd.Source = neutral; curUiEmotion = EnumEmotion.neutral; });
                lastMoveUi = now;
            }
        }

        private void uiEmotionHappy()
        {
            DateTime now = DateTime.Now;
            if ((now - lastMoveUi > Common.lipsyncDelay) &&(curUiEmotion != EnumEmotion.happy))
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.classd.Source = happy; curUiEmotion = EnumEmotion.happy; });
                lastMoveUi = now;
            }
        }

        private void uiEmotionScared()
        {
            DateTime now = DateTime.Now;
            if ((now - lastMoveUi > Common.lipsyncDelay) &&(curUiEmotion != EnumEmotion.scared))
            {
                Dispatcher.UIThread.InvokeAsync(() => { this.classd.Source = scared; curUiEmotion = EnumEmotion.scared; });
                lastMoveUi = now;
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

    }

    public enum EnumEmotion
    {
        neutral,
        happy,
        scared
    }

}
