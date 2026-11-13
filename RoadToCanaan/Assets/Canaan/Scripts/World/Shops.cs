using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public class Shop
    {
        public string id, title;
        public Human keeper;
        public List<string> items = new List<string>(), weapons = new List<string>();
        public float buysAt;                 // share of an item's worth paid for the player's goods; 0 = does not buy
        public HashSet<ItemKind> buys = new HashSet<ItemKind>();
    }

    /// <summary>Shops and services of a town: buying, selling, a drink and a bed, the doctor, the stable, the post.</summary>
    public static class Shops
    {
        public static readonly List<Shop> All = new List<Shop>();

        public static void Reset() { All.Clear(); }

        static Shop Add(Region r, string id, string title, Human keeper, string prompt)
        {
            var s = new Shop { id = id, title = title, keeper = keeper };
            All.Add(s);
            if (!r.points.ContainsKey(id + "_counter")) Debug.LogError("Shops: no counter mark for " + id);
            Vector3 at = r.P(id + "_counter");
            Interactable.Add(prompt, () => at, _ => Open(s), 2.4f,
                () => keeper != null && !keeper.dead && !keeper.knockedOut && keeper.brain is Townsman t && t.AtPost);
            return s;
        }

        public static void Setup(Region r, Dictionary<string, Human> keepers)
        {
            Shop store = Add(r, "store", "Лавка Пибоди", keepers["store"], "Торговать");
            store.items.AddRange(new[] { "beans", "bread", "jerky", "crackers", "apple", "coffee", "tobacco", "whiskey", "tonic_stamina", "gun_oil", "horse_brush", "hay" });
            store.buysAt = 0.6f;
            store.buys.UnionWith(new[] { ItemKind.Valuable, ItemKind.Herb, ItemKind.Provision, ItemKind.Tonic, ItemKind.Kit });

            Shop gun = Add(r, "gunsmith", "Оружейник Шрам", keepers["gunsmith"], "Торговать");
            gun.weapons.AddRange(new[] { "pilgrim", "wasp", "drover", "longshot", "thunder", "magpie", "bow", "knife" });
            gun.items.AddRange(new[] { "ammo_revolver", "ammo_rifle", "ammo_shell", "ammo_22", "ammo_arrow", "gun_oil" });

            Shop doc = Add(r, "doctor", "Доктор Уитфилд", keepers["doctor"], "К доктору");
            doc.items.AddRange(new[] { "tonic_health", "tonic_stamina", "tonic_cool" });
            doc.buysAt = 0.8f;
            doc.buys.Add(ItemKind.Herb);

            Shop saloon = Add(r, "saloon", "Салун «Последний вагон»", keepers["saloon"], "К стойке");

            Shop butcher = Add(r, "butcher", "Мясник", keepers["butcher"], "Продать добычу");
            butcher.items.Add("jerky");
            butcher.buysAt = 1f;
            butcher.buys.UnionWith(new[] { ItemKind.Meat, ItemKind.Pelt });

            Add(r, "stable", "Конюшня Харпера", keepers["stable"], "К конюху");
            Add(r, "station", "Станция и почта", keepers["station"], "К окошку");
            Shop smith = Add(r, "smithy", "Кузница", keepers["smithy"], "К кузнецу");
        }

        static string Price(float p) { return U.Money(p); }

        public static void Open(Shop s)
        {
            Game g = Game.I;
            if (s.keeper != null) g.hud.Say(s.keeper, Barks.KeeperHello());
            g.menus.OpenList(s.title, () => Rows(s), () => { g.menus.Close(); if (s.keeper != null && !s.keeper.dead) g.hud.Say(s.keeper, Barks.KeeperBye()); },
                () => U.Money(g.stats.money));
        }

        static bool Pay(float price)
        {
            Game g = Game.I;
            if (g.stats.money + 0.0001f < price) { Sfx.Play2D("ui_back", 0.5f); return false; }
            g.stats.money = Mathf.Round((g.stats.money - price) * 100f) / 100f;
            Sfx.Play2D("coin", 0.6f);
            return true;
        }

        static List<MenuRow> Rows(Shop s)
        {
            Game g = Game.I;
            PlayerStats st = g.stats;
            float mul = g.honor.PriceMul;
            var rows = new List<MenuRow>();

            // ---- services first
            switch (s.id)
            {
                case "saloon":
                    rows.Add(MenuRow.Header("Стойка"));
                    rows.Add(new MenuRow("Стопка виски", () => { if (Pay(0.25f * mul)) { st.Feed(10f, 0f, -8f, g.player); g.Notify("Тепло пошло по жилам"); } }, Price(0.25f * mul), "Запас здоровья +10, хладнокровия −8.", st.money >= 0.25f * mul));
                    rows.Add(new MenuRow("Кружка пива", () => { if (Pay(0.10f * mul)) st.Feed(0f, 12f, -3f, g.player); }, Price(0.10f * mul), "Запас выносливости +12, хладнокровия −3.", st.money >= 0.10f * mul));
                    rows.Add(new MenuRow("Горячий обед", () => { if (Pay(0.75f * mul)) { st.Feed(55f, 55f, 20f, g.player); g.Notify("Вы сыты"); } }, Price(0.75f * mul), "Рагу, хлеб и кофе. Заметно пополняет все запасы.", st.money >= 0.75f * mul));
                    rows.Add(new MenuRow("Комната на ночь", () => { if (Pay(1.00f * mul)) { g.menus.Close(); g.Rest(7f, "Вы выспались в комнате над салуном"); } }, Price(1.00f * mul), "Сон до семи утра. Восстанавливает запасы, игра сохраняется.", st.money >= 1.00f * mul));
                    break;
                case "doctor":
                    rows.Add(MenuRow.Header("Приём"));
                    rows.Add(new MenuRow("Осмотр и перевязка", () => { if (Pay(1.50f * mul)) { st.healthCore = 100f; g.player.maxHealth = st.MaxHealthBar + 100f * PlayerStats.CoreShare; g.player.health = g.player.maxHealth; g.Notify("Доктор подлатал вас"); } },
                        Price(1.50f * mul), "Здоровье и его запас — до полного.", st.money >= 1.50f * mul));
                    break;
                case "gunsmith":
                    rows.Add(MenuRow.Header("Мастерская"));
                    rows.Add(new MenuRow("Почистить всё оружие", () => { if (Pay(0.50f * mul)) { foreach (Weapon w in g.player.weapons) w.shotsSinceClean = 0; g.Notify("Оружие вычищено и смазано"); } },
                        Price(0.50f * mul), "Грязное оружие бьёт слабее и хуже целится.", st.money >= 0.50f * mul));
                    break;
                case "smithy":
                    rows.Add(MenuRow.Header("Кузница"));
                    rows.Add(new MenuRow("Перековать лошадь", () => { Horse h = g.ctrl.horse; if (h != null && !h.dead && Pay(0.80f * mul)) { h.health = h.maxHealth; h.AddBond(6f); g.Notify("Подковы новые"); } },
                        Price(0.80f * mul), "Лечит лошадь и немного прибавляет привязанности.", st.money >= 0.80f * mul && g.ctrl.horse != null));
                    break;
                case "station":
                    rows.Add(MenuRow.Header("Почта"));
                    rows.Add(new MenuRow("Заплатить награду за свою голову", () => { if (g.law.PayBounty()) { Sfx.Play2D("coin", 0.6f); g.Notify("Награда снята. Закон к вам претензий не имеет"); } },
                        g.law.bounty > 0f ? Price(g.law.bounty) : "нет", "Платёж шерифу через почту. Пока идёт погоня, не принимается.", g.law.bounty > 0f && !g.law.Wanted && st.money >= g.law.bounty));
                    rows.Add(new MenuRow("Билет", null, "поезда не ходят", "Ветку дальше Сухого Брода ещё не проложили.", false));
                    break;
                case "stable":
                {
                    Horse own = g.ctrl.horse;
                    rows.Add(MenuRow.Header("Уход"));
                    rows.Add(new MenuRow("Корм, вода и чистка", () => { if (own != null && !own.dead && Pay(1.00f * mul)) { own.health = own.maxHealth; own.stamina = own.maxStamina; own.AddBond(10f); g.Notify(own.horseName + " накормлен и вычищен"); } },
                        Price(1.00f * mul), "Здоровье и выносливость лошади — до полных, привязанность растёт.", own != null && !own.dead && st.money >= 1.00f * mul));
                    rows.Add(MenuRow.Header("Лошади на продажу"));
                    float tradeIn = own != null && !own.dead ? Mathf.Round(own.breed.price * 0.4f) : 0f;
                    foreach (HorseBreed b in HorseDB.All)
                    {
                        if (b.id == "mustang") continue;       // mustangs are caught, not bought
                        HorseBreed breed = b;
                        float price = Mathf.Max(0f, b.price * mul - tradeIn);
                        bool same = own != null && !own.dead && own.breed == b;
                        rows.Add(new MenuRow(b.name, () => { if (Pay(price)) { g.menus.Close(); BuyHorse(breed); } }, same ? "ваша" : Price(price),
                            "Скорость " + b.speedStat + ", разгон " + b.accelStat + ", выносливость " + b.staminaStat + ", здоровье " + b.healthStat + ".\n" +
                            (b.calm > 0.8f ? "Спокойная, выстрелов почти не боится." : b.calm > 0.6f ? "Ровного нрава." : b.calm > 0.4f ? "Упрямая." : "Пугливая: в перестрелке может понести.") +
                            (tradeIn > 0f ? "\n\nВаша лошадь идёт в зачёт за " + Price(tradeIn) + "." : ""), !same && st.money >= price));
                    }
                    break;
                }
            }

            // ---- goods
            if (s.weapons.Count > 0)
            {
                rows.Add(MenuRow.Header("Оружие"));
                foreach (string id in s.weapons)
                {
                    WeaponDef d = WeaponDB.Get(id);
                    bool owned = g.player.Find(id) != null;
                    float price = d.price * mul;
                    rows.Add(new MenuRow(d.name, () => { if (Pay(price)) { g.player.Give(d.id); if (d.ammo != null) g.sat.Add(d.ammo, d.magazine * 2); } }, owned ? "есть" : Price(price),
                        d.Melee ? "Урон " + d.damage.ToString("0") + "." : "Урон " + d.damage.ToString("0") + (d.pellets > 1 ? "×" + d.pellets : "") + ", магазин " + d.magazine + ", дальность " + d.range.ToString("0") + " м.\n" +
                        "Чистый убой: " + (d.cleanFor == GameSize.Small ? "мелкая дичь" : d.cleanFor == GameSize.Medium ? "средняя дичь" : "крупный зверь") + ".", !owned && st.money >= price));
                }
            }
            if (s.items.Count > 0)
            {
                rows.Add(MenuRow.Header("Товары"));
                foreach (string id in s.items)
                {
                    ItemDef d = ItemDB.Get(id);
                    float price = Mathf.Round(d.price * mul * 100f) / 100f;
                    rows.Add(new MenuRow(d.name + (d.pack > 1 ? " (" + d.pack + " шт.)" : ""), () => { if (Pay(price)) g.sat.Add(d.id, d.pack); }, Price(price),
                        Menus.ItemAbout(d) + "\n\n<color=#B3A78C>В сумке: " + g.sat.Count(id) + "</color>", st.money >= price));
                }
            }
            Horse ch = g.ctrl.horse;
            if (s.id == "butcher" && ch != null && ch.cargo != null && Vector3.Distance(ch.Pos, g.player.Pos) < 30f)
            {
                AnimalDef ad = AnimalDB.Get(ch.cargo);
                ItemDef pelt = ItemDB.Get(ItemDB.PeltId(ad.id, ch.cargoQuality));
                float worth = (pelt != null ? pelt.sell : 0f) + ItemDB.Get(ad.meat).sell * ad.meatCount;
                rows.Add(MenuRow.Header("С крупа лошади"));
                rows.Add(new MenuRow("Продать тушу: " + ad.name.ToLowerInvariant(), () => { Wildlife.TakeCargo(ch, out _); st.money = Mathf.Round((st.money + worth) * 100f) / 100f; Sfx.Play2D("coin", 0.6f); },
                    "+" + Price(worth), "Целая туша: шкура и всё мясо разом."));
            }
            if (s.buysAt > 0f)
            {
                var sell = new List<ItemDef>();
                foreach (var kv in g.sat.items)
                {
                    ItemDef d = ItemDB.Get(kv.Key);
                    if (d != null && d.sell > 0f && s.buys.Contains(d.kind)) sell.Add(d);
                }
                if (sell.Count > 0)
                {
                    rows.Add(MenuRow.Header("Продать"));
                    sell.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    foreach (ItemDef d in sell)
                    {
                        ItemDef item = d;
                        float pay = Mathf.Max(0.01f, Mathf.Round(d.sell * s.buysAt * 100f) / 100f);
                        rows.Add(new MenuRow(d.name, () => { if (g.sat.Remove(item.id)) { st.money = Mathf.Round((st.money + pay) * 100f) / 100f; Sfx.Play2D("coin", 0.6f); } },
                            "+" + Price(pay) + "  ×" + g.sat.Count(d.id), d.about));
                    }
                }
            }
            if (rows.Count == 0) rows.Add(new MenuRow("Пока нечем торговать", null, "", "", false));
            return rows;
        }

        static void BuyHorse(HorseBreed breed)
        {
            Game g = Game.I;
            Horse old = g.ctrl.horse;
            if (old != null)
            {
                if (g.player.mount == old) g.player.Dismount();
                Object.Destroy(old.gameObject);
            }
            string[] names = { "Уголёк", "Ветер", "Рыжик", "Буран", "Искра", "Гром" };
            Vector3 at = g.region.P("stable_hitch");
            Horse h = g.SpawnPlayerHorse(World.OnGround(at), g.region.Yaw("stable_hitch") + 180f, breed.id, U.Pick(names));
            g.Notify("Куплена лошадь: " + breed.name + " по кличке " + h.horseName);
            g.Save("auto");
        }
    }
}
