using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>The sky in every weather and at every hour that matters, looking up into the cumulus, and the
        /// clouds' shadows on the plain from a rise (CanaanSky, CanaanClouds.hlsl, the atmosphere pass).</summary>
        IEnumerator SkyShow()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            g.scriptLock = true;
            Check(Shader.GetGlobalTexture("_CloudNoise") != null, "the cloud noise is bound");
            Vector3 eye = RegionDryFord.OnLand(new Vector2(r.P("town_west").x - 60f, r.P("town_west").z)) + Vector3.up * 1.7f;
            P.Teleport(eye + Vector3.forward * 40f - Vector3.up * 1.7f, 0f);
            Vector3 Look(float az, float el) { return eye + new Vector3(Mathf.Sin(az * Mathf.Deg2Rad), Mathf.Tan(el * Mathf.Deg2Rad), Mathf.Cos(az * Mathf.Deg2Rad)) * 100f; }
            yield return BeautyShot("clear_noon_up", eye, Look(20f, 32f), 70f, 13f, WeatherKind.Clear, 1.2f);
            yield return BeautyShot("cloudy_noon_up", eye, Look(20f, 32f), 70f, 13f, WeatherKind.Cloudy, 1.2f);
            Texture layer = Shader.GetGlobalTexture("_CloudTex");
            Check(layer is RenderTexture crt && crt.width == 640 && crt.height == 360, "the clouds are marched at half resolution (" + (layer != null ? layer.width + "x" + layer.height : "none") + ")");
            yield return BeautyShot("cloudy_noon_high", eye, Look(140f, 62f), 70f, 13f, WeatherKind.Cloudy, 0.4f);
            yield return BeautyShot("cloudy_golden", eye, Look(270f, 14f), 70f, 18.2f, WeatherKind.Cloudy, 0.6f);
            yield return BeautyShot("cloudy_sunset_away", eye, Look(90f, 18f), 70f, 18.9f, WeatherKind.Cloudy, 0.6f);
            yield return BeautyShot("cloudy_morning", eye, Look(90f, 16f), 70f, 7.0f, WeatherKind.Cloudy, 0.6f);
            yield return BeautyShot("overcast", eye, Look(20f, 24f), 70f, 13f, WeatherKind.Overcast, 0.6f);
            yield return BeautyShot("night_cloudy", eye, Look(200f, 25f), 70f, 23.5f, WeatherKind.Cloudy, 0.6f);
            // the shadows of the clouds on the land, from the top of the butte: fair-weather heaps leave separate
            // patches (under a cloudy sky the eye mostly stands in one wide shadow)
            Vector3 top = r.points.ContainsKey("flora_juniper") ? r.P("flora_juniper") : eye;
            yield return BeautyShot("cloud_shadows", top + Vector3.up * 40f, top + new Vector3(220f, -90f, 260f), 60f, 12.5f, WeatherKind.Clear, 0.6f);
            yield return BeautyShot("cloud_shadows_late", top + Vector3.up * 40f, top + new Vector3(-260f, -90f, 220f), 60f, 16.5f, WeatherKind.Clear, 0.6f);
            // the street on a cloudy afternoon: under a cloud the houses' shadows melt into the shade around them
            Vector3 town = r.P("town_west"), east = r.P("town_east"), along = (east - town).normalized;
            Vector3 street = RegionDryFord.OnLand(new Vector2(town.x, town.z) + new Vector2(along.x, along.z) * 40f);
            yield return BeautyShot("cloud_street", street + Vector3.up * 1.8f - along * 6f, street + along * 60f + Vector3.up * 4f, 60f, 15f, WeatherKind.Cloudy, 0.6f);
            // and from high over the town on a cloudy day: the patches of shadow drifting over the plain
            yield return BeautyShot("cloud_shadows_above", town + new Vector3(-500f, 520f, -420f), town + new Vector3(150f, 0f, 120f), 60f, 13f, WeatherKind.Cloudy, 0.6f);
        }
    }
}
