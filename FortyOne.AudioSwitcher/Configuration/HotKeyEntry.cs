using System;

namespace FortyOne.AudioSwitcher.Configuration
{
    /// <summary>
    /// Structured hotkey persisted as JSON (preferred) with legacy string fallback.
    /// </summary>
    public class HotKeyEntry
    {
        public int Key { get; set; }
        public int Modifiers { get; set; }
        public string DeviceId { get; set; }

        public Guid GetDeviceId()
        {
            Guid id;
            return Guid.TryParse(DeviceId, out id) ? id : Guid.Empty;
        }
    }
}
