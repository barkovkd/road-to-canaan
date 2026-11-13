using UnityEngine;

namespace Canaan
{
    /// <summary>Pooled particle effects: muzzle flash, tracers, impacts, blood, dust.</summary>
    public static class Fx
    {
        static ParticleSystem dust, spark, blood, smoke, plume, flame, ember;
        static LineRenderer[] tracers;
        static float[] tracerLife;
        static int tracerNext;
        static Light flash;
        static float flashOff;

        public static void Init(Transform parent)
        {
            dust = Make(parent, "FxDust", Mats.ParticleAlpha, 0.9f, 0.3f, -0.03f, 2.6f, 600);
            smoke = Make(parent, "FxSmoke", Mats.ParticleAlpha, 1.6f, 0.25f, -0.06f, 3.2f, 300);
            blood = Make(parent, "FxBlood", Mats.ParticleAlpha, 0.5f, 0.07f, 1.4f, 0.6f, 300);
            spark = Make(parent, "FxSpark", Mats.ParticleAdd, 0.09f, 0.16f, 0f, 0.4f, 200);
            // standing smoke (chimneys, fires, the engine): ragged puffs that fade in, billow and thin out
            plume = Make(parent, "FxPlume", Mats.ParticleSmoke, 6f, 0.3f, -0.012f, 6f, 1600);
            Fade(plume, new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(0f, 1f));
            var drag = plume.limitVelocityOverLifetime;
            drag.enabled = true; drag.limit = 0.35f; drag.dampen = 0.035f;
            flame = Make(parent, "FxFlame", Mats.ParticleAdd, 0.5f, 0.3f, -0.25f, 0.2f, 500);
            Fade(flame, new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f));
            ember = Make(parent, "FxEmber", Mats.ParticleAdd, 1.4f, 0.03f, -0.05f, 0.6f, 200);

            tracers = new LineRenderer[10];
            tracerLife = new float[10];
            for (int i = 0; i < tracers.Length; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(parent, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = Mats.ParticleAdd;
                lr.positionCount = 2;
                lr.startWidth = 0.018f;
                lr.endWidth = 0.03f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 0;
                lr.enabled = false;
                tracers[i] = lr;
            }
            var lg = new GameObject("MuzzleLight");
            lg.transform.SetParent(parent, false);
            flash = lg.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.78f, 0.45f);
            flash.range = 9f;
            flash.intensity = 6f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;
        }

        static ParticleSystem Make(Transform parent, string name, Material mat, float life, float size, float gravity, float grow, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = size;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps, params GradientAlphaKey[] alpha)
        {
            var col = ps.colorOverLifetime;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha);
            col.color = g;
        }

        /// <summary>A puff of standing smoke or steam: turned at random, slowly rolling.</summary>
        public static void Plume(Vector3 pos, Vector3 vel, float size, Color32 color, float life)
        {
            if (plume == null) return;
            turn = (turn * 0.618034f + 0.411f) % 1f;                     // its own sequence: standing smoke must not use the game's dice
            plume.Emit(new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startColor = color, startSize = size, startLifetime = life,
                rotation = turn * 360f, angularVelocity = (turn * 7.3f % 1f - 0.5f) * 44f
            }, 1);
        }
        static float turn = 0.37f;

        public static void Flame(Vector3 pos, Vector3 vel, float size, Color32 color, float life) { Emit(flame, pos, vel, color, size, life); }
        public static void Ember(Vector3 pos, Vector3 vel, Color32 color, float life) { Emit(ember, pos, vel, color, 0.02f + turn * 0.025f, life); }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color32 color, float size, float life)
        {
            if (ps == null) return;
            ps.Emit(new ParticleSystem.EmitParams { position = pos, velocity = vel, startColor = color, startSize = size, startLifetime = life }, 1);
        }

        public static void Muzzle(Vector3 pos, Vector3 dir, bool big)
        {
            if (spark == null) return;
            int n = big ? 9 : 6;
            for (int i = 0; i < n; i++)
                Emit(spark, pos + dir * 0.05f, dir * Random.Range(5f, 14f) + Random.insideUnitSphere * 2.5f, new Color32(255, 214, 140, 255),
                     Random.Range(0.1f, 0.26f) * (big ? 1.4f : 1f), Random.Range(0.04f, 0.09f));
            for (int i = 0; i < (big ? 5 : 3); i++)
                Emit(smoke, pos + dir * 0.15f, dir * Random.Range(0.6f, 2f) + Random.insideUnitSphere * 0.4f + Vector3.up * 0.3f,
                     new Color32(205, 200, 190, 120), Random.Range(0.18f, 0.3f), Random.Range(0.9f, 1.8f));
            flash.transform.position = pos + dir * 0.3f;
            flash.enabled = true;
            flashOff = Time.time + 0.05f;
        }

        public static void Tracer(Vector3 a, Vector3 b)
        {
            if (tracers == null) return;
            LineRenderer lr = tracers[tracerNext];
            tracerLife[tracerNext] = 0.07f;
            tracerNext = (tracerNext + 1) % tracers.Length;
            lr.SetPosition(0, Vector3.Lerp(a, b, 0.15f));
            lr.SetPosition(1, b);
            lr.startColor = new Color(1f, 0.92f, 0.7f, 0.0f);
            lr.endColor = new Color(1f, 0.92f, 0.7f, 0.55f);
            lr.enabled = true;
        }

        public static void Impact(Vector3 p, Vector3 n)
        {
            for (int i = 0; i < 5; i++)
                Emit(dust, p + n * 0.03f, n * Random.Range(0.6f, 2.2f) + Random.insideUnitSphere * 0.7f, new Color32(176, 152, 118, 170),
                     Random.Range(0.1f, 0.22f), Random.Range(0.5f, 1.1f));
            Emit(spark, p + n * 0.02f, n * 3f + Random.insideUnitSphere * 3f, new Color32(255, 220, 160, 255), 0.07f, 0.07f);
        }

        public static void Blood(Vector3 p, Vector3 dir)
        {
            for (int i = 0; i < 8; i++)
                Emit(blood, p, dir * Random.Range(0.5f, 3f) + Random.insideUnitSphere * 1.2f + Vector3.up * 0.6f, new Color32(120, 18, 14, 230),
                     Random.Range(0.04f, 0.09f), Random.Range(0.3f, 0.6f));
        }

        public static void Dust(Vector3 p, float amount, Color32 color)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(amount * 3f));
            for (int i = 0; i < n; i++)
                Emit(dust, p + Random.insideUnitSphere * 0.1f, Random.insideUnitSphere * 0.5f + Vector3.up * 0.4f, color,
                     Random.Range(0.15f, 0.35f) * Mathf.Sqrt(amount), Random.Range(0.6f, 1.3f));
        }

        public static void Smoke(Vector3 p, Vector3 vel, float size, Color32 color, float life)
        {
            Emit(smoke, p, vel, color, size, life);
        }

        public static void Tick(float dt)
        {
            if (tracers == null) return;
            for (int i = 0; i < tracers.Length; i++)
            {
                if (tracerLife[i] <= 0f) continue;
                tracerLife[i] -= dt;
                if (tracerLife[i] <= 0f) tracers[i].enabled = false;
            }
            if (flash.enabled && Time.time > flashOff) flash.enabled = false;
        }
    }
}
