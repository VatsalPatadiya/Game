using UnityEngine;

namespace GameClient.Data
{
    [CreateAssetMenu(fileName = "TileSetAsset", menuName = "GameClient/Tile Set")]
    public sealed class TileSetAsset : ScriptableObject
    {
        public string TileSetId;
        public Sprite[] Icons;
        public Color[] AccentColors;
        public GameObject[] FoodModels;

        // Similarity cluster id per FoodModel index (parallel to FoodModels). Models
        // that look alike share a cluster id; -1 = visually unique. Drives the
        // difficulty confusability lever (see PaletteSelector). Authored in Inspector.
        public int[] SimilarityClusterId;
    }
}
