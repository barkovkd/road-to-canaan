using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// The train on the branch line. It stands at Dry Ford with steam up, backs out along the line to the edge of the
    /// map, is gone a while and comes in again engine first to stop at the platform. Every car is its own object set on
    /// the track at two points (its trucks); wheels turn with the distance run, the engine's coupling rods ride on the
    /// crank pins, the main rods drive the crossheads, the stack puffs with each beat of the exhaust - hard and dark
    /// when she pulls, a wisp when she drifts. The whistle blows leaving, coming in and at anyone on the line, and the
    /// engineer stops short of them. The running gear's numbers are rail.py's.
    /// </summary>
    public static class Trains
    {
        public enum Phase { Standing, Leaving, Away, Coming }

        class Car
        {
            public Transform t;
            public float offset, half;                 // behind the engine's centre; half the distance between its trucks
            public readonly List<Transform> wheels = new List<Transform>();
            public readonly List<float> radius = new List<float>();
        }

        // ---- running gear (Tools/blender/rail.py)
        const float Rail = 0.11f, DriverR = 0.80f, Crank = 0.30f, CylX = 1.07f, MainRod = 2.25f, LeadR = 0.38f, CarR = 0.42f;
        const float PinR = 205f, PinL = 115f;
        static readonly float[] DriverZ = { -2.55f, -0.75f }, LeadZ = { 2.1f, 3.3f };
        const float DriverY = Rail + DriverR, CylY = DriverY + 0.04f, RodMidZ = -1.65f;
        // ---- the timetable and the engineer
        const float Cruise = 11f, Backing = 8f, Pull = 0.35f, Brake = 0.6f, Emergency = 1.6f;
        const float FirstStand = 300f, Stand = 240f, Away = 200f;

        static Vector3[] pts;
        static float[] cum;
        static float length;
        static readonly List<Car> cars = new List<Car>();
        static readonly Transform[] drivers = new Transform[2], siderods = new Transform[2], mainrods = new Transform[2], crossheads = new Transform[2];
        static readonly List<Transform> leads = new List<Transform>();
        static Vector3 stackMark, whistleMark, cockR, cockL;
        static float s, v, stopAt, distance, phaseT, standFor, awayFor, blockedFor, smokeDue, whistleUntil, cocksUntil, bellAt, whistleAt;
        static int beat;
        static Phase phase;
        static bool built, blocked, approachWhistled;

        public static Phase Now => phase;
        public static float Speed => v;
        public static float Position => s;
        public static float StopAt => stopAt;
        public static float Distance => distance;
        public static bool Built => built;
        public static Transform Engine => cars.Count > 0 ? cars[0].t : null;
        public static float DriverAngle => distance / DriverR * Mathf.Rad2Deg;
        public static Transform SideRod(int side) { return siderods[side]; }
        /// <summary>A point on the track (the roadbed) at a distance along the line (tests).</summary>
        public static Vector3 TrackPoint(float along) { return At(along); }
        /// <summary>Where the last car ends, along the line.</summary>
        public static float Tail => cars.Count == 0 ? s : s - cars[cars.Count - 1].offset - cars[cars.Count - 1].half - 1.8f;
        public static Transform Driver(int i) { return drivers[i]; }

        public static void Clear()
        {
            cars.Clear(); leads.Clear(); built = false;
            for (int i = 0; i < 2; i++) { drivers[i] = siderods[i] = mainrods[i] = crossheads[i] = null; }
        }

        /// <summary>
        /// Lays the train on the line: 'roadbed' runs from the far end of the line to the buffers (points on the
        /// roadbed, as the track pieces lie); the cars stand 'behind' metres short of the end, the engine first.
        /// </summary>
        public static void Build(Transform parent, IList<Vector3> roadbed, float[] behind, int[] kinds)
        {
            Clear();
            if (!Models.Has("rail_loco") || roadbed.Count < 2) return;
            pts = new Vector3[roadbed.Count];
            cum = new float[roadbed.Count];
            for (int i = 0; i < roadbed.Count; i++)
            {
                pts[i] = roadbed[i];
                if (i > 0) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            }
            length = cum[cum.Length - 1];
            stopAt = length - behind[0];
            for (int i = 0; i < kinds.Length; i++)
            {
                var car = new Car { offset = behind[i] - behind[0], half = kinds[i] == 0 ? 2.2f : kinds[i] == 1 ? 1.35f : 3.2f };
                car.t = MakeCar(parent, kinds[i], car, i);
                cars.Add(car);
            }
            s = stopAt; v = 0f; distance = 0f; beat = 0;
            phase = Phase.Standing; phaseT = 0f; standFor = FirstStand;
            built = true;
            Place();
        }

        static readonly string[] Bodies = { "rail_loco", "rail_tender", "rail_boxcar", "rail_coach" };

        static Transform MakeCar(Transform parent, int kind, Car car, int index)
        {
            GameObject go = HumanModel.Dyed(Bodies[kind], "Train_" + index + "_" + Bodies[kind], 12, kind == 3 ? Pal.WoodGreen : Pal.WoodRed);
            go.transform.SetParent(parent, false);
            go.layer = Layers.Static;
            float len = kind == 0 ? 8.5f : kind == 1 ? 5f : 10f;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 2.2f, kind == 0 ? 0.6f : 0f);
            box.size = new Vector3(2.7f, 4f, len + (kind == 0 ? 1.4f : 0f));
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            Transform t = go.transform;

            void Wheels(string model, float r, params float[] zs)
            {
                foreach (float z in zs)
                {
                    GameObject w = Models.Spawn(model, t, new Vector3(0f, Rail + r, z), Quaternion.identity);
                    car.wheels.Add(w.transform);
                    car.radius.Add(r);
                }
            }

            // panes in the open windows: they glow with the town's at night
            var glass = new MeshBuilder();
            glass.Col(Pal.Glass, 0.9f);
            if (kind == 0)
            {
                foreach (float x in new[] { -1.225f, 1.225f }) glass.Box(new Vector3(x, 3.0f, -2.9f), new Vector3(0.012f, 0.8f, 1.4f));
                ModelLibrary.Entry e = Models.Get("rail_loco");
                if (!e.Mark("smoke", out stackMark)) stackMark = new Vector3(0f, 4.3f, 2.95f);
                if (!e.Mark("whistle", out whistleMark)) whistleMark = new Vector3(0.16f, 3.4f, -0.45f);
                if (!e.Mark("cock_r", out cockR)) cockR = new Vector3(CylX, 0.6f, 2.7f);
                if (!e.Mark("cock_l", out cockL)) cockL = new Vector3(-CylX, 0.6f, 2.7f);
                glass.Col(Pal.GlassLit);
                if (e.Mark("lamp", out Vector3 lamp))
                {
                    glass.Cyl(lamp + Vector3.back * 0.008f, lamp + Vector3.forward * 0.004f, 0.165f, 0.165f, 12);
                    var lg = new GameObject("Headlamp");
                    lg.transform.SetParent(t, false);
                    lg.transform.localPosition = lamp + Vector3.forward * 0.1f;
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Spot; l.spotAngle = 38f; l.range = 70f; l.color = new Color(1f, 0.82f, 0.55f);
                    l.intensity = 9f * RegionLights.Boost; l.shadows = LightShadows.None;
                    RegionLights.Register(l, true, false);
                }
                if (e.Mark("firedoor", out Vector3 fire))
                {
                    glass.Box(fire + Vector3.back * 0.004f, new Vector3(0.34f, 0.26f, 0.008f));
                    var fg = new GameObject("Firebox");
                    fg.transform.SetParent(t, false);
                    fg.transform.localPosition = fire + Vector3.back * 0.45f;
                    var l = fg.AddComponent<Light>();
                    l.type = LightType.Point; l.range = 3.2f; l.color = new Color(1f, 0.55f, 0.22f);
                    l.intensity = 1.6f * RegionLights.Boost; l.shadows = LightShadows.None;
                    RegionLights.Register(l, true, true);
                }
                // the running gear
                for (int i = 0; i < 2; i++)
                {
                    drivers[i] = Models.Spawn("rail_loco_driver", t, new Vector3(0f, DriverY, DriverZ[i]), Quaternion.identity).transform;
                    float sx = i == 0 ? 1f : -1f;
                    siderods[i] = Models.Spawn("rail_loco_siderod", t, new Vector3(sx * 0.89f, DriverY, RodMidZ), Quaternion.identity).transform;
                    mainrods[i] = Models.Spawn("rail_loco_mainrod", t, new Vector3(sx * 0.95f, DriverY, DriverZ[1]), Quaternion.identity).transform;
                    crossheads[i] = Models.Spawn("rail_loco_crosshead", t, new Vector3(sx * CylX, CylY, 1.5f), Quaternion.identity).transform;
                }
                foreach (float z in LeadZ)
                {
                    GameObject w = Models.Spawn("rail_wheelset_s", t, new Vector3(0f, Rail + LeadR, z), Quaternion.identity);
                    car.wheels.Add(w.transform);
                    car.radius.Add(LeadR);
                }
            }
            else if (kind == 1) Wheels("rail_wheelset", CarR, -1.95f, -0.75f, 0.75f, 1.95f);
            else
            {
                Wheels("rail_wheelset", CarR, -3.92f, -2.48f, 2.48f, 3.92f);
                if (kind == 3)
                    for (int i = 0; i < 6; i++)
                        foreach (float x in new[] { -1.262f, 1.262f }) glass.Box(new Vector3(x, 2.8f, -3.5f + i * 1.4f), new Vector3(0.012f, 0.9f, 0.9f));
            }
            if (glass.VertexCount > 0) glass.ToObject("Glass", t, RegionLights.WindowMaterial, false).layer = Layers.Static;
            return t;
        }

        // ================================================================== the track

        /// <summary>The roadbed at a distance along the line.</summary>
        static Vector3 At(float along)
        {
            if (along <= 0f) return pts[0] + (pts[1] - pts[0]).normalized * along;
            if (along >= length) return pts[pts.Length - 1] + (pts[pts.Length - 1] - pts[pts.Length - 2]).normalized * (along - length);
            int lo = 0, hi = cum.Length - 1;
            while (hi - lo > 1) { int mid = (lo + hi) >> 1; if (cum[mid] <= along) lo = mid; else hi = mid; }
            return Vector3.Lerp(pts[lo], pts[hi], (along - cum[lo]) / Mathf.Max(1e-4f, cum[hi] - cum[lo]));
        }

        static void Place()
        {
            foreach (Car c in cars)
            {
                float at = s - c.offset;
                Vector3 front = At(at + c.half), back = At(at - c.half);
                Vector3 fwd = front - back;
                c.t.SetPositionAndRotation((front + back) * 0.5f + Vector3.up * 0.14f, Quaternion.LookRotation(fwd.sqrMagnitude > 1e-6f ? fwd : Vector3.forward, Vector3.up));
                for (int i = 0; i < c.wheels.Count; i++)
                    c.wheels[i].localRotation = Quaternion.Euler(distance / c.radius[i] * Mathf.Rad2Deg, 0f, 0f);
            }
            // the rods follow the crank pins: the coupling rod rides both pins of its side, the main rod joins the
            // forward pin to the crosshead, which slides on its bars
            float turn = distance / DriverR;
            for (int i = 0; i < 2; i++)
            {
                if (drivers[i] != null) drivers[i].localRotation = Quaternion.Euler(turn * Mathf.Rad2Deg, 0f, 0f);
                if (siderods[i] == null) continue;
                float sx = i == 0 ? 1f : -1f;
                float a = (i == 0 ? PinR : PinL) * Mathf.Deg2Rad + turn;
                float py = DriverY + Crank * Mathf.Cos(a), dz = Crank * Mathf.Sin(a);
                siderods[i].localPosition = new Vector3(sx * 0.89f, py, RodMidZ + dz);
                Vector3 pin = new Vector3(sx * 0.95f, py, DriverZ[1] + dz);
                float zc = pin.z + Mathf.Sqrt(Mathf.Max(0f, MainRod * MainRod - (CylY - py) * (CylY - py)));
                Vector3 cross = new Vector3(sx * CylX, CylY, zc);
                mainrods[i].localPosition = pin;
                mainrods[i].localRotation = Quaternion.LookRotation(new Vector3(0f, cross.y - pin.y, cross.z - pin.z), Vector3.up);
                crossheads[i].localPosition = cross;
            }
        }

        static void Show(bool on)
        {
            foreach (Car c in cars) if (c.t.gameObject.activeSelf != on) c.t.gameObject.SetActive(on);
        }

        // ================================================================== running

        /// <summary>Sends the standing train off now (tests); it then keeps the timetable.</summary>
        public static void Depart() { if (built && phase == Phase.Standing) standFor = phaseT; }

        /// <summary>Brings the train in from 'metres' out (tests), running at speed.</summary>
        public static void ArriveFrom(float metres)
        {
            if (!built) return;
            phase = Phase.Coming; phaseT = 0f;
            s = Mathf.Max(cars[cars.Count - 1].offset + 6f, stopAt - metres);
            v = Cruise; approachWhistled = false;
            Show(true);
            Place();
        }

        public static bool Smoking => built && phase != Phase.Away;

        public static void Tick(float dt)
        {
            if (!built || dt <= 0f || cars.Count == 0 || cars[0].t == null) return;
            phaseT += dt;
            float t = Time.time;
            float want = 0f, accel = 0f;
            switch (phase)
            {
                case Phase.Standing:
                    v = 0f;
                    if (phaseT >= standFor)
                    {
                        Whistle(t, 0.5f); whistleAt = t + 0.9f;          // two short: off we go
                        cocksUntil = t + 4.5f;
                        phase = Phase.Leaving; phaseT = 0f;
                    }
                    break;
                case Phase.Leaving:
                    if (whistleAt > 0f && t >= whistleAt) { Whistle(t, 0.5f); whistleAt = -1f; }
                    want = phaseT < 1.5f ? 0f : -Backing;               // the brakes come off before she moves
                    if (s - cars[cars.Count - 1].offset - cars[cars.Count - 1].half < 4f)
                    {
                        phase = Phase.Away; phaseT = 0f; awayFor = Away; v = 0f;
                        Show(false);
                    }
                    break;
                case Phase.Away:
                    if (phaseT >= awayFor) ArriveFrom(length);
                    return;
                case Phase.Coming:
                {
                    float left = stopAt - s;
                    if (!approachWhistled && left < 420f) { Whistle(t, 2.2f); approachWhistled = true; }
                    // brake so as to stop with the engine at the platform
                    float stopping = v * v / (2f * Brake);
                    want = left <= stopping + 2f ? 0f : Cruise;
                    if (left <= stopping + 2f && v > 0.05f) accel = -Mathf.Min(Emergency, v * v / (2f * Mathf.Max(0.3f, left)));
                    if (left < 0.25f || (left < 1.5f && v < 0.3f))
                    {
                        s = stopAt; v = 0f;
                        phase = Phase.Standing; phaseT = 0f; standFor = Stand;
                        cocksUntil = t + 2.5f;
                    }
                    break;
                }
            }

            // anyone on the line ahead: whistle and stop short of them
            bool wasBlocked = blocked;
            blocked = phase != Phase.Standing && Mathf.Abs(want) + Mathf.Abs(v) > 0.01f && SomeoneAhead(want != 0f ? Mathf.Sign(want) : Mathf.Sign(v));
            if (blocked)
            {
                blockedFor = 2f;
                if (!wasBlocked || t > whistleUntil + 2.5f) Whistle(t, 1.2f);
            }
            else blockedFor = Mathf.Max(0f, blockedFor - dt);
            if (blockedFor > 0f) { want = 0f; accel = -Emergency * Mathf.Sign(v); }

            if (accel == 0f)
            {
                float diff = want - v;
                accel = Mathf.Abs(diff) < 1e-3f ? 0f : Mathf.Sign(diff) * (Mathf.Sign(diff) == Mathf.Sign(v) || v == 0f ? Pull : Brake);
            }
            float before = v;
            v += accel * dt;
            if (want == 0f && Mathf.Sign(before) != Mathf.Sign(v) && before != 0f) v = 0f;   // brakes stop, they don't reverse
            if (want != 0f && Mathf.Abs(v) > Mathf.Abs(want)) v = want;
            s += v * dt;
            distance += v * dt;
            Place();
            Effects(dt, t, Mathf.Abs(accel) > 0.01f && Mathf.Sign(accel) == Mathf.Sign(v) ? 1f : Mathf.Abs(v) > 0.3f && accel == 0f ? 0.45f : 0f);
        }

        static bool SomeoneAhead(float dir)
        {
            float lead = dir > 0f ? s + 5.6f : s - cars[cars.Count - 1].offset - cars[cars.Count - 1].half - 2f;
            float look = Mathf.Max(14f, v * v / (2f * Emergency) + 8f);
            Vector3 head = At(lead);
            foreach (Actor a in World.actors)
            {
                if (a == null || a.dead || !a.gameObject.activeInHierarchy) continue;
                Vector3 p = a.transform.position;
                if ((p - head).sqrMagnitude > (look + 6f) * (look + 6f)) continue;
                for (float d = 0f; d <= look; d += 2f)
                {
                    Vector3 q = At(lead + dir * d);
                    float dx = p.x - q.x, dz = p.z - q.z;
                    if (dx * dx + dz * dz < 1.7f * 1.7f && Mathf.Abs(p.y - q.y) < 3f) return true;
                }
            }
            return false;
        }

        static void Whistle(float t, float seconds)
        {
            whistleUntil = Mathf.Max(whistleUntil, t + seconds);
            if (cars.Count > 0) Sfx.Play("loco_whistle", cars[0].t.TransformPoint(whistleMark), 1f, seconds > 1f ? 0.97f : 1.02f, 900f);
        }

        /// <summary>Smoke, steam and sound. 'work': 1 pulling hard, 0.45 running on, 0 drifting or braking.</summary>
        static void Effects(float dt, float t, float work)
        {
            Transform eng = cars[0].t;
            Vector3 stack = eng.TransformPoint(stackMark);
            Vector3 fwd = eng.forward;
            Vector4 w = Shader.GetGlobalVector("_CanaanWind");
            Vector3 wind = new Vector3(w.x, 0f, w.y) * (w.z * 9f);
            float day = Game.I != null && Game.I.clock != null ? Mathf.Clamp01(Game.I.clock.Daylight) : 1f;
            float lit = Mathf.Lerp(0.3f, 1f, day);
            Vector3 trail = -fwd * v * 0.55f;                       // the smoke is left behind as she runs
            bool near = Game.I != null && Game.I.cam != null && (stack - Game.I.cam.transform.position).sqrMagnitude < 420f * 420f;

            // the exhaust beats four times a turn of the drivers
            int now = Mathf.FloorToInt(distance / DriverR / (Mathf.PI * 0.5f));
            if (now != beat)
            {
                beat = now;
                if (work > 0.01f)
                {
                    if (near)
                    {
                        byte g = (byte)Mathf.Lerp(150f, 70f, work);
                        for (int k = 0; k < 3; k++)
                            Fx.Plume(stack + Vector3.up * (0.1f * k), Vector3.up * (2.6f + 1.8f * work) + trail + wind + Jitter(k) * 0.4f,
                                     Mathf.Lerp(0.55f, 0.95f, work), Tone(new Color32(g, g, (byte)(g - 6), (byte)(120 + 60 * work)), lit), Mathf.Lerp(4f, 7f, work));
                    }
                    Sfx.Play("chuff", stack, Mathf.Lerp(0.35f, 0.9f, work), 0.9f + Mathf.Abs(v) * 0.01f, 160f);
                }
            }
            if (!near) return;
            // standing or drifting: grey smoke curling off the banked fire
            if (work < 0.01f)
            {
                smokeDue += dt * (Mathf.Abs(v) < 0.1f ? 4f : 2.5f);
                while (smokeDue >= 1f)
                {
                    smokeDue -= 1f;
                    Fx.Plume(stack + Jitter(beat) * 0.1f, Vector3.up * 1.0f + trail + wind * 1.3f + Jitter(beat + 3) * 0.3f,
                             0.6f, Tone(new Color32(168, 163, 156, 92), lit), 6f);
                }
            }
            // cylinder cocks open as she starts and stops: white jets either side
            if (t < cocksUntil)
            {
                foreach (Vector3 c in new[] { cockR, cockL })
                {
                    Vector3 p = eng.TransformPoint(c);
                    Vector3 side = eng.right * Mathf.Sign(c.x);
                    Fx.Plume(p, side * 3.2f + Vector3.down * 0.4f + Vector3.up * 0.8f + Jitter(beat + (int)(t * 60f)) * 0.5f, 0.35f,
                             Tone(new Color32(240, 240, 236, 110), Mathf.Lerp(0.5f, 1f, day)), 1.2f);
                }
            }
            // a plume of white steam stands over the whistle while it blows
            if (t < whistleUntil)
                Fx.Plume(eng.TransformPoint(whistleMark), Vector3.up * 2.4f + trail * 0.5f + Jitter((int)(t * 40f)) * 0.3f, 0.22f,
                         Tone(new Color32(245, 245, 242, 130), Mathf.Lerp(0.5f, 1f, day)), 1.1f);
            // the bell rings while she moves about the station
            if (Mathf.Abs(v) > 0.2f && Mathf.Abs(stopAt - s) < 160f && t >= bellAt)
            {
                bellAt = t + 0.9f;
                Sfx.Play("bell", eng.TransformPoint(new Vector3(0f, 3.5f, 1.1f)), 0.6f, 1f, 120f);
            }
        }

        static Vector3 Jitter(int k)
        {
            float a = k * 2.399963f, b = k * 0.618034f % 1f;
            return new Vector3(Mathf.Cos(a) * b, (k * 0.377f % 1f) - 0.5f, Mathf.Sin(a) * b);
        }

        static Color32 Tone(Color32 c, float k)
        {
            return new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);
        }
    }
}
