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
	internal float Tension = 20f;

	internal float TensionFireAt = 70f;

	private bool _tensionInTown;

	private bool _firstLogged;

	private bool? _tensionWasInTown;

	private float _fightStartTension = -1f;

	internal float TensionRate;

	internal string TensionWhy = "";

	private int _nextTensionTick;

	private int _nextTensionSample;

	private int _nextTensionTry;

	private int _nextTensionLog;

	private readonly List<float> _tensionHistory = new List<float>();

	private readonly List<(int sample, string what)> _tensionMarks = new List<(int, string)>();

	private int _tensionSamples;

	private bool _tensionFighting;

	private bool _fightHadOurs;

	internal int _tensionKills;

	internal int _tensionKillsOurs;

	private bool _townFireFailed;

	private int _wildCameAt;

	private bool _tensionRestored;

	internal float FireLevel
	{
		get
		{
			if (_tensionInTown)
			{
				return TensionFireAt;
			}
			float num = Math.Max(0.01f, Config.TensionFireMax - Config.TensionFireMin);
			float num2 = Math.Max(0f, Math.Min(1f, (TensionFireAt - Config.TensionFireMin) / num));
			float num3 = Config.TensionWildFireMin + num2 * Math.Max(0f, Config.TensionWildFireMax - Config.TensionWildFireMin);
			if (!FirstOfDay)
			{
				return num3;
			}
			return num3 * Config.TensionFirstMult;
		}
	}

	internal bool FirstOfDay
	{
		get
		{
			if (Chronicle.WildFreshAt > 0)
			{
				return GameMinutesNow() >= Chronicle.WildFreshAt;
			}
			return true;
		}
	}

	private void WildEncounterCame()
	{
		float num = Config.TensionFirstMinDays + (float)_rng.NextDouble() * Math.Max(0f, Config.TensionFirstMaxDays - Config.TensionFirstMinDays);
		Chronicle.WildFreshAt = GameMinutesNow() + (long)(num * 1440f);
		_firstLogged = false;
		GrudgeLog($"TENSION the next quick one (first of the day) after {num:0.0} game days");
		SaveWorld();
	}

	internal float OnScale(float townValue)
	{
		if (_tensionInTown)
		{
			return townValue;
		}
		float num = Math.Max(0.01f, Config.TensionFireMax - Config.TensionFireMin);
		float val = (townValue - Config.TensionFireMin) / num;
		if (townValue <= Config.TensionFireMin)
		{
			return townValue * Config.TensionWildFireMin / Math.Max(0.01f, Config.TensionFireMin);
		}
		return Config.TensionWildFireMin + Math.Min(1f, val) * Math.Max(0f, Config.TensionWildFireMax - Config.TensionWildFireMin);
	}

	private void UpdateTension(Ped player, bool canAmbush)
	{
		if (!Config.TensionEnabled || Game.GameTime < _nextTensionTick)
		{
			return;
		}
		_nextTensionTick = Game.GameTime + 1000;
		if (!_tensionRestored)
		{
			_tensionRestored = true;
			WorldState chronicle = Chronicle;
			if (chronicle.Tension >= 0f)
			{
				Tension = Math.Max(0f, Math.Min(100f, chronicle.Tension));
				if (chronicle.TensionFireAt > 0f)
				{
					TensionFireAt = Math.Max(Config.TensionFireMin, Math.Min(Config.TensionFireMax, chronicle.TensionFireAt));
				}
				GrudgeLog($"TENSION restored {Tension:0} (fires at {TensionFireAt:0})");
			}
		}
		bool flag = _currentTown != null || TownAt(player.Position) != null;
		if (_tensionWasInTown.HasValue && flag != _tensionWasInTown.Value)
		{
			float fireLevel = FireLevel;
			_tensionInTown = flag;
			float tension = Tension;
			if (fireLevel > 0.01f)
			{
				Tension = Math.Max(0f, Math.Min(100f, Tension * FireLevel / fireLevel));
			}
			if (!flag)
			{
				Tension = Math.Min(Tension, FireLevel * 0.7f);
			}
			if (Math.Abs(tension - Tension) >= 1f)
			{
				GrudgeLog(string.Format("TENSION {0}: {1:0} -> {2:0} (fires at {3:0} here)", flag ? "into town" : "out of town", tension, Tension, FireLevel));
			}
		}
		_tensionWasInTown = flag;
		_tensionInTown = flag;
		bool flag2 = ActiveRecord != null && _enc != null && _enc.Engaged;
		List<int> list = (from kv in _combatants
			where Game.GameTime - kv.Value < 2500
			select kv.Key).ToList();
		bool flag3 = list.Count > 0 || flag2;
		string text = NoSpawnReason(player);
		string text2 = ((text == null) ? PlanDueHere(player) : null);
		if (text2 != null)
		{
			text = text2;
		}
		List<string> list2 = new List<string>();
		float num = -1f;
		int tensionKills = _tensionKills;
		int tensionKillsOurs = _tensionKillsOurs;
		_tensionKills = 0;
		_tensionKillsOurs = 0;
		float num7;
		if (flag3)
		{
			int num2 = Math.Max(list.Count, flag2 ? 1 : 0);
			int num3 = list.Count((int h) => EncounterSide(h)) + ((flag2 && !list.Any((int h) => EncounterSide(h))) ? 1 : 0);
			if (!_tensionFighting)
			{
				_fightStartTension = Tension;
				Tension = Math.Max(0f, Tension - Config.TensionFightStartDrop);
				list2.Add($"a fight started -{Config.TensionFightStartDrop:0}");
			}
			if (num3 > 0 && !_fightHadOurs)
			{
				Tension = Math.Max(0f, Tension - Config.TensionFightStartDrop * (Config.TensionOursMult - 1f));
			}
			if (num3 > 0)
			{
				_fightHadOurs = true;
			}
			float num4 = ((num3 > 0) ? Config.TensionOursMult : 1f);
			float num5 = (Config.TensionDrainBase + Config.TensionDrainPerMan * (float)num2) * num4;
			float num6 = (float)(tensionKills - tensionKillsOurs) * Config.TensionKillDrain + (float)tensionKillsOurs * Config.TensionKillDrain * Config.TensionOursMult;
			num7 = (0f - num5) * 60f;
			Tension = Math.Max(0f, Tension - num6);
			if (!_fightHadOurs)
			{
				num = ((_fightStartTension < 0f) ? Tension : _fightStartTension) * Config.TensionStrangerKeep;
			}
			list2.Add(string.Format("in a fight with {0} {1}{2}{3} - draining", num2, (num2 == 1) ? "man" : "men", (num3 > 0) ? $" ({num3} an encounter's / a camp's, x{Config.TensionOursMult:0.#})" : "", (num6 > 0f) ? $", -{num6:0} for {tensionKills} killed" : ""));
		}
		else if (_menu.IsOpen || _bulletImpact.ViewerOpen || WorldBusy(player))
		{
			num7 = 0f;
			list2.Add((_menu.IsOpen || _bulletImpact.ViewerOpen) ? "held: menu open" : ("held: " + QuietWhy));
		}
		else if (_activeNemesisPed != null)
		{
			num7 = 0f;
			list2.Add("held: " + (ActiveRecord?.Name ?? "a nemesis") + " is out there");
		}
		else if (!Config.RandomAmbushes)
		{
			num7 = 0f;
			list2.Add("held: random encounters are off ([Ambush] RandomAmbushes)");
		}
		else if (text != null && text.StartsWith("resting"))
		{
			num7 = Config.TensionRisePerMin * 0.35f * (flag ? Config.TensionTownMult : 1f);
			list2.Add("resting x.35 (nothing comes while you sit)");
		}
		else if (text != null)
		{
			num7 = 0f;
			list2.Add("held: " + text);
		}
		else
		{
			num7 = Config.TensionRisePerMin;
			list2.Add($"base {Config.TensionRisePerMin:0.#}");
			int num8 = Gangs.All.Max((Gang g) => Notoriety.Level(NotorietyOf(g)));
			if (num8 > 0)
			{
				num7 += (float)num8 * Config.TensionNotorietyBonus;
				list2.Add($"notoriety +{(float)num8 * Config.TensionNotorietyBonus:0.#}");
			}
			int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
			if (cLOCK_HOURS >= 21 || cLOCK_HOURS < 5)
			{
				num7 += Config.TensionNightBonus;
				list2.Add($"night +{Config.TensionNightBonus:0.#}");
			}
			if (flag)
			{
				num7 *= Config.TensionTownMult;
				list2.Add($"town x{Config.TensionTownMult:0.#}");
			}
			if ((float)player.Health < (float)player.MaxHealth * 0.4f)
			{
				num7 *= Config.TensionHurtMult;
				list2.Add($"hurt x{Config.TensionHurtMult:0.#}");
			}
			if (!flag && WarHere.HasValue)
			{
				num7 *= 1.5f;
				list2.Add("war zone x1.5");
			}
			float num9 = WeatherTensionMult();
			if (num9 < 1f)
			{
				num7 *= num9;
				list2.Add($"storm x{num9:0.#}");
			}
			float encounterMult = Config.EncounterMult;
			if (encounterMult != 1f)
			{
				num7 *= encounterMult;
				list2.Add($"{NemesisSettings.EncounterAmountNames[Config.EncounterAmount].ToLowerInvariant()} x{encounterMult:0.#}");
			}
		}
		TensionRate = num7;
		Tension = Math.Max(0f, Math.Min(100f, Tension + num7 / 60f));
		if (num >= 0f && Tension < num)
		{
			Tension = num;
			TensionRate = 0f;
			list2.Add($"a stranger fight - held at {num:0}");
		}
		if (!flag)
		{
			_townFireFailed = false;
		}
		if (flag && _townFireFailed && Tension > FireLevel)
		{
			Tension = FireLevel;
			TensionRate = 0f;
			list2.Add("held at the fire level: nothing in town can come");
		}
		TensionWhy = string.Join(", ", list2);
		if (_tensionFighting && !flag3)
		{
			float tension2 = Tension;
			if (_fightHadOurs)
			{
				Tension = Math.Min(Tension, Config.TensionAfterFight);
			}
			Mark("fight over");
			GrudgeLog(string.Format("TENSION fight over{0}: {1:0} -> {2:0}", _fightHadOurs ? " (an encounter / a camp)" : " (a scrap)", tension2, Tension));
			_fightHadOurs = false;
			_fightStartTension = -1f;
		}
		_tensionFighting = flag3;
		if (Game.GameTime >= _nextTensionSample)
		{
			_nextTensionSample = Game.GameTime + 10000;
			_tensionHistory.Add(Tension);
			_tensionSamples++;
			while (_tensionHistory.Count > 90)
			{
				_tensionHistory.RemoveAt(0);
			}
			_tensionMarks.RemoveAll(((int sample, string what) m) => m.sample < _tensionSamples - 90);
		}
		if (Game.GameTime >= _nextTensionLog)
		{
			_nextTensionLog = Game.GameTime + 60000;
			StoreTension();
			GrudgeLog($"TENSION {Tension:0} (fires at {FireLevel:0}) {num7:+0.0;-0.0}/min - {TensionWhy}");
		}
		if (!flag && FirstOfDay && !_firstLogged)
		{
			_firstLogged = true;
			GrudgeLog($"TENSION first of the day: the wild fires at {FireLevel:0} until one comes");
		}
		bool flag4 = !flag && _wildCameAt != 0 && Game.GameTime - _wildCameAt < 300000;
		if (!(Tension >= FireLevel && Config.RandomAmbushes && canAmbush) || Game.GameTime < _nextTensionTry || flag3 || flag4 || text2 != null)
		{
			return;
		}
		_nextTensionTry = Game.GameTime + 20000;
		bool flag5 = _activeNemesisPed != null;
		int count = _skirmish.Count;
		int count2 = _raids.Count;
		int count3 = _hunters.Count;
		int worldCardsPlayed = _worldCardsPlayed;
		RunRandomEncounter(player, test: false);
		bool flag6 = (!flag5 && _activeNemesisPed != null) || _skirmish.Count > count || _raids.Count > count2 || _hunters.Count > count3 || _worldCardsPlayed > worldCardsPlayed;
		if (!flag6 && !flag && LastEncounterNote != null && !LastEncounterNote.StartsWith("held"))
		{
			string lastEncounterNote = LastEncounterNote;
			string text3 = WorldFallback(player);
			flag6 = (!flag5 && _activeNemesisPed != null) || _skirmish.Count > count || _raids.Count > count2 || _hunters.Count > count3 || _worldCardsPlayed > worldCardsPlayed || text3 != null;
			if (text3 != null)
			{
				LastEncounterNote = "the world instead (" + lastEncounterNote + "): " + text3;
			}
		}
		GrudgeLog(string.Format("TENSION fired at {0:0}/{1:0}: {2}{3}", Tension, FireLevel, flag6 ? "" : "nothing came - ", LastEncounterNote));
		if (!flag6 && flag)
		{
			_townFireFailed = true;
		}
		if (flag6 && !flag)
		{
			WildEncounterCame();
			_wildCameAt = Game.GameTime;
		}
		if (flag6)
		{
			TensionReleased(_enc?.Card?.Id ?? ((_skirmish.Count > count) ? "skirmish" : ((_raids.Count > count2) ? "patrol" : ((_hunters.Count > count3) ? "hunters" : "world card"))));
		}
	}

	private string WorldFallback(Ped player)
	{
		List<(string, Func<bool>)> list = new List<(string, Func<bool>)>();
		if (!WarHere.HasValue)
		{
			list.Add(("a skirmish", () => TrySkirmish(player, town: false, test: false, rolled: true)));
		}
		list.Add(("a patrol on the road", () => SpawnRoadPatrol(player)));
		foreach (var item in list.OrderBy<(string, Func<bool>), int>(((string what, Func<bool> run) _) => _rng.Next()))
		{
			bool flag = false;
			try
			{
				flag = item.Item2();
			}
			catch (Exception ex)
			{
				GrudgeLog("TENSION fallback " + item.Item1 + " failed: " + ex.Message);
			}
			if (flag)
			{
				_worldCardsPlayed++;
				return item.Item1;
			}
		}
		return null;
	}

	internal void TownHappening(string what)
	{
		if (Config.TensionEnabled && !(Tension < FireLevel * 0.75f))
		{
			_townFireFailed = false;
			TensionReleased(what);
		}
	}

	private void TensionReleased(string what)
	{
		float tension = Tension;
		TensionFireAt = Config.TensionFireMin + (float)_rng.NextDouble() * Math.Max(0f, Config.TensionFireMax - Config.TensionFireMin);
		Tension = Math.Min(Tension, OnScale(Config.TensionAfterEncounter));
		Mark(what);
		GrudgeLog($"TENSION released by {what}: {tension:0} -> {Tension:0}; next at {TensionFireAt:0}");
		StoreTension();
	}

	private bool EncounterSide(int h)
	{
		OurRole ourRole = RoleOf(h);
		if (ourRole != OurRole.Active && ourRole != OurRole.Crew && ourRole != OurRole.HiredGun && ourRole != OurRole.Camp && ourRole != OurRole.Rival)
		{
			return ourRole == OurRole.Hunter;
		}
		return true;
	}

	private void Mark(string what)
	{
		_tensionMarks.Add((_tensionSamples, what));
	}

	internal void StoreTension()
	{
		if (_tensionRestored)
		{
			WorldState chronicle = Chronicle;
			chronicle.Tension = Tension;
			chronicle.TensionFireAt = TensionFireAt;
			SaveWorld();
		}
	}

	internal void ResetTension()
	{
		Tension = 0f;
		_tensionFighting = false;
		Mark("respawn");
		StoreTension();
	}

	internal string TensionDebugLine()
	{
		if (Config.TensionEnabled)
		{
			return string.Format("TENSION {0:0} / fires at {1:0}   {2:+0.0;-0.0}/min   ({3}){4}", Tension, FireLevel, TensionRate, TensionWhy, (!Config.RandomAmbushes) ? "  -  RANDOM ENCOUNTERS OFF" : ((Tension >= FireLevel) ? "  -  READY, rolling" : ((TensionRate > 0.01f) ? $"  -  ~{Math.Max(0f, (FireLevel - Tension) / TensionRate):0.0} min to go" : "")));
		}
		return "tension director OFF ([Tension] Enabled) - the old timer rolls";
	}

	internal void DrawTensionGraph(float x, float y, float w, float h)
	{
		UiDraw.Rect(x, y, w, h, 20, 20, 20, 180);
		int num = 90;
		float num2 = w / (float)num;
		int num3 = num - _tensionHistory.Count;
		for (int i = 0; i < _tensionHistory.Count; i++)
		{
			float num4 = _tensionHistory[i] / 100f;
			float num5 = Math.Max(0.0009259259f, num4 * h);
			(int, int, int) tuple = ((_tensionHistory[i] >= FireLevel) ? Theme.Blood : ((_tensionHistory[i] >= FireLevel * 0.75f) ? Theme.Warn : Theme.Dim));
			UiDraw.Rect(x + (float)(num3 + i) * num2, y + h - num5, Math.Max(num2 - 0.00052083336f, 0.00052083336f), num5, tuple.Item1, tuple.Item2, tuple.Item3, 230);
		}
		float y2 = y + h - FireLevel / 100f * h;
		UiDraw.Rect(x, y2, w, 0.0009259259f, 214, 178, 95, 255);
		foreach (var tensionMark in _tensionMarks)
		{
			int num6 = tensionMark.sample - (_tensionSamples - _tensionHistory.Count) - 1;
			if (num6 >= 0 && num6 < _tensionHistory.Count)
			{
				UiDraw.Rect(x + (float)(num3 + num6) * num2, y, 0.0010416667f, h, 120, 170, 220, 220);
			}
		}
	}

	internal string TensionMarksLine()
	{
		IEnumerable<string> enumerable = from m in _tensionMarks.Where(((int sample, string what) m) => m.sample > _tensionSamples - 90).Reverse().Take(4)
			select $"{(_tensionSamples - m.sample) * 10 / 60}m {(_tensionSamples - m.sample) * 10 % 60:00}s ago {m.what}";
		return "graph: last 15 min, gold = fire level, blue = something came" + (enumerable.Any() ? ("  -  " + string.Join(",  ", enumerable)) : "");
	}
}
