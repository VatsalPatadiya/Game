using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // One shared back-navigation handler (round-2 fix 5), driven by BOTH the
    // Android hardware/gesture back button (mapped to KeyCode.Escape) and the
    // on-screen back button - so they always do the same thing. Priority:
    //   1. pause overlay open  -> close it
    //   2. in gameplay (HUD up) -> return to level select
    //   3. level-start screen up -> return to level select
    //   4. on level select      -> nothing
    public sealed class BackNavigator : MonoBehaviour
    {
        [SerializeField] private GameObject _levelSelectScreen;
        [SerializeField] private GameObject _levelStartScreen;
        [SerializeField] private GameObject[] _gameHudObjects;
        [SerializeField] private PauseMenu3D _pauseMenu;
        [SerializeField] private PressScaleButton3D _backButton;

        private void Start()
        {
            if (_backButton != null) _backButton.OnClick += HandleBack;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleBack();
        }

        public void HandleBack()
        {
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
