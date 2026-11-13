using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>Moves the camera, lets the scenery catch up for a frame, and takes a picture.</summary>
        IEnumerator View(string name, Vector3 from, Vector3 at, float fov = 50f)
        {
            Look(from, at, fov);
            yield return null;
            RegionDryFord.SubmitScatter();
            Snap(name);
        }

        IEnumerator Tour()
        {
            float t0 = Time.realtimeSinceStartup;
            yield return Game.I.LoadRegion("dryford");
            Log("region load took " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s real time");
            Game.I.hud.FadeTo(0f, 0f);
            Game.I.clock.frozen = true;
            Game.I.clock.hour = 10.5f;
            Region r = Game.I.region;
            Check(r != null && r.id == "dryford", "region is built");
            Check(World.terrain != null, "terrain exists");
            yield return null;

            Vector3 town = r.P("menu");
            yield return View("town_air", town + new Vector3(-120f, 70f, -110f), town + new Vector3(10f, 0f, 0f), 50f);
            yield return View("street_west", r.P("town_west") + new Vector3(-6f, 1.7f, 0f), r.P("town_east") + Vector3.up * 2.5f, 55f);
            yield return View("saloon", r.P("saloon_door") + new Vector3(-7f, 1.7f, -7f), r.P("saloon_door") + new Vector3(0f, 2.6f, 6f), 55f);
            yield return View("saloon_inside", r.P("saloon_inside") + new Vector3(3.5f, 1.7f, -0.6f), r.P("saloon_keeper") + new Vector3(2f, 1.0f, 2f), 70f);
            yield return View("store_inside", r.P("store_inside") + new Vector3(2f, 1.7f, -1.2f), r.P("store_keeper") + Vector3.up * 1.2f, 70f);
            yield return View("station", r.P("platform") + new Vector3(-22f, 4f, -16f), r.P("platform") + new Vector3(4f, 1.5f, 0f), 55f);
            yield return View("camp_air", r.P("camp_fire") + new Vector3(-34f, 30f, -38f), r.P("camp_fire"), 50f);
            yield return View("camp_ground", r.P("camp_gate") + new Vector3(-3f, 1.7f, -3f), r.P("camp_fire") + Vector3.up * 1.2f, 60f);
            yield return View("ford", r.P("ford_west") + new Vector3(-14f, 5f, -10f), r.P("ford_east") + Vector3.up, 55f);
            yield return View("ranch", r.P("ranch_yard") + new Vector3(-50f, 22f, 60f), r.P("ranch_yard"), 50f);
            yield return View("prairie", r.P("hunt_plain") + new Vector3(0f, 1.7f, 0f), r.P("hunt_plain") + new Vector3(60f, 4f, 40f), 60f);
            Vector3 butte = RegionDryFord.OnLand(RegionDryFord.Butte + new Vector2(190f, 120f));
            yield return View("butte", butte + Vector3.up * 3f, RegionDryFord.OnLand(RegionDryFord.Butte) + Vector3.up * 10f, 55f);
            yield return View("region_air", new Vector3(1024f, 950f, 300f), new Vector3(1024f, 0f, 1050f), 60f);

            Game.I.clock.hour = 21.5f;
            Game.I.clock.Tick(0.01f);
            yield return null;
            yield return View("street_night", r.P("town_west") + new Vector3(-6f, 1.7f, 0f), r.P("town_east") + Vector3.up * 2.5f, 55f);
            Game.I.clock.hour = 19.4f;
            Game.I.clock.Tick(0.01f);
            yield return View("camp_dusk", r.P("camp_gate") + new Vector3(-10f, 3f, -10f), r.P("camp_fire") + Vector3.up * 1.2f, 55f);
        }
    }
}
