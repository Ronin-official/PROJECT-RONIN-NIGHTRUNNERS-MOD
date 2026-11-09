using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace Ronin.NRProjectRonin
{
    /// <summary>
    /// 3.2.8 鐗堣嚜瀹氫箟闊充箰鏇挎崲锛圔epInEx/plugins/music 鐩綍锛夈€?    /// 鐢?3.1.11 闊充箰妯″潡绉绘锛屽幓鎺?3.1.11 涓撴湁鐨勭數璇濅氦浜掋€?    /// </summary>
    [BepInPlugin(Guid, "NR Music Replace", "1.0.1")]
    public class Plugin : BasePlugin
    {
        public const string Guid = "ronin.nrmusic328";
        public static BepInEx.Logging.ManualLogSource L;

        static readonly Harmony H = new Harmony(Guid);

        public override void Load()
        {
            L = Log;
            try
            {
                MusicReplace311.EnsureFolders();
                MusicReplace311.Enabled = Config.Bind("Music", "ReplaceMusic", true, "replace game music with files in BepInEx/plugins/music");
                MusicReplace311.FolderCfg = Config.Bind("Music", "MusicFolder", "", "music folder (blank = BepInEx/plugins/music)");
                MusicReplace311.VolumeCfg = Config.Bind("Music", "MusicVolume", 0.8f, "playback volume");
                MusicReplace311.ShowNames = Config.Bind("Music", "ShowTrackName", true, "show track name in phone/UI (may cause a small stutter)").Value;

                var nextKey = Config.Bind("Music", "NextTrackKey", "RightArrow", "manual next track key");
                var prevKey = Config.Bind("Music", "PrevTrackKey", "LeftArrow", "manual previous track key");
                try
                {
                    KeyCode k;
                    if (Enum.TryParse<KeyCode>(nextKey.Value, true, out k)) MusicReplace311.NextKey = k;
                    if (Enum.TryParse<KeyCode>(prevKey.Value, true, out k)) MusicReplace311.PrevKey = k;
                }
                catch { }

                Patch(typeof(PatchMusicReplace311));
                Patch(typeof(PatchMusicTrackNameGetter311));
                Patch(typeof(PatchMusicLibraryName311));
                Patch(typeof(PatchMusicNowPlayingGetter311));
                Patch(typeof(PatchUINowPlayingSetName));
                Patch(typeof(MusicReplace311.AudioManagerNowPlayingPatch));
                Patch(typeof(MusicReplace311.PlayMusicSkip));

                int n = 0;
                foreach (var m in H.GetPatchedMethods()) n++;
                L.LogInfo("MusicReplace328 loaded, patches=" + n + ", enabled=" + MusicReplace311.Enabled.Value);
            }
            catch (Exception e) { L.LogError(e); }
        }

        void Patch(Type t)
        {
            try { H.CreateClassProcessor(t, true).Patch(); }
            catch (Exception e) { L.LogWarning("patch " + t.Name + " failed: " + e.GetBaseException().Message); }
        }
    }
}










