using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PressScaleButton3D : MonoBehaviour
    {
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private float _pressedScale = 0.92f;
        [SerializeField] private float _scaleSpeed = 12f;

        public System.Action OnClick;
        // Mirrors Unity UI's Button.interactable - ControlButtonUsesDisplay3D
        // (below) sets this false when a control has 0 uses remaining.
        public bool Interactable = true;

        private Vector3 _restScale;
        private bool _isPressed;
        private int _activeFingerId = -1;

        private void Awake() => _restScale = transform.localScale;

        private void Update()
        {
            var target = _isPressed ? _restScale * _pressedScale : _restScale;
            transform.localScale = Vector3.Lerp(transform.localScale, target, Time.deltaTime * _scaleSpeed);

            if (!_isPressed)
            {
                if (Interactable && TryGetPressStart(out Vector3 screenPos) && HitsThis(screenPos))
                    _isPressed = true;
                return;
            }

            if (TryGetReleaseForActive(out bool released) && released)
            {
                _isPressed = false;
                _activeFingerId = -1;
                OnClick?.Invoke();
            }
        }

        private bool HitsThis(Vector3 screenPos)
        {
            var ray = _targetCamera.ScreenPointToRay(screenPos);
            return Physics.Raycast(ray, out var hit) && hit.collider.gameObject == gameObject;
        }

        private bool TryGetPressStart(out Vector3 screenPos)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _activeFingerId = touch.fingerId;
                    screenPos = touch.position;
                    return true;
                }
            }
            if (Input.GetMouseButtonDown(0))
            {
                screenPos = Input.mousePosition;
                return true;
            }
            screenPos = default;
            return false;
        }

        private bool TryGetReleaseForActive(out bool released)
        {
            if (_activeFingerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.fingerId != _activeFingerId) continue;
                    released = touch.phase == TouchPhase.Ended;
                    return true;
                }
                released = true; // finger disappeared without an Ended phase - treat as released
                return true;
            }
            released = Input.GetMouseButtonUp(0);
            return true;
        }
    }
}
