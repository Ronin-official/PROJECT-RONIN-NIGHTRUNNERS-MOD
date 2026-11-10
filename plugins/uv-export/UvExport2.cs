using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Ronin.UvExport2
{
    /// <summary>
    /// F11：把玩家当前车的 UV 线稿导出成 PNG。
    /// 黑线 = 车身/车漆，蓝线 = 玻璃。
    /// </summary>
    [BepInPlugin(Guid, "NR UV Export 2", "2.0.0")]
    public class Plugin : BasePlugin
    {
        public const string Guid = "ronin.nruvexport2";
        public static BepInEx.Logging.ManualLogSource L;
        static ConfigEntry<string> Hotkey;
        static ConfigEntry<int> SizeCfg;

        public override void Load()
        {
            L = Log;
            Hotkey = Config.Bind("Export", "Hotkey", "F11", "export UV of the player car");
            SizeCfg = Config.Bind("Export", "Size", 2048, "output texture size");
            Exporter.PluginSize = () => SizeCfg.Value;
            Harmony.CreateAndPatchAll(typeof(Tick), Guid);
            L.LogInfo("UV Export 2 loaded (F11)");
        }

        [HarmonyPatch(typeof(GodConstant), "Update")]
        internal static class Tick
        {
            static void Postfix()
            {
                try
                {
                    KeyCode k;
                    if (!Enum.TryParse<KeyCode>(Hotkey.Value, true, out k)) k = KeyCode.F11;
                    if (Input.GetKeyDown(k)) Exporter.Run();
                }
                catch { }
            }
        }
    }

    internal static class Exporter
    {
        internal static Func<int> PluginSize;
        struct Tri
        {
            public Vector2[] uv;
            public int[] tris;
            public bool glass;
        }

        static readonly List<Tri> Parts = new List<Tri>();

        internal static void Run()
        {
            try
            {
                var gc = GodConstant.Instance;
                var pc = gc != null ? gc.playerCar : null;
                var cl = pc != null ? pc.carLocal : null;
                if (cl == null) { Log("no player car"); return; }

                string carName = "car";
                try { carName = cl.carOrigin.modelType.ToString(); } catch { }

                Parts.Clear();

                foreach (var r in cl.GetComponentsInChildren<MeshRenderer>(true))
                    Collect(r, carName);
                foreach (var r in cl.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Collect(r, carName);

                if (Parts.Count == 0) { Log("no UV meshes found for " + carName); return; }

                int size = Mathf.Clamp(Plugin2Size(), 512, 4096);
                var buf = new Color32[size * size];
                var white = new Color32(255, 255, 255, 255);
                var body = new Color32(20, 20, 20, 255);
                var glassInk = new Color32(30, 90, 220, 255);
                for (int i = 0; i < buf.Length; i++) buf[i] = white;

                int lines = 0;
                foreach (var p in Parts)
                {
                    var ink = p.glass ? glassInk : body;
                    for (int t = 0; t + 2 < p.tris.Length; t += 3)
                    {
                        int a = p.tris[t], b = p.tris[t + 1], c = p.tris[t + 2];
                        if (a >= p.uv.Length || b >= p.uv.Length || c >= p.uv.Length) continue;
                        var pa = Px(p.uv[a], size); var pb = Px(p.uv[b], size); var pc2 = Px(p.uv[c], size);
                        Line(buf, size, pa.x, pa.y, pb.x, pb.y, ink);
                        Line(buf, size, pb.x, pb.y, pc2.x, pc2.y, ink);
                        Line(buf, size, pc2.x, pc2.y, pa.x, pa.y, ink);
                        lines++;
                    }
                }

                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.SetPixels32(buf);
                tex.Apply(false);

                string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "UVExport");
                Directory.CreateDirectory(dir);
                string safe = carName.Replace(' ', '_').Replace('/', '_').Replace('\\', '_').Replace(':', '_');
                string path = Path.Combine(dir, safe + "_uv.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);

                Log("wrote " + path + " (" + Parts.Count + " meshes, " + lines + " tris)");
            }
            catch (Exception e) { Log("export failed: " + e.Message); }
        }

        static int Plugin2Size()
        {
            try { return PluginSize != null ? PluginSize() : 2048; } catch { return 2048; }
        }

        static void Collect(Renderer r, string carName)
        {
            try
            {
                if (r == null || r.gameObject == null) return;
                Mesh m = null;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) m = mf.sharedMesh;
                if (m == null)
                {
                    var sm = r as SkinnedMeshRenderer;
                    if (sm != null) m = sm.sharedMesh;
                }
                if (m == null) return;
                if (!m.isReadable) return;
                var uv = m.uv;
                if (uv == null || uv.Length == 0) return;
                string meshName = (m.name ?? "").ToLowerInvariant();
                string goName = "";
                try { goName = (r.gameObject.name ?? "").ToLowerInvariant(); } catch { }
                if (SkipName(meshName) || SkipName(goName)) return;

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) return;

                int subs = Mathf.Min(m.subMeshCount, mats.Length);
                for (int si = 0; si < subs; si++)
                {
                    int cat = Classify(mats[si]);   // 0 body, 1 glass, -1 skip
                    if (cat < 0) continue;
                    bool glass = cat == 1;
                    int[] tris;
                    try { tris = m.GetTriangles(si); } catch { continue; }
                    if (tris == null || tris.Length < 3) continue;
                    Parts.Add(new Tri { uv = uv, tris = tris, glass = glass });
                }
            }
            catch { }
        }

        static readonly string[] SkipParts = {
            "wheel", "whl", "tire", "tyre", "brake", "caliper", "disc",
            "rim", "spoke", "hub", "tread",
            "interior", "seat", "dash", "steer", "pedal", "shifter",
            "gauge", "radio", "console", "carpet", "doorcard",
            "engine", "eng_", "_eng", "exhaust", "manifold", "turbo",
            "intake", "radiator", "fan", "battery", "bay", "mount",
            "suspension", "coil", "spring", "shock", "arm_",
            "undercarriage", "bottom", "chassis_movement",
            "lod1", "lod3", "badge", "emblem", "plate", "mirror_glass"
        };

        static bool SkipName(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            foreach (var k in SkipParts)
                if (n.Contains(k)) return true;
            return false;
        }

        static int Classify(Material mat)
        {
            try
            {
                string s = mat != null ? (mat.name ?? "").ToLowerInvariant() : "";
                try
                {
                    var t = mat != null ? mat.mainTexture : null;
                    if (t != null) s += " " + (t.name ?? "").ToLowerInvariant();
                }
                catch { }

                if (s.Contains("glass")) return 1;
                if (s.Contains("paint")) return 0;
                if (s.Contains("livery")) return 0;
                if (s.Contains("carbon")) return 0;
                return -1;
            }
            catch { return -1; }
        }

        static Vector2Int Px(Vector2 uv, int size)
        {
            int x = Mathf.Clamp((int)(uv.x * size), 0, size - 1);
            int y = Mathf.Clamp((int)(uv.y * size), 0, size - 1);
            return new Vector2Int(x, y);
        }

        static void Line(Color32[] buf, int size, int x0, int y0, int x1, int y1, Color32 ink)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int guard = 0;
            while (true)
            {
                if (x0 >= 0 && x0 < size && y0 >= 0 && y0 < size) buf[y0 * size + x0] = ink;
                if ((x0 == x1 && y0 == y1) || guard++ > 4000) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        static void Log(string msg)
        {
            try { if (Plugin.L != null) Plugin.L.LogInfo("[uv2] " + msg); } catch { }
        }
    }
}
