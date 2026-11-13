using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Shared materials. The assets are created by CanaanSetup so their shaders ship in the build.</summary>
    public static class Mats
    {
        public static Material Lit, LitDouble, Wind, Grass, Emissive, Transparent, Sky, Water, ParticleAlpha, ParticleAdd, ParticleSmoke, Terrain, GroundMark;
        /// <summary>For models with real UVs (made in Blender); Lit and LitDouble project the textures on code-built meshes.</summary>
        public static Material SurfaceUV, SurfaceUVDouble, SurfaceUVWind, Cliff;
        static readonly Dictionary<int, Material> emissive = new Dictionary<int, Material>();
        static Texture2D softDot;

        public static void Load()
        {
            Lit = Get("Surface");
            LitDouble = Get("SurfaceDouble");
            SurfaceUV = Get("SurfaceUV");
            SurfaceUVDouble = Get("SurfaceUVDouble");
            SurfaceUVWind = Get("SurfaceUVWind");
            Cliff = Get("SurfaceCliff");
            Wind = Get("LitWind");
            Grass = Get("LitGrass");
            Emissive = Get("SurfaceEmissive");
            Transparent = Get("SurfaceTransparent");
            var colour = Resources.Load<Texture2DArray>("Surf/SurfColor");
            var detail = Resources.Load<Texture2DArray>("Surf/SurfDetail");
            if (colour == null || detail == null) Debug.LogError("Mats: surface texture arrays missing — run Tools/setup.sh");
            Shader.SetGlobalTexture("_SurfColor", colour);
            Shader.SetGlobalTexture("_SurfDetail", detail);
            Sky = Get("Sky");
            Water = Get("Water");
            GroundMark = Get("GroundMark");
            ParticleAlpha = new Material(Get("ParticleAlpha"));
            ParticleAdd = new Material(Get("ParticleAdd"));
            Terrain = Get("Terrain");
            ParticleAlpha.SetTexture("_BaseMap", SoftDot);
            ParticleAdd.SetTexture("_BaseMap", SoftDot);
            ParticleSmoke = new Material(Get("ParticleAlpha"));
            ParticleSmoke.SetTexture("_BaseMap", Puff);
        }

        static Material Get(string name)
        {
            var m = Resources.Load<Material>("Mat/" + name);
            if (m == null) Debug.LogError("Mats: missing material Mat/" + name + " — run Tools/setup.sh");
            return m;
        }

        /// <summary>A lit material that also glows with the given colour (lamps, windows, fire).</summary>
        public static Material Glow(Color c, float intensity)
        {
            int key = ((Color32)c).GetHashCode() ^ Mathf.RoundToInt(intensity * 16f) * 7919;
            if (!emissive.TryGetValue(key, out Material m))
            {
                m = new Material(Emissive);
                m.SetColor("_EmissionColor", c.linear * intensity);
                emissive[key] = m;
            }
            return m;
        }

        static Texture2D puff;

        /// <summary>A ragged puff for smoke and steam: a soft ball eaten into by noise, so a plume of them turned every which way reads as billows.</summary>
        public static Texture2D Puff
        {
            get
            {
                if (puff != null) return puff;
                const int n = 128;
                puff = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                        float dx = u * 2f - 1f, dy = v * 2f - 1f;
                        float f = Mathf.PerlinNoise(u * 3.1f + 7.3f, v * 3.1f + 2.9f) * 0.55f + Mathf.PerlinNoise(u * 7.4f + 1.1f, v * 7.4f + 5.7f) * 0.3f
                                  + Mathf.PerlinNoise(u * 16f + 3.3f, v * 16f + 9.1f) * 0.15f;
                        float a = Mathf.SmoothStep(1f, 0.2f, Mathf.Sqrt(dx * dx + dy * dy) + (f - 0.5f) * 0.75f);
                        a *= Mathf.Clamp01(1.15f - (dx * dx + dy * dy));          // never touches the edge of the sprite
                        byte shade = (byte)(215f + 40f * Mathf.Clamp01(f * 1.3f - dy * 0.25f));
                        px[y * n + x] = new Color32(shade, shade, shade, (byte)(Mathf.Clamp01(a * a * 1.15f) * 255f));
                    }
                puff.SetPixels32(px);
                puff.Apply(true, true);
                return puff;
            }
        }

        /// <summary>Round soft-edged sprite for particles.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int n = 64;
                softDot = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                softDot.SetPixels32(px);
                softDot.Apply(true, true);
                return softDot;
            }
        }
    }

    /// <summary>The game's palette (sRGB).</summary>
    public static class Pal
    {
        public static Color32 H(int rgb) { return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255); }

        public static Color32 Lerp(Color32 a, Color32 b, float t) { return Color32.Lerp(a, b, t); }

        public static Color32 Shade(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);
        }

        // wood and building
        public static readonly Color32 WoodNew = H(0xB08A5A), WoodOld = H(0x8A6B48), WoodDark = H(0x5E4630), WoodGrey = H(0x8C8377);
        public static readonly Color32 WoodRed = H(0x8E3B2C), WoodWhite = H(0xD9D2C0), WoodGreen = H(0x55694A), WoodBlue = H(0x5B7486);
        public static readonly Color32 WoodYellow = H(0xC9A857), Shingle = H(0x4A3F38), Tar = H(0x2E2A28), Canvas = H(0xD8CDB2);
        public static readonly Color32 Brick = H(0x8F4B38), Stone = H(0x8D877C), StoneDark = H(0x5F5B55), Plaster = H(0xCDBFA3);
        public static readonly Color32 Glass = H(0x33424A), GlassLit = H(0xF2C46B), Adobe = H(0xB98B63);
        public static readonly Color32 AdobeLight = H(0xCBAE8A), AdobePink = H(0xC0957A), Whitewash = H(0xDCD2BE);

        // metal
        public static readonly Color32 Iron = H(0x3A3B3F), Steel = H(0x7B8088), Brass = H(0xB8923E), GunBlue = H(0x2B2F38), Rust = H(0x7A4A2E);
        public static readonly Color32 Tin = H(0x9AA0A3), Gold = H(0xD9B441), Silver = H(0xC4C8CC);

        // ground and nature
        public static readonly Color32 Dirt = H(0x8B6B4A), DirtDark = H(0x6A5038), Sand = H(0xC2A878), Mud = H(0x57432F);
        public static readonly Color32 GrassDry = H(0xB5A45E), GrassGreen = H(0x7C8C45), GrassDark = H(0x55683A), Sage = H(0x8E9A7A);
        public static readonly Color32 Rock = H(0x8A7F72), RockRed = H(0xA1583C), RockDark = H(0x5C554E);
        public static readonly Color32 Bark = H(0x5B4A3A), BarkPale = H(0x9A8F7C), Leaf = H(0x6C8440), LeafDark = H(0x47602F), LeafDry = H(0xA39A4A);
        public static readonly Color32 Hay = H(0xD1B665), Bone = H(0xE4DCC6), Ash = H(0x4B4743), Ember = H(0xE8742C);

        // leather, cloth
        public static readonly Color32 Leather = H(0x6B4226), LeatherDark = H(0x3C2617), LeatherTan = H(0xA47445), Rope = H(0xB79E6D);
        public static readonly Color32 ClothWhite = H(0xD9D4C7), ClothCream = H(0xC9B994), ClothGrey = H(0x77746E), ClothBlack = H(0x25262A);
        public static readonly Color32 ClothRed = H(0x9A2F28), ClothBlue = H(0x3E5674), ClothNavy = H(0x283247), ClothGreen = H(0x4C6146);
        public static readonly Color32 ClothBrown = H(0x6D5137), ClothTan = H(0xA98F68), ClothYellow = H(0xC49C3F), ClothPurple = H(0x5C4460);
        public static readonly Color32 Denim = H(0x4A5E78), Wool = H(0x8B8170);

        // people
        public static readonly Color32[] Skin =
        {
            H(0xE0B594), H(0xD4A27F), H(0xC48E6C), H(0xA9744F), H(0x8A5A3B), H(0x6A4229)
        };
        public static readonly Color32[] Hair =
        {
            H(0x231A14), H(0x3B2A1D), H(0x5E4027), H(0x8A6238), H(0xB08C4E), H(0x8E8A84), H(0xCFCAC0), H(0x7A3A22)
        };
        public static readonly Color32 Eye = H(0x1C1612), Lip = H(0x9B5A4C);

        // game accent
        public static readonly Color32 Vermilion = H(0xC9412B), Parchment = H(0xE9DFC6), Ink = H(0x1E1A16);

        // animals
        public static readonly Color32 HorseBay = H(0x6B3F24), HorseChestnut = H(0x8A4A26), HorseBlack = H(0x1E1B1A), HorseGrey = H(0x9A9892);
        public static readonly Color32 HorsePalomino = H(0xC9A261), HorseDun = H(0xB08A5E), HorseWhite = H(0xDAD6CC), HorseRoan = H(0x8C6F66);
        public static readonly Color32 Hoof = H(0x2A2420), Antler = H(0xC9B48C), FurTan = H(0xB58A58), FurGrey = H(0x7F7A70), FurBrown = H(0x5E4632);
        public static readonly Color32 FurWhite = H(0xE2DDD0), Blood = H(0x6E1512);
    }
}
