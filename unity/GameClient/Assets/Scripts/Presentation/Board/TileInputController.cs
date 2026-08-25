using UnityEngine;

namespace GameClient.Presentation.Board
{
    public sealed class TileInputController : MonoBehaviour
    {
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private GameController _gameController;

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            var worldPoint = _targetCamera.ScreenToWorldPoint(Input.mousePosition);
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
