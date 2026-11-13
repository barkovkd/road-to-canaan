using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Canaan
{
    public class MenuRow
    {
        public string label = "", right = "", about = "";
        public bool enabled = true, header;
        public Action act;

        public MenuRow() { }
        public MenuRow(string label, Action act, string right = "", string about = "", bool enabled = true)
        {
            this.label = label; this.act = act; this.right = right; this.about = about; this.enabled = enabled;
        }

        public static MenuRow Header(string label) { return new MenuRow { label = label, header = true, enabled = false }; }
    }

    /// <summary>A titled list with a description pane: the body of nearly every menu in the game.</summary>
    public class ListMenu
    {
        const int Visible = 13;
        const float RowH = 52f;
        public RectTransform root;
        public List<MenuRow> rows = new List<MenuRow>();
        public int index;
        public Action onBack;
        public Func<string> header;
        public Func<List<MenuRow>> rebuild;      // called after every action so counts and prices stay current

        readonly Text title, about, footer, headerText;
        readonly List<Text> labels = new List<Text>(), rights = new List<Text>();
        readonly List<Image> backs = new List<Image>();
        readonly RectTransform listArea;
        int scroll;

        public ListMenu(Transform parent)
        {
            root = UI.Node(parent, "ListMenu");
            UI.Stretch(root);
            Image shade = UI.Img(root, UI.white, new Color(0.05f, 0.04f, 0.03f, 0.78f), "Shade");
            UI.Stretch(shade);
            Image panel = UI.Img(root, UI.rounded, UI.Ink, "Panel");
            UI.Place(panel, 0f, 0.5f, 110f, 0f, 760f, 900f);
            title = UI.Label(panel.transform, "", 46, UI.Paper, TextAnchor.UpperLeft, true);
            UI.Place(title, 0f, 1f, 40f, -30f, 680f, 60f);
            Image rule = UI.Img(panel.transform, UI.white, UI.Accent, "Rule");
            UI.Place(rule, 0f, 1f, 40f, -98f, 680f, 3f);
            headerText = UI.Label(panel.transform, "", 26, UI.Muted, TextAnchor.UpperRight);
            UI.Place(headerText, 1f, 1f, -40f, -44f, 360f, 40f);
            listArea = UI.Node(panel.transform, "Rows");
            UI.Place(listArea, 0f, 1f, 30f, -118f, 700f, Visible * RowH);
            for (int i = 0; i < Visible; i++)
            {
                Image back = UI.Img(listArea, UI.roundedSmall, UI.Paper, "Row");
                UI.Place(back, 0f, 1f, 0f, -i * RowH, 700f, RowH - 6f);
                Text l = UI.Label(back.transform, "", 28, UI.Paper, TextAnchor.MiddleLeft);
                UI.Stretch(l, 18f, 0f, 150f, 0f);
                l.horizontalOverflow = HorizontalWrapMode.Overflow;
                Text r = UI.Label(back.transform, "", 26, UI.Muted, TextAnchor.MiddleRight);
                UI.Stretch(r, 300f, 0f, 18f, 0f);
                backs.Add(back); labels.Add(l); rights.Add(r);
            }
            footer = UI.Label(panel.transform, "", 22, UI.Dim, TextAnchor.LowerLeft);
            UI.Place(footer, 0f, 0f, 40f, 24f, 680f, 30f);

            Image aboutPanel = UI.Img(root, UI.rounded, UI.Ink, "About");
            UI.Place(aboutPanel, 0f, 0.5f, 900f, 180f, 620f, 540f);
            about = UI.Label(aboutPanel.transform, "", 28, UI.Paper, TextAnchor.UpperLeft);
            UI.Stretch(about, 34f, 30f, 34f, 30f);
            root.gameObject.SetActive(false);
        }

        public void Show(string titleText, List<MenuRow> list, Action back, Func<string> head = null, Func<List<MenuRow>> rebuildRows = null)
        {
            title.text = titleText;
            rows = list;
            onBack = back;
            header = head;
            rebuild = rebuildRows;
            // start on the first row that does something
            index = Mathf.Max(0, rows.FindIndex(x => !x.header && x.enabled && x.act != null));
            scroll = 0;
            Step(0);
            footer.text = UI.Key(Btn.Accept) + " выбрать    " + UI.Key(Btn.Back) + " назад";
            root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide() { root.gameObject.SetActive(false); }
        public bool Shown => root.gameObject.activeSelf;

        void Step(int dir)
        {
            if (rows.Count == 0) return;
            int i = Mathf.Clamp(index, 0, rows.Count - 1);
            if (dir == 0) { if (rows[i].header) dir = 1; else { index = i; return; } }
            for (int n = 0; n < rows.Count; n++)
            {
                i = (i + dir + rows.Count) % rows.Count;
                if (!rows[i].header) { index = i; return; }
            }
        }

        public void Refresh()
        {
            if (index < scroll) scroll = index;
            if (index >= scroll + Visible) scroll = index - Visible + 1;
            if (index > 0 && rows.Count > 0 && rows[Mathf.Max(0, index - 1)].header && index - 1 < scroll) scroll = index - 1;
            for (int i = 0; i < Visible; i++)
            {
                int r = scroll + i;
                bool on = r < rows.Count;
                backs[i].gameObject.SetActive(on);
                if (!on) continue;
                MenuRow row = rows[r];
                bool sel = r == index;
                backs[i].color = sel ? UI.Paper : new Color(1f, 1f, 1f, row.header ? 0f : 0.04f);
                labels[i].text = row.header ? row.label.ToUpperInvariant() : row.label;
                labels[i].fontSize = row.header ? 20 : 28;
                labels[i].color = sel ? UI.InkSolid : row.header ? UI.Accent : row.enabled ? UI.Paper : UI.Dim;
                labels[i].fontStyle = sel || row.header ? FontStyle.Bold : FontStyle.Normal;
                rights[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
                rights[i].text = row.right;
                rights[i].color = sel ? UI.InkSolid : row.enabled ? UI.Muted : UI.Dim;
            }
            about.text = rows.Count > 0 && index < rows.Count ? rows[index].about : "";
            about.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(about.text));
            headerText.text = header != null ? header() : "";
        }

        /// <summary>Handles input for one frame. Returns true if something changed.</summary>
        public void Tick()
        {
            if (!Shown) return;
            bool changed = false;
            if (GameInput.Pressed(Btn.Down)) { Step(1); changed = true; Sfx.Play2D("ui_move", 0.5f); }
            if (GameInput.Pressed(Btn.Up)) { Step(-1); changed = true; Sfx.Play2D("ui_move", 0.5f); }

            // mouse: hover selects, click activates
            bool click = false;
            if (!GameInput.Virtual && (GameInput.PointerMoved || GameInput.Pressed(Btn.Fire)))
            {
                Vector2 p = UI.PointerOnCanvas();
                Vector3[] c = new Vector3[4];
                listArea.GetLocalCorners(c);
                Vector2 local = listArea.InverseTransformPoint(UI.root.TransformPoint(p));
                if (local.x >= c[0].x && local.x <= c[2].x && local.y <= c[1].y && local.y >= c[0].y)
                {
                    int r = scroll + Mathf.FloorToInt((c[1].y - local.y) / RowH);
                    if (r >= 0 && r < rows.Count && !rows[r].header)
                    {
                        if (r != index) { index = r; changed = true; }
                        click = GameInput.Pressed(Btn.Fire);
                    }
                }
            }
            if ((GameInput.Pressed(Btn.Accept) || click) && rows.Count > 0)
            {
                MenuRow row = rows[index];
                if (row.enabled && row.act != null)
                {
                    Sfx.Play2D("ui_select", 0.6f);
                    row.act();
                    if (Shown && rebuild != null)
                    {
                        string keep = index < rows.Count ? rows[index].label : "";
                        rows = rebuild();
                        int at = rows.FindIndex(x => x.label == keep && !x.header);
                        index = at >= 0 ? at : Mathf.Clamp(index, 0, Mathf.Max(0, rows.Count - 1));
                        Step(0);
                    }
                    changed = true;
                }
                else Sfx.Play2D("ui_back", 0.4f);
            }
            else if (GameInput.Pressed(Btn.Back))
            {
                Sfx.Play2D("ui_back", 0.5f);
                onBack?.Invoke();
                return;
            }
            if (changed && Shown) Refresh();
        }
    }

    /// <summary>All menus: pause, satchel, hero sheet, map, weapon wheel, and lists opened by shops and the camp.</summary>
    public class Menus
    {
        public ListMenu list;
        public bool open;                      // a full-screen menu that pauses the game
        public bool wheel;
        public Vector3? waypoint;
        Action tick;

        RectTransform mapRoot, mapArea, mapIcons, wheelRoot;
        RawImage mapImg;
        Image mapCursor, mapPlayer, wheelDot;
        Text mapHint, mapPlace;
        Vector2 cursor;
        readonly List<Image> mapBlips = new List<Image>();
        readonly List<Text> mapLabels = new List<Text>();
        readonly List<Text> wheelTexts = new List<Text>();
        Vector2 wheelDir;
        int wheelPick = -1;
        const float MapPx = 900f;

        static readonly string[] WheelNames = { "Убрать оружие", "Револьвер", "Первое длинное", "Второе длинное", "Нож", "Лук" };

        public void Build()
        {
            list = new ListMenu(UI.root);
            BuildMap();
            BuildWheel();
            Blip.Add(() => waypoint ?? Vector3.zero, "default", UI.Accent, "Метка", true, () => waypoint.HasValue, 20f);
        }

        // ============================================================ frame

        public void Tick(float udt)
        {
            Game g = Game.I;
            if (open)
            {
                tick?.Invoke();
                return;
            }
            if (!g.playing || g.player == null || g.player.dead || g.ctrl.locked && !wheel) return;

            if (GameInput.Pressed(Btn.Pause)) { Pause(); return; }
            if (GameInput.Pressed(Btn.Satchel)) { Satchel(() => Close()); return; }
            if (GameInput.Pressed(Btn.Map)) { Map(() => Close()); return; }

            // weapon wheel: hold, point, release
            bool want = GameInput.Held(Btn.Wheel) && !g.ctrl.slowmo;
            if (want && !wheel) { wheel = true; wheelDir = Vector2.zero; wheelPick = -1; wheelRoot.gameObject.SetActive(true); }
            if (wheel)
            {
                Vector2 look = GameInput.Look;
                wheelDir = Vector2.ClampMagnitude(wheelDir + new Vector2(look.x, look.y) * 0.08f, 1f);
                if (GameInput.Move.sqrMagnitude > 0.3f && GameInput.Gamepad) wheelDir = GameInput.Move.normalized;
                wheelPick = -1;
                if (wheelDir.magnitude > 0.35f)
                {
                    float ang = Mathf.Atan2(wheelDir.x, wheelDir.y) * Mathf.Rad2Deg;
                    wheelPick = Mathf.RoundToInt(Mathf.Repeat(ang, 360f) / 60f) % 6;
                }
                for (int i = 0; i < 6; i++)
                {
                    bool has = i == 0 || WheelWeapon(g.player, i) != null;
                    wheelTexts[i].color = i == wheelPick ? UI.Accent : has ? UI.Paper : UI.Dim;
                    Weapon w = WheelWeapon(g.player, i);
                    wheelTexts[i].text = i == 0 ? WheelNames[0] : w != null ? w.def.name + "\n<size=20>" + (w.def.Melee ? "" : w.loaded + " / " + g.sat.Count(w.def.ammo)) + "</size>" : "<size=22>" + WheelNames[i] + "\n—</size>";
                }
                wheelDot.rectTransform.anchoredPosition = wheelDir * 150f;
                if (!want)
                {
                    wheel = false;
                    wheelRoot.gameObject.SetActive(false);
                    if (wheelPick == 0) g.player.Equip(null);
                    else if (wheelPick > 0)
                    {
                        Weapon w = WheelWeapon(g.player, wheelPick);
                        if (w != null) g.player.Equip(w);
                    }
                }
            }
        }

        static Weapon WheelWeapon(Human p, int slot)
        {
            int n = 0;
            foreach (Weapon w in p.weapons)
            {
                bool match = slot == 1 ? w.def.cls == WeaponClass.Revolver
                           : slot == 2 || slot == 3 ? w.def.Long && w.def.cls != WeaponClass.Bow
                           : slot == 4 ? w.def.cls == WeaponClass.Knife : slot == 5 && w.def.cls == WeaponClass.Bow;
                if (!match) continue;
                if (slot == 3 && n++ == 0) continue;
                return w;
            }
            return null;
        }

        void Begin(Action onTick)
        {
            open = true;
            tick = onTick;
            if (wheel) { wheel = false; wheelRoot.gameObject.SetActive(false); }
            GameInput.SetCursor(false);
        }

        public void Close()
        {
            open = false;
            tick = null;
            list.Hide();
            mapRoot.gameObject.SetActive(false);
            GameInput.SetCursor(true);
        }

        /// <summary>Opens a list menu from any system (shop, campfire, ledger...).</summary>
        public void OpenList(string title, Func<List<MenuRow>> rows, Action onBack = null, Func<string> header = null)
        {
            mapRoot.gameObject.SetActive(false);
            list.Show(title, rows(), onBack ?? Close, header, rows);
            Begin(list.Tick);
        }

        // ============================================================ pause and its pages

        public void Pause()
        {
            Game g = Game.I;
            OpenList("Пауза", () => new List<MenuRow>
            {
                new MenuRow("Продолжить", Close),
                new MenuRow("Карта", () => Map(Pause)),
                new MenuRow("Сумка", () => Satchel(Pause)),
                new MenuRow("Герой", () => Hero(Pause)),
                new MenuRow("Сохранить игру", () => SaveMenu(true), "", "Три ячейки. Игра также сохраняется сама: после сна, покупок и раз в несколько минут.", g.CanSave),
                new MenuRow("Загрузить игру", () => SaveMenu(false)),
                new MenuRow("Управление", () => Controls(Pause)),
                new MenuRow("Авторы звуков", () => Credits(Pause)),
                new MenuRow("Выйти в главное меню", () => { Close(); g.ToMainMenu(); }),
                new MenuRow("Выйти из игры", () => Application.Quit()),
            }, Close, () => g.clock != null ? g.clock.Text : "");
        }

        public void Satchel(Action back)
        {
            Game g = Game.I;
            OpenList("Сумка", () =>
            {
                var rows = new List<MenuRow>();
                (ItemKind kind, string title)[] groups =
                {
                    (ItemKind.Provision, "Провизия"), (ItemKind.Tonic, "Тоники и табак"), (ItemKind.Herb, "Травы"), (ItemKind.Meat, "Мясо"),
                    (ItemKind.Pelt, "Шкуры"), (ItemKind.Ammo, "Патроны"), (ItemKind.Kit, "Снаряжение"), (ItemKind.Valuable, "Ценности"),
                };
                foreach (var gr in groups)
                {
                    List<ItemDef> items = g.sat.Of(gr.kind);
                    if (items.Count == 0) continue;
                    rows.Add(MenuRow.Header(gr.title));
                    foreach (ItemDef d in items)
                    {
                        ItemDef item = d;
                        rows.Add(new MenuRow(d.name, d.usable ? () => g.UseItem(item.id) : (Action)null, "×" + g.sat.Count(d.id), ItemAbout(d), true));
                    }
                }
                if (rows.Count == 0) rows.Add(new MenuRow("Пусто", null, "", "В сумке ничего нет.", false));
                return rows;
            }, back, () => U.Money(g.stats.money));
        }

        public static string ItemAbout(ItemDef d)
        {
            string s = d.about;
            var fx = new List<string>();
            if (d.fullHealth) fx.Add("здоровье — до полного"); else if (d.health != 0f) fx.Add("запас здоровья " + Signed(d.health));
            if (d.fullStamina) fx.Add("выносливость — до полной"); else if (d.stamina != 0f) fx.Add("запас выносливости " + Signed(d.stamina));
            if (d.fullCool) fx.Add("хладнокровие — до полного"); else if (d.cool != 0f) fx.Add("запас хладнокровия " + Signed(d.cool));
            if (fx.Count > 0) s += (s.Length > 0 ? "\n\n" : "") + "<color=#8FA85A>" + string.Join("\n", fx) + "</color>";
            if (d.sell > 0f) s += "\n\n<color=#B3A78C>Цена у скупщика: " + U.Money(d.sell) + "</color>";
            if (d.usable) s += "\n\n<color=#7A705E>" + GameInput.Label(Btn.Accept) + " — использовать</color>";
            return s;
        }

        static string Signed(float v) { return (v > 0f ? "+" : "") + v.ToString("0"); }

        public void Hero(Action back)
        {
            Game g = Game.I;
            OpenList("Калеб Торн", () =>
            {
                PlayerStats s = g.stats;
                var rows = new List<MenuRow>
                {
                    MenuRow.Header("Состояние"),
                    Info("Здоровье", "уровень " + s.healthLevel, "Шкала " + s.HealthBar(g.player).ToString("0") + " из " + s.MaxHealthBar.ToString("0") + ", запас " + s.healthCore.ToString("0") + " %.\n\nЗапас пополняют еда, сон и бальзамы. От запаса зависит, как быстро восстанавливается шкала."),
                    Info("Выносливость", "уровень " + s.staminaLevel, "Шкала " + s.stamina.ToString("0") + " из " + s.MaxStamina.ToString("0") + ", запас " + s.staminaCore.ToString("0") + " %.\n\nТратится на бег. Растёт от бега."),
                    Info("Хладнокровие", "ступень " + s.coolTier, "Шкала " + s.cool.ToString("0") + " из " + s.MaxCool.ToString("0") + ", запас " + s.coolCore.ToString("0") + " %.\n\nВ прицеле — " + GameInput.Label(Btn.DeadEye) + ": время замедляется, на целях под прицелом остаются метки, выстрел разряжает оружие по меткам."),
                    MenuRow.Header("Положение"),
                    Info("Деньги", U.Money(s.money), ""),
                    Info("Честь", g.honor != null ? g.honor.Label : "—", g.honor != null ? g.honor.About : ""),
                    Info("Награда за голову", g.law != null && g.law.bounty > 0f ? U.Money(g.law.bounty) : "нет", "Награду назначают за преступления, которые видели свидетели. Заплатить можно на почте при станции."),
                };
                Horse h = g.ctrl.horse;
                rows.Add(MenuRow.Header("Лошадь"));
                if (h != null && !h.dead)
                {
                    rows.Add(Info(h.horseName, h.breed.name, "Привязанность: уровень " + h.BondLevel + " из 4.\nЗдоровье " + h.health.ToString("0") + " из " + h.maxHealth.ToString("0") + ", выносливость " + h.stamina.ToString("0") + " из " + h.maxStamina.ToString("0") + ".\n\nПривязанность растёт от езды, чистки, корма и похлопывания. Чем она выше, тем дальше лошадь слышит свист и тем меньше боится выстрелов."));
                }
                else rows.Add(Info("Нет лошади", "", "Лошадь можно купить в конюшне."));
                return rows;
            }, back);
        }

        static MenuRow Info(string label, string right, string about) { return new MenuRow(label, null, right, about, true); }

        public void Controls(Action back)
        {
            OpenList("Управление", () =>
            {
                (string a, string k)[] keys =
                {
                    ("Движение / обзор", "WASD или стрелки / мышь"), ("Бег, шпоры верхом", "Shift"), ("Шаг", "CapsLock"), ("Присесть", "Ctrl"),
                    ("Прыжок", "Пробел"), ("Укрытие", "Q"), ("Действие, в седло", "E"), ("Удар", "F"), ("Прицел / выстрел", "ПКМ / ЛКМ или Z / C"),
                    ("Хладнокровие (в прицеле)", "X или СКМ"), ("Чутьё", "Alt"), ("Перезарядка", "R"), ("Колесо оружия", "Tab"),
                    ("Оружие", "1–6"), ("Свист", "H"), ("Вид / кинокамера", "V / держать V"), ("Сумка / карта", "I / M"),
                };
                var rows = new List<MenuRow>();
                foreach (var kv in keys) rows.Add(Info(kv.a, kv.k, ""));
                return rows;
            }, back);
        }

        /// <summary>Who recorded the sounds (Tools/sound/SOURCES.md): the CC-BY ones must be named, the CC0 ones
        /// deserve it.</summary>
        public void Credits(Action back)
        {
            OpenList("Авторы звуков", () =>
            {
                (string a, string b, string about)[] lines =
                {
                    ("Галоп по доскам", "Twigy233, CC BY 4.0", "«Horse Galloping Steady Pace SFX» (freesound.org); нарезка — congusbongus, «Horse gallop on different surfaces» (opengameart.org), CC BY 4.0."),
                    ("Галоп по земле и траве", "congusbongus, CC BY 4.0", "Сборник «Horse gallop on different surfaces» (opengameart.org); исходные записи D4XX, StephenSaldanha, MrFizzywig — CC0."),
                    ("Ржание", "AntumDeluge, CC BY", "«Zebra Whinny» (opengameart.org), из видео Peet J van Eeden (CC BY); в игре понижено до голоса лошади."),
                    ("Выстрелы", "The Free Firearm Sound Library, CC0", "Ben Jaszczak и соавторы (opengameart.org)."),
                    ("Ветер, птицы, река", "Thimras, CC0", "«Park ambiences» (opengameart.org)."),
                    ("Дождь и гром", "kindland, CC0", "«rain and thunders» (opengameart.org)."),
                    ("Сверчки", "Wolfgang_, CC0", "«Crickets Ambient Noise - loopable» (opengameart.org)."),
                    ("Костёр", "AntumDeluge, CC0", "«Fire Crackling» (opengameart.org)."),
                    ("Шаги, двери, монеты, удары, меню", "Kenney, CC0", "RPG Audio, Impact Sounds, Interface Sounds (kenney.nl)."),
                    ("Паровоз, колокол, свист", "синтез", "Сделаны в самой игре."),
                };
                var rows = new List<MenuRow>();
                foreach (var l in lines) rows.Add(Info(l.a, l.b, l.about));
                return rows;
            }, back);
        }

        public void SaveMenu(bool saving)
        {
            Game g = Game.I;
            OpenList(saving ? "Сохранить игру" : "Загрузить игру", () =>
            {
                var rows = new List<MenuRow>();
                string[] slots = saving ? new[] { "save1", "save2", "save3" } : new[] { "auto", "save1", "save2", "save3" };
                foreach (string slot in slots)
                {
                    string s = slot;
                    string info = SaveSystem.Describe(slot);
                    string name = slot == "auto" ? "Автосохранение" : "Ячейка " + slot.Substring(4);
                    if (saving) rows.Add(new MenuRow(name, () => { g.Save(s); g.Notify("Игра сохранена"); }, info ?? "пусто", "Записать текущее положение в эту ячейку."));
                    else rows.Add(new MenuRow(name, () => { Close(); g.LoadGame(s); }, info ?? "пусто", info != null ? "Загрузить это сохранение. Несохранённое будет потеряно." : "", info != null));
                }
                return rows;
            }, g.playing ? (Action)Pause : g.ToMainMenu);
        }

        // ============================================================ map

        void BuildMap()
        {
            mapRoot = UI.Node(UI.root, "MapScreen");
            UI.Stretch(mapRoot);
            Image shade = UI.Img(mapRoot, UI.white, new Color(0.05f, 0.04f, 0.03f, 0.86f));
            UI.Stretch(shade);
            Image frame = UI.Img(mapRoot, UI.rounded, UI.Ink);
            UI.PlaceCentre(frame, 0.5f, 0.5f, -180f, 0f, MapPx + 24f, MapPx + 24f);
            mapArea = UI.Node(frame.transform, "Area");
            UI.PlaceCentre(mapArea, 0.5f, 0.5f, 0f, 0f, MapPx, MapPx);
            mapImg = mapArea.gameObject.AddComponent<RawImage>();
            mapImg.raycastTarget = false;
            mapIcons = UI.Node(mapArea, "Icons");
            UI.Stretch(mapIcons);
            mapPlayer = UI.Img(mapArea, UI.arrow, UI.Paper, "Player");
            mapCursor = UI.Img(mapArea, UI.ring, UI.Accent, "Cursor");
            mapPlace = UI.Label(mapRoot, "", 44, UI.Paper, TextAnchor.UpperLeft, true);
            UI.Place(mapPlace, 0.5f, 0.5f, 310f, 420f, 520f, 60f);
            mapHint = UI.Label(mapRoot, "", 24, UI.Muted, TextAnchor.UpperLeft);
            UI.Place(mapHint, 0.5f, 0.5f, 310f, 340f, 520f, 600f);
            mapRoot.gameObject.SetActive(false);
        }

        public void Map(Action back)
        {
            Game g = Game.I;
            list.Hide();
            mapRoot.gameObject.SetActive(true);
            MapData m = g.map;
            Vector3 pp = g.player.Mounted ? g.player.mount.Pos : g.player.Pos;
            cursor = m != null ? ToMap(m, pp) : Vector2.zero;
            mapPlace.text = g.regionName;
            Begin(() =>
            {
                if (m != null) mapImg.texture = m.tex;
                Vector2 mv = Vector2.zero;
                if (GameInput.Held(Btn.Up)) mv.y += 1f;
                if (GameInput.Held(Btn.Down)) mv.y -= 1f;
                if (GameInput.Held(Btn.Left)) mv.x -= 1f;
                if (GameInput.Held(Btn.Right)) mv.x += 1f;
                cursor += mv * (Game.UDt * 420f);
                if (!GameInput.Virtual && GameInput.PointerMoved)
                {
                    Vector2 local = mapArea.InverseTransformPoint(UI.root.TransformPoint(UI.PointerOnCanvas()));
                    if (Mathf.Abs(local.x) <= MapPx * 0.5f && Mathf.Abs(local.y) <= MapPx * 0.5f) cursor = local;
                }
                cursor.x = Mathf.Clamp(cursor.x, -MapPx * 0.5f, MapPx * 0.5f);
                cursor.y = Mathf.Clamp(cursor.y, -MapPx * 0.5f, MapPx * 0.5f);
                UI.PlaceCentre(mapCursor, 0.5f, 0.5f, cursor.x, cursor.y, 30f, 30f);

                Vector3 now = g.player.Mounted ? g.player.mount.Pos : g.player.Pos;
                if (m != null)
                {
                    Vector2 pm = ToMap(m, now);
                    UI.PlaceCentre(mapPlayer, 0.5f, 0.5f, pm.x, pm.y, 30f, 30f);
                    mapPlayer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(g.player.Mounted ? g.player.mount.yaw : g.player.yaw));
                }
                int used = 0;
                string nearest = "";
                float nearD = 26f;
                foreach (Blip b in Blip.All)
                {
                    if (m == null || (b.show != null && !b.show())) continue;
                    Vector2 bp = ToMap(m, b.pos());
                    if (used >= mapBlips.Count)
                    {
                        mapBlips.Add(UI.Img(mapIcons, UI.circle, Color.white));
                        Text t = UI.Shadowed(mapIcons, "", 18, UI.Paper, TextAnchor.UpperCenter);
                        mapLabels.Add(t);
                    }
                    Image im = mapBlips[used];
                    Text lab = mapLabels[used++];
                    im.enabled = true; im.sprite = b.icon; im.color = b.color;
                    UI.PlaceCentre(im, 0.5f, 0.5f, bp.x, bp.y, b.size + 6f, b.size + 6f);
                    lab.enabled = true; lab.text = b.label;
                    UI.PlaceCentre(lab, 0.5f, 0.5f, bp.x, bp.y - 24f, 220f, 24f);
                    float d = Vector2.Distance(bp, cursor);
                    if (d < nearD && b.label != "") { nearD = d; nearest = b.label; }
                }
                for (int i = used; i < mapBlips.Count; i++) { mapBlips[i].enabled = false; mapLabels[i].enabled = false; }

                mapHint.text = (nearest != "" ? "<size=30><color=#E9DFC6>" + nearest + "</color></size>\n\n" : "")
                    + UI.Key(Btn.Accept) + " поставить или снять метку\n" + UI.Key(Btn.Back) + " закрыть\n\n"
                    + "<color=#7A705E>Метка видна на мини-карте и держит направление, пока вы до неё не доберётесь.</color>";

                if (GameInput.Pressed(Btn.Accept) || (!GameInput.Virtual && GameInput.Pressed(Btn.Fire)))
                {
                    if (m != null)
                    {
                        Vector3 w = FromMap(m, cursor);
                        if (waypoint.HasValue && U.FlatDist(waypoint.Value, w) < m.size * 0.02f) waypoint = null;
                        else waypoint = World.OnGround(w);
                        Sfx.Play2D("ui_select", 0.6f);
                    }
                }
                else if (GameInput.Pressed(Btn.Back) || GameInput.Pressed(Btn.Map)) back();
            });
        }

        static Vector2 ToMap(MapData m, Vector3 w)
        {
            return new Vector2(((w.x - m.origin.x) / m.size - 0.5f) * MapPx, ((w.z - m.origin.y) / m.size - 0.5f) * MapPx);
        }

        static Vector3 FromMap(MapData m, Vector2 p)
        {
            return new Vector3(m.origin.x + (p.x / MapPx + 0.5f) * m.size, 0f, m.origin.y + (p.y / MapPx + 0.5f) * m.size);
        }

        // ============================================================ wheel

        void BuildWheel()
        {
            wheelRoot = UI.Node(UI.root, "Wheel");
            UI.Stretch(wheelRoot);
            Image shade = UI.Img(wheelRoot, UI.white, new Color(0.05f, 0.04f, 0.03f, 0.45f));
            UI.Stretch(shade);
            Image disc = UI.Img(wheelRoot, UI.circle, new Color(0.1f, 0.09f, 0.07f, 0.8f));
            UI.PlaceCentre(disc, 0.5f, 0.5f, 0f, 0f, 760f, 760f);
            Image hub = UI.Img(wheelRoot, UI.ring, new Color(1f, 1f, 1f, 0.25f));
            UI.PlaceCentre(hub, 0.5f, 0.5f, 0f, 0f, 320f, 320f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Text t = UI.Shadowed(wheelRoot, "", 28, UI.Paper, TextAnchor.MiddleCenter, true);
                UI.PlaceCentre(t, 0.5f, 0.5f, Mathf.Sin(a) * 270f, Mathf.Cos(a) * 270f, 300f, 90f);
                wheelTexts.Add(t);
            }
            wheelDot = UI.Img(wheelRoot, UI.circle, UI.Accent);
            UI.PlaceCentre(wheelDot, 0.5f, 0.5f, 0f, 0f, 18f, 18f);
            wheelRoot.gameObject.SetActive(false);
        }
    }
}
