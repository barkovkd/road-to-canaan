using System;
using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        IEnumerator UntilReal(Func<bool> condition, float seconds)
        {
            float end = Game.UTime + seconds;
            while (Game.UTime < end && !condition()) yield return null;
        }

        IEnumerator WaitReal(float seconds)
        {
            float end = Game.UTime + seconds;
            while (Game.UTime < end) yield return null;
        }

        /// <summary>Holds aim on a living target and fires when the sights are on it.</summary>
        IEnumerator ShootAt(Actor a, float timeout, bool head = false)
        {
            Hold(Btn.Aim, true);
            float end = Time.time + timeout;
            while (Time.time < end && !a.dead)
            {
                Vector3 p = head ? a.HeadPos : a.Chest;
                Vector3 dir = (p - Game.I.cam.transform.position).normalized;
                float dy = Mathf.DeltaAngle(Rig.yaw, U.Yaw(dir));
                float dp = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg - Rig.pitch;
                GameInput.vLook = new Vector2(Mathf.Clamp(dy * 0.6f, -14f, 14f), -Mathf.Clamp(dp * 0.6f, -14f, 14f));
                if (Mathf.Abs(dy) < 0.5f && Mathf.Abs(dp) < 0.5f && P.CanFire && !P.reloading && P.weapon != null && P.weapon.loaded > 0) Tap(Btn.Fire);
                yield return null;
            }
            GameInput.vLook = Vector2.zero;
            Hold(Btn.Aim, false);
            Log("  shootAt " + a.displayName + ": dead=" + a.dead + " hp=" + a.health.ToString("0") + " dist=" + Vector3.Distance(a.Pos, P.Pos).ToString("0")
                + " | hero hp=" + P.health.ToString("0") + " dead=" + P.dead + " shots=" + P.ShotsFired + " loaded=" + (P.weapon != null ? P.weapon.loaded : -1)
                + " aimed=" + (Ctrl.aimedActor != null ? Ctrl.aimedActor.displayName : "none") + " crouch=" + P.crouch + " cover=" + Ctrl.inCover);
        }

        IEnumerator Combat()
        {
            StartStudioPlay();
            Ctrl.horse.Teleport(new Vector3(-30f, 0f, -20f), 0f);
            yield return Wait(0.3f);

            // ---- aimed fire
            Human e1 = Spawner.Outlaw(new Vector3(-5f, 0f, 26f), 180f);
            Human e2 = Spawner.Outlaw(new Vector3(1f, 0f, 30f), 180f, 0.45f, true);
            Human e3 = Spawner.Outlaw(new Vector3(7f, 0f, 27f), 180f);
            foreach (Human e in new[] { e1, e2, e3 }) ((Gunman)e.brain).holdFire = true;
            Tap(Btn.Slot2);
            yield return Wait(0.5f);
            Check(P.weapon != null && P.weapon.def.cls == WeaponClass.Revolver, "slot 2 draws the revolver");
            int ammo = Game.I.sat.Count("ammo_revolver");
            Hold(Btn.Aim, true);
            yield return LookAt(() => e1.Chest, 1.5f);
            yield return Wait(0.3f);
            Check(Ctrl.aimedActor == e1, "the sights are on the first outlaw");
            Snap("aim");
            yield return ShootAt(e1, 10f);
            Check(e1.dead, "aimed revolver fire kills the first outlaw (shots " + P.ShotsFired + ")");
            Check(P.ShotsFired >= 2 && P.ShotsFired <= 6, "it takes 2 to 6 shots at 26 m");
            yield return Wait(1.2f);
            Snap("first_down");

            // ---- slow motion with marks
            Tap(Btn.Reload);
            yield return Wait(3f);
            Check(P.weapon.loaded == 6, "reload fills the cylinder");
            Hold(Btn.Aim, true);
            yield return LookAt(() => e2.Chest, 1.5f);
            float coolBefore = Game.I.stats.cool;
            Tap(Btn.DeadEye);
            yield return Frames(3);
            Check(Ctrl.slowmo && Time.timeScale < 0.5f, "slow motion starts");
            yield return WaitReal(0.6f);
            yield return LookAt(() => e3.Chest, 1.5f);
            yield return WaitReal(0.6f);
            Check(Ctrl.marks.Count >= 2, "marks are placed on targets under the sights (" + Ctrl.marks.Count + ")");
            Snap("marks");
            float h2 = e2.health, h3 = e3.health;
            Tap(Btn.Fire);
            yield return UntilReal(() => !Ctrl.slowmo, 4f);
            yield return Frames(2);
            Check(!Ctrl.slowmo && Mathf.Approximately(Time.timeScale, 1f), "slow motion ends after the marked shots");
            Check(e2.health < h2 && e3.health < h3, "both marked outlaws are hit");
            Check(Game.I.stats.cool < coolBefore, "slow motion spends the cool bar");
            Hold(Btn.Aim, false);
            yield return ShootAt(e2, 8f);
            yield return ShootAt(e3, 8f);
            Check(e2.dead && e3.dead, "the other two outlaws are finished off");
            Check(Game.I.sat.Count("ammo_revolver") < ammo, "reloading takes cartridges from the satchel");
            yield return Wait(1f);
            Snap("all_down");

            // ---- cover
            Game.Block(new Vector3(0f, 0.55f, 12f), new Vector3(3.4f, 1.1f, 0.4f), Pal.WoodOld);
            Human e4 = Spawner.Outlaw(new Vector3(-6f, 0f, 44f), 180f, 0.6f, true);
            Human e5 = Spawner.Outlaw(new Vector3(6f, 0f, 42f), 180f, 0.6f);
            foreach (Human e in new[] { e4, e5 }) ((Gunman)e.brain).holdFire = true;
            yield return GoTo(new Vector3(0f, 0f, 11.2f), 0.5f, 10f, true);
            yield return LookAt(new Vector3(0f, 1.2f, 40f), 1.5f);
            Tap(Btn.Cover);
            yield return Wait(0.6f);
            Check(Ctrl.inCover && P.crouch, "hero takes cover behind the low wall and crouches");
            Snap("cover");
            foreach (Human e in new[] { e4, e5 }) ((Gunman)e.brain).holdFire = false;
            float hp = P.health;
            yield return Wait(6f);
            float lost = hp - P.health;
            Check(lost < 40f, "behind cover the hero takes little damage (" + lost.ToString("0") + ")");
            Tap(Btn.Slot3);
            yield return Wait(0.5f);
            Check(P.weapon != null && P.weapon.def.id == "drover", "slot 3 takes the carbine");
            yield return ShootAt(e4, 14f);
            yield return ShootAt(e5, 14f);
            Check(e4.dead && e5.dead, "carbine from cover kills both riflemen");
            Check(Ctrl.inCover, "aiming does not leave cover");
            Snap("cover_fight");
            Tap(Btn.Cover);
            yield return Wait(0.3f);
            Check(!Ctrl.inCover, "cover button leaves cover");

            // ---- an outlaw who is allowed to shoot hurts the hero
            Human e6 = Spawner.Outlaw(P.Pos + new Vector3(0f, 0f, 14f), 180f, 0.9f);
            hp = P.health;
            yield return Until(() => P.health < hp - 10f, 12f);
            Check(P.health < hp - 10f, "an outlaw in the open hits the hero");
            yield return ShootAt(e6, 14f);
            Check(e6.dead, "and is shot down");
            hp = P.health;
            yield return Wait(8f);
            Check(P.health > hp + 3f, "health comes back after a few quiet seconds");

            // ---- fists
            Human c = Spawner.Civilian(P.Pos + new Vector3(1.2f, 0f, 0f), 270f);
            Tap(Btn.Slot1);
            yield return Wait(0.4f);
            Check(P.weapon == null, "slot 1 puts the weapon away");
            yield return LookAt(() => c.Chest, 1.2f);
            Hold(Btn.Aim, true);
            for (int i = 0; i < 14 && !c.knockedOut; i++) { Tap(Btn.Fire); yield return Wait(0.5f); }
            Hold(Btn.Aim, false);
            Check(c.knockedOut && !c.dead, "fists knock a man out without killing him");
            Snap("knockout");
        }
    }
}
