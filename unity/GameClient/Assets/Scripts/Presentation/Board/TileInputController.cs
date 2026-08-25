using UnityEngine;

namespace GameClient.Presentation.Board
{
    public sealed class TileInputController : MonoBehaviour
    {
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private GameController _gameController;

        private void Update()
        {
            Vector3? pointerPos = null;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    if (Input.GetTouch(i).phase == TouchPhase.Began)
                    {
                        pointerPos = Input.GetTouch(i).position;
                        break;
                    }
                }
            }
            
            if (pointerPos == null && Input.GetMouseButtonDown(0))
            {
                pointerPos = Input.mousePosition;
            }

            if (pointerPos == null) return;

            var worldPoint = _targetCamera.ScreenToWorldPoint(pointerPos.Value);
            var hits = Physics2D.OverlapPointAll(new Vector2(worldPoint.x, worldPoint.y));

            TileView topmost = null;
            foreach (var hit in hits)
            {
                var view = hit.GetComponent<TileView>();
                if (view == null) continue;
                if (topmost == null || view.Layer > topmost.Layer)
                    topmost = view;
            }

            if (topmost != null)
                _gameController.OnTileTapped(topmost.SlotId);
        }
    }
}
