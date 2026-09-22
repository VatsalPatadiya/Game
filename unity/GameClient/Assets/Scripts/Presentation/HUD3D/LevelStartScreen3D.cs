using System.Collections;
using GameClient.Presentation.Board;
using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Full-screen "Level N" start screen (mockup's left phone): shown on scene
    // load in front of the felt, hides the in-game HUD until the player taps
    // Play, then reveals the HUD and deals the board in via
    // GameController.BeginLevel. Built by GameSceneBuilder3D.
    public sealed class LevelStartScreen3D : MonoBehaviour
    {
        [SerializeField] private PressScaleButton3D _playButton;
        [SerializeField] private GameController _gameController;
        // Progress bar, tray, top-bar discs and the bottom control buttons -
        // hidden while this screen is up so the game HUD (and its AlwaysOnTop
        // button-icon glyphs, which would otherwise draw over this screen
        // regardless of depth) stays out of sight until Play is tapped.
        [SerializeField] private GameObject[] _gameHudObjects;
        // Updated to reflect the current level. The "Level N" title label was
        // removed (redundant with this badge) - only the badge number remains.
        [SerializeField] private TMP_Text _badgeText;

        // Two halves of the arched door art (GameSceneBuilder3D.
        // BuildLevelStartScreen) - this screen's actual background, closed/
        // visible at rest (the badge and Play button sit on top of it). On
        // Play they slide apart by _doorSlideDistance (a local-space offset
        // along each door's own "outward" axis) to reveal the game HUD/
        // board underneath.
        [SerializeField] private Transform _doorLeft;
        [SerializeField] private Transform _doorRight;
        [SerializeField] private float _doorSlideDistance;
        // Badge disc/number + Play button pill/label (GameSceneBuilder3D
        // groups all four under this one GameObject). They sit on the door,
        // not on it as a child, so nothing moved them when the door slid -
        // hidden explicitly, the instant Play is tapped, instead.
        [SerializeField] private GameObject _overlayContent;
        private const float DoorSlideDuration = 2.0f; // 0.45 -> 0.9 -> 1.4 -> 2.0, plus switching the easing curve below to smoothstep
        private Vector3 _doorLeftClosedLocalPos;
        private Vector3 _doorRightClosedLocalPos;

        private void Awake()
        {
            SetHudActive(false);
            if (_doorLeft != null) _doorLeftClosedLocalPos = _doorLeft.localPosition;
            if (_doorRight != null) _doorRightClosedLocalPos = _doorRight.localPosition;
        }

        private void OnEnable()
        {
            // Clear any board left over from a game we just backed out of, so its
            // tiles don't show through this screen. No-op on the first (fresh) show.
            _gameController?.ClearBoard();
            RefreshLevel();
            // Re-arm the Play button and reset the doors closed/visible for
            // the next time this screen is shown (HandlePlay opened them).
            if (_playButton != null) _playButton.Interactable = true;
            if (_doorLeft != null) { _doorLeft.localPosition = _doorLeftClosedLocalPos; _doorLeft.gameObject.SetActive(true); }
            if (_doorRight != null) { _doorRight.localPosition = _doorRightClosedLocalPos; _doorRight.gameObject.SetActive(true); }
            if (_overlayContent != null) _overlayContent.SetActive(true);
        }

        private void RefreshLevel()
        {
            if (_gameController == null) return;
            int id = _gameController.CurrentLevelId;
            if (_badgeText != null) _badgeText.text = id.ToString();
        }

        private void Start()
        {
            if (_playButton != null)
                _playButton.OnClick += HandlePlay;
        }

        private void HandlePlay()
        {
            if (_playButton != null) _playButton.Interactable = false; // guard a second tap mid-transition
            if (_overlayContent != null) _overlayContent.SetActive(false); // badge/Play button vanish immediately, don't linger over the reveal
            StartCoroutine(PlayDoorTransition());
        }

        private IEnumerator PlayDoorTransition()
        {
            // Board/HUD stay untouched (inactive) while the doors slide, so
            // nothing is visible behind the widening gap. Only once both
            // doors have fully finished opening do we load and reveal the
            // board, all at once.
            Coroutine leftRoutine = null, rightRoutine = null;
            if (_doorLeft != null)
            {
                var openLocal = _doorLeftClosedLocalPos + Vector3.left * _doorSlideDistance;
                var openWorld = _doorLeft.parent.TransformPoint(openLocal);
                leftRoutine = StartCoroutine(SlideDoor(_doorLeft, _doorLeft.position, openWorld));
            }
            if (_doorRight != null)
            {
                var openLocal = _doorRightClosedLocalPos + Vector3.right * _doorSlideDistance;
                var openWorld = _doorRight.parent.TransformPoint(openLocal);
                rightRoutine = StartCoroutine(SlideDoor(_doorRight, _doorRight.position, openWorld));
            }
            if (leftRoutine != null) yield return leftRoutine;
            if (rightRoutine != null) yield return rightRoutine;

            // Doors are fully open now - reveal the board only at this point.
            SetHudActive(true);
            _gameController?.BeginLevel();

            gameObject.SetActive(false); // doors + everything else on this screen; OnEnable resets them closed next time
        }

        // Not CardAnimator.MoveTransform: BeginLevel above synchronously deals
        // the whole board (dozens of tile Instantiates spread across several
        // frames while it animates in), which inflates Time.deltaTime on
        // those frames - MoveTransform's unclamped `elapsed += Time.deltaTime`
        // would consume that inflated delta and jump most of the slide in one
        // step (confirmed on-device: visible for a single frame instead of
        // ~0.45s). Clamping the per-frame step here guarantees the slide
        // actually plays out over real frames regardless of hitches nearby.
        private static IEnumerator SlideDoor(Transform target, Vector3 fromWorldPos, Vector3 toWorldPos)
        {
            const float maxStep = 1f / 30f;
            target.position = fromWorldPos;
            float elapsed = 0f;
            while (elapsed < DoorSlideDuration)
            {
                elapsed += Mathf.Min(Time.deltaTime, maxStep);
                float raw = Mathf.Clamp01(elapsed / DoorSlideDuration);
                // Cubic smoothstep, not EaseOut: EaseOut's velocity peaks at
                // t=0, so the door launches at full speed instantly - that
                // abrupt start read as "too fast" no matter how long
                // DoorSlideDuration was. Smoothstep ramps up from rest and
                // back down to rest (same "organic acceleration and gentle
                // deceleration" curve CardAnimator.MoveTransformSmooth uses
                // for the tray flight), which is what actually reads as smooth.
                float t = raw * raw * (3f - 2f * raw);
                target.position = Vector3.Lerp(fromWorldPos, toWorldPos, t);
                yield return null;
            }
            target.position = toWorldPos;
        }

        private void SetHudActive(bool active)
        {
            if (_gameHudObjects == null) return;
            foreach (var go in _gameHudObjects)
                if (go != null) go.SetActive(active);
        }
    }
}
