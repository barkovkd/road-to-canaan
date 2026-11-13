using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Canaan
{
    /// <summary>Root of the running game: owns the systems, the hero, the frame loop, saving and loading.</summary>
    public class Game : MonoBehaviour
    {
        public static Game I;
        /// <summary>Real-time step and clock, unaffected by slow motion. Tests step frames at a fixed rate, so
        /// Unity's own unscaled time (wall clock) cannot be used there.</summary>
        public static float UDt = 1f / 60f, UTime;

        public Camera cam;
        public Light sun;
        public Human player;
        public PlayerController ctrl;
        public PlayerStats stats = new PlayerStats();
        public Satchel sat = new Satchel();
        public CameraRig rig;
        public Hud hud;
        public Menus menus;
        public SkyWeather clock;
        public Honor honor = new Honor();
        public Law law;
        public Camp camp;
        public Region region;
        public bool playing, scriptLock, inMainMenu;
        public bool hideHud;                          // photo mode and beauty shots: the picture without the interface
        public float playSeconds;
        public readonly HashSet<string> flags = new HashSet<string>();
        public event Action<SaveData> Saving, Loaded;
        public event Action<float> Ticked;            // world systems that need a per-frame tick
        public event Action RegionReady;

        public PostFx post;

        public struct Note { public string text; public float until; }
        public readonly List<Note> notes = new List<Note>();
        float slowFx, autosaveAt = 300f, deathAt = -1f, menuOrbit;
        bool respawning;

        public bool MenuOpen => menus != null && menus.open;
        public MapData map => region != null ? region.map : null;
        public string regionName => region != null ? region.name : "";
        public bool CanSave => playing && player != null && !player.dead && Time.time - player.lastHurt > 6f && (law == null || !law.Wanted);

        void Awake()
        {
            I = this;
            Layers.Configure();
            Mats.Load();
            Application.targetFrameRate = Args.IsTest ? -1 : 60;
            QualitySettings.vSyncCount = Args.IsTest ? 0 : 1;
            GameInput.Virtual = Args.IsTest;

            CreateCameraAndLight();
            Fx.Init(transform);
            Sfx.Init(transform);
            rig = new CameraRig(cam);
            UI.Init(transform, cam);
            hud = new Hud();
            hud.Build();
            menus = new Menus();
            menus.Build();
            clock = new SkyWeather(sun, cam, transform);
            CloudLayer.Init(cam);
            law = new Law(this);
            camp = new Camp(this);

            if (Args.IsTest) gameObject.AddComponent<AutoTest>();
            else StartCoroutine(BootFlow());
        }

        void CreateCameraAndLight()
        {
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 6500f;                     // the country beyond the map (Horizon) reaches the haze
            cam.fieldOfView = 55f;
            cam.allowHDR = true;
            if (Args.Offscreen)
            {
                // tests render only when they take a picture, always into the same target so the interface keeps its layout
                cam.enabled = false;
                cam.targetTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            }
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            camGo.AddComponent<AudioListener>();

            var sunGo = new GameObject("Sun");
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.25f;
            sunGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            RenderSettings.skybox = Mats.Sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = false;      // the air is drawn by the Canaan/Atmosphere pass and CanaanAtmosphere.hlsl
            Shader.SetGlobalVector("_CanaanGrass", new Vector4(Scatter.GrassRange * 0.8f, Scatter.GrassRange, 0f, 0f));      // tufts sink into the ground at the edge of their range

            post = new PostFx(transform);
        }

        // ================================================================== flow

        IEnumerator BootFlow()
        {
            hud.FadeTo(1f, 0f);
            yield return LoadRegion("dryford");
            ToMainMenu();
        }

        /// <summary>Tears the world down and builds a region. The hero is not spawned here.</summary>
        public IEnumerator LoadRegion(string id)
        {
            ClearWorld();
            yield return null;
            SetRegion(id == "studio" ? Region.Studio() : RegionDryFord.Build());
            yield return null;
        }

        public void SetRegion(Region r)
        {
            region = r;
            camp.Setup(r);
            RegionReady?.Invoke();
        }

        void ClearWorld()
        {
            foreach (Actor a in World.actors.ToArray()) if (a != null) Destroy(a.gameObject);
            World.Clear();
            if (region != null && region.root != null) Destroy(region.root);
            region = null;
            Interactable.All.Clear();
            Blip.All.Clear();
            menus.waypoint = null;
            Blip.Add(() => menus.waypoint ?? Vector3.zero, "default", UI.Accent, "Метка", true, () => menus.waypoint.HasValue, 20f);
            player = null;
            ctrl = null;
            playing = false;
            scriptLock = false;
            deathAt = -1f;
            respawning = false;
            hud.objective = "";
            hud.wantedText = "";
            law.Reset();
            Wildlife.Reset();
            Population.Reset();
            Shops.Reset();
            camp.Exists = false;
        }

        public void ToMainMenu()
        {
            StopAllCoroutines();
            StartCoroutine(MainMenuFlow());
        }

        IEnumerator MainMenuFlow()
        {
            if (playing || region == null || region.id == "studio") yield return LoadRegion("dryford");
            inMainMenu = true;
            hud.FadeTo(0f, 1.2f);
            ShowMainMenu();
        }

        void ShowMainMenu()
        {
            string latest = SaveSystem.Latest();
            menus.OpenList("Дорога на Ханаан", () => new List<MenuRow>
            {
                new MenuRow("Продолжить", () => { menus.Close(); LoadGame(latest); }, latest != null ? SaveSystem.Describe(latest) : "", "", latest != null),
                new MenuRow("Новая игра", () => { menus.Close(); NewGame(); }, "", "Территория Вермильон, весна 1887 года. Калеб Торн сходит с последнего поезда в Сухом Броде."),
                new MenuRow("Загрузить", () => menus.SaveMenu(false)),
                new MenuRow("Управление", () => menus.Controls(ShowMainMenu)),
                new MenuRow("Авторы звуков", () => menus.Credits(ShowMainMenu)),
                new MenuRow("Выйти", () => Application.Quit()),
            }, ShowMainMenu);
        }

        public void NewGame() { StopAllCoroutines(); StartCoroutine(NewGameFlow()); }

        IEnumerator NewGameFlow()
        {
            inMainMenu = false;
            hud.FadeTo(1f, 0.5f);
            yield return WaitReal(0.6f);
            yield return LoadRegion("dryford");
            stats = new PlayerStats();
            sat = new Satchel();
            honor = new Honor();
            flags.Clear();
            playSeconds = 0f;
            clock.day = 1; clock.hour = 9f; clock.SetWeather(WeatherKind.Clear, true);
            SpawnPlayer(region.playerStart, region.playerYaw);
            SpawnPlayerHorse(region.horseStart, region.horseYaw);
            player.Give("pilgrim");
            player.Give("knife");
            sat.Add("ammo_revolver", 30);
            sat.Add("beans", 2);
            sat.Add("jerky", 2);
            sat.Add("horse_brush", 1);
            sat.Add("hay", 2);
            Loaded?.Invoke(new SaveData());
            yield return null;
            hud.FadeTo(0f, 1.5f);
            hud.Title(region.name, region.subtitle);
            Save("auto");
        }

        public void LoadGame(string slot)
        {
            SaveData d = SaveSystem.Load(slot);
            if (d == null) { Notify("Сохранение не найдено"); return; }
            StopAllCoroutines();
            StartCoroutine(LoadFlow(d));
        }

        IEnumerator LoadFlow(SaveData d)
        {
            inMainMenu = false;
            hud.FadeTo(1f, 0.4f);
            yield return WaitReal(0.5f);
            yield return LoadRegion(d.region);
            Apply(d);
            yield return null;
            hud.FadeTo(0f, 1.2f);
            hud.Title(region.name, region.subtitle, 3f);
        }

        static IEnumerator WaitReal(float seconds)
        {
            float end = UTime + seconds;
            while (UTime < end) yield return null;
        }

        // ================================================================== hero

        public static HumanSpec CalebSpec()
        {
            return new HumanSpec
            {
                height = 1.83f, build = 1.03f, skin = Pal.Skin[1], hair = Pal.Hair[1], hairKind = HairKind.Short, beard = BeardKind.Full,
                shirt = Pal.H(0x8C8577), pants = Pal.H(0x4A4038), boots = Pal.LeatherDark, vest = true, vestColor = Pal.H(0x4E3524),
                gunbelt = true, bandana = true, bandanaColor = Pal.Vermilion, hat = HatKind.Cowboy, hatColor = Pal.H(0x3A2A1E)
            };
        }

        public Human SpawnPlayer(Vector3 pos, float yaw)
        {
            player = Human.Create(CalebSpec(), pos, yaw, Faction.Player, "Калеб Торн");
            player.isPlayer = true;
            player.infiniteAmmo = false;
            player.ammoCount = id => sat.Count(id);
            player.ammoUse = (id, n) => sat.Remove(id, n);
            ctrl = new PlayerController { self = player, stats = stats, cam = rig };
            ctrl.extraPrompts = ExtraPrompts;
            player.brain = ctrl;
            player.maxHealth = stats.MaxHealthBar + stats.healthCore * PlayerStats.CoreShare;
            player.health = player.maxHealth;
            rig.target = player;
            rig.mode = CamMode.Follow;
            rig.SnapBehind(yaw);
            World.Death += OnDeath;
            playing = true;
            GameInput.SetCursor(true);
            return player;
        }

        public Horse SpawnPlayerHorse(Vector3 pos, float yaw, string breedId = "nag", string name = "Табак")
        {
            Horse h = Horse.Create(HorseDB.Get(breedId), pos, yaw, true, name);
            h.owner = player;
            ctrl.horse = h;
            Blip.Add(() => h.Pos, "horse", UI.Paper, name, true, () => h != null && !h.dead && ctrl != null && ctrl.horse == h && player != null && player.mount != h, 20f);
            return h;
        }

        void ExtraPrompts(Human h, List<Prompt> prompts)
        {
            Social.Prompts(h, prompts);
            law.Prompts(h, prompts);
            Wildlife.Prompts(h, prompts);
        }

        /// <summary>Sleep: the clock jumps, the reserves recover, the game is saved.</summary>
        public void Rest(float toHour, string note) { StartCoroutine(RestFlow(toHour, note)); }

        IEnumerator RestFlow(float toHour, string note)
        {
            scriptLock = true;
            hud.FadeTo(1f, 0.8f);
            yield return WaitReal(1f);
            clock.SkipTo(toHour);
            stats.healthCore = Mathf.Max(stats.healthCore, 90f);
            stats.staminaCore = Mathf.Max(stats.staminaCore, 90f);
            stats.coolCore = Mathf.Max(stats.coolCore, 90f);
            stats.stamina = stats.MaxStamina; stats.cool = stats.MaxCool;
            player.maxHealth = stats.MaxHealthBar + stats.healthCore * PlayerStats.CoreShare;
            player.health = player.maxHealth;
            if (ctrl.horse != null && !ctrl.horse.dead) { ctrl.horse.stamina = ctrl.horse.maxStamina; ctrl.horse.health = ctrl.horse.maxHealth; }
            yield return WaitReal(0.5f);
            hud.FadeTo(0f, 1.2f);
            scriptLock = false;
            Notify(note);
            if (!Args.IsTest) Save("auto");
        }

        void OnDeath(Actor victim, DamageInfo d)
        {
            if (player == null || d.attacker != player || victim == player) return;
            bool head = d.part == BodyPart.Head;
            stats.cool = Mathf.Min(stats.MaxCool, stats.cool + (head ? 14f : 8f));
            stats.AddXp(2, head ? 4f : 2f);
        }

        public void UseItem(string id)
        {
            ItemDef d = ItemDB.Get(id);
            if (d == null || !d.usable || sat.Count(id) <= 0 || player == null || player.dead) return;
            if (id == "gun_oil")
            {
                if (player.weapon == null || player.weapon.def.Melee) { Notify("Возьмите в руки оружие, которое нужно почистить"); return; }
                player.weapon.shotsSinceClean = 0;
                Notify("Оружие вычищено");
            }
            else
            {
                stats.Feed(d.health, d.stamina, d.cool, player);
                if (d.fullHealth) { stats.healthCore = 100f; player.maxHealth = stats.MaxHealthBar + 100f * PlayerStats.CoreShare; player.health = player.maxHealth; }
                if (d.fullStamina) { stats.staminaCore = 100f; stats.stamina = stats.MaxStamina; }
                if (d.fullCool) { stats.coolCore = 100f; stats.cool = stats.MaxCool; }
                stats.healthCore = Mathf.Max(0f, stats.healthCore);
                stats.coolCore = Mathf.Max(0f, stats.coolCore);
            }
            sat.Remove(id);
            Sfx.Play2D("pickup", 0.6f);
        }

        void Respawn()
        {
            Vector3 at = camp != null && camp.Exists ? camp.RespawnPoint : region.respawn;
            player.Revive(World.OnGround(at), region.playerYaw);
            stats.healthCore = Mathf.Max(stats.healthCore, 50f);
            stats.staminaCore = Mathf.Max(stats.staminaCore, 50f);
            stats.coolCore = Mathf.Max(stats.coolCore, 50f);
            player.maxHealth = stats.MaxHealthBar + stats.healthCore * PlayerStats.CoreShare;
            player.health = player.maxHealth;
            float lost = Mathf.Floor(stats.money * 10f) / 100f;
            stats.money -= lost;
            clock.SkipTo(Mathf.Repeat(clock.hour + 6f, 24f));
            law.OnPlayerDown();
            rig.SnapBehind(player.yaw);
            if (ctrl.horse != null && !ctrl.horse.dead) ctrl.horse.Teleport(World.OnGround(at + new Vector3(4f, 0f, 2f)), 0f);
            Notify(lost > 0f ? "Вы очнулись. Потеряно " + U.Money(lost) : "Вы очнулись");
            hud.FadeTo(0f, 1.5f);
            deathAt = -1f;
            respawning = false;
        }

        // ================================================================== saving

        public void Save(string slot)
        {
            if (player == null || region == null) return;
            var d = new SaveData
            {
                region = region.id, regionName = region.name, day = clock.day, hour = clock.hour, weather = (int)clock.weather,
                health = player.health, stamina = stats.stamina, cool = stats.cool,
                healthCore = stats.healthCore, staminaCore = stats.staminaCore, coolCore = stats.coolCore,
                healthXp = stats.healthXp, staminaXp = stats.staminaXp, coolXp = stats.coolXp,
                healthLevel = stats.healthLevel, staminaLevel = stats.staminaLevel, coolLevel = stats.coolLevel, coolTier = stats.coolTier,
                money = stats.money, honor = honor.value, bounty = law.bounty, playSeconds = playSeconds
            };
            Vector3 p = player.Mounted ? player.mount.Pos + player.mount.transform.right * -1.2f : player.Pos;
            d.px = p.x; d.py = p.y; d.pz = p.z; d.pyaw = player.yaw;
            foreach (var kv in sat.items) { d.itemIds.Add(kv.Key); d.itemCounts.Add(kv.Value); }
            foreach (Weapon w in player.weapons) { d.weaponIds.Add(w.def.id); d.weaponLoaded.Add(w.loaded); d.weaponDirt.Add(w.shotsSinceClean); }
            d.equipped = player.weapon != null ? player.weapon.def.id : "";
            Horse h = ctrl.horse;
            if (h != null && !h.dead)
            {
                d.hasHorse = true; d.horseBreed = h.breed.id; d.horseName = h.horseName; d.horseBond = h.bondXp; d.horseHealth = h.health;
                d.hx = h.Pos.x; d.hy = h.Pos.y; d.hz = h.Pos.z; d.hyaw = h.yaw;
            }
            foreach (string f in flags) d.flags.Add(f);
            Saving?.Invoke(d);
            SaveSystem.Save(slot, d);
        }

        public void Apply(SaveData d)
        {
            stats = new PlayerStats
            {
                stamina = d.stamina, cool = d.cool, healthCore = d.healthCore, staminaCore = d.staminaCore, coolCore = d.coolCore,
                healthXp = d.healthXp, staminaXp = d.staminaXp, coolXp = d.coolXp,
                healthLevel = d.healthLevel, staminaLevel = d.staminaLevel, coolLevel = d.coolLevel, coolTier = d.coolTier, money = d.money
            };
            sat = new Satchel();
            for (int i = 0; i < d.itemIds.Count; i++) sat.Add(d.itemIds[i], d.itemCounts[i]);
            honor = new Honor { value = d.honor };
            law.bounty = d.bounty;
            flags.Clear();
            foreach (string f in d.flags) flags.Add(f);
            playSeconds = d.playSeconds;
            clock.day = d.day; clock.hour = d.hour; clock.SetWeather((WeatherKind)d.weather, true);

            SpawnPlayer(World.OnGround(new Vector3(d.px, d.py, d.pz)), d.pyaw);
            player.maxHealth = stats.MaxHealthBar + stats.healthCore * PlayerStats.CoreShare;
            player.health = Mathf.Clamp(d.health, 1f, player.maxHealth);
            for (int i = 0; i < d.weaponIds.Count; i++)
            {
                Weapon w = player.Give(d.weaponIds[i]);
                if (w == null) continue;
                w.loaded = d.weaponLoaded[i];
                w.shotsSinceClean = d.weaponDirt[i];
            }
            if (d.equipped != "") player.Equip(player.Find(d.equipped));
            if (d.hasHorse)
            {
                Horse h = SpawnPlayerHorse(World.OnGround(new Vector3(d.hx, d.hy, d.hz)), d.hyaw, d.horseBreed, d.horseName);
                h.AddBond(d.horseBond);
                h.health = Mathf.Clamp(d.horseHealth, 1f, h.maxHealth);
            }
            Loaded?.Invoke(d);
        }

        // ================================================================== frame

        void Update()
        {
            UDt = Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            UTime += UDt;
            GameInput.Tick(UDt);
            menus.Tick(UDt);

            float scale = MenuOpen ? 0f : menus.wheel ? 0.15f : ctrl != null && ctrl.slowmo ? 0.25f : 1f;
            if (!Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = scale;
            float dt = Time.deltaTime;

            if (playing && !MenuOpen && player != null)
            {
                ctrl.locked = scriptLock || menus.wheel;
                stats.Tick(dt, player);
                World.Tick(dt);
                law.Tick(dt);
                Social.Tick(dt);
                Population.Tick(dt);
                Wildlife.Tick(dt);
                camp.Tick(dt);
                Ticked?.Invoke(dt);
                playSeconds += dt;

                autosaveAt -= dt;
                if (autosaveAt <= 0f) { autosaveAt = 300f; if (CanSave && !Args.IsTest) Save("auto"); }

                if (player.dead && !respawning)
                {
                    if (deathAt < 0f) { deathAt = UTime; hud.Centre("Вы погибли", 3.5f); if (ctrl.slowmo) ctrl.EndSlowmo(); }
                    if (UTime - deathAt > 2.4f) { respawning = true; hud.FadeTo(1f, 1.2f); }
                }
                if (respawning && UTime - deathAt > 4.2f) Respawn();
            }
            if (region != null) { clock.Tick(dt); RegionLights.Tick(cam.transform.position, dt); }
            Fx.Tick(dt);
            for (int i = notes.Count - 1; i >= 0; i--) if (UTime > notes[i].until) notes.RemoveAt(i);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (playing && player != null)
            {
                if (!MenuOpen) World.LateTick(dt);
                rig.Tick(UDt, !MenuOpen && !menus.wheel && !scriptLock);

                // slow motion drains the colour and closes the frame in
                slowFx = U.Damp(slowFx, ctrl.slowmo ? 1f : 0f, 8f, UDt);
                rig.fovKick = -5f * slowFx;
                post.slow = slowFx;
                post.cinema = U.Damp(post.cinema, rig.mode == CamMode.Cinematic ? 1f : 0f, 3f, UDt);
                post.focus = Vector3.Distance(cam.transform.position, player.Chest);
                float life = player.maxHealth > 0f ? player.health / player.maxHealth : 1f;
                post.hurt = U.Damp(post.hurt, player.dead ? 0.8f : Mathf.Clamp01((0.3f - life) / 0.3f), 4f, UDt);
            }
            else if (inMainMenu && region != null)
            {
                // slow orbit over the town behind the menu
                menuOrbit += UDt * 2.2f;
                Vector3 centre = region.P("menu");
                Vector3 pos = centre + Quaternion.Euler(0f, menuOrbit, 0f) * new Vector3(0f, 26f, -95f);
                float gy = World.GroundY(pos);
                if (pos.y < gy + 6f) pos.y = gy + 6f;
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(centre + Vector3.up * 4f - pos);
                cam.fieldOfView = 46f;
                post.slow = 0f; post.cinema = 0f; post.hurt = 0f;
            }
            post.Tick(clock, UDt);
            hud.visible = playing && !inMainMenu && !hideHud;
            hud.Tick(UDt);
        }

        public void Notify(string text, float seconds = 4.5f)
        {
            if (notes.Count > 0 && notes[notes.Count - 1].text == text) return;
            notes.Add(new Note { text = text, until = UTime + seconds });
            if (notes.Count > 5) notes.RemoveAt(0);
        }

        /// <summary>A solid coloured block: test obstacles, and the simplest kind of prop.</summary>
        public static GameObject Block(Vector3 centre, Vector3 size, Color32 color, float yawDeg = 0f)
        {
            var b = new MeshBuilder();
            b.Col(color);
            b.Box(Vector3.zero, size);
            GameObject go = b.ToObject("Block", I != null && I.region != null && I.region.root != null ? I.region.root.transform : null, Mats.Lit);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yawDeg, 0f));
            go.layer = Layers.Static;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }
    }
}
