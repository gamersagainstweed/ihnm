using Avalonia.Media;
using ihnm.Enums;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ihnm.Managers.DownloadManager;


namespace ihnm.Managers
{
    public static class Common
    {
        public static List<string> voices = new List<string>() {};
        
        public static Dictionary<string,(string,int,double,EnumLanguage,EnumGender)> sherpaVoices = 
            new Dictionary<string, (string, int, double, EnumLanguage, EnumGender)>();


        public static List<string> sherpaModels = new List<string>();
        public static Dictionary<string, sherpaTTSmodel> sherpaModelsDict = new Dictionary<string, sherpaTTSmodel>();


        public static string voice = "glados";
        public static EnumLanguage voiceLanguage;
        public static double pitch = 1;
        public static double volume = 1;
        public static double tempo = 1;
        public static double offset = 230;
        public static double concatoffset = 120;

        public static double voicethreshold = -10;

        public static double dotdelay = 400;

        public static bool forceStop = false;

        public static string sentence="";
        public static string sentence2="";

        public static string sentenceFull { get { 
                if (sentence2.Length>0)
                    return sentence + sentence2;
                else
                    return sentence;
            }}

        public static string virtualcablename;
        public static string playbackname;

        public static bool sttEnabled = false;
        public static string sherpaSTTmodel = "";
        public static bool isSttRealtime = false;

        public static int cursorPos=0;

        public static double playbackVolume = 1;
        public static double micVolume = 1;

        public static bool isLipsyncOn = false;
        public static double lipsyncThreshold = 0.07;
        public static TimeSpan lipsyncDelay = TimeSpan.FromMilliseconds(70);


        public static bool downloadInProcess = false;

        public static bool enableMicToCable = false;


        public static Color soundColor = Color.Parse("LightGreen");
        public static Color musicColor = Color.Parse("Yellow");
        public static Color loopColor = Color.Parse("#a347ff");
        public static Color songColor = Color.Parse("Lime");

        public static string songsFolder = "sounds/songs/";

        public static int ping=0;

        public static KeyCode invokeSpecialKey = KeyCode.VcUndefined;
        public static KeyCode invokeKey = KeyCode.VcY;

        public static bool hodlvtt = false;
        public static KeyCode vttKey = KeyCode.VcQ;

    }


}
