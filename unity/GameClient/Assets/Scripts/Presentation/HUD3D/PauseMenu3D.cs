using System.Collections;
using GameClient.Presentation;
using GameDomain.Progression;
using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Pause menu overlay (sub-project #4D): opened by the top menu (hamburger)
    // button. Offers Resume, Restart, and Sound/Music toggles. Settings persist
    // via SaveSystem (audio wiring pending audio assets - the toggles record the
    // preference now). Built by GameSceneBuilder3D on an always-active root that
    // toggles a child overlay.
    public sealed class PauseMenu3D : MonoBehaviour
    {
        [SerializeField] private GameObject _overlay;
        [SerializeField] private PressScaleButton3D _menuButton; // top hamburger - opens the overlay
        [SerializeField] private PressScaleButton3D _resumeButton;
        [SerializeField] private PressScaleButton3D _restartButton;
        [SerializeField] private PressScaleButton3D _soundToggle;
        [SerializeField] private PressScaleButton3D _musicToggle;
        [SerializeField] private TMP_Text _soundLabel;
        [SerializeField] private TMP_Text _musicLabel;
        [SerializeField] private GameController _gameController;
        // Hidden while the pause overlay is up (their always-on-top icon glyphs
        // would otherwise draw over the overlay regardless of depth).
        [SerializeField] private GameObject[] _gameHudObjects;
        // So Pause can never open on top of a still-visible win/lose popup -
        // the reverse of GameOverPopup3D's own pauseMenu?.Hide() guard.
        [SerializeField] private GameOverPopup3D _gameOverPopup;
        // Hidden (not just visually covered) while paused: the board and this
        // panel are positioned via two different camera-distance systems, and
        // an upper-layer board tile can be genuinely closer to the camera than
        // the panel, winning the depth test and showing through it regardless
        // of panel size. SetAlwaysOnTop/_ZTest override is a documented no-op
        // in this URP setup, so the only reliable fix is to not render the
        // board at all while the panel is up - the same technique already used
        // for _gameHudObjects, just kept separate since the board isn't part
        // of that shared array (which other screens also hide/show).
        [SerializeField] private GameObject _boardRoot;

        private SettingsData _settings;
        private Coroutine _showAnimation;

        public bool IsOpen => _overlay != null && _overlay.activeSelf;

        private void Awake()
        {
            _settings = SaveSystem.LoadSettings();
            if (_overlay != null) _overlay.SetActive(false);
        }

        private void Start()
        {
            if (_menuButton != null) _menuButton.OnClick += Show;
            if (_resumeButton != null) _resumeButton.OnClick += Hide;
            // Hide() FIRST (re-activates the HUD incl. the tray) then RestartLevel:
            // rebuilding the tray while its GameObject is inactive skips the slots'
            // Awake and left them half-height (the retry bug). TraySlotView3D is
            // also defensively lazy-init now, but this keeps the intent clear.
            if (_restartButton != null) _restartButton.OnClick += () => { Hide(); _gameController?.RestartLevel(); };
            if (_soundToggle != null) _soundToggle.OnClick += () => { _settings.SoundOn = !_settings.SoundOn; Persist(); };
            if (_musicToggle != null) _musicToggle.OnClick += () => { _settings.MusicOn = !_settings.MusicOn; Persist(); };
            RefreshLabels();
        }

        public void Show()
        {
            // One popup at a time: if a win/lose result is showing, the user
            // must dismiss it first rather than Pause silently closing it out
            // from under them.
            if (_gameOverPopup != null && _gameOverPopup.IsShowing) return;
            _gameController?.SetPaused(true);
            SetHudActive(false);
            if (_boardRoot != null) _boardRoot.SetActive(false);
            if (_overlay != null)
            {
                _overlay.SetActive(true);
                if (_showAnimation != null) StopCoroutine(_showAnimation);
                _showAnimation = StartCoroutine(PlayShowAnimation(_overlay.transform));
            }
            RefreshLabels();
        }

        public void Hide()
        {
            if (_overlay != null) _overlay.SetActive(false);
            SetHudActive(true);
            if (_boardRoot != null) _boardRoot.SetActive(true);
            _gameController?.SetPaused(false);
        }

        // A popup that just snaps into existence reads as cheap. Scale-only (no
        // fade): the panel/button materials are opaque, so animating alpha on
        // them has no visual effect - scale is the safe, always-works option.
        private static IEnumerator PlayShowAnimation(Transform target)
        {
            const float duration = 0.15f;
            var startScale = Vector3.one * 0.85f;
            var endScale = Vector3.one;
            target.localScale = startScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                target.localScale = Vector3.Lerp(startScale, endScale, eased);
                yield return null;
            }
            target.localScale = endScale;
        }

        private void SetHudActive(bool active)
        {
            if (_gameHudObjects == null) return;
            foreach (var go in _gameHudObjects)
                if (go != null) go.SetActive(active);
        }

        private void Persist()
        {
            SaveSystem.SaveSettings(_settings);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (_settings == null) _settings = new SettingsData();
            if (_soundLabel != null) _soundLabel.text = "Sound: " + (_settings.SoundOn ? "ON" : "OFF");
            if (_musicLabel != null) _musicLabel.text = "Music: " + (_settings.MusicOn ? "ON" : "OFF");
        }
    }
}
