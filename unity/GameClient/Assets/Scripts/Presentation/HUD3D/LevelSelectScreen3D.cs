using System;
using GameDomain.Progression;
using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Level-select screen (sub-project #4B): shown first on scene load, in front
    // of the felt. Presents a row of level tokens; tapping an unlocked one selects
    // it and shows the level-start screen. Keeps the in-game HUD hidden until play
    // begins. Built by GameSceneBuilder3D.
    public sealed class LevelSelectScreen3D : MonoBehaviour
    {
        [SerializeField] private PressScaleButton3D[] _levelButtons;
        [SerializeField] private int[] _levelIds;
        [SerializeField] private TMP_Text[] _starTexts;   // per token: earned stars
        [SerializeField] private TMP_Text[] _numberTexts; // per token: level number (dimmed when locked)
        [SerializeField] private GameObject _levelStartScreen;
        [SerializeField] private GameObject[] _gameHudObjects;
        [SerializeField] private GameController _gameController;
        [SerializeField] private PressScaleButton3D _dailyButton;
        [SerializeField] private TMP_Text _dailyLabel;

        private static readonly Color LockedTint = new Color(0.45f, 0.43f, 0.38f, 1f);
        private static readonly Color UnlockedTint = new Color(0.96f, 0.93f, 0.84f, 1f);

        private void Awake()
        {
            SetActiveAll(_gameHudObjects, false);
            if (_levelStartScreen != null) _levelStartScreen.SetActive(false);
        }

        private void OnEnable()
        {
            RefreshTokens();
        }

        // Update each token's star pips + lock dimming from saved progress.
        private void RefreshTokens()
        {
            var progress = _gameController != null ? _gameController.Progress : null;
            if (_levelButtons == null) return;
            for (int i = 0; i < _levelButtons.Length; i++)
            {
                int id = (_levelIds != null && i < _levelIds.Length) ? _levelIds[i] : i + 1;
                bool unlocked = progress == null || progress.IsUnlocked(id);
                int stars = progress != null ? progress.GetStars(id) : 0;

                if (_numberTexts != null && i < _numberTexts.Length && _numberTexts[i] != null)
                    _numberTexts[i].color = unlocked ? UnlockedTint : LockedTint;

                if (_starTexts != null && i < _starTexts.Length && _starTexts[i] != null)
                    _starTexts[i].text = unlocked ? StarString(stars) : "locked";
            }

            if (_dailyLabel != null)
            {
                bool done = progress != null && progress.IsDailyDone(DailyChallenge.DateKey(DateTime.Now));
                _dailyLabel.text = done ? "DAILY  DONE" : "DAILY CHALLENGE";
            }
        }

        private static string StarString(int stars)
        {
            // No star glyph in the bundled fonts; use filled/empty dots.
            stars = Mathf.Clamp(stars, 0, 3);
            return new string('*', stars) + new string('.', 3 - stars);
        }

        private void Start()
        {
            if (_levelButtons == null) return;
            for (int i = 0; i < _levelButtons.Length; i++)
            {
                if (_levelButtons[i] == null) continue;
                int id = (_levelIds != null && i < _levelIds.Length) ? _levelIds[i] : i + 1;
                _levelButtons[i].OnClick += () => HandlePick(id);
            }
            if (_dailyButton != null) _dailyButton.OnClick += HandleDaily;
        }

        // The daily challenge skips the level-start screen: reveal the HUD and
        // deal the seeded daily board straight away.
        private void HandleDaily()
        {
            if (_gameController == null) return;
            SetActiveAll(_gameHudObjects, true);
            gameObject.SetActive(false);
            _gameController.BeginDaily();
        }

        private void HandlePick(int levelId)
        {
            if (_gameController == null) return;
            if (_gameController.Progress != null && !_gameController.Progress.IsUnlocked(levelId))
                return; // locked - ignore
            _gameController.SelectLevel(levelId);
            if (_levelStartScreen != null) _levelStartScreen.SetActive(true);
            gameObject.SetActive(false);
        }

        private static void SetActiveAll(GameObject[] gos, bool active)
        {
            if (gos == null) return;
            foreach (var go in gos)
                if (go != null) go.SetActive(active);
        }
    }
}
