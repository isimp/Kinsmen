using System;

namespace Kinsmen
{
    /// <summary>
    /// Recolours pixels to bronze, keeping each pixel's brightness and transparency: the effect
    /// icon is the game's own Rested knot in bronze, as the game shows that knot in green for
    /// Immobilized. Brightness is taken the usual way (0.299 red, 0.587 green, 0.114 blue),
    /// lifted so the knot's deep red comes out a middle bronze, and laid on a scale from a dark
    /// bronze through bronze to a light one.
    /// </summary>
    internal static class Bronze
    {
        private static readonly float[] Dark = { 36f, 25f, 11f };
        private static readonly float[] Middle = { 200f, 140f, 60f };
        private static readonly float[] Light = { 250f, 175f, 75f };

        /// <summary>How much the brightness is lifted before it is laid on the scale.</summary>
        private const float Lift = 1.6f;

        /// <summary>Recolours a buffer of pixels in place, four bytes each: red, green, blue, alpha.</summary>
        public static void Recolour(byte[] pixels)
        {
            for (var i = 0; i + 3 < pixels.Length; i += 4)
            {
                var brightness = (0.299f * pixels[i] + 0.587f * pixels[i + 1] + 0.114f * pixels[i + 2]) * Lift;
                var v = Math.Min(255f, brightness) / 255f;
                for (var c = 0; c < 3; c++)
                {
                    var value = v < 0.5f
                        ? Dark[c] + (Middle[c] - Dark[c]) * (v / 0.5f)
                        : Middle[c] + (Light[c] - Middle[c]) * ((v - 0.5f) / 0.5f);
                    pixels[i + c] = (byte)Math.Round(value);
                }
            }
        }
    }
}
