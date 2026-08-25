using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private float _cellSize = 1f;

        private readonly Dictionary<string, TileView> _tileViews = new Dictionary<string, TileView>();
        private Dictionary<string, TileSlot> _slotsById;

        public void Build(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            _slotsById = slotsById;

            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();

            foreach (var kv in board.Cells)
            {
                if (kv.Value.Cleared) continue;

                var slot = slotsById[kv.Key];
                var view = Instantiate(_tilePrefab, transform);
                view.transform.localPosition = new Vector3(
                    slot.X * _cellSize,
                    slot.Y * _cellSize,
                    -slot.Layer * 0.1f);
                view.Initialize(slot.Id, slot.Layer, ColorForValue(kv.Value.Value), kv.Value.Value);
                _tileViews[kv.Key] = view;
            }

            RefreshFreeStates(board);
        }

        public void RefreshFreeStates(BoardState board)
        {
            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));

            foreach (var kv in _tileViews)
            {
                bool isFree = FreedomRuleCalculator.IsFree(_slotsById[kv.Key], remaining);
                kv.Value.SetFree(isFree);
            }
        }

        public void RemoveTiles(IEnumerable<string> slotIds)
        {
            foreach (var id in slotIds)
            {
                if (!_tileViews.TryGetValue(id, out var view)) continue;
                view.PlayClearAndDestroy();
                _tileViews.Remove(id);
            }
        }

        public TileView GetTileView(string slotId) =>
            _tileViews.TryGetValue(slotId, out var view) ? view : null;

        private static Color ColorForValue(string value)
        {
            int hash = value.GetHashCode();
            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.65f, 0.9f);
        }
    }
}
