using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// Talking to people: hold the aim button with empty hands to address someone, then greet or insult;
    /// with a gun on them the offer becomes robbery.
    /// </summary>
    public static class Social
    {
        public static Human focus;
        static readonly Dictionary<Human, float> robbedAt = new Dictionary<Human, float>();
        static readonly List<(float at, Action act)> later = new List<(float, Action)>();

        public static void Reset() { focus = null; robbedAt.Clear(); later.Clear(); }

        public static void Tick(float dt)
        {
            for (int i = later.Count - 1; i >= 0; i--)
                if (Time.time >= later[i].at) { Action a = later[i].act; later.RemoveAt(i); a(); }
        }

        static void After(float seconds, Action act) { later.Add((Time.time + seconds, act)); }

        public static void Prompts(Human p, List<Prompt> prompts)
        {
            focus = null;
            if (!GameInput.Held(Btn.Aim)) return;
            Game g = Game.I;
            Vector3 fwd = g.rig.Forward;
            Vector3 from = g.cam.transform.position;
            Human best = null;
            float bestScore = 28f;
            foreach (Human h in World.humans)
            {
                if (h == p || h.dead || h.knockedOut || !h.isActiveAndEnabled) continue;
                if (h.faction == Faction.Outlaw) continue;
                float d = Vector3.Distance(h.Pos, p.Pos);
                if (d > 7f) continue;
                float ang = Vector3.Angle(fwd, h.Chest - from);
                if (ang < bestScore) { bestScore = ang; best = h; }
            }
            if (best == null) return;
            bool gun = p.weapon != null && !p.weapon.def.Melee;
            Human target = best;
            if (!gun)
            {
                if (target.brain is Townsman tm && tm.mood == Mood.Fight) return;     // fists are for this one
                focus = target;
                g.hud.Centre(target.displayName, 0.25f);
                prompts.Add(new Prompt { btn = Btn.Interact, text = "Поздороваться", action = () => Greet(p, target), priority = 30f });
                prompts.Add(new Prompt { btn = Btn.Melee, text = "Нагрубить", action = () => Insult(p, target), priority = 30f });
            }
            else if (target.faction == Faction.Civilian && g.ctrl.aimedActor == target)
            {
                prompts.Add(new Prompt { btn = Btn.Interact, text = "Ограбить", action = () => Rob(p, target), priority = 30f });
            }
        }

        static void Greet(Human p, Human h)
        {
            Game g = Game.I;
            g.hud.Say(p, Barks.PlayerGreet(), 2.2f);
            Townsman t = h.brain as Townsman;
            if (t == null && h.brain is Gunman gm && g.law.patrols.TryGetValue(h, out Townsman pt)) t = pt;
            bool fresh = t == null || Time.time - t.lastGreeted > 25f;
            if (t != null) t.lastGreeted = Time.time;
            After(0.9f, () =>
            {
                if (h == null || h.dead) return;
                string line = h.faction == Faction.Camp ? Barks.Camp(h.displayName)
                            : h.faction == Faction.Law ? Barks.LawIdle()
                            : t != null && t.routine == Routine.Keeper && t.AtPost ? Barks.KeeperHello()
                            : Barks.Greet(h.spec.female, g.honor.value);
                g.hud.Say(h, line, 3.5f);
            });
            if (fresh) g.honor.Greeted(g.clock.day);
        }

        static void Insult(Human p, Human h)
        {
            Game g = Game.I;
            g.hud.Say(p, Barks.PlayerInsult(), 2.2f);
            g.honor.Add(-1f, false);
            Townsman t = h.brain as Townsman;
            if (t == null) { After(0.9f, () => { if (h != null && !h.dead) g.hud.Say(h, Barks.LawIdle()); }); return; }
            t.insulted++;
            int n = t.insulted;
            After(0.9f, () =>
            {
                if (h == null || h.dead) return;
                g.hud.Say(h, Barks.Insulted(n), 3f);
                if (n < 2) return;
                if (h.faction == Faction.Camp) { g.honor.Add(-2f); return; }
                // (the mood used to be set here without its time limit, so the man calmed down in the same breath; a seated one never got up)
                if (UnityEngine.Random.value < 0.5f && !h.spec.female) { t.Fight(p); t.insulted = 0; }
                else { t.Run(p.Pos, 10f); g.law.Crime(CrimeKind.Disturbance, h, h.Pos); }
            });
        }

        static void Rob(Human p, Human h)
        {
            Game g = Game.I;
            g.hud.Say(p, Barks.PlayerRob(), 2.2f);
            if (robbedAt.TryGetValue(h, out float at) && Time.time - at < 600f)
            {
                After(0.8f, () => { if (h != null && !h.dead) g.hud.Say(h, "У меня больше ничего нет!"); });
                return;
            }
            robbedAt[h] = Time.time;
            float take = Mathf.Round(UnityEngine.Random.Range(0.4f, 3.6f) * 100f) / 100f;
            After(1.1f, () =>
            {
                if (h == null || h.dead) return;
                g.hud.Say(h, Barks.Robbed());
                g.stats.money += take;
                g.Notify("Отнято " + U.Money(take));
                Sfx.Play2D("coin", 0.6f);
                if (UnityEngine.Random.value < 0.25f) g.sat.Add(U.Pick(new[] { "pocket_watch", "silver_ring", "belt_buckle" }));
                g.law.Crime(CrimeKind.Robbery, h, h.Pos);
                if (h.brain is Townsman t && t.mood != Mood.Report) t.Run(p.Pos, 14f);
            });
        }
    }
}
