using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.Effects
{
    public sealed class ComboEffect : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private float _duration = 0.6f;
        [SerializeField] private float _riseDistance = 40f;

        public void Show(int points)
        {
            if (_label != null)
                _label.text = "+" + points;
            StartCoroutine(RiseAndFade());
        }

        private IEnumerator RiseAndFade()
        {
            var rectTransform = transform as RectTransform;
            Vector3 start = rectTransform != null ? rectTransform.anchoredPosition3D : transform.localPosition;
            Vector3 end = start + new Vector3(0f, _riseDistance, 0f);

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);

                if (rectTransform != null)
                    rectTransform.anchoredPosition3D = Vector3.Lerp(start, end, t);
                else
                    transform.localPosition = Vector3.Lerp(start, end, t);

                if (_label != null)
                {
                    var color = _label.color;
                    color.a = 1f - t;
                    _label.color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
