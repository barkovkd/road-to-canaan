using UnityEngine;

namespace Canaan
{
    /// <summary>Creates people of the standard kinds (outlaws, lawmen, townsfolk...).</summary>
    public static class Spawner
    {
        static readonly System.Random rng = new System.Random(4711);

        public static Human Outlaw(Vector3 pos, float yaw, float skill = 0.45f, bool rifle = false)
        {
            HumanSpec spec = HumanSpec.Random(rng, false, true);
            spec.hat = HatKind.Cowboy;
            spec.hatColor = Pal.ClothBlack;
            spec.bandana = true;
            spec.bandanaColor = U.Pick(rng, new[] { Pal.ClothBlack, Pal.ClothRed, Pal.ClothNavy });
            spec.coat = rng.NextDouble() < 0.4;
            spec.coatColor = Pal.H(0x3B3530);
            Human h = Human.Create(spec, pos, yaw, Faction.Outlaw, "Бандит");
            h.rig.mask.SetActive(true);
            h.masked = true;
            h.Give(rifle ? "drover" : "pilgrim");
            if (rifle) h.Give("pilgrim");
            h.brain = new Gunman(h, skill);
            return h;
        }

        public static Human Lawman(HumanSpec spec, Vector3 pos, float yaw, string name, float skill, bool rifle)
        {
            Human h = Human.Create(spec, pos, yaw, Faction.Law, name);
            h.Give("pilgrim");
            if (rifle) h.Give("drover");
            h.brain = new Gunman(h, skill) { sightRange = 60f };
            h.maxHealth = h.health = 130f;
            return h;
        }

        public static Human Deputy(Vector3 pos, float yaw, int index)
        {
            return Lawman(Cast.Deputy(index % 3), pos, yaw, "Помощник шерифа", 0.55f, index % 2 == 0);
        }

        public static Human Civilian(Vector3 pos, float yaw, bool female = false)
        {
            HumanSpec spec = HumanSpec.Random(rng, female, false);
            return Human.Create(spec, pos, yaw, Faction.Civilian, female ? "Горожанка" : "Горожанин");
        }
    }
}
