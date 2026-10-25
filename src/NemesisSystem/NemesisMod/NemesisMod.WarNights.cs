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
	private int _nextWarEncRoll;

	private int _warEncAllowed;

	private int _nextDistantCheck;

	private int _nextDistantAllowed;

	internal bool SkirmishDistant;

	private string _distantNoWhy;

	private string _distantSpotNote;

	private int _distantNoAt;

	private long _sleepWatchMin = -1L;

	private int _nextSleepWatch;

	private int _sleepBattleAt;

	private const int SleepBattlePct = 25;

	private static readonly (string key, string name)[] CauseWords = new(string, string)[19]
	{
		("card", "Card Table"),
		("horse", "Horse Thief"),
		("herd", "Stolen Herd"),
		("payroll", "Payroll"),
		("barn", "Burned Barn"),
		("strung", "Hanging Tree"),
		("hanged", "Hanging Tree"),
		("woman", "Sweetheart"),
		("brother", "Missing Brother"),
		("insult", "Insult"),
		("wagon", "Wagon Road"),
		("burned", "Fire"),
		("drunk", "Whiskey"),
		("saloon", "Saloon"),
		("revenge", "Vengeance"),
		("break", "Vengeance"),
		("camp", "Camp"),
		("ground", "Land"),
		("rich", "Money")
	};

	private static readonly string[] Ordinals = new string[6] { "", "Second ", "Third ", "Fourth ", "Fifth ", "Sixth " };

	internal static bool WarNight
	{
		get
		{
			int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
			if (cLOCK_HOURS < 21)
			{
				return cLOCK_HOURS < 5;
			}
			return true;
		}
	}

	private void UpdateWarPatrolRoll(Ped player)
	{
		if (Game.GameTime < _nextWarPatrolRoll)
		{
			return;
		}
		_nextWarPatrolRoll = Game.GameTime + 60000;
		(Gang, Gang)? warHere = WarHere;
		if (!warHere.HasValue || Game.GameTime < _warPatrolAllowed || (FightHoldsWar() && PatrolInTheFight(player)) || WorldBusy(player) || MISC.GET_MISSION_FLAG() || InsideAnyTown(player.Position) || NoSpawnReason(player) != null || _raids.Count((Raid r) => r.CampId == null) >= 2)
		{
			return;
		}
		District d = CurrentDistrict;
		WarRecord warRecord = ((d == null) ? null : Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key));
		int num = 40;
		if (warRecord != null)
		{
			InitHeat(warRecord);
			num = warRecord.Heat;
		}
		double num2 = (double)num / 4.0 * (WarNight ? 2.0 : 1.0);
		double num3 = _rng.NextDouble() * 100.0;
		if (num3 >= num2)
		{
			return;
		}
		List<Gang> list = new Gang[2]
		{
			warHere.Value.Item1,
			warHere.Value.Item2
		}.Where((Gang g) => g != null && FactionModels.ContainsKey(g.Id) && !_raids.Any((Raid r) => r.Gang == g && r.CampId == null) && SuppliesOf(g) > 0).ToList();
		if (list.Count != 0)
		{
			Gang gang = list[_rng.Next(list.Count)];
			if (SpawnRoadPatrol(player, force: false, gang))
			{
				_warPatrolAllowed = Game.GameTime + (WarNight ? 120000 : 180000);
				GrudgeLog(string.Format("WAR patrol roll: heat {0}, {1:0}%{2} - rolled {3:0}: the {4} ride the road", num, num2, WarNight ? " (night)" : "", num3, gang.Name));
			}
		}
	}

	private void UpdateWarEncounterRoll(Ped player)
	{
		if (Game.GameTime < _nextWarEncRoll)
		{
			return;
		}
		_nextWarEncRoll = Game.GameTime + 90000;
		if (!WarHere.HasValue || Game.GameTime < _warEncAllowed || _skirmish.Count > 0 || _warKind != null || FightHoldsWar() || WorldBusy(player) || MISC.GET_MISSION_FLAG() || LawAfterYou() || InsideAnyTown(player.Position) || NoSpawnReason(player) != null || _enc != null)
		{
			return;
		}
		District d = CurrentDistrict;
		WarRecord warRecord = ((d == null) ? null : Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key));
		int num = 40;
		if (warRecord != null)
		{
			InitHeat(warRecord);
			num = warRecord.Heat;
		}
		CLOCK.GET_CLOCK_HOURS();
		double num2 = (double)num / 5.0 * (WarNight ? 2.0 : 1.0);
		double num3 = _rng.NextDouble() * 100.0;
		if (num3 >= num2)
		{
			GrudgeLog($"WAR encounter roll: heat {num}, {num2:0}% - rolled {num3:0}, nothing");
			return;
		}
		bool mounted = PED.IS_PED_ON_MOUNT(player.Handle);
		List<(EncounterCard, int)> list = (from c in Cards
			where (WarCardStage(c.Stage) || c.Stage == "skirmish") && CardFits(c, null, town: false, mounted, test: false)
			select (c: c, wgt: Math.Max(1, CardChance(c)))).ToList();
		while (list.Count > 0)
		{
			int t = _rng.Next(list.Sum<(EncounterCard, int)>(((EncounterCard c, int wgt) x) => x.wgt));
			(EncounterCard, int) tuple = list.First<(EncounterCard, int)>(((EncounterCard c, int wgt) x) => (t -= x.wgt) < 0);
			list.Remove(tuple);
			var (encounterCard, _) = tuple;
			if ((encounterCard.Stage == "skirmish") ? TrySkirmish(player, town: false, test: false, rolled: true) : RunWarCard(encounterCard.Stage, player))
			{
				UseCard(encounterCard);
				_warEncAllowed = Game.GameTime + (WarNight ? 180000 : 300000);
				GrudgeLog(string.Format("WAR encounter roll: heat {0}, {1:0}% - rolled {2:0}: {3} played ({4})", num, num2, num3, encounterCard.Id, (encounterCard.Stage == "skirmish") ? SkirmishNote : encounterCard.Stage));
				return;
			}
		}
		GrudgeLog($"WAR encounter roll: heat {num}, {num2:0}% - rolled {num3:0}, but nothing fitted or could be placed");
	}

	private void UpdateDistantBattle(Ped player)
	{
		WatchSleep(player);
		try
		{
			WatchCamped(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("WAR camped failed: " + ex.Message);
		}
		try
		{
			UpdateRoadBodies(player);
		}
		catch (Exception ex2)
		{
			GrudgeLog("WAR road bodies failed: " + ex2.Message);
		}
		try
		{
			UpdateWarEncounterRoll(player);
		}
		catch (Exception ex3)
		{
			GrudgeLog("WAR encounter roll failed: " + ex3.Message);
		}
		try
		{
			UpdateWarPatrolRoll(player);
		}
		catch (Exception ex4)
		{
			GrudgeLog("WAR patrol roll failed: " + ex4.Message);
		}
		try
		{
			UpdateNightShelling(player);
		}
		catch (Exception ex5)
		{
			GrudgeLog("WAR night blasts failed: " + ex5.Message);
		}
		if (Game.GameTime < _nextDistantCheck)
		{
			return;
		}
		_nextDistantCheck = Game.GameTime + 60000;
		if (!Config.DistantBattles)
		{
			DistantNo("turned off ([World] DistantBattles)");
			return;
		}
		if (Game.GameTime < _nextDistantAllowed)
		{
			DistantNo($"too soon after the last ({(_nextDistantAllowed - Game.GameTime) / 1000} s to wait)");
			return;
		}
		if (_skirmish.Count > 0 || _warKind != null)
		{
			DistantNo("a skirmish / war encounter is already on");
			return;
		}
		if (_wildCameAt != 0 && Game.GameTime - _wildCameAt < 300000)
		{
			DistantNo("an encounter came less than 5 minutes ago");
			return;
		}
		if (FightHoldsWar())
		{
			DistantNo("you're in a fight");
			return;
		}
		if (WorldBusy(player))
		{
			DistantNo("the world is busy (fade / no control)");
			return;
		}
		if (MISC.GET_MISSION_FLAG())
		{
			DistantNo("on a mission");
			return;
		}
		if (LawAfterYou())
		{
			DistantNo("the law is after you");
			return;
		}
		if (InsideAnyTown(player.Position))
		{
			DistantNo("in a town");
			return;
		}
		string text = NoSpawnReason(player);
		if (text != null && (text.StartsWith("on a mission") || text.StartsWith("settling") || text.StartsWith("on a train")))
		{
			DistantNo(text);
			return;
		}
		if (_enc != null && _enc.Engaged)
		{
			DistantNo("your own encounter is on");
			return;
		}
		District currentDistrict = CurrentDistrict;
		Vector3 position = player.Position;
		WarRecord warRecord = null;
		List<string> list = new List<string>();
		foreach (WarRecord w in Wars.Where((WarRecord x) => x.Result == null))
		{
			District district = Districts.FirstOrDefault((District x) => x.Key == w.District);
			if (district == null)
			{
				continue;
			}
			string text2 = WarName(w);
			if (TruceBetween(w.A, w.B))
			{
				list.Add(text2 + ": a truce");
				continue;
			}
			if (!FactionModels.ContainsKey(w.A) || !FactionModels.ContainsKey(w.B))
			{
				list.Add(text2 + ": a side has no troops");
				continue;
			}
			float num = new Vector3(district.X - position.X, district.Y - position.Y, 0f).Length();
			if (district != currentDistrict && num >= 1800f)
			{
				list.Add($"{text2}: {num:0} m off (needs under 1800)");
				continue;
			}
			InitHeat(w);
			if (warRecord == null || w.Heat > warRecord.Heat)
			{
				warRecord = w;
			}
		}
		if (warRecord == null)
		{
			DistantNo((list.Count == 0) ? "no war on" : ("no war near you - " + string.Join("; ", list)));
			return;
		}
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		double num2 = (double)warRecord.Heat / 5.0 * (WarNight ? 2.0 : 1.0);
		double num3 = _rng.NextDouble() * 100.0;
		_distantNoWhy = null;
		string text3 = string.Format("WAR distant battle roll: {0}, heat {1}{2} - {3:0}%, rolled {4:0}", WarName(warRecord), warRecord.Heat, WarNight ? ", night" : "", num2, num3);
		if (num3 >= num2)
		{
			GrudgeLog(text3 + ": no");
			return;
		}
		Gang gang = Gangs.ById(warRecord.A);
		Gang gang2 = Gangs.ById(warRecord.B);
		if (gang != null && gang2 != null)
		{
			if (_rng.Next(2) == 0)
			{
				Gang gang3 = gang2;
				gang2 = gang;
				gang = gang3;
			}
			GrudgeLog(text3 + ": yes");
			_distantSpotNote = null;
			if (TrySkirmish(player, town: false, test: false, rolled: true, (gang, gang2), distant: true))
			{
				_nextDistantAllowed = Game.GameTime + (WarNight ? 150000 : 240000);
				GrudgeLog(string.Format("WAR distant battle: the {0} and the {1}, {2} (heat {3}{4})", gang.Name, gang2.Name, Away(position, _skirmishAt), warRecord.Heat, (cLOCK_HOURS >= 21 || cLOCK_HOURS < 5) ? ", night" : ""));
			}
			else
			{
				_nextDistantAllowed = Game.GameTime + 120000;
				GrudgeLog("WAR distant battle: didn't happen - " + (_distantSpotNote ?? "the skirmish wouldn't start") + " (next try in 2 min)");
			}
		}
	}

	private void DistantNo(string why)
	{
		if (!(why == _distantNoWhy) || Game.GameTime - _distantNoAt >= 300000)
		{
			_distantNoWhy = why;
			_distantNoAt = Game.GameTime;
			GrudgeLog("WAR distant battle roll: skipped - " + why);
		}
	}

	private void WatchSleep(Ped player)
	{
		if (Game.GameTime < _nextSleepWatch)
		{
			return;
		}
		_nextSleepWatch = Game.GameTime + 2000;
		long num = GameMinutesNow();
		long sleepWatchMin = _sleepWatchMin;
		_sleepWatchMin = num;
		if (_sleepBattleAt != 0 && Game.GameTime >= _sleepBattleAt)
		{
			_sleepBattleAt = 0;
			WakeBattle(player);
		}
		if (sleepWatchMin < 0 || num - sleepWatchMin < 120 || num - sleepWatchMin > 4320 || Game.GameTime - _loadScreenAt < 30000 || Game.GameTime - _lastJumpAt < 30000 || InsideAnyTown(player.Position))
		{
			return;
		}
		long val = sleepWatchMin / 720;
		long num2 = num / 720;
		long num3 = Math.Max(val, Chronicle.SleepRollWindow + 1);
		int num4 = (int)Math.Max(0L, Math.Min(2L, num2 - num3 + 1));
		Chronicle.SleepRollWindow = Math.Max(Chronicle.SleepRollWindow, num2);
		SaveWorld();
		if (num4 == 0)
		{
			GrudgeLog($"WAR sleep {(num - sleepWatchMin) / 60}h: this half day already rolled");
			return;
		}
		bool flag = false;
		bool flag2 = (num - sleepWatchMin >= 480 || WarNightAt(sleepWatchMin) || WarNightAt(num)) && NearWar(player);
		if (flag2)
		{
			num4 = 1;
			flag = _rng.Next(100) < 75;
		}
		else
		{
			for (int i = 0; i < num4; i++)
			{
				if (flag)
				{
					break;
				}
				flag = _rng.Next(100) < 25;
			}
		}
		GrudgeLog(string.Format("WAR sleep {0}h: {1} roll{2} at {3}%{4} - {5}", (num - sleepWatchMin) / 60, num4, (num4 == 1) ? "" : "s", flag2 ? 75 : 25, flag2 ? " (a night near a war)" : "", flag ? "gunfire when you wake" : "a quiet night"));
		if (flag)
		{
			_sleepBattleAt = Game.GameTime + 25000 + _rng.Next(35000);
		}
	}

	private bool WarNightAt(long gameMin)
	{
		int hour = FromGameMinutes(gameMin).Hour;
		if (hour < 21)
		{
			return hour < 5;
		}
		return true;
	}

	private void WakeBattle(Ped player)
	{
		if (!Config.DistantBattles || _skirmish.Count > 0 || _warKind != null || WorldBusy(player) || MISC.GET_MISSION_FLAG() || LawAfterYou() || InsideAnyTown(player.Position))
		{
			GrudgeLog("WAR wake battle: not now (busy / mission / wanted / town / a fight on) - skipped");
			return;
		}
		Vector3 me = player.Position;
		(Gang, Gang)? force = null;
		WarRecord warRecord = Wars.Where((WarRecord x) => x.Result == null && !TruceBetween(x.A, x.B) && FactionModels.ContainsKey(x.A) && FactionModels.ContainsKey(x.B)).OrderBy(delegate(WarRecord x)
		{
			District district2 = DistrictByKey(x.District);
			return (district2 != null) ? new Vector3(district2.X - me.X, district2.Y - me.Y, 0f).Length() : 1E+09f;
		}).FirstOrDefault();
		District district = ((warRecord != null) ? DistrictByKey(warRecord.District) : null);
		if (district != null && (district == CurrentDistrict || new Vector3(district.X - me.X, district.Y - me.Y, 0f).Length() < 2500f))
		{
			force = (Gangs.ById(warRecord.A), Gangs.ById(warRecord.B));
		}
		if (!force.HasValue)
		{
			District here = CurrentDistrict;
			if (here != null)
			{
				(Gang, Gang) tuple = (from x in Gangs.All
					where !x.StoryOnly && !IsDestroyed(x) && FactionModels.ContainsKey(x.Id) && HoldOf(here, x.Id) >= 3
					from y in from y in Gangs.All
						where string.CompareOrdinal(x.Id, y.Id) < 0 && !y.StoryOnly && !IsDestroyed(y) && FactionModels.ContainsKey(y.Id) && HoldOf(here, y.Id) >= 3 && Relation(x.Id, y.Id) <= Config.ClashLine && !TruceBetween(x.Id, y.Id)
						select y
					select (x: x, y: y) into t
					orderby Relation(t.x.Id, t.y.Id)
					select t).FirstOrDefault();
				if (tuple.Item1 != null)
				{
					force = (tuple.Item1, tuple.Item2);
				}
			}
		}
		if (!force.HasValue || force.Value.Item1 == null || force.Value.Item2 == null)
		{
			GrudgeLog("WAR wake battle: no war or cold outfits near you - nothing");
		}
		else if (TrySkirmish(player, town: false, test: false, rolled: true, force, distant: true))
		{
			_nextDistantAllowed = Game.GameTime + 240000;
			GrudgeLog("WAR wake battle: the " + force.Value.Item1.Name + " and the " + force.Value.Item2.Name + ", " + Away(me, _skirmishAt));
		}
		else
		{
			GrudgeLog("WAR wake battle: no hidden, loaded ground 200-250 m away");
		}
	}

	private Vector3 DistantSpot(Ped player)
	{
		Vector3 position = player.Position;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		for (int i = 0; i < 16; i++)
		{
			double num6 = _rng.NextDouble() * Math.PI * 2.0;
			float num7 = 200f + (float)_rng.NextDouble() * 35f;
			Vector3 raw = new Vector3(position.X + (float)Math.Cos(num6) * num7, position.Y + (float)Math.Sin(num6) * num7, position.Z);
			STREAMING.REQUEST_COLLISION_AT_COORD(raw.X, raw.Y, raw.Z);
			if (!GroundOk(raw, out var p))
			{
				num++;
				continue;
			}
			if (InsideAnyTown(p))
			{
				num2++;
				continue;
			}
			if (!OnLand(p) || !Dry(p))
			{
				num3++;
				continue;
			}
			if (GangCamps.Any((Vector3 c) => new Vector3(c.X - p.X, c.Y - p.Y, 0f).Length() < 190f))
			{
				num4++;
				continue;
			}
			if (!HiddenFromView(player, p))
			{
				num5++;
				continue;
			}
			return p;
		}
		_distantSpotNote = $"no spot 200-235 m off out of your sight (16 tried: {num} no loaded ground, {num2} in a town, {num3} water, {num4} near the gang's camp, {num5} in view)";
		return Vector3.Zero;
	}

	private static string Adjective(Gang g)
	{
		string text = (g.Name.StartsWith("The ") ? g.Name.Substring(4) : g.Name);
		if (text.EndsWith("s") && !text.EndsWith("ss"))
		{
			text = text.Substring(0, text.Length - 1);
		}
		return text;
	}
}
