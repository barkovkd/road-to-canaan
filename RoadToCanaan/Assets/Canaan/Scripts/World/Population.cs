using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Puts the people of a region in place and looks after them (night sleep, panic at gunfire).</summary>
    public static class Population
    {
        public static readonly List<Townsman> people = new List<Townsman>();
        public static readonly Dictionary<string, Human> named = new Dictionary<string, Human>();
        static float checkAt;

        public static void Reset() { people.Clear(); named.Clear(); checkAt = 0f; }

        static Human Person(Region r, string name, HumanSpec spec, string at, Faction f, Routine routine, Action<Townsman> cfg = null)
        {
            Human h = Human.Create(spec, r.P(at), r.Yaw(at), f, name);
            var t = new Townsman(h, routine);
            cfg?.Invoke(t);
            h.brain = t;
            people.Add(t);
            named[name] = h;
            return h;
        }

        public static void SpawnDryFord(Region r)
        {
            Game g = Game.I;
            Reset();
            Townsman.ResetSeats();
            Social.Reset();
            Shops.Reset();
            var rng = new System.Random(31);

            // ---- people behind counters
            var keepers = new Dictionary<string, Human>();
            void Keeper(string shop, string name, HumanSpec spec, float open, float close, string home)
            {
                keepers[shop] = Person(r, name, spec, shop + "_keeper", Faction.Civilian, Routine.Keeper, t => { t.postKey = shop + "_keeper"; t.openHour = open; t.closeHour = close; t.homeKey = home; });
            }
            Keeper("saloon", "Мод Келлер", Cast.Maud(), 9f, 2f, "hotel_door");
            Keeper("store", "Гас Пибоди", Cast.Peabody(), 7.5f, 20f, "house_n1_door");
            Keeper("gunsmith", "Отто Шрам", Cast.Schramm(), 8f, 19.5f, "house_n2_door");
            Keeper("doctor", "Доктор Уитфилд", Cast.Whitfield(), 8f, 21f, "hotel_door");
            Keeper("stable", "Нед Харпер", Cast.Harper(), 6f, 21f, "house_s1_door");
            Keeper("station", "Сайлас Трой", Cast.Troy(), 6f, 22f, "house_s2_door");
            Keeper("butcher", "Мясник Йорген", Cast.Butcher(), 7f, 19f, "house_s1_door");
            Keeper("smithy", "Кузнец Абнер", Cast.Smith(), 7f, 19f, "house_s2_door");
            Keeper("office", "Мистер Прюитт", Cast.Pruitt(), 9f, 17f, "hotel_door");
            Shops.Setup(r, keepers);

            // ---- the law
            g.law.office = r.P("sheriff_door");
            Human sheriff = Spawner.Lawman(Cast.Sheriff(), r.P("sheriff_keeper"), r.Yaw("sheriff_keeper"), "Шериф Бриско", 0.7f, true);
            g.law.AddLawman(sheriff, new Townsman(sheriff, Routine.Keeper) { postKey = "sheriff_keeper", openHour = 0f, closeHour = 24f });
            named[sheriff.displayName] = sheriff;
            for (int i = 0; i < 2; i++)
            {
                Human d = Spawner.Deputy(r.P("town_spot" + (2 + i * 5)), 0f, i);
                var patrol = new Townsman(d, Routine.Patrol);
                for (int k = 0; k < 10; k++) patrol.spots.Add("town_spot" + k);
                patrol.spots.Add("sheriff_door"); patrol.spots.Add("station_door"); patrol.spots.Add("saloon_door");
                g.law.AddLawman(d, patrol);
            }
            g.law.Hook();

            // ---- townsfolk
            string[] homes = { "house_n1_door", "house_n2_door", "house_s1_door", "house_s2_door", "hotel_door", "church_door" };
            for (int i = 0; i < 14; i++)
            {
                bool female = i % 3 == 1;
                string start = "town_spot" + (i % 10);
                Person(r, female ? "Горожанка" : "Горожанин", HumanSpec.Random(rng, female), start, Faction.Civilian, Routine.Loiter, t =>
                {
                    for (int k = 0; k < 10; k++) t.spots.Add("town_spot" + k);
                    t.spots.AddRange(new[] { "store_door", "saloon_door", "station_door", "platform", "doctor_door", "gunsmith_door" });
                    for (int k = 0; k < 4; k++) t.seats.Add("town_seat" + k);
                    t.seats.Add("platform_seat");
                    if (!female) for (int k = 0; k < 9; k++) t.seats.Add("saloon_seat" + k);
                    t.homeKey = homes[rng.Next(homes.Length)];
                });
            }

            // ---- ranch and homesteads
            Person(r, "Олден Прайс", new HumanSpec { height = 1.8f, build = 1.1f, belly = 0.4f, hair = Pal.Hair[5], beard = BeardKind.Moustache, shirt = Pal.ClothWhite, pants = Pal.ClothBrown, vest = true, vestColor = Pal.LeatherTan, hat = HatKind.Cowboy, hatColor = Pal.ClothCream },
                   "ranch_house_door", Faction.Civilian, Routine.Loiter, t => { t.spots.AddRange(new[] { "ranch_house_door", "ranch_yard" }); t.homeKey = "ranch_house_door"; });
            Person(r, "Хосефа Ривас", Cast.Josefa(), "ranch_corral", Faction.Civilian, Routine.Loiter, t => { t.spots.AddRange(new[] { "ranch_hand0", "ranch_hand1", "ranch_hand2", "ranch_yard", "ranch_barn_door" }); t.homeKey = "ranch_barn_inside"; });
            for (int i = 0; i < 2; i++)
                Person(r, "Работник ранчо", HumanSpec.Random(rng, false), "ranch_hand" + i, Faction.Civilian, Routine.Loiter, t => { t.spots.AddRange(new[] { "ranch_hand0", "ranch_hand1", "ranch_hand2", "ranch_yard", "ranch_hitch" }); t.homeKey = "ranch_barn_inside"; });
            Person(r, "Ларс Олсон", HumanSpec.Random(rng, false), "olson_yard", Faction.Civilian, Routine.Loiter, t => { t.spots.AddRange(new[] { "olson_yard", "olson_door" }); t.homeKey = "olson_door"; });
            Person(r, "Ингрид Олсон", HumanSpec.Random(rng, true), "olson_door", Faction.Civilian, Routine.Loiter, t => { t.spots.AddRange(new[] { "olson_yard", "olson_door" }); t.homeKey = "olson_door"; });
            Person(r, "Фермер", HumanSpec.Random(rng, false), "millfarm_yard", Faction.Civilian, Routine.Loiter, t => { t.spots.AddRange(new[] { "millfarm_yard", "millfarm_door" }); t.homeKey = "millfarm_door"; });

            // ---- the fellowship «Новый Ханаан»
            void Campfolk(string name, HumanSpec spec, string post, string home, bool atPost)
            {
                Person(r, name, spec, post, Faction.Camp, atPost ? Routine.Keeper : Routine.Camp, t =>
                {
                    t.reportsCrime = false;
                    t.homeKey = home;
                    if (atPost) { t.postKey = post; t.openHour = 6f; t.closeHour = 21.5f; }
                    else
                    {
                        for (int k = 0; k < 8; k++) t.spots.Add("camp_spot" + k);
                        t.spots.AddRange(new[] { "camp_cookfront", "camp_water", "camp_gate" });
                        for (int k = 0; k < 4; k++) t.seats.Add("camp_seat" + k);
                    }
                });
            }
            Campfolk("Марта Линдквист", Cast.Marta(), "camp_ledger", "camp_wagon0", true);
            Campfolk("Бак Тиллер", Cast.Buck(), "camp_cook", "camp_wagon1", true);
            Campfolk("Ефим Кузнецов", Cast.Yefim(), "camp_smith", "camp_wagon2", true);
            Campfolk("Док Финч", Cast.Finch(), "camp_doc", "camp_wagon3", true);
            Campfolk("Тесс", Cast.Tess(), "camp_spot1", "camp_wagon0", false);
            Campfolk("Исайя Фримен", Cast.Isaiah(), "camp_spot2", "camp_wagon4", false);
            Campfolk("Пастор Холт", Cast.Holt(), "camp_spot4", "camp_wagon5", false);
            Campfolk("Эйб Коулман", Cast.Abe(), "camp_spot6", "camp_wagon6", false);
            Campfolk("Джуд Харкер", Cast.Jude(), "camp_gate", "camp_wagon7", false);
            for (int i = 0; i < 8; i++)
                Campfolk(i % 2 == 0 ? "Переселенец" : "Переселенка", HumanSpec.Random(rng, i % 2 == 1), "camp_spot" + i, "camp_wagon" + (i % 10), false);

            // ---- horses at the rails
            void Tied(string key, int breed, bool other, float side = 0f)
            {
                Vector3 p = r.P(key) + Quaternion.Euler(0f, r.Yaw(key), 0f) * new Vector3(side, 0f, 0f);
                Horse h = Horse.Create(HorseDB.All[breed], World.OnGround(p), r.Yaw(key), true, "", (breed * 3 + (int)(side + 9f)) % 7);
                h.mode = HorseMode.Hitched;
                h.othersProperty = other;
            }
            Tied("camp_hitch", 0, true, -1.8f);
            Tied("camp_hitch", 2, true, 0f);
            Tied("camp_hitch", 1, true, 1.8f);
            Tied("saloon_hitch", 3, true, 0.6f);
            Tied("sheriff_hitch", 2, true, -0.5f);
            Tied("ranch_hitch", 3, true, 0f);
            for (int i = 0; i < 3; i++)
            {
                Horse h = Horse.Create(HorseDB.All[(i * 2 + 1) % HorseDB.All.Count], World.OnGround(r.P("stable_corral") + new Vector3(i * 4f - 4f, 0f, i * 2f - 2f)), i * 110f, false, "", i * 2 + 1);
                h.mode = HorseMode.Wander;
                h.home = r.P("stable_corral");
                h.wanderRadius = 6f;
                h.othersProperty = true;
            }
            World.Noise += OnNoise;
        }

        static void OnNoise(Vector3 pos, float radius, Actor source, bool gunshot)
        {
            if (!gunshot) return;
            float reach = Mathf.Min(55f, radius * 0.3f);
            foreach (Townsman t in people)
            {
                if (t.asleep || t.self == null || t.self.dead || t.mood != Mood.Calm) continue;
                if (Vector3.Distance(t.self.Pos, pos) > reach) continue;
                if (source != null && source.faction == Faction.Camp && t.self.faction == Faction.Camp) continue;
                t.threat = source;
                t.Run(pos, UnityEngine.Random.Range(9f, 16f));
            }
        }

        public static void Tick(float dt)
        {
            if (Time.time < checkAt) return;
            checkAt = Time.time + 2f;
            float hour = Game.I.clock.hour;
            foreach (Townsman t in people)
            {
                if (!t.asleep || t.self == null || t.self.dead) continue;
                bool up = t.routine == Routine.Keeper
                    ? (t.openHour < t.closeHour ? hour >= t.openHour - 0.4f && hour < t.closeHour : hour >= t.openHour - 0.4f || hour < t.closeHour)
                    : hour >= 5.8f && hour < 23f;
                if (!up) continue;
                t.self.gameObject.SetActive(true);
                t.Wake();
            }
        }
    }
}
