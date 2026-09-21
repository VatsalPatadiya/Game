namespace GameDomain.Progression
{
    // Player settings (sub-project #4D). Persisted as JSON. SoundOn mutes
    // GameController's AudioSource; VibrationOn gates its Handheld.Vibrate().
    [System.Serializable]
    public class SettingsData
    {
        public bool SoundOn = true;
        public bool VibrationOn = true;
    }
}
