using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WordFlow.Adventure.View
{
    /// <summary>A tray tile placeholder (colored square + grapheme). Raises Tapped(trayIndex)
    /// on click and Hovered(trayIndex) on pointer-enter (hover-to-hear the phoneme).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class StoneTile : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        public int TrayIndex { get; private set; }
        public string Grapheme { get; private set; }
        public event Action<int> Tapped;
        public event Action<int> Hovered;

        public void Configure(int trayIndex, string grapheme)
        {
            TrayIndex = trayIndex;
            Grapheme = grapheme;
        }

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke(TrayIndex);

        public void OnPointerEnter(PointerEventData eventData) => Hovered?.Invoke(TrayIndex);
    }
}
