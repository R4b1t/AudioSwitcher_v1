using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using fastJSON;
using FortyOne.AudioSwitcher.Helpers;
using Microsoft.Win32;

namespace FortyOne.AudioSwitcher.Configuration
{
    public class ConfigurationSettings
    {
        public static readonly Regex GuidRegex = new Regex(
            @"([a-fA-F0-9]{8}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{12})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Kept for callers that reference the string constant
        public const string GUID_REGEX = @"([a-fA-F0-9]{8}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{12})";

        public const string SETTING_CLOSETOTRAY = "CloseToTray";
        public const string SETTING_AUTOSTARTWITHWINDOWS = "AutoStartWithWindows";
        public const string SETTING_STARTMINIMIZED = "StartMinimized";
        public const string SETTING_HOTKEYS = "HotKeys";
        public const string SETTING_FAVOURITEDEVICES = "FavouriteDevices";
        public const string SETTING_WINDOWWIDTH = "WindowWidth";
        public const string SETTING_WINDOWHEIGHT = "WindowHeight";
        public const string SETTING_DISABLEHOTKEYS = "DisableHotKeys";
        public const string SETTING_ENABLEQUICKSWITCH = "EnableQuickSwitch";
        public const string SETTING_CHECKFORUPDATESONSTARTUP = "CheckForUpdatesOnStartup";
        public const string SETTING_POLLFORUPDATES = "PollForUpdates";
        public const string SETTING_STARTUPRECORDINGDEVICE = "StartupRecordingDeviceID";
        public const string SETTING_STARTUPPLAYBACKDEVICE = "StartupPlaybackDeviceID";
        public const string SETTING_DUALSWITCHMODE = "DualSwitchMode";
        public const string SETTING_SHOWDISABLEDDEVICES = "ShowDisabledDevices";
        public const string SETTING_SHOWUNKNOWNDEVICESINHOTKEYLIST = "ShowUnknownDevicesInHotkeyList";
        public const string SETTING_SHOWDISCONNECTEDDDEVICES = "ShowDisconnectedDevices";
        public const string SETTING_SHOWDPDEVICEIICONINTRAY = "ShowDPDeviceIconInTray";
        public const string SETTING_UPDATE_NOTIFICATIONS_ENABLED = "UpdateNotificationsEnabled";
        public const string SETTING_SHOW_SWITCH_NOTIFICATIONS = "ShowSwitchNotifications";
        public const string SETTING_PROFILES = "Profiles";
        private readonly ISettingsSource _configWriter;

        public ConfigurationSettings(ISettingsSource source)
        {
            _configWriter = source;
            _configWriter.Load();
        }

        public void Flush()
        {
            _configWriter.Flush();
        }

        public Guid StartupRecordingDeviceID
        {
            get { return ParseGuid(_configWriter.Get(SETTING_STARTUPRECORDINGDEVICE)); }
            set { _configWriter.Set(SETTING_STARTUPRECORDINGDEVICE, value.ToString()); }
        }

        public Guid StartupPlaybackDeviceID
        {
            get { return ParseGuid(_configWriter.Get(SETTING_STARTUPPLAYBACKDEVICE)); }
            set { _configWriter.Set(SETTING_STARTUPPLAYBACKDEVICE, value.ToString()); }
        }

        public int PollForUpdates
        {
            get { return Convert.ToInt32(_configWriter.Get(SETTING_POLLFORUPDATES)); }
            set { _configWriter.Set(SETTING_POLLFORUPDATES, value.ToString()); }
        }

        public bool CheckForUpdatesOnStartup
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_CHECKFORUPDATESONSTARTUP)); }
            set { _configWriter.Set(SETTING_CHECKFORUPDATESONSTARTUP, value.ToString()); }
        }

        public bool DualSwitchMode
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_DUALSWITCHMODE)); }
            set { _configWriter.Set(SETTING_DUALSWITCHMODE, value.ToString()); }
        }

        public bool ShowDisabledDevices
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_SHOWDISABLEDDEVICES)); }
            set { _configWriter.Set(SETTING_SHOWDISABLEDDEVICES, value.ToString()); }
        }

        public bool ShowUnknownDevicesInHotkeyList
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_SHOWUNKNOWNDEVICESINHOTKEYLIST)); }
            set { _configWriter.Set(SETTING_SHOWUNKNOWNDEVICESINHOTKEYLIST, value.ToString()); }
        }

        public bool ShowDisconnectedDevices
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_SHOWDISCONNECTEDDDEVICES)); }
            set { _configWriter.Set(SETTING_SHOWDISCONNECTEDDDEVICES, value.ToString()); }
        }

        public bool ShowDPDeviceIconInTray
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_SHOWDPDEVICEIICONINTRAY)); }
            set { _configWriter.Set(SETTING_SHOWDPDEVICEIICONINTRAY, value.ToString()); }
        }

        public bool ShowSwitchNotifications
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_SHOW_SWITCH_NOTIFICATIONS)); }
            set { _configWriter.Set(SETTING_SHOW_SWITCH_NOTIFICATIONS, value.ToString()); }
        }

        public int WindowWidth
        {
            get { return Convert.ToInt32(_configWriter.Get(SETTING_WINDOWWIDTH)); }
            set { _configWriter.Set(SETTING_WINDOWWIDTH, value.ToString()); }
        }

        public int WindowHeight
        {
            get { return Convert.ToInt32(_configWriter.Get(SETTING_WINDOWHEIGHT)); }
            set { _configWriter.Set(SETTING_WINDOWHEIGHT, value.ToString()); }
        }

        public string FavouriteDevices
        {
            get { return _configWriter.Get(SETTING_FAVOURITEDEVICES); }
            set { _configWriter.Set(SETTING_FAVOURITEDEVICES, value); }
        }

        public string HotKeys
        {
            get { return _configWriter.Get(SETTING_HOTKEYS); }
            set { _configWriter.Set(SETTING_HOTKEYS, value); }
        }

        public string Profiles
        {
            get
            {
                try
                {
                    return _configWriter.Get(SETTING_PROFILES);
                }
                catch
                {
                    return "[]";
                }
            }
            set { _configWriter.Set(SETTING_PROFILES, value); }
        }

        public bool CloseToTray
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_CLOSETOTRAY)); }
            set { _configWriter.Set(SETTING_CLOSETOTRAY, value.ToString()); }
        }

        public bool StartMinimized
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_STARTMINIMIZED)); }
            set { _configWriter.Set(SETTING_STARTMINIMIZED, value.ToString()); }
        }

        public bool AutoStartWithWindows
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_AUTOSTARTWITHWINDOWS)); }
            set
            {
                try
                {
                    if (value)
                    {
                        var add =
                            Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                        add.SetValue("AudioSwitcher", "\"" + Assembly.GetEntryAssembly().Location + "\"");
                    }
                    else
                    {
                        var key =
                            Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                        if (key != null && key.GetValue("AudioSwitcher") != null)
                            key.DeleteValue("AudioSwitcher");
                    }

                    _configWriter.Set(SETTING_AUTOSTARTWITHWINDOWS, value.ToString());
                }
                catch (Exception ex)
                {
                    AppLog.Error("Failed to update AutoStartWithWindows registry key", ex);
                    _configWriter.Set(SETTING_AUTOSTARTWITHWINDOWS, false.ToString());
                }
            }
        }

        public bool DisableHotKeys
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_DISABLEHOTKEYS)); }
            set { _configWriter.Set(SETTING_DISABLEHOTKEYS, value.ToString()); }
        }

        public bool EnableQuickSwitch
        {
            get { return Convert.ToBoolean(_configWriter.Get(SETTING_ENABLEQUICKSWITCH)); }
            set { _configWriter.Set(SETTING_ENABLEQUICKSWITCH, value.ToString()); }
        }

        public bool UpdateNotificationsEnabled
        {
            get { return false; }
            set { _configWriter.Set(SETTING_UPDATE_NOTIFICATIONS_ENABLED, "False"); }
        }

        public List<HotKeyEntry> GetHotKeyEntries()
        {
            var raw = HotKeys;
            if (string.IsNullOrWhiteSpace(raw) || raw == "[]")
                return new List<HotKeyEntry>();

            // Preferred: structured JSON array
            if (raw.TrimStart().StartsWith("[{") || raw.TrimStart().StartsWith("[\n") || raw.Contains("\"Key\""))
            {
                try
                {
                    var list = JSON.ToObject<List<HotKeyEntry>>(raw);
                    return list ?? new List<HotKeyEntry>();
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Structured hotkey parse failed, trying legacy: " + ex.Message);
                }
            }

            // Legacy: [key,mod,guid][key,mod,guid]
            var result = new List<HotKeyEntry>();
            var entries = raw.Split(new[] { ",", "[", "]" }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 2 < entries.Length; i += 3)
            {
                int key;
                int modifiers;
                if (!int.TryParse(entries[i], out key) || !int.TryParse(entries[i + 1], out modifiers))
                    continue;

                var match = GuidRegex.Match(entries[i + 2]);
                if (!match.Success)
                    continue;

                result.Add(new HotKeyEntry
                {
                    Key = key,
                    Modifiers = modifiers,
                    DeviceId = match.Value
                });
            }

            return result;
        }

        public void SetHotKeyEntries(IEnumerable<HotKeyEntry> entries)
        {
            try
            {
                HotKeys = JSON.ToJSON(new List<HotKeyEntry>(entries));
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to serialize hotkeys", ex);
            }
        }

        public List<Guid> GetFavouriteDeviceIds()
        {
            var raw = FavouriteDevices;
            var result = new List<Guid>();
            if (string.IsNullOrWhiteSpace(raw) || raw == "[]")
                return result;

            // Structured JSON array of strings
            if (raw.TrimStart().StartsWith("[\""))
            {
                try
                {
                    var strings = JSON.ToObject<List<string>>(raw);
                    if (strings != null)
                    {
                        foreach (var s in strings)
                        {
                            Guid id;
                            if (Guid.TryParse(s, out id) && id != Guid.Empty)
                                result.Add(id);
                        }
                        return result;
                    }
                }
                catch
                {
                    // fall through to legacy
                }
            }

            foreach (Match match in GuidRegex.Matches(raw))
            {
                Guid id;
                if (Guid.TryParse(match.Value, out id) && id != Guid.Empty)
                    result.Add(id);
            }

            return result;
        }

        public void SetFavouriteDeviceIds(IEnumerable<Guid> ids)
        {
            var list = new List<string>();
            foreach (var id in ids)
                list.Add(id.ToString());
            FavouriteDevices = JSON.ToJSON(list);
        }

        public List<AudioProfile> GetProfiles()
        {
            try
            {
                var raw = Profiles;
                if (string.IsNullOrWhiteSpace(raw) || raw == "[]")
                    return new List<AudioProfile>();

                return JSON.ToObject<List<AudioProfile>>(raw) ?? new List<AudioProfile>();
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to parse profiles", ex);
                return new List<AudioProfile>();
            }
        }

        public void SetProfiles(IEnumerable<AudioProfile> profiles)
        {
            Profiles = JSON.ToJSON(new List<AudioProfile>(profiles));
        }

        public void CreateDefaults()
        {
            if (!SettingExists(SETTING_CLOSETOTRAY))
                CloseToTray = false;

            if (!SettingExists(SETTING_STARTMINIMIZED))
                StartMinimized = false;

            if (!SettingExists(SETTING_AUTOSTARTWITHWINDOWS))
                AutoStartWithWindows = false;

            if (!SettingExists(SETTING_DISABLEHOTKEYS))
                DisableHotKeys = false;

            if (!SettingExists(SETTING_ENABLEQUICKSWITCH))
                EnableQuickSwitch = false;

            if (!SettingExists(SETTING_HOTKEYS))
                HotKeys = "[]";

            if (!SettingExists(SETTING_FAVOURITEDEVICES))
                FavouriteDevices = "[]";

            if (!SettingExists(SETTING_WINDOWHEIGHT))
                WindowHeight = 400;

            if (!SettingExists(SETTING_WINDOWWIDTH))
                WindowWidth = 300;

            // Updates are permanently disabled in this fork
            CheckForUpdatesOnStartup = false;
            PollForUpdates = 0;
            UpdateNotificationsEnabled = false;

            if (!SettingExists(SETTING_STARTUPPLAYBACKDEVICE))
                StartupPlaybackDeviceID = Guid.Empty;

            if (!SettingExists(SETTING_STARTUPRECORDINGDEVICE))
                StartupRecordingDeviceID = Guid.Empty;

            if (!SettingExists(SETTING_DUALSWITCHMODE))
                DualSwitchMode = false;

            if (!SettingExists(SETTING_SHOWDISABLEDDEVICES))
                ShowDisabledDevices = false;

            // Default true so ghost hotkeys remain visible and manageable
            if (!SettingExists(SETTING_SHOWUNKNOWNDEVICESINHOTKEYLIST))
                ShowUnknownDevicesInHotkeyList = true;

            if (!SettingExists(SETTING_SHOWDISCONNECTEDDDEVICES))
                ShowDisconnectedDevices = false;

            if (!SettingExists(SETTING_SHOWDPDEVICEIICONINTRAY))
                ShowDPDeviceIconInTray = false;

            if (!SettingExists(SETTING_SHOW_SWITCH_NOTIFICATIONS))
                ShowSwitchNotifications = true;

            if (!SettingExists(SETTING_PROFILES))
                Profiles = "[]";
        }

        public void LoadFrom(ConfigurationSettings otherSettings)
        {
            AutoStartWithWindows = otherSettings.AutoStartWithWindows;
            CheckForUpdatesOnStartup = false;
            CloseToTray = otherSettings.CloseToTray;
            DisableHotKeys = otherSettings.DisableHotKeys;
            DualSwitchMode = otherSettings.DualSwitchMode;
            EnableQuickSwitch = otherSettings.EnableQuickSwitch;
            FavouriteDevices = otherSettings.FavouriteDevices;
            HotKeys = otherSettings.HotKeys;
            PollForUpdates = 0;
            ShowDisabledDevices = otherSettings.ShowDisabledDevices;
            ShowUnknownDevicesInHotkeyList = otherSettings.ShowUnknownDevicesInHotkeyList;
            ShowDisconnectedDevices = otherSettings.ShowDisconnectedDevices;
            StartMinimized = otherSettings.StartMinimized;
            StartupPlaybackDeviceID = otherSettings.StartupPlaybackDeviceID;
            StartupRecordingDeviceID = otherSettings.StartupRecordingDeviceID;
            WindowHeight = otherSettings.WindowHeight;
            WindowWidth = otherSettings.WindowWidth;

            try
            {
                Profiles = otherSettings.Profiles;
            }
            catch
            {
                Profiles = "[]";
            }

            try
            {
                ShowSwitchNotifications = otherSettings.ShowSwitchNotifications;
            }
            catch
            {
                ShowSwitchNotifications = true;
            }
        }

        public bool SettingExists(string name)
        {
            try
            {
                _configWriter.Get(name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Guid ParseGuid(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Guid.Empty;

            var match = GuidRegex.Match(value);
            if (match.Success)
                return new Guid(match.Value);

            Guid id;
            return Guid.TryParse(value, out id) ? id : Guid.Empty;
        }
    }
}
