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
	private static readonly Dictionary<string, string[]> FateTowns = new Dictionary<string, string[]>
	{
		["odriscoll"] = new string[3] { "Valentine", "Strawberry", "Annesburg" },
		["lemoyneraiders"] = new string[2] { "Rhodes", "Saint Denis" },
		["klan"] = new string[1] { "Rhodes" },
		["bronte"] = new string[1] { "Saint Denis" },
		["murfree"] = new string[2] { "Annesburg", "Van Horn" },
		["skinners"] = new string[2] { "Blackwater", "Strawberry" },
		[""] = new string[5] { "Valentine", "Rhodes", "Blackwater", "Strawberry", "Saint Denis" }
	};

	internal static List<LoreRow> EraLoreRows = new List<LoreRow>
	{
		new LoreRow
		{
			Year = 1899,
			Month = 6,
			Title = "Colm O'Driscoll hanged",
			Place = "",
			Gang = "odriscoll",
			Lines = { "The law finally put a rope around Colm O'Driscoll's neck.", "His boys scattered - some to other outfits, some to the rope, a few to honest work." }
		},
		new LoreRow
		{
			Year = 1899,
			Month = 4,
			Title = "Angelo Bronte is dead",
			Place = "Saint Denis",
			Gang = "bronte",
			Lines = { "Saint Denis's crime boss disappeared one night; the papers never said where.", "His lieutenants kept the family's grip on the city - the docks, the police, the mayor's ear." }
		},
		new LoreRow
		{
			Year = 1899,
			Month = 10,
			Title = "The Wapiti leave for Canada",
			Place = "Ambarino",
			Gang = "wapiti",
			Lines = { "Rains Fall led what was left of his people north, out of the Army's reach." }
		},
		new LoreRow
		{
			Year = 1899,
			Month = 11,
			Title = "The Van der Linde gang is finished",
			Place = "Beaver Hollow",
			Gang = "vdl",
			Lines = { "Dutch van der Linde's gang tore itself apart. Arthur Morgan is dead; the rest scattered." }
		},
		new LoreRow
		{
			Year = 1901,
			Month = 3,
			Title = "Colonel Favours leaves Fort Wallace",
			Place = "Fort Wallace",
			Gang = "army",
			Lines = { "The colonel took a post back East. A new commander holds the fort, with fewer men." }
		},
		new LoreRow
		{
			Year = 1902,
			Month = 5,
			Title = "The Del Lobos ride out of Mexico",
			Place = "New Austin",
			Gang = "delobo",
			Lines = { "Bandits from across the river make the badlands their own - Gaptooth Ridge, Rio Bravo, Cholla Springs." }
		},
		new LoreRow
		{
			Year = 1903,
			Month = 8,
			Title = "Rustlers on the Great Plains",
			Place = "the Great Plains",
			Gang = "laramie",
			Lines = { "The Laramie boys run off herds from Hennigan's Stead to Blackwater, and change the brands before the owners wake." }
		},
		new LoreRow
		{
			Year = 1904,
			Month = 9,
			Title = "Lanterns in the bayou",
			Place = "Bayou Nwa",
			Gang = "nightfolk",
			Lines = { "Travellers vanish on the roads south of Saint Denis. Those who come back talk about lanterns in the trees, and the Night Folk." }
		},
		new LoreRow
		{
			Year = 1905,
			Month = 4,
			Title = "Blackwater grows respectable",
			Place = "Blackwater",
			Lines = { "New money, new streets, a bigger jail. The town wants the outlaw years forgotten." }
		},
		new LoreRow
		{
			Year = 1906,
			Month = 7,
			Title = "The old Raiders grow old",
			Place = "Lemoyne",
			Gang = "lemoyneraiders",
			Lines = { "The Lemoyne Raiders still ride the bayous - fewer every year, and angrier." }
		}
	};

	private bool _holdTurn;

	private const int ZoneTypeState = 0;

	internal void MarkYouInStoryGang()
	{
		bool flag = IsEpiloguePlayer();
		NemesisRecord nemesisRecord = StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == "vdl_arthur");
		NemesisRecord nemesisRecord2 = StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == "vdl_john");
		if (nemesisRecord != null)
		{
			nemesisRecord.Title = (flag ? "an enforcer" : "(you)");
		}
		if (nemesisRecord2 != null)
		{
			nemesisRecord2.Title = (flag ? "(you)" : "a gun");
		}
	}

	internal static int EraYearNow
	{
		get
		{
			if (Gangs.Era < 1907)
			{
				return GameYear();
			}
			return Math.Max(1907, GameYear());
		}
	}

	internal void EightYearsLater(string why, bool debug = false, bool alreadyEpilogue = false)
	{
		WorldState chronicle = Chronicle;
		if (chronicle.Era >= 1907)
		{
			GrudgeLog("EPILOGUE already 1907 - nothing to do");
			return;
		}
		try
		{
			BenchLeaderCopies("the turn to 1907");
		}
		catch (Exception ex)
		{
			GrudgeLog("EPILOGUE leaders' copies: " + ex.Message);
		}
		if (_menu.IsOpen)
		{
			_menu.Close();
		}
		try
		{
			_bulletImpact.CloseViewer();
		}
		catch
		{
		}
		if (!debug && (alreadyEpilogue || chronicle.HistoryYear >= 1906 || (chronicle.Events.Count > 0 && !chronicle.Events.Any((WorldEvent e) => e.GameMin > 0 && GameDate.FromMinutes(e.GameMin).Year < 1906))))
		{
			OutfitsOf1907(why);
			return;
		}
		long num = (from e in chronicle.Events
			where Old(e.GameMin)
			select e.GameMin).DefaultIfEmpty(0L).Max();
		if (Old(chronicle.LastClockMin))
		{
			num = Math.Max(num, chronicle.LastClockMin);
		}
		if (num <= 0)
		{
			num = (Old(GameMinutesRaw()) ? GameMinutesRaw() : GameDate.ToMinutes(new DateTime(1899, 12, 31, 12, 0, 0)));
		}
		string text = TakeSavePoint("before 1907", sync: true, num);
		if (!debug)
		{
			chronicle.StoryChapter = 7;
			chronicle.StoryChapterAt = GameMinutesNow();
		}
		GrudgeLog("EPILOGUE eight years later (" + why + ") - the 1899 world kept as " + Path.GetFileName(text ?? "?"));
		chronicle.Era = 1907;
		Gangs.SetEra(1907);
		chronicle.EraTurnAt = GameMinutesNow();
		chronicle.EraFuneralsFixed = true;
		chronicle.WoffordFixed = true;
		chronicle.BronteBack = true;
		chronicle.BronteFull = true;
		int eraYearNow = EraYearNow;
		int month = GameMonth();
		Random rng = _rng;
		long num2 = GameMinutesNow();
		Gang[] defined = Gangs.Defined;
		foreach (Gang gang in defined)
		{
			Config.Notoriety[gang.Id] = 0;
		}
		foreach (Acquaintance person in People)
		{
			person.Grudge = 0;
			person.GrudgeSettled = false;
			person.Fear = 0;
			person.Reported = false;
			person.KeptQuiet = false;
			person.Witnessed = new List<string>();
		}
		_peopleDirty = true;
		chronicle.TownWord = new Dictionary<string, TownTalk>();
		chronicle.YourCrimes = new Dictionary<string, int>();
		foreach (NemesisRecord item in Roster)
		{
			item.KilledByPlayer = false;
		}
		chronicle.JohnRecordFixed = true;
		chronicle.MaskedWithheld = null;
		chronicle.MaskedNotoriety = null;
		chronicle.NotorietyGainDay = null;
		chronicle.UnnamedKills = null;
		chronicle.PenPals = null;
		chronicle.Past = chronicle.Past ?? new List<WorldEvent>();
		int num4 = chronicle.Events.RemoveAll((WorldEvent e) => !Old(e.GameMin));
		if (num4 > 0)
		{
			GrudgeLog($"EPILOGUE {num4} world event(s) the catch-up dated after 1905 dropped (run on the old world)");
		}
		foreach (WorldEvent @event in chronicle.Events)
		{
			DateTime dateTime = GameDate.FromMinutes(@event.GameMin);
			@event.IsPast = true;
			@event.Shift = null;
			@event.Marked = false;
			@event.AftermathUntil = 0L;
			if (dateTime.Year >= 1850 && dateTime.Year <= 1950)
			{
				@event.When = $"{GameDate.Months[dateTime.Month - 1]} {dateTime.Year}";
			}
			chronicle.Past.Add(@event);
		}
		int count = chronicle.Events.Count;
		chronicle.Events.Clear();
		chronicle.ClockShift = 0L;
		WorldEvent worldEvent = new WorldEvent
		{
			Id = NewId(10),
			Kind = "ERA",
			GameMin = num + 1,
			Day = GameDate.DayKey(GameDate.FromMinutes(num + 1)),
			When = "1899 - 1907",
			Title = "Eight years later",
			Place = "",
			Heard = true,
			IsPast = true
		};
		chronicle.Past.Add(worldEvent);
		List<NemesisRecord> list = Roster.Where(delegate(NemesisRecord n)
		{
			if (n.IsAlive && !n.Story && !n.LeaderPage && !n.Person)
			{
				Gang gang3 = Gangs.ById(n.Gang);
				if (gang3 == null)
				{
					return true;
				}
				return !gang3.StoryOnly;
			}
			return false;
		}).ToList();
		SeedGangRosters();
		Dictionary<string, int> tally = new Dictionary<string, int>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		int num5 = 0;
		foreach (NemesisRecord item2 in list)
		{
			try
			{
				string text2 = EightYearsFor(item2, eraYearNow, rng, dictionary);
				tally[text2] = (tally.TryGetValue(text2, out var value) ? value : 0) + 1;
				if (item2.Gang == null && text2 != "riding")
				{
					num5++;
				}
			}
			catch (Exception ex2)
			{
				GrudgeLog("EPILOGUE fate failed for " + item2.Name + ": " + ex2.Message);
			}
		}
		foreach (KeyValuePair<string, LeaderSeat> item3 in Seats.ToList())
		{
			LeaderSeat s = item3.Value;
			NemesisRecord nemesisRecord = ((s.Successor != null) ? Roster.FirstOrDefault((NemesisRecord x) => x.Id == s.Successor) : null);
			if (nemesisRecord != null && !nemesisRecord.IsAlive)
			{
				s.Fallen = ((nemesisRecord.Fate != null) ? nemesisRecord.Fate.ToLowerInvariant() : "died");
				s.OldName = nemesisRecord.Name;
				s.Successor = null;
				s.At = num2;
				s.FillAt = num2;
			}
		}
		ResetWorldFor1907(chronicle, num2);
		Apply1907Seats(num2, rng, transition: true);
		Apply1907Camps();
		foreach (NemesisRecord item4 in Roster.Where((NemesisRecord n) => n.IsAlive && n.Gang != null))
		{
			Gang gang2 = Gangs.ById(item4.Gang);
			if (gang2 != null && !gang2.StoryOnly)
			{
				item4.Hate = (gang2.SeedHate.HasValue ? (gang2.SeedHate.Value.min + rng.Next(Math.Max(1, gang2.SeedHate.Value.max - gang2.SeedHate.Value.min + 1))) : ((item4.Rank >= 2) ? (72 + rng.Next(10)) : SeedFootSoldierHate(gang2, item4)));
				if (item4.Rank >= 2 && item4.Personality == Personality.Coward)
				{
					item4.Hate -= 12;
				}
				item4.YourNotoriety = 0;
				item4.RestUntil = 0L;
				item4.LastCameAt = 0L;
				item4.ArcStep = 0;
				item4.LastOutcome = null;
				item4.OwesYou = false;
				item4.Informant = false;
				item4.CampKnown = false;
				item4.CampUnsure = false;
				item4.PersonalityKnown = false;
			}
		}
		EraLore(fresh: false);
		foreach (Gang item5 in Gangs.All.Where((Gang g) => !g.StoryOnly))
		{
			try
			{
				GangPast(item5, (item5.Era == 1907) ? 3 : 2, eraYearNow, month);
			}
			catch (Exception ex3)
			{
				GrudgeLog("EPILOGUE tales failed for " + item5.Name + ": " + ex3.Message);
			}
		}
		try
		{
			RankYears(eraYearNow);
		}
		catch (Exception ex4)
		{
			GrudgeLog("EPILOGUE ranks failed: " + ex4.Message);
		}
		foreach (NemesisRecord item6 in Roster.Where((NemesisRecord n) => n.IsAlive))
		{
			item6.Woven = false;
		}
		try
		{
			if (Chronicle.StoryChapter >= 7)
			{
				ApplyStoryChapter(announce: false);
			}
		}
		catch (Exception ex5)
		{
			GrudgeLog("EPILOGUE story fates failed: " + ex5.Message);
		}
		try
		{
			RunSuccession(slept: true, Gangs.All.Where((Gang g) => !g.StoryOnly).ToList());
		}
		catch (Exception ex6)
		{
			GrudgeLog("EPILOGUE shake-up failed: " + ex6.Message);
		}
		if (_successionCard != null)
		{
			_successionTitle = "EIGHT YEARS LATER";
		}
		try
		{
			AssignCamps();
		}
		catch (Exception ex7)
		{
			GrudgeLog("EPILOGUE camps failed: " + ex7.Message);
		}
		ApplyLeaders();
		int num6 = tally.Values.Sum();
		worldEvent.Lines.Add("Arthur Morgan is dead and the Van der Linde gang is gone. Eight years on, the West has new men in it - and old ones who remember.");
		worldEvent.Lines.Add(string.Format("Of the {0} men the world knew in 1899: {1} died, {2} sit in Sisika, {3} went straight, {4} went west, {5} took a badge{6} - {7} still ride.", num6, T("died"), T("jailed"), T("straight"), T("west"), T("lawman"), (T("canada") > 0) ? string.Format(", {0} went north with Rains Fall", T("canada")) : "", T("riding")));
		worldEvent.Lines.Add("The O'Driscoll Boys and the Wapiti are gone from these parts; the Bronte family still holds Saint Denis.");
		worldEvent.Lines.Add("New outfits ride now: the Laramie Gang on the plains, the Del Lobos in New Austin, and the Night Folk in the bayou.");
		foreach (KeyValuePair<string, int> item7 in dictionary.Where((KeyValuePair<string, int> kv) => kv.Value > 0))
		{
			worldEvent.Lines.Add(string.Format("{0} old hand{1} rode on with the {2}.", item7.Value, (item7.Value == 1) ? "" : "s", Gangs.ById(item7.Key)?.Name ?? item7.Key));
		}
		if (num5 > 0)
		{
			worldEvent.Lines.Add($"The {num5} who held a grudge against Arthur himself have let it go, or gone where grudges don't matter.");
		}
		SaveRoster();
		SaveWorld();
		SavePeople(force: true);
		Config.Save();
		TakeSavePoint("eight years later", sync: true, 0L);
		string text3 = string.Format("{0} men: {1} dead, {2} in Sisika, {3} straight, {4} west, {5} lawmen, {6} Canada, {7} riding; {8} events to the past", num6, T("died"), T("jailed"), T("straight"), T("west"), T("lawman"), T("canada"), T("riding"), count);
		GrudgeLog("EPILOGUE done - " + text3 + "; joined: " + string.Join(", ", dictionary.Select((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value)));
		Feed("EPILOGUE eight years later - " + text3, Theme.Gold);
		ShowNotification("enRAGE: eight years later. The O'Driscolls and the Wapiti are gone; the Laramie Gang, the Del Lobos and the Night Folk ride, and the Bronte family still holds Saint Denis. The World tab tells what became of everyone." + (debug ? (" (test: " + KeyCombo.UndoText + " within 2 minutes puts the 1899 world back)") : ""));
		if (debug && text != null)
		{
			_undoPoint = text;
			_undoSlot = _currentSlot;
			_undoUntil = Game.GameTime + 120000;
			_undoIsFresh = true;
		}
		static bool Old(long m)
		{
			if (m > 0)
			{
				return GameDate.FromMinutes(m).Year < 1906;
			}
			return false;
		}
		int T(string f)
		{
			if (!tally.TryGetValue(f, out var value2))
			{
				return 0;
			}
			return value2;
		}
	}

	private void OutfitsOf1907(string why)
	{
		WorldState c = Chronicle;
		string text = TakeSavePoint("before conversion", sync: true, 0L);
		c.StoryChapter = 7;
		c.StoryChapterAt = GameMinutesNow();
		long num = GameMinutesNow();
		List<string> retired = (from g in Gangs.Defined
			where g.Era != 0 && g.Era < 1907
			select g.Id).ToList();
		c.Era = 1907;
		Gangs.SetEra(1907);
		c.EraTurnAt = num;
		c.EraFuneralsFixed = true;
		c.WoffordFixed = true;
		c.BronteBack = true;
		c.BronteFull = true;
		int eraYearNow = EraYearNow;
		int month = GameMonth();
		GrudgeLog("EPILOGUE the outfits of 1907 (" + why + ") - a world already living in 1907: kept, only the outfits change (safety copy " + Path.GetFileName(text ?? "?") + ")");
		SeedGangRosters();
		Dictionary<string, int> tally = new Dictionary<string, int>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.IsAlive && retired.Contains(n.Gang)).ToList())
		{
			try
			{
				string key = EightYearsFor(item, eraYearNow, _rng, dictionary, now: true);
				tally[key] = (tally.TryGetValue(key, out var value) ? value : 0) + 1;
			}
			catch (Exception ex)
			{
				GrudgeLog("EPILOGUE fate failed for " + item.Name + ": " + ex.Message);
			}
		}
		foreach (string item2 in retired)
		{
			Config.Notoriety[item2] = 0;
			c.Destroyed?.Remove(item2);
		}
		foreach (WarRecord item3 in c.Wars ?? new List<WarRecord>())
		{
			if (item3.Result == null && (retired.Contains(item3.A) || retired.Contains(item3.B)))
			{
				item3.Result = "peace";
			}
		}
		Dictionary<string, int>[] array = new Dictionary<string, int>[2] { c.Relations, c.Grievance };
		foreach (Dictionary<string, int> dictionary2 in array)
		{
			if (dictionary2 == null)
			{
				continue;
			}
			foreach (string item4 in dictionary2.Keys.Where(Touches).ToList())
			{
				dictionary2.Remove(item4);
			}
		}
		c.RelationWhy?.Keys.Where(Touches).ToList().ForEach(delegate(string k)
		{
			c.RelationWhy.Remove(k);
		});
		c.Alliances?.RemoveAll(Touches);
		c.Truces?.RemoveAll(Touches);
		c.WarZones?.RemoveAll(Touches);
		c.Plans?.RemoveAll((Plan p) => retired.Contains(p.Gang));
		District[] districts = Districts;
		foreach (District district in districts)
		{
			Dictionary<string, int> dictionary3 = ControlMap[district.Key];
			foreach (string item5 in retired)
			{
				dictionary3.Remove(item5);
			}
			foreach (KeyValuePair<string, int> item6 in district.Lore)
			{
				Gang gang = Gangs.ById(item6.Key);
				if (gang != null && gang.Era == 1907 && !dictionary3.ContainsKey(item6.Key))
				{
					dictionary3[item6.Key] = item6.Value;
				}
			}
			int num3 = dictionary3.Values.Sum();
			if (num3 <= 100)
			{
				continue;
			}
			foreach (string item7 in dictionary3.Keys.ToList())
			{
				dictionary3[item7] = (int)Math.Floor((double)dictionary3[item7] * 100.0 / (double)num3);
			}
		}
		c.ControlBase = null;
		Apply1907Seats(num, _rng, transition: false);
		Apply1907Camps();
		EraLore(fresh: false);
		foreach (Gang item8 in Gangs.All.Where((Gang g) => g.Era == 1907))
		{
			try
			{
				GangPast(item8, 3, eraYearNow, month);
			}
			catch (Exception ex2)
			{
				GrudgeLog("EPILOGUE tales failed for " + item8.Name + ": " + ex2.Message);
			}
		}
		try
		{
			if (c.StoryChapter >= 7)
			{
				ApplyStoryChapter(announce: false);
			}
		}
		catch
		{
		}
		try
		{
			RunSuccession(slept: true, Gangs.All.Where((Gang g) => !g.StoryOnly && (g.Era == 1907 || (SeatOf(g)?.Fallen != null && SeatOf(g).Successor == null))).ToList());
		}
		catch (Exception ex3)
		{
			GrudgeLog("EPILOGUE shake-up failed: " + ex3.Message);
		}
		if (_successionCard != null)
		{
			_successionTitle = "THE OUTFITS OF 1907";
		}
		try
		{
			AssignCamps();
		}
		catch
		{
		}
		ApplyLeaders();
		long num4 = (from e in c.Events
			where e.GameMin > 0 && GameDate.FromMinutes(e.GameMin).Year >= 1906
			select e.GameMin).DefaultIfEmpty((from e in c.Events
			where e.GameMin > 0
			select e.GameMin).DefaultIfEmpty(num).Min()).Min();
		WorldEvent worldEvent = new WorldEvent
		{
			Id = NewId(10),
			Kind = "ERA",
			GameMin = num4 - 1,
			Day = GameDate.DayKey(GameDate.FromMinutes(num4 - 1)),
			When = "1907",
			Title = "The outfits of 1907",
			Place = "",
			Heard = true,
			IsPast = true
		};
		worldEvent.Lines.Add("The O'Driscoll Boys and the Wapiti are gone from these parts; the Bronte family still holds Saint Denis. The Laramie Gang rides the plains, the Del Lobos hold New Austin, and the Night Folk haunt the bayou.");
		int num5 = tally.Values.Sum();
		if (num5 > 0)
		{
			worldEvent.Lines.Add(string.Format("Of their {0} men: {1} died, {2} went to Sisika, {3} went straight, {4} went west, {5} took a badge{6} - {7} rode on with another outfit.", num5, T("died"), T("jailed"), T("straight"), T("west"), T("lawman"), (T("canada") > 0) ? string.Format(", {0} went north with Rains Fall", T("canada")) : "", T("riding")));
		}
		foreach (KeyValuePair<string, int> item9 in dictionary.Where((KeyValuePair<string, int> kv) => kv.Value > 0))
		{
			worldEvent.Lines.Add($"{item9.Value} of them joined the {Gangs.ById(item9.Key)?.Name ?? item9.Key}.");
		}
		c.Past = c.Past ?? new List<WorldEvent>();
		c.Past.Add(worldEvent);
		SaveRoster();
		SaveWorld();
		Config.Save();
		TakeSavePoint("eight years later", sync: true, 0L);
		string text2 = string.Format("{0} men of the retired outfits: {1} dead, {2} Sisika, {3} straight, {4} west, {5} lawmen, {6} Canada, {7} to new outfits", num5, T("died"), T("jailed"), T("straight"), T("west"), T("lawman"), T("canada"), T("riding"));
		GrudgeLog("EPILOGUE done (light) - " + text2 + "; joined: " + string.Join(", ", dictionary.Select((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value)));
		Feed("EPILOGUE the outfits of 1907 - " + text2, Theme.Gold);
		ShowNotification("enRAGE: the outfits of 1907. The O'Driscolls and the Wapiti are gone; the Laramie Gang, the Del Lobos and the Night Folk ride, and the Bronte family still holds Saint Denis. Everything you've done is kept.");
		int T(string f)
		{
			if (!tally.TryGetValue(f, out var value2))
			{
				return 0;
			}
			return value2;
		}
		bool Touches(string s)
		{
			if (s != null)
			{
				return retired.Any((string id) => s.Split('|', ':').Contains(id));
			}
			return false;
		}
	}

	private string EightYearsFor(NemesisRecord r, int year, Random rng, Dictionary<string, int> joined, bool now = false)
	{
		Gang gang = Gangs.ById(r.Gang);
		string text = gang?.Id ?? "";
		bool flag = gang != null && !Gangs.InEra(gang);
		int num = ((r.BornYear > 0) ? (year - r.BornYear) : 38);
		string[] value;
		string[] places = (FatePlaces.TryGetValue(text, out value) ? value : FatePlaces[""]);
		string[] value2;
		string[] towns = (FateTowns.TryGetValue(text, out value2) ? value2 : FateTowns[""]);
		if (text == "wapiti")
		{
			if (rng.Next(100) < 12)
			{
				return Died(r, When(), "Died on the long road north to Canada", "LIFE", null, text);
			}
			return Gone(r, "canada", "Went to Canada", now ? year : 1899, "Went north to Canada with Rains Fall and his people", null, text);
		}
		bool flag2 = gang?.OrdersNotFeelings ?? false;
		List<(string, int)> list = new List<(string, int)>
		{
			("died", 14 + ((num >= 55) ? 18 : ((num >= 45) ? 8 : 0)) + ((r.Personality == Personality.Hothead) ? 8 : 0) + ((r.Rank <= 1) ? 4 : 0) + (r.Paralyzed ? 20 : 0)),
			("jailed", (!flag2) ? 9 : 0),
			("straight", (flag2 ? 32 : 12) + ((r.Personality == Personality.Coward) ? 10 : 0) + ((r.Children > 0) ? 6 : 0) + ((num >= 45) ? 8 : 0)),
			("west", flag2 ? 5 : 8),
			("lawman", (!flag2 && !(text == "klan")) ? 2 : 0),
			("riding", (gang != null) ? (flag2 ? 42 : (flag ? 38 : 48)) : 0)
		};
		if (gang == null)
		{
			list.Add(("straight", 18));
		}
		int val = list.Sum<(string, int)>(((string f, int w) x) => x.w);
		int num2 = rng.Next(Math.Max(1, val));
		string text2 = "riding";
		foreach (var item2 in list)
		{
			if (num2 < item2.Item2)
			{
				(text2, _) = item2;
				break;
			}
			num2 -= item2.Item2;
		}
		switch (text2)
		{
		case "died":
		{
			int y = When();
			int num9 = rng.Next(100);
			if (flag2)
			{
				return Died(r, y, (num9 < 55) ? ("Killed on patrol in " + P()) : ((num9 < 85) ? "Died of a fever at his post" : ("Thrown from his horse near " + P())), (num9 < 55) ? "DUTY" : "LIFE", null, text);
			}
			if (num >= 62 && num9 < 40)
			{
				return Died(r, y, "Died of old age", "LIFE", null, text);
			}
			if (num9 < 22)
			{
				return Died(r, y, "Shot dead in a robbery outside " + P(), "CRIME", null, text);
			}
			if (num9 < 40)
			{
				return Died(r, y, "Hanged in " + Tn(), "CRIME", "the law", text);
			}
			if (num9 < 56)
			{
				return Died(r, y, "Killed by lawmen near " + P(), "CRIME", "the law", text);
			}
			if (num9 < 72)
			{
				return Died(r, y, "Killed in a quarrel in " + P(), "DUEL", null, text);
			}
			if (num9 < 88)
			{
				return Died(r, y, "Died of a fever", "LIFE", null, text);
			}
			return Died(r, y, "Drowned crossing a river near " + P(), "LIFE", null, text);
		}
		case "jailed":
		{
			string text3 = (new string[6] { "robbery", "horse theft", "murder", "cattle rustling", "assault", "robbing the mail" })[rng.Next(6)];
			int y2 = When(1901);
			return Gone(r, "jailed", "In Sisika", y2, "Sent to Sisika Penitentiary for " + text3, "CRIME", text);
		}
		case "straight":
		{
			string how2 = (flag2 ? (new string[3] { "Mustered out and went home", "Left the Army and took up farming", "Mustered out - works for the railroad now" })[rng.Next(3)] : (new string[7]
			{
				"Took up farming near " + P(),
				"Works the docks in Saint Denis",
				"Married, and keeps a store in " + Tn(),
				"Works cattle on a ranch near " + P(),
				"Went down the mine in Annesburg",
				"Found religion and put down his gun",
				"Tends bar in " + Tn()
			})[rng.Next(7)]);
			return Gone(r, "straight", flag2 ? "Mustered out" : "Went straight", When(), how2, (rng.Next(100) < 50) ? "LIFE" : null, text);
		}
		case "west":
		{
			string how = (new string[4] { "Rode west for California", "Crossed into Mexico", "Took the railroad west", "Went north to the Yukon gold" })[rng.Next(4)];
			return Gone(r, "west", "Gone west", When(), how, (rng.Next(100) < 40) ? "LIFE" : null, text);
		}
		case "lawman":
			return Gone(r, "lawman", "Wears a badge", When(1901), "Took a deputy's badge in " + Tn(), "LIFE", text);
		default:
		{
			if (gang == null)
			{
				return Gone(r, "straight", "Moved on", When(), "Let the old grudge go and moved on", null, text);
			}
			int num3 = When();
			if (!flag2 && rng.Next(100) < 15)
			{
				int num4 = 1 + rng.Next(4);
				int num5 = Math.Min(year - num4, num3);
				AddFatePast(r, num5, string.Format("Did {0} year{1} in Sisika - out in {2}", num4, (num4 == 1) ? "" : "s", num5 + num4), known: true);
			}
			if (flag)
			{
				(string, int)[] value3;
				(string, int)[] array = (RetiredHeirs.TryGetValue(text, out value3) ? value3.Where(((string to, int w) h) => Gangs.ById(h.to) != null && Gangs.InEra(Gangs.ById(h.to))).ToArray() : new(string, int)[0]);
				if (array.Length == 0)
				{
					return Gone(r, "straight", "Went straight", When(), "Quit the life when the " + gang.Name + " broke up", null, text);
				}
				int num6 = rng.Next(array.Sum(((string to, int w) h) => h.w));
				string id = array.Last().Item1;
				(string, int)[] array2 = array;
				for (int num7 = 0; num7 < array2.Length; num7++)
				{
					(string, int) tuple2 = array2[num7];
					if (num6 < tuple2.Item2)
					{
						(id, _) = tuple2;
						break;
					}
					num6 -= tuple2.Item2;
				}
				Gang gang2 = Gangs.ById(id);
				int num8 = Math.Max(1900, Math.Min(year - 1, 1900 + rng.Next(4)));
				AddFatePast(r, num8, "Rode with the " + gang2.Name + " after the " + gang.Name + " broke up", known: true);
				History(r, $"{num8}: joined the {gang2.Name} after the {gang.Name} broke up", Theme.Dim);
				r.Gang = gang2.Id;
				r.Rank = Math.Max(1, Math.Min(3, r.Rank - 1));
				(int, int, PedHash[]) tuple4 = (from s in gang2.Seed
					where s.Rank <= r.Rank
					orderby s.Rank descending
					select s).FirstOrDefault();
				PedHash[] array3 = ((tuple4.Item3 != null && tuple4.Item3.Length != 0) ? tuple4.Item3 : gang2.TroopModels);
				if (array3 != null && array3.Length != 0)
				{
					r.ModelHash = (uint)array3[rng.Next(array3.Length)];
				}
				r.OutfitHash = 0u;
				r.PlainModel = null;
				r.CampId = null;
				r.BossId = null;
				r.SeedMember = false;
				r.Habits = null;
				r.FormerGang = text;
				r.Title = r.Title ?? "";
				joined[gang2.Id] = (joined.TryGetValue(gang2.Id, out var value4) ? value4 : 0) + 1;
				return "riding";
			}
			if (r.Rank < 4 && rng.Next(100) < 35)
			{
				r.Rank++;
				string item = gang.Tiers.FirstOrDefault(((string Label, int MinRank, int Seats) t) => r.Rank >= t.MinRank).Label;
				r.Past = r.Past ?? new List<PastEntry>();
				r.Past.Add(new PastEntry
				{
					Year = num3,
					Month = 1 + rng.Next(12),
					Kind = "rank",
					Text = "Rose to " + ((item != null) ? Backstory.Singular(item).ToLowerInvariant() : ("rank " + r.Rank)) + " in the " + gang.Name,
					Public = false
				});
			}
			else
			{
				AddFatePast(r, num3, flag2 ? "Still serving" : ("Still riding with the " + gang.Name), known: false);
			}
			r.Past = (from e in r.Past
				orderby e.Year, e.Month
				select e).ToList();
			return "riding";
		}
		}
		string P()
		{
			return places[rng.Next(places.Length)];
		}
		string Tn()
		{
			return towns[rng.Next(towns.Length)];
		}
		int When(int lo = 1900)
		{
			if (!now)
			{
				return Math.Min(year - 1, Math.Max(lo, (r.BornYear > 0) ? (r.BornYear + 16) : lo) + rng.Next(Math.Max(1, year - 1 - lo)));
			}
			return year;
		}
	}

	private string Died(NemesisRecord r, int y, string how, string kind, string killer, string gid)
	{
		r.IsAlive = false;
		r.PresumedDead = false;
		r.Buried = true;
		r.Fate = null;
		r.KilledByPlayer = false;
		AddFatePast(r, y, how, known: true);
		History(r, $"DIED {y} - {how}", Theme.Blood);
		PastEvent(kind, y, 0, r.Name + ": " + Lower(how), "", true, gid).People.Add(new WorldPart
		{
			Id = r.Id,
			Name = r.Name,
			Gang = r.Gang,
			Outcome = "KILLED",
			Note = how
		});
		return "died";
	}

	private string Gone(NemesisRecord r, string key, string fate, int y, string how, string kind, string gid)
	{
		r.IsAlive = false;
		r.PresumedDead = false;
		r.Buried = true;
		r.Fate = fate;
		AddFatePast(r, y, how, known: true);
		History(r, $"{y}: {how}", Theme.Dim);
		if (kind != null)
		{
			PastEvent(kind, y, 0, r.Name + ": " + Lower(how), "", true, gid).People.Add(new WorldPart
			{
				Id = r.Id,
				Name = r.Name,
				Gang = r.Gang,
				Outcome = ((key == "jailed") ? "JAILED" : "LEFT"),
				Note = how
			});
		}
		return key;
	}

	private void AddFatePast(NemesisRecord r, int y, string text, bool known)
	{
		r.Past = r.Past ?? new List<PastEntry>();
		r.Past.Add(new PastEntry
		{
			Year = y,
			Month = 1 + _rng.Next(12),
			Kind = "fate",
			Text = text,
			Public = true,
			Known = known
		});
		r.Past = (from e in r.Past
			orderby e.Year, e.Month
			select e).ToList();
	}

	private void ResetWorldFor1907(WorldState c, long now)
	{
		foreach (WarRecord item in c.Wars ?? new List<WarRecord>())
		{
			if (item.Result == null)
			{
				item.Result = "peace";
			}
		}
		c.Grievance = null;
		c.Treasury = null;
		c.Men = null;
		c.MenRescaled = true;
		c.MenRescaled2 = true;
		c.BrokeDays = null;
		c.BrokeSaid = null;
		c.Agendas = null;
		c.WarRollDay = 0;
		c.LastWarStartAt = 0L;
		c.CampTakeovers = null;
		c.EmptyTakeovers = null;
		c.Destroyed = null;
		c.Truces = null;
		c.Alliances = null;
		c.WarZones = null;
		c.Relations = null;
		c.RelationWhy = null;
		c.Beaten = null;
		c.WarEndedAt = null;
		c.Plans = new List<Plan>();
		c.DeathNews = new List<DeathNews>();
		c.PeopleNews = new List<DeathNews>();
		c.Supplies = null;
		c.SupplyCamps = null;
		c.SupplyCampHitAt = null;
		c.SupplyCampHitSite = null;
		c.SupplyCaches = null;
		c.SupplyKnownAt = null;
		c.KnownSites = null;
		c.Treasuries = null;
		c.TreasuryKnown = null;
		c.TreasuryRobbedAt = null;
		c.TreasuryRobbedSite = null;
		c.TreasuryStash = null;
		c.Control = null;
		c.ControlBase = null;
		c.FortCleared = false;
		c.FortKnown = false;
		c.FortClearedAt = 0L;
		c.NextShakeUp = null;
		c.ShakeUpHour = null;
		c.GpsCamp = null;
		c.CompanionHurt = null;
		c.Tension = -1f;
		c.NextEventAt = now + 60;
		Config.ClearedCamps = "";
		_cleared = null;
		Config.LastSuccessionDay = 0;
		Config.NextSuccessionDay = 0;
	}

	private void Apply1907Seats(long now, Random rng, bool transition)
	{
		Fall(Gangs.ODriscolls, "hanged by the law");
		Fall(Gangs.SaintDenisMob, "fed to an alligator by Dutch van der Linde");
		Fall(Gangs.Wapiti, "went north to Canada");
		Fall(Gangs.Army, "left Fort Wallace for a post back East");
		Fall(Gangs.LemoyneRaiders, "taken in on his bounty years ago - dead or in Sisika, nobody's sure");
		if (transition)
		{
			foreach (Gang item in Gangs.All.Where((Gang g) => !g.StoryOnly && !g.LoreLeaderProtected && g.Era != 1907 && !g.OrdersNotFeelings))
			{
				if (SeatOf(item)?.Fallen == null && rng.Next(100) < 35)
				{
					Fall(item, (new string[3] { "died of a fever", "was killed in a fight", "died in his bed" })[rng.Next(3)]);
				}
			}
		}
		foreach (LeaderSeat value in Seats.Values)
		{
			if (value.Fallen != null && value.Successor == null)
			{
				value.FillAt = now;
			}
		}
		ApplyLeaders();
		void Fall(Gang g, string how)
		{
			if (g != null)
			{
				LeaderSeat s = SeatOf(g);
				if (s?.Fallen == null || (s.Successor != null && !Gangs.InEra(g)))
				{
					NemesisRecord nemesisRecord = ((s?.Successor != null) ? Roster.FirstOrDefault((NemesisRecord x) => x.Id == s.Successor) : null);
					Seats[g.Id] = new LeaderSeat
					{
						Fallen = ((nemesisRecord != null) ? "broke up" : how),
						OldName = (nemesisRecord?.Name ?? g.LoreLeader ?? g.LeaderName),
						At = now,
						FillAt = now
					};
				}
			}
		}
	}

	private void Apply1907Camps()
	{
		Chronicle.HomeStrongholdsRestored = true;
		foreach (CampSite allSite in Camps.AllSites)
		{
			if (allSite.Id == "bronte_copperhead" && SiteOwner(allSite) == "bronte")
			{
				CampOwners[allSite.Id] = "nightfolk";
				continue;
			}
			Gang gang = Gangs.ById(Camps.StartingOwner(allSite));
			if (gang != null && gang.Era == 1907)
			{
				CampOwners.Remove(allSite.Id);
				continue;
			}
			Gang gang2 = Gangs.ById(SiteOwner(allSite));
			if (gang2 == null || !Gangs.InEra(gang2))
			{
				string text = ((allSite.Id == "hanging_dog_ranch" || allSite.Id.StartsWith("gang_odriscollboys_bgv")) ? "laramie" : null);
				if (text != null)
				{
					CampOwners[allSite.Id] = text;
				}
			}
		}
	}

	private void BronteRideAgain()
	{
		WorldState chronicle = Chronicle;
		if (chronicle.BronteBack && !chronicle.BronteFull && Gangs.Era >= 1907 && Gangs.InEra(Gangs.SaintDenisMob))
		{
			chronicle.BronteFull = true;
			int num = TopUpFromSeed(Gangs.SaintDenisMob);
			if (num > 0)
			{
				try
				{
					AssignCamps();
				}
				catch
				{
				}
				SaveRoster();
			}
			SaveWorld();
			GrudgeLog($"EPILOGUE the Bronte family brought up to its full table (one-time): {num} men added");
		}
		else
		{
			if (chronicle.BronteBack || Gangs.Era < 1907)
			{
				return;
			}
			chronicle.BronteBack = true;
			Gang g = Gangs.SaintDenisMob;
			if (g == null || !Gangs.InEra(g))
			{
				return;
			}
			chronicle.Destroyed?.Remove(g.Id);
			if (!Roster.Any((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead))
			{
				SeedGangRosters(g.Id);
			}
			else
			{
				TopUpFromSeed(g);
			}
			chronicle.BronteFull = true;
			chronicle.Men?.Remove(g.Id);
			chronicle.Treasury?.Remove(g.Id);
			chronicle.Supplies?.Remove(g.Id);
			Config.Notoriety[g.Id] = 0;
			District district = Districts.FirstOrDefault((District x) => x.Key == "bayou_nwa");
			if (district != null)
			{
				Dictionary<string, int> dictionary = ControlMap[district.Key];
				dictionary[g.Id] = 36;
				int num2 = dictionary.Where((KeyValuePair<string, int> kv) => kv.Key != g.Id).Sum((KeyValuePair<string, int> kv) => kv.Value);
				if (num2 > 64)
				{
					foreach (string item in dictionary.Keys.Where((string k) => k != g.Id).ToList())
					{
						dictionary[item] = (int)Math.Floor((double)dictionary[item] * 64.0 / (double)num2);
					}
				}
				chronicle.ControlBase = null;
			}
			foreach (CampSite item2 in Camps.AllSites.Where((CampSite s) => Camps.StartingOwner(s) == g.Id))
			{
				Takeovers.Remove(item2.Id);
				if (item2.Id == "bronte_copperhead")
				{
					CampOwners[item2.Id] = "nightfolk";
				}
				else
				{
					CampOwners.Remove(item2.Id);
				}
			}
			LeaderSeat leaderSeat = SeatOf(g);
			if (leaderSeat == null || leaderSeat.Fallen == null)
			{
				Seats[g.Id] = new LeaderSeat
				{
					Fallen = "fed to an alligator by Dutch van der Linde",
					OldName = (g.LoreLeader ?? "Angelo Bronte"),
					At = ((chronicle.EraTurnAt > 0) ? chronicle.EraTurnAt : GameMinutesNow()),
					FillAt = GameMinutesNow()
				};
			}
			else
			{
				leaderSeat.Successor = null;
				leaderSeat.FillAt = GameMinutesNow();
			}
			try
			{
				AssignCamps();
			}
			catch
			{
			}
			try
			{
				RunSuccession(slept: true, new List<Gang> { g });
			}
			catch (Exception ex)
			{
				GrudgeLog("EPILOGUE Bronte shake-up failed: " + ex.Message);
			}
			try
			{
				AssignCamps();
			}
			catch
			{
			}
			ApplyLeaders();
			if (!Chronicle.Past.Any((WorldEvent e) => e.Kind == "LORE" && e.Title == "The Bronte family holds on"))
			{
				PastEvent("LORE", 1900, 2, "The Bronte family holds on", "Saint Denis", true, g.Id).Lines.Add("With Angelo gone, his lieutenants kept the family together - Saint Denis is still theirs.");
			}
			SaveRoster();
			SaveWorld();
			Config.Save();
			int num3 = Roster.Count((NemesisRecord n) => n.Gang == g.Id && n.IsAlive);
			GrudgeLog($"EPILOGUE the Bronte family rides again in 1907 (one-time): {num3} men, 36 of Bayou Nwa, their sites back (Copperhead stays the Night Folk's), a new head for Angelo's seat");
			ReplyNotice("The Bronte family still holds Saint Denis.");
		}
	}

	private void EraLore(bool fresh)
	{
		foreach (LoreRow row in EraLoreRows)
		{
			if (!string.IsNullOrEmpty(row.Title) && !Chronicle.Past.Any((WorldEvent e) => e.Kind == "LORE" && e.Title == row.Title))
			{
				PastEvent("LORE", row.Year, row.Month, row.Title, row.Place ?? "", true, row.Gang).Lines.AddRange(row.Lines ?? new List<string>());
			}
		}
		GrudgeLog("EPILOGUE the lore of 1899-1907 written" + (fresh ? " (a world made in 1907)" : ""));
	}

	private void StartWorldIn1907(string why)
	{
		WorldState chronicle = Chronicle;
		chronicle.Era = 1907;
		Gangs.SetEra(1907);
		chronicle.EraTurnAt = GameMinutesNow();
		chronicle.EraFuneralsFixed = true;
		chronicle.WoffordFixed = true;
		chronicle.BronteBack = true;
		chronicle.BronteFull = true;
		Apply1907Camps();
		SaveWorld();
		GrudgeLog("EPILOGUE a new world on an epilogue save (" + why + ") - it starts in 1907");
	}

	internal void DebugOldEpilogueWorld(Ped player)
	{
		if (CLOCK.GET_CLOCK_YEAR() < 1906)
		{
			ShowNotification("enRAGE: load an epilogue save (playing as John) first.");
			return;
		}
		string undoPoint = TakeSavePoint("before fresh", sync: true, 0L);
		_holdTurn = true;
		if (_menu.IsOpen)
		{
			_menu.Close();
		}
		WipeWorldAfterRespawn();
		ResetEverything();
		WorldState chronicle = Chronicle;
		chronicle.Era = 0;
		Gangs.SetEra(1899);
		chronicle.EraTurnAt = 0L;
		chronicle.EraFuneralsFixed = false;
		chronicle.WoffordFixed = false;
		chronicle.CampOwner = null;
		chronicle.HomeStrongholdsRestored = false;
		DespawnAftermath();
		ClearAftermathBlips();
		SeedGangRosters();
		AssignCamps();
		EnsureWorldHistory();
		chronicle.StoryChapter = 7;
		chronicle.StoryChapterAt = GameMinutesNow();
		try
		{
			ApplyStoryChapter(announce: false);
		}
		catch
		{
		}
		SaveRoster();
		SaveWorld();
		_undoPoint = undoPoint;
		_undoSlot = _currentSlot;
		_undoUntil = Game.GameTime + 120000;
		_undoIsFresh = true;
		GrudgeLog($"EPILOGUE test: an old 0.9.4 epilogue world made (the 1899 outfits on the {GameYear()} clock, history year {chronicle.HistoryYear}) - the turn held");
		ShowNotification("enRAGE: an old-style epilogue world (the 1899 outfits in 1907). Play a little, then the spawner's 'Convert it now'. " + KeyCombo.UndoText + " within 2 minutes undoes.");
	}

	internal void DebugConvertOld(Ped player)
	{
		_holdTurn = false;
		if (Chronicle.Era >= 1907)
		{
			ShowNotification("enRAGE: this world is already in 1907.");
		}
		else
		{
			EightYearsLater("the spawner (conversion test)");
		}
	}

	internal void DebugEightYears(Ped player)
	{
		if (Chronicle.Era >= 1907)
		{
			ShowNotification("enRAGE: this world is already in 1907.");
		}
		else
		{
			EightYearsLater("the spawner (test)", debug: true);
		}
	}
}
