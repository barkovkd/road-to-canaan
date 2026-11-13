using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>The wheels of the wind pumps: each turns about its own axis as fast as the weather blows.</summary>
    public static class Windmills
    {
        class Wheel { public Transform t; public Quaternion rest; public float angle, gust; }

        static readonly List<Wheel> wheels = new List<Wheel>();
        static readonly int WindId = Shader.PropertyToID("_CanaanWind");

        public static int Count => wheels.Count;

        public static void Clear() { wheels.Clear(); }

        public static void Add(Transform wheel)
        {
            wheels.Add(new Wheel { t = wheel, rest = wheel.localRotation, angle = wheels.Count * 47f, gust = wheels.Count * 1.9f });
        }

        /// <summary>How far a wheel has turned since it was hung, in degrees (tests).</summary>
        public static float Turned(int i) { return i < wheels.Count ? wheels[i].angle : 0f; }

        public static void Tick(float dt)
        {
            if (dt <= 0f) return;
            // SkyWeather publishes the wind for the grass: z is its strength, 0.02 in fog to 0.24 in a storm
            float wind = Shader.GetGlobalVector(WindId).z;
            float t = Time.time;
            for (int i = wheels.Count - 1; i >= 0; i--)
            {
                Wheel w = wheels[i];
                if (w.t == null) { wheels.RemoveAt(i); continue; }
                float rate = (30f + wind * 950f) * (0.82f + 0.18f * Mathf.Sin(t * 0.37f + w.gust));
                w.angle = Mathf.Repeat(w.angle + rate * dt, 360f * 1000f);
                w.t.localRotation = w.rest * Quaternion.Euler(0f, 0f, w.angle);
            }
        }
    }
}
