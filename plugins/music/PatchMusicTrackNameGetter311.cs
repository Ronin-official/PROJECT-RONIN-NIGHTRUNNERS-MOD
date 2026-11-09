using System;
using HarmonyLib;
using PlanetJem.Audio;

namespace Ronin.NRProjectRonin
{
	// Token: 0x02000042 RID: 66
	[HarmonyPatch(typeof(MusicTrackEntry), "get_TrackName")]
	internal static class PatchMusicTrackNameGetter311
	{
		// Token: 0x060000CA RID: 202 RVA: 0x0000D910 File Offset: 0x0000BB10
		private static void Postfix(ref string __result)
		{
			try
			{
				if (MusicReplace311._active)
				{
					string currentName = MusicReplace311._currentName;
					if (!string.IsNullOrEmpty(currentName))
					{
						__result = currentName;
					}
				}
			}
			catch
			{
			}
		}
	}
}
