using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Human bones. Left is -X when the figure faces +Z.</summary>
    public enum HB
    {
        Hips, Spine, Chest, Neck, Head,
        ArmL, ForeL, HandL, ArmR, ForeR, HandR,
        ThighL, ShinL, FootL, ToeL, ThighR, ShinR, FootR, ToeR,
        Count
    }

    public enum HatKind { None, Cowboy, Derby, FlatBrim, Kepi, Straw, Bonnet }
    public enum HairKind { Bald, Short, Long, Balding, Bun }
    public enum BeardKind { None, Moustache, Full, Goatee }

    /// <summary>Everything that distinguishes one person's look from another's.</summary>
    [Serializable]
    public class HumanSpec
    {
        public float height = 1.78f;
        public float build = 1f;          // 0.85 slim .. 1.2 heavy
        public float belly = 0f;          // 0..1
        public bool female;
        public Color32 skin = Pal.Skin[1], hair = Pal.Hair[1];
        public HairKind hairKind = HairKind.Short;
        public BeardKind beard = BeardKind.None;
        public Color32 shirt = Pal.ClothCream, pants = Pal.ClothBrown, boots = Pal.LeatherDark;
        public bool vest; public Color32 vestColor = Pal.ClothBlack;
        public bool coat; public Color32 coatColor = Pal.LeatherTan;
        public bool skirt; public Color32 skirtColor = Pal.ClothBlue;
        public bool apron;
        public bool suspenders;
        public bool gunbelt;
        public bool bandana; public Color32 bandanaColor = Pal.ClothRed;
        public bool badge;
        public bool rolledSleeves;
        public HatKind hat = HatKind.None; public Color32 hatColor = Pal.LeatherDark;

        public HumanSpec Clone() { return (HumanSpec)MemberwiseClone(); }

        /// <summary>A plausible random townsperson or settler.</summary>
        public static HumanSpec Random(System.Random r, bool female, bool armed = false)
        {
            Color32[] shirts = { Pal.ClothCream, Pal.ClothWhite, Pal.ClothBlue, Pal.ClothRed, Pal.ClothGreen, Pal.ClothGrey, Pal.ClothTan, Pal.Denim, Pal.ClothYellow };
            Color32[] trousers = { Pal.ClothBrown, Pal.ClothGrey, Pal.ClothBlack, Pal.Denim, Pal.ClothTan, Pal.Wool };
            Color32[] dark = { Pal.ClothBlack, Pal.ClothBrown, Pal.ClothNavy, Pal.ClothGrey, Pal.LeatherDark, Pal.ClothGreen, Pal.ClothPurple };
            Color32[] hats = { Pal.LeatherDark, Pal.ClothBlack, Pal.LeatherTan, Pal.ClothGrey, Pal.ClothBrown, Pal.ClothCream };
            var s = new HumanSpec
            {
                female = female,
                height = female ? U.Range(r, 1.58f, 1.72f) : U.Range(r, 1.68f, 1.88f),
                build = U.Range(r, 0.88f, 1.15f),
                belly = r.NextDouble() < 0.25 ? U.Range(r, 0.3f, 0.9f) : 0f,
                skin = Pal.Skin[r.Next(r.NextDouble() < 0.8 ? 3 : Pal.Skin.Length)],
                hair = U.Pick(r, Pal.Hair),
                shirt = U.Pick(r, shirts),
                pants = U.Pick(r, trousers),
                boots = r.NextDouble() < 0.5 ? Pal.LeatherDark : Pal.Leather,
            };
            if (female)
            {
                s.hairKind = r.NextDouble() < 0.6 ? HairKind.Bun : HairKind.Long;
                s.skirt = r.NextDouble() < 0.85;
                s.skirtColor = U.Pick(r, dark);
                s.apron = s.skirt && r.NextDouble() < 0.3;
                s.hat = r.NextDouble() < 0.35 ? HatKind.Bonnet : (r.NextDouble() < 0.2 ? HatKind.Straw : HatKind.None);
                s.hatColor = r.NextDouble() < 0.5 ? Pal.ClothCream : Pal.Hay;
                s.belly = 0f;
            }
            else
            {
                s.hairKind = r.NextDouble() < 0.15 ? HairKind.Balding : HairKind.Short;
                double b = r.NextDouble();
                s.beard = b < 0.35 ? BeardKind.None : b < 0.6 ? BeardKind.Moustache : b < 0.9 ? BeardKind.Full : BeardKind.Goatee;
                s.vest = r.NextDouble() < 0.45; s.vestColor = U.Pick(r, dark);
                s.coat = !s.vest && r.NextDouble() < 0.2; s.coatColor = U.Pick(r, dark);
                s.suspenders = !s.vest && !s.coat && r.NextDouble() < 0.5;
                s.rolledSleeves = !s.coat && r.NextDouble() < 0.3;
                s.bandana = r.NextDouble() < 0.3; s.bandanaColor = U.Pick(r, shirts);
                double h = r.NextDouble();
                s.hat = h < 0.5 ? HatKind.Cowboy : h < 0.65 ? HatKind.Derby : h < 0.78 ? HatKind.FlatBrim : h < 0.86 ? HatKind.Straw : HatKind.None;
                s.hatColor = s.hat == HatKind.Straw ? Pal.Hay : U.Pick(r, hats);
                s.gunbelt = armed;
            }
            return s;
        }
    }

    /// <summary>A built figure: bones, renderer, attachment points and the measurements the animator needs.</summary>
    public class HumanRig
    {
        public GameObject go;
        public Transform root;
        public Transform[] bones;
        public Vector3[] bind;        // bone positions in model space, bind pose
        public Vector3[] bindLocal;   // bone local positions, bind pose
        public SkinnedMeshRenderer smr;
        public GameObject hat, mask;
        public Transform gripR, gripL, holster, back, eyes;
        public HumanSpec spec;
        public float k;               // height scale relative to 1.78 m
        public float armU, armF, legU, legF, hipHeight, eyeHeight, footHeight;

        public Transform B(HB b) { return bones[(int)b]; }
        public Vector3 Bind(HB b) { return bind[(int)b]; }

        public void ResetPose()
        {
            for (int i = 0; i < bones.Length; i++) bones[i].SetLocalPositionAndRotation(bindLocal[i], Quaternion.identity);
        }
    }

    /// <summary>Builds people from code: skeleton, skinned body, clothes, hat.</summary>
    public static class HumanBuilder
    {
        static readonly HB[] Parent =
        {
            HB.Count, HB.Hips, HB.Spine, HB.Chest, HB.Neck,
            HB.Chest, HB.ArmL, HB.ForeL, HB.Chest, HB.ArmR, HB.ForeR,
            HB.Hips, HB.ThighL, HB.ShinL, HB.FootL, HB.Hips, HB.ThighR, HB.ShinR, HB.FootR
        };

        const int N = 12;   // points per torso ring
        const float HeadScale = 1.08f;
        static readonly Vector3 HeadPivot = new Vector3(0, 1.575f, 0.008f);

        static Vector3 HeadPoint(Vector3 v) { return HeadPivot + (v - HeadPivot) * HeadScale; }

        struct Row { public float y, rx, rz, z; public Color32 c; public BoneWeight w; }

        static Vector3[] Ell(float x, float y, float z, float rx, float rz, int n, float sq = 2.3f)
        {
            var r = new Vector3[n];
            float e = 2f / sq;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                r[i] = new Vector3(x + rx * Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), e), y, z + rz * Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e));
            }
            return r;
        }

        static BoneWeight W(HB a) { return MeshBuilder.W((int)a); }
        static BoneWeight W(HB a, float wa, HB b) { return MeshBuilder.W((int)a, wa, (int)b); }

        /// <summary>Joint positions of a 1.78 m figure; sw and hw widen the shoulders and the hips.</summary>
        public static Vector3[] Joints(float sw, float hw)
        {
            var p = new Vector3[(int)HB.Count];
            p[(int)HB.Hips] = new Vector3(0, 0.97f, 0);
            p[(int)HB.Spine] = new Vector3(0, 1.09f, 0);
            p[(int)HB.Chest] = new Vector3(0, 1.25f, 0);
            p[(int)HB.Neck] = new Vector3(0, 1.50f, 0);
            p[(int)HB.Head] = new Vector3(0, 1.585f, 0.01f);
            for (int side = -1; side <= 1; side += 2)
            {
                bool l = side < 0;
                p[(int)(l ? HB.ArmL : HB.ArmR)] = new Vector3(side * 0.185f * sw, 1.445f, 0);
                p[(int)(l ? HB.ForeL : HB.ForeR)] = new Vector3(side * 0.205f * sw, 1.165f, 0);
                p[(int)(l ? HB.HandL : HB.HandR)] = new Vector3(side * 0.215f * sw, 0.905f, 0.01f);
                p[(int)(l ? HB.ThighL : HB.ThighR)] = new Vector3(side * 0.092f * hw, 0.93f, 0);
                p[(int)(l ? HB.ShinL : HB.ShinR)] = new Vector3(side * 0.098f * hw, 0.50f, 0);
                p[(int)(l ? HB.FootL : HB.FootR)] = new Vector3(side * 0.10f * hw, 0.085f, -0.01f);
                p[(int)(l ? HB.ToeL : HB.ToeR)] = new Vector3(side * 0.10f * hw, 0.02f, 0.13f);
            }
            return p;
        }

        static readonly Vector3[] Reference = Joints(1f, 1f);

        public static HumanRig Build(HumanSpec s, string name = "Human")
        {
            float k = s.height / 1.78f;
            float bw = s.build;
            float sw = (s.female ? 0.90f : 1f) * Mathf.Lerp(1f, bw, 0.6f);      // shoulder width factor
            float hw = (s.female ? 1.05f : 1f) * Mathf.Lerp(1f, bw, 0.5f);      // hip width factor

            // ---------------------------------------------------------------- skeleton (unscaled metres)
            Vector3[] p = Joints(sw, hw);

            SurfaceMap.Hint(s.shirt, Surf.Cloth); SurfaceMap.Hint(s.pants, Surf.Denim); SurfaceMap.Hint(s.boots, Surf.Leather);
            SurfaceMap.Hint(s.vestColor, Surf.Wool); SurfaceMap.Hint(s.coatColor, Surf.Leather); SurfaceMap.Hint(s.skirtColor, Surf.Cloth);
            SurfaceMap.Hint(s.bandanaColor, Surf.Cloth); SurfaceMap.Hint(s.hatColor, Surf.Wool);
            SurfaceMap.Hint(s.skin, Surf.Skin); SurfaceMap.Hint(s.hair, Surf.Hair);
            var b = new MeshBuilder { skinned = true };
            Color32 sleeve = s.coat ? s.coatColor : s.shirt;
            Color32 skinDark = Pal.Shade(s.skin, 0.9f);

            // ---------------------------------------------------------------- torso
            var rows = new List<Row>
            {
                new Row { y = 0.86f, rx = 0.105f, rz = 0.085f, z = 0f, c = s.pants, w = W(HB.Hips) },
                new Row { y = 0.94f, rx = 0.165f * hw, rz = 0.105f, z = 0f, c = s.pants, w = W(HB.Hips) },
                new Row { y = 1.02f, rx = 0.158f * hw, rz = 0.102f, z = 0f, c = s.pants, w = W(HB.Hips, 0.7f, HB.Spine) },
                new Row { y = 1.06f, rx = 0.150f * bw, rz = 0.098f, z = 0f, c = s.shirt, w = W(HB.Hips, 0.4f, HB.Spine) },
                new Row { y = 1.12f, rx = 0.142f * bw, rz = 0.094f, z = 0.003f, c = s.shirt, w = W(HB.Spine) },
                new Row { y = 1.20f, rx = 0.152f * bw, rz = 0.104f, z = 0.006f, c = s.shirt, w = W(HB.Spine, 0.5f, HB.Chest) },
                new Row { y = 1.31f, rx = 0.176f * sw, rz = 0.118f, z = 0.010f, c = s.shirt, w = W(HB.Chest) },
                new Row { y = 1.40f, rx = 0.190f * sw, rz = 0.108f, z = 0.006f, c = s.shirt, w = W(HB.Chest) },
                new Row { y = 1.465f, rx = 0.188f * sw, rz = 0.086f, z = 0f, c = s.shirt, w = W(HB.Chest) },
                new Row { y = 1.50f, rx = 0.080f, rz = 0.070f, z = 0f, c = s.shirt, w = W(HB.Chest, 0.6f, HB.Neck) },
                new Row { y = 1.53f, rx = 0.054f, rz = 0.056f, z = 0.004f, c = s.skin, w = W(HB.Neck) },
                new Row { y = 1.575f, rx = 0.050f, rz = 0.055f, z = 0.008f, c = s.skin, w = W(HB.Neck, 0.5f, HB.Head) },
            };
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                if (s.female)
                {
                    if (r.y > 1.05f && r.y < 1.25f) r.rx *= 0.88f;
                    if (Mathf.Abs(r.y - 1.31f) < 0.01f) { r.rz += 0.02f; r.z += 0.012f; }
                }
                if (s.belly > 0f && r.y > 1.0f && r.y < 1.25f)
                {
                    float f = s.belly * (1f - Mathf.Abs(r.y - 1.12f) / 0.14f);
                    if (f > 0f) { r.rz += 0.06f * f; r.z += 0.045f * f; r.rx += 0.02f * f; }
                }
                rows[i] = r;
            }
            LoftRows(b, rows, 1f, 0f);

            // belt
            b.Col(Pal.LeatherDark);
            LoftRows(b, new List<Row>
            {
                new Row { y = 1.035f, rx = 0.158f * hw + 0.006f, rz = 0.108f, z = 0f, c = Pal.LeatherDark, w = W(HB.Hips, 0.6f, HB.Spine) },
                new Row { y = 1.075f, rx = 0.152f * bw + 0.006f, rz = 0.104f + s.belly * 0.02f, z = s.belly * 0.012f, c = Pal.LeatherDark, w = W(HB.Hips, 0.5f, HB.Spine) },
            }, 1f, 0f, false);
            b.Col(Pal.Brass, 0.8f); b.Bone((int)HB.Hips);
            b.Box(new Vector3(0, 1.055f, 0.112f + s.belly * 0.03f), new Vector3(0.04f, 0.035f, 0.01f));

            // ---------------------------------------------------------------- head
            b.Push(Matrix4x4.TRS(HeadPivot, Quaternion.identity, Vector3.one * HeadScale) * Matrix4x4.Translate(-HeadPivot));
            var head = new List<Row>
            {
                new Row { y = 1.575f, rx = 0.050f, rz = 0.055f, z = 0.008f },
                new Row { y = 1.600f, rx = 0.060f, rz = 0.074f, z = 0.014f },
                new Row { y = 1.632f, rx = 0.070f, rz = 0.088f, z = 0.014f },
                new Row { y = 1.672f, rx = 0.076f, rz = 0.096f, z = 0.010f },
                new Row { y = 1.712f, rx = 0.078f, rz = 0.099f, z = 0.004f },
                new Row { y = 1.748f, rx = 0.073f, rz = 0.093f, z = 0f },
                new Row { y = 1.778f, rx = 0.052f, rz = 0.068f, z = -0.004f },
            };
            for (int i = 0; i < head.Count; i++) { Row r = head[i]; r.c = s.skin; r.w = W(HB.Head); head[i] = r; }
            LoftRows(b, head, 1f, 0f, true, 2f);

            b.Bone((int)HB.Head);
            b.Col(skinDark);
            b.Box(new Vector3(0, 1.662f, 0.103f), new Vector3(0.022f, 0.042f, 0.03f));                 // nose
            b.Col(s.skin);
            b.Ellipsoid(new Vector3(-0.079f, 1.675f, 0f), new Vector3(0.012f, 0.024f, 0.016f), 6, 4);    // ears
            b.Ellipsoid(new Vector3(0.079f, 1.675f, 0f), new Vector3(0.012f, 0.024f, 0.016f), 6, 4);
            b.Col(Pal.Eye);
            b.Ellipsoid(new Vector3(-0.031f, 1.697f, 0.089f), new Vector3(0.012f, 0.008f, 0.007f), 6, 4);
            b.Ellipsoid(new Vector3(0.031f, 1.697f, 0.089f), new Vector3(0.012f, 0.008f, 0.007f), 6, 4);
            b.Col(Pal.Shade(s.hair, 0.8f));
            b.Box(new Vector3(-0.031f, 1.716f, 0.094f), new Vector3(0.032f, 0.007f, 0.01f));           // brows
            b.Box(new Vector3(0.031f, 1.716f, 0.094f), new Vector3(0.032f, 0.007f, 0.01f));
            b.Col(Pal.Shade(s.skin, 0.62f));
            b.Box(new Vector3(0, 1.627f, 0.097f), new Vector3(0.036f, 0.005f, 0.006f));                // mouth

            // hair
            b.Col(s.hair);
            if (s.hairKind != HairKind.Bald)
            {
                bool balding = s.hairKind == HairKind.Balding;
                Shell(b, new Vector3(0, 1.712f, -0.012f), new Vector3(0.085f, 0.094f, 0.106f), 12, 8, v =>
                    balding ? (v.z < 0.01f && v.y < 1.745f && v.y > 1.625f)
                            : (v.y > 1.737f || (v.z < 0.032f && v.y > 1.625f)));
                if (s.hairKind == HairKind.Long)
                    Shell(b, new Vector3(0, 1.60f, -0.045f), new Vector3(0.088f, 0.13f, 0.07f), 10, 6, v => v.z < -0.02f);
                if (s.hairKind == HairKind.Bun)
                    b.Sphere(new Vector3(0, 1.715f, -0.115f), 0.047f, 8, 6);
            }
            if (s.beard == BeardKind.Full)
                Shell(b, new Vector3(0, 1.618f, 0.02f), new Vector3(0.076f, 0.058f, 0.088f), 12, 6, v => v.y < 1.652f && v.z > -0.035f);
            if (s.beard == BeardKind.Goatee)
                b.Ellipsoid(new Vector3(0, 1.598f, 0.078f), new Vector3(0.028f, 0.03f, 0.022f), 8, 5);
            if (s.beard != BeardKind.None)
                b.Box(new Vector3(0, 1.643f, 0.098f), new Vector3(0.056f, 0.013f, 0.012f));

            b.Pop();

            // ---------------------------------------------------------------- arms and legs
            for (int side = -1; side <= 1; side += 2)
            {
                bool l = side < 0;
                HB arm = l ? HB.ArmL : HB.ArmR, fore = l ? HB.ForeL : HB.ForeR, hand = l ? HB.HandL : HB.HandR;
                Vector3 S = p[(int)arm], E = p[(int)fore], Wr = p[(int)hand];
                float ar = Mathf.Lerp(1f, bw, 0.7f) * (s.female ? 0.86f : 1f) * (s.coat ? 1.12f : 1f);
                Color32 lower = s.rolledSleeves && !s.coat ? s.skin : sleeve;
                var rings = new List<Vector3[]>(); var cols = new List<Color32>(); var ws = new List<BoneWeight>();
                void A(Vector3 c, float r, Color32 col, BoneWeight w) { rings.Add(Ell(c.x, c.y, c.z, r * ar, r * ar, 8, 2f)); cols.Add(col); ws.Add(w); }
                A(S + new Vector3(-side * 0.01f, 0.012f, 0), 0.05f, sleeve, W(HB.Chest, 0.5f, arm));
                A(Vector3.Lerp(S, E, 0.15f), 0.054f, sleeve, W(arm));
                A(Vector3.Lerp(S, E, 0.55f), 0.05f, sleeve, W(arm));
                A(Vector3.Lerp(S, E, 0.88f), 0.043f, sleeve, W(arm));
                A(E, 0.041f, sleeve, W(arm, 0.5f, fore));
                A(Vector3.Lerp(E, Wr, 0.12f), 0.041f, lower, W(fore));
                A(Vector3.Lerp(E, Wr, 0.5f), 0.037f, lower, W(fore));
                A(Vector3.Lerp(E, Wr, 0.86f), 0.030f, lower, W(fore));
                A(Wr, 0.027f, s.skin, W(fore, 0.5f, hand));
                b.Loft(rings, cols, ws);
                b.Col(sleeve); b.Bones((int)HB.Chest, 0.45f, (int)arm);
                b.Ellipsoid(S + new Vector3(-side * 0.008f, -0.016f, 0), new Vector3(0.054f, 0.056f, 0.058f) * ar, 8, 5);
                b.Col(s.skin); b.Bone((int)hand);
                b.Ellipsoid(Wr + new Vector3(0, -0.055f, 0.008f), new Vector3(0.025f, 0.052f, 0.038f), 8, 5);
                b.Ellipsoid(Wr + new Vector3(-side * 0.012f, -0.04f, 0.04f), new Vector3(0.014f, 0.026f, 0.016f), 6, 4);

                HB thigh = l ? HB.ThighL : HB.ThighR, shin = l ? HB.ShinL : HB.ShinR, foot = l ? HB.FootL : HB.FootR, toe = l ? HB.ToeL : HB.ToeR;
                Vector3 H = p[(int)thigh], K = p[(int)shin], An = p[(int)foot];
                float lr = Mathf.Lerp(1f, bw, 0.6f);
                rings = new List<Vector3[]>(); cols = new List<Color32>(); ws = new List<BoneWeight>();
                void L(Vector3 c, float r, Color32 col, BoneWeight w) { rings.Add(Ell(c.x, c.y, c.z, r * lr, r * lr, 8, 2f)); cols.Add(col); ws.Add(w); }
                L(H + new Vector3(0, 0.0f, 0), 0.086f, s.pants, W(HB.Hips, 0.5f, thigh));
                L(Vector3.Lerp(H, K, 0.2f), 0.084f, s.pants, W(thigh));
                L(Vector3.Lerp(H, K, 0.55f), 0.073f, s.pants, W(thigh));
                L(Vector3.Lerp(H, K, 0.88f), 0.060f, s.pants, W(thigh));
                L(K, 0.057f, s.pants, W(thigh, 0.5f, shin));
                L(Vector3.Lerp(K, An, 0.14f), 0.054f, s.pants, W(shin));
                L(Vector3.Lerp(K, An, 0.3f), 0.055f, s.pants, W(shin));
                L(Vector3.Lerp(K, An, 0.31f), 0.060f, s.boots, W(shin));
                L(Vector3.Lerp(K, An, 0.7f), 0.050f, s.boots, W(shin));
                L(An + new Vector3(0, 0.02f, 0), 0.045f, s.boots, W(shin, 0.6f, foot));
                b.Loft(rings, cols, ws);

                // boot
                rings = new List<Vector3[]>(); cols = new List<Color32>(); ws = new List<BoneWeight>();
                void F(float z, float y, float rx, float ry, BoneWeight w)
                {
                    rings.Add(MeshBuilder.Ring(new Vector3(An.x, y, An.z + z), Vector3.right, Vector3.up, rx, ry, 8));
                    cols.Add(s.boots); ws.Add(w);
                }
                F(-0.075f, 0.047f, 0.034f, 0.047f, W(foot));
                F(-0.01f, 0.058f, 0.044f, 0.058f, W(foot));
                F(0.07f, 0.042f, 0.047f, 0.042f, W(foot, 0.6f, toe));
                F(0.135f, 0.032f, 0.046f, 0.032f, W(toe));
                F(0.195f, 0.024f, 0.030f, 0.024f, W(toe));
                b.Loft(rings, cols, ws);
            }

            // ---------------------------------------------------------------- clothes
            if (s.vest) OpenShell(b, rows, 3, 9, 1.07f, 0.012f, 14f, s.vestColor, null);
            if (s.coat)
            {
                var coat = new List<Row>
                {
                    new Row { y = 0.56f, rx = 0.215f * hw, rz = 0.150f, z = -0.01f, w = W(HB.Hips) },
                    new Row { y = 0.74f, rx = 0.200f * hw, rz = 0.138f, z = -0.005f, w = W(HB.Hips) },
                    new Row { y = 0.90f, rx = 0.180f * hw, rz = 0.122f, z = 0f, w = W(HB.Hips) },
                };
                for (int i = 2; i <= 9; i++) coat.Add(rows[i]);
                OpenShell(b, coat, 0, coat.Count - 1, 1.085f, 0.014f, 10f, s.coatColor, (row, x) =>
                {
                    if (row.y > 0.92f) return row.w;
                    float t = Mathf.InverseLerp(0.92f, 0.56f, row.y) * 0.45f;
                    return W(HB.Hips, 1f - t, x < 0f ? HB.ThighL : HB.ThighR);
                });
                // collar
                b.Col(Pal.Shade(s.coatColor, 0.85f));
                OpenShell(b, new List<Row>
                {
                    new Row { y = 1.49f, rx = 0.085f, rz = 0.078f, z = 0f, w = W(HB.Chest, 0.6f, HB.Neck) },
                    new Row { y = 1.545f, rx = 0.074f, rz = 0.072f, z = -0.004f, w = W(HB.Neck) },
                }, 0, 1, 1.1f, 0.01f, 40f, Pal.Shade(s.coatColor, 0.85f), null);
            }
            if (s.skirt)
            {
                var sk = new List<Row>
                {
                    new Row { y = 1.07f, rx = 0.152f, rz = 0.102f, z = 0f, c = s.skirtColor, w = W(HB.Hips, 0.5f, HB.Spine) },
                    new Row { y = 0.95f, rx = 0.195f * hw, rz = 0.150f, z = 0f, c = s.skirtColor, w = W(HB.Hips) },
                    new Row { y = 0.70f, rx = 0.250f, rz = 0.215f, z = 0f, c = s.skirtColor, w = W(HB.Hips) },
                    new Row { y = 0.40f, rx = 0.300f, rz = 0.275f, z = 0f, c = s.skirtColor, w = W(HB.Hips) },
                    new Row { y = 0.13f, rx = 0.335f, rz = 0.315f, z = 0f, c = Pal.Shade(s.skirtColor, 0.85f), w = W(HB.Hips) },
                };
                LoftRows(b, sk, 1f, 0f, true, 2f);
            }
            if (s.apron)
            {
                b.Col(Pal.ClothWhite);
                float top = 0.118f + s.belly * 0.09f;
                float low = (s.skirt ? 0.245f : 0.14f) + s.belly * 0.05f;
                b.Bones((int)HB.Hips, 0.75f, (int)HB.Spine);
                b.Beam(new Vector3(0, 1.09f, top), new Vector3(0, 0.62f, low), 0.26f, 0.008f, Vector3.forward);
                b.Bone((int)HB.Chest);
                b.Beam(new Vector3(0, 1.36f, 0.142f), new Vector3(0, 1.09f, top), 0.17f, 0.008f, Vector3.forward);
            }
            if (s.suspenders)
            {
                b.Col(Pal.LeatherDark);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 0.075f;
                    b.Bone((int)HB.Chest);
                    b.Beam(new Vector3(x, 1.10f, 0.105f + s.belly * 0.08f), new Vector3(x * 1.15f, 1.33f, 0.132f), 0.028f, 0.008f, Vector3.forward);
                    b.Beam(new Vector3(x * 1.15f, 1.33f, 0.132f), new Vector3(x * 1.2f, 1.475f, 0.03f), 0.028f, 0.008f, Vector3.forward);
                    b.Beam(new Vector3(x * 1.2f, 1.475f, 0.03f), new Vector3(x * 0.6f, 1.36f, -0.118f), 0.028f, 0.008f, Vector3.back);
                    b.Beam(new Vector3(x * 0.6f, 1.36f, -0.118f), new Vector3(0, 1.10f, -0.102f), 0.028f, 0.008f, Vector3.back);
                }
            }
            if (s.gunbelt)
            {
                b.Col(Pal.Leather);
                LoftRows(b, new List<Row>
                {
                    new Row { y = 0.955f, rx = 0.172f * hw + 0.012f, rz = 0.118f, z = 0f, c = Pal.Leather, w = W(HB.Hips) },
                    new Row { y = 1.005f, rx = 0.166f * hw + 0.012f, rz = 0.115f, z = 0f, c = Pal.Leather, w = W(HB.Hips) },
                }, 1f, 0f, false);
                b.Col(Pal.LeatherDark); b.Bones((int)HB.ThighR, 0.6f, (int)HB.Hips);
                b.Box(new Vector3(0.155f * hw + 0.01f, 0.86f, 0.015f), new Vector3(0.04f, 0.17f, 0.075f));
            }
            if (s.bandana)
            {
                LoftRows(b, new List<Row>
                {
                    new Row { y = 1.495f, rx = 0.082f, rz = 0.076f, z = 0f, c = s.bandanaColor, w = W(HB.Chest, 0.5f, HB.Neck) },
                    new Row { y = 1.535f, rx = 0.062f, rz = 0.064f, z = 0.003f, c = s.bandanaColor, w = W(HB.Neck) },
                }, 1f, 0f, false);
                b.Col(s.bandanaColor); b.Bone((int)HB.Chest);
                b.QuadDouble(new Vector3(-0.07f, 1.505f, 0.07f), new Vector3(0.07f, 1.505f, 0.07f),
                             new Vector3(0.004f, 1.40f, 0.128f), new Vector3(-0.004f, 1.40f, 0.128f));
            }
            if (s.badge)
            {
                b.Col(Pal.Tin, 1f); b.Bone((int)HB.Chest);
                b.Cyl(new Vector3(-0.085f, 1.36f, 0.118f), new Vector3(-0.085f, 1.36f, 0.128f), 0.022f, 0.022f, 5);
            }

            // ---------------------------------------------------------------- assemble
            for (int i = 0; i < b.verts.Count; i++)
            {
                b.verts[i] *= k;
                Vector4 uv = b.uvs[i];
                b.uvs[i] = new Vector4(uv.x * k, uv.y * k, uv.z * k, uv.w);
            }

            var rig = new HumanRig { spec = s, k = k };
            rig.go = new GameObject(name);
            rig.root = rig.go.transform;
            rig.bones = new Transform[(int)HB.Count];
            rig.bind = new Vector3[(int)HB.Count];
            rig.bindLocal = new Vector3[(int)HB.Count];
            for (int i = 0; i < (int)HB.Count; i++)
            {
                var bone = new GameObject(((HB)i).ToString()).transform;
                rig.bones[i] = bone;
                rig.bind[i] = p[i] * k;
            }
            var poses = new Matrix4x4[(int)HB.Count];
            for (int i = 0; i < (int)HB.Count; i++)
            {
                Transform parent = Parent[i] == HB.Count ? rig.root : rig.bones[(int)Parent[i]];
                Vector3 parentPos = Parent[i] == HB.Count ? Vector3.zero : rig.bind[(int)Parent[i]];
                rig.bones[i].SetParent(parent, false);
                rig.bindLocal[i] = rig.bind[i] - parentPos;
                rig.bones[i].localPosition = rig.bindLocal[i];
                poses[i] = Matrix4x4.Translate(-rig.bind[i]);
            }

            // people made in Blender when this build has them; the code-built figure otherwise
            bool modelled = HumanModel.Available;
            Mesh mesh = modelled ? HumanModel.Build(s, k, p, Reference, name) : b.ToMesh(name);
            mesh.bindposes = poses;
            var body = new GameObject("Body");
            body.transform.SetParent(rig.root, false);
            rig.smr = body.AddComponent<SkinnedMeshRenderer>();
            rig.smr.sharedMesh = mesh;
            rig.smr.bones = rig.bones;
            rig.smr.rootBone = rig.bones[(int)HB.Hips];
            rig.smr.sharedMaterial = modelled ? Mats.SurfaceUV : Mats.Lit;
            rig.smr.localBounds = new Bounds(Vector3.zero, new Vector3(2.6f, 2.6f, 2.6f));
            rig.smr.updateWhenOffscreen = Args.Offscreen;   // tests render on demand, so skin every frame
            rig.smr.quality = SkinQuality.Bone2;

            rig.armU = Vector3.Distance(rig.Bind(HB.ArmR), rig.Bind(HB.ForeR));
            rig.armF = Vector3.Distance(rig.Bind(HB.ForeR), rig.Bind(HB.HandR));
            rig.legU = Vector3.Distance(rig.Bind(HB.ThighR), rig.Bind(HB.ShinR));
            rig.legF = Vector3.Distance(rig.Bind(HB.ShinR), rig.Bind(HB.FootR));
            rig.hipHeight = rig.Bind(HB.Hips).y;
            rig.footHeight = rig.Bind(HB.FootR).y;
            rig.eyeHeight = HeadPoint(new Vector3(0, 1.70f, 0)).y * k;

            rig.gripR = Attach(rig.B(HB.HandR), "GripR", new Vector3(0, -0.055f, 0.012f) * k);
            rig.gripL = Attach(rig.B(HB.HandL), "GripL", new Vector3(0, -0.055f, 0.012f) * k);
            rig.holster = Attach(rig.B(HB.ThighR), "Holster", (new Vector3(0.155f * hw + 0.03f, 0.89f, 0.02f) - p[(int)HB.ThighR]) * k);
            rig.back = Attach(rig.B(HB.Chest), "Back", new Vector3(0.02f, 0.12f, -0.135f) * k);
            rig.eyes = Attach(rig.B(HB.Head), "Eyes", (HeadPoint(new Vector3(0, 1.70f, 0.09f)) - p[(int)HB.Head]) * k);

            if (s.hat != HatKind.None)
            {
                rig.hat = BuildHat(s.hat, s.hatColor);
                rig.hat.transform.SetParent(rig.B(HB.Head), false);
                rig.hat.transform.localPosition = (HeadPoint(new Vector3(0, 1.752f, -0.004f)) - p[(int)HB.Head]) * k;
                rig.hat.transform.localScale = Vector3.one * (k * HeadScale);
            }

            // face mask (bandana pulled up), hidden until the wearer covers the face
            var mb = new MeshBuilder();
            mb.Col(s.bandana ? s.bandanaColor : Pal.ClothBlack);
            Shell(mb, new Vector3(0, 1.632f, 0.012f), new Vector3(0.08f, 0.062f, 0.098f), 12, 6, v => v.z > 0f && v.y < 1.68f);
            rig.mask = mb.ToObject("Mask", rig.B(HB.Head), Mats.LitDouble);
            rig.mask.transform.localPosition = (HeadPoint(Vector3.zero) - p[(int)HB.Head]) * k;
            rig.mask.transform.localScale = Vector3.one * (k * HeadScale);
            rig.mask.SetActive(false);
            return rig;
        }

        static Transform Attach(Transform bone, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(bone, false);
            t.localPosition = local;
            return t;
        }

        static void LoftRows(MeshBuilder b, List<Row> rows, float scale, float grow, bool caps = true, float sq = 2.3f)
        {
            var rings = new List<Vector3[]>(); var cols = new List<Color32>(); var ws = new List<BoneWeight>();
            foreach (Row r in rows)
            {
                rings.Add(Ell(0, r.y, r.z, r.rx * scale + grow, r.rz * scale + grow, N, sq));
                cols.Add(r.c); ws.Add(r.w);
            }
            b.Loft(rings, cols, ws, caps, caps);
        }

        /// <summary>
        /// An open, double-sided shell wrapped around torso rows (vest, coat): everything except a gap of
        /// 'gapDeg' either side of the front centre line.
        /// </summary>
        static void OpenShell(MeshBuilder b, List<Row> rows, int from, int to, float scale, float grow, float gapDeg,
                              Color32 col, Func<Row, float, BoneWeight> weightAt)
        {
            const int M = 16;
            float a0 = (90f + gapDeg) * Mathf.Deg2Rad, a1 = (450f - gapDeg) * Mathf.Deg2Rad;
            const float e = 2f / 2.3f;
            Color32 keep = b.color; BoneWeight keepW = b.weight;
            b.color = col;
            int count = to - from + 1;
            for (int pass = 0; pass < 2; pass++)
            {
                int start = b.verts.Count;
                int t0 = b.tris.Count;
                for (int j = 0; j < count; j++)
                {
                    Row r = rows[from + j];
                    for (int i = 0; i <= M; i++)
                    {
                        float a = Mathf.Lerp(a0, a1, i / (float)M);
                        float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                        float x = (r.rx * scale + grow) * Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), e);
                        float z = r.z + (r.rz * scale + grow) * Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e);
                        b.weight = weightAt != null ? weightAt(r, x) : r.w;
                        b.Vert(new Vector3(x, r.y, z), Vector3.up);
                    }
                }
                int row = M + 1;
                for (int j = 0; j < count - 1; j++)
                    for (int i = 0; i < M; i++)
                    {
                        int k = start + j * row + i;
                        if (pass == 0) { b.Tri(k, k + row + 1, k + row); b.Tri(k, k + 1, k + row + 1); }
                        else { b.Tri(k, k + row, k + row + 1); b.Tri(k, k + row + 1, k + 1); }
                    }
                b.SmoothNormals(start, b.verts.Count, t0, b.tris.Count);
            }
            b.color = keep; b.weight = keepW;
        }

        /// <summary>Part of an ellipsoid: only triangles whose centre passes 'keep' are emitted.</summary>
        static void Shell(MeshBuilder b, Vector3 c, Vector3 radii, int seg, int rings, Func<Vector3, bool> keep)
        {
            int v0 = b.verts.Count, t0 = b.tris.Count;
            b.Ellipsoid(c, radii, seg, rings);
            var kept = new List<int>();
            for (int t = t0; t < b.tris.Count; t += 3)
            {
                Vector3 mid = (b.verts[b.tris[t]] + b.verts[b.tris[t + 1]] + b.verts[b.tris[t + 2]]) / 3f;
                if (keep(mid)) { kept.Add(b.tris[t]); kept.Add(b.tris[t + 1]); kept.Add(b.tris[t + 2]); }
            }
            b.tris.RemoveRange(t0, b.tris.Count - t0);
            b.tris.AddRange(kept);
        }

        // -------------------------------------------------------------------- hats

        public static GameObject BuildHat(HatKind kind, Color32 col)
        {
            string model = "hat_" + kind.ToString().ToLowerInvariant();
            if (Models.Has(model)) return HumanModel.Dyed(model, "Hat", HumanModel.Hat, col);
            SurfaceMap.Hint(col, kind == HatKind.Straw ? Surf.Straw : Surf.Wool);
            var b = new MeshBuilder();
            b.Col(col);
            Color32 band = Pal.Shade(col, 0.6f);
            switch (kind)
            {
                case HatKind.Cowboy:
                    Brim(b, 0.085f, 0.105f, 0.18f, 0.225f, 0.038f, 0f);
                    Crown(b, 0.082f, 0.102f, 0.115f, 0.86f, 0.018f);
                    Band(b, 0.084f, 0.104f, 0.004f, 0.028f, band);
                    break;
                case HatKind.Derby:
                    Brim(b, 0.085f, 0.105f, 0.125f, 0.145f, 0.018f, 0.012f);
                    Crown(b, 0.082f, 0.102f, 0.105f, 0.9f, 0f, true);
                    Band(b, 0.084f, 0.104f, 0.004f, 0.022f, band);
                    break;
                case HatKind.FlatBrim:
                    Brim(b, 0.085f, 0.105f, 0.20f, 0.215f, 0f, 0f);
                    Crown(b, 0.082f, 0.102f, 0.095f, 0.92f, 0f);
                    Band(b, 0.084f, 0.104f, 0.004f, 0.025f, band);
                    break;
                case HatKind.Straw:
                    Brim(b, 0.085f, 0.105f, 0.24f, 0.26f, 0.02f, -0.02f);
                    Crown(b, 0.082f, 0.102f, 0.085f, 0.8f, 0f, true);
                    Band(b, 0.084f, 0.104f, 0.004f, 0.02f, Pal.ClothRed);
                    break;
                case HatKind.Kepi:
                    Crown(b, 0.083f, 0.10f, 0.07f, 0.92f, 0f);
                    b.Col(Pal.LeatherDark, 0.6f);
                    b.Box(new Vector3(0, 0.004f, 0.125f), new Vector3(0.13f, 0.008f, 0.07f), Quaternion.Euler(12f, 0, 0));
                    break;
                case HatKind.Bonnet:
                    b.Ellipsoid(new Vector3(0, -0.035f, -0.025f), new Vector3(0.095f, 0.10f, 0.115f), 10, 6);
                    Brim(b, 0.085f, 0.10f, 0.115f, 0.14f, 0f, -0.05f);
                    break;
            }
            return b.ToObject("Hat", null, Mats.LitDouble);
        }

        /// <summary>Brim from the head ellipse (r0x, r0z) out to (r1x, r1z); 'curl' lifts the sides, 'droop' the rim.</summary>
        static void Brim(MeshBuilder b, float r0x, float r0z, float r1x, float r1z, float curl, float droop)
        {
            const int seg = 20, steps = 4;
            int start = b.verts.Count, t0 = b.tris.Count;
            for (int j = 0; j <= steps; j++)
            {
                float t = j / (float)steps;
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float y = curl * t * t * cs * cs + droop * t * t;
                    b.Vert(new Vector3(cs * Mathf.Lerp(r0x, r1x, t), y, sn * Mathf.Lerp(r0z, r1z, t)), Vector3.up);
                }
            }
            for (int j = 0; j < steps; j++)
                for (int i = 0; i < seg; i++)
                {
                    int k = start + j * seg + i, k1 = start + j * seg + (i + 1) % seg;
                    b.Tri(k, k + seg, k1 + seg);
                    b.Tri(k, k1 + seg, k1);
                }
            b.SmoothNormals(start, b.verts.Count, t0, b.tris.Count);
        }

        static void Crown(MeshBuilder b, float rx, float rz, float height, float taper, float pinch, bool round = false)
        {
            var rings = new List<Vector3[]>();
            int steps = round ? 5 : 3;
            for (int j = 0; j <= steps; j++)
            {
                float t = j / (float)steps;
                float s = round ? Mathf.Sqrt(Mathf.Max(0.02f, 1f - t * t * 0.96f)) : Mathf.Lerp(1f, taper, t);
                rings.Add(MeshBuilder.Ring(new Vector3(0, height * t, 0), Vector3.right, Vector3.forward, rx * s, rz * s, 14));
            }
            if (pinch > 0f)
            {
                Vector3[] top = rings[rings.Count - 1];
                for (int i = 0; i < top.Length; i++)
                {
                    float f = Mathf.Abs(top[i].x) / (rx * taper);
                    top[i].y -= pinch * (1f - f);
                }
            }
            b.Loft(rings, null, null, false, true);
        }

        static void Band(MeshBuilder b, float rx, float rz, float y0, float y1, Color32 col)
        {
            Color32 keep = b.color;
            b.color = col;
            b.Loft(new List<Vector3[]>
            {
                MeshBuilder.Ring(new Vector3(0, y0, 0), Vector3.right, Vector3.forward, rx, rz, 14),
                MeshBuilder.Ring(new Vector3(0, y1, 0), Vector3.right, Vector3.forward, rx * 0.985f, rz * 0.985f, 14),
            }, null, null, false, false);
            b.color = keep;
        }
    }
}
