using System.Collections;
using UnityEngine;

namespace Canaan
{
    public partial class AutoTest
    {
        /// <summary>The Blender models next to each other on the studio floor, close up and from a few steps.</summary>
        IEnumerator PropsShow()
        {
            Game g = Game.I;
            var lib = Resources.Load<ModelLibrary>("Models");
            Check(lib != null && lib.entries.Count > 0, "the model library is in the build");
            if (lib == null) yield break;
            var site = new Site();
            int n = lib.entries.Count, perRow = 8;
            float x = 0f, z = 0f, rowDepth = 0f;
            int inRow = 0;
            foreach (ModelLibrary.Entry e in lib.entries)
            {
                float w = Mathf.Max(e.size.x, 0.4f) + 0.5f;
                site.Model(e.name, new Vector3(x + w * 0.5f - e.centre.x, 0f, z - e.centre.z));
                x += w;
                rowDepth = Mathf.Max(rowDepth, e.size.z);
                if (++inRow >= perRow) { inRow = 0; x = 0f; z += rowDepth + 1.2f; rowDepth = 0f; }
            }
            site.Finish(g.region, "PropsShow", new Vector3(-4f, 0f, 2f), 0f);
            Log(n + " models: " + string.Join(", ", lib.entries.ConvertAll(en => en.name + " " + en.mesh.vertexCount + "v")));
            g.clock.hour = 16.2f;
            yield return Wait(0.3f);
            Look(new Vector3(1.5f, 2.6f, -4.5f), new Vector3(1.5f, 0.5f, 3f), 55f);
            yield return null;
            Snap("props_wide");
            Look(new Vector3(-2.2f, 1.25f, 0.2f), new Vector3(-2.6f, 0.45f, 2.2f), 45f);
            yield return null;
            Snap("props_close");
        }
    }
}
