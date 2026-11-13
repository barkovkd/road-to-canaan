using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// The wagon camp of the fellowship: the cash box and the ledger, three supplies that feed its spirit,
    /// daily chores, Buck's stew, the doctor, the forge, the captain's bed and the cooking fire.
    /// </summary>
    public class Camp
    {
        readonly Game game;
        public bool Exists;
        public Vector3 RespawnPoint;
        public float funds, provisions = 55f, medicine = 50f, ammo = 45f, morale = 60f;
        public int kitchen = 1, chest = 1, armory = 1;
        public bool tent, hitch;
        int day = -1, stewDay = -1, medDay = -1, ammoDay = -1, woodDay = -1, waterDay = -1, hayDay = -1;

        public Camp(Game g)
        {
            game = g;
            g.Saving += Save;
            g.Loaded += Load;
        }

        Human Who(string name) { return Population.named.TryGetValue(name, out Human h) ? h : null; }

        static bool AtPost(Human h) { return h != null && !h.dead && h.isActiveAndEnabled && h.brain is Townsman t && t.AtPost; }

        public void Setup(Region r)
        {
            Exists = r.points.ContainsKey("camp_fire");
            if (!Exists) return;
            RespawnPoint = r.P("camp_bed");
            Vector3 P(string k) { return r.P(k); }
            Human marta = Who("Марта Линдквист"), buck = Who("Бак Тиллер"), yefim = Who("Ефим Кузнецов"), doc = Who("Док Финч");

            Interactable.Add("Книга учёта", () => P("camp_ledgerfront"), _ => Ledger(), 2.4f, () => AtPost(marta));
            Interactable.Add("Положить доллар в кассу", () => P("camp_box"), _ => Donate(1f), 1.8f, () => game.stats.money >= 1f);
            Interactable.Add("К Баку", () => P("camp_cookfront"), _ => Kitchen(), 2.4f, () => AtPost(buck));
            Interactable.Add("К доктору", () => P("camp_docfront"), _ => Doctor(), 2.4f, () => AtPost(doc));
            Interactable.Add("К Ефиму", () => P("camp_smithfront"), _ => Forge(), 2.4f, () => AtPost(yefim));
            Interactable.Add("Лечь спать", () => P("camp_bed"), _ => Bed(), 2.2f);
            Interactable.Add("Готовить на костре", () => P("camp_fire"), _ => Cook(), 2.6f);
            Chore("Наколоть дров", "camp_wood", () => woodDay, d => woodDay = d, 4f, "Дрова наколоты");
            Chore("Принести воды", "camp_water", () => waterDay, d => waterDay = d, 3f, "Бочка полна");
            Chore("Задать корм волам", "camp_hay", () => hayDay, d => hayDay = d, 3f, "Волы накормлены");
            day = game.clock.day;
        }

        void Chore(string text, string key, System.Func<int> get, System.Action<int> set, float lift, string done)
        {
            Vector3 p = game.region.P(key);
            var it = Interactable.Add(text, () => p, _ =>
            {
                set(game.clock.day);
                game.honor.Add(2f);
                morale = Mathf.Min(100f, morale + lift);
                game.stats.UseStamina(12f);
                game.Notify(done + ". Лагерь это видит");
                Sfx.Play2D("pickup", 0.5f);
            }, 2.2f, () => get() != game.clock.day);
            it.hold = 2.2f;
        }

        public void Tick(float dt)
        {
            if (!Exists) return;
            int today = game.clock.day;
            if (day < 0) day = today;
            while (day < today)
            {
                day++;
                provisions = Mathf.Max(0f, provisions - (kitchen == 1 ? 12f : kitchen == 2 ? 9f : 6f));
                medicine = Mathf.Max(0f, medicine - (chest == 1 ? 5f : chest == 2 ? 4f : 3f));
                ammo = Mathf.Max(0f, ammo - (armory == 1 ? 4f : armory == 2 ? 3f : 2f));
                float want = (provisions * 0.5f + medicine * 0.25f + ammo * 0.25f);
                morale = Mathf.MoveTowards(morale, Mathf.Clamp(want + 15f, 0f, 100f), 8f);
            }
        }

        string Status()
        {
            return "Касса: " + U.Money(funds) + "\nПровизия: " + provisions.ToString("0") + "   Лекарства: " + medicine.ToString("0") + "   Патроны: " + ammo.ToString("0")
                 + "\nДух обоза: " + (morale >= 70f ? "крепкий" : morale >= 40f ? "ровный" : morale >= 20f ? "тревожный" : "на исходе") + " (" + morale.ToString("0") + ")";
        }

        void Donate(float amount)
        {
            amount = Mathf.Min(amount, game.stats.money);
            if (amount <= 0f) return;
            game.stats.money = Mathf.Round((game.stats.money - amount) * 100f) / 100f;
            funds += amount;
            game.honor.Add(amount / 5f, amount >= 5f);
            morale = Mathf.Min(100f, morale + amount * 0.3f);
            Sfx.Play2D("coin", 0.6f);
        }

        void Ledger()
        {
            game.menus.OpenList("Книга учёта", () =>
            {
                var rows = new List<MenuRow> { MenuRow.Header("Товарищество «Новый Ханаан»"), new MenuRow("Состояние обоза", null, "", Status()) };
                rows.Add(MenuRow.Header("Взносы"));
                rows.Add(new MenuRow("Внести 1 $", () => Donate(1f), "", "Деньги идут в общую кассу. Каждые пять долларов — заметный шаг к доверию.", game.stats.money >= 1f));
                rows.Add(new MenuRow("Внести 5 $", () => Donate(5f), "", "", game.stats.money >= 5f));
                float val = 0f;
                foreach (ItemDef d in game.sat.Of(ItemKind.Valuable)) val += d.sell * game.sat.Count(d.id);
                rows.Add(new MenuRow("Отдать все ценности", () =>
                {
                    float sum = 0f;
                    foreach (ItemDef d in game.sat.Of(ItemKind.Valuable)) { int n = game.sat.Count(d.id); sum += d.sell * n; game.sat.Remove(d.id, n); }
                    funds += sum;
                    game.honor.Add(sum / 5f);
                    Sfx.Play2D("coin", 0.6f);
                }, val > 0f ? "+" + U.Money(val) : "нет", "Часы, кольца и прочее, что нашлось по дороге.", val > 0f));

                rows.Add(MenuRow.Header("Улучшения — из кассы"));
                void Upgrade(string name, System.Func<int> get, System.Action<int> set, string about)
                {
                    int lv = get();
                    float cost = lv == 1 ? 30f : 60f;
                    if (lv >= 3) { rows.Add(new MenuRow(name + " III", null, "готово", about, false)); return; }
                    rows.Add(new MenuRow(name + (lv == 1 ? " II" : " III"), () => { if (funds >= cost) { funds -= cost; set(lv + 1); morale = Mathf.Min(100f, morale + 6f); Sfx.Play2D("coin", 0.6f); } },
                        U.Money(cost), about, funds >= cost));
                }
                Upgrade("Кухня", () => kitchen, v => kitchen = v, "Провизия расходуется медленнее, похлёбка сытнее.");
                Upgrade("Аптечка", () => chest, v => chest = v, "Лекарства расходуются медленнее, доктор даёт больше.");
                Upgrade("Патронный ящик", () => armory, v => armory = v, "Патроны расходуются медленнее, Ефим даёт больше.");
                rows.Add(new MenuRow("Палатка капитана", () => { if (funds >= 40f && !tent) { funds -= 40f; tent = true; Sfx.Play2D("coin", 0.6f); } }, tent ? "готово" : U.Money(40f),
                    "Крепкая палатка с койкой: после сна все запасы полны.", !tent && funds >= 40f));
                rows.Add(new MenuRow("Коновязь с навесом", () => { if (funds >= 35f && !hitch) { funds -= 35f; hitch = true; Sfx.Play2D("coin", 0.6f); } }, hitch ? "готово" : U.Money(35f),
                    "Лошадь в лагере отдыхает и лечится сама.", !hitch && funds >= 35f));
                return rows;
            }, null, () => "касса " + U.Money(funds));
        }

        void Kitchen()
        {
            game.menus.OpenList("Кухня Бака", () =>
            {
                var rows = new List<MenuRow> { new MenuRow("Провизия обоза", null, provisions.ToString("0"), Status()) };
                bool can = stewDay != game.clock.day && provisions >= 10f;
                rows.Add(new MenuRow("Миска похлёбки", () =>
                {
                    stewDay = game.clock.day;
                    provisions -= 4f;
                    float k = kitchen == 1 ? 45f : kitchen == 2 ? 60f : 80f;
                    game.stats.Feed(k, k, k * 0.4f, game.player);
                    game.Notify("Горячее и сытное. Бак доволен");
                }, stewDay == game.clock.day ? "уже ели" : provisions < 10f ? "котёл пуст" : "раз в день", "Пополняет запасы здоровья и выносливости.", can));
                Horse h = game.ctrl.horse;
                if (h != null && h.cargo != null && Vector3.Distance(h.Pos, game.player.Pos) < 25f)
                {
                    AnimalDef d = AnimalDB.Get(h.cargo);
                    rows.Add(new MenuRow("Отдать тушу с лошади", () =>
                    {
                        AnimalDef got = Wildlife.TakeCargo(h, out _);
                        provisions = Mathf.Min(100f, provisions + got.provisions);
                        morale = Mathf.Min(100f, morale + 5f);
                        game.honor.Add(2f);
                        game.Notify("Провизия пополнена: " + got.name.ToLowerInvariant());
                    }, "+" + d.provisions.ToString("0"), "Целая туша кормит обоз не один день."));
                }
                foreach (ItemDef d in game.sat.Of(ItemKind.Meat))
                {
                    ItemDef item = d;
                    float add = d.id == "meat_small" ? 4f : d.id == "meat_prime" ? 12f : 8f;
                    rows.Add(new MenuRow("Отдать: " + d.name.ToLowerInvariant(), () =>
                    {
                        if (!game.sat.Remove(item.id)) return;
                        provisions = Mathf.Min(100f, provisions + add);
                        game.honor.Add(0.5f, false);
                    }, "+" + add.ToString("0") + "  ×" + game.sat.Count(d.id), "В общий котёл."));
                }
                return rows;
            });
        }

        void Doctor()
        {
            game.menus.OpenList("Док Финч", () =>
            {
                var rows = new List<MenuRow> { new MenuRow("Лекарства обоза", null, medicine.ToString("0"), Status()) };
                rows.Add(new MenuRow("Осмотр", () =>
                {
                    medicine -= 6f;
                    game.stats.healthCore = 100f;
                    game.player.maxHealth = game.stats.MaxHealthBar + 100f * PlayerStats.CoreShare;
                    game.player.health = game.player.maxHealth;
                    game.Notify("Док Финч подлатала вас");
                }, "", "Здоровье и его запас — до полного. Тратит лекарства обоза.", medicine >= 10f && game.player.health < game.player.maxHealth - 1f));
                rows.Add(new MenuRow("Взять лекарство в дорогу", () =>
                {
                    medDay = game.clock.day;
                    medicine -= 8f;
                    game.sat.Add("tonic_health");
                    if (chest >= 2) game.sat.Add("tonic_stamina");
                }, medDay == game.clock.day ? "уже брали" : "раз в день", "Бальзам из запасов обоза.", medDay != game.clock.day && medicine >= 20f));
                foreach (ItemDef d in game.sat.Of(ItemKind.Herb))
                {
                    ItemDef item = d;
                    rows.Add(new MenuRow("Отдать: " + d.name.ToLowerInvariant(), () =>
                    {
                        if (!game.sat.Remove(item.id)) return;
                        medicine = Mathf.Min(100f, medicine + 3f);
                        game.honor.Add(0.3f, false);
                    }, "+3  ×" + game.sat.Count(d.id), "Травы идут на отвары."));
                }
                return rows;
            });
        }

        void Forge()
        {
            game.menus.OpenList("Кузня Ефима", () =>
            {
                var rows = new List<MenuRow> { new MenuRow("Патроны обоза", null, ammo.ToString("0"), Status()) };
                rows.Add(new MenuRow("Почистить оружие", () => { foreach (Weapon w in game.player.weapons) w.shotsSinceClean = 0; game.Notify("Ефим вычистил оружие"); }, "", "Бесплатно для капитана."));
                rows.Add(new MenuRow("Взять патроны", () =>
                {
                    ammoDay = game.clock.day;
                    ammo -= 8f;
                    game.sat.Add("ammo_revolver", armory >= 2 ? 18 : 12);
                    if (armory >= 2) game.sat.Add("ammo_rifle", 8);
                }, ammoDay == game.clock.day ? "уже брали" : "раз в день", "Из ящика обоза.", ammoDay != game.clock.day && ammo >= 15f));
                return rows;
            });
        }

        void Bed()
        {
            game.menus.OpenList("Отдых", () => new List<MenuRow>
            {
                new MenuRow("Спать до утра", () => Sleep(7f), "07:00", "Сон восстанавливает запасы. Игра сохраняется."),
                new MenuRow("Спать до полудня", () => Sleep(12f), "12:00"),
                new MenuRow("Спать до вечера", () => Sleep(18f), "18:00"),
                new MenuRow("Спать до ночи", () => Sleep(22f), "22:00"),
            }, null, () => game.clock.Text);
        }

        void Sleep(float hour)
        {
            game.menus.Close();
            game.Rest(hour, "Вы отдохнули");
            if (tent) { game.stats.healthCore = 100f; game.stats.staminaCore = 100f; game.stats.coolCore = 100f; }
            Horse h = game.ctrl.horse;
            if (hitch && h != null && !h.dead) { h.health = h.maxHealth; h.stamina = h.maxStamina; }
        }

        // ================================================================== cooking

        struct Recipe { public string result, title, a, b; public int na, nb; }

        static readonly Recipe[] Recipes =
        {
            new Recipe { result = "grilled_meat", title = "Жареное мясо", a = "MEAT", na = 1 },
            new Recipe { result = "meat_onion", title = "Мясо с диким луком", a = "MEAT", na = 1, b = "herb_onion", nb = 1 },
            new Recipe { result = "meat_sage", title = "Мясо с полынью", a = "MEAT", na = 1, b = "herb_sage", nb = 1 },
            new Recipe { result = "meat_yarrow", title = "Мясо с тысячелистником", a = "MEAT", na = 1, b = "herb_yarrow", nb = 1 },
            new Recipe { result = "brew_vigor", title = "Отвар бодрости", a = "herb_onion", na = 2 },
            new Recipe { result = "brew_heal", title = "Целебный отвар", a = "herb_yarrow", na = 2 },
        };

        static readonly string[] Meats = { "meat_small", "meat_game", "meat_prime" };

        int Have(string id)
        {
            if (id != "MEAT") return game.sat.Count(id);
            int n = 0;
            foreach (string m in Meats) n += game.sat.Count(m);
            return n;
        }

        void Take(string id, int n)
        {
            if (id != "MEAT") { game.sat.Remove(id, n); return; }
            foreach (string m in Meats)
                while (n > 0 && game.sat.Count(m) > 0) { game.sat.Remove(m); n--; }
        }

        public void Cook()
        {
            game.menus.OpenList("Костёр", () =>
            {
                var rows = new List<MenuRow>();
                foreach (Recipe rc in Recipes)
                {
                    Recipe r = rc;
                    ItemDef d = ItemDB.Get(r.result);
                    bool can = Have(r.a) >= r.na && (r.b == null || Have(r.b) >= r.nb);
                    string need = (r.a == "MEAT" ? "мясо" : ItemDB.Get(r.a).name.ToLowerInvariant()) + " ×" + r.na + " (есть " + Have(r.a) + ")"
                                + (r.b != null ? "\n" + ItemDB.Get(r.b).name.ToLowerInvariant() + " ×" + r.nb + " (есть " + Have(r.b) + ")" : "");
                    rows.Add(new MenuRow(r.title, () =>
                    {
                        Take(r.a, r.na);
                        if (r.b != null) Take(r.b, r.nb);
                        game.sat.Add(r.result);
                        Sfx.Play2D("pickup", 0.6f);
                    }, "в сумке " + game.sat.Count(r.result), "Нужно:\n" + need + "\n\n" + Menus.ItemAbout(d), can));
                }
                return rows;
            });
        }

        // ================================================================== saving

        void Save(SaveData d)
        {
            d.Set("camp.funds", funds); d.Set("camp.provisions", provisions); d.Set("camp.medicine", medicine); d.Set("camp.ammo", ammo); d.Set("camp.morale", morale);
            d.Set("camp.kitchen", kitchen); d.Set("camp.chest", chest); d.Set("camp.armory", armory); d.Set("camp.tent", tent ? 1f : 0f); d.Set("camp.hitch", hitch ? 1f : 0f);
            d.Set("camp.day", day); d.Set("camp.stew", stewDay); d.Set("camp.med", medDay); d.Set("camp.ammoDay", ammoDay);
            d.Set("camp.wood", woodDay); d.Set("camp.water", waterDay); d.Set("camp.hay", hayDay);
        }

        void Load(SaveData d)
        {
            funds = d.Get("camp.funds", 0f); provisions = d.Get("camp.provisions", 55f); medicine = d.Get("camp.medicine", 50f); ammo = d.Get("camp.ammo", 45f); morale = d.Get("camp.morale", 60f);
            kitchen = (int)d.Get("camp.kitchen", 1f); chest = (int)d.Get("camp.chest", 1f); armory = (int)d.Get("camp.armory", 1f);
            tent = d.Get("camp.tent") > 0.5f; hitch = d.Get("camp.hitch") > 0.5f;
            day = (int)d.Get("camp.day", -1f); stewDay = (int)d.Get("camp.stew", -1f); medDay = (int)d.Get("camp.med", -1f); ammoDay = (int)d.Get("camp.ammoDay", -1f);
            woodDay = (int)d.Get("camp.wood", -1f); waterDay = (int)d.Get("camp.water", -1f); hayDay = (int)d.Get("camp.hay", -1f);
        }
    }
}
