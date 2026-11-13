using System.Collections.Generic;
using UnityEngine;

namespace Canaan
{
    /// <summary>A loaded region: its scenery, named places and map. Built from code by a region generator.</summary>
    public class Region
    {
        public string id = "studio", name = "Студия", subtitle = "";
        public GameObject root;
        public MapData map;
        public Vector3 playerStart, horseStart, campPos, respawn;
        public float playerYaw, horseYaw;
        public readonly Dictionary<string, Vector3> points = new Dictionary<string, Vector3>();
        public readonly Dictionary<string, float> yaws = new Dictionary<string, float>();

        public Vector3 P(string key) { return points.TryGetValue(key, out Vector3 p) ? p : playerStart; }
        public float Yaw(string key) { return yaws.TryGetValue(key, out float y) ? y : 0f; }
        public void Mark(string key, Vector3 p, float yaw = 0f) { points[key] = p; yaws[key] = yaw; }

        /// <summary>The flat test ground.</summary>
        public static Region Studio()
        {
            var r = new Region { root = new GameObject("Region_Studio") };
            var b = new MeshBuilder();
            b.Col(Pal.Sand);
            b.Box(new Vector3(0, -0.5f, 0), new Vector3(400, 1, 400));
            GameObject ground = b.ToObject("Ground", r.root.transform, Mats.Lit);
            ground.layer = Layers.Ground;
            var box = ground.AddComponent<BoxCollider>();
            box.center = new Vector3(0, -0.5f, 0);
            box.size = new Vector3(400, 1, 400);

            var tex = new Texture2D(8, 8, TextureFormat.RGB24, false);
            var px = new Color32[64];
            for (int i = 0; i < 64; i++) px[i] = Pal.Sand;
            tex.SetPixels32(px);
            tex.Apply();
            r.map = new MapData { tex = tex, origin = new Vector2(-200f, -200f), size = 400f };
            r.playerStart = Vector3.zero;
            r.horseStart = new Vector3(5f, 0f, 4f);
            r.horseYaw = 200f;
            r.respawn = Vector3.zero;
            return r;
        }
    }
}
