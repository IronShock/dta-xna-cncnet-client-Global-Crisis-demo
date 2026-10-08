using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.XNAUI;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// Generates a procedural "world map" texture for the campaign selector's 3D globe.
    /// The map consists of a star field, a bright ocean, a handful of simplified
    /// landmasses and white lines that crudely separate "countries" within each
    /// landmass. This is a stylized, abstract map with no real geographic precision.
    /// Colors are chosen to stay clearly visible on the rotating sphere.
    /// </summary>
    public static class WorldMapGenerator
    {
        private const int CONTINENT_COUNT = 7;
        private const int LAND_COLOR_SEED = 42;
        private const int STAR_SEED = 1337;

        /// <summary>
        /// Creates a new world map texture with the given dimensions.
        /// </summary>
        public static Texture2D Create(WindowManager wm, int width, int height)
        {
            var rng = new Random(STAR_SEED);
            GraphicsDevice gd = wm.GraphicsDevice;

            var renderTarget = new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            gd.SetRenderTarget(renderTarget);
            gd.Clear(Color.Black);

            using (var sb = new SpriteBatch(gd))
            {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                Texture2D pixel = AssetLoader.CreateTexture(Color.White, 1, 1);

                DrawBackground(sb, pixel, width, height);
                DrawGraticule(sb, pixel, width, height);
                DrawStars(sb, pixel, width, height, rng);
                DrawContinents(sb, pixel, width, height);

                pixel.Dispose();
                sb.End();
            }

            gd.SetRenderTarget(null);
            return renderTarget;
        }

        private static void DrawBackground(SpriteBatch sb, Texture2D pixel, int w, int h)
        {
            const int bands = 48;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                int top = h * i / bands;
                int bottom = h * (i + 1) / bands;
                // Bright blue ocean, slightly deeper near the poles.
                var color = new Color(
                    (byte)(26 + 26 * t),
                    (byte)(88 + 62 * t),
                    (byte)(168 + 52 * t),
                    (byte)255);
                sb.Draw(pixel, new Rectangle(0, top, w, bottom - top), color);
            }
        }

        /// <summary>
        /// Draws a subtle latitude / longitude graticule to give the map structure.
        /// </summary>
        private static void DrawGraticule(SpriteBatch sb, Texture2D pixel, int w, int h)
        {
            var gridColor = new Color(255, 255, 255, 34);

            int lonLines = 12;
            for (int i = 1; i < lonLines; i++)
            {
                int x = w * i / lonLines;
                sb.Draw(pixel, new Rectangle(x, 0, 1, h), gridColor);
            }

            int latLines = 6;
            for (int i = 1; i < latLines; i++)
            {
                int y = h * i / latLines;
                sb.Draw(pixel, new Rectangle(0, y, w, 1), gridColor);
            }
        }

        private static void DrawStars(SpriteBatch sb, Texture2D pixel, int w, int h, Random rng)
        {
            int count = (int)(w * h / 2200.0);
            for (int i = 0; i < count; i++)
            {
                int x = rng.Next(w);
                int y = rng.Next(h);
                int size = rng.Next(1, 3);
                int alpha = rng.Next(70, 210);
                bool bluish = rng.Next(4) == 0;
                var color = bluish
                    ? new Color(190, 205, 255, alpha)
                    : new Color(255, 255, 255, alpha);
                sb.Draw(pixel, new Rectangle(x, y, size, size), color);
            }
        }

        private static void DrawContinents(SpriteBatch sb, Texture2D pixel, int w, int h)
        {
            var rng = new Random(LAND_COLOR_SEED);

            for (int i = 0; i < CONTINENT_COUNT; i++)
            {
                int centerX = (int)(w * (0.12 + 0.76 * rng.NextDouble()));
                int centerY = (int)(h * (0.12 + 0.76 * rng.NextDouble()));
                int radius = rng.Next(55, 120);

                List<Vector2> polygon = GenerateBlob(centerX, centerY, radius, rng);
                Vector2[] points = polygon.ToArray();

                Color landColor = GetLandColor(rng.Next(LAND_COLOR_SEED, LAND_COLOR_SEED + 100));

                // Fill the landmass.
                DrawPolygon(sb, pixel, points, landColor);

                // White coastline outline.
                DrawPolygonOutline(sb, pixel, points, 2, new Color(255, 255, 255, 255));

                // Crude "country" borders: white chords between vertices of the polygon.
                int borderCount = rng.Next(2, 5);
                for (int b = 0; b < borderCount; b++)
                {
                    Vector2 a = points[rng.Next(points.Length)];
                    Vector2 c = points[rng.Next(points.Length)];
                    if (a != c)
                        DrawLine(sb, pixel, a, c, 1, new Color(255, 255, 255, 220));
                }
            }
        }

        private static Color GetLandColor(int seed)
        {
            var rng = new Random(seed);
            // Green / tan land colors that stand out against the bright ocean.
            int shade = rng.Next(0, 3);
            return shade switch
            {
                0 => new Color(94, 168, 84, 255),
                1 => new Color(146, 162, 74, 255),
                _ => new Color(168, 138, 82, 255),
            };
        }

        /// <summary>
        /// Generates a closed, irregular polygon (a "blob") around a center point.
        /// </summary>
        private static List<Vector2> GenerateBlob(int cx, int cy, int radius, Random rng)
        {
            const int vertexCount = 16;
            var result = new List<Vector2>(vertexCount);

            for (int i = 0; i < vertexCount; i++)
            {
                double angle = (Math.PI * 2.0 * i) / vertexCount;
                double jitter = 0.65 + 0.75 * rng.NextDouble();
                float x = cx + (float)(Math.Cos(angle) * radius * jitter);
                float y = cy + (float)(Math.Sin(angle) * radius * jitter);
                result.Add(new Vector2(x, y));
            }

            return result;
        }

        private static void DrawPolygon(SpriteBatch sb, Texture2D pixel, Vector2[] points, Color color)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                DrawLine(sb, pixel, a, b, 1, color);
            }
        }

        private static void DrawPolygonOutline(SpriteBatch sb, Texture2D pixel, Vector2[] points, int thickness, Color color)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                DrawLine(sb, pixel, a, b, thickness, color);
            }
        }

        /// <summary>
        /// Draws a line segment between two points using a stretched pixel texture.
        /// </summary>
        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, int thickness, Color color)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length < 0.01f)
                return;

            float angle = (float)Math.Atan2(delta.Y, delta.X);
            sb.Draw(pixel, start, null, color, angle, Vector2.Zero,
                new Vector2(length, thickness), SpriteEffects.None, 0.0f);
        }
    }
}
