using ihnm.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace ihnm.Managers
{
    public static class WordCountersManager
    {

        public static Timer updateTimer;
        public static EnumLanguage currentLang;

        public static Dictionary<EnumLanguage, Dictionary<string, int>> wordCounters;
        public static Dictionary<EnumLanguage,List<string>> sortedWords;


        public static void runUpdateTimer(EnumLanguage lang)
        {

            currentLang = lang;

            updateTimer = new Timer();
            updateTimer.Interval = 20000;
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();

        }

        private static void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            string newJson = JsonSerializer.Serialize(wordCounters[currentLang],options);
            File.WriteAllText("wordcounters/" + currentLang.ToString() + ".json", newJson);
        }

        public static void initWordCounters()
        {
            if (!Directory.Exists("wordcounters/"))
            {
                Directory.CreateDirectory("wordcounters/");
            }

            wordCounters = new Dictionary<EnumLanguage, Dictionary<string, int>>();
            sortedWords = new Dictionary<EnumLanguage, List<string>>();
            foreach (string f in Directory.GetFiles("wordcounters/"))
            {
                EnumLanguage lang = (EnumLanguage)Enum.Parse(typeof(EnumLanguage), Path.GetFileNameWithoutExtension(f));
                
                wordCounters[lang] = JsonSerializer.Deserialize<Dictionary<string, int>>(File.OpenRead(f));
                if (wordCounters[lang].Count>0)
                    sortedWords.Add(lang,(from entry in wordCounters[lang] orderby entry.Value ascending select entry.Key).ToList());
            }

        }

        public static void incWord ( string word)
        {
            if (!wordCounters.ContainsKey(currentLang))
            {
                wordCounters.Add(currentLang, new Dictionary<string, int>());
            }
            if (!wordCounters[currentLang].ContainsKey(word))
            {
                wordCounters[currentLang].Add(word, 0);
            }
            wordCounters[currentLang][word] += 1;

        }

        public static void parseSentence( List<string> sentenceList, List<string> wordlist)
        {
            foreach (string wrd in sentenceList)
            {
                if (wordlist.Contains(wrd))
                    incWord(wrd);
            }
        }

    }
}
