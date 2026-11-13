using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Quadruped bones. Legs: F/H = front/hind, L/R, a/b/c = upper, lower, cannon with hoof.</summary>
    public enum QB
    {
        Pelvis, Spine, Chest, Neck, Head, Tail1, Tail2,
        FLa, FLb, FLc, FRa, FRb, FRc, HLa, HLb, HLc, HRa, HRb, HRc,
        Count
    }

    public enum HornKind { None, Cow, Bison, Pronghorn, Antlers }

    /// <summary>Proportions and colours of a four-legged animal, as multipliers on a horse-shaped template.</summary>
    [Serializable]
    public class QuadSpec
    {
        public string kind = "horse";
        public float scale = 1f;          // uniform; 1 = horse with withers at 1.55 m
        public float legLen = 1f, legThick = 1f;
        public float bodyLen = 1f, bodyW = 1f, bodyD = 1f;
        public float neckLen = 1f, neckAngle = 53f, neckThick = 1f;
        public float headLen = 1f, headSize = 1f, headDrop = 42f;   // head pitch below horizontal
        public float tailLen = 1f, tailThick = 1f, tailAngle = 12f; // degrees back from straight down
        public float earLen = 1f, hump = 0f, ruff = 0f;
        public bool mane = true, pads;                              // pads: paws instead of hooves
        public HornKind horns = HornKind.None;
        public Color32 coat = Pal.HorseBay, belly = Pal.HorseBay, maneColor = Pal.HorseBlack, legColor = Pal.HorseBlack;
        public Color32 muzzle = Pal.HorseBlack, hoof = Pal.Hoof, rump = Pal.HorseBay;
        public bool blaze, socks;

        public QuadSpec Clone() { return (QuadSpec)MemberwiseClone(); }

        public static QuadSpec Horse(int coatIndex)
        {
            var s = new QuadSpec();
            switch (coatIndex % 7)
            {
                case 0: s.coat = Pal.HorseBay; s.maneColor = Pal.HorseBlack; s.legColor = Pal.HorseBlack; s.muzzle = Pal.HorseBlack; break;
                case 1: s.coat = Pal.HorseChestnut; s.maneColor = Pal.Shade(Pal.HorseChestnut, 0.8f); s.legColor = s.coat; s.muzzle = Pal.Shade(s.coat, 0.7f); s.blaze = true; s.socks = true; break;
                case 2: s.coat = Pal.HorseBlack; s.maneColor = Pal.HorseBlack; s.legColor = Pal.HorseBlack; s.muzzle = Pal.HorseBlack; break;
                case 3: s.coat = Pal.HorseGrey; s.maneColor = Pal.HorseWhite; s.legColor = Pal.Shade(Pal.HorseGrey, 0.7f); s.muzzle = Pal.Shade(Pal.HorseGrey, 0.55f); break;
                case 4: s.coat = Pal.HorsePalomino; s.maneColor = Pal.HorseWhite; s.legColor = s.coat; s.muzzle = Pal.Shade(s.coat, 0.75f); s.socks = true; break;
                case 5: s.coat = Pal.HorseDun; s.maneColor = Pal.HorseBlack; s.legColor = Pal.Shade(Pal.HorseBlack, 1.4f); s.muzzle = Pal.HorseBlack; break;
                default: s.coat = Pal.HorseRoan; s.maneColor = Pal.Shade(Pal.HorseRoan, 0.6f); s.legColor = Pal.Shade(Pal.HorseRoan, 0.6f); s.muzzle = Pal.Shade(Pal.HorseRoan, 0.6f); s.rump = Pal.HorseWhite; s.blaze = true; break;
            }
            if (coatIndex % 7 != 6) s.rump = s.coat;
            s.belly = s.coat;
            return s;
        }

        public static QuadSpec Ox(bool white = false)
        {
            Color32 c = white ? Pal.H(0xCFC6B4) : Pal.H(0x7A4B2E);
            return new QuadSpec
            {
                kind = "ox", scale = 1.0f, legLen = 0.80f, legThick = 1.25f, bodyLen = 1.05f, bodyW = 1.25f, bodyD = 1.2f,
                neckLen = 0.55f, neckAngle = 12f, neckThick = 1.45f, headLen = 0.8f, headSize = 1.32f, headDrop = 58f,
                tailLen = 1.0f, tailThick = 0.35f, tailAngle = 4f, earLen = 0.9f, mane = false, horns = HornKind.Cow,
                coat = c, belly = c, rump = c, legColor = Pal.Shade(c, 0.9f), maneColor = Pal.Shade(c, 0.6f), muzzle = Pal.H(0x3A2C26)
            };
        }

        public static QuadSpec Cow(int variant)
        {
            QuadSpec s = Ox(variant % 3 == 1);
            s.kind = "cow"; s.scale = 0.92f; s.legThick = 1.1f; s.bodyW = 1.15f;
            if (variant % 3 == 2) { s.coat = Pal.H(0x2B2522); s.belly = s.coat; s.rump = Pal.H(0xD8D0C0); s.legColor = s.coat; }
            return s;
        }

        public static QuadSpec Bison()
        {
            Color32 c = Pal.H(0x4A3526);
            return new QuadSpec
            {
                kind = "bison", scale = 1.05f, legLen = 0.80f, legThick = 1.3f, bodyLen = 1.05f, bodyW = 1.25f, bodyD = 1.2f,
                neckLen = 0.55f, neckAngle = -8f, neckThick = 1.7f, headLen = 0.9f, headSize = 1.45f, headDrop = 70f,
                tailLen = 0.6f, tailThick = 0.4f, tailAngle = 6f, earLen = 0.5f, hump = 1f, ruff = 1f, mane = false, horns = HornKind.Bison,
                coat = c, belly = Pal.Shade(c, 0.85f), rump = Pal.Shade(c, 1.25f), legColor = Pal.Shade(c, 0.7f), maneColor = Pal.H(0x2A1E17), muzzle = Pal.H(0x1E1714)
            };
        }

        public static QuadSpec Pronghorn(bool buck)
        {
            Color32 c = Pal.H(0xB98652);
            return new QuadSpec
            {
                kind = "pronghorn", scale = 0.60f, legLen = 1.12f, legThick = 0.6f, bodyLen = 0.9f, bodyW = 0.82f, bodyD = 0.86f,
                neckLen = 0.82f, neckAngle = 66f, neckThick = 0.75f, headLen = 0.72f, headSize = 0.86f, headDrop = 28f,
                tailLen = 0.16f, tailThick = 0.9f, tailAngle = 30f, earLen = 1.1f, mane = false, horns = buck ? HornKind.Pronghorn : HornKind.None,
                coat = c, belly = Pal.FurWhite, rump = Pal.FurWhite, legColor = Pal.H(0xC9A375), maneColor = Pal.FurWhite, muzzle = Pal.H(0x2A211C)
            };
        }

        public static QuadSpec Deer(bool buck)
        {
            Color32 c = Pal.H(0x8E6540);
            return new QuadSpec
            {
                kind = "deer", scale = buck ? 0.68f : 0.62f, legLen = 1.1f, legThick = 0.6f, bodyLen = 0.92f, bodyW = 0.84f, bodyD = 0.88f,
                neckLen = 0.9f, neckAngle = 62f, neckThick = 0.78f, headLen = 0.74f, headSize = 0.84f, headDrop = 30f,
                tailLen = 0.22f, tailThick = 1.0f, tailAngle = 25f, earLen = 1.5f, mane = false, horns = buck ? HornKind.Antlers : HornKind.None,
                coat = c, belly = Pal.H(0xD9CDB8), rump = c, legColor = Pal.Shade(c, 0.9f), maneColor = Pal.FurWhite, muzzle = Pal.H(0x2A211C)
            };
        }

        public static QuadSpec Canine(bool wolf)
        {
            Color32 c = wolf ? Pal.H(0x77736B) : Pal.H(0xA58A62);
            return new QuadSpec
            {
                kind = wolf ? "wolf" : "coyote", scale = wolf ? 0.50f : 0.38f, legLen = 0.92f, legThick = 0.8f, bodyLen = 0.92f, bodyW = 0.72f, bodyD = 0.74f,
                neckLen = 0.5f, neckAngle = 22f, neckThick = 1.15f, headLen = 0.68f, headSize = 1.1f, headDrop = 14f,
                tailLen = 0.72f, tailThick = 1.25f, tailAngle = 40f, earLen = 1.25f, ruff = wolf ? 0.5f : 0.25f, mane = false, pads = true,
                coat = c, belly = Pal.Shade(c, 1.25f), rump = c, legColor = Pal.Shade(c, 1.1f), maneColor = Pal.Shade(c, 0.6f), muzzle = Pal.H(0x1C1815), hoof = Pal.Shade(c, 0.8f)
            };
        }

        public static QuadSpec Rabbit()
        {
            Color32 c = Pal.H(0x9A8468);
            return new QuadSpec
            {
                kind = "rabbit", scale = 0.17f, legLen = 0.50f, legThick = 1.1f, bodyLen = 0.62f, bodyW = 0.95f, bodyD = 0.92f,
                neckLen = 0.28f, neckAngle = 35f, neckThick = 1.5f, headLen = 0.5f, headSize = 1.6f, headDrop = 12f,
                tailLen = 0.1f, tailThick = 1.6f, tailAngle = 60f, earLen = 5.5f, mane = false, pads = true,
                coat = c, belly = Pal.FurWhite, rump = c, legColor = c, maneColor = Pal.FurWhite, muzzle = Pal.H(0x4A3A30), hoof = c
            };
        }
    }

    /// <summary>A built quadruped with the measurements the gait animator needs.</summary>
    public class QuadRig
    {
        public GameObject go;
        public Transform root;
        public Transform[] bones;
        public Vector3[] bind, bindLocal;
        public SkinnedMeshRenderer smr;
        public QuadSpec spec;
        public float k;                       // overall scale
        public Vector3[] hoofRest = new Vector3[4];   // FL, FR, HL, HR in model space
        public float[] lenA = new float[4], lenB = new float[4], lenC = new float[4];
        public Vector3 seat;                  // top of the back where a saddle sits, model space
        public float withers, backLength;
        public Transform saddle, mouth;
        public GameObject tack;

        public Transform B(QB b) { return bones[(int)b]; }
        public Vector3 Bind(QB b) { return bind[(int)b]; }
        public Transform Leg(int leg, int seg) { return bones[(int)QB.FLa + leg * 3 + seg]; }
        public Vector3 LegBind(int leg, int seg) { return bind[(int)QB.FLa + leg * 3 + seg]; }

        public void ResetPose()
        {
            for (int i = 0; i < bones.Length; i++) bones[i].SetLocalPositionAndRotation(bindLocal[i], Quaternion.identity);
        }
    }

    /// <summary>Builds horses and other four-legged animals from one parametric template.</summary>
    public static class QuadBuilder
    {
        static readonly QB[] Parent =
        {
            QB.Count, QB.Pelvis, QB.Spine, QB.Chest, QB.Neck, QB.Pelvis, QB.Tail1,
            QB.Chest, QB.FLa, QB.FLb, QB.Chest, QB.FRa, QB.FRb,
            QB.Pelvis, QB.HLa, QB.HLb, QB.Pelvis, QB.HRa, QB.HRb
        };

        static BoneWeight W(QB a) { return MeshBuilder.W((int)a); }
        static BoneWeight W(QB a, float wa, QB b) { return MeshBuilder.W((int)a, wa, (int)b); }

        static Vector3[] reference;
        static bool buildingReference;

        /// <summary>Joints of the default horse, unscaled: what the Blender model was built on.</summary>
        static Vector3[] ReferenceJoints()
        {
            if (reference != null) return reference;
            buildingReference = true;
            QuadRig r = Build(new QuadSpec(), "Reference");
            buildingReference = false;
            reference = new Vector3[r.bind.Length];
            for (int i = 0; i < reference.Length; i++) reference[i] = r.bind[i] / r.k;
            UnityEngine.Object.Destroy(r.go);
            return reference;
        }

        public static QuadRig Build(QuadSpec s, string name = "Quad")
        {
            float dy = 1.2f * (s.legLen - 1f);   // body rises or sinks with leg length
            Vector3 Body(float x, float y, float z) { return new Vector3(x * s.bodyW, y + dy, z * s.bodyLen); }
            Vector3 LegP(float x, float y, float z) { return new Vector3(x * s.bodyW, y * s.legLen, z * s.bodyLen); }

            var p = new Vector3[(int)QB.Count];
            p[(int)QB.Pelvis] = Body(0, 1.28f, -0.52f);
            p[(int)QB.Spine] = Body(0, 1.30f, -0.12f);
            p[(int)QB.Chest] = Body(0, 1.30f, 0.32f);
            Vector3 neckBase = Body(0, 1.38f + s.hump * 0.06f, 0.56f);
            p[(int)QB.Neck] = neckBase;
            float na = s.neckAngle * Mathf.Deg2Rad;
            Vector3 neckDir = new Vector3(0, Mathf.Sin(na), Mathf.Cos(na));
            float neckLength = 0.74f * s.neckLen;
            Vector3 poll = neckBase + neckDir * neckLength;
            p[(int)QB.Head] = poll;
            float ha = -s.headDrop * Mathf.Deg2Rad;
            Vector3 headDir = new Vector3(0, Mathf.Sin(ha), Mathf.Cos(ha));
            float headLength = 0.54f * s.headLen;
            Vector3 tailHead = Body(0, 1.38f, -0.80f);
            float ta = s.tailAngle * Mathf.Deg2Rad;
            Vector3 tailDir = new Vector3(0, -Mathf.Cos(ta), -Mathf.Sin(ta));
            float tailLength = 0.85f * s.tailLen;
            p[(int)QB.Tail1] = tailHead;
            p[(int)QB.Tail2] = tailHead + tailDir * tailLength * 0.5f;

            // legs: joints a (shoulder/hip), b (elbow/stifle), c (knee/hock), ground
            var joints = new Vector3[4, 4];
            for (int leg = 0; leg < 4; leg++)
            {
                float x = (leg % 2 == 0 ? -1f : 1f);
                if (leg < 2)
                {
                    joints[leg, 0] = LegP(x * 0.19f, 1.20f, 0.46f);
                    joints[leg, 1] = LegP(x * 0.20f, 0.95f, 0.38f);
                    joints[leg, 2] = LegP(x * 0.20f, 0.53f, 0.41f);
                    joints[leg, 3] = LegP(x * 0.20f, 0.0f, 0.43f);
                }
                else
                {
                    joints[leg, 0] = LegP(x * 0.19f, 1.24f, -0.54f);
                    joints[leg, 1] = LegP(x * 0.205f, 0.90f, -0.40f);
                    joints[leg, 2] = LegP(x * 0.205f, 0.56f, -0.65f);
                    joints[leg, 3] = LegP(x * 0.205f, 0.0f, -0.60f);
                }
                for (int seg = 0; seg < 3; seg++) p[(int)QB.FLa + leg * 3 + seg] = joints[leg, seg];
            }

            SurfaceMap.Hint(s.coat, Surf.Fur); SurfaceMap.Hint(s.belly, Surf.Fur); SurfaceMap.Hint(s.rump, Surf.Fur);
            SurfaceMap.Hint(s.legColor, Surf.Fur); SurfaceMap.Hint(s.muzzle, Surf.Fur); SurfaceMap.Hint(s.maneColor, Surf.Hair);
            SurfaceMap.Hint(s.hoof, Surf.Plain);
            var b = new MeshBuilder { skinned = true };
            const int N = 12;

            // ------------------------------------------------------------ body
            var rings = new List<Vector3[]>(); var cols = new List<Color32>(); var ws = new List<BoneWeight>();
            void BodyRing(float z, float yc, float rx, float ry, Color32 c, BoneWeight w, float lift = 0f)
            {
                Vector3 centre = Body(0, yc + lift, z);
                rings.Add(MeshBuilder.Ring(centre, Vector3.right, Vector3.up, rx * s.bodyW, (ry + lift * 0.5f) * s.bodyD, N));
                cols.Add(c); ws.Add(w);
            }
            float hf = s.hump;
            BodyRing(-0.80f, 1.34f, 0.09f, 0.10f, s.rump, W(QB.Pelvis));
            BodyRing(-0.67f, 1.245f, 0.265f, 0.305f, s.rump, W(QB.Pelvis));
            BodyRing(-0.45f, 1.19f, 0.30f, 0.36f, s.rump, W(QB.Pelvis));
            BodyRing(-0.18f, 1.165f, 0.275f, 0.335f, s.coat, W(QB.Pelvis, 0.5f, QB.Spine));
            BodyRing(0.08f, 1.145f, 0.29f, 0.35f, s.coat, W(QB.Spine), hf * 0.05f);
            BodyRing(0.30f, 1.145f, 0.285f, 0.37f, s.coat, W(QB.Spine, 0.4f, QB.Chest), hf * 0.16f);
            BodyRing(0.50f, 1.18f, 0.24f, 0.355f, s.coat, W(QB.Chest), hf * 0.2f);
            BodyRing(0.65f, 1.23f, 0.15f, 0.24f, s.coat, W(QB.Chest), hf * 0.1f);
            // belly shading: lower vertices of each ring take the belly colour
            int v0 = b.verts.Count;
            b.Loft(rings, cols, ws);
            if (!s.belly.Equals(s.coat))
                for (int i = v0; i < b.verts.Count; i++)
                    if (b.verts[i].y < 0.98f + dy && b.verts[i].z > -0.62f * s.bodyLen)
                        b.colors[i] = new Color32(s.belly.r, s.belly.g, s.belly.b, 255);

            float backTop = 1.145f + 0.35f * s.bodyD + dy;

            // ------------------------------------------------------------ neck
            Vector3 neckSide = Vector3.right;
            Vector3 neckUp = Vector3.Cross(neckDir, neckSide).normalized;   // "depth" axis of the neck section
            rings = new List<Vector3[]>(); cols = new List<Color32>(); ws = new List<BoneWeight>();
            void NeckRing(float t, float rx, float rd, BoneWeight w)
            {
                Vector3 c = neckBase + neckDir * (neckLength * t) - Vector3.up * (0.13f * (1f - t) * (1f - t));
                rings.Add(MeshBuilder.Ring(c, neckSide, neckUp, rx * s.neckThick, rd * s.neckThick, 10));
                cols.Add(s.coat); ws.Add(w);
            }
            NeckRing(-0.14f, 0.16f, 0.30f, W(QB.Chest));
            NeckRing(0.14f, 0.135f, 0.245f, W(QB.Chest, 0.5f, QB.Neck));
            NeckRing(0.46f, 0.108f, 0.185f, W(QB.Neck));
            NeckRing(0.78f, 0.09f, 0.145f, W(QB.Neck));
            NeckRing(1.02f, 0.08f, 0.122f, W(QB.Neck, 0.5f, QB.Head));
            b.Loft(rings, cols, ws);

            // ------------------------------------------------------------ head
            Vector3 headUp = Vector3.Cross(headDir, Vector3.right).normalized;
            if (headUp.y < 0) headUp = -headUp;
            float hs = s.headSize;
            rings = new List<Vector3[]>(); cols = new List<Color32>(); ws = new List<BoneWeight>();
            void HeadRing(float t, float rx, float rd, Color32 c)
            {
                Vector3 centre = poll + headDir * (headLength * t) - headUp * (0.02f * hs);
                rings.Add(MeshBuilder.Ring(centre, Vector3.right, headUp, rx * hs, rd * hs, 10));
                cols.Add(c); ws.Add(W(QB.Head));
            }
            HeadRing(-0.12f, 0.07f, 0.09f, s.coat);
            HeadRing(0.05f, 0.096f, 0.126f, s.coat);
            HeadRing(0.28f, 0.102f, 0.138f, s.coat);
            HeadRing(0.62f, 0.074f, 0.094f, s.coat);
            HeadRing(0.9f, 0.062f, 0.072f, s.muzzle);
            HeadRing(1.02f, 0.047f, 0.053f, s.muzzle);
            b.Loft(rings, cols, ws);

            b.Bone((int)QB.Head);
            Vector3 HeadPt(float t, float side, float up) { return poll + headDir * (headLength * t) + Vector3.right * (side * hs) + headUp * (up * hs); }
            b.Col(Pal.Eye);
            b.Ellipsoid(HeadPt(0.3f, -0.088f, 0.03f), new Vector3(0.016f, 0.022f, 0.026f) * hs, 6, 4);
            b.Ellipsoid(HeadPt(0.3f, 0.088f, 0.03f), new Vector3(0.016f, 0.022f, 0.026f) * hs, 6, 4);
            b.Ellipsoid(HeadPt(0.98f, -0.026f, 0.012f), new Vector3(0.012f, 0.012f, 0.016f) * hs, 5, 3);
            b.Ellipsoid(HeadPt(0.98f, 0.026f, 0.012f), new Vector3(0.012f, 0.012f, 0.016f) * hs, 5, 3);
            if (s.blaze)
            {
                b.Col(Pal.HorseWhite);
                b.Beam(HeadPt(0.12f, 0, 0.098f), HeadPt(0.9f, 0, 0.047f), 0.04f * hs, 0.012f, headUp);
            }
            // ears
            b.Col(s.coat);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 e0 = HeadPt(-0.02f, side * 0.055f, 0.085f);
                Vector3 e1 = e0 + (headUp * 0.9f - headDir * 0.25f + Vector3.right * side * 0.25f).normalized * (0.125f * s.earLen * hs);
                b.Cyl(e0, e1, 0.03f * hs, 0.006f * hs, 6, false, false);
            }
            Horns(b, s, HeadPt, headUp, headDir, hs);

            // mane, forelock, ruff, beard
            if (s.mane)
            {
                b.Col(s.maneColor);
                for (int i = 0; i < 6; i++)
                {
                    float t0 = i / 6f, t1 = (i + 1) / 6f;
                    Vector3 a = neckBase + neckDir * (neckLength * Mathf.Lerp(0.02f, 1.02f, t0)) + neckUp * ((Mathf.Lerp(0.21f, 0.115f, t0)) * s.neckThick - 0.03f);
                    Vector3 c = neckBase + neckDir * (neckLength * Mathf.Lerp(0.02f, 1.02f, t1)) + neckUp * ((Mathf.Lerp(0.21f, 0.115f, t1)) * s.neckThick - 0.03f);
                    b.weight = t0 < 0.25f ? W(QB.Chest, 0.5f, QB.Neck) : W(QB.Neck);
                    b.Beam(a, c, 0.045f, 0.13f, neckUp);
                }
                b.Bone((int)QB.Head);
                b.Ellipsoid(HeadPt(0.12f, 0, 0.11f), new Vector3(0.045f, 0.03f, 0.085f) * hs, 6, 4);
            }
            if (s.ruff > 0f)
            {
                b.Col(s.maneColor);
                b.weight = W(QB.Chest, 0.6f, QB.Neck);
                Vector3 rc = neckBase + neckDir * (neckLength * 0.3f) - Vector3.up * 0.05f;
                b.Ellipsoid(rc, new Vector3(0.20f, 0.30f, 0.26f) * (s.neckThick * (0.7f + 0.4f * s.ruff)), 10, 6);
                if (s.kind == "bison")
                {
                    b.Bone((int)QB.Head);
                    b.Ellipsoid(HeadPt(0.55f, 0, -0.13f), new Vector3(0.07f, 0.12f, 0.09f) * hs, 8, 5);   // beard
                    b.Ellipsoid(HeadPt(0.1f, 0, 0.1f), new Vector3(0.12f, 0.08f, 0.12f) * hs, 8, 5);      // cap
                }
            }

            // ------------------------------------------------------------ tail
            rings = new List<Vector3[]>(); cols = new List<Color32>(); ws = new List<BoneWeight>();
            Vector3 tailSide = Vector3.right, tailFwd = Vector3.Cross(tailDir, tailSide).normalized;
            void TailRing(float t, float r, BoneWeight w)
            {
                rings.Add(MeshBuilder.Ring(tailHead + tailDir * (tailLength * t), tailSide, tailFwd, r * s.tailThick, r * s.tailThick, 7));
                cols.Add(s.maneColor); ws.Add(w);
            }
            TailRing(-0.03f, 0.04f, W(QB.Pelvis, 0.5f, QB.Tail1));
            TailRing(0.15f, 0.065f, W(QB.Tail1));
            TailRing(0.45f, 0.085f, W(QB.Tail1, 0.5f, QB.Tail2));
            TailRing(0.8f, 0.07f, W(QB.Tail2));
            TailRing(1.0f, 0.025f, W(QB.Tail2));
            b.Loft(rings, cols, ws);

            // ------------------------------------------------------------ legs
            for (int leg = 0; leg < 4; leg++)
            {
                bool front = leg < 2;
                QB a = (QB)((int)QB.FLa + leg * 3), bb = a + 1, c = a + 2;
                QB body = front ? QB.Chest : QB.Pelvis;
                Vector3 J0 = joints[leg, 0], J1 = joints[leg, 1], J2 = joints[leg, 2], G = joints[leg, 3];
                float lt = s.legThick;
                Color32 upper = !front && !s.rump.Equals(s.coat) ? s.rump : s.coat;
                Color32 low = s.socks ? Pal.HorseWhite : s.legColor;
                rings = new List<Vector3[]>(); cols = new List<Color32>(); ws = new List<BoneWeight>();
                void LR(Vector3 pos, float rx, float rz, Color32 col, BoneWeight w)
                {
                    rings.Add(MeshBuilder.Ring(pos, Vector3.right, Vector3.forward, rx * lt, rz * lt, 8));
                    cols.Add(col); ws.Add(w);
                }
                Vector3 inward = new Vector3(-Mathf.Sign(J0.x) * 0.02f, 0.02f, 0);
                if (front)
                {
                    LR(J0 + inward, 0.085f, 0.15f, s.coat, W(body, 0.6f, a));
                    LR(Vector3.Lerp(J0, J1, 0.5f), 0.088f, 0.135f, s.coat, W(a));
                    LR(J1, 0.075f, 0.095f, s.coat, W(a, 0.5f, bb));
                    LR(Vector3.Lerp(J1, J2, 0.25f), 0.074f, 0.092f, s.coat, W(bb));
                    LR(Vector3.Lerp(J1, J2, 0.8f), 0.052f, 0.06f, s.legColor, W(bb));
                    LR(J2, 0.056f, 0.064f, s.legColor, W(bb, 0.5f, c));
                }
                else
                {
                    LR(J0 + inward, 0.085f, 0.17f, upper, W(body, 0.6f, a));
                    LR(Vector3.Lerp(J0, J1, 0.55f), 0.092f, 0.16f, upper, W(a));
                    LR(J1, 0.082f, 0.125f, s.coat, W(a, 0.5f, bb));
                    LR(Vector3.Lerp(J1, J2, 0.35f), 0.078f, 0.105f, s.coat, W(bb));
                    LR(Vector3.Lerp(J1, J2, 0.8f), 0.056f, 0.068f, s.legColor, W(bb));
                    LR(J2, 0.056f, 0.07f, s.legColor, W(bb, 0.5f, c));
                }
                float gz = G.z - J2.z;
                LR(Vector3.Lerp(J2, G, 0.14f), 0.045f, 0.052f, s.legColor, W(c));
                LR(Vector3.Lerp(J2, G, 0.62f) + new Vector3(0, 0, gz * 0.1f), 0.04f, 0.046f, low, W(c));
                LR(Vector3.Lerp(J2, G, 0.74f), 0.05f, 0.057f, low, W(c));
                LR(Vector3.Lerp(J2, G, 0.86f) + new Vector3(0, 0, 0.012f), 0.044f, 0.05f, low, W(c));
                b.Loft(rings, cols, ws);
                b.Bone((int)c);
                b.Col(s.hoof, s.pads ? 0f : 0.4f);
                Vector3 hoofTop = Vector3.Lerp(J2, G, 0.86f) + new Vector3(0, 0, 0.012f);
                if (s.pads) b.Ellipsoid(G + new Vector3(0, 0.035f * lt, 0.03f), new Vector3(0.05f, 0.04f, 0.075f) * lt, 8, 4);
                else b.Cyl(hoofTop, G + new Vector3(0, 0, 0.025f), 0.052f * lt, 0.07f * lt, 8);
            }

            // ------------------------------------------------------------ assemble
            float k = s.scale;
            for (int i = 0; i < b.verts.Count; i++)
            {
                b.verts[i] *= k;
                Vector4 uv = b.uvs[i];
                b.uvs[i] = new Vector4(uv.x * k, uv.y * k, uv.z * k, uv.w);
            }

            var rig = new QuadRig { spec = s, k = k };
            rig.go = new GameObject(name);
            rig.root = rig.go.transform;
            int n = (int)QB.Count;
            rig.bones = new Transform[n];
            rig.bind = new Vector3[n];
            rig.bindLocal = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                rig.bones[i] = new GameObject(((QB)i).ToString()).transform;
                rig.bind[i] = p[i] * k;
            }
            var poses = new Matrix4x4[n];
            for (int i = 0; i < n; i++)
            {
                Transform parent = Parent[i] == QB.Count ? rig.root : rig.bones[(int)Parent[i]];
                Vector3 parentPos = Parent[i] == QB.Count ? Vector3.zero : rig.bind[(int)Parent[i]];
                rig.bones[i].SetParent(parent, false);
                rig.bindLocal[i] = rig.bind[i] - parentPos;
                rig.bones[i].localPosition = rig.bindLocal[i];
                poses[i] = Matrix4x4.Translate(-rig.bind[i]);
            }
            // the animal modelled in Blender when this build has it (horse.py, quad.py); the code-built one otherwise
            string bodyModel = s.kind + "_body";
            bool modelled = !buildingReference && Models.Has(bodyModel);
            Mesh mesh;
            if (modelled)
            {
                var dyes = new Color32[16];
                for (int i = 0; i < dyes.Length; i++) dyes[i] = new Color32(255, 255, 255, 255);
                dyes[1] = s.coat; dyes[2] = s.maneColor; dyes[3] = s.legColor; dyes[4] = s.muzzle; dyes[5] = s.hoof; dyes[6] = s.rump;
                dyes[7] = s.blaze ? Pal.HorseWhite : s.coat;
                dyes[8] = s.socks ? Pal.HorseWhite : s.legColor;
                dyes[9] = s.belly;
                // the horse model shades from the coat into its points and its muzzle (horse.py, parts 10-12)
                dyes[10] = Color32.Lerp(s.coat, s.legColor, 0.35f);
                dyes[11] = Color32.Lerp(s.coat, s.legColor, 0.7f);
                dyes[12] = Color32.Lerp(s.coat, s.muzzle, 0.5f);
                var pieces = new List<string> { bodyModel };
                if (s.horns != HornKind.None && Models.Has(s.kind + "_horns")) pieces.Add(s.kind + "_horns");
                // The horse model follows whatever joints a breed has; the others were modelled on their own preset's joints.
                Vector3[] modelJoints = s.kind == "horse" ? ReferenceJoints() : p;
                mesh = HumanModel.Combine(pieces, bone => Enum.TryParse(bone, out QB q) ? (int)q : 0, dyes, k, p, modelJoints, name);
            }
            else mesh = b.ToMesh(name);
            mesh.bindposes = poses;
            var body2 = new GameObject("Body");
            body2.transform.SetParent(rig.root, false);
            rig.smr = body2.AddComponent<SkinnedMeshRenderer>();
            rig.smr.sharedMesh = mesh;
            rig.smr.bones = rig.bones;
            rig.smr.rootBone = rig.bones[(int)QB.Spine];
            rig.smr.sharedMaterial = modelled ? Mats.SurfaceUV : Mats.Lit;
            float ext = 4.2f * k * Mathf.Max(1f, s.bodyLen);
            rig.smr.localBounds = new Bounds(Vector3.zero, new Vector3(ext, ext, ext));
            rig.smr.quality = SkinQuality.Bone2;
            rig.smr.updateWhenOffscreen = Args.Offscreen;

            for (int leg = 0; leg < 4; leg++)
            {
                rig.hoofRest[leg] = joints[leg, 3] * k;
                rig.lenA[leg] = Vector3.Distance(joints[leg, 0], joints[leg, 1]) * k;
                rig.lenB[leg] = Vector3.Distance(joints[leg, 1], joints[leg, 2]) * k;
                rig.lenC[leg] = Vector3.Distance(joints[leg, 2], joints[leg, 3]) * k;
            }
            rig.withers = (backTop + 0.02f) * k;
            rig.backLength = 1.0f * s.bodyLen * k;
            rig.seat = new Vector3(0, backTop - 0.005f, 0.02f * s.bodyLen) * k;
            rig.saddle = new GameObject("Saddle").transform;
            rig.saddle.SetParent(rig.B(QB.Spine), false);
            rig.saddle.localPosition = rig.seat - rig.Bind(QB.Spine);
            rig.mouth = new GameObject("Mouth").transform;
            rig.mouth.SetParent(rig.B(QB.Head), false);
            rig.mouth.localPosition = (headDir * headLength) * k;
            return rig;
        }

        static void Horns(MeshBuilder b, QuadSpec s, Func<float, float, float, Vector3> headPt, Vector3 up, Vector3 fwd, float hs)
        {
            if (s.horns == HornKind.None) return;
            b.Bone((int)QB.Head);
            b.Col(s.horns == HornKind.Antlers ? Pal.Antler : (s.horns == HornKind.Pronghorn || s.horns == HornKind.Bison ? Pal.H(0x2A2420) : Pal.Bone), 0.3f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 right = Vector3.right * side;
                Vector3 o = headPt(-0.02f, side * 0.05f, 0.09f);
                switch (s.horns)
                {
                    case HornKind.Cow:
                    {
                        Vector3 a = o + right * 0.03f * hs;
                        Vector3 m = a + (right * 0.9f + up * 0.25f).normalized * 0.16f * hs;
                        Vector3 e = m + (right * 0.4f + up * 0.9f - fwd * 0.2f).normalized * 0.16f * hs;
                        b.Cyl(a, m, 0.03f * hs, 0.022f * hs, 6, false, false);
                        b.Cyl(m, e, 0.022f * hs, 0.004f * hs, 6, false, false);
                        break;
                    }
                    case HornKind.Bison:
                    {
                        Vector3 a = o + right * 0.06f * hs;
                        Vector3 m = a + (right * 0.95f + up * 0.2f).normalized * 0.09f * hs;
                        Vector3 e = m + (right * 0.1f + up * 1f).normalized * 0.1f * hs;
                        b.Cyl(a, m, 0.032f * hs, 0.024f * hs, 6, false, false);
                        b.Cyl(m, e, 0.024f * hs, 0.004f * hs, 6, false, false);
                        break;
                    }
                    case HornKind.Pronghorn:
                    {
                        Vector3 m = o + (up * 1f + right * 0.12f).normalized * 0.22f * hs;
                        Vector3 e = m + (up * 0.6f - fwd * 0.6f + right * -0.3f).normalized * 0.1f * hs;
                        b.Cyl(o, m, 0.022f * hs, 0.016f * hs, 6, false, false);
                        b.Cyl(m, e, 0.016f * hs, 0.004f * hs, 6, false, false);
                        b.Cyl(o + (m - o) * 0.6f, o + (m - o) * 0.6f + (fwd * 0.9f + up * 0.4f).normalized * 0.07f * hs, 0.014f * hs, 0.004f * hs, 5, false, false);
                        break;
                    }
                    case HornKind.Antlers:
                    {
                        Vector3 beam0 = o;
                        Vector3 beam1 = beam0 + (up * 0.75f + right * 0.55f - fwd * 0.3f).normalized * 0.26f * hs;
                        Vector3 beam2 = beam1 + (up * 0.55f + right * 0.25f + fwd * 0.75f).normalized * 0.3f * hs;
                        b.Cyl(beam0, beam1, 0.02f * hs, 0.016f * hs, 5, false, false);
                        b.Cyl(beam1, beam2, 0.016f * hs, 0.006f * hs, 5, false, false);
                        b.Cyl(beam0 + (beam1 - beam0) * 0.3f, beam0 + (beam1 - beam0) * 0.3f + (up * 0.8f + fwd * 0.6f).normalized * 0.14f * hs, 0.012f * hs, 0.004f * hs, 5, false, false);
                        b.Cyl(beam1, beam1 + (up * 1f - fwd * 0.1f).normalized * 0.2f * hs, 0.012f * hs, 0.004f * hs, 5, false, false);
                        b.Cyl(beam1 + (beam2 - beam1) * 0.5f, beam1 + (beam2 - beam1) * 0.5f + up * 0.16f * hs, 0.01f * hs, 0.004f * hs, 5, false, false);
                        break;
                    }
                }
            }
        }

        // -------------------------------------------------------------------- tack

        /// <summary>Western saddle with blanket, stirrups, cinch, saddlebags and a bridle. Fits horses only.</summary>
        public static void AddTack(QuadRig rig, Color32 blanket, Color32 leather, bool bags = true)
        {
            float k = rig.k;
            QuadSpec s = rig.spec;
            // saddle, bags and bridle modelled in Blender (Tools/blender/tack.py), dyed to this horse's blanket and leather
            if (Models.Has("tack_saddle") && Models.Has("tack_bridle") && rig.spec.kind == "horse")
            {
                var dyes = new Color32[16];
                for (int i = 0; i < dyes.Length; i++) dyes[i] = new Color32(255, 255, 255, 255);
                dyes[12] = blanket; dyes[13] = leather;
                rig.tack = HumanModel.Dyed("tack_saddle", "SaddleMesh", dyes);
                rig.tack.transform.SetParent(rig.saddle, false);
                rig.tack.transform.localScale = Vector3.one * rig.k;
                if (bags && Models.Has("tack_bags"))
                    HumanModel.Dyed("tack_bags", "Bags", dyes).transform.SetParent(rig.tack.transform, false);
                GameObject modelBridle = HumanModel.Dyed("tack_bridle", "Bridle", dyes, true);
                modelBridle.transform.SetParent(rig.B(QB.Head), false);
                modelBridle.transform.localScale = Vector3.one * rig.k;
                return;
            }
            var b = new MeshBuilder();
            float top = 0f;   // builder space: origin at the seat point, +Z forward
            float halfW = 0.29f * s.bodyW, halfD = 0.35f * s.bodyD;

            // blanket: an arc over the back
            b.Col(blanket);
            Arc(b, -0.36f, 0.30f, halfW + 0.012f, halfD + 0.012f, -halfD, 62f, false);
            b.Col(Pal.Shade(blanket, 0.75f));
            Arc(b, -0.37f, -0.33f, halfW + 0.014f, halfD + 0.014f, -halfD, 62f, false);
            Arc(b, 0.27f, 0.31f, halfW + 0.014f, halfD + 0.014f, -halfD, 62f, false);

            // skirt and seat
            b.Col(leather, 0.35f);
            Arc(b, -0.30f, 0.24f, halfW + 0.022f, halfD + 0.022f, -halfD, 44f, false);
            b.Ellipsoid(new Vector3(0, top + 0.045f, -0.04f), new Vector3(0.17f, 0.045f, 0.24f), 10, 5);
            // cantle
            b.Push(new Vector3(0, top + 0.06f, -0.25f), Quaternion.Euler(-22f, 0, 0));
            b.Ellipsoid(Vector3.zero, new Vector3(0.17f, 0.09f, 0.035f), 10, 5);
            b.Pop();
            // pommel and horn
            b.Ellipsoid(new Vector3(0, top + 0.07f, 0.19f), new Vector3(0.10f, 0.075f, 0.06f), 8, 5);
            b.Cyl(new Vector3(0, top + 0.12f, 0.20f), new Vector3(0, top + 0.19f, 0.225f), 0.018f, 0.016f, 6);
            b.Ellipsoid(new Vector3(0, top + 0.2f, 0.228f), new Vector3(0.032f, 0.012f, 0.032f), 6, 3);

            // fenders, stirrups, cinch
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (halfW + 0.035f);
                b.Col(leather, 0.3f);
                b.Beam(new Vector3(side * (halfW * 0.86f), top - 0.10f, 0.03f), new Vector3(x + side * 0.015f, top - 0.52f, 0.06f), 0.012f, 0.19f, Vector3.right * side);
                b.Col(Pal.WoodDark);
                Vector3 st = new Vector3(x + side * 0.03f, top - 0.68f, 0.07f);
                b.Box(st + new Vector3(0, -0.07f, 0), new Vector3(0.11f, 0.018f, 0.075f));
                b.Beam(st + new Vector3(-0.05f, -0.07f, 0), st + new Vector3(-0.015f, 0.06f, 0), 0.012f, 0.06f, Vector3.forward);
                b.Beam(st + new Vector3(0.05f, -0.07f, 0), st + new Vector3(0.015f, 0.06f, 0), 0.012f, 0.06f, Vector3.forward);
                b.Col(leather);
                b.Beam(st + new Vector3(0, 0.06f, 0), new Vector3(x + side * 0.015f, top - 0.5f, 0.06f), 0.03f, 0.01f, Vector3.right * side);
                if (bags)
                {
                    b.Col(Pal.Shade(leather, 0.85f), 0.25f);
                    b.Box(new Vector3(side * (halfW + 0.03f), top - 0.17f, -0.42f), new Vector3(0.09f, 0.24f, 0.26f), Quaternion.Euler(0, 0, side * -8f));
                    b.Col(Pal.Brass, 0.9f);
                    b.Box(new Vector3(side * (halfW + 0.08f), top - 0.2f, -0.42f), new Vector3(0.012f, 0.03f, 0.03f));
                }
            }
            b.Col(Pal.Shade(leather, 0.7f));
            Arc(b, 0.09f, 0.15f, halfW + 0.01f, halfD + 0.01f, -halfD, 180f, true);
            if (bags)
            {
                b.Col(Pal.ClothGrey);
                b.Cyl(new Vector3(-0.27f, top + 0.05f, -0.40f), new Vector3(0.27f, top + 0.05f, -0.40f), 0.075f, 0.075f, 10);
                b.Col(leather);
                b.Cyl(new Vector3(-0.16f, top + 0.05f, -0.40f), new Vector3(-0.13f, top + 0.05f, -0.40f), 0.08f, 0.08f, 10);
                b.Cyl(new Vector3(0.13f, top + 0.05f, -0.40f), new Vector3(0.16f, top + 0.05f, -0.40f), 0.08f, 0.08f, 10);
            }

            rig.tack = b.ToObject("SaddleMesh", rig.saddle, Mats.LitDouble);
            rig.tack.transform.localScale = Vector3.one * k;

            // bridle
            var h = new MeshBuilder();
            h.Col(leather);
            float hs = s.headSize;
            float ha = -s.headDrop * Mathf.Deg2Rad;
            Vector3 headDir = new Vector3(0, Mathf.Sin(ha), Mathf.Cos(ha));
            Vector3 headUp = Vector3.Cross(headDir, Vector3.right).normalized;
            if (headUp.y < 0) headUp = -headUp;
            float len = 0.54f * s.headLen;
            Vector3 P(float t, float side, float up) { return headDir * (len * t) + Vector3.right * (side * hs) + headUp * ((up - 0.02f) * hs); }
            h.Loft(new List<Vector3[]>
            {
                MeshBuilder.Ring(P(0.70f, 0, 0), Vector3.right, headUp, 0.068f * hs, 0.083f * hs, 10),
                MeshBuilder.Ring(P(0.75f, 0, 0), Vector3.right, headUp, 0.064f * hs, 0.078f * hs, 10),
            }, null, null, false, false);
            h.Loft(new List<Vector3[]>
            {
                MeshBuilder.Ring(P(0.03f, 0, 0), Vector3.right, headUp, 0.094f * hs, 0.12f * hs, 10),
                MeshBuilder.Ring(P(0.08f, 0, 0), Vector3.right, headUp, 0.097f * hs, 0.124f * hs, 10),
            }, null, null, false, false);
            for (int side = -1; side <= 1; side += 2)
                h.Beam(P(0.06f, side * 0.097f, 0.02f), P(0.72f, side * 0.068f, 0.0f), 0.01f, 0.02f, Vector3.right * side);
            h.Col(Pal.Steel, 0.9f);
            for (int side = -1; side <= 1; side += 2)
                h.Cyl(P(0.74f, side * 0.062f, -0.03f), P(0.74f, side * 0.078f, -0.03f), 0.02f, 0.02f, 8);
            GameObject bridle = h.ToObject("Bridle", rig.B(QB.Head), Mats.LitDouble);
            bridle.transform.localScale = Vector3.one * k;
        }

        /// <summary>A strip over the back between z0 and z1, following the body ellipse; 'spread' degrees each side of the top.</summary>
        static void Arc(MeshBuilder b, float z0, float z1, float rx, float ry, float centreY, float spread, bool closed)
        {
            const int M = 14;
            int start = b.verts.Count;
            float a0 = closed ? -Mathf.PI : -spread * Mathf.Deg2Rad, a1 = closed ? Mathf.PI : spread * Mathf.Deg2Rad;
            for (int j = 0; j < 2; j++)
                for (int i = 0; i <= M; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)M);
                    float sn = Mathf.Sin(a), cs = Mathf.Cos(a);
                    b.Vert(new Vector3(sn * rx, centreY + cs * ry, j == 0 ? z0 : z1), new Vector3(sn * ry, cs * rx, 0f));
                }
            int row = M + 1;
            for (int i = 0; i < M; i++)
            {
                int k = start + i;
                b.Tri(k, k + row, k + row + 1);
                b.Tri(k, k + row + 1, k + 1);
            }
        }
    }
}
