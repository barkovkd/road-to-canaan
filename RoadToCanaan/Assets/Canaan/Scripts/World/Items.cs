using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    public enum ItemKind { Provision, Tonic, Ammo, Herb, Meat, Pelt, Valuable, Kit }

    public class ItemDef
    {
        public string id, name, about = "";
        public ItemKind kind;
        public float price, sell;                 // shop price and what a buyer pays
        public float health, stamina, cool;       // reserves restored when eaten or drunk
        public bool fullHealth, fullStamina, fullCool;
        public int pack = 1;                      // units per purchase (cartridge boxes)
        public bool usable;
    }

    public static class ItemDB
    {
        public static readonly List<ItemDef> All = new List<ItemDef>();
        static readonly Dictionary<string, ItemDef> byId = new Dictionary<string, ItemDef>();

        // name in the genitive ("шкура кого?") and prices of a poor, good and perfect pelt
        static readonly Dictionary<string, (string gen, float p1, float p2, float p3)> Pelts = new Dictionary<string, (string, float, float, float)>
        {
            { "pronghorn", ("вилорога", 1.2f, 2.2f, 3.5f) }, { "deer", ("оленя", 1.4f, 2.5f, 4.0f) }, { "bison", ("бизона", 3f, 6f, 10f) },
            { "coyote", ("койота", 0.8f, 1.5f, 2.4f) }, { "wolf", ("волка", 1.2f, 2.2f, 3.6f) }, { "rabbit", ("зайца", 0.3f, 0.5f, 0.8f) },
            { "cow", ("коровы", 1.5f, 1.5f, 1.5f) }, { "ox", ("вола", 1.5f, 1.5f, 1.5f) }, { "horse", ("лошади", 1.0f, 1.0f, 1.0f) },
        };
        static readonly string[] Quality = { "плохая", "хорошая", "отличная" };

        static ItemDB()
        {
            Food("beans", "Банка бобов", 0.40f, 20f, 25f, 0f, "Сытно и надолго.");
            Food("bread", "Хлеб", 0.15f, 12f, 12f, 0f, "Вчерашний, но съедобный.");
            Food("jerky", "Вяленое мясо", 0.30f, 10f, 30f, 0f, "Жуётся долго, держит в седле.");
            Food("crackers", "Галеты", 0.20f, 8f, 15f, 0f, "Армейские, твёрдые как подкова.");
            Food("apple", "Яблоко", 0.10f, 8f, 8f, 0f, "Годится и лошади.");
            Food("grilled_meat", "Жареное мясо", 0f, 35f, 10f, 0f, "С костра.");
            Food("meat_onion", "Мясо с диким луком", 0f, 35f, 50f, 0f, "Лук бодрит.");
            Food("meat_sage", "Мясо с полынью", 0f, 35f, 10f, 50f, "Горчит и проясняет голову.");
            Food("meat_yarrow", "Мясо с тысячелистником", 0f, 80f, 10f, 0f, "Лагерное лекарство.");
            Add(new ItemDef { id = "coffee", name = "Кофе", kind = ItemKind.Provision, price = 0.25f, sell = 0.08f, stamina = 20f, cool = 15f, usable = true, about = "Молотый, на одну кружку." });

            Add(new ItemDef { id = "tonic_health", name = "Целебный бальзам", kind = ItemKind.Tonic, price = 1.50f, sell = 0.5f, fullHealth = true, usable = true, about = "Аптечный. Восстанавливает здоровье." });
            Add(new ItemDef { id = "tonic_stamina", name = "Бодрящая настойка", kind = ItemKind.Tonic, price = 1.20f, sell = 0.4f, fullStamina = true, usable = true, about = "Восстанавливает выносливость." });
            Add(new ItemDef { id = "tonic_cool", name = "Нюхательная соль", kind = ItemKind.Tonic, price = 1.40f, sell = 0.45f, fullCool = true, usable = true, about = "Восстанавливает хладнокровие." });
            Add(new ItemDef { id = "tobacco", name = "Табак", kind = ItemKind.Tonic, price = 0.50f, sell = 0.15f, cool = 40f, health = -4f, usable = true, about = "Успокаивает руку. Здоровью не на пользу." });
            Add(new ItemDef { id = "whiskey", name = "Виски", kind = ItemKind.Tonic, price = 0.75f, sell = 0.25f, health = 18f, cool = -12f, usable = true, about = "Греет. Рука после него уже не та." });
            Add(new ItemDef { id = "brew_heal", name = "Целебный отвар", kind = ItemKind.Tonic, sell = 0.4f, fullHealth = true, usable = true, about = "Из тысячелистника." });
            Add(new ItemDef { id = "brew_vigor", name = "Отвар бодрости", kind = ItemKind.Tonic, sell = 0.4f, fullStamina = true, usable = true, about = "Из дикого лука." });

            Add(new ItemDef { id = "ammo_revolver", name = "Револьверные патроны", kind = ItemKind.Ammo, price = 1.20f, sell = 0.01f, pack = 30 });
            Add(new ItemDef { id = "ammo_rifle", name = "Винтовочные патроны", kind = ItemKind.Ammo, price = 1.50f, sell = 0.02f, pack = 20 });
            Add(new ItemDef { id = "ammo_shell", name = "Дробовые патроны", kind = ItemKind.Ammo, price = 1.40f, sell = 0.02f, pack = 15 });
            Add(new ItemDef { id = "ammo_22", name = "Патроны .22", kind = ItemKind.Ammo, price = 0.80f, sell = 0.005f, pack = 40 });
            Add(new ItemDef { id = "ammo_arrow", name = "Стрелы", kind = ItemKind.Ammo, price = 0.60f, sell = 0.02f, pack = 10 });

            Add(new ItemDef { id = "herb_yarrow", name = "Тысячелистник", kind = ItemKind.Herb, sell = 0.10f, health = 8f, usable = true, about = "Белые зонтики. Заживляет." });
            Add(new ItemDef { id = "herb_onion", name = "Дикий лук", kind = ItemKind.Herb, sell = 0.08f, stamina = 8f, usable = true, about = "Растёт в низинах у воды." });
            Add(new ItemDef { id = "herb_sage", name = "Полынь", kind = ItemKind.Herb, sell = 0.08f, cool = 8f, usable = true, about = "Серебристые кусты по сухим склонам." });

            Add(new ItemDef { id = "meat_small", name = "Мясо мелкой дичи", kind = ItemKind.Meat, sell = 0.40f, about = "Заячье. На один зуб." });
            Add(new ItemDef { id = "meat_game", name = "Дичина", kind = ItemKind.Meat, sell = 0.80f, about = "Хороший кусок с ноги." });
            Add(new ItemDef { id = "meat_prime", name = "Отборное мясо", kind = ItemKind.Meat, sell = 1.20f, about = "Бизоний горб." });

            Add(new ItemDef { id = "pocket_watch", name = "Карманные часы", kind = ItemKind.Valuable, sell = 2.5f, about = "Идут." });
            Add(new ItemDef { id = "silver_ring", name = "Серебряное кольцо", kind = ItemKind.Valuable, sell = 1.8f });
            Add(new ItemDef { id = "belt_buckle", name = "Пряжка", kind = ItemKind.Valuable, sell = 0.9f });
            Add(new ItemDef { id = "gold_tooth", name = "Золотой зуб", kind = ItemKind.Valuable, sell = 1.4f });

            Add(new ItemDef { id = "gun_oil", name = "Оружейное масло", kind = ItemKind.Kit, price = 0.75f, sell = 0.2f, usable = true, about = "Чистит оружие в руках." });
            Add(new ItemDef { id = "hay", name = "Охапка сена", kind = ItemKind.Kit, price = 0.30f, sell = 0.05f, about = "Корм для лошади." });
            Add(new ItemDef { id = "horse_brush", name = "Щётка", kind = ItemKind.Kit, price = 1.00f, sell = 0.25f, about = "Чистить лошадь." });
            Add(new ItemDef { id = "horse_reviver", name = "Оживитель", kind = ItemKind.Kit, price = 4.50f, sell = 1.0f, about = "Поднимает тяжело раненую лошадь." });
        }

        static void Food(string id, string name, float price, float health, float stamina, float cool, string about)
        {
            Add(new ItemDef { id = id, name = name, kind = ItemKind.Provision, price = price, sell = price > 0f ? price * 0.3f : 0.25f, health = health, stamina = stamina, cool = cool, usable = true, about = about });
        }

        static void Add(ItemDef d) { All.Add(d); byId[d.id] = d; }

        public static ItemDef Get(string id)
        {
            if (byId.TryGetValue(id, out ItemDef d)) return d;
            if (id.StartsWith("pelt_"))
            {
                // pelt_<animal>_<quality 1..3>
                string[] parts = id.Split('_');
                if (parts.Length == 3 && Pelts.TryGetValue(parts[1], out var info) && int.TryParse(parts[2], out int q))
                {
                    q = Mathf.Clamp(q, 1, 3);
                    d = new ItemDef
                    {
                        id = id, kind = ItemKind.Pelt, name = "Шкура " + info.gen + " (" + Quality[q - 1] + ")",
                        sell = q == 1 ? info.p1 : q == 2 ? info.p2 : info.p3
                    };
                    byId[id] = d;
                    return d;
                }
            }
            return null;
        }

        public static string AnimalGenitive(string kind) { return Pelts.TryGetValue(kind, out var i) ? i.gen : kind; }
        public static string PeltId(string kind, int quality) { return "pelt_" + kind + "_" + Mathf.Clamp(quality, 1, 3); }
    }

    /// <summary>The hero's bag: counts of items by id.</summary>
    public class Satchel
    {
        public readonly Dictionary<string, int> items = new Dictionary<string, int>();
        public event Action Changed;

        public int Count(string id) { return items.TryGetValue(id, out int n) ? n : 0; }

        public void Add(string id, int n = 1)
        {
            if (n <= 0 || ItemDB.Get(id) == null) return;
            items[id] = Count(id) + n;
            Changed?.Invoke();
        }

        public bool Remove(string id, int n = 1)
        {
            int have = Count(id);
            if (have < n) return false;
            if (have == n) items.Remove(id); else items[id] = have - n;
            Changed?.Invoke();
            return true;
        }

        public List<ItemDef> Of(ItemKind kind)
        {
            var list = new List<ItemDef>();
            foreach (var kv in items)
            {
                ItemDef d = ItemDB.Get(kv.Key);
                if (d != null && d.kind == kind) list.Add(d);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        public void Clear() { items.Clear(); Changed?.Invoke(); }
    }
}
