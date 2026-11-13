using UnityEngine;

namespace Canaan
{
    /// <summary>Shared body of horses and animals: a steered motor, gait animation, jumping, bullet hits, death.</summary>
    public class Quadruped : Actor
    {
        public QuadRig rig;
        public QuadAnim anim;
        public QuadSpec spec;

        // ---- control, set every frame by the rider or the animal's own brain
        public float targetSpeed;      // m/s, may be negative to back up
        public float steerYaw;
        public bool steering;

        public float speed;
        public float maxSpeed = 12f, accel = 4.5f, brake = 8f;
        public float turnSlow = 150f, turnFast = 55f;
        public float jumpT = -1f, rearT = -1f;
        public float camDistance;
        protected Vector3 groundNormal = Vector3.up;
        protected float airTime;
        protected int frame;

        protected void InitQuad(QuadRig quadRig, Faction f, string name)
        {
            rig = quadRig;
            spec = quadRig.spec;
            anim = new QuadAnim(rig);
            faction = f;
            displayName = name;
            float r = Mathf.Max(0.07f, 0.46f * rig.k * spec.bodyW);
            float h = Mathf.Max(r * 2.05f, rig.withers * 1.02f);
            SetupController(r, h);
            cc.stepOffset = Mathf.Min(0.45f, h * 0.3f);
        }

        public override Vector3 Chest => rig.B(QB.Chest).position;
        public override Vector3 HeadPos => rig.B(QB.Head).position;

        protected void MotorTick(float dt)
        {
            float a = Mathf.Abs(targetSpeed) > Mathf.Abs(speed) ? accel : brake;
            speed = Mathf.MoveTowards(speed, Mathf.Clamp(targetSpeed, -2f, maxSpeed), a * dt);

            float turnInput = 0f;
            if (steering && (Mathf.Abs(speed) > 0.25f || Mathf.Abs(targetSpeed) > 0.1f))
            {
                float rate = Mathf.Lerp(turnSlow, turnFast, Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed));
                float before = yaw;
                yaw = Mathf.MoveTowardsAngle(yaw, steerYaw, rate * dt);
                turnInput = Mathf.Clamp(Mathf.DeltaAngle(before, yaw) / Mathf.Max(0.0001f, rate * dt), -1f, 1f);
            }
            anim.turn = U.Damp(anim.turn, turnInput, 6f, dt);

            Vector3 fwd = U.Dir(yaw);
            // going downhill fast or uphill: project the motion on the slope so the controller keeps contact
            Vector3 along = Vector3.ProjectOnPlane(fwd, groundNormal).normalized;
            float depth = World.WaterDepth(transform.position);
            float wet = depth > 0.5f * rig.k ? Mathf.Lerp(0.8f, 0.4f, Mathf.InverseLerp(0.5f, 1.5f, depth / Mathf.Max(0.3f, rig.k))) : 1f;
            Vector3 hv = (grounded ? along : fwd) * (speed * wet);
            velocity.x = hv.x; velocity.z = hv.z;
            velocity.y = grounded && velocity.y <= 0f ? Mathf.Min(hv.y, 0f) - 2.5f : velocity.y - 19f * dt;

            if (cc != null && cc.enabled)
            {
                cc.Move(velocity * dt);
                grounded = cc.isGrounded;
                // a wall in front kills the speed instead of sliding along it at a gallop
                if ((cc.collisionFlags & CollisionFlags.Sides) != 0 && speed > 2f)
                {
                    Vector3 real = cc.velocity; real.y = 0f;
                    if (real.magnitude < speed * 0.35f) speed = Mathf.Max(1f, real.magnitude);
                }
            }
            airTime = grounded ? 0f : airTime + dt;

            if (jumpT >= 0f)
            {
                jumpT += dt / 0.85f;
                if (jumpT >= 1f) jumpT = -1f;
            }
            if (rearT >= 0f)
            {
                rearT += dt / 1.3f;
                if (rearT >= 1f) rearT = -1f;
            }

            if (camDistance < 90f && Physics.Raycast(transform.position + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                groundNormal = Vector3.Slerp(groundNormal, hit.normal, 1f - Mathf.Exp(-6f * dt));
            else groundNormal = Vector3.Slerp(groundNormal, Vector3.up, 1f - Mathf.Exp(-6f * dt));
            Vector3 tilt = Vector3.Slerp(Vector3.up, groundNormal, 0.85f);
            transform.rotation = Quaternion.FromToRotation(Vector3.up, tilt) * Quaternion.Euler(0f, yaw, 0f);
        }

        public bool Jump()
        {
            if (!grounded || jumpT >= 0f || dead) return false;
            velocity.y = 7.2f * Mathf.Sqrt(Mathf.Clamp(rig.k * spec.legLen, 0.3f, 1.2f));
            grounded = false;
            jumpT = 0f;
            return true;
        }

        public void Rear() { if (rearT < 0f && !dead) rearT = 0f; }

        public override void LateTick(float dt)
        {
            frame++;
            Camera cam = Game.I != null ? Game.I.cam : null;
            camDistance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
            if (dead) { if (Time.time - deadAt < 1.2f) anim.Tick(dt); return; }
            if (camDistance > 200f && (frame & 7) != 0) return;
            anim.speed = speed;
            anim.jump = jumpT;
            anim.rear = rearT >= 0f ? Mathf.Sin(Mathf.Clamp01(rearT) * Mathf.PI) : 0f;
            anim.Tick(dt);
            if (anim.stepped && grounded && camDistance < 45f)
            {
                float v = Mathf.Abs(speed);
                Sfx.Hoof(transform.position, Mathf.Lerp(0.2f, 0.75f, v / 12f) * rig.k, Mathf.Lerp(1.3f, 0.9f, rig.k), 34f);
                if (v > 6f) Fx.Dust(transform.position - transform.forward * 0.6f * rig.k + Vector3.up * 0.05f, 0.5f * rig.k, new Color32(176, 152, 118, 120));
            }
        }

        protected override void OnDeath(DamageInfo d)
        {
            anim.Die();
            if (cc != null) cc.enabled = false;
            speed = 0f; targetSpeed = 0f; velocity = Vector3.zero;
        }

        public override bool RayHit(Vector3 origin, Vector3 dir, float maxDist, out BodyHit hit)
        {
            hit = default;
            float k = rig.k;
            Vector3 mid = rig.B(QB.Spine).position;
            if (!Geo.RaySphere(origin, dir, mid, 2.3f * k * Mathf.Max(1f, spec.bodyLen), out _)) return false;
            float best = maxDist;
            bool any = false;
            BodyPart part = BodyPart.Torso;
            Vector3 pelvis = rig.B(QB.Pelvis).position, chest = rig.B(QB.Chest).position;
            Vector3 neck = rig.B(QB.Neck).position, head = rig.B(QB.Head).position, mouth = rig.mouth.position;
            float bodyR = 0.29f * k * Mathf.Max(spec.bodyW, spec.bodyD);
            if (Geo.RayCapsule(origin, dir, pelvis - Vector3.up * 0.08f * k, chest - Vector3.up * 0.1f * k, bodyR, out float t) && t < best)
            {
                best = t; any = true;
                Vector3 heart = rig.B(QB.Chest).TransformPoint(new Vector3(0f, -0.2f, 0.02f) * k);
                part = Geo.RaySphere(origin, dir, heart, 0.15f * k, out _) ? BodyPart.Heart : BodyPart.Torso;
            }
            if (Geo.RayCapsule(origin, dir, neck, head, 0.13f * k * spec.neckThick, out t) && t < best) { best = t; part = BodyPart.Torso; any = true; }
            if (Geo.RayCapsule(origin, dir, head, mouth, 0.1f * k * spec.headSize, out t) && t < best) { best = t; part = BodyPart.Head; any = true; }
            for (int leg = 0; leg < 4; leg++)
            {
                Vector3 a = rig.Leg(leg, 0).position, b = rig.Leg(leg, 1).position, c = rig.Leg(leg, 2).position;
                if (Geo.RayCapsule(origin, dir, a, b, 0.09f * k * spec.legThick, out t) && t < best) { best = t; part = BodyPart.LegL; any = true; }
                if (Geo.RayCapsule(origin, dir, b, c, 0.06f * k * spec.legThick, out t) && t < best) { best = t; part = BodyPart.LegL; any = true; }
            }
            if (!any) return false;
            hit = new BodyHit { dist = best, part = part, point = origin + dir * best };
            return true;
        }
    }
}
