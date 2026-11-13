using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Canaan
{
    /// <summary>Films gaits and poses into contact sheets so motion can be judged frame by frame.</summary>
    public partial class AutoTest
    {
        class Sheet
        {
            readonly Texture2D tex;
            readonly int cols, rows, cw, ch;
            int n;

            public Sheet(int cols, int rows, int cw, int ch)
            {
                this.cols = cols; this.rows = rows; this.cw = cw; this.ch = ch;
                tex = new Texture2D(cols * cw, rows * ch, TextureFormat.RGB24, false);
            }

            public void Add(Camera cam)
            {
                if (n >= cols * rows) return;
                Shot.CaptureInto(cam, tex, (n % cols) * cw, (rows - 1 - n / cols) * ch, cw, ch);
                n++;
            }

            public void Save(string path)
            {
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToJPG(84));
            }
        }

        void SaveSheet(Sheet s, string name)
        {
            shots++;
            string file = shots.ToString("00") + "_" + name + ".jpg";
            s.Save(Path.Combine(outDir, file));
            Log("sheet " + file);
        }

        static void Advance(Action<float> tick, float seconds)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds * 60f));
            for (int i = 0; i < steps; i++) tick(1f / 60f);
        }

        /// <summary>Captures 'frames' cells, advancing the animation by 'interval' seconds between them.</summary>
        IEnumerator Film(Sheet sheet, Action<float> tick, float interval, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                yield return null;   // skinning is refreshed once per frame
                sheet.Add(Game.I.cam);
                Advance(tick, interval);
            }
        }

        IEnumerator Still(Sheet sheet)
        {
            yield return null;
            sheet.Add(Game.I.cam);
        }

        IEnumerator Anim()
        {
            Camera cam = Game.I.cam;
            var spec = SampleSpecs()[0];
            HumanRig rig = HumanBuilder.Build(spec, "Walker");
            var anim = new HumanAnim(rig);
            yield return null;

            // ---- human gaits, side view, one cycle each
            const int F = 7;
            var gaits = new Sheet(F, 4, 250, 330);
            Look(new Vector3(4.4f, 1.0f, 0.1f), new Vector3(0, 0.92f, 0.1f), 28f);
            float[] speeds = { 1.5f, 3.4f, 6.2f, 1.3f };
            for (int g = 0; g < 4; g++)
            {
                anim.localVel = new Vector3(0, 0, speeds[g]);
                anim.crouch = g == 3 ? 1f : 0f;
                Advance(anim.Tick, 1.5f);
                float cycle = g == 0 ? 0.95f : g == 1 ? 0.68f : g == 2 ? 0.56f : 0.9f;
                yield return Film(gaits, anim.Tick, cycle / F, F);
            }
            SaveSheet(gaits, "human_gaits");

            // ---- human gaits from behind (what the player sees)
            var back = new Sheet(F, 2, 250, 330);
            Look(new Vector3(0.9f, 1.55f, -3.6f), new Vector3(0, 1.0f, 0), 30f);
            for (int g = 0; g < 2; g++)
            {
                anim.crouch = 0f;
                anim.localVel = new Vector3(0, 0, g == 0 ? 1.5f : 4.5f);
                Advance(anim.Tick, 1.2f);
                yield return Film(back, anim.Tick, (g == 0 ? 0.95f : 0.62f) / F, F);
            }
            SaveSheet(back, "human_back");

            // ---- momentum: leaning into turns, forward when setting off, back when braking; the weight on one leg
            var lean = new Sheet(6, 2, 250, 330);
            Vector3 ChestUp() { return rig.root.InverseTransformDirection(rig.B(HB.Chest).up); }
            Look(new Vector3(0.9f, 1.55f, -3.6f), new Vector3(0, 1.0f, 0), 30f);
            float[] turnSpeed = { 3.4f, 3.4f, 6.2f, 6.2f };
            float[] turnRate = { 260f, -260f, 200f, -200f };
            for (int t = 0; t < 4; t++)
            {
                anim.localVel = new Vector3(0, 0, turnSpeed[t]); anim.turnRate = turnRate[t]; anim.accel = 0f;
                Advance(anim.Tick, 0.8f);
                yield return Still(lean);
                if (t == 0) Check(ChestUp().x > 0.05f, "a runner turning right leans right (chest up " + ChestUp().x.ToString("0.00") + ")");
                if (t == 1) Check(ChestUp().x < -0.05f, "a runner turning left leans left (chest up " + ChestUp().x.ToString("0.00") + ")");
            }
            anim.turnRate = 0f; anim.localVel = Vector3.zero;
            Advance(anim.Tick, 1.5f);
            yield return Still(lean);
            Advance(anim.Tick, 7f);
            yield return Still(lean);
            Look(new Vector3(4.4f, 1.0f, 0.1f), new Vector3(0, 0.92f, 0.1f), 28f);
            anim.localVel = new Vector3(0, 0, 3.4f); anim.accel = 0f;
            Advance(anim.Tick, 1f);
            float steady = ChestUp().z;
            yield return Still(lean);
            anim.accel = 9f;
            Advance(anim.Tick, 0.4f);
            float start = ChestUp().z;
            yield return Still(lean);
            anim.localVel = new Vector3(0, 0, 1.2f); anim.accel = -12f;
            Advance(anim.Tick, 0.3f);
            float brake = ChestUp().z;
            yield return Still(lean);
            Check(start > steady + 0.05f && brake < steady - 0.05f, "the body leans forward setting off and back braking (" +
                  start.ToString("0.00") + " / " + steady.ToString("0.00") + " / " + brake.ToString("0.00") + ")");
            anim.accel = 0f; anim.localVel = Vector3.zero;
            anim.crouch = 1f;
            Advance(anim.Tick, 1f);
            yield return Still(lean);
            anim.crouch = 0f;
            Advance(anim.Tick, 1f);
            yield return Still(lean);
            Advance(anim.Tick, 4f);
            yield return Still(lean);
            SaveSheet(lean, "human_momentum");

            // ---- poses
            var poses = new Sheet(6, 3, 290, 330);
            anim.localVel = Vector3.zero;
            anim.crouch = 0f;
            IEnumerator Pose(BodyKind b, ArmKind a, Vector3 aim, float settle = 1f, Action extra = null)
            {
                anim.body = b; anim.arms = a; anim.aimLocal = aim;
                extra?.Invoke();
                Advance(anim.Tick, settle);
                yield return Still(poses);
            }
            Look(new Vector3(2.6f, 1.25f, 2.6f), new Vector3(0, 0.9f, 0.05f), 30f);
            yield return Pose(BodyKind.Stand, ArmKind.Free, Vector3.forward);
            yield return Pose(BodyKind.Stand, ArmKind.PistolAim, Vector3.forward);
            yield return Pose(BodyKind.Stand, ArmKind.PistolAim, new Vector3(0.5f, 0.5f, 1f).normalized);
            yield return Pose(BodyKind.Stand, ArmKind.RifleAim, Vector3.forward);
            yield return Pose(BodyKind.Stand, ArmKind.Rifle, Vector3.forward);
            yield return Pose(BodyKind.Stand, ArmKind.Fists, Vector3.forward);
            yield return Pose(BodyKind.Stand, ArmKind.Fists, Vector3.forward, 0.15f, () => anim.Punch(false));
            yield return Pose(BodyKind.Stand, ArmKind.HandsUp, Vector3.forward);
            yield return Pose(BodyKind.Stand, ArmKind.Reload, Vector3.forward);
            yield return Pose(BodyKind.SitChair, ArmKind.Free, Vector3.forward);
            yield return Pose(BodyKind.SitGround, ArmKind.Free, Vector3.forward);
            yield return Pose(BodyKind.Kneel, ArmKind.Free, Vector3.forward);
            yield return Pose(BodyKind.Stand, ArmKind.Free, Vector3.forward, 1f, () => anim.crouch = 1f);
            yield return Pose(BodyKind.Stand, ArmKind.RifleAim, Vector3.forward, 1f, () => anim.crouch = 1f);
            anim.crouch = 0f;
            yield return Pose(BodyKind.Stand, ArmKind.Work, Vector3.forward, 0.5f, () => anim.workPhase = 0.55f);
            anim.workPhase = -1f;
            yield return Pose(BodyKind.Stand, ArmKind.Free, Vector3.forward, 0.6f);
            Look(new Vector3(2.9f, 1.9f, 1.9f), new Vector3(0, 0.3f, -0.4f), 34f);
            yield return Pose(BodyKind.Dead, ArmKind.Free, Vector3.forward, 1.2f, () => anim.deadVariant = 0);
            anim.body = BodyKind.Stand; Advance(anim.Tick, 1f);
            Look(new Vector3(2.9f, 1.9f, -1.9f), new Vector3(0, 0.3f, 0.5f), 34f);
            yield return Pose(BodyKind.Dead, ArmKind.Free, Vector3.forward, 1.2f, () => anim.deadVariant = 1);
            SaveSheet(poses, "human_poses");
            UnityEngine.Object.Destroy(rig.go);

            // ---- horse gaits
            QuadRig horse = QuadBuilder.Build(QuadSpec.Horse(0), "Horse");
            QuadBuilder.AddTack(horse, Pal.ClothRed, Pal.Leather);
            var qa = new QuadAnim(horse);
            HumanRig rider = HumanBuilder.Build(spec, "Rider");
            rider.root.SetParent(horse.saddle, false);
            var ra = new HumanAnim(rider) { body = BodyKind.Ride, arms = ArmKind.Reins };
            void TickBoth(float dt)
            {
                qa.Tick(dt);
                ra.rideBounce = qa.riderBounce; ra.rideLean = qa.riderLean; ra.seatPitch = qa.seatPitch;
                ra.Tick(dt);
            }
            var hg = new Sheet(F, 4, 250, 250);
            Look(new Vector3(9.5f, 1.3f, 0.1f), new Vector3(0, 1.15f, 0.1f), 24f);
            float[] hs = { 1.8f, 4.0f, 7.5f, 13f };
            float[] ht = { 1.05f, 0.68f, 0.54f, 0.42f };
            for (int g = 0; g < 4; g++)
            {
                qa.speed = hs[g];
                Advance(TickBoth, 1.5f);
                yield return Film(hg, TickBoth, ht[g] / F, F);
            }
            SaveSheet(hg, "horse_gaits");

            // ---- the front legs where they meet the chest, close up: from the side and from ahead, at a walk and a gallop
            var hsh = new Sheet(F, 4, 250, 250);
            Vector3 shoulder = horse.root.TransformPoint(horse.Bind(QB.FLa));
            for (int g = 0; g < 2; g++)
            {
                qa.speed = g == 0 ? 1.8f : 13f;
                float cycle = g == 0 ? 1.05f : 0.42f;
                Advance(TickBoth, 1.5f);
                Look(shoulder + new Vector3(2.7f, 0.05f, 0.25f), shoulder + new Vector3(0f, -0.2f, 0f), 30f);
                yield return Film(hsh, TickBoth, cycle / F, F);
                Look(shoulder + new Vector3(1.6f, 0.15f, 2.3f), shoulder + new Vector3(0f, -0.2f, 0f), 30f);
                yield return Film(hsh, TickBoth, cycle / F, F);
            }
            SaveSheet(hsh, "horse_shoulder");

            var hp = new Sheet(6, 1, 290, 290);
            Look(new Vector3(6.5f, 2.1f, 5.2f), new Vector3(0, 1.2f, 0.1f), 30f);
            qa.speed = 0f; Advance(TickBoth, 1f); yield return Still(hp);
            qa.graze = 1f; Advance(TickBoth, 1f); yield return Still(hp);
            qa.graze = 0f; qa.rear = 1f; Advance(TickBoth, 0.5f); yield return Still(hp);
            qa.rear = 0f; qa.speed = 9f; qa.jump = 0.45f; Advance(TickBoth, 0.3f); yield return Still(hp);
            qa.jump = -1f; qa.speed = 9f; qa.turn = 1f; Advance(TickBoth, 0.6f); yield return Still(hp);
            rider.root.SetParent(null); rider.root.position = new Vector3(0, 0, -60f);
            qa.turn = 0f; qa.speed = 0f; Advance(TickBoth, 0.5f);
            qa.Die(); Advance(qa.Tick, 1.2f); yield return Still(hp);
            SaveSheet(hp, "horse_poses");
            UnityEngine.Object.Destroy(horse.go);

            // ---- other animals at a run
            var others = new Sheet(F, 4, 250, 190);
            QuadSpec[] specs = { QuadSpec.Deer(true), QuadSpec.Canine(true), QuadSpec.Bison(), QuadSpec.Rabbit() };
            float[] os = { 11f, 9f, 9f, 5f };
            for (int g = 0; g < 4; g++)
            {
                QuadRig q = QuadBuilder.Build(specs[g], "Q");
                var a = new QuadAnim(q) { speed = os[g] };
                float size = specs[g].scale;
                Look(new Vector3(7.5f * Mathf.Max(0.35f, size), 1.0f * size, 0.1f), new Vector3(0, 0.95f * size, 0.1f), 24f);
                Advance(a.Tick, 1.2f);
                yield return Film(others, a.Tick, 0.42f / F, F);
                UnityEngine.Object.Destroy(q.go);
                yield return null;
            }
            SaveSheet(others, "animals_run");
        }
    }
}
