using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Small maths and helper functions used everywhere.</summary>
    public static class U
    {
        /// <summary>Frame-rate independent approach of a to b; 'rate' is roughly 1/seconds.</summary>
        public static float Damp(float a, float b, float rate, float dt) { return Mathf.Lerp(a, b, 1f - Mathf.Exp(-rate * dt)); }
        public static Vector3 Damp(Vector3 a, Vector3 b, float rate, float dt) { return Vector3.Lerp(a, b, 1f - Mathf.Exp(-rate * dt)); }
        public static Quaternion Damp(Quaternion a, Quaternion b, float rate, float dt) { return Quaternion.Slerp(a, b, 1f - Mathf.Exp(-rate * dt)); }
        public static Color Damp(Color a, Color b, float rate, float dt) { return Color.Lerp(a, b, 1f - Mathf.Exp(-rate * dt)); }

        public static float DampAngle(float a, float b, float rate, float dt) { return Mathf.LerpAngle(a, b, 1f - Mathf.Exp(-rate * dt)); }

        public static float Remap(float v, float a0, float a1, float b0, float b1)
        {
            return Mathf.Lerp(b0, b1, Mathf.InverseLerp(a0, a1, v));
        }

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        public static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
        public static Vector3 FlatDir(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward; }
        public static float FlatDist(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }
        public static Vector3 XZ(float x, float z) { return new Vector3(x, 0f, z); }

        /// <summary>Heading in degrees (0 = +Z, clockwise) of a direction.</summary>
        public static float Yaw(Vector3 dir) { return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; }
        public static Vector3 Dir(float yawDeg) { float r = yawDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)); }

        /// <summary>True on average 'rate' times per second, whatever the frame rate.</summary>
        public static bool Chance(float rate, float dt) { return Random.value < 1f - Mathf.Exp(-rate * dt); }

        public static T Pick<T>(IList<T> list) { return list[Random.Range(0, list.Count)]; }
        public static T Pick<T>(System.Random r, IList<T> list) { return list[r.Next(list.Count)]; }
        public static float Range(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        // ------------------------------------------------------------ hashing and noise

        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            return x;
        }

        public static float Hash01(int x, int y, int seed = 0)
        {
            uint h = Hash((uint)x * 73856093U ^ (uint)y * 19349663U ^ (uint)seed * 83492791U);
            return (h & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Fractal Perlin noise in 0..1.</summary>
        public static float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x, y);
                norm += amp;
                x = x * lacunarity + 31.7f;
                y = y * lacunarity + 17.3f;
                amp *= gain;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------ scene helpers

        public static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        public static string Money(float dollars)
        {
            return dollars.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " $";
        }
    }

    /// <summary>Physics layers. Names are not registered in the project; only the numbers matter.</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int UI = 5;
        public const int Ground = 6;      // terrain
        public const int Static = 7;      // buildings, props, rocks, trunks
        public const int Actor = 8;       // character controllers of people and animals
        public const int Hitbox = 9;      // body-part colliders, bullets only
        public const int Ragdoll = 10;    // corpses and loose items
        public const int Trigger = 11;    // interaction volumes
        public const int Water = 12;
        public const int NoCollide = 13;  // visual-only helpers

        public const int GroundMask = 1 << Ground;
        public const int SolidMask = (1 << Ground) | (1 << Static);
        public const int BulletMask = (1 << Ground) | (1 << Static) | (1 << Hitbox) | (1 << Ragdoll);
        public const int SightMask = (1 << Ground) | (1 << Static);
        public const int ActorMask = 1 << Actor;

        public static void Configure()
        {
            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(Hitbox, i, true);
                Physics.IgnoreLayerCollision(Trigger, i, true);
                Physics.IgnoreLayerCollision(NoCollide, i, true);
                Physics.IgnoreLayerCollision(Water, i, true);
            }
            Physics.IgnoreLayerCollision(Ragdoll, Actor, true);
            Physics.IgnoreLayerCollision(Ragdoll, Ragdoll, false);
        }
    }
}
