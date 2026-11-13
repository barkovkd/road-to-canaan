using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        Animal NearestAnimal(string id, bool alive = true)
        {
            Animal best = null;
            float bestD = 1e9f;
            foreach (Animal a in Wildlife.Animals())
            {
                if (a.def.id != id || a.dead == alive) continue;
                float d = Vector3.Distance(a.Pos, P.Pos);
                if (d < bestD) { bestD = d; best = a; }
            }
            return best;
        }

        IEnumerator HoldFor(Btn b, float realSeconds)
        {
            Hold(b, true);
            yield return WaitReal(realSeconds);
            Hold(b, false);
            yield return Frames(2);
        }

        IEnumerator Hunt()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            P.Give("drover");
            g.sat.Add("ammo_rifle", 40);
            int total = 0;
            foreach (Animal a in Wildlife.Animals()) total++;
            Log("animals in the region: " + total);
            Check(total >= 50, "herds, packs and livestock are spawned");

            // ---- stalk a pronghorn: crouched, 40 m off, carbine to the head
            P.Teleport(r.P("hunt_plain") + new Vector3(-70f, 0f, -40f), 60f);
            Ctrl.horse.Teleport(World.OnGround(P.Pos + new Vector3(-6f, 0f, -4f)), 60f);
            yield return Wait(0.5f);
            Animal prey = NearestAnimal("pronghorn");
            Check(prey != null, "a pronghorn herd grazes on the plain");
            Vector3 stand = prey.Pos + U.FlatDir(P.Pos - prey.Pos) * 46f;
            P.Teleport(World.OnGround(stand), U.Yaw(prey.Pos - stand));
            Rig.SnapBehind(P.yaw);
            Tap(Btn.Crouch);
            Tap(Btn.Slot3);
            yield return Wait(0.8f);
            Check(prey.mood != AnimalMood.Flee, "a crouching hunter 46 m away does not scare the herd");
            System.Func<Vector3> heart = () => prey.rig.B(QB.Chest).TransformPoint(new Vector3(0f, -0.2f, 0.02f) * prey.rig.k);
            Hold(Btn.Aim, true);
            yield return LookAt(heart, 2f);
            yield return Wait(0.25f);
            Snap("stalk");
            int stars = prey.stars;
            float until = Time.time + 6f;
            while (Time.time < until && !prey.dead)
            {
                yield return LookAt(heart, 0.6f);
                if (P.CanFire) Tap(Btn.Fire);
                yield return Frames(2);
            }
            Hold(Btn.Aim, false);
            Check(prey.dead, "the pronghorn falls");
            Check(prey.peltQuality >= Mathf.Max(1, stars - 1), "right rifle and a vital hit keep the pelt (" + prey.peltQuality + " of " + stars + " stars)");
            yield return Wait(0.6f);
            int fled = 0;
            foreach (Animal a in Wildlife.Animals()) if (!a.dead && a.def.id == "pronghorn" && a.mood == AnimalMood.Flee) fled++;
            Check(fled >= 2, "the shot scatters the rest of the herd (" + fled + " fleeing)");
            Tap(Btn.Crouch);

            // ---- load the carcass on the horse
            Ctrl.horse.Teleport(World.OnGround(prey.Pos + new Vector3(3f, 0f, 2f)), 0f);
            yield return GoTo(prey.Pos, 1.6f, 25f, true);
            yield return Frames(4);
            Check(Ctrl.prompts.Exists(p => p.text == "Освежевать"), "skinning is offered at the carcass");
            Check(Ctrl.prompts.Exists(p => p.text.StartsWith("Погрузить")), "loading on the horse is offered");
            Snap("carcass");
            yield return HoldFor(Btn.Melee, 1.6f);
            Check(Ctrl.horse.cargo == "pronghorn", "the carcass rides behind the saddle");
            yield return Wait(0.3f);
            Snap("carcass_on_horse");

            // ---- a second animal, skinned on the spot
            Animal second = NearestAnimal("pronghorn");
            if (second != null)
            {
                second.Damage(new DamageInfo { amount = 999f, attacker = P, part = BodyPart.Heart, weapon = WeaponDB.Get("drover"), dir = Vector3.forward });
                P.Teleport(World.OnGround(second.Pos + new Vector3(1.4f, 0f, 0f)), 270f);
                yield return Wait(0.8f);
                int meat = g.sat.Count("meat_game");
                yield return HoldFor(Btn.Interact, 2.1f);
                Check(second.skinned && g.sat.Count("meat_game") == meat + 2, "skinning gives meat");
                Check(g.sat.Count(ItemDB.PeltId("pronghorn", second.peltQuality)) == 1, "and a pelt of the earned quality");
            }

            // ---- herbs
            P.Teleport(World.OnGround(r.P("hunt_plain")), 0f);
            yield return Wait(0.3f);
            Interactable herb = null;
            float best = 1e9f;
            foreach (Interactable it in Interactable.All)
            {
                if (!it.text.StartsWith("Сорвать")) continue;
                float d = Vector3.Distance(it.position(), P.Pos);
                if (d < best) { best = d; herb = it; }
            }
            Check(herb != null, "herbs grow in the region");
            if (herb != null)
            {
                Vector3 hp = herb.position();
                P.Teleport(hp + new Vector3(0.9f, 0f, 0f), 270f);
                Rig.SnapBehind(270f);
                yield return Wait(0.3f);
                int n = g.sat.Count("herb_sage") + g.sat.Count("herb_yarrow") + g.sat.Count("herb_onion");
                Tap(Btn.Interact);
                yield return Frames(4);
                Check(g.sat.Count("herb_sage") + g.sat.Count("herb_yarrow") + g.sat.Count("herb_onion") == n + 1, "a herb can be picked");
            }

            // ---- sell the carcass to the butcher
            float money = g.stats.money;
            Shop butcher = Shops.All.Find(s => s.id == "butcher");
            Ctrl.horse.Teleport(World.OnGround(r.P("butcher_door") + new Vector3(3f, 0f, 3f)), 0f);
            P.Teleport(r.P("butcher_counter"), r.Yaw("butcher_counter"));
            yield return Wait(0.5f);
            Check(Ctrl.prompts.Exists(p => p.text == "Продать добычу"), "the butcher offers to buy");
            Tap(Btn.Interact);
            yield return Frames(4);
            Snap("butcher_menu");
            // rows: [Товары] jerky / [С крупа] carcass ...
            Tap(Btn.Down); yield return Frames(2);
            Tap(Btn.Accept); yield return Frames(4);
            Check(Ctrl.horse.cargo == null && g.stats.money > money + 2f, "the carcass sells for its pelt and meat (" + U.Money(g.stats.money - money) + ")");
            Tap(Btn.Back); yield return Frames(3);
        }

        IEnumerator CampTest()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            Check(g.camp.Exists, "the camp exists");
            P.Teleport(r.P("camp_gate"), r.Yaw("camp_gate"));
            Rig.SnapBehind(r.Yaw("camp_gate"));
            yield return Wait(1f);
            Snap("camp");

            // ---- ledger: give five dollars
            P.Teleport(r.P("camp_ledgerfront"), r.Yaw("camp_ledgerfront"));
            Rig.SnapBehind(P.yaw);
            yield return Wait(0.5f);
            Check(Ctrl.prompts.Exists(p => p.text == "Книга учёта"), "Marta keeps the ledger at her table");
            Tap(Btn.Interact); yield return Frames(4);
            Snap("ledger");
            float honor = g.honor.value, money = g.stats.money;
            Tap(Btn.Down); yield return Frames(2);          // from «Внести 1 $» to «Внести 5 $»
            Tap(Btn.Accept); yield return Frames(4);
            Check(Mathf.Approximately(g.camp.funds, 5f) && Mathf.Abs(money - g.stats.money - 5f) < 0.01f, "five dollars go into the cash box");
            Check(g.honor.value >= honor + 1f, "a contribution raises honour");
            Tap(Btn.Back); yield return Frames(3);

            // ---- chores
            P.Teleport(r.P("camp_wood"), r.Yaw("camp_wood") + 180f);
            Rig.SnapBehind(P.yaw);
            yield return Wait(0.4f);
            honor = g.honor.value;
            Check(Ctrl.prompts.Exists(p => p.text == "Наколоть дров"), "chopping wood is offered");
            yield return HoldFor(Btn.Interact, 2.7f);
            Check(g.honor.value >= honor + 2f, "a chore raises honour");
            yield return Frames(3);
            Check(!Ctrl.prompts.Exists(p => p.text == "Наколоть дров"), "the chore is done for the day");

            // ---- Buck's stew
            g.stats.healthCore = 30f; g.stats.staminaCore = 30f;
            P.Teleport(r.P("camp_cookfront"), r.Yaw("camp_cookfront"));
            Rig.SnapBehind(P.yaw);
            yield return Wait(0.4f);
            Tap(Btn.Interact); yield return Frames(4);
            Tap(Btn.Accept); yield return Frames(4);         // the cursor starts on the stew
            Check(g.stats.healthCore > 60f && g.stats.staminaCore > 60f, "the stew refills the reserves");
            Snap("kitchen");
            Tap(Btn.Back); yield return Frames(3);

            // ---- cook at the fire
            g.sat.Add("meat_game", 1);
            P.Teleport(r.P("camp_fire") + new Vector3(1.6f, 0f, 0f), 270f);
            Rig.SnapBehind(270f);
            yield return Wait(0.4f);
            Check(Ctrl.prompts.Exists(p => p.text == "Готовить на костре"), "the fire offers cooking");
            Tap(Btn.Interact); yield return Frames(4);
            Tap(Btn.Accept); yield return Frames(4);
            Check(g.sat.Count("grilled_meat") == 1 && g.sat.Count("meat_game") == 0, "raw meat becomes a grilled steak");
            Tap(Btn.Back); yield return Frames(3);

            // ---- sleep until morning
            int day = g.clock.day;
            g.clock.frozen = false;
            P.Teleport(r.P("camp_bed"), 0f);
            yield return Wait(0.4f);
            Tap(Btn.Interact); yield return Frames(4);
            Tap(Btn.Accept);
            yield return WaitReal(3.5f);
            Check(g.clock.day == day + 1 && Mathf.Abs(g.clock.hour - 7f) < 0.3f, "sleep jumps to seven the next morning (day " + g.clock.day + ", " + g.clock.TimeText + ")");
            Check(g.stats.staminaCore >= 85f, "sleep restores the reserves");
            g.clock.frozen = true;

            // ---- evening by the fire
            g.clock.hour = 19.6f;
            yield return Wait(40f);
            int seated = 0;
            foreach (Human h in World.humans) if (h.faction == Faction.Camp && h.pose == BodyKind.SitChair) seated++;
            Log("people sitting by the fire: " + seated);
            Look(r.P("camp_fire") + new Vector3(-7f, 2.6f, -8f), r.P("camp_fire") + Vector3.up * 0.9f, 55f);
            yield return null;
            Snap("camp_evening");
        }

        /// <summary>Real window, real frame pacing: rides through the town and reports the frame rate.</summary>
        IEnumerator Perf()
        {
            yield return StartRegionPlay();
            Game g = Game.I;
            Region r = g.region;
            // -probe <what>: switch one thing off to see what it costs (wear, cloudshadow, props, marks, joinery)
            string probe = Args.Get("-probe");
            if (probe != null)
            {
                Log("probe: without " + probe);
                foreach (string m in new[] { "wagon", "wagon_cover", "barrel", "crate", "sack", "haybale", "hitchrail", "trough", "lamppost", "telegraph_pole", "fence_post",
                                             "fence_post_round", "fence_rail", "fence_pole", "fence_gate", "bench", "chair", "well", "signpost", "windmill_tower", "water_tower",
                                             "yd_outhouse", "yd_woodpile", "yd_chopblock", "yd_washline", "yd_pump", "yd_chute", "yd_tumbleweed", "ab_horno", "bush_sage", "bush_rabbit", "bush_scrub" })
                    if (Canaan.Models.Has(m)) Log("  model " + m + ": " + Canaan.Models.Mesh(m).vertexCount);
                foreach (MeshFilter mf in r.root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.transform.parent != null && (mf.transform.parent.name == "TownProps" || mf.transform.parent.name == "TownWeeds") && mf.sharedMesh != null)
                        Log("  " + mf.transform.parent.name + "/" + mf.name + ": " + mf.sharedMesh.vertexCount + " vertices");
                if (probe == "wear") Shader.SetGlobalFloat("_CanaanWearOff", 1f);
                if (probe == "cloudshadow") Shader.SetGlobalFloat("_CloudShadowOff", 1f);
                foreach (MeshRenderer mr in r.root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Transform t0 = mr.transform;
                    if ((probe == "props" && t0.parent != null && t0.parent.name == "TownProps") || (probe == "marks" && t0.name == "GroundMarks")
                        || (probe == "joinery" && t0.name == "Models" && t0.parent != null && t0.parent.name.StartsWith("B_")))
                        mr.enabled = false;
                }
            }
            yield return Wait(1f);
            float t = 0f, worst = 0f, worstAt = 0f;
            int frames = 0, slow = 0;
            Vector3[] route = { r.P("town_west"), r.P("town_east"), r.P("camp_gate") };
            yield return Until(() => Ctrl.prompts.Exists(p => p.text.StartsWith("Сесть")), 2f);
            P.Teleport(Ctrl.horse.Pos + Ctrl.horse.transform.right * -1.3f, Ctrl.horse.yaw);
            yield return Wait(0.3f);
            Tap(Btn.Interact);
            yield return Wait(1.2f);
            foreach (Vector3 goal in route)
            {
                float end = Time.time + 22f;
                while (Time.time < end && U.FlatDist(Ctrl.horse.Pos, goal) > 6f)
                {
                    Stick(goal - Ctrl.horse.Pos);
                    float dt = Time.unscaledDeltaTime;
                    t += dt; frames++;
                    if (dt > worst) { worst = dt; worstAt = t; }
                    if (dt > 0.05f) { slow++; Log("slow frame " + (dt * 1000f).ToString("0") + " ms at " + t.ToString("0.0") + " s"); }
                    yield return null;
                }
                string file = (++shots).ToString("00") + "_perf.png";
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(outDir, file));
                Log("screen " + file);
            }
            Release();
            float fps = frames / Mathf.Max(0.01f, t);
            Log("frames " + frames + " in " + t.ToString("0.0") + " s: average " + fps.ToString("0") + " fps, worst frame " + (worst * 1000f).ToString("0") + " ms at " + worstAt.ToString("0.0") + " s, " + slow + " over 50 ms, screen " + Screen.width + "x" + Screen.height);
            Check(fps > 30f, "the game holds more than 30 fps riding through the town (" + fps.ToString("0") + ")");
            yield return WaitReal(0.5f);
        }
    }
}
