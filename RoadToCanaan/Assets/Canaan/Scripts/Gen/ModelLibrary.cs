using System;
using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>
    /// The models made in Blender (Tools/blender/*.py), indexed by name. The asset is written by CanaanSetup
    /// from the FBX files in Assets/Canaan/Models and loaded from Resources.
    /// </summary>
    public class ModelLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public Mesh mesh;
            public Vector3 centre, size;                 // bounds in model space
            public List<string> boneNames = new List<string>();   // skinned models: the bone each weight index means
            public List<string> markNames = new List<string>();
            public List<Vector3> markPoints = new List<Vector3>();

            public bool Mark(string key, out Vector3 p)
            {
                int i = markNames.IndexOf(key);
                p = i >= 0 ? markPoints[i] : Vector3.zero;
                return i >= 0;
            }
        }

        public List<Entry> entries = new List<Entry>();
    }

    public static class Models
    {
        static Dictionary<string, ModelLibrary.Entry> map;

        static void Load()
        {
            map = new Dictionary<string, ModelLibrary.Entry>();
            var lib = Resources.Load<ModelLibrary>("Models");
            if (lib == null) { Debug.LogWarning("Models: no library asset — Blender models are not in this build"); return; }
            foreach (ModelLibrary.Entry e in lib.entries) if (e.mesh != null) map[e.name] = e;
        }

        public static bool Has(string name)
        {
            if (map == null) Load();
            return map.ContainsKey(name);
        }

        public static ModelLibrary.Entry Get(string name)
        {
            if (map == null) Load();
            if (map.TryGetValue(name, out ModelLibrary.Entry e)) return e;
            Debug.LogError("Models: no model named " + name);
            return null;
        }

        public static Mesh Mesh(string name) { return Get(name)?.mesh; }

        /// <summary>A model as its own object (things that move or get picked up). Static dressing goes through Site.Model.</summary>
        public static GameObject Spawn(string name, Transform parent, Vector3 localPos, Quaternion localRot, bool shadows = true)
        {
            ModelLibrary.Entry e = Get(name);
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            if (e == null) return go;
            go.AddComponent<MeshFilter>().sharedMesh = e.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.SurfaceUV;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
