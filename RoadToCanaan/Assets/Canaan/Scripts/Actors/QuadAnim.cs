using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// Procedural gaits for four-legged animals: walk, trot, canter and gallop blended by speed, plus grazing,
    /// rearing, jumping and lying dead. Legs are placed with inverse kinematics on hoof targets.
    /// </summary>
    public class QuadAnim
    {
        public readonly QuadRig rig;
        static int counter;
        readonly int seed = ++counter * 7919;

        // ---- inputs
        public float speed;              // forward speed, m/s
        public float turn;               // -1..1, bends the body
        public float graze;              // 0..1 head down to the ground
        public float alert;              // 0..1 head high
        public float rear;               // 0..1 rearing up
        public float jump = -1f;         // -1 or 0..1 through a jump
        public float lookYaw;            // degrees, head turned
        public bool dead;
        public bool hop;                 // rabbits bound instead of walking

        // ---- outputs
        public float phase;
        public bool stepped;
        public float riderBounce, riderLean, seatPitch;
        public float gallopW, trotW;

        static readonly float[][] Offsets =
        {
            new[] { 0.25f, 0.75f, 0.00f, 0.50f },   // walk     FL FR HL HR
            new[] { 0.50f, 0.00f, 0.00f, 0.50f },   // trot
            new[] { 0.55f, 0.28f, 0.28f, 0.00f },   // canter
            new[] { 0.50f, 0.40f, 0.10f, 0.00f },   // gallop
        };

        readonly Quaternion[] invA = new Quaternion[4], invB = new Quaternion[4], invC = new Quaternion[4];
        readonly float size;             // leg length relative to a horse
        readonly float k;
        float idleT, deadT, moveW;
        Quaternion[] deadFrom;
        Vector3 deadFromPos;
        readonly float[] w = new float[4];
        readonly float[] lastP = new float[4];

        public QuadAnim(QuadRig rig)
        {
            this.rig = rig;
            k = rig.k;
            size = rig.k * rig.spec.legLen;
            hop = rig.spec.kind == "rabbit";
            for (int leg = 0; leg < 4; leg++)
            {
                Vector3 bend = leg < 2 ? Vector3.back : Vector3.forward;
                int a = (int)QB.FLa + leg * 3;
                invA[leg] = IK.InvBind(rig.bindLocal[a + 1], bend);
                invB[leg] = IK.InvBind(rig.bindLocal[a + 2], bend);
                invC[leg] = IK.InvBind(rig.hoofRest[leg] - rig.bind[a + 2], Vector3.forward);
            }
            idleT = (seed & 1023) * 0.21f;
        }

        public void Tick(float dt)
        {
            stepped = false;
            idleT += dt;
            if (dead) { TickDead(dt); return; }

            float vn = Mathf.Abs(speed) / Mathf.Max(0.05f, size);
            bool moving = vn > 0.12f;
            moveW = U.Damp(moveW, moving ? 1f : 0f, 8f, dt);

            // gait weights
            float wTrot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.3f, 3.0f, vn));
            float wCanter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5.2f, 6.2f, vn));
            float wGallop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8.5f, 10.0f, vn));
            if (hop) { wTrot = 1f; wCanter = 1f; wGallop = 1f; }
            w[0] = 1f - wTrot; w[1] = wTrot * (1f - wCanter); w[2] = wCanter * (1f - wGallop); w[3] = wGallop;
            trotW = w[1]; gallopW = w[3];

            float T = w[0] * Mathf.Lerp(1.3f, 0.95f, Mathf.InverseLerp(0.5f, 2.3f, vn))
                    + w[1] * Mathf.Lerp(0.74f, 0.62f, Mathf.InverseLerp(3f, 5.2f, vn))
                    + w[2] * Mathf.Lerp(0.58f, 0.5f, Mathf.InverseLerp(6.2f, 8.5f, vn))
                    + w[3] * Mathf.Lerp(0.46f, 0.38f, Mathf.InverseLerp(10f, 15f, vn));
            T *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(size));            // small animals step faster
            if (hop) T = 0.36f;
            float duty = w[0] * 0.63f + w[1] * 0.45f + w[2] * 0.36f + w[3] * 0.26f;
            float lift = (w[0] * 0.10f + w[1] * 0.17f + w[2] * 0.22f + w[3] * 0.27f) * size;
            if (moving) phase += dt / T * Mathf.Sign(speed == 0f ? 1f : speed);
            phase -= Mathf.Floor(phase);

            float stride = Mathf.Abs(speed) * T;
            float half = Mathf.Min(stride * duty * 0.5f, 0.62f * size);
            float dirSign = speed < 0f ? -1f : 1f;
            float twoPi = Mathf.PI * 2f;

            // ---- body
            float asym = w[2] + w[3];
            float pitch = -(w[2] * 4.5f + w[3] * 6.5f) * Mathf.Cos(twoPi * (phase - 0.1f)) * moveW;
            float bob = (-(w[2] * 0.045f + w[3] * 0.06f) * Mathf.Cos(twoPi * (phase - 0.35f))
                         - (w[0] * 0.012f + w[1] * 0.035f) * Mathf.Cos(2f * twoPi * (phase - duty * 0.5f))) * size * moveW;
            float flex = w[3] * 7f * Mathf.Sin(twoPi * (phase - 0.3f)) * moveW;
            float swayRoll = (w[0] * 2f + w[1] * 1.2f) * Mathf.Sin(twoPi * phase) * moveW;
            float bend = Mathf.Clamp(turn, -1f, 1f);
            float bank = -bend * Mathf.Clamp01(vn / 8f) * 9f;

            float rearA = rear * 40f;
            float jt = jump;
            float jumpPitch = 0f, jumpLift = 0f, tuck = 0f;
            if (jt >= 0f)
            {
                jumpPitch = jt < 0.35f ? Mathf.Lerp(0f, -24f, jt / 0.35f) : jt < 0.7f ? Mathf.Lerp(-24f, 14f, (jt - 0.35f) / 0.35f) : Mathf.Lerp(14f, 0f, (jt - 0.7f) / 0.3f);
                jumpLift = Mathf.Sin(Mathf.Clamp01(jt) * Mathf.PI) * 0.12f * size;
                tuck = Mathf.Sin(Mathf.Clamp01((jt - 0.1f) / 0.8f) * Mathf.PI);
            }
            float totalPitch = pitch - rearA + jumpPitch;
            // pitch about the middle of the back (or the hind feet when rearing)
            Vector3 pelvisBind = rig.bindLocal[(int)QB.Pelvis];
            float halfBack = (rig.Bind(QB.Chest).z - rig.Bind(QB.Pelvis).z) * 0.5f;
            float pivot = Mathf.Lerp(halfBack, 0f, rear);
            float pr = totalPitch * Mathf.Deg2Rad;
            Vector3 pelvisOff = new Vector3(0f, bob + jumpLift + Mathf.Sin(pr) * pivot - rear * 0.10f * size, (1f - Mathf.Cos(pr)) * pivot - rear * 0.12f * size);
            Transform pelvis = rig.B(QB.Pelvis);
            float breath = Mathf.Sin(idleT * 1.3f);
            pelvis.localPosition = pelvisBind + pelvisOff;
            pelvis.localRotation = Quaternion.Euler(totalPitch - flex * 0.5f, bend * 4f, bank + swayRoll);
            rig.B(QB.Spine).localRotation = Quaternion.Euler(flex * 0.5f, bend * 8f, -swayRoll * 0.6f);
            rig.B(QB.Chest).localRotation = Quaternion.Euler(flex * 0.5f + breath * 0.4f * (1f - moveW), bend * 7f, -swayRoll * 0.6f);

            // ---- neck and head
            float nod = (w[0] * 4f * Mathf.Cos(2f * twoPi * phase) + asym * 7f * Mathf.Cos(twoPi * (phase - 0.55f))) * moveW;
            float stretch = (w[2] * 10f + w[3] * 20f) * moveW;
            float neckBase = -totalPitch * 0.75f + stretch + nod - alert * 12f + graze * 78f;
            if (rig.spec.neckAngle < 30f) neckBase = -totalPitch * 0.5f + nod * 0.5f + graze * 42f - alert * 8f;
            float lookY = Mathf.Clamp(lookYaw, -80f, 80f);
            rig.B(QB.Neck).localRotation = Quaternion.Euler(neckBase, bend * 14f + lookY * 0.5f, 0f);
            rig.B(QB.Head).localRotation = Quaternion.Euler(-stretch * 0.7f - nod * 0.6f - graze * 22f + alert * 6f, bend * 8f + lookY * 0.5f, 0f);

            // ---- tail
            float swish = Mathf.Sin(idleT * 2.1f) * Mathf.Max(0f, Mathf.Sin(idleT * 0.37f)) * 18f * (1f - moveW);
            float stream = -(w[1] * 10f + w[2] * 25f + w[3] * 42f) * moveW;
            rig.B(QB.Tail1).localRotation = Quaternion.Euler(stream + totalPitch * 0.5f, 0f, swish + Mathf.Sin(twoPi * phase) * 6f * moveW);
            rig.B(QB.Tail2).localRotation = Quaternion.Euler(stream * 0.4f, 0f, swish * 0.8f);

            // ---- legs
            Transform root = rig.root;
            Quaternion rr = root.rotation;
            Quaternion bodyRot = pelvis.rotation;   // used to carry tucked or raised legs with the body
            for (int leg = 0; leg < 4; leg++)
            {
                bool front = leg < 2;
                float off = Offsets[0][leg] * w[0] + Offsets[1][leg] * w[1] + Offsets[2][leg] * w[2] + Offsets[3][leg] * w[3];
                if (hop) off = leg < 2 ? 0.45f + leg * 0.04f : leg == 2 ? 0f : 0.04f;
                float p = phase + off;
                p -= Mathf.Floor(p);
                float z, y, fold;
                if (p < duty)
                {
                    float s = p / duty;
                    z = Mathf.Lerp(half, -half, s);
                    y = 0f; fold = 0f;
                }
                else
                {
                    float s = (p - duty) / (1f - duty);
                    z = Mathf.Lerp(-half, half, U.Smooth01(s));
                    y = lift * Mathf.Sin(Mathf.PI * s);
                    fold = Mathf.Sin(Mathf.PI * Mathf.Min(1f, s * 1.25f));
                }
                if (lastP[leg] > p && moving) stepped = true;
                lastP[leg] = p;

                Vector3 rest = rig.hoofRest[leg];
                Vector3 hoof = rest + new Vector3(bend * z * (front ? 0.25f : -0.1f), y * moveW, z * dirSign * moveW);
                // idle: square stance with a resting hind leg now and then
                float restLeg = !moving && leg == 2 ? Mathf.Clamp01(Mathf.Sin(idleT * 0.23f) * 3f - 1.6f) : 0f;
                hoof.y += restLeg * 0.03f * size;
                float f = fold * moveW;

                Vector3 hoofW;
                Vector3 upDir;   // from the hoof towards the knee or hock, model space
                int a = (int)QB.FLa + leg * 3;
                Vector3 jointA = rig.bind[a];
                Vector3 toJoint = (jointA - hoof);
                toJoint.x = 0f;
                Vector3 lean = Vector3.Lerp(Vector3.up, toJoint.normalized, 0.3f).normalized;
                float foldDeg = front ? 68f * f : -34f * f;
                upDir = Quaternion.Euler(foldDeg + restLeg * (front ? 0f : -18f), 0f, 0f) * lean;

                float raise = front ? Mathf.Max(rear, tuck) : tuck * 0.8f;
                if (raise > 0.001f)
                {
                    // carried with the body: folded under the chest or belly
                    Vector3 local = rest - rig.Bind(QB.Pelvis) + new Vector3(0f, 0.32f * size, front ? 0.10f * size : 0.18f * size);
                    Vector3 carried = pelvis.localPosition + (pelvis.localRotation * local);
                    hoof = Vector3.Lerp(hoof, carried, raise);
                    upDir = Vector3.Slerp(upDir, Quaternion.Euler(front ? 80f : -50f, 0f, 0f) * Vector3.up, raise);
                }
                hoofW = root.TransformPoint(hoof);
                Vector3 cW = hoofW + (rr * upDir) * rig.lenC[leg];
                Transform ta = rig.Leg(leg, 0), tb = rig.Leg(leg, 1), tc = rig.Leg(leg, 2);
                IK.TwoBone(ta, tb, cW, rr * (front ? Vector3.back : Vector3.forward), rig.lenA[leg], rig.lenB[leg], invA[leg], invB[leg]);
                tc.rotation = Quaternion.LookRotation(hoofW - tc.position, rr * Vector3.forward) * invC[leg];
            }

            riderBounce = (w[1] * 0.035f * Mathf.Max(0f, -Mathf.Cos(2f * twoPi * (phase - duty * 0.5f)))) * moveW;
            riderLean = (w[2] * 0.45f + w[3] * 1f) * moveW;
            seatPitch = totalPitch;
        }

        /// <summary>Call once when the animal dies; the body then settles onto its side.</summary>
        public void Die()
        {
            if (dead) return;
            dead = true;
            deadT = 0f;
            deadFrom = new Quaternion[rig.bones.Length];
            for (int i = 0; i < rig.bones.Length; i++) deadFrom[i] = rig.bones[i].localRotation;
            deadFromPos = rig.B(QB.Pelvis).localPosition;
        }

        void TickDead(float dt)
        {
            if (deadT >= 1f) return;
            deadT = Mathf.Min(1f, deadT + dt / 0.7f);
            float e = deadT * deadT;
            float side = (seed & 1) == 0 ? 1f : -1f;
            float halfW = 0.27f * rig.spec.bodyW * k;
            Vector3 pelvisPos = new Vector3(0f, halfW, rig.bindLocal[(int)QB.Pelvis].z);
            rig.B(QB.Pelvis).localPosition = Vector3.Lerp(deadFromPos, pelvisPos, e);
            for (int i = 0; i < rig.bones.Length; i++)
            {
                Quaternion to = Quaternion.identity;
                QB b = (QB)i;
                if (b == QB.Pelvis) to = Quaternion.Euler(0f, 0f, 86f * side);
                else if (b == QB.Neck) to = Quaternion.Euler(35f, side * -12f, 0f);
                else if (b == QB.Head) to = Quaternion.Euler(-20f, side * -18f, 0f);
                else if (b == QB.Tail1) to = Quaternion.Euler(-10f, 0f, 0f);
                else if (i >= (int)QB.FLa)
                {
                    int leg = (i - (int)QB.FLa) / 3, seg = (i - (int)QB.FLa) % 3;
                    bool front = leg < 2;
                    bool upper = (leg % 2 == 0) == (side < 0f);   // the leg on the upper side lies flatter
                    if (seg == 0) to = Quaternion.Euler(front ? -22f : 18f, 0f, upper ? side * 14f : 0f);
                    else if (seg == 1) to = Quaternion.Euler(front ? 30f : -34f, 0f, 0f);
                    else to = Quaternion.Euler(front ? -42f : 38f, 0f, 0f);
                }
                rig.bones[i].localRotation = Quaternion.Slerp(deadFrom[i], to, e);
            }
        }
    }
}
