using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>What stands smoking at a point of the world.</summary>
    public enum Plume { Chimney, Campfire, Forge, Engine, Steam }

    /// <summary>
    /// Standing smoke, fire and steam: stovepipes, the camp fire, forges, the engine at the station. Each source
    /// feeds the pooled particles of Fx while the camera is near enough to see it; the wind leans the plumes and the
    /// hour darkens them.
    /// </summary>
    public static class Emitters
    {
        class Source { public Vector3 p; public Plume kind; public float due, seed; }

        static readonly List<Source> sources = new List<Source>();
        static readonly int WindId = Shader.PropertyToID("_CanaanWind");
        const float Range = 320f;
        // their own dice: the game's Random also decides fights and loot, and smoke on the skyline must not change those
        static readonly System.Random dice = new System.Random(4711);
        static float Rnd(float a, float b) { return a + (float)dice.NextDouble() * (b - a); }
        static float Rnd01 => (float)dice.NextDouble();
        static Vector3 RndSphere { get { return new Vector3(Rnd(-1f, 1f), Rnd(-1f, 1f), Rnd(-1f, 1f)) * 0.7f; } }
        static Vector2 RndCircle { get { float a = Rnd(0f, 6.2832f), r = Mathf.Sqrt(Rnd01); return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); } }

        public static int Count => sources.Count;
        static readonly bool off = Args.Has("-nofx");          // for measuring what the smoke costs

        static readonly List<AudioSource> crackle = new List<AudioSource>();

        public static void Clear()
        {
            sources.Clear();
            foreach (AudioSource a in crackle) if (a != null) UnityEngine.Object.Destroy(a.gameObject);
            crackle.Clear();
        }

        public static void Add(Vector3 worldPos, Plume kind)
        {
            sources.Add(new Source { p = worldPos, kind = kind, seed = sources.Count * 1.618f });
            // a camp fire crackles; so does a forge's
            if ((kind == Plume.Campfire || kind == Plume.Forge) && Sfx.Ready)
                crackle.Add(Sfx.Emitter("fire", worldPos - Vector3.up * 0.4f, kind == Plume.Campfire ? 0.85f : 0.6f, 26f));
        }

        static Color32 Tone(Color32 c, float k, float alpha)
        {
            return new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), (byte)(c.a * alpha));
        }

        public static void Tick(float dt)
        {
            if (dt <= 0f || Game.I == null || Game.I.cam == null || off) return;
            Vector3 eye = Game.I.cam.transform.position;
            Vector4 w = Shader.GetGlobalVector(WindId);
            Vector3 wind = new Vector3(w.x, 0f, w.y) * (w.z * 9f);              // metres a second: a breath to a gale
            // smoke takes the light of the hour: pale by day, a dark smudge against the stars
            float day = Game.I.clock != null ? Mathf.Clamp01(Game.I.clock.Daylight) : 1f;
            float lit = Mathf.Lerp(0.2f, 1f, day);
            float hour = Game.I.clock != null ? Game.I.clock.hour : 12f;
            // stoves are stoked morning and evening
            float stove = 0.55f + 0.45f * Mathf.Max(Mathf.Exp(-(hour - 7f) * (hour - 7f) / 6f), Mathf.Exp(-(hour - 19.5f) * (hour - 19.5f) / 8f));
            float t = Time.time;
            foreach (Source s in sources)
            {
                float d2 = (s.p - eye).sqrMagnitude;
                if (d2 > Range * Range) { s.due = 0f; continue; }
                float gust = 0.8f + 0.2f * Mathf.Sin(t * 0.6f + s.seed * 3f);
                switch (s.kind)
                {
                    case Plume.Chimney:
                        s.due += dt * 4.5f * stove;
                        while (s.due >= 1f)
                        {
                            s.due -= 1f;
                            Fx.Plume(s.p + RndSphere * 0.04f, Vector3.up * Rnd(0.55f, 0.9f) + wind * gust + RndSphere * 0.1f,
                                     Rnd(0.24f, 0.36f), Tone(new Color32(210, 206, 200, 118), lit, 1f), Rnd(4.5f, 7f));
                        }
                        break;
                    case Plume.Engine:
                        s.due += dt * 4f;
                        while (s.due >= 1f)
                        {
                            s.due -= 1f;
                            // an engine standing with banked fire: grey wood smoke drifting off, not the black column of one at work
                            Fx.Plume(s.p + RndSphere * 0.12f, Vector3.up * Rnd(0.8f, 1.3f) + wind * gust * 1.3f + RndSphere * 0.3f,
                                     Rnd(0.5f, 0.75f), Tone(new Color32(168, 163, 156, 92), Mathf.Lerp(0.3f, 1f, day), 1f), Rnd(5f, 7.5f));
                        }
                        break;
                    case Plume.Steam:
                        s.due += dt * 2.2f;
                        while (s.due >= 1f)
                        {
                            s.due -= 1f;
                            Fx.Plume(s.p + RndSphere * 0.05f, Vector3.up * Rnd(0.35f, 0.7f) + wind * 0.6f + RndSphere * 0.25f,
                                     Rnd(0.12f, 0.2f), Tone(new Color32(236, 236, 232, 78), Mathf.Lerp(0.4f, 1f, day), 1f), Rnd(1.1f, 1.9f));
                        }
                        break;
                    case Plume.Campfire:
                    case Plume.Forge:
                    {
                        bool forge = s.kind == Plume.Forge;
                        if (d2 > 140f * 140f) break;                                    // flames only where they can be seen
                        float spread = forge ? 0.12f : 0.2f, big = forge ? 0.7f : 1.45f;
                        s.due += dt * (forge ? 14f : 22f);
                        while (s.due >= 1f)
                        {
                            s.due -= 1f;
                            Vector2 o = RndCircle * spread;
                            float heat = Rnd01;
                            Fx.Flame(s.p + new Vector3(o.x, Rnd(0f, 0.08f), o.y), Vector3.up * Rnd(0.5f, 1.3f) * big + wind * 0.15f + new Vector3(-o.x, 0f, -o.y) * 0.8f,
                                     Rnd(0.2f, 0.36f) * big, new Color32(255, (byte)Mathf.Lerp(120f, 215f, heat), (byte)Mathf.Lerp(30f, 110f, heat * heat), 150), Rnd(0.3f, 0.6f) * (forge ? 1f : 1.25f));
                            if (Rnd01 < 0.07f)
                                Fx.Ember(s.p + new Vector3(o.x, 0.2f, o.y), Vector3.up * Rnd(1.2f, 2.4f) + wind * 0.5f + RndSphere * 0.5f,
                                         new Color32(255, 170, 70, 255), Rnd(0.8f, 1.7f));
                            if (!forge && Rnd01 < 0.14f)
                                Fx.Plume(s.p + new Vector3(o.x, 0.5f, o.y), Vector3.up * Rnd(0.7f, 1.1f) + wind * gust + RndSphere * 0.1f,
                                         Rnd(0.26f, 0.38f), Tone(new Color32(170, 164, 156, 70), lit, 1f), Rnd(4f, 6f));
                        }
                        break;
                    }
                }
            }
        }
    }
}
