﻿using livelywpf.Core;
using livelywpf.Views;
using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace livelywpf
{
    public class SettingsViewModel : ObservableObject
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public SettingsViewModel()
        {
            Settings = SettingsJSON.LoadConfig(Path.Combine(Program.AppDataDir, "Settings.json"));
            if (Settings == null)
            {
                Settings = new SettingsModel();
                SettingsJSON.SaveConfig(Path.Combine(Program.AppDataDir, "Settings.json"), Settings);
            }

            //lang-codes: https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c
            LanguageItems = new ObservableCollection<LanguagesModel>()
            {
                    new LanguagesModel("English(en-US)", new string[]{"en", "en-US"}), //technically not US english, sue me..
                    new LanguagesModel("中文(zh-CN)", new string[]{"zh", "zh-Hans","zh-CN","zh-SG"}), //are they same?
                    new LanguagesModel("日本人(ja-JP)", new string[]{"ja", "ja-JP"}),
                    new LanguagesModel("Pусский(ru)", new string[]{"ru", "ru-BY", "ru-KZ", "ru-KG", "ru-MD", "ru-RU","ru-UA"}), //are they same?
                    new LanguagesModel("हिन्दी(hi-IN)", new string[]{"hi", "hi-IN"}),
                    new LanguagesModel("Español(es)", new string[]{"es"}),
                    new LanguagesModel("Italian(it)", new string[]{"it", "it-IT", "it-SM","it-CH","it-VA"}),
                    new LanguagesModel("عربى(ar-AE)", new string[]{"ar"}),
                    new LanguagesModel("Française(fr)", new string[]{"fr"}),
                    new LanguagesModel("Deutsche(de)", new string[]{"de"}),
                    new LanguagesModel("portuguesa(pt)", new string[]{"pt"}),
            };
            SelectedLanguageItem = SearchSupportedLanguage(Settings.Language);

            var startupStatus = WindowsStartup.CheckStartupRegistry();
            if (startupStatus)
            {
                IsStartup = true;
            }
            else
            {
                IsStartup = false;
                //delete the wrong key if any.
                WindowsStartup.SetStartupRegistry(false);
            }

            SelectedTileSizeIndex = Settings.TileSize;
            SelectedAppFullScreenIndex = (int)Settings.AppFullscreenPause;
            SelectedAppFocusIndex = (int)Settings.AppFocusPause;
            SelectedBatteryPowerIndex = (int)Settings.BatteryPause;
            SelectedDisplayPauseRuleIndex = (int)Settings.DisplayPauseSettings;
            SelectedPauseAlgorithmIndex = (int)Settings.ProcessMonitorAlgorithm;
            SelectedVideoPlayerIndex = (int)Settings.VideoPlayer;
            SelectedLivelyUIModeIndex = (int)Settings.LivelyGUIRendering;
            SetupDesktop.WallpaperInputForward(Settings.InputForward);
        }

        private SettingsModel _settings;
        public SettingsModel Settings
        {
            get
            {
                return _settings;
            }
            set
            {
                _settings = value;
                OnPropertyChanged("Settings");
            }
        }

        public void UpdateConfigFile()
        {
            //testing
            SettingsJSON.SaveConfig(Path.Combine(Program.AppDataDir, "Settings.json"), Settings);
        }

        /// <summary>
        /// Checks LanguageItems and see if language with the given code exists.
        /// </summary>
        /// <param name="langCode">language code</param>
        /// <returns>Languagemodel if found; null otherwise.</returns>
        private LanguagesModel SearchSupportedLanguage(string langCode)
        {
            foreach (var lang in LanguageItems)
            {
                foreach (var code in lang.Codes)
                {
                    if (string.Equals(code, langCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return lang;
                    }
                }
            }
            return null;
        }

        #region general

        private bool _isStartup;
        public bool IsStartup
        {
            get
            {
                return _isStartup;
            }
            set
            {
                _isStartup = value;
                OnPropertyChanged("IsStartup");

                WindowsStartup.SetStartupRegistry(value);
            }
        }

        private ObservableCollection<LanguagesModel> _languageItems;
        public ObservableCollection<LanguagesModel> LanguageItems
        {
            get
            {
                return _languageItems;
            }
            set
            {

                _languageItems = value;
                OnPropertyChanged("LanguageItems");
            }
        }

        private LanguagesModel _selectedLanguageItem;
        public LanguagesModel SelectedLanguageItem
        {
            get
            {
                return _selectedLanguageItem;
            }
            set
            {
                if(LanguageItems.Contains(value))
                {
                    _selectedLanguageItem = value;
                }
                else
                {
                    //en-US
                    _selectedLanguageItem = LanguageItems[0];
                }
                Settings.Language = _selectedLanguageItem.Codes[0];

                OnPropertyChanged("SelectedLanguageItem");
                //UpdateConfigFile();
            }
        }

        private int _selectedTileSizeIndex;
        public int SelectedTileSizeIndex
        {
            get
            {
                return _selectedTileSizeIndex;
            }
            set
            {
                _selectedTileSizeIndex = value;
                OnPropertyChanged("SelectedTileSizeIndex");

                //todo: argumentoutofrange exception
                Settings.TileSize = value;
            }
        }

        public event EventHandler<LivelyGUIState> LivelyGUIStateChanged;
        private int _selectedLivelyUIModeIndex;
        public int SelectedLivelyUIModeIndex
        {
            get
            {
                return _selectedLivelyUIModeIndex;
            }
            set
            {
                _selectedLivelyUIModeIndex = value;
                OnPropertyChanged("SelectedLivelyUIModeIndex");

                //prevent running on startup etc.
                if (Settings.LivelyGUIRendering != (LivelyGUIState)value)
                {
                    Settings.LivelyGUIRendering = (LivelyGUIState)value;
                    UpdateConfigFile();

                    LivelyGUIStateChanged?.Invoke(null, (LivelyGUIState)value);
                }
            }
        }

        public event EventHandler<string> LivelyWallpaperDirChange;
        private RelayCommand _wallpaperDirectoryChangeCommand;
        public RelayCommand WallpaperDirectoryChangeCommand
        {
            get
            {
                if (_wallpaperDirectoryChangeCommand == null)
                {
                    _wallpaperDirectoryChangeCommand = new RelayCommand(
                        param => WallpaperDirectoryChange(), param => !WallpapeDirectoryChanging
                        );
                }
                return _wallpaperDirectoryChangeCommand;
            }
        }

        public bool _wallpapeDirectoryChanging;
        public bool WallpapeDirectoryChanging
        {
            get { return _wallpapeDirectoryChanging; }
            set
            {
                _wallpapeDirectoryChanging = value;
                OnPropertyChanged("WallpapeDirectoryChanging");
            }
        }

        private async void WallpaperDirectoryChange()
        {
            var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    WallpapeDirectoryChanging = true;
                    WallpaperDirectoryChangeCommand.RaiseCanExecuteChanged();
                    await Task.Run(() =>
                    {
                        FileOperations.DirectoryCopy(Path.Combine(Program.WallpaperDir, "wallpapers"),
                            Path.Combine(folderBrowserDialog.SelectedPath, "wallpapers"), true);
                        FileOperations.DirectoryCopy(Path.Combine(Program.WallpaperDir, "SaveData", "wptmp"),
                            Path.Combine(folderBrowserDialog.SelectedPath, "SaveData", "wptmp"), true);
                        FileOperations.DirectoryCopy(Path.Combine(Program.WallpaperDir, "SaveData", "wpdata"),
                            Path.Combine(folderBrowserDialog.SelectedPath, "SaveData", "wpdata"), true);
                    });
                }
                catch (Exception e)
                {
                    Logger.Error("Lively Folder Change Fail: " + e.Message);
                    System.Windows.MessageBox.Show("Failed to write to new directory:\n" + e.Message, "Error");
                    return;
                }
                finally
                {
                    WallpapeDirectoryChanging = false;
                    WallpaperDirectoryChangeCommand.RaiseCanExecuteChanged();
                }

                //close all running wp's.
                SetupDesktop.CloseAllWallpapers();

                var previousDirectory = Settings.WallpaperDir;
                Settings.WallpaperDir = folderBrowserDialog.SelectedPath;
                UpdateConfigFile();
                Program.WallpaperDir = Settings.WallpaperDir;
                LivelyWallpaperDirChange?.Invoke(null, folderBrowserDialog.SelectedPath);

                //not deleting the root folder, what if the user selects a folder that is not used by Lively alone!
                var result1 = await FileOperations.DeleteDirectoryAsync( Path.Combine(previousDirectory, "wallpapers"), 1000, 3000);
                var result2 = await FileOperations.DeleteDirectoryAsync(Path.Combine(previousDirectory, "SaveData"), 0, 1000);
                if (!(result1 && result2))
                {
                    System.Windows.MessageBox.Show("Failed to delete old wallpaper directory!\nTry deleting it manually.", "Error");
                }
            }
        }

        #endregion general

        #region performance

        private RelayCommand _applicationRulesCommand;
        public RelayCommand ApplicationRulesCommand
        {
            get
            {
                if (_applicationRulesCommand == null)
                {
                    _applicationRulesCommand = new RelayCommand(
                        param => ShowApplicationRulesWindow()
                        );
                }
                return _applicationRulesCommand;
            }
        }
        
        private void ShowApplicationRulesWindow()
        {
            ApplicationRulesView app = new ApplicationRulesView
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = App.AppWindow,
            };
            app.ShowDialog();
        }

        private int _selectedAppFullScreenIndex;
        public int SelectedAppFullScreenIndex
        {
            get
            {
                return _selectedAppFullScreenIndex;
            }
            set
            {
                _selectedAppFullScreenIndex = value;
                OnPropertyChanged("SelectedAppFullScreenIndex");

                //todo: argumentoutofrange exception
                Settings.AppFullscreenPause = (AppRulesEnum)value;
            }
        }

        private int _selectedAppFocusIndex;
        public int SelectedAppFocusIndex
        {
            get
            {
                return _selectedAppFocusIndex;
            }
            set
            {
                _selectedAppFocusIndex = value;
                OnPropertyChanged("SelectedAppFocusIndex");

                //todo: argumentoutofrange exception
                Settings.AppFocusPause = (AppRulesEnum)value;
            }
        }

        private int _selectedBatteryPowerIndex;
        public int SelectedBatteryPowerIndex
        {
            get
            {
                return _selectedBatteryPowerIndex;
            }
            set
            {
                _selectedBatteryPowerIndex = value;
                OnPropertyChanged("SelectedBatteryPowerIndex");

                //todo: argumentoutofrange exception
                Settings.BatteryPause = (AppRulesEnum)value;
            }
        }

        private int _selectedDisplayPauseRuleIndex;
        public int SelectedDisplayPauseRuleIndex
        {
            get
            {
                return _selectedDisplayPauseRuleIndex;
            }
            set
            {
                _selectedDisplayPauseRuleIndex = value;
                OnPropertyChanged("SelectedDisplayPauseRuleIndex");

                //todo: argumentoutofrange exception
                Settings.DisplayPauseSettings = (DisplayPauseEnum)value;
            }
        }

        private int _selectedPauseAlgorithmIndex;
        public int SelectedPauseAlgorithmIndex
        {
            get
            {
                return _selectedPauseAlgorithmIndex;
            }
            set
            {
                _selectedPauseAlgorithmIndex = value;
                OnPropertyChanged("SelectedPauseAlgorithmIndex");

                //todo: argumentoutofrange exception
                Settings.ProcessMonitorAlgorithm = (ProcessMonitorAlgorithm)value;
            }
        }

        #endregion performance

        #region wallpaper

        private int _selectedVideoPlayerIndex;
        public int SelectedVideoPlayerIndex
        {
            get
            {
                return _selectedVideoPlayerIndex;
            }
            set
            {
                if(_selectedVideoPlayerIndex != value)
                {
                    VideoPlayerSwitch((LivelyMediaPlayer)value);
                }
                _selectedVideoPlayerIndex = value;
                OnPropertyChanged("SelectedVideoPlayerIndex");

                //todo: argumentoutofrange exception
                Settings.VideoPlayer = (LivelyMediaPlayer)value;
                UpdateConfigFile();
            }
        }

        private void VideoPlayerSwitch(LivelyMediaPlayer player)
        {
            List<LibraryModel> wpCurr = new List<LibraryModel>();
            foreach (var item in SetupDesktop.Wallpapers)
            {
                if(item.GetWallpaperType() == WallpaperType.video)
                    wpCurr.Add(item.GetWallpaperData());
            }
            SetupDesktop.CloseWallpaper(WallpaperType.video);

            //todo: restart wpCurr
        }

        #endregion wallpaper
    }
}
