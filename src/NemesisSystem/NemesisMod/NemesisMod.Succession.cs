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
	private long _lastSleepAt = -100000L;

	private List<(string text, (int r, int g, int b) c)> _successionCard;

	private string _successionTitle;

	private int _successionShownMs = -1;

	private int _nextShakeUpRun;

	private int _nextHollowCheck;

	private readonly Dictionary<string, double> _supplyFrac = new Dictionary<string, double>();

	private readonly HashSet<string> _unguardedCaches = new HashSet<string>();

	private void UpdateSuccession()
	{
		if (!Config.Succession || Game.GameTime < _nextSuccessionCheck)
		{
			return;
		}
		_nextSuccessionCheck = Game.GameTime + 2000;
		long num = GameMinutesNow();
		if (_lastClockStamp >= 0 && num - _lastClockStamp > 120 && Game.GameTime - _lastJumpAt > 30000)
		{
			_lastSleepAt = num;
		}
		_lastClockStamp = num;
		int num2 = GameDayKey();
		if (Config.LastDecayDay != 0 && num2 != Config.LastDecayDay && DayOf(num2) < DayOf(Config.LastDecayDay))
		{
			Config.LastDecayDay = num2;
			Config.Save();
		}
		else if (Config.LastDecayDay != num2 && CLOCK.GET_CLOCK_HOURS() >= Config.SuccessionHour)
		{
			bool flag = Config.LastDecayDay == 0;
			int num3 = ((!flag) ? Math.Max(1, Math.Min(30, (int)(DayOf(num2) - DayOf(Config.LastDecayDay)).TotalDays)) : 0);
			Config.LastDecayDayBefore = Config.LastDecayDay;
			Config.LastDecayDay = num2;
			if (!flag)
			{
				try
				{
					DriftControl(num3);
				}
				catch (Exception ex)
				{
					GrudgeLog("CONTROL drift failed: " + ex.Message);
				}
			}
			if (!flag && Balance.NotorietyDecayPerDay > 0)
			{
				Gang[] all = Gangs.All;
				foreach (Gang gang in all)
				{
					int num4 = NotorietyOf(gang);
					if (num4 > 0)
					{
						int num5 = num3;
						if (Chronicle.NotorietyGainDay != null && Chronicle.NotorietyGainDay.TryGetValue(gang.Id, out var value) && Config.LastDecayDayBefore != 0 && DayOf(value) >= DayOf(Config.LastDecayDayBefore))
						{
							num5 = Math.Max(0, num3 - 1);
						}
						int num6 = num3 - num5;
						int num7 = Balance.NotorietyDecayPerDay * num5 + ((num6 > 0) ? (Math.Max(1, Balance.NotorietyDecayPerDay / 2) * num6) : 0);
						if (num7 <= 0)
						{
							GrudgeLog($"NOTORIETY {gang.Id} {num4} holds");
							continue;
						}
						Config.Notoriety[gang.Id] = Math.Max(0, num4 - num7);
						GrudgeLog($"NOTORIETY {gang.Id} {num4} -> {Config.Notoriety[gang.Id]} (a day passes)");
					}
				}
			}
			Config.Save();
		}
		Dictionary<string, int> dictionary = Chronicle.NextShakeUp ?? (Chronicle.NextShakeUp = new Dictionary<string, int>());
		Dictionary<string, int> dictionary3 = Chronicle.ShakeUpHour ?? (Chronicle.ShakeUpHour = new Dictionary<string, int>());
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		List<(Gang, DateTime)> list = new List<(Gang, DateTime)>();
		foreach (Gang item2 in Gangs.All.Where((Gang gang2) => gang2.Seed != null && gang2.Tiers != null))
		{
			if (!dictionary.TryGetValue(item2.Id, out var value2) || value2 == 0)
			{
				ScheduleShakeUp(item2, num2, first: true);
				continue;
			}
			try
			{
				if ((DayOf(value2) - DayOf(num2)).TotalDays > (double)(Config.SuccessionMaxDays + 1))
				{
					ScheduleShakeUp(item2, num2, first: true);
					continue;
				}
				if (!dictionary3.ContainsKey(item2.Id))
				{
					dictionary3[item2.Id] = _rng.Next(24);
					SaveWorld();
				}
				int num8 = dictionary3[item2.Id];
				if (DayOf(num2) > DayOf(value2) || (DayOf(num2) == DayOf(value2) && cLOCK_HOURS >= num8))
				{
					list.Add((item2, DayOf(value2).AddHours(num8)));
				}
			}
			catch
			{
				ScheduleShakeUp(item2, num2, first: true);
			}
		}
		if (Game.GameTime >= _nextHollowCheck)
		{
			_nextHollowCheck = Game.GameTime + 30000;
			Gang[] all = Gangs.All;
			foreach (Gang g in all)
			{
				PullShakeUpIn(g, num2);
			}
		}
		Config.NextSuccessionDay = ((dictionary.Count > 0) ? dictionary.Values.OrderBy(delegate(int v)
		{
			try
			{
				return DayOf(v);
			}
			catch
			{
				return DateTime.MaxValue;
			}
		}).First() : 0);
		if (list.Count != 0 && Game.GameTime >= _nextShakeUpRun && !_menu.IsOpen && !_bulletImpact.ViewerOpen && !(_activeNemesisPed != null) && PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle))
		{
			Gang item = list.OrderBy<(Gang, DateTime), DateTime>(((Gang g, DateTime when) x) => x.when).First().Item1;
			Config.LastSuccessionDay = num2;
			ScheduleShakeUp(item, num2, first: false);
			_nextShakeUpRun = Game.GameTime + ((list.Count > 2) ? (15000 + _rng.Next(15000)) : (60000 + _rng.Next(120000)));
			GrudgeLog("SUCCESSION shake-up now for the " + item.Name + ((list.Count > 1) ? $" ({list.Count - 1} more due - one at a time)" : ""));
			RunSuccession(GameMinutesNow() - _lastSleepAt < 720, new List<Gang> { item });
			PullShakeUpIn(item, num2);
		}
	}

	private void ScheduleShakeUp(Gang g, int fromDay, bool first)
	{
		Dictionary<string, int> dictionary = Chronicle.NextShakeUp ?? (Chronicle.NextShakeUp = new Dictionary<string, int>());
		Dictionary<string, int> dictionary3 = Chronicle.ShakeUpHour ?? (Chronicle.ShakeUpHour = new Dictionary<string, int>());
		int num = 0;
		int key = fromDay;
		for (int i = 0; i < 4; i++)
		{
			num = (first ? _rng.Next(1, Config.SuccessionMaxDays + 1) : _rng.Next(Config.SuccessionMinDays, Config.SuccessionMaxDays + 1));
			try
			{
				key = GameDate.DayKey(DayOf(fromDay).AddDays(num));
			}
			catch
			{
				key = fromDay;
				break;
			}
			if (!dictionary.Any((KeyValuePair<string, int> kv) => kv.Key != g.Id && kv.Value == key))
			{
				break;
			}
		}
		dictionary[g.Id] = key;
		dictionary3[g.Id] = _rng.Next(24);
		SaveWorld();
		GrudgeLog($"SUCCESSION {g.Id}: next shake-up in {num} days ({dictionary[g.Id]}) at {dictionary3[g.Id]:00}:00");
	}

	internal double UpperShare(Gang g)
	{
		if (g?.Tiers == null || g.Tiers.Length < 2)
		{
			return 1.0;
		}
		int num = 0;
		for (int i = 0; i < g.Tiers.Length - 1; i++)
		{
			num += g.Tiers[i].Seats;
		}
		if (num <= 0)
		{
			return 1.0;
		}
		int lowest = g.Tiers[g.Tiers.Length - 2].MinRank;
		string succ = SuccessorId(g);
		int num2 = Roster.Count((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n.Id != succ && n.Rank >= lowest);
		return Math.Min(1.0, (double)num2 / (double)num);
	}

	private bool ReadyToRise(NemesisRecord n)
	{
		if (n.RankXp < 2 * n.Rank)
		{
			return Standing(n).score >= 70;
		}
		return true;
	}

	private void PullShakeUpIn(Gang g, int today)
	{
		if (g == null || g.OrdersNotFeelings || g.Tiers == null || g.Seed == null || IsDestroyed(g))
		{
			return;
		}
		Dictionary<string, int> nextShakeUp = Chronicle.NextShakeUp;
		if (nextShakeUp == null || !nextShakeUp.TryGetValue(g.Id, out var value) || value == 0)
		{
			return;
		}
		double num = UpperShare(g);
		int num2 = ((num < 0.5) ? 1 : ((num < 0.75) ? 2 : int.MaxValue));
		if (num2 == int.MaxValue)
		{
			return;
		}
		try
		{
			DateTime dateTime = DayOf(today).AddDays(num2);
			if (!(DayOf(value) <= dateTime))
			{
				nextShakeUp[g.Id] = GameDate.DayKey(dateTime);
				SaveWorld();
				GrudgeLog(string.Format("SUCCESSION {0}: officer seats {1:0}% filled - the next shake-up comes sooner, within {2} day{3} ({4})", g.Id, num * 100.0, num2, (num2 == 1) ? "" : "s", nextShakeUp[g.Id]));
			}
		}
		catch
		{
		}
	}

	internal int ShakeUpDayOf(Gang g)
	{
		if (g == null || Chronicle.NextShakeUp == null || !Chronicle.NextShakeUp.TryGetValue(g.Id, out var value))
		{
			return Config.NextSuccessionDay;
		}
		return value;
	}

	private void ScheduleNextSuccession(int fromDay)
	{
		int num = _rng.Next(Config.SuccessionMinDays, Config.SuccessionMaxDays + 1);
		try
		{
			DateTime d = DayOf(fromDay).AddDays(num);
			Config.NextSuccessionDay = GameDate.DayKey(d);
		}
		catch
		{
			Config.NextSuccessionDay = fromDay;
		}
		Config.Save();
		GrudgeLog($"SUCCESSION next shake-up in {num} days ({Config.NextSuccessionDay})");
	}

	internal (int score, string why) Standing(NemesisRecord n)
	{
		int num = n.Hate + 30 * n.TimesKilledPlayer + 20 * n.TimesCheatedDeath + 12 * n.Scars.Count + 8 * n.TimesAmbushed + 6 * n.Rank + ((n.Origin != GrudgeOrigin.GangMember) ? 10 : 0) + n.Aggression / 10 - EffectiveFear(n) / 4 + Mind.StandingBonus(n).bonus;
		switch (n.Personality)
		{
		case Personality.Proud:
			num += 15;
			break;
		case Personality.Hothead:
			num += 10;
			break;
		case Personality.Loyal:
			num += 8;
			break;
		case Personality.Cold:
			num += 5;
			break;
		case Personality.Coward:
			num -= 20;
			break;
		}
		string item = ((n.TimesKilledPlayer > 0) ? ("killed you " + ((n.TimesKilledPlayer == 1) ? "once" : (n.TimesKilledPlayer + "x"))) : ((n.TimesCheatedDeath > 0) ? "came back from the dead" : (n.ScarredByYou ? "carries your scar" : ((n.Scars.Count > 0) ? "a scarred veteran" : ((n.TimesAmbushed > 0) ? $"has come for you {n.TimesAmbushed}x" : ((n.Origin != GrudgeOrigin.GangMember) ? "has a score to settle" : ((n.Personality == Personality.Proud || n.Personality == Personality.Hothead) ? "ambitious" : "next in line")))))));
		return (score: num, why: item);
	}

	private void RunSuccession(bool slept, List<Gang> due)
	{
		try
		{
			TrimGangRosters();
		}
		catch (Exception ex)
		{
			GrudgeLog("ERROR shake-up roster trim: " + ex);
		}
		if (due.Any((Gang gang2) => gang2.Id == Gangs.LemoyneRaiders.Id))
		{
			RaiderInfighting();
		}
		List<(string, (int, int, int))> list = new List<(string, (int, int, int))>();
		int num = 0;
		foreach (Gang item4 in due)
		{
			Gang gang = item4;
			string succ = SuccessorId(gang);
			List<NemesisRecord> living = Roster.Where((NemesisRecord n) => n.Gang == gang.Id && n.IsAlive && !n.PresumedDead && n.Id != succ).ToList();
			if (living.Count == 0 && !Roster.Any((NemesisRecord n) => n.Gang == gang.Id))
			{
				continue;
			}
			List<(string, (int, int, int))> list2 = new List<(string, (int, int, int))>();
			(string, NemesisRecord)? tuple = FillLeaderSeat(gang, living);
			if (tuple.HasValue)
			{
				living.Remove(tuple.Value.Item2);
				list2.Add((tuple.Value.Item1, Theme.Gold));
				num++;
			}
			List<(NemesisRecord rec, string label)> list3 = new List<(NemesisRecord rec, string label)>();
			List<WorldPart> list4 = new List<WorldPart>();
			int num2 = HireBudget(gang);
			int num3 = 0;
			int num4 = 0;
			if (HasPurse(gang) && !gang.OrdersNotFeelings)
			{
				GrudgeLog($"SUCCESSION {gang.Id}: purse ${PurseOf(gang)} - ${num2} to spend on new men");
			}
			double num5 = UpperShare(gang);
			int num6 = 0;
			for (int num7 = 0; num7 < gang.Tiers.Length; num7++)
			{
				(string Label, int MinRank, int Seats) tuple2 = gang.Tiers[num7];
				int item = tuple2.MinRank;
				int item2 = tuple2.Seats;
				int count = In(num7).Count;
				int num8 = item2 - count;
				bool flag = !gang.OrdersNotFeelings && num7 < gang.Tiers.Length - 1;
				int num9 = num8;
				if (flag && num8 > 0)
				{
					num9 = Math.Max(0, (int)Math.Round((double)item2 * (0.7 + _rng.NextDouble() * 0.2)) - count);
				}
				for (int num10 = 0; num10 < num8; num10++)
				{
					NemesisRecord up = null;
					if (num7 + 1 < gang.Tiers.Length)
					{
						up = (from n in In(num7 + 1)
							where !n.Paralyzed
							orderby Standing(n).score descending, n.Id
							select n).FirstOrDefault();
					}
					if (flag && num10 >= num9 && (up == null || !ReadyToRise(up)))
					{
						int num11 = num8 - num10;
						GrudgeLog(string.Format("SUCCESSION {0} {1} {2} seat(s) left open - nobody ready{3}", gang.Id, num11, Seat(num7), (up != null) ? $" (best: {up.Name}, xp {up.RankXp}/{2 * up.Rank}, standing {Standing(up).score})" : ""));
						list2.Add((string.Format("{0} {1} seat{2} left open - nobody ready yet", num11, (num11 == 1) ? Seat(num7) : gang.Tiers[num7].Label.ToLowerInvariant(), (num11 == 1) ? "" : "s"), Theme.Dim));
						break;
					}
					if (up != null && item >= 2)
					{
						num6++;
					}
					if (up != null)
					{
						string item3 = Standing(up).why;
						int rank = up.Rank;
						up.Rank = item;
						up.BossId = null;
						if (item >= 2)
						{
							int hate = up.Hate;
							up.Hate = Math.Min(100, up.Hate + (int)Math.Round(10f * Tempers.Of(up.Personality).HateGain));
							int num12 = (gang.SeedHate.HasValue ? gang.SeedHate.Value.max : Hate.VengefulAt);
							if (up.Hate < num12)
							{
								up.Hate = num12 + _rng.Next(4);
							}
							if (up.Hate != hate)
							{
								GrudgeLog($"SUCCESSION {up.Name}: hate {hate} -> {up.Hate} with the promotion ({up.Personality})");
							}
						}
						if (up == ActiveRecord && _activeNemesisPed != null)
						{
							ApplyRankStats(_activeNemesisPed, up, fullHeal: false);
						}
						list2.Add((up.Name + " rises to " + Seat(num7) + "  -  " + item3, Theme.Text));
						WorldPart worldPart = PartFor(up, "PROMOTED", item3);
						worldPart.Effects.Add($"rank {rank} -> {up.Rank}");
						worldPart.Effects.Add(Seat(num7));
						list4.Add(worldPart);
						History(up, "ROSE TO " + Seat(num7).ToUpperInvariant() + " in the " + gang.Name + " (" + item3 + ")", gang.Accent);
						Remember(up, "Pride", Mind.Has(up, "Rise") ? 70 : 45, "made " + Seat(num7), null);
						if (Mind.Has(up, "Rise") && num7 == 0)
						{
							up.Goals.RemoveAll((Goal goal) => goal.Type == "Rise");
						}
						foreach (NemesisRecord item5 in (from o in In(num7 + 1)
							where o != up && o.IsAlive && Mind.Has(o, "Rise")
							select o).Take(2))
						{
							Mind.SetTie(item5, up.Id, "rival", Math.Min(Mind.TieTo(item5, up.Id)?.Strength ?? 0, 0) - 35, "made " + Seat(num7) + " over him");
							Remember(item5, "Shame", 40, "passed over - " + up.Name + " made " + Seat(num7), up.Id);
						}
						GrudgeLog($"SUCCESSION {gang.Id} {up.Name} rank {rank} -> {up.Rank} ({Seat(num7)}) score={Standing(up).score} {item3}");
						num++;
					}
					else
					{
						if (IsDestroyed(gang))
						{
							continue;
						}
						if (_rng.NextDouble() > RecruitChance(gang))
						{
							GrudgeLog($"SUCCESSION {gang.Id} {Seat(num7)} seat stays empty - recruits {RecruitChance(gang) * 100.0:0}% (ground {TerritoryShare(gang) * 100.0:0}%)");
							continue;
						}
						bool num13 = num7 == gang.Tiers.Length - 1;
						int num14 = RecruitCost((!num13) ? 1 : 0);
						bool flag2 = HasPurse(gang) && (PurseOf(gang) < num14 || num3 + num14 > num2);
						if (num13 && !gang.OrdersNotFeelings && (flag2 || MenShare(gang) < 0.75))
						{
							NemesisRecord nemesisRecord = LocalRecruit(gang, num7);
							if (nemesisRecord != null)
							{
								int num15 = (HasPurse(gang) ? Math.Min(10, Math.Max(0, PurseOf(gang) - 60)) : 0);
								if (num15 > 0)
								{
									AddPurse(gang, -num15, "a local man as " + Seat(num7));
									num3 += num15;
								}
								living.Add(nemesisRecord);
								WorldPart worldPart2 = PartFor(nemesisRecord, "JOINED", "a local man");
								worldPart2.Effects.Add(Seat(num7));
								list4.Add(worldPart2);
								list2.Add((nemesisRecord.Name + " joins as " + Seat(num7) + "  -  a local man", Theme.Text));
								GrudgeLog(string.Format("SUCCESSION {0} local {1} takes the {2} seat (${3}; {4})", gang.Id, nemesisRecord.Name, Seat(num7), num15, flag2 ? "no money for a new man" : $"men {MenOf(gang)} of {MenStart(gang)}"));
								num++;
								continue;
							}
						}
						if (HasPurse(gang))
						{
							int num16 = num14;
							if (PurseOf(gang) < num16 || num3 + num16 > num2)
							{
								num4++;
								if (num4 == 1)
								{
									GrudgeLog($"SUCCESSION {gang.Id} {Seat(num7)} seat stays empty - can't pay a new man (spent ${num3} of ${num2}, purse ${PurseOf(gang)}) - and any more seats this shake-up");
								}
								continue;
							}
							num3 += num16;
							AddPurse(gang, -num16, "a new man as " + Seat(num7));
						}
						var (nemesisRecord2, text) = Recruit(gang, num7);
						living.Add(nemesisRecord2);
						list3.Add((nemesisRecord2, Seat(num7)));
						WorldPart worldPart3 = PartFor(nemesisRecord2, "JOINED", text ?? "a new man");
						worldPart3.Effects.Add(Seat(num7));
						list4.Add(worldPart3);
						if (text != null)
						{
							list2.Add((nemesisRecord2.Name + " joins as " + Seat(num7) + "  -  " + text, Theme.Warn));
						}
						GrudgeLog(string.Format("SUCCESSION {0} recruit {1} as {2} rank {3} hate {4}{5}", gang.Id, nemesisRecord2.Name, Seat(num7), nemesisRecord2.Rank, nemesisRecord2.Hate, (text != null) ? (" - " + text) : ""));
						num++;
					}
				}
			}
			num += PromoteWithinTiers(gang, living, list2, list4);
			if (list3.Count<(NemesisRecord, string)>(((NemesisRecord rec, string label) r) => r.rec.KinId == null) > 0)
			{
				IEnumerable<string> values = from r in list3
					where r.rec.KinId == null
					group r by r.label into grouping
					select $"{grouping.Count()} new {((grouping.Count() == 1) ? grouping.Key : gang.Tiers.First(((string Label, int MinRank, int Seats) x) => Singular(x.Label) == grouping.Key).Label.ToLowerInvariant())}";
				list2.Add((string.Join(", ", values) + " brought in", Theme.Dim));
			}
			if (num4 > 0)
			{
				list2.Add((string.Format("No money for more men - {0} seat{1} left empty", num4, (num4 == 1) ? "" : "s"), Theme.Warn));
			}
			if (list2.Count != 0)
			{
				ChronicleRanks(gang, list4, (num5 < 0.5 && num6 >= 2) ? $"The {gang.Name} rebuild their ranks - {num6} new officers" : null);
				list.Add((gang.Name.ToUpperInvariant(), gang.Accent));
				list.AddRange(list2);
				Feed($"SUCCESSION {gang.Name}: {list2.Count} change(s)", gang.Accent);
			}
			List<NemesisRecord> In(int t)
			{
				return living.Where((NemesisRecord n) => n.Rank >= gang.Tiers[t].MinRank && n.Rank <= MaxOf(t)).ToList();
			}
			int MaxOf(int t)
			{
				if (t != 0)
				{
					return gang.Tiers[t - 1].MinRank - 1;
				}
				return 5;
			}
			string Seat(int t)
			{
				return Singular(gang.Tiers[t].Label);
			}
		}
		Gang[] all = Gangs.All;
		foreach (Gang g in all)
		{
			try
			{
				CheckCollapse(g);
			}
			catch (Exception ex2)
			{
				GrudgeLog("SANDBOX collapse check failed: " + ex2.Message);
			}
		}
		if (num != 0)
		{
			SaveRoster();
			AssignCamps();
			_successionTitle = (slept ? "WHILE YOU SLEPT" : "WORD ON THE TRAIL");
			_successionCard = list;
			_successionShownMs = -1;
		}
	}

	private NemesisRecord LocalRecruit(Gang gang, int tier)
	{
		List<NemesisRecord> source = Roster.Where((NemesisRecord n) => n.Gang == null && n.IsAlive && !n.PresumedDead && !n.Story && n.Id != null && n != ActiveRecord && !_worldNemeses.ContainsValue(n) && !IsArmy(n) && n.Affiliation != "army").ToList();
		NemesisRecord nemesisRecord = (from _ in source
			where _.Affiliation == gang.Id
			orderby _rng.Next()
			select _).FirstOrDefault() ?? (from n in source.Where(OnTheirGround)
			orderby n.Hate descending
			select n).ThenBy((NemesisRecord _) => _rng.Next()).FirstOrDefault();
		if (nemesisRecord == null)
		{
			return null;
		}
		nemesisRecord.Gang = gang.Id;
		nemesisRecord.Rank = gang.Tiers[tier].MinRank;
		nemesisRecord.BossId = null;
		nemesisRecord.CampId = null;
		nemesisRecord.CampKnown = false;
		if (nemesisRecord.Affiliation == gang.Id)
		{
			nemesisRecord.Affiliation = null;
		}
		History(nemesisRecord, "JOINED the " + gang.Name + " as " + Singular(gang.Tiers[tier].Label) + " - a local who threw in with them", gang.Accent);
		SaveRoster();
		return nemesisRecord;
		bool OnTheirGround(NemesisRecord n)
		{
			string killLocation = n.KillLocation;
			if (killLocation == null || !TownCore(killLocation, out var centre, out var _))
			{
				return false;
			}
			District district = DistrictAt(centre);
			if (district != null)
			{
				return HoldOf(district, gang.Id) >= 10;
			}
			return false;
		}
	}

	private (NemesisRecord rec, string note) Recruit(Gang gang, int tier)
	{
		(string, int, int) tuple = gang.Tiers[tier];
		int minRank = tuple.Item2;
		(int, int, PedHash[]) tuple2 = (from s in gang.Seed
			where s.Rank >= minRank
			orderby s.Rank
			select s).FirstOrDefault();
		if (tuple2.Item3 == null)
		{
			tuple2 = gang.Seed.Last();
		}
		PedHash[] item = tuple2.Item3;
		NemesisRecord nemesisRecord = NewRecord((uint)item[_rng.Next(item.Length)], "", gang, minRank);
		nemesisRecord.Gang = gang.Id;
		nemesisRecord.Rank = minRank;
		nemesisRecord.Origin = GrudgeOrigin.GangMember;
		nemesisRecord.SeedMember = true;
		nemesisRecord.TimesKilledPlayer = 0;
		nemesisRecord.KilledPlayerAt = DateTime.Now;
		nemesisRecord.Hate = (gang.SeedHate.HasValue ? (gang.SeedHate.Value.min + _rng.Next(Math.Max(1, gang.SeedHate.Value.max - gang.SeedHate.Value.min + 1))) : ((minRank >= 2) ? (Hate.MurderousAt + _rng.Next(10)) : SeedFootSoldierHate(gang, nemesisRecord)));
		nemesisRecord.AddHate(NotorietyOf(gang) / 5);
		GiveHomeTown(nemesisRecord, gang);
		string text = null;
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.Gang == gang.Id && !n.IsAlive && n.KilledByPlayer && n.Name != null && n.Name.Contains(' ') && !n.Id.StartsWith("fallen_") && !Roster.Any((NemesisRecord k) => k.IsAlive && k.KinId == n.Id)).ToList();
		if (list.Count > 0 && _rng.Next(100) < Config.SuccessionKinChance)
		{
			NemesisRecord nemesisRecord2 = list[_rng.Next(list.Count)];
			string last = FamilyNameOf(nemesisRecord2);
			string first = nemesisRecord.Name.Split(' ')[0];
			nemesisRecord.KinId = nemesisRecord2.Id;
			nemesisRecord.KinRelation = Traits.RollKin(_rng);
			if (gang.FamilyNames && Traits.SharesSurname(nemesisRecord.KinRelation, _rng) && !Roster.Any((NemesisRecord n) => n.Name == first + " " + last))
			{
				nemesisRecord.Name = first + " " + last;
			}
			if (Backstory.FitKinAge(nemesisRecord, nemesisRecord2, Seeds.For(nemesisRecord, "kin"), GameYear()))
			{
				Backstory.Roll(nemesisRecord, Seeds.For(nemesisRecord, "life"), GameYear(), GameMonth(), derive: false);
				GrudgeLog($"HISTORY recruit {nemesisRecord.Name}: {nemesisRecord.KinRelation} of the late {nemesisRecord2.Name} - born {nemesisRecord.BornYear} (his {nemesisRecord2.BornYear}), life rewritten");
			}
			nemesisRecord.AddHate(15);
			text = nemesisRecord.KinRelation + " of the late " + nemesisRecord2.Name;
		}
		History(nemesisRecord, "JOINED the " + gang.Name + " as " + Singular(gang.Tiers[tier].Label) + ((text != null) ? (" - " + text + ", come to settle it") : ""), gang.Accent);
		StartRest(nemesisRecord);
		Roster.Add(nemesisRecord);
		return (rec: nemesisRecord, note: text);
	}

	private static string Singular(string label)
	{
		string text = ((label.Length > 1 && label.EndsWith("S")) ? label.Substring(0, label.Length - 1) : label);
		if (text.Length != 0)
		{
			return text[0] + text.Substring(1).ToLowerInvariant();
		}
		return text;
	}

	private void DrawSuccessionCard()
	{
		if (_successionCard != null && !Config.GrudgeMessages)
		{
			_successionCard = null;
		}
		else
		{
			if (_successionCard == null || _menu.IsOpen || _bulletImpact.ViewerOpen)
			{
				return;
			}
			if (_successionShownMs < 0)
			{
				_successionShownMs = UiDraw.RealMs;
			}
			int num = UiDraw.RealMs - _successionShownMs;
			if (num > 14000)
			{
				_successionCard = null;
				return;
			}
			int num2 = ((num < 800) ? (255 * num / 800) : ((num > 13200) ? (255 * (14000 - num) / 800) : 255));
			int num3 = Math.Min(_successionCard.Count, 16);
			float h = 0.06f + (float)num3 * 0.024f + 0.012f;
			Theme.Brush(0.665f, 0.3f, 0.315f, h, num2);
			UiDraw.Rect(0.665f, 0.3f, 0.315f, 0.0027777776f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, num2);
			UiDraw.TextFont(_successionTitle, UiDraw.TitleFont, 0.67700005f, 0.31f, 0.42f, Theme.Title.r, Theme.Title.g, Theme.Title.b, num2);
			UiDraw.Text("The ranks have shifted.", 0.67700005f, 0.338f, 0.24f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b, num2);
			float num4 = 0.36200002f;
			for (int i = 0; i < num3; i++)
			{
				var (text, tuple2) = _successionCard[i];
				if (Gangs.All.Any((Gang g) => g.Name.ToUpperInvariant() == text))
				{
					UiDraw.Rect(0.67700005f, num4 + 0.005f, 0.0015625001f, 0.013f, tuple2.Item1, tuple2.Item2, tuple2.Item3, num2);
					UiDraw.TextFont(text, UiDraw.TitleFont, 0.684f, num4 + 0.001f, 0.26f, tuple2.Item1, tuple2.Item2, tuple2.Item3, num2);
				}
				else
				{
					UiDraw.Text((text.Length > 52) ? (text.Substring(0, 51) + ".") : text, 0.684f, num4 + 0.002f, 0.25f, tuple2.Item1, tuple2.Item2, tuple2.Item3, num2);
				}
				num4 += 0.024f;
			}
		}
	}

	internal static string OutfitPoss(Gang g)
	{
		if (g != null)
		{
			if (!g.Name.EndsWith("s"))
			{
				return g.Name + "'s";
			}
			return g.Name + "'";
		}
		return "";
	}
}
