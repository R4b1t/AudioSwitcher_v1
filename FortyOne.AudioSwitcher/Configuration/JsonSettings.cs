using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using fastJSON;
using FortyOne.AudioSwitcher.Helpers;

namespace FortyOne.AudioSwitcher.Configuration
{
    public class JsonSettings : ISettingsSource
    {
        private readonly object _mutex = new object();
        private string _path;
        private IDictionary<string, string> _settingsObject;
        private Timer _debounceTimer;
        private bool _dirty;
        private const int DebounceMs = 400;

        public JsonSettings()
        {
            _settingsObject = new Dictionary<string, string>();
        }

        public void SetFilePath(string path)
        {
            _path = path;
        }

        public void Load()
        {
            lock (_mutex)
            {
                try
                {
                    if (File.Exists(_path))
                        _settingsObject = JSON.ToObject<Dictionary<string, string>>(File.ReadAllText(_path))
                                           ?? new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    AppLog.Error("Failed to load settings file", ex);
                    _settingsObject = new Dictionary<string, string>();
                }

                _dirty = false;
            }
        }

        public void Save()
        {
            // Debounced save — callers should use Flush() for immediate persistence
            lock (_mutex)
            {
                _dirty = true;
                if (_debounceTimer == null)
                {
                    _debounceTimer = new Timer(DebounceCallback, null, DebounceMs, Timeout.Infinite);
                }
                else
                {
                    _debounceTimer.Change(DebounceMs, Timeout.Infinite);
                }
            }
        }

        public void Flush()
        {
            lock (_mutex)
            {
                if (_debounceTimer != null)
                    _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);

                if (_dirty)
                    WriteToDiskUnlocked();
            }
        }

        public string Get(string key)
        {
            lock (_mutex)
            {
                return _settingsObject[key];
            }
        }

        public void Set(string key, string value)
        {
            lock (_mutex)
            {
                string existing;
                if (_settingsObject.TryGetValue(key, out existing) && existing == value)
                    return;

                _settingsObject[key] = value;
                _dirty = true;
            }

            Save();
        }

        private void DebounceCallback(object state)
        {
            lock (_mutex)
            {
                if (_dirty)
                    WriteToDiskUnlocked();
            }
        }

        private void WriteToDiskUnlocked()
        {
            try
            {
                if (string.IsNullOrEmpty(_path))
                    return;

                var json = JSON.Beautify(JSON.ToJSON(_settingsObject));
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Atomic write: temp file then replace
                var tempPath = _path + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(_path))
                {
                    var backup = _path + ".bak";
                    try
                    {
                        File.Copy(_path, backup, true);
                    }
                    catch
                    {
                        // backup is best-effort
                    }

                    File.Replace(tempPath, _path, null);
                }
                else
                {
                    File.Move(tempPath, _path);
                }

                _dirty = false;
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to save settings file", ex);
            }
        }
    }
}
