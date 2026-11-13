using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>The plants where they grow: juniper on the slopes, yucca, prickly pear and cholla on the thin grass,
        /// willows and cattails by the river, stones at the ford (flora.py, RegionDryFord.DesertAndRiverside), and the
        /// worn street of the town.</summary>
        IEnumerator FloraShow()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            g.scriptLock = true;
            foreach (string m in new[] { "tree_juniper", "tree_pinyon", "bush_rabbit", "bush_willow", "plant_yucca", "plant_pear", "plant_cholla", "plant_reeds", "rock_river", "log_drift" })
                Check(Canaan.Models.Has(m), "the library has " + m);
            P.Teleport(RegionDryFord.OnLand(new Vector2(r.P("town_west").x - 30f, r.P("town_west").z)), 90f);
            const float hour = 16.5f;
            // a view of a marked plant from a few metres off, the camera on the ground's own height
            IEnumerator Plant(string key, float dist, float height, float az, float fov = 50f)
            {
                if (!r.points.ContainsKey(key)) { Check(false, "the region marks " + key); yield break; }
                Vector3 at = r.P(key);
                Vector3 dir = new Vector3(Mathf.Sin(az * Mathf.Deg2Rad), 0f, Mathf.Cos(az * Mathf.Deg2Rad));
                Vector3 eye = RegionDryFord.OnLand(new Vector2(at.x + dir.x * dist, at.z + dir.z * dist)) + Vector3.up * height;
                yield return BeautyShot(key, eye, at + Vector3.up * 0.7f, fov, hour, WeatherKind.Clear, 0.6f);
            }
            yield return Plant("flora_juniper", 14f, 2.2f, 30f);
            yield return Plant("flora_yucca", 7f, 1.5f, 200f);
            yield return Plant("flora_pear", 5f, 1.3f, 120f);
            yield return Plant("flora_cholla", 6f, 1.4f, 60f);
            yield return Plant("flora_willow", 16f, 2.0f, 250f);
            yield return Plant("flora_reeds", 9f, 1.6f, 80f);
            yield return Plant("flora_stones", 12f, 2.6f, 300f, 55f);
            Vector3 street = r.P("town_west");
            yield return BeautyShot("street_ruts", street + new Vector3(-6f, 1.7f, 0.5f), street + new Vector3(40f, 0f, -1f), 55f, hour);
            yield return BeautyShot("street_trough", r.P("saloon_hitch") + new Vector3(3f, 1.6f, -6f), r.P("saloon_hitch") + new Vector3(3f, 0.3f, 0f), 55f, hour);
        }
    }
}
