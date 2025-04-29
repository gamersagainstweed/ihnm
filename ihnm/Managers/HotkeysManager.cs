using SharpHook;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ihnm.Managers
{
    public class HotkeysManager
    {
        private string hotkeysDir = "hotkeys/";
        public int hotkeysPage = 1;

        public List<Hotkey> hotkeys = new List<Hotkey>();

        private Dictionary<string,bool> allKeys = new Dictionary<string,bool>();

        public HotkeysManager(TaskPoolGlobalHook globalHook)
        {
            this.loadHotkeys();

            this.initKeysDict();

            globalHook.KeyPressed += GlobalHook_KeyPressed;
            globalHook.KeyReleased += GlobalHook_KeyReleased;
        }

        public bool SetPage(int page)
        {
            if (Directory.Exists(hotkeysDir + (page).ToString() + "/"))
            {
                this.hotkeysPage=page;

                this.loadHotkeys();
                return true;
            }
            return false;
        }

        public void NextPage()
        {
            if (Directory.Exists(hotkeysDir + (this.hotkeysPage + 1).ToString() + "/"))
            {
                this.hotkeysPage++;

                this.loadHotkeys();
            }
            else
            {
                this.hotkeysPage = 1;

                this.loadHotkeys();
            }
        }

        public void PrevPage()
        {
            if (Directory.Exists(hotkeysDir + (this.hotkeysPage - 1).ToString() + "/"))
            {
                this.hotkeysPage--;

                this.loadHotkeys();
            }
            else
            {
                string[] subdirs = Directory.GetDirectories(hotkeysDir);
                List<int> pages = new List<int>();

                foreach (string dir in subdirs)
                {
                    string subdirname = Path.GetFileNameWithoutExtension(dir);
                    pages.Add(int.Parse(subdirname));
                }

                this.hotkeysPage = pages.Count;

                this.loadHotkeys();
            }
        }

        private void initKeysDict()
        {
            foreach (KeyCode kC in Enum.GetValues(typeof(KeyCode)))
            {
                allKeys.Add(kC.ToString(), false);
            }
        }

        public void addSave(string action, string hkeyStr)
        {
            StreamWriter sw = new StreamWriter(hotkeysDir + (this.hotkeysPage).ToString() + "/"+action+".txt");
            sw.WriteLine(hkeyStr);
            sw.Close();
            this.loadHotkeys();
        }

        public void remove(string action)
        {
            File.Delete(hotkeysDir + (this.hotkeysPage).ToString() + "/" + action + ".txt");
            this.loadHotkeys();
        }

        private void loadHotkeys()
        {
            this.hotkeys = new List<Hotkey>();

            if (!Directory.Exists(hotkeysDir + this.hotkeysPage.ToString()))
                Directory.CreateDirectory(hotkeysDir + this.hotkeysPage.ToString());

            foreach (string file in Directory.GetFiles(hotkeysDir+this.hotkeysPage.ToString()+"/", "*.txt", SearchOption.AllDirectories))
            {
                string action = Path.GetFileNameWithoutExtension(file);

                Hotkey hotkey = new Hotkey();
                hotkey.action = action;

                StreamReader sr = new StreamReader(file);
                string line = sr.ReadLine();
                sr.Close();

                hotkey.Parse(line);

                this.hotkeys.Add(hotkey);
            }
        }


        public class HotkeyTypedEventArgs : EventArgs
        {
            public string action {  get; set; }
        }


        protected virtual void OnHotkeyTyped(HotkeyTypedEventArgs e)
        {
            HotkeyTyped?.Invoke(this, e);
        }

        public delegate void HotkeyEventHandler(object myObject, HotkeyTypedEventArgs myArgs);

        public event HotkeyEventHandler HotkeyTyped;



        private void GlobalHook_KeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            KeyCode kCode = e.Data.KeyCode;
            allKeys[kCode.ToString()]= true;

            foreach(Hotkey hKey in hotkeys)
            {
                if (hKey.keys[^1]==kCode.ToString())
                {

                    bool fl = true;

                    foreach (string k in hKey.keys)
                    {
                        if (!allKeys[k])
                        {
                            fl = false;
                        }
                    }

                    if (fl)
                    {

                        HotkeyTypedEventArgs args = new HotkeyTypedEventArgs();
                        args.action = hKey.action;
                        this.OnHotkeyTyped(args);


                    }

                }
            }
            
        }

        private void GlobalHook_KeyReleased(object? sender, KeyboardHookEventArgs e)
        {
            allKeys[e.Data.KeyCode.ToString()] = false;
        }

    }


    public class Hotkey
    {
        public List<string> keys { get; set; }
        public string action { get; set; }

        public void Parse(string hotkeyString)
        {
            List<string> keys = hotkeyString.Split(new char[] { '+' }).ToList();
            List<string> newKeys= new List<string>();

            foreach (string key in keys)
            {
                newKeys.Add("Vc"+key);
            }



            this.keys = newKeys;


        }
    }

}
