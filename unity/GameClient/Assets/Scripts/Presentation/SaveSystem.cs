using System.IO;
using GameDomain.Progression;
using UnityEngine;

namespace GameClient.Presentation
{
    // JSON persistence for player progress (sub-project #4). Writes progress.json
    // to Application.persistentDataPath. The path is injectable so EditMode tests
    // can round-trip against a temp file. Pure serialization is JsonUtility, which
    // round-trips GameProgress's public fields/List.
    public static class SaveSystem
    {
        private static string DefaultPath => Path.Combine(Application.persistentDataPath, "progress.json");

        public static void Save(GameProgress progress, string path = null)
        {
            File.WriteAllText(path ?? DefaultPath, JsonUtility.ToJson(progress));
        }

        public static GameProgress Load(string path = null)
        {
            string p = path ?? DefaultPath;
            if (!File.Exists(p)) return new GameProgress();
            try
            {
                var loaded = JsonUtility.FromJson<GameProgress>(File.ReadAllText(p));
                return loaded ?? new GameProgress();
            }
            catch
            {
                return new GameProgress();
            }
        }
    }
}
