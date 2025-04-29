#region Assembly NAudio.Wasapi, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41
// C:\Users\Gamespace\.nuget\packages\naudio.wasapi\2.2.1\lib\netstandard2.0\NAudio.Wasapi.dll
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Fizzler;
using NAudio.CoreAudioApi;
using NAudio.Utils;
using NAudio.Wave.SampleProviders;
using ihnm.Helpers;
using SoundTouch.Net.NAudioSupport;
using static ihnm.AudioPlaybackEngine;

namespace NAudio.Wave;


public class WaveInfo
{
    public IWaveProvider waveProvider { get; set; }
    public TimeSpan length { get; set; }
    public TimeSpan lipsyncDelay { get; set; }
    public double lipsyncThreshold { get; set; }

    public WaveInfo(IWaveProvider waveProvider, TimeSpan length, TimeSpan lipsyncDelay, double lipsyncThreshold)
    {
        this.waveProvider = waveProvider;
        this.length = length;
        this.lipsyncDelay = lipsyncDelay;
        this.lipsyncThreshold = lipsyncThreshold;
    }

}


//
// Summary:
//     Support for playback using Wasapi
public class SyncWasapiOut : IWavePlayer, IDisposable, IWavePosition
{
    private AudioClient audioClient;

    private readonly MMDevice mmDevice;

    private readonly AudioClientShareMode shareMode;

    private AudioRenderClient renderClient;

    private IWaveProvider sourceProvider;

    private int latencyMilliseconds;

    private int bufferFrameCount;

    private int bytesPerFrame;

    private int bytesPerMillisecond;

    private readonly bool isUsingEventSync;

    private EventWaitHandle frameEventWaitHandle;

    private byte[] readBuffer;

    private volatile PlaybackState playbackState;

    private Thread playThread;

    private readonly SynchronizationContext syncContext;

    private bool dmoResamplerNeeded;

    public TimeSpan TotalTime;

    public int currentSoundTime = 0;
    public int skipOffset=0;
    public bool skipComplete = false;

    TimeSpan toSkip;
    Stopwatch timewatch;

    Queue<WaveInfo> playbackQueue = new Queue<WaveInfo>();
    WaveInfo currentWave;

    //
    // Summary:
    //     Gets a NAudio.Wave.WaveFormat instance indicating the format the hardware is
    //     using.
    public WaveFormat OutputWaveFormat { get; private set; }

    //
    // Summary:
    //     Playback State
    public PlaybackState PlaybackState => playbackState;

    //
    // Summary:
    //     Volume
    public float Volume
    {
        get
        {
            return mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
        }
        set
        {
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException("value", "Volume must be between 0.0 and 1.0");
            }

            if (value > 1f)
            {
                throw new ArgumentOutOfRangeException("value", "Volume must be between 0.0 and 1.0");
            }

            mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar = value;
        }
    }

    public TimeSpan currentTime
        {
        get { return this.toSkip + timewatch.Elapsed; }
        }

    //
    // Summary:
    //     Retrieve the AudioStreamVolume object for this audio stream
    //
    // Exceptions:
    //   T:System.InvalidOperationException:
    //     This is thrown when an exclusive audio stream is being used.
    //
    // Remarks:
    //     This returns the AudioStreamVolume object ONLY for shared audio streams.
    public AudioStreamVolume AudioStreamVolume
    {
        get
        {
            if (shareMode == AudioClientShareMode.Exclusive)
            {
                throw new InvalidOperationException("AudioStreamVolume is ONLY supported for shared audio streams.");
            }

            return audioClient.AudioStreamVolume;
        }
    }

    //
    // Summary:
    //     Playback Stopped
    public event EventHandler<StoppedEventArgs> PlaybackStopped;

    //
    // Summary:
    //     WASAPI Out shared mode, default
    public SyncWasapiOut()
        : this(GetDefaultAudioEndpoint(), AudioClientShareMode.Shared, useEventSync: true, 200)
    {
    }

    //
    // Summary:
    //     WASAPI Out using default audio endpoint
    //
    // Parameters:
    //   shareMode:
    //     ShareMode - shared or exclusive
    //
    //   latency:
    //     Desired latency in milliseconds
    public SyncWasapiOut(AudioClientShareMode shareMode, int latency)
        : this(GetDefaultAudioEndpoint(), shareMode, useEventSync: true, latency)
    {
    }

    //
    // Summary:
    //     WASAPI Out using default audio endpoint
    //
    // Parameters:
    //   shareMode:
    //     ShareMode - shared or exclusive
    //
    //   useEventSync:
    //     true if sync is done with event. false use sleep.
    //
    //   latency:
    //     Desired latency in milliseconds
    public SyncWasapiOut(AudioClientShareMode shareMode, bool useEventSync, int latency)
        : this(GetDefaultAudioEndpoint(), shareMode, useEventSync, latency)
    {
    }

    //
    // Summary:
    //     Creates a new WASAPI Output
    //
    // Parameters:
    //   device:
    //     Device to use
    //
    //   shareMode:
    //
    //   useEventSync:
    //     true if sync is done with event. false use sleep.
    //
    //   latency:
    //     Desired latency in milliseconds
    public SyncWasapiOut(MMDevice device, AudioClientShareMode shareMode, bool useEventSync, int latency)
    {
        audioClient = device.AudioClient;
        mmDevice = device;
        this.shareMode = shareMode;
        isUsingEventSync = useEventSync;
        latencyMilliseconds = latency;
        syncContext = SynchronizationContext.Current;
        OutputWaveFormat = audioClient.MixFormat;
    }

    private static MMDevice GetDefaultAudioEndpoint()
    {
        if (Environment.OSVersion.Version.Major < 6)
        {
            throw new NotSupportedException("WASAPI supported only on Windows Vista and above");
        }

        return new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    }

    private void PlayThread(int ping)
    {
        ResamplerDmoStream resamplerDmoStream = null;
        IWaveProvider playbackProvider = sourceProvider;
        Exception e = null;
        try
        {

            bufferFrameCount = audioClient.BufferSize;
            bytesPerFrame = OutputWaveFormat.Channels * OutputWaveFormat.BitsPerSample / 8;
            readBuffer = BufferHelpers.Ensure(readBuffer, bufferFrameCount * bytesPerFrame);
            WaitHandle[] waitHandles = new WaitHandle[1] { frameEventWaitHandle };
            audioClient.Start();


            bytesPerMillisecond = playbackProvider.WaveFormat.AverageBytesPerSecond / 1000;


            toSkip = SkipOverHelper.getStartTimeTask(this.TotalTime, ping).Result;
            timewatch = Stopwatch.StartNew();

            skipComplete = false;
            currentSoundTime = 0;

            TimeSpan currentTimeSpan = TimeSpan.Zero;

            int skipped = 0;


            if (currentWave != null)
            {
                while (true)
                {
                    if (toSkip > currentTimeSpan + currentWave.length)
                    {
                        currentTimeSpan += currentWave.length;
                        currentWave = playbackQueue.Dequeue();
                        skipped++;
                    }
                    else
                    {
                        break;
                    }
                }
                playbackProvider = currentWave.waveProvider;
            }
            toSkip -= currentTimeSpan;


            while (playbackState != 0)
            {

                if (playbackState == PlaybackState.Playing)
                {
                    int num = ((!isUsingEventSync) ? audioClient.CurrentPadding : ((shareMode == AudioClientShareMode.Shared) ? audioClient.CurrentPadding : 0));
                    int num2;

                    if (skipComplete == false)
                        num2 = (bufferFrameCount - num) / 70;
                    else
                        num2 = (bufferFrameCount - num)/40;

                    if (num2 > 10 && FillBuffer(playbackProvider, num2, skipped))
                    {
                        if (this.playbackQueue.Count==0)
                            break;
                        else
                        {
                            playbackProvider = this.playbackQueue.Dequeue().waveProvider;
                            skipped++;
                            this.CallLipsync(skipped);
                        }
                            

                    }
                }

                if (skipComplete==true)
                    Thread.Sleep(latencyMilliseconds / 2);
                

            }

            if (playbackState == PlaybackState.Playing)
            {
                Thread.Sleep(isUsingEventSync ? latencyMilliseconds : (latencyMilliseconds / 2));
            }

            timewatch.Stop();
            audioClient.Stop();
            playbackState = PlaybackState.Stopped;
            audioClient.Reset();
        }
        catch (Exception ex)
        {
            e = ex;
        }
        finally
        {
            resamplerDmoStream?.Dispose();
            RaisePlaybackStopped(e);
        }
    }

    private void RaisePlaybackStopped(Exception e)
    {
        EventHandler<StoppedEventArgs> handler = this.PlaybackStopped;
        if (handler == null)
        {
            return;
        }

        if (syncContext == null)
        {
            handler(this, new StoppedEventArgs(e));
            return;
        }

        syncContext.Post(delegate
        {
            handler(this, new StoppedEventArgs(e));
        }, null);
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

    //
    // Summary:
    //     returns true if reached the end
    private unsafe bool FillBuffer(IWaveProvider playbackProvider, int frameCount, int skipped=0)
    {

        int num = frameCount * bytesPerFrame;


        //var watch2 = Stopwatch.StartNew();
        int num2 = playbackProvider.Read(readBuffer, 0, num);
        if (num2 == 0)
        {
            return true;
        }

        this.currentSoundTime += num;

        

        if (this.skipComplete==false)
        {
            TimeSpan curTime = this.currentTime;
            this.skipOffset = (int)curTime.TotalMilliseconds * bytesPerMillisecond;

            //if ((this.skipOffset < this.currentSoundTime))
            //{
            //    int div = this.skipOffset / num;
            //    this.skipOffset = div * num;
            //}

            if (this.skipOffset > this.currentSoundTime - num)
                return false;

            this.skipComplete = true;

            this.CallLipsync(skipped);

        }

  


        IntPtr buffer = renderClient.GetBuffer(frameCount);

        Marshal.Copy(readBuffer, 0, buffer, num2);



        //var watch4 = Stopwatch.StartNew();
        if (isUsingEventSync && shareMode == AudioClientShareMode.Exclusive)
        {
            if (num2 < num)
            {
                byte* ptr = (byte*)(void*)buffer;
                while (num2 < num)
                {
                    ptr[num2++] = 0;
                }
            }

            renderClient.ReleaseBuffer(frameCount, AudioClientBufferFlags.None);
        }
        else
        {
            int numFramesWritten = num2 / bytesPerFrame;
            renderClient.ReleaseBuffer(numFramesWritten, AudioClientBufferFlags.None);
        }

        return false;
    }


    public class SyncwoutLipsyncEventArgs : EventArgs
    {
        public int skipped { get; set; }
    }

    protected void CallLipsync(int skipped)
    {
        SyncwoutLipsyncEventArgs e = new SyncwoutLipsyncEventArgs();
        e.skipped = skipped;
        SyncwoutLipsync?.Invoke(null, e);
    }

    public delegate void SyncwoutLipsyncEventHandler(object myObject, SyncwoutLipsyncEventArgs myArgs);

    public event SyncwoutLipsyncEventHandler SyncwoutLipsync;


    private WaveFormat GetFallbackFormat()
    {
        int sampleRate = audioClient.MixFormat.SampleRate;
        int channels = audioClient.MixFormat.Channels;
        List<int> list = new List<int> { OutputWaveFormat.SampleRate };
        if (!list.Contains(sampleRate))
        {
            list.Add(sampleRate);
        }

        if (!list.Contains(44100))
        {
            list.Add(44100);
        }

        if (!list.Contains(48000))
        {
            list.Add(48000);
        }

        List<int> list2 = new List<int> { OutputWaveFormat.Channels };
        if (!list2.Contains(channels))
        {
            list2.Add(channels);
        }

        if (!list2.Contains(2))
        {
            list2.Add(2);
        }

        List<int> list3 = new List<int> { OutputWaveFormat.BitsPerSample };
        if (!list3.Contains(32))
        {
            list3.Add(32);
        }

        if (!list3.Contains(24))
        {
            list3.Add(24);
        }

        if (!list3.Contains(16))
        {
            list3.Add(16);
        }

        foreach (int item in list)
        {
            foreach (int item2 in list2)
            {
                foreach (int item3 in list3)
                {
                    WaveFormatExtensible waveFormatExtensible = new WaveFormatExtensible(item, item3, item2);
                    if (audioClient.IsFormatSupported(shareMode, waveFormatExtensible))
                    {
                        return waveFormatExtensible;
                    }
                }
            }
        }

        throw new NotSupportedException("Can't find a supported format to use");
    }

    //
    // Summary:
    //     Gets the current position in bytes from the wave output device. (n.b. this is
    //     not the same thing as the position within your reader stream)
    //
    // Returns:
    //     Position in bytes
    public long GetPosition()
    {
        ulong position;
        switch (playbackState)
        {
            case PlaybackState.Stopped:
                return 0L;
            case PlaybackState.Playing:
                position = audioClient.AudioClockClient.AdjustedPosition;
                break;
            default:
                {
                    audioClient.AudioClockClient.GetPosition(out position, out var _);
                    break;
                }
        }

        return (long)position * (long)OutputWaveFormat.AverageBytesPerSecond / (long)audioClient.AudioClockClient.Frequency;
    }

    //
    // Summary:
    //     Begin Playback
    public void Play(TimeSpan TotalTime, int ping=0)
    {
        this.TotalTime = TotalTime;
        if (playbackState != PlaybackState.Playing)
        {
            if (playbackState == PlaybackState.Stopped)
            {
                playThread = new Thread(()=>PlayThread(ping))
                {
                    IsBackground = true
                };
                playbackState = PlaybackState.Playing;
                playThread.Start();
            }
            else
            {
                playbackState = PlaybackState.Playing;
            }
        }
    }

    public void Play()
    {
        if (playbackState != PlaybackState.Playing)
        {
            if (playbackState == PlaybackState.Stopped)
            {
                playThread = new Thread(() => PlayThread(0))
                {
                    IsBackground = true
                };
                playbackState = PlaybackState.Playing;
                playThread.Start();
            }
            else
            {
                playbackState = PlaybackState.Playing;
            }
        }
    }

    //
    // Summary:
    //     Stop playback and flush buffers
    public void Stop()
    {
        if (playbackState != 0)
        {
            playbackState = PlaybackState.Stopped;
            playThread.Join();
            playThread = null;
        }
    }

    //
    // Summary:
    //     Stop playback without flushing buffers
    public void Pause()
    {
        if (playbackState == PlaybackState.Playing)
        {
            playbackState = PlaybackState.Paused;
        }
    }


    public void Init(List<WaveInfo> waveInfos)
    {
        
        for (int i = 0; i < waveInfos.Count; i++)
        {

            this.playbackQueue.Enqueue(waveInfos[i]);

        }

        this.currentWave = this.playbackQueue.Dequeue();

        this.Init(this.currentWave.waveProvider);

    }
    //
    // Summary:
    //     Initialize for playing the specified wave stream
    //
    // Parameters:
    //   waveProvider:
    //     IWaveProvider to play
    public void Init(IWaveProvider waveProvider)
    {
        long num = (long)latencyMilliseconds * 10000L;
        OutputWaveFormat = waveProvider.WaveFormat;
        AudioClientStreamFlags audioClientStreamFlags = AudioClientStreamFlags.SrcDefaultQuality | AudioClientStreamFlags.AutoConvertPcm;
        sourceProvider = waveProvider;
        if (shareMode == AudioClientShareMode.Exclusive)
        {
            audioClientStreamFlags = AudioClientStreamFlags.None;
            if (!audioClient.IsFormatSupported(shareMode, OutputWaveFormat, out var closestMatchFormat))
            {
                if (closestMatchFormat == null)
                {
                    OutputWaveFormat = GetFallbackFormat();
                }
                else
                {
                    OutputWaveFormat = closestMatchFormat;
                }

                try
                {
                    using (new ResamplerDmoStream(waveProvider, OutputWaveFormat))
                    {
                    }
                }
                catch (Exception)
                {
                    OutputWaveFormat = GetFallbackFormat();
                    using (new ResamplerDmoStream(waveProvider, OutputWaveFormat))
                    {
                    }
                }

                dmoResamplerNeeded = true;
            }
            else
            {
                dmoResamplerNeeded = false;
            }
        }

        if (isUsingEventSync)
        {
            if (shareMode == AudioClientShareMode.Shared)
            {
                audioClient.Initialize(shareMode, AudioClientStreamFlags.EventCallback | audioClientStreamFlags, num, 0L, OutputWaveFormat, Guid.Empty);
                long streamLatency = audioClient.StreamLatency;
                if (streamLatency != 0L)
                {
                    latencyMilliseconds = (int)(streamLatency / 10000);
                }
            }
            else
            {
                try
                {
                    audioClient.Initialize(shareMode, AudioClientStreamFlags.EventCallback | audioClientStreamFlags, num, num, OutputWaveFormat, Guid.Empty);
                }
                catch (COMException ex2)
                {
                    if (ex2.ErrorCode != -2004287463)
                    {
                        throw;
                    }

                    long num2 = (long)(10000000.0 / (double)OutputWaveFormat.SampleRate * (double)audioClient.BufferSize + 0.5);
                    audioClient.Dispose();
                    audioClient = mmDevice.AudioClient;
                    audioClient.Initialize(shareMode, AudioClientStreamFlags.EventCallback | audioClientStreamFlags, num2, num2, OutputWaveFormat, Guid.Empty);
                }
            }

            frameEventWaitHandle = new EventWaitHandle(initialState: false, EventResetMode.AutoReset);
            audioClient.SetEventHandle(frameEventWaitHandle.SafeWaitHandle.DangerousGetHandle());
        }
        else
        {
            audioClient.Initialize(shareMode, audioClientStreamFlags, num, 0L, OutputWaveFormat, Guid.Empty);
        }

        renderClient = audioClient.AudioRenderClient;
    }

    //
    // Summary:
    //     Dispose
    public void Dispose()
    {
        if (audioClient != null)
        {
            Stop();
            audioClient.Dispose();
            audioClient = null;
            renderClient = null;
        }
    }
}
#if false // Decompilation log
'260' items in cache
------------------
Resolve: 'netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
WARN: Version mismatch. Expected: '2.0.0.0', Got: '2.1.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\netstandard.dll'
------------------
Resolve: 'NAudio.Core, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Found single assembly: 'NAudio.Core, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Load from: 'C:\Users\Gamespace\.nuget\packages\naudio.core\2.2.1\lib\netstandard2.0\NAudio.Core.dll'
------------------
Resolve: 'System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.dll'
------------------
Resolve: 'System.IO.MemoryMappedFiles, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.IO.MemoryMappedFiles, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.MemoryMappedFiles.dll'
------------------
Resolve: 'System.IO.Pipes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.IO.Pipes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.Pipes.dll'
------------------
Resolve: 'System.Diagnostics.Process, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.Process, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Diagnostics.Process.dll'
------------------
Resolve: 'System.Security.Cryptography, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Security.Cryptography, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Security.Cryptography.dll'
------------------
Resolve: 'System.Memory, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Memory, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Memory.dll'
------------------
Resolve: 'System.Collections, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Collections, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Collections.dll'
------------------
Resolve: 'System.Collections.NonGeneric, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Collections.NonGeneric, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Collections.NonGeneric.dll'
------------------
Resolve: 'System.Collections.Concurrent, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Collections.Concurrent, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Collections.Concurrent.dll'
------------------
Resolve: 'System.ObjectModel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ObjectModel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.ObjectModel.dll'
------------------
Resolve: 'System.Collections.Specialized, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Collections.Specialized, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Collections.Specialized.dll'
------------------
Resolve: 'System.ComponentModel.TypeConverter, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ComponentModel.TypeConverter, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.ComponentModel.TypeConverter.dll'
------------------
Resolve: 'System.ComponentModel.EventBasedAsync, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ComponentModel.EventBasedAsync, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.ComponentModel.EventBasedAsync.dll'
------------------
Resolve: 'System.ComponentModel.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ComponentModel.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.ComponentModel.Primitives.dll'
------------------
Resolve: 'System.ComponentModel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.ComponentModel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.ComponentModel.dll'
------------------
Resolve: 'Microsoft.Win32.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'Microsoft.Win32.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\Microsoft.Win32.Primitives.dll'
------------------
Resolve: 'System.Console, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Console, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Console.dll'
------------------
Resolve: 'System.Data.Common, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Data.Common, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Data.Common.dll'
------------------
Resolve: 'System.Runtime.InteropServices, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.InteropServices, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.InteropServices.dll'
------------------
Resolve: 'System.Diagnostics.TraceSource, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.TraceSource, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Diagnostics.TraceSource.dll'
------------------
Resolve: 'System.Diagnostics.Contracts, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.Contracts, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Diagnostics.Contracts.dll'
------------------
Resolve: 'System.Diagnostics.TextWriterTraceListener, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.TextWriterTraceListener, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Diagnostics.TextWriterTraceListener.dll'
------------------
Resolve: 'System.Diagnostics.FileVersionInfo, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.FileVersionInfo, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Diagnostics.FileVersionInfo.dll'
------------------
Resolve: 'System.Diagnostics.StackTrace, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.StackTrace, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Diagnostics.StackTrace.dll'
------------------
Resolve: 'System.Diagnostics.Tracing, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Diagnostics.Tracing, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Diagnostics.Tracing.dll'
------------------
Resolve: 'System.Drawing.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Drawing.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Drawing.Primitives.dll'
------------------
Resolve: 'System.Linq.Expressions, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Linq.Expressions, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Linq.Expressions.dll'
------------------
Resolve: 'System.IO.Compression.Brotli, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Found single assembly: 'System.IO.Compression.Brotli, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.Compression.Brotli.dll'
------------------
Resolve: 'System.IO.Compression, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Found single assembly: 'System.IO.Compression, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.Compression.dll'
------------------
Resolve: 'System.IO.Compression.ZipFile, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Found single assembly: 'System.IO.Compression.ZipFile, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.Compression.ZipFile.dll'
------------------
Resolve: 'System.IO.FileSystem.DriveInfo, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.IO.FileSystem.DriveInfo, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.FileSystem.DriveInfo.dll'
------------------
Resolve: 'System.IO.FileSystem.Watcher, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.IO.FileSystem.Watcher, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.FileSystem.Watcher.dll'
------------------
Resolve: 'System.IO.IsolatedStorage, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.IO.IsolatedStorage, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.IO.IsolatedStorage.dll'
------------------
Resolve: 'System.Linq, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Linq, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Linq.dll'
------------------
Resolve: 'System.Linq.Queryable, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Linq.Queryable, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Linq.Queryable.dll'
------------------
Resolve: 'System.Linq.Parallel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Linq.Parallel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Linq.Parallel.dll'
------------------
Resolve: 'System.Threading.Thread, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading.Thread, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Threading.Thread.dll'
------------------
Resolve: 'System.Net.Requests, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.Requests, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.Requests.dll'
------------------
Resolve: 'System.Net.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.Primitives.dll'
------------------
Resolve: 'System.Net.HttpListener, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Net.HttpListener, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.HttpListener.dll'
------------------
Resolve: 'System.Net.ServicePoint, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Net.ServicePoint, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.ServicePoint.dll'
------------------
Resolve: 'System.Net.NameResolution, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.NameResolution, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.NameResolution.dll'
------------------
Resolve: 'System.Net.WebClient, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Net.WebClient, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.WebClient.dll'
------------------
Resolve: 'System.Net.Http, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.Http, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.Http.dll'
------------------
Resolve: 'System.Net.WebHeaderCollection, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.WebHeaderCollection, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.WebHeaderCollection.dll'
------------------
Resolve: 'System.Net.WebProxy, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Net.WebProxy, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.WebProxy.dll'
------------------
Resolve: 'System.Net.Mail, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Net.Mail, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.Mail.dll'
------------------
Resolve: 'System.Net.NetworkInformation, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.NetworkInformation, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.NetworkInformation.dll'
------------------
Resolve: 'System.Net.Ping, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.Ping, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.Ping.dll'
------------------
Resolve: 'System.Net.Security, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.Security, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.Security.dll'
------------------
Resolve: 'System.Net.Sockets, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.Sockets, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.Sockets.dll'
------------------
Resolve: 'System.Net.WebSockets.Client, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.WebSockets.Client, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.WebSockets.Client.dll'
------------------
Resolve: 'System.Net.WebSockets, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Net.WebSockets, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Net.WebSockets.dll'
------------------
Resolve: 'System.Runtime.Numerics, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.Numerics, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.Numerics.dll'
------------------
Resolve: 'System.Numerics.Vectors, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Numerics.Vectors, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Numerics.Vectors.dll'
------------------
Resolve: 'System.Reflection.DispatchProxy, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Reflection.DispatchProxy, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Reflection.DispatchProxy.dll'
------------------
Resolve: 'System.Reflection.Emit, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Reflection.Emit, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Reflection.Emit.dll'
------------------
Resolve: 'System.Reflection.Emit.ILGeneration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Reflection.Emit.ILGeneration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Reflection.Emit.ILGeneration.dll'
------------------
Resolve: 'System.Reflection.Emit.Lightweight, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Reflection.Emit.Lightweight, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Reflection.Emit.Lightweight.dll'
------------------
Resolve: 'System.Reflection.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Reflection.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Reflection.Primitives.dll'
------------------
Resolve: 'System.Resources.Writer, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Resources.Writer, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Resources.Writer.dll'
------------------
Resolve: 'System.Runtime.CompilerServices.VisualC, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.CompilerServices.VisualC, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.CompilerServices.VisualC.dll'
------------------
Resolve: 'System.Runtime.Serialization.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.Serialization.Primitives, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.Serialization.Primitives.dll'
------------------
Resolve: 'System.Runtime.Serialization.Xml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.Serialization.Xml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.Serialization.Xml.dll'
------------------
Resolve: 'System.Runtime.Serialization.Json, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.Serialization.Json, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.Serialization.Json.dll'
------------------
Resolve: 'System.Runtime.Serialization.Formatters, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime.Serialization.Formatters, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.Serialization.Formatters.dll'
------------------
Resolve: 'System.Security.Claims, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Security.Claims, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Security.Claims.dll'
------------------
Resolve: 'System.Text.Encoding.Extensions, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Text.Encoding.Extensions, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Text.Encoding.Extensions.dll'
------------------
Resolve: 'System.Text.RegularExpressions, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Text.RegularExpressions, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Text.RegularExpressions.dll'
------------------
Resolve: 'System.Threading, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Threading.dll'
------------------
Resolve: 'System.Threading.Overlapped, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading.Overlapped, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Threading.Overlapped.dll'
------------------
Resolve: 'System.Threading.ThreadPool, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading.ThreadPool, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Threading.ThreadPool.dll'
------------------
Resolve: 'System.Threading.Tasks.Parallel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Threading.Tasks.Parallel, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Threading.Tasks.Parallel.dll'
------------------
Resolve: 'System.Transactions.Local, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Transactions.Local, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Transactions.Local.dll'
------------------
Resolve: 'System.Web.HttpUtility, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Web.HttpUtility, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Web.HttpUtility.dll'
------------------
Resolve: 'System.Xml.ReaderWriter, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Xml.ReaderWriter, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Xml.ReaderWriter.dll'
------------------
Resolve: 'System.Xml.XDocument, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Xml.XDocument, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Xml.XDocument.dll'
------------------
Resolve: 'System.Xml.XmlSerializer, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Xml.XmlSerializer, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Xml.XmlSerializer.dll'
------------------
Resolve: 'System.Xml.XPath.XDocument, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Xml.XPath.XDocument, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Xml.XPath.XDocument.dll'
------------------
Resolve: 'System.Xml.XPath, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Xml.XPath, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Xml.XPath.dll'
------------------
Resolve: 'System.Runtime.CompilerServices.Unsafe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'System.Runtime.CompilerServices.Unsafe, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
WARN: Version mismatch. Expected: '2.0.0.0', Got: '8.0.0.0'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.CompilerServices.Unsafe.dll'
#endif
