using System.Collections.Generic;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameDomain.Model;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation
{
    public class TrayView : MonoBehaviour
    {
        public HorizontalLayoutGroup layoutGroup;
        public GameObject traySlotPrefab;
        public TileSetAsset tileSet;

        private static readonly Color EmptySlotColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color FilledCardColor = new Color(0.969f, 0.957f, 0.922f, 1f);

        private List<GameObject> _slots = new List<GameObject>();

        public void Initialize(int maxTraySize)
        {
            foreach (var slot in _slots)
            {
                if (slot != null) Destroy(slot);
            }
            _slots.Clear();

            for (int i = 0; i < maxTraySize; i++)
            {
                var slot = Instantiate(traySlotPrefab, layoutGroup.transform);
                _slots.Add(slot);
                SetSlotEmpty(slot);
            }
        }

        public void UpdateTray(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i < board.TrayTileIds.Count)
                {
                    string slotId = board.TrayTileIds[i];
                    var cell = board.Cells[slotId];
                    SetSlotFilled(_slots[i], cell.Value);
                }
                else
                {
                    SetSlotEmpty(_slots[i]);
                }
            }
        }

        private void SetSlotFilled(GameObject slot, string value)
        {
            var accentImage = slot.transform.Find("AccentBorder")?.GetComponent<Image>();
            var cardImage = slot.transform.Find("Card")?.GetComponent<Image>();
            var iconImage = slot.transform.Find("Icon")?.GetComponent<Image>();

            var accentColor = TileVisual.AccentColorFor(tileSet, value);
            accentColor.a = 1f;
            if (accentImage != null) accentImage.color = accentColor;
            if (cardImage != null) cardImage.color = FilledCardColor;

            if (iconImage != null)
            {
                iconImage.sprite = TileVisual.IconFor(tileSet, value);
                iconImage.color = accentColor;
                iconImage.enabled = true;
            }
        }

        private void SetSlotEmpty(GameObject slot)
        {
            var accentImage = slot.transform.Find("AccentBorder")?.GetComponent<Image>();
            var cardImage = slot.transform.Find("Card")?.GetComponent<Image>();
            var iconImage = slot.transform.Find("Icon")?.GetComponent<Image>();

            if (accentImage != null) accentImage.color = EmptySlotColor;
            if (cardImage != null) cardImage.color = new Color(0f, 0f, 0f, 0f);
            if (iconImage != null) iconImage.enabled = false;
        }
    }
}
