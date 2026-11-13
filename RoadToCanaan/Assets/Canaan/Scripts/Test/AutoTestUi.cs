using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        IEnumerator Ui()
        {
            StartStudioPlay();
            Game g = Game.I;
            g.sat.Add("beans", 3); g.sat.Add("tonic_health", 1); g.sat.Add("herb_sage", 4); g.sat.Add("pelt_pronghorn_3", 1);
            g.sat.Add("meat_game", 2); g.sat.Add("tobacco", 2); g.sat.Add("pocket_watch", 1); g.sat.Add("gun_oil", 1);
            g.stats.healthCore = 40f; g.stats.stamina = 55f; g.stats.cool = 70f; g.stats.staminaCore = 15f;
            P.health = 70f;
            g.Notify("Привязанность лошади выросла: уровень 2");
            g.Notify("Честь выросла");
            g.hud.objective = "Доберитесь до лагеря переселенцев";
            g.hud.Subtitle("Марта", "Капитан Уитлок? Мы ждали вас ещё вчера.", 30f);
            g.hud.wantedText = "РАЗЫСКИВАЕТСЯ · 15,00 $";
            Human c = Spawner.Civilian(new Vector3(-2.5f, 0f, 6f), 160f);
            g.hud.Say(c, "Добрый день, мистер.", 30f);
            Tap(Btn.Slot2);
            yield return Wait(0.6f);
            yield return LookAt(new Vector3(-1f, 1.2f, 8f), 1f);
            Snap("hud");
            Check(g.hud.root.gameObject.activeSelf, "HUD is shown during play");

            Hold(Btn.Aim, true);
            yield return LookAt(() => c.Chest, 1f);
            yield return Wait(0.3f);
            Snap("hud_aim");
            Hold(Btn.Aim, false);
            g.hud.wantedText = "";

            // hold the wheel and point at the carbine
            Hold(Btn.Wheel, true);
            yield return Frames(3);
            Check(g.menus.wheel && Time.timeScale < 0.3f, "holding the wheel button opens the weapon wheel and slows time");
            GameInput.vLook = new Vector2(8f, -4f);
            yield return Frames(6);
            GameInput.vLook = Vector2.zero;
            yield return Frames(2);
            Snap("wheel");
            Hold(Btn.Wheel, false);
            yield return WaitReal(0.3f);
            Check(!g.menus.wheel && P.weapon != null && P.weapon.def.id == "drover", "releasing on the carbine equips it");

            Tap(Btn.Pause);
            yield return Frames(3);
            Check(g.MenuOpen && Time.timeScale == 0f, "pause menu stops the game");
            Snap("pause");
            Tap(Btn.Down); yield return Frames(2);
            Tap(Btn.Down); yield return Frames(2);
            Tap(Btn.Accept); yield return Frames(3);
            Snap("satchel");
            int beans = g.sat.Count("beans");
            float core = g.stats.healthCore;
            Tap(Btn.Accept); yield return Frames(3);
            Check(g.sat.Count("beans") == beans - 1 && g.stats.healthCore > core, "eating from the satchel uses the item and refills the reserve");
            Tap(Btn.Back); yield return Frames(3);
            Tap(Btn.Down); yield return Frames(2);
            Tap(Btn.Accept); yield return Frames(3);
            Snap("hero");
            Tap(Btn.Back); yield return Frames(3);
            Tap(Btn.Back); yield return Frames(3);
            Check(!g.MenuOpen && Time.timeScale == 1f, "closing the menu resumes the game");

            Tap(Btn.Map);
            yield return Frames(3);
            Check(g.MenuOpen, "map opens");
            Hold(Btn.Up, true); yield return WaitReal(0.4f); Hold(Btn.Up, false);
            Tap(Btn.Accept); yield return Frames(3);
            Check(g.menus.waypoint.HasValue, "a waypoint can be placed on the map");
            Snap("map");
            Tap(Btn.Back); yield return Frames(3);

            // save, change things, load
            float money = g.stats.money;
            Vector3 where = P.Pos;
            g.Save("save1");
            g.stats.money += 50f;
            g.sat.Add("beans", 10);
            yield return GoTo(P.Pos + new Vector3(8f, 0f, 0f), 1f, 6f);
            g.LoadGame("save1");
            yield return WaitReal(2.5f);
            Check(g.player != null && Mathf.Approximately(g.stats.money, money), "loading restores money");
            Check(g.sat.Count("beans") == beans - 1, "loading restores the satchel");
            Check(g.player != null && U.FlatDist(g.player.Pos, where) < 1.5f, "loading restores the position");
            Check(g.player != null && g.player.Find("drover") != null && g.ctrl.horse != null, "loading restores weapons and the horse");
            yield return Wait(1.5f);
            Snap("after_load");
        }
    }
}
