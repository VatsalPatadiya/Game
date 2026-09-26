using System.Collections;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Plays for a fixed duration right after Unity's own mandatory splash
    // (unavoidable on Unity Personal - see the design discussion), then hides
    // itself to reveal whatever screen was already going to show underneath
    // (LevelStartScreen3D today). Self-contained: GameSceneBuilder3D builds
    // this object active-by-default and parents nothing to it that isn't part
    // of the splash itself, so hiding it never affects the rest of the scene.
    public sealed class AppSplashScreen3D : MonoBehaviour
    {
        [SerializeField] private float _displaySeconds = 1.8f;

        private void Start()
        {
            StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(_displaySeconds);
            gameObject.SetActive(false);
        }
    }
}
