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

        public int SlotCount => _slots.Count;

        public void Initialize(int maxTraySize)
        {
            foreach (var slot in _slots)
                if (slot != null) Destroy(slot.gameObject);
            _slots.Clear();

            for (int i = 0; i < maxTraySize; i++)
            {
                var slotGO = Instantiate(traySlotPrefab, slotAnchors[i].position, Quaternion.identity, transform);
                var slotView = slotGO.GetComponent<TraySlotView3D>();
                _slots.Add(slotView);
                slotView.SetEmpty();
            }
        }

        public Vector3 GetSlotWorldPosition(int index) => _slots[index].transform.position;

        public void PlayArrivalPopIn(int index, GameObject foodModelPrefab)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].PlayPopIn(foodModelPrefab);
        }

        public GameObject SpawnFlightCard(GameObject foodModelPrefab, Vector3 startWorldPosition)
        {
            var flightCard = Instantiate(traySlotPrefab, startWorldPosition, Quaternion.identity);
            var flightSlotView = flightCard.GetComponent<TraySlotView3D>();
            flightSlotView.SetFilled(foodModelPrefab);
            return flightCard;
        }

        public IEnumerator ResolveAfterPush(
            List<string> oldTrayIds, string newTileId, List<string> newTrayIds, BoardState board)
        {
            var beforePush = new List<string>(oldTrayIds) { newTileId };

            if (newTrayIds.Count == beforePush.Count)
                yield break;

            var matchedIds = beforePush.Except(newTrayIds).ToList();
            int firstIndex = beforePush.IndexOf(matchedIds[0]);
            int secondIndex = beforePush.IndexOf(matchedIds[1]);

            bool clearedFirst = false, clearedSecond = false;
            _slots[firstIndex].PlayHighlightThenClear(() => clearedFirst = true);
            _slots[secondIndex].PlayHighlightThenClear(() => clearedSecond = true);

            yield return new WaitUntil(() => clearedFirst && clearedSecond);

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
            Destroy(flightCard);

            _slots[toIndex].SetFilled(foodModel);
        }
    }
}
