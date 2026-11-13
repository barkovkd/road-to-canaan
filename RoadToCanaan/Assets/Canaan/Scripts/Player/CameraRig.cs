using UnityEngine;

namespace Canaan
{
    public enum CamMode { Follow, FirstPerson, Cinematic, Fixed }

    /// <summary>
    /// Third-person camera: orbit behind the hero, over-the-shoulder when aiming, further back on a horse,
    /// with collision against the world. Also a first-person view and a cinematic mode.
    /// </summary>
    public class CameraRig
    {
        public Camera cam;
        public Human target;
        public float yaw, pitch = 14f;
        public CamMode mode = CamMode.Follow;
        public float letterbox;                 // 0..1, read by the HUD
        public float shake;
        public float fovKick;                   // extra field of view (slow motion pulls it in)
        public Vector3 fixedPos, fixedLook;

        float dist = 3.2f, side = 0.42f, fov = 55f, pivotY, idleLook, cineT, cineSeed;
        int cineShot;
        bool pivotInit;

        public CameraRig(Camera cam) { this.cam = cam; }

        public Vector3 Forward => Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

        public void SnapBehind(float newYaw) { yaw = newYaw; pitch = 14f; pivotInit = false; }

        public void Tick(float dt, bool allowLook)
        {
            if (target == null) return;
            if (mode == CamMode.Fixed)
            {
                cam.transform.position = fixedPos;
                cam.transform.rotation = Quaternion.LookRotation(fixedLook - fixedPos);
                return;
            }
            Vector2 look = allowLook ? GameInput.Look : Vector2.zero;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -68f, 76f);
            idleLook = look.sqrMagnitude > 0.0001f ? 0f : idleLook + dt;

            Horse h = target.mount;
            bool aiming = target.aiming && !target.dead;
            float k = target.rig.k;
            bool longGun = target.weapon != null && target.weapon.def.Long;

            float wantDist, wantSide, wantFov, height;
            Vector3 basePos;
            if (h != null)
            {
                basePos = h.transform.position;
                height = h.rig.withers + 0.85f * k;
                float sp = Mathf.Abs(h.speed);
                wantDist = aiming ? 2.6f : 4.6f + sp * 0.09f;
                wantSide = aiming ? 0.7f : 0f;
                wantFov = aiming ? (longGun ? 38f : 46f) : 57f + sp * 0.55f;
                if (!aiming && sp > 2f && idleLook > 1.2f) yaw = U.DampAngle(yaw, h.yaw, 1.4f, dt);
            }
            else
            {
                basePos = target.transform.position;
                height = (1.56f - (target.crouch ? 0.42f : 0f)) * k;
                wantDist = aiming ? 1.75f : target.dead ? 4.5f : 3.1f;
                wantSide = aiming ? 0.62f : 0.38f;
                wantFov = aiming ? (longGun ? 36f : 46f) : target.sprint ? 60f : 55f;
            }
            if (mode == CamMode.Cinematic) { Cinematic(dt, basePos, height); return; }
            letterbox = U.Damp(letterbox, 0f, 6f, dt);

            if (!pivotInit) { pivotY = basePos.y; pivotInit = true; dist = wantDist; side = wantSide; fov = wantFov; }
            pivotY = U.Damp(pivotY, basePos.y, 12f, dt);       // hides the bob of a walk or a canter
            Vector3 pivot = new Vector3(basePos.x, pivotY + height, basePos.z);
            dist = U.Damp(dist, wantDist, aiming ? 14f : 5f, dt);
            side = U.Damp(side, wantSide, 10f, dt);
            fov = U.Damp(fov, wantFov + fovKick, 8f, dt);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

            if (mode == CamMode.FirstPerson && h == null && !target.dead)
            {
                Transform eyes = target.rig.eyes;
                cam.transform.position = eyes.position + rot * new Vector3(0f, 0.02f, 0.07f);
                cam.transform.rotation = rot;
                cam.fieldOfView = aiming ? 44f : 62f;
                SetHatVisible(false);
                return;
            }
            SetHatVisible(true);

            // shoulder offset first (so a wall beside the hero pushes the camera back to centre), then the boom
            Vector3 shoulder = pivot;
            Vector3 sideDir = rot * Vector3.right;
            float sd = Mathf.Abs(side);
            if (sd > 0.01f)
            {
                Vector3 sdir = sideDir * Mathf.Sign(side);
                float clear = Physics.SphereCast(pivot, 0.18f, sdir, out RaycastHit sh, sd, Layers.SolidMask, QueryTriggerInteraction.Ignore) ? Mathf.Max(0f, sh.distance - 0.02f) : sd;
                shoulder = pivot + sdir * clear;
            }
            Vector3 back = rot * Vector3.back;
            float d = dist;
            if (Physics.SphereCast(shoulder, 0.22f, back, out RaycastHit hit, dist, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(0.3f, hit.distance - 0.04f);
            Vector3 pos = shoulder + back * d + Vector3.up * (0.12f * Mathf.Clamp01(d / 3f));
            if (shake > 0.001f)
            {
                pos += Random.insideUnitSphere * (shake * 0.05f);
                shake = Mathf.Max(0f, shake - dt * 4f);
            }
            cam.transform.position = pos;
            cam.transform.rotation = rot;
            cam.fieldOfView = fov;
        }

        void SetHatVisible(bool visible)
        {
            GameObject hat = target.rig.hat;
            if (hat != null && hat.activeSelf != visible) hat.SetActive(visible);
        }

        /// <summary>Slow scenic angles that change every few seconds.</summary>
        void Cinematic(float dt, Vector3 basePos, float height)
        {
            letterbox = U.Damp(letterbox, 1f, 4f, dt);
            cineT -= dt;
            if (cineT <= 0f)
            {
                cineT = Random.Range(5.5f, 8.5f);
                cineShot = (cineShot + 1 + Random.Range(0, 3)) % 4;
                cineSeed = Random.value;
            }
            float facing = target.mount != null ? target.mount.yaw : target.yaw;
            Vector3 fwd = U.Dir(facing), right = Quaternion.Euler(0f, 90f, 0f) * fwd;
            Vector3 look = basePos + Vector3.up * height * 0.8f;
            float sign = cineSeed > 0.5f ? 1f : -1f;
            float prog = 1f - cineT / 8.5f;
            Vector3 pos;
            switch (cineShot)
            {
                case 0: pos = look + right * (7f * sign) + fwd * Mathf.Lerp(3f, -1f, prog) + Vector3.up * 0.4f; break;         // side tracking
                case 1: pos = look + fwd * 9f + right * (2.5f * sign) - Vector3.up * (height * 0.55f); break;                 // low in front
                case 2: pos = look - fwd * 11f + right * (4f * sign) + Vector3.up * 5f; break;                               // high behind
                default: pos = look + right * (14f * sign) + fwd * 6f + Vector3.up * 2.5f; break;                             // wide
            }
            if (World.Ground(pos, out float gy, out _, 30f, 60f) && pos.y < gy + 0.4f) pos.y = gy + 0.4f;
            cam.transform.position = Vector3.Lerp(cam.transform.position, pos, 1f - Mathf.Exp(-2.5f * dt));
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), 1f - Mathf.Exp(-5f * dt));
            cam.fieldOfView = U.Damp(cam.fieldOfView, 38f, 3f, dt);
        }

        /// <summary>The world point under the centre of the screen: what the hero aims at.</summary>
        public Vector3 AimPoint(out Actor actor, out BodyPart part, float range = 400f)
        {
            Vector3 o = cam.transform.position, f = cam.transform.forward;
            float skip = target != null ? Mathf.Max(0f, Vector3.Dot(target.Chest - o, f)) : 0f;
            Hitscan.Cast(o + f * skip, f, range, target, target != null ? target.mount : null, out actor, out BodyHit body, out Vector3 end, out _);
            part = body.part;
            return end;
        }

        public Vector3 AimPoint() { return AimPoint(out _, out _); }
    }
}
