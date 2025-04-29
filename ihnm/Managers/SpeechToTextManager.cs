using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using EchoSharp.NAudio;
using EchoSharp.SpeechTranscription;
using EchoSharp.Abstractions.VoiceActivityDetection;
using EchoSharp.Abstractions.SpeechTranscription;
using System.Globalization;
using EchoSharp.Abstractions.Audio;
using EchoSharp.Onnx.SileroVad;
using System.Net.Http;
using System.Threading;
using EchoSharp.Audio;
using ihnm.Helpers;
using EchoSharp.Onnx.Sherpa.SpeechTranscription;
using SherpaOnnx;
using System.Text.RegularExpressions;
using AvaloniaEdit;



namespace ihnm.Managers
{
    public class SpeechToTextManager
    {
        public MMDevice microphone;
        private WasapiCapture waveIn; 
        private static int frameSize;

        string sherpafolder = "sherpa/stt-models/";

        private static readonly Lazy<HttpClient> httpClient = new Lazy<HttpClient>(() => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });


        IRealtimeSpeechTranscriptor realTimeTranscriptor;
        WasapiInputSource micAudioSource;

        OfflineStream voiceStream;

        OfflineRecognizer voiceRecognizer;

        int SampleRate = 16000;



        public SpeechToTextManager(MMDevice microphone) 
        {
            this.microphone = microphone;

            this.micAudioSource = new WasapiInputSource(this.microphone);

            this.SampleRate = (int)this.micAudioSource.SampleRate;

            new Thread(() =>
            {
                LoadSTT();
            }).Start();

            

        }

        private async Task LoadSTT()
        {
            



            var vadDetectorFactory = GetSileroVadDetector();
            var speechTranscriptorFactory = GetSherpaOnnxTranscriptor();


            var realTimeFactory = GetEchoSharpTranscriptorFactory(speechTranscriptorFactory, vadDetectorFactory);

            this.realTimeTranscriptor = realTimeFactory.Create(new RealtimeSpeechTranscriptorOptions()
            {
                AutodetectLanguageOnce = false, // Flag to detect the language only once or for each segment
                IncludeSpeechRecogizingEvents = false, // Flag to include speech recognizing events (RealtimeSegmentRecognizing)
                RetrieveTokenDetails = false, // Flag to retrieve token details
                LanguageAutoDetect = false, // Flag to auto-detect the language
                Language = new CultureInfo("en-US"), // Language to use for transcription
            });


            var microphoneTask = Task.Run(() =>
            {
                //this.micAudioSource.NewData += MicAudioSource_NewData;
                this.micAudioSource.StartRecording();
                this.CallOutput($"STT is ready. Say something");
            });


            var showTranscriptTask = ShowTranscriptAsync();

            var firstReady = await Task.WhenAny(microphoneTask, showTranscriptTask);
            await firstReady;

            //await showTranscriptTask;

            await Task.WhenAll(microphoneTask, showTranscriptTask);



        }

        private void MicAudioSource_NewData(object myObject, WasapiInputSource.NewDataEventArgs myArgs)
        {
            WaveBuffer wBuffer = new WaveBuffer(myArgs.buffer);

            voiceStream.AcceptWaveform(this.SampleRate,wBuffer.FloatBuffer);
        }

        IVadDetectorFactory GetSileroVadDetector()
        {
            // Replace with the path to the Silero VAD ONNX model (Download from here): https://github.com/snakers4/silero-vad/blob/master/src/silero_vad/data/silero_vad.onnx
            // Or execute `downloadModels.ps1` script in the root of this repository
            var sileroOnnxPath = "sherpa/vad-models/silero_vad_v5/silero_vad_v5.onnx";
            return new SileroVadDetectorFactory(new SileroVadOptions(sileroOnnxPath)
            {
                Threshold = 0.5f, // The threshold for Silero VAD. The default is 0.5f.
                ThresholdGap = 0.15f, // The threshold gap for Silero VAD. The default is 0.15f.
            });
        }

        ISpeechTranscriptorFactory GetSherpaOnnxTranscriptor()
        {
            var config = new OfflineRecognizerConfig();

            // Replace with your own model path (download from here: https://github.com/k2-fsa/sherpa-onnx/releases/tag/asr-models)

            config.FeatConfig.SampleRate = SampleRate;



            config.ModelConfig.Provider = "cuda";
            config.ModelConfig.NumThreads = 4;
            config.ModelConfig.Debug = 1;

            SherpaOnnxSpeechTranscriptorFactory factory=null;

            if (Common.sherpaSTTmodel.Contains("zipformer")|| Common.sherpaSTTmodel.Contains("transducer"))
            {
                config.ModelConfig.Tokens = "./" + sherpafolder + Common.sherpaSTTmodel + "/tokens.txt";

                string encoderMdl = "";
                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match encMatch = Regex.Match(fname, "^encoder.*.onnx$");
                    if (encMatch.Success && !Regex.Match(fname, "^encoder.*.int8.onnx$").Success)
                    {
                        encoderMdl = encMatch.Value;
                    }
                }

                config.ModelConfig.Transducer.Encoder = "./" + sherpafolder + Common.sherpaSTTmodel + "/" + encoderMdl;


                string decoderMdl = "";

                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match decMatch = Regex.Match(fname, "^decoder.*.onnx$");
                    if (decMatch.Success && !Regex.Match(fname, "^decoder.*.int8.onnx$").Success)
                    {
                        decoderMdl = decMatch.Value;
                    }
                }



                config.ModelConfig.Transducer.Decoder = "./" + sherpafolder + Common.sherpaSTTmodel + "/" + decoderMdl;


                string joinerMdl = "";

                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match joinMatch = Regex.Match(fname, "^joiner.*.onnx$");
                    if (joinMatch.Success && !Regex.Match(fname, "^joiner.*.int8.onnx$").Success)
                    {
                        joinerMdl = joinMatch.Value;
                    }
                }



                config.ModelConfig.Transducer.Joiner = "./" + sherpafolder + Common.sherpaSTTmodel + "/" + joinerMdl;

                if (!File.Exists("hotwords/hotwords.txt"))
                {
                    Directory.CreateDirectory("hotwords");
                    File.CreateText("hotwords/hotwords.txt").Close();
                }

                config.HotwordsFile = "hotwords/hotwords.txt";

                factory = new SherpaOnnxSpeechTranscriptorFactory(new SherpaOnnxOfflineTranscriptorOptions()
                {
                    OfflineModelConfig = config.ModelConfig
                });
            }
            else if (Common.sherpaSTTmodel.Contains("paraformer"))
            {
                config.ModelConfig.Tokens = "./" + sherpafolder + Common.sherpaSTTmodel + "/tokens.txt";

                string paraformerMdl = "";
                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match encMatch = Regex.Match(fname, "^.*.onnx$");
                    if (encMatch.Success && !Regex.Match(fname, "^.*.int8.onnx$").Success)
                    {
                        paraformerMdl = encMatch.Value;
                    }
                }

                config.ModelConfig.Paraformer.Model = "./" + sherpafolder + Common.sherpaSTTmodel + "/" + paraformerMdl;


                factory = new SherpaOnnxSpeechTranscriptorFactory(new SherpaOnnxOfflineTranscriptorOptions()
                {
                    OfflineModelConfig = config.ModelConfig
                });
            }
            else if (Common.sherpaSTTmodel.Contains("whisper"))
            {
                string whisperEncoder = "";
                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match encMatch = Regex.Match(fname, ".*encoder.onnx$");
                    if (encMatch.Success && !Regex.Match(fname, "^.*.int8.onnx$").Success)
                    {
                        whisperEncoder = encMatch.Value;
                    }
                }

                string whisperDecoder = "";
                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match encMatch = Regex.Match(fname, ".*decoder.onnx$");
                    if (encMatch.Success && !Regex.Match(fname, "^.*.int8.onnx$").Success)
                    {
                        whisperDecoder = encMatch.Value;
                    }
                }

                string tokens = "";
                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match encMatch = Regex.Match(fname, ".*tokens.txt$");
                    if (encMatch.Success)
                    {
                        tokens = encMatch.Value;
                    }
                }

                config.ModelConfig.Whisper.Encoder= "./" + sherpafolder + Common.sherpaSTTmodel + "/" + whisperEncoder;
                config.ModelConfig.Whisper.Decoder = "./" + sherpafolder + Common.sherpaSTTmodel + "/" + whisperDecoder;
                config.ModelConfig.Tokens = "./" + sherpafolder + Common.sherpaSTTmodel + "/" + tokens;


                factory = new SherpaOnnxSpeechTranscriptorFactory(new SherpaOnnxOfflineTranscriptorOptions()
                {
                    OfflineModelConfig = config.ModelConfig
                });

            }
            else if (Common.sherpaSTTmodel.Contains("ctc") && !Common.sherpaSTTmodel.Contains("dolphin"))
            {
                config.ModelConfig.Tokens = "./" + sherpafolder + Common.sherpaSTTmodel + "/tokens.txt";

                string ctcModel = "";
                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match encMatch = Regex.Match(fname, ".*.onnx$");
                    if (encMatch.Success && !Regex.Match(fname, "^.*.int8.onnx$").Success)
                    {
                        ctcModel = encMatch.Value;
                    }
                }

                config.ModelConfig.NeMoCtc.Model= "./" + sherpafolder + Common.sherpaSTTmodel + "/" + ctcModel;  


                factory = new SherpaOnnxSpeechTranscriptorFactory(new SherpaOnnxOfflineTranscriptorOptions()
                {
                    OfflineModelConfig = config.ModelConfig
                });


            }
            else if (Common.sherpaSTTmodel.Contains("dolphin"))
            {
                config.ModelConfig.Tokens = "./" + sherpafolder + Common.sherpaSTTmodel + "/tokens.txt";

                string dolphinModel = "";
                foreach (string f in Directory.GetFiles(sherpafolder + Common.sherpaSTTmodel + "/"))
                {
                    string fname = Path.GetFileName(f);
                    Match encMatch = Regex.Match(fname, ".*.onnx$");
                    if (encMatch.Success && !Regex.Match(fname, "^.*.int8.onnx$").Success)
                    {
                        dolphinModel = encMatch.Value;
                    }
                }

                config.ModelConfig.Dolphin.Model = "./" + sherpafolder + Common.sherpaSTTmodel + "/" + dolphinModel;


                factory = new SherpaOnnxSpeechTranscriptorFactory(new SherpaOnnxOfflineTranscriptorOptions()
                {
                    OfflineModelConfig = config.ModelConfig
                });


            }



            return factory;

        }

        IRealtimeSpeechTranscriptorFactory GetEchoSharpTranscriptorFactory(ISpeechTranscriptorFactory speechTranscriptorFactory, IVadDetectorFactory vadDetectorFactory)
        {
            return new EchoSharpRealtimeTranscriptorFactory(speechTranscriptorFactory, vadDetectorFactory, echoSharpOptions: new EchoSharpRealtimeOptions()
            {
                ConcatenateSegmentsToPrompt = false // Flag to concatenate segments to prompt when new segment is recognized (for the whole session)
            });
        }

        async Task ShowTranscriptAsync()
        {

            await foreach (var transcription in realTimeTranscriptor.TranscribeAsync(micAudioSource))
            {
                var eventType = transcription.GetType().Name;

                if (eventType != "RealtimeSegmentRecognized")
                    continue;

                var segmentText = transcription switch
                {
                    RealtimeSegmentRecognized segmentRecognized => segmentRecognized.Segment.Text,
                    _ => string.Empty
                };


                this.CallSegmentRecognized(segmentText);

            }
        }

        public class SegmentRecognizedEventArgs : EventArgs
        {
            public string SegmentText { get; set; }
        }

        protected virtual void CallSegmentRecognized(string SegmentText)
        {
            SegmentRecognizedEventArgs e = new SegmentRecognizedEventArgs();
            e.SegmentText = SegmentText;
            SegmentRecognized?.Invoke(this, e);
        }

        public delegate void SegmentRecognizedEventHandler(object myObject, SegmentRecognizedEventArgs myArgs);

        public event SegmentRecognizedEventHandler SegmentRecognized;

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
}

 