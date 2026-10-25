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
	private WarRecord WarOf(Gang g, Vector3 at)
	{
		District d = DistrictAt(at);
		if (g == null || d == null)
		{
			return null;
		}
		return Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key && (x.A == g.Id || x.B == g.Id));
	}

	private void DailyWarTurn(WarRecord w, District d, WorldEvent ev, long days)
	{
		InitHeat(w);
		Gang gang = Gangs.ById(w.A);
		Gang gang2 = Gangs.ById(w.B);
		AddHeat(w, -((days >= 10) ? 8 : ((days >= 6) ? 6 : 4)), "a day of war wears them down");
		(Gang, bool)[] array = new(Gang, bool)[2]
		{
			(gang, true),
			(gang2, false)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(Gang, bool) tuple = array[i];
			Gang item = tuple.Item1;
			bool item2 = tuple.Item2;
			LeaderSeat leaderSeat = SeatOf(item);
			if (leaderSeat != null && leaderSeat.Fallen != null && leaderSeat.At >= w.StartMin && !(item2 ? w.LeaderFellA : w.LeaderFellB))
			{
				if (item2)
				{
					w.LeaderFellA = true;
				}
				else
				{
					w.LeaderFellB = true;
				}
				Personality? personality = LeaderTemper(item);
				if (!personality.HasValue)
				{
					AddHeat(w, -15, "the " + item.Name + " have nobody in the leader's seat");
					ev.Lines.Add("The " + item.Name + " have lost their leader and nobody leads them now - the fight is going out of them.");
				}
				else if (personality == Personality.Hothead || personality == Personality.Proud)
				{
					AddHeat(w, 15, "the " + OutfitPoss(item) + " new leader wants blood");
					ev.Lines.Add("The " + OutfitPoss(item) + " new leader, " + item.LeaderName + ", swears to avenge the old one.");
				}
				else
				{
					AddHeat(w, -6, "the " + OutfitPoss(item) + " new leader is cautious");
				}
				if (!item2 && w.Goal != "drive")
				{
					SetWarAim(w, "drive", null, "they mean to drive the leaderless " + gang2.Name + " out of " + d.Name, ev, "the " + gang2.Name + " lost their leader");
				}
			}
		}
		ShiftWarGoal(w, d, ev);
		if (w.YouKilledA + w.YouKilledB > 0)
		{
			string item3 = $"Your part: you've killed {w.YouKilledA} of the {gang.Name} and {w.YouKilledB} of the {gang2.Name}" + ((w.OfficersA + w.OfficersB > 0) ? $", {w.OfficersA + w.OfficersB} of them senior officers" : "") + ".";
			ev.Lines.Add(item3);
		}
		ev.Lines.Add($"The war is {HeatWord(w.Heat)} ({w.Heat}).");
	}

	private void SetWarAim(WarRecord w, string goal, string site, string text, WorldEvent ev, string why)
	{
		if (!(w.Goal == goal) || !(w.GoalSite == site))
		{
			w.Goal = goal;
			w.GoalSite = site;
			w.GoalText = text;
			if (w.GoalLog == null)
			{
				w.GoalLog = new List<string>();
			}
			w.GoalLog.Add($"{GameDate.FromMinutes(GameMinutesNow()):MMM d}: {text} ({why})");
			while (w.GoalLog.Count > 8)
			{
				w.GoalLog.RemoveAt(0);
			}
			Gang gang = Gangs.ById(w.A);
			ev?.Lines.Add("The " + gang?.Name + " change their aim: " + text + " (" + why + ").");
			GrudgeLog("WAR aim " + w.District + ": " + text + " (" + why + ")");
			SaveWorld();
		}
	}

	private bool RetargetWar(WarRecord w, District d, string why)
	{
		Gang gang = Gangs.ById(w.B);
		if (gang == null || d == null)
		{
			return false;
		}
		CampSite campSite = (from _ in SitesHeldBy(gang.Id)
			where !TakeoverPending(_) && _.Id != w.GoalSite && DistrictAt(new Vector3(_.X, _.Y, _.Z)) == d
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (campSite != null)
		{
			SetWarAim(w, "camp", campSite.Id, "they want the camp at " + campSite.Label, null, why);
		}
		else
		{
			SetWarAim(w, "ground", null, "they want more of " + d.Name, null, why);
		}
		return true;
	}

	private void ShiftWarGoal(WarRecord w, District d, WorldEvent ev)
	{
		Gang gang = Gangs.ById(w.B);
		int num = HoldOf(d, w.A);
		int num2 = HoldOf(d, w.B);
		if (w.Goal == "camp")
		{
			CampSite campSite = ((w.GoalSite != null) ? Camps.ById(w.GoalSite) : null);
			if (campSite == null || SiteOwner(campSite) != w.B)
			{
				RetargetWar(w, d, "the camp they came for is gone");
				return;
			}
		}
		if (w.Goal != "hold" && num <= w.HoldA0 - 5)
		{
			SetWarAim(w, "hold", null, "they're holding on to what's left of " + d.Name, ev, "the war is going against them");
		}
		else if (w.Goal == "hold" && num >= w.HoldA0)
		{
			SetWarAim(w, "ground", null, "they want more of " + d.Name, ev, "they've won their ground back");
		}
		else if (w.Goal == "ground" && num >= w.HoldA0 + 6 && num2 <= w.HoldB0 - 6)
		{
			SetWarAim(w, "drive", null, "they mean to drive the " + gang?.Name + " out of " + d.Name, ev, "they smell victory");
		}
	}

	private void NoteWarKill(Gang g, int rank, Vector3 at)
	{
		WarRecord w = WarOf(g, at);
		if (w == null)
		{
			return;
		}
		InitHeat(w);
		bool flag = w.A == g.Id;
		bool flag2 = rank >= 4;
		if (flag)
		{
			w.YouKilledA++;
			if (flag2)
			{
				w.OfficersA++;
			}
		}
		else
		{
			w.YouKilledB++;
			if (flag2)
			{
				w.OfficersB++;
			}
		}
		Gang gang = Gangs.ById(flag ? w.B : w.A);
		if (flag2)
		{
			AddHeat(w, -6, "you killed a " + g.Name + " senior officer");
			Feed("WAR you killed one of the " + OutfitPoss(g) + " senior officers - their war in " + Districts.FirstOrDefault((District x) => x.Key == w.District)?.Name + " falters", Theme.Warn);
		}
		int num = (flag ? w.YouKilledA : w.YouKilledB);
		int num2 = (flag ? w.YouKilledB : w.YouKilledA);
		if (!w.FavourSaid && num >= 5 && num2 <= 1 && gang != null)
		{
			w.FavourSaid = true;
			if (Config.Notoriety.TryGetValue(gang.Id, out var value) && value > 0)
			{
				Config.Notoriety[gang.Id] = Math.Max(0, value - 3);
				Config.Save();
			}
			Feed("WAR the " + gang.Name + " hear you've been killing the " + OutfitPoss(g) + " men - they're glad of it (notoriety with them -3)", Theme.Good);
			GrudgeLog($"WAR {w.District}: you've picked a side - the {gang.Name} ({num} of the {g.Name} killed, {num2} of theirs)");
		}
		SaveWorld();
	}

	private void WarBattles(Ped player, long now)
	{
		int num = GameDate.DayKey(FromGameMinutes(now));
		foreach (WarRecord w in Wars.Where((WarRecord x) => x.Result == null).ToList())
		{
			District district = Districts.FirstOrDefault((District x) => x.Key == w.District);
			Gang gang = Gangs.ById(w.A);
			Gang gang2 = Gangs.ById(w.B);
			if (district == null || gang == null || gang2 == null || IsDestroyed(gang) || IsDestroyed(gang2) || TruceBetween(gang.Id, gang2.Id))
			{
				continue;
			}
			InitHeat(w);
			if (w.BattleDay != num)
			{
				w.BattleDay = num;
				w.BattlesToday = 0;
			}
			if (w.BattlesToday < Math.Min(4, 1 + w.Heat / 30) && (w.LastBattleAt <= 0 || now - w.LastBattleAt < 0 || now - w.LastBattleAt >= 180) && !(_rng.NextDouble() * 100.0 >= (double)w.Heat * 0.25))
			{
				_historyAt = now;
				try
				{
					SimWarBattle(w, district, gang, gang2, now, player);
				}
				finally
				{
					_historyAt = null;
				}
			}
		}
	}
}
