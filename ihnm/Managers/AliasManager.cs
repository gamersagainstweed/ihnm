using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ihnm.Managers
{
    public class AliasManager
    {

        private string aliasesDir = "aliases/";

        public List<Alias> aliases = new List<Alias>();
        public Dictionary<string, Alias> aliasNames = new Dictionary<string,Alias>();
        public List<string> aliasNamesList = new List<string>();

        public Dictionary<string,string> oneLiners = new Dictionary<string,string>();

        public AliasManager()
        {
        }

        private void loadAliases()
        {
            if (!Directory.Exists(aliasesDir))
                Directory.CreateDirectory(aliasesDir);

            foreach (string file in Directory.GetFiles(aliasesDir, "*.txt", SearchOption.AllDirectories))
            {
                List<string> aliasLines = new List<string>();
                

                string name = Path.GetFileNameWithoutExtension(file);

                

                StreamReader sr = new StreamReader(file);

                while (!sr.EndOfStream)
                {
                    aliasLines.Add(sr.ReadLine());
                }


                sr.Close();

                Alias alias = new Alias(name, aliasLines);
                aliases.Add(alias);

                if (aliasLines.Count==1 && aliasLines[0][0]!='/')
                {
                    oneLiners.Add(name, aliasLines[0]);
                }

                fillNameList();
            }
        }

        public void loadAliasesForVoice()
        {
            aliases = new List<Alias>();
            oneLiners = new Dictionary<string, string>();


            loadAliases();

            string voicesDir = "sounds/voices/" + Common.voice + "/";


            if (Directory.Exists(voicesDir + "aliases/"))
            {
                foreach (string file in Directory.GetFiles(voicesDir + "aliases/", "*.txt", SearchOption.AllDirectories))
                {
                    List<string> aliasLines = new List<string>();

                    string name = Path.GetFileNameWithoutExtension(file);



                    StreamReader sr = new StreamReader(file);

                    while (!sr.EndOfStream)
                    {
                        aliasLines.Add(sr.ReadLine());
                    }


                    sr.Close();

                    Alias alias = new Alias(name, aliasLines);
                    aliases.Add(alias);


                    fillNameList();
                }
            }
        }

        private void fillNameList()
        {
            aliasNames = new Dictionary<string, Alias>();
            aliasNamesList = new List<string>();

            foreach (Alias alias in aliases)
            {
                if (!aliasNames.ContainsKey(alias.aliasName))
                {
                    aliasNames.Add(alias.aliasName, alias);
                    aliasNamesList.Add(alias.aliasName);
                }

            }

        }
    }

    public class Alias
    {
        public List<string> aliasLines = new List<string>();
        public string aliasName;

        public Alias() { }
        public Alias(string name, List<string> lines)
        {
            this.aliasLines = lines;
            this.aliasName= name;
        }
    }

}
