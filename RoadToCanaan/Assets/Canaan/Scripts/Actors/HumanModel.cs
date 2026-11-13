using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// Dresses a person from the pieces modelled in Blender (Tools/blender/human.py): head, hands, shirt,
    /// trousers and so on, each skinned to the game's bones. The pieces are joined into one mesh per person,
    /// moved onto that person's joints, scaled to their height and dyed: the colour alpha of a vertex names
    /// the part (skin, hair, shirt ...), its rgb the shade to multiply the dye by.
    /// </summary>
    public static class HumanModel
    {
        public const int Skin = 1, Hair = 2, Shirt = 3, Pants = 4, Boots = 5, Vest = 6, Coat = 7, Scarf = 8, Skirt = 9, Cuff = 10, Hat = 11;

        public static bool Available => Models.Has("hu_head") && Models.Has("hu_shirt") && Models.Has("hu_trousers") && Models.Has("hu_boots");

        static IEnumerable<string> Pieces(HumanSpec s)
        {
            yield return "hu_head";
            yield return "hu_hands";
            yield return "hu_shirt";
            yield return "hu_boots";
            yield return s.skirt ? "hu_skirt" : "hu_trousers";
            if (s.vest) yield return "hu_vest";
            if (s.coat) yield return "hu_coat";
            if (s.gunbelt) yield return "hu_gunbelt";
            if (s.bandana) yield return "hu_bandana";
            switch (s.hairKind)
            {
                case HairKind.Short: yield return "hu_hair_short"; break;
                case HairKind.Long: yield return "hu_hair_long"; break;
                case HairKind.Balding: yield return "hu_hair_balding"; break;
                case HairKind.Bun: yield return "hu_hair_bun"; break;
            }
            switch (s.beard)
            {
                case BeardKind.Full: yield return "hu_beard_full"; break;
                case BeardKind.Moustache: yield return "hu_beard_moustache"; break;
                case BeardKind.Goatee: yield return "hu_beard_goatee"; break;
            }
        }

        static Color32 Dye(Color32 dye, Color32 shade)
        {
            return new Color32((byte)(dye.r * shade.r / 255), (byte)(dye.g * shade.g / 255), (byte)(dye.b * shade.b / 255), 0);
        }

        /// <param name="joints">this person's joints, unscaled</param>
        /// <param name="reference">the joints the pieces were modelled on</param>
        public static Mesh Build(HumanSpec s, float k, Vector3[] joints, Vector3[] reference, string name)
        {
            var dyes = new Color32[16];
            for (int i = 0; i < dyes.Length; i++) dyes[i] = new Color32(255, 255, 255, 255);
            dyes[Skin] = s.skin; dyes[Hair] = s.hair; dyes[Shirt] = s.shirt; dyes[Pants] = s.pants; dyes[Boots] = s.boots;
            dyes[Vest] = s.vestColor; dyes[Coat] = s.coatColor; dyes[Scarf] = s.bandanaColor; dyes[Skirt] = s.skirtColor;
            bool bare = s.rolledSleeves && !s.coat;
            dyes[Cuff] = bare ? s.skin : s.shirt;
            return Combine(Pieces(s), n => Enum.TryParse(n, out HB hb) ? (int)hb : 0, dyes, k, joints, reference, name, bare ? Cuff : -1);
        }

        /// <summary>
        /// Joins skinned pieces into one mesh for one creature: weights are re-indexed to the game's bone order,
        /// vertices follow this build's joints and scale, and each part takes its dye.
        /// </summary>
        public static Mesh Combine(IEnumerable<string> pieces, Func<string, int> boneOf, Color32[] dyes, float k, Vector3[] joints, Vector3[] reference,
                                   string name, int skinPart = -1)
        {
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
            var colors = new List<Color32>(); var uvs = new List<Vector2>(); var surfs = new List<Vector4>();
            var weights = new List<BoneWeight>(); var tris = new List<int>();
            var uv = new List<Vector2>(); var sf = new List<Vector4>();
            foreach (string piece in pieces)
            {
                if (!Models.Has(piece)) continue;
                ModelLibrary.Entry e = Models.Get(piece);
                Mesh m = e.mesh;
                var map = new int[e.boneNames.Count];
                for (int i = 0; i < map.Length; i++) map[i] = boneOf(e.boneNames[i]);
                Vector3[] v = m.vertices, n = m.normals;
                Vector4[] t = m.tangents;
                Color32[] c = m.colors32;
                BoneWeight[] w = m.boneWeights;
                uv.Clear(); sf.Clear();
                m.GetUVs(0, uv); m.GetUVs(3, sf);
                int b = verts.Count;
                for (int i = 0; i < v.Length; i++)
                {
                    BoneWeight bw = w[i];
                    int b0 = map[bw.boneIndex0], b1 = map[bw.boneIndex1];
                    float w0 = bw.weight0, w1 = bw.weight1, sum = w0 + w1;
                    if (sum < 1e-4f) { w0 = 1f; w1 = 0f; sum = 1f; }
                    w0 /= sum; w1 /= sum;
                    // follow the joints of this build, then scale to this height
                    Vector3 shift = (joints[b0] - reference[b0]) * w0 + (joints[b1] - reference[b1]) * w1;
                    verts.Add((v[i] + shift) * k);
                    normals.Add(i < n.Length ? n[i] : Vector3.up);
                    tangents.Add(i < t.Length ? t[i] : new Vector4(1, 0, 0, 1));
                    Color32 col = i < c.Length ? c[i] : new Color32(255, 255, 255, 0);
                    int part = col.a < dyes.Length ? col.a : 0;
                    colors.Add(Dye(dyes[part], col));
                    uvs.Add(i < uv.Count ? uv[i] : Vector2.zero);
                    Vector4 layer = i < sf.Count ? sf[i] : new Vector4(0, 1, 0, 0);
                    if (part == skinPart) layer.x = (int)Surf.Skin;
                    surfs.Add(layer);
                    weights.Add(new BoneWeight { boneIndex0 = b0, weight0 = w0, boneIndex1 = b1, weight1 = w1 });
                }
                int[] tri = m.triangles;
                for (int i = 0; i < tri.Length; i++) tris.Add(tri[i] + b);
            }
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(3, surfs);
            mesh.SetTriangles(tris, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A rigid model with several parts dyed: dyes[part]; parts beyond the array keep their colour.</summary>
        public static GameObject Dyed(string model, string objectName, Color32[] dyes, bool twoSided = false)
        {
            Mesh src = Models.Mesh(model);
            var mesh = UnityEngine.Object.Instantiate(src);
            mesh.name = objectName;
            Color32[] c = mesh.colors32;
            for (int i = 0; i < c.Length; i++)
                c[i] = c[i].a > 0 && c[i].a < dyes.Length ? Dye(dyes[c[i].a], c[i]) : new Color32(c[i].r, c[i].g, c[i].b, 0);
            mesh.colors32 = c;
            var go = new GameObject(objectName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = twoSided ? Mats.SurfaceUVDouble : Mats.SurfaceUV;
            return go;
        }

        /// <summary>A rigid model with one part dyed (a hat in its owner's colour).</summary>
        public static GameObject Dyed(string model, string objectName, int part, Color32 dye)
        {
            Mesh src = Models.Mesh(model);
            var mesh = UnityEngine.Object.Instantiate(src);
            mesh.name = objectName;
            Color32[] c = mesh.colors32;
            for (int i = 0; i < c.Length; i++) c[i] = c[i].a == part ? Dye(dye, c[i]) : new Color32(c[i].r, c[i].g, c[i].b, 0);
            mesh.colors32 = c;
            var go = new GameObject(objectName);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.SurfaceUV;
            return go;
        }
    }
}
