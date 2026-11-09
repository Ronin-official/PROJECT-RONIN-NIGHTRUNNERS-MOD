using System;
using HarmonyLib;

namespace Ronin.NRProjectRonin
{
	// Token: 0x0200003B RID: 59
	[HarmonyPatch(typeof(GodConstant), "Update")]
	internal static class PatchMusicReplace311
	{
		// Token: 0x060000C3 RID: 195 RVA: 0x0000D74C File Offset: 0x0000B94C
		private static void Postfix()
		{
			try
			{
				MusicReplace311.Tick();
			}
			catch
			{
			}
		}
	}
}
