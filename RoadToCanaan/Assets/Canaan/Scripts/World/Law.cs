using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public enum CrimeKind { Disturbance, Threat, Assault, Theft, HorseTheft, Robbery, Murder, LawmanKilled, AnimalCruelty }
    public enum LawState { None, Witnessed, Investigating, Wanted }

    /// <summary>
    /// Crime and punishment: a crime needs a witness; the witness runs to the sheriff; the lawmen come to look;
    /// if they find the hero he is wanted and the bounty lands on his head. Leaving them behind ends the chase,
    /// the bounty stays until it is paid or served.
    /// </summary>
    public class Law
    {
        readonly Game game;
        public float bounty, pending;
        public LawState state = LawState.None;
        public readonly List<Human> lawmen = new List<Human>();
        public readonly Dictionary<Human, Townsman> patrols = new Dictionary<Human, Townsman>();
        public Vector3 scene, lastSeen, office;
        public int crimesSeen;
        readonly List<Townsman> witnesses = new List<Townsman>();
        readonly Dictionary<Actor, float> assaultAt = new Dictionary<Actor, float>();
        float stateUntil, seenAt, noiseAt = -99f, recogniseT, reinforceAt, checkAt;
        int reinforcements;
        bool hooked;

        public bool Wanted => state == LawState.Wanted;

        public Law(Game g) { game = g; }

        public void Reset()
        {
            state = LawState.None;
            pending = 0f;
            Factions.playerWanted = false;
            lawmen.Clear(); patrols.Clear(); witnesses.Clear(); assaultAt.Clear();
            reinforcements = 0;
            hooked = false;
        }

        /// <summary>Called once the region and its people exist.</summary>
        public void Hook()
        {
            if (hooked) return;
            hooked = true;
            World.Death += OnDeath;
            World.Hurt += OnHurt;
            World.Noise += OnNoise;
        }

        public void AddLawman(Human h, Townsman patrol)
        {
            lawmen.Add(h);
            patrols[h] = patrol;
            var g = (Gunman)h.brain;
            g.idle = LawmanIdle;
        }

        static float BountyFor(CrimeKind k)
        {
            switch (k)
            {
                case CrimeKind.Disturbance: return 2f;
                case CrimeKind.Threat: return 3f;
                case CrimeKind.Assault: return 5f;
                case CrimeKind.Theft: return 10f;
                case CrimeKind.HorseTheft: return 15f;
                case CrimeKind.Robbery: return 25f;
                case CrimeKind.Murder: return 40f;
                case CrimeKind.LawmanKilled: return 60f;
                default: return 4f;
            }
        }

        static float HonorFor(CrimeKind k)
        {
            switch (k)
            {
                case CrimeKind.Assault: return -3f;
                case CrimeKind.Theft: return -3f;
                case CrimeKind.HorseTheft: return -3f;
                case CrimeKind.Robbery: return -5f;
                case CrimeKind.Murder: return -10f;
                case CrimeKind.LawmanKilled: return -6f;
                case CrimeKind.AnimalCruelty: return -2f;
                case CrimeKind.Threat: return -1f;
                default: return 0f;
            }
        }

        // ================================================================== crimes

        public void Crime(CrimeKind kind, Actor victim, Vector3 pos)
        {
            Human p = game.player;
            if (p == null) return;
            game.honor.Add(HonorFor(kind));
            float amount = BountyFor(kind);
            if (state == LawState.Wanted) { bounty += amount; return; }

            bool lawSaw = false;
            foreach (Human h in World.humans)
            {
                if (h == p || h.dead || h.knockedOut || !h.isActiveAndEnabled) continue;
                if (h.faction != Faction.Civilian && h.faction != Faction.Law) continue;
                float d = Vector3.Distance(h.Pos, p.Pos);
                if (d > 55f) continue;
                if (!(d < 8f || Factions.CanSee(h, p, 55f, 115f))) continue;
                if (h.faction == Faction.Law) { lawSaw = true; continue; }
                if (!(h.brain is Townsman t) || !t.reportsCrime || t.mood == Mood.Report || witnesses.Count >= 3) continue;
                if (h == victim && t.mood == Mood.Fight) continue;
                witnesses.Add(t);
                t.threat = p;
                t.reported = WitnessArrived;
                t.StartReport(NearestLawmanOrOffice(h.Pos));
            }
            if (lawSaw)
            {
                pending += amount;
                BecomeWanted();
                return;
            }
            if (witnesses.Count == 0) return;      // nobody saw it
            crimesSeen++;
            pending += amount;
            scene = pos;
            if (state == LawState.None) { state = LawState.Witnessed; stateUntil = Time.time + 60f; }
        }

        Vector3 NearestLawmanOrOffice(Vector3 from)
        {
            Vector3 best = office;
            float bestD = Vector3.Distance(from, office);
            foreach (Human l in lawmen)
            {
                if (l.dead) continue;
                float d = Vector3.Distance(from, l.Pos);
                if (d < bestD) { bestD = d; best = l.Pos; }
            }
            return best;
        }

        /// <summary>Someone was held at gunpoint.</summary>
        public void Threatened(Human victim)
        {
            if (victim.faction == Faction.Civilian) Crime(CrimeKind.Threat, victim, victim.Pos);
        }

        void WitnessArrived(Townsman t)
        {
            witnesses.Remove(t);
            if (state != LawState.Witnessed) return;
            state = LawState.Investigating;
            stateUntil = Time.time + 55f;
            game.Notify("Свидетель донёс шерифу");
        }

        void OnDeath(Actor victim, DamageInfo d)
        {
            Human p = game.player;
            if (p == null || d.attacker != p || victim == p) return;
            if (victim is Human h)
            {
                if (h.brain is Townsman t) witnesses.Remove(t);
                if (h.faction == Faction.Law) Crime(CrimeKind.LawmanKilled, victim, victim.Pos);
                else if (h.faction == Faction.Civilian || h.faction == Faction.Camp) Crime(CrimeKind.Murder, victim, victim.Pos);
            }
            else if (victim.faction == Faction.Livestock && !(victim is Horse ho && ho.owner == p)) Crime(CrimeKind.AnimalCruelty, victim, victim.Pos);
        }

        void OnHurt(Actor victim, DamageInfo d)
        {
            Human p = game.player;
            if (p == null || d.attacker != p || victim == p || victim.dead) return;
            if (!(victim is Human h) || (h.faction != Faction.Civilian && h.faction != Faction.Law && h.faction != Faction.Camp)) return;
            if (assaultAt.TryGetValue(victim, out float at) && Time.time - at < 4f) return;
            assaultAt[victim] = Time.time;
            Crime(CrimeKind.Assault, victim, victim.Pos);
        }

        void OnNoise(Vector3 pos, float radius, Actor source, bool gunshot)
        {
            if (!gunshot || source != game.player || Time.time - noiseAt < 15f) return;
            if (Vector3.Distance(pos, office) > 190f) return;        // out on the prairie nobody minds
            noiseAt = Time.time;
            Crime(CrimeKind.Disturbance, null, pos);
        }

        void BecomeWanted()
        {
            bool was = state == LawState.Wanted;
            state = LawState.Wanted;
            bounty += pending;
            pending = 0f;
            Factions.playerWanted = true;
            seenAt = Time.time;
            lastSeen = game.player.Pos;
            reinforceAt = Time.time + 30f;
            if (!was)
            {
                game.Notify("Вас разыскивают");
                foreach (Human l in lawmen) if (!l.dead && Vector3.Distance(l.Pos, game.player.Pos) < 30f) { game.hud.Say(l, Barks.LawOrder()); break; }
            }
        }

        // ================================================================== frame

        void LawmanIdle(Gunman g)
        {
            Human l = g.self;
            if (state == LawState.Investigating)
            {
                l.walk = false; l.sprint = true;
                g.mover.Go(scene, 5f);
                g.mover.Tick(Time.deltaTime);
                return;
            }
            if (patrols.TryGetValue(l, out Townsman patrol)) patrol.Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            Human p = game.player;
            if (p == null) return;
            if (Time.time >= checkAt)
            {
                checkAt = Time.time + 0.25f;
                bool seen = false;
                float nearest = 999f;
                foreach (Human l in lawmen)
                {
                    if (l.dead || l.knockedOut || !l.isActiveAndEnabled) continue;
                    float d = Vector3.Distance(l.Pos, p.Pos);
                    nearest = Mathf.Min(nearest, d);
                    if (d < 60f && Factions.CanSee(l, p, 60f, 120f)) seen = true;
                }
                switch (state)
                {
                    case LawState.Witnessed:
                        witnesses.RemoveAll(w => w.self.dead || w.self.knockedOut || w.mood != Mood.Report);
                        if (witnesses.Count == 0 || Time.time > stateUntil) { state = LawState.None; pending = 0f; }
                        break;
                    case LawState.Investigating:
                        if (seen && !p.dead && Vector3.Distance(p.Pos, scene) < 70f) BecomeWanted();
                        else if (Time.time > stateUntil) { state = LawState.None; pending = 0f; game.Notify("Законники никого не нашли"); }
                        break;
                    case LawState.Wanted:
                        if (seen) { seenAt = Time.time; lastSeen = p.Pos; }
                        if (Time.time - seenAt > 25f && Vector3.Distance(p.Pos, lastSeen) > 60f)
                        {
                            state = LawState.None;
                            Factions.playerWanted = false;
                            game.Notify("Вы ушли от погони. Награда за голову: " + U.Money(bounty));
                        }
                        else if (Time.time > reinforceAt && reinforcements < 4 && AliveLawmen() < 4)
                        {
                            reinforceAt = Time.time + 35f;
                            reinforcements++;
                            Vector3 from = Vector3.Distance(p.Pos, game.region.P("town_west")) > Vector3.Distance(p.Pos, game.region.P("town_east")) ? game.region.P("town_west") : game.region.P("town_east");
                            Human d = Spawner.Deputy(World.OnGround(from + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f))), 0f, reinforcements);
                            var patrol = new Townsman(d, Routine.Patrol) { spots = new List<string> { "town_spot2", "town_spot5", "sheriff_door" } };
                            AddLawman(d, patrol);
                        }
                        break;
                    default:
                        // a known face in town
                        if (bounty > 0f && seen && nearest < 22f && !p.dead)
                        {
                            recogniseT += 0.25f;
                            if (recogniseT > 4f)
                            {
                                recogniseT = 0f;
                                BecomeWanted();
                                foreach (Human l in lawmen) if (!l.dead) { game.hud.Say(l, Barks.LawRecognise()); break; }
                            }
                        }
                        else recogniseT = Mathf.Max(0f, recogniseT - 0.1f);
                        break;
                }
            }
            switch (state)
            {
                case LawState.Witnessed: game.hud.wantedText = "СВИДЕТЕЛЬ"; game.hud.wantedColor = UI.Stamina; break;
                case LawState.Investigating: game.hud.wantedText = "РАССЛЕДОВАНИЕ"; game.hud.wantedColor = UI.Stamina; break;
                case LawState.Wanted: game.hud.wantedText = "РАЗЫСКИВАЕТСЯ · " + U.Money(bounty); game.hud.wantedColor = UI.Accent; break;
                default: game.hud.wantedText = ""; break;
            }
        }

        int AliveLawmen()
        {
            int n = 0;
            foreach (Human l in lawmen) if (!l.dead) n++;
            return n;
        }

        /// <summary>Offers surrender when a lawman is close and the hands are empty.</summary>
        public void Prompts(Human p, List<Prompt> prompts)
        {
            if (state != LawState.Wanted || p.weapon != null || p.Mounted) return;
            foreach (Human l in lawmen)
            {
                if (l.dead || Vector3.Distance(l.Pos, p.Pos) > 10f) continue;
                prompts.Add(new Prompt { btn = Btn.Interact, text = "Сдаться", hold = 0.8f, action = Arrest, priority = 20f });
                return;
            }
        }

        void Arrest()
        {
            float paid = Mathf.Min(game.stats.money, bounty);
            game.stats.money -= paid;
            bounty = 0f;
            pending = 0f;
            state = LawState.None;
            Factions.playerWanted = false;
            game.clock.SkipTo(Mathf.Repeat(game.clock.hour + 12f, 24f));
            game.player.Teleport(game.region.P("sheriff_door"), 0f);
            game.rig.SnapBehind(0f);
            game.hud.FadeTo(1f, 0f);
            game.hud.FadeTo(0f, 2f);
            game.Notify(paid > 0f ? "Вы отсидели в камере. В счёт награды списано " + U.Money(paid) : "Вы отсидели в камере");
            foreach (Human l in lawmen) if (l.brain is Gunman g) g.target = null;
        }

        public bool PayBounty()
        {
            if (bounty <= 0f || state == LawState.Wanted || game.stats.money < bounty) return false;
            game.stats.money -= bounty;
            bounty = 0f;
            return true;
        }

        public void OnPlayerDown()
        {
            state = LawState.None;
            pending = 0f;
            Factions.playerWanted = false;
            foreach (Human l in lawmen) if (l.brain is Gunman g) g.target = null;
        }
    }
}
