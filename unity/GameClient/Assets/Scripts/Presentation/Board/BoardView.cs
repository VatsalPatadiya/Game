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
        [SerializeField] private Camera _camera;
        [SerializeField] private float _cellSize = 0.95f;
        [SerializeField] private float _cameraMargin = 0.3f;

        private readonly Dictionary<string, TileView> _tileViews = new Dictionary<string, TileView>();
        private Dictionary<string, TileSlot> _slotsById;

        public void Build(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            _slotsById = slotsById;

            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();

            FitCameraToBoard(slotsById);

            foreach (var kv in board.Cells)
            {
                if (kv.Value.Cleared) continue;

                var slot = slotsById[kv.Key];
                var view = Instantiate(_tilePrefab, transform);
                view.transform.localPosition = new Vector3(
                    slot.X * _cellSize,
                    slot.Y * _cellSize,
                    -slot.Layer * 0.1f);
                view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, kv.Value.Value), TileVisual.AccentColorFor(_tileSet, kv.Value.Value));
                _tileViews[kv.Key] = view;
            }

            RefreshFreeStates(board);
        }

        // Orthographic size only controls vertical half-height; the visible width
        // depends on the device's actual aspect ratio. A fixed size tuned for one
        // aspect ratio crops the board on narrower phones, so this recomputes the
        // size (and re-centers) every time the board is built, from the board's
        // real bounds and Screen.width/Screen.height, taking whichever axis is the
        // tighter constraint (letterboxing the other) instead of guessing a value.
        private void FitCameraToBoard(Dictionary<string, TileSlot> slotsById)
        {
            if (_camera == null || slotsById.Count == 0) return;

            float minX = slotsById.Values.Min(s => s.X);
            float maxX = slotsById.Values.Max(s => s.X);
            float minY = slotsById.Values.Min(s => s.Y);
            float maxY = slotsById.Values.Max(s => s.Y);

            float boardWidth = (maxX - minX) * _cellSize + _cellSize;
            float boardHeight = (maxY - minY) * _cellSize + _cellSize;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5f;
            float sizeForWidth = (boardWidth / 2f + _cameraMargin) / aspect;
            float sizeForHeight = boardHeight / 2f + _cameraMargin;
            _camera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);

            float centerX = (minX + maxX) / 2f * _cellSize;
            float centerY = (minY + maxY) / 2f * _cellSize;
            var pos = _camera.transform.position;
            _camera.transform.position = new Vector3(centerX, centerY, pos.z);
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
    }
}
