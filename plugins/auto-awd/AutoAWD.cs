using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Ronin.AutoAWD
{
    /// <summary>
    /// 玩家当前车自动驱动形式：
    ///  · 普通车 → AWD
    ///  · 装了漂移件（焊接差速器 / DIY 加长转向节）→ RWD
    /// 无热键，配置里可整体关闭。
    /// </summary>
    [BepInPlugin(Guid, "NR Auto AWD", "1.1.0")]
    public class Plugin : BasePlugin
    {
        public const string Guid = "ronin.nrjautoawd";
        public static BepInEx.Logging.ManualLogSource L;

        static ConfigEntry<bool> Enabled;
        static ConfigEntry<bool> SkipDriftCars;

        static int _logs;
        static int _probeLogs;

        public override void Load()
        {
            L = Log;
            Enabled = Config.Bind("AWD", "Enabled", true, "auto drivetrain for the player car");
            SkipDriftCars = Config.Bind("AWD", "SkipDriftCars", true, "cars with drift parts get RWD");
            Harmony.CreateAndPatchAll(typeof(CarPatch), Guid);
            L.LogInfo("AutoAWD 1.1.0 loaded, enabled=" + Enabled.Value);
        }

        static bool IsPlayerCar(CarLocalCustom car)
        {
            try
            {
                var gc = GodConstant.Instance;
                var pc = gc != null ? gc.playerCar : null;
                if (pc == null) return false;
                try { if (pc.carLocal == car) return true; } catch { }
                try { if (pc.gameObject == car.gameObject) return true; } catch { }
            }
            catch { }
            return false;
        }

        static bool HasDriftParts(CarData data)
        {
            int diff = -1, knuckle = -1;
            try { diff = data.CurrentHandlingPartLevel(CarPart.PartType.han_differential); } catch { }
            try { knuckle = data.CurrentHandlingPartLevel(CarPart.PartType.han_steeringKnuckle); } catch { }

            if (_probeLogs++ < 12) LogMsg("probe: diff=" + diff + " knuckle=" + knuckle);

            if (diff >= 4) return true;     // 焊接差速器 (prototype)
            if (knuckle >= 2) return true;  // DIY 加长转向节 (street+)
            return false;
        }

        [HarmonyPatch(typeof(CarLocalCustom), "LateUpdate")]
        internal static class CarPatch
        {
            static void Postfix(CarLocalCustom __instance)
            {
                try
                {
                    if (!Enabled.Value || __instance == null) return;
                    if (!IsPlayerCar(__instance)) return;

                    CarData data = null;
                    try { data = __instance.carData; } catch { }
                    if (data == null) return;
                    var rcc = data.rcc;
                    if (rcc == null) return;

                    bool drift = SkipDriftCars.Value && HasDriftParts(data);

                    var wantData = drift ? CarData.Drivetrain.RWD : CarData.Drivetrain.AWD;
                    var wantWheel = drift ? RCC_CarControllerV3.WheelType.RWD : RCC_CarControllerV3.WheelType.AWD;

                    bool changed = false;
                    try { if (data.drivetrain != wantData) { data.drivetrain = wantData; changed = true; } } catch { }
                    try { if (rcc._wheelTypeChoise != wantWheel) { rcc._wheelTypeChoise = wantWheel; changed = true; } } catch { }
                    try { if (rcc._wheelTypeOriginal != wantWheel) { rcc._wheelTypeOriginal = wantWheel; changed = true; } } catch { }

                    if (changed && _logs++ < 20)
                        LogMsg((drift ? "RWD (drift parts) " : "AWD ") + (__instance.gameObject != null ? __instance.gameObject.name : "?"));
                }
                catch { }
            }
        }

        static void LogMsg(string msg)
        {
            try { if (L != null) L.LogInfo("[autoawd] " + msg); } catch { }
        }
    }
}
