using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>A context action offered to the player ("E — сесть в седло").</summary>
    public class Prompt
    {
        public Btn btn;
        public string text;
        public Action action;
        public float hold;          // seconds the button must be held; 0 = a tap
        public float priority;
    }

    /// <summary>Something in the world the hero can use when near and roughly facing it.</summary>
    public class Interactable
    {
        public string text;
        public Func<Vector3> position;
        public float range = 2.2f;
        public Action<Human> use;
        public Func<bool> enabled;
        public Btn btn = Btn.Interact;
        public float hold;

        public static readonly List<Interactable> All = new List<Interactable>();

        public static Interactable Add(string text, Func<Vector3> position, Action<Human> use, float range = 2.2f, Func<bool> enabled = null, float hold = 0f)
        {
            var i = new Interactable { text = text, position = position, use = use, range = range, enabled = enabled, hold = hold };
            All.Add(i);
            return i;
        }
    }

    public class DeadEyeMark
    {
        public Actor actor;
        public Transform bone;
        public Vector3 local;
        public Vector3 World => bone != null ? bone.TransformPoint(local) : local;
    }

    /// <summary>The player's brain: turns input into the hero's intent and actions.</summary>
    public class PlayerController : Brain
    {
        public PlayerStats stats;
        public CameraRig cam;
        public Horse horse;                     // the hero's own horse
        public bool locked;                     // cutscenes and menus: no control
        public bool walkToggle, inCover, coverLow, slowmo, sensing;
        public float bloom;
        public Actor aimedActor;
        public BodyPart aimedPart;
        public Vector3 aimPoint;
        public readonly List<Prompt> prompts = new List<Prompt>();
        public readonly List<DeadEyeMark> marks = new List<DeadEyeMark>();
        public Action<Human, List<Prompt>> extraPrompts;        // other systems add their context actions here
        public Action onFired;

        Vector3 coverNormal, coverPoint;
        float holdT, markCooldown, execT, viewHold, lastShot = -9f;
        Btn holdBtn = Btn.Count;
        bool executing;
        int execIndex;

        public bool Aiming => self.aiming;

        public override void Tick(float dt)
        {
            prompts.Clear();
            if (locked || self.dead || self.knockedOut)
            {
                self.moveInput = Vector3.zero;
                self.sprint = false;
                self.aiming = false;
                if (self.mount != null) { self.mount.targetSpeed = 0f; self.mount.steering = false; }
                if (slowmo) EndSlowmo();
                return;
            }
            float udt = Game.UDt;
            Vector2 mv = GameInput.Move;
            Vector3 wish = Quaternion.Euler(0f, cam.yaw, 0f) * new Vector3(mv.x, 0f, mv.y);

            aimPoint = cam.AimPoint(out aimedActor, out aimedPart);
            if (aimedActor != null && aimedActor.dead) aimedActor = null;

            if (GameInput.Pressed(Btn.Walk)) walkToggle = !walkToggle;
            SelectWeapons();
            if (self.mount != null) Riding(dt, mv, wish); else OnFoot(dt, mv, wish);
            Context(udt);
            Combat(dt, udt);

            // camera view: tap toggles first person, hold for the cinematic camera
            if (GameInput.Held(Btn.View)) viewHold += udt;
            if (GameInput.Released(Btn.View))
            {
                if (viewHold < 0.4f && cam.mode != CamMode.Cinematic) cam.mode = cam.mode == CamMode.FirstPerson ? CamMode.Follow : CamMode.FirstPerson;
                viewHold = 0f;
            }
            if (viewHold > 0.55f && cam.mode != CamMode.Cinematic) { cam.mode = CamMode.Cinematic; viewHold = -99f; }
            if (cam.mode == CamMode.Cinematic && (GameInput.Pressed(Btn.Aim) || GameInput.Pressed(Btn.View) || GameInput.Look.sqrMagnitude > 9f)) cam.mode = CamMode.Follow;

            if (GameInput.Pressed(Btn.Whistle)) Whistle();
            sensing = GameInput.Held(Btn.Sense) && stats.HasCool && !slowmo;
            if (sensing) stats.UseCool(udt * 2f);
            bloom = Mathf.Max(0f, bloom - dt * 2.2f);
        }

        // ================================================================== movement

        void OnFoot(float dt, Vector2 mv, Vector3 wish)
        {
            if (GameInput.Pressed(Btn.Crouch) && !inCover) self.crouch = !self.crouch;
            bool moving = mv.sqrMagnitude > 0.02f;
            bool wantSprint = GameInput.Held(Btn.Sprint) && moving && !self.crouch && !self.aiming && !inCover && stats.CanSprint;
            if (wantSprint && !stats.UseStamina(dt * 6.5f)) wantSprint = false;
            if (wantSprint) stats.AddXp(1, dt * 0.25f);

            if (inCover)
            {
                // slide along the wall; pushing away from it leaves cover
                if (moving && Vector3.Dot(wish.normalized, coverNormal) > 0.65f) LeaveCover();
                else
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, coverNormal);
                    wish = tangent * Vector3.Dot(wish, tangent);
                    if (!Physics.Raycast(self.transform.position + Vector3.up * 0.5f + wish.normalized * 0.45f, -coverNormal, 1.0f, Layers.SolidMask, QueryTriggerInteraction.Ignore))
                        wish = Vector3.zero;   // the wall ends here
                }
            }
            self.moveInput = wish;
            self.sprint = wantSprint;
            self.walk = walkToggle || (mv.magnitude < 0.55f && moving);
            if (GameInput.Pressed(Btn.Jump)) { if (inCover) LeaveCover(); self.Jump(); }
            if (GameInput.Pressed(Btn.Cover))
            {
                if (inCover) LeaveCover();
                else if (!slowmo) TryCover();
            }
        }

        void Riding(float dt, Vector2 mv, Vector3 wish)
        {
            Horse h = self.mount;
            inCover = false;
            self.crouch = false;
            if (mv.sqrMagnitude > 0.03f)
            {
                bool spur = GameInput.Held(Btn.Sprint) && !h.exhausted;
                h.targetSpeed = spur ? h.maxSpeed : walkToggle || mv.magnitude < 0.55f ? Horse.WalkSpeed : Horse.CanterSpeed;
                h.steerYaw = U.Yaw(wish);
                h.steering = true;
            }
            else { h.targetSpeed = 0f; h.steering = false; }
            if (GameInput.Pressed(Btn.Jump))
            {
                if (h.speed > 4f) h.Jump();
                else if (h.BondLevel >= 2) h.Rear();
            }
        }

        // ================================================================== cover

        void TryCover()
        {
            Vector3 o = self.transform.position + Vector3.up * 0.6f;
            RaycastHit best = default;
            float bestD = 1.5f;
            bool found = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * self.transform.forward;
                if (Physics.Raycast(o, d, out RaycastHit hit, bestD, 1 << Layers.Static, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.normal.y) < 0.35f)
                {
                    best = hit; bestD = hit.distance; found = true;
                }
            }
            if (!found) return;
            coverNormal = new Vector3(best.normal.x, 0f, best.normal.z).normalized;
            coverPoint = best.point + coverNormal * 0.42f;
            coverLow = !Physics.Raycast(self.transform.position + Vector3.up * 1.5f, -coverNormal, 1.6f, Layers.SolidMask, QueryTriggerInteraction.Ignore);
            inCover = true;
            Vector3 p = coverPoint;
            p.y = self.transform.position.y;
            self.Teleport(p, U.Yaw(coverNormal));
        }

        void LeaveCover()
        {
            inCover = false;
            self.crouch = false;
            self.faceLocked = false;
        }

        // ================================================================== weapons

        void SelectWeapons()
        {
            if (GameInput.Pressed(Btn.Slot1)) self.Equip(null);
            if (GameInput.Pressed(Btn.Slot2)) EquipWhere(w => w.def.cls == WeaponClass.Revolver, 0);
            if (GameInput.Pressed(Btn.Slot3)) EquipWhere(w => w.def.Long && w.def.cls != WeaponClass.Bow, 0);
            if (GameInput.Pressed(Btn.Slot4)) EquipWhere(w => w.def.Long && w.def.cls != WeaponClass.Bow, 1);
            if (GameInput.Pressed(Btn.Slot5)) EquipWhere(w => w.def.cls == WeaponClass.Knife, 0);
            if (GameInput.Pressed(Btn.Slot6)) EquipWhere(w => w.def.cls == WeaponClass.Bow, 0);
        }

        public bool EquipWhere(Predicate<Weapon> match, int index)
        {
            int n = 0;
            foreach (Weapon w in self.weapons)
                if (match(w) && n++ == index) { self.Equip(self.weapon == w ? null : w); return true; }
            return false;
        }

        /// <summary>
        /// Where the hero's head turns: to the person he is speaking with, to someone close in front of him, and
        /// otherwise the way the camera looks - less the faster he goes, and not at all once that is behind him
        /// (HumanAnim lets such a point go instead of wringing the neck round).
        /// </summary>
        void LookAround()
        {
            Human near = Social.focus;
            float speed = self.HorizontalSpeed;
            if (near == null && speed < 2.5f && self.mount == null)
            {
                float best = 4f;
                Vector3 fwd = self.transform.forward;
                foreach (Human h in World.humans)
                {
                    if (h == self || h.dead || !h.gameObject.activeInHierarchy) continue;
                    Vector3 d = h.Pos - self.Pos;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist >= best || dist < 0.3f || Vector3.Dot(fwd, d / dist) < 0.35f) continue;
                    best = dist;
                    near = h;
                }
            }
            if (near != null)
            {
                self.lookPoint = near.HeadPos;
                self.lookStrength = 1f;
            }
            else
            {
                self.lookPoint = cam.cam.transform.position + cam.Forward * 30f;
                self.lookStrength = self.mount != null ? 0.75f : Mathf.Lerp(0.85f, 0.3f, Mathf.InverseLerp(1.6f, 6f, speed));
            }
        }

        void Combat(float dt, float udt)
        {
            Weapon w = self.weapon;
            bool gun = w != null && !w.def.Melee;
            bool wantAim = GameInput.Held(Btn.Aim) && gun;
            self.fistsUp = GameInput.Held(Btn.Aim) && !gun && Social.focus == null;

            if (wantAim && !self.aiming && (GameInput.Gamepad || GameInput.AimKey)) SnapToTarget();
            self.aiming = wantAim;
            self.aimPoint = aimPoint;
            if (self.mount == null)
            {
                bool addressing = Social.focus != null;      // turn to the person being spoken to
                self.faceLocked = wantAim || self.fistsUp || inCover || addressing;
                if (wantAim || self.fistsUp || addressing) self.faceYaw = cam.yaw;
                else if (inCover) self.faceYaw = U.Yaw(coverNormal);
                if (inCover) self.crouch = coverLow && !wantAim;
                self.crouchDepth = inCover ? 1.7f : 1f;
            }
            self.looking = !wantAim;
            LookAround();

            if (GameInput.Pressed(Btn.Reload)) self.Reload();
            if (gun && w.loaded == 0 && !self.reloading && Time.time - lastShot > 0.5f) self.Reload();

            // «Хладнокровие»
            if (slowmo) SlowmoTick(udt, w);
            else if (wantAim && GameInput.Pressed(Btn.DeadEye) && stats.HasCool && w.loaded > 0) BeginSlowmo();
            if (executing) { Execute(udt); return; }

            if (GameInput.Pressed(Btn.Melee) && !meleeTaken) { if (inCover) LeaveCover(); self.Strike(); }
            if (GameInput.Pressed(Btn.Fire))
            {
                if (!gun) { if (!self.Mounted) self.Strike(); }
                else if (slowmo && marks.Count > 0) { executing = true; execIndex = 0; execT = 0f; }
                else Shoot(wantAim);
            }
        }

        void Shoot(bool aimed)
        {
            Weapon w = self.weapon;
            float spread = 1f + bloom;
            if (!aimed)
            {
                // from the hip: turn to the camera and fire wide
                spread += 2.2f;
                if (self.mount == null) { self.yaw = cam.yaw; self.transform.rotation = Quaternion.Euler(0f, self.yaw, 0f); }
                self.aiming = true;
                self.aimPoint = aimPoint;
            }
            if (self.HorizontalSpeed > 1f) spread += self.Mounted ? 1.2f : 0.7f;
            if (self.crouch) spread *= 0.75f;
            if (slowmo) spread = 0.15f;
            if (self.Fire(aimPoint, spread))
            {
                if (self.LastHit != null) Game.I.hud.HitMarker(self.LastHit.dead);
                bloom = Mathf.Min(3f, bloom + 0.6f * w.def.recoil);
                cam.shake = Mathf.Max(cam.shake, 0.6f * w.def.recoil);
                cam.pitch -= 0.5f * w.def.recoil;
                lastShot = Time.time;
                onFired?.Invoke();
                if (slowmo && stats.coolTier < 3) EndSlowmo();
            }
        }

        void SnapToTarget()
        {
            Actor best = null;
            float bestScore = 22f;
            Vector3 o = cam.cam.transform.position, f = cam.Forward;
            foreach (Actor a in World.actors)
            {
                if (a == self || a == self.mount || a.dead) continue;
                if (a.faction != Faction.Outlaw && a.faction != Faction.Predator) continue;
                Vector3 to = a.Chest - o;
                float d = to.magnitude;
                if (d > 45f) continue;
                float ang = Vector3.Angle(f, to);
                if (ang < bestScore && World.LineOfSight(self.HeadPos, a.Chest)) { bestScore = ang; best = a; }
            }
            if (best == null) return;
            Vector3 dir = (best.Chest - o).normalized;
            cam.yaw = U.Yaw(dir);
            cam.pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
        }

        // ---- slow motion with marks

        void BeginSlowmo()
        {
            slowmo = true;
            marks.Clear();
            executing = false;
            Sfx.Play2D("ui_select", 0.5f, 0.6f);
        }

        public void EndSlowmo()
        {
            slowmo = false;
            executing = false;
            marks.Clear();
        }

        void SlowmoTick(float udt, Weapon w)
        {
            float cost = stats.coolTier >= 5 ? 6f : 9f;
            if (!stats.UseCool(udt * cost) || !self.aiming || w == null || (GameInput.Pressed(Btn.DeadEye) && !executing))
            {
                if (!executing || w == null) { EndSlowmo(); return; }
            }
            if (executing) return;
            markCooldown -= udt;
            bool canMark = marks.Count < w.loaded && aimedActor != null && aimedActor != self && aimedActor != self.mount;
            if (!canMark) return;
            bool manual = stats.coolTier >= 2;
            if (manual ? GameInput.Pressed(Btn.Cover) : markCooldown <= 0f && CountMarks(aimedActor) < 2)
            {
                Transform bone = aimedActor.transform;
                if (aimedActor is Human hu) bone = aimedPart == BodyPart.Head ? hu.rig.B(HB.Head) : hu.rig.B(HB.Chest);
                else if (aimedActor is Quadruped q) bone = aimedPart == BodyPart.Head ? q.rig.B(QB.Head) : q.rig.B(QB.Chest);
                marks.Add(new DeadEyeMark { actor = aimedActor, bone = bone, local = bone.InverseTransformPoint(aimPoint) });
                markCooldown = 0.22f;
                Sfx.Play2D("ui_move", 0.7f, 1.4f);
            }
        }

        int CountMarks(Actor a)
        {
            int n = 0;
            foreach (DeadEyeMark m in marks) if (m.actor == a) n++;
            return n;
        }

        void Execute(float udt)
        {
            execT -= udt;
            if (execT > 0f) return;
            if (execIndex >= marks.Count || self.weapon == null || self.weapon.loaded <= 0) { EndSlowmo(); return; }
            DeadEyeMark m = marks[execIndex++];
            Vector3 p = m.World;
            Vector3 dir = (p - cam.cam.transform.position).normalized;
            cam.yaw = U.Yaw(dir);
            cam.pitch = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
            self.aimPoint = p;
            self.ReadyToFire();
            if (self.Fire(p, 0.05f))
            {
                cam.shake = 0.5f; onFired?.Invoke(); lastShot = Time.time;
                if (self.LastHit != null) Game.I.hud.HitMarker(self.LastHit.dead);
            }
            execT = 0.11f;
        }

        // ================================================================== context actions

        void Whistle()
        {
            if (self.Mounted) return;
            self.anim.Whistle();
            Sfx.Play("whistle", self.HeadPos, 0.8f, 1f, 60f);
            World.MakeNoise(self.transform.position, 40f, self, false);
            if (horse != null && !horse.dead && !horse.Call(self)) Game.I.Notify("Лошадь слишком далеко и не слышит свиста");
        }

        void Context(float udt)
        {
            Vector3 pos = self.transform.position;
            Vector3 fwd = cam.Forward; fwd.y = 0f; fwd.Normalize();

            if (self.Mounted)
            {
                Horse h = self.mount;
                prompts.Add(new Prompt { btn = Btn.Interact, text = "Спешиться", action = () => self.Dismount() });
            }
            else
            {
                // the nearest horse to mount
                Horse best = null;
                float bestD = 2.6f;
                foreach (Horse h in World.horses)
                {
                    if (h.dead || h.rider != null || !h.saddled && h != horse) continue;
                    float d = U.FlatDist(h.transform.position, pos);
                    if (d < bestD) { bestD = d; best = h; }
                }
                if (best != null)
                {
                    Horse target = best;
                    bool theft = target.othersProperty && target != horse;
                    prompts.Add(new Prompt
                    {
                        btn = Btn.Interact, priority = 5f,
                        text = theft ? "Украсть лошадь" : "Сесть в седло" + (target.horseName != "" ? " (" + target.horseName + ")" : ""),
                        action = () =>
                        {
                            if (inCover) LeaveCover();
                            self.Mount(target);
                            if (theft) { target.mode = HorseMode.Idle; Game.I.law.Crime(CrimeKind.HorseTheft, target, target.Pos); }
                        }
                    });
                    if (target == horse) prompts.Add(new Prompt { btn = Btn.Melee, text = "Похлопать", action = () => target.Pat(), priority = 1f });
                }
                foreach (Interactable it in Interactable.All)
                {
                    if (it.enabled != null && !it.enabled()) continue;
                    Vector3 ip = it.position();
                    Vector3 to = ip - pos;
                    if (Mathf.Abs(to.y) > 2.2f) continue;
                    to.y = 0f;
                    float d = to.magnitude;
                    if (d > it.range) continue;
                    if (d > 0.9f && Vector3.Dot(to / d, fwd) < 0.1f) continue;
                    Interactable cap = it;
                    prompts.Add(new Prompt { btn = it.btn, text = it.text, hold = it.hold, action = () => cap.use(self), priority = 10f - d });
                }
            }
            extraPrompts?.Invoke(self, prompts);

            // one prompt per button: the highest priority wins
            prompts.Sort((a, b) => b.priority.CompareTo(a.priority));
            for (int i = prompts.Count - 1; i > 0; i--)
                for (int j = 0; j < i; j++)
                    if (prompts[j].btn == prompts[i].btn) { prompts.RemoveAt(i); break; }

            bool anyHeld = false;
            meleeTaken = false;
            foreach (Prompt p in prompts)
            {
                if (p.btn == Btn.Melee && (self.fistsUp || self.aiming)) continue;
                if (p.btn == Btn.Melee) meleeTaken = true;
                if (p.hold <= 0f)
                {
                    if (GameInput.Pressed(p.btn)) { p.action?.Invoke(); break; }
                }
                else if (GameInput.Held(p.btn))
                {
                    anyHeld = true;
                    if (holdBtn != p.btn) { holdBtn = p.btn; holdT = 0f; }
                    holdT += udt;
                    HoldProgress = holdT / p.hold;
                    if (holdT >= p.hold) { holdT = 0f; holdBtn = Btn.Count; HoldProgress = 0f; p.action?.Invoke(); }
                    break;
                }
            }
            if (!anyHeld) { holdBtn = Btn.Count; holdT = 0f; HoldProgress = 0f; }
        }

        public float HoldProgress;
        bool meleeTaken;
    }
}
