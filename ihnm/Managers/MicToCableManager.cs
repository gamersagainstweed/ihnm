using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ihnm.Managers
{
    public class MicToCableManager
    {

        private WasapiCapture Recorder;
        public BufferedWaveProvider Sound;

        private MeteringSampleProvider meteringSample;
        private VolumeSampleProvider volSample;

        private AudioPlaybackEngine engine;

        public MicToCableManager(AudioPlaybackEngine engine, MMDevice microphone) 
        {
            this.engine = engine;

            this.engine.InitMic();

            Recorder = new WasapiCapture(microphone,false)
            {
                WaveFormat = new WaveFormat(48000, 1)
            };

        }

        public void startMicToCable()
        {

            Recorder.DataAvailable += ProcessData;

            Sound = new BufferedWaveProvider(Recorder.WaveFormat);
            meteringSample = new NAudio.Wave.SampleProviders.MeteringSampleProvider(Sound.ToSampleProvider());

            volSample = new VolumeSampleProvider(meteringSample) { Volume = (float)Common.micVolume };

            this.engine.AddMicrophoneInput(volSample);


            if (Common.isLipsyncOn)
            {
                this.CallLipsync(meteringSample);
            }
            Recorder.StartRecording();
        }

        public void renewLipsync()
        {
            if (Common.isLipsyncOn)
            {
                this.CallLipsync(meteringSample);
            }
        }

        public void stopMicToCable()
        {
            Recorder.DataAvailable -= ProcessData;
            Recorder.StopRecording();
            Sound = null;
        }

        private void ProcessData(object sender, WaveInEventArgs e)
        {
            Sound.AddSamples(e.Buffer, 0, e.BytesRecorded);
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
        

        public void updateMicVolume()
        {
            volSample.Volume = (float)Common.micVolume;
        }

    }
}
