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
	private static readonly string[] Sparks = new string[8] { "a {a} man shot a {b} man over cards in a saloon in {d}", "{a} men ran off a {b} horse herd in {d}", "a {b} man insulted {al} in front of his men", "{a} men burned a barn the {b} used in {d}", "a {b} man was found strung up on a {a} road in {d}", "{a} men robbed a {b} payroll in {d}", "a woman, a {a} man and a {b} man - and a knife", "{a} men took a {b} man's brother and never brought him back" };

	private static readonly Dictionary<string, (int fear, float range, bool loud, string text)> Crimes = new Dictionary<string, (int, float, bool, string)>
	{
		["kill"] = (35, 60f, true, "saw you kill a man"),
		["shot"] = (25, 50f, true, "saw you shoot a man"),
		["cut"] = (22, 30f, false, "saw you cut a man"),
		["ko"] = (14, 30f, false, "saw you knock a man cold"),
		["punch"] = (5, 25f, false, "saw you beat a man"),
		["rob"] = (14, 30f, false, "saw you rob a man"),
		["stickup"] = (10, 30f, false, "saw you hold a man up"),
		["hogtie"] = (14, 30f, false, "saw you tie a man up"),
		["horse"] = (8, 30f, false, "saw you steal a horse"),
		["wagon"] = (8, 30f, false, "saw you steal a wagon"),
		["horsekill"] = (12, 40f, true, "saw you kill a man's horse")
	};

	private readonly Dictionary<(int, string), int> _witnessedAt = new Dictionary<(int, string), int>();

	internal Personality? LeaderTemper(Gang g)
	{
		if (g == null)
		{
			return null;
		}
		LeaderSeat seat = SeatOf(g);
		if (seat?.Fallen != null)
		{
			if (seat.Successor == null)
			{
				return null;
			}
			return Roster.FirstOrDefault((NemesisRecord n) => n.Id == seat.Successor)?.Personality ?? Personality.Cold;
		}
		if (LeaderLore.TryGetValue(g.Id, out (Personality, int, string, (int, string, string)[]) value))
		{
			return value.Item1;
		}
		Personality value2;
		return DefaultLeaderTemper.TryGetValue(g.Id, out value2) ? value2 : Personality.Cold;
	}

	internal float Appetite(Gang g)
	{
		return LeaderTemper(g) switch
		{
			null => 0.4f, 
			Personality.Hothead => 1.6f, 
			Personality.Proud => 1.4f, 
			Personality.Greedy => 1.15f, 
			Personality.Loyal => 0.85f, 
			Personality.Drifter => 0.8f, 
			Personality.Coward => 0.5f, 
			_ => 1f, 
		};
	}

	private int Caution(Gang g)
	{
		switch (LeaderTemper(g))
		{
		case null:
			return 12;
		case Personality.Coward:
			return 15;
		case Personality.Loyal:
		case Personality.Drifter:
			return 8;
		case Personality.Cold:
		case Personality.Greedy:
			return 6;
		case Personality.Proud:
			return 2;
		case Personality.Hothead:
			return 1;
		default:
			return 6;
		}
	}

	private int RefusePeace(Gang g)
	{
		switch (LeaderTemper(g))
		{
		case null:
			return 10;
		case Personality.Hothead:
			return 60;
		case Personality.Proud:
			return 50;
		case Personality.Greedy:
		case Personality.Loyal:
			return 25;
		case Personality.Cold:
			return 20;
		case Personality.Drifter:
			return 15;
		case Personality.Coward:
			return 5;
		default:
			return 20;
		}
	}

	private string LeaderWord(Gang g)
	{
		Personality? personality = LeaderTemper(g);
		string text = g.LeaderName ?? "their leader";
		if (personality.HasValue)
		{
			return text + " (" + personality.Value.ToString().ToLowerInvariant() + ")";
		}
		return "the " + g.Name + ", with nobody in the leader's seat,";
	}

	internal WarRecord PairWar(string a, string b)
	{
		return (from x in Wars
			where x.Result == null && ((x.A == a && x.B == b) || (x.A == b && x.B == a))
			orderby x.StartMin
			select x).FirstOrDefault();
	}

	internal bool BeatenThere(District d, Gang g)
	{
		if (d != null && g != null && Chronicle.Beaten != null && Chronicle.Beaten.TryGetValue(d.Key + "|" + g.Id, out var value))
		{
			return value > GameMinutesNow();
		}
		return false;
	}

	private void RollWarDeclarations(bool missedDay = false)
	{
		int num = GameDayKey();
		if (_warRollDay == num && !missedDay)
		{
			return;
		}
		if (_warRollDay == 0 && Chronicle.WarRollDay == num)
		{
			_warRollDay = num;
			return;
		}
		_warRollDay = num;
		Chronicle.WarRollDay = num;
		List<(District d, Gang a, Gang b, double pct)> list = new List<(District d, Gang a, Gang b, double pct)>();
		District[] districts = Districts;
		foreach (District d in districts)
		{
			List<string> warZones = Chronicle.WarZones;
			if ((warZones != null && warZones.Any((string t) => t.StartsWith(d.Key + ":"))) || (Chronicle.WarEndedAt != null && Chronicle.WarEndedAt.TryGetValue(d.Key, out var value) && GameMinutesNow() - value < 2880 && GameMinutesNow() >= value))
			{
				continue;
			}
			List<(Gang, int)> list2 = (from kv in ControlMap[d.Key]
				where kv.Value >= 8
				select (g: Gangs.ById(kv.Key), h: kv.Value) into t
				where t.g != null && t.g.TurfWars && !t.g.StoryOnly && !IsDestroyed(t.g)
				select t).ToList();
			for (int num2 = 0; num2 < list2.Count; num2++)
			{
				for (int num3 = num2 + 1; num3 < list2.Count; num3++)
				{
					(Gang, int) tuple = list2[num2];
					(Gang, int) tuple2 = list2[num3];
					int num4 = Relation(tuple.Item1.Id, tuple2.Item1.Id);
					if (num4 > -20 || TruceBetween(tuple.Item1.Id, tuple2.Item1.Id))
					{
						continue;
					}
					double num5 = Math.Min(15.0, (double)(-num4 - 20) * 0.5 * 0.45);
					num5 *= Math.Max(0.4, Math.Min(1.3, (double)Math.Min(tuple.Item2, tuple2.Item2) / 15.0));
					num5 *= ((Math.Abs(tuple.Item2 - tuple2.Item2) <= 10) ? 1.2 : 0.8);
					num5 *= (double)((Appetite(tuple.Item1) + Appetite(tuple2.Item1)) / 2f);
					num5 *= AgendaPairMult(tuple.Item1, tuple2.Item1, d);
					if (WarsOf(tuple.Item1) > 0 && !KeenForWar(tuple.Item1, tuple2.Item1))
					{
						num5 *= 0.75;
					}
					if (WarsOf(tuple2.Item1) > 0 && !KeenForWar(tuple2.Item1, tuple.Item1))
					{
						num5 *= 0.75;
					}
					int num6 = AgendaLean(tuple.Item1, tuple2.Item1, d);
					bool flag = ((num6 != 0) ? (num6 > 0) : (Appetite(tuple.Item1) > Appetite(tuple2.Item1) || (Appetite(tuple.Item1) == Appetite(tuple2.Item1) && tuple.Item2 >= tuple2.Item2)));
					Gang g;
					if (flag)
					{
						(g, _) = tuple;
					}
					else
					{
						(g, _) = tuple2;
					}
					if (!LowSupplies(g))
					{
						District item = d;
						Gang item2;
						if (flag)
						{
							(item2, _) = tuple;
						}
						else
						{
							(item2, _) = tuple2;
						}
						Gang item3;
						if (flag)
						{
							(item3, _) = tuple2;
						}
						else
						{
							(item3, _) = tuple;
						}
						list.Add((item, item2, item3, num5));
					}
				}
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		long num7 = GameMinutesNow() - Chronicle.LastWarStartAt;
		if (Chronicle.LastWarStartAt > 0 && num7 >= 0 && num7 < 4320)
		{
			GrudgeLog($"WAR roll skipped: a war started {(double)num7 / 1440.0:0.0} days ago (one every 3 days at most)");
			return;
		}
		GrudgeLog("WAR roll (once a game day): " + string.Join(", ", from c in list.OrderByDescending(((District d, Gang a, Gang b, double pct) c) => c.pct).Take(8)
			select $"{c.a.Id} on {c.b.Id} in {c.d.Name} {c.pct:0.#}%"));
		foreach (var item4 in list.OrderBy(((District d, Gang a, Gang b, double pct) _) => _rng.Next()))
		{
			if (!(_rng.NextDouble() * 100.0 >= item4.Item4))
			{
				DeclareWar(item4.Item1, item4.Item2, item4.Item3, (AgendaWarMult(item4.Item2, item4.Item3, item4.Item1) > 1.5) ? (LeaderWord(item4.Item2) + " means to " + AgendaText(item4.Item2)) : (LeaderWord(item4.Item2) + " chose to fight"));
				break;
			}
		}
	}

	internal void DeclareWar(District d, Gang a, Gang b, string cause)
	{
		List<string> list = Chronicle.WarZones ?? (Chronicle.WarZones = new List<string>());
		if (list.Any((string t) => t.StartsWith(d.Key + ":")))
		{
			return;
		}
		if (BeatenThere(d, a) && (cause == null || !cause.Contains("debug")))
		{
			GrudgeLog("WAR " + d.Name + ": the " + a.Name + " were beaten here lately - no war from them yet");
			return;
		}
		WarRecord warRecord = PairWar(a.Id, b.Id);
		if (warRecord != null && (cause == null || !cause.Contains("debug")))
		{
			AddHeat(warRecord, 15, "the war spreads to " + d.Name);
			warRecord.GoalLog?.Add($"{GameDate.FromMinutes(GameMinutesNow()):MMM d}: the fighting spread to {d.Name}");
			GrudgeLog("WAR " + d.Name + ": the " + a.Name + " and the " + b.Name + " are already at war (" + WarName(warRecord) + ") - it spreads here, no second war");
			SaveWorld();
			return;
		}
		list.Add(d.Key + ":" + a.Id + "|" + b.Id);
		RelWhy[PairKey(a.Id, b.Id)] = cause;
		Chronicle.LastWarStartAt = GameMinutesNow();
		SaveWorld();
		try
		{
			WarStarted(d, a, b);
		}
		catch (Exception ex)
		{
			GrudgeLog("WAR event failed: " + ex.Message);
		}
		WarRecord warRecord2 = Wars.FirstOrDefault((WarRecord x) => x.District == d.Key);
		string text = "War in " + d.Name + ": the " + a.Name + " came for the " + b.Name + ((warRecord2 != null && warRecord2.GoalText != null) ? (" - " + warRecord2.GoalText) : "") + ".";
		Feed("TERRITORY " + text, Theme.Warn);
		GrudgeNotice(text);
		GrudgeLog("WAR " + text + " (" + cause + ")");
	}

	private void PickWarGoal(WarRecord w, District d, Gang a, Gang b)
	{
		CampSite campSite = (from _ in SitesHeldBy(b.Id)
			where !TakeoverPending(_) && DistrictAt(new Vector3(_.X, _.Y, _.Z)) == d
			orderby _rng.Next()
			select _).FirstOrDefault();
		int num = HoldOf(d, b.Id);
		int num2 = HoldOf(d, a.Id);
		if (campSite != null && _rng.Next(100) < 60)
		{
			w.Goal = "camp";
			w.GoalSite = campSite.Id;
			w.GoalText = "they want the camp at " + campSite.Label;
		}
		else if (num <= num2)
		{
			w.Goal = "drive";
			w.GoalText = "they mean to drive the " + b.Name + " out of " + d.Name;
		}
		else
		{
			w.Goal = "ground";
			w.GoalText = "they want more of " + d.Name;
		}
	}

	private bool WarSettled(WarRecord w, District d)
	{
		if (w == null || d == null)
		{
			return false;
		}
		if (w.Result != null)
		{
			return true;
		}
		if (!WorldClockSettled)
		{
			return false;
		}
		WarRecord warRecord = PairWar(w.A, w.B);
		if (warRecord != null && warRecord != w && warRecord.StartMin <= w.StartMin)
		{
			w.Result = "merged";
			AddHeat(warRecord, 10, "the fighting in " + d.Name + " joined it");
			GrudgeLog("WAR " + d.Name + ": a second war between the same outfits - folded into " + WarName(warRecord));
			return true;
		}
		if (GameMinutesNow() - w.StartMin < 1440 && GameMinutesNow() >= w.StartMin)
		{
			return false;
		}
		int num = HoldOf(d, w.A);
		int num2 = HoldOf(d, w.B);
		long num3 = (GameMinutesNow() - w.StartMin) / 1440;
		CampSite campSite = ((w.GoalSite != null) ? Camps.ById(w.GoalSite) : null);
		if (num < 5)
		{
			w.Result = "defended";
			return true;
		}
		if (num3 >= 3 && num <= w.HoldA0 - 8)
		{
			w.Result = "defended";
			return true;
		}
		Gang gang = Gangs.ById(w.A);
		Gang gang2 = Gangs.ById(w.B);
		if (w.MenA0 <= 0 && gang != null)
		{
			w.MenA0 = Math.Max(1, MenOf(gang));
		}
		if (w.MenB0 <= 0 && gang2 != null)
		{
			w.MenB0 = Math.Max(1, MenOf(gang2));
		}
		if (num3 >= 3 && gang != null && gang2 != null)
		{
			if ((double)MenOf(gang2) <= (double)w.MenB0 * 0.25)
			{
				w.Result = "broken";
				GrudgeLog($"WAR {d.Name}: the {gang2.Name} are broken - {MenOf(gang2)} of the {w.MenB0} men they had");
				return true;
			}
			if ((double)MenOf(gang) <= (double)w.MenA0 * 0.4)
			{
				w.Result = "spent";
				GrudgeLog($"WAR {d.Name}: the {gang.Name} have spent themselves - {MenOf(gang)} of the {w.MenA0} men they had");
				return true;
			}
		}
		switch (w.Goal)
		{
		case "camp":
		{
			if (campSite != null && (SiteOwner(campSite) == w.A || (Chronicle.CampTakeovers != null && Chronicle.CampTakeovers.TryGetValue(campSite.Id, out var value) && value.Gang == w.A)))
			{
				w.Result = "met";
				return true;
			}
			if ((campSite == null || SiteOwner(campSite) != w.B) && !RetargetWar(w, d, "the camp they came for fell to someone else"))
			{
				w.Result = "moot";
				return true;
			}
			break;
		}
		case "drive":
		{
			int value2;
			int num4 = (d.Lore.TryGetValue(w.B, out value2) ? value2 : w.HoldB0);
			bool flag = gang2 == null || (double)MenOf(gang2) <= (double)w.MenB0 * 0.6;
			if ((num2 < 5 || (num2 <= num4 / 2 && num2 <= w.HoldB0 - 8)) && flag)
			{
				w.Result = "met";
				return true;
			}
			break;
		}
		case "ground":
			if (num - w.HoldA0 >= 12)
			{
				w.Result = "met";
				return true;
			}
			break;
		}
		if (w.GiveUpDay <= 0)
		{
			w.GiveUpDay = Math.Max(GiveUpDayFor(w), (int)num3 + 3);
		}
		if (num3 >= Math.Max(20, w.GiveUpDay - 2 * w.OfficersA))
		{
			w.Result = "gave up";
			return true;
		}
		if (num3 >= 5 && Strength(Gangs.ById(w.A)) < 20)
		{
			w.Result = "gave up";
			return true;
		}
		return false;
	}

	private int GiveUpDayFor(WarRecord w)
	{
		Gang gang = Gangs.ById(w.A);
		double num = Math.Max(0.0, Math.Min(10.0, ((double)Appetite(gang) - 0.4) / 1.2 * 10.0));
		double num2 = Math.Min(6.0, (double)Strength(gang) / 100.0 * 6.0);
		int num3 = (int)((uint)Seeds.Stable("giveup/" + w.District + "/" + w.A + "/" + w.StartMin) % 5u);
		int num4 = Math.Max(25, Math.Min(45, 25 + (int)Math.Round(num + num2) + num3));
		GrudgeLog($"WAR {w.District}: the {gang?.Name ?? w.A} will give it up by day {num4} (appetite {Appetite(gang):0.00}, strength {Strength(gang)}, luck {num3})");
		return num4;
	}

	private string WarResultText(WarRecord w, Gang a, Gang b)
	{
		switch (w?.Result)
		{
		case "met":
			if (!(w.Goal == "camp"))
			{
				if (!(w.Goal == "drive"))
				{
					return "the " + a.Name + " won the ground they wanted";
				}
				return "the " + a.Name + " drove the " + b.Name + " out";
			}
			return "the " + a.Name + " took the camp they came for";
		case "defended":
			return "the " + b.Name + " drove the " + a.Name + " off";
		case "moot":
			return "the camp they fought over fell to someone else";
		case "gave up":
			return "the " + a.Name + " gave up on it (" + w.GoalText + ")";
		case "broken":
			return "the " + b.Name + " ran out of men and broke";
		case "merged":
			return "it became part of the war the " + a.Name + " and the " + b.Name + " were already fighting";
		case "spent":
			return "the " + a.Name + " ran out of men and went home";
		case "peace":
			return "the " + a.Name + " and the " + b.Name + " made peace";
		default:
			return null;
		}
	}

	private CampSite WarGoalCamp(Gang attacker, Gang defender, District d)
	{
		WarRecord warRecord = Wars.FirstOrDefault((WarRecord x) => x.District == d?.Key && x.A == attacker?.Id && x.B == defender?.Id && x.Goal == "camp");
		CampSite campSite = ((warRecord != null && warRecord.GoalSite != null) ? Camps.ById(warRecord.GoalSite) : null);
		if (campSite == null || !(SiteOwner(campSite) == defender.Id) || TakeoverPending(campSite) || _liveCamps.ContainsKey(campSite.Id))
		{
			return null;
		}
		return campSite;
	}

	private bool LeadersSueForPeace(WarRecord w, District d, WorldEvent ev, long days)
	{
		if (days < 4 || TruceBetween(w.A, w.B))
		{
			return false;
		}
		Gang gang = Gangs.ById(w.A);
		Gang gang2 = Gangs.ById(w.B);
		int num = (int)Math.Round((float)(Caution(gang) + Caution(gang2)) * HeatPeaceMult(w));
		if (StretchedThin(gang) || StretchedThin(gang2))
		{
			num = (int)Math.Round((double)num * 1.25);
		}
		if (LowSupplies(gang) || LowSupplies(gang2))
		{
			num = (int)Math.Round((double)num * 1.2);
		}
		num += Math.Max(0, 40 - Math.Min(Strength(gang), Strength(gang2))) / 2 + (int)Math.Min(15L, days / 6);
		if (_rng.Next(100) >= num)
		{
			return false;
		}
		Gang gang3 = ((Caution(gang) >= Caution(gang2)) ? gang : gang2);
		Gang gang4 = ((gang3 == gang) ? gang2 : gang);
		int num2 = HoldOf(d, gang4.Id);
		int num3 = ((gang4.Id == w.A) ? w.HoldA0 : w.HoldB0);
		int val = RefusePeace(gang4) + GrievanceOf(w.A, w.B) / 2 + ((num2 > num3 + 2) ? 20 : ((num2 < num3 - 2) ? (-15) : 0)) - ((Strength(gang4) < 25) ? 20 : 0);
		val = Math.Max(0, Math.Min(90, val));
		int num4 = _rng.Next(100);
		if (num4 < val)
		{
			ev.Lines.Add(LeaderWord(gang3) + " offered peace. " + LeaderWord(gang4) + " wouldn't hear of it.");
			AddHeat(w, 5, "a peace offer refused");
			ShiftRelation(w.A, w.B, -3, LeaderWord(gang4) + " turned down peace");
			GrudgeLog($"WAR {d.Name}: {LeaderWord(gang3)} offered peace ({num}%) - {LeaderWord(gang4)} refused ({val}%, rolled {num4})");
			return false;
		}
		int num5 = 3 + _rng.Next(3);
		Truces.Add($"{w.A}|{w.B}|{GameMinutesNow() + (long)num5 * 1440L}");
		_sidedAgainst.Clear();
		ev.Lines.Add($"{LeaderWord(gang3)} sued for peace - a truce for {num5} days.");
		bool flag = days >= 10 || Math.Min(Strength(gang), Strength(gang2)) < 35;
		ShiftRelation(w.A, w.B, flag ? 20 : 10, LeaderWord(gang3) + " sued for peace");
		if (flag)
		{
			w.Result = "peace";
			ev.Lines.Add("This time it holds - the war is over.");
		}
		else
		{
			ev.Lines.Add("Only a truce - the war waits.");
		}
		GrudgeLog(string.Format("WAR {0}: {1} sued for peace ({2}%) - truce {3} days{4}", d.Name, LeaderWord(gang3), num, num5, flag ? ", and it ends the war" : ", the war is paused"));
		return true;
	}

	private void RollFlashpoint(int days)
	{
		for (int i = 0; i < Math.Min(days, 3); i++)
		{
			if (_rng.Next(100) >= 20)
			{
				continue;
			}
			List<(District, Gang, Gang, int)> list = new List<(District, Gang, Gang, int)>();
			District[] districts = Districts;
			foreach (District district in districts)
			{
				List<Gang> list2 = (from kv in ControlMap[district.Key]
					where kv.Value >= 5
					select Gangs.ById(kv.Key) into g
					where g != null && !g.StoryOnly && !IsDestroyed(g)
					select g).ToList();
				for (int num = 0; num < list2.Count; num++)
				{
					for (int num2 = num + 1; num2 < list2.Count; num2++)
					{
						list.Add((district, list2[num], list2[num2], Math.Min(HoldOf(district, list2[num].Id), HoldOf(district, list2[num2].Id))));
					}
				}
			}
			if (list.Count == 0)
			{
				break;
			}
			double num3 = _rng.NextDouble() * (double)list.Sum<(District, Gang, Gang, int)>(((District d, Gang a, Gang b, int w) p) => p.w);
			(District d, Gang a, Gang b, int w) pick = list.Last();
			foreach (var item in list)
			{
				if ((num3 -= (double)item.Item4) <= 0.0)
				{
					pick = item;
					break;
				}
			}
			bool flag = _rng.Next(2) == 0;
			Gang A = (flag ? pick.a : pick.b);
			Gang B = (flag ? pick.b : pick.a);
			string text = Sparks[_rng.Next(Sparks.Length)].Replace("{a}", A.Name).Replace("{b}", B.Name).Replace("{d}", pick.d.Name)
				.Replace("{al}", A.LeaderName ?? "their leader");
			WorldEvent worldEvent = NewEvent("WAR", GameMinutesNow());
			worldEvent.Gangs.Add(A.Id);
			worldEvent.Gangs.Add(B.Id);
			worldEvent.Title = "Trouble in " + pick.d.Name + ": the " + A.Name + " and the " + B.Name;
			worldEvent.Place = "in " + pick.d.Name;
			worldEvent.HasPos = true;
			worldEvent.X = pick.d.X;
			worldEvent.Y = pick.d.Y;
			worldEvent.Z = 100f;
			worldEvent.Lines.Add(char.ToUpper(text[0]) + text.Substring(1) + ".");
			int num4 = -(12 + _rng.Next(14));
			ShiftRelation(A.Id, B.Id, num4, text);
			AddGrievance(A.Id, B.Id, 8, "a flashpoint");
			if (TruceBetween(A.Id, B.Id) && _rng.Next(100) < 40)
			{
				Truces.RemoveAll(delegate(string s)
				{
					string[] array = s.Split('|');
					return (array[0] == A.Id && array[1] == B.Id) || (array[0] == B.Id && array[1] == A.Id);
				});
				ShiftRelation(A.Id, B.Id, -15, "broke the truce");
				AddGrievance(A.Id, B.Id, 15, "broke the truce");
				worldEvent.Lines.Add("The truce between them is broken.");
			}
			int num5;
			if (A.TurfWars && B.TurfWars && HoldOf(pick.d, A.Id) >= 8 && HoldOf(pick.d, B.Id) >= 8 && !TruceBetween(A.Id, B.Id) && Relation(A.Id, B.Id) <= -20)
			{
				List<string> warZones = Chronicle.WarZones;
				num5 = ((warZones == null || !warZones.Any((string t) => t.StartsWith(pick.d.Key + ":"))) ? 1 : 0);
			}
			else
			{
				num5 = 0;
			}
			bool flag2 = (byte)num5 != 0;
			worldEvent.Heard = true;
			Chronicle.Events.Add(worldEvent);
			SaveWorld();
			GrudgeLog(string.Format("SPARK {0}: {1} (relation {2}){3}", pick.d.Name, text, num4, flag2 ? "" : " - no war possible here"));
			long num6 = GameMinutesNow() - Chronicle.LastWarStartAt;
			bool flag3 = Chronicle.LastWarStartAt > 0 && num6 >= 0 && num6 < 4320;
			if (flag2 && !flag3 && _rng.Next(100) < 10)
			{
				DeclareWar(pick.d, A, B, text);
			}
		}
	}

	private void FadeGrievances(int days)
	{
		foreach (string item in Grievances.Keys.ToList())
		{
			string[] array = item.Split('|');
			if (array.Length != 2 || !GrievanceHeld(array[0], array[1]))
			{
				int num = Grievances[item] - Math.Min(days, 30);
				if (num <= 0)
				{
					Grievances.Remove(item);
				}
				else
				{
					Grievances[item] = num;
				}
			}
		}
	}

	internal string GrievanceLine()
	{
		return "grievances: " + ((Grievances.Count == 0) ? "none" : string.Join(", ", from kv in Grievances.OrderByDescending((KeyValuePair<string, int> kv) => kv.Value).Take(8)
			select $"{kv.Key} {kv.Value}"));
	}

	private static string CrimeOf(GrudgeAction a)
	{
		switch (a)
		{
		case GrudgeAction.Punch:
			return "punch";
		case GrudgeAction.KnockOut:
			return "ko";
		case GrudgeAction.Hogtie:
			return "hogtie";
		case GrudgeAction.Robbed:
			return "rob";
		case GrudgeAction.StickUp:
		case GrudgeAction.StickUpCalm:
			return "stickup";
		case GrudgeAction.HorseStolen:
			return "horse";
		case GrudgeAction.WagonStolen:
			return "wagon";
		case GrudgeAction.Slashed:
			return "cut";
		case GrudgeAction.HorseKilled:
			return "horsekill";
		case GrudgeAction.Wounded:
			return "shot";
		default:
			return null;
		}
	}
}
