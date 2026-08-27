using System;
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameDomain.Generation;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    public sealed class BoardView3D : MonoBehaviour
    {
        private const float TargetDealInSeconds = 0.45f;
        private const float MinBatchStaggerSeconds = 0.004f;
        private const float MaxBatchStaggerSeconds = 0.018f;

        [SerializeField] private TileView3D _tilePrefab;
        [SerializeField] private TileSetAsset _tileSet;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _cellWidth = 0.64f;
        [SerializeField] private float _cellHeight = 0.95f;
        [SerializeField] private float _layerHeight = 0.22f; // real Z step per layer
        [SerializeField] private float _cameraMargin = 0.3f; // leaves felt margins top/bottom for the HUD, tiles still large (0.02 filled the whole screen and hid the HUD)
        [SerializeField] private float _cameraTiltDegrees = 6f; // near front-on to match the flat mock; depth now comes from the per-tile drop shadow, not camera parallax (was 30, which skewed the board into a parallelogram)
        [SerializeField] private float _tiltDistancePadding = 1.05f; // barely-tilted view needs almost no extra distance (was 1.35 for the 30-degree pitch)
        [SerializeField] private float _tileJitterAmount = 0f; // clean aligned grid (premium mahjong look); was 0.07 loose-pile scatter
        [SerializeField] private float _tileRotationJitterDegrees = 0f;

        private readonly Dictionary<string, TileView3D> _tileViews = new Dictionary<string, TileView3D>();
        private Dictionary<string, TileSlot> _slotsById;

        public TileSetAsset TileSet => _tileSet;

        public void Build(
            BoardState board, Dictionary<string, TileSlot> slotsById, bool animateDealIn, Action onDealInComplete = null)
        {
            _slotsById = slotsById;

            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();

            FitCameraToBoard(slotsById);

            var orderedCells = board.Cells
                .Where(kv => !kv.Value.Cleared)
                .OrderBy(kv => slotsById[kv.Key].Layer)
                .ThenBy(kv => -slotsById[kv.Key].Y)
                .ThenBy(kv => slotsById[kv.Key].X)
                .ToList();

            int tileCount = orderedCells.Count;

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
                var jitter = JitterFor(slot.Id);
                view.transform.localPosition = new Vector3(
                    slot.X * _cellWidth + jitter.x,
                    slot.Y * _cellHeight + jitter.y,
                    -slot.Layer * _layerHeight);
                view.transform.localRotation = Quaternion.Euler(0f, 0f, jitter.z);
                view.Initialize(slot.Id, slot.Layer, TileVisual.FoodModelFor(_tileSet, kv.Value.Value));
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

        // Perspective equivalent of the 2D BoardView's orthographic-size fit:
        // instead of solving for orthographicSize, back the camera away along
        // -Z until the board's bounding box fits within the vertical FOV,
        // then check the horizontal FOV (from the device aspect ratio) isn't
        // the tighter constraint - same "whichever axis is tighter" idea as
        // before, just perspective trigonometry instead of orthographic size.
        private void FitCameraToBoard(Dictionary<string, TileSlot> slotsById)
        {
            if (_camera == null || slotsById.Count == 0) return;

            float minX = slotsById.Values.Min(s => s.X);
            float maxX = slotsById.Values.Max(s => s.X);
            float minY = slotsById.Values.Min(s => s.Y);
            float maxY = slotsById.Values.Max(s => s.Y);

            float boardWidth = (maxX - minX) * _cellWidth + _cellWidth + _cameraMargin * 2f;
            float boardHeight = (maxY - minY) * _cellHeight + _cellHeight + _cameraMargin * 2f;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5f;
            float verticalFovRad = _camera.fieldOfView * Mathf.Deg2Rad;

            float distanceForHeight = (boardHeight / 2f) / Mathf.Tan(verticalFovRad / 2f);
            float horizontalFovRad = 2f * Mathf.Atan(Mathf.Tan(verticalFovRad / 2f) * aspect);
            float distanceForWidth = (boardWidth / 2f) / Mathf.Tan(horizontalFovRad / 2f);

            float distance = Mathf.Max(distanceForHeight, distanceForWidth) * _tiltDistancePadding;

            float centerX = (minX + maxX) / 2f * _cellWidth;
            float centerY = (minY + maxY) / 2f * _cellHeight;
            var boardCenter = new Vector3(centerX, centerY, 0f);

            var rotation = Quaternion.Euler(_cameraTiltDegrees, 0f, 0f);
            _camera.transform.rotation = rotation;
            _camera.transform.position = boardCenter - (rotation * Vector3.forward) * distance;
        }

        // Deterministic per-tile scatter (position x/y, rotation z) seeded by
        // slot ID so it's stable across rebuilds of the same board - purely a
        // rendering offset, doesn't touch the domain-layer slot.X/Y that
        // drive matching/freedom-rule logic.
        private Vector3 JitterFor(string slotId)
        {
            int hash = slotId.GetHashCode();
            float jx = ((hash & 0xFFFF) / 65535f - 0.5f) * 2f * _tileJitterAmount;
            float jy = (((hash >> 16) & 0xFFFF) / 65535f - 0.5f) * 2f * _tileJitterAmount;
            int hash2 = unchecked(hash * unchecked((int)0x9E3779B1)) ^ (hash >> 13);
            float jr = ((hash2 & 0xFFFF) / 65535f - 0.5f) * 2f * _tileRotationJitterDegrees;
            return new Vector3(jx, jy, jr);
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

        public void RemoveTileInstant(string slotId)
        {
            if (!_tileViews.TryGetValue(slotId, out var view)) return;
            _tileViews.Remove(slotId);
            if (view != null) Destroy(view.gameObject);
        }

        public TileView3D GetTileView(string slotId) =>
            _tileViews.TryGetValue(slotId, out var view) ? view : null;
    }
}
