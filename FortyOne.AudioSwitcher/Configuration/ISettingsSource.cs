namespace FortyOne.AudioSwitcher.Configuration
{
    public interface ISettingsSource
    {
        void SetFilePath(string path);
        void Load();
        void Save();
        /// <summary>Force an immediate disk write (e.g. on process exit).</summary>
        void Flush();
        string Get(string key);
        void Set(string key, string value);
    }
}
