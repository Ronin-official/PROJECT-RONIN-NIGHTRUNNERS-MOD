using System;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlanetJem.Audio;
using PlanetJem.UI.Components;
using PlanetJem.UI.Overlay;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Ronin.NRProjectRonin
{
	// Token: 0x0200003A RID: 58
	internal static class MusicReplace311
	{
		// Token: 0x060000AA RID: 170 RVA: 0x0000BD94 File Offset: 0x00009F94
		internal static void EnsureFolders()
		{
			try
			{
				string text = MusicReplace311.Root();
				if (!string.IsNullOrEmpty(text))
				{
					if (!Directory.Exists(text))
					{
						Directory.CreateDirectory(text);
					}
					string[] array = new string[] { "Cruise", "Race", "RaceWin", "RaceLoss", "Garage", "Storage", "Meetspot", "MainMenu", "Loading", "_All" };
					for (int i = 0; i < array.Length; i++)
					{
						string text2 = Path.Combine(text, array[i]);
						if (!Directory.Exists(text2))
						{
							Directory.CreateDirectory(text2);
						}
					}
					ManualLogSource l = Plugin.L;
					if (l != null)
					{
						l.LogInfo("[music] 目录就绪: " + text);
					}
				}
			}
			catch (Exception ex)
			{
				ManualLogSource l2 = Plugin.L;
				if (l2 != null)
				{
					l2.LogWarning("[music] 建目录失败: " + ex.Message);
				}
			}
		}

		// Token: 0x060000AB RID: 171 RVA: 0x0000BE98 File Offset: 0x0000A098
		// Token: 0x060000AC RID: 172 RVA: 0x0000BF84 File Offset: 0x0000A184
		static float _nextSlow;
		static bool _hostReady;

		internal static void Tick()
		{
			try
			{
				if (!_hostReady)
				{
					MusicReplace311.EnsureHost();
					_hostReady = MusicReplace311._host != null;
				}

				bool next = UnityEngine.Input.GetKeyDown(MusicReplace311.NextKey);
				bool prev = UnityEngine.Input.GetKeyDown(MusicReplace311.PrevKey);
				if ((next || prev) && MusicReplace311.PhoneOpen())
				{
					if (next) MusicReplace311.PlayNext();
					if (prev) MusicReplace311.PlayPrev();
				}
			}
			catch { }

			try
			{
				float now = Time.unscaledTime;
				if (now < _nextSlow) return;
				_nextSlow = now + 0.35f;
				MusicReplace311.SlowTick();
			}
			catch { }
		}

		internal static void SlowTick()
		{
			try
			{
				if (MusicReplace311.Enabled != null && MusicReplace311.Enabled.Value)
				{
					MusicReplace311.EnsureHost();

					float now = Time.unscaledTime;
					bool heavy = now >= MusicReplace311._nextHeavy;
					if (heavy)
					{
						MusicReplace311._nextHeavy = now + 2.5f;
					}
					{
						string text = heavy ? MusicReplace311.CurrentStateKey() : MusicReplace311._cachedState;
						if (heavy)
						{
							MusicReplace311._cachedState = text;
						}
						if (text != MusicReplace311._stateKey && now >= MusicReplace311._nextStateChange)
						{
							MusicReplace311._nextStateChange = now + 2f;
							bool flag = string.IsNullOrEmpty(MusicReplace311._stateKey);
							MusicReplace311._prevState = MusicReplace311._stateKey;
							MusicReplace311._stateKey = text;
							MusicReplace311.ClearCaches();
							MusicReplace311._muteCache = null;
							MusicReplace311._prefClip = null;
							MusicReplace311._prefIndex = -1;
							ManualLogSource l = Plugin.L;
							if (l != null)
							{
								l.LogInfo(string.Concat(new string[]
								{
									"[music] 状态 -> ",
									text,
									"（来自 ",
									string.IsNullOrEmpty(MusicReplace311._trackState) ? "无" : MusicReplace311._trackState,
									"）"
								}));
							}
							bool flag2 = text == "MainMenu" || text == "Loading" || (text == "Garage" && MusicReplace311._trackState != "Garage") || (text == "Storage" && MusicReplace311._trackState != "Storage") || (text == "Cruise" && (MusicReplace311._trackState == "Garage" || MusicReplace311._trackState == "Storage" || MusicReplace311._trackState == "Loading"));
							AudioSource act = MusicReplace311.ActiveSrc();
						bool flag3 = act != null && act.clip != null && act.isPlaying;
							if (flag || flag2 || !flag3)
							{
								MusicReplace311._readyClip = null;
								int kept;
								if (MusicReplace311._stateIndex.TryGetValue(text ?? "", out kept)) MusicReplace311._index = kept;
								else MusicReplace311._index = -1;
								MusicReplace311.PlayNext();
							}
						}
						MusicReplace311.PumpLoad();
						MusicReplace311.PumpPending();
						MusicReplace311.PumpPrefetch();
						MusicReplace311.PumpName();
						MusicReplace311.ApplyQueuedSwitch();
						if (MusicReplace311.ActiveSrc() != null && MusicReplace311.ActiveSrc().isPlaying
							&& MusicReplace311.ActiveSrc().clip != null
							&& MusicReplace311.ActiveSrc().clip.length - MusicReplace311.ActiveSrc().time < 20f)
						{
							MusicReplace311.StartPrefetch();
						}
						if (MusicReplace311._endAt > 0f && now >= MusicReplace311._endAt)
						{
							MusicReplace311._endAt = 0f;

							// 开车/比赛时不切歌（切歌解码会卡一帧，影响漂移和比赛）
							float speed = 0f;
							try
							{
								var pc = GodConstant.Instance != null ? GodConstant.Instance.playerCar : null;
								if (pc != null) speed = pc.speed;
							}
							catch { }

							string st = MusicReplace311._stateKey ?? "";
							bool busy = st == "Race" || st == "RaceWin" || st == "RaceLoss" || speed > 30f;

							bool played = MusicReplace311.PlayPrefetched();
							if (!played)
							{
								if (busy)
								{
									// 没预载好且在开车：重放当前曲目避免卡顿
									try
									{
										AudioSource a = MusicReplace311.ActiveSrc();
										if (a != null && a.clip != null)
										{
											a.time = 0f;
											a.Play();
											float len = 60f;
											try { len = a.clip.length > 1f ? a.clip.length : 60f; } catch { }
											MusicReplace311._endAt = now + len;
										}
									}
									catch { }
								}
								else
								{
									MusicReplace311.PlayNext();
								}
							}
						}
						else if (MusicReplace311.ActiveSrc() != null && MusicReplace311.ActiveSrc().clip != null && !MusicReplace311.ActiveSrc().isPlaying && !MusicReplace311.ActiveSrc().loop)
						{
							MusicReplace311.PlayNext();
						}
						if (heavy)
						{
							MusicReplace311.MuteGame();
							
						}
						if (string.IsNullOrEmpty(MusicReplace311._vanillaName))
						{
							try
							{
								MusicPlayer musicPlayer = MusicReplace311.FindPlayer();
								if (musicPlayer != null)
								{
									MusicTrackEntry musicTrackEntry = null;
									try
									{
										musicTrackEntry = musicPlayer._NowPlaying_k__BackingField;
									}
									catch
									{
									}
									if (musicTrackEntry == null)
									{
										try
										{
											musicTrackEntry = musicPlayer.NowPlaying;
										}
										catch
										{
										}
									}
									if (musicTrackEntry != null)
									{
										string trackName = musicTrackEntry.TrackName;
										if (!string.IsNullOrEmpty(trackName) && trackName != MusicReplace311._currentName && !trackName.ToLowerInvariant().EndsWith(".mp3") && !trackName.ToLowerInvariant().EndsWith(".ogg") && !trackName.ToLowerInvariant().EndsWith(".wav"))
										{
											MusicReplace311._vanillaName = trackName;
										}
									}
								}
							}
							catch
							{
							}
						}
						MusicReplace311._nameT += Time.deltaTime;
						if (MusicReplace311._nameT > 1f)
						{
							MusicReplace311._nameT = 0f;
							if (MusicReplace311._active && !string.IsNullOrEmpty(MusicReplace311._currentName))
							{
								MusicReplace311.PushName(MusicReplace311._currentName);
								
								MusicReplace311.SweepPhoneText();
								MusicReplace311.SweepTmpText();
								MusicReplace311.SweepSongName();
							}
						}
						MusicReplace311._scanT += Time.deltaTime;
						if (MusicReplace311._scanT > 5f)
						{
							MusicReplace311._scanT = 0f;
							MusicReplace311._lists.Clear();
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000AD RID: 173 RVA: 0x0000C368 File Offset: 0x0000A568
		private static string Root()
		{
			string text2;
			try
			{
				string text = ((MusicReplace311.FolderCfg != null) ? (MusicReplace311.FolderCfg.Value ?? "") : "");
				if (string.IsNullOrWhiteSpace(text))
				{
					text = Path.Combine(Paths.PluginPath, "music");
				}
				text2 = text;
			}
			catch
			{
				text2 = "";
			}
			return text2;
		}

		// Token: 0x060000AE RID: 174 RVA: 0x0000C3D0 File Offset: 0x0000A5D0
		private static bool InStorage()
		{
			try
			{
				Il2CppArrayBase<customization_storageUI> il2CppArrayBase = StorageUis();
				if (il2CppArrayBase != null)
				{
					for (int i = 0; i < il2CppArrayBase.Length; i++)
					{
						customization_storageUI customization_storageUI = il2CppArrayBase[i];
						if (!(customization_storageUI == null))
						{
							GameObject gameObject = customization_storageUI.gameObject;
							if (gameObject != null && gameObject.activeInHierarchy)
							{
								return true;
							}
						}
					}
				}
				Il2CppArrayBase<car_storage> il2CppArrayBase2 = CarStorages();
				if (il2CppArrayBase2 != null)
				{
					for (int j = 0; j < il2CppArrayBase2.Length; j++)
					{
						car_storage car_storage = il2CppArrayBase2[j];
						if (!(car_storage == null))
						{
							GameObject gameObject2 = car_storage.gameObject;
							if (gameObject2 != null && gameObject2.activeInHierarchy)
							{
								return true;
							}
						}
					}
				}
			}
			catch
			{
			}
			return false;
		}

		// Token: 0x060000AF RID: 175 RVA: 0x0000C494 File Offset: 0x0000A694
		private static string CurrentStateKey()
		{
			string text;
			try
			{
				if (MusicReplace311.InStorage())
				{
					text = "Storage";
				}
				else
				{
					MusicPlayer musicPlayer = MusicReplace311.FindPlayer();
					if (musicPlayer == null)
					{
						text = "Cruise";
					}
					else
					{
						MusicPlayerState musicPlayerState = (MusicPlayerState)4;
						try
						{
							musicPlayerState = musicPlayer.State;
						}
						catch
						{
						}
						switch ((int)musicPlayerState)
						{
						case 1:
							return "Loading";
						case 2:
							return "Garage";
						case 3:
							return "Meetspot";
						case 5:
							return "Race";
						case 6:
							return "RaceWin";
						case 7:
							return "RaceLoss";
						case 8:
							return "MainMenu";
						}
						text = "Cruise";
					}
				}
			}
			catch
			{
				text = "Cruise";
			}
			return text;
		}

		// Token: 0x060000B0 RID: 176 RVA: 0x0000C57C File Offset: 0x0000A77C

		// ---- 缓存：避免每次全场景扫描（性能修复，保持 Il2Cpp 数组类型）----
		private static MusicPlayer _cachedPlayer;
		private static Il2CppArrayBase<UINowPlaying> _cachedNowPlaying;
		private static Il2CppArrayBase<UIText> _cachedUitexts;
		private static Il2CppArrayBase<TMP_Text> _cachedTmps;
		private static Il2CppArrayBase<customization_storageUI> _cachedStorageUis;
		private static Il2CppArrayBase<car_storage> _cachedCarStorages;

		internal static void ClearCaches()
		{
			_cachedPlayer = null;
			_cachedNowPlaying = null;
			_cachedUitexts = null;
			_cachedTmps = null;
			_cachedStorageUis = null;
			_cachedCarStorages = null;
		}

		private static Il2CppArrayBase<UINowPlaying> NowPlayings()
		{
			if (_cachedNowPlaying == null || _cachedNowPlaying.Length == 0)
				_cachedNowPlaying = Resources.FindObjectsOfTypeAll<UINowPlaying>();
			return _cachedNowPlaying;
		}

		private static Il2CppArrayBase<UIText> Uitexts()
		{
			if (_cachedUitexts == null || _cachedUitexts.Length == 0)
				_cachedUitexts = Resources.FindObjectsOfTypeAll<UIText>();
			return _cachedUitexts;
		}

		private static Il2CppArrayBase<TMP_Text> Tmps()
		{
			if (_cachedTmps == null || _cachedTmps.Length == 0)
				_cachedTmps = Resources.FindObjectsOfTypeAll<TMP_Text>();
			return _cachedTmps;
		}

		private static Il2CppArrayBase<customization_storageUI> StorageUis()
		{
			if (_cachedStorageUis == null || _cachedStorageUis.Length == 0)
				_cachedStorageUis = Resources.FindObjectsOfTypeAll<customization_storageUI>();
			return _cachedStorageUis;
		}

		private static Il2CppArrayBase<car_storage> CarStorages()
		{
			if (_cachedCarStorages == null || _cachedCarStorages.Length == 0)
				_cachedCarStorages = Resources.FindObjectsOfTypeAll<car_storage>();
			return _cachedCarStorages;
		}

		private static MusicPlayer FindPlayer()
		{
			if (_cachedPlayer != null) return _cachedPlayer;
			try
			{
				Il2CppArrayBase<MusicPlayer> il2CppArrayBase = Resources.FindObjectsOfTypeAll<MusicPlayer>();
				if (il2CppArrayBase != null && il2CppArrayBase.Length > 0)
				{
					_cachedPlayer = il2CppArrayBase[0];
					return _cachedPlayer;
				}
			}
			catch
			{
			}
			return null;
		}

		// Token: 0x060000B1 RID: 177 RVA: 0x0000C5C0 File Offset: 0x0000A7C0
		private static List<string> ListFor(string state)
		{
			List<string> list;
			if (MusicReplace311._lists.TryGetValue(state, out list) && list.Count > 0)
			{
				return list;
			}
			list = new List<string>();
			try
			{
				string text = MusicReplace311.Root();
				string text2 = Path.Combine(text, state);
				if (!Directory.Exists(text2))
				{
					text2 = Path.Combine(text, "_All");
				}
				if (!Directory.Exists(text2))
				{
					text2 = text;
				}
				if (Directory.Exists(text2))
				{
					foreach (string text3 in new string[] { "*.ogg", "*.wav", "*.mp3" })
					{
						try
						{
							list.AddRange(Directory.GetFiles(text2, text3, 0));
						}
						catch
						{
						}
					}
					if (list.Count == 0)
					{
						string[] array2 = new string[] { "Race", "Cruise", "_All" };
						int num = 0;
						while (num < array2.Length && list.Count == 0)
						{
							string text4 = Path.Combine(text, array2[num]);
							if (Directory.Exists(text4) && !(text4 == text2))
							{
								foreach (string text5 in new string[] { "*.ogg", "*.wav", "*.mp3" })
								{
									try
									{
										list.AddRange(Directory.GetFiles(text4, text5, 0));
									}
									catch
									{
									}
								}
								if (list.Count > 0)
								{
									text2 = text4;
								}
							}
							num++;
						}
					}
					if (list.Count == 0 && text != text2)
					{
						foreach (string text6 in new string[] { "*.ogg", "*.wav", "*.mp3" })
						{
							try
							{
								list.AddRange(Directory.GetFiles(text, text6, 0));
							}
							catch
							{
							}
						}
						if (list.Count > 0)
						{
							text2 = text;
						}
					}
				}
				list.Sort(StringComparer.OrdinalIgnoreCase);
			}
			catch
			{
			}
			MusicReplace311._lists[state] = list;
			if (list.Count > 0)
			{
				ManualLogSource l = Plugin.L;
				if (l != null)
				{
					string[] array3 = new string[7];
					array3[0] = "[music] ";
					array3[1] = state;
					array3[2] = " 歌单 ";
					int num2 = 3;
					int i = list.Count;
					array3[num2] = i.ToString();
					array3[4] = " 首（";
					array3[5] = Path.GetDirectoryName(list[0]);
					array3[6] = "）";
					l.LogInfo(string.Concat(array3));
				}
			}
			return list;
		}

		// Token: 0x060000B2 RID: 178 RVA: 0x0000C884 File Offset: 0x0000AA84
		internal static int _queuedDir;

		internal static void ApplyQueuedSwitch()
		{
			try
			{
				if (_queuedDir == 0) return;
				int dir = _queuedDir;
				_queuedDir = 0;
				if (dir > 0) PlayNext();
				else PlayPrev();
			}
			catch { }
		}







		[HarmonyPatch(typeof(PlanetJem.Audio.AudioManager), "get_NowPlaying")]
		internal static class AudioManagerNowPlayingPatch
		{
			static PlanetJem.Audio.MusicTrackEntry _entry;
			static string _shown;

			static void Postfix(ref PlanetJem.Audio.MusicTrackEntry __result)
			{
				try
				{
					string name = MusicReplace311._currentName;
					if (string.IsNullOrEmpty(name)) return;
					if (__result == null)
					{
						if (_entry == null) _entry = new PlanetJem.Audio.MusicTrackEntry();
						__result = _entry;
					}
					if (__result.TrackName != name) __result.TrackName = name;
				}
				catch { }
			}
		}


		[HarmonyPatch(typeof(PlanetJem.Audio.AudioManager), "PlayMusic")]
		internal static class PlayMusicSkip
		{
			static bool Prefix()
			{
				try
				{
					// 只在 PA（Meetspot）里拦：PA 有自己的一套音乐，会和我们的叠
					return !(MusicReplace311.Enabled != null && MusicReplace311.Enabled.Value
						&& MusicReplace311._stateKey == "Meetspot");
				}
				catch { return true; }
			}
		}

		internal static bool PhoneOpen()
		{
			try
			{
				if (!PlanetJem.Phone.PhoneController.IsRaised) return false;
				if (PlanetJem.Phone.PhoneController.IsCallActive) return false;
				if (PlanetJem.Phone.PhoneController.IsCallBusy) return false;
				return true;
			}
			catch { return false; }
		}

		internal static void QueueSwitch(int dir)
		{
			_queuedDir = dir;
		}

		internal static void PlayNext()
		{
			try
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (realtimeSinceStartup - MusicReplace311._switchT >= 0.5f)
				{
					MusicReplace311._switchT = realtimeSinceStartup;
					List<string> list = MusicReplace311.ListFor(MusicReplace311._stateKey);
					if (list.Count != 0)
					{
						MusicReplace311._index = (MusicReplace311._index + 1) % list.Count;
						MusicReplace311.StartLoad(list[MusicReplace311._index]);
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000B3 RID: 179 RVA: 0x0000C8FC File Offset: 0x0000AAFC
		internal static void PlayPrev()
		{
			try
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (realtimeSinceStartup - MusicReplace311._switchT >= 0.5f)
				{
					MusicReplace311._switchT = realtimeSinceStartup;
					List<string> list = MusicReplace311.ListFor(MusicReplace311._stateKey);
					if (list.Count != 0)
					{
						MusicReplace311._index = (MusicReplace311._index - 1 + list.Count) % list.Count;
						MusicReplace311.StartLoad(list[MusicReplace311._index]);
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000B4 RID: 180 RVA: 0x0000C97C File Offset: 0x0000AB7C
		private static void StartLoad(string path)
		{
			try
			{
				// 旧请求进队列，只释放最老的（其音频已不再播放）
				if (MusicReplace311._req != null)
				{
					MusicReplace311._reqQueue.Add(MusicReplace311._req);
					while (MusicReplace311._reqQueue.Count > 3)
					{
						try { MusicReplace311._reqQueue[0].Dispose(); } catch { }
						MusicReplace311._reqQueue.RemoveAt(0);
					}
				}
				MusicReplace311._loadingPath = path;
				MusicReplace311._trackState = MusicReplace311._stateKey;
				AudioType audioType = (AudioType)14;
				string text = path.ToLowerInvariant();
				if (text.EndsWith(".wav"))
				{
					audioType = (AudioType)20;
				}
				else if (text.EndsWith(".mp3"))
				{
					audioType = (AudioType)13;
				}
				MusicReplace311._req = UnityWebRequestMultimedia.GetAudioClip("file:///" + path.Replace('\\', '/'), audioType);
				MusicReplace311._req.SendWebRequest();
			}
			catch (Exception ex)
			{
				ManualLogSource l = Plugin.L;
				if (l != null)
				{
					l.LogWarning("[music] load: " + ex.Message);
				}
			}
		}

		// Token: 0x060000B5 RID: 181 RVA: 0x0000CA4C File Offset: 0x0000AC4C
		private static void PumpLoad()
		{
			try
			{
				if (MusicReplace311._req != null && MusicReplace311._req.isDone)
				{
					if (string.IsNullOrEmpty(MusicReplace311._req.error))
					{
						AudioClip content = DownloadHandlerAudioClip.GetContent(MusicReplace311._req);
						if (content != null)
						{
							MusicReplace311._readyClip = content;
							// 延迟 0.3 秒再换：先停旧歌，下一帧才赋新 clip（IL2CPP 同帧换 clip 会崩）
							MusicReplace311._pendingClip = content;
							MusicReplace311._pendingAt = Time.realtimeSinceStartup + 0.05f;
						}
					}
					else
					{
						ManualLogSource l2 = Plugin.L;
						if (l2 != null)
						{
							l2.LogWarning("[music] 加载失败 " + Path.GetFileName(MusicReplace311._loadingPath) + ": " + MusicReplace311._req.error);
						}
					}
					// 保留 _req 引用，避免 AudioClip 被回收
					MusicReplace311._reqKeep = MusicReplace311._req;
					MusicReplace311._req = null;
				}
			}
			catch
			{
			}
		}

		private static void StartPrefetch()
		{
			try
			{
				if (_prefClip != null || _prefReq != null) return;
				var list = ListFor(_stateKey);
				if (list == null || list.Count == 0) return;

				int next = (_index + 1) % list.Count;
				string path = list[next];
				if (path == _loadingPath) return;

				_prefPath = path;
				_prefIndex = next;
				AudioType audioType = (AudioType)14;
				string lower = path.ToLowerInvariant();
				if (lower.EndsWith(".wav")) audioType = (AudioType)20;
				else if (lower.EndsWith(".mp3")) audioType = (AudioType)13;

				_prefReq = UnityWebRequestMultimedia.GetAudioClip("file:///" + path.Replace('\\', '/'), audioType);
				_prefReq.SendWebRequest();
			}
			catch { }
		}

		private static void PumpPrefetch()
		{
			try
			{
				if (_prefReq == null || !_prefReq.isDone) return;
				UnityWebRequest req = _prefReq;
				_prefReq = null;
				if (string.IsNullOrEmpty(req.error))
				{
					AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
					if (clip != null)
					{
						_prefClip = clip;
						_reqQueue.Add(req);
						while (_reqQueue.Count > 3)
						{
							try { _reqQueue[0].Dispose(); } catch { }
							_reqQueue.RemoveAt(0);
						}
						return;
					}
				}
				try { req.Dispose(); } catch { }
				_prefClip = null;
				_prefIndex = -1;
			}
			catch { }
		}

		private static bool PlayPrefetched()
		{
			try
			{
				if (_prefClip == null) return false;
				AudioSource next = IdleSrc();
				AudioSource cur = ActiveSrc();
				if (next == null) return false;

				AudioClip clip = _prefClip;
				_prefClip = null;
				int playedIndex = _prefIndex;
				_prefIndex = -1;
				string path = _prefPath;
				if (playedIndex >= 0) _index = playedIndex;

				next.clip = clip;
				next.volume = ((VolumeCfg != null) ? VolumeCfg.Value : 0.8f);
				try
				{
					MusicPlayer mp = FindPlayer();
					if (mp != null && mp.outputMixerGroup_ != null) next.outputAudioMixerGroup = mp.outputMixerGroup_;
				}
				catch { }
				next.Play();
				if (cur != null && cur != next) { try { cur.Stop(); cur.clip = null; } catch { } }
				_activeSrc = (_activeSrc == 0) ? 1 : 0;
				_loadingPath = path;
				_currentName = Path.GetFileNameWithoutExtension(path);
				_active = true;
				try { _endAt = Time.realtimeSinceStartup + (clip.length > 1f ? clip.length : 60f); } catch { _endAt = Time.realtimeSinceStartup + 60f; }
				PushName(_currentName);
				try { if (!string.IsNullOrEmpty(_stateKey)) { _stateIndex[_stateKey] = _index; } } catch { }
				ManualLogSource l = Plugin.L;
				if (l != null) l.LogInfo("[music] 预载切换 " + Path.GetFileName(path));
				return true;
			}
			catch { return false; }
		}

		private static void PumpPending()
		{
			try
			{
				if (MusicReplace311._pendingClip == null) return;
				if (Time.realtimeSinceStartup < MusicReplace311._pendingAt) return;
				if (MusicReplace311._src == null) return;

				AudioClip clip = MusicReplace311._pendingClip;
				MusicReplace311._pendingClip = null;

				AudioSource next = MusicReplace311.IdleSrc();
				AudioSource cur = MusicReplace311.ActiveSrc();
				if (next == null) return;

				next.clip = clip;
				next.volume = ((MusicReplace311.VolumeCfg != null) ? MusicReplace311.VolumeCfg.Value : 0.8f);
				try
				{
					MusicPlayer musicPlayer = MusicReplace311.FindPlayer();
					if (musicPlayer != null && musicPlayer.outputMixerGroup_ != null)
					{
						next.outputAudioMixerGroup = musicPlayer.outputMixerGroup_;
					}
				}
				catch
				{
				}
				next.Play();
				try { _endAt = Time.realtimeSinceStartup + (clip.length > 1f ? clip.length : 60f); } catch { _endAt = Time.realtimeSinceStartup + 60f; }
				if (cur != null && cur != next)
				{
					try { cur.Stop(); cur.clip = null; } catch { }
				}
				MusicReplace311._activeSrc = (MusicReplace311._activeSrc == 0) ? 1 : 0;
				MusicReplace311._currentName = Path.GetFileNameWithoutExtension(MusicReplace311._loadingPath);
				try { if (!string.IsNullOrEmpty(MusicReplace311._stateKey)) MusicReplace311._stateIndex[MusicReplace311._stateKey] = MusicReplace311._index; } catch { }
				MusicReplace311._active = true;
				MusicReplace311.PushName(MusicReplace311._currentName);
				ManualLogSource l = Plugin.L;
				if (l != null)
				{
					l.LogInfo("[music] 播放 " + Path.GetFileName(MusicReplace311._loadingPath));
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000B6 RID: 182 RVA: 0x0000CC00 File Offset: 0x0000AE00
		private static void EnsureHost()
		{
			try
			{
				if (MusicReplace311._host == null)
				{
					MusicReplace311._host = new GameObject("ProjectRonin_Music");
					UnityEngine.Object.DontDestroyOnLoad(MusicReplace311._host);
					MusicReplace311._host.hideFlags = (HideFlags)61;
				}
				if (MusicReplace311._src == null)
				{
					MusicReplace311._src = MusicReplace311._host.AddComponent<AudioSource>();
					MusicReplace311._src.playOnAwake = false;
					MusicReplace311._src.loop = false;
					MusicReplace311._src.spatialBlend = 0f;
				}
				if (MusicReplace311._src2 == null)
				{
					MusicReplace311._src2 = MusicReplace311._host.AddComponent<AudioSource>();
					MusicReplace311._src2.playOnAwake = false;
					MusicReplace311._src2.loop = false;
					MusicReplace311._src2.spatialBlend = 0f;
				}
			}
			catch
			{
			}
		}


		// Token: 0x060000B8 RID: 184 RVA: 0x0000CD54 File Offset: 0x0000AF54
		private static void SweepPhoneText()
		{
			return;
			if (!ShowNames) return;
			try
			{
				if (MusicReplace311._active && !string.IsNullOrEmpty(MusicReplace311._currentName))
				{
					if (!string.IsNullOrEmpty(MusicReplace311._vanillaName))
					{
						Il2CppArrayBase<UIText> il2CppArrayBase = Uitexts();
						if (il2CppArrayBase != null)
						{
							for (int i = 0; i < il2CppArrayBase.Length; i++)
							{
								UIText uitext = il2CppArrayBase[i];
								if (!(uitext == null))
								{
									string text = "";
									try
									{
										text = uitext.Text ?? "";
									}
									catch
									{
									}
									if (!string.IsNullOrEmpty(text) && text.IndexOf(MusicReplace311._vanillaName, 5) >= 0)
									{
										try
										{
											string text2 = text.Replace(MusicReplace311._vanillaName, MusicReplace311._currentName);
											uitext.SetText(text2);
											TMP_Text text_ = uitext.text_;
											if (text_ != null)
											{
												text_.text = text2;
											}
										}
										catch
										{
										}
									}
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000B9 RID: 185 RVA: 0x0000CE5C File Offset: 0x0000B05C
		internal static void DumpTexts()
		{
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				MusicPlayer musicPlayer = MusicReplace311.FindPlayer();
				string text = "?";
				try
				{
					if (musicPlayer != null && musicPlayer._NowPlaying_k__BackingField != null)
					{
						text = musicPlayer._NowPlaying_k__BackingField.TrackName ?? "?";
					}
				}
				catch
				{
				}
				stringBuilder.Append("nowPlaying=").Append(text).Append(" ours=")
					.Append(MusicReplace311._currentName)
					.Append(" :: ");
				Il2CppArrayBase<UIText> il2CppArrayBase = Uitexts();
				if (il2CppArrayBase != null)
				{
					for (int i = 0; i < il2CppArrayBase.Length; i++)
					{
						UIText uitext = il2CppArrayBase[i];
						if (!(uitext == null) && !(uitext.gameObject == null) && MusicReplace311.UnderPhone(uitext.transform))
						{
							string text2 = "";
							try
							{
								text2 = uitext.Text ?? "";
							}
							catch
							{
							}
							stringBuilder.Append("[UI:").Append(MusicReplace311.ObjPath(uitext.transform)).Append("]")
								.Append(text2)
								.Append(" | ");
						}
					}
				}
				Il2CppArrayBase<TMP_Text> il2CppArrayBase2 = Tmps();
				if (il2CppArrayBase2 != null)
				{
					for (int j = 0; j < il2CppArrayBase2.Length; j++)
					{
						TMP_Text tmp_Text = il2CppArrayBase2[j];
						if (!(tmp_Text == null) && !(tmp_Text.gameObject == null) && MusicReplace311.UnderPhone(tmp_Text.transform))
						{
							string text3 = "";
							try
							{
								text3 = tmp_Text.text ?? "";
							}
							catch
							{
							}
							stringBuilder.Append("[TMP:").Append(MusicReplace311.ObjPath(tmp_Text.transform)).Append("]")
								.Append(text3)
								.Append(" | ");
						}
					}
				}
				ManualLogSource l = Plugin.L;
				if (l != null)
				{
					l.LogInfo("[music-dump] " + stringBuilder.ToString());
				}
			}
			catch (Exception ex)
			{
				ManualLogSource l2 = Plugin.L;
				if (l2 != null)
				{
					l2.LogWarning("[music-dump] " + ex.Message);
				}
			}
		}

		// Token: 0x060000BA RID: 186 RVA: 0x0000D0E4 File Offset: 0x0000B2E4
		private static void SweepSongName()
		{
			try
			{
				if (MusicReplace311._active && !string.IsNullOrEmpty(MusicReplace311._currentName))
				{
					Il2CppArrayBase<UIText> il2CppArrayBase = Uitexts();
					if (il2CppArrayBase != null)
					{
						for (int i = 0; i < il2CppArrayBase.Length; i++)
						{
							UIText uitext = il2CppArrayBase[i];
							if (!(uitext == null) && !(uitext.gameObject == null) && (uitext.gameObject.name ?? "").IndexOf("phone_songName", 5) >= 0)
							{
								try
								{
									uitext.SetText(MusicReplace311._currentName);
								}
								catch
								{
								}
								try
								{
									TMP_Text text_ = uitext.text_;
									if (text_ != null)
									{
										text_.text = MusicReplace311._currentName;
									}
								}
								catch
								{
								}
							}
						}
					}
					Il2CppArrayBase<TMP_Text> il2CppArrayBase2 = Tmps();
					if (il2CppArrayBase2 != null)
					{
						for (int j = 0; j < il2CppArrayBase2.Length; j++)
						{
							TMP_Text tmp_Text = il2CppArrayBase2[j];
							if (!(tmp_Text == null) && !(tmp_Text.gameObject == null) && (tmp_Text.gameObject.name ?? "").IndexOf("phone_songName", 5) >= 0)
							{
								try
								{
									tmp_Text.text = MusicReplace311._currentName;
								}
								catch
								{
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000BB RID: 187 RVA: 0x0000D280 File Offset: 0x0000B480
		private static void SweepTmpText()
		{
			try
			{
				if (MusicReplace311._active && !string.IsNullOrEmpty(MusicReplace311._currentName))
				{
					if (!string.IsNullOrEmpty(MusicReplace311._vanillaName))
					{
						Il2CppArrayBase<TMP_Text> il2CppArrayBase = Tmps();
						if (il2CppArrayBase != null)
						{
							for (int i = 0; i < il2CppArrayBase.Length; i++)
							{
								TMP_Text tmp_Text = il2CppArrayBase[i];
								if (!(tmp_Text == null))
								{
									string text = "";
									try
									{
										text = tmp_Text.text ?? "";
									}
									catch
									{
									}
									if (!string.IsNullOrEmpty(text) && text.IndexOf(MusicReplace311._vanillaName, 5) >= 0)
									{
										try
										{
											tmp_Text.text = text.Replace(MusicReplace311._vanillaName, MusicReplace311._currentName);
										}
										catch
										{
										}
									}
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000BC RID: 188 RVA: 0x0000D360 File Offset: 0x0000B560
		private static bool UnderPhone(Transform tr)
		{
			try
			{
				Transform transform = tr;
				int num = 0;
				while (num < 8 && transform != null)
				{
					string text = transform.name ?? "";
					if (text.IndexOf("phone", 5) >= 0)
					{
						return true;
					}
					if (text.IndexOf("music", 5) >= 0)
					{
						return true;
					}
					transform = transform.parent;
					num++;
				}
			}
			catch
			{
			}
			return false;
		}

		// Token: 0x060000BD RID: 189 RVA: 0x0000D3DC File Offset: 0x0000B5DC
		private static string ObjPath(Transform tr)
		{
			string text2;
			try
			{
				string text = tr.gameObject.name;
				Transform transform = tr.parent;
				int num = 0;
				while (num < 2 && transform != null)
				{
					text = transform.name + "/" + text;
					transform = transform.parent;
					num++;
				}
				text2 = text;
			}
			catch
			{
				text2 = "?";
			}
			return text2;
		}



		// Token: 0x060000C0 RID: 192 RVA: 0x0000D5A4 File Offset: 0x0000B7A4
		private static string _pendingName;
		private static float _pendingNameAt;

		private static void PushName(string name)
		{
			if (!ShowNames) return;
			// 延迟 0.2 秒再写：等通知弹窗构建完成，避免同帧布局重建造成卡顿
			_pendingName = name;
			_pendingNameAt = Time.realtimeSinceStartup + 0.2f;
		}

		private static void PumpName()
		{
			if (!ShowNames) return;
			if (string.IsNullOrEmpty(_pendingName)) return;
			if (Time.realtimeSinceStartup < _pendingNameAt) return;

			string name = _pendingName;
			_pendingName = null;
			try
			{
				Il2CppArrayBase<UINowPlaying> arr = NowPlayings();
				if (arr != null)
				{
					for (int i = 0; i < arr.Length; i++)
					{
						UINowPlaying u = arr[i];
						if (u == null) continue;
						try { u.SetTrackName(name); } catch { }
					}
				}
			}
			catch { }
		}

		// Token: 0x060000C1 RID: 193 RVA: 0x0000D614 File Offset: 0x0000B814
		static Il2CppArrayBase<MusicPlayer> _muteCache;

		private static void MuteGame()
		{
			try
			{
				var all = _muteCache;
				if (all == null || all.Length == 0)
				{
					all = Resources.FindObjectsOfTypeAll<MusicPlayer>();
					_muteCache = all;
				}
				if (all == null) return;
				for (int i = 0; i < all.Length; i++)
				{
					var mp = all[i];
					if (mp == null) continue;
					try { mp.VolumeScale = 0f; } catch { }
					try { if (mp.currentSource_ != null) mp.currentSource_.volume = 0f; } catch { }
					try { if (mp.sourceA_ != null) mp.sourceA_.volume = 0f; } catch { }
					try { if (mp.sourceB_ != null) mp.sourceB_.volume = 0f; } catch { }
				}
			}
			catch { }
		}

		// Token: 0x0400007D RID: 125
		internal static ConfigEntry<bool> Enabled;
		internal static bool ShowNames;
		internal static UnityEngine.KeyCode NextKey = UnityEngine.KeyCode.RightArrow;
		internal static UnityEngine.KeyCode PrevKey = UnityEngine.KeyCode.LeftArrow;

		// Token: 0x0400007E RID: 126
		internal static ConfigEntry<string> FolderCfg;

		// Token: 0x0400007F RID: 127
		internal static ConfigEntry<float> VolumeCfg;

		// Token: 0x04000080 RID: 128
		// Token: 0x04000081 RID: 129
		// Token: 0x04000082 RID: 130
		private static GameObject _host;

		// Token: 0x04000083 RID: 131
		private static AudioSource _src;
		private static AudioSource _src2;
		private static int _activeSrc;
		private static float _endAt;
		private static UnityWebRequest _prefReq;
		private static AudioClip _prefClip;
		private static string _prefPath;
		private static int _prefIndex = -1;
		private static readonly System.Collections.Generic.Dictionary<string, int> _stateIndex = new System.Collections.Generic.Dictionary<string, int>();
		private static readonly System.Collections.Generic.List<UnityWebRequest> _reqQueue = new System.Collections.Generic.List<UnityWebRequest>();

		private static AudioSource ActiveSrc()
		{
			return (_activeSrc == 0) ? _src : _src2;
		}

		private static AudioSource IdleSrc()
		{
			return (_activeSrc == 0) ? _src2 : _src;
		}

		// Token: 0x04000084 RID: 132
		private static readonly Dictionary<string, List<string>> _lists = new Dictionary<string, List<string>>();

		// Token: 0x04000085 RID: 133
		private static string _stateKey = "";
		private static float _nextHeavy;
		static float _nextStateChange;
		private static string _cachedState = "";

		// Token: 0x04000086 RID: 134
		private static string _prevState = "";

		// Token: 0x04000087 RID: 135
		private static string _trackState = "";

		// Token: 0x04000088 RID: 136
		private static int _index = -1;

		// Token: 0x04000089 RID: 137
		private static UnityWebRequest _req;

		// Token: 0x0400008A RID: 138
		private static string _loadingPath;

		// Token: 0x0400008B RID: 139
		private static AudioClip _readyClip;
		private static AudioClip _pendingClip;
		private static float _pendingAt;
		private static UnityWebRequest _reqKeep;

		// Token: 0x0400008C RID: 140
		private static float _scanT;

		// Token: 0x0400008D RID: 141
		private static float _nameT;

		// Token: 0x0400008E RID: 142
		private static float _switchT;

		// Token: 0x0400008F RID: 143
		internal static string _currentName = "";

		// Token: 0x04000090 RID: 144
		internal static bool _active;

		// Token: 0x04000091 RID: 145
		private static float _lastNextTap = -10f;

		// Token: 0x04000092 RID: 146
		private static float _lastLeftTap = -10f;

		// Token: 0x04000093 RID: 147
		internal static string _vanillaName = "";
	}
}