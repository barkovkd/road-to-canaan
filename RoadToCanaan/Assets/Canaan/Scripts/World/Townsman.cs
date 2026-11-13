using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public enum Routine { Loiter, Keeper, Patrol, Camp, Static }
    public enum Mood { Calm, Flee, HandsUp, Fight, Report }

    /// <summary>
    /// Brain of ordinary people: a daily routine (stand at the counter, stroll, sit, patrol) and reactions —
    /// run from gunfire, raise hands at a gun, punch back, run to the sheriff after seeing a crime.
    /// </summary>
    public class Townsman : Brain
    {
        public Routine routine;
        public List<string> spots = new List<string>();     // region marks to drift between
        public List<string> seats = new List<string>();     // marks where this person may sit
        public string postKey, homeKey;
        public float openHour = 7.5f, closeHour = 20.5f;
        public Mood mood = Mood.Calm;
        public Actor threat;
        public bool reportsCrime = true, asleep;
        public int insulted;
        public float lastGreeted = -99f;
        public Vector3 reportTo;
        public Action<Townsman> reported;
        public readonly Mover mover;

        float waitUntil, moodUntil, checkAt, barkAt;
        bool sitting, headingHome, travelling;
        float pause;
        string arrivedKey;
        string seatTaken;
        static readonly HashSet<string> takenSeats = new HashSet<string>();

        public Townsman(Human h, Routine r)
        {
            self = h;
            routine = r;
            mover = new Mover(h);
            waitUntil = Time.time + UnityEngine.Random.Range(0f, 6f);
        }

        public static void ResetSeats() { takenSeats.Clear(); }

        Region R => Game.I.region;
        float Hour => Game.I.clock.hour;
        bool Open => openHour < closeHour ? Hour >= openHour && Hour < closeHour : Hour >= openHour || Hour < closeHour;
        public bool AtPost => routine == Routine.Keeper && mood == Mood.Calm && !asleep && postKey != null && U.FlatDist(self.Pos, R.P(postKey)) < 1.3f;

        public override void OnDamaged(DamageInfo d)
        {
            if (!(d.attacker is Human a) || a == self) return;
            Rise();
            threat = a;
            if (d.melee && d.weapon == null && UnityEngine.Random.value < 0.55f && self.health > self.maxHealth * 0.4f) Fight(a);
            else Run(a.Pos, 14f);
        }

        /// <summary>Squares up to somebody with his fists for a while (an insult too many, a punch taken).</summary>
        public void Fight(Human who, float seconds = 20f)
        {
            Rise();
            threat = who;
            mood = Mood.Fight;
            moodUntil = Time.time + seconds;
        }

        public void Run(Vector3 from, float seconds)
        {
            if (mood == Mood.Report) return;
            Rise();
            mood = Mood.Flee;
            moodUntil = Time.time + seconds;
            Vector3 away = U.FlatDir(self.Pos - from);
            mover.Go(self.Pos + away * 35f + new Vector3(UnityEngine.Random.Range(-8f, 8f), 0f, UnityEngine.Random.Range(-8f, 8f)), 1.5f);
            Say(Barks.Flee());
        }

        public void StartReport(Vector3 to)
        {
            Rise();
            mood = Mood.Report;
            reportTo = to;
            moodUntil = Time.time + 40f;
            mover.Go(to, 2.5f);
            Say(Barks.Witness());
        }

        public void Calm() { mood = Mood.Calm; threat = null; self.handsUp = false; self.sprint = false; }

        public void Say(string line, float seconds = 3.2f)
        {
            if (Time.time < barkAt) return;
            barkAt = Time.time + 2.5f;
            Game.I.hud.Say(self, line, seconds);
        }

        void Rise()
        {
            if (sitting) { self.StandUp(); sitting = false; }
            if (seatTaken != null) { takenSeats.Remove(seatTaken); seatTaken = null; }
            if (asleep) { asleep = false; }
        }

        public override void Tick(float dt)
        {
            if (asleep) { self.moveInput = Vector3.zero; return; }
            Human player = Game.I.player;
            if (Time.time >= checkAt)
            {
                checkAt = Time.time + 0.3f;
                // a gun pointed this way
                if (player != null && !player.dead && mood != Mood.Fight && Game.I.ctrl.aimedActor == self && player.aiming && Vector3.Distance(player.Pos, self.Pos) < 16f)
                {
                    if (mood != Mood.HandsUp)
                    {
                        Rise();
                        bool wasReporting = mood == Mood.Report;
                        mood = Mood.HandsUp;
                        mover.Stop();
                        Say(Barks.Scared());
                        if (wasReporting) Game.I.honor.Add(-1f);
                        Game.I.law.Threatened(self);
                    }
                    moodUntil = Time.time + 2.5f;
                }
            }

            self.walk = false; self.sprint = false; self.handsUp = false; self.fistsUp = false; self.faceLocked = false;
            switch (mood)
            {
                case Mood.HandsUp:
                    self.handsUp = true;
                    self.moveInput = Vector3.zero;
                    if (player != null) { self.faceLocked = true; self.faceYaw = U.Yaw(player.Pos - self.Pos); }
                    if (Time.time > moodUntil) Run(player != null ? player.Pos : self.Pos, 12f);
                    return;
                case Mood.Flee:
                    self.sprint = true;
                    if (mover.Arrived && threat != null) mover.Go(self.Pos + U.FlatDir(self.Pos - threat.Pos) * 25f, 1.5f);
                    mover.Tick(dt);
                    if (Time.time > moodUntil) Calm();
                    return;
                case Mood.Report:
                    self.sprint = true;
                    mover.Go(reportTo, 2.5f);
                    mover.Tick(dt);
                    if (mover.Arrived || Time.time > moodUntil)
                    {
                        Calm();
                        reported?.Invoke(this);
                    }
                    return;
                case Mood.Fight:
                {
                    if (threat == null || threat.dead || Time.time > moodUntil || self.health < self.maxHealth * 0.3f || Vector3.Distance(threat.Pos, self.Pos) > 14f)
                    {
                        if (threat != null && self.health < self.maxHealth * 0.3f) Run(threat.Pos, 10f); else Calm();
                        return;
                    }
                    float d = U.FlatDist(threat.Pos, self.Pos);
                    self.fistsUp = true;
                    self.faceLocked = true;
                    self.faceYaw = U.Yaw(threat.Pos - self.Pos);
                    self.moveInput = d > 1.25f ? U.FlatDir(threat.Pos - self.Pos) : Vector3.zero;
                    if (d < 1.5f && self.CanFire && UnityEngine.Random.value < 1.6f * dt) self.Strike();
                    return;
                }
            }

            // ---- calm: the routine
            self.looking = player != null && !sitting && Vector3.Distance(player.Pos, self.Pos) < 4.5f;
            if (self.looking) self.lookPoint = player.HeadPos;
            self.walk = true;
            switch (routine)
            {
                case Routine.Keeper: KeeperTick(dt); break;
                case Routine.Static: self.moveInput = Vector3.zero; break;
                default: DriftTick(dt); break;
            }
        }

        void KeeperTick(float dt)
        {
            if (Open)
            {
                headingHome = false;
                Vector3 post = R.P(postKey);
                if (U.FlatDist(self.Pos, post) > 0.7f) { mover.Go(post, 0.4f); mover.Tick(dt); }
                else { mover.Stop(); self.faceLocked = true; self.faceYaw = R.Yaw(postKey); }
            }
            else GoHome(dt);
        }

        void GoHome(float dt)
        {
            if (homeKey == null) { self.moveInput = Vector3.zero; return; }
            if (!headingHome) { headingHome = true; mover.Go(R.P(homeKey), 1.2f); }
            mover.Tick(dt);
            if (mover.Arrived) Sleep();
        }

        void Sleep()
        {
            Rise();
            asleep = true;
            headingHome = false;
            self.moveInput = Vector3.zero;
            self.gameObject.SetActive(false);
        }

        public void Wake()
        {
            asleep = false;
            waitUntil = Time.time + UnityEngine.Random.Range(0f, 20f);
        }

        void DriftTick(float dt)
        {
            float hour = Hour;
            bool night = routine == Routine.Patrol ? false : hour >= 23f || hour < 5.8f;
            if (night) { GoHome(dt); return; }
            headingHome = false;
            if (sitting)
            {
                self.moveInput = Vector3.zero;
                if (Time.time > waitUntil) Rise();
                return;
            }
            if (!mover.Arrived) { mover.Tick(dt); return; }
            self.moveInput = Vector3.zero;
            if (travelling)
            {
                // the pause starts on arrival, and the person turns the way the spot faces
                travelling = false;
                waitUntil = Time.time + pause;
            }
            if (arrivedKey != null && seatTaken == null) { self.faceLocked = true; self.faceYaw = R.Yaw(arrivedKey); }
            if (Time.time < waitUntil) return;

            // arrived at a seat we were heading for?
            if (seatTaken != null && U.FlatDist(self.Pos, R.P(seatTaken)) < 1.2f)
            {
                bool ground = seatTaken.StartsWith("camp_seat");
                Vector3 sp = R.P(seatTaken);
                self.SitAt(sp, R.Yaw(seatTaken), false, ground ? 0.36f : 0.47f);
                sitting = true;
                waitUntil = Time.time + UnityEngine.Random.Range(25f, 80f);
                return;
            }
            bool evening = hour >= 18f;
            if (seats.Count > 0 && UnityEngine.Random.value < (evening ? 0.6f : 0.25f))
            {
                string s = seats[UnityEngine.Random.Range(0, seats.Count)];
                if (!takenSeats.Contains(s))
                {
                    seatTaken = s;
                    takenSeats.Add(s);
                    Vector3 sp = R.P(s);
                    mover.Go(sp + Quaternion.Euler(0f, R.Yaw(s), 0f) * new Vector3(0f, 0f, 0.5f), 0.5f);
                    travelling = true;
                    pause = 0.4f;
                    arrivedKey = null;
                    return;
                }
            }
            if (spots.Count == 0) return;
            string key = spots[UnityEngine.Random.Range(0, spots.Count)];
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 1.2f;
            mover.Go(R.P(key) + new Vector3(jitter.x, 0f, jitter.y), 0.7f);
            travelling = true;
            arrivedKey = key;
            pause = UnityEngine.Random.Range(6f, routine == Routine.Patrol ? 12f : 28f);
        }
    }
}
