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
	internal const string WoffordJailed = "turned in on his bounty";

	private static readonly string[] WoffordOrder = new string[3] { "Alive", "Dead", "In jail" };

	internal const string BronteFate = "fed to an alligator by Dutch van der Linde";

	internal const string BronteSaid = "died in your story";

	private static readonly (string from, string to)[] LeaderRenames = new(string, string)[7]
	{
		("General Beauregard Coyle", "Lindsey Wofford"),
		("Beauregard Coyle", "Lindsey Wofford"),
		("General Coyle", "Wofford"),
		("Gen. Harlan Whitlock", "Col. Henry Favours"),
		("Harlan Whitlock", "Henry Favours"),
		("General Whitlock", "Colonel Favours"),
		("Gen. Whitlock", "Col. Favours")
	};

	private static readonly Dictionary<string, int> StartMen = new Dictionary<string, int>
	{
		["odriscoll"] = 1200,
		["lemoyneraiders"] = 1000,
		["bronte"] = 600,
		["klan"] = 450,
		["wapiti"] = 400,
		["murfree"] = 450,
		["skinners"] = 500,
		["army"] = 3000,
		["laramie"] = 600,
		["delobo"] = 800,
		["nightfolk"] = 250
	};

	private static readonly Dictionary<string, int> MidStartMen = new Dictionary<string, int>
	{
		["odriscoll"] = 200,
		["lemoyneraiders"] = 180,
		["bronte"] = 120,
		["klan"] = 90,
		["wapiti"] = 80,
		["murfree"] = 90,
		["skinners"] = 100,
		["army"] = 600
	};

	private void Fall(Gang g, string who, string how, bool byYou = false, NemesisRecord record = null, string model = null)
	{
		if (record == null && !Roster.Any((NemesisRecord n) => !n.IsAlive && n.Gang == g.Id && n.Name == who))
		{
			NemesisRecord nemesisRecord = new NemesisRecord();
			nemesisRecord.Id = $"fallen_{g.Id}_{GameMinutesNow()}";
			nemesisRecord.Name = who;
			nemesisRecord.Title = "";
			nemesisRecord.Gang = g.Id;
			nemesisRecord.Rank = 5;
			nemesisRecord.IsAlive = false;
			nemesisRecord.Origin = GrudgeOrigin.GangMember;
			nemesisRecord.SeedMember = true;
			nemesisRecord.KilledByPlayer = byYou;
			nemesisRecord.ModelHash = ((model != null) ? MISC.GET_HASH_KEY(model.Split('|')[0]) : 0u);
			nemesisRecord.KillLocation = GetZoneName(Game.Player.Character.Position);
			NemesisRecord nemesisRecord2 = nemesisRecord;
			if (how == "arrested")
			{
				nemesisRecord2.Fate = "Arrested";
			}
			GiveLeaderLife(nemesisRecord2, g);
			History(nemesisRecord2, FallLabel(how) + " - the leader of " + g.Name + ", " + how + (byYou ? " (by you)" : ""), Theme.Blood);
			Roster.Add(nemesisRecord2);
			SaveRoster();
			GrudgeLog("LEADER " + who + ": on the " + g.Name + " fallen row");
		}
		if (byYou)
		{
			AddNotoriety(g, 40, (how.StartsWith("killed") ? "killed" : "brought down") + " their leader, " + who);
		}
		Seats[g.Id] = new LeaderSeat
		{
			Fallen = how,
			OldName = who,
			At = GameMinutesNow()
		};
		ApplyLeaders();
		SaveWorld();
		try
		{
			_nextRosterSave = 0;
			FlushRosterSave();
			SaveWorldNow(background: true);
			_nextWorldSave = Game.GameTime + 60000;
		}
		catch (Exception ex)
		{
			GrudgeLog("SAVE after a leader's fall failed: " + ex.Message);
		}
		GrudgeLog("LEADER " + g.Name + ": " + who + " " + how + " - the seat is open");
		Feed("LEADER " + who + " of " + g.Name + " " + how + " - the seat is open", g.Accent);
		DeathNotice(who + " was " + how + ". " + g.Name + " have no leader - for now.");
		Vector3 position = Game.Player.Character.Position;
		if (byYou)
		{
			ChronicleDeed("leader", who + " of " + g.Name + " was " + how, "near " + GetZoneName(position), position, g.Id, new WorldPart
			{
				Name = who,
				Gang = g.Id,
				Outcome = ((FallLabel(how) == "ARRESTED") ? "JAILED" : "KILLED"),
				Note = "the leader"
			});
		}
	}

	private static string FallLabel(string how)
	{
		if (how == null || (!how.StartsWith("arrested") && !how.StartsWith("turned in")))
		{
			if (LeftFate(how) == null)
			{
				return "DIED";
			}
			return "GONE";
		}
		return "ARRESTED";
	}

	private static string LeftFate(string how)
	{
		if (how != null)
		{
			if (!how.StartsWith("left"))
			{
				if (!how.StartsWith("went north"))
				{
					if (!(how == "broke up"))
					{
						if (!how.StartsWith("taken in on his bounty"))
						{
							return null;
						}
						return "Gone";
					}
					return "Gone";
				}
				return "Went to Canada";
			}
			return "Left";
		}
		return null;
	}

	internal string WoffordState()
	{
		LeaderSeat leaderSeat = SeatOf(Gangs.LemoyneRaiders);
		if (leaderSeat == null || leaderSeat.Fallen == null)
		{
			return "Alive";
		}
		if (!leaderSeat.Fallen.StartsWith("turned in") && !leaderSeat.Fallen.StartsWith("arrested"))
		{
			return "Dead";
		}
		return "In jail";
	}

	internal string WoffordAfter(int dir)
	{
		return WoffordOrder[(Array.IndexOf(WoffordOrder, WoffordState()) + dir + 3) % 3];
	}

	internal string WoffordLocked()
	{
		return StoryFateLocked(Gangs.LemoyneRaiders, "killed by you on his bounty", "turned in on his bounty");
	}

	internal string WoffordAsk(int dir)
	{
		string text = WoffordAfter(dir);
		if (!(text == "Dead"))
		{
			if (!(text == "In jail"))
			{
				return "Bring Lindsey Wofford back? Only if he's still alive in your game. He leads the Lemoyne Raiders again.";
			}
			return "Mark Lindsey Wofford in jail? Only if you've already turned him in alive. The Lemoyne Raiders pick a new leader within a few days.";
		}
		return "Mark Lindsey Wofford dead? Only if you've already collected his bounty dead. The Lemoyne Raiders pick a new leader within a few days.";
	}

	internal void SetWofford(int dir)
	{
		Gang lemoyneRaiders = Gangs.LemoyneRaiders;
		string text = WoffordAfter(dir);
		string text2 = WoffordLocked();
		if (text2 != null)
		{
			GrudgeLog("SETUP Wofford: " + text2 + " - left as it is");
			return;
		}
		SetLeaderFate(lemoyneRaiders, (text == "Alive") ? null : ((text == "Dead") ? "killed by you on his bounty" : "turned in on his bounty"), lemoyneRaiders.LoreLeader ?? "Lindsey Wofford");
		GrudgeLog("SETUP Lindsey Wofford: " + text);
	}

	internal string BronteState()
	{
		if (SeatOf(Gangs.SaintDenisMob)?.Fallen != null)
		{
			return "Dead";
		}
		return "Alive";
	}

	internal string BronteLocked()
	{
		return StoryFateLocked(Gangs.SaintDenisMob, "died in your story", "fed to an alligator by Dutch van der Linde");
	}

	internal string BronteAsk(int dir)
	{
		if (!(BronteState() == "Alive"))
		{
			return "Bring Angelo Bronte back? Only if he's still alive in your story. He leads the Bronte Family again.";
		}
		return "Mark Angelo Bronte dead? Only if he's already dead in your story. The Bronte Family pick a new leader within a few days.";
	}

	internal void SetBronte(int dir)
	{
		Gang saintDenisMob = Gangs.SaintDenisMob;
		string text = ((BronteState() == "Alive") ? "Dead" : "Alive");
		string text2 = BronteLocked();
		if (text2 != null)
		{
			GrudgeLog("SETUP Bronte: " + text2 + " - left as it is");
			return;
		}
		SetLeaderFate(saintDenisMob, (text == "Dead") ? "died in your story" : null, saintDenisMob.LoreLeader ?? "Angelo Bronte");
		GrudgeLog("SETUP Angelo Bronte: " + text);
	}

	private string StoryFateLocked(Gang g, params string[] cards)
	{
		LeaderSeat leaderSeat = SeatOf(g);
		if (leaderSeat == null || leaderSeat.Fallen == null)
		{
			return null;
		}
		if (leaderSeat.Successor != null)
		{
			return "The " + g.Name + " already have a new leader - this can't be changed now.";
		}
		if (!cards.Contains(leaderSeat.Fallen))
		{
			return (leaderSeat.OldName ?? "He") + " fell in your game (" + leaderSeat.Fallen + ") - this can't be changed here.";
		}
		return null;
	}

	private void SetLeaderFate(Gang g, string fallen, string oldName)
	{
		LeaderSeat s = SeatOf(g);
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == LeaderCopyId(g) && n.IsAlive);
		if (nemesisRecord != null)
		{
			BenchLeaderCopy(nemesisRecord, "the setup card");
		}
		Roster.RemoveAll((NemesisRecord n) => !n.IsAlive && n.Gang == g.Id && ((n.Id.StartsWith("fallen_" + g.Id + "_") && s != null && n.Id == $"fallen_{g.Id}_{s.At}") || n.Id == LeaderCopyId(g)));
		if (fallen == null)
		{
			Seats.Remove(g.Id);
		}
		else
		{
			Seats[g.Id] = new LeaderSeat
			{
				Fallen = fallen,
				OldName = oldName,
				At = GameMinutesNow()
			};
		}
		ApplyLeaders();
		BackfillFallenLeaders();
		if (fallen != null)
		{
			NemesisRecord nemesisRecord2 = Roster.FirstOrDefault((NemesisRecord n) => n.Id == $"fallen_{g.Id}_{SeatOf(g)?.At}");
			if (nemesisRecord2 != null)
			{
				nemesisRecord2.Buried = true;
			}
		}
		SaveRoster();
		SaveWorld();
	}

	private void FixWofford1907()
	{
		WorldState chronicle = Chronicle;
		if (chronicle.WoffordFixed || Gangs.Era < 1907)
		{
			return;
		}
		chronicle.WoffordFixed = true;
		Gang lemoyneRaiders = Gangs.LemoyneRaiders;
		if (SeatOf(lemoyneRaiders)?.Fallen == null)
		{
			if (chronicle.EraTurnAt <= 0)
			{
				chronicle.EraTurnAt = ((chronicle.StoryChapterAt > 0) ? chronicle.StoryChapterAt : GameMinutesNow());
			}
			Seats[lemoyneRaiders.Id] = new LeaderSeat
			{
				Fallen = "taken in on his bounty years ago - dead or in Sisika, nobody's sure",
				OldName = (lemoyneRaiders.LoreLeader ?? "Lindsey Wofford"),
				At = chronicle.EraTurnAt,
				FillAt = GameMinutesNow()
			};
			ApplyLeaders();
			SaveWorld();
			GrudgeLog("LEADER Lindsey Wofford: gone by 1907 (one-time) - the Raiders pick a new head");
		}
	}

	private void FixEraFunerals()
	{
		WorldState chronicle = Chronicle;
		if (chronicle.EraFuneralsFixed || Gangs.Era < 1907)
		{
			return;
		}
		chronicle.EraFuneralsFixed = true;
		if (chronicle.EraTurnAt <= 0)
		{
			chronicle.EraTurnAt = ((chronicle.StoryChapterAt > 0) ? chronicle.StoryChapterAt : GameMinutesNow());
		}
		HashSet<string> ids = new HashSet<string>();
		foreach (KeyValuePair<string, LeaderSeat> kv in Seats)
		{
			LeaderSeat s = kv.Value;
			if (s?.Fallen == null || s.At > chronicle.EraTurnAt + 1440)
			{
				continue;
			}
			foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => !n.IsAlive && n.Gang == kv.Key && (n.Id.StartsWith("fallen_" + kv.Key + "_") || n.Name == s.OldName)))
			{
				string text = LeftFate(s.Fallen);
				if (text != null && item.Fate == null)
				{
					item.Fate = text;
					foreach (HistoryEntry item2 in item.History ?? new List<HistoryEntry>())
					{
						if (item2.Text != null && item2.Text.StartsWith("DIED - the leader"))
						{
							item2.Text = "GONE" + item2.Text.Substring(4);
						}
					}
				}
				item.Buried = true;
				ids.Add(item.Id);
			}
		}
		int num = Plans.RemoveAll((Plan p) => p.Kind == "funeral" && !p.Done && (FuneralDead(p).Any(ids.Contains) || p.Gang == "nightfolk"));
		SaveRoster();
		SaveWorld();
		try
		{
			RefreshPlanBlips();
		}
		catch
		{
		}
		GrudgeLog($"FUNERAL one-time: {ids.Count} leader(s) gone since the turn to 1907 set as long buried / gone - {num} funeral(s) called off");
	}

	private long SeatFillAt(Gang g, LeaderSeat seat)
	{
		if (seat.FillAt > 0)
		{
			return seat.FillAt;
		}
		int top = ((g.Tiers != null && g.Tiers.Length != 0) ? g.Tiers[0].MinRank : 4);
		List<int> list = (from n in Roster
			where n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n.Rank >= top && !n.Paralyzed
			select Standing(n).score into s
			orderby s descending
			select s).ToList();
		double num = ((list.Count >= 2 && list[0] - list[1] >= 25) ? 0.35 : ((list.Count == 1) ? 0.5 : ((list.Count == 0) ? 0.9 : 0.65)));
		double num2 = 1.0 + 3.0 * Math.Min(1.0, Math.Max(0.0, num + (_rng.NextDouble() - 0.5) * 0.6));
		long num3 = Math.Max(seat.At, GameMinutesNow() - 5760);
		seat.FillAt = num3 + (long)(num2 * 24.0 * 60.0) + _rng.Next(60);
		SaveWorld();
		GrudgeLog($"LEADER {g.Name}: the seat will be filled in about {(seat.FillAt - GameMinutesNow()) / 60} game hours ({list.Count} officers)");
		return seat.FillAt;
	}

	private bool SeatDue(Gang g)
	{
		LeaderSeat leaderSeat = SeatOf(g);
		if (leaderSeat != null)
		{
			return GameMinutesNow() >= SeatFillAt(g, leaderSeat);
		}
		return false;
	}

	private (string line, NemesisRecord heir)? FillLeaderSeat(Gang g, List<NemesisRecord> living)
	{
		if (!LeaderVacant(g) || g.Tiers == null || g.Tiers.Length == 0)
		{
			return null;
		}
		if (!SeatDue(g))
		{
			return null;
		}
		NemesisRecord nemesisRecord = (from n in living
			where n.Rank >= g.Tiers[0].MinRank && !n.Paralyzed
			orderby Standing(n).score descending, n.Id
			select n).FirstOrDefault() ?? (from n in living
			where !n.Paralyzed
			orderby n.Rank descending, Standing(n).score descending
			select n).FirstOrDefault();
		if (nemesisRecord == null)
		{
			return null;
		}
		LeaderSeat leaderSeat = Seats[g.Id];
		leaderSeat.Successor = nemesisRecord.Id;
		nemesisRecord.Rank = 5;
		nemesisRecord.BossId = null;
		History(nemesisRecord, "TOOK THE LEAD of " + g.Name + " after " + leaderSeat.OldName + " was " + leaderSeat.Fallen, Theme.Gold);
		Remember(nemesisRecord, "Pride", 70, "he leads " + g.Name + " now");
		ApplyLeaders();
		SaveWorld();
		GrudgeLog("LEADER " + g.Name + ": " + nemesisRecord.FullName + " takes the seat (" + Standing(nemesisRecord).why + ")");
		if (g == Gangs.Army && Chronicle.FortCleared)
		{
			Chronicle.FortCleared = false;
			Chronicle.FortKnown = false;
			SaveWorld();
			GrudgeLog("ARMY fort: a new commander holds Fort Wallace - its ways unknown again");
			Feed("HIDEOUT " + nemesisRecord.Name + " holds Fort Wallace now - an officer could tell you his ways", Theme.Warn);
			ReplyNotice("A new commander holds Fort Wallace: " + nemesisRecord.Name + ". His ways will have to be learned again.");
		}
		if (g == Gangs.Army)
		{
			RefreshFortBlip();
		}
		return (nemesisRecord.Name + " takes the lead of " + g.Name + "  -  " + leaderSeat.OldName + " is gone", nemesisRecord);
	}

	private void FixDuplicateNames()
	{
		int num = 0;
		foreach (IGrouping<string, NemesisRecord> item in (from n in Roster
			where n.IsAlive && !n.Story && n.Name != null && n.Name.Contains(' ')
			group n by n.Name into g
			where g.Count() > 1
			select g).ToList())
		{
			foreach (NemesisRecord r in (from n in item
				orderby (!Keeps(n)) ? 1 : 0, n.KilledPlayerAt
				select n).Skip(1).ToList())
			{
				Gang gang = Gangs.Of(r);
				if (Keeps(r) || (gang != null && !gang.FamilyNames) || (gang != null && gang.RankInName))
				{
					continue;
				}
				string text = NameBank.Surname(r.Name);
				string name = r.Name;
				string[] array = (Models.Female(r.ModelHash) ? FemaleFirstFor(gang?.FirstNames ?? NameBank.AmericanFirst) : (gang?.FirstNames ?? NameBank.AmericanFirst));
				for (int num2 = 0; num2 < 30; num2++)
				{
					if (!Roster.Any((NemesisRecord n) => n != r && n.Name == r.Name))
					{
						break;
					}
					r.Name = array[_rng.Next(array.Length)] + " " + text;
				}
				if (Roster.Any((NemesisRecord n) => n != r && n.Name == r.Name))
				{
					r.Name = name;
				}
				if (r.Name != name)
				{
					num++;
					GrudgeLog("NAMING two men were " + name + " - the one you never met is " + r.Name + " now");
				}
			}
		}
		if (num > 0)
		{
			SaveRoster();
		}
		bool Keeps(NemesisRecord n)
		{
			if (n.TimesAmbushed + n.TimesInterrogated + n.TimesKilledPlayer <= 0 && !n.PersonalityKnown && n.LoreModel == null && !n.Id.StartsWith("lore_"))
			{
				return Seats.Values.Any((LeaderSeat s) => s.Successor == n.Id);
			}
			return true;
		}
	}

	private void FixLeaderNames()
	{
		try
		{
			FixDuplicateNames();
		}
		catch (Exception ex)
		{
			GrudgeLog("NAMING duplicate fix failed: " + ex.Message);
		}
		int n = 0;
		foreach (NemesisRecord item3 in Roster)
		{
			if (item3.History != null)
			{
				foreach (HistoryEntry item4 in item3.History)
				{
					item4.Text = Fix(item4.Text);
				}
			}
			if (item3.Past == null)
			{
				continue;
			}
			foreach (PastEntry item5 in item3.Past)
			{
				item5.Text = Fix(item5.Text);
			}
		}
		foreach (WorldEvent item6 in Chronicle.Events.Concat(Chronicle.Past ?? new List<WorldEvent>()))
		{
			item6.Title = Fix(item6.Title);
			for (int i = 0; i < item6.Lines.Count; i++)
			{
				item6.Lines[i] = Fix(item6.Lines[i]);
			}
		}
		if (n > 0)
		{
			GrudgeLog($"LEADER renamed the old made-up leaders in {n} saved texts (Wofford / Favours)");
			SaveRoster();
			SaveWorld();
		}
		string Fix(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return s;
			}
			string text = s;
			(string, string)[] leaderRenames = LeaderRenames;
			for (int j = 0; j < leaderRenames.Length; j++)
			{
				(string, string) tuple = leaderRenames[j];
				string item = tuple.Item1;
				string item2 = tuple.Item2;
				s = s.Replace(item, item2);
			}
			if (s != text)
			{
				n++;
			}
			return s;
		}
	}
}
