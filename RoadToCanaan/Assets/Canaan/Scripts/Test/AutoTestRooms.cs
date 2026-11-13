using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>Inside every open house by day and the saloon and store by lamplight: wainscot and paper, the
        /// ceiling, the lamps, the stove, the things on the walls (Architect.Interior, interior.py).</summary>
        IEnumerator Rooms()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            g.scriptLock = true;
            foreach (string m in new[] { "in_wainscot", "in_lamp_hang", "in_sconce", "in_wheel_lamp", "in_stove", "in_pipe", "in_piano", "in_mirror",
                                         "in_painting", "in_portrait", "in_clock", "in_antlers", "in_spittoon", "in_desk", "in_cabinet", "in_poster", "in_rug", "in_scale" })
                Check(Canaan.Models.Has(m), "the library has " + m);
            P.Teleport(RegionDryFord.OnLand(new Vector2(r.P("town_west").x - 30f, r.P("town_west").z)), 90f);

            // a point in a house's own frame: x across the front, y over the floor, z in from the front wall
            Vector3 In(string id, float x, float y, float z)
            {
                Vector3 o = r.P(id + "_inside");
                return o + Quaternion.Euler(0f, r.Yaw(id + "_inside"), 0f) * new Vector3(x, y, z - 2.2f);
            }
            const float day = 14f, night = 21.5f;
            yield return BeautyShot("saloon_bar", In("saloon", 3.2f, 1.65f, 1.0f), In("saloon", -4.5f, 2.1f, 6.0f), 70f, day, WeatherKind.Clear, 0.6f);
            yield return BeautyShot("saloon_room", In("saloon", -2.5f, 1.7f, 1.0f), In("saloon", 4.5f, 1.8f, 8.0f), 70f, day);
            yield return BeautyShot("saloon_night", In("saloon", 3.2f, 1.65f, 1.0f), In("saloon", -4.5f, 2.1f, 6.0f), 70f, night, WeatherKind.Clear, 0.6f);
            yield return BeautyShot("saloon_up", In("saloon", -1.0f, 1.6f, 2.0f), In("saloon", 1.6f, 3.3f, 6.6f), 70f, night);
            yield return BeautyShot("store", In("store", 3.0f, 1.7f, 0.8f), In("store", -1.5f, 1.3f, 7.0f), 70f, day);
            yield return BeautyShot("store_night", In("store", 3.0f, 1.7f, 0.8f), In("store", -1.5f, 1.3f, 7.0f), 70f, night);
            yield return BeautyShot("gunsmith", In("gunsmith", 2.5f, 1.7f, 0.8f), In("gunsmith", -2.0f, 1.4f, 6.0f), 70f, day);
            yield return BeautyShot("doctor", In("doctor", 2.2f, 1.7f, 0.8f), In("doctor", -2.0f, 1.2f, 5.0f), 70f, day);
            yield return BeautyShot("office", In("office", -2.5f, 1.7f, 0.8f), In("office", 2.0f, 1.4f, 6.0f), 70f, day);
            yield return BeautyShot("station", In("station", -4.0f, 1.7f, 0.8f), In("station", 3.0f, 1.3f, 5.5f), 70f, day);
            yield return BeautyShot("sheriff", In("sheriff", 3.0f, 1.7f, 0.8f), In("sheriff", -3.0f, 1.5f, 4.5f), 70f, day);
        }
    }
}
