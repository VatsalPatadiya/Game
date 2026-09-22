using System.Collections;
using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public class GameOverPopup3D : MonoBehaviour
    {
        public PressScaleButton3D restartButton;
        public TextMeshPro titleText;
        public TextMeshPro messageText;
        public TextMeshPro primaryButtonText;
        // 3 star quads built by GameSceneBuilder3D.BuildStarRow; colored at
        // runtime per the actual result instead of a fixed sample.
        public MeshRenderer[] starRenderers;
        // Lose-only: 4 tray-slot chips + a caption ("N OF 4 SLOTS · NO
        // PAIR") that shows WHY the tray is full, instead of just saying so.
        public GameObject trayChipRow;
        public MeshRenderer[] trayChipRenderers;
        public TextMeshPro trayChipCaption;
        // Win-only: score split out of the message sentence into its own
        // eyebrow + big Cinzel numeral, reading as a result instead of a caption.
        public GameObject scoreBlock;
        public TextMeshPro scoreValueText;
        // So a win/lose/stuck popup can never render underneath an open Pause
        // overlay - each one force-closes the other before showing itself.
        public PauseMenu3D pauseMenu;

        private static readonly Color StarGold = new Color(0.96f, 0.78f, 0.36f);
        private static readonly Color StarMuted = new Color(0.44f, 0.32f, 0.18f);
        private static readonly Color ChipFilled = new Color(0.97f, 0.93f, 0.82f); // cream ivory
        private static readonly Color ChipEmpty = new Color(0.060f, 0.080f, 0.100f); // TrayRecess dark well

        public bool IsShowing => gameObject.activeSelf;

        private GameController _gameController;
        private Coroutine _showAnimation;

        private void Start()
        {
            if (restartButton != null)
                restartButton.OnClick += () => _gameController?.RestartLevel();
        }

        public void ShowWin(GameController controller, int score, int starsEarned)
        {
            _gameController = controller;
            pauseMenu?.Hide();
            SetTitle("Well done!");
            if (messageText != null) messageText.gameObject.SetActive(false);
            SetButtonLabel("NEXT LEVEL"); // all-caps, matches Resume/Restart/Play
            SetStars(starsEarned);
            if (trayChipRow != null) trayChipRow.SetActive(false);
            if (scoreBlock != null) scoreBlock.SetActive(true);
            if (scoreValueText != null)
            {
                scoreValueText.text = score.ToString("N0");
                scoreValueText.ForceMeshUpdate();
            }
            ShowPopup();
        }

        public void ShowStuck(GameController controller)
        {
            _gameController = controller;
            pauseMenu?.Hide();
            SetTitle("No matches left");
            SetMessage("Try shuffling, or start a fresh board.");
            SetButtonLabel("TRY AGAIN"); // all-caps, matches Resume/Restart/Play
            SetStars(-1);
            if (trayChipRow != null) trayChipRow.SetActive(false);
            if (scoreBlock != null) scoreBlock.SetActive(false);
            ShowPopup();
        }

        public void ShowLose(GameController controller, int trayCount, int trayMax)
        {
            _gameController = controller;
            pauseMenu?.Hide();
            SetTitle("Tray full!");
            SetMessage("No more matches possible. Try again!");
            SetButtonLabel("TRY AGAIN"); // all-caps, matches Resume/Restart/Play
            SetStars(-1);
            if (scoreBlock != null) scoreBlock.SetActive(false);
            SetTrayChips(trayCount, trayMax);
            ShowPopup();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ShowPopup()
        {
            gameObject.SetActive(true);
            if (_showAnimation != null) StopCoroutine(_showAnimation);
            _showAnimation = StartCoroutine(PlayShowAnimation(transform));
        }

        // Scale-only (no fade) - the panel/button materials are opaque, so
        // animating alpha has no visual effect; scale is the safe, always-works
        // option for a popup that shouldn't just snap into existence.
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

        // TextMeshPro doesn't rebuild its mesh the instant .text is set - it
        // normally catches up next frame, but a capture/screenshot taken
        // immediately after (or two popups opened back-to-back the same
        // frame) can observe stale glyphs. ForceMeshUpdate matches the same
        // defensive call GameSceneBuilder3D.CreateLabel3D already makes at
        // build time, applied here to the runtime-changed labels too.
        private void SetTitle(string text)
        {
            if (titleText == null) return;
            titleText.text = text;
            titleText.ForceMeshUpdate();
        }

        private void SetMessage(string text)
        {
            if (messageText == null) return;
            messageText.gameObject.SetActive(true);
            messageText.text = text;
            messageText.ForceMeshUpdate();
        }

        private void SetButtonLabel(string text)
        {
            if (primaryButtonText == null) return;
            primaryButtonText.text = text;
            primaryButtonText.ForceMeshUpdate();
        }

        // filledCount < 0 hides the row entirely (loses/stuck/daily have no
        // star rating).
        private void SetStars(int filledCount)
        {
            if (starRenderers == null) return;
            bool show = filledCount >= 0;
            for (int i = 0; i < starRenderers.Length; i++)
            {
                var r = starRenderers[i];
                if (r == null) continue;
                r.gameObject.SetActive(show);
                if (show) r.material.SetColor("_BaseColor", i < filledCount ? StarGold : StarMuted);
            }
        }

        private void SetTrayChips(int filledCount, int totalCount)
        {
            if (trayChipRow != null) trayChipRow.SetActive(true);
            if (trayChipRenderers != null)
            {
                for (int i = 0; i < trayChipRenderers.Length; i++)
                {
                    var r = trayChipRenderers[i];
                    if (r == null) continue;
                    r.material.SetColor("_BaseColor", i < filledCount ? ChipFilled : ChipEmpty);
                }
            }
            if (trayChipCaption != null)
            {
                trayChipCaption.text = $"{filledCount} OF {totalCount} SLOTS · NO PAIR";
                trayChipCaption.ForceMeshUpdate();
            }
        }
    }
}
