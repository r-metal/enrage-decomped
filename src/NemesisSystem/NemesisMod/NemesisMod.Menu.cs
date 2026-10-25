using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NemesisSystem.BulletImpact;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;

namespace NemesisSystem;

public partial class NemesisMod
{
	private bool _worldPaused;

	private bool _hudHidden;

	private string _lastTab = "hier";

	private const int MenuFreezeMs = 60000;

	private const float MenuEaseScale = 0.1f;

	private int _pausedAt;

	private bool _pauseEased;

	private bool _pauseWhyLogged;

	private bool _missionPauseSaid;

	private bool _missionPauseNote;

	private bool _menuMissionLogged;

	private bool _feedCleared;

	internal static int LastSeatTaskAt = Environment.TickCount - 100000;

	private void StampNemesisHandles()
	{
		DateTime now = DateTime.UtcNow;
		List<KeyValuePair<int, NemesisRecord>> list = _worldNemeses.ToList();
		if (_activeNemesisPed != null && ActiveRecord != null)
		{
			list.Add(new KeyValuePair<int, NemesisRecord>(_activeNemesisPed.Handle, ActiveRecord));
		}
		foreach (KeyValuePair<int, NemesisRecord> item in list)
		{
			if (item.Value != null && ENTITY.DOES_ENTITY_EXIST(item.Key) && !ENTITY.IS_ENTITY_DEAD(item.Key))
			{
				item.Value.LastHandle = item.Key;
				item.Value.LastHandleModel = ENTITY.GET_ENTITY_MODEL(item.Key);
				item.Value.LastHandleUtc = now;
			}
		}
		SaveRoster();
		Chronicle.Bodies = (from kv in _nemesisBodies
			where kv.Value != null && ENTITY.DOES_ENTITY_EXIST(kv.Key)
			select $"{kv.Key}|{ENTITY.GET_ENTITY_MODEL(kv.Key)}|{now.Ticks}|{kv.Value.Id}").ToList();
		SaveWorld();
	}

	internal bool KnownBody(int h, uint model)
	{
		if (_nemesisBodies.ContainsKey(h))
		{
			return true;
		}
		List<string> bodies = Chronicle.Bodies;
		if (bodies == null)
		{
			return false;
		}
		long ticks = DateTime.UtcNow.Ticks;
		foreach (string item in bodies)
		{
			string[] array = item.Split('|');
			if (array.Length >= 3 && int.TryParse(array[0], out var result) && uint.TryParse(array[1], out var result2) && long.TryParse(array[2], out var result3) && result == h && result2 == model && ticks - result3 < 18000000000L)
			{
				return true;
			}
		}
		return false;
	}

	private void OnAborted(object sender, EventArgs e)
	{
		if (!ActiveCopy())
		{
			try
			{
				GrudgeLog("COPIES this copy stood down - nothing saved on its way out");
				return;
			}
			catch
			{
				return;
			}
		}
		try
		{
			SetWorldPaused(paused: false);
		}
		catch
		{
		}
		try
		{
			UiMouse.Release();
		}
		catch
		{
		}
		try
		{
			ClearEnemyDots();
		}
		catch
		{
		}
		try
		{
			Theme.SetBackdrop(on: false);
		}
		catch
		{
		}
		int stepNo = 0;
		Step(delegate
		{
			PLAYER.SET_PLAYER_WEAPON_DAMAGE_MODIFIER(Game.Player.Handle, 1f);
		});
		Step(FairGameOff);
		bool started = SafeFile.BeginWaitBudget(3000);
		Step(StampNemesisHandles);
		Step(WipeCompanion);
		Step(delegate
		{
			_bulletImpact.FlushLog(force: true);
		});
		Step(delegate
		{
			_bulletImpact.SaveXRayNow();
		});
		Step(delegate
		{
			SavePrints();
		});
		Step(delegate
		{
			SavePeople(force: true);
		});
		Step(ClearPlanBlips);
		Step(ClearWarMarkers);
		Step(RelockAllHideouts);
		Step(ClearLanding);
		Step(ClearTownRepBlips);
		Step(delegate
		{
			EndRival("abort");
		});
		Step(StopModMusic);
		Step(CleanupGestures);
		_aborting = true;
		Step(delegate
		{
			DespawnAftermath();
			ClearAftermathBlips();
		});
		Step(ReleaseActive);
		Step(delegate
		{
			ClearSighting(delete: true);
		});
		Step(delegate
		{
			if (_interro != null)
			{
				EndInterrogation(released: false);
			}
		});
		Step(ClearStaged);
		Step(CleanupCamps);
		Step(CleanupFuneral);
		Step(delegate
		{
			foreach (Fighter value in _fighters.Values)
			{
				EndGiveGround(value);
			}
		});
		Step(delegate
		{
			foreach (Raid item in _raids.ToList())
			{
				EndRaid(item);
			}
		});
		Step(DeletePrompts);
		Step(DeleteBoxPrompt);
		Step(DeleteCratePrompt);
		Step(EndEncounter);
		Step(DespawnGeneral);
		Step(delegate
		{
			EndSaloonFight(wipe: false);
		});
		Step(delegate
		{
			EndCheckpoint(Game.Player.Character, wipe: false);
		});
		Step(delegate
		{
			EndSupplyWagon(Game.Player.Character, wipe: false);
		});
		Step(delegate
		{
			foreach (string item2 in _liveSupplies.Keys.Where((string k) => k.StartsWith("town:")).ToList())
			{
				DespawnSupplies(item2);
			}
		});
		Step(delegate
		{
			QuietPrompts("abort");
		});
		Step(RemoveFortBlip);
		Step(ReleaseFortGates);
		Step(ReleaseUiSounds);
		Step(delegate
		{
			ClearTownPresence(delete: true);
		});
		Step(delegate
		{
			foreach (Ped item3 in _regulars.ToList())
			{
				DeleteOrRelease(item3);
			}
			_regulars.Clear();
		});
		_convo = null;
		Step(delegate
		{
			if (_warKind != null)
			{
				EndWarBuildUp(Game.Player.Character);
			}
			_skirmishEnds = 0;
			UpdateSkirmish(Game.Player.Character);
		});
		Step(delegate
		{
			_huntersEnd = 0;
			UpdateHunters(Game.Player.Character);
		});
		Step(StoreTension);
		Step(delegate
		{
			WritePerf(force: true);
		});
		Step(delegate
		{
			ForceSaves();
		});
		SafeFile.EndWaitBudget(started);
		Step(delegate
		{
			if (!SafeFile.WaitWrites(5000))
			{
				GrudgeLog("SAVE the background writer didn't finish in 5 s before the unload");
			}
		});
		Step(delegate
		{
			Task[] array;
			lock (_pointWrites)
			{
				array = _pointWrites.Where((Task t) => !t.IsCompleted).ToArray();
			}
			if (array.Length != 0)
			{
				Task.WaitAll(array, 3000);
			}
		});
		Crumbs.Close("Insert / abort");
		void Step(Action a)
		{
			string text = $"{++stepNo}: {a.Method.Name}";
			Crumbs.Mark("abort step " + text);
			try
			{
				a();
			}
			catch (Exception ex)
			{
				GrudgeLog("ABORT step " + text + " failed: " + ex.GetType().Name + ": " + ex.Message + " at " + ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim());
			}
		}
	}

	private void OpenLastTab()
	{
		if (_menu.IsOpen || _bulletImpact.ViewerOpen)
		{
			return;
		}
		if (_lastTab == "xray" && !WorldBusy(Game.Player.Character))
		{
			_bulletImpact.OpenViewer();
			return;
		}
		_menu.Open();
		if (_lastTab == "world")
		{
			_menu.ShowWorld();
		}
	}

	private void UpdateMenuPause()
	{
		if (_bulletImpact.ViewerOpen)
		{
			_lastTab = "xray";
		}
		else if (_menu.IsOpen)
		{
			_lastTab = (_menu.InWorld ? "world" : "hier");
		}
		bool flag = _menu.IsOpen || _bulletImpact.ViewerOpen;
		bool flag2 = false;
		try
		{
			flag2 = MISC.GET_MISSION_FLAG();
		}
		catch
		{
		}
		if (flag && flag2 && !_menuMissionLogged)
		{
			_menuMissionLogged = true;
			GrudgeLog("MENU open during a mission - the world isn't paused, the feed isn't cleared, no screen effect");
		}
		if (!flag)
		{
			_menuMissionLogged = false;
		}
		if (Theme.BackdropOn != (flag && !flag2))
		{
			Crumbs.Native("menu backdrop " + ((flag && !flag2) ? "on" : "off"));
		}
		Theme.SetBackdrop(flag && !flag2);
		if (flag != _hudHidden)
		{
			UiSound(flag ? "MENU_ENTER" : "MENU_CLOSE");
		}
		if (flag)
		{
			HUD.HIDE_HUD_AND_RADAR_THIS_FRAME();
			HUD._HIDE_HUD_THIS_FRAME();
			if (!_feedCleared && !flag2 && SeatTasksSettled)
			{
				_feedCleared = true;
				Crumbs.Native("menu feed clear");
				try
				{
					UIFEED._UI_FEED_CLEAR_ALL_CHANNELS();
				}
				catch
				{
				}
			}
		}
		else
		{
			_feedCleared = false;
		}
		_hudHidden = flag;
		bool flag3 = Config.PauseInMenus && (_menu.IsOpen || _bulletImpact.ViewerOpen) && !flag2 && (_worldPaused || SeatTasksSettled);
		if (flag3 != _worldPaused)
		{
			SetWorldPaused(flag3);
		}
		if (flag && !_pauseWhyLogged && (_worldPaused || SeatTasksSettled || flag2 || !Config.PauseInMenus))
		{
			_pauseWhyLogged = true;
			string text = (_worldPaused ? "paused" : ((!Config.PauseInMenus) ? "not paused - Pause in menus is off" : (flag2 ? "not paused - the game says a mission is active" : "not paused")));
			GrudgeLog("MENU open - the world " + text);
			if (flag2 && Config.PauseInMenus && !_missionPauseSaid)
			{
				_missionPauseSaid = true;
				_missionPauseNote = true;
			}
		}
		if (!flag)
		{
			_pauseWhyLogged = false;
			if (_missionPauseNote)
			{
				_missionPauseNote = false;
				ShowNotification("enRAGE: the world kept running behind the menu - the game says a mission is active, and pausing then can crash it. (Another mod can leave that on.)");
			}
		}
		if (_worldPaused)
		{
			MISC.SET_TIME_SCALE(_pauseEased ? 0.1f : 0f);
		}
		if (_worldPaused && !_pauseEased && Environment.TickCount - _pausedAt > 60000)
		{
			_pauseEased = true;
			Crumbs.Native("menu time scale eased");
			MISC.SET_TIME_SCALE(0.1f);
			GrudgeLog($"MENU open {60} s - the world runs on in slow motion (x{0.1f}) instead of a full stop");
		}
	}

	private bool MouseMenuOpen
	{
		get
		{
			if (!_menu.IsOpen && !_bulletImpact.ViewerOpen && _tg == null && !_iniPanel.IsOpen && !_welcome.IsOpen)
			{
				return _spawnMenuOpen;
			}
			return true;
		}
	}

	internal void OpenSetupScreen()
	{
		_menu.Close();
		UpdateMenuPause();
		_welcome.OpenSetup(thenGuide: false);
		GrudgeLog("SETUP opened from the menu");
	}

	internal void OpenGuide()
	{
		_menu.Close();
		UpdateMenuPause();
		_welcome.Open();
		GrudgeLog("WELCOME guide opened from the menu");
	}

	private void GoToTab(MenuTab tab)
	{
		bool viewerOpen = _bulletImpact.ViewerOpen;
		MenuTab menuTab = (viewerOpen ? MenuTab.XRay : (_menu.InWorld ? MenuTab.World : MenuTab.Hierarchy));
		if (tab == menuTab || (tab == MenuTab.XRay && WorldBusy(Game.Player.Character)))
		{
			return;
		}
		UiSound((tab > menuTab) ? "BUMPER_RIGHT" : "BUMPER_LEFT");
		if (tab == MenuTab.XRay)
		{
			_menu.Close();
			_bulletImpact.OpenViewer();
			return;
		}
		if (viewerOpen)
		{
			_bulletImpact.CloseViewer();
			_menu.Open();
		}
		if (tab == MenuTab.World)
		{
			_menu.ShowWorld();
		}
		else
		{
			_menu.ShowHierarchy();
		}
	}

	internal static void NoteSeatTask()
	{
		LastSeatTaskAt = Environment.TickCount;
	}

	private static bool SeatTasksSettled => Environment.TickCount - LastSeatTaskAt >= 1500;

	private void SetWorldPaused(bool paused)
	{
		if (paused != _worldPaused)
		{
			_worldPaused = paused;
			_pausedAt = Environment.TickCount;
			_pauseEased = false;
			Crumbs.Native("menu world " + (paused ? "paused" : "running"));
			Crumbs.Native($"menu time scale {((!paused) ? 1 : 0)}");
			MISC.SET_TIME_SCALE(paused ? 0f : 1f);
			CLOCK.PAUSE_CLOCK(paused, 0uL);
		}
	}

	internal void RequestSummon(NemesisRecord rec)
	{
		_summonRequested = rec;
	}

	internal void OpenXRays(NemesisRecord rec)
	{
		_menu.Toggle();
		_bulletImpact.OpenViewer(rec.Id);
	}

	internal int XRayShotsOn(NemesisRecord rec)
	{
		return _bulletImpact.ShotsOnNemesis(rec.Id);
	}

	internal string CampLine(NemesisRecord n)
	{
		if (!n.IsAlive)
		{
			return "-";
		}
		if (IsOfficer(n))
		{
			CampSite campSite = CampOf(n);
			if (campSite == null)
			{
				return "No camp";
			}
			if (!n.CampKnown)
			{
				return "Unknown - interrogate his men to find it";
			}
			return campSite.Label + "  (" + GetZoneName(SitePos(campSite)) + ")";
		}
		NemesisRecord nemesisRecord = BossOf(n);
		if (nemesisRecord != null)
		{
			Gang gang = Gangs.Of(nemesisRecord);
			if (gang == null || !gang.RankInName)
			{
				return "Rides with " + OfficerTitle(nemesisRecord) + " " + nemesisRecord.FullName;
			}
			return "Serves under " + nemesisRecord.Name;
		}
		return "Drifting - no captain";
	}

	internal Vector3? HomeOf(NemesisRecord n)
	{
		if (n.Gang != null)
		{
			CampSite campSite = CampOf(IsOfficer(n) ? n : BossOf(n));
			if (campSite != null)
			{
				return SitePos(campSite);
			}
		}
		if (!n.HasOrigin)
		{
			return null;
		}
		return new Vector3(n.OriginX, n.OriginY, n.OriginZ);
	}

	internal bool InHuntingRange(NemesisRecord n, Vector3 playerPos)
	{
		if (n.HateTier == HateTier.Murderous)
		{
			return true;
		}
		Vector3? vector = HomeOf(n);
		if (!vector.HasValue)
		{
			return true;
		}
		return playerPos.DistanceTo(vector.Value) <= ((n.HateTier == HateTier.Vengeful) ? Config.VengefulRange : Config.ResentfulRange) * (Quirks.HasSkill(n, "tracker") ? 1.5f : 1f);
	}

	internal string RoamText(NemesisRecord n)
	{
		if (n.HateTier == HateTier.Murderous)
		{
			return "hunts you anywhere";
		}
		string arg = ((n.Gang != null && HomeOf(n).HasValue) ? "his camp" : n.KillLocation);
		int num = (int)((n.HateTier == HateTier.Vengeful) ? Config.VengefulRange : Config.ResentfulRange);
		if (HomeOf(n).HasValue)
		{
			return $"won't stray far from {arg} (about {num} m)";
		}
		return "stays near where it happened";
	}

	internal bool CanTrackCamp(NemesisRecord n)
	{
		if (IsOfficer(n) && n.CampKnown && CampOf(n) != null && n.CanHunt)
		{
			return !n.PresumedDead;
		}
		return false;
	}
}
