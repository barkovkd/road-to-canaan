using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Canaan.EditorTools
{
    /// <summary>
    /// Import rules for the FBX files exported from Blender, and the library asset built from them.
    /// A model arrives with two UV maps: the texture coordinates (in tiles of its surface texture) and "Surf",
    /// which holds the layer of the surface arrays. The shader reads the layer from TEXCOORD3, so the second
    /// map is moved there; a model is one mesh with one material, whatever Blender materials it had.
    /// </summary>
    public class CanaanModelImport : AssetPostprocessor
    {
        const string Dir = "Assets/Canaan/Models/";

        public override uint GetVersion() { return 5; }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Dir)) return;
            var imp = (ModelImporter)assetImporter;
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.bakeAxisConversion = true;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importVisibility = false;
            imp.importBlendShapes = true;
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.Generic;      // 'None' would drop the skin weights too
            imp.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            imp.skinWeights = ModelImporterSkinWeights.Standard;
            imp.optimizeBones = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.isReadable = true;                       // sites merge models into their own meshes; rigs rebind them
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.weldVertices = true;
            imp.generateSecondaryUV = false;
            imp.optimizeMeshPolygons = true;
            imp.optimizeMeshVertices = true;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Dir)) return;
            var done = new HashSet<Mesh>();
            // A model built facing -Y in Blender arrives facing -Z. Rigid meshes are turned half a circle here so that they
            // face +Z like everything in the game; skinned ones are turned when their rest pose is taken (RestPose).
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true)) Fix(mf.sharedMesh, done, true);
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Fix(smr.sharedMesh, done, false);
        }

        static void Fix(Mesh m, HashSet<Mesh> done, bool turn)
        {
            if (m == null || !done.Add(m)) return;
            if (turn)
            {
                Vector3[] v = m.vertices, n = m.normals;
                Vector4[] t = m.tangents;
                for (int i = 0; i < v.Length; i++) v[i] = new Vector3(-v[i].x, v[i].y, -v[i].z);
                for (int i = 0; i < n.Length; i++) n[i] = new Vector3(-n[i].x, n[i].y, -n[i].z);
                for (int i = 0; i < t.Length; i++) t[i] = new Vector4(-t[i].x, t[i].y, -t[i].z, t[i].w);
                m.vertices = v;
                if (n.Length == v.Length) m.normals = n;
                if (t.Length == v.Length) m.tangents = t;
            }
            var surf = new List<Vector2>();
            m.GetUVs(1, surf);
            var packed = new List<Vector4>(m.vertexCount);
            for (int i = 0; i < m.vertexCount; i++)
                packed.Add(surf.Count == m.vertexCount ? new Vector4(Mathf.Round(surf[i].x), surf[i].y > 0f ? surf[i].y : 1f, 0f, 0f) : new Vector4(0f, 1f, 0f, 0f));
            m.SetUVs(3, packed);
            m.SetUVs(1, (List<Vector2>)null);
            if (m.subMeshCount > 1)
            {
                int[] tris = m.triangles;
                m.subMeshCount = 1;
                m.SetTriangles(tris, 0);
            }
            if (m.colors32 == null || m.colors32.Length != m.vertexCount)
            {
                var white = new Color32[m.vertexCount];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 0);
                m.colors32 = white;
            }
            m.RecalculateBounds();
        }

        /// <summary>The skinned mesh as it stands in the imported prefab, vertices in the prefab root's space.</summary>
        static Mesh RestPose(SkinnedMeshRenderer smr, Transform root)
        {
            Mesh src = smr.sharedMesh;
            Matrix4x4[] bind = src.bindposes;
            var m = new Matrix4x4[bind.Length];
            for (int i = 0; i < bind.Length; i++) m[i] = root.worldToLocalMatrix * smr.bones[i].localToWorldMatrix * bind[i];
            Vector3[] v = src.vertices, n = src.normals;
            Vector4[] t = src.tangents;
            BoneWeight[] w = src.boneWeights;
            for (int i = 0; i < v.Length; i++)
            {
                BoneWeight bw = w[i];
                Vector3 p = Vector3.zero, nn = Vector3.zero, tt = Vector3.zero;
                void Add(int b, float k)
                {
                    if (k <= 0f) return;
                    p += m[b].MultiplyPoint3x4(v[i]) * k;
                    if (i < n.Length) nn += m[b].MultiplyVector(n[i]) * k;
                    if (i < t.Length) tt += m[b].MultiplyVector(t[i]) * k;
                }
                Add(bw.boneIndex0, bw.weight0); Add(bw.boneIndex1, bw.weight1); Add(bw.boneIndex2, bw.weight2); Add(bw.boneIndex3, bw.weight3);
                // The export faces -Z in Unity (see OnPostprocessModel): turn it to face +Z.
                v[i] = new Vector3(-p.x, p.y, -p.z);
                if (i < n.Length) { nn.Normalize(); n[i] = new Vector3(-nn.x, nn.y, -nn.z); }
                if (i < t.Length) { tt.Normalize(); t[i] = new Vector4(-tt.x, tt.y, -tt.z, t[i].w); }
            }
            var rest = new Mesh { name = src.name + "_rest" };
            rest.vertices = v;
            rest.normals = n;
            rest.tangents = t;
            rest.colors32 = src.colors32;
            var uv = new List<Vector2>();
            var sf = new List<Vector4>();
            src.GetUVs(0, uv);
            src.GetUVs(3, sf);
            rest.SetUVs(0, uv);
            rest.SetUVs(3, sf);
            rest.triangles = src.triangles;
            rest.boneWeights = w;
            rest.RecalculateBounds();
            return rest;
        }

        /// <summary>Indexes every mesh object of every FBX by its Blender object name; "name@key" empties become marks.</summary>
        public static void BuildLibrary()
        {
            const string path = "Assets/Canaan/Resources/Models.asset";
            var lib = AssetDatabase.LoadAssetAtPath<ModelLibrary>(path);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(lib, path);
            }
            lib.entries.Clear();
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub is Mesh) Object.DestroyImmediate(sub, true);
            if (Directory.Exists(Dir))
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Dir.TrimEnd('/') }))
                {
                    string file = AssetDatabase.GUIDToAssetPath(guid);
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                    if (root == null) continue;
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        var mf = t.GetComponent<MeshFilter>();
                        var smr = t.GetComponent<SkinnedMeshRenderer>();
                        Mesh mesh = mf != null ? mf.sharedMesh : smr != null ? smr.sharedMesh : null;
                        if (mesh == null) continue;
                        var e = new ModelLibrary.Entry { name = t.name, mesh = mesh, centre = mesh.bounds.center, size = mesh.bounds.size };
                        if (smr != null)
                        {
                            // The game binds the mesh to its own skeleton: keep the rest pose in model space and the bone names.
                            e.mesh = RestPose(smr, root.transform);
                            AssetDatabase.AddObjectToAsset(e.mesh, lib);
                            foreach (Transform bone in smr.bones) e.boneNames.Add(bone.name);
                            e.centre = e.mesh.bounds.center;
                            e.size = e.mesh.bounds.size;
                        }
                        foreach (Transform c in t)
                        {
                            int at = c.name.IndexOf('@');
                            if (at < 0) continue;
                            e.markNames.Add(c.name.Substring(at + 1));
                            Vector3 mp = c.localPosition;
                            e.markPoints.Add(smr != null ? mp : new Vector3(-mp.x, mp.y, -mp.z));     // rigid models are turned on import
                        }
                        lib.entries.Add(e);
                    }
                }
            EditorUtility.SetDirty(lib);
            int skinned = 0;
            foreach (ModelLibrary.Entry e in lib.entries) if (e.boneNames.Count > 0) skinned++;
            Debug.Log("CANAAN_SETUP model library: " + lib.entries.Count + " models, " + skinned + " skinned");
        }
    }
}
