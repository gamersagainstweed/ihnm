using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Markdown.Avalonia;
using static ihnm.Managers.SampledVoiceManager;

namespace ihnm.Managers
{
    public class FavoritesManager
    {

        public List<string> favorites;
        public Dictionary<string, string> favoriteHotkeys;
        public HotkeysManager hotkeysMgr;
        public FavoritesWindow favoritesWnd;
        public SongsManager songsMgr;

        public Dictionary<string, string> hotkeyDict;

        public MusicManager musicMgr;
        public SoundboardManager soundboard;
        public LoopManager loopMgr;

        protected virtual void CallSortByFavorite()
        {
            SortByFavorite?.Invoke(this, null);
        }

        public event EventHandler SortByFavorite;

        public void initFavorites()
        {
            this.favorites = new List<string>();

            if (File.Exists("favorites/global.txt"))
            {

                string line;

                StreamReader sr = new StreamReader("favorites/global.txt");
                while (!sr.EndOfStream) 
                {
                    line = sr.ReadLine();
                    this.favorites.Add(line);
                }
                sr.Close();
            }

        }

        public void getHotkeys()
        {
            this.favoriteHotkeys = new Dictionary<string, string>();
            this.hotkeyDict= new Dictionary<string, string>();
            foreach (Hotkey h in hotkeysMgr.hotkeys)
            {
                List<string> tempKeys = new List<string>();
                foreach(string key in h.keys)
                {
                    if (key.Contains("Control"))
                        tempKeys.Add("Ctrl");
                    else if (key.Contains("Shift"))
                        tempKeys.Add("Shift");
                    else if (key.Contains("Alt"))
                        tempKeys.Add("Alt");
                    else
                        tempKeys.Add(key.Substring(2));
                }
                    
                this.hotkeyDict[h.action] = String.Join("+", tempKeys);
            }
            foreach (string fav in favorites)
            {
                if (this.hotkeyDict.ContainsKey(fav))
                {
                    this.favoriteHotkeys.Add(fav, this.hotkeyDict[fav]);
                }
                else
                {
                    this.favoriteHotkeys.Add(fav, "");
                }
            }
        }

        public void setupFavoritesGrid()
        {
            this.favoritesWnd.favoritesGrid.Children.Clear();
            int i = 0;
            foreach (KeyValuePair<string, string> fav in this.favoriteHotkeys)
            {

                if (i >= 17)
                    break;

                string color = "white";
                MarkdownScrollViewer favName = new MarkdownScrollViewer();
                if (this.soundboard.sounds.Contains(fav.Key))
                    color = "lightgreen";
                else if (this.musicMgr.music.Contains(fav.Key))
                    color = "yellow";
                else if (this.loopMgr.loops.Contains(fav.Key))
                    color = "purple";
                else
                    color = "white";

                MarkdownScrollViewer favText = new MarkdownScrollViewer();
                favText.Markdown = "%{color:" + color + "}" + fav.Key + "%  ";

                TextBlock favHkText = new TextBlock();
                favHkText.Text = fav.Value;
                favHkText.Opacity = 0.8;
                favHkText.FontSize = 10;

                if (favHkText.Text.Length>11)
                {
                    favHkText.FontSize = 10 - (favHkText.Text.Length - 11)/3;
                }
                
                favText.HorizontalAlignment = HorizontalAlignment.Center;
                favHkText.HorizontalAlignment  = HorizontalAlignment.Center;

                Grid favEntry = new Grid();
                favEntry.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Parse("20") });
                favEntry.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                favEntry.Children.Add(favText);
                favEntry.Children.Add(favHkText);
                Grid.SetRow(favHkText, 1);

                this.favoritesWnd.favoritesGrid.Children.Add(favEntry);
                Grid.SetColumn(favEntry, i);
                i++;
            }

 
        }

        public void setupHotkeysGrid()
        {
            this.favoritesWnd.hotkeysGrid.Children.Clear();
            int i = 0;
            foreach (KeyValuePair<string, string> fav in this.hotkeyDict)
            {

                if (i >= 17)
                    break;

                string color = "white";
                MarkdownScrollViewer favName = new MarkdownScrollViewer();
                if (this.soundboard.sounds.Contains(fav.Key))
                    color = Common.soundColor.ToString();
                else if (this.musicMgr.music.Contains(fav.Key))
                    color = Common.musicColor.ToString();
                else if (this.loopMgr.loops.Contains(fav.Key))
                    color = Common.loopColor.ToString();
                else if (this.songsMgr.songs.Contains(fav.Key))
                    color = Common.songColor.ToString();
                else
                    color = "white";

                MarkdownScrollViewer favText = new MarkdownScrollViewer();
                favText.Markdown = "%{color:" + color + "}" + fav.Key + "%  ";

                TextBlock favHkText = new TextBlock();
                favHkText.Text = fav.Value;
                favHkText.Opacity = 0.8;
                favHkText.FontSize = 10;

                if (favHkText.Text.Length > 11)
                {
                    favHkText.FontSize = 10 - (favHkText.Text.Length - 11) / 3;
                }

                favText.HorizontalAlignment = HorizontalAlignment.Center;
                favHkText.HorizontalAlignment = HorizontalAlignment.Center;

                Grid favEntry = new Grid();
                favEntry.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Parse("20") });
                favEntry.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                favEntry.Children.Add(favText);
                favEntry.Children.Add(favHkText);
                Grid.SetRow(favHkText, 1);

                this.favoritesWnd.hotkeysGrid.Children.Add(favEntry);
                Grid.SetColumn(favEntry, i);
                i++;
            }
        }

        public void setContext(bool favs)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (favs == true)
                {
                    this.favoritesWnd.hotkeysGrid.IsVisible = false;
                    this.favoritesWnd.favoritesGrid.IsVisible = true;
                }
                else
                {
                    this.favoritesWnd.hotkeysGrid.IsVisible = true;
                    this.favoritesWnd.favoritesGrid.IsVisible = false;
                }
            });
        }


                public FavoritesManager(HotkeysManager hotkeysMgr, FavoritesWindow favoritesWnd, MusicManager musicMgr,
                    LoopManager loopMgr, SoundboardManager soundboard, SongsManager songsMgr)
                {
                    this.hotkeysMgr = hotkeysMgr;
                    this.favoritesWnd = favoritesWnd;

                    this.musicMgr = musicMgr;
                    this.loopMgr = loopMgr;
                    this.soundboard = soundboard;
                    this.songsMgr = songsMgr;

                    this.initFavorites();
                    this.getHotkeys();

                    this.setupFavoritesGrid();
                    this.setupHotkeysGrid();

                }

        public void updateHotkeysGrid()
        {
            this.getHotkeys();
            this.setupHotkeysGrid();
            this.setupFavoritesGrid();
        }

    }
}
