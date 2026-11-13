using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// The hero's three bars (health, stamina, cool) and their reserves ("cores"). A bar spends and refills by
    /// itself at a rate set by its reserve; the reserve drains slowly and is refilled by food, sleep and tonics.
    /// Health works as one pool: the bar on top of a slice of the reserve, so heavy damage eats into the reserve.
    /// </summary>
    public class PlayerStats
    {
        public const float CoreShare = 0.6f;   // how many hit points a full health reserve is worth

        public float stamina = 100f, cool = 100f;
        public float healthCore = 85f, staminaCore = 85f, coolCore = 85f;
        public int healthLevel = 1, staminaLevel = 1, coolLevel = 1;
        public float healthXp, staminaXp, coolXp;
        public int coolTier = 1;               // «Хладнокровие» level 1..5, unlocked by the story
        public float money = 12f;

        float sprintLock, staminaUsedAt = -9f, coolUsedAt = -9f;

        public float MaxHealthBar => 100f + 10f * (healthLevel - 1);
        public float MaxStamina => 100f + 10f * (staminaLevel - 1);
        public float MaxCool => 100f + 10f * (coolLevel - 1);
        public bool CanSprint => Time.time >= sprintLock && (stamina > 1f || staminaCore > 1f);

        public float HealthBar(Human h) { return Mathf.Clamp(h.health - healthCore * CoreShare, 0f, MaxHealthBar); }

        public void Tick(float dt, Human h)
        {
            // reserves fade with time
            healthCore = Mathf.Max(0f, healthCore - dt * 100f / (90f * 60f));
            staminaCore = Mathf.Max(0f, staminaCore - dt * 100f / (60f * 60f));
            coolCore = Mathf.Max(0f, coolCore - dt * 100f / (75f * 60f));

            if (!h.dead)
            {
                float pool = healthCore * CoreShare;
                if (h.health < pool) healthCore = h.health / CoreShare;     // damage reached the reserve
                h.maxHealth = MaxHealthBar + healthCore * CoreShare;
                if (h.health > h.maxHealth) h.health = h.maxHealth;
                if (Time.time - h.lastHurt > 4.5f)
                    h.health = Mathf.Min(h.maxHealth, h.health + dt * Mathf.Lerp(0.4f, 3.2f, healthCore / 100f));
            }
            // a bar starts to refill a moment after it was last used
            if (Time.time - staminaUsedAt > 1.2f) stamina = Mathf.Min(MaxStamina, stamina + dt * Mathf.Lerp(3f, 13f, staminaCore / 100f));
            if (Game.UTime - coolUsedAt > 2f) cool = Mathf.Min(MaxCool, cool + dt * Mathf.Lerp(0.15f, 1.6f, coolCore / 100f));
        }

        /// <summary>Spends stamina; when the bar is empty it bites into the reserve. False when nothing is left.</summary>
        public bool UseStamina(float amount)
        {
            staminaUsedAt = Time.time;
            if (stamina >= amount) { stamina -= amount; return true; }
            amount -= stamina;
            stamina = 0f;
            staminaCore = Mathf.Max(0f, staminaCore - amount * 0.5f);
            if (staminaCore <= 0f) { sprintLock = Time.time + 3f; return false; }
            return true;
        }

        public bool UseCool(float amount)
        {
            coolUsedAt = Game.UTime;
            if (cool >= amount) { cool -= amount; return true; }
            amount -= cool;
            cool = 0f;
            coolCore = Mathf.Max(0f, coolCore - amount * 0.6f);
            return coolCore > 0f;
        }

        public bool HasCool => cool > 0.5f || coolCore > 0.5f;

        public void Feed(float health, float staminaAmount, float coolAmount, Human h)
        {
            healthCore = Mathf.Min(100f, healthCore + health);
            staminaCore = Mathf.Min(100f, staminaCore + staminaAmount);
            coolCore = Mathf.Min(100f, coolCore + coolAmount);
            if (h != null && !h.dead)
            {
                h.maxHealth = MaxHealthBar + healthCore * CoreShare;
                h.health = Mathf.Min(h.maxHealth, h.health + health * CoreShare);
            }
        }

        public void AddXp(int which, float amount)
        {
            if (which == 0) { healthXp += amount; if (healthLevel < 5 && healthXp >= healthLevel * 120f) { healthXp = 0f; healthLevel++; } }
            else if (which == 1) { staminaXp += amount; if (staminaLevel < 5 && staminaXp >= staminaLevel * 120f) { staminaXp = 0f; staminaLevel++; } }
            else { coolXp += amount; if (coolLevel < 5 && coolXp >= coolLevel * 120f) { coolXp = 0f; coolLevel++; } }
        }
    }
}
