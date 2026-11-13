using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// The game's sound. Recorded clips where there are any (Resources/Sounds, made by Tools/sounds.sh from the
    /// recordings listed in Tools/sound/SOURCES.md: 'id__n' are the variants of a one-shot, 'id' a loop), sounds
    /// synthesised at start-up for the rest (the engine's chuff, whistle and bell, the horse call, the bow...).
    /// One-shots play through a pool of 3D voices that carry as sound does in the open - whole within a near field,
    /// then halving as the distance doubles, gone at their reach - and lose their highs far off. A shot is answered
    /// by the land: a slap off the nearest wall, a long roll off the hills. Feet and hooves sound what they come down
    /// on: boards, grass, or dirt and sand. Running water and fires are 3D loops placed in the world.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 32000;
        const float Near = 0.1f;                      // the near field, a share of a sound's reach
        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        static readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
        static AudioSource[] pool;
        static AudioLowPassFilter[] dull;
        static AudioSource ui, river;
        static int next;
        static Transform root;
        static AnimationCurve falloff;
        static System.Random rng = new System.Random(11);
        public static bool Ready;
        public static float Volume = 1f;
        /// <summary>How many sounds come from recordings (the rest are synthesised).</summary>
        public static int Recorded { get; private set; }
        /// <summary>Grass on the ground at (x, z), 0..1 (the region sets it): feet swish through it.</summary>
        public static Func<float, float, float> Grass;
        /// <summary>The nearest running water to a point and whether there is any within earshot (the region sets it).</summary>
        public delegate bool WaterNear(Vector3 p, out Vector3 at);
        public static WaterNear Water;

        public static void Init(Transform parent)
        {
            root = parent;
            falloff = Falloff();
            pool = new AudioSource[28];
            dull = new AudioLowPassFilter[pool.Length];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(parent, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Custom;
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, falloff);
                s.dopplerLevel = 0f;
                pool[i] = s;
                dull[i] = go.AddComponent<AudioLowPassFilter>();
                dull[i].cutoffFrequency = 22000f;
            }
            var ugo = new GameObject("SfxUI");
            ugo.transform.SetParent(parent, false);
            ui = ugo.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
            ui.ignoreListenerPause = true;
            if (Args.IsTest && !Args.Has("-audio")) return;
            if (Args.Has("-mute")) AudioListener.volume = 0f;       // all the work of sound, nothing out of the speakers (measuring)
            Build();
            Recorded = LoadRecorded();
            Ready = true;
            Debug.Log("CANAAN sound: " + clips.Count + " sounds, " + Recorded + " recorded");
        }

        public static bool Has(string id) { return clips.ContainsKey(id); }

        /// <summary>A random number from the sound's own generator: the game's (UnityEngine.Random) is left alone, so
        /// whether sound is on never changes what people and animals decide.</summary>
        public static float Vary(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        static Vector2 Around(float radius)
        {
            float a = Vary(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        /// <summary>Recorded clips take the place of the synthesised ones of the same name.</summary>
        static int LoadRecorded()
        {
            var sets = new Dictionary<string, List<AudioClip>>();
            foreach (AudioClip c in Resources.LoadAll<AudioClip>("Sounds"))
            {
                int k = c.name.IndexOf("__", StringComparison.Ordinal);
                string id = k > 0 ? c.name.Substring(0, k) : c.name;
                if (!sets.TryGetValue(id, out List<AudioClip> list)) sets[id] = list = new List<AudioClip>();
                list.Add(c);
            }
            foreach (var kv in sets)
            {
                kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                clips[kv.Key] = kv.Value.ToArray();
            }
            return sets.Count;
        }

        /// <summary>Strength over distance, as a share of the reach: whole in the near field, then halving as the
        /// distance doubles, faded out over the last third.</summary>
        static AnimationCurve Falloff()
        {
            float[] xs = { 0f, 0.05f, Near, 0.12f, 0.15f, 0.2f, 0.27f, 0.35f, 0.45f, 0.55f, 0.66f, 0.75f, 0.85f, 0.93f, 1f };
            var keys = new Keyframe[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i];
                float g = x <= Near ? 1f : Near / x;
                keys[i] = new Keyframe(x, g * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 1f, x))));
            }
            var c = new AnimationCurve(keys);
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        static Vector3 Ear()
        {
            Camera c = Camera.main;
            return c != null ? c.transform.position : Vector3.zero;
        }

        /// <summary>A variant, never the one played last time.</summary>
        static AudioClip Pick(string id, AudioClip[] set)
        {
            if (set.Length == 1) return set[0];
            bool had = lastPick.TryGetValue(id, out int prev);
            int i = rng.Next(had ? set.Length - 1 : set.Length);
            if (had && i >= prev) i++;
            lastPick[id] = i;
            return set[i];
        }

        /// <summary>A free voice, or the oldest one if all are busy.</summary>
        static int Free()
        {
            for (int k = 0; k < pool.Length; k++)
            {
                int i = (next + k) % pool.Length;
                if (!pool[i].isPlaying) { next = (i + 1) % pool.Length; return i; }
            }
            int j = next;
            next = (next + 1) % pool.Length;
            return j;
        }

        static AudioSource Voice(AudioClip clip, Vector3 pos, float volume, float pitch, float reach, float dist, float delay, float blend = 1f, float cutoff = 22000f)
        {
            int i = Free();
            AudioSource s = pool[i];
            s.Stop();
            s.transform.position = pos;
            s.clip = clip;
            s.volume = volume * Volume;
            s.pitch = pitch;
            s.spatialBlend = blend;
            s.maxDistance = reach;
            s.minDistance = reach * Near;
            // the air takes the highs off a far sound
            dull[i].cutoffFrequency = Mathf.Min(cutoff, Mathf.Clamp(22000f / (1f + dist / 30f), 1200f, 22000f));
            if (delay > 0f) s.PlayDelayed(delay); else s.Play();
            return s;
        }

        // ------------------------------------------------------------ playback

        public static AudioSource Play(string id, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 40f, float delay = 0f)
        {
            if (!Ready || !clips.TryGetValue(id, out AudioClip[] set)) return null;
            float d = Vector3.Distance(Ear(), pos);
            if (d > maxDistance) return null;
            return Voice(Pick(id, set), pos, volume, pitch * Vary(0.96f, 1.04f), maxDistance, d, delay);
        }

        public static void Play2D(string id, float volume = 1f, float pitch = 1f)
        {
            if (!Ready || !clips.TryGetValue(id, out AudioClip[] set)) return;
            ui.pitch = pitch;
            ui.PlayOneShot(Pick(id, set), volume * Volume);
        }

        /// <summary>A gunshot. Close by the near take, past 45 m the far one (the report with the land's answer in
        /// it); then the land answers here as well - a slap off the nearest wall or slope, a long dull roll off the
        /// hills, both late by the way the sound goes.</summary>
        public static void Shot(string id, Vector3 pos, float volume, float maxDistance)
        {
            if (!Ready) return;
            if (!clips.ContainsKey(id)) id = "shot_rifle";
            Vector3 ear = Ear();
            float d = Vector3.Distance(ear, pos);
            if (d > maxDistance || !clips.ContainsKey(id)) return;
            string take = d > 45f && clips.ContainsKey(id + "_far") ? id + "_far" : id;
            AudioClip c = Pick(take, clips[take]);
            Voice(c, pos, volume, Vary(0.97f, 1.03f), maxDistance, d, 0f);
            // in a running fight the voices go to the shots themselves, not to their echoes
            int busy = 0;
            for (int i = 0; i < pool.Length; i++) if (pool[i].isPlaying) busy++;
            if (busy > pool.Length - 6) return;
            float wall = -1f;
            for (int k = 0; k < 6; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 60f + Vary(0f, 60f), 0f) * Vector3.forward;
                if (Physics.Raycast(pos + Vector3.up * 0.6f, dir, out RaycastHit hit, 60f, Layers.SolidMask, QueryTriggerInteraction.Ignore)
                    && (wall < 0f || hit.distance < wall)) wall = hit.distance;
            }
            float left = Mathf.Clamp01(1f - d / maxDistance);
            if (wall > 2f)
            {
                Vector2 r = Around(20f);
                Voice(c, ear + new Vector3(r.x, 3f, r.y), volume * left * Mathf.Lerp(0.3f, 0.12f, wall / 60f), 0.99f, 400f, 20f,
                      wall * 2f / 343f + 0.02f, 0.6f, 3200f);
            }
            Vector2 h = Around(60f);
            Voice(c, ear + new Vector3(h.x, 8f, h.y), volume * left * 0.2f, 0.93f, 600f, 60f,
                  Vary(0.6f, 1.0f) + d / 343f * 0.3f, 0.4f, 900f);
        }

        public enum Ground { Dirt, Grass, Boards }

        /// <summary>What a foot or a hoof at 'pos' comes down on: anything built (on the Static layer) is boards, the
        /// land is grass where it grows thick, else dirt and sand.</summary>
        public static Ground GroundAt(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 0.35f, Vector3.down, out RaycastHit hit, 0.8f, Layers.SolidMask, QueryTriggerInteraction.Ignore)
                && hit.collider.gameObject.layer == Layers.Static) return Ground.Boards;
            return Grass != null && Grass(pos.x, pos.z) > 0.55f ? Ground.Grass : Ground.Dirt;
        }

        /// <summary>A footfall: boots on boards, through grass, on dirt.</summary>
        public static void Step(Vector3 pos, float volume, float maxDistance, float pitch = 1f)
        {
            if (!Ready) return;
            Ground g = GroundAt(pos);
            string id = g == Ground.Boards ? "step_wood" : g == Ground.Grass ? "step_grass" : "step_dirt";
            if (!clips.ContainsKey(id)) id = g == Ground.Boards ? "step_wood" : "step";
            Play(id, pos, volume * (g == Ground.Grass ? 0.8f : 1f), pitch, maxDistance);
        }

        /// <summary>A hoof coming down: hollow on boards, a thud on the land (softer in grass).</summary>
        public static void Hoof(Vector3 pos, float volume, float pitch, float maxDistance)
        {
            if (!Ready) return;
            Ground g = GroundAt(pos);
            string id = g == Ground.Boards && clips.ContainsKey("hoof_wood") ? "hoof_wood" : clips.ContainsKey("hoof_dirt") ? "hoof_dirt" : "hoof";
            Play(id, pos, volume * (g == Ground.Grass ? 0.75f : 1f), pitch, maxDistance);
        }

        /// <summary>A bullet striking: splinters from anything built, a thud in the land.</summary>
        public static void Impact(Vector3 pos, bool built)
        {
            if (built) Play("impact_wood", pos, 0.55f, 1f, 40f);
            else Play("step_dirt", pos, 0.5f, 0.6f, 30f);
        }

        /// <summary>A looping source for the caller to position and fade.</summary>
        public static AudioSource Loop(string id, Transform parent, bool spatial, float maxDistance = 30f)
        {
            var go = new GameObject("Loop_" + id);
            go.transform.SetParent(parent != null ? parent : root, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, falloff ?? Falloff());
            s.minDistance = maxDistance * Near;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            if (Ready && clips.TryGetValue(id, out AudioClip[] set))
            {
                s.clip = set[0];
                s.Play();
                if (s.clip.length > 1f) s.time = Vary(0f, s.clip.length * 0.9f);    // two fires never crackle in step
            }
            return s;
        }

        /// <summary>A loop standing in the world (a fire): plays on its own, at 'volume'.</summary>
        public static AudioSource Emitter(string id, Vector3 pos, float volume, float maxDistance)
        {
            AudioSource s = Loop(id, null, true, maxDistance);
            s.transform.position = pos;
            s.volume = volume * Volume;
            return s;
        }

        /// <summary>Per frame: the river sounds from the nearest reach of it.</summary>
        public static void Tick(float dt)
        {
            if (!Ready || Water == null) return;
            if (river == null) river = Loop("river", null, true, 110f);
            float want = 0f;
            if (Water(Ear(), out Vector3 at))
            {
                river.transform.position = at;
                want = 0.9f * Volume;
            }
            river.volume = Mathf.MoveTowards(river.volume, want, dt * 0.5f);
        }

        // ------------------------------------------------------------ synthesis

        static float[] Buf(float seconds) { return new float[Mathf.CeilToInt(seconds * Rate)]; }
        static float Rnd() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        static void Noise(float[] b, float amp, float decay)
        {
            for (int i = 0; i < b.Length; i++) b[i] += Rnd() * amp * Mathf.Exp(-i / (decay * Rate));
        }

        static void LowPass(float[] b, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] = y; }
        }

        static void HighPass(float[] b, float cutoff)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate), y = 0f;
            for (int i = 0; i < b.Length; i++) { y += a * (b[i] - y); b[i] -= y; }
        }

        static void Sweep(float[] b, float f0, float f1, float amp, float decay, float start = 0f, float length = -1f)
        {
            int i0 = (int)(start * Rate), n = length < 0f ? b.Length - i0 : Mathf.Min(b.Length - i0, (int)(length * Rate));
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate;
                float env = Mathf.Exp(-i / (decay * Rate)) * Mathf.Min(1f, i / (0.004f * Rate)) * Mathf.Min(1f, (n - i) / (0.004f * Rate));
                b[i0 + i] += Mathf.Sin(ph) * amp * env;
            }
        }

        static void Normalize(float[] b, float peak)
        {
            float m = 1e-6f;
            for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        /// <summary>Makes the end of a loop flow into its start.</summary>
        static void Seam(float[] b, float seconds)
        {
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                int tail = b.Length - n + i;
                b[tail] = b[tail] * (1f - t) + b[i] * t;
            }
        }

        static void Add(string id, params float[][] variants)
        {
            var arr = new AudioClip[variants.Length];
            for (int i = 0; i < variants.Length; i++)
            {
                arr[i] = AudioClip.Create(id + i, variants[i].Length, 1, Rate, false);
                arr[i].SetData(variants[i], 0);
            }
            clips[id] = arr;
        }

        static float[] Shot(float body, float crack, float tail, float thump)
        {
            float[] b = Buf(0.9f);
            float[] n = Buf(0.9f);
            Noise(n, 1f, 0.035f * body);
            LowPass(n, crack);
            float[] t = Buf(0.9f);
            Noise(t, 0.35f, tail);
            LowPass(t, 900f);
            for (int i = 0; i < b.Length; i++) b[i] = n[i] + t[i];
            Sweep(b, thump * 1.8f, thump, 0.9f, 0.05f);
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * 2.2f);
            Normalize(b, 0.95f);
            return b;
        }

        static float[] Step(float cutoff, float decay, float amp)
        {
            float[] b = Buf(0.18f);
            Noise(b, 1f, decay);
            LowPass(b, cutoff * (1f + Rnd() * 0.2f));
            Normalize(b, amp);
            return b;
        }

        static float[] LoopNoise(float seconds, float low, float high, Func<float, float> mod)
        {
            float[] b = Buf(seconds);
            for (int i = 0; i < b.Length; i++) b[i] = Rnd();
            if (high > 0f) LowPass(b, high);
            if (low > 0f) HighPass(b, low);
            if (mod != null) for (int i = 0; i < b.Length; i++) b[i] *= mod(i / (float)Rate);
            Seam(b, 0.4f);
            Normalize(b, 0.7f);
            return b;
        }

        static void Build()
        {
            Add("shot_revolver", Shot(1f, 4200f, 0.22f, 95f), Shot(1.1f, 3800f, 0.25f, 88f));
            Add("shot_rifle", Shot(1.3f, 5200f, 0.34f, 75f), Shot(1.4f, 4800f, 0.36f, 70f));
            Add("shot_shotgun", Shot(1.8f, 2600f, 0.4f, 58f));
            Add("shot_small", Shot(0.6f, 6500f, 0.12f, 160f));
            Add("step", Step(900f, 0.03f, 0.5f), Step(800f, 0.035f, 0.45f), Step(1000f, 0.028f, 0.5f));
            Add("step_wood", Step(420f, 0.05f, 0.7f), Step(380f, 0.055f, 0.65f));
            Add("hoof", Step(1500f, 0.022f, 0.75f), Step(1300f, 0.026f, 0.7f), Step(1700f, 0.02f, 0.75f));
            Add("punch", Step(260f, 0.06f, 0.9f));
            Add("impact", Step(2600f, 0.02f, 0.5f), Step(3200f, 0.016f, 0.45f));
            Add("flesh", Step(500f, 0.05f, 0.8f));
            Add("splash", Step(2200f, 0.12f, 0.6f));

            { float[] b = Buf(0.06f); Sweep(b, 2400f, 1800f, 0.6f, 0.012f); Noise(b, 0.3f, 0.006f); Add("click", b); }
            { float[] b = Buf(0.25f); Sweep(b, 1500f, 1200f, 0.5f, 0.02f); Sweep(b, 1900f, 1500f, 0.5f, 0.02f, 0.11f); Noise(b, 0.2f, 0.01f); Add("reload", b); }
            { float[] b = Buf(0.3f); Noise(b, 0.6f, 0.05f); HighPass(b, 1800f); Sweep(b, 300f, 140f, 0.5f, 0.06f); Normalize(b, 0.6f); Add("bow", b); }
            { float[] b = Buf(0.9f); Sweep(b, 1700f, 2500f, 0.5f, 1f, 0f, 0.3f); Sweep(b, 2100f, 2900f, 0.5f, 1f, 0.38f, 0.42f); Add("whistle", b); }
            { float[] b = Buf(0.5f); Sweep(b, 4100f, 4050f, 0.4f, 0.09f); Sweep(b, 5300f, 5250f, 0.3f, 0.07f, 0.07f); Add("coin", b); }
            { float[] b = Buf(0.05f); Sweep(b, 720f, 640f, 0.45f, 0.015f); Add("ui_move", b); }
            { float[] b = Buf(0.14f); Sweep(b, 620f, 930f, 0.5f, 0.05f); Add("ui_select", b); }
            { float[] b = Buf(0.12f); Sweep(b, 520f, 360f, 0.45f, 0.04f); Add("ui_back", b); }
            { float[] b = Buf(0.35f); Noise(b, 0.5f, 0.08f); LowPass(b, 1400f); HighPass(b, 300f); Normalize(b, 0.45f); Add("pickup", b); }
            {
                // whinny: falling pitch with vibrato and breath
                float[] b = Buf(1.2f);
                float ph = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)b.Length;
                    float f = Mathf.Lerp(1250f, 520f, t * t) * (1f + 0.06f * Mathf.Sin(t * 60f));
                    ph += 2f * Mathf.PI * f / Rate;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f));
                    b[i] = (Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 2f) * 0.3f + Mathf.Sin(ph * 3f) * 0.15f + Rnd() * 0.12f) * env;
                }
                Normalize(b, 0.7f);
                Add("whinny", b);
            }
            {
                // a snort: a blast of breath through the nostrils, the lips fluttering in it
                float[] b = Buf(0.55f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.015f) * Mathf.Exp(-t / 0.16f);
                    b[i] = Rnd() * env * (0.55f + 0.45f * Mathf.Sin(t * 2f * Mathf.PI * 34f));
                }
                LowPass(b, 1300f); HighPass(b, 160f); Normalize(b, 0.6f);
                Add("snort", b);
            }
            // the engine: an exhaust beat (a gust of steam up the stack), a three-chime whistle, the bell
            {
                float[] a = Buf(0.32f); Noise(a, 1f, 0.07f); LowPass(a, 950f); HighPass(a, 70f); Normalize(a, 0.85f);
                float[] c = Buf(0.36f); Noise(c, 1f, 0.085f); LowPass(c, 800f); HighPass(c, 60f); Normalize(c, 0.8f);
                Add("chuff", a, c);
            }
            {
                float[] b = Buf(2.6f);
                float[] chord = { 311f, 392f, 466f };
                float ph0 = 0f, ph1 = 0f, ph2 = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.12f) * Mathf.Min(1f, (b.Length - i) / (0.45f * Rate));
                    float wob = 1f + 0.004f * Mathf.Sin(t * 31f) - 0.012f * Mathf.Exp(-t * 6f);        // the note settles as the valve opens
                    ph0 += 2f * Mathf.PI * chord[0] * wob / Rate; ph1 += 2f * Mathf.PI * chord[1] * wob / Rate; ph2 += 2f * Mathf.PI * chord[2] * wob / Rate;
                    float tone = Mathf.Sin(ph0) + 0.85f * Mathf.Sin(ph1) + 0.7f * Mathf.Sin(ph2) + 0.25f * Mathf.Sin(ph0 * 2f) + 0.15f * Mathf.Sin(ph1 * 3f);
                    b[i] = (tone * 0.55f + Rnd() * 0.18f) * env;
                }
                LowPass(b, 3800f);
                Normalize(b, 0.9f);
                Add("loco_whistle", b);
            }
            {
                float[] b = Buf(1.6f);
                Sweep(b, 640f, 640f, 0.5f, 0.55f); Sweep(b, 1528f, 1528f, 0.32f, 0.35f); Sweep(b, 2560f, 2560f, 0.2f, 0.18f); Sweep(b, 3360f, 3360f, 0.12f, 0.1f);
                Normalize(b, 0.7f);
                Add("bell", b);
            }
            {
                float[] b = Buf(3.2f);
                for (int i = 0; i < b.Length; i++) b[i] = Rnd();
                LowPass(b, 160f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    b[i] *= Mathf.Exp(-t * 1.1f) * (0.6f + 0.4f * Mathf.Sin(t * 9f));
                }
                Normalize(b, 0.95f);
                Add("thunder", b);
            }

            Add("wind", LoopNoise(6f, 80f, 520f, t => 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f / 6f)));
            Add("rain", LoopNoise(3f, 1500f, 7000f, null));
            Add("river", LoopNoise(4f, 400f, 2200f, t => 0.8f + 0.2f * Mathf.Sin(t * Mathf.PI * 2f / 2f)));
            {
                float[] b = LoopNoise(4f, 150f, 1500f, null);
                for (int k = 0; k < 40; k++)
                {
                    int at = rng.Next(b.Length - 400);
                    float a = 0.4f + (float)rng.NextDouble() * 0.6f;
                    for (int i = 0; i < 300; i++) b[at + i] += Rnd() * a * Mathf.Exp(-i / 60f);
                }
                Normalize(b, 0.7f);
                Add("fire", b);
            }
            {
                float[] b = Buf(4f);
                for (int c = 0; c < 9; c++)
                {
                    float start = c * 0.44f + (float)rng.NextDouble() * 0.05f;
                    for (int p = 0; p < 7; p++) Sweep(b, 4300f, 4350f, 0.22f, 0.012f, start + p * 0.028f, 0.016f);
                }
                Add("crickets", b);
            }
            {
                float[] b = Buf(7f);
                for (int c = 0; c < 11; c++)
                {
                    float start = (float)rng.NextDouble() * 6.4f;
                    float f = 2200f + (float)rng.NextDouble() * 2400f;
                    int notes = 2 + rng.Next(4);
                    for (int p = 0; p < notes; p++)
                        Sweep(b, f * (1f + p * 0.05f), f * (1.25f - p * 0.04f), 0.16f, 0.05f, Mathf.Min(6.8f, start + p * 0.11f), 0.08f);
                }
                Add("birds", b);
            }
        }
    }
}
