using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Canaan
{
    public enum WeatherKind { Clear, Cloudy, Overcast, Rain, Storm, Dust, Fog }

    /// <summary>
    /// The clock, the sun and moon, the sky colours and the weather. One directional light serves as sun by day
    /// and moon by night; the same colour tables drive the sky shader, the fog and the ambient light.
    /// </summary>
    public class SkyWeather
    {
        public float hour = 9f;
        public int day = 1;
        public float minutesPerDay = 48f;     // real minutes for 24 game hours
        public WeatherKind weather = WeatherKind.Clear;
        public bool frozen;                   // tests and cutscenes stop the clock
        public event Action<bool> NightChanged;

        readonly Light sun;
        readonly Camera cam;
        WeatherKind from = WeatherKind.Clear;
        float blend = 1f, nextChange = 600f, windPhase, flash, thunderAt = -1f, nextBolt;
        bool night;
        ParticleSystem rain, dust;
        AudioSource windLoop, rainLoop, cricketLoop, birdLoop;
        readonly System.Random rng = new System.Random(99);

        struct Look
        {
            public float cover, sunMul, fog, wind, wet, rain, dust, grey;
            public static Look Lerp(Look a, Look b, float t)
            {
                return new Look
                {
                    cover = Mathf.Lerp(a.cover, b.cover, t), sunMul = Mathf.Lerp(a.sunMul, b.sunMul, t), fog = Mathf.Lerp(a.fog, b.fog, t),
                    wind = Mathf.Lerp(a.wind, b.wind, t), wet = Mathf.Lerp(a.wet, b.wet, t), rain = Mathf.Lerp(a.rain, b.rain, t),
                    dust = Mathf.Lerp(a.dust, b.dust, t), grey = Mathf.Lerp(a.grey, b.grey, t)
                };
            }
        }

        static Look Of(WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Cloudy: return new Look { cover = 0.55f, sunMul = 0.85f, fog = 0.0013f, wind = 0.07f, grey = 0.15f };
                case WeatherKind.Overcast: return new Look { cover = 0.95f, sunMul = 0.4f, fog = 0.002f, wind = 0.09f, grey = 0.6f };
                case WeatherKind.Rain: return new Look { cover = 1f, sunMul = 0.3f, fog = 0.0032f, wind = 0.12f, wet = 1f, rain = 0.6f, grey = 0.75f };
                case WeatherKind.Storm: return new Look { cover = 1f, sunMul = 0.2f, fog = 0.0045f, wind = 0.24f, wet = 1f, rain = 1f, grey = 0.9f };
                case WeatherKind.Dust: return new Look { cover = 0.35f, sunMul = 0.6f, fog = 0.0075f, wind = 0.22f, dust = 1f, grey = 0.2f };
                case WeatherKind.Fog: return new Look { cover = 0.7f, sunMul = 0.5f, fog = 0.011f, wind = 0.02f, grey = 0.55f };
                default: return new Look { cover = 0.22f, sunMul = 1f, fog = 0.0011f, wind = 0.05f };
            }
        }

        public static string Name(WeatherKind w)
        {
            switch (w)
            {
                case WeatherKind.Cloudy: return "облачно";
                case WeatherKind.Overcast: return "пасмурно";
                case WeatherKind.Rain: return "дождь";
                case WeatherKind.Storm: return "гроза";
                case WeatherKind.Dust: return "пыльная буря";
                case WeatherKind.Fog: return "туман";
                default: return "ясно";
            }
        }

        public bool IsNight => night;
        public string TimeText => Mathf.FloorToInt(hour).ToString("00") + ":" + Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f).ToString("00");
        public string Text => "День " + day + ", " + TimeText + " · " + Name(weather);
        public float Wetness { get; private set; }
        public float Daylight { get; private set; }
        Texture3D cloudNoise;
        public float SunElevation { get; private set; }
        /// <summary>1 when the sun stands just above the horizon: the hour of long shadows.</summary>
        public float Golden { get; private set; }
        /// <summary>How overcast it is, 0..1.</summary>
        public float Grey { get; private set; }
        /// <summary>How thick the air is compared with a clear day (1 = clear).</summary>
        public float Murk { get; private set; }

        public SkyWeather(Light sun, Camera cam, Transform parent)
        {
            this.sun = sun;
            this.cam = cam;
            rain = MakeFall(parent, "Rain", new Color32(190, 200, 215, 110), 0.045f, 0.55f, 26f, 3000);
            dust = MakeFall(parent, "DustStorm", new Color32(190, 160, 115, 60), 1.6f, 2.2f, 0.5f, 400);
            windLoop = Sfx.Loop("wind", parent, false);
            rainLoop = Sfx.Loop("rain", parent, false);
            cricketLoop = Sfx.Loop("crickets", parent, false);
            birdLoop = Sfx.Loop("birds", parent, false);
        }

        static ParticleSystem MakeFall(Transform parent, string name, Color32 color, float size, float life, float speed, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = life; main.startSpeed = 0f; main.startSize = size; main.startColor = (Color)color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = 0f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.ParticleAlpha;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (speed > 5f) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.035f; r.lengthScale = 1f; }
            ps.Play();
            return ps;
        }

        public void SetWeather(WeatherKind w, bool instant = false)
        {
            if (w == weather && blend >= 1f) return;
            from = weather;
            weather = w;
            blend = instant ? 1f : 0f;
        }

        /// <summary>Jumps the clock forward to an hour of the day (sleeping, waiting).</summary>
        public void SkipTo(float targetHour)
        {
            if (targetHour <= hour) day++;
            hour = targetHour;
            RollWeather();
            blend = 1f;
        }

        void RollWeather()
        {
            double r = rng.NextDouble();
            WeatherKind w = r < 0.46 ? WeatherKind.Clear : r < 0.68 ? WeatherKind.Cloudy : r < 0.80 ? WeatherKind.Overcast
                          : r < 0.90 ? WeatherKind.Rain : r < 0.94 ? WeatherKind.Storm : r < 0.97 ? WeatherKind.Dust : WeatherKind.Fog;
            if (w == WeatherKind.Fog && (hour > 10f && hour < 20f)) w = WeatherKind.Cloudy;
            SetWeather(w);
            nextChange = 420f + (float)rng.NextDouble() * 900f;
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        static Color Key(float e, Color night, Color dusk, Color low, Color day)
        {
            if (e < -14f) return night;
            if (e < -3f) return Color.Lerp(night, dusk, Mathf.InverseLerp(-14f, -3f, e));
            if (e < 6f) return Color.Lerp(dusk, low, Mathf.InverseLerp(-3f, 6f, e));
            return Color.Lerp(low, day, Mathf.InverseLerp(6f, 28f, e));
        }

        public void Tick(float dt)
        {
            if (!frozen)
            {
                hour += dt * 24f / (minutesPerDay * 60f);
                if (hour >= 24f) { hour -= 24f; day++; }
                nextChange -= dt;
                if (nextChange <= 0f) RollWeather();
            }
            blend = Mathf.Min(1f, blend + dt / 45f);
            Look L = Look.Lerp(Of(from), Of(weather), U.Smooth01(blend));

            // ---- sun and moon
            float t = (hour - 6f) / 14f;                       // 0 at sunrise, 1 at sunset
            float elev = Mathf.Sin(t * Mathf.PI) * 62f;
            if (t < 0f || t > 1f) elev = -Mathf.Sin(Mathf.Repeat(hour - 20f, 24f) / 10f * Mathf.PI) * 55f;
            float az = Mathf.Lerp(90f, 270f, Mathf.Clamp01(t));
            if (t < 0f) az = 90f - (-t) * 60f; else if (t > 1f) az = 270f + (t - 1f) * 60f;
            Vector3 sunDir = Dir(az, elev);
            float mt = Mathf.Repeat(hour - 20f, 24f) / 10f;
            Vector3 moonDir = Dir(Mathf.Lerp(100f, 260f, Mathf.Clamp01(mt)), Mathf.Sin(Mathf.Clamp01(mt) * Mathf.PI) * 48f + 4f);

            float sunPower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-2f, 10f, elev));
            float moonPower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-4f, -12f, elev));
            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-8f, 8f, elev));
            SunElevation = elev;
            Golden = Mathf.Clamp01(1f - Mathf.Abs(elev - 6f) / 15f) * Mathf.Clamp01((elev + 4f) / 6f) * (1f - L.grey);
            Grey = L.grey;
            bool useMoon = elev < -3f;
            Color sunCol = Key(elev, C(1f, 0.5f, 0.3f), C(1f, 0.5f, 0.28f), C(1f, 0.68f, 0.42f), C(1f, 0.95f, 0.85f));
            if (useMoon)
            {
                sun.transform.rotation = Quaternion.LookRotation(-moonDir);
                sun.color = C(0.55f, 0.66f, 0.95f);
                sun.intensity = 0.8f * moonPower * Mathf.Lerp(1f, 0.4f, L.cover);     // a bright moon: nights stay playable
            }
            else
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunDir);
                sun.color = sunCol;
                sun.intensity = 1.9f * sunPower * L.sunMul;
            }
            if (flash > 0f) { sun.intensity += flash * 3f; flash = Mathf.Max(0f, flash - dt * 7f); }

            // ---- sky, fog, ambient
            Color top = Key(elev, C(0.012f, 0.018f, 0.045f), C(0.10f, 0.13f, 0.30f), C(0.24f, 0.33f, 0.56f), C(0.20f, 0.38f, 0.66f));
            Color hor = Key(elev, C(0.04f, 0.055f, 0.10f), C(0.50f, 0.32f, 0.33f), C(0.95f, 0.66f, 0.42f), C(0.72f, 0.80f, 0.86f));
            Color grd = Key(elev, C(0.02f, 0.02f, 0.03f), C(0.16f, 0.13f, 0.14f), C(0.40f, 0.34f, 0.28f), C(0.50f, 0.46f, 0.40f));
            Color cloudLit = Key(elev, C(0.10f, 0.12f, 0.18f), C(0.75f, 0.45f, 0.40f), C(1f, 0.80f, 0.62f), C(0.98f, 0.97f, 0.94f));
            Color cloudDark = Key(elev, C(0.03f, 0.035f, 0.06f), C(0.20f, 0.17f, 0.24f), C(0.50f, 0.42f, 0.44f), C(0.62f, 0.66f, 0.72f));
            float lum = Mathf.Lerp(0.06f, 0.6f, Daylight);
            Color greyTop = C(0.42f, 0.45f, 0.5f) * lum * 1.5f, greyHor = C(0.6f, 0.62f, 0.64f) * lum * 1.6f;
            top = Color.Lerp(top, greyTop, L.grey);
            hor = Color.Lerp(hor, greyHor, L.grey);
            cloudLit = Color.Lerp(cloudLit, greyHor * 1.05f, L.grey * 0.8f);
            cloudDark = Color.Lerp(cloudDark, greyTop * 0.8f, L.grey * 0.8f);
            Color dustCol = C(0.72f, 0.58f, 0.40f) * Mathf.Lerp(0.15f, 1f, Daylight);
            hor = Color.Lerp(hor, dustCol, L.dust * 0.8f);
            top = Color.Lerp(top, dustCol * 0.9f, L.dust * 0.5f);
            if (flash > 0f) { top += Color.white * flash * 0.3f; hor += Color.white * flash * 0.4f; }

            Shader.SetGlobalColor("_SkyTop", top.linear);
            Shader.SetGlobalColor("_SkyHorizon", hor.linear);
            Shader.SetGlobalColor("_SkyGround", grd.linear);
            Shader.SetGlobalColor("_SkySunColor", (sunCol * (1f - L.grey * 0.8f) * Mathf.Clamp01(sunPower * 1.5f + 0.15f)).linear);
            Shader.SetGlobalColor("_SkyCloudLit", cloudLit.linear);
            Shader.SetGlobalColor("_SkyCloudDark", cloudDark.linear);
            Shader.SetGlobalVector("_SkySunDir", sunDir);
            if (cloudNoise == null)
            {
                // the shape of the cumulus: a tiling 3D noise (Tools/textures/make_clouds.py, imported by CanaanSetup.Clouds)
                cloudNoise = Resources.Load<Texture3D>("Clouds/cloud_noise");
                if (cloudNoise != null) Shader.SetGlobalTexture("_CloudNoise", cloudNoise);
            }
            Shader.SetGlobalVector("_SkyMoonDir", moonDir);
            windPhase += dt * (0.8f + L.wind * 10f);
            Shader.SetGlobalVector("_SkyParams", new Vector4((1f - Daylight) * (1f - L.cover * 0.9f), L.cover, windPhase * 0.004f + day * 0.37f, 1f - L.grey));
            Shader.SetGlobalVector("_CanaanWind", new Vector4(0.8f, 0.6f, L.wind, windPhase));
            Wetness = U.Damp(Wetness, L.wet, 0.15f, dt);
            Shader.SetGlobalVector("_CanaanWet", new Vector4(Wetness, 0f, 0f, 0f));

            // ---- the air: haze everywhere, a heavier layer near the ground, morning mist in the valley
            Murk = L.fog / 0.0011f;
            float mist = Mathf.Clamp01(1f - Mathf.Abs(hour - 6.3f) / 2.4f) * (1f - L.dust) * (1f - L.rain);
            float haze = 0.00042f * Mathf.Pow(Murk, 1.4f) * (1f + Golden * 0.9f);
            float low = 0.0011f * Mathf.Pow(Murk, 1.15f) * (1f + mist * 7f + Golden * 0.8f);
            float fall = Mathf.Lerp(0.03f, 0.075f, mist);
            float limit = Mathf.Lerp(0.9f, 1f, Mathf.Clamp01((Murk - 1f) / 4f));
            Color air = Color.Lerp(hor, top, 0.3f).linear * Mathf.Lerp(0.95f, 0.8f, L.grey);
            Color glow = useMoon ? C(0.55f, 0.66f, 0.95f).linear * (0.06f * moonPower * (1f - L.cover * 0.7f))
                                 : sunCol.linear * (sunPower * (1f - L.grey * 0.85f) * Mathf.Lerp(0.55f, 1.25f, Golden));
            Vector3 lightDir = useMoon ? moonDir : sunDir;
            float shafts = useMoon ? 0.25f * moonPower * (1f - L.cover) : sunPower * (1f - L.grey) * Mathf.Lerp(0.35f, 1f, Golden);
            Shader.SetGlobalVector("_AtmoFogColor", new Vector4(air.r, air.g, air.b, haze));
            Shader.SetGlobalVector("_AtmoSunColor", new Vector4(glow.r, glow.g, glow.b, Mathf.Lerp(7f, 4f, Mathf.Clamp01((Murk - 1f) / 6f))));
            Shader.SetGlobalVector("_AtmoHeight", new Vector4(World.waterLevel + 2f, fall, low, limit));
            Shader.SetGlobalVector("_AtmoSunDir", new Vector4(lightDir.x, lightDir.y, lightDir.z, shafts));
            Color amb = Key(elev, C(0.19f, 0.23f, 0.38f), C(0.30f, 0.30f, 0.44f), C(0.52f, 0.52f, 0.62f), C(0.62f, 0.72f, 0.90f));
            Color ambEq = Key(elev, C(0.14f, 0.16f, 0.27f), C(0.26f, 0.23f, 0.28f), C(0.52f, 0.44f, 0.38f), C(0.58f, 0.57f, 0.54f));
            Color ambGr = Key(elev, C(0.07f, 0.08f, 0.13f), C(0.12f, 0.10f, 0.11f), C(0.28f, 0.22f, 0.18f), C(0.42f, 0.36f, 0.28f));
            float dim = Mathf.Lerp(1f, 0.72f, L.grey);
            RenderSettings.ambientSkyColor = amb * dim + Color.white * flash * 0.3f;
            RenderSettings.ambientEquatorColor = ambEq * dim;
            RenderSettings.ambientGroundColor = ambGr * dim;

            bool nowNight = elev < -4f;
            if (nowNight != night) { night = nowNight; NightChanged?.Invoke(night); }

            // ---- rain, dust, thunder
            Vector3 cp = cam.transform.position;
            if (L.rain > 0.02f)
            {
                int n = Mathf.RoundToInt(L.rain * 900f * dt + UnityEngine.Random.value);
                for (int i = 0; i < n; i++)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 22f;
                    rain.Emit(new ParticleSystem.EmitParams
                    {
                        position = cp + new Vector3(r.x, 11f + UnityEngine.Random.value * 4f, r.y) + cam.transform.forward * 6f,
                        velocity = new Vector3(L.wind * 22f, -24f, L.wind * 16f)
                    }, 1);
                }
            }
            if (L.dust > 0.02f)
            {
                int n = Mathf.RoundToInt(L.dust * 40f * dt + UnityEngine.Random.value);
                for (int i = 0; i < n; i++)
                {
                    Vector3 r = UnityEngine.Random.insideUnitSphere * 26f;
                    r.y = Mathf.Abs(r.y) * 0.3f;
                    dust.Emit(new ParticleSystem.EmitParams { position = cp + r - new Vector3(8f, 0f, 6f), velocity = new Vector3(8f, 0.4f, 6f) }, 1);
                }
            }
            if (weather == WeatherKind.Storm && blend > 0.6f && !frozen)
            {
                nextBolt -= dt;
                if (nextBolt <= 0f)
                {
                    nextBolt = UnityEngine.Random.Range(6f, 22f);
                    flash = 1f;
                    thunderAt = Time.time + UnityEngine.Random.Range(0.4f, 2.8f);
                }
            }
            if (thunderAt > 0f && Time.time >= thunderAt) { thunderAt = -1f; Sfx.Play2D("thunder", 0.9f, Sfx.Vary(0.85f, 1.1f)); }

            // ---- ambience
            float udt = Game.UDt;
            windLoop.volume = U.Damp(windLoop.volume, Mathf.Clamp01(0.1f + L.wind * 3f) * Sfx.Volume, 1f, udt);
            rainLoop.volume = U.Damp(rainLoop.volume, L.rain * 0.7f * Sfx.Volume, 1f, udt);
            cricketLoop.volume = U.Damp(cricketLoop.volume, (1f - Daylight) * (1f - L.rain) * 0.3f * Sfx.Volume, 0.5f, udt);
            birdLoop.volume = U.Damp(birdLoop.volume, Daylight * (1f - L.rain) * (1f - L.dust) * 0.22f * Sfx.Volume, 0.5f, udt);
        }

        static Vector3 Dir(float azimuthDeg, float elevationDeg)
        {
            float a = azimuthDeg * Mathf.Deg2Rad, e = elevationDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
        }
    }
}
