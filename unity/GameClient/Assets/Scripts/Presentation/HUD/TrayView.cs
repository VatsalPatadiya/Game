using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameDomain.Model;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public class TrayView : MonoBehaviour
    {
        private const float ReflowDuration = 0.15f;

        public HorizontalLayoutGroup layoutGroup;
        public GameObject traySlotPrefab;
        public TileSetAsset tileSet;

        private List<TraySlotView> _slots = new List<TraySlotView>();

        public int SlotCount => _slots.Count;

        public void Initialize(int maxTraySize)
        {
            foreach (var slot in _slots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            _slots.Clear();

            for (int i = 0; i < maxTraySize; i++)
            {
                var slotGO = Instantiate(traySlotPrefab, layoutGroup.transform);
                var slotView = slotGO.GetComponent<TraySlotView>();
                _slots.Add(slotView);
                slotView.SetEmpty();
            }
        }

        public Vector3 GetSlotScreenPosition(int index) => _slots[index].RectTransform.position;

        public void PlayArrivalPopIn(int index, Sprite icon, Color accentColor)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].PlayPopIn(icon, accentColor);
        }

        // Spawns a free-standing copy of the tray card (parented under the
        // Canvas root, not the layout group, so it can be positioned/animated
        // independently) — shared by GameController's tap-to-tray flight and
        // this class's own reflow.
        public GameObject SpawnFlightCard(Sprite icon, Color accentColor, Vector3 startScreenPosition)
        {
            var flightCard = Instantiate(traySlotPrefab, transform.root, false);
            var flightSlotView = flightCard.GetComponent<TraySlotView>();
            flightSlotView.SetFilled(icon, accentColor);
            var rect = (RectTransform)flightCard.transform;
            rect.position = startScreenPosition;
            return flightCard;
        }

        // Compares the tray immediately before this push (plus the tile that
        // just landed) against the tray after TrayManager's match-check to
        // find which two slots to highlight+clear, then reflows whatever
        // remains into its new slot positions. Slot *indices* are what
        // matters here, since the tray's persistent slots are fixed by
        // index, not by tile identity.
        public IEnumerator ResolveAfterPush(
            List<string> oldTrayIds, string newTileId, List<string> newTrayIds, BoardState board)
        {
            var beforePush = new List<string>(oldTrayIds) { newTileId };

            if (newTrayIds.Count == beforePush.Count)
                yield break; // landed, no match — nothing further to animate

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
            var icon = TileVisual.IconFor(tileSet, value);
            var accent = TileVisual.AccentColorFor(tileSet, value);
            var fromPos = _slots[fromIndex].RectTransform.position;
            var toPos = _slots[toIndex].RectTransform.position;

            _slots[fromIndex].SetEmpty();

            var flightCard = SpawnFlightCard(icon, accent, fromPos);
            var rect = (RectTransform)flightCard.transform;
            yield return CardAnimator.MoveRectTransform(rect, fromPos, toPos, ReflowDuration);
            Destroy(flightCard);

            _slots[toIndex].SetFilled(icon, accent);
        }
    }
}
