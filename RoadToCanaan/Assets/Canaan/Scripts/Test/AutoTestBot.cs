using System;
using System.Collections;
using UnityEngine;

namespace Canaan
{
    /// <summary>The bot: drives the hero through the virtual input device, the way a player would.</summary>
    public partial class AutoTest
    {
        Human P => Game.I.player;
        PlayerController Ctrl => Game.I.ctrl;
        CameraRig Rig => Game.I.rig;

        static void Hold(Btn b, bool on) { GameInput.vHeld[(int)b] = on; }
        static void Tap(Btn b) { GameInput.Tap(b); }

        /// <summary>Pushes the stick so the hero (or his horse) goes in a world direction.</summary>
        void Stick(Vector3 worldDir, float amount = 1f)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-6f) { GameInput.vMove = Vector2.zero; return; }
            Vector3 local = Quaternion.Euler(0f, -Rig.yaw, 0f) * worldDir.normalized;
            GameInput.vMove = new Vector2(local.x, local.z) * amount;
        }

        void Release()
        {
            GameInput.vMove = Vector2.zero;
            GameInput.vLook = Vector2.zero;
            for (int i = 0; i < (int)Btn.Count; i++) GameInput.vHeld[i] = false;
        }

        IEnumerator Go(Vector3 worldDir, float seconds, params Btn[] held)
        {
            foreach (Btn b in held) Hold(b, true);
            float end = Time.time + seconds;
            while (Time.time < end) { Stick(worldDir); yield return null; }
            GameInput.vMove = Vector2.zero;
            foreach (Btn b in held) Hold(b, false);
        }

        /// <summary>Walks, runs or rides to a point. Returns when within 'tolerance' or after 'timeout' seconds.</summary>
        IEnumerator GoTo(Vector3 point, float tolerance = 1.2f, float timeout = 30f, bool fast = false)
        {
            float end = Time.time + timeout;
            if (fast) Hold(Btn.Sprint, true);
            while (Time.time < end)
            {
                Vector3 from = P.Mounted ? P.mount.transform.position : P.transform.position;
                Vector3 to = point - from;
                to.y = 0f;
                if (to.magnitude <= tolerance) break;
                Stick(to, to.magnitude < 3f && fast ? 0.5f : 1f);
                yield return null;
            }
            GameInput.vMove = Vector2.zero;
            Hold(Btn.Sprint, false);
        }

        /// <summary>Turns the camera with the look stick until the screen centre is on the point.</summary>
        IEnumerator LookAt(Func<Vector3> point, float maxSeconds = 2f, float hold = 0f)
        {
            float end = Game.UTime + maxSeconds;
            float heldUntil = -1f;
            while (Game.UTime < end)
            {
                Vector3 dir = (point() - Game.I.cam.transform.position).normalized;
                float dy = Mathf.DeltaAngle(Rig.yaw, U.Yaw(dir));
                float dp = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg - Rig.pitch;
                GameInput.vLook = new Vector2(Mathf.Clamp(dy * 0.5f, -14f, 14f), -Mathf.Clamp(dp * 0.5f, -14f, 14f));
                if (Mathf.Abs(dy) < 0.25f && Mathf.Abs(dp) < 0.25f)
                {
                    if (heldUntil < 0f) heldUntil = Game.UTime + hold;
                    if (Game.UTime >= heldUntil) break;
                }
                yield return null;
            }
            GameInput.vLook = Vector2.zero;
        }

        IEnumerator LookAt(Vector3 point, float maxSeconds = 2f) { return LookAt(() => point, maxSeconds); }

        IEnumerator Until(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (Time.time < end && !condition()) yield return null;
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        void StartStudioPlay()
        {
            Game.I.SpawnPlayer(Vector3.zero, 0f);
            Game.I.hud.FadeTo(0f, 0f);
            Game.I.SpawnPlayerHorse(new Vector3(5f, 0f, 4f), 200f);
            P.Give("pilgrim"); P.Give("drover"); P.Give("thunder"); P.Give("knife");
            Game.I.sat.Add("ammo_revolver", 60); Game.I.sat.Add("ammo_rifle", 40); Game.I.sat.Add("ammo_shell", 20);
        }

        // ------------------------------------------------------------ scenarios: on foot and on horseback

        IEnumerator Move()
        {
            StartStudioPlay();
            yield return Wait(0.5f);
            Vector3 a = P.Pos;
            yield return Go(Vector3.forward, 3f);
            float jog = U.FlatDist(a, P.Pos);
            Check(jog > 8.5f && jog < 10.6f, "jog 3 s covers ~10 m (got " + jog.ToString("0.0") + ")");
            Check(Mathf.Abs(P.Pos.x) < 0.5f, "jog goes straight along the camera direction");
            Snap("jog");

            float st = Game.I.stats.stamina;
            a = P.Pos;
            yield return Go(Vector3.forward, 2f, Btn.Sprint);
            float spr = U.FlatDist(a, P.Pos);
            Check(spr > 10.5f && spr < 12.6f, "sprint 2 s covers ~11.5 m (got " + spr.ToString("0.0") + ")");
            Check(Game.I.stats.stamina < st - 8f, "sprinting spends stamina");

            Tap(Btn.Walk);
            a = P.Pos;
            yield return Go(Vector3.right, 2f);
            float walk = U.FlatDist(a, P.Pos);
            Check(walk > 2.4f && walk < 3.4f, "walk toggle: 2 s covers ~3 m (got " + walk.ToString("0.0") + ")");
            Check(Mathf.Abs(Mathf.DeltaAngle(P.yaw, 90f)) < 20f, "hero turns to face the direction of travel");
            Tap(Btn.Walk);
            yield return null;

            Tap(Btn.Crouch);
            yield return Go(Vector3.right, 1.5f);
            Check(P.crouch, "crouch toggles on");
            Snap("crouch");
            Tap(Btn.Crouch);
            yield return Wait(0.4f);

            Tap(Btn.Jump);
            float top = 0f;
            for (int i = 0; i < 40; i++) { top = Mathf.Max(top, P.Pos.y); yield return null; }
            Check(top > 0.5f, "jump lifts the hero (peak " + top.ToString("0.00") + " m)");
            yield return Until(() => P.grounded, 2f);
            Check(P.grounded && P.Pos.y < 0.1f, "hero lands back on the ground");

            // the camera stays behind the shoulder and out of the wall
            Game.Block(P.Pos - new Vector3(0, -1.5f, 1.2f), new Vector3(8f, 3f, 0.4f), Pal.WoodOld);
            yield return LookAt(P.Pos + Vector3.forward * 20f + Vector3.up * 1.5f, 1.5f);
            yield return Wait(0.3f);
            float back = Vector3.Dot(P.Pos - Game.I.cam.transform.position, Vector3.forward);
            Check(back > 0.2f && back < 1.4f, "camera is pulled in front of the wall behind the hero (" + back.ToString("0.00") + " m)");
            Snap("camera_wall");

            Tap(Btn.View);
            yield return Wait(0.3f);
            Check(Rig.mode == CamMode.FirstPerson, "view button switches to first person");
            Snap("first_person");
            Tap(Btn.View);
            yield return Wait(0.2f);
            Check(Rig.mode == CamMode.Follow, "view button switches back");

            // the head follows the camera only so far, lets it go behind the shoulder and never jumps across
            yield return Wait(0.6f);
            float HeadYaw()
            {
                Quaternion rel = Quaternion.Inverse(P.rig.root.rotation) * P.rig.B(HB.Head).rotation;
                return Mathf.DeltaAngle(0f, rel.eulerAngles.y);
            }
            float turned = 0f, prevCam = Rig.yaw, prevHead = HeadYaw(), maxHead = 0f, maxStep = 0f, faceYaw = P.yaw;
            bool shotFace = false;
            for (int f = 0; f < 600 && turned < 400f; f++)
            {
                GameInput.vLook = new Vector2(3f, 0f);         // 180 degrees a second
                yield return null;
                turned += Mathf.Abs(Mathf.DeltaAngle(prevCam, Rig.yaw));
                prevCam = Rig.yaw;
                float h = HeadYaw();
                maxHead = Mathf.Max(maxHead, Mathf.Abs(h));
                maxStep = Mathf.Max(maxStep, Mathf.Abs(Mathf.DeltaAngle(prevHead, h)));
                prevHead = h;
                if (!shotFace && Mathf.Abs(Mathf.DeltaAngle(Rig.yaw, faceYaw + 180f)) < 4f)
                {
                    GameInput.vLook = Vector2.zero;
                    yield return Wait(0.6f);
                    float atFace = HeadYaw();
                    Check(Mathf.Abs(atFace) < 12f, "with the camera at his face the hero looks straight ahead (" + atFace.ToString("0") + "°)");
                    Snap("head_camera_front");
                    shotFace = true;
                    prevHead = HeadYaw();
                }
            }
            GameInput.vLook = Vector2.zero;
            Check(maxHead < 80f, "the head turns at most about 75° after the camera (max " + maxHead.ToString("0") + "°)");
            Check(maxStep < 8f, "the head never jumps when the camera passes behind it (largest step " + maxStep.ToString("0.0") + "° a frame)");
            Check(shotFace, "the camera came round to the hero's face");
        }

        IEnumerator HorseRide()
        {
            StartStudioPlay();
            Horse h = Ctrl.horse;
            h.Teleport(new Vector3(18f, 0f, 22f), 0f);
            yield return Wait(0.5f);

            Tap(Btn.Whistle);
            yield return Until(() => U.FlatDist(h.Pos, P.Pos) < 4f, 12f);
            Check(U.FlatDist(h.Pos, P.Pos) < 4f, "horse comes to the whistle");
            yield return Wait(1f);
            Snap("horse_arrived");

            yield return GoTo(h.Pos, 2.2f, 6f);
            yield return Frames(3);
            Check(Ctrl.prompts.Exists(p => p.text.StartsWith("Сесть")), "mount prompt is offered next to the horse");
            Tap(Btn.Interact);
            yield return Wait(1f);
            Check(P.Mounted, "hero mounts");
            Snap("mounted");

            // a fence across the path, 45 m ahead
            Vector3 fwd = Vector3.forward;
            Vector3 start = h.Pos;
            Game.Block(start + fwd * 60f + Vector3.up * 0.45f, new Vector3(14f, 0.9f, 0.3f), Pal.WoodOld);
            yield return LookAt(start + fwd * 80f + Vector3.up * 2f, 1.5f);

            yield return Go(fwd, 2.5f);
            Check(h.speed > 6f && h.speed < 8.5f, "canter without spurs (" + h.speed.ToString("0.0") + " m/s)");
            Snap("canter");
            float topSpeed = 0f, stBefore = h.stamina;
            bool jumped = false;
            Hold(Btn.Sprint, true);
            float end = Time.time + 6f;
            while (Time.time < end)
            {
                Stick(fwd);
                topSpeed = Mathf.Max(topSpeed, h.speed);
                if (h.jumpT >= 0f) jumped = true;
                yield return null;
            }
            Hold(Btn.Sprint, false);
            Check(topSpeed > h.maxSpeed - 0.6f, "gallop reaches top speed (" + topSpeed.ToString("0.0") + " of " + h.maxSpeed.ToString("0.0") + ")");
            Check(h.stamina < stBefore - 5f, "galloping tires the horse");
            Check(jumped, "horse jumps the fence by itself");
            Check(h.Pos.z > start.z + 62f, "horse is past the fence (z " + h.Pos.z.ToString("0") + ")");
            Snap("gallop");

            // turn round at a gallop and come back
            yield return Go(-fwd, 3f, Btn.Sprint);
            Check(Mathf.Abs(Mathf.DeltaAngle(h.yaw, 180f)) < 25f, "horse turns to the stick direction");
            Snap("turn");
            GameInput.vMove = Vector2.zero;
            yield return Until(() => Mathf.Abs(h.speed) < 0.2f, 5f);
            Check(Mathf.Abs(h.speed) < 0.2f, "horse stops when the stick is released");

            Tap(Btn.Jump);
            yield return Wait(0.5f);
            Tap(Btn.Interact);
            yield return Wait(0.6f);
            Check(!P.Mounted && P.cc.enabled, "hero dismounts");
            Check(P.Pos.y < 0.2f && U.FlatDist(P.Pos, h.Pos) < 2f, "hero stands beside the horse");
            Snap("dismounted");
            Check(h.bondXp > 0f, "riding raises the bond (" + h.bondXp.ToString("0") + " xp)");
        }
    }
}
