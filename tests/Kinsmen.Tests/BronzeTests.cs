using Xunit;

namespace Kinsmen.Tests
{
    /// <summary>
    /// The effect icon is the game's own Rested knot recoloured to bronze, the way the game
    /// recolours that knot for Immobilized: each pixel keeps its shading and its transparency, and
    /// takes a bronze of the same brightness.
    /// </summary>
    public class BronzeTests
    {
        private static byte[] Pixel(byte r, byte g, byte b, byte a) => new[] { r, g, b, a };

        [Fact]
        public void TheRestedRedBecomesAWarmBronze()
        {
            var pixel = Pixel(220, 20, 50, 255);
            Bronze.Recolour(pixel);
            Assert.True(pixel[0] > pixel[1] && pixel[1] > pixel[2], $"{pixel[0]},{pixel[1]},{pixel[2]} is not a bronze");
            Assert.True(pixel[0] >= 150, "too dark for the HUD: red " + pixel[0]);
        }

        [Fact]
        public void TransparencyStaysAsItWas()
        {
            var pixels = new byte[] { 220, 20, 50, 0, 220, 20, 50, 128, 220, 20, 50, 255 };
            Bronze.Recolour(pixels);
            Assert.Equal(0, pixels[3]);
            Assert.Equal(128, pixels[7]);
            Assert.Equal(255, pixels[11]);
        }

        [Fact]
        public void ShadingIsKeptDarkStaysDarkerThanLight()
        {
            var dark = Pixel(90, 5, 20, 255);
            var light = Pixel(255, 60, 90, 255);
            Bronze.Recolour(dark);
            Bronze.Recolour(light);
            Assert.True(dark[0] < light[0] && dark[1] < light[1] && dark[2] < light[2]);
        }

        [Fact]
        public void BlackAndWhiteStayInTheBronzeRange()
        {
            var black = Pixel(0, 0, 0, 255);
            var white = Pixel(255, 255, 255, 255);
            Bronze.Recolour(black);
            Bronze.Recolour(white);
            Assert.True(black[0] > black[2] && white[0] > white[2], "both keep a warm cast");
            Assert.True(black[0] < 60 && white[0] > 200);
        }

        [Fact]
        public void EveryPixelOfABufferIsRecoloured()
        {
            var pixels = new byte[4 * 64 * 64];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 220;
                pixels[i + 1] = 20;
                pixels[i + 2] = 50;
                pixels[i + 3] = 255;
            }
            Bronze.Recolour(pixels);
            for (var i = 0; i < pixels.Length; i += 4) Assert.True(pixels[i] > pixels[i + 2], "pixel " + i / 4 + " left red");
        }
    }
}
