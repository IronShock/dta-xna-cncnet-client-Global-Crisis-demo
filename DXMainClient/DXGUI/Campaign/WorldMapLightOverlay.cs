using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// Draws dynamic, smoothly pulsing light points over the world map background
    /// of the campaign selector. Lights are placed at the positions of major
    /// cities, projected with the same equirectangular mapping as the world map
    /// texture (visible longitude range -152.5 .. +152.5).
    /// </summary>
    public class WorldMapLightOverlay : XNAControl
    {
        // Projection of Resources/worldmap.png: full longitude range, and a
        // vertical range of roughly +60 .. -48 degrees of latitude (the map is
        // a 5:3 crop of the modder's square world map).
        private const double LON_MIN = -180.0;
        private const double LON_MAX = 180.0;
        private const double LAT_TOP = 60.0;
        private const double LAT_BOTTOM = -48.0;

        private readonly List<LightPoint> lightPoints = new();
        private Texture2D glowTexture;

        public WorldMapLightOverlay(WindowManager windowManager) : base(windowManager)
        {
            DrawOrder = -100;
        }

        private struct LightPoint
        {
            public float X;
            public float Y;
            public float Period;
            public float Phase;
            public float BaseAlpha;
            public int GlowSize;
        }

        private static void AddLight(List<LightPoint> list, float lat, float lon,
            float period, float phase, float baseAlpha, int glowSize)
        {
            float x = (float)((lon - LON_MIN) / (LON_MAX - LON_MIN));
            float y = (float)((LAT_TOP - lat) / (LAT_TOP - LAT_BOTTOM));

            if (x < 0f || x > 1f || y < 0f || y > 1f)
                return;

            list.Add(new LightPoint
            {
                X = x,
                Y = y,
                Period = period,
                Phase = phase,
                BaseAlpha = baseAlpha,
                GlowSize = glowSize
            });
        }

        public override void Initialize()
        {
            // Major city locations (latitude, longitude), with varied blink
            // periods and phase offsets so the lights twinkle out of sync.
            AddLight(lightPoints, 47.6f, -122.3f, 4.2f, 0.0f, 0.9f, 44); // Seattle
            AddLight(lightPoints, 45.5f, -122.7f, 4.2f, 1.1f, 0.7f, 32); // Portland
            AddLight(lightPoints, 37.8f, -122.4f, 3.4f, 2.3f, 0.9f, 40); // San Francisco
            AddLight(lightPoints, 34.1f, -118.2f, 5.1f, 4.0f, 0.9f, 46); // Los Angeles
            AddLight(lightPoints, 33.7f, -84.4f, 4.8f, 1.7f, 0.7f, 34);  // Atlanta
            AddLight(lightPoints, 38.9f, -77.0f, 3.8f, 3.2f, 1.0f, 48);  // Washington
            AddLight(lightPoints, 40.7f, -74.0f, 4.4f, 0.8f, 0.9f, 44);  // New York
            AddLight(lightPoints, 43.7f, -79.4f, 5.6f, 2.9f, 0.8f, 40);  // Toronto
            AddLight(lightPoints, 45.4f, -75.7f, 6.1f, 1.4f, 0.7f, 36);  // Ottawa
            AddLight(lightPoints, 19.4f, -99.1f, 4.9f, 0.3f, 0.8f, 40);  // Mexico City
            AddLight(lightPoints, 9.0f, -79.5f, 5.3f, 3.7f, 0.6f, 30);   // Panama City
            AddLight(lightPoints, 10.5f, -66.9f, 4.1f, 1.9f, 0.7f, 34);  // Caracas
            AddLight(lightPoints, -23.6f, -46.6f, 3.7f, 5.0f, 0.8f, 42); // Sao Paulo
            AddLight(lightPoints, -15.8f, -47.9f, 5.8f, 2.1f, 0.8f, 38); // Brasilia
            AddLight(lightPoints, -34.6f, -58.4f, 4.6f, 0.6f, 0.8f, 40); // Buenos Aires
            AddLight(lightPoints, -33.4f, -70.7f, 6.4f, 3.9f, 0.7f, 36); // Santiago
            AddLight(lightPoints, 51.5f, -0.1f, 3.6f, 1.2f, 1.0f, 50);   // London
            AddLight(lightPoints, 48.9f, 2.35f, 4.5f, 4.4f, 0.9f, 44);   // Paris
            AddLight(lightPoints, 40.4f, -3.7f, 5.2f, 2.6f, 0.8f, 40);   // Madrid
            AddLight(lightPoints, 41.9f, 12.5f, 4.3f, 0.9f, 0.8f, 40);   // Rome
            AddLight(lightPoints, 48.1f, 11.6f, 5.7f, 3.1f, 0.7f, 36);   // Munich
            AddLight(lightPoints, 52.5f, 13.4f, 4.0f, 5.3f, 0.9f, 44);   // Berlin
            AddLight(lightPoints, 59.9f, 10.75f, 6.3f, 1.6f, 0.7f, 34); // Oslo
            AddLight(lightPoints, 60.2f, 24.9f, 4.7f, 3.4f, 0.6f, 32);   // Helsinki
            AddLight(lightPoints, 59.3f, 18.1f, 5.9f, 0.4f, 0.7f, 34);   // Stockholm
            AddLight(lightPoints, 55.75f, 37.6f, 3.4f, 2.0f, 1.0f, 50);  // Moscow
            AddLight(lightPoints, 50.0f, 36.2f, 5.5f, 4.8f, 0.6f, 32);   // Kharkiv
            AddLight(lightPoints, 41.0f, 28.9f, 4.4f, 1.8f, 0.8f, 40);   // Istanbul
            AddLight(lightPoints, 35.7f, 51.4f, 5.0f, 3.6f, 0.7f, 34);   // Tehran
            AddLight(lightPoints, 24.7f, 46.7f, 4.2f, 0.7f, 0.7f, 34);   // Riyadh
            AddLight(lightPoints, 30.0f, 31.2f, 4.6f, 2.4f, 0.8f, 38);   // Cairo
            AddLight(lightPoints, 32.8f, 13.0f, 6.0f, 4.2f, 0.6f, 32);   // Tripoli
            AddLight(lightPoints, 6.5f, 3.4f, 5.4f, 1.0f, 0.7f, 36);     // Lagos
            AddLight(lightPoints, -26.2f, 28.0f, 3.9f, 3.8f, 0.7f, 36);  // Johannesburg
            AddLight(lightPoints, 39.9f, 116.4f, 3.8f, 0.5f, 1.0f, 48);  // Beijing
            AddLight(lightPoints, 31.2f, 121.5f, 4.9f, 2.8f, 0.9f, 44);  // Shanghai
            AddLight(lightPoints, 22.3f, 114.2f, 5.3f, 4.6f, 0.8f, 40);  // Hong Kong
            AddLight(lightPoints, 23.1f, 113.3f, 6.2f, 1.3f, 0.6f, 30);  // Guangzhou
            AddLight(lightPoints, 30.6f, 104.1f, 4.1f, 5.5f, 0.6f, 32);  // Chengdu
            AddLight(lightPoints, 34.3f, 108.9f, 5.6f, 2.2f, 0.6f, 32);  // Xi'an
            AddLight(lightPoints, 37.6f, 127.0f, 4.5f, 3.0f, 0.8f, 38);  // Seoul
            AddLight(lightPoints, 35.7f, 139.7f, 3.6f, 1.5f, 1.0f, 48);  // Tokyo
            AddLight(lightPoints, 34.7f, 135.5f, 5.8f, 4.4f, 0.7f, 36);  // Osaka
            AddLight(lightPoints, 21.0f, 105.8f, 6.5f, 0.2f, 0.5f, 30);  // Hanoi
            AddLight(lightPoints, 13.7f, 100.5f, 4.8f, 2.7f, 0.7f, 36);  // Bangkok
            AddLight(lightPoints, 1.35f, 103.8f, 5.2f, 4.1f, 0.8f, 40);  // Singapore
            AddLight(lightPoints, -6.2f, 106.8f, 4.3f, 0.9f, 0.8f, 40);  // Jakarta
            AddLight(lightPoints, 14.6f, 121.0f, 5.7f, 3.3f, 0.7f, 36);  // Manila
            AddLight(lightPoints, 28.6f, 77.2f, 4.7f, 2.0f, 0.9f, 42);   // New Delhi
            AddLight(lightPoints, 18.9f, 72.8f, 5.5f, 4.7f, 0.8f, 40);   // Mumbai
            AddLight(lightPoints, 24.9f, 67.0f, 6.0f, 1.1f, 0.6f, 32);   // Karachi
            AddLight(lightPoints, -33.9f, 151.2f, 4.0f, 3.5f, 0.8f, 42); // Sydney
            AddLight(lightPoints, -35.3f, 149.1f, 5.4f, 1.9f, 0.7f, 36); // Canberra
            AddLight(lightPoints, -37.8f, 144.9f, 6.2f, 4.3f, 0.7f, 36); // Melbourne
            AddLight(lightPoints, -33.9f, 18.4f, 5.1f, 2.5f, 0.7f, 36);  // Cape Town

            for (int i = 0; i < lightPoints.Count; i++)
            {
                LightPoint lp = lightPoints[i];
                lp.X *= Width;
                lp.Y *= Height;
                lightPoints[i] = lp;
            }

            glowTexture = CreateGlowTexture(WindowManager.GraphicsDevice);

            base.Initialize();
        }

        /// <summary>
        /// Creates a soft radial glow texture used to draw the light points.
        /// </summary>
        private static Texture2D CreateGlowTexture(GraphicsDevice gd)
        {
            const int size = 64;
            var texture = new Texture2D(gd, size, size, false, SurfaceFormat.Color);
            var data = new Color[size * size];

            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = MathHelper.Clamp(1.0f - dist, 0.0f, 1.0f);
                    alpha = alpha * alpha; // quadratic falloff for a soft glow
                    data[y * size + x] = Color.White * alpha;
                }
            }

            texture.SetData(data);
            return texture;
        }

        public override void Draw(GameTime gameTime)
        {
            float t = (float)gameTime.TotalGameTime.TotalSeconds;

            foreach (LightPoint lp in lightPoints)
            {
                // Smooth breathing pulse, phase-shifted per light.
                float phase = MathHelper.TwoPi * t / lp.Period + lp.Phase;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(phase);
                float alpha = lp.BaseAlpha * (0.12f + 0.88f * pulse);

                int glowSize = lp.GlowSize;
                DrawTexture(glowTexture,
                    new Rectangle((int)lp.X - glowSize / 2, (int)lp.Y - glowSize / 2, glowSize, glowSize),
                    Color.White * alpha);

                int coreSize = glowSize / 3;
                if (coreSize > 2)
                {
                    DrawTexture(glowTexture,
                        new Rectangle((int)lp.X - coreSize / 2, (int)lp.Y - coreSize / 2, coreSize, coreSize),
                        Color.White * Math.Min(1.0f, alpha * 1.6f));
                }
            }

            base.Draw(gameTime);
        }
    }
}
