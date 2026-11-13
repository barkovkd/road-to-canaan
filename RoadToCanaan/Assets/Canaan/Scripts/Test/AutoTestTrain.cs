using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>The train keeps its timetable: it backs out of Dry Ford with its wheels and rods turning together,
        /// leaves the map, comes in again engine first to stop at the platform, and stops short of a man on the line.</summary>
        IEnumerator TrainRun()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.hideHud = true;
            g.clock.frozen = true;
            g.clock.hour = 15f;
            g.scriptLock = true;
            Check(Trains.Built, "the train is on the line");
            Check(Trains.Now == Trains.Phase.Standing, "at the start it stands at the station");
            Transform eng = Trains.Engine;
            Vector3 mark = r.P("train0");
            Check(new Vector2(eng.position.x - mark.x, eng.position.z - mark.z).magnitude < 0.6f, "the engine stands at its mark by the platform");

            // the hero waits on the platform, on the engine's left
            P.Teleport(eng.position - eng.right * 5.5f, U.Yaw(eng.right));
            yield return Wait(4f);
            float stop = Trains.StopAt;
            Trains.Depart();
            yield return Wait(2.2f);
            yield return BeautyShot("train_leaving", eng.position - eng.right * 7f + eng.forward * 9f + Vector3.up * 1.9f, eng.position + Vector3.up * 1.6f, 55f, 15f, WeatherKind.Clear, 0.05f);
            yield return Wait(18f);
            float run = stop - Trains.Position;
            Check(run > 30f, "she backs out along the line (" + run.ToString("0") + " m in 20 s)");
            Check(Trains.Speed < -3f, "running backwards (" + Trains.Speed.ToString("0.0") + " m/s)");

            // the wheels turn with the distance; the coupling rod rides the crank pin of the forward driver
            float expect = -run / 0.8f * Mathf.Rad2Deg;
            Check(Mathf.Abs(Trains.DriverAngle - expect) < 1f, "the drivers have turned as far as she ran (" + Trains.DriverAngle.ToString("0") + " deg)");
            float a = 205f * Mathf.Deg2Rad;
            Vector3 pin = eng.InverseTransformPoint(Trains.Driver(1).TransformPoint(new Vector3(0.8f, 0.30f * Mathf.Cos(a), 0.30f * Mathf.Sin(a))));
            Vector3 rodEnd = eng.InverseTransformPoint(Trains.SideRod(0).TransformPoint(new Vector3(0f, 0f, 0.9f)));
            float miss = new Vector2(pin.y - rodEnd.y, pin.z - rodEnd.z).magnitude;
            Check(miss < 0.02f, "the coupling rod stays on its crank pin (" + (miss * 1000f).ToString("0") + " mm)");

            yield return Until(() => Trains.Now == Trains.Phase.Away, 160f);
            Check(Trains.Now == Trains.Phase.Away && !eng.gameObject.activeInHierarchy, "she runs off the map and is gone");

            // coming in, engine first, at speed; she brakes to stop at the platform
            Trains.ArriveFrom(650f);
            yield return Until(() => stop - Trains.Position < 230f, 80f);
            Vector3 ahead = Trains.TrackPoint(Trains.Position + 70f);
            Vector3 dir = eng.forward, side = eng.right;
            yield return BeautyShot("train_coming", ahead + side * 7f + Vector3.up * 1.6f, eng.position + Vector3.up * 2.2f - dir * 6f, 45f, 15f, WeatherKind.Clear, 0.05f);
            Check(Trains.Speed > 8f, "she comes in at speed (" + Trains.Speed.ToString("0.0") + " m/s)");
            yield return Wait(0.6f);
            yield return BeautyShot("train_gear_running", eng.position + eng.right * 3.4f + eng.forward * 0.8f + Vector3.up * 1.1f, eng.position + eng.right * 0.8f + Vector3.up * 0.9f - eng.forward * 0.8f, 50f, 15f, WeatherKind.Clear, 0.02f);
            yield return Until(() => Trains.Now == Trains.Phase.Standing, 120f);
            float off = Trains.Position - stop;
            Check(Trains.Now == Trains.Phase.Standing && Mathf.Abs(off) < 0.3f, "she stops at the platform (" + off.ToString("0.00") + " m off)");
            yield return Wait(1.5f);
            yield return BeautyShot("train_arrived", eng.position - eng.right * 9f + eng.forward * 6f + Vector3.up * 2.4f, eng.position + Vector3.up * 1.8f - eng.forward * 6f, 55f, 15f, WeatherKind.Clear, 0.4f);

            // a man on the line behind her: she whistles and stops short of him, then goes on when he steps off
            Vector3 onLine = Trains.TrackPoint(Trains.Tail - 24f);
            P.Teleport(onLine, U.Yaw(eng.forward));
            yield return Wait(1f);
            Trains.Depart();
            yield return Wait(26f);
            float toMan = 0f;
            {
                // how far the end of the train is from the man, along the line
                float best = 1e9f;
                for (float d = -40f; d <= 40f; d += 0.25f)
                {
                    Vector3 q = Trains.TrackPoint(Trains.Tail + d);
                    float dist = new Vector2(q.x - P.Pos.x, q.z - P.Pos.z).magnitude;
                    if (dist < best) { best = dist; toMan = -d; }
                }
            }
            Check(Mathf.Abs(Trains.Speed) < 0.05f && toMan > 2f, "she stops short of a man on the line (" + toMan.ToString("0.0") + " m from him)");
            yield return BeautyShot("train_stopped_short", P.Pos + eng.right * 6f + Vector3.up * 2.2f - eng.forward * 4f, P.Pos + Vector3.up * 1.4f + eng.forward * 6f, 55f, 15f, WeatherKind.Clear, 0.3f);
            P.Teleport(P.Pos - eng.right * 7f, U.Yaw(eng.right));
            float was = Trains.Position;
            yield return Wait(8f);
            Check(Trains.Position < was - 3f, "and goes on when he steps off (" + (was - Trains.Position).ToString("0.0") + " m)");
        }
    }
}
