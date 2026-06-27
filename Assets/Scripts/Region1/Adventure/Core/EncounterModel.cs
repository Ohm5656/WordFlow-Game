using System;
using System.Collections.Generic;
using System.Text;

namespace WordFlow.Adventure.Core
{
    /// <summary>
    /// Pure tap-to-place state. Tray tiles keep fixed indices (tray order never changes).
    /// Slots fill left->right; returning a slot frees its tile back to the tray.
    /// </summary>
    public sealed class EncounterModel
    {
        private readonly string[] _trayGraphemes; // by fixed tray index
        private readonly bool[] _placed;          // tray tile currently in a slot?
        private readonly int[] _slotToTray;       // slot index -> tray index, or -1

        public EncounterModel(IReadOnlyList<string> trayGraphemes)
            : this(trayGraphemes, trayGraphemes != null ? trayGraphemes.Count : 0) { }

        public EncounterModel(IReadOnlyList<string> trayGraphemes, int slotCount)
        {
            if (trayGraphemes == null || trayGraphemes.Count == 0)
                throw new ArgumentException("trayGraphemes must be non-empty");
            int n = trayGraphemes.Count;
            if (slotCount < 1 || slotCount > n)
                throw new ArgumentException("slotCount must be between 1 and trayGraphemes.Count");
            _trayGraphemes = new string[n];
            for (int i = 0; i < n; i++) _trayGraphemes[i] = trayGraphemes[i];
            _placed = new bool[n];
            _slotToTray = new int[slotCount];
            for (int i = 0; i < slotCount; i++) _slotToTray[i] = -1;
        }

        public int TrayCount => _trayGraphemes.Length;
        public int SlotCount => _slotToTray.Length;

        public bool IsInTray(int trayIndex) =>
            trayIndex >= 0 && trayIndex < _placed.Length && !_placed[trayIndex];

        public int SlotTrayIndex(int slotIndex) =>
            slotIndex >= 0 && slotIndex < _slotToTray.Length ? _slotToTray[slotIndex] : -1;

        public string TrayGrapheme(int trayIndex) =>
            trayIndex >= 0 && trayIndex < _trayGraphemes.Length ? _trayGraphemes[trayIndex] : "";

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < _slotToTray.Length; i++)
                    if (_slotToTray[i] < 0) return false;
                return true;
            }
        }

        /// <summary>Concatenation of filled slots, left->right (partial allowed).</summary>
        public string BuiltString
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < _slotToTray.Length; i++)
                {
                    int t = _slotToTray[i];
                    if (t >= 0) sb.Append(_trayGraphemes[t]);
                }
                return sb.ToString();
            }
        }

        /// <summary>Move a tray tile into the lowest empty slot. Returns the slot index, or -1 on failure.</summary>
        public int PlaceFromTrayAt(int trayIndex)
        {
            if (!IsInTray(trayIndex)) return -1;
            for (int s = 0; s < _slotToTray.Length; s++)
            {
                if (_slotToTray[s] < 0)
                {
                    _slotToTray[s] = trayIndex;
                    _placed[trayIndex] = true;
                    return s;
                }
            }
            return -1;
        }

        public bool PlaceFromTray(int trayIndex) => PlaceFromTrayAt(trayIndex) >= 0;

        /// <summary>Empty a slot; its tile returns to the tray. Returns the freed tray index, or -1.</summary>
        public int ReturnSlotAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotToTray.Length) return -1;
            int t = _slotToTray[slotIndex];
            if (t < 0) return -1;
            _slotToTray[slotIndex] = -1;
            _placed[t] = false;
            return t;
        }

        public bool ReturnSlot(int slotIndex) => ReturnSlotAt(slotIndex) >= 0;

        public void ResetToTray()
        {
            for (int s = 0; s < _slotToTray.Length; s++) _slotToTray[s] = -1;
            for (int t = 0; t < _placed.Length; t++) _placed[t] = false;
        }
    }
}
