using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // One shared back-navigation handler (round-2 fix 5), driven by BOTH the
    // Android hardware/gesture back button (mapped to KeyCode.Escape) and the
    // on-screen back button - so they always do the same thing. Priority:
    //   1. a win/lose popup is showing -> close it
    //   2. pause overlay open  -> close it
    //   3. in gameplay (HUD up) -> return to level select
    //   4. level-start screen up -> return to level select
    //   5. on level select      -> nothing
    // Also closes whichever popup is open if the app loses focus (task-switch,
    // notification shade, incoming call, screen lock) - scoped to popups only,
    // not the full back-navigation cascade, so a brief app-switch never dumps
    // the player out of a level.
    public sealed class BackNavigator : MonoBehaviour
    {
        [SerializeField] private GameObject _levelSelectScreen;
        [SerializeField] private GameObject _levelStartScreen;
        [SerializeField] private GameObject[] _gameHudObjects;
        [SerializeField] private PauseMenu3D _pauseMenu;
        [SerializeField] private GameOverPopup3D _gameOverPopup;
        [SerializeField] private PressScaleButton3D _backButton;

        private void Start()
        {
            if (_backButton != null) _backButton.OnClick += HandleBack;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CloseAnyOpenPopup();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) CloseAnyOpenPopup();
        }

        private void CloseAnyOpenPopup()
        {
            if (_gameOverPopup != null && _gameOverPopup.IsShowing) _gameOverPopup.Hide();
            if (_pauseMenu != null && _pauseMenu.IsOpen) _pauseMenu.Hide();
        }

        public void HandleBack()
        {
            if (_gameOverPopup != null && _gameOverPopup.IsShowing) { _gameOverPopup.Hide(); return; }
            if (_pauseMenu != null && _pauseMenu.IsOpen) { _pauseMenu.Hide(); return; }
            if (IsHudActive()) { GoToLevelSelect(); return; }
            if (_levelStartScreen != null && _levelStartScreen.activeSelf) { GoToLevelSelect(); return; }
            // On the level-select screen: nothing to go back to.
        }

        private bool IsHudActive()
        {
            if (_gameHudObjects == null) return false;
            foreach (var go in _gameHudObjects)
                if (go != null && go.activeSelf) return true;
            return false;
        }

        private void GoToLevelSelect()
        {
            SetActiveAll(_gameHudObjects, false);
            if (_levelStartScreen != null) _levelStartScreen.SetActive(false);
            if (_levelSelectScreen != null) _levelSelectScreen.SetActive(true);
        }

        private static void SetActiveAll(GameObject[] gos, bool active)
        {
            if (gos == null) return;
            foreach (var go in gos)
                if (go != null) go.SetActive(active);
        }
    }
}
