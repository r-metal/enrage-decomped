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
	internal static void FixLore(List<LoreRow> rows)
	{
		if (rows == null)
		{
			return;
		}
		foreach (LoreRow row in rows)
		{
			if (row != null)
			{
				if (row.Title == "The Blackwater ferry job" && (row.Year != 1899 || row.Month != 2))
				{
					row.Year = 1899;
					row.Month = 2;
					row.YearsAgo = 0;
				}
				if (row.Title == "The O'Driscoll - Van der Linde feud" && row.Year != 1889)
				{
					row.Year = 1889;
					row.YearsAgo = 0;
					row.When = "About 1889";
				}
			}
		}
	}

	private void ValidateTimeline()
	{
		if (StoryBuildError != null)
		{
			GrudgeLog("STORY gang failed to build: " + StoryBuildError);
		}
		else
		{
			GrudgeLog($"STORY Van der Linde gang: {StoryRoster.Count} people");
		}
		int num = GameYear();
		int num2 = GameMonth();
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		foreach (NemesisRecord item in Roster.Concat(StoryRoster))
		{
			NemesisRecord r = item;
			if (r.Past == null || r.BornYear <= 0)
			{
				continue;
			}
			int count = r.Past.Count;
			List<PastEntry> list = r.Past.Where(Bad).ToList();
			foreach (PastEntry item2 in list)
			{
				GrudgeLog(string.Format("TIMELINE {0} (born {1}): dropped '{2}' ({3}{4}, {5})", r.Name, r.BornYear, item2.Text, item2.Year, (item2.Month > 0) ? ("/" + item2.Month) : "", item2.Kind));
			}
			r.Past.RemoveAll(list.Contains);
			if (r.Past.Count != count)
			{
				num3++;
				num4 += count - r.Past.Count;
			}
			bool Bad(PastEntry e)
			{
				if (e.Year <= 0)
				{
					return false;
				}
				if (e.Year < r.BornYear)
				{
					return true;
				}
				if (r.Story)
				{
					return false;
				}
				if (!Grown(e.Kind) || e.Year >= r.BornYear + 14)
				{
					if (e.Kind == "war")
					{
						return e.Year < r.BornYear + 15;
					}
					return false;
				}
				return true;
			}
		}
		foreach (WorldEvent ev in Chronicle.Past ?? new List<WorldEvent>())
		{
			if (ev.Title == "The Blackwater ferry job" && ev.Day / 10000 != 1899)
			{
				DateTime d = new DateTime(1899, 2, 10, 22, 0, 0);
				ev.GameMin = GameDate.ToMinutes(d);
				ev.Day = 18990110;
				ev.When = "Feb 1899";
				num6++;
			}
			int num7 = ev.Day / 10000;
			foreach (WorldPart p in ev.People.ToList())
			{
				NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Id);
				if (nemesisRecord != null && nemesisRecord.BornYear > 0 && num7 < nemesisRecord.BornYear + 14)
				{
					ev.People.Remove(p);
					nemesisRecord.Past?.RemoveAll((PastEntry e) => e.WorldId == ev.Id);
					num5++;
					GrudgeLog($"TIMELINE {nemesisRecord.Name} (born {nemesisRecord.BornYear}) was too young for '{ev.Title}' ({num7}) - taken out of it");
				}
			}
		}
		if (num3 + num5 + num6 > 0)
		{
			SaveRoster();
			SaveWorld();
		}
		GrudgeLog($"TIMELINE checked {Roster.Count + StoryRoster.Count} lives and {Chronicle.Past?.Count ?? 0} past events for {num}/{num2}: " + $"{num4} lines dropped from {num3} lives, {num5} men taken out of events too early for them, {num6} lore dates fixed");
		static bool Grown(string kind)
		{
			switch (kind)
			{
			default:
				return kind == "weave";
			case "gang":
			case "rank":
			case "crime":
			case "work":
			case "war":
			case "world":
				return true;
			}
		}
	}

	private void HealFutureTimeline(long now)
	{
		List<long> list = new List<long>();
		foreach (NemesisRecord item in Roster)
		{
			list.Add(Math.Max(item.LastGameMin, item.LastCameAt));
		}
		foreach (Acquaintance person in People)
		{
			list.Add(person.LastGameMin);
		}
		foreach (WorldEvent @event in Chronicle.Events)
		{
			list.Add(@event.GameMin);
		}
		list = (from x in list
			where x > 0
			orderby x descending
			select x).ToList();
		if (list.Count != 0)
		{
			long num = list[Math.Min(9, list.Count - 1)];
			if (num - now >= 1440)
			{
				GrudgeLog($"TIMESHIFT the world is ahead of your clock: newest {GameDate.FromMinutes(num):MMM d HH:mm}, clock {GameDate.FromMinutes(now):MMM d HH:mm}");
				ShiftTimeline(now - num, "the world was ahead of this save's clock");
				ShowNotification("enRAGE: the world was ahead of this save's clock - its timers moved back to fit.");
			}
		}
	}

	private void ShiftTimeline(long delta, string why)
	{
		if (delta == 0L)
		{
			return;
		}
		int n = 0;
		foreach (NemesisRecord item in Roster)
		{
			item.RestUntil = S(item.RestUntil);
			item.LastCameAt = S(item.LastCameAt);
			item.LastGameMin = S(item.LastGameMin);
			item.SocialDoneAt = S(item.SocialDoneAt);
			item.BargainAt = S(item.BargainAt);
			item.InformedDay = D(item.InformedDay);
			if (item.Thoughts != null)
			{
				foreach (Thought thought in item.Thoughts)
				{
					thought.At = S(thought.At);
					thought.Until = S(thought.Until);
				}
			}
			if (item.Memories != null)
			{
				foreach (Memory memory in item.Memories)
				{
					memory.At = S(memory.At);
				}
			}
			if (item.Goals == null)
			{
				continue;
			}
			foreach (Goal goal in item.Goals)
			{
				goal.Since = S(goal.Since);
			}
		}
		WorldState chronicle = Chronicle;
		chronicle.NextEventAt = S(chronicle.NextEventAt);
		chronicle.StoryChapterAt = S(chronicle.StoryChapterAt);
		chronicle.FortClearedAt = S(chronicle.FortClearedAt);
		chronicle.LastFearFadeDay = D(chronicle.LastFearFadeDay);
		foreach (WorldEvent @event in chronicle.Events)
		{
			@event.GameMin = S(@event.GameMin);
			@event.AftermathUntil = S(@event.AftermathUntil);
			@event.Day = D(@event.Day);
		}
		foreach (Plan item2 in chronicle.Plans ?? new List<Plan>())
		{
			item2.DueAt = S(item2.DueAt);
		}
		foreach (DeathNews item3 in (chronicle.DeathNews ?? new List<DeathNews>()).Concat(chronicle.PeopleNews ?? new List<DeathNews>()))
		{
			item3.At = S(item3.At);
			item3.DueAt = S(item3.DueAt);
		}
		foreach (Telegram item4 in chronicle.Telegrams ?? new List<Telegram>())
		{
			item4.SentAt = S(item4.SentAt);
			item4.ArriveAt = S(item4.ArriveAt);
		}
		foreach (PenPal value in (chronicle.PenPals ?? new Dictionary<string, PenPal>()).Values)
		{
			value.LastAt = S(value.LastAt);
		}
		if (chronicle.CompanionHurt != null)
		{
			foreach (string item5 in chronicle.CompanionHurt.Keys.ToList())
			{
				chronicle.CompanionHurt[item5] = S(chronicle.CompanionHurt[item5]);
			}
		}
		if (chronicle.Leaders != null)
		{
			foreach (LeaderSeat value2 in chronicle.Leaders.Values)
			{
				if (value2 != null)
				{
					value2.At = S(value2.At);
					value2.FillAt = S(value2.FillAt);
				}
			}
		}
		if (chronicle.NotorietyGainDay != null)
		{
			foreach (string item6 in chronicle.NotorietyGainDay.Keys.ToList())
			{
				chronicle.NotorietyGainDay[item6] = D(chronicle.NotorietyGainDay[item6]);
			}
		}
		if (chronicle.TownWord != null)
		{
			foreach (TownTalk value3 in chronicle.TownWord.Values)
			{
				if (value3 != null)
				{
					value3.LastWordDay = D(value3.LastWordDay);
				}
			}
		}
		if (chronicle.Destroyed != null)
		{
			foreach (string item7 in chronicle.Destroyed.Keys.ToList())
			{
				chronicle.Destroyed[item7] = S(chronicle.Destroyed[item7]);
			}
		}
		if (chronicle.WarEndedAt != null)
		{
			foreach (string item8 in chronicle.WarEndedAt.Keys.ToList())
			{
				chronicle.WarEndedAt[item8] = S(chronicle.WarEndedAt[item8]);
			}
		}
		if (chronicle.Agendas != null)
		{
			foreach (Agenda value4 in chronicle.Agendas.Values)
			{
				value4.SetAt = S(value4.SetAt);
				value4.ReviewAt = S(value4.ReviewAt);
			}
		}
		if (chronicle.Beaten != null)
		{
			foreach (string item9 in chronicle.Beaten.Keys.ToList())
			{
				chronicle.Beaten[item9] = S(chronicle.Beaten[item9]);
			}
		}
		if (chronicle.CampTakeovers != null)
		{
			foreach (CampTakeover value5 in chronicle.CampTakeovers.Values)
			{
				value5.DueAt = S(value5.DueAt);
			}
		}
		chronicle.WildFreshAt = S(chronicle.WildFreshAt);
		chronicle.LastWarStartAt = S(chronicle.LastWarStartAt);
		if (chronicle.SleepRollWindow >= 0)
		{
			chronicle.SleepRollWindow = S(chronicle.SleepRollWindow * 720) / 720;
		}
		if (chronicle.Wars != null)
		{
			foreach (WarRecord war in chronicle.Wars)
			{
				war.StartMin = S(war.StartMin);
				war.LastReportDay = D(war.LastReportDay);
				war.LastBattleAt = S(war.LastBattleAt);
				war.BattleDay = D(war.BattleDay);
			}
		}
		if (chronicle.PastWars != null)
		{
			foreach (WarRecord pastWar in chronicle.PastWars)
			{
				pastWar.StartMin = S(pastWar.StartMin);
				pastWar.EndMin = S(pastWar.EndMin);
			}
		}
		chronicle.Truces = ShiftLast(chronicle.Truces);
		chronicle.Alliances = ShiftLast(chronicle.Alliances);
		foreach (Acquaintance person in People)
		{
			person.LastGameMin = S(person.LastGameMin);
			person.LastScaredMin = S(person.LastScaredMin);
		}
		foreach (string item10 in _cardUsedAt.Keys.ToList())
		{
			_cardUsedAt[item10] = S(_cardUsedAt[item10]);
		}
		try
		{
			n += _bulletImpact.ShiftGameMinutes(delta);
		}
		catch
		{
		}
		foreach (Print value6 in Prints.Values)
		{
			value6.LastGameMin = S(value6.LastGameMin);
		}
		Config.LastSuccessionDay = D(Config.LastSuccessionDay);
		Config.NextSuccessionDay = D(Config.NextSuccessionDay);
		if (Chronicle.NextShakeUp != null)
		{
			foreach (string item11 in Chronicle.NextShakeUp.Keys.ToList())
			{
				Chronicle.NextShakeUp[item11] = D(Chronicle.NextShakeUp[item11]);
			}
		}
		Config.LastDecayDay = D(Config.LastDecayDay);
		if (!string.IsNullOrEmpty(Config.ClearedCamps))
		{
			Config.ClearedCamps = string.Join(",", Config.ClearedCamps.Split(',').Select(delegate(string e)
			{
				int num = e.LastIndexOf(':');
				int result;
				return (num <= 0 || !int.TryParse(e.Substring(num + 1), out result)) ? e : (e.Substring(0, num + 1) + D(result));
			}));
		}
		_cleared = null;
		_lastMurderousAt = S(_lastMurderousAt);
		_lastMindHour = -1L;
		_lastMindDay = -1L;
		_nextUnaskedAt = 0L;
		try
		{
			Config.Save();
		}
		catch
		{
		}
		SaveRoster();
		SaveWorld();
		_peopleDirty = true;
		_printsDirty = true;
		GrudgeLog($"TIMESHIFT {n} timers moved {(double)delta / 60.0:+0.#;-0.#} h ({why}) - rest, cooldowns, news, telegrams, plans, world events, shake-up dates");
		int D(int key)
		{
			if (key <= 0)
			{
				return key;
			}
			try
			{
				n++;
				return GameDate.DayKey(GameDate.FromDayKey(key).AddMinutes(delta));
			}
			catch
			{
				return key;
			}
		}
		long S(long t)
		{
			if (t <= 0)
			{
				return t;
			}
			n++;
			return t + delta;
		}
		List<string> ShiftLast(List<string> list)
		{
			return list?.Select(delegate(string e)
			{
				int num = e.LastIndexOf('|');
				long result;
				return (num <= 0 || !long.TryParse(e.Substring(num + 1), out result)) ? e : (e.Substring(0, num + 1) + S(result));
			}).ToList();
		}
	}
}
