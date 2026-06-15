using System.Collections.Generic;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Maps <see cref="PeopleColor"/> to bright, high-contrast pastel RGB values used by every
    /// material in the game. Centralising this means re-skinning the whole game is one edit.
    /// </summary>
    public static class ColorPalette
    {
        static readonly Dictionary<PeopleColor, Color> s_colors = new Dictionary<PeopleColor, Color>
        {
            { PeopleColor.Red,    new Color(0.96f, 0.36f, 0.40f) },
            { PeopleColor.Blue,   new Color(0.33f, 0.62f, 0.95f) },
            { PeopleColor.Green,  new Color(0.40f, 0.83f, 0.49f) },
            { PeopleColor.Yellow, new Color(0.99f, 0.83f, 0.33f) },
            { PeopleColor.Purple, new Color(0.69f, 0.51f, 0.93f) },
            { PeopleColor.Orange, new Color(0.99f, 0.60f, 0.30f) },
        };

        /// <summary>Grey "?" tint used while a hidden colour is concealed.</summary>
        public static readonly Color Hidden = new Color(0.55f, 0.55f, 0.58f);

        /// <summary>Neutral material colour for the runway / lane pads.</summary>
        public static readonly Color Neutral = new Color(0.86f, 0.88f, 0.92f);

        public static Color ToColor(this PeopleColor c)
            => s_colors.TryGetValue(c, out var col) ? col : Color.magenta;
    }
}
