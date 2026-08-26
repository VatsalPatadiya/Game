using UnityEngine;

namespace GameClient.Presentation.Board
{
    // Tracks one press-drag-release gesture at a time and decides at the
    // end whether it was a tap (fire OnTileTapped, same as before) or a
    // drag-to-peek (just let the tile spring back - see
    // TileView.BeginDrag/UpdateDragOffset/EndDrag). Deciding on release
    // instead of on press-down is what makes both gestures possible on the
    // same tile without conflicting.
    public sealed class TileInputController : MonoBehaviour
    {
        private const float DragThresholdPixels = 18f;

        [SerializeField] private Camera _targetCamera;
        [SerializeField] private GameController _gameController;

        private TileView _pressedTile;
        private Vector3 _pressScreenPos;
        private Vector3 _pressWorldPoint;
        private bool _isDragging;
        private int _activeFingerId = -1;

        private void Update()
        {
            if (_pressedTile == null)
            {
                TryBeginPress();
                return;
            }

            if (!TryGetActivePointer(out Vector3 screenPos, out bool released))
            {
                // Touch data vanished without a clean release (e.g. an
                // interrupted gesture) - cancel safely rather than guess.
                CancelGesture();
                return;
            }

            var worldPoint = _targetCamera.ScreenToWorldPoint(screenPos);
            var worldDelta = worldPoint - _pressWorldPoint;

            if (!_isDragging && (screenPos - _pressScreenPos).magnitude >= DragThresholdPixels)
            {
                _isDragging = true;
                _pressedTile.BeginDrag();
            }

            if (_isDragging)
                _pressedTile.UpdateDragOffset(worldDelta);

            if (released)
            {
                var tile = _pressedTile;
                bool wasDragging = _isDragging;
                ResetPressState();

                if (wasDragging)
                    tile.EndDrag();
                else
                    _gameController.OnTileTapped(tile.SlotId);
            }
        }

        private void TryBeginPress()
        {
            if (_gameController.IsInputLocked) return;
            if (!TryGetPressStart(out Vector3 screenPos)) return;

            var worldPoint = _targetCamera.ScreenToWorldPoint(screenPos);
            var hits = Physics2D.OverlapPointAll(new Vector2(worldPoint.x, worldPoint.y));

            TileView topmost = null;
            foreach (var hit in hits)
            {
                var view = hit.GetComponent<TileView>();
                if (view == null) continue;
                if (topmost == null || view.Layer > topmost.Layer)
                    topmost = view;
            }

            if (topmost == null) return;

            _pressedTile = topmost;
            _pressScreenPos = screenPos;
            _pressWorldPoint = worldPoint;
            _isDragging = false;
        }

        private bool TryGetPressStart(out Vector3 screenPos)
        {
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began)
                    {
                        _activeFingerId = touch.fingerId;
                        screenPos = touch.position;
                        return true;
                    }
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                _activeFingerId = -1;
                screenPos = Input.mousePosition;
                return true;
            }

            screenPos = default;
            return false;
        }

        private bool TryGetActivePointer(out Vector3 screenPos, out bool released)
        {
            if (_activeFingerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != _activeFingerId) continue;
                    screenPos = touch.position;
                    released = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                    return true;
                }
                screenPos = default;
                released = false;
                return false;
            }

            screenPos = Input.mousePosition;
            released = Input.GetMouseButtonUp(0);
            return true;
        }

        private void CancelGesture()
        {
            var tile = _pressedTile;
            bool wasDragging = _isDragging;
            ResetPressState();
            if (wasDragging) tile.EndDrag();
        }

        private void ResetPressState()
        {
            _pressedTile = null;
            _isDragging = false;
            _activeFingerId = -1;
        }
    }
}
