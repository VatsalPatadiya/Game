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
        // Slot X/Y are in HALF-tile units (see TurtleShapeBuilder), so a cell here
        // is HALF a tile: same-layer tiles (step 2) land one full tile apart
        // (packed), and half-tile-offset layers (step 1) land half a tile over -
        // the real mahjong straddle. Tile is ~0.626w x 0.92h, so half = 0.313 x 0.46.
        [SerializeField] private float _cellWidth = 0.313f;
        [SerializeField] private float _cellHeight = 0.46f;
        [SerializeField] private float _layerHeight = 0.28f; // real Z step per layer (stacked layers read as depth via the straddle + shadow + tilt)
        [SerializeField] private float _layerStraddle = 0f;  // NO fake render offset: the half-tile straddle now lives in the slot coordinates, so rendered overlap == domain coverage (a tile that looks covered really is covered/non-free)
        // 0.3 -> 0.15: the board's WIDTH (not height) was the binding fit
        // constraint on this portrait screen (6-column layer 0 vs a narrow
        // horizontal FOV), so the old 0.3 margin (0.6 total) pushed the
        // camera back much farther than needed and left a large unused
        // vertical gap between the tray and the bottom buttons. Paired with
        // the wider FOV below.
        [SerializeField] private float _cameraMargin = 0.05f; // trimmed 0.15 -> 0.05 so the board fills the width (bigger tiles) inside the enlarged board band
        // Fraction of the SCREEN height the board is allowed to occupy (the board
        // band). 1 = full screen (unchanged / default for tests). The scene builder
        // sets this to the board band's height (~0.60) so a tall board's on-screen
        // height is capped to its band and can't bleed into the top cluster or the
        // bottom buttons.
        [SerializeField] private float _boardBandHeightFrac = 1f;
        // Tile-size floor: an upper clamp on orthographicSize so tiles can never
        // scale below a comfortable size (a bigger orthographicSize = smaller tiles).
        // 0 = disabled (default / tests). The builder sets it from the approved tile
        // size; a board too big to fit at that size overflows rather than shrinking.
        [SerializeField] private float _maxOrthographicSize = 0f;
        // FIXED orthographic zoom. When > 0 the board renders at this constant
        // orthographicSize regardless of board size, so (a) every level's tiles are
        // the SAME comfortable size and (b) the camera-parented HUD - whose on-screen
        // positions were baked against the camera's build-time orthographicSize - is
        // always calibrated (a per-board zoom spread the HUD off-screen on small
        // boards). The scene builder sets it to the same value it calibrates the HUD
        // at, and sizes the largest board (120 tiles) to fit at this zoom. 0 = the
        // legacy dynamic fit (kept for tests / back-compat).
        [SerializeField] private float _fixedOrthographicSize = 0f;
        // Viewport Y (1 = top of screen) to pin the board's TOP edge to, so the board
        // always sits just under the tray regardless of its size (a small board no
        // longer floats with a big gap above it, and a large board doesn't ride up
        // into the tray). The board grows DOWNWARD from this line. 0 = disabled
        // (fall back to _verticalBiasViewportFrac centring).
        [SerializeField] private float _boardTopAnchorViewport = 0f;
        [SerializeField] private float _cameraTiltDegrees = 5f; // near-front pitch: the tile's green top side-wall shrinks to a thin clean edge (matches reference's clean ivory tops); depth still reads via drop shadows + stacking straddle. Was 14 (exposed a prominent green "cap" on top-row tiles).
        [SerializeField] private float _tiltDistancePadding = 1.05f; // barely-tilted view needs almost no extra distance (was 1.35 for the 30-degree pitch)
        [SerializeField] private float _tileJitterAmount = 0f; // clean aligned grid (premium mahjong look); was 0.07 loose-pile scatter
        [SerializeField] private float _tileRotationJitterDegrees = 0f;
        // The HUD (score bar, tray, control buttons) is parented to this camera at
        // a fixed distance baked by GameSceneBuilder3D (its HudDistance constant,
        // wired in here via SetField so the two can't silently drift apart). The
        // fit below picks whatever distance the CURRENT board needs, which shrinks
        // for a smaller board/tile size - if that ever undercuts HudDistance, the
        // board's front tiles end up nearer the camera than the HUD and occlude it
        // entirely. Clamping the fit distance to at least this value guarantees the
        // HUD always clears the board, regardless of how the board's size changes.
        [SerializeField] private float _minDistanceForHud = 0f; // 0 = no floor; GameSceneBuilder3D sets this to HudDistance

        // FitCameraToBoard below aims the camera dead-centre on the board's
        // own bounding box (viewport Y=0.5) - correct only if the usable
        // band above and below the board is symmetric. It isn't: the
        // topbar+progress+tray cluster eats far more of the top of the
        // screen than the 3-button row eats at the bottom, so a
        // screen-centred board leaves excess empty felt between its bottom
        // edge and the buttons (measured on-device: ~12% of screen height,
        // vs ~2% at the top). GameSceneBuilder3D computes this from the
        // real HUD anchor positions (SetFieldFloat, alongside
        // _minDistanceForHud) and sets it here: positive shifts the board's
        // rendered position DOWN the screen (toward the buttons) by this
        // many viewport-height units at the board's own distance.
        [SerializeField] private float _verticalBiasViewportFrac = 0f;



        private readonly Dictionary<string, TileView3D> _tileViews = new Dictionary<string, TileView3D>();
        private Dictionary<string, TileSlot> _slotsById;

        public TileSetAsset TileSet => _tileSet;

        // Destroy every rendered tile and forget them. Used when leaving gameplay
        // (e.g. back to level-select) so the old board doesn't linger on screen and
        // bleed through the next screen. Safe to call when already empty.
        public void Clear()
        {
            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();
        }

        public void Build(
            BoardState board, Dictionary<string, TileSlot> slotsById, bool animateDealIn, Action onDealInComplete = null)
        {
            _slotsById = slotsById;

            Clear();

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
                PlaceTileView(view, slot);
                view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, kv.Value.Value));
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
            int maxLayer = slotsById.Values.Max(s => s.Layer);

            // Upper layers shift up-left by LayerRenderOffset, so the fitted box has
            // to grow on those two sides (and its centre shifts half that) or the
            // topmost tiles clip past the felt / behind the HUD.
            float offX = maxLayer * _layerStraddle * _cellWidth;  // extra extent to the left
            float offY = maxLayer * _layerStraddle * _cellHeight; // extra extent up

            // A tile footprint is 2 half-units, so pad by a whole tile (2 cells) on
            // top of the centre-to-centre span.
            float boardWidth = (maxX - minX) * _cellWidth + 2f * _cellWidth + offX + _cameraMargin * 2f;
            float boardHeight = (maxY - minY) * _cellHeight + 2f * _cellHeight + offY + _cameraMargin * 2f;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5f;

            float centerX = (minX + maxX) / 2f * _cellWidth - offX / 2f;
            float centerY = (minY + maxY) / 2f * _cellHeight + offY / 2f;
            var boardCenter = new Vector3(centerX, centerY, 0f);

            if (_camera.orthographic)
            {
                float orthoSize;
                if (_fixedOrthographicSize > 0f)
                {
                    // Constant zoom: same tile size every level, and the HUD (baked
                    // against this same value) stays put. The board is still centred
                    // in its band via _verticalBiasViewportFrac below.
                    orthoSize = _fixedOrthographicSize;
                }
                else
                {
                    // Legacy dynamic fit: divide the height requirement by the board
                    // band fraction so the board's world height fills at most that
                    // fraction of the screen; clamp to the tile-size floor.
                    float bandFrac = _boardBandHeightFrac > 0f ? _boardBandHeightFrac : 1f;
                    float sizeForHeight = (boardHeight / 2f) / bandFrac;
                    float sizeForWidth = (boardWidth / 2f) / aspect;
                    orthoSize = Mathf.Max(sizeForHeight, sizeForWidth) * _tiltDistancePadding;
                    if (_maxOrthographicSize > 0f)
                        orthoSize = Mathf.Min(orthoSize, _maxOrthographicSize);
                }
                _camera.orthographicSize = orthoSize;

                // Orthographic 2.5D look: pitch up slightly to see bottom edges. 
                // We leave yaw at 0f so the board grid remains perfectly horizontal 
                // and un-skewed (no parallelogram effect).
                var orthoRotation = Quaternion.Euler(_cameraTiltDegrees, 0f, 0f);
                _camera.transform.rotation = orthoRotation;

                // Vertical placement: either pin the board's TOP to a viewport line
                // (just under the tray, so every board size sits there and grows down)
                // or fall back to the centring bias. A positive bias shifts the board
                // DOWN, so bias = 0.5 - desiredCentreViewport.
                float bias = _verticalBiasViewportFrac;
                if (_boardTopAnchorViewport > 0f)
                {
                    float halfHeightViewport = boardHeight / (4f * orthoSize); // (boardHeight/2)/(2*orthoSize)
                    float boardCenterViewport = _boardTopAnchorViewport - halfHeightViewport;
                    bias = 0.5f - boardCenterViewport;
                }
                float orthoWorldYOffset = bias * (orthoSize * 2f);
                var orthoAimPoint = boardCenter + new Vector3(0f, orthoWorldYOffset, 0f);
                _camera.transform.position = orthoAimPoint - (orthoRotation * Vector3.forward) * 50f;
                return;
            }

            float verticalFovRad = _camera.fieldOfView * Mathf.Deg2Rad;

            float distanceForHeight = (boardHeight / 2f) / Mathf.Tan(verticalFovRad / 2f);
            float horizontalFovRad = 2f * Mathf.Atan(Mathf.Tan(verticalFovRad / 2f) * aspect);
            float distanceForWidth = (boardWidth / 2f) / Mathf.Tan(horizontalFovRad / 2f);

            float distance = Mathf.Max(distanceForHeight, distanceForWidth) * _tiltDistancePadding;
            distance = Mathf.Max(distance, _minDistanceForHud);

            var rotation = Quaternion.Euler(_cameraTiltDegrees, 0f, 0f);
            _camera.transform.rotation = rotation;

            // Panning the AIM point up (positive world Y) shifts the whole
            // rendered scene down on screen, so a positive
            // _verticalBiasViewportFrac (defined as "shift board down") maps
            // to a positive worldYOffset added here - the camera still faces
            // the same direction, it just isn't centred on boardCenter
            // itself anymore.
            float frustumHeightAtDistance = 2f * distance * Mathf.Tan(verticalFovRad * 0.5f);
            float worldYOffset = _verticalBiasViewportFrac * frustumHeightAtDistance;
            var aimPoint = boardCenter + new Vector3(0f, worldYOffset, 0f);
            _camera.transform.position = aimPoint - (rotation * Vector3.forward) * distance;
        }

        // Deterministic per-tile scatter (position x/y, rotation z) seeded by
        // slot ID so it's stable across rebuilds of the same board - purely a
        // rendering offset, doesn't touch the domain-layer slot.X/Y that
        // drive matching/freedom-rule logic.
        // Screen-space shift applied to a whole layer so upper layers sit up-left
        // of the ones they cover (the stacked-pyramid straddle look). Purely a
        // rendering offset - the domain slot.X/Y/Layer that drive matching and the
        // freedom rule are untouched.
        private Vector2 LayerRenderOffset(int layer) => new Vector2(
            -layer * _layerStraddle * _cellWidth,
             layer * _layerStraddle * _cellHeight);

        // Shared tile placement (position + rotation jitter + layer straddle) so
        // Build() and RestoreTiles() lay a tile down the exact same way.
        private void PlaceTileView(TileView3D view, TileSlot slot)
        {
            var jitter = JitterFor(slot.Id);
            var layerOffset = LayerRenderOffset(slot.Layer);
            view.transform.localPosition = new Vector3(
                slot.X * _cellWidth + jitter.x + layerOffset.x,
                slot.Y * _cellHeight + jitter.y + layerOffset.y,
                -slot.Layer * _layerHeight);
            view.transform.localRotation = Quaternion.Euler(0f, 0f, jitter.z);
        }

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
            // Tiles sitting in the tray are physically off the board, so they must
            // NOT count as covering the tiles beneath them - exclude them exactly
            // like TrayManager's own freedom check does, or uncovered tiles won't
            // brighten after a tile flies up.
            var remaining = new HashSet<string>(
                board.Cells
                    .Where(kv => !kv.Value.Cleared && !board.TrayTileIds.Contains(kv.Key))
                    .Select(kv => kv.Key));

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

        // Re-materialize tiles that Undo un-cleared, placing them back at their
        // original layer/position and fading them in.
        public void RestoreTiles(IEnumerable<string> slotIds, BoardState board)
        {
            foreach (var id in slotIds)
            {
                if (_tileViews.ContainsKey(id)) continue;
                if (!_slotsById.TryGetValue(id, out var slot)) continue;
                if (!board.Cells.TryGetValue(id, out var cell)) continue;

                var view = Instantiate(_tilePrefab, transform);
                PlaceTileView(view, slot);
                view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, cell.Value));
                view.PlayFadeInOnly();
                _tileViews[id] = view;
            }
            RefreshFreeStates(board);
        }

        // Swap the face/food-model of existing tile views to match the board's
        // (post-shuffle) values without destroying the GameObjects.
        public void RefreshTileValues(IEnumerable<string> slotIds, BoardState board)
        {
            foreach (var id in slotIds)
            {
                if (!_tileViews.TryGetValue(id, out var view)) continue;
                if (!_slotsById.TryGetValue(id, out var slot)) continue;
                if (!board.Cells.TryGetValue(id, out var cell)) continue;
                view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, cell.Value));
            }
            RefreshFreeStates(board);
        }
    }
}
