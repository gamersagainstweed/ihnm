#region Assembly EchoSharp.NAudio, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// C:\Users\Gamespace\.nuget\packages\echosharp.naudio\0.1.0\lib\net8.0\EchoSharp.NAudio.dll
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464
#endregion

using System;
using System.Diagnostics;
using EchoSharp.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace ihnm.Helpers;

public class WasapiInputSource : AwaitableWaveFileSource
{
    private readonly WasapiCapture microphoneIn;

    public WasapiInputSource(MMDevice microphone, int sampleRate = 16000, int bitsPerSample = 16, int channels = 1, bool storeSamples = true, bool storeBytes = false, int initialSizeFloats = 16384, int initialSizeBytes = 16384, IChannelAggregationStrategy? aggregationStrategy = null)
        : base(storeSamples, storeBytes, initialSizeFloats, initialSizeBytes, aggregationStrategy)
    {
        microphoneIn = new WasapiCapture(microphone)
        {
            WaveFormat = new WaveFormat(sampleRate, bitsPerSample, channels)
        };
        Initialize(new AudioSourceHeader
        {
            BitsPerSample = (ushort)bitsPerSample,
            Channels = (ushort)channels,
            SampleRate = (uint)sampleRate
        });
        microphoneIn.DataAvailable += WaveIn_DataAvailable;
        microphoneIn.RecordingStopped += MicrophoneIn_RecordingStopped;
    }

    public void StartRecording()
    {
        microphoneIn.StartRecording();
    }

    public void StopRecording()
    {
        microphoneIn.StopRecording();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            microphoneIn.DataAvailable -= WaveIn_DataAvailable;
            microphoneIn.RecordingStopped -= MicrophoneIn_RecordingStopped;
            microphoneIn.Dispose();
        }

        base.Dispose(disposing);
    }

    private void MicrophoneIn_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            throw e.Exception;
        }

        Flush();
    }

    private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
    {
        WriteData(e.Buffer.AsMemory(0, e.BytesRecorded));
        //RaiseNewData(e.Buffer);
    }

    public class NewDataEventArgs : EventArgs
    {
        public byte[] buffer { get; set; }
    }

    protected virtual void RaiseNewData(byte[] buffer)
    {
        NewDataEventArgs e = new NewDataEventArgs();
        e.buffer = buffer;
        NewData?.Invoke(this, e);
    }

    public delegate void NewDataEventHandler(object myObject, NewDataEventArgs myArgs);

    public event NewDataEventHandler NewData;


}
#if false // Decompilation log
'254' items in cache
------------------
Resolve: 'System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Found single assembly: 'System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.dll'
------------------
Resolve: 'EchoSharp, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'EchoSharp, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null'
Load from: 'C:\Users\Gamespace\.nuget\packages\echosharp\0.1.0\lib\net8.0\EchoSharp.dll'
------------------
Resolve: 'NAudio.WinMM, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Found single assembly: 'NAudio.WinMM, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Load from: 'C:\Users\Gamespace\.nuget\packages\naudio.winmm\2.2.1\lib\netstandard2.0\NAudio.WinMM.dll'
------------------
Resolve: 'NAudio.Core, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Found single assembly: 'NAudio.Core, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Load from: 'C:\Users\Gamespace\.nuget\packages\naudio.core\2.2.1\lib\netstandard2.0\NAudio.Core.dll'
------------------
Resolve: 'NAudio, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Found single assembly: 'NAudio, Version=2.2.1.0, Culture=neutral, PublicKeyToken=e279aa5131008a41'
Load from: 'C:\Users\Gamespace\.nuget\packages\naudio\2.2.1\lib\net6.0\NAudio.dll'
------------------
Resolve: 'EchoSharp.Abstractions, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'EchoSharp.Abstractions, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null'
Load from: 'C:\Users\Gamespace\.nuget\packages\echosharp.abstractions\0.1.0\lib\net8.0\EchoSharp.Abstractions.dll'
------------------
Resolve: 'System.Memory, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'System.Memory, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Memory.dll'
------------------
Resolve: 'System.Runtime.InteropServices, Version=8.0.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'System.Runtime.InteropServices, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.InteropServices.dll'
------------------
Resolve: 'System.Runtime.CompilerServices.Unsafe, Version=8.0.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'System.Runtime.CompilerServices.Unsafe, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'
Load from: 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.12\ref\net8.0\System.Runtime.CompilerServices.Unsafe.dll'
#endif

