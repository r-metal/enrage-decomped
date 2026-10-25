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
	private int _nextRoadBodies;

	private int _roadBodiesAllowed;

	private int _restSince;

	private int _upSince;

	private bool _restRolled;

	private const int CampedBattlePct = 75;

	private int _nextWarPatrolRoll;

	private int _warPatrolAllowed;

	private WorldEvent SimWarBattle(WarRecord w, District d, Gang a, Gang b, long now, Ped player, Vector3? at = null)
	{
		Vector3 vector = at ?? Vector3.Zero;
		for (int i = 0; i < 10; i++)
		{
			if (!(vector == Vector3.Zero))
			{
				break;
			}
			double num = _rng.NextDouble() * Math.PI * 2.0;
			float num2 = 150f + (float)_rng.NextDouble() * 500f;
			Vector3 vector2 = new Vector3(d.X + (float)Math.Cos(num) * num2, d.Y + (float)Math.Sin(num) * num2, 100f);
			if ((!(player != null) || !(new Vector3(vector2.X - player.Position.X, vector2.Y - player.Position.Y, 0f).Length() < Config.WorldMinDistance)) && !InsideAnyTown(vector2) && OnLand(vector2))
			{
				vector = vector2;
			}
		}
		if (vector == Vector3.Zero)
		{
			return null;
		}
		if (_rng.Next(2) == 0)
		{
			Gang gang = b;
			b = a;
			a = gang;
		}
		string text = "near " + GetZoneName(vector);
		NemesisRecord nemesisRecord = Named(a);
		NemesisRecord nemesisRecord2 = Named(b);
		int num3 = 3 + _rng.Next(4);
		int num4 = 3 + _rng.Next(4);
		float num5 = ((float)num3 * 1.5f + ((nemesisRecord != null) ? (2f + (float)nemesisRecord.Rank) : 0f) + (float)_rng.NextDouble() * 6f) * Stretched(a) * MenPower(a) - Malus(a);
		float num6 = ((float)num4 * 1.5f + ((nemesisRecord2 != null) ? (2f + (float)nemesisRecord2.Rank) : 0f) + (float)_rng.NextDouble() * 6f) * Stretched(b) * MenPower(b) - Malus(b);
		bool flag = num5 >= num6;
		Gang gang2 = (flag ? a : b);
		Gang gang3 = (flag ? b : a);
		int hour = FromGameMinutes(now).Hour;
		string[] array = ((hour >= 21 || hour < 5) ? NightTitles : BattleTitles);
		WorldEvent worldEvent = NewEvent("CLASH", now);
		worldEvent.Place = text;
		worldEvent.HasPos = true;
		worldEvent.X = vector.X;
		worldEvent.Y = vector.Y;
		worldEvent.Z = vector.Z;
		worldEvent.Gangs.Add(a.Id);
		worldEvent.Gangs.Add(b.Id);
		worldEvent.Title = array[_rng.Next(array.Length)].Replace("{a}", a.Name).Replace("{b}", b.Name).Replace("{p}", text);
		worldEvent.Lines.Add($"Part of the war in {d.Name}. {a.Name}: {num3 + ((nemesisRecord != null) ? 1 : 0)} men.  {b.Name}: {num4 + ((nemesisRecord2 != null) ? 1 : 0)} men.");
		worldEvent.Lines.Add("The " + gang2.Name + " had the better of it.");
		(NemesisRecord, Gang, bool)[] array2 = new(NemesisRecord, Gang, bool)[2]
		{
			(nemesisRecord, a, flag),
			(nemesisRecord2, b, !flag)
		};
		for (int j = 0; j < array2.Length; j++)
		{
			var (nemesisRecord3, gang4, flag2) = array2[j];
			if (nemesisRecord3 != null)
			{
				ResolveFighter(nemesisRecord3, flag2, worldEvent, "the " + ((gang4 == a) ? b : a).Name, gun: true, 0.5f);
			}
			int num7 = (int)Math.Round((float)((flag2 ? _rng.Next(2) : (1 + _rng.Next(2))) * Config.WorldLethality) / 100f);
			if (num7 > 0)
			{
				worldEvent.UnnamedDead[gang4.Id] = num7;
			}
		}
		if (worldEvent.People.Any((WorldPart p) => p.Outcome == "KILLED") || worldEvent.UnnamedDead.Count > 0)
		{
			worldEvent.AftermathUntil = now + (long)Config.WorldAftermathHours * 60L;
		}
		ShiftControl(d, gang2, 2, "won a fight with the " + gang3.Name);
		ShiftControl(d, gang3, -LossDown(d, gang3, 2), "lost a fight to the " + gang2.Name);
		w.BattlesToday++;
		w.LastBattleAt = now;
		worldEvent.Heard = WordReachesYou(worldEvent, player);
		Chronicle.Events.Add(worldEvent);
		try
		{
			NoteClashForWars(worldEvent);
		}
		catch (Exception ex)
		{
			GrudgeLog("RELATION clash note failed: " + ex.Message);
		}
		int num8 = worldEvent.People.Count((WorldPart p) => p.Outcome == "KILLED") + worldEvent.UnnamedDead.Values.Sum();
		GrudgeLog($"WAR battle {d.Name}: {worldEvent.Title} - {num8} dead (heat {w.Heat}, {w.BattlesToday} today)");
		Feed("WAR " + worldEvent.Title + ((num8 > 0) ? $" ({num8} dead)" : ""), KindColor("CLASH"));
		SaveWorld();
		return worldEvent;
		float Malus(Gang g)
		{
			return 1.5f * (float)Math.Min(4, (g.Id == w.A) ? w.OfficersA : w.OfficersB);
		}
		NemesisRecord Named(Gang g)
		{
			if (_rng.Next(100) < 35)
			{
				return (from n in Roster
					where n.Gang == g.Id && OffStage(n) && !n.Paralyzed && n.IsAlive && !n.PresumedDead
					orderby ((n.Rank >= 4) ? 200 : 0) + _rng.Next(100)
					select n).FirstOrDefault();
			}
			return null;
		}
	}

	internal bool SkirmishForWar(Vector3 at, Gang winner, Gang loser, int loserDead, int winnerDead, List<string> lines)
	{
		WarRecord warRecord = WarOf(winner, at);
		if (warRecord == null || (warRecord.A != loser.Id && warRecord.B != loser.Id))
		{
			return false;
		}
		District district = DistrictByKey(warRecord.District);
		if (district == null)
		{
			return false;
		}
		int num = Math.Max(2, Math.Min(6, loserDead));
		ShiftControl(district, winner, num, $"won a fight with the {loser.Name} ({loserDead} of theirs down)");
		ShiftControl(district, loser, -LossDown(district, loser, num), "lost a fight to the " + winner.Name);
		try
		{
			PurseForClash(winner, loser);
		}
		catch
		{
		}
		AddHeat(warRecord, Math.Min(6, loserDead + winnerDead), $"a fight you heard - {loserDead + winnerDead} men down");
		lines?.Add($"Part of {WarName(warRecord)}: the {winner.Name} took ground in {district.Name} (+{num}).");
		GrudgeLog($"WAR {WarName(warRecord)}: a live fight - the {winner.Name} +{num} ground, {loserDead} of the {loser.Name} and {winnerDead} of theirs down");
		SaveWorld();
		return true;
	}

	private void UpdateRoadBodies(Ped player)
	{
		if (Game.GameTime < _nextRoadBodies)
		{
			return;
		}
		_nextRoadBodies = Game.GameTime + 90000;
		if (Game.GameTime < _roadBodiesAllowed || !PED.IS_PED_ON_MOUNT(player.Handle) || _skirmish.Count > 0 || _warKind != null || FightHoldsWar() || WorldBusy(player) || MISC.GET_MISSION_FLAG() || InsideAnyTown(player.Position))
		{
			return;
		}
		int num = PED.GET_MOUNT(player.Handle);
		if (num == 0 || ENTITY.GET_ENTITY_SPEED(num) < 4f)
		{
			return;
		}
		District d = CurrentDistrict;
		WarRecord warRecord = ((d == null) ? null : Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key));
		if (warRecord != null && !TruceBetween(warRecord.A, warRecord.B))
		{
			InitHeat(warRecord);
			if (!(_rng.NextDouble() * 100.0 >= (double)warRecord.Heat / 4.0 * (WarNight ? 1.5 : 1.0)))
			{
				RoadBodiesNow(player, warRecord, d);
			}
		}
	}

	private bool RoadBodiesNow(Ped player, WarRecord w, District d)
	{
		Gang gang = Gangs.ById(w.A);
		Gang gang2 = Gangs.ById(w.B);
		if (gang == null || gang2 == null || !FactionModels.ContainsKey(gang.Id) || !FactionModels.ContainsKey(gang2.Id))
		{
			return false;
		}
		Vector3 fwd = Forward(player);
		Vector3 vector = GoodSpot(player, delegate
		{
			double num2 = Math.Atan2(fwd.Y, fwd.X) + (_rng.NextDouble() * 2.0 - 1.0) * 0.6;
			float num3 = 150f + (float)_rng.NextDouble() * 40f;
			return Ground(player.Position + new Vector3((float)Math.Cos(num2) * num3, (float)Math.Sin(num2) * num3, 0f));
		}, 14);
		if (vector == Vector3.Zero || InsideAnyTown(vector))
		{
			return false;
		}
		long num = GameMinutesNow();
		_historyAt = num;
		WorldEvent worldEvent;
		try
		{
			worldEvent = SimWarBattle(w, d, gang, gang2, num, player, vector);
		}
		finally
		{
			_historyAt = null;
		}
		if (worldEvent == null)
		{
			return false;
		}
		if (worldEvent.People.Count((WorldPart p) => p.Outcome == "KILLED") + worldEvent.UnnamedDead.Values.Sum() < 2)
		{
			string key = ((worldEvent.Gangs.Count > 1) ? worldEvent.Gangs[_rng.Next(2)] : w.B);
			worldEvent.UnnamedDead[key] = (worldEvent.UnnamedDead.TryGetValue(key, out var value) ? value : 0) + 2;
		}
		worldEvent.Heard = true;
		worldEvent.ForceSurvivors = true;
		if (worldEvent.AftermathUntil <= num)
		{
			worldEvent.AftermathUntil = num + (long)Config.WorldAftermathHours * 60L;
		}
		worldEvent.Lines.Add("You rode up on it not long after.");
		_roadBodiesAllowed = Game.GameTime + 360000;
		_nextAftermathCheck = 0;
		GrudgeLog($"WAR road bodies: {worldEvent.Title} - {vector.DistanceTo(player.Position):0} m ahead (heat {w.Heat})");
		SaveWorld();
		return true;
	}

	internal int WarsOf(Gang g)
	{
		if (g != null)
		{
			return Wars.Count((WarRecord w) => w.Result == null && (w.A == g.Id || w.B == g.Id) && !TruceBetween(w.A, w.B));
		}
		return 0;
	}

	internal bool StretchedThin(Gang g)
	{
		return WarsOf(g) >= 2;
	}

	internal string WarDefenders(WarRecord w)
	{
		Gang gang = Gangs.ById(w.A);
		Gang gang2 = Gangs.ById(w.B);
		District district = Districts.FirstOrDefault((District x) => x.Key == w.District);
		if (gang == null || gang2 == null)
		{
			return null;
		}
		int num = ((district != null) ? HoldOf(district, gang2.Id) : w.HoldB0);
		Personality? personality = LeaderTemper(gang2);
		Agenda agenda = AgendaOf(gang2);
		string text = ((!personality.HasValue || w.LeaderFellB) ? "Leaderless - holding on as best they can" : ((agenda?.Kind == "revenge" && agenda.Target == gang.Id) ? ("Out to break the " + gang.Name + " in turn") : ((num >= w.HoldB0 + 3) ? $"Holding firm - they've gained ground (+{num - w.HoldB0})" : ((num <= w.HoldB0 - 6) ? $"Giving ground - {w.HoldB0 - num} lost so far" : (personality.Value switch
		{
			Personality.Hothead => "Hitting back hard", 
			Personality.Proud => "Won't give an inch", 
			Personality.Cold => "Dug in and waiting", 
			Personality.Coward => "Looking for a way out", 
			Personality.Greedy => "Fighting for what pays", 
			Personality.Loyal => "Standing by their own", 
			_ => "Not much heart for it", 
		})))));
		if (StretchedThin(gang2))
		{
			text += " - stretched thin by another war";
		}
		if (HasPurse(gang2) && PurseOf(gang2) < 60)
		{
			text += " - short of money";
		}
		return text;
	}

	internal string WarOutlook(WarRecord w)
	{
		Gang gang = Gangs.ById(w.A);
		Gang gang2 = Gangs.ById(w.B);
		District district = Districts.FirstOrDefault((District x) => x.Key == w.District);
		if (gang == null || gang2 == null)
		{
			return null;
		}
		if (TruceBetween(w.A, w.B))
		{
			return "A truce holds - for now";
		}
		long num = WarDay(w);
		int num2 = Math.Max(30, 100 - 6 * w.OfficersA);
		int num3 = ((district != null) ? HoldOf(district, gang.Id) : w.HoldA0);
		int num4 = ((district != null) ? HoldOf(district, gang2.Id) : w.HoldB0);
		string text = ((!w.LeaderFellA && LeaderTemper(gang).HasValue) ? ((w.LeaderFellB || !LeaderTemper(gang2).HasValue) ? ("Leaderless - the " + gang2.Name + " may break") : ((num4 < 8 && num > 1) ? ("The " + gang2.Name + " are close to being driven out") : ((num3 < 8 && num > 1) ? ("The " + gang.Name + " are close to losing their foothold") : ((Strength(gang) < 30) ? ("The " + gang.Name + " are close to breaking - men and money running out") : ((Strength(gang2) < 30) ? ("The " + gang2.Name + " are close to breaking - men and money running out") : ((num >= num2 - 5) ? "Wearing thin - talk of peace" : ((HasPurse(gang) && PurseOf(gang) < 60) ? ("The " + gang.Name + " are running out of money") : ((HasPurse(gang2) && PurseOf(gang2) < 60) ? ("The " + gang2.Name + " are running out of money") : ((num <= 2) ? "Just begun" : ((w.Heat >= 75) ? "Raging - no end in sight" : ((w.Heat >= 35 || num < 4) ? "No end in sight yet" : "Cooling - both sides tired"))))))))))) : ("Leaderless - the " + gang.Name + " may break"));
		if (DebugOn)
		{
			text += $"  (Day {num}, heat {w.Heat}, strength {Strength(gang)} / {Strength(gang2)}, gives up day {num2})";
		}
		return text;
	}

	internal float Stretched(Gang g)
	{
		if (!StretchedThin(g))
		{
			return 1f;
		}
		return 0.85f;
	}

	private bool KeenForWar(Gang g, Gang on)
	{
		if (LeaderTemper(g) != Personality.Hothead)
		{
			if (AgendaOf(g)?.Kind == "revenge")
			{
				return AgendaOf(g)?.Target == on?.Id;
			}
			return false;
		}
		return true;
	}

	private bool NearWar(Ped player)
	{
		if (WarHere.HasValue)
		{
			return true;
		}
		Vector3 me = player.Position;
		return Wars.Any(delegate(WarRecord w)
		{
			if (w.Result == null && !TruceBetween(w.A, w.B))
			{
				District district = DistrictByKey(w.District);
				if (district != null)
				{
					return new Vector3(district.X - me.X, district.Y - me.Y, 0f).Length() < 1800f;
				}
			}
			return false;
		});
	}

	private void WatchCamped(Ped player)
	{
		if (!PED.IS_PED_USING_ANY_SCENARIO(player.Handle) || PED.IS_PED_ON_MOUNT(player.Handle) || PED.IS_PED_IN_ANY_VEHICLE(player.Handle, atGetIn: false))
		{
			_restSince = 0;
			if (_upSince == 0)
			{
				_upSince = Game.GameTime;
			}
			if (Game.GameTime - _upSince > 30000)
			{
				_restRolled = false;
			}
			return;
		}
		_upSince = 0;
		if (_restSince == 0)
		{
			_restSince = Game.GameTime;
		}
		if (!_restRolled && Game.GameTime - _restSince >= 8000 && WarNight && _sleepBattleAt == 0 && !FightHoldsWar() && Config.DistantBattles && _skirmish.Count <= 0 && _warKind == null && !WorldBusy(player) && !MISC.GET_MISSION_FLAG() && !LawAfterYou() && !InsideAnyTown(player.Position) && !AtGangCamp(player) && NearWar(player))
		{
			_restRolled = true;
			int num = _rng.Next(100);
			bool flag = num < 75;
			GrudgeLog(string.Format("WAR camped at night near a war: {0}% - rolled {1}: {2}", 75, num, flag ? "gunfire soon" : "a quiet night"));
			if (flag)
			{
				_sleepBattleAt = Game.GameTime + 15000 + _rng.Next(25000);
			}
		}
	}

	internal bool InAFight()
	{
		if (!_combatants.Values.Any((int t) => Game.GameTime - t < 10000) && (_enc == null || !_enc.Engaged))
		{
			return _liveCamps.Values.Any((LiveCamp c) => c.Engaged && !c.Cleared);
		}
		return true;
	}

	internal bool PatrolInTheFight(Ped player)
	{
		return _raids.Any((Raid r) => r.Men.Any((Ped m) => m != null && m.Exists() && !m.IsDead && m.Position.DistanceTo(player.Position) < 400f));
	}

	internal bool FightHoldsWar()
	{
		return InAFight();
	}
}
