using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Canaan
{
    /// <summary>A marker on the minimap and the big map.</summary>
    public class Blip
    {
        public Func<Vector3> pos;
        public Sprite icon;
        public Color color = Color.white;
        public bool edge;                 // stays on the rim of the minimap when out of range
        public Func<bool> show;
        public float size = 22f;
        public string label = "";

        public static readonly List<Blip> All = new List<Blip>();

        public static Blip Add(Func<Vector3> pos, string icon, Color color, string label = "", bool edge = false, Func<bool> show = null, float size = 22f)
        {
            var b = new Blip { pos = pos, icon = UI.Icon(icon), color = color, label = label, edge = edge, show = show, size = size };
            All.Add(b);
            return b;
        }
    }

    /// <summary>The region map picture and how it maps to the world.</summary>
    public class MapData
    {
        public Texture2D tex;
        public Vector2 origin;     // world x,z of the texture's corner
        public float size;         // world metres across
    }

    /// <summary>Heads-up display: bars and reserves, minimap, prompts, sights, notices, subtitles.</summary>
    public class Hud
    {
        public RectTransform root;
        public bool visible = true;
        public string objective = "";
        public string wantedText = "";
        public Color wantedColor = UI.Accent;
        public float fade;                       // 0 clear .. 1 black
        float fadeTarget, fadeSpeed = 2f;

        const float MapSize = 236f, MapView = 110f;      // pixels and metres shown from the centre
        RectTransform mapRot, blipLayer;
        RawImage mapImg;
        Image playerArrow;
        Text north;
        readonly List<Image> blipPool = new List<Image>();

        Image hpBar, stBar, clBar, hpCore, stCore, clCore, horseHp, horseSt;
        RectTransform horseRow;
        Text weaponText, ammoText, moneyText, objectiveText, subtitleText, wantedLabel, titleText, subtitleSmall, centreText;
        Image wantedBack, reticle, reticleRing, hitMark, holdBar, holdBack, fadeImg, hurtImg, barTop, barBottom;
        readonly List<Text> promptTexts = new List<Text>();
        readonly List<Text> noteTexts = new List<Text>();
        readonly List<Image> noteBacks = new List<Image>();
        readonly List<Image> markPool = new List<Image>();
        readonly List<Text> barkPool = new List<Text>();

        class Bark { public Actor who; public string text; public float until; }
        readonly List<Bark> barks = new List<Bark>();
        string subtitle = "";
        float subtitleUntil, titleUntil, titleStart, hitUntil, moneyShown = -1f, moneyUntil, centreUntil;
        bool hitKill;
        float lastHealth = -1f, hurt;

        public void Build()
        {
            root = UI.Node(UI.root, "Hud");
            UI.Stretch(root);

            // ---- hurt flash and screen fade live under everything else
            hurtImg = UI.Img(root, UI.soft, new Color(0.5f, 0.02f, 0.02f, 0f), "Hurt");
            UI.Stretch(hurtImg, -500f, -400f, -500f, -400f);

            // ---- minimap
            Image frame = UI.Img(root, UI.rounded, UI.Ink, "MapFrame");
            UI.Place(frame, 0f, 0f, 36f, 36f, MapSize + 12f, MapSize + 12f);
            Image maskImg = UI.Img(frame.transform, UI.rounded, Color.white, "MapMask");
            UI.Stretch(maskImg, 6f, 6f, 6f, 6f);
            var mask = maskImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            mapRot = UI.Node(maskImg.transform, "MapRot");
            UI.PlaceCentre(mapRot, 0.5f, 0.5f, 0f, 0f, MapSize * 1.45f, MapSize * 1.45f);
            var raw = UI.Node(mapRot, "Map");
            UI.Stretch(raw);
            mapImg = raw.gameObject.AddComponent<RawImage>();
            mapImg.raycastTarget = false;
            mapImg.color = new Color(1f, 1f, 1f, 0.96f);
            blipLayer = UI.Node(maskImg.transform, "Blips");
            UI.Stretch(blipLayer);
            playerArrow = UI.Img(maskImg.transform, UI.arrow, UI.Paper, "Player");
            UI.PlaceCentre(playerArrow, 0.5f, 0.5f, 0f, 0f, 26f, 26f);
            north = UI.Shadowed(maskImg.transform, "С", 20, UI.Accent, TextAnchor.MiddleCenter, true);
            UI.PlaceCentre(north, 0.5f, 0.5f, 0f, 0f, 30f, 30f);

            // ---- bars with reserves
            float bx = 36f + MapSize + 26f;
            MakeBar("heart", UI.Health, bx, 96f, out hpBar, out hpCore);
            MakeBar("bolt", UI.Stamina, bx, 66f, out stBar, out stCore);
            MakeBar("eye", UI.Cool, bx, 36f, out clBar, out clCore);
            horseRow = UI.Node(root, "HorseRow");
            UI.Place(horseRow, 0f, 0f, bx, 132f, 260f, 24f);
            Image hi = UI.Img(horseRow, UI.Icon("horse"), UI.Paper);
            UI.Place(hi, 0f, 0.5f, 0f, 0f, 22f, 22f);
            horseHp = MiniBar(horseRow, 32f, 84f, UI.Health);
            horseSt = MiniBar(horseRow, 124f, 84f, UI.Stamina);

            // ---- weapon, prompts
            weaponText = UI.Shadowed(root, "", 24, UI.Paper, TextAnchor.LowerRight);
            UI.Place(weaponText, 1f, 0f, -44f, 196f, 520f, 34f);
            ammoText = UI.Shadowed(root, "", 34, UI.Paper, TextAnchor.LowerRight, true);
            UI.Place(ammoText, 1f, 0f, -44f, 226f, 300f, 46f);
            for (int i = 0; i < 4; i++)
            {
                Text t = UI.Shadowed(root, "", 26, UI.Muted, TextAnchor.LowerRight);
                UI.Place(t, 1f, 0f, -44f, 44f + i * 36f, 760f, 34f);
                promptTexts.Add(t);
            }
            holdBack = UI.Img(root, UI.white, new Color(0f, 0f, 0f, 0.55f), "HoldBack");
            UI.Place(holdBack, 1f, 0f, -44f, 38f, 220f, 5f);
            holdBar = UI.Bar(holdBack.transform, UI.white, UI.Paper);
            UI.Stretch(holdBar);

            // ---- sights
            reticleRing = UI.Img(root, UI.ring, new Color(1f, 1f, 1f, 0.5f), "Ring");
            UI.PlaceCentre(reticleRing, 0.5f, 0.5f, 0f, 0f, 26f, 26f);
            reticle = UI.Img(root, UI.circle, Color.white, "Dot");
            UI.PlaceCentre(reticle, 0.5f, 0.5f, 0f, 0f, 6f, 6f);
            hitMark = UI.Img(root, UI.Icon("cross"), Color.white, "Hit");
            UI.PlaceCentre(hitMark, 0.5f, 0.5f, 0f, 0f, 34f, 34f);

            // ---- texts
            objectiveText = UI.Shadowed(root, "", 28, UI.Paper, TextAnchor.LowerCenter);
            UI.Place(objectiveText, 0.5f, 0f, 0f, 210f, 1100f, 40f);
            subtitleText = UI.Shadowed(root, "", 30, UI.Paper, TextAnchor.LowerCenter);
            UI.Place(subtitleText, 0.5f, 0f, 0f, 120f, 1200f, 80f);
            centreText = UI.Shadowed(root, "", 30, UI.Paper, TextAnchor.MiddleCenter);
            UI.PlaceCentre(centreText, 0.5f, 0.5f, 0f, -150f, 900f, 50f);
            moneyText = UI.Shadowed(root, "", 32, UI.Paper, TextAnchor.UpperRight, true);
            UI.Place(moneyText, 1f, 1f, -44f, -40f, 400f, 44f);
            wantedBack = UI.Img(root, UI.roundedSmall, UI.Accent, "Wanted");
            UI.Place(wantedBack, 0.5f, 1f, 0f, -36f, 560f, 46f);
            wantedLabel = UI.Label(wantedBack.transform, "", 26, UI.Paper, TextAnchor.MiddleCenter, true);
            UI.Stretch(wantedLabel);
            titleText = UI.Shadowed(root, "", 76, UI.Paper, TextAnchor.MiddleCenter, true);
            UI.PlaceCentre(titleText, 0.5f, 0.74f, 0f, 0f, 1500f, 100f);
            subtitleSmall = UI.Shadowed(root, "", 30, UI.Muted, TextAnchor.MiddleCenter);
            UI.PlaceCentre(subtitleSmall, 0.5f, 0.74f, 0f, -70f, 1500f, 40f);
            for (int i = 0; i < 5; i++)
            {
                Image back = UI.Img(root, UI.roundedSmall, UI.Ink, "Note");
                UI.Place(back, 0f, 1f, 36f, -36f - i * 52f, 620f, 46f);
                Text t = UI.Label(back.transform, "", 24, UI.Paper, TextAnchor.MiddleLeft);
                UI.Stretch(t, 16f, 2f, 12f, 2f);
                noteBacks.Add(back);
                noteTexts.Add(t);
            }

            // ---- cinematic bars and fade on top
            barTop = UI.Img(root, UI.white, Color.black, "BarTop");
            barBottom = UI.Img(root, UI.white, Color.black, "BarBottom");
            fadeImg = UI.Img(root, UI.white, new Color(0f, 0f, 0f, 0f), "Fade");
            UI.Stretch(fadeImg);
        }

        void MakeBar(string icon, Color color, float x, float y, out Image bar, out Image core)
        {
            RectTransform row = UI.Node(root, "Bar_" + icon);
            UI.Place(row, 0f, 0f, x, y, 270f, 24f);
            Image ic = UI.Img(row, UI.Icon(icon), color);
            UI.Place(ic, 0f, 0.5f, 0f, 0f, 22f, 22f);
            Image back = UI.Img(row, UI.roundedSmall, new Color(0f, 0f, 0f, 0.6f));
            UI.Place(back, 0f, 0.5f, 32f, 0f, 176f, 12f);
            bar = UI.Bar(back.transform, UI.roundedSmall, color);
            UI.Stretch(bar, 2f, 2f, 2f, 2f);
            Image coreBack = UI.Img(row, UI.diamond, new Color(0f, 0f, 0f, 0.7f));
            UI.Place(coreBack, 0f, 0.5f, 216f, 0f, 26f, 26f);
            core = UI.Img(coreBack.transform, UI.diamond, color);
            UI.Stretch(core, 3f, 3f, 3f, 3f);
            core.type = Image.Type.Filled;
            core.fillMethod = Image.FillMethod.Vertical;
            core.fillOrigin = 0;
        }

        static Image MiniBar(Transform parent, float x, float w, Color color)
        {
            Image back = UI.Img(parent, UI.roundedSmall, new Color(0f, 0f, 0f, 0.6f));
            UI.Place(back, 0f, 0.5f, x, 0f, w, 10f);
            Image bar = UI.Bar(back.transform, UI.roundedSmall, color);
            UI.Stretch(bar, 2f, 2f, 2f, 2f);
            return bar;
        }

        // ------------------------------------------------------------ messages

        public void Subtitle(string speaker, string line, float seconds = 4f)
        {
            subtitle = string.IsNullOrEmpty(speaker) ? line : "<color=#C9412B><b>" + speaker + ":</b></color> " + line;
            subtitleUntil = Game.UTime + seconds;
        }

        public void Title(string big, string small = "", float seconds = 4.5f)
        {
            titleText.text = big;
            subtitleSmall.text = small;
            titleStart = Game.UTime;
            titleUntil = Game.UTime + seconds;
        }

        public void Centre(string text, float seconds = 2.5f) { centreText.text = text; centreUntil = Game.UTime + seconds; }

        /// <summary>A line floating over someone's head (and in the subtitles if they are close).</summary>
        public void Say(Actor who, string text, float seconds = 3.5f)
        {
            barks.RemoveAll(b => b.who == who);
            barks.Add(new Bark { who = who, text = text, until = Game.UTime + seconds });
        }

        public void HitMarker(bool kill) { hitUntil = Game.UTime + (kill ? 0.45f : 0.18f); hitKill = kill; }

        public void FadeTo(float target, float seconds) { fadeTarget = target; fadeSpeed = seconds <= 0f ? 1000f : 1f / seconds; }
        public bool FadeDone => Mathf.Approximately(fade, fadeTarget);

        // ------------------------------------------------------------ per frame

        public void Tick(float udt)
        {
            Game g = Game.I;
            Human p = g.player;
            fade = Mathf.MoveTowards(fade, fadeTarget, udt * fadeSpeed);
            fadeImg.color = new Color(0f, 0f, 0f, fade);
            fadeImg.enabled = fade > 0.001f;

            float lb = g.rig.letterbox;
            float bh = lb * 120f;
            UI.Place(barTop, 0f, 1f, 0f, 0f, 4000f, bh);
            UI.Place(barBottom, 0f, 0f, 0f, 0f, 4000f, bh);
            barTop.enabled = barBottom.enabled = lb > 0.01f;

            bool show = visible && p != null && lb < 0.5f && !g.MenuOpen;
            if (root.gameObject.activeSelf != (visible && p != null)) root.gameObject.SetActive(visible && p != null);
            if (p == null) return;
            foreach (Transform child in root)
            {
                bool always = child == fadeImg.transform || child == barTop.transform || child == barBottom.transform || child == titleText.transform
                              || child == subtitleSmall.transform || child == subtitleText.transform;
                if (!always && child.gameObject.activeSelf != show) child.gameObject.SetActive(show);
            }
            TickTexts(udt, g, p);
            if (!show) return;

            PlayerStats s = g.stats;
            hpBar.fillAmount = s.HealthBar(p) / s.MaxHealthBar;
            stBar.fillAmount = s.stamina / s.MaxStamina;
            clBar.fillAmount = s.cool / s.MaxCool;
            hpCore.fillAmount = s.healthCore / 100f;
            stCore.fillAmount = s.staminaCore / 100f;
            clCore.fillAmount = s.coolCore / 100f;
            Pulse(hpCore, s.healthCore, UI.Health);
            Pulse(stCore, s.staminaCore, UI.Stamina);
            Pulse(clCore, s.coolCore, UI.Cool);

            Horse h = p.mount != null ? p.mount : g.ctrl.horse;
            bool horseNear = h != null && !h.dead && (p.mount == h || Vector3.Distance(h.Pos, p.Pos) < 18f);
            horseRow.gameObject.SetActive(horseNear);
            if (horseNear) { horseHp.fillAmount = h.health / h.maxHealth; horseSt.fillAmount = h.stamina / h.maxStamina; }

            // hurt flash
            if (lastHealth >= 0f && p.health < lastHealth - 0.5f) hurt = Mathf.Min(1f, hurt + (lastHealth - p.health) / 40f);
            lastHealth = p.health;
            hurt = Mathf.Max(0f, hurt - udt * 1.2f);
            float low = s.HealthBar(p) < 1f && s.healthCore < 35f ? 0.25f + 0.12f * Mathf.Sin(Game.UTime * 5f) : 0f;
            hurtImg.color = new Color(0.5f, 0.02f, 0.02f, Mathf.Clamp01(hurt * 0.7f + low));

            TickWeapon(g, p);
            TickPrompts(g);
            TickSights(g, p);
            TickMap(g, p);
        }

        static void Pulse(Image core, float value, Color color)
        {
            core.color = value < 20f ? Color.Lerp(color, Color.white, 0.5f + 0.5f * Mathf.Sin(Game.UTime * 6f)) : color;
        }

        void TickTexts(float udt, Game g, Human p)
        {
            bool sub = Game.UTime < subtitleUntil;
            subtitleText.text = sub && !g.MenuOpen ? subtitle : "";
            objectiveText.text = objective;
            centreText.enabled = Game.UTime < centreUntil;

            float ta = 0f;
            if (Game.UTime < titleUntil)
                ta = Mathf.Clamp01((Game.UTime - titleStart) / 0.8f) * Mathf.Clamp01((titleUntil - Game.UTime) / 1.2f);
            titleText.color = new Color(UI.Paper.r, UI.Paper.g, UI.Paper.b, ta);
            subtitleSmall.color = new Color(UI.Muted.r, UI.Muted.g, UI.Muted.b, ta);

            for (int i = 0; i < noteTexts.Count; i++)
            {
                bool on = i < g.notes.Count;
                noteBacks[i].gameObject.SetActive(on && visible && !g.MenuOpen);
                if (on) noteTexts[i].text = g.notes[i].text;
            }

            bool wanted = !string.IsNullOrEmpty(wantedText);
            wantedBack.enabled = wanted;
            wantedBack.color = wantedColor;
            wantedLabel.text = wanted ? wantedText : "";

            if (moneyShown < 0f) moneyShown = g.stats.money;
            if (!Mathf.Approximately(moneyShown, g.stats.money)) { moneyShown = g.stats.money; moneyUntil = Game.UTime + 5f; }
            moneyText.text = Game.UTime < moneyUntil ? U.Money(g.stats.money) : "";

            // lines over heads
            Camera cam = g.cam;
            int used = 0;
            for (int i = barks.Count - 1; i >= 0; i--)
            {
                Bark b = barks[i];
                if (Game.UTime > b.until || b.who == null) { barks.RemoveAt(i); continue; }
                float d = Vector3.Distance(cam.transform.position, b.who.Pos);
                if (d > 28f || !UI.WorldToCanvas(cam, b.who.HeadPos + Vector3.up * 0.38f, out Vector2 sp)) continue;
                if (used >= barkPool.Count)
                {
                    Text t = UI.Shadowed(root, "", 24, UI.Paper, TextAnchor.LowerCenter);
                    t.rectTransform.SetSiblingIndex(1);
                    barkPool.Add(t);
                }
                Text bt = barkPool[used++];
                bt.gameObject.SetActive(visible && !g.MenuOpen);
                bt.text = b.text;
                bt.fontSize = Mathf.RoundToInt(Mathf.Lerp(27f, 19f, Mathf.InverseLerp(4f, 26f, d)));
                UI.PlaceCentre(bt, 0.5f, 0.5f, sp.x, sp.y + 14f, 520f, 60f);
            }
            for (int i = used; i < barkPool.Count; i++) barkPool[i].gameObject.SetActive(false);
        }

        void TickWeapon(Game g, Human p)
        {
            Weapon w = p.weapon;
            if (w == null) { weaponText.text = ""; ammoText.text = ""; return; }
            weaponText.text = w.def.name + (w.Condition < 0.85f ? "  <color=#C9412B>(грязное)</color>" : "");
            ammoText.text = w.def.Melee ? "" : w.loaded + "  <size=22><color=#B3A78C>" + g.sat.Count(w.def.ammo) + "</color></size>";
        }

        void TickPrompts(Game g)
        {
            List<Prompt> ps = g.ctrl.prompts;
            float holdProgress = g.ctrl.HoldProgress;
            for (int i = 0; i < promptTexts.Count; i++)
            {
                if (i < ps.Count)
                    promptTexts[i].text = UI.Key(ps[i].btn) + "  " + ps[i].text + (ps[i].hold > 0f ? "  <size=18>(держать)</size>" : "");
                else promptTexts[i].text = "";
            }
            holdBack.enabled = holdProgress > 0.01f;
            holdBar.enabled = holdProgress > 0.01f;
            holdBar.fillAmount = holdProgress;
        }

        void TickSights(Game g, Human p)
        {
            PlayerController c = g.ctrl;
            bool aiming = p.aiming && p.weapon != null && !p.weapon.def.Melee && g.rig.mode != CamMode.Cinematic;
            reticle.enabled = aiming;
            reticleRing.enabled = aiming;
            if (aiming)
            {
                bool hostile = c.aimedActor != null && (Factions.Hostile(p, c.aimedActor) || c.aimedActor.faction == Faction.Predator);
                Color col = hostile ? UI.Accent : Color.white;
                reticle.color = col;
                reticleRing.color = new Color(col.r, col.g, col.b, 0.5f);
                float sz = 22f + c.bloom * 22f + (p.HorizontalSpeed > 1f ? 14f : 0f);
                reticleRing.rectTransform.sizeDelta = Vector2.Lerp(reticleRing.rectTransform.sizeDelta, new Vector2(sz, sz), 0.35f);
            }
            hitMark.enabled = Game.UTime < hitUntil;
            hitMark.color = hitKill ? UI.Accent : Color.white;

            int n = 0;
            if (c.slowmo)
            {
                foreach (DeadEyeMark m in c.marks)
                {
                    if (!UI.WorldToCanvas(g.cam, m.World, out Vector2 sp)) continue;
                    if (n >= markPool.Count) markPool.Add(UI.Img(root, UI.Icon("cross"), UI.Accent, "Mark"));
                    Image im = markPool[n++];
                    im.enabled = true;
                    UI.PlaceCentre(im, 0.5f, 0.5f, sp.x, sp.y, 30f, 30f);
                }
            }
            for (int i = n; i < markPool.Count; i++) markPool[i].enabled = false;
        }

        void TickMap(Game g, Human p)
        {
            MapData map = g.map;
            float yaw = g.rig.yaw;
            Vector3 pp = p.mount != null ? p.mount.Pos : p.Pos;
            if (map != null && map.tex != null)
            {
                mapImg.texture = map.tex;
                float view = MapView * 1.45f;       // the rotated layer is larger than the window
                float u = (pp.x - map.origin.x) / map.size, v = (pp.z - map.origin.y) / map.size, e = view / map.size;
                mapImg.uvRect = new Rect(u - e, v - e, e * 2f, e * 2f);
            }
            mapRot.localRotation = Quaternion.Euler(0f, 0f, yaw);
            playerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, yaw - (p.mount != null ? p.mount.yaw : p.yaw));
            float half = MapSize * 0.5f;
            Vector2 nd = Rot(new Vector2(0f, 1f), yaw) * (half - 14f);
            north.rectTransform.anchoredPosition = nd;

            int used = 0;
            float scale = half / MapView;
            foreach (Blip b in Blip.All)
            {
                if (b.show != null && !b.show()) continue;
                Vector3 wp = b.pos();
                Vector2 d = Rot(new Vector2(wp.x - pp.x, wp.z - pp.z), yaw) * scale;
                float lim = half - 12f;
                bool outside = Mathf.Abs(d.x) > lim || Mathf.Abs(d.y) > lim;
                if (outside && !b.edge) continue;
                if (outside) d *= lim / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                if (used >= blipPool.Count) blipPool.Add(UI.Img(blipLayer, UI.circle, Color.white, "Blip"));
                Image im = blipPool[used++];
                im.enabled = true;
                im.sprite = b.icon;
                im.color = b.color;
                float sz = outside ? b.size * 0.8f : b.size;
                UI.PlaceCentre(im, 0.5f, 0.5f, d.x, d.y, sz, sz);
            }
            for (int i = used; i < blipPool.Count; i++) blipPool[i].enabled = false;
        }

        /// <summary>Rotates a world offset (x east, y north) into minimap space for a camera heading.</summary>
        static Vector2 Rot(Vector2 v, float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
