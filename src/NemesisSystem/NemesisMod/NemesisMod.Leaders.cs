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
	private readonly Dictionary<string, (int handle, int at, Vector3 pos)> _leaderTied = new Dictionary<string, (int, int, Vector3)>();

	private int _nextLeaderScan;

	private bool _leadersApplied;

	private bool _leaderDupesCleaned;

	private static readonly Dictionary<string, (Personality temper, int born, string place, (int y, string kind, string text)[] past)> LeaderLore = new Dictionary<string, (Personality, int, string, (int, string, string)[])>
	{
		["lemoyneraiders"] = (Personality.Proud, 1846, "Lemoyne", new(int, string, string)[7]
		{
			(1862, "war", "Rode off to the war with the Lemoyne volunteers"),
			(1865, "war", "Came home from the war beaten - and never accepted it"),
			(1868, "loss", "Watched carpetbaggers and Yankee money buy up the parish"),
			(1880, "gang", "Fell in with the old soldiers who would call themselves the Lemoyne Raiders"),
			(1890, "crime", "Robbed and burned his way across Scarlett Meadows in the name of a cause long lost"),
			(1895, "gang", "Ran the Raiders out of Fort Brennand like the war never ended"),
			(1899, "crime", "Wanted in Saint Denis: $100, dead or alive")
		}),
		["odriscoll"] = (Personality.Cold, 1852, "New York", new(int, string, string)[7]
		{
			(1870, "youth", "Grew up hard among the Irish of the eastern cities"),
			(1880, "gang", "Built the O'Driscoll Boys into the biggest gang in the territories"),
			(1885, "crime", "Robbed trains, banks and stagecoaches from the plains to the mountains - a price on his head in every state"),
			(1889, "crime", "His feud with Dutch van der Linde turned to blood: Dutch killed his brother, his men killed Dutch's woman Annabelle"),
			(1898, "gang", "Keeps his Boys spread through the north - Six Point Cabin, Hanging Dog Ranch and camps across the Heartlands"),
			(1899, "crime", "His men lost a camp in the snows above Colter to Dutch's gang, fleeing Blackwater"),
			(1899, "gang", "Still runs the O'Driscolls, and still wants Dutch dead")
		}),
		["army"] = (Personality.Cold, 1845, "Pennsylvania", new(int, string, string)[6]
		{
			(1863, "war", "Fought for the Union"),
			(1870, "work", "Stayed in the Army and went west with it"),
			(1876, "war", "Rode in the campaigns against the plains nations"),
			(1896, "work", "Took command at Fort Wallace"),
			(1899, "work", "Keeps the Wapiti on their reservation and wants the outlaw problem buried"),
			(1899, "work", "Answers to Washington and to the men with money who want the land opened up")
		}),
		["wapiti"] = (Personality.Loyal, 1835, "the plains", new(int, string, string)[6]
		{
			(1860, "family", "Became a father - his son is Eagle Flies"),
			(1870, "gang", "Became chief of the Wapiti"),
			(1876, "war", "Saw the Army take the plains, treaty by broken treaty"),
			(1892, "world", "Led his people onto the reservation in Ambarino rather than see them die fighting the Army"),
			(1898, "world", "Speaks for his people to the soldiers at Fort Wallace and to anyone in Washington who'll listen"),
			(1899, "gang", "Still trusts in treaties and talk; his young men, Eagle Flies among them, are losing patience")
		}),
		["bronte"] = (Personality.Greedy, 1850, "Italy", new(int, string, string)[6]
		{
			(1875, "work", "Crossed from Italy to America"),
			(1885, "crime", "Made his money in Saint Denis on the docks, in the gambling halls and the bootleg trade"),
			(1890, "crime", "Built a mansion on the bayou and filled it with paintings and fine things"),
			(1895, "crime", "Bought the police, the mayor's ear and most of the city"),
			(1898, "crime", "Calls himself a businessman; the men who cross him end up in the swamp"),
			(1899, "crime", "Owns Saint Denis - and smiles while he does it")
		})
	};

	internal static readonly HashSet<string> LoreModels = new HashSet<string>();

	private readonly Dictionary<string, string> _leaderState = new Dictionary<string, string>();

	internal NemesisRecord LeaderPageOf(Gang g)
	{
		if (g == null || g.StoryOnly)
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
			return Roster.FirstOrDefault((NemesisRecord n) => n.Id == seat.Successor);
		}
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == LeaderCopyId(g) && n.IsAlive);
		if (nemesisRecord != null)
		{
			return nemesisRecord;
		}
		return LorePage(g);
	}

	internal NemesisRecord LorePage(Gang g)
	{
		if (g == null)
		{
			return null;
		}
		if (_leaderPages.TryGetValue(g.Id, out var value))
		{
			return value;
		}
		string name = g.LoreLeader ?? g.LeaderName;
		NemesisRecord nemesisRecord = new NemesisRecord();
		nemesisRecord.Id = "leader_" + g.Id;
		nemesisRecord.Name = name;
		nemesisRecord.Gang = g.Id;
		nemesisRecord.Rank = 5;
		nemesisRecord.IsAlive = true;
		nemesisRecord.LeaderPage = true;
		nemesisRecord.Origin = GrudgeOrigin.GangMember;
		nemesisRecord.SeedMember = true;
		nemesisRecord.ModelHash = ((!string.IsNullOrEmpty(g.LeaderModel)) ? MISC.GET_HASH_KEY(g.LeaderModel.Split('|')[0]) : 0u);
		value = nemesisRecord;
		GiveLeaderLife(value, g);
		value.Name = name;
		value.Title = "Leader of the " + g.Name;
		value.KinId = null;
		value.KinRelation = null;
		value.PersonalityKnown = true;
		_leaderPages[g.Id] = value;
		return value;
	}

	internal List<(string label, string value)> LeaderFacts(NemesisRecord r)
	{
		Gang g = Gangs.ById(r.Gang);
		List<(string, string)> list = new List<(string, string)>();
		if (g == null)
		{
			return list;
		}
		if (r.BornYear > 0)
		{
			list.Add(("AGE", string.Format("{0} (born {1}{2})", GameYear() - r.BornYear, r.BornYear, string.IsNullOrEmpty(r.Birthplace) ? "" : (", " + r.Birthplace))));
		}
		if (!string.IsNullOrEmpty(g.HomeTown))
		{
			list.Add(("HOLDS", g.HomeTown));
		}
		string text = AgendaText(g);
		if (!string.IsNullOrEmpty(text))
		{
			list.Add(("AIM", char.ToUpperInvariant(text[0]) + text.Substring(1)));
		}
		if (HasPurse(g))
		{
			list.Add(("PURSE", $"${PurseOf(g)}  -  {PurseWord(PurseOf(g))}"));
		}
		list.Add(("MEN", $"{MenOf(g)}  -  {StrengthWord(Strength(g))}"));
		list.Add(("SUPPLIES", $"{SuppliesOf(g)} / 100  -  {SupplyState(SuppliesOf(g))} ({SupplyWord(g)})"));
		List<WarRecord> list2 = Wars.Where((WarRecord w) => w.Result == null && (w.A == g.Id || w.B == g.Id)).ToList();
		list.Add(("WARS", (list2.Count == 0) ? "none" : string.Join(",  ", list2.Select((WarRecord w) => WarName(w) + " (the " + Gangs.ById((w.A == g.Id) ? w.B : w.A)?.Name + ")"))));
		int num = NotorietyOf(g);
		list.Add(("YOU", g.OrdersNotFeelings ? (Notoriety.LevelNames[Notoriety.Level(num)] + " to his men - orders, not feelings") : $"{Notoriety.LevelNames[Notoriety.Level(num)]} to his outfit ({num} / 100)"));
		return list;
	}

	private Dictionary<string, LeaderSeat> Seats => Chronicle.Leaders ?? (Chronicle.Leaders = new Dictionary<string, LeaderSeat>());

	internal void ForgetTiedLeaders()
	{
		_leaderTied.Clear();
	}

	internal void ReapplyLeaders(IEnumerable<NemesisRecord> removed = null)
	{
		if (removed != null)
		{
			foreach (LeaderSeat s in Seats.Values)
			{
				if (s.Successor != null && removed.Any((NemesisRecord r) => r.Id == s.Successor))
				{
					s.Successor = null;
				}
			}
		}
		_leadersApplied = false;
		_leaderTied.Clear();
	}

	internal LeaderSeat SeatOf(Gang g)
	{
		if (g == null || !Seats.TryGetValue(g.Id, out var value))
		{
			return null;
		}
		return value;
	}

	internal bool LeaderVacant(Gang g)
	{
		LeaderSeat leaderSeat = SeatOf(g);
		if (leaderSeat != null && leaderSeat.Fallen != null)
		{
			return leaderSeat.Successor == null;
		}
		return false;
	}

	internal string SuccessorId(Gang g)
	{
		return SeatOf(g)?.Successor;
	}

	internal (string name, string blurb, bool vacant) LeaderCard(Gang g)
	{
		LeaderSeat s = SeatOf(g);
		if (s?.Fallen == null)
		{
			return (name: g.LoreLeader ?? g.LeaderName, blurb: g.LoreBlurb ?? g.LeaderBlurb, vacant: false);
		}
		if (s.Successor != null)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == s.Successor);
			if (nemesisRecord != null)
			{
				return (name: nemesisRecord.FullName, blurb: "Took over after " + s.OldName + " was " + s.Fallen + ".", vacant: false);
			}
		}
		return (name: "Vacant", blurb: s.OldName + " was " + s.Fallen + ". The seat is open until the next shake-up.", vacant: true);
	}

	internal string LeaderAsSaidBy(Gang g, NemesisRecord speaker)
	{
		if (g == null)
		{
			return null;
		}
		if (speaker != null && speaker.Id != null && SuccessorId(g) == speaker.Id)
		{
			return null;
		}
		if (!LeaderCard(g).vacant)
		{
			return g.LeaderName;
		}
		return null;
	}

	internal bool LeaderOut(Gang g, NemesisRecord speaker)
	{
		if (g != null)
		{
			return LeaderAsSaidBy(g, speaker) == null;
		}
		return false;
	}

	internal bool IsLeader(Gang g, NemesisRecord r)
	{
		if (g != null && r != null && r.Id != null)
		{
			return SuccessorId(g) == r.Id;
		}
		return false;
	}

	private void ApplyLeaders()
	{
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			if (gang.LoreLeader == null)
			{
				gang.LoreLeader = gang.LeaderName;
				gang.LoreBlurb = gang.LeaderBlurb;
			}
			(string name, string blurb, bool vacant) tuple = LeaderCard(gang);
			string item = tuple.name;
			string item2 = tuple.blurb;
			bool item3 = tuple.vacant;
			gang.LeaderName = (item3 ? (SeatOf(gang)?.OldName ?? gang.LoreLeader) : item);
			gang.LeaderBlurb = item2;
		}
		_leadersApplied = true;
	}

	private void CleanLeaderCardDupes()
	{
		if (_leaderDupesCleaned)
		{
			return;
		}
		_leaderDupesCleaned = true;
		int num = 0;
		foreach (IGrouping<string, NemesisRecord> item in (from n in Roster
			where n.Id != null && n.Id.StartsWith("fallen_")
			group n by n.Id).ToList())
		{
			NemesisRecord nemesisRecord = item.First();
			foreach (NemesisRecord item2 in item.Skip(1).ToList())
			{
				Roster.Remove(item2);
				num++;
			}
			Gang gang = Gangs.ById(nemesisRecord.Gang);
			LeaderSeat leaderSeat = ((gang != null) ? SeatOf(gang) : null);
			if (leaderSeat != null && leaderSeat.OldName != null && nemesisRecord.Name != leaderSeat.OldName && nemesisRecord.Id == $"fallen_{gang.Id}_{leaderSeat.At}")
			{
				GrudgeLog("LEADER card " + nemesisRecord.Id + ": named " + nemesisRecord.Name + " by mistake - back to " + leaderSeat.OldName);
				nemesisRecord.Name = leaderSeat.OldName;
				nemesisRecord.KinId = null;
				nemesisRecord.KinRelation = null;
			}
		}
		if (num > 0)
		{
			GrudgeLog($"LEADER {num} duplicate fallen leader cards removed");
			SaveRoster();
		}
	}

	private void BackfillFallenLeaders()
	{
		CleanLeaderCardDupes();
		Gang[] all = Gangs.All;
		foreach (Gang g in all)
		{
			LeaderSeat s = SeatOf(g);
			if (s?.Fallen == null || string.IsNullOrEmpty(s.OldName) || s.OldName != (g.LoreLeader ?? s.OldName))
			{
				continue;
			}
			string cardId = $"fallen_{g.Id}_{s.At}";
			if (!Roster.Any((NemesisRecord n) => !n.IsAlive && n.Gang == g.Id && (n.Name == s.OldName || n.Id == cardId)))
			{
				NemesisRecord nemesisRecord = new NemesisRecord();
				nemesisRecord.Id = $"fallen_{g.Id}_{s.At}";
				nemesisRecord.Name = s.OldName;
				nemesisRecord.Title = "";
				nemesisRecord.Gang = g.Id;
				nemesisRecord.Rank = 5;
				nemesisRecord.IsAlive = false;
				nemesisRecord.Origin = GrudgeOrigin.GangMember;
				nemesisRecord.SeedMember = true;
				nemesisRecord.KilledByPlayer = s.Fallen.Contains("by you");
				nemesisRecord.ModelHash = ((!string.IsNullOrEmpty(g.LeaderModel)) ? MISC.GET_HASH_KEY(g.LeaderModel.Split('|')[0]) : 0u);
				NemesisRecord nemesisRecord2 = nemesisRecord;
				if (s.Fallen == "arrested" || s.Fallen.StartsWith("turned in"))
				{
					nemesisRecord2.Fate = "Arrested";
				}
				string text = LeftFate(s.Fallen);
				if (text != null)
				{
					nemesisRecord2.Fate = text;
				}
				if (nemesisRecord2.Fate != null || (Gangs.Era >= 1907 && Chronicle.EraTurnAt > 0 && s.At <= Chronicle.EraTurnAt) || s.Fallen.Contains("on his bounty"))
				{
					nemesisRecord2.Buried = true;
				}
				GiveLeaderLife(nemesisRecord2, g);
				History(nemesisRecord2, FallLabel(s.Fallen) + " - the leader of " + g.Name + ", " + s.Fallen, Theme.Blood);
				Roster.Add(nemesisRecord2);
				SaveRoster();
				GrudgeLog("LEADER " + s.OldName + ": added to the " + g.Name + " fallen row (he fell before the card existed)");
			}
		}
	}

	private void UpdateLeaders(Ped player)
	{
		if (!_leadersApplied)
		{
			FixLeaderNames();
			EnsureLoreMen();
			BountyModelsStayInTheirMissions();
			try
			{
				RetireMistakenNemeses();
			}
			catch (Exception ex)
			{
				GrudgeLog("ROSTER retire failed: " + ex.Message);
			}
			ApplyLeaders();
			try
			{
				FixEraFunerals();
				FixWofford1907();
				BronteRideAgain();
				FixWomensTrades();
			}
			catch (Exception ex2)
			{
				GrudgeLog("FUNERAL era fix failed: " + ex2.Message);
			}
			BackfillFallenLeaders();
			NoteLoreModels();
			RepairLeaderLives();
			try
			{
				RepairPlansOffMap();
			}
			catch (Exception ex3)
			{
				GrudgeLog("PLAN repair failed: " + ex3.Message);
			}
		}
		if (Game.GameTime < _nextLeaderScan)
		{
			return;
		}
		_nextLeaderScan = Game.GameTime + 1000;
		try
		{
			SyncLeaderCopies();
		}
		catch (Exception ex4)
		{
			GrudgeLog("LEADER copies failed: " + ex4.Message);
		}
		BackfillFallenLeaders();
		LoreMenSeen(player);
		TrackNamedPeds();
		try
		{
			LogMissionPeds(player);
		}
		catch
		{
		}
		Gang[] all = Gangs.All;
		foreach (Gang g in all)
		{
			LeaderSeat seat = SeatOf(g);
			if (seat?.Fallen != null && seat.Successor == null && LeaderVacant(g) && !IsDestroyed(g) && SeatDue(g) && !WorldBusy(player))
			{
				List<NemesisRecord> living = Roster.Where((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead).ToList();
				(string, NemesisRecord)? tuple = FillLeaderSeat(g, living);
				if (tuple.HasValue)
				{
					Feed("LEADER " + tuple.Value.Item1, Theme.Gold);
					GrudgeNotice(tuple.Value.Item1);
					_nextCampAssign = 0;
				}
				else
				{
					seat.FillAt = GameMinutesNow() + 720;
				}
				continue;
			}
			if (seat?.Successor != null)
			{
				NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == seat.Successor);
				if (nemesisRecord == null || !nemesisRecord.IsAlive || nemesisRecord.PresumedDead)
				{
					Fall(g, nemesisRecord?.FullName ?? "the man who took over", "killed", nemesisRecord?.KilledByPlayer ?? false, nemesisRecord);
					continue;
				}
			}
			if (seat?.Fallen != null || string.IsNullOrEmpty(g.LeaderModel))
			{
				continue;
			}
			HashSet<uint> models = new HashSet<uint>(from x in g.LeaderModel.Split('|')
				where x.Trim().Length > 0
				select MISC.GET_HASH_KEY(x.Trim()));
			Ped ped = PedsAround(player).FirstOrDefault((Ped p) => p != null && p.Exists() && models.Contains((uint)p.Model.Hash) && AtLeaderSpot(g, p.Position) && !(LeaderCopyAt?.Invoke(p.Handle) ?? false));
			if (ped == null && _leaderTied.TryGetValue(g.Id, out (int, int, Vector3) value) && Entity.FromHandle(value.Item1) is Ped ped2 && ped2.Exists() && models.Contains((uint)ped2.Model.Hash))
			{
				ped = ped2;
			}
			LeaderWatch(g, ped, player);
			if (g.LoreLeader == null)
			{
				_ = g.LeaderName;
			}
			if (Game.GameTime - _cashRoseAt < 20000 && TryArrest(g, player))
			{
				continue;
			}
			if (ped != null)
			{
				(int, int, Vector3) value2;
				if (ped.IsDead && !LooksKnockedOut(ped))
				{
					int num = PED.GET_PED_SOURCE_OF_DEATH(ped.Handle);
					bool byYou = num == player.Handle || (num == 0 && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true)) || IsCompanionKiller(num);
					Fall(g, g.LoreLeader ?? g.LeaderName, "killed", byYou, null, g.LeaderModel);
				}
				else if (ped.IsHogtied || ENTITY.IS_ENTITY_ATTACHED_TO_ENTITY(ped.Handle, player.Handle) || ENTITY.IS_ENTITY_ATTACHED(ped.Handle))
				{
					_leaderTied[g.Id] = (ped.Handle, Game.GameTime, ped.Position);
				}
				else if (_leaderTied.TryGetValue(g.Id, out value2) && value2.Item1 == ped.Handle)
				{
					_leaderTied[g.Id] = (value2.Item1, value2.Item2, ped.Position);
				}
			}
			else
			{
				if (!_leaderTied.TryGetValue(g.Id, out (int, int, Vector3) value3))
				{
					continue;
				}
				Entity entity = Entity.FromHandle(value3.Item1);
				if (Game.GameTime - value3.Item2 > 300000)
				{
					_leaderTied.Remove(g.Id);
				}
				else if (entity == null || !entity.Exists())
				{
					_leaderTied.Remove(g.Id);
					bool flag = player.Position.DistanceTo(value3.Item3) < 60f;
					bool flag2 = Game.GameTime - _cashRoseAt < 90000;
					if (flag && flag2)
					{
						Fall(g, g.LoreLeader ?? g.LeaderName, "arrested", byYou: true, null, g.LeaderModel);
						continue;
					}
					GrudgeLog("LEADER " + (g.LoreLeader ?? g.LeaderName) + " was tied up and is gone (" + (flag ? "no bounty paid" : $"{player.Position.DistanceTo(value3.Item3):0} m from where you left him") + ") - not turned in");
				}
			}
		}
	}

	private void GiveLeaderLife(NemesisRecord r, Gang g)
	{
		if (r == null || g == null || (r.Past != null && r.Past.Count > 0))
		{
			return;
		}
		if (LeaderLore.TryGetValue(g.Id, out (Personality, int, string, (int, string, string)[]) value) && r.Name == (g.LoreLeader ?? g.LeaderName))
		{
			try
			{
				RollNewLife(r, Roster, mind: true, 1);
			}
			catch
			{
			}
			r.KinId = null;
			r.KinRelation = null;
			r.Personality = value.Item1;
			r.BornYear = value.Item2;
			r.Birthplace = value.Item3;
			r.Past = new List<PastEntry>
			{
				new PastEntry
				{
					Year = value.Item2,
					Kind = "born",
					Text = "Born in " + value.Item3,
					Public = true,
					Known = true
				}
			};
			(int, string, string)[] item = value.Item4;
			for (int i = 0; i < item.Length; i++)
			{
				(int, string, string) tuple = item[i];
				r.Past.Add(new PastEntry
				{
					Year = tuple.Item1,
					Kind = tuple.Item2,
					Text = tuple.Item3,
					Public = true,
					Known = true
				});
			}
			r.PastRolled = true;
			r.PersonalityKnown = true;
			GrudgeLog($"LEADER {r.Name}: his life written from the lore ({r.Past.Count} lines, {value.Item1})");
		}
		else
		{
			string name = r.Name;
			string title = r.Title;
			try
			{
				RollNewLife(r, Roster, mind: true, 1);
			}
			catch (Exception ex)
			{
				GrudgeLog("LEADER life roll failed: " + ex.Message);
			}
			r.Name = name;
			r.Title = title;
			r.KinId = null;
			r.KinRelation = null;
			if (r.Past != null)
			{
				foreach (PastEntry item2 in r.Past)
				{
					item2.Public = true;
					item2.Known = true;
				}
			}
			r.PersonalityKnown = true;
			GrudgeLog($"LEADER {r.Name}: a life rolled for him ({r.Past?.Count ?? 0} lines, {r.Personality})");
		}
		r.Rank = 5;
	}

	private void RepairLeaderLives()
	{
		bool flag = false;
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.Id != null && n.Id.StartsWith("fallen_") && (n.Past == null || n.Past.Count == 0)).ToList())
		{
			Gang gang = Gangs.ById(item.Gang);
			if (gang != null)
			{
				GiveLeaderLife(item, gang);
				if (item.History == null || item.History.Count == 0)
				{
					History(item, ((item.Fate == "Arrested") ? "ARRESTED" : "DIED") + " - the leader of " + gang.Name, Theme.Blood);
				}
				flag = true;
			}
		}
		if (flag)
		{
			SaveRoster();
		}
	}

	internal void ArrestOnPayout(Ped player)
	{
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			if (SeatOf(gang)?.Fallen == null && _leaderTied.ContainsKey(gang.Id))
			{
				try
				{
					TryArrest(gang, player);
				}
				catch (Exception ex)
				{
					GrudgeLog("LEADER arrest check failed: " + ex.Message);
				}
			}
		}
	}

	private bool TryArrest(Gang g, Ped player)
	{
		if (!_leaderTied.TryGetValue(g.Id, out (int, int, Vector3) value) || Game.GameTime - value.Item2 > 1800000)
		{
			return false;
		}
		Ped ped = Entity.FromHandle(value.Item1) as Ped;
		bool flag = ped != null && ped.Exists();
		if (flag && ped.IsDead && !LooksKnockedOut(ped))
		{
			return false;
		}
		if ((!flag || !(ped.Position.DistanceTo(player.Position) < 300f)) && !(value.Item3.DistanceTo(player.Position) < 150f) && flag)
		{
			GrudgeLog("LEADER " + (g.LoreLeader ?? g.LeaderName) + ": money came in but he's " + (flag ? $"{ped.Position.DistanceTo(player.Position):0} m" : "?") + " away - not counted");
			return false;
		}
		_leaderTied.Remove(g.Id);
		string text = g.LoreLeader ?? g.LeaderName;
		GrudgeLog("LEADER " + text + ": the bounty paid out with him in your hands (" + (flag ? $"{ped.Position.DistanceTo(player.Position):0} m" : "taken away") + ") - ARRESTED");
		Fall(g, text, "arrested", byYou: true, null, g.LeaderModel);
		return true;
	}

	internal static bool AtLeaderSpot(Gang g, Vector3 p)
	{
		if (g.LeaderSpot != null && g.LeaderSpot.Length >= 3)
		{
			return new Vector3(p.X - g.LeaderSpot[0], p.Y - g.LeaderSpot[1], 0f).Length() < g.LeaderSpot[2];
		}
		return true;
	}
}
