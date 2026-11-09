using System;
using HarmonyLib;
using PlanetJem.Audio;

namespace Ronin.NRProjectRonin
{
    // 3.2.8：手动切歌（Next/Prev/Skip）被证实会导致 coreclr 访问冲突，
    // 这里不挂任何补丁，只保留自动播完切歌。
    internal static class PatchMusicManualSwitchDisabled
    {
    }
}