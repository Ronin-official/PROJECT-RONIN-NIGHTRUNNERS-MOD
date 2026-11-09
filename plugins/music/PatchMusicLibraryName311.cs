using System;
using HarmonyLib;
using PlanetJem.Audio;

namespace Ronin.NRProjectRonin
{
	// Token: 0x02000040 RID: 64
	[HarmonyPatch(typeof(MusicLibrary), "GetTrackInfo")]
	internal static class PatchMusicLibraryName311
	{
		// Token: 0x060000C8 RID: 200 RVA: 0x0000D874 File Offset: 0x0000BA74
		private static void Postfix(ref MusicTrackEntry __result)
		{
			try
			{
				if (MusicReplace311._active)
				{
					string currentName = MusicReplace311._currentName;
					if (!string.IsNullOrEmpty(currentName))
					{
						if (__result != null)
						{
							__result.TrackName = currentName;
						}
					}
				}
			}
			catch
			{
			}
		}
	}
}
