using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Canaan
{
    /// <summary>Everything that survives between sessions.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string region = "dryford", regionName = "", savedAt = "";
        public int day = 1;
        public float hour = 9f;
        public int weather;
        public float px, py, pz, pyaw;
        public float health, stamina, cool, healthCore, staminaCore, coolCore, healthXp, staminaXp, coolXp;
        public int healthLevel = 1, staminaLevel = 1, coolLevel = 1, coolTier = 1;
        public float money, honor, bounty;
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();
        public List<string> weaponIds = new List<string>();
        public List<int> weaponLoaded = new List<int>(), weaponDirt = new List<int>();
        public string equipped = "";
        public bool hasHorse;
        public string horseBreed = "nag", horseName = "";
        public float hx, hy, hz, hyaw, horseBond, horseHealth;
        public List<string> flags = new List<string>();
        public List<string> keys = new List<string>();       // free-form values of other systems (camp, shops...)
        public List<float> values = new List<float>();
        public float playSeconds;

        public void Set(string key, float value)
        {
            int i = keys.IndexOf(key);
            if (i >= 0) values[i] = value; else { keys.Add(key); values.Add(value); }
        }

        public float Get(string key, float fallback = 0f)
        {
            int i = keys.IndexOf(key);
            return i >= 0 ? values[i] : fallback;
        }
    }

    public static class SaveSystem
    {
        static string Dir => Path.Combine(Application.persistentDataPath, Args.IsTest ? "saves_test" : "saves");
        static string File_(string slot) { return Path.Combine(Dir, slot + ".json"); }

        public static bool Exists(string slot) { return File.Exists(File_(slot)); }

        public static void Save(string slot, SaveData data)
        {
            Directory.CreateDirectory(Dir);
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            string tmp = File_(slot) + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(File_(slot))) File.Delete(File_(slot));
            File.Move(tmp, File_(slot));
        }

        public static SaveData Load(string slot)
        {
            try
            {
                if (!Exists(slot)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(File_(slot)));
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: cannot read " + slot + ": " + e.Message);
                return null;
            }
        }

        /// <summary>One line for the load menu, or null if the slot is empty.</summary>
        public static string Describe(string slot)
        {
            SaveData d = Load(slot);
            if (d == null) return null;
            int h = Mathf.FloorToInt(d.hour), m = Mathf.FloorToInt((d.hour - h) * 60f);
            return "день " + d.day + ", " + h.ToString("00") + ":" + m.ToString("00") + " · " + d.regionName;
        }

        /// <summary>The most recently written slot, or null.</summary>
        public static string Latest()
        {
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string slot in new[] { "auto", "save1", "save2", "save3" })
            {
                if (!Exists(slot)) continue;
                DateTime t = File.GetLastWriteTime(File_(slot));
                if (t > bestTime) { bestTime = t; best = slot; }
            }
            return best;
        }

        public static void Delete(string slot) { if (Exists(slot)) File.Delete(File_(slot)); }
    }
}
