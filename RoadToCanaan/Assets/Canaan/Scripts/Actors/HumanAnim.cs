using UnityEngine;

namespace Canaan
{
    public enum BodyKind { Stand, SitChair, SitGround, Kneel, Ride, Dead }
    public enum ArmKind { Free, Pistol, PistolAim, Rifle, RifleAim, Fists, HandsUp, Reins, Reload, BowAim, Work }

    /// <summary>Target of an arm or leg in model space: where the wrist or ankle goes and how the joint bends.</summary>
    public struct Limb
    {
        public Vector3 pos, pole;
        public Quaternion rot;
        public float weight;   // hands: 0 = hand follows the forearm, 1 = use 'rot'

        public static Limb Lerp(Limb a, Limb b, float t)
        {
            return new Limb
            {
                pos = Vector3.Lerp(a.pos, b.pos, t),
                pole = Vector3.Slerp(a.pole, b.pole, t),
                rot = a.weight < 0.01f ? b.rot : b.weight < 0.01f ? a.rot : Quaternion.Slerp(a.rot, b.rot, t),
                weight = Mathf.Lerp(a.weight, b.weight, t)
            };
        }
    }

    /// <summary>A whole-body pose in model space (root at the feet, or at the saddle when riding).</summary>
    public class BodyPose
    {
        public Vector3 hips;
        public Quaternion hipsRot = Quaternion.identity, spine = Quaternion.identity, chest = Quaternion.identity;
        public Quaternion neck = Quaternion.identity, head = Quaternion.identity;
        public Limb footL, footR, handL, handR;
        public float toeL, toeR;

        public void CopyFrom(BodyPose o)
        {
            hips = o.hips; hipsRot = o.hipsRot; spine = o.spine; chest = o.chest; neck = o.neck; head = o.head;
            footL = o.footL; footR = o.footR; handL = o.handL; handR = o.handR; toeL = o.toeL; toeR = o.toeR;
        }

        public static void LerpBody(BodyPose a, BodyPose b, float t, BodyPose o)
        {
            o.hips = Vector3.Lerp(a.hips, b.hips, t);
            o.hipsRot = Quaternion.Slerp(a.hipsRot, b.hipsRot, t);
            o.spine = Quaternion.Slerp(a.spine, b.spine, t);
            o.chest = Quaternion.Slerp(a.chest, b.chest, t);
            o.neck = Quaternion.Slerp(a.neck, b.neck, t);
            o.head = Quaternion.Slerp(a.head, b.head, t);
            o.footL = Limb.Lerp(a.footL, b.footL, t);
            o.footR = Limb.Lerp(a.footR, b.footR, t);
            o.toeL = Mathf.Lerp(a.toeL, b.toeL, t);
            o.toeR = Mathf.Lerp(a.toeR, b.toeR, t);
        }
    }

    /// <summary>
    /// Procedural animation of a person. No clips: the gait is computed from the velocity, limbs are placed with
    /// inverse kinematics, and whole-body poses (sitting, riding, kneeling, lying) are blended in.
    /// The owner sets the public inputs and calls Tick once per frame.
    /// </summary>
    public class HumanAnim
    {
        public readonly HumanRig rig;
        static int counter;
        readonly int seed = ++counter * 7919;

        // ---- inputs
        public Vector3 localVel;                 // velocity in the actor's local space, m/s
        public bool grounded = true;
        public float crouch;                     // 0..1
        public BodyKind body = BodyKind.Stand;
        public ArmKind arms = ArmKind.Free;
        public Vector3 aimLocal = Vector3.forward;   // aim direction in model space
        public Vector3 lookLocal = Vector3.forward;  // head look direction in model space
        public float lookWeight;
        public float seatHeight = 0.46f;
        public int deadVariant;
        public float rideLean, rideBounce;       // set by the horse each frame
        public float seatPitch;                  // saddle pitch in degrees (negative = nose up), set by the horse
        public float bowDraw;
        public float workPhase = -1f;            // >= 0: chopping/hammering swing, cycles
        public float footOffsetL, footOffsetR;   // ground height under each foot relative to the root
        public float turnRate;                   // how fast the body turns, deg/s (+ = to the right)
        public float accel;                      // speeding up (+) or braking (-) along the facing, m/s2

        // ---- outputs
        public float phase;                      // gait cycle 0..1 (0 = left heel strike)
        public bool stepped;                     // a foot struck the ground this frame
        public float speedNorm;

        readonly BodyPose target = new BodyPose(), cur = new BodyPose(), bodyFrom = new BodyPose();
        Limb armFromL, armFromR;
        float bodyBlend = 1f, bodyDur = 0.25f, armBlend = 1f, armDur = 0.18f;
        BodyKind lastBody = BodyKind.Stand;
        ArmKind lastArms = ArmKind.Free;
        float moveW, airW, idleT, recoil, flinch, punchT = -1f, whistleT = -1f;
        bool punchLeft;
        Vector3 flinchDir;
        Vector3 moveDir = Vector3.forward;
        readonly Quaternion invThighL, invShinL, invThighR, invShinR, invArmL, invForeL, invArmR, invForeR;
        readonly float k;
        bool first = true;
        float lookYaw, lookPitch, glanceYaw, glancePitch, glanceUntil = -1f, nextGlance;
        readonly System.Random rnd;             // its own dice: background motion must not touch the game's

        public HumanAnim(HumanRig rig)
        {
            this.rig = rig;
            k = rig.k;
            Vector3[] l = rig.bindLocal;
            invThighL = IK.InvBind(l[(int)HB.ShinL], Vector3.forward);
            invShinL = IK.InvBind(l[(int)HB.FootL], Vector3.forward);
            invThighR = IK.InvBind(l[(int)HB.ShinR], Vector3.forward);
            invShinR = IK.InvBind(l[(int)HB.FootR], Vector3.forward);
            invArmL = IK.InvBind(l[(int)HB.ForeL], Vector3.back);
            invForeL = IK.InvBind(l[(int)HB.HandL], Vector3.back);
            invArmR = IK.InvBind(l[(int)HB.ForeR], Vector3.back);
            invForeR = IK.InvBind(l[(int)HB.HandR], Vector3.back);
            idleT = (seed & 1023) * 0.37f;
            rnd = new System.Random(seed);
            nextGlance = idleT + 2f + (float)rnd.NextDouble() * 5f;
        }

        // ---- one-shot events
        public void Punch(bool left) { punchT = 0f; punchLeft = left; }
        public void Recoil(float strength) { recoil = Mathf.Max(recoil, strength); }
        public void Flinch(Vector3 dirLocal) { flinch = 1f; flinchDir = dirLocal; }
        public void Whistle() { whistleT = 0f; }
        public bool Punching => punchT >= 0f;

        /// <summary>Skips the blend, e.g. right after spawning in a pose.</summary>
        public void Snap() { first = true; }

        public void Tick(float dt)
        {
            stepped = false;
            idleT += dt;
            if (body != lastBody)
            {
                bodyFrom.CopyFrom(cur);
                bodyBlend = 0f;
                bodyDur = body == BodyKind.Dead ? 0.62f : (body == BodyKind.Ride || lastBody == BodyKind.Ride) ? 0.45f : 0.35f;
                armFromL = cur.handL; armFromR = cur.handR; armBlend = 0f; armDur = bodyDur;
                lastBody = body;
            }
            if (arms != lastArms)
            {
                armFromL = cur.handL; armFromR = cur.handR; armBlend = 0f;
                armDur = (arms == ArmKind.PistolAim || lastArms == ArmKind.PistolAim) ? 0.13f : 0.2f;
                lastArms = arms;
            }

            Generate(target, dt);

            if (first) { bodyBlend = 1f; armBlend = 1f; first = false; }
            if (bodyBlend < 1f)
            {
                bodyBlend = Mathf.Min(1f, bodyBlend + dt / bodyDur);
                float e = body == BodyKind.Dead ? bodyBlend * bodyBlend : U.Smooth01(bodyBlend);
                BodyPose.LerpBody(bodyFrom, target, e, cur);
            }
            else BodyPose.LerpBody(target, target, 1f, cur);
            if (armBlend < 1f)
            {
                armBlend = Mathf.Min(1f, armBlend + dt / armDur);
                float e = U.Smooth01(armBlend);
                cur.handL = Limb.Lerp(armFromL, target.handL, e);
                cur.handR = Limb.Lerp(armFromR, target.handR, e);
            }
            else { cur.handL = target.handL; cur.handR = target.handR; }
            Apply(cur);
        }

        // ================================================================== generation

        void Generate(BodyPose o, float dt)
        {
            o.spine = o.chest = o.neck = o.head = Quaternion.identity;
            o.toeL = o.toeR = 0f;
            switch (body)
            {
                case BodyKind.Stand: GenStand(o, dt); break;
                case BodyKind.SitChair: GenSit(o, seatHeight, false); break;
                case BodyKind.SitGround: GenSit(o, 0.13f, true); break;
                case BodyKind.Kneel: GenKneel(o); break;
                case BodyKind.Ride: GenRide(o); break;
                case BodyKind.Dead: GenDead(o); return;
            }
            GenArms(o, dt);
            GenExtras(o, dt);
        }

        static Limb Foot(Vector3 pos, float pitch, float yaw, Vector3 pole)
        {
            return new Limb { pos = pos, rot = Quaternion.Euler(pitch, yaw, 0f), pole = pole, weight = 1f };
        }

        void GenStand(BodyPose o, float dt)
        {
            Vector3 v = localVel; v.y = 0f;
            float speed = v.magnitude;
            float vn = speed / k;
            speedNorm = vn;
            bool moving = vn > 0.15f;
            if (moving) moveDir = Vector3.Slerp(moveDir, v / speed, 1f - Mathf.Exp(-14f * dt)).normalized;
            moveW = U.Damp(moveW, moving ? 1f : 0f, 10f, dt);
            airW = U.Damp(airW, grounded ? 0f : 1f, 12f, dt);

            float run = Mathf.InverseLerp(1.9f, 3.4f, vn);
            float sprint = Mathf.InverseLerp(3.9f, 6.2f, vn);
            float cycleLen = vn < 1.6f ? Mathf.Lerp(1.0f, 1.5f, vn / 1.6f)
                           : vn < 3.4f ? Mathf.Lerp(1.5f, 2.35f, (vn - 1.6f) / 1.8f)
                           : Mathf.Lerp(2.35f, 3.5f, Mathf.Clamp01((vn - 3.4f) / 2.8f));
            cycleLen *= Mathf.Lerp(1f, 0.78f, crouch);
            float prev = phase;
            if (moving) phase += vn * dt / cycleLen;
            phase -= Mathf.Floor(phase);
            if (moving && ((prev < 0.5f && phase >= 0.5f) || phase < prev)) stepped = true;

            float duty = Mathf.Lerp(Mathf.Lerp(0.62f, 0.45f, run), 0.36f, sprint);
            float lift = Mathf.Lerp(Mathf.Lerp(0.07f, 0.15f, run), 0.24f, sprint);
            float half = duty * cycleLen * 0.5f;
            float lat = Mathf.Lerp(0.098f, 0.062f, run) + crouch * 0.04f;
            float heelOff = 26f + 14f * run + 16f * sprint;
            float footH = rig.footHeight / k;
            bool backwards = moveDir.z < -0.3f;
            // standing still, the weight rests on one leg and moves to the other now and then; the free knee eases
            float ws = Mathf.Clamp(Mathf.Sin(idleT * 0.23f + (seed & 255) * 0.1f) * 1.8f, -1f, 1f) * (1f - moveW) * (1f - Mathf.Clamp01(crouch));

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float p = phase + (i == 0 ? 0f : 0.5f);
                p -= Mathf.Floor(p);
                float z, y, pitch, toe = 0f;
                if (p < duty)
                {
                    float s = p / duty;
                    z = Mathf.Lerp(half, -half, s);
                    pitch = s < 0.15f ? Mathf.Lerp(-14f, 0f, s / 0.15f) : s > 0.68f ? Mathf.Lerp(0f, heelOff, (s - 0.68f) / 0.32f) : 0f;
                    y = pitch > 0f ? Mathf.Sin(pitch * Mathf.Deg2Rad) * 0.105f : 0f;
                    toe = Mathf.Max(0f, pitch) * 0.85f;
                }
                else
                {
                    float s = (p - duty) / (1f - duty);
                    float e = U.Smooth01(s);
                    z = Mathf.Lerp(-half, half, e);
                    float heelEnd = Mathf.Sin(heelOff * Mathf.Deg2Rad) * 0.105f;
                    y = lift * Mathf.Sin(Mathf.PI * s) + heelEnd * (1f - s) * (1f - s)
                        + sprint * 0.12f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, s * 1.6f));
                    pitch = Mathf.Lerp(heelOff + 12f * run, -14f, e);
                }
                if (backwards) pitch = -pitch * 0.5f;
                Vector3 gait = new Vector3(side * lat, footH + y, 0f) + moveDir * z;
                float free = Mathf.Max(0f, -side * ws);
                Vector3 idle = new Vector3(side * (0.105f + 0.02f * free), footH + 0.016f * free, side * -0.012f + (i == 0 ? 0.03f : -0.02f) + 0.06f * free);
                Vector3 air = new Vector3(side * 0.10f, footH + 0.2f, i == 0 ? 0.14f : -0.16f);
                Vector3 pos = Vector3.Lerp(idle, gait, moveW);
                pos = Vector3.Lerp(pos, air, airW);
                float pit = Mathf.Lerp(pitch * moveW + 9f * free * (1f - moveW), 28f, airW);
                Vector3 pole = new Vector3(side * (0.12f + crouch * 0.25f), 0f, 1f);
                Limb foot = Foot(pos * k, pit, side * Mathf.Lerp(9f, 4f, moveW), pole);
                if (i == 0) { o.footL = foot; o.toeL = -toe * moveW * (1f - airW); }
                else { o.footR = foot; o.toeR = -toe * moveW * (1f - airW); }
            }

            float twoPi = Mathf.PI * 2f;
            float bobA = Mathf.Lerp(0.02f, 0.042f, run) * moveW;
            float shift = Mathf.Lerp(0f, duty * 0.5f, run);
            float bob = -bobA * Mathf.Cos(2f * twoPi * (phase - shift));
            float drop = Mathf.Lerp(0.018f, 0.045f, run) * moveW + 0.012f + 0.03f * sprint + crouch * 0.25f;
            float sway = -Mathf.Lerp(0.026f, 0.01f, run) * Mathf.Sin(twoPi * phase) * moveW + Mathf.Sin(idleT * 0.45f) * 0.006f * (1f - moveW) + ws * 0.028f;
            // momentum: a lean into the turn the faster he runs, forward when he speeds up, back when he brakes
            float bank = Mathf.Clamp(turnRate * speed * 0.011f, -13f, 13f) * moveW;
            float brake = Mathf.Clamp(accel * 1.1f, -9f, 9f) * Mathf.Max(moveW, 0.35f) * (1f - airW);
            float lean = (Mathf.Lerp(Mathf.Lerp(2f, 7f, run), 14f, sprint) * moveW) * (backwards ? -0.5f : 1f) + crouch * 22f + brake;
            float yaw = Mathf.Lerp(5f, 8f, run) * Mathf.Cos(twoPi * phase) * moveW;
            float roll = -Mathf.Lerp(3f, 2f, run) * Mathf.Sin(twoPi * phase) * moveW;
            float breath = Mathf.Sin(idleT * 1.5f) * 0.7f;
            float lead = Mathf.Clamp(turnRate * 0.05f, -12f, 12f) * moveW;            // the head looks into the turn
            o.hips = new Vector3(sway, rig.hipHeight / k - drop + bob - airW * 0.02f, -crouch * 0.05f) * k;
            o.hipsRot = Quaternion.Euler(lean * 0.5f, yaw, roll - bank * 0.5f + ws * 3f);
            o.spine = Quaternion.Euler(lean * 0.3f + crouch * 20f + breath * 0.4f, -yaw * 0.8f, -roll * 0.7f - bank * 0.3f - ws * 2f);
            o.chest = Quaternion.Euler(lean * 0.2f + crouch * 12f - breath, -yaw * 0.9f, -roll * 0.3f - bank * 0.2f - ws * 1f);
            o.neck = Quaternion.Euler(-lean * 0.4f - crouch * 22f, yaw * 0.4f + lead * 0.4f, bank * 0.45f);
            o.head = Quaternion.Euler(-lean * 0.45f - crouch * 16f, yaw * 0.3f + lead * 0.6f, bank * 0.35f);
        }

        void GenSit(BodyPose o, float seat, bool ground)
        {
            float footH = rig.footHeight / k;
            o.hips = new Vector3(0f, seat + 0.09f, 0f) * k;
            o.hipsRot = Quaternion.Euler(ground ? -12f : -6f, 0f, 0f);
            o.spine = Quaternion.Euler(ground ? 14f : 6f, 0f, 0f);
            o.chest = Quaternion.Euler(ground ? 10f : 4f, 0f, 0f);
            o.neck = Quaternion.Euler(ground ? -8f : -2f, 0f, 0f);
            float fz = ground ? 0.52f : 0.40f;
            o.footL = Foot(new Vector3(-0.13f, footH, fz) * k, ground ? -10f : 0f, -8f, new Vector3(-0.2f, 1f, 0.5f));
            o.footR = Foot(new Vector3(0.13f, footH, fz + (ground ? 0.1f : 0f)) * k, ground ? -10f : 0f, 8f, new Vector3(0.2f, 1f, 0.5f));
        }

        void GenKneel(BodyPose o)
        {
            float footH = rig.footHeight / k;
            o.hips = new Vector3(0f, 0.44f, -0.08f) * k;
            o.hipsRot = Quaternion.Euler(8f, 0f, 0f);
            o.spine = Quaternion.Euler(18f, 0f, 0f);
            o.chest = Quaternion.Euler(14f, 0f, 0f);
            o.neck = Quaternion.Euler(6f, 0f, 0f);
            o.footL = Foot(new Vector3(-0.13f, footH, 0.26f) * k, 0f, -6f, new Vector3(-0.2f, 0.6f, 1f));
            o.footR = Foot(new Vector3(0.12f, 0.07f, -0.46f) * k, 62f, 0f, new Vector3(0.15f, -0.6f, 1f));
            o.toeR = -40f;
        }

        void GenRide(BodyPose o)
        {
            // model space: origin at the saddle seat
            float rise = Mathf.Max(0f, rideLean) * 0.10f + rideBounce;
            o.hips = new Vector3(0f, 0.105f + rise, -0.05f) * k;
            o.hipsRot = Quaternion.Euler(rideLean * 10f - 4f - seatPitch * 0.85f, 0f, 0f);
            o.spine = Quaternion.Euler(rideLean * 12f + 5f, 0f, 0f);
            o.chest = Quaternion.Euler(rideLean * 8f + 3f, 0f, 0f);
            o.neck = Quaternion.Euler(-rideLean * 16f - 3f, 0f, 0f);
            o.head = Quaternion.Euler(-rideLean * 10f, 0f, 0f);
            o.footL = Foot(new Vector3(-0.36f, -0.66f, 0.10f) * k, -12f, -14f, new Vector3(-0.75f, 0.1f, 1f));
            o.footR = Foot(new Vector3(0.36f, -0.66f, 0.10f) * k, -12f, 14f, new Vector3(0.75f, 0.1f, 1f));
        }

        void GenDead(BodyPose o)
        {
            float flip = (seed & 1) == 0 ? 1f : -1f;
            if (deadVariant == 0)
            {
                // on the back, head away from where the body was facing
                o.hips = new Vector3(0.05f * flip, 0.125f, -0.62f) * k;
                o.hipsRot = Quaternion.Euler(-88f, 0f, 6f * flip);
                o.spine = Quaternion.Euler(0f, 5f * flip, 4f * flip);
                o.chest = Quaternion.Euler(-3f, 4f * flip, 3f * flip);
                o.neck = Quaternion.Euler(4f, 20f * flip, 0f);
                o.head = Quaternion.Euler(0f, 22f * flip, 0f);
                o.footL = Foot(new Vector3(-0.19f, 0.07f, 0.24f) * k, -62f, -24f, new Vector3(-0.5f, 1f, 0.2f));
                o.footR = Foot(new Vector3(0.14f, 0.07f, 0.16f + 0.1f * flip) * k, -62f, 20f, new Vector3(0.6f, 1f, 0.1f));
                o.handL = new Limb { pos = new Vector3(-0.58f, 0.05f, -0.92f - 0.1f * flip) * k, pole = new Vector3(-1f, 0.2f, 0.4f) };
                o.handR = new Limb { pos = new Vector3(0.52f, 0.05f, -1.22f + 0.2f * flip) * k, pole = new Vector3(1f, 0.2f, 0.3f) };
            }
            else
            {
                // face down
                o.hips = new Vector3(0.04f * flip, 0.13f, 0.56f) * k;
                o.hipsRot = Quaternion.Euler(88f, 0f, 5f * flip);
                o.spine = Quaternion.Euler(0f, 0f, 5f * flip);
                o.chest = Quaternion.Euler(2f, 0f, 4f * flip);
                o.neck = Quaternion.Euler(-6f, 0f, 30f * flip);
                o.head = Quaternion.Euler(-4f, 0f, 35f * flip);
                o.footL = Foot(new Vector3(-0.2f, 0.06f, -0.22f) * k, 72f, -18f, new Vector3(-0.6f, -0.2f, 1f));
                o.footR = Foot(new Vector3(0.13f, 0.06f, -0.3f) * k, 72f, 14f, new Vector3(0.6f, -0.2f, 1f));
                o.handL = new Limb { pos = new Vector3(-0.42f, 0.05f, 1.2f + 0.12f * flip) * k, pole = new Vector3(-1f, 0.4f, 0f) };
                o.handR = new Limb { pos = new Vector3(0.5f, 0.05f, 0.95f - 0.15f * flip) * k, pole = new Vector3(1f, 0.4f, 0f) };
                o.toeL = o.toeR = 0f;
            }
        }

        Matrix4x4 ChestMatrix(BodyPose o)
        {
            Vector3[] l = rig.bindLocal;
            return Matrix4x4.TRS(o.hips, o.hipsRot, Vector3.one)
                 * Matrix4x4.TRS(l[(int)HB.Spine], o.spine, Vector3.one)
                 * Matrix4x4.TRS(l[(int)HB.Chest], o.chest, Vector3.one);
        }

        void GenArms(BodyPose o, float dt)
        {
            ArmKind a = arms;
            bool aiming = a == ArmKind.PistolAim || a == ArmKind.RifleAim || a == ArmKind.BowAim;
            float aimYaw = 0f, aimPitch = 0f;
            Vector3 aim = aimLocal.sqrMagnitude > 0.001f ? aimLocal.normalized : Vector3.forward;
            if (aiming)
            {
                aimYaw = Mathf.Clamp(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, -75f, 75f);
                aimPitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
                float side = a == ArmKind.RifleAim || a == ArmKind.BowAim ? 18f : 0f;   // long guns are shot bladed
                o.spine = o.spine * Quaternion.Euler(aimPitch * 0.25f, aimYaw * 0.3f + side * 0.4f, 0f);
                o.chest = o.chest * Quaternion.Euler(aimPitch * 0.3f, aimYaw * 0.4f + side * 0.6f, 0f);
                o.neck = o.neck * Quaternion.Euler(-aimPitch * 0.1f, -side * 0.5f, 0f);
                o.head = o.head * Quaternion.Euler(-aimPitch * 0.1f, -side * 0.5f, a == ArmKind.RifleAim ? 7f : 0f);
            }
            if (punchT >= 0f)
            {
                punchT += dt / 0.3f;
                if (punchT >= 1f) punchT = -1f;
                else
                {
                    float tw = Mathf.Sin(punchT * Mathf.PI) * (punchLeft ? 26f : -30f);
                    o.spine = o.spine * Quaternion.Euler(4f, tw * 0.4f, 0f);
                    o.chest = o.chest * Quaternion.Euler(4f, tw * 0.6f, 0f);
                }
            }

            Matrix4x4 cm = ChestMatrix(o);
            Vector3[] l = rig.bindLocal;
            float run = Mathf.InverseLerp(1.9f, 3.4f, speedNorm), sprint = Mathf.InverseLerp(3.9f, 6.2f, speedNorm);
            float mw = body == BodyKind.Stand ? moveW : 0f;
            float cosP = Mathf.Cos(phase * Mathf.PI * 2f);
            float cosLag = Mathf.Cos((phase - 0.05f) * Mathf.PI * 2f);

            // natural swing, used as the default for both arms
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float E = Mathf.Lerp(Mathf.Lerp(26f, 30f, run), 44f, sprint);
                float swing = -side * E * cosP * mw - crouch * 34f;
                float trail = -side * E * cosLag * mw - crouch * 34f;
                float forward = Mathf.Max(0f, -side * cosP);
                float flex = (Mathf.Lerp(Mathf.Lerp(10f, 62f, run), 86f, sprint)) * mw + 9f + 24f * forward * mw * (1f - run);
                float abd = 7f + 5f * run + airW * 30f;
                float up = airW * -35f;
                Quaternion qu = Quaternion.Euler(swing + 2f + up, 0f, side * abd);
                Quaternion qf = Quaternion.Euler(trail + 2f + up - flex - airW * 30f, 0f, side * abd * 0.3f - side * run * 14f);
                Vector3 shoulder = l[(int)(i == 0 ? HB.ArmL : HB.ArmR)];
                Vector3 local = shoulder + (qu * Vector3.down) * rig.armU + (qf * Vector3.down) * rig.armF;
                var limb = new Limb { pos = cm.MultiplyPoint3x4(local), pole = new Vector3(side * 0.35f, -0.1f, -1f), rot = Quaternion.identity, weight = 0f };
                if (i == 0) o.handL = limb; else o.handR = limb;
            }

            Vector3 chestTop = cm.MultiplyPoint3x4(new Vector3(0f, 0.2f, 0f) * k);
            Vector3 right = Vector3.Cross(Vector3.up, aim);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            right.Normalize();
            Vector3 upA = Vector3.Cross(aim, right);
            Quaternion aimRot = Quaternion.LookRotation(aim, upA);
            bool riding = body == BodyKind.Ride;
            Limb reinL = new Limb { pos = new Vector3(-0.08f, 0.22f, 0.30f) * k, pole = new Vector3(-0.8f, -1f, -0.6f), rot = Quaternion.identity };
            Limb reinR = new Limb { pos = new Vector3(0.08f, 0.22f, 0.30f) * k, pole = new Vector3(0.8f, -1f, -0.6f), rot = Quaternion.identity };
            if (riding) { o.handL = reinL; o.handR = reinR; }

            switch (a)
            {
                case ArmKind.Reins:
                    break;
                case ArmKind.Pistol:
                    if (!riding)
                    {
                        Vector3 pr = o.handR.pos;
                        o.handR = new Limb { pos = pr, pole = o.handR.pole, rot = Quaternion.Euler(55f, 0f, 0f), weight = 1f };
                    }
                    else o.handR = new Limb { pos = new Vector3(0.24f, 0.30f, 0.22f) * k, pole = new Vector3(0.7f, -1f, -0.3f), rot = Quaternion.Euler(50f, 0f, 0f), weight = 1f };
                    break;
                case ArmKind.PistolAim:
                    o.handR = new Limb
                    {
                        pos = chestTop + right * (0.17f * k) + upA * (0.03f * k) + aim * (0.55f * k),
                        pole = new Vector3(0.5f, -1f, -0.2f), rot = aimRot, weight = 1f
                    };
                    break;
                case ArmKind.Rifle:
                {
                    float bobz = Mathf.Sin(phase * Mathf.PI * 4f) * 0.015f * mw;
                    Vector3 hr = riding ? new Vector3(0.20f, 0.30f, 0.18f) : new Vector3(0.21f, 1.0f - crouch * 0.28f + bobz, 0.09f);
                    Vector3 hl = riding ? new Vector3(-0.06f, 0.44f, 0.42f) : new Vector3(-0.07f, 1.14f - crouch * 0.28f + bobz, 0.34f);
                    Quaternion r = Quaternion.LookRotation((hl - hr).normalized, Vector3.up);
                    o.handR = new Limb { pos = hr * k, pole = new Vector3(0.8f, -0.4f, -1f), rot = r, weight = 1f };
                    o.handL = new Limb { pos = hl * k, pole = new Vector3(-0.8f, -0.8f, -0.2f), rot = r, weight = 1f };
                    break;
                }
                case ArmKind.RifleAim:
                {
                    Vector3 pocket = chestTop + right * (0.125f * k) - upA * (0.05f * k) + aim * (0.03f * k);
                    o.handR = new Limb { pos = pocket + aim * (0.20f * k) - upA * (0.03f * k), pole = new Vector3(1f, -0.7f, -0.3f), rot = aimRot, weight = 1f };
                    o.handL = new Limb { pos = pocket + aim * (0.56f * k) - upA * (0.035f * k), pole = new Vector3(-0.3f, -1f, 0.1f), rot = aimRot, weight = 1f };
                    break;
                }
                case ArmKind.BowAim:
                {
                    o.handL = new Limb { pos = chestTop - right * (0.05f * k) + aim * (0.62f * k) + upA * (0.02f * k), pole = new Vector3(-0.6f, -0.8f, 0f), rot = aimRot, weight = 1f };
                    o.handR = new Limb { pos = chestTop + right * (0.10f * k) + aim * ((0.52f - 0.42f * bowDraw) * k) + upA * (0.03f * k), pole = new Vector3(1f, 0.1f, -0.4f), rot = aimRot, weight = 1f };
                    break;
                }
                case ArmKind.Fists:
                    o.handR = new Limb { pos = cm.MultiplyPoint3x4(l[(int)HB.ArmR] + new Vector3(-0.05f, -0.08f, 0.22f) * k), pole = new Vector3(0.6f, -1f, -0.2f), rot = Quaternion.identity };
                    o.handL = new Limb { pos = cm.MultiplyPoint3x4(l[(int)HB.ArmL] + new Vector3(0.07f, -0.04f, 0.30f) * k), pole = new Vector3(-0.6f, -1f, -0.2f), rot = Quaternion.identity };
                    break;
                case ArmKind.HandsUp:
                    o.handR = new Limb { pos = cm.MultiplyPoint3x4(l[(int)HB.ArmR] + new Vector3(0.12f, 0.36f, 0.08f) * k), pole = new Vector3(1f, -0.4f, -0.2f), rot = Quaternion.identity };
                    o.handL = new Limb { pos = cm.MultiplyPoint3x4(l[(int)HB.ArmL] + new Vector3(-0.12f, 0.36f, 0.08f) * k), pole = new Vector3(-1f, -0.4f, -0.2f), rot = Quaternion.identity };
                    break;
                case ArmKind.Reload:
                {
                    float w = idleT * 9f;
                    Vector3 hr = chestTop + new Vector3(0.10f, -0.30f, 0.26f) * k;
                    Vector3 hl = chestTop + new Vector3(-0.05f + Mathf.Cos(w) * 0.025f, -0.31f + Mathf.Sin(w) * 0.03f, 0.31f) * k;
                    Quaternion r = Quaternion.LookRotation(new Vector3(-0.45f, 0.45f, 0.75f).normalized, Vector3.up);
                    o.handR = new Limb { pos = hr, pole = new Vector3(0.8f, -0.8f, -0.3f), rot = r, weight = 1f };
                    o.handL = new Limb { pos = hl, pole = new Vector3(-0.8f, -0.8f, -0.3f), rot = Quaternion.identity };
                    break;
                }
                case ArmKind.Work:
                {
                    // two-handed overhead swing (axe, hammer): workPhase runs 0..1 per stroke
                    float s = workPhase < 0f ? 0f : workPhase - Mathf.Floor(workPhase);
                    float lift = s < 0.6f ? U.Smooth01(s / 0.6f) : 1f - U.Smooth01((s - 0.6f) / 0.25f);
                    lift = Mathf.Clamp01(lift);
                    Vector3 lo = new Vector3(0.06f, -0.42f, 0.34f), hi = new Vector3(0.16f, 0.34f, -0.02f);
                    Vector3 pR = chestTop + Vector3.Lerp(lo, hi, lift) * k;
                    Vector3 pL = pR + new Vector3(-0.07f, -0.10f, 0.03f) * k;
                    Quaternion r = Quaternion.Euler(Mathf.Lerp(40f, -120f, lift), 0f, 0f);
                    o.handR = new Limb { pos = pR, pole = new Vector3(0.8f, -0.5f, -0.3f), rot = r, weight = 1f };
                    o.handL = new Limb { pos = pL, pole = new Vector3(-0.8f, -0.5f, -0.3f), rot = r, weight = 1f };
                    o.spine = o.spine * Quaternion.Euler(Mathf.Lerp(16f, -8f, lift), 0f, 0f);
                    break;
                }
            }
            if (riding && (a == ArmKind.PistolAim || a == ArmKind.Pistol)) o.handL = reinL;

            if (punchT >= 0f)
            {
                float reach = Mathf.Sin(punchT * Mathf.PI);
                Vector3 hit = cm.MultiplyPoint3x4((punchLeft ? l[(int)HB.ArmL] : l[(int)HB.ArmR]) + new Vector3(punchLeft ? 0.1f : -0.1f, -0.02f, 0.62f) * k);
                if (punchLeft) o.handL.pos = Vector3.Lerp(o.handL.pos, hit, reach);
                else o.handR.pos = Vector3.Lerp(o.handR.pos, hit, reach);
            }
            if (whistleT >= 0f)
            {
                whistleT += dt / 0.9f;
                if (whistleT >= 1f) whistleT = -1f;
                else
                {
                    float w = Mathf.Sin(Mathf.Clamp01(whistleT * 1.4f) * Mathf.PI);
                    Vector3 mouth = cm.MultiplyPoint3x4(new Vector3(0.03f, 0.36f, 0.13f) * k);
                    o.handL.pos = Vector3.Lerp(o.handL.pos, mouth, w);
                }
            }
            if (recoil > 0.001f)
            {
                o.handR.pos += (-aim * 0.07f + Vector3.up * 0.035f) * (recoil * k);
                o.handR.rot = o.handR.rot * Quaternion.Euler(-16f * recoil, 0f, 0f);
                if (a == ArmKind.RifleAim) o.handL.pos += (-aim * 0.05f + Vector3.up * 0.03f) * (recoil * k);
                o.chest = o.chest * Quaternion.Euler(-3f * recoil, 0f, 0f);
                recoil *= Mathf.Exp(-16f * dt);
            }
        }

        void GenExtras(BodyPose o, float dt)
        {
            float wantYaw = 0f, wantPitch = 0f;
            if (lookWeight > 0.01f)
            {
                Vector3 d = lookLocal.sqrMagnitude > 0.001f ? lookLocal.normalized : Vector3.forward;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
                // a point behind the shoulder is let go: the head comes back to the front instead of being wrung
                // round to the stop and flipping to the other side when the point crosses behind the back
                float w = lookWeight * (1f - Mathf.InverseLerp(95f, 130f, Mathf.Abs(yaw)));
                wantYaw = Mathf.Clamp(yaw, -75f, 75f) * w;
                wantPitch = Mathf.Clamp(pitch, -30f, 35f) * w;
            }
            else if (body != BodyKind.Dead)
            {
                // nobody to look at: now and then a glance round, wider when standing than when walking
                if (idleT >= nextGlance)
                {
                    float sign = rnd.NextDouble() < 0.5 ? -1f : 1f;
                    glanceYaw = sign * (18f + (float)rnd.NextDouble() * 40f);
                    glancePitch = -6f + (float)rnd.NextDouble() * 12f;
                    glanceUntil = idleT + 1.2f + (float)rnd.NextDouble() * 1.8f;
                    nextGlance = glanceUntil + 3f + (float)rnd.NextDouble() * 6f;
                }
                if (idleT < glanceUntil) { wantYaw = glanceYaw * (1f - 0.6f * moveW); wantPitch = glancePitch * (1f - moveW); }
            }
            // a head turns at a human pace and settles without a jerk
            lookYaw = U.Damp(lookYaw, wantYaw, 6f, dt);
            lookPitch = U.Damp(lookPitch, wantPitch, 6f, dt);
            if (Mathf.Abs(lookYaw) > 0.05f || Mathf.Abs(lookPitch) > 0.05f)
            {
                o.neck = o.neck * Quaternion.Euler(lookPitch * 0.4f, lookYaw * 0.42f, 0f);
                o.head = o.head * Quaternion.Euler(lookPitch * 0.6f, lookYaw * 0.58f, -lookYaw * 0.05f);
            }
            if (flinch > 0.01f)
            {
                float f = flinch * flinch;
                o.spine = o.spine * Quaternion.Euler(-flinchDir.z * 10f * f, 0f, flinchDir.x * 8f * f);
                o.chest = o.chest * Quaternion.Euler(-flinchDir.z * 14f * f, flinchDir.x * -10f * f, flinchDir.x * 8f * f);
                o.head = o.head * Quaternion.Euler(-flinchDir.z * 12f * f, 0f, 0f);
                flinch = Mathf.Max(0f, flinch - dt * 3.2f);
            }
        }

        // ================================================================== application

        void Apply(BodyPose p)
        {
            Transform root = rig.root;
            Quaternion rr = root.rotation;
            Transform hips = rig.B(HB.Hips);
            hips.localPosition = p.hips;
            hips.localRotation = p.hipsRot;
            rig.B(HB.Spine).localRotation = p.spine;
            rig.B(HB.Chest).localRotation = p.chest;
            rig.B(HB.Neck).localRotation = p.neck;
            rig.B(HB.Head).localRotation = p.head;

            bool adapt = body == BodyKind.Stand && bodyBlend >= 1f;
            Vector3 fl = p.footL.pos, fr = p.footR.pos;
            if (adapt) { fl.y += footOffsetL; fr.y += footOffsetR; }
            IK.TwoBone(rig.B(HB.ThighL), rig.B(HB.ShinL), root.TransformPoint(fl), rr * p.footL.pole, rig.legU, rig.legF, invThighL, invShinL);
            IK.TwoBone(rig.B(HB.ThighR), rig.B(HB.ShinR), root.TransformPoint(fr), rr * p.footR.pole, rig.legU, rig.legF, invThighR, invShinR);
            rig.B(HB.FootL).rotation = rr * p.footL.rot;
            rig.B(HB.FootR).rotation = rr * p.footR.rot;
            rig.B(HB.ToeL).localRotation = Quaternion.Euler(p.toeL, 0f, 0f);
            rig.B(HB.ToeR).localRotation = Quaternion.Euler(p.toeR, 0f, 0f);

            IK.TwoBone(rig.B(HB.ArmR), rig.B(HB.ForeR), root.TransformPoint(p.handR.pos), rr * p.handR.pole, rig.armU, rig.armF, invArmR, invForeR);
            Transform hr = rig.B(HB.HandR);
            hr.rotation = p.handR.weight > 0.001f ? Quaternion.Slerp(rig.B(HB.ForeR).rotation, rr * p.handR.rot, p.handR.weight) : rig.B(HB.ForeR).rotation;
            IK.TwoBone(rig.B(HB.ArmL), rig.B(HB.ForeL), root.TransformPoint(p.handL.pos), rr * p.handL.pole, rig.armU, rig.armF, invArmL, invForeL);
            Transform hl = rig.B(HB.HandL);
            hl.rotation = p.handL.weight > 0.001f ? Quaternion.Slerp(rig.B(HB.ForeL).rotation, rr * p.handL.rot, p.handL.weight) : rig.B(HB.ForeL).rotation;
        }
    }
}
