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
	internal static int TurmoilWeight = 10;

	private string _lastCrackdown;

	private static readonly string[] SaveFiles = new string[6] { "nemeses.json", "world.json", "people.json", "fingerprints2.json", "xray_log.json", "fallen.json" };

	private int _nextSavePointCheck;

	private Dictionary<string, long> Destroyed => Chronicle.Destroyed ?? (Chronicle.Destroyed = new Dictionary<string, long>());

	internal bool IsDestroyed(Gang g)
	{
		if (g != null)
		{
			if (!Destroyed.ContainsKey(g.Id))
			{
				return !Gangs.InEra(g);
			}
			return true;
		}
		return false;
	}

	internal bool LoreLeaderHolds(Gang g)
	{
		if (g != null && g.LoreLeaderProtected)
		{
			if (SeatOf(g) != null)
			{
				return SeatOf(g).Fallen == null;
			}
			return true;
		}
		return false;
	}

	internal static bool LoreProtected(NemesisRecord r)
	{
		if (r != null && r.Id != null)
		{
			return r.Id.StartsWith("lore_");
		}
		return false;
	}

	internal double TerritoryShare(Gang g)
	{
		if (g == null)
		{
			return 1.0;
		}
		double num = 0.0;
		double num2 = 0.0;
		District[] districts = Districts;
		foreach (District district in districts)
		{
			num += (double)HoldOf(district, g.Id);
			num2 += (double)(district.Lore.TryGetValue(g.Id, out var value) ? value : 0);
		}
		if (!(num2 <= 0.0))
		{
			return num / num2;
		}
		return 1.0;
	}

	internal double Manpower(Gang g)
	{
		if (g?.Tiers == null)
		{
			return 1.0;
		}
		int num = g.Tiers.Sum(((string Label, int MinRank, int Seats) t) => t.Seats);
		int num2 = Roster.Count((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead);
		double num3 = ((num <= 0) ? 1.0 : ((double)num2 / (double)num));
		return 0.6 * num3 + 0.4 * Math.Min(1.2, MenShare(g));
	}

	internal double RecruitChance(Gang g)
	{
		if (g == null)
		{
			return 1.0;
		}
		if (IsDestroyed(g))
		{
			return 0.0;
		}
		if (g.OrdersNotFeelings)
		{
			return 1.0;
		}
		return Math.Max(0.05, Math.Min(1.0, 0.1 + 0.9 * Math.Min(1.2, TerritoryShare(g))));
	}

	internal double HoldStrength(Gang g)
	{
		if (g == null)
		{
			return 1.0;
		}
		if (IsDestroyed(g))
		{
			return 0.0;
		}
		if (g.OrdersNotFeelings)
		{
			return 1.0;
		}
		return Math.Max(0.3, Math.Min(1.2, 0.3 + 0.7 * Manpower(g)));
	}

	private List<string> Truces => Chronicle.Truces ?? (Chronicle.Truces = new List<string>());

	private List<string> Alliances => Chronicle.Alliances ?? (Chronicle.Alliances = new List<string>());

	private void PruneDeals()
	{
		long now = GameMinutesNow();
		int count = Truces.Count;
		int count2 = Alliances.Count;
		Truces.RemoveAll((string s) => !Live(s));
		Alliances.RemoveAll((string s) => !Live(s));
		if (Truces.Count != count || Alliances.Count != count2)
		{
			SaveWorld();
		}
		bool Live(string s)
		{
			string[] array = s.Split('|');
			if (long.TryParse(array[array.Length - 1], out var result))
			{
				return result > now;
			}
			return false;
		}
	}

	private bool DealLive(string[] p)
	{
		if (long.TryParse(p[p.Length - 1], out var result))
		{
			return result > GameMinutesNow();
		}
		return false;
	}

	internal bool TruceBetween(string a, string b)
	{
		return Truces.Any(delegate(string s)
		{
			string[] array = s.Split('|');
			return ((array[0] == a && array[1] == b) || (array[0] == b && array[1] == a)) && DealLive(array);
		});
	}

	internal bool AtWarNow(string a, string b)
	{
		if (AtWarWith(a, b) && !TruceBetween(a, b) && !IsDestroyed(Gangs.ById(a)))
		{
			return !IsDestroyed(Gangs.ById(b));
		}
		return false;
	}

	internal IEnumerable<Gang> AlliesAgainst(Gang side, Gang enemy)
	{
		return from s in Alliances
			select s.Split('|') into p
			where p.Length == 4 && p[2] == enemy.Id && (p[0] == side.Id || p[1] == side.Id) && DealLive(p)
			select Gangs.ById((p[0] == side.Id) ? p[1] : p[0]) into g
			where g != null && !IsDestroyed(g)
			select g;
	}

	private void CheckCollapse(Gang g)
	{
		if (g == null || g.OrdersNotFeelings || g.StoryOnly || IsDestroyed(g) || Roster.Any((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead))
		{
			return;
		}
		LeaderSeat leaderSeat = SeatOf(g);
		int num;
		if (leaderSeat != null && leaderSeat.Fallen != null)
		{
			num = ((leaderSeat.Successor == null) ? 1 : 0);
			if (num != 0)
			{
				goto IL_00ad;
			}
		}
		else
		{
			num = 0;
		}
		if (LoreLeaderHolds(g))
		{
			goto IL_00ad;
		}
		int num2 = ((TerritoryShare(g) < 0.05) ? 1 : 0);
		goto IL_00ae;
		IL_00ae:
		bool flag = (byte)num2 != 0;
		if (num == 0 && !flag)
		{
			return;
		}
		if (flag)
		{
			Seats[g.Id] = new LeaderSeat
			{
				Fallen = "went to ground - nobody left to lead",
				OldName = g.LeaderName,
				At = GameMinutesNow()
			};
			ApplyLeaders();
		}
		Destroyed[g.Id] = GameMinutesNow();
		int num3 = 0;
		_quietControl = true;
		try
		{
			District[] districts = Districts;
			foreach (District district in districts)
			{
				int num5 = HoldOf(district, g.Id);
				if (num5 > 0)
				{
					num3 += num5;
					ShiftControl(district, g, -num5, "the outfit is finished");
					KeyValuePair<string, int> keyValuePair = (from kv in ControlMap[district.Key]
						where kv.Key != g.Id && !IsDestroyed(Gangs.ById(kv.Key))
						orderby kv.Value descending
						select kv).FirstOrDefault();
					if (keyValuePair.Key != null)
					{
						ShiftControl(district, Gangs.ById(keyValuePair.Key), num5, "took the " + g.Name + "' ground");
					}
				}
			}
		}
		finally
		{
			_quietControl = false;
		}
		if (num3 > 0)
		{
			GrudgeNotice($"TERRITORY: the {g.Name}' ground ({num3} points) went to the outfits around them.");
		}
		WorldEvent worldEvent = NewEvent("TURMOIL", GameMinutesNow());
		worldEvent.Gangs.Add(g.Id);
		worldEvent.Heard = true;
		worldEvent.Title = "The " + g.Name + " are finished";
		worldEvent.Place = "everywhere they rode";
		worldEvent.Lines.Add($"No leader, no men left standing. {num3} points of ground went to the others.");
		Chronicle.Events.Add(worldEvent);
		SaveWorld();
		Feed("TURMOIL the " + g.Name + " are finished - nobody's left to carry their name", Theme.Gold);
		DeathNotice("The " + g.Name + " are finished.");
		GrudgeLog($"SANDBOX {g.Id} COLLAPSED (no men, no leader) - {num3} hold given away");
		return;
		IL_00ad:
		num2 = 0;
		goto IL_00ae;
	}

	private void MaybeRemnants()
	{
		long num = GameMinutesNow();
		foreach (KeyValuePair<string, long> item in Destroyed.ToList())
		{
			Gang g = Gangs.ById(item.Key);
			if (!Gangs.InEra(g) || g?.Tiers == null || g.Tiers.Length == 0 || g.Seed == null || !g.Seed.Any() || num - item.Value < 7200 || _rng.Next(100) >= 12)
			{
				continue;
			}
			Destroyed.Remove(item.Key);
			List<NemesisRecord> list = new List<NemesisRecord>();
			int tier = Math.Min(1, g.Tiers.Length - 1);
			int tier2 = g.Tiers.Length - 1;
			list.Add(Recruit(g, tier).rec);
			int i = 0;
			for (int num2 = 2 + _rng.Next(3); i < num2; i++)
			{
				list.Add(Recruit(g, tier2).rec);
			}
			District district = (from d in Districts
				where d.Lore.ContainsKey(g.Id)
				orderby d.Lore[g.Id] descending
				select d).FirstOrDefault();
			if (district != null)
			{
				ShiftControl(district, g, 8, "the remnants regroup");
			}
			WorldEvent worldEvent = NewEvent("TURMOIL", num);
			worldEvent.Gangs.Add(g.Id);
			worldEvent.Heard = true;
			worldEvent.Title = "The " + g.Name + " ride again";
			worldEvent.Place = ((district != null) ? ("in " + district.Name) : "out of nowhere");
			worldEvent.Lines.Add($"{list[0].Name} gathered the last of them - {list.Count} men, and a name to rebuild.");
			foreach (NemesisRecord item2 in list)
			{
				worldEvent.People.Add(PartFor(item2, "JOINED", "came back to it"));
			}
			Treasury[g.Id] = 80;
			Chronicle.Events.Add(worldEvent);
			SaveWorld();
			SaveRoster();
			Feed("TURMOIL the " + g.Name + " ride again under " + list[0].Name, g.Accent);
			GrudgeNotice("The " + g.Name + " ride again.");
			GrudgeLog($"SANDBOX {g.Id} REMNANTS regroup: {list.Count} men under {list[0].Name}");
		}
	}

	private WorldEvent SimTurmoil(Ped player, long now)
	{
		PruneDeals();
		List<Gang> outfits = Gangs.All.Where((Gang g) => !g.StoryOnly && !IsDestroyed(g) && Roster.Any((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && !n.PresumedDead)).ToList();
		List<(Gang, Gang)> wars = (from x in outfits
			from y in outfits
			select (x: x, y: y) into t
			where string.CompareOrdinal(t.x.Id, t.y.Id) < 0 && AtWarNow(t.x.Id, t.y.Id)
			select t).ToList();
		List<(string, int)> list = (from kv in TurmoilKinds
			where kv.Value > 0
			select (kind: kv.Key, w: kv.Value)).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		for (int num = 0; num < 6; num++)
		{
			int num2 = _rng.Next(list.Sum<(string, int)>(((string kind, int w) p) => p.w));
			string text = list.Last().Item1;
			foreach (var item in list)
			{
				num2 -= item.Item2;
				if (num2 < 0)
				{
					(text, _) = item;
					break;
				}
			}
			WorldEvent worldEvent = null;
			switch (text)
			{
			case "INCIDENT":
				worldEvent = Incident(outfits, now);
				break;
			case "DEFECTION":
				worldEvent = Defection(wars, now);
				break;
			case "ASSASSINATION":
				worldEvent = Assassination(wars, now);
				break;
			case "TRUCE":
				worldEvent = Truce(wars, now);
				break;
			case "ALLIANCE":
				worldEvent = Alliance(outfits, now);
				break;
			case "CRACKDOWN":
				worldEvent = Crackdown(outfits, now);
				break;
			case "FEVER":
				worldEvent = Fever(outfits, now);
				break;
			case "WINDFALL":
				worldEvent = Windfall(outfits, now);
				break;
			}
			if (worldEvent != null)
			{
				GrudgeLog("SANDBOX " + text + ": " + worldEvent.Title);
				return worldEvent;
			}
		}
		return null;
	}

	private (Gang x, Gang y)? AnyWar(List<(Gang x, Gang y)> wars)
	{
		if (wars.Count != 0)
		{
			return wars[_rng.Next(wars.Count)];
		}
		return null;
	}

	private WorldEvent Defection(List<(Gang x, Gang y)> wars, long now)
	{
		(Gang, Gang)? tuple = AnyWar(wars.Where(((Gang x, Gang y) p) => !Closed(p.x) && !Closed(p.y)).ToList());
		if (!tuple.HasValue)
		{
			return null;
		}
		Gang from;
		Gang gang;
		if (_rng.Next(2) != 0)
		{
			Gang item = tuple.Value.Item2;
			Gang item2 = tuple.Value.Item1;
			from = item2;
			gang = item;
		}
		else
		{
			Gang item3 = tuple.Value.Item1;
			Gang item2 = tuple.Value.Item2;
			from = item2;
			gang = item3;
		}
		string succ = SuccessorId(from);
		NemesisRecord man = (from n in Roster.Where(delegate(NemesisRecord n)
			{
				if (n.Gang == @from.Id && n.IsAlive && !n.PresumedDead && OffStage(n) && n.Id != succ && !LoreProtected(n))
				{
					List<HistoryEntry> history = n.History;
					if (history == null || !history.Any((HistoryEntry h) => h.Text != null && h.Text.StartsWith("DEFECTED")))
					{
						if (Mind.F(n, "Loyalty") >= 45 && !Quirks.Has(n, "traitor"))
						{
							return n.Ties?.Any((Tie t) => t.Type == "rival") ?? false;
						}
						return true;
					}
				}
				return false;
			})
			orderby Mind.F(n, "Loyalty")
			select n).ThenBy((NemesisRecord _) => _rng.Next()).FirstOrDefault();
		if (man == null)
		{
			return null;
		}
		int rank = man.Rank;
		bool campKnown = man.CampKnown;
		int num = Plans.Count((Plan p) => p.Kind == "habit" && !p.Done && p.Known && p.LeaderId == man.Id);
		man.Gang = gang.Id;
		man.Rank = Math.Max(1, man.Rank - 1);
		man.BossId = null;
		man.CampId = null;
		man.CampKnown = false;
		DressForOutfit(man, gang);
		NewWeekFor(man);
		if (campKnown || num > 0)
		{
			ReplyNotice("Word is " + man.Name + " has gone over to the " + gang.Name + ". " + ((campKnown && num > 0) ? "His old camp and his habits" : (campKnown ? "His old camp" : "His habits")) + " no longer hold.");
		}
		ShiftRelation(from.Id, gang.Id, -10, man.Name + " defected to the " + gang.Name);
		WorldEvent worldEvent = NewEvent("TURMOIL", now);
		worldEvent.Gangs.Add(from.Id);
		worldEvent.Gangs.Add(gang.Id);
		worldEvent.Title = man.Name + " turned his coat - from the " + from.Name + " to the " + gang.Name;
		worldEvent.Place = "in the night";
		WorldPart worldPart = PartFor(man, "DEFECTED", $"rank {rank} -> {man.Rank}");
		worldPart.Effects.Add("now rides with the " + gang.Name);
		worldEvent.People.Add(worldPart);
		History(man, "DEFECTED from the " + from.Name + " to the " + gang.Name, Theme.Warn);
		Remember(man, "Shame", 40, "turned on the " + from.Name, null);
		foreach (NemesisRecord item4 in Roster.Where(delegate(NemesisRecord n)
		{
			if (n.Gang == from.Id && n.IsAlive)
			{
				List<string> friendIds = n.FriendIds;
				if ((friendIds == null || !friendIds.Contains(man.Id)) && !(n.KinId == man.Id))
				{
					return man.KinId == n.Id;
				}
				return true;
			}
			return false;
		}))
		{
			Remember(item4, "Anger", 70, man.Name + " turned traitor", man.Id);
			Mind.SetTie(item4, man.Id, "enemy", -70, "turned traitor");
			worldEvent.People.Add(PartFor(item4, "BETRAYED", man.Name + " turned on them"));
		}
		SaveRoster();
		return worldEvent;
		static bool Closed(Gang g)
		{
			if (g != null && !g.OrdersNotFeelings && !g.SquadsOnly && !g.KeepsToItsOwn)
			{
				return g.OwnLives;
			}
			return true;
		}
	}

	private WorldEvent Assassination(List<(Gang x, Gang y)> wars, long now)
	{
		(Gang, Gang)? tuple = AnyWar(wars);
		if (!tuple.HasValue)
		{
			return null;
		}
		Gang target;
		Gang gang;
		if (_rng.Next(2) != 0)
		{
			Gang item = tuple.Value.Item2;
			Gang item2 = tuple.Value.Item1;
			target = item2;
			gang = item;
		}
		else
		{
			Gang item3 = tuple.Value.Item1;
			Gang item2 = tuple.Value.Item2;
			target = item2;
			gang = item3;
		}
		WorldEvent worldEvent = NewEvent("TURMOIL", now);
		worldEvent.Gangs.Add(gang.Id);
		worldEvent.Gangs.Add(target.Id);
		ShiftRelation(gang.Id, target.Id, -20, "the " + gang.Name + " sent killers after the " + target.Name);
		LeaderSeat seat = SeatOf(target);
		bool num = seat == null || seat.Fallen == null;
		WarRecord warRecord = LiveWar(gang.Id, target.Id);
		bool flag = warRecord != null && now - warRecord.StartMin >= 4320;
		NemesisRecord nemesisRecord = ((seat?.Successor != null && seat.Fallen == null) ? Roster.FirstOrDefault((NemesisRecord n) => n.Id == seat.Successor && IsLeaderCopy(n)) : null);
		if (num && flag && (seat?.Successor == null || (nemesisRecord != null && OffStage(nemesisRecord))) && !LoreLeaderHolds(target) && _rng.Next(100) < 18)
		{
			string leaderName = target.LeaderName;
			bool ordersNotFeelings = gang.OrdersNotFeelings;
			if (nemesisRecord != null)
			{
				nemesisRecord.IsAlive = false;
				nemesisRecord.PresumedDead = false;
				nemesisRecord.KilledByPlayer = false;
				if (ordersNotFeelings)
				{
					nemesisRecord.Fate = "Arrested";
				}
				History(nemesisRecord, ordersNotFeelings ? ("ARRESTED by the " + gang.Name + " in a raid at dawn") : ("KILLED by the " + gang.Name + " in an ambush"), Theme.Blood);
				SaveRoster();
			}
			Seats[target.Id] = new LeaderSeat
			{
				Fallen = (ordersNotFeelings ? ("arrested by the " + gang.Name) : ("killed by the " + gang.Name)),
				OldName = leaderName,
				At = now
			};
			ApplyLeaders();
			worldEvent.Title = (ordersNotFeelings ? ("The " + gang.Name + " arrested " + leaderName + ", who led the " + target.Name) : ("The " + gang.Name + " killed " + leaderName + ", who led the " + target.Name));
			worldEvent.Place = (ordersNotFeelings ? "in a raid at dawn" : "in an ambush");
			worldEvent.Lines.Add("The seat stands empty - the shake-up will see who takes it, if anyone.");
			foreach (District item4 in Districts.Where((District d) => HoldOf(d, target.Id) > 0))
			{
				ShiftControl(item4, target, -LossDown(item4, target, 4), "their leader was killed");
			}
			if (ordersNotFeelings)
			{
				GrudgeNotice(leaderName + " is in irons - the " + gang.Name + " took him.");
			}
			else
			{
				DeathNotice(leaderName + " is dead - the " + gang.Name + " got him.");
			}
			return worldEvent;
		}
		NemesisRecord nemesisRecord2 = (from n in Roster
			where n.Gang == target.Id && n.IsAlive && !n.PresumedDead && OffStage(n) && n.Rank >= 3 && !LoreProtected(n)
			orderby n.Rank descending
			select n).ThenBy((NemesisRecord _) => _rng.Next()).FirstOrDefault();
		if (nemesisRecord2 == null)
		{
			return null;
		}
		WorldPart worldPart = new WorldPart
		{
			Id = nemesisRecord2.Id,
			Name = nemesisRecord2.Name,
			Gang = nemesisRecord2.Gang
		};
		if (gang.OrdersNotFeelings && _rng.Next(100) < 60)
		{
			worldEvent.Title = "The " + gang.Name + " arrested " + nemesisRecord2.Name + " of the " + target.Name;
			worldEvent.Place = "in a raid";
			int num2 = 2 + _rng.Next(3);
			nemesisRecord2.RestUntil = Math.Max(nemesisRecord2.RestUntil, now + (long)num2 * 1440L);
			AddThought(nemesisRecord2, "Jailed", "held in the Army stockade", -3, num2 * 24, now, worldPart);
			worldPart.Outcome = "ARRESTED";
			worldPart.Effects.Add($"in the stockade {num2} days");
			History(nemesisRecord2, $"ARRESTED by the {gang.Name} - {num2} days in the stockade", Theme.Warn);
			worldEvent.People.Add(worldPart);
			return worldEvent;
		}
		worldEvent.Title = "The " + gang.Name + " went after " + nemesisRecord2.Name + " of the " + target.Name;
		worldEvent.Place = "on a lonely road";
		if (_rng.Next(100) < 55)
		{
			OffscreenDeath(nemesisRecord2, worldEvent, worldPart, "the " + gang.Name, gun: true);
		}
		else
		{
			OffscreenWound(nemesisRecord2, worldEvent, worldPart, "the " + gang.Name, gun: true);
		}
		worldEvent.People.Add(worldPart);
		return worldEvent;
	}

	private WorldEvent Truce(List<(Gang x, Gang y)> wars, long now)
	{
		(Gang, Gang)? tuple = AnyWar(wars.Where(((Gang x, Gang y) p) => !p.x.OrdersNotFeelings && !p.y.OrdersNotFeelings && !Young(p.x, p.y)).ToList());
		if (!tuple.HasValue)
		{
			return null;
		}
		int num = 3 + _rng.Next(4);
		Truces.Add($"{tuple.Value.Item1.Id}|{tuple.Value.Item2.Id}|{now + (long)num * 1440L}");
		SaveWorld();
		_sidedAgainst.Clear();
		ShiftRelation(tuple.Value.Item1.Id, tuple.Value.Item2.Id, 25, "a truce");
		WorldEvent worldEvent = NewEvent("TURMOIL", now);
		worldEvent.Gangs.Add(tuple.Value.Item1.Id);
		worldEvent.Gangs.Add(tuple.Value.Item2.Id);
		worldEvent.Title = "The " + tuple.Value.Item1.Name + " and the " + tuple.Value.Item2.Name + " call a truce";
		worldEvent.Place = "";
		worldEvent.Lines.Add($"No fighting between them for {num} days - or that's the word. Nobody trusts it.");
		return worldEvent;
		bool Young(Gang x, Gang y)
		{
			return Wars.Any((WarRecord rw) => rw.Result == null && ((rw.A == x.Id && rw.B == y.Id) || (rw.A == y.Id && rw.B == x.Id)) && now - rw.StartMin < 4320);
		}
	}

	private static bool AllyClosed(Gang g)
	{
		if (g != null && !g.OrdersNotFeelings && !g.SquadsOnly && !g.KeepsToItsOwn)
		{
			return g.OwnLives;
		}
		return true;
	}

	private bool SharedFront(Gang x, Gang y, Gang enemy)
	{
		return Districts.Any((District d) => HoldOf(d, enemy.Id) >= 5 && HoldOf(d, x.Id) >= 3 && HoldOf(d, y.Id) >= 3);
	}

	private WorldEvent Alliance(List<Gang> outfits, long now)
	{
		foreach (Gang item in outfits.OrderBy((Gang _) => _rng.Next()))
		{
			Gang enemy = item;
			List<Gang> foes = outfits.Where((Gang g) => g != enemy && !g.OrdersNotFeelings && AtWarNow(g.Id, enemy.Id)).ToList();
			(Gang, Gang) tuple = (from x in foes
				from y in foes
				select (x: x, y: y) into _
				where string.CompareOrdinal(_.x.Id, _.y.Id) < 0 && !AtWarNow(_.x.Id, _.y.Id) && !AllyClosed(_.x) && !AllyClosed(_.y) && Relation(_.x.Id, _.y.Id) > -20 && SharedFront(_.x, _.y, enemy) && !Allied(_.x, _.y)
				orderby _rng.Next()
				select _).FirstOrDefault();
			if (tuple.Item1 != null)
			{
				int num = 5 + _rng.Next(4);
				Alliances.Add($"{tuple.Item1.Id}|{tuple.Item2.Id}|{enemy.Id}|{now + (long)num * 1440L}");
				SaveWorld();
				ShiftRelation(tuple.Item1.Id, tuple.Item2.Id, 12, "an alliance against the " + enemy.Name);
				ShiftRelation(tuple.Item1.Id, enemy.Id, -5, "the " + tuple.Item1.Name + " joined against them");
				ShiftRelation(tuple.Item2.Id, enemy.Id, -5, "the " + tuple.Item2.Name + " joined against them");
				WorldEvent worldEvent = NewEvent("TURMOIL", now);
				worldEvent.Gangs.Add(tuple.Item1.Id);
				worldEvent.Gangs.Add(tuple.Item2.Id);
				worldEvent.Gangs.Add(enemy.Id);
				worldEvent.Title = "The " + tuple.Item1.Name + " and the " + tuple.Item2.Name + " join against the " + enemy.Name;
				worldEvent.Place = "an uneasy deal";
				worldEvent.Lines.Add($"For {num} days their men ride together when the {enemy.Name} are the target.");
				return worldEvent;
			}
			bool Allied(Gang x, Gang y)
			{
				return Alliances.Select((string s) => s.Split('|')).Any((string[] p) => p.Length == 4 && p[2] == enemy.Id && ((p[0] == x.Id && p[1] == y.Id) || (p[0] == y.Id && p[1] == x.Id)) && DealLive(p));
			}
		}
		return null;
	}

	private WorldEvent Crackdown(List<Gang> outfits, long now)
	{
		Gang army = Gangs.Army;
		List<Gang> list = outfits.Where((Gang g) => g != army && !g.OrdersNotFeelings && g != Gangs.Wapiti && Districts.Sum((District d) => HoldOf(d, g.Id)) > 0).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		Gang target = null;
		double num = list.Sum((Gang g) => W(g));
		double num2 = _rng.NextDouble() * num;
		foreach (Gang item in list)
		{
			num2 -= W(item);
			if (num2 <= 0.0)
			{
				target = item;
				break;
			}
		}
		if (target == null)
		{
			target = list.Last();
		}
		_lastCrackdown = target.Id;
		List<District> list2 = Districts.Where((District d) => HoldOf(d, target.Id) > 0).ToList();
		District district = null;
		double num3 = _rng.NextDouble() * ((IEnumerable<District>)list2).Sum((Func<District, double>)((District d) => HoldOf(d, target.Id)));
		foreach (District item2 in list2)
		{
			num3 -= (double)HoldOf(item2, target.Id);
			if (num3 <= 0.0)
			{
				district = item2;
				break;
			}
		}
		if (district == null)
		{
			district = list2.LastOrDefault();
		}
		if (district == null || HoldOf(district, target.Id) <= 0)
		{
			return null;
		}
		WorldEvent worldEvent = NewEvent("TURMOIL", now);
		worldEvent.Gangs.Add(army.Id);
		worldEvent.Gangs.Add(target.Id);
		worldEvent.Title = "The Army cracked down on the " + target.Name + " in " + district.Name;
		worldEvent.Place = "in " + district.Name;
		Vector3 p = new Vector3(district.X, district.Y, 100f);
		for (int num4 = 0; num4 < 8; num4++)
		{
			if (!InsideAnyTown(p))
			{
				break;
			}
			double num5 = _rng.NextDouble() * Math.PI * 2.0;
			p = new Vector3(district.X + (float)Math.Cos(num5) * 400f, district.Y + (float)Math.Sin(num5) * 400f, 100f);
		}
		worldEvent.HasPos = true;
		worldEvent.X = p.X;
		worldEvent.Y = p.Y;
		worldEvent.Z = 100f;
		foreach (NemesisRecord item3 in (from _ in Roster
			where _.Gang == target.Id && _.IsAlive && !_.PresumedDead && OffStage(_) && _.Rank < 4
			orderby _rng.Next()
			select _).Take(1 + _rng.Next(3)).ToList())
		{
			ResolveFighter(item3, won: false, worldEvent, "the Army", gun: true);
		}
		worldEvent.UnnamedDead[target.Id] = 2 + _rng.Next(4);
		ShiftControl(district, target, -LossDown(district, target, 10), "an Army crackdown");
		ShiftControl(district, army, 4, "a crackdown");
		if (worldEvent.People.Any((WorldPart worldPart) => worldPart.Outcome == "KILLED") || worldEvent.UnnamedDead.Count > 0)
		{
			worldEvent.AftermathUntil = now + (long)Config.WorldAftermathHours * 60L;
		}
		return worldEvent;
		double W(Gang g)
		{
			return (double)Districts.Sum((District d) => HoldOf(d, g.Id)) * ((g.Id == _lastCrackdown) ? 0.4 : 1.0) * TargetWeight(g);
		}
	}

	private WorldEvent Fever(List<Gang> outfits, long now)
	{
		Gang g = (from _ in outfits
			where !_.OrdersNotFeelings
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (g == null)
		{
			return null;
		}
		List<NemesisRecord> list = (from _ in Roster
			where _.Gang == g.Id && _.IsAlive && !_.PresumedDead && OffStage(_)
			orderby _rng.Next()
			select _).Take(2 + _rng.Next(3)).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		WorldEvent worldEvent = NewEvent("TURMOIL", now);
		worldEvent.Gangs.Add(g.Id);
		worldEvent.Title = "Fever runs through the " + g.Name;
		worldEvent.Place = "in camp";
		foreach (NemesisRecord item in list)
		{
			WorldPart worldPart = PartFor(item, "SICK", "laid up with the fever");
			if (_rng.Next(100) < 12 && !LoreProtected(item) && item.Id != SuccessorId(g))
			{
				item.IsAlive = false;
				item.PresumedDead = false;
				worldPart.Outcome = "KILLED";
				worldPart.Note = "the fever took him";
				History(item, "DIED of the fever", Theme.Blood);
				QueueDeathNews(item, byYou: false, "the fever", worldEvent, null);
			}
			else
			{
				item.RestUntil = Math.Max(item.RestUntil, now + 2160);
				AddThought(item, "Sick", "the fever", -3, 48, now, worldPart);
			}
			worldEvent.People.Add(worldPart);
		}
		SaveRoster();
		return worldEvent;
	}

	private WorldEvent Windfall(List<Gang> outfits, long now)
	{
		Gang g = (from _ in outfits
			where !_.OrdersNotFeelings
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (g == null)
		{
			return null;
		}
		District district = (from _ in Districts
			where HoldOf(_, g.Id) > 0 || _.Lore.ContainsKey(g.Id)
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (district == null)
		{
			return null;
		}
		NemesisRecord nemesisRecord = (from n in Roster
			where n.Gang == g.Id && n.IsAlive && !n.PresumedDead && OffStage(n)
			orderby n.Rank descending
			select n).ThenBy((NemesisRecord _) => _rng.Next()).FirstOrDefault();
		WorldEvent worldEvent = NewEvent("TURMOIL", now);
		worldEvent.Gangs.Add(g.Id);
		string[] array = new string[6] { "a payroll wagon", "a bank in a small town", "a rich man's ranch", "a riverboat's safe", "a train's mail car", "a cattle drive" };
		worldEvent.Title = "The " + g.Name + " took " + array[_rng.Next(array.Length)];
		worldEvent.Place = "near " + district.Name;
		worldEvent.Lines.Add("Money buys men and guns - they hold more of the country now.");
		ShiftControl(district, g, 6, "a big score");
		AddPurse(g, 150 + _rng.Next(151), "a big score");
		if (nemesisRecord != null)
		{
			WorldPart worldPart = PartFor(nemesisRecord, "PROUD", "led the job");
			AddThought(nemesisRecord, "Proud", "pulled off a big score", 3, 72, now, worldPart);
			GainRankXp(nemesisRecord, 2, "a big score");
			worldEvent.People.Add(worldPart);
		}
		return worldEvent;
	}

	internal string SandboxLine()
	{
		PruneDeals();
		IEnumerable<string> values = from g in Gangs.All
			where !g.StoryOnly
			select (!IsDestroyed(g)) ? $"{g.Name} {TerritoryShare(g) * 100.0:0}% ground {Manpower(g) * 100.0:0}% men (recruits {RecruitChance(g) * 100.0:0}%)" : (g.Name + " FINISHED");
		string text = ((Truces.Count > 0) ? (" | truces: " + string.Join(", ", from t in Truces
			select t.Split('|') into p
			select p[0] + "-" + p[1])) : "") + ((Alliances.Count > 0) ? (" | alliances: " + string.Join(", ", from t in Alliances
			select t.Split('|') into p
			select p[0] + "+" + p[1] + " vs " + p[2])) : "");
		return "strength: " + string.Join("; ", values) + text;
	}
}
