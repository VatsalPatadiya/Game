using GameClient.Presentation.Board;
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    public sealed class TileInputController3D : MonoBehaviour
    {
        private const float DragThresholdPixels = 18f;

        [SerializeField] private Camera _targetCamera;
        [SerializeField] private GameController _gameController;

        private TileView3D _pressedTile;
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
                CancelGesture();
                return;
            }

            var worldPoint = ScreenToWorldOnTilePlane(screenPos);
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

            var ray = _targetCamera.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray);
            if (hits.Length == 0) return;

            RaycastHit closest = hits[0];
            for (int i = 1; i < hits.Length; i++)
                if (hits[i].distance < closest.distance) closest = hits[i];

            var tile = closest.collider.GetComponent<TileView3D>();
            if (tile == null) return;

            _pressedTile = tile;
            _pressScreenPos = screenPos;
            _pressWorldPoint = ScreenToWorldOnTilePlane(screenPos);
            _isDragging = false;
        }

        private Vector3 ScreenToWorldOnTilePlane(Vector3 screenPos)
        {
            var ray = _targetCamera.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.forward, _pressedTile != null ? _pressedTile.transform.position : Vector3.zero);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : Vector3.zero;
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
            if (wasDragging && tile != null) tile.EndDrag();
        }

        private void ResetPressState()
        {
            _pressedTile = null;
            _isDragging = false;
            _activeFingerId = -1;
        }
    }
}
