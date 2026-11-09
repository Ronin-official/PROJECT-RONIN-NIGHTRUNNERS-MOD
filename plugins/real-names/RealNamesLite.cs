using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Ronin.RealNamesLite
{
    [BepInPlugin(Guid, "NR Real Names Lite", "1.0.0")]
    public class Plugin : BasePlugin
    {
        public const string Guid = "ronin.nrrealnameslite";
        internal static Plugin I;
        public static BepInEx.Logging.ManualLogSource L;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> KeepYear;

        public override void Load()
        {
            I = this;
            L = Log;
            Enabled = Config.Bind("Names", "Enabled", true, "replace fictional car names with real ones");
            KeepYear = Config.Bind("Names", "KeepYear", true, "append model year to the name");

            Harmony.CreateAndPatchAll(typeof(PatchLocaleModel), Guid);
            Harmony.CreateAndPatchAll(typeof(PatchLocaleModelType), Guid);
            Harmony.CreateAndPatchAll(typeof(PatchLocaleChassis), Guid);

            // NRMP 的 F7 刷车菜单：CarPicker.Title 直接用枚举名，单独补一刀
            try
            {
                var t = AccessTools.TypeByName("NightRunnersMP.CarPicker");
                var m = t != null ? AccessTools.Method(t, "Title") : null;
                if (m != null)
                {
                    var h = new Harmony(Guid + ".nrmp");
                    h.Patch(m, postfix: new HarmonyMethod(typeof(PatchNrmpTitle), nameof(PatchNrmpTitle.Postfix)));
                    L.LogInfo("NRMP CarPicker.Title patched (real names in F7 menu)");
                }
                else L.LogWarning("NRMP CarPicker.Title not found");
            }
            catch (Exception e) { L.LogWarning("NRMP title patch failed: " + e.Message); }

            L.LogInfo("NR Real Names Lite 1.0.1 loaded");
        }

        internal static readonly System.Collections.Generic.Dictionary<int, string> Cache =
            new System.Collections.Generic.Dictionary<int, string>();

        internal static bool On()
        {
            try { return Enabled != null && Enabled.Value; } catch { return false; }
        }

        internal static string StripYear(string s)
        {
            try
            {
                int i = s.LastIndexOf(" (");
                if (i > 0) return s.Substring(0, i);
            }
            catch { }
            return s;
        }

        internal static string WithYear(string name, int year)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (KeepYear != null && KeepYear.Value && year > 0) return name + " (" + year + ")";
            return name;
        }
    }

    /// <summary>杞﹀簱/瀛樺偍/杞﹀晢閲屾樉绀虹殑杞﹀悕璧拌繖閲岋紝鍙湪鏋勫缓 UI 鏂囨湰鏃惰皟鐢ㄣ€?/summary>

    internal static class PatchNrmpTitle
    {
        public static void Postfix(car_carOrigin.ModelType model, ref string __result)
        {
            try
            {
                if (!Plugin.On()) return;
                string real = CarNameMap.Model(model);
                if (!string.IsNullOrEmpty(real)) __result = real;
            }
            catch { }
        }
    }

    /// <summary>把 REDUX 起的新名字（K6 TURBO FLA 等）换回真实车型名。</summary>
    internal static class ReduxNames
    {
        static readonly string[][] Map =
        {
            new[] { "K6 TURBO FLA",  "Porsche 911 Turbo Flachbau (964)" },
            new[] { "K6 TURBO SLA",  "Porsche 911 Slantnose (964)" },
            new[] { "K6 TURBO GEM",  "Gemballa 911 (964)" },
            new[] { "K6 TURBO KOE",  "Koenig Specials 911 (964)" },
            new[] { "K6 TURBO STR",  "Strosek 911 (964)" },
            new[] { "K6 TURBO CAB",  "Porsche 911 Turbo Cabriolet (964)" },
            new[] { "K6 TURBO",      "Porsche 911 Turbo (964)" },
            new[] { "K6 GEM",        "Gemballa 911 (964)" },
            new[] { "K6 KOE",        "Koenig Specials 911 (964)" },
            new[] { "K6 STR",        "Strosek 911 (964)" },
            new[] { "K6 SLA",        "Porsche 911 Slantnose (964)" },
            new[] { "K6 FLA",        "Porsche 911 Turbo Flachbau (964)" },
            new[] { "K6 CAB",        "Porsche 911 Carrera Cabriolet (964)" },
            new[] { "K6",            "Porsche 911 (964)" },
            new[] { "KORSCHEN 911",  "Porsche 911" },
        };

        public static string Rewrite(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string up = name.ToUpperInvariant();
            if (up.IndexOf("K6", System.StringComparison.Ordinal) < 0 &&
                up.IndexOf("KORSCHEN", System.StringComparison.Ordinal) < 0)
                return name;

            for (int i = 0; i < Map.Length; i++)
            {
                if (up.IndexOf(Map[i][0], System.StringComparison.Ordinal) >= 0)
                    return Map[i][1];
            }
            return name;
        }
    }

    [HarmonyPatch(typeof(PlanetJem.Localization.Locale), "GetCarModelName", new Type[] { typeof(car_carOrigin), typeof(bool) })]
    internal static class PatchLocaleModel
    {
        private static void Postfix(car_carOrigin carOrigin, bool noYear, ref string __result)
        {
            try
            {
                if (!Plugin.On() || carOrigin == null) return;
                int key = (int)carOrigin.modelType;
                string cached;
                if (Plugin.Cache.TryGetValue(key, out cached))
                {
                    if (!string.IsNullOrEmpty(cached)) __result = noYear ? Plugin.StripYear(cached) : cached;
                    return;
                }

                string real = CarNameMap.Model(carOrigin.modelType);
                if (string.IsNullOrEmpty(real)) real = CarNameMap.Chassis(carOrigin.chassisType);
                if (string.IsNullOrEmpty(real)) { Plugin.Cache[key] = ""; return; }

                int year = 0;
                try { year = carOrigin.modelYear; } catch { }
                string final = Plugin.WithYear(real, year);
                Plugin.Cache[key] = final;
                __result = noYear ? real : final;
                __result = ReduxNames.Rewrite(__result);
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(PlanetJem.Localization.Locale), "GetCarModelName", new Type[] { typeof(car_carOrigin.ModelType), typeof(int), typeof(bool) })]
    internal static class PatchLocaleModelType
    {
        private static void Postfix(car_carOrigin.ModelType modelType, ref string __result)
        {
            try
            {
                if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
                string mapped = CarNameMap.Model(modelType);
                if (!string.IsNullOrEmpty(mapped)) __result = mapped;
                __result = ReduxNames.Rewrite(__result);
            }
            catch { }
        }
    }
    [HarmonyPatch(typeof(PlanetJem.Localization.Locale), "GetCarChassisName")]
    internal static class PatchLocaleChassis
    {
        private static void Postfix(car_carOrigin.ChassisType chassisType, ref string __result)
        {
            try
            {
                if (!Plugin.On()) return;
                string real = CarNameMap.Chassis(chassisType);
                if (!string.IsNullOrEmpty(real)) __result = real;
            }
            catch { }
        }
    }
}
