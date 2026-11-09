using System;
using HarmonyLib;
using PlanetJem.Audio;

namespace Ronin.NRProjectRonin
{
	// Token: 0x02000041 RID: 65
	[HarmonyPatch(typeof(MusicPlayer), "get_NowPlaying")]
	internal static class PatchMusicNowPlayingGetter311
	{
		// Token: 0x060000C9 RID: 201 RVA: 0x0000D8C0 File Offset: 0x0000BAC0
		private static void Postfix(ref MusicTrackEntry __result)
		{
			try
			{
				if (MusicReplace311._active)
				{
					string currentName = MusicReplace311._currentName;
					if (!string.IsNullOrEmpty(currentName))
					{
						if (__result == null)
						{
							__result = new MusicTrackEntry();
						}
						__result.TrackName = currentName;
					}
				}
			}
			catch
			{
			}
		}
	}
}
