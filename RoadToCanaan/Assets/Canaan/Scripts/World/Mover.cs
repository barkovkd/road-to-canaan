using UnityEngine;
using UnityEngine.AI;

namespace Canaan
{
    /// <summary>
    /// Gets a walking person to a destination: along the navigation mesh where one exists (towns, camp),
    /// by direct steering with simple obstacle avoidance elsewhere.
    /// </summary>
    public class Mover
    {
        readonly Human self;
        readonly NavMeshPath path = new NavMeshPath();
        Vector3 dest, lastPos;
        Vector3[] corners;
        int corner;
        bool usePath;
        float repathAt, stuckT, sideUntil, stop = 0.6f;
        int side;
        public bool active;

        public Mover(Human h) { self = h; }

        public bool Arrived => !active;
        public Vector3 Destination => dest;

        public void Go(Vector3 d, float stopDistance = 0.6f)
        {
            if (!active || (d - dest).sqrMagnitude > 0.6f) { dest = d; repathAt = 0f; stuckT = 0f; }
            active = true;
            stop = stopDistance;
        }

        public void Stop()
        {
            active = false;
            self.moveInput = Vector3.zero;
        }

        public void Tick(float dt)
        {
            if (!active || self.Mounted || self.pose != BodyKind.Stand) { self.moveInput = Vector3.zero; return; }
            Vector3 pos = self.transform.position;
            if (U.FlatDist(pos, dest) <= stop) { Stop(); return; }

            if (Time.time >= repathAt)
            {
                repathAt = Time.time + 1.2f;
                usePath = false;
                if (NavMesh.SamplePosition(pos, out NavMeshHit a, 1.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(dest, out NavMeshHit b, 2.5f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
                {
                    corners = path.corners;
                    if (corners.Length >= 2) { usePath = true; corner = 1; }
                }
            }
            Vector3 aim = dest;
            if (usePath)
            {
                while (corner < corners.Length - 1 && U.FlatDist(pos, corners[corner]) < 0.45f) corner++;
                aim = corners[corner];
            }
            Vector3 dir = U.FlatDir(aim - pos);
            if (!usePath) dir = Avoid(pos, dir);
            self.moveInput = dir;

            // not getting anywhere: sidestep for a moment
            if ((pos - lastPos).magnitude < self.HorizontalSpeed * dt * 0.2f + 0.002f) stuckT += dt; else stuckT = 0f;
            lastPos = pos;
            if (stuckT > 0.7f)
            {
                side = Random.value < 0.5f ? -1 : 1;
                sideUntil = Time.time + 0.7f;
                stuckT = 0f;
                repathAt = 0f;
            }
            if (Time.time < sideUntil) self.moveInput = Quaternion.Euler(0f, 75f * side, 0f) * dir;
        }

        Vector3 Avoid(Vector3 pos, Vector3 dir)
        {
            Vector3 o = pos + Vector3.up * 0.7f;
            int mask = 1 << Layers.Static;
            if (!Physics.SphereCast(o, 0.25f, dir, out _, 1.5f, mask, QueryTriggerInteraction.Ignore)) return dir;
            for (int i = 1; i <= 3; i++)
            {
                float a = i * 35f;
                Vector3 l = Quaternion.Euler(0f, -a, 0f) * dir, r = Quaternion.Euler(0f, a, 0f) * dir;
                bool lf = !Physics.SphereCast(o, 0.25f, l, out _, 1.5f, mask, QueryTriggerInteraction.Ignore);
                bool rf = !Physics.SphereCast(o, 0.25f, r, out _, 1.5f, mask, QueryTriggerInteraction.Ignore);
                if (lf && rf) return side >= 0 ? r : l;
                if (lf) { side = -1; return l; }
                if (rf) { side = 1; return r; }
            }
            return dir;
        }
    }

    /// <summary>Who shoots whom.</summary>
    public static class Factions
    {
        /// <summary>Set by the law system: lawmen and bounty hunters treat the player as an enemy.</summary>
        public static bool playerWanted;

        public static bool Hostile(Actor a, Actor b)
        {
            if (a == b || a == null || b == null) return false;
            Faction fa = a.faction, fb = b.faction;
            if (fa == Faction.Outlaw) return fb == Faction.Player || fb == Faction.Camp || fb == Faction.Law;
            if (fa == Faction.Law) return fb == Faction.Outlaw || (fb == Faction.Player && playerWanted);
            if (fa == Faction.Camp) return fb == Faction.Outlaw;
            if (fa == Faction.Player) return fb == Faction.Outlaw || (fb == Faction.Law && playerWanted);
            if (fa == Faction.Predator) return fb != Faction.Predator && fb != Faction.Wild;
            return false;
        }

        public static bool CanSee(Human self, Actor target, float range, float halfFov = 100f)
        {
            Vector3 to = target.Chest - self.HeadPos;
            float d = to.magnitude;
            if (d > range) return false;
            if (d > 5f && Vector3.Angle(self.transform.forward, to) > halfFov) return false;
            return World.LineOfSight(self.HeadPos, target.Chest) || World.LineOfSight(self.HeadPos, target.HeadPos);
        }
    }
}
