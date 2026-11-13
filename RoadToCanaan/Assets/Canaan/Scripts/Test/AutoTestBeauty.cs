using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>A fixed shot with the interface hidden: the picture the game makes, at a given hour and weather.</summary>
        IEnumerator BeautyShot(string name, Vector3 from, Vector3 at, float fov, float hour, WeatherKind weather = WeatherKind.Clear, float settle = 0.4f)
        {
            Game g = Game.I;
            g.clock.hour = hour;
            g.clock.SetWeather(weather, true);
            g.rig.mode = CamMode.Fixed;
            g.rig.fixedPos = from;
            g.rig.fixedLook = at;
            g.cam.fieldOfView = fov;
            yield return Wait(settle);
            g.cam.fieldOfView = fov;
            RegionDryFord.SubmitScatter();
            Snap(name);
        }

        /// <summary>The comparison set for the look of the game: same places, same hours, every build.</summary>
        IEnumerator Beauty()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            Human hero = P;
            g.scriptLock = true;                          // the camera is set by hand: the hero must not turn to follow it
            hero.looking = false;
            Horse horse = g.ctrl.horse;
            Vector3 west = r.P("town_west"), east = r.P("town_east");
            Vector3 along = (east - west).normalized, across = Vector3.Cross(Vector3.up, along);
            yield return Wait(6f);                       // the chimneys' smoke takes this long to stand

            // 1. the street at noon, the hero in the foreground
            Vector3 hp = RegionDryFord.OnLand(new Vector2(west.x, west.z) + new Vector2(along.x, along.z) * 14f);
            hero.Teleport(hp, U.Yaw(along));
            horse.Teleport(RegionDryFord.OnLand(new Vector2(hp.x, hp.z) + new Vector2(across.x, across.z) * 3f + new Vector2(along.x, along.z) * 4f), U.Yaw(along) + 20f);
            yield return BeautyShot("street_noon", hp - along * 4.2f + across * 0.9f + Vector3.up * 1.75f, hp + along * 30f + Vector3.up * 2.2f, 55f, 12f);

            // 2. the same street an hour before sunset, looking into the light
            Vector3 mid = RegionDryFord.OnLand(new Vector2(west.x, west.z) + new Vector2(along.x, along.z) * 110f);
            hero.Teleport(mid, U.Yaw(along));
            horse.Teleport(RegionDryFord.OnLand(new Vector2(mid.x, mid.z) - new Vector2(across.x, across.z) * 2.5f - new Vector2(along.x, along.z) * 5f), U.Yaw(along) - 15f);
            yield return BeautyShot("street_golden", mid + along * 6f - across * 1.4f + Vector3.up * 1.45f, mid - along * 30f + Vector3.up * 2.6f, 48f, 18.9f);
            hero.Teleport(hp, U.Yaw(along));

            // 3. the hero's face and clothes
            Vector3 face = hp + Vector3.up * 1.62f;
            Vector3 lens = face + (along * 1.5f + across * 0.9f) + Vector3.up * 0.05f;
            hero.looking = true;
            hero.lookPoint = lens;
            yield return BeautyShot("hero_front", lens, face, 38f, 17.3f);
            hero.looking = false;
            yield return BeautyShot("hero_back", hp - along * 2.6f - across * 0.7f + Vector3.up * 1.7f, hp + along * 4f + Vector3.up * 1.3f, 50f, 17.3f);

            // 4. in the saddle on the open plain
            Vector3 plain = r.P("hunt_plain");
            horse.Teleport(plain, 70f);
            hero.Teleport(plain + Vector3.right, 70f);
            hero.Mount(horse, true);
            yield return Wait(0.5f);
            Vector3 hf = U.Dir(70f), hs = Vector3.Cross(Vector3.up, hf);
            yield return BeautyShot("rider_side", plain + hs * 6.5f + hf * 2.5f + Vector3.up * 1.5f, plain + Vector3.up * 1.5f, 42f, 17.8f);
            yield return BeautyShot("rider_morning", plain - hf * 7f - hs * 2.5f + Vector3.up * 2.6f, plain + hf * 60f + Vector3.up * 3f, 55f, 7.2f);
            hero.Dismount();
            yield return Wait(1.2f);

            // 5. the river
            Vector3 fw = r.P("ford_west"), fe = r.P("ford_east");
            yield return BeautyShot("ford", fw + new Vector3(-10f, 3.2f, -8f), fe + Vector3.up * 0.5f, 55f, 9.5f);

            // 6. the camp at dusk
            Vector3 fire = r.P("camp_fire");
            yield return BeautyShot("camp_dusk", r.P("camp_gate") + new Vector3(-6f, 2.2f, -6f), fire + Vector3.up * 1.1f, 55f, 19.6f, WeatherKind.Clear, 6f);
            yield return BeautyShot("camp_night", fire + new Vector3(5.5f, 1.5f, -4.5f), fire + Vector3.up * 0.9f, 55f, 22.5f);

            // 7. indoors
            yield return BeautyShot("saloon_inside", r.P("saloon_inside") + new Vector3(3.5f, 1.7f, -0.6f), r.P("saloon_keeper") + new Vector3(2f, 1.0f, 2f), 70f, 14f);

            // 8. the town at night and in the rain
            yield return BeautyShot("street_night", west + along * 8f + Vector3.up * 1.7f, east + Vector3.up * 2.5f, 55f, 22f);
            yield return BeautyShot("street_rain", west + along * 8f + Vector3.up * 1.7f, east + Vector3.up * 2.5f, 55f, 15f, WeatherKind.Rain, 6f);

            // 9. far views
            Vector3 town = r.P("menu");
            yield return BeautyShot("town_air", town + new Vector3(-120f, 70f, -110f), town + new Vector3(10f, 0f, 0f), 50f, 17.5f);
            Vector3 butte = RegionDryFord.OnLand(RegionDryFord.Butte + new Vector2(190f, 120f));
            yield return BeautyShot("butte_sunset", butte + Vector3.up * 3f, RegionDryFord.OnLand(RegionDryFord.Butte) + Vector3.up * 10f, 55f, 19.3f);
            g.scriptLock = false;
            g.hideHud = false;
        }
    }
}
