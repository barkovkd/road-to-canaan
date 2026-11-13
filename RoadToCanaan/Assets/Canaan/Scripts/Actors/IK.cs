using UnityEngine;

namespace Canaan
{
    /// <summary>Analytic two-bone inverse kinematics with a deterministic twist.</summary>
    public static class IK
    {
        /// <summary>
        /// Inverse of the frame a bone has in its bind pose: Z along the bone towards its child,
        /// Y towards the side its joint bends to. Precompute once per bone.
        /// </summary>
        public static Quaternion InvBind(Vector3 childLocal, Vector3 bendLocal)
        {
            return Quaternion.Inverse(Quaternion.LookRotation(childLocal.normalized, bendLocal));
        }

        /// <summary>
        /// Rotates bones a and b (b is a's child, the end joint is b's child at distance lb) so the end reaches
        /// 'target'. 'pole' is the world direction the middle joint should bend towards. Returns the end position.
        /// </summary>
        public static Vector3 TwoBone(Transform a, Transform b, Vector3 target, Vector3 pole, float la, float lb,
                                      Quaternion invBindA, Quaternion invBindB)
        {
            Vector3 ap = a.position;
            Vector3 to = target - ap;
            float d = to.magnitude;
            if (d < 1e-4f) { to = Vector3.down * 1e-3f; d = 1e-3f; }
            Vector3 dir = to / d;
            d = Mathf.Clamp(d, Mathf.Abs(la - lb) + 1e-3f, la + lb - 1e-3f);

            Vector3 bend = pole - dir * Vector3.Dot(pole, dir);
            if (bend.sqrMagnitude < 1e-6f)
            {
                bend = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right);
            }
            bend.Normalize();

            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 mid = ap + dir * (la * cosA) + bend * (la * sinA);
            Vector3 end = ap + dir * d;

            a.rotation = Quaternion.LookRotation(mid - ap, bend) * invBindA;
            b.rotation = Quaternion.LookRotation(end - mid, bend) * invBindB;
            return end;
        }
    }
}
