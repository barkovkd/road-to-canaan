using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Canaan
{
    /// <summary>
    /// Bot scenarios, started with "-autotest name -outdir dir". Time advances by a fixed step per frame, so a
    /// run does not depend on machine speed. Writes report.txt ending in "RESULT: PASS" or "RESULT: FAIL why".
    /// </summary>
    public partial class AutoTest : MonoBehaviour
    {
        string outDir;
        StreamWriter report;
        readonly List<string> errors = new List<string>();
        readonly Dictionary<string, Func<IEnumerator>> scenarios = new Dictionary<string, Func<IEnumerator>>();
        bool finished;
        int shots;
        float realStart;

        void Awake()
        {
            outDir = Args.OutDir;
            Directory.CreateDirectory(outDir);
            report = new StreamWriter(Path.Combine(outDir, "report.txt")) { AutoFlush = true };
            Application.logMessageReceived += OnLog;
            Time.captureFramerate = Args.RealRender ? 0 : 60;
            UnityEngine.Random.InitState(20261112);   // the same dice every run
            realStart = Time.realtimeSinceStartup;
            Register();
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            if (errors.Count < 30) errors.Add(type + ": " + message + (type == LogType.Exception ? "\n" + stack : ""));
        }

        IEnumerator Start()
        {
            string name = Args.AutoTest;
            Log("scenario " + name + "  unity " + Application.unityVersion + "  gpu " + SystemInfo.graphicsDeviceName);
            if (!scenarios.TryGetValue(name, out var make))
            {
                Finish("unknown scenario " + name);
                yield break;
            }
            Game.I.SetRegion(Region.Studio());
            Game.I.clock.frozen = true;
            Game.I.clock.hour = 10.5f;
            yield return null;
            yield return null;
            IEnumerator run = make();
            while (true)
            {
                bool more;
                try { more = run.MoveNext(); }
                catch (Exception e) { Finish("exception in scenario: " + e); yield break; }
                if (!more || finished) break;
                yield return run.Current;
            }
            if (!finished) Finish(null);
        }

        // ------------------------------------------------------------ helpers for scenarios

        public void Log(string s)
        {
            string line = string.Format(CultureInfo.InvariantCulture, "[{0,7:0.00}] {1}", Time.time, s);
            report.WriteLine(line);
        }

        public void Check(bool ok, string what)
        {
            Log((ok ? "ok    " : "FAILED ") + what);
            if (!ok) failures.Add(what);
        }

        readonly List<string> failures = new List<string>();

        public void Snap(string name, int w = 1280, int h = 720)
        {
            shots++;
            string file = shots.ToString("00") + "_" + name + ".jpg";
            Shot.Capture(Game.I.cam, Path.Combine(outDir, file), w, h);
            Log("shot " + file);
        }

        public static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        void Finish(string fatal)
        {
            if (finished) return;
            finished = true;
            foreach (string e in errors) report.WriteLine("LOGGED " + e);
            string why = fatal;
            if (why == null && failures.Count > 0) why = failures.Count + " check(s) failed: " + string.Join("; ", failures);
            if (why == null && errors.Count > 0) why = errors.Count + " error(s) logged";
            report.WriteLine(string.Format(CultureInfo.InvariantCulture, "game time {0:0.0}s, real time {1:0.0}s, frames {2}",
                Time.time, Time.realtimeSinceStartup - realStart, Time.frameCount));
            report.WriteLine(why == null ? "RESULT: PASS" : "RESULT: FAIL " + why);
            report.Close();
            Application.Quit(why == null ? 0 : 1);
        }

        // ------------------------------------------------------------ scenarios

        void Register()
        {
            scenarios["smoke"] = Smoke;
            scenarios["models"] = Models;
            scenarios["anim"] = Anim;
            scenarios["move"] = Move;
            scenarios["horse"] = HorseRide;
            scenarios["combat"] = Combat;
            scenarios["ui"] = Ui;
            scenarios["tour"] = Tour;
            scenarios["town"] = Town;
            scenarios["law"] = LawTest;
            scenarios["hunt"] = Hunt;
            scenarios["camp"] = CampTest;
            scenarios["perf"] = Perf;
            scenarios["newgame"] = NewGame;
            scenarios["beauty"] = Beauty;
            scenarios["props"] = PropsShow;
            scenarios["facades"] = Facades;
            scenarios["yard"] = Yard;
            scenarios["train"] = TrainRun;
            scenarios["rooms"] = Rooms;
            scenarios["flora"] = FloraShow;
            scenarios["sky"] = SkyShow;
            scenarios["townlook"] = TownLook;
        }

        IEnumerator Smoke()
        {
            yield return Wait(0.2f);
            Look(new Vector3(1, 3.2f, -9), new Vector3(0.5f, 0.6f, 0), 55f);
            Snap("stage");
            Check(Mats.Lit != null, "materials loaded");
            if (Args.Has("-audio"))
            {
                Check(Sfx.Recorded >= 30, "recorded sounds loaded: " + Sfx.Recorded + " sets");
                foreach (string id in new[] { "shot_revolver", "shot_revolver_far", "shot_repeater", "shot_rifle", "shot_shotgun", "shot_small",
                                              "step_dirt", "step_wood", "step_grass", "hoof_dirt", "hoof_wood", "whinny", "door_open", "door_close",
                                              "wind", "rain", "river", "fire", "birds", "crickets", "thunder", "loco_whistle", "chuff", "bell" })
                    Check(Sfx.Has(id), "sound " + id);
            }
            KeyboardMap();
        }

        /// <summary>The real keyboard, not the bot's virtual buttons: a test keyboard device presses the keys and
        /// GameInput reads them as the game does - the arrows move, Z aims, C fires, WASD still works.</summary>
        void KeyboardMap()
        {
            var kb = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            bool was = GameInput.Virtual;
            GameInput.Virtual = false;
            void Press(params Key[] keys)
            {
                InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
                InputSystem.Update();
                GameInput.Tick(0.016f);
            }
            Press(Key.UpArrow, Key.LeftArrow);
            Check(Keyboard.current == kb, "the test keyboard is the current one");
            Check(GameInput.Move.y > 0.5f && GameInput.Move.x < -0.5f, "the arrows move (up and left: " + GameInput.Move + ")");
            Press(Key.Z);
            Check(GameInput.Held(Btn.Aim) && GameInput.AimKey && !GameInput.Held(Btn.Fire), "Z aims");
            Press(Key.Z, Key.C);
            Check(GameInput.Held(Btn.Fire) && GameInput.Pressed(Btn.Fire), "C fires");
            Press(Key.S, Key.D);
            Check(GameInput.Move.y < -0.5f && GameInput.Move.x > 0.5f && !GameInput.Held(Btn.Aim), "WASD still moves");
            Press();
            InputSystem.RemoveDevice(kb);
            GameInput.Virtual = was;
            GameInput.Tick(0.016f);
        }
    }
}
