using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using FortyOne.AudioSwitcher.Configuration;
using FortyOne.AudioSwitcher.Helpers;

namespace FortyOne.AudioSwitcher.HotKeyData
{
    public static class HotKeyManager
    {
        private static readonly List<HotKey> _hotkeys = new List<HotKey>();
        public static BindingList<HotKey> HotKeys = new BindingList<HotKey>();

        static HotKeyManager()
        {
            LoadHotKeys();
            RefreshHotkeys();
        }

        public static event EventHandler HotKeyPressed;

        public static void ClearAll()
        {
            foreach (var hk in _hotkeys)
            {
                hk.UnregisterHotkey();
                hk.HotKeyPressed -= hk_HotKeyPressed;
            }

            _hotkeys.Clear();
            Program.Settings.SetHotKeyEntries(new HotKeyEntry[0]);
            RefreshHotkeys();
        }

        public static void LoadHotKeys()
        {
            foreach (var hk in _hotkeys)
            {
                try
                {
                    hk.UnregisterHotkey();
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Unregister during load: " + ex.Message);
                }

                hk.HotKeyPressed -= hk_HotKeyPressed;
            }

            _hotkeys.Clear();

            try
            {
                var entries = Program.Settings.GetHotKeyEntries();
                foreach (var entry in entries)
                {
                    try
                    {
                        var deviceId = entry.GetDeviceId();
                        if (deviceId == Guid.Empty)
                            continue;

                        var hk = new HotKey
                        {
                            DeviceId = deviceId,
                            Modifiers = (Modifiers)entry.Modifiers,
                            Key = (Keys)entry.Key
                        };

                        if (DuplicateHotKey(hk))
                            continue;

                        _hotkeys.Add(hk);
                        hk.HotKeyPressed += hk_HotKeyPressed;
                        hk.RegisterHotkey();
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warn("Skipped malformed hotkey entry: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("LoadHotKeys failed", ex);
            }

            // Migrate legacy bracket format to structured JSON when needed
            try
            {
                var raw = Program.Settings.HotKeys;
                var needsMigration = !string.IsNullOrEmpty(raw)
                                     && raw != "[]"
                                     && !raw.Contains("\"Key\"");
                if (needsMigration)
                    SaveHotKeys();
                else
                    RefreshHotkeys();
            }
            catch (Exception ex)
            {
                AppLog.Error("Hotkey post-load failed", ex);
                RefreshHotkeys();
            }
        }

        private static void hk_HotKeyPressed(object sender, EventArgs e)
        {
            if (HotKeyPressed != null)
                HotKeyPressed(sender, e);
        }

        public static void SaveHotKeys()
        {
            var entries = _hotkeys.Select(hk => new HotKeyEntry
            {
                Key = (int)hk.Key,
                Modifiers = (int)hk.Modifiers,
                DeviceId = hk.DeviceId.ToString()
            }).ToList();

            Program.Settings.SetHotKeyEntries(entries);
            RefreshHotkeys();
        }

        public static bool AddHotKey(HotKey hk)
        {
            var existing = FindDuplicate(hk);
            if (existing != null)
            {
                if (!IsGhostHotKey(existing))
                    return false;

                RemoveHotKeyInternal(existing);
            }

            hk.HotKeyPressed += hk_HotKeyPressed;
            if (!hk.RegisterHotkey())
            {
                hk.HotKeyPressed -= hk_HotKeyPressed;
                return false;
            }

            _hotkeys.Add(hk);
            SaveHotKeys();
            return true;
        }

        public static bool UpdateHotKey(HotKey existing, HotKey updated)
        {
            if (existing == null || updated == null)
                return false;

            var duplicate = FindDuplicate(updated);
            if (duplicate != null && !ReferenceEquals(duplicate, existing))
            {
                if (!IsGhostHotKey(duplicate))
                    return false;

                RemoveHotKeyInternal(duplicate);
            }

            var previousDeviceId = existing.DeviceId;
            var previousKey = existing.Key;
            var previousModifiers = existing.Modifiers;
            var wasRegistered = existing.IsRegistered;

            existing.UnregisterHotkey();
            existing.DeviceId = updated.DeviceId;
            existing.Key = updated.Key;
            existing.Modifiers = updated.Modifiers;

            if (!existing.RegisterHotkey())
            {
                existing.DeviceId = previousDeviceId;
                existing.Key = previousKey;
                existing.Modifiers = previousModifiers;
                if (wasRegistered)
                    existing.RegisterHotkey();
                return false;
            }

            SaveHotKeys();
            return true;
        }

        public static void RefreshHotkeys()
        {
            foreach (var hk in _hotkeys)
                hk.InvalidateDeviceCache();

            HotKeys.Clear();
            // Always show all hotkeys (including unknown devices) for manageability.
            // Setting still exists for compatibility but defaults to showing unknowns.
            var filterInvalid = !Program.Settings.ShowUnknownDevicesInHotkeyList;
            IEnumerable<HotKey> hotkeyList = _hotkeys;
            if (filterInvalid)
                hotkeyList = hotkeyList.Where(x => x.Device != null);

            foreach (var k in hotkeyList)
                HotKeys.Add(k);
        }

        public static bool DuplicateHotKey(HotKey hk)
        {
            return FindDuplicate(hk) != null;
        }

        public static bool DuplicateHotKey(HotKey hk, HotKey exclude)
        {
            var duplicate = FindDuplicate(hk);
            return duplicate != null && !ReferenceEquals(duplicate, exclude);
        }

        public static HotKey FindDuplicate(HotKey hk)
        {
            if (hk == null)
                return null;

            return _hotkeys.FirstOrDefault(k => !ReferenceEquals(k, hk) && hk.Key == k.Key && hk.Modifiers == k.Modifiers);
        }

        public static void DeleteHotKey(HotKey hk)
        {
            if (hk == null)
                return;

            var toRemove = _hotkeys.FirstOrDefault(k => ReferenceEquals(k, hk))
                           ?? FindDuplicate(hk)
                           ?? _hotkeys.FirstOrDefault(k => k.DeviceId == hk.DeviceId && k.Key == hk.Key && k.Modifiers == hk.Modifiers);

            if (toRemove == null)
                return;

            RemoveHotKeyInternal(toRemove);
            SaveHotKeys();
        }

        public static void UnregisterAllHotkeys()
        {
            foreach (var hk in _hotkeys)
            {
                try
                {
                    hk.UnregisterHotkey();
                }
                catch (Exception ex)
                {
                    AppLog.Warn("UnregisterAll: " + ex.Message);
                }
            }
        }

        public static void ReregisterHotkeys()
        {
            if (Program.Settings.DisableHotKeys)
            {
                UnregisterAllHotkeys();
                return;
            }

            foreach (var hk in _hotkeys)
            {
                try
                {
                    hk.InvalidateDeviceCache();
                    hk.UnregisterHotkey();
                    hk.RegisterHotkey();
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Reregister failed for " + hk.HotKeyString + ": " + ex.Message);
                }
            }

            RefreshHotkeys();
        }

        private static bool IsGhostHotKey(HotKey hk)
        {
            return hk != null && hk.Device == null;
        }

        private static void RemoveHotKeyInternal(HotKey hk)
        {
            try
            {
                hk.UnregisterHotkey();
            }
            catch (Exception ex)
            {
                AppLog.Warn("RemoveHotKey unregister: " + ex.Message);
            }

            hk.HotKeyPressed -= hk_HotKeyPressed;
            _hotkeys.Remove(hk);
        }
    }
}
