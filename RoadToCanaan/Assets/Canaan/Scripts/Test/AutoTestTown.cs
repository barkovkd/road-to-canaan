using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>Loads region 1 and puts the hero and his horse at the station, as a new game does.</summary>
        IEnumerator StartRegionPlay()
        {
            yield return Game.I.LoadRegion("dryford");
            Game g = Game.I;
            g.hud.FadeTo(0f, 0f);
            Region r = g.region;
            g.SpawnPlayer(r.playerStart, r.playerYaw);
            g.SpawnPlayerHorse(r.horseStart, r.horseYaw);
            P.Give("pilgrim"); P.Give("knife");
            g.sat.Add("ammo_revolver", 60);
            g.stats.money = 20f;
            yield return Wait(0.6f);
        }

        /// <summary>Walks the hero along the navigation mesh, the way a player would thread the streets.</summary>
        IEnumerator Nav(Vector3 dest, float tolerance = 1f, float timeout = 60f, bool fast = false)
        {
            var path = new NavMeshPath();
            Vector3[] corners = null;
            int corner = 1, side = 1;
            float end = Time.time + timeout, repath = 0f, stuck = 0f, sideUntil = 0f;
            Vector3 last = P.Pos;
            while (Time.time < end)
            {
                Vector3 pos = P.Pos;
                // caught on a corner (a bench on a boardwalk the path hugs): step aside for a moment, as townsfolk do
                if ((pos - last).magnitude < 0.01f) stuck += Time.deltaTime; else stuck = 0f;
                last = pos;
                if (stuck > 0.7f) { side = -side; sideUntil = Time.time + 0.6f; stuck = 0f; repath = 0f; }
                if (U.FlatDist(pos, dest) <= tolerance) break;
                if (Time.time >= repath)
                {
                    repath = Time.time + 2f;
                    corners = null;
                    if (NavMesh.SamplePosition(pos, out NavMeshHit a, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(dest, out NavMeshHit b, 3f, NavMesh.AllAreas)
                        && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 1) { corners = path.corners; corner = 1; }
                }
                Vector3 aim = dest;
                if (corners != null)
                {
                    while (corner < corners.Length - 1 && U.FlatDist(pos, corners[corner]) < 0.5f) corner++;
                    aim = corners[corner];
                }
                Vector3 go = aim - pos;
                if (Time.time < sideUntil) go = Quaternion.Euler(0f, 75f * side, 0f) * go;
                Stick(go, U.FlatDist(pos, dest) < 2f ? 0.6f : 1f);
                Hold(Btn.Sprint, fast);
                yield return null;
            }
            GameInput.vMove = Vector2.zero;
            Hold(Btn.Sprint, false);
        }

        Human Nearest(Faction f, System.Predicate<Human> also = null)
        {
            Human best = null;
            float bestD = 1e9f;
            foreach (Human h in World.humans)
            {
                if (h == P || h.dead || !h.isActiveAndEnabled || h.faction != f || (also != null && !also(h))) continue;
                float d = Vector3.Distance(h.Pos, P.Pos);
                if (d < bestD) { bestD = d; best = h; }
            }
            return best;
        }

        IEnumerator Town()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            int civ = 0, law = 0, camp = 0;
            foreach (Human h in World.humans) { if (h.faction == Faction.Civilian) civ++; else if (h.faction == Faction.Law) law++; else if (h.faction == Faction.Camp) camp++; }
            Log("people: " + civ + " civilians, " + law + " lawmen, " + camp + " in the camp; horses " + World.horses.Count);
            Check(civ >= 25 && law == 3 && camp >= 15, "the town, the law and the camp are populated");
            Check(P.grounded && Mathf.Abs(P.Pos.y - r.playerStart.y) < 0.4f, "hero stands on the station platform");
            Snap("arrival");

            // ---- shop: walk to the store counter and buy a can of beans
            yield return Nav(r.P("store_counter"), 0.8f, 90f, true);
            Check(U.FlatDist(P.Pos, r.P("store_counter")) < 1.5f, "hero reaches the store counter through the door");
            yield return LookAt(r.P("store_keeper") + Vector3.up * 1.5f, 1.5f);
            yield return Frames(4);
            Snap("store");
            Check(Ctrl.prompts.Exists(p => p.text == "Торговать"), "trade is offered at the counter");
            Tap(Btn.Interact);
            yield return Frames(4);
            Check(g.MenuOpen, "the shop menu opens");
            Snap("store_menu");
            float money = g.stats.money;
            int beans = g.sat.Count("beans");
            Tap(Btn.Accept);
            yield return Frames(4);
            Check(g.sat.Count("beans") == beans + 1 && Mathf.Abs(money - g.stats.money - 0.40f) < 0.011f, "buying beans costs 40 cents");
            Tap(Btn.Back);
            yield return Frames(4);
            Check(!g.MenuOpen, "the shop menu closes");

            // ---- greet and insult someone in the street
            yield return Nav(r.P("store_door"), 1.2f, 30f);
            Human c = Nearest(Faction.Civilian, h => h.brain is Townsman t && t.routine == Routine.Loiter);
            Check(c != null, "there is a townsperson about");
            float until = Time.time + 30f;
            while (Time.time < until && Vector3.Distance(P.Pos, c.Pos) > 3.2f) { Stick(c.Pos - P.Pos); yield return null; }
            GameInput.vMove = Vector2.zero;
            Hold(Btn.Aim, true);
            yield return LookAt(() => c.Chest, 1.5f);
            yield return Frames(4);
            Check(Ctrl.prompts.Exists(p => p.text == "Поздороваться"), "holding aim with empty hands offers a greeting");
            Check(!P.fistsUp, "fists stay down while addressing someone");
            float honor = g.honor.value;
            Tap(Btn.Interact);
            yield return Wait(1.6f);
            Snap("greeting");
            Check(g.honor.value > honor, "a greeting adds a little honour");
            honor = g.honor.value;
            yield return LookAt(() => c.Chest, 1f);
            Tap(Btn.Melee);
            yield return Wait(1.4f);
            yield return LookAt(() => c.Chest, 1f);
            Tap(Btn.Melee);
            yield return Wait(1.6f);
            Hold(Btn.Aim, false);
            var tm = (Townsman)c.brain;
            Check(g.honor.value < honor, "insults cost honour");
            Check(tm.mood == Mood.Fight || tm.mood == Mood.Flee || tm.mood == Mood.Report, "the second insult gets a reaction (" + tm.mood + ")");
            yield return Wait(1f);
            Snap("after_insult");

            // a man who squares up stays squared up: the mood used to lapse the moment it was set, so nobody ever fought over an insult
            Human brawler = Nearest(Faction.Civilian, h => h != c && !h.spec.female && !h.dead && h.brain is Townsman);
            if (brawler != null)
            {
                P.Teleport(brawler.Pos + U.Dir(brawler.yaw) * 2.2f, brawler.yaw + 180f);
                yield return Wait(0.3f);
                var bt = (Townsman)brawler.brain;
                bt.Fight(P);
                yield return Wait(1.2f);
                Check(bt.mood == Mood.Fight && brawler.fistsUp, "a townsman who takes offence puts his fists up and keeps them up (" + bt.mood + ")");
                Snap("fists_up");
                bt.Run(P.Pos, 2f);
            }
        }

        IEnumerator LawTest()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            g.stats.money = 80f;
            P.Teleport(r.P("town_spot4"), 0f);
            // every wound the hero takes goes into the report: who, with what, how much, where he was
            System.Action<Actor, DamageInfo> wound = (a, d) =>
            {
                if (a == P) Log("  hero hurt " + d.amount.ToString("0") + " by " + (d.attacker != null ? d.attacker.displayName : "-") +
                                (d.weapon != null ? " (" + d.weapon.id + ")" : d.melee ? " (fists)" : "") + " hp " + P.health.ToString("0") +
                                (P.Mounted ? " riding" : " on foot") + " at " + P.Pos.ToString("F0"));
            };
            World.Hurt += wound;
            yield return Wait(1f);

            // ---- a shot in the street with people about
            Tap(Btn.Slot2);
            yield return Wait(0.6f);
            Human v = Nearest(Faction.Civilian, h => h.brain is Townsman t && t.routine == Routine.Loiter && World.LineOfSight(P.HeadPos, h.Chest));
            Check(v != null, "a townsperson is in sight");
            yield return ShootAt(v, 12f);
            Check(v.dead, "the townsperson is shot");
            yield return Wait(0.5f);
            Check(g.honor.value <= -10f, "murder costs honour (" + g.honor.value.ToString("0") + ")");
            Check(g.law.state != LawState.None, "the crime was seen (" + g.law.state + ")");
            Snap("crime");
            yield return Until(() => g.law.Wanted, 50f);
            Check(g.law.Wanted && g.law.bounty >= 40f, "the hero becomes wanted with a bounty (" + U.Money(g.law.bounty) + ")");
            Check(g.hud.wantedText.StartsWith("РАЗЫСКИВАЕТСЯ"), "the wanted notice is on screen");
            yield return Wait(2f);
            Snap("wanted");

            // ---- ride out of town until the law loses the trail
            Horse h2 = Ctrl.horse;
            // bring the horse to open ground beside him: World.OnGround finds the top of whatever is there, and the
            // spot next to a man under a porch is the porch roof
            Vector3 stand = World.OnGround(P.Pos + new Vector3(2f, 0f, 1f));
            for (int k = 0; k < 16; k++)
            {
                float a = k * 45f * Mathf.Deg2Rad, rad = k < 8 ? 2.5f : 4f;
                Vector3 c = P.Pos + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                Vector3 top = World.OnGround(c), land = RegionDryFord.OnLand(new Vector2(c.x, c.z));
                if (top.y - land.y < 0.3f && !Physics.CheckSphere(land + Vector3.up * 1.2f, 0.9f, Layers.SolidMask, QueryTriggerInteraction.Ignore)) { stand = land; break; }
            }
            h2.Teleport(stand, 90f);
            yield return Frames(3);
            Log("  horse brought to " + h2.Pos.ToString("F1") + ", hero at " + P.Pos.ToString("F1"));
            yield return Until(() => Ctrl.prompts.Exists(p => p.text.StartsWith("Сесть")), 3f);
            Tap(Btn.Interact);
            yield return Wait(1f);
            Check(P.Mounted, "hero mounts to get away");
            Vector3 away = r.P("hunt_plain");
            float end = Time.time + 150f;
            Hold(Btn.Sprint, true);
            while (Time.time < end && g.law.Wanted && !P.dead)
            {
                Vector3 to = away - h2.Pos;
                Stick(to.magnitude > 30f ? to : new Vector3(1f, 0f, 0.3f));
                yield return null;
            }
            Release();
            World.Hurt -= wound;
            Check(!P.dead, "hero survives the chase (health " + P.health.ToString("0") + ")");
            Check(!g.law.Wanted && g.law.bounty >= 40f, "out of sight the chase ends, the bounty stays");
            Snap("escaped");

            // ---- pay the bounty at the post office
            float bounty = g.law.bounty, money = g.stats.money;
            Check(g.law.PayBounty(), "the bounty can be paid");
            Check(g.law.bounty == 0f && Mathf.Abs(money - g.stats.money - bounty) < 0.01f, "paying clears it and costs the full sum");
        }
    }
}
