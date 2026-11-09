using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Ronin.NeonFlow
{
    /// <summary>
    /// 0.11.0  回滚版行为：
    ///  · 玩家自己的车：渐变（车库/街道都生效）
    ///  · 其他玩家车：走原版逻辑，会显示粉色/固色底盘灯（已知副作用，接受）
    ///  · 不做字段改写、不做本地模式持久化
    /// </summary>
    [BepInPlugin(Guid, "NR Neon Flow", "0.11.0")]
    public class Plugin : BasePlugin
    {
        public const string Guid = "ronin.nrneonflow";
        public static BepInEx.Logging.ManualLogSource L;

        internal static ConfigEntry<int>[] Prices;
        internal static ConfigEntry<float> FlowSpeed;
        internal static ConfigEntry<float> Intensity;

        public const int ModeCount = 5;
        public const int ModeRainbow = 4;
        public const int BaseCount = 14;
        public const int Placeholder = 13;

        public static CarLight Current;
        public static int NeonPartValue = -999999;

        static CarLight _playerLight;
        static int _playerIdx = -1;

        static readonly Color[][] Palettes = new Color[][]
        {
            new Color[] { new Color(1f,0.05f,0.1f), new Color(0.1f,0.75f,1f) },
            new Color[] { new Color(1f,0.1f,0.1f), new Color(0.1f,1f,0.35f), new Color(0.15f,0.35f,1f) },
            new Color[] { new Color(1f,0.1f,0.1f), new Color(1f,0.85f,0.1f), new Color(0.15f,1f,0.3f), new Color(0.2f,0.4f,1f) },
            new Color[] { new Color(1f,0.1f,0.1f), new Color(1f,0.6f,0.05f), new Color(0.95f,1f,0.1f), new Color(0.1f,0.9f,0.9f), new Color(0.6f,0.15f,1f) },
            null
        };

        static readonly string[] Names = { "Duo Flow", "Tri Flow", "Quad Flow", "Quint Flow", "Rainbow Flow" };

        static readonly Harmony H = new Harmony(Guid);

        public override void Load()
        {
            L = Log;
            Prices = new ConfigEntry<int>[ModeCount];
            Prices[0] = Config.Bind("Shop", "PriceDuo", 750000, "2 colors");
            Prices[1] = Config.Bind("Shop", "PriceTri", 1000000, "3 colors");
            Prices[2] = Config.Bind("Shop", "PriceQuad", 1250000, "4 colors");
            Prices[3] = Config.Bind("Shop", "PriceQuint", 1500000, "5 colors");
            Prices[4] = Config.Bind("Shop", "PriceRainbow", 1750000, "full spectrum");
            FlowSpeed = Config.Bind("Visual", "FlowSpeed", 0.35f, "flow speed");
            Intensity = Config.Bind("Visual", "Intensity", 2.5f, "intensity");

            try { NeonPartValue = (int)CarPart.PartType.misc_neonsExt; } catch { }

            Patch(typeof(CountPatch));
            Patch(typeof(BeginPatch));
            Patch(typeof(LabelPatch));
            Patch(typeof(TitlePatch));
            Patch(typeof(PricePatch));
            Patch(typeof(PricePropPatch));
            Patch(typeof(NameProbe));
            Patch(typeof(ApplyIndicesPatch));
            Patch(typeof(UpdateNeonPatch));
            Patch(typeof(ForceActivePatch));
            Patch(typeof(CarLatePatch));
            Patch(typeof(LightLatePatch));

            int n = 0;
            foreach (var m in H.GetPatchedMethods()) n++;
            L.LogInfo("NeonFlow 0.11.0 loaded, modes=" + ModeCount + ", patched=" + n);
        }

        void Patch(Type t)
        {
            try { H.CreateClassProcessor(t, true).Patch(); }
            catch (Exception e) { L.LogError("patch FAILED " + t.Name + ": " + e.Message); }
        }

        public static int CustomIndex(int index) { return index - BaseCount; }
        public static bool IsCustom(int index) { return index >= BaseCount && index < BaseCount + ModeCount; }

        public static int EnumValue(object o)
        {
            try { return o == null ? -999999 : Convert.ToInt32(o); }
            catch { return -999999; }
        }

        public static Color Sample(int index, float t)
        {
            int mode = CustomIndex(index);
            if (mode < 0 || mode >= ModeCount) return Color.white;
            if (mode == ModeRainbow)
                return Color.HSVToRGB(Mathf.Repeat(FlowSpeed.Value * t, 1f), 1f, 1f);

            var pal = Palettes[mode];
            float phase = Mathf.Repeat(FlowSpeed.Value * t, 1f) * pal.Length;
            int i0 = Mathf.FloorToInt(phase) % pal.Length;
            int i1 = (i0 + 1) % pal.Length;
            float k = phase - Mathf.Floor(phase);
            return Color.Lerp(pal[i0], pal[i1], k);
        }

        static bool IsPlayerCar(CarLocalCustom car)
        {
            if (car == null) return false;
            try
            {
                var gc = GodConstant.Instance;
                var pc = gc != null ? gc.playerCar : null;
                if (pc == null) return false;
                try { var local = pc.carLocal; if (local != null && local == car) return true; } catch { }
                try { if (pc.gameObject != null && car.gameObject != null && pc.gameObject == car.gameObject) return true; } catch { }
                try { var p = pc.GetComponentInParent<CarLocalCustom>(); if (p != null && p == car) return true; } catch { }
                try { var c = pc.GetComponentInChildren<CarLocalCustom>(); if (c != null && c == car) return true; } catch { }
            }
            catch { }
            return false;
        }

        static bool PlayerHasCustom()
        {
            try
            {
                var pc = GodConstant.Instance != null ? GodConstant.Instance.playerCar : null;
                var cl = pc != null ? pc.carLocal : null;
                return cl != null && IsCustom(cl.car_neonExtColorInt);
            }
            catch { return false; }
        }

        [HarmonyPatch(typeof(PlanetJem.Customization.PartsShopExtrasBrowse), "get_Count")]
        internal static class CountPatch
        {
            static void Postfix(PlanetJem.Customization.PartsShopExtrasBrowse __instance, ref int __result)
            {
                if (EnumValue(__instance.PartType) != NeonPartValue) return;
                __result = BaseCount + ModeCount;
            }
        }

        [HarmonyPatch(typeof(PlanetJem.Customization.PartsShopExtrasBrowse), "TryBegin")]
        internal static class BeginPatch
        {
            static void Postfix(PlanetJem.Customization.PartsShopExtrasBrowse __instance)
            {
                try
                {
                    if (EnumValue(__instance.PartType) == NeonPartValue)
                        __instance.count_ = BaseCount + ModeCount;
                }
                catch { }
            }
        }

        [HarmonyPatch(typeof(PlanetJem.Customization.PartsShopExtrasBrowse), "get_Label")]
        internal static class LabelPatch
        {
            static void Postfix(PlanetJem.Customization.PartsShopExtrasBrowse __instance, ref string __result)
            {
                int idx = __instance.Index;
                if (!IsCustom(idx)) return;
                __result = Names[CustomIndex(idx)];
            }
        }

        [HarmonyPatch(typeof(PlanetJem.Customization.PartsShopExtrasBrowse), "get_Title")]
        internal static class TitlePatch
        {
            static void Postfix(PlanetJem.Customization.PartsShopExtrasBrowse __instance, ref string __result)
            {
                int idx = __instance.Index;
                if (!IsCustom(idx)) return;
                __result = Names[CustomIndex(idx)];
            }
        }

        [HarmonyPatch(typeof(PlanetJem.Customization.PartsShopExtrasBrowse), "PriceForIndex")]
        internal static class PricePatch
        {
            static void Postfix(int index, ref int __result)
            {
                if (!IsCustom(index)) return;
                __result = Prices[CustomIndex(index)].Value;
            }
        }

        [HarmonyPatch(typeof(PlanetJem.Customization.PartsShopExtrasBrowse), "get_Price")]
        internal static class PricePropPatch
        {
            static void Postfix(PlanetJem.Customization.PartsShopExtrasBrowse __instance, ref int __result)
            {
                int idx = __instance.Index;
                if (!IsCustom(idx)) return;
                __result = Prices[CustomIndex(idx)].Value;
            }
        }

        [HarmonyPatch(typeof(PlanetJem.Localization.Locale), "GetNeonColorName")]
        internal static class NameProbe
        {
            static void Postfix(int index, ref string __result)
            {
                if (!IsCustom(index)) return;
                __result = Names[CustomIndex(index)];
            }
        }

        // 越界索引不交给游戏（避免崩溃）
        [HarmonyPatch(typeof(CarLight), "ApplyNeonIndices")]
        internal static class ApplyIndicesPatch
        {
            static bool Prefix(int exteriorIndex, int interiorIndex)
            {
                return !(IsCustom(exteriorIndex) || IsCustom(interiorIndex));
            }
        }

        // 玩家车：换成占位色建灯（随后我们上色）；其他车：换成 0
        [HarmonyPatch(typeof(CarLocalCustom), "car_updateNeonColor")]
        internal static class UpdateNeonPatch
        {
            static bool Prefix(CarLocalCustom __instance, ref int exteriorNeon, ref int interiorNeon)
            {
                if (!(IsCustom(exteriorNeon) || IsCustom(interiorNeon))) return true;

                // 玩家车和远程车都用占位色，让游戏把灯建出来；颜色随后由本插件覆盖
                if (IsCustom(exteriorNeon)) exteriorNeon = Placeholder;
                if (IsCustom(interiorNeon)) interiorNeon = Placeholder;
                return true;
            }
        }

        // 回滚版行为：只要有流灯配置就强制点亮（会让别人车也亮起来，已知副作用）
        [HarmonyPatch(typeof(CarLight), "SetNeonExteriorActive")]
        internal static class ForceActivePatch
        {
            static void Prefix(CarLight __instance, ref bool active)
            {
                if (active) return;
                try
                {
                    CarLocalCustom car = null;
                    try { car = __instance.GetComponentInParent<CarLocalCustom>(); } catch { }
                    if (car == null) return;
                    if (!IsCustom(car.car_neonExtColorInt)) return;
                    active = true;
                }
                catch { }
            }
        }

        // 玩家车每帧上色
        [HarmonyPatch(typeof(CarLocalCustom), "LateUpdate")]
        internal static class CarLatePatch
        {
            static void Postfix(CarLocalCustom __instance)
            {
                try
                {
                    int idx = __instance.car_neonExtColorInt;
                    if (!IsCustom(idx)) return;

                    CarLight light = null;
                    try { light = __instance.GetComponentInChildren<CarLight>(true); } catch { }
                    if (light == null) return;

                    var neon = light.NeonLight;
                    if (neon == null) return;

                    bool isPlayer = IsPlayerCar(__instance);
                    if (isPlayer)
                    {
                        _playerIdx = idx;
                        _playerLight = light;
                    }

                    neon.color = Sample(idx, Time.time);
                    neon.intensity = Intensity.Value;
                    neon.enabled = true;
                }
                catch { }
            }
        }

        // 再补一次，确保不被游戏覆盖
        [HarmonyPatch(typeof(CarLight), "LateUpdate")]
        internal static class LightLatePatch
        {
            static void Postfix(CarLight __instance)
            {
                try
                {
                    if (_playerLight == null || __instance != _playerLight) return;
                    if (!IsCustom(_playerIdx)) return;
                    var neon = __instance.NeonLight;
                    if (neon == null) return;
                    neon.color = Sample(_playerIdx, Time.time);
                    neon.intensity = Intensity.Value;
                    neon.enabled = true;
                }
                catch { }
            }
        }
    }
}
