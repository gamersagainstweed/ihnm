using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ihnm.Helpers
{
    public class SyncOffsetSampleProvider:OffsetSampleProvider
    {
        SyncMaster syncMstr;

        public SyncOffsetSampleProvider(ISampleProvider sourceProvider, ref SyncMaster syncMstr): base(sourceProvider)
        {
            this.syncMstr = syncMstr;
            this.syncMstr.SyncSong += SyncMstr_SyncSong;
        }

        private void SyncMstr_SyncSong(object? sender, EventArgs e)
        {
            this.Skip(SkipOverHelper.getStartTime(syncMstr.currentSoundLength));
        }

    }
}
