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

        private void Awake()
        {
            SetHudActive(false);
        }

        private void Start()
        {
            if (_playButton != null)
                _playButton.OnClick += HandlePlay;
        }

        private void HandlePlay()
        {
            SetHudActive(true);
            gameObject.SetActive(false);
            _gameController?.BeginLevel();
        }

        private void SetHudActive(bool active)
        {
            if (_gameHudObjects == null) return;
            foreach (var go in _gameHudObjects)
                if (go != null) go.SetActive(active);
        }
    }
}
