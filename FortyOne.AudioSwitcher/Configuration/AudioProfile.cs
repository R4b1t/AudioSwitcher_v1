using System;

namespace FortyOne.AudioSwitcher.Configuration
{
    /// <summary>
    /// Named snapshot of preferred playback/recording devices.
    /// </summary>
    public class AudioProfile
    {
        public string Name { get; set; }
        public string PlaybackDeviceId { get; set; }
        public string RecordingDeviceId { get; set; }
        public bool DualSwitch { get; set; }

        public Guid GetPlaybackDeviceId()
        {
            Guid id;
            return Guid.TryParse(PlaybackDeviceId, out id) ? id : Guid.Empty;
        }

        public Guid GetRecordingDeviceId()
        {
            Guid id;
            return Guid.TryParse(RecordingDeviceId, out id) ? id : Guid.Empty;
        }
    }
}
