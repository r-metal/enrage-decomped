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
	private long _lastMurderousAt = -100000L;

	private readonly Dictionary<int, (Gang gang, int at)> _factionHits = new Dictionary<int, (Gang, int)>();

	private int _nextFactionPoll;

	internal void DebugReset(string what, Gang gang = null)
	{
		try
		{
			switch (what)
			{
			case "all":
			{
				WipeWorldAfterRespawn();
				int num = ResetEverything();
				DespawnAftermath();
				ClearAftermathBlips();
				SeedGangRosters();
				AssignCamps();
				EnsureWorldHistory();
				SaveRoster();
				ShowNotification($"Fresh start: {num} records cleared, notoriety, X-rays and world events wiped. New rosters drawn up.");
				break;
			}
			case "safecamps":
				ForgetSafeCamps();
				ShowNotification("Safe camps forgotten - the next one is learned when you're seen there twice.");
				break;
			case "loners":
				ShowNotification($"{SeedTestLoners(12)} test loners added to the Unaffiliated list.");
				break;
			case "xray":
				_bulletImpact.ClearXRayLog();
				ShowNotification("X-ray log cleared.");
				break;
			case "world":
			{
				DespawnAftermath();
				ClearAftermathBlips();
				int era = Gangs.Era;
				_chron = new WorldState
				{
					Era = ((era >= 1907) ? 1907 : 0)
				};
				if (era >= 1907)
				{
					_chron.EraTurnAt = GameMinutesNow();
					_chron.EraFuneralsFixed = true;
					_chron.WoffordFixed = true;
					_chron.HomeStrongholdsRestored = true;
					Apply1907Camps();
				}
				EnsureWorldHistory();
				SaveWorld();
				ShowNotification("World events cleared - the years before you written anew.");
				break;
			}
			case "faction":
			{
				BackupRoster((gang != null) ? (gang.Id + "-reset") : "loners-reset");
				WipeWorldAfterRespawn();
				_rosterGen++;
				List<NemesisRecord> list = Roster.Where((NemesisRecord x) => x.Gang == gang?.Id).ToList();
				foreach (NemesisRecord item in list)
				{
					RemoveRecord(item, "faction reset");
				}
				ReapplyLeaders(list);
				int count = list.Count;
				if (gang != null)
				{
					Config.SeededGangs = string.Join(",", from id in Config.SeededGangs.Split(',')
						select id.Trim() into id
						where id.Length > 0 && id != gang.Id
						select id);
					Config.Notoriety[gang.Id] = 0;
					if (gang == Gangs.Army)
					{
						Config.ArmyTopUp = true;
					}
				}
				Config.Save();
				SeedGangRosters();
				AssignCamps();
				SaveRoster();
				ShowNotification(string.Format("{0} reset: {1} records cleared{2}.", gang?.Name ?? "Unaffiliated", count, (gang != null) ? ", a fresh roster drawn up" : ""));
				break;
			}
			}
			GrudgeLog("RESET " + what + ((what == "faction") ? (" " + (gang?.Id ?? "loners")) : "") + " (debug menu)");
		}
		catch (Exception ex)
		{
			ShowNotification("Reset failed: " + ex.Message);
			GrudgeLog("RESET failed: " + ex);
		}
	}

	private int SeedFootSoldierHate(Gang gang, NemesisRecord rec)
	{
		if (!gang.MixedFootSoldiers)
		{
			return 50 + _rng.Next(28);
		}
		int num = _rng.Next(100);
		if (rec.Personality == Personality.Hothead)
		{
			num += 25;
		}
		if (rec.Personality == Personality.Proud)
		{
			num += 10;
		}
		if (rec.Personality == Personality.Coward)
		{
			num -= 25;
		}
		if (num < 35)
		{
			return 22 + _rng.Next(16);
		}
		if (num < 70)
		{
			return Hate.VengefulAt + 2 + _rng.Next(Hate.MurderousAt - Hate.VengefulAt - 4);
		}
		return Hate.MurderousAt + 2 + _rng.Next(12);
	}

	private int TopUpFromSeed(Gang gang)
	{
		if (gang?.Seed == null)
		{
			return 0;
		}
		int num = 0;
		(int, int, PedHash[])[] seed = gang.Seed;
		for (int i = 0; i < seed.Length; i++)
		{
			(int, int, PedHash[]) tuple = seed[i];
			int rank = tuple.Item1;
			int item = tuple.Item2;
			PedHash[] item2 = tuple.Item3;
			for (int num2 = Roster.Count((NemesisRecord n) => n.Gang == gang.Id && n.IsAlive && !n.PresumedDead && ((rank < 4) ? (n.Rank == rank) : (n.Rank >= 4))); num2 < item; num2++)
			{
				NemesisRecord nemesisRecord = NewRecord((uint)item2[_rng.Next(item2.Length)], "", gang, rank);
				nemesisRecord.Gang = gang.Id;
				nemesisRecord.Rank = rank;
				nemesisRecord.Origin = GrudgeOrigin.GangMember;
				nemesisRecord.SeedMember = true;
				nemesisRecord.TimesKilledPlayer = 0;
				nemesisRecord.KilledPlayerAt = DateTime.Now;
				nemesisRecord.Hate = (gang.SeedHate.HasValue ? (gang.SeedHate.Value.min + _rng.Next(Math.Max(1, gang.SeedHate.Value.max - gang.SeedHate.Value.min + 1))) : ((rank >= 2) ? (72 + _rng.Next(10)) : SeedFootSoldierHate(gang, nemesisRecord)));
				if (rank >= 2 && !gang.SeedHate.HasValue && nemesisRecord.Personality == Personality.Hothead)
				{
					nemesisRecord.Hate = Math.Min(100, nemesisRecord.Hate + 6);
				}
				if (rank >= 2 && nemesisRecord.Personality == Personality.Coward)
				{
					nemesisRecord.Hate -= 12;
				}
				GiveHomeTown(nemesisRecord, gang);
				Roster.Add(nemesisRecord);
				num++;
			}
		}
		if (num > 0)
		{
			GrudgeLog($"ROSTER {gang.Name}: {num} men added to bring it up to its starting table");
		}
		return num;
	}

	private void SeedGangRosters(string again = null)
	{
		HashSet<string> seeded = new HashSet<string>(Config.SeededGangs.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries));
		foreach (NemesisRecord item4 in Roster.Where((NemesisRecord n) => n.Origin == GrudgeOrigin.GangMember))
		{
			item4.SeedMember = true;
		}
		foreach (Gang item5 in Gangs.All.Where((Gang g) => Roster.Any((NemesisRecord n) => n.Gang == g.Id && n.SeedMember)))
		{
			seeded.Add(item5.Id);
		}
		if (again != null)
		{
			seeded.Remove(again);
		}
		foreach (Gang item6 in Gangs.All.Where((Gang g) => g.Seed != null && !seeded.Contains(g.Id)))
		{
			int num = 0;
			(int, int, PedHash[])[] seed = item6.Seed;
			for (int num2 = 0; num2 < seed.Length; num2++)
			{
				(int, int, PedHash[]) tuple = seed[num2];
				int item = tuple.Item1;
				int item2 = tuple.Item2;
				PedHash[] item3 = tuple.Item3;
				for (int num3 = 0; num3 < item2; num3++)
				{
					NemesisRecord nemesisRecord = NewRecord((uint)item3[_rng.Next(item3.Length)], "", item6, item);
					nemesisRecord.Gang = item6.Id;
					nemesisRecord.Rank = item;
					nemesisRecord.Origin = GrudgeOrigin.GangMember;
					nemesisRecord.SeedMember = true;
					nemesisRecord.TimesKilledPlayer = 0;
					nemesisRecord.KilledPlayerAt = DateTime.Now;
					nemesisRecord.Hate = (item6.SeedHate.HasValue ? (item6.SeedHate.Value.min + _rng.Next(Math.Max(1, item6.SeedHate.Value.max - item6.SeedHate.Value.min + 1))) : ((item >= 2) ? (72 + _rng.Next(10)) : SeedFootSoldierHate(item6, nemesisRecord)));
					if (item >= 2 && !item6.SeedHate.HasValue && nemesisRecord.Personality == Personality.Hothead)
					{
						nemesisRecord.Hate = Math.Min(100, nemesisRecord.Hate + 6);
					}
					if (item >= 2 && nemesisRecord.Personality == Personality.Coward)
					{
						nemesisRecord.Hate -= 12;
					}
					GiveHomeTown(nemesisRecord, item6);
					Roster.Add(nemesisRecord);
					num++;
				}
			}
			seeded.Add(item6.Id);
			GrudgeNotice($"{item6.Name}: {num} men now stand under {item6.LeaderName}.");
		}
		if (!Config.ArmyTopUp && seeded.Contains(Gangs.Army.Id))
		{
			for (int num4 = Roster.Count((NemesisRecord n) => n.Gang == Gangs.Army.Id && n.SeedMember && n.Rank == 1); num4 < 14; num4++)
			{
				NemesisRecord nemesisRecord2 = NewRecord(2150064377u, "", Gangs.Army);
				nemesisRecord2.Rank = 1;
				nemesisRecord2.Origin = GrudgeOrigin.GangMember;
				nemesisRecord2.SeedMember = true;
				nemesisRecord2.TimesKilledPlayer = 0;
				nemesisRecord2.KilledPlayerAt = DateTime.Now;
				nemesisRecord2.Hate = 50 + _rng.Next(28);
				Roster.Add(nemesisRecord2);
			}
			Config.ArmyTopUp = true;
		}
		Config.SeededGangs = string.Join(",", seeded);
		Config.Save();
		SaveRoster();
	}

	private void TrimGangRosters()
	{
		int num = 0;
		foreach (Gang gang in Gangs.All.Where((Gang g) => g.Seed != null))
		{
			string succ = SuccessorId(gang);
			List<NemesisRecord> source = Roster.Where((NemesisRecord n) => n.Gang == gang.Id && n.IsAlive && !n.PresumedDead && n.Id != succ).ToList();
			for (int num2 = 0; num2 < gang.Tiers.Length; num2++)
			{
				(string, int, int) tuple = gang.Tiers[num2];
				int minRank = tuple.Item2;
				int item = tuple.Item3;
				int maxRank = ((num2 == 0) ? int.MaxValue : (gang.Tiers[num2 - 1].MinRank - 1));
				List<NemesisRecord> list = source.Where((NemesisRecord n) => n.Rank >= minRank && n.Rank <= maxRank).ToList();
				List<NemesisRecord> list2 = (from n in list
					where n.Origin == GrudgeOrigin.GangMember && n.TimesAmbushed == 0 && n.TimesKilledPlayer == 0 && n.LoreModel == null && n.FormerGang == null && !_worldNemeses.ContainsValue(n) && n != ActiveRecord
					orderby n.KilledPlayerAt descending
					select n).ToList();
				for (int num3 = 0; num3 < list.Count - item && num3 < list2.Count; num3++)
				{
					RemoveRecord(list2[num3], "gang roster trimmed");
					num++;
				}
			}
		}
		if (num > 0)
		{
			SaveRoster();
			GrudgeLog($"ROSTER gang rosters trimmed back to their seats ({num} extra men removed)");
		}
	}

	private NemesisRecord PickAmbusher(List<NemesisRecord> candidates)
	{
		List<NemesisRecord> list = candidates.Where((NemesisRecord n) => !_worldNemeses.ContainsValue(n) && n != ActiveRecord).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		candidates = list;
		double num = ((IEnumerable<NemesisRecord>)candidates).Sum((Func<NemesisRecord, double>)AmbushWeightOf);
		if (num <= 0.0)
		{
			return null;
		}
		double num2 = _rng.NextDouble() * num;
		foreach (NemesisRecord candidate in candidates)
		{
			double num3 = AmbushWeightOf(candidate);
			if (!(num3 <= 0.0))
			{
				num2 -= num3;
				if (num2 <= 0.0)
				{
					return candidate;
				}
			}
		}
		return candidates.LastOrDefault((NemesisRecord n) => AmbushWeightOf(n) > 0.0);
	}

	internal double AmbushWeightOf(NemesisRecord n)
	{
		double num = ((n.Origin == GrudgeOrigin.GangMember && n.TimesAmbushed == 0) ? ((double)Config.GangAmbushWeight) : 1.0) * (n.Personality.HasValue ? Personalities.AmbushWeight(n.Personality.Value) : 1.0);
		Gang gang = Gangs.ById(n.Gang);
		if (gang != null)
		{
			num *= ((CurrentDistrict != null) ? gang.EncounterRate : gang.AmbushBase) * Notoriety.Ambush(NotorietyOf(gang)) * RegionMult(gang);
		}
		if (gang != null && !ReasonMet(gang))
		{
			num = 0.0;
		}
		if (gang != null && IsDestroyed(gang))
		{
			num = 0.0;
		}
		if (gang != null)
		{
			LeaderSeat leaderSeat = SeatOf(gang);
			if (leaderSeat != null && leaderSeat.Fallen != null && leaderSeat.Successor == null)
			{
				num *= 0.5;
			}
		}
		if (gang != null)
		{
			int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
			num *= ((cLOCK_HOURS >= 21 || cLOCK_HOURS < 5) ? gang.NightWeight : gang.DayWeight);
		}
		num *= 1.0 - Math.Min(0.8, (double)EffectiveFear(n) / 150.0);
		num *= (double)((n.HateTier == HateTier.Murderous) ? Balance.WildMurderous : ((n.HateTier == HateTier.Vengeful) ? Balance.WildVengeful : Balance.WildResentful));
		num *= Mind.AmbushMult(n);
		if (gang != null)
		{
			num *= RankEncounterMult(n.Rank);
		}
		if (gang != null)
		{
			num *= AgendaAmbushMult(gang);
		}
		return num;
	}

	internal static double RankEncounterMult(int rank)
	{
		if (rank > 1)
		{
			return rank switch
			{
				4 => 0.35, 
				3 => 0.55, 
				2 => 0.75, 
				_ => 0.2, 
			};
		}
		return 1.0;
	}

	internal bool MurderousCooling
	{
		get
		{
			long num = GameMinutesNow();
			if (_lastMurderousAt <= num)
			{
				return num - _lastMurderousAt < (long)Balance.MurderousCooldownHours * 60L;
			}
			return false;
		}
	}

	internal bool ManCooling(NemesisRecord n)
	{
		long num = GameMinutesNow();
		if (n.LastCameAt > 0 && n.LastCameAt <= num)
		{
			return num - n.LastCameAt < (long)Balance.ManCooldownHours * 60L;
		}
		return false;
	}

	internal bool CoolingOff(NemesisRecord n)
	{
		if (!ManCooling(n))
		{
			if (n.HateTier == HateTier.Murderous)
			{
				return MurderousCooling;
			}
			return false;
		}
		return true;
	}

	internal float HateGainOf(NemesisRecord n)
	{
		return n.HateGainMult * Balance.HateGainScale;
	}

	internal string HateGainWhy(NemesisRecord n)
	{
		List<string> list = new List<string>();
		if (n.Personality.HasValue && Math.Abs(Personalities.GainMult(n.Personality.Value) - 1f) > 0.01f)
		{
			list.Add($"{n.Personality.Value.ToString().ToLowerInvariant()} x{Personalities.GainMult(n.Personality.Value):0.00}");
		}
		float num = Traits.HateGain(n);
		if (Math.Abs(num - 1f) > 0.01f)
		{
			list.Add($"traits x{num:0.00}");
		}
		if (list.Count != 0)
		{
			return " (" + string.Join(", ", list) + ")";
		}
		return "";
	}

	internal void NoteCame(NemesisRecord n)
	{
		long lastMurderousAt = (n.LastCameAt = GameMinutesNow());
		if (n.HateTier == HateTier.Murderous)
		{
			_lastMurderousAt = lastMurderousAt;
		}
	}

	internal int EffectiveFear(NemesisRecord r)
	{
		return Math.Min(100, r.Fear + Math.Max(r.YourNotoriety, NotorietyOf(Gangs.ById(r.Gang))) / Math.Max(1, Balance.NotorietyFearDivisor));
	}

	internal Intent IntentOf(NemesisRecord r)
	{
		int effectiveFear = EffectiveFear(r);
		return Mind.ShiftIntent(r, Traits.IntentOf(r, effectiveFear), effectiveFear);
	}

	internal int NotorietyOf(Gang g)
	{
		if (g == null || !Config.Notoriety.TryGetValue(g.Id, out var value))
		{
			return 0;
		}
		return value;
	}

	private void AddNotoriety(Gang g, int amount, string why)
	{
		if (g == null || amount <= 0)
		{
			return;
		}
		bool flag = false;
		try
		{
			flag = PlayerMasked(Game.Player.Character);
		}
		catch
		{
		}
		if (flag)
		{
			Dictionary<string, float> obj2 = Chronicle.MaskedNotoriety ?? (Chronicle.MaskedNotoriety = new Dictionary<string, float>());
			obj2.TryGetValue(g.Id, out var value);
			value += (float)amount * Balance.NotorietyGainScale / 4f;
			Dictionary<string, float> obj3 = Chronicle.MaskedWithheld ?? (Chronicle.MaskedWithheld = new Dictionary<string, float>());
			obj3.TryGetValue(g.Id, out var value2);
			obj3[g.Id] = value2 + (float)amount * Balance.NotorietyGainScale * 0.75f;
			int num = (int)value;
			obj2[g.Id] = value - (float)num;
			SaveWorld();
			if (num <= 0)
			{
				GrudgeLog($"NOTORIETY {g.Id} stays {NotorietyOf(g)} - you were masked ({why}): a quarter counts, {value:0.00} of a point so far");
				return;
			}
			why += " - masked, a quarter";
			amount = num;
		}
		else
		{
			amount = Math.Max(1, (int)Math.Round((float)amount * Balance.NotorietyGainScale));
		}
		int num2 = NotorietyOf(g);
		int num3 = Math.Min(100, num2 + amount);
		Config.Notoriety[g.Id] = num3;
		(Chronicle.NotorietyGainDay ?? (Chronicle.NotorietyGainDay = new Dictionary<string, int>()))[g.Id] = GameDayKey();
		Config.Save();
		GrudgeLog($"NOTORIETY {g.Id} {num2} -> {num3} ({why})");
		Feed($"NOTORIETY {g.Name} {num2} -> {num3} ({why})", g.Accent);
		if (Notoriety.Level(num3) > Notoriety.Level(num2))
		{
			GrudgeNotice(g.Name + ": your notoriety rises - " + Notoriety.LevelName(num3) + ". More of them will come for you.");
		}
	}

	private void NoteFactionHit(int victim)
	{
		if (_worldNemeses.ContainsKey(victim) || (_activeNemesisPed != null && victim == _activeNemesisPed.Handle))
		{
			return;
		}
		uint model = ENTITY.GET_ENTITY_MODEL(victim);
		if (LoreModels.Count <= 0 || !LoreModels.Contains(Models.Name(model)))
		{
			Gang gang = Gangs.ForModel(model);
			if (gang != null)
			{
				_factionHits[victim] = (gang, Game.GameTime);
			}
		}
	}

	private void PollFactionKills()
	{
		if (_factionHits.Count == 0 || Game.GameTime < _nextFactionPoll)
		{
			return;
		}
		_nextFactionPoll = Game.GameTime + 500;
		foreach (KeyValuePair<int, (Gang, int)> item in _factionHits.ToList())
		{
			int key = item.Key;
			if (!ENTITY.DOES_ENTITY_EXIST(key) || !ENTITY.IS_ENTITY_A_PED(key) || Game.GameTime - item.Value.Item2 > 20000)
			{
				_factionHits.Remove(key);
				continue;
			}
			if (_worldNemeses.ContainsKey(key))
			{
				_factionHits.Remove(key);
				continue;
			}
			Ped ped = new Ped(key);
			if (!ped.IsDead || LooksKnockedOut(ped))
			{
				continue;
			}
			_factionHits.Remove(key);
			int num = PED.GET_PED_SOURCE_OF_DEATH(key);
			int handle = Game.Player.Character.Handle;
			if (num != 0 && num != handle && !IsCompanionKiller(num))
			{
				NoteKill(ped, item.Value.Item1?.Id);
				continue;
			}
			NoteKill(ped, item.Value.Item1?.Id);
			try
			{
				NoteFairKill(item.Value.Item1, ped);
			}
			catch (Exception ex)
			{
				GrudgeLog("FAIRGAME failed: " + ex.Message);
			}
			try
			{
				MercyTakenBack("h" + key);
			}
			catch
			{
			}
			string key2 = item.Value.Item1?.Id ?? "";
			int num2 = Math.Max(1, item.Value.Item1?.UnnamedKillsPerPoint ?? Balance.UnnamedKillsPerPoint);
			Dictionary<string, int> dictionary = Chronicle.UnnamedKills ?? (Chronicle.UnnamedKills = new Dictionary<string, int>());
			dictionary.TryGetValue(key2, out var value);
			value++;
			if (value >= num2)
			{
				value = 0;
				AddNotoriety(item.Value.Item1, Notoriety.ForKill(0), (num2 == 1) ? "killed one of their men" : $"killed {num2} of their men");
			}
			try
			{
				ControlAfterKill(item.Value.Item1, 0, ped.Position);
			}
			catch
			{
			}
			dictionary[key2] = value;
			SaveWorld();
		}
	}
}
