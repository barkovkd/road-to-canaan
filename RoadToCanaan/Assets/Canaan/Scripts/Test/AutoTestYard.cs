using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>The train, the water tower, the wind pump, fences and gates, shop fittings: the look-over after work on Props.</summary>
        IEnumerator Yard()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            foreach (string m in new[] { "rail_track", "rail_loco", "rail_tender", "rail_boxcar", "rail_coach", "rail_buffer", "water_tower", "windmill_tower", "windmill_wheel",
                                         "fence_post", "fence_rail", "fence_pole", "fence_gate", "ranch_gate", "signpost", "shop_counter", "bar_counter", "shop_shelf", "goods_bottle", "goods_tin" })
                Check(Canaan.Models.Has(m), "the library has " + m);
            Check(Windmills.Count >= 2, "the wind pumps' wheels are hung (" + Windmills.Count + ")");
            Check(Emitters.Count >= 8 && Trains.Smoking, "stoves, fires and the engine smoke (" + Emitters.Count + " sources, engine " + Trains.Smoking + ")");
            P.Teleport(RegionDryFord.OnLand(new Vector2(r.P("town_west").x - 30f, r.P("town_west").z)), 90f);
            yield return Wait(7f);                       // long enough for the plumes to stand

            // a point by a car of the train: to its right, up, along the track
            Vector3 T(int car, float right, float up, float along)
            {
                Vector3 dir = U.Dir(r.Yaw("train" + car));
                return r.P("train" + car) + Vector3.Cross(Vector3.up, dir) * right + Vector3.up * up + dir * along;
            }
            const float noon = 13.2f;
            yield return BeautyShot("loco_front", T(0, 5.5f, 2.0f, 10f), T(0, 0f, 2.1f, 1.5f), 50f, noon);
            yield return BeautyShot("loco_side", T(0, 9.5f, 2.3f, 0.5f), T(0, 0f, 2.1f, 0.5f), 50f, noon, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("loco_cab", T(0, 3.4f, 2.7f, -6.4f), T(0, 0f, 2.5f, -2.4f), 50f, noon, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("loco_gear", T(0, 3.2f, 1.2f, 1.6f), T(0, 0.7f, 0.95f, -0.6f), 50f, noon, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("tender_boxcar", T(1, 8f, 2.6f, 1.5f), T(1, 0f, 2.0f, -4f), 55f, noon, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("coach", T(3, 8f, 2.3f, 2.5f), T(3, 0f, 2.4f, -0.3f), 50f, noon, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("coach_platform", T(3, 3.4f, 2.0f, -7.6f), T(3, 0f, 2.0f, -4.5f), 50f, noon, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("track", T(4, 2.4f, 1.0f, -8.5f), T(4, 0f, 0.15f, -15f), 50f, noon, WeatherKind.Clear, 0.1f);
            Vector3 end = r.P("rail_end"), ed = U.Dir(r.Yaw("rail_end")), er = Vector3.Cross(Vector3.up, ed);
            yield return BeautyShot("buffer", end + er * 4.2f + Vector3.up * 1.7f - ed * 3.2f, end + Vector3.up * 0.8f + ed * 0.3f, 50f, noon, WeatherKind.Clear, 0.1f);
            Vector3 wt = r.P("water_tower");
            yield return BeautyShot("water_tower", wt + new Vector3(8f, 2.2f, -13f), wt + Vector3.up * 5.6f, 55f, noon);
            yield return BeautyShot("water_tower_up", wt + new Vector3(-5f, 1.7f, -5.5f), wt + Vector3.up * 6.6f, 60f, noon, WeatherKind.Clear, 0.1f);

            Vector3 mill = r.P("ranch_mill");
            float turned = Windmills.Turned(0);
            yield return BeautyShot("windmill", mill + new Vector3(5.5f, 2.2f, -13.5f), mill + Vector3.up * 5.6f, 55f, noon);
            yield return BeautyShot("windmill_wheel", mill + new Vector3(2.8f, 7.6f, -5.5f), mill + new Vector3(0f, 9.1f, -0.3f), 45f, noon, WeatherKind.Clear, 0.5f);
            Check(Mathf.Abs(Windmills.Turned(0) - turned) > 5f, "the wheels turn in the wind (" + (Windmills.Turned(0) - turned).ToString("0") + " deg in a second)");
            Vector3 pen = r.P("ranch_corral");
            yield return BeautyShot("ranch_pen", pen + new Vector3(11f, 2.0f, 25f), pen + new Vector3(1f, 0.7f, 16f), 50f, noon, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("ranch_pen_close", pen + new Vector3(-9.5f, 1.5f, 18.6f), pen + new Vector3(-6.2f, 0.75f, 16.1f), 45f, noon, WeatherKind.Clear, 0.1f);
            Vector3 gate = r.P("ranch_gate");
            yield return BeautyShot("ranch_gate", gate + new Vector3(-5.5f, 1.8f, -10f), gate + Vector3.up * 3.0f, 50f, noon, WeatherKind.Clear, 0.1f);

            Vector3 sign = r.P("town_sign");
            yield return BeautyShot("town_sign", sign + new Vector3(2.6f, 1.7f, -5.6f), sign + Vector3.up * 1.9f, 50f, noon);
            Vector3 corral = r.P("stable_corral");
            yield return BeautyShot("corral", corral + new Vector3(-2f, 1.9f, 2.5f), corral + new Vector3(7f, 0.8f, 9.2f), 55f, noon, WeatherKind.Clear, 0.1f);
            Vector3 graves = r.P("cemetery");
            yield return BeautyShot("cemetery", graves + new Vector3(7.5f, 1.9f, -10.5f), graves + new Vector3(2.5f, 0.6f, -2f), 55f, noon, WeatherKind.Clear, 0.1f);

            // indoors
            Vector3 saloon = r.P("saloon_inside"), bar = r.P("saloon_keeper");
            yield return BeautyShot("saloon_bar", saloon + new Vector3(-0.8f, 1.6f, 0.4f), bar + new Vector3(0.2f, 1.05f, 0.8f), 60f, noon);
            yield return BeautyShot("saloon_bottles", bar + new Vector3(2.4f, 1.5f, -2.2f), bar + new Vector3(-0.8f, 1.25f, -0.4f), 45f, noon, WeatherKind.Clear, 0.1f);
            Vector3 store = r.P("store_inside"), keeper = r.P("store_keeper");
            yield return BeautyShot("store_shelves", store + new Vector3(1.8f, 1.7f, 1.6f), keeper + new Vector3(-1.4f, 1.2f, 1.2f), 60f, noon);
            yield return BeautyShot("store_goods", keeper + new Vector3(1.3f, 1.55f, -1.9f), keeper + new Vector3(-0.4f, 1.3f, 1.4f), 40f, noon, WeatherKind.Clear, 0.1f);
            Vector3 guns = r.P("gunsmith_inside");
            yield return BeautyShot("gunsmith_rack", guns + new Vector3(-1.4f, 1.6f, 0.2f), guns + new Vector3(-4.2f, 1.45f, 1.5f), 50f, noon);

            // smoke and fire
            Vector3 roofs = r.P("store_door");
            yield return BeautyShot("town_smoke", roofs + new Vector3(-26f, 13f, -30f), roofs + new Vector3(8f, 6f, 8f), 50f, 7.6f, WeatherKind.Clear, 6f);
            yield return BeautyShot("loco_smoke", T(0, 9f, 2.0f, 12f), T(0, 0f, 4.2f, 2f), 55f, 7.6f, WeatherKind.Clear, 0.3f);
            yield return BeautyShot("smithy_fire", r.P("smithy_door") + new Vector3(1.5f, 1.6f, 1.5f), r.P("smithy_keeper") + new Vector3(1.2f, 1.2f, -2.2f), 55f, 18.5f, WeatherKind.Clear, 1.5f);
            Vector3 fire = r.P("camp_fire");
            yield return BeautyShot("camp_fire", fire + new Vector3(3.2f, 1.25f, -3.0f), fire + Vector3.up * 0.7f, 50f, 20.2f, WeatherKind.Clear, 7f);
            yield return BeautyShot("camp_fire_wide", fire + new Vector3(-9f, 2.4f, -12f), fire + Vector3.up * 1.6f, 55f, 20.2f, WeatherKind.Clear, 0.3f);

            yield return BeautyShot("night_train", T(0, 7.5f, 2.2f, 10f), T(0, 0f, 2.6f, -2f), 55f, 21.5f, WeatherKind.Clear, 6f);
            yield return BeautyShot("night_coach", T(3, 8f, 2.2f, 4f), T(3, 0f, 2.6f, -2f), 55f, 21.5f, WeatherKind.Clear, 0.2f);
            g.hideHud = false;
        }
    }
}
