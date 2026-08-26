using System;
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
        // Measured from actual gameplay footage: board population (after any
        // level-transition effect) completes in ~450ms as fast, overlapping
        // waves rather than a slow single-file reveal, so tiles are staggered
        // per row-batch, not per tile.
        private const float TargetDealInSeconds = 0.45f;
        private const float MinBatchStaggerSeconds = 0.004f;
        private const float MaxBatchStaggerSeconds = 0.018f;

        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private TileSetAsset _tileSet;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _cellSize = 0.95f;
        [SerializeField] private float _cameraMargin = 0.3f;

        private readonly Dictionary<string, TileView> _tileViews = new Dictionary<string, TileView>();
        private Dictionary<string, TileSlot> _slotsById;

        public TileSetAsset TileSet => _tileSet;

        // animateDealIn should only be true for a fresh level load. Undo and
        // Shuffle rebuild the whole board too, but replaying the deal-in
        // flourish on every one of those would be repetitive rather than
        // polished, so they pass false and tiles simply appear at full
        // opacity as before.
        public void Build(
            BoardState board, Dictionary<string, TileSlot> slotsById, bool animateDealIn, Action onDealInComplete = null)
        {
            _slotsById = slotsById;

            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();

            FitCameraToBoard(slotsById);

            // Layer ascending, then top-to-bottom, then left-to-right within a
            // layer, so the deal-in reads as the board building up from the
            // bottom layer to the top, matching the shadow-depth-per-layer cue.
            var orderedCells = board.Cells
                .Where(kv => !kv.Value.Cleared)
                .OrderBy(kv => slotsById[kv.Key].Layer)
                .ThenBy(kv => -slotsById[kv.Key].Y)
                .ThenBy(kv => slotsById[kv.Key].X)
                .ToList();

            int tileCount = orderedCells.Count;

            // Tiles sharing a layer+row deal in together as one batch (a
            // "wave"), rather than every tile getting its own stagger slot -
            // that's what keeps a 100+-tile board's cascade to ~450ms instead
            // of stretching out linearly with tile count.
            var batchIndexByPosition = new int[orderedCells.Count];
            int batchCount = 0;
            int? lastLayer = null;
            int? lastY = null;
            for (int i = 0; i < orderedCells.Count; i++)
            {
                var slot = slotsById[orderedCells[i].Key];
                if (lastLayer != slot.Layer || lastY != slot.Y)
                {
                    batchCount++;
                    lastLayer = slot.Layer;
                    lastY = slot.Y;
                }
                batchIndexByPosition[i] = batchCount - 1;
            }

            float stagger = batchCount > 0
                ? Mathf.Clamp(TargetDealInSeconds / batchCount, MinBatchStaggerSeconds, MaxBatchStaggerSeconds)
                : 0f;
            int pendingDealIns = animateDealIn ? tileCount : 0;

            for (int i = 0; i < orderedCells.Count; i++)
            {
                var kv = orderedCells[i];
                var slot = slotsById[kv.Key];
                var view = Instantiate(_tilePrefab, transform);
                view.transform.localPosition = new Vector3(
                    slot.X * _cellSize,
                    slot.Y * _cellSize,
                    -slot.Layer * 0.1f);
                view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, kv.Value.Value), TileVisual.AccentColorFor(_tileSet, kv.Value.Value));
                _tileViews[kv.Key] = view;

                if (animateDealIn)
                {
                    float delay = batchIndexByPosition[i] * stagger;
                    view.PlayDealIn(delay, () =>
                    {
                        pendingDealIns--;
                        if (pendingDealIns == 0)
                            onDealInComplete?.Invoke();
                    });
                }
            }

            if (animateDealIn && tileCount == 0)
                onDealInComplete?.Invoke();

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

        // Used after a tile has already been faded out by the tap-to-tray
        // flight (see GameController) — no further animation needed here.
        public void RemoveTileInstant(string slotId)
        {
            if (!_tileViews.TryGetValue(slotId, out var view)) return;
            _tileViews.Remove(slotId);
            if (view != null) Destroy(view.gameObject);
        }

        public TileView GetTileView(string slotId) =>
            _tileViews.TryGetValue(slotId, out var view) ? view : null;
    }
}
