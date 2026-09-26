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
        public Transform[] slotAnchors; 

        private List<TraySlotView3D> _slots = new List<TraySlotView3D>();
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
                slotGO.transform.localScale = Vector3.one;
                var slotView = slotGO.GetComponent<TraySlotView3D>();
                _slots.Add(slotView);
                slotView.SetEmpty();
            }
        }

        public Vector3 GetSlotWorldPosition(int index) => _slots[index].transform.position;

        // How far above the tray's top slot the incoming-tile flight should
        // apex, in units of the tray's own slot-to-slot spacing (derived from
        // the actual slot 0/1 world positions so it stays correct regardless
        // of camera tilt) - anchored to slot 0 (not the landing slot) so every
        // tile clears the whole tray box before descending, even one landing
        // in the bottom slot.
        private const float FlightApexAboveTopFactor = 1.0f;

        public Vector3 GetFlightApexWorldPosition()
        {
            Vector3 topSlotPos = _slots[0].transform.position;
            Vector3 traySlotUp = _slots[0].transform.position - _slots[1].transform.position;
            return topSlotPos + traySlotUp * FlightApexAboveTopFactor;
        }

        public void RenderTray(List<string> trayTileIds, BoardState board)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < trayTileIds.Count)
                {
                    var value = board.Cells[trayTileIds[i]].Value;
                    _slots[i].SetFilled(TileVisual.IconFor(tileSet, value));
                }
                else
                {
                    _slots[i].SetEmpty();
                }
            }
        }

        public void PlayArrivalPopIn(int index, Sprite tileSprite)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].PlayPopIn(tileSprite);
        }

        public GameObject SpawnFlightCard(Sprite tileSprite, Vector3 startWorldPosition)
        {
            TraySlotView3D flightSlotView;
            if (_flightCardPool.Count > 0)
            {
                flightSlotView = _flightCardPool.Pop();
                flightSlotView.transform.SetPositionAndRotation(startWorldPosition, Quaternion.identity);
                flightSlotView.transform.localScale = Vector3.one;
                flightSlotView.gameObject.SetActive(true);
            }
            else
            {
                var flightCardGO = Instantiate(traySlotPrefab, startWorldPosition, Quaternion.identity);
                flightCardGO.transform.localScale = Vector3.one;
                flightSlotView = flightCardGO.GetComponent<TraySlotView3D>();
            }
            flightSlotView.SetFilled(tileSprite);
            flightSlotView.SetFlightTrailEnabled(true);
            return flightSlotView.gameObject;
        }

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
            var tileSprite = TileVisual.IconFor(tileSet, value);
            var fromPos = _slots[fromIndex].transform.position;
            var toPos = _slots[toIndex].transform.position;

            _slots[fromIndex].SetEmpty();

            var flightCard = SpawnFlightCard(tileSprite, fromPos);
            yield return CardAnimator.MoveTransform(flightCard.transform, fromPos, toPos, ReflowDuration);
            ReleaseFlightCard(flightCard);

            _slots[toIndex].SetFilled(tileSprite);
        }
    }
}
