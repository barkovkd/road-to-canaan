using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>The joinery of the buildings up close, and the doors that swing: the look-over after work on Architect.</summary>
        IEnumerator Facades()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            Human hero = P;
            foreach (string m in new[] { "bk_stile", "bk_head", "bk_sill", "bk_sash", "bk_doorleaf", "bk_batwing", "bk_barndoor", "bk_post", "bk_railing", "bk_cornice", "bk_lantern", "bk_window_arch" })
                Check(Canaan.Models.Has(m), "the kit has " + m);
            Check(Doors.Count >= 8, "doors are hung (" + Doors.Count + ")");

            // a building's own frame: x along the front, z into the house; the north row faces south, the south row north
            // (the door mark stands 3.5 m out on the porch floor; the barn and the forge have no porch)
            Vector3 Origin(string id, bool south)
            {
                float lift = id == "stable" || id == "smithy" ? 0f : Architect.Floor, back = id == "stable" ? 2.5f : id == "smithy" ? 2f : 3.5f;
                return r.P(id + "_door") + new Vector3(0f, -lift, south ? -back : back);
            }
            Vector3 B(string id, bool south, float x, float y, float z) { return Origin(id, south) + (south ? new Vector3(-x, y, -z) : new Vector3(x, y, z)); }
            float f = Architect.Floor;

            // out of the way: the hero waits behind the camera unless a shot needs somebody at a door
            hero.Teleport(RegionDryFord.OnLand(new Vector2(r.P("town_west").x - 30f, r.P("town_west").z)), 90f);
            yield return Wait(0.8f);

            yield return BeautyShot("saloon_front", B("saloon", false, -5.5f, 2.1f, -8.5f), B("saloon", false, 0f, 2.3f, 0f), 55f, 14.5f);
            yield return BeautyShot("saloon_window", B("saloon", false, -2.5f, 1.95f, -1.9f), B("saloon", false, -3.6f, 1.95f, 0f), 50f, 14.5f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("saloon_window_in", B("saloon", false, -2.2f, 1.9f, 2.6f), B("saloon", false, -3.6f, 1.95f, 0f), 50f, 14.5f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("saloon_porch_side", B("saloon", false, -9.6f, 2.0f, -1.2f), B("saloon", false, 0f, 2.4f, -1.2f), 45f, 14.5f, WeatherKind.Clear, 0.1f);
            float shut = Doors.OpenNear(B("saloon", false, 0f, f, 0f));
            yield return BeautyShot("saloon_doors_shut", B("saloon", false, 1.7f, 1.55f, -2.3f), B("saloon", false, 0f, 1.25f, 0f), 50f, 14.5f, WeatherKind.Clear, 0.1f);
            hero.Teleport(B("saloon", false, 0f, f, -1.0f), 0f);
            yield return Wait(1.0f);
            float open = Doors.OpenNear(B("saloon", false, 0f, f, 0f));
            Log("saloon batwings: " + shut.ToString("0") + " deg with the porch empty, " + open.ToString("0") + " deg with the hero at them");
            Check(open > 40f, "the batwings give way before the hero (" + open.ToString("0") + " deg)");
            yield return BeautyShot("saloon_doors_open", B("saloon", false, 1.7f, 1.55f, -2.3f), B("saloon", false, 0f, 1.25f, 0f), 50f, 14.5f, WeatherKind.Clear, 0.1f);

            hero.Teleport(B("store", false, 3f, f, -6f), 0f);
            yield return Wait(1.0f);
            yield return BeautyShot("store_door_shut", B("store", false, 2.2f, 1.7f, -2.6f), B("store", false, 0.2f, 1.45f, 0f), 50f, 14.5f, WeatherKind.Clear, 0.1f);
            hero.Teleport(B("store", false, 0f, f, -1.3f), 0f);
            yield return Wait(1.2f);
            open = Doors.OpenNear(B("store", false, 0f, f, 0f));
            Check(open > 60f, "the store door swings in before the hero (" + open.ToString("0") + " deg)");
            yield return BeautyShot("store_door_open", B("store", false, 2.2f, 1.7f, -2.6f), B("store", false, 0.2f, 1.45f, 0f), 50f, 14.5f, WeatherKind.Clear, 0.1f);
            hero.Teleport(RegionDryFord.OnLand(new Vector2(r.P("town_west").x - 30f, r.P("town_west").z)), 90f);
            yield return Wait(0.5f);
            yield return BeautyShot("store_cornice", B("store", false, -3.4f, 4.7f, -5f), B("store", false, 0.6f, 5.9f, 0f), 45f, 14.5f, WeatherKind.Clear, 0.1f);

            yield return BeautyShot("hotel_front", B("hotel", false, -6.5f, 2.6f, -13f), B("hotel", false, 0f, 3.6f, 0f), 55f, 15.5f);
            yield return BeautyShot("hotel_balcony", B("hotel", false, 4.2f, 4.9f, -6f), B("hotel", false, 0.6f, 3.9f, 0f), 45f, 15.5f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("hotel_door", B("hotel", false, 2.0f, 1.75f, -2.1f), B("hotel", false, 0.3f, 1.5f, 0f), 50f, 15.5f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("bank_front", B("bank", false, 4.5f, 1.9f, -7f), B("bank", false, 0f, 2.3f, 0f), 55f, 15.5f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("house_front", B("house_n1", false, -5.5f, 2.3f, -9.5f), B("house_n1", false, 0f, 2.4f, 2f), 50f, 15.5f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("house_window", B("house_n1", false, -1.0f, 1.9f, -1.7f), B("house_n1", false, -2.06f, 1.95f, 0f), 50f, 15.5f, WeatherKind.Clear, 0.1f);

            // the south row stands with its back to the sun: late light from the west rakes its fronts
            yield return BeautyShot("stable_front", B("stable", true, -7f, 2.3f, -11f), B("stable", true, 0f, 2.9f, 0f), 55f, 17.6f);
            yield return BeautyShot("stable_side", B("stable", true, -12f, 2.2f, 4f), B("stable", true, -6f, 2.2f, 7f), 55f, 17.6f, WeatherKind.Clear, 0.1f);
            Vector3 church = r.P("church_door") + new Vector3(0f, 0f, -1.5f);                    // no porch: the door mark is 1.5 m out
            Vector3 C(float x, float y, float z) { return church + new Vector3(-x, y, -z); }
            yield return BeautyShot("church", C(-7.5f, 2.4f, -9f), C(0f, 3.0f, 4f), 55f, 17.6f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("church_window", C(-6.2f, 2.2f, 3.2f), C(-4f, 2.3f, 5.6f), 50f, 17.6f, WeatherKind.Clear, 0.1f);
            yield return BeautyShot("smithy", B("smithy", true, -4.5f, 2.0f, -7f), B("smithy", true, 0f, 1.8f, 2f), 55f, 17.6f, WeatherKind.Clear, 0.1f);

            yield return BeautyShot("night_lanterns", B("store", false, -6f, 1.9f, -7f), B("store", false, 2f, 2.4f, 0f), 55f, 21.5f, WeatherKind.Clear, 0.8f);
            yield return BeautyShot("night_hotel", B("hotel", false, 6f, 2.2f, -11f), B("hotel", false, 0f, 3.4f, 0f), 55f, 21.5f, WeatherKind.Clear, 0.3f);
            g.hideHud = false;
        }
    }
}
