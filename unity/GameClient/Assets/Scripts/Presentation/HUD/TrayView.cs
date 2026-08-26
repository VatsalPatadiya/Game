using System.Collections.Generic;
using GameDomain.Model;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation
{
    public class TrayView : MonoBehaviour
    {
        public HorizontalLayoutGroup layoutGroup;
        public GameObject traySlotPrefab;
        
        private List<GameObject> _slots = new List<GameObject>();
        
        public void Initialize(int maxTraySize)
        {
            // Clear existing slots
            foreach (var slot in _slots)
            {
                if (slot != null) Destroy(slot);
            }
            _slots.Clear();
            
            for (int i = 0; i < maxTraySize; i++)
            {
                var slot = Instantiate(traySlotPrefab, layoutGroup.transform);
                _slots.Add(slot);
                
                // Hide any text/image inside initially
                var text = slot.transform.Find("Text")?.GetComponent<Text>();
                if (text != null) text.text = "";
                
                var icon = slot.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null) icon.color = new Color(0, 0, 0, 0); // Transparent
            }
        }
        
        public void UpdateTray(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            // Update slots to match TrayTileIds
            for (int i = 0; i < _slots.Count; i++)
            {
                var textMesh = _slots[i].transform.Find("Text")?.GetComponent<Text>();
                var iconImage = _slots[i].transform.Find("Icon")?.GetComponent<Image>();
                
                if (i < board.TrayTileIds.Count)
                {
                    string slotId = board.TrayTileIds[i];
                    var cell = board.Cells[slotId];
                    if (textMesh != null) textMesh.text = ((char)('A' + int.Parse(cell.Value))).ToString();
                    
                    if (iconImage != null)
                    {
                        iconImage.color = ColorForValue(cell.Value);
                    }
                }
                else
                {
                    if (textMesh != null) textMesh.text = "";
                    if (iconImage != null) iconImage.color = new Color(0, 0, 0, 0); // Hide icon
                }
            }
        }

        private static Color ColorForValue(string value)
        {
            int hash = value.GetHashCode();
            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.65f, 0.9f);
        }
    }
}
