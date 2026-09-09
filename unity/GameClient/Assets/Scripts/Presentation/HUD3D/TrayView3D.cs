using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public class TrayView3D : MonoBehaviour
    {
        private const float ReflowDuration = 0.15f;

        public GameObject traySlotPrefab;
        public TileSetAsset tileSet;
        public Transform[] slotAnchors; // fixed world positions, set by GameSceneBuilder3D (Task 10)

        private List<TraySlotView3D> _slots = new List<TraySlotView3D>();

        // Flight cards (the visual that flies from a tapped board tile into a
        // tray slot, and the ones used for reflow-after-match) are pooled -
        // rented on demand, returned (deactivated, not destroyed) when the
        // flight ends. See TraySlotView3D's own food-model pool for why:
        // Instantiate/Destroy on this path was the actual cause of animation
        // hitching, not the movement easing itself.
        private readonly Stack<TraySlotView3D> _flightCardPool = new Stack<TraySlotView3D>();

        public int SlotCount => _slots.Count;

        public void Initialize(int maxTraySize)
        {
            foreach (var slot in _slots)
                if (slot != null) Destroy(slot.gameObject);
            _slots.Clear();

            for (int i = 0; i < maxTraySize; i++)
            {
                var slotGO = Instantiate(traySlotPrefab, transform);
                slotGO.transform.position = slotAnchors[i].position;
                slotGO.transform.rotation = Quaternion.identity;
                // Force local scale to 1 to prevent Unity from adjusting it based on the parent's world scale
                slotGO.transform.localScale = Vector3.one;
                var slotView = slotGO.GetComponent<TraySlotView3D>();
                _slots.Add(slotView);
                slotView.SetEmpty();
            }
        }

        public Vector3 GetSlotWorldPosition(int index) => _slots[index].transform.position;

        // Re-render every slot from the current tray state (used after Undo pops a
        // tile back to the board). Slots beyond the tray count show empty.
        public void RenderTray(List<string> trayTileIds, BoardState board)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < trayTileIds.Count)
                {
                    var value = board.Cells[trayTileIds[i]].Value;
                    _slots[i].SetFilled(TileVisual.FoodModelFor(tileSet, value));
                }
                else
                {
                    _slots[i].SetEmpty();
                }
            }
        }

        public void PlayArrivalPopIn(int index, GameObject foodModelPrefab)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].PlayPopIn(foodModelPrefab);
        }

        public GameObject SpawnFlightCard(GameObject foodModelPrefab, Vector3 startWorldPosition)
        {
            TraySlotView3D flightSlotView;
            if (_flightCardPool.Count > 0)
            {
                flightSlotView = _flightCardPool.Pop();
                flightSlotView.transform.SetPositionAndRotation(startWorldPosition, Quaternion.identity);
                flightSlotView.gameObject.SetActive(true);
            }
            else
            {
                var flightCardGO = Instantiate(traySlotPrefab, startWorldPosition, Quaternion.identity);
                flightSlotView = flightCardGO.GetComponent<TraySlotView3D>();
            }
            flightSlotView.SetFilled(foodModelPrefab);
            flightSlotView.SetFlightTrailEnabled(true);
            return flightSlotView.gameObject;
        }

        // Returns a flight card to the pool instead of destroying it - callers
        // that used to Destroy(flight) after a MoveTransform coroutine should
        // call this instead.
        public void ReleaseFlightCard(GameObject flightCard)
        {
            if (flightCard == null) return;
            flightCard.GetComponent<TraySlotView3D>()?.SetFlightTrailEnabled(false);
            flightCard.SetActive(false);
            _flightCardPool.Push(flightCard.GetComponent<TraySlotView3D>());
        }

        public IEnumerator ResolveAfterPush(
            List<string> oldTrayIds, string newTileId, List<string> newTrayIds, BoardState board)
        {
            var beforePush = new List<string>(oldTrayIds) { newTileId };

            if (newTrayIds.Count == beforePush.Count)
                yield break;

            // Any number of matched tiles clear together (3 for a triple match).
            var matchedIds = beforePush.Except(newTrayIds).ToList();
            int clearedCount = 0;
            foreach (var id in matchedIds)
            {
                int index = beforePush.IndexOf(id);
                _slots[index].PlayHighlightThenClear(() => clearedCount++);
            }

            yield return new WaitUntil(() => clearedCount >= matchedIds.Count);

            var reflowRoutines = new List<Coroutine>();
            for (int newIndex = 0; newIndex < newTrayIds.Count; newIndex++)
            {
                string id = newTrayIds[newIndex];
                int oldIndex = beforePush.IndexOf(id);
                if (oldIndex == newIndex) continue;

                reflowRoutines.Add(StartCoroutine(ReflowSlot(oldIndex, newIndex, board.Cells[id].Value)));
            }

            foreach (var routine in reflowRoutines)
                yield return routine;

            for (int i = newTrayIds.Count; i < _slots.Count; i++)
                _slots[i].SetEmpty();
        }

        private IEnumerator ReflowSlot(int fromIndex, int toIndex, string value)
        {
            var foodModel = TileVisual.FoodModelFor(tileSet, value);
            var fromPos = _slots[fromIndex].transform.position;
            var toPos = _slots[toIndex].transform.position;

            _slots[fromIndex].SetEmpty();

            var flightCard = SpawnFlightCard(foodModel, fromPos);
            yield return CardAnimator.MoveTransform(flightCard.transform, fromPos, toPos, ReflowDuration);
            ReleaseFlightCard(flightCard);

            _slots[toIndex].SetFilled(foodModel);
        }
    }
}
