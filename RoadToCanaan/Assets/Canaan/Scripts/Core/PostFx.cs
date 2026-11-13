using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Canaan
{
    /// <summary>
    /// The lens and the film: tone mapping, a grade that follows the hour and the weather, bloom, grain,
    /// vignette, depth of field for the cinematic camera, and the look of «Хладнокровие».
    /// One global volume whose values are rewritten every frame.
    /// </summary>
    public class PostFx
    {
        public readonly Volume volume;
        public float slow;            // 0..1, «Хладнокровие»
        public float cinema;          // 0..1, the cinematic camera: shallow focus, heavier vignette
        public float focus = 8f;      // metres to the subject, used while 'cinema' is up
        public float hurt;            // 0..1, low health drains the colour

        readonly Tonemapping tone;
        readonly Bloom bloom;
        readonly ColorAdjustments grade;
        readonly WhiteBalance balance;
        readonly SplitToning split;
        readonly ShadowsMidtonesHighlights smh;
        readonly Vignette vignette;
        readonly FilmGrain grain;
        readonly ChromaticAberration chroma;
        readonly DepthOfField dof;
        readonly MotionBlur blur;

        public PostFx(Transform parent)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(parent, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.94f, 0.86f));
            bloom.highQualityFiltering.Override(true);

            grade = profile.Add<ColorAdjustments>(true);
            balance = profile.Add<WhiteBalance>(true);
            split = profile.Add<SplitToning>(true);
            smh = profile.Add<ShadowsMidtonesHighlights>(true);

            vignette = profile.Add<Vignette>(true);
            vignette.smoothness.Override(0.42f);
            vignette.rounded.Override(false);

            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.response.Override(0.7f);

            chroma = profile.Add<ChromaticAberration>(true);

            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.highQualitySampling.Override(false);

            blur = profile.Add<MotionBlur>(true);
            blur.mode.Override(MotionBlurMode.CameraOnly);
            blur.quality.Override(MotionBlurQuality.Low);
            blur.clamp.Override(0.03f);
            // tests render single frames on demand from far-apart places: a motion trail between them would be a lie
            blur.intensity.Override(Args.Offscreen ? 0f : 0.22f);
        }

        public void Tick(SkyWeather sky, float udt)
        {
            float day = sky.Daylight, gold = sky.Golden, grey = sky.Grey, night = 1f - day;
            float murk = Mathf.Clamp01((sky.Murk - 1f) / 6f);

            // exposure: ACES sits a little dark; nights are lifted so the game stays readable by moonlight
            float exposure = 0.42f + night * 0.55f - gold * 0.05f + grey * 0.12f;
            grade.postExposure.Override(exposure - slow * 0.1f);
            grade.contrast.Override(Mathf.Lerp(13f, 6f, grey) + gold * 5f + slow * 14f);
            float sat = 6f + gold * 10f - grey * 14f - night * 16f - murk * 8f;
            grade.saturation.Override(Mathf.Lerp(sat, -58f, slow) - hurt * 30f);
            grade.colorFilter.Override(Color.Lerp(Color.white, new Color(1f, 0.9f, 0.74f), slow));

            balance.temperature.Override(4f + gold * 16f - night * 24f - grey * 6f + slow * 10f);
            balance.tint.Override(gold * 4f - night * 3f);

            // cool shadows against warm highlights; stronger when the sun is low
            float tone01 = 0.5f + gold * 0.5f;
            split.shadows.Override(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.40f, 0.49f, 0.56f), 0.75f * tone01 + night * 0.25f));
            split.highlights.Override(Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.62f, 0.53f, 0.42f), 0.7f * tone01 * day));
            split.balance.Override(-12f);
            smh.shadows.Override(new Vector4(0.98f, 1f, 1.04f, -0.02f + night * 0.04f));
            smh.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
            smh.highlights.Override(new Vector4(1.03f, 1f, 0.96f, 0.02f * day));

            bloom.intensity.Override(0.42f + night * 0.5f + gold * 0.25f + murk * 0.2f);
            bloom.threshold.Override(Mathf.Lerp(1.0f, 0.82f, night));

            vignette.intensity.Override(Mathf.Lerp(Mathf.Lerp(0.25f, 0.36f, cinema), 0.48f, slow) + hurt * 0.12f);
            grain.intensity.Override(0.26f + night * 0.14f + slow * 0.2f);
            chroma.intensity.Override(0.06f + slow * 0.3f + cinema * 0.05f);

            if (cinema > 0.02f)
            {
                // a long lens on the subject
                dof.active = true;
                dof.mode.Override(DepthOfFieldMode.Bokeh);
                dof.focusDistance.Override(Mathf.Max(0.6f, focus));
                dof.focalLength.Override(Mathf.Lerp(40f, 105f, cinema));
                dof.aperture.Override(Mathf.Lerp(8f, 2.4f, cinema));
                dof.bladeCount.Override(7);
            }
            else
            {
                // in play the far distance softens only in thick air (fog, dust): on a clear day a far blur would
                // smear the clouds, which stand at the sky's depth, into a veil
                dof.active = murk > 0.15f;
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(Mathf.Lerp(260f, 70f, murk));
                dof.gaussianEnd.Override(Mathf.Lerp(1100f, 300f, murk));
                dof.gaussianMaxRadius.Override(0.4f);
            }
        }
    }
}
