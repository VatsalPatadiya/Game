using UnityEngine;

namespace GameClient.Data
{
    [CreateAssetMenu(fileName = "LevelShapeAsset", menuName = "GameClient/Level Shape")]
    public sealed class LevelShapeAsset : ScriptableObject
    {
        public int LevelId;
        public int[] RowLengthsByLayer;
        public string TileSetId;
    }
}
