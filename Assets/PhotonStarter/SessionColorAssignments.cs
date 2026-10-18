using System;
using System.Collections.Generic;

namespace GameJam.Multiplayer
{
    // One instance per runner. The coordinator reserves before publishing the assignment.
    public sealed class SessionColorAssignments
    {
        private readonly Dictionary<int, byte> colors = new Dictionary<int, byte>();
        public bool TryGet(int player, out byte color) { return colors.TryGetValue(player, out color); }
        public void ForgetDeparted(IEnumerable<int> activePlayers)
        {
            var active = new HashSet<int>(activePlayers);
            var departed = new List<int>();
            foreach (var entry in colors) if (!active.Contains(entry.Key)) departed.Add(entry.Key);
            foreach (int player in departed) colors.Remove(player);
        }
        public bool Observe(int player, byte color, int paletteCount)
        {
            if (color >= paletteCount) return false;
            foreach (var entry in colors)
                if (entry.Key != player && entry.Value == color) return false;
            colors[player] = color;
            return true;
        }
        public bool TryReserve(int player, int paletteCount, int firstChoice, IEnumerable<int> activePlayers, out byte color)
        {
            if (paletteCount < 1 || paletteCount > 255) throw new ArgumentOutOfRangeException(nameof(paletteCount));
            var active = new HashSet<int>(activePlayers);
            ForgetDeparted(active);
            color = byte.MaxValue;
            if (!active.Contains(player)) return false;
            if (colors.TryGetValue(player, out color) && color < paletteCount) return true;
            int first = ((firstChoice % paletteCount) + paletteCount) % paletteCount;
            for (int offset = 0; offset < paletteCount; offset++)
            {
                byte candidate = (byte)((first + offset) % paletteCount);
                if (colors.ContainsValue(candidate)) continue;
                colors[player] = color = candidate;
                return true;
            }
            color = byte.MaxValue; // Never reuse a live player's color when the palette is full.
            return false;
        }
    }
}
