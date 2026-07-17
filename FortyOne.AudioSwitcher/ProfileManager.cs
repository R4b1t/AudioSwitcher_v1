using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AudioSwitcher.AudioApi;
using FortyOne.AudioSwitcher.Configuration;
using FortyOne.AudioSwitcher.Helpers;

namespace FortyOne.AudioSwitcher
{
    /// <summary>
    /// Manages named audio device profiles (playback + recording snapshots).
    /// </summary>
    public static class ProfileManager
    {
        private static readonly List<AudioProfile> _profiles = new List<AudioProfile>();
        private static bool _loaded;

        public static event EventHandler ProfilesChanged;

        public static IList<AudioProfile> Profiles
        {
            get
            {
                EnsureLoaded();
                return _profiles.AsReadOnly();
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _profiles.Clear();
            try
            {
                var list = Program.Settings.GetProfiles();
                if (list != null)
                {
                    foreach (var p in list)
                    {
                        if (p != null && !string.IsNullOrWhiteSpace(p.Name))
                            _profiles.Add(p);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to load profiles", ex);
            }

            _loaded = true;
        }

        public static void SaveProfile(string name, Guid playbackId, Guid recordingId, bool dualSwitch)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Profile name is required.", "name");

            EnsureLoaded();
            name = name.Trim();

            var existing = _profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.PlaybackDeviceId = playbackId == Guid.Empty ? null : playbackId.ToString();
                existing.RecordingDeviceId = recordingId == Guid.Empty ? null : recordingId.ToString();
                existing.DualSwitch = dualSwitch;
            }
            else
            {
                _profiles.Add(new AudioProfile
                {
                    Name = name,
                    PlaybackDeviceId = playbackId == Guid.Empty ? null : playbackId.ToString(),
                    RecordingDeviceId = recordingId == Guid.Empty ? null : recordingId.ToString(),
                    DualSwitch = dualSwitch
                });
            }

            Persist();
        }

        public static void DeleteProfile(string name)
        {
            EnsureLoaded();
            _profiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            Persist();
        }

        public static async Task ApplyProfileAsync(AudioProfile profile)
        {
            if (profile == null)
                return;

            var playbackId = profile.GetPlaybackDeviceId();
            var recordingId = profile.GetRecordingDeviceId();

            if (playbackId != Guid.Empty)
            {
                var dev = AudioDeviceManager.Controller.GetDevice(playbackId, DeviceState.All);
                if (dev != null)
                {
                    await dev.SetAsDefaultAsync();
                    if (profile.DualSwitch)
                        await dev.SetAsDefaultCommunicationsAsync();
                }
                else
                {
                    AppLog.Warn("Profile playback device not found: " + playbackId);
                }
            }

            if (recordingId != Guid.Empty)
            {
                var dev = AudioDeviceManager.Controller.GetDevice(recordingId, DeviceState.All);
                if (dev != null)
                {
                    await dev.SetAsDefaultAsync();
                    if (profile.DualSwitch)
                        await dev.SetAsDefaultCommunicationsAsync();
                }
                else
                {
                    AppLog.Warn("Profile recording device not found: " + recordingId);
                }
            }
        }

        private static void Persist()
        {
            Program.Settings.SetProfiles(_profiles);
            if (ProfilesChanged != null)
                ProfilesChanged(null, EventArgs.Empty);
        }
    }
}
