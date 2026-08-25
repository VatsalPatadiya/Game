using UnityEngine;

namespace GameClient.Data
{
    [CreateAssetMenu(fileName = "AccessibilityTokens", menuName = "GameClient/Accessibility Tokens")]
    public sealed class AccessibilityTokens : ScriptableObject
    {
        [Min(0)] public float MinTapTargetSize = 88f;
        [Min(0)] public float MinBodyTextSize = 20f;
        public Color FreeTileColor = Color.white;
        public Color BlockedTileColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        public Color HighlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        public Color HudTextColor = Color.black;
        public Color HudBackgroundColor = Color.white;
    }
}
