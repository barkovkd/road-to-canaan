using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>Main menu → new game → save → quit to menu → continue.</summary>
        IEnumerator NewGame()
        {
            foreach (string slot in new[] { "auto", "save1", "save2", "save3" }) SaveSystem.Delete(slot);
            Game g = Game.I;
            g.clock.frozen = false;
            g.ToMainMenu();
            yield return WaitReal(2f);
            Check(g.inMainMenu && g.MenuOpen && g.region != null && g.region.id == "dryford", "the main menu is shown over the town");
            Look(g.region.P("menu") + new Vector3(-60f, 24f, -70f), g.region.P("menu") + Vector3.up * 4f, 46f);
            yield return null;
            RegionDryFord.SubmitScatter();
            Snap("main_menu");

            // with no saves the cursor already stands on «Новая игра»
            Tap(Btn.Accept);
            yield return UntilReal(() => g.playing && g.player != null && g.hud.fade < 0.05f, 12f);
            Check(g.playing && g.player != null, "a new game starts");
            if (g.player == null) yield break;
            Check(P.Find("pilgrim") != null && g.sat.Count("ammo_revolver") == 30 && Mathf.Approximately(g.stats.money, 12f), "the hero has his starting kit and twelve dollars");
            Check(g.ctrl.horse != null && U.FlatDist(g.ctrl.horse.Pos, P.Pos) < 30f, "his horse waits by the station");
            Check(Mathf.Abs(g.clock.hour - 9f) < 0.2f && g.clock.day == 1, "it is nine in the morning of the first day");
            Check(SaveSystem.Exists("auto"), "the game saved itself at the start");
            yield return Wait(1.5f);
            Snap("new_game");

            // walk a little, save by hand, go back to the menu, continue
            yield return Go(Vector3.forward, 1.5f);
            Vector3 where = P.Pos;
            g.stats.money = 33f;
            Tap(Btn.Pause); yield return Frames(4);
            for (int i = 0; i < 4; i++) { Tap(Btn.Down); yield return Frames(2); }
            Tap(Btn.Accept); yield return Frames(4);        // «Сохранить игру»
            Tap(Btn.Accept); yield return Frames(4);        // slot 1
            Check(SaveSystem.Exists("save1"), "saving from the pause menu writes the slot");
            g.ToMainMenu();
            yield return WaitReal(2.5f);
            Check(g.inMainMenu && g.player == null, "back at the main menu the world is reset");
            Tap(Btn.Accept);                                 // «Продолжить»
            yield return UntilReal(() => g.playing && g.player != null && g.hud.fade < 0.05f, 12f);
            Check(g.player != null && Mathf.Approximately(g.stats.money, 33f) && U.FlatDist(P.Pos, where) < 1.5f, "«Продолжить» restores the saved game");
            foreach (string slot in new[] { "auto", "save1" }) SaveSystem.Delete(slot);
        }
    }
}
