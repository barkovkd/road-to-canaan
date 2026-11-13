using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>Scenarios that put generated models on a neutral stage for a look.</summary>
    public partial class AutoTest
    {
        void Look(Vector3 from, Vector3 at, float fov = 40f)
        {
            Camera cam = Game.I.cam;
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(at - from);
            cam.fieldOfView = fov;
        }

        static List<HumanSpec> SampleSpecs()
        {
            var r = new System.Random(7);
            var list = new List<HumanSpec>
            {
                new HumanSpec { hat = HatKind.Cowboy, hatColor = Pal.LeatherDark, coat = true, coatColor = Pal.LeatherTan, gunbelt = true,
                                bandana = true, beard = BeardKind.Full, hair = Pal.Hair[1], shirt = Pal.ClothGrey, pants = Pal.ClothBrown },
                new HumanSpec { female = true, height = 1.68f, hairKind = HairKind.Bun, hair = Pal.Hair[4], skirt = true, skirtColor = Pal.ClothNavy,
                                shirt = Pal.ClothCream, apron = true },
                new HumanSpec { vest = true, vestColor = Pal.ClothBlack, hat = HatKind.Derby, hatColor = Pal.ClothBlack, beard = BeardKind.Moustache,
                                shirt = Pal.ClothWhite, pants = Pal.ClothGrey, belly = 0.7f, build = 1.12f, badge = true, gunbelt = true },
                new HumanSpec { suspenders = true, rolledSleeves = true, hat = HatKind.Straw, hatColor = Pal.Hay, skin = Pal.Skin[4], hair = Pal.Hair[0],
                                shirt = Pal.ClothBlue, pants = Pal.Wool, height = 1.84f },
                new HumanSpec { female = true, height = 1.60f, hairKind = HairKind.Long, hair = Pal.Hair[3], shirt = Pal.ClothRed, pants = Pal.Denim,
                                hat = HatKind.Cowboy, hatColor = Pal.LeatherTan, gunbelt = true, build = 0.9f },
            };
            for (int i = 0; i < 5; i++) list.Add(HumanSpec.Random(r, i % 3 == 2, i % 2 == 0));
            return list;
        }

        IEnumerator Models()
        {
            var specs = SampleSpecs();
            for (int i = 0; i < specs.Count; i++)
            {
                HumanRig rig = HumanBuilder.Build(specs[i], "H" + i);
                rig.root.position = new Vector3((i - (specs.Count - 1) * 0.5f) * 0.95f, 0, 0);
                rig.root.rotation = Quaternion.Euler(0, 180f, 0);
            }
            yield return Wait(0.1f);
            Look(new Vector3(0, 1.15f, -7.6f), new Vector3(0, 0.95f, 0), 38f);
            Snap("humans_front", 1600, 600);
            Look(new Vector3(-4.5f, 1.6f, -5.2f), new Vector3(0.6f, 0.95f, 0), 38f);
            Snap("humans_angle", 1600, 600);
            Look(new Vector3(0, 1.3f, 7.6f), new Vector3(0, 0.95f, 0), 38f);
            Snap("humans_back", 1600, 600);
            Look(new Vector3(-4.2f, 1.72f, -1.55f), new Vector3(-3.8f, 1.6f, 0), 30f);
            Snap("faces", 1280, 720);

            // animals, 12 m further back
            var quads = new List<QuadSpec>
            {
                QuadSpec.Horse(0), QuadSpec.Horse(1), QuadSpec.Horse(3), QuadSpec.Horse(4), QuadSpec.Horse(6),
                QuadSpec.Ox(), QuadSpec.Cow(2), QuadSpec.Bison(), QuadSpec.Pronghorn(true), QuadSpec.Deer(true), QuadSpec.Deer(false),
                QuadSpec.Canine(true), QuadSpec.Canine(false), QuadSpec.Rabbit()
            };
            float x = -12f;
            for (int i = 0; i < quads.Count; i++)
            {
                QuadRig q = QuadBuilder.Build(quads[i], "Q" + i);
                if (i < 2) QuadBuilder.AddTack(q, i == 0 ? Pal.ClothRed : Pal.ClothBlue, Pal.Leather);
                float w = 1.1f * quads[i].scale + 0.7f;
                x += w * 0.5f;
                q.root.position = new Vector3(x, 0, 20f);
                q.root.rotation = Quaternion.Euler(0, 90f, 0);
                x += w * 0.5f + quads[i].scale * 1.6f;
            }
            HumanRig scaleRef = HumanBuilder.Build(SampleSpecs()[0], "Ref");
            scaleRef.root.position = new Vector3(-13.5f, 0, 20f);
            scaleRef.root.rotation = Quaternion.Euler(0, 180f, 0);
            yield return Wait(0.1f);
            Look(new Vector3(-5.5f, 1.5f, 9.5f), new Vector3(-5.5f, 0.9f, 20f), 44f);
            Snap("quads_left", 1600, 620);
            Look(new Vector3(9.5f, 1.3f, 10.5f), new Vector3(9.5f, 0.7f, 20f), 44f);
            Snap("quads_right", 1600, 620);
            Look(new Vector3(x - 3.2f, 0.75f, 15.2f), new Vector3(x - 3.2f, 0.3f, 20f), 40f);
            Snap("quads_small", 1280, 520);
            Look(new Vector3(-13f, 2.4f, 15.5f), new Vector3(-10.5f, 1.1f, 20f), 40f);
            Snap("horse_tack", 1280, 720);
            Look(new Vector3(-7f, 1.5f, 24.5f), new Vector3(-9.2f, 1.2f, 20f), 40f);
            Snap("horse_tack_back", 1280, 720);
        }
    }
}
