namespace GameDomain.Progression
{
    // Player settings (sub-project #4D). Persisted as JSON. Audio wiring is
    // pending audio assets; these persist the preference in the meantime.
    [System.Serializable]
    public class SettingsData
    {
        public bool SoundOn = true;
        public bool MusicOn = true;
    }
}
