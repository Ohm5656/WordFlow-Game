using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WordFlow.Adventure.View
{
    /// <summary>A slot placeholder (outlined square). Raises Tapped(slotIndex) to return its tile.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TileSlot : MonoBehaviour, IPointerClickHandler
    {
        public int SlotIndex { get; private set; }
        public event Action<int> Tapped;

        public void Configure(int slotIndex) => SlotIndex = slotIndex;

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(SlotIndex);
    }
}
