using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Ronin.EngineHeatSkip
{
    /// <summary>
    /// 关闭引擎过热：直接拦截 RCC 的 EngineHeat()，水温/油温不再上升。
    /// 3.2.8 版，热键默认 F4（F7 被联机菜单占用）。
    /// </summary>
    [BepInPlugin(Guid, "NR Engine Heat Skip", "1.1.0")]
    public class Plugin : BasePlugin
    {
        public const string Guid = "ronin.nrengineheatskip";
        public static BepInEx.Logging.ManualLogSource L;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> PlayerOnly;
        internal static ConfigEntry<string> ToggleKey;
        internal static ConfigEntry<string> ExcludeNames;

        static bool _active;
        static float _gate;
        static int _skipped;

        public override void Load()
        {
            L = Log;
            Enabled = Config.Bind("Heat", "Enabled", true, "turn off engine overheat (water/oil stays cool)");
            PlayerOnly = Config.Bind("Heat", "PlayerOnly", true, "only affect the local player car");
            ToggleKey = Config.Bind("Heat", "ToggleKey", "F4", "toggle key");
            ExcludeNames = Config.Bind("Heat", "ExcludeNames", "RONIN,Tommykaira,ZZ", "cars with these name parts keep vanilla heat");
            _active = Enabled.Value;
            Harmony.CreateAndPatchAll(typeof(EngineHeatPatch), Guid);
            Harmony.CreateAndPatchAll(typeof(TickPatch), Guid);
            L.LogInfo("EngineHeatSkip 1.1.0 loaded, active=" + _active);
        }

        internal static bool Active
        {
            get { return _active; }
        }

        static KeyCode Key()
        {
            try
            {
                KeyCode k;
                if (Enum.TryParse<KeyCode>(ToggleKey.Value, true, out k)) return k;
            }
            catch { }
            return KeyCode.F4;
        }

        static bool IsExcluded(string goName)
        {
            try
            {
                foreach (var part in (ExcludeNames.Value ?? "").Split(','))
                {
                    string q = part.Trim();
                    if (q.Length > 0 && goName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>拦下 EngineHeat()：不升温、不触发过热</summary>
        [HarmonyPatch(typeof(RCC_CarControllerV3), "EngineHeat")]
        internal static class EngineHeatPatch
        {
            static bool Prefix(RCC_CarControllerV3 __instance)
            {
                try
                {
                    if (!_active || __instance == null) return true;

                    if (PlayerOnly.Value)
                    {
                        var gc = GodConstant.Instance;
                        if (gc == null || gc.playerCar == null || gc.playerCar != __instance) return true;
                    }

                    string name = "";
                    try { name = __instance.gameObject != null ? (__instance.gameObject.name ?? "") : ""; } catch { }
                    if (IsExcluded(name)) return true;

                    __instance.engineHeatWater = 0f;
                    __instance.engineHeatOil = 0f;
                    if (_skipped++ < 5)
                    {
                        try { L.LogInfo("[heat] EngineHeat blocked: " + name); } catch { }
                    }
                    return false;
                }
                catch { return true; }
            }
        }

        /// <summary>保险丝：万一别处直接改字段，定期把温度压回 0</summary>
        [HarmonyPatch(typeof(GodConstant), "Update")]
        internal static class TickPatch
        {
            static void Postfix()
            {
                try
                {
                    if (Input.GetKeyDown(Key()))
                    {
                        _active = !_active;
                        L.LogInfo("[heat] overheat skip: " + (_active ? "ON" : "OFF"));
                    }
                    if (!_active) return;

                    _gate += Time.deltaTime;
                    if (_gate < 1f) return;
                    _gate = 0f;

                    var gc = GodConstant.Instance;
                    if (gc == null) return;
                    var car = gc.playerCar;
                    if (car == null) return;

                    if (car.engineHeatWater > 1f) car.engineHeatWater = 0f;
                    if (car.engineHeatOil > 1f) car.engineHeatOil = 0f;
                    if (car.useEngineHeat)
                    {
                        // 某些车会重新打开，强制关掉
                        if (_skipped2++ < 5) L.LogInfo("[heat] useEngineHeat re-disabled");
                        car.useEngineHeat = false;
                    }
                }
                catch { }
            }

            static int _skipped2;
        }
    }
}
