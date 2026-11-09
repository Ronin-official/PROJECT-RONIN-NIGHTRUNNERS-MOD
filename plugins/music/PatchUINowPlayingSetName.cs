using System;
using HarmonyLib;
using PlanetJem.UI.Overlay;

namespace Ronin.NRProjectRonin
{
	// Token: 0x0200003E RID: 62
	[HarmonyPatch(typeof(UINowPlaying), "SetTrackName")]
	internal static class PatchUINowPlayingSetName
	{
		// Token: 0x060000C6 RID: 198 RVA: 0x0000D7C4 File Offset: 0x0000B9C4
		private static void Postfix(UINowPlaying __instance, string __0)
		{
			try
			{
				if (!PatchUINowPlayingSetName._busy)
				{
					if (MusicReplace311._active)
					{
						string currentName = MusicReplace311._currentName;
						if (!string.IsNullOrEmpty(currentName))
						{
							if (!(__0 == currentName))
							{
								PatchUINowPlayingSetName._busy = true;
								try
								{
									__instance.SetTrackName(currentName);
								}
								catch
								{
								}
								PatchUINowPlayingSetName._busy = false;
							}
						}
					}
				}
			}
			catch
			{
				PatchUINowPlayingSetName._busy = false;
			}
		}

		// Token: 0x04000094 RID: 148
		private static bool _busy;
	}
}
