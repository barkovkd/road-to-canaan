using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// Decides which surface texture a colour gets when a mesh is built from code. Palette colours carry their
    /// material (wood paints are planks, cloth dyes are cloth); a builder that knows better registers a hint
    /// for its own colours (a brown that is a waistcoat, not a board) or sets the surface explicitly.
    /// </summary>
    public static class SurfaceMap
    {
        struct Entry { public Vector3 dir; public float lum; public Surf surf; }
        static readonly List<Entry> entries = new List<Entry>();
        static readonly Dictionary<int, Surf> exact = new Dictionary<int, Surf>();
        static readonly Dictionary<int, Surf> hints = new Dictionary<int, Surf>();
        static readonly Dictionary<int, Surf> guessed = new Dictionary<int, Surf>();

        static int Key(Color32 c) { return c.r << 16 | c.g << 8 | c.b; }

        static void Add(Surf s, params Color32[] colours)
        {
            foreach (Color32 c in colours)
            {
                if (!exact.ContainsKey(Key(c))) exact[Key(c)] = s;
                var v = new Vector3(c.r, c.g, c.b);
                entries.Add(new Entry { dir = v.normalized, lum = v.magnitude / 441.7f, surf = s });
            }
        }

        static SurfaceMap()
        {
            Add(Surf.Planks, Pal.WoodNew, Pal.WoodOld, Pal.WoodDark, Pal.WoodGrey, Pal.WoodRed, Pal.WoodWhite, Pal.WoodGreen, Pal.WoodBlue, Pal.WoodYellow);
            Add(Surf.Shingles, Pal.Shingle, Pal.Tar);
            Add(Surf.Canvas, Pal.Canvas);
            Add(Surf.Brick, Pal.Brick);
            Add(Surf.Stone, Pal.Stone, Pal.StoneDark);
            Add(Surf.Plaster, Pal.Plaster, Pal.Adobe, Pal.AdobeLight, Pal.AdobePink, Pal.Whitewash);
            Add(Surf.Iron, Pal.Iron, Pal.Rust, Pal.GunBlue);
            Add(Surf.Steel, Pal.Steel, Pal.Brass, Pal.Gold, Pal.Silver);
            Add(Surf.Tin, Pal.Tin);
            Add(Surf.Dirt, Pal.Dirt, Pal.DirtDark, Pal.Sand, Pal.Mud, Pal.Ash);
            Add(Surf.Straw, Pal.GrassDry, Pal.Hay, Pal.Rope);
            Add(Surf.Foliage, Pal.GrassGreen, Pal.GrassDark, Pal.Sage, Pal.Leaf, Pal.LeafDark, Pal.LeafDry);
            Add(Surf.Rock, Pal.Rock, Pal.RockRed, Pal.RockDark);
            Add(Surf.Bark, Pal.Bark, Pal.BarkPale);
            Add(Surf.Leather, Pal.Leather, Pal.LeatherDark, Pal.LeatherTan);
            Add(Surf.Cloth, Pal.ClothWhite, Pal.ClothCream, Pal.ClothGrey, Pal.ClothBlack, Pal.ClothRed, Pal.ClothBlue, Pal.ClothNavy,
                Pal.ClothGreen, Pal.ClothBrown, Pal.ClothTan, Pal.ClothYellow, Pal.ClothPurple);
            Add(Surf.Denim, Pal.Denim);
            Add(Surf.Wool, Pal.Wool);
            Add(Surf.Skin, Pal.Skin);
            Add(Surf.Hair, Pal.Hair);
            Add(Surf.Fur, Pal.HorseBay, Pal.HorseChestnut, Pal.HorseBlack, Pal.HorseGrey, Pal.HorsePalomino, Pal.HorseDun, Pal.HorseWhite,
                Pal.HorseRoan, Pal.FurTan, Pal.FurGrey, Pal.FurBrown, Pal.FurWhite);
            Add(Surf.Plain, Pal.Glass, Pal.GlassLit, Pal.Bone, Pal.Eye, Pal.Lip, Pal.Hoof, Pal.Antler, Pal.Blood, Pal.Ember,
                Pal.Vermilion, Pal.Parchment, Pal.Ink);
        }

        /// <summary>Says what a colour is made of for whatever is built next (a spec's shirt, a horse's coat).</summary>
        public static void Hint(Color32 c, Surf s) { hints[Key(c)] = s; }

        public static void ClearHints() { hints.Clear(); }

        public static Surf Of(Color32 c)
        {
            int key = Key(c);
            if (hints.TryGetValue(key, out Surf s)) return s;
            if (exact.TryGetValue(key, out s)) return s;
            if (guessed.TryGetValue(key, out s)) return s;
            // a shade or blend of palette colours: the nearest hue, with brightness as a tie-breaker
            var v = new Vector3(c.r, c.g, c.b);
            Vector3 dir = v.sqrMagnitude > 1f ? v.normalized : new Vector3(0.577f, 0.577f, 0.577f);
            float lum = v.magnitude / 441.7f, best = float.MaxValue;
            s = Surf.Plain;
            foreach (Entry e in entries)
            {
                float d = (e.dir - dir).sqrMagnitude + 0.08f * (e.lum - lum) * (e.lum - lum);
                if (d < best) { best = d; s = e.surf; }
            }
            guessed[key] = s;
            return s;
        }
    }
}
