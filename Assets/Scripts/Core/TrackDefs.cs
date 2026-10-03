using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    public enum Theme { Candy, Corrupted, Colorful, Rainbow }

    public class TrackDef
    {
        public string name;
        public Theme theme;
        public int region, index, seed, difficulty; // difficulty 0..4 inside a region
        public float radius, width, stretch;
        public string blurb;
        public int Id => region * 5 + index;
    }

    public class ThemePalette
    {
        public string name, tagline;
        public Color ground, groundB, sky, fog, road, curbA, curbB, fence, sun, accent, ambient;
        public float fogDensity;
    }

    /// <summary>The island: four regions, five tracks each. Every track is generated from a seed, so
    /// adding or reshaping tracks is just data.</summary>
    public static class TrackDefs
    {
        public static readonly List<TrackDef> All = new List<TrackDef>();
        public static readonly ThemePalette[] Palettes = new ThemePalette[4];

        private static readonly string[][] Names =
        {
            new[] { "Sprinkle Speedway", "Gumdrop Gardens", "Frosting Falls", "Licorice Loop", "Cocoa Canyon" },
            new[] { "Glitch Gorge", "Static Strait", "Void Valley", "Crimson Crash", "Null Nights" },
            new[] { "Paint Palace", "Confetti Cove", "Balloon Boulevard", "Mosaic Meadows", "Crayon Circuit" },
            new[] { "Prism Park", "Aurora Alley", "Spectrum Summit", "Halo Highway", "Starbow Grand Prix" },
        };

        static TrackDefs()
        {
            Palettes[0] = new ThemePalette
            {
                name = "Candy Coast", tagline = "Sweet shortcuts and sugar-rush corners",
                ground = new Color(1f, 0.76f, 0.86f), groundB = new Color(0.98f, 0.6f, 0.78f),
                sky = new Color(0.72f, 0.9f, 1f), fog = new Color(0.95f, 0.85f, 0.95f), road = new Color(0.45f, 0.3f, 0.25f),
                curbA = new Color(1f, 0.3f, 0.4f), curbB = Color.white, fence = new Color(1f, 0.95f, 0.7f),
                sun = new Color(1f, 0.95f, 0.85f), accent = new Color(1f, 0.4f, 0.6f), ambient = new Color(0.85f, 0.75f, 0.85f), fogDensity = 0.0035f,
            };
            Palettes[1] = new ThemePalette
            {
                name = "Corrupted Core", tagline = "Reality is glitching. Don't blink",
                ground = new Color(0.1f, 0.05f, 0.16f), groundB = new Color(0.2f, 0.05f, 0.25f),
                sky = new Color(0.08f, 0.02f, 0.12f), fog = new Color(0.18f, 0.03f, 0.2f), road = new Color(0.08f, 0.08f, 0.1f),
                curbA = new Color(1f, 0.1f, 0.6f), curbB = new Color(0.1f, 1f, 0.9f), fence = new Color(0.9f, 0.1f, 0.4f),
                sun = new Color(0.7f, 0.4f, 1f), accent = new Color(1f, 0.1f, 0.6f), ambient = new Color(0.35f, 0.2f, 0.5f), fogDensity = 0.007f,
            };
            Palettes[2] = new ThemePalette
            {
                name = "Color Coves", tagline = "Paint, balloons and pure chaos",
                ground = new Color(0.45f, 0.9f, 0.45f), groundB = new Color(0.25f, 0.75f, 0.9f), sky = new Color(0.55f, 0.85f, 1f),
                fog = new Color(0.85f, 0.95f, 1f), road = new Color(0.3f, 0.3f, 0.38f),
                curbA = new Color(1f, 0.85f, 0.1f), curbB = new Color(0.2f, 0.4f, 1f), fence = new Color(1f, 0.5f, 0.15f),
                sun = new Color(1f, 1f, 0.9f), accent = new Color(1f, 0.6f, 0.1f), ambient = new Color(0.8f, 0.85f, 0.9f), fogDensity = 0.0025f,
            };
            Palettes[3] = new ThemePalette
            {
                name = "Rainbow Reach", tagline = "The sky road. Don't look down",
                ground = new Color(0.04f, 0.04f, 0.2f), groundB = new Color(0.1f, 0.05f, 0.3f), sky = new Color(0.03f, 0.03f, 0.15f),
                fog = new Color(0.1f, 0.08f, 0.3f), road = new Color(0.1f, 0.1f, 0.2f),
                curbA = Color.white, curbB = new Color(0.6f, 0.5f, 1f), fence = new Color(0.7f, 0.9f, 1f),
                sun = new Color(0.9f, 0.9f, 1f), accent = new Color(0.4f, 0.9f, 1f), ambient = new Color(0.5f, 0.5f, 0.8f), fogDensity = 0.0018f,
            };

            for (int r = 0; r < 4; r++)
            {
                for (int i = 0; i < 5; i++)
                {
                    All.Add(new TrackDef
                    {
                        name = Names[r][i], theme = (Theme)r, region = r, index = i, difficulty = i,
                        seed = 1000 + r * 77 + i * 13,
                        radius = 110f + i * 18f + (r % 2) * 14f,
                        width = 17f - i * 0.8f,
                        stretch = 1f + 0.12f * ((i + r) % 4),
                        blurb = "Track " + (i + 1) + " of 5",
                    });
                }
            }
        }

        public static TrackDef Get(int region, int index) => All[region * 5 + index];
    }
}
