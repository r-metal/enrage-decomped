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
	private const int CommonEnemyCap = 15;

	private readonly Dictionary<string, int> _warMarkers = new Dictionary<string, int>();

	private string _warNoticeDistrict;

	private const ulong MapEnableRegionBlip = 6214909650283936023uL;

	private const ulong MapDisableRegionBlip = 7459886982293971635uL;

	private readonly Dictionary<string, (int blip, string name)> _warDistrictBlips = new Dictionary<string, (int, string)>();

	private static string PairKey(string a, string b)
	{
		if (string.CompareOrdinal(a, b) >= 0)
		{
			return b + "|" + a;
		}
		return a + "|" + b;
	}

	private Dictionary<string, int> RelMap => Chronicle.Relations ?? (Chronicle.Relations = new Dictionary<string, int>());

	private Dictionary<string, string> RelWhy => Chronicle.RelationWhy ?? (Chronicle.RelationWhy = new Dictionary<string, string>());

	private List<WarRecord> Wars => Chronicle.Wars ?? (Chronicle.Wars = new List<WarRecord>());

	private WarRecord LiveWar(string a, string b)
	{
		return Wars.FirstOrDefault((WarRecord w) => (w.A == a && w.B == b) || (w.A == b && w.B == a));
	}

	internal static int LoreRelation(string a, string b)
	{
		if (a == null || b == null || a == b)
		{
			return 100;
		}
		Gang gang = Gangs.ById(a);
		Gang gang2 = Gangs.ById(b);
		if (gang?.Relations != null && gang.Relations.TryGetValue(b, out var value))
		{
			return value;
		}
		if (gang2?.Relations != null && gang2.Relations.TryGetValue(a, out var value2))
		{
			return value2;
		}
		return 0;
	}

	internal int Relation(string a, string b)
	{
		if (a == null || b == null || a == b)
		{
			return 100;
		}
		if (!RelMap.TryGetValue(PairKey(a, b), out var value))
		{
			return LoreRelation(a, b);
		}
		return value;
	}

	internal string RelationCause(string a, string b)
	{
		if (!RelWhy.TryGetValue(PairKey(a, b), out var value))
		{
			return "old blood between them";
		}
		return value;
	}

	internal void ShiftRelation(string a, string b, int delta, string why, bool force = false)
	{
		if (a == null || b == null || a == b || delta == 0 || Gangs.ById(a) == null || Gangs.ById(b) == null)
		{
			return;
		}
		int num = Relation(a, b);
		if (force)
		{
			int num2 = Math.Max(-100, Math.Min(100, num + delta));
			RelMap[PairKey(a, b)] = num2;
			if (!string.IsNullOrEmpty(why))
			{
				RelWhy[PairKey(a, b)] = why;
			}
			SaveWorld();
			GrudgeLog($"RELATION {Gangs.ById(a).Name} / {Gangs.ById(b).Name}: {num} -> {num2} ({why}, forced)");
			return;
		}
		if (delta < 0 && num <= -80)
		{
			delta = Math.Min(-1, (int)Math.Round((double)delta / 3.0));
		}
		if (delta > 0 && num >= 80)
		{
			delta = Math.Max(1, (int)Math.Round((double)delta / 3.0));
		}
		string key = PairKey(a, b) + "|" + GameDayKey();
		_relSwing.TryGetValue(key, out var value);
		int num3 = Math.Max(-20, Math.Min(20, value + delta)) - value;
		if (num3 == 0)
		{
			GrudgeLog($"RELATION {Gangs.ById(a).Name} / {Gangs.ById(b).Name}: {why} - no more change today (moved {value:+0;-0} already)");
			return;
		}
		if (_relSwing.Count > 400)
		{
			_relSwing.Clear();
		}
		_relSwing[key] = value + num3;
		delta = num3;
		int num4 = Math.Max(-100, Math.Min(100, num + delta));
		if (num4 != num)
		{
			string key2 = PairKey(a, b);
			RelMap[key2] = num4;
			if (!string.IsNullOrEmpty(why))
			{
				RelWhy[key2] = why;
			}
			SaveWorld();
			GrudgeLog($"RELATION {Gangs.ById(a).Name} / {Gangs.ById(b).Name}: {num} -> {num4} ({why})");
			int warLine = Config.WarLine;
			if (num > warLine && num4 <= warLine)
			{
				Feed("BAD BLOOD the " + Gangs.ById(a).Name + " and the " + Gangs.ById(b).Name + " are ready to go to war (" + why + ")", Theme.Blood);
			}
			else if (num <= warLine && num4 > warLine)
			{
				Feed("COOLING the " + Gangs.ById(a).Name + " and the " + Gangs.ById(b).Name + " back off from war (" + why + ")", Theme.Dim);
			}
		}
	}

	internal static string RelationWord(int r)
	{
		if (r > -70)
		{
			if (r > -35)
			{
				if (r > -15)
				{
					if (r >= 15)
					{
						if (r >= 40)
						{
							return "friendly";
						}
						return "on terms";
					}
					return "wary";
				}
				return "hostile";
			}
			return "at war";
		}
		return "blood feud";
	}

	internal void DriftRelations(int days)
	{
		List<string> list = (from g in Gangs.All
			where !g.StoryOnly
			select g.Id).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			for (int num2 = num + 1; num2 < list.Count; num2++)
			{
				string text = list[num];
				string text2 = list[num2];
				string key = PairKey(text, text2);
				if (!RelMap.TryGetValue(key, out var value))
				{
					continue;
				}
				int num3 = Math.Max(-100, LoreRelation(text, text2) - GrievanceOf(text, text2));
				int num4 = value;
				if (num4 <= -80)
				{
					num3 = Math.Max(num3, -70);
				}
				for (int num5 = 0; num5 < Math.Min(days, 30); num5++)
				{
					int num6 = (int)Math.Round((double)(num3 - num4) * 0.03);
					if (num6 == 0 && num4 != num3)
					{
						num6 = Math.Sign(num3 - num4);
					}
					if (Math.Abs(num3 - num4) >= 15 && Math.Abs(num6) < 2)
					{
						num6 = 2 * Math.Sign(num3 - num4);
					}
					num4 += num6;
				}
				if (num4 != value)
				{
					RelMap[key] = num4;
					GrudgeLog($"RELATION {text} / {text2}: {value} -> {num4} (time - toward the old {num3})");
				}
				if (num4 == LoreRelation(text, text2))
				{
					RelMap.Remove(key);
					RelWhy.Remove(key);
				}
			}
		}
		try
		{
			FadeGrievances(days);
			RollFlashpoint(days);
		}
		catch (Exception ex)
		{
			GrudgeLog("SPARK failed: " + ex.Message);
		}
		for (int num7 = 0; num7 < list.Count; num7++)
		{
			for (int num8 = num7 + 1; num8 < list.Count; num8++)
			{
				string a = list[num7];
				string b = list[num8];
				if (Relation(a, b) <= Config.WarLine)
				{
					continue;
				}
				Gang gang = Gangs.ById(a);
				if (gang != null && gang.OrdersNotFeelings)
				{
					continue;
				}
				Gang gang2 = Gangs.ById(b);
				if (gang2 == null || !gang2.OrdersNotFeelings)
				{
					int num9 = Math.Min(100, LoreRelation(a, b) + 15) - Relation(a, b);
					if (num9 > 0 && list.Any(delegate(string c)
					{
						if (c != a && c != b)
						{
							Gang gang3 = Gangs.ById(c);
							if ((gang3 == null || !gang3.OrdersNotFeelings) && AtWarNow(a, c))
							{
								return AtWarNow(b, c);
							}
						}
						return false;
					}))
					{
						ShiftRelation(a, b, Math.Min(num9, 2 * Math.Min(days, 5)), "a common enemy");
					}
				}
			}
		}
		SaveWorld();
	}

	internal string RelationsLine()
	{
		List<string> list = (from g in Gangs.All
			where !g.StoryOnly && !IsDestroyed(g)
			select g.Id).ToList();
		List<(string t, int r)> list2 = new List<(string t, int r)>();
		for (int num = 0; num < list.Count; num++)
		{
			for (int num2 = num + 1; num2 < list.Count; num2++)
			{
				int num3 = Relation(list[num], list[num2]);
				if (num3 <= -15 || RelMap.ContainsKey(PairKey(list[num], list[num2])))
				{
					list2.Add((string.Format("{0}/{1} {2}{3}", list[num], list[num2], num3, (num3 != LoreRelation(list[num], list[num2])) ? $" (lore {LoreRelation(list[num], list[num2])})" : ""), num3));
				}
			}
		}
		return "relations: " + ((list2.Count == 0) ? "all at peace" : string.Join(", ", from x in list2.OrderBy(((string t, int r) x) => x.r).Take(10)
			select x.t));
	}

	private int RecentClashes(string a, string b)
	{
		long num = GameMinutesNow() - 4320;
		int num2 = 0;
		int num3 = Chronicle.Events.Count - 1;
		while (num3 >= 0 && num2 < 10)
		{
			WorldEvent worldEvent = Chronicle.Events[num3];
			if (!worldEvent.IsPast && worldEvent.GameMin >= num && (worldEvent.Kind == "CLASH" || worldEvent.Kind == "CAMP OVERRUN" || worldEvent.Kind == "RAID") && worldEvent.Gangs.Contains(a) && worldEvent.Gangs.Contains(b))
			{
				num2++;
			}
			num3--;
		}
		return num2;
	}

	private (Gang a, Gang b, District d)? PickClashPair()
	{
		List<Gang> list = Gangs.All.Where((Gang g) => !g.StoryOnly && !IsDestroyed(g) && Roster.Any((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead)).ToList();
		List<(Gang, Gang, District, double)> list2 = new List<(Gang, Gang, District, double)>();
		foreach (Gang item in list)
		{
			foreach (Gang item2 in list)
			{
				if (string.CompareOrdinal(item.Id, item2.Id) >= 0)
				{
					continue;
				}
				int num = Relation(item.Id, item2.Id);
				if (num > Config.ClashLine || TruceBetween(item.Id, item2.Id))
				{
					continue;
				}
				District[] districts = Districts;
				foreach (District district in districts)
				{
					int num3 = HoldOf(district, item.Id);
					int num4 = HoldOf(district, item2.Id);
					if ((num3 >= 3 && num4 >= 3) || (item.OrdersNotFeelings && num4 >= 8) || (item2.OrdersNotFeelings && num3 >= 8))
					{
						double num5 = (((item.OrdersNotFeelings || item2.OrdersNotFeelings) && LiveWar(item.Id, item2.Id) == null) ? 0.4 : 1.0);
						list2.Add((item, item2, district, num5 * (double)(-num) * (double)Math.Max(3, Math.Min(Math.Max(num3, 3), Math.Max(num4, 3))) / (1.0 + 0.6 * (double)RecentClashes(item.Id, item2.Id)) * AgendaPairMult(item, item2, district) * Math.Min(TargetWeight(item), TargetWeight(item2))));
					}
				}
			}
		}
		if (list2.Count == 0)
		{
			return null;
		}
		double num6 = _rng.NextDouble() * list2.Sum<(Gang, Gang, District, double)>(((Gang a, Gang b, District d, double w) c) => c.w);
		foreach (var item3 in list2)
		{
			if (!((num6 -= item3.Item4) > 0.0))
			{
				return (item3.Item1.OrdersNotFeelings || (!item3.Item2.OrdersNotFeelings && _rng.Next(100) < ((HoldOf(item3.Item3, item3.Item1.Id) >= HoldOf(item3.Item3, item3.Item2.Id)) ? 65 : 35))) ? (item3.Item1, item3.Item2, item3.Item3) : (item3.Item2, item3.Item1, item3.Item3);
			}
		}
		(Gang, Gang, District, double) tuple = list2.Last();
		return (tuple.Item1, tuple.Item2, tuple.Item3);
	}

	private (string place, Vector3 pos, CampSite site)? ClashPlace(Gang attacker, Gang defender, District d, Ped player)
	{
		CampSite campSite = WarGoalCamp(attacker, defender, d);
		if (campSite != null && _rng.Next(100) < 70 && (player == null || new Vector3(campSite.X, campSite.Y, campSite.Z).DistanceTo(player.Position) >= Config.WorldMinDistance))
		{
			return ("at " + campSite.Label, new Vector3(campSite.X, campSite.Y, campSite.Z), campSite);
		}
		if (defender.HasCamps && _rng.Next(100) < (attacker.OrdersNotFeelings ? 60 : 40))
		{
			HashSet<string> manned = new HashSet<string>(from n in Roster
				where n.Gang == defender.Id && n.CampId != null && OffStage(n)
				select n.CampId);
			CampSite campSite2 = (from s in SitesHeldBy(defender.Id)
				where !IsCleared(s) && !_liveCamps.ContainsKey(s.Id) && DistrictAt(new Vector3(s.X, s.Y, s.Z)) == d && (player == null || new Vector3(s.X, s.Y, s.Z).DistanceTo(player.Position) >= Config.WorldMinDistance)
				orderby manned.Contains(s.Id) descending
				select s).ThenBy((CampSite _) => _rng.Next()).FirstOrDefault();
			if (campSite2 != null)
			{
				return ("at " + campSite2.Label, new Vector3(campSite2.X, campSite2.Y, campSite2.Z), campSite2);
			}
		}
		if (_rng.Next(100) < 35)
		{
			foreach (string item in TownNames.OrderBy((string _) => _rng.Next()))
			{
				if (TownCore(item, out var centre, out var radius) && DistrictAt(centre) == d)
				{
					double num = _rng.NextDouble() * Math.PI * 2.0;
					float num2 = radius + 180f + (float)_rng.NextDouble() * 200f;
					Vector3 vector = new Vector3(centre.X + (float)Math.Cos(num) * num2, centre.Y + (float)Math.Sin(num) * num2, centre.Z);
					if ((!(player != null) || !(new Vector3(vector.X - player.Position.X, vector.Y - player.Position.Y, 0f).Length() < Config.WorldMinDistance)) && !InsideAnyTown(vector) && OnLand(vector))
					{
						return ("outside " + item, vector, null);
					}
				}
			}
		}
		for (int num3 = 0; num3 < 10; num3++)
		{
			double num4 = _rng.NextDouble() * Math.PI * 2.0;
			float num5 = 150f + (float)_rng.NextDouble() * 450f;
			Vector3 vector2 = new Vector3(d.X + (float)Math.Cos(num4) * num5, d.Y + (float)Math.Sin(num4) * num5, 100f);
			if ((!(player != null) || !(new Vector3(vector2.X - player.Position.X, vector2.Y - player.Position.Y, 0f).Length() < Config.WorldMinDistance)) && !InsideAnyTown(vector2) && OnLand(vector2))
			{
				return ("in " + d.Name, vector2, null);
			}
		}
		return null;
	}

	private void WarStarted(District d, Gang a, Gang b)
	{
		WarRecord warRecord = new WarRecord
		{
			District = d.Key,
			A = a.Id,
			B = b.Id,
			StartMin = GameMinutesNow(),
			HoldA0 = HoldOf(d, a.Id),
			HoldB0 = HoldOf(d, b.Id),
			MenA0 = Math.Max(1, MenOf(a)),
			MenB0 = Math.Max(1, MenOf(b)),
			LastReportDay = GameDayKey(),
			Cause = RelationCause(a.Id, b.Id)
		};
		Wars.RemoveAll((WarRecord x) => x.District == d.Key);
		try
		{
			PickWarGoal(warRecord, d, a, b);
		}
		catch (Exception ex)
		{
			GrudgeLog("WAR goal failed: " + ex.Message);
		}
		InitHeat(warRecord);
		if (warRecord.GoalText != null && warRecord.GoalLog.Count == 0)
		{
			warRecord.GoalLog.Add($"{GameDate.FromMinutes(GameMinutesNow()):MMM d}: {warRecord.GoalText}");
		}
		warRecord.Name = NameWar(warRecord, d, a, b, warRecord.Cause);
		Wars.Add(warRecord);
		WorldEvent worldEvent = NewEvent("WAR", GameMinutesNow());
		worldEvent.Gangs.Add(a.Id);
		worldEvent.Gangs.Add(b.Id);
		worldEvent.Title = "War breaks out in " + d.Name + ": the " + a.Name + " and the " + b.Name;
		worldEvent.Place = "in " + d.Name;
		worldEvent.HasPos = true;
		worldEvent.X = d.X;
		worldEvent.Y = d.Y;
		worldEvent.Z = 100f;
		worldEvent.Lines.Add("They're already calling it " + warRecord.Name + ".");
		worldEvent.Lines.Add("What started it: " + warRecord.Cause + ".");
		if (warRecord.GoalText != null)
		{
			worldEvent.Lines.Add("What the " + a.Name + " want: " + warRecord.GoalText + ".");
		}
		worldEvent.Lines.Add($"The {a.Name} hold {warRecord.HoldA0} of the district, the {b.Name} {warRecord.HoldB0}. They're {RelationWord(Relation(a.Id, b.Id))} ({Relation(a.Id, b.Id)}).");
		worldEvent.Lines.Add("Expect skirmishes on the roads there, raids on camps, and bodies by morning.");
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		SaveWorld();
	}

	private void WarEnded(District d, string aId, string bId)
	{
		WarRecord warRecord = Wars.FirstOrDefault((WarRecord x) => x.District == d.Key);
		Gang gang = Gangs.ById(aId);
		Gang gang2 = Gangs.ById(bId);
		if (gang == null || gang2 == null)
		{
			Wars.Remove(warRecord);
			return;
		}
		int num = HoldOf(d, aId);
		int num2 = HoldOf(d, bId);
		int num3 = Relation(aId, bId);
		string text = WarResultText(warRecord, gang, gang2) ?? (TruceBetween(aId, bId) ? "they called a truce" : ((IsDestroyed(gang) || IsDestroyed(gang2)) ? "one side is finished" : ((num3 > Config.WarLine) ? "the bad blood cooled" : ((Math.Min(num, num2) < 8) ? ("the " + ((num < num2) ? gang.Name : gang2.Name) + " were driven out") : "one side won the ground"))));
		Gang gang3 = ((num >= num2) ? gang : gang2);
		WorldEvent worldEvent = NewEvent("WAR", GameMinutesNow());
		worldEvent.Gangs.Add(aId);
		worldEvent.Gangs.Add(bId);
		worldEvent.Title = ((warRecord != null) ? (WarName(warRecord) + " is over - the " + gang3.Name + " hold " + d.Name) : ("The war in " + d.Name + " is over - the " + gang3.Name + " hold it"));
		Gang gang4 = (((warRecord?.Goal == "drive" && warRecord.Result == "met") || warRecord?.Result == "broken") ? gang2 : ((warRecord?.Result == "defended" || warRecord?.Result == "spent") ? gang : null));
		if (warRecord?.Result == "broken" || warRecord?.Result == "spent")
		{
			Gang g = ((warRecord.Result == "broken") ? gang : gang2);
			Gang gang5 = ((warRecord.Result == "broken") ? gang2 : gang);
			int num4 = Math.Min(10, HoldOf(d, gang5.Id));
			if (num4 > 0)
			{
				ShiftControl(d, gang5, -num4, "ran out of men");
				ShiftControl(d, g, num4, "the " + gang5.Name + " ran out of men");
			}
		}
		if (gang4 != null)
		{
			(Chronicle.Beaten ?? (Chronicle.Beaten = new Dictionary<string, long>()))[d.Key + "|" + gang4.Id] = GameMinutesNow() + 10080;
			worldEvent.Title = ((gang4 == gang2) ? ("The " + gang.Name + " drove the " + gang2.Name + " out of " + d.Name) : ("The " + gang2.Name + " drove the " + gang.Name + " off in " + d.Name));
			GrudgeLog("WAR " + d.Name + ": the " + gang4.Name + " were beaten - their ground there stays down for 7 days");
		}
		worldEvent.Place = "in " + d.Name;
		worldEvent.HasPos = true;
		worldEvent.X = d.X;
		worldEvent.Y = d.Y;
		worldEvent.Z = 100f;
		worldEvent.Lines.Add("How it ended: " + text + ".");
		if (warRecord != null)
		{
			long num5 = Math.Max(0L, (GameMinutesNow() - warRecord.StartMin) / 1440);
			if (num5 > 120)
			{
				num5 = 0L;
			}
			worldEvent.Lines.Add(string.Format("It lasted {0} day{1}: {2} fights that word got out about, {3} named men dead.", num5, (num5 == 1) ? "" : "s", warRecord.Clashes, warRecord.Dead));
			worldEvent.Lines.Add($"Ground: the {gang.Name} {warRecord.HoldA0} -> {num}, the {gang2.Name} {warRecord.HoldB0} -> {num2}.");
		}
		if (gang4 != null)
		{
			worldEvent.Lines.Add("The " + gang4.Name + " will be a while getting their ground back in " + d.Name + ".");
		}
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		if (warRecord != null)
		{
			warRecord.EndMin = GameMinutesNow();
			warRecord.EndTitle = worldEvent.Title;
			warRecord.EndHow = text;
			try
			{
				Gang gang6 = Gangs.ById(warRecord.A) ?? gang;
				Gang gang7 = Gangs.ById(warRecord.B) ?? gang2;
				warRecord.EndPurseA = WarPurse(gang6);
				warRecord.EndPurseB = WarPurse(gang7);
				warRecord.EndMenA = $"{MenOf(gang6)} of {MenStart(gang6)}  -  {StrengthWord(Strength(gang6))}";
				warRecord.EndMenB = $"{MenOf(gang7)} of {MenStart(gang7)}  -  {StrengthWord(Strength(gang7))}";
				warRecord.EndLeaderA = WarLeader(gang6);
				warRecord.EndLeaderB = WarLeader(gang7);
				warRecord.EndAimA = WarAim(gang6);
				warRecord.EndAimB = WarAim(gang7);
				warRecord.EndHoldA = HoldOf(d, gang6.Id);
				warRecord.EndHoldB = HoldOf(d, gang7.Id);
			}
			catch (Exception ex)
			{
				GrudgeLog("WAR end snapshot failed: " + ex.Message);
			}
			List<WarRecord> list = Chronicle.PastWars ?? (Chronicle.PastWars = new List<WarRecord>());
			list.Add(warRecord);
			while (list.Count > 12)
			{
				list.RemoveAt(0);
			}
		}
		Wars.Remove(warRecord);
		SaveWorld();
	}

	internal long WarDay(WarRecord w)
	{
		if (w != null)
		{
			return Math.Max(0L, GameMinutesNow() - w.StartMin) / 1440 + 1;
		}
		return 0L;
	}

	internal static string WarSince(WarRecord w)
	{
		return GameDate.FromMinutes(w.StartMin).ToString("MMM d");
	}

	private void WarReport(District d, WarRecord w)
	{
		Gang gang = Gangs.ById(w.A);
		Gang gang2 = Gangs.ById(w.B);
		if (gang == null || gang2 == null)
		{
			return;
		}
		if (w.StartMin > GameMinutesNow() || GameMinutesNow() - w.StartMin > 172800)
		{
			GrudgeLog($"WAR {d.Name}: start time {w.StartMin} doesn't fit this save - counted from today");
			w.StartMin = GameMinutesNow();
		}
		int num = HoldOf(d, w.A);
		int num2 = HoldOf(d, w.B);
		long num3 = Math.Max(1L, (GameMinutesNow() - w.StartMin) / 1440);
		WorldEvent worldEvent = NewEvent("WAR", GameMinutesNow());
		worldEvent.Gangs.Add(w.A);
		worldEvent.Gangs.Add(w.B);
		Gang gang3 = ((num == num2) ? null : ((num > num2) ? gang : gang2));
		long num4 = WarDay(w);
		GrudgeLog($"WAR {d.Name} day {num4}: ground {gang.Name} {num} (from {w.HoldA0}), {gang2.Name} {num2} (from {w.HoldB0}) - men {MenOf(gang)} of {w.MenA0}, {MenOf(gang2)} of {w.MenB0} - heat {w.Heat}");
		worldEvent.Title = ((gang3 == null) ? $"Day {num4} of {WarName(w)}: dead even" : $"Day {num4} of {WarName(w)}: the {gang3.Name} are winning");
		worldEvent.Place = "in " + d.Name;
		worldEvent.HasPos = true;
		worldEvent.X = d.X;
		worldEvent.Y = d.Y;
		worldEvent.Z = 100f;
		worldEvent.Lines.Add("At war since " + WarSince(w) + ".");
		worldEvent.Lines.Add($"Ground now: the {gang.Name} {num} ({num - w.HoldA0:+0;-0}), the {gang2.Name} {num2} ({num2 - w.HoldB0:+0;-0}).");
		worldEvent.Lines.Add($"{w.Clashes} fights so far, {w.Dead} named men dead. They're {RelationWord(Relation(w.A, w.B))} ({Relation(w.A, w.B)}).");
		try
		{
			DailyWarTurn(w, d, worldEvent, num3);
		}
		catch (Exception ex)
		{
			GrudgeLog("WAR daily turn failed: " + ex.Message);
		}
		int num5 = Math.Max(0, w.Dead - w.DeadAtReport);
		w.DeadAtReport = w.Dead;
		int num6 = ((num3 >= 5) ? 1 : 0) + num5;
		if (w.GoalText != null)
		{
			worldEvent.Lines.Add("The " + gang.Name + ": " + w.GoalText + ".");
		}
		worldEvent.Lines.Add($"Strength: the {gang.Name} {StrengthWord(Strength(gang)).ToLowerInvariant()} ({MenOf(gang)} men), the {gang2.Name} {StrengthWord(Strength(gang2)).ToLowerInvariant()} ({MenOf(gang2)} men).");
		if (TruceBetween(w.A, w.B))
		{
			w.Paused = true;
			worldEvent.Title = $"Day {num4} of {WarName(w)}: a truce holds";
			worldEvent.Lines.Add("Neither side moves while the truce holds. When it runs out they'll fight again - unless they've cooled.");
		}
		else
		{
			if (w.Paused)
			{
				w.Paused = false;
				worldEvent.Lines.Add("The truce is over. The fighting starts again.");
				GrudgeLog("WAR " + d.Name + ": the truce ran out - the war goes on");
			}
			bool flag = false;
			try
			{
				flag = LeadersSueForPeace(w, d, worldEvent, num3);
			}
			catch (Exception ex2)
			{
				GrudgeLog("WAR peace roll failed: " + ex2.Message);
			}
			if (!flag)
			{
				if (num6 > 0)
				{
					ShiftRelation(w.A, w.B, num6, string.Format("war-weary (day {0}{1})", num3, (num5 > 0) ? $", {num5} named dead" : ""));
				}
				if (num3 >= 10)
				{
					worldEvent.Lines.Add((num3 >= 25) ? "Both sides are bleeding men - there's talk of calling it off." : "The fighting is wearing on both sides.");
				}
			}
		}
		worldEvent.Heard = true;
		Chronicle.Events.Add(worldEvent);
		SaveWorld();
	}

	private void NoteClashForWars(WorldEvent ev)
	{
		try
		{
			LoseMenInEvent(ev);
		}
		catch (Exception ex)
		{
			GrudgeLog("MEN event note failed: " + ex.Message);
		}
		if (ev != null && ev.Gangs != null && ev.Gangs.Count >= 2 && (ev.Kind == "CLASH" || ev.Kind == "CAMP OVERRUN" || ev.Kind == "RAID"))
		{
			NoteClash(ev.Gangs[0], ev.Gangs[1], ev.People.Count((WorldPart p) => p.Outcome == "KILLED"), ev.Kind == "CAMP OVERRUN", ev.Place, ev.HasPos ? new Vector3?(new Vector3(ev.X, ev.Y, ev.Z)) : ((Vector3?)null));
		}
	}

	internal void NoteLiveClash(string a, string b, int namedKilled, string place, Vector3 pos)
	{
		if (a == null || b == null || a == b)
		{
			return;
		}
		try
		{
			NoteClash(a, b, namedKilled, overrun: false, place, pos);
		}
		catch (Exception ex)
		{
			GrudgeLog("RELATION live clash note failed: " + ex.Message);
		}
	}

	internal WarRecord WarOfFight(string a, string b, District d, long when, bool liveOnly)
	{
		List<WarRecord> source = (liveOnly ? Wars.Where(Pair).ToList() : (from x in Wars.Concat(Chronicle.PastWars ?? new List<WarRecord>())
			where Pair(x) && when >= x.StartMin - 5 && (x.EndMin <= 0 || when <= x.EndMin + 60)
			orderby x.StartMin
			select x).ToList());
		return ((d == null) ? null : source.FirstOrDefault((WarRecord x) => x.District == d.Key)) ?? source.FirstOrDefault((WarRecord x) => !liveOnly || x.Result == null);
		bool Pair(WarRecord x)
		{
			if (x != null)
			{
				if (!(x.A == a) || !(x.B == b))
				{
					if (x.A == b)
					{
						return x.B == a;
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	private void NoteClash(string a, string b, int killed, bool overrun, string place, Vector3? at)
	{
		if (killed > 0)
		{
			AddGrievance(a, b, 3 * killed, $"{killed} named men killed {place}");
		}
		if (overrun)
		{
			AddGrievance(a, b, 10, "overran a camp");
		}
		WarRecord warRecord = WarOfFight(a, b, at.HasValue ? DistrictAt(at.Value) : null, GameMinutesNow(), liveOnly: true);
		bool flag = warRecord != null && GameMinutesNow() - warRecord.StartMin > 4320;
		ShiftRelation(a, b, flag ? (-2) : (-6), overrun ? ("the " + Gangs.ById(a)?.Name + " overran a camp") : ("a fight " + place));
		if (warRecord != null)
		{
			warRecord.Clashes++;
			warRecord.Dead += killed;
			AddHeat(warRecord, 4 + 2 * Math.Min(killed, 4) + (overrun ? 8 : 0), overrun ? "a camp overrun" : ((killed > 0) ? $"{killed} named men killed" : "a fight"));
		}
	}

	private void RefreshWarMarkers()
	{
		if (_mapHidden)
		{
			ClearWarMarkers();
		}
		else
		{
			if (!MapSettled() || _worldCatchingUp)
			{
				return;
			}
			HashSet<string> hashSet = new HashSet<string>();
			if (Config.WarMarkers)
			{
				District[] districts = Districts;
				foreach (District district in districts)
				{
					(Gang, Gang)? tuple = WarIn(district);
					if (!tuple.HasValue)
					{
						continue;
					}
					hashSet.Add(district.Key);
					if (_warMarkers.ContainsKey(district.Key) || string.IsNullOrEmpty(district.Region))
					{
						continue;
					}
					uint num = Joaat.Of(district.Region);
					uint num2 = Joaat.Of(Config.WarRegionStyle);
					Crumbs.Mark("war shading " + district.Region);
					FlushGrudgeLog(force: true);
					int num3;
					try
					{
						num3 = Function.Call<int>(6214909650283936023uL, new InputArgument[2] { num, num2 });
					}
					catch (Exception ex)
					{
						GrudgeLog("WAR shading " + district.Region + " failed: " + ex.Message);
						continue;
					}
					_warMarkers[district.Key] = (int)num;
					string text = "War: the " + tuple.Value.Item1.Name + " vs the " + tuple.Value.Item2.Name;
					if (num3 != 0)
					{
						try
						{
							Crumbs.Native("war shading name");
							Function.Call(11290983183885202434uL, num3, new InputArgument(UiDraw.ForeverVarString(text)));
						}
						catch
						{
						}
					}
					if (Config.Verbose)
					{
						GrudgeLog("WAR " + district.Name + " shaded on the map (" + district.Region + ", " + Config.WarRegionStyle + ")");
					}
				}
			}
			foreach (string item in _warMarkers.Keys.ToList())
			{
				if (!hashSet.Contains(item))
				{
					RemoveWarMarker(item);
				}
			}
			try
			{
				RefreshWarDistrictBlips(hashSet);
			}
			catch (Exception ex2)
			{
				GrudgeLog("WAR district marker failed: " + ex2.Message);
			}
			District currentDistrict = CurrentDistrict;
			(Gang, Gang)? tuple2 = WarIn(currentDistrict);
			if (tuple2.HasValue && currentDistrict.Key != _warNoticeDistrict)
			{
				_warNoticeDistrict = currentDistrict.Key;
				GrudgeNotice("You ride into " + currentDistrict.Name + " - the " + tuple2.Value.Item1.Name + " and the " + tuple2.Value.Item2.Name + " are at war here.");
				Feed("WAR you rode into " + currentDistrict.Name + ": the " + tuple2.Value.Item1.Name + " vs the " + tuple2.Value.Item2.Name, Theme.Warn);
			}
			else if (!tuple2.HasValue)
			{
				_warNoticeDistrict = null;
			}
		}
	}

	internal static string ShortOutfit(Gang g)
	{
		if (g == null)
		{
			return "?";
		}
		switch (g.Id)
		{
		case "odriscoll":
			return "O'Driscolls";
		case "lemoyneraiders":
			return "Raiders";
		case "army":
			return "Army";
		case "klan":
			return "Klan";
		case "wapiti":
			return "Wapiti";
		case "bronte":
			return "Brontes";
		case "murfree":
			return "Murfrees";
		case "skinners":
			return "Skinners";
		default:
		{
			string text = g.Name ?? "?";
			if (!text.StartsWith("The "))
			{
				return text;
			}
			return text.Substring(4);
		}
		}
	}

	private unsafe void RefreshWarDistrictBlips(HashSet<string> live)
	{
		foreach (string item in _warDistrictBlips.Keys.ToList())
		{
			(int, string) tuple = _warDistrictBlips[item];
			if (live.Contains(item) && MAP.DOES_BLIP_EXIST(tuple.Item1))
			{
				continue;
			}
			var (blip, _) = tuple;
			Crumbs.Native("war district blip remove");
			try
			{
				if (MAP.DOES_BLIP_EXIST(blip))
				{
					MAP.REMOVE_BLIP(&blip);
				}
			}
			catch
			{
			}
			_warDistrictBlips.Remove(item);
		}
		if (!Config.WarMarkers)
		{
			return;
		}
		foreach (string key in live)
		{
			District district = Districts.FirstOrDefault((District x) => x.Key == key);
			WarRecord warRecord = Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == key);
			if (district == null || warRecord == null || (district.X == 0f && district.Y == 0f))
			{
				continue;
			}
			Gang g = Gangs.ById(warRecord.A);
			Gang g2 = Gangs.ById(warRecord.B);
			string text = UiDraw.TheFix(WarName(warRecord).Replace("The ", "") + ": " + ShortOutfit(g) + " v " + ShortOutfit(g2) + ", " + (warRecord.Paused ? "truce" : $"Day {WarDay(warRecord)}"));
			if (_warDistrictBlips.TryGetValue(key, out (int, string) value))
			{
				if (value.Item2 != text)
				{
					Function.Call(11290983183885202434uL, value.Item1, new InputArgument(UiDraw.ForeverVarString(text)));
					_warDistrictBlips[key] = (value.Item1, text);
				}
				continue;
			}
			Crumbs.Native("war district blip add");
			int num = MAP.BLIP_ADD_FOR_COORDS(3911271093u, district.X, district.Y, 0f);
			try
			{
				MAP.SET_BLIP_SPRITE(num, MISC.GET_HASH_KEY("blip_town"), p2: true);
			}
			catch
			{
			}
			Function.Call(11290983183885202434uL, num, new InputArgument(UiDraw.ForeverVarString(text)));
			_warDistrictBlips[key] = (num, text);
			GrudgeLog($"WAR map marker: {text} ({district.X:0},{district.Y:0})");
		}
	}

	private void RemoveWarMarker(string key)
	{
		if (!_warMarkers.TryGetValue(key, out var value))
		{
			return;
		}
		bool flag = false;
		try
		{
			flag = CAM.IS_SCREEN_FADED_OUT() || CAM.IS_SCREEN_FADING_OUT();
		}
		catch
		{
		}
		if (!flag)
		{
			Crumbs.Mark("war shading off " + key);
			try
			{
				Function.Call(7459886982293971635uL, (uint)value);
			}
			catch
			{
			}
		}
		_warMarkers.Remove(key);
	}

	internal void ClearWarMarkers()
	{
		foreach (string item in _warMarkers.Keys.ToList())
		{
			RemoveWarMarker(item);
		}
		try
		{
			RefreshWarDistrictBlips(new HashSet<string>());
		}
		catch
		{
		}
	}

	internal static string OutfitWord(Gang g)
	{
		string text = g?.Name ?? "";
		if (text.StartsWith("The "))
		{
			text = text.Substring(4);
		}
		string[] array = new string[4] { " Boys", " Brothers", " Family", " Brood" };
		foreach (string text2 in array)
		{
			if (text.EndsWith(text2))
			{
				return text.Substring(0, text.Length - text2.Length);
			}
		}
		if (text.EndsWith("s") && !text.EndsWith("ss"))
		{
			text = text.Substring(0, text.Length - 1);
		}
		return text;
	}
}
