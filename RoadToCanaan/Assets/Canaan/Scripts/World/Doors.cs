using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// Doors that open before whoever walks up to them and fall shut behind: the panelled doors of the shops swing
    /// in, the saloon's batwings swing away from the one who pushes and ring a little before they settle. The leaves
    /// have no colliders - nothing in the way of people, bullets or the path finder; they are there to be looked at.
    /// </summary>
    public static class Doors
    {
        class Leaf
        {
            public Transform pivot;
            public Vector3 doorway, outward;
            public float shut, open, reach, angle, speed, side = 1f;
            public bool spring;
        }

        static readonly List<Leaf> leaves = new List<Leaf>();

        public static int Count => leaves.Count;

        public static void Clear() { leaves.Clear(); }

        /// <param name="doorway">middle of the threshold, world</param>
        /// <param name="outward">the street side of the doorway, world</param>
        /// <param name="shutYaw">local yaw of the leaf when shut</param>
        /// <param name="open">swing in degrees for somebody coming from the street</param>
        public static void Add(Transform pivot, Vector3 doorway, Vector3 outward, float shutYaw, float open, float reach, bool spring)
        {
            leaves.Add(new Leaf { pivot = pivot, doorway = doorway, outward = outward, shut = shutYaw, open = open, reach = reach, spring = spring });
        }

        /// <summary>How far the widest leaf near a point stands open, in degrees (tests).</summary>
        public static float OpenNear(Vector3 p, float within = 3f)
        {
            float best = 0f;
            foreach (Leaf l in leaves)
                if ((l.doorway - p).sqrMagnitude < within * within) best = Mathf.Max(best, Mathf.Abs(l.angle));
            return best;
        }

        public static void Tick(float dt)
        {
            if (dt <= 0f || Game.I == null || Game.I.cam == null) return;
            Vector3 eye = Game.I.cam.transform.position;
            dt = Mathf.Min(dt, 0.05f);
            for (int i = leaves.Count - 1; i >= 0; i--)
            {
                Leaf l = leaves[i];
                if (l.pivot == null) { leaves.RemoveAt(i); continue; }
                bool near = (l.doorway - eye).sqrMagnitude < 80f * 80f;
                if (!near && Mathf.Abs(l.angle) < 0.05f && Mathf.Abs(l.speed) < 0.05f) continue;

                // somebody at the doorway? the nearest one decides which way a batwing gives
                float want = 0f;
                if (near)
                {
                    float best = l.reach * l.reach;
                    Human who = null;
                    foreach (Human h in World.humans)
                    {
                        if (h == null || h.dead || h.mount != null) continue;
                        Vector3 d = h.Pos - l.doorway;
                        if (d.y < -1f || d.y > 2f) continue;
                        float sq = d.x * d.x + d.z * d.z;
                        if (sq < best) { best = sq; who = h; }
                    }
                    if (who != null)
                    {
                        if (l.spring && Mathf.Abs(l.angle) < 4f) l.side = Vector3.Dot(who.Pos - l.doorway, l.outward) >= 0f ? 1f : -1f;
                        want = l.open * (l.spring ? l.side : 1f);
                    }
                }
                // a spring: stiff and well damped for a house door, loose for a batwing
                float k = l.spring ? 70f : 110f, c = l.spring ? 3.4f : 19f;
                if (want != 0f && l.spring) c = 9f;                       // held by a hand while somebody is in the doorway
                float before = l.angle;
                l.speed += (-(l.angle - want) * k - l.speed * c) * dt;
                l.angle += l.speed * dt;
                // a house door creaks open and bangs to; a batwing only creaks as it is pushed through
                if (want != 0f && Mathf.Abs(before) < 1f && Mathf.Abs(l.angle) >= 1f)
                    Sfx.Play("door_open", l.doorway + Vector3.up, l.spring ? 0.22f : 0.45f, l.spring ? 1.15f : 1f, 18f);
                if (!l.spring && want == 0f && Mathf.Abs(before) >= 3f && Mathf.Abs(l.angle) < 3f)
                    Sfx.Play("door_close", l.doorway + Vector3.up, 0.5f, 1f, 22f);
                if (want == 0f && Mathf.Abs(l.angle) < 0.03f && Mathf.Abs(l.speed) < 0.5f) { l.angle = 0f; l.speed = 0f; }
                l.pivot.localRotation = Quaternion.Euler(0f, l.shut + l.angle, 0f);
            }
        }
    }
}
