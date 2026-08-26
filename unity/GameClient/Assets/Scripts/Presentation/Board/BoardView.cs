using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameDomain.Generation;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private TileSetAsset _tileSet;
        [SerializeField] private float _cellSize = 0.9f;

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
                view.Initialize(slot.Id, slot.Layer, IconForValue(kv.Value.Value), AccentColorForValue(kv.Value.Value));
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

        // Icons.Length (7) * AccentColors.Length (4) = 28 unique combinations, which
        // covers the full 0-25 value range the domain layer can assign (values are
        // capped at mod 26 by ReverseConstructionSolver), so no two distinct pair
        // values ever render as the same icon+color combo.
        private Sprite IconForValue(string value)
        {
            int index = int.Parse(value);
            return _tileSet.Icons[index % _tileSet.Icons.Length];
        }

        private Color AccentColorForValue(string value)
        {
            int index = int.Parse(value);
            int colorIndex = (index / _tileSet.Icons.Length) % _tileSet.AccentColors.Length;
            return _tileSet.AccentColors[colorIndex];
        }
    }
}
