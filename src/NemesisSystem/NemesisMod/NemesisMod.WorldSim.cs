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
	private readonly List<WorldEvent> _catchUpEvents = new List<WorldEvent>();

	private static readonly string[] FeudCauses = new string[7] { "over a hand of cards", "over a woman", "over a horse", "over his share of a job", "over an insult", "over who gives the orders", "over a bottle" };

	private static readonly string[] DuelTitles = new string[8] { "{n} called out {o} in the street", "{n} and {o} stepped out into the road at noon", "{n} faced {o} outside the saloon", "{n} met {o} at dawn behind the livery", "{n} told {o} to draw", "{n} and {o} counted their paces in {t}", "{n} drew on {o} over a marked deck", "{n} shot it out with {o} by the train depot" };

	private static readonly string[] KnifeTitles = new string[6] { "{n} pulled a knife on {o} in the saloon", "{n} cut {o} over a game of dominoes", "{n} went at {o} with a blade in an alley", "{n} and {o} drew knives over a woman", "{n} slashed {o} outside the general store", "{n} put a knife to {o}'s throat at the bar" };

	private static readonly string[] BrawlTitles = new string[8] { "{n} started a saloon brawl", "{n} threw the first punch in the saloon", "{n} broke a chair over {o}'s back", "{n} and {o} went through the saloon window", "{n} knocked {o} cold at the bar", "{n} started a fight over a spilled drink", "{n} took on three men at once in the saloon", "{n} brawled with {o} in the mud outside the stables" };

	private readonly Dictionary<string, string> _lastTitle = new Dictionary<string, string>();

	private List<WorldEvent> _chronCache;

	private (int filter, bool all, int count, int past) _chronKey;

	private int _chronAt = -100000;

	private readonly Dictionary<string, int> _aftermathBlips = new Dictionary<string, int>();

	private readonly List<Ped> _aftermathPeds = new List<Ped>();

	private void UpdateWorldSim(Ped player)
	{
		if (!Config.WorldEvents || Game.GameTime < _nextWorldCheck)
		{
			return;
		}
		_nextWorldCheck = Game.GameTime + 2000;
		FadeWitnessFear();
		if (_menu.IsOpen || _bulletImpact.ViewerOpen)
		{
			return;
		}
		long num = GameMinutesNow();
		if (num <= 0)
		{
			return;
		}
		WorldState chronicle = Chronicle;
		long num2 = (long)Config.WorldMaxHours * 60L;
		if (chronicle.NextEventAt == 0L || chronicle.NextEventAt - num > num2 * 2)
		{
			chronicle.NextEventAt = num + NextWorldGap();
			SaveWorld();
		}
		else
		{
			if (num < chronicle.NextEventAt)
			{
				return;
			}
			long num3 = num - chronicle.NextEventAt;
			bool flag = num3 >= 60 || _catchUpHours > 0;
			if (flag && (WorldBusy(player) || Game.GameTime - _loadedAt < 15000 || Game.GameTime - _quietEndedAt < 5000))
			{
				return;
			}
			if (num3 > (long)Config.WorldCatchUpMax * 60L)
			{
				chronicle.NextEventAt = num - (long)(Config.WorldCatchUpMax - 1) * 60L;
			}
			HashSet<WorldEvent> had = new HashSet<WorldEvent>(Chronicle.Events);
			int num4 = 0;
			_worldCatchingUp = flag;
			try
			{
				Crumbs.Mark(flag ? "world catch-up" : "world batch");
				while (chronicle.NextEventAt <= num && num4 < (flag ? 3 : Config.WorldCatchUpMax))
				{
					RunWorldBatch(player, chronicle.NextEventAt);
					num4++;
					chronicle.NextEventAt += NextWorldGap();
				}
			}
			finally
			{
				_worldCatchingUp = false;
			}
			if (flag)
			{
				_catchUpHours += num4;
				_catchUpEvents.AddRange(Chronicle.Events.Where((WorldEvent e) => !had.Contains(e)));
				if (chronicle.NextEventAt <= num)
				{
					SaveWorld();
					return;
				}
				List<WorldEvent> list = _catchUpEvents.ToList();
				int catchUpHours = _catchUpHours;
				_catchUpEvents.Clear();
				_catchUpHours = 0;
				int num5 = list.Sum((WorldEvent e) => e.People.Count((WorldPart p) => p.Outcome == "KILLED"));
				GrudgeLog($"WORLD caught up {catchUpHours} hours: {list.Count} events, {num5} named dead");
				Feed(string.Format("WORLD while you were away: {0} events over {1} hours{2}", list.Count, catchUpHours, (num5 > 0) ? $", {num5} named dead" : ""), Theme.Gold);
				string text = string.Format("While you were away: {0} things happened{1}. See the World tab.", list.Count, (num5 > 0) ? $" - {num5} named men dead" : "");
				if (Config.WorldNews >= ((num5 > 0) ? 1 : 2))
				{
					WorldNotice(text);
				}
			}
			if (chronicle.NextEventAt <= num)
			{
				chronicle.NextEventAt = num + NextWorldGap();
			}
			SaveRoster();
			SaveWorld();
		}
	}

	private static DateTime FromGameMinutes(long m)
	{
		return GameDate.FromMinutes(m);
	}

	private long NextWorldGap()
	{
		return Math.Max(25L, (long)(60.0 * ((double)Config.WorldMinHours + _rng.NextDouble() * (double)Math.Max(0, Config.WorldMaxHours - Config.WorldMinHours))) + _rng.Next(-20, 21));
	}

	internal string NextWorldEventText()
	{
		long num = Chronicle.NextEventAt - GameMinutesNow();
		if (num > 0)
		{
			if (num < 60)
			{
				return $"{num}m";
			}
			return $"{num / 60}h {num % 60:00}m";
		}
		return "soon";
	}

	private void RunWorldBatch(Ped player, long now)
	{
		int num = ((_rng.Next(100) < Config.WorldBigBatchChance) ? (Config.WorldBigBatchMin + _rng.Next(Math.Max(1, Config.WorldBigBatchMax - Config.WorldBigBatchMin + 1))) : (Config.WorldBatchMin + _rng.Next(Math.Max(1, Config.WorldBatchMax - Config.WorldBatchMin + 1))));
		bool flag = false;
		DateTime d = FromGameMinutes(now);
		int day = GameDate.DayKey(d);
		int num2 = Chronicle.Events.Count((WorldEvent e) => e.Day == day && (e.Kind == "CLASH" || e.Kind == "CAMP OVERRUN"));
		int count = Chronicle.Events.Count;
		for (int num3 = 0; num3 < num; num3++)
		{
			string text = PickWorldKind();
			if (text == "CLASH" && (flag || num2 >= Config.WorldMaxClashesPerDay))
			{
				text = "LIFE";
			}
			if (text == "CLASH")
			{
				flag = true;
			}
			RunWorldEvent(player, now - ((num3 == 0) ? _rng.Next(0, 6) : _rng.Next(0, 50)), text);
		}
		try
		{
			WarBattles(player, now);
		}
		catch (Exception ex)
		{
			GrudgeLog("WAR battles failed: " + ex.Message);
		}
		if (Chronicle.Events.Count > count)
		{
			int num4 = Math.Max(0, count - 8);
			List<WorldEvent> list = (from e in Chronicle.Events.Skip(num4)
				orderby e.GameMin
				select e).ToList();
			Chronicle.Events.RemoveRange(num4, list.Count);
			Chronicle.Events.AddRange(list);
		}
	}

	private string PickWorldKind()
	{
		int turmoilWeight = TurmoilWeight;
		int val = Config.WorldClashWeight + Config.WorldFeudWeight + Config.WorldBrawlWeight + Config.WorldLifeWeight + Config.WorldRivalryWeight + Config.WorldVdlWeight + Config.WorldWordWeight + turmoilWeight;
		int num = _rng.Next(Math.Max(1, val));
		if ((num -= turmoilWeight) < 0)
		{
			return "TURMOIL";
		}
		if ((num -= Config.WorldWordWeight) < 0)
		{
			return "WORD";
		}
		if ((num -= Config.WorldClashWeight) < 0)
		{
			return "CLASH";
		}
		if ((num -= Config.WorldRivalryWeight) < 0)
		{
			return "RIVALRY";
		}
		if ((num -= Config.WorldVdlWeight) < 0)
		{
			return "VDL";
		}
		if ((num -= Config.WorldFeudWeight) < 0)
		{
			return "FEUD";
		}
		if ((num -= Config.WorldBrawlWeight) < 0)
		{
			return "BRAWL";
		}
		return "LIFE";
	}

	internal void SimulateWorldEventNow()
	{
		RunWorldBatch(Game.Player.Character, GameMinutesNow());
		SaveRoster();
		SaveWorld();
	}

	private void RunWorldEvent(Ped player, long now, string force)
	{
		_historyAt = now;
		try
		{
			RunWorldEventAt(player, now, force);
		}
		finally
		{
			_historyAt = null;
		}
	}

	private void RunWorldEventAt(Ped player, long now, string force)
	{
		string text = force ?? PickWorldKind();
		WorldEvent worldEvent = null;
		try
		{
			switch (text)
			{
			case "CLASH":
				worldEvent = SimClash(player, now);
				break;
			case "FEUD":
				worldEvent = SimFeud(now);
				break;
			case "BRAWL":
				worldEvent = SimBrawl(player, now);
				break;
			case "RIVALRY":
				worldEvent = SimRivalry(now);
				break;
			case "VDL":
				worldEvent = SimVanDerLinde(now);
				break;
			case "WORD":
				worldEvent = SimWord(now);
				break;
			case "TURMOIL":
				worldEvent = SimTurmoil(player, now);
				break;
			}
			if (worldEvent == null)
			{
				worldEvent = SimLife(now);
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("WORLD event failed: " + ex);
		}
		if (worldEvent == null)
		{
			return;
		}
		worldEvent.Heard = WordReachesYou(worldEvent, player);
		if (!Chronicle.Events.Contains(worldEvent))
		{
			Chronicle.Events.Add(worldEvent);
		}
		try
		{
			NoteClashForWars(worldEvent);
		}
		catch (Exception ex2)
		{
			GrudgeLog("RELATION clash note failed: " + ex2.Message);
		}
		int num = worldEvent.People.Count((WorldPart p) => p.Outcome == "KILLED");
		GrudgeLog("WORLD " + worldEvent.Kind + " " + worldEvent.Title + ((string.IsNullOrEmpty(worldEvent.Place) || (worldEvent.Title != null && worldEvent.Title.Contains(worldEvent.Place))) ? "" : (" " + ((worldEvent.Place != null && (worldEvent.Place.StartsWith("in ") || worldEvent.Place.StartsWith("at ") || worldEvent.Place.StartsWith("near ") || worldEvent.Place.StartsWith("outside "))) ? worldEvent.Place : ("at " + worldEvent.Place)))) + ((worldEvent.People.Count == 0) ? "" : (": " + string.Join("; ", worldEvent.People.Select((WorldPart p) => p.Name + " " + p.Outcome + (p.SecretlyLived ? " (lived)" : ""))))) + ((worldEvent.UnnamedDead.Count > 0) ? (" | unnamed dead " + string.Join(", ", worldEvent.UnnamedDead.Select((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value))) : "") + $" | heard {worldEvent.Heard}");
		Feed("WORLD " + worldEvent.Kind + ": " + worldEvent.Title + ((worldEvent.Place != null) ? (" - " + worldEvent.Place) : "") + ((num > 0) ? $" ({num} dead)" : ""), KindColor(worldEvent.Kind));
		if (!_worldCatchingUp && worldEvent.Heard && worldEvent.Kind != "LIFE" && Config.MessageLevel >= 2)
		{
			UiFeed.Toast((worldEvent.Kind == "VDL") ? "Van der Linde" : ((worldEvent.Kind == "JOB") ? "Word on the trail" : ((worldEvent.Kind == "RIVALRY") ? "Rivals" : "Word on the trail")), worldEvent.Title);
		}
		if (_worldCatchingUp || !worldEvent.Heard)
		{
			return;
		}
		int worldNews = Config.WorldNews;
		string text2 = TitleWithPlace(worldEvent);
		if (num > 0 && worldNews >= 1)
		{
			WorldNotice(text2 + ". " + string.Join(", ", from p in worldEvent.People
				where p.Outcome == "KILLED"
				select p.Name) + " dead.");
		}
		else if (num == 0 && ((worldEvent.Kind != "LIFE" && worldNews >= 2) || (worldEvent.Kind == "LIFE" && worldNews >= 3)))
		{
			WorldNotice(text2 + ".");
		}
	}

	private bool WordReachesYou(WorldEvent ev, Ped player)
	{
		if (ev.HasPos && player != null && new Vector3(ev.X, ev.Y, ev.Z).DistanceTo(player.Position) < 1500f)
		{
			return true;
		}
		foreach (WorldPart p in ev.People)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Id);
			if (nemesisRecord != null && (nemesisRecord.TimesAmbushed > 0 || nemesisRecord.PersonalityKnown || nemesisRecord.TimesInterrogated > 0 || nemesisRecord.Origin != GrudgeOrigin.GangMember))
			{
				return true;
			}
		}
		return _rng.Next(100) < 35;
	}

	internal static string TitleWithPlace(WorldEvent ev)
	{
		if (!string.IsNullOrEmpty(ev.Place) && (ev.Title ?? "").IndexOf(ev.Place, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return ev.Title + " " + ev.Place;
		}
		return ev.Title;
	}

	internal static string LoreNow(string s)
	{
		if (string.IsNullOrEmpty(s) || !s.Contains("Favours"))
		{
			return s;
		}
		string text = Gangs.Army?.LeaderName;
		if (text == null || text.Contains("Favours"))
		{
			return s;
		}
		string newValue = (text.Contains(' ') ? text.Substring(text.LastIndexOf(' ') + 1) : text);
		return s.Replace("Colonel Favours", text).Replace("Favours", newValue);
	}

	internal static string KindLabel(string kind)
	{
		object obj;
		switch (kind)
		{
		default:
			obj = kind;
			if (obj == null)
			{
				return "";
			}
			break;
		case "YOU":
			return "YOUR DOING";
		case "TURMOIL":
			return "UPHEAVAL";
		case "WORD":
			return "RUMOUR";
		case "VDL":
			return "THE GANG";
		case "ERA":
			obj = "EIGHT YEARS LATER";
			break;
		}
		return (string)obj;
	}

	internal static (int r, int g, int b) KindColor(string kind)
	{
		switch (kind)
		{
		default:
			return (r: 170, g: 180, b: 150);
		case "DUTY":
			return (r: 140, g: 150, b: 110);
		case "CRIME":
			return (r: 214, g: 150, b: 80);
		case "RAID":
			return (r: 200, g: 96, b: 80);
		case "LORE":
			return (r: 196, g: 170, b: 120);
		case "VDL":
			return (r: 170, g: 140, b: 110);
		case "WORD":
			return (r: 150, g: 170, b: 210);
		case "RIVALRY":
			return (r: 205, g: 120, b: 90);
		case "JOB":
			return (r: 200, g: 160, b: 90);
		case "WAR":
			return (r: 214, g: 72, b: 52);
		case "RANKS":
			return (r: 170, g: 150, b: 220);
		case "WITNESSED":
			return (r: 110, g: 160, b: 210);
		case "YOU":
			return (r: 215, g: 178, b: 95);
		case "FEUD":
		case "BRAWL":
		case "DUEL":
			return Theme.Warn;
		case "CLASH":
		case "CAMP OVERRUN":
			return Theme.Blood;
		case "ERA":
			return Theme.Gold;
		}
	}

	internal WorldEvent ChronicleDeed(string tag, string title, string place, Vector3? pos, string gangId, params WorldPart[] parts)
	{
		if (!Config.WorldEvents)
		{
			return null;
		}
		long num = GameMinutesNow();
		WorldState chronicle = Chronicle;
		if ((tag == "kill" || tag == "nemesis") && pos.HasValue)
		{
			int num2 = ((tag == "kill") ? 20 : 90);
			WorldEvent worldEvent = chronicle.Events.Skip(Math.Max(0, chronicle.Events.Count - 30)).LastOrDefault((WorldEvent e) => e.Tag == tag);
			if (worldEvent != null && num - worldEvent.GameMin <= num2 && num >= worldEvent.GameMin && worldEvent.HasPos && new Vector3(worldEvent.X, worldEvent.Y, worldEvent.Z).DistanceTo(pos.Value) < 400f)
			{
				foreach (WorldPart p in parts)
				{
					if (!worldEvent.People.Any((WorldPart x) => x.Id != null && x.Id == p.Id))
					{
						worldEvent.People.Add(p);
					}
				}
				if (gangId != null && !worldEvent.Gangs.Contains(gangId))
				{
					worldEvent.Gangs.Add(gangId);
				}
				if (tag == "kill")
				{
					List<string> list = (from x in worldEvent.People
						where x.Outcome == "KILLED"
						select x.Name).ToList();
					worldEvent.Title = ((list.Count <= 3) ? ("You killed " + string.Join(", ", list)) : $"You killed {list.Count} men");
				}
				else
				{
					List<string> list2 = (from x in worldEvent.People
						select x.Name into x
						where x != null
						select x).ToList();
					worldEvent.Title = ((list2.Count == 1) ? (list2[0] + " swore vengeance on you") : ((list2.Count <= 3) ? (string.Join(", ", list2.Take(list2.Count - 1)) + " and " + list2.Last() + " swore vengeance on you") : $"{list2.Count} men swore vengeance on you"));
				}
				SaveWorld();
				return worldEvent;
			}
		}
		WorldEvent worldEvent2 = NewEvent("YOU", num);
		worldEvent2.Tag = tag;
		worldEvent2.Title = title;
		worldEvent2.Place = place;
		worldEvent2.Heard = true;
		if (pos.HasValue)
		{
			worldEvent2.HasPos = true;
			worldEvent2.X = pos.Value.X;
			worldEvent2.Y = pos.Value.Y;
			worldEvent2.Z = pos.Value.Z;
		}
		worldEvent2.Gangs.Add(gangId ?? "");
		worldEvent2.People.AddRange(parts);
		chronicle.Events.Add(worldEvent2);
		SaveWorld();
		return worldEvent2;
	}

	internal WorldPart PartFor(NemesisRecord r, string outcome, string note = null)
	{
		WorldPart worldPart = new WorldPart
		{
			Id = r?.Id,
			Name = (r?.Name ?? "a stranger"),
			Gang = r?.Gang,
			Outcome = outcome,
			Note = note
		};
		if (r != null)
		{
			(string mood, List<Thought> thoughts) tuple = MoodOf(r);
			var (text, _) = tuple;
			if (tuple.thoughts.Count > 0 && r.IsAlive && !r.PresumedDead)
			{
				worldPart.Effects.Add(text ?? "");
			}
		}
		return worldPart;
	}

	internal void ChronicleWitness(string title, string place, Vector3 pos, IEnumerable<string> gangs, List<WorldPart> parts, Dictionary<string, int> unnamedDead, List<string> lines)
	{
		if (!Config.WorldEvents)
		{
			return;
		}
		WorldEvent worldEvent = NewEvent("WITNESSED", GameMinutesNow());
		worldEvent.Title = title;
		worldEvent.Place = place;
		worldEvent.Heard = true;
		worldEvent.HasPos = true;
		worldEvent.X = pos.X;
		worldEvent.Y = pos.Y;
		worldEvent.Z = pos.Z;
		worldEvent.Gangs.AddRange(gangs.Distinct());
		worldEvent.People.AddRange(parts);
		foreach (KeyValuePair<string, int> item in unnamedDead)
		{
			if (item.Value > 0)
			{
				worldEvent.UnnamedDead[item.Key] = item.Value;
			}
		}
		worldEvent.Lines.AddRange(lines);
		Chronicle.Events.Add(worldEvent);
		SaveWorld();
		Feed("WORLD WITNESSED: " + title + " " + place, KindColor("WITNESSED"));
	}

	internal void ChronicleRanks(Gang g, List<WorldPart> parts, string title = null)
	{
		if (Config.WorldEvents && parts.Count != 0)
		{
			WorldEvent worldEvent = NewEvent("RANKS", GameMinutesNow());
			worldEvent.Title = title ?? ("The " + g.Name + " shook up their ranks");
			worldEvent.Place = "";
			worldEvent.Heard = true;
			worldEvent.Gangs.Add(g.Id);
			worldEvent.People.AddRange(parts);
			Chronicle.Events.Add(worldEvent);
			SaveWorld();
		}
	}

	private WorldEvent NewEvent(string kind, long now)
	{
		DateTime d = FromGameMinutes(now);
		WorldEvent worldEvent = new WorldEvent();
		worldEvent.Id = NewId(10);
		worldEvent.Kind = kind;
		worldEvent.GameMin = now;
		worldEvent.Day = GameDate.DayKey(d);
		worldEvent.When = $"{Months[d.Month - 1]} {d.Day}  {d.Hour:00}:{d.Minute:00}";
		return worldEvent;
	}

	private bool OffStage(NemesisRecord r)
	{
		if (r != null && r.IsAlive && !r.PresumedDead && r != ActiveRecord && r != _sightingRec && !_worldNemeses.ContainsValue(r) && !_townMen.Any((TownMan t) => t.Rec == r) && !_recruitOffers.Any((RecruitOffer o) => o.Rec == r))
		{
			return !_liveCamps.Values.Any((LiveCamp c) => c.Boss == r);
		}
		return false;
	}

	private WorldEvent SimClash(Ped player, long now, (Gang a, Gang b, string place, Vector3 pos)? forced = null)
	{
		(Gang, Gang, string, Vector3)? tuple = forced ?? ((_rng.Next(100) < 50) ? WarClash(player) : (((Gang, Gang, string, Vector3)?)null));
		Gang gang;
		Gang gang2;
		(string, Vector3, CampSite)? tuple2;
		if (tuple.HasValue)
		{
			gang = tuple.Value.Item1;
			gang2 = tuple.Value.Item2;
			tuple2 = (tuple.Value.Item3, tuple.Value.Item4, null);
			if (!forced.HasValue)
			{
				District d = DistrictAt(tuple.Value.Item4);
				CampSite campSite = WarGoalCamp(gang, gang2, d);
				if (campSite == null)
				{
					CampSite campSite2 = WarGoalCamp(gang2, gang, d);
					if (campSite2 != null)
					{
						campSite = campSite2;
						Gang gang3 = gang;
						gang = gang2;
						gang2 = gang3;
					}
				}
				if (campSite != null && _rng.Next(100) < 70 && (player == null || new Vector3(campSite.X, campSite.Y, campSite.Z).DistanceTo(player.Position) >= Config.WorldMinDistance))
				{
					tuple2 = ("at " + campSite.Label, new Vector3(campSite.X, campSite.Y, campSite.Z), campSite);
				}
			}
			goto IL_01d6;
		}
		(Gang, Gang, District)? tuple3 = PickClashPair();
		if (tuple3.HasValue)
		{
			(string, Vector3, CampSite)? tuple4 = ClashPlace(tuple3.Value.Item1, tuple3.Value.Item2, tuple3.Value.Item3, player);
			if (tuple4.HasValue)
			{
				gang = tuple3.Value.Item1;
				gang2 = tuple3.Value.Item2;
				tuple2 = tuple4;
				goto IL_01d6;
			}
		}
		return null;
		IL_01d6:
		if (!tuple2.HasValue)
		{
			return null;
		}
		if (TruceBetween(gang.Id, gang2.Id) || IsDestroyed(gang) || IsDestroyed(gang2))
		{
			return null;
		}
		(string, Vector3, CampSite) value = tuple2.Value;
		string item = value.Item1;
		Vector3 item2 = value.Item2;
		CampSite site = value.Item3;
		List<NemesisRecord> list = Side(gang, defending: false);
		List<NemesisRecord> list2 = Side(gang2, defending: true);
		List<Gang> list3 = AlliesAgainst(gang, gang2).ToList();
		List<Gang> list4 = AlliesAgainst(gang2, gang).ToList();
		foreach (Gang item6 in list3)
		{
			list.AddRange(Side(item6, defending: false));
		}
		foreach (Gang item7 in list4)
		{
			list2.AddRange(Side(item7, defending: false));
		}
		if (list.Count + list2.Count == 0)
		{
			return null;
		}
		int num = 2 + _rng.Next(4);
		int num2 = 2 + _rng.Next(4);
		float num3 = Power(list, num) * Stretched(gang) * MenPower(gang);
		float num4 = Power(list2, num2) * Stretched(gang2) * MenPower(gang2) + ((site != null) ? 3f : 0f);
		bool flag = num3 >= num4;
		Gang gang4 = (flag ? gang : gang2);
		Gang loser = (flag ? gang2 : gang);
		WorldEvent worldEvent = NewEvent("CLASH", now);
		worldEvent.Place = item;
		worldEvent.HasPos = true;
		worldEvent.X = item2.X;
		worldEvent.Y = item2.Y;
		worldEvent.Z = item2.Z;
		worldEvent.Gangs.Add(gang.Id);
		worldEvent.Gangs.Add(gang2.Id);
		worldEvent.Title = ((site != null) ? ("The " + gang.Name + " raided the " + gang2.Name + " " + item) : (tuple.HasValue ? ("The " + gang.Name + " hit the " + gang2.Name + " " + item + " - the war goes on") : ("The " + gang.Name + " hit the " + gang2.Name)));
		worldEvent.Lines.Add($"{gang.Name}: {list.Count} named + {num} more.  {gang2.Name}: {list2.Count} named + {num2} more.");
		if (list3.Count > 0)
		{
			worldEvent.Lines.Add("The " + string.Join(" and the ", list3.Select((Gang x) => x.Name)) + " rode with the " + gang.Name + ".");
		}
		if (list4.Count > 0)
		{
			worldEvent.Lines.Add("The " + string.Join(" and the ", list4.Select((Gang x) => x.Name)) + " rode with the " + gang2.Name + ".");
		}
		worldEvent.Lines.Add("The " + gang4.Name + " carried the day.");
		bool flag2 = false;
		(List<NemesisRecord>, Gang, bool)[] array = new(List<NemesisRecord>, Gang, bool)[2]
		{
			(list, gang, flag),
			(list2, gang2, !flag)
		};
		for (int num5 = 0; num5 < array.Length; num5++)
		{
			(List<NemesisRecord>, Gang, bool) tuple5 = array[num5];
			List<NemesisRecord> item3 = tuple5.Item1;
			Gang item4 = tuple5.Item2;
			bool item5 = tuple5.Item3;
			Gang gang5 = ((item4 == gang) ? gang2 : gang);
			foreach (NemesisRecord item8 in item3)
			{
				if (ResolveFighter(item8, item5, worldEvent, "the " + gang5.Name, gun: true).Outcome == "KILLED" && site != null && item8.CampId == site.Id)
				{
					flag2 = true;
				}
			}
			int num6 = (item5 ? _rng.Next(3) : (1 + _rng.Next(4)));
			num6 = (int)Math.Round((float)(num6 * Config.WorldLethality) / 100f);
			if (num6 > 0)
			{
				worldEvent.UnnamedDead[item4.Id] = num6;
			}
		}
		if (site != null && SiteOwner(site) == gang2.Id && flag && (flag2 || _rng.Next(100) < 50))
		{
			worldEvent.Kind = "CAMP OVERRUN";
			if (HoldsCamps(gang) && BeginTakeover(site, gang, "overran it"))
			{
				worldEvent.Lines.Add("The camp was overrun - the " + gang.Name + " will hold " + site.Label + " once they've settled in.");
			}
			else
			{
				worldEvent.Lines.Add("The camp was overrun - nobody holds " + site.Label + " now.");
				MarkCampCleared(site);
			}
		}
		if (worldEvent.People.Any((WorldPart p) => p.Outcome == "KILLED") || worldEvent.UnnamedDead.Count > 0)
		{
			worldEvent.AftermathUntil = now + (long)Config.WorldAftermathHours * 60L;
		}
		try
		{
			ControlAfterClash(worldEvent, gang4, loser, worldEvent.Kind == "CAMP OVERRUN");
		}
		catch (Exception ex)
		{
			GrudgeLog("CONTROL failed: " + ex.Message);
		}
		foreach (NemesisRecord n in from nemesisRecord in list.Concat(list2)
			where nemesisRecord.IsAlive && !nemesisRecord.PresumedDead
			select nemesisRecord)
		{
			bool flag3 = n.Gang == gang4.Id;
			if (!worldEvent.People.Any((WorldPart pp) => pp.Id == n.Id && pp.Effects.Count > 0))
			{
				AddThought(n, flag3 ? "Proud" : "Shaken", flag3 ? ("won the fight with the " + ((n.Gang == gang.Id) ? gang2 : gang).Name + " " + item) : ("lost the fight with the " + ((n.Gang == gang.Id) ? gang2 : gang).Name + " " + item), flag3 ? 2 : (-2), 48, now, worldEvent.People.FirstOrDefault((WorldPart p) => p.Id == n.Id));
			}
		}
		return worldEvent;
		float Power(List<NemesisRecord> men, int extra)
		{
			return men.Sum((NemesisRecord nemesisRecord) => 2f + (float)nemesisRecord.Rank + (float)nemesisRecord.Aggression / 25f) + (float)extra * 1.5f + (float)_rng.NextDouble() * 6f;
		}
		List<NemesisRecord> Side(Gang g, bool defending)
		{
			List<NemesisRecord> source = Roster.Where((NemesisRecord nemesisRecord) => nemesisRecord.Gang == g.Id && OffStage(nemesisRecord) && !nemesisRecord.Paralyzed).ToList();
			List<NemesisRecord> list5 = new List<NemesisRecord>();
			if (defending && site != null)
			{
				NemesisRecord holder = source.FirstOrDefault((NemesisRecord nemesisRecord) => nemesisRecord.CampId == site.Id);
				if (holder != null)
				{
					list5.Add(holder);
					list5.AddRange((from _ in source
						where _.BossId == holder.Id
						orderby _rng.Next()
						select _).Take(1 + _rng.Next(3)));
					return list5;
				}
			}
			int num7 = 1 + _rng.Next(3);
			foreach (NemesisRecord item9 in source.OrderBy((NemesisRecord nemesisRecord) => ((nemesisRecord.Rank >= 4) ? 3 : ((nemesisRecord.Rank >= 2) ? 1 : 0)) * 100 + _rng.Next(100) - ((_rng.Next(4) == 0) ? 300 : 0)))
			{
				if (list5.Count >= num7)
				{
					break;
				}
				list5.Add(item9);
			}
			return list5;
		}
	}

	private WorldPart ResolveFighter(NemesisRecord n, bool won, WorldEvent ev, string by, bool gun, float lethal = 1f)
	{
		int num = (won ? 7 : 20);
		int num2 = (won ? 18 : 28);
		if (n.Rank >= 4)
		{
			num = num * 6 / 10;
		}
		num = num * Config.WorldLethality / 100;
		num = (int)Math.Round((float)num * lethal);
		num2 = (int)Math.Round((float)num2 * Math.Max(0.5f, lethal));
		WorldPart worldPart = new WorldPart
		{
			Id = n.Id,
			Name = n.Name,
			Gang = n.Gang
		};
		int num3 = ((n.Personality != Personality.Coward) ? ((!won && n.Facets != null && Mind.F(n, "Bravery") < 35) ? 15 : 0) : (won ? 8 : 35));
		if (!won && StretchedThin(Gangs.ById(n.Gang)))
		{
			num3 += 6;
		}
		if (num3 > 0 && _rng.Next(100) < num3)
		{
			worldPart.Outcome = "FLED";
			worldPart.Note = (won ? "ran before it was over" : "ran when it turned");
			n.Fear = Math.Min(100, n.Fear + 5);
			try
			{
				Remember(n, "Shame", 45, "ran from " + by + " " + ev.Place, "fight");
			}
			catch
			{
			}
			AddThought(n, "Ashamed", "ran from " + by + " " + ev.Place, -2, 48, ev.GameMin, worldPart);
			History(n, "RAN from " + by + " " + ev.Place + " when the fight turned", Theme.Dim);
			worldPart.Effects.Add("ran");
			ev.People.Add(worldPart);
			return worldPart;
		}
		int num4 = _rng.Next(100);
		if (num4 < num)
		{
			OffscreenDeath(n, ev, worldPart, by, gun);
		}
		else if (num4 < num + num2)
		{
			OffscreenWound(n, ev, worldPart, by, gun);
		}
		else if (won && _rng.Next(100) < 20)
		{
			worldPart.Outcome = "DISTINGUISHED";
			worldPart.Note = (gun ? "dropped two of them himself" : "came out on top");
			AddThought(n, "Proud", "fought well against " + by + " " + ev.Place, 3, 72, ev.GameMin, worldPart);
			History(n, "DISTINGUISHED HIMSELF against " + by + " " + ev.Place, Theme.Good);
			GainRankXp(n, 2, "distinguished himself against " + by);
			worldPart.Effects.Add("rank xp +2");
		}
		else
		{
			worldPart.Outcome = "UNHURT";
			History(n, "FOUGHT " + by + " " + ev.Place + " (" + (won ? "won" : "lost") + ") - unhurt", Theme.Dim);
		}
		ev.People.Add(worldPart);
		return worldPart;
	}

	private void OffscreenWound(NemesisRecord n, WorldEvent ev, WorldPart part, string by, bool gun)
	{
		ScarOption[] array = (gun ? GunshotScars : ((_rng.Next(2) == 0) ? BladeScars : BeatingScars));
		ScarOption scarOption = array[_rng.Next(array.Length)];
		n.Scars.Add(new Scar
		{
			DamagePack = scarOption.Pack,
			Description = scarOption.Description,
			Location = ev.Place,
			ReceivedAt = DateTime.Now
		});
		bool flag = gun && _rng.Next(100) < 20;
		if (flag)
		{
			n.Limp = true;
		}
		part.Outcome = "WOUNDED";
		part.Note = scarOption.Description + (flag ? ", walks with a limp now" : "");
		part.Effects.Add("scar: " + scarOption.Description);
		if (flag)
		{
			part.Effects.Add("limp for life");
		}
		AddThought(n, "Hurting", "wounded by " + by + " " + ev.Place, -2, 72, ev.GameMin, part);
		History(n, "WOUNDED by " + by + " " + ev.Place + ": " + part.Note, Theme.Warn);
	}

	private void OffscreenDeath(NemesisRecord n, WorldEvent ev, WorldPart part, string by, bool gun)
	{
		if (LoreProtected(n))
		{
			OffscreenWound(n, ev, part, by, gun);
			return;
		}
		int num = Math.Min(Config.CheatDeathMaxChance, Config.CheatDeathBaseChance + Config.CheatDeathPerRank * (n.Rank - 1));
		bool flag = _rng.Next(100) < 25;
		bool num2 = !flag && (Config.CheatDeathAlways || _rng.Next(100) < num);
		part.Outcome = "KILLED";
		if (num2)
		{
			ScarOption[] array = (gun ? GunshotScars : BladeScars);
			ScarOption scarOption = array[_rng.Next(array.Length)];
			n.Scars.Add(new Scar
			{
				DamagePack = scarOption.Pack,
				Description = scarOption.Description,
				Location = ev.Place,
				ReceivedAt = DateTime.Now
			});
			n.TimesCheatedDeath++;
			n.PresumedDead = true;
			n.LastDefeatLocation = ev.Place;
			if (gun && _rng.Next(100) < 15)
			{
				n.Limp = true;
			}
			part.SecretlyLived = true;
			part.Note = "left for dead";
			History(n, "LEFT FOR DEAD by " + by + " " + ev.Place + " - he lived: " + scarOption.Description, Theme.Blood);
		}
		else
		{
			n.IsAlive = false;
			n.PresumedDead = false;
			part.Note = ((!flag) ? (gun ? "shot dead" : "cut down") : (gun ? "shot through the head" : "bled out"));
			History(n, "KILLED by " + by + " " + ev.Place, Theme.Blood);
		}
		QueueDeathNews(n, byYou: false, by, ev, null);
	}

	private void OffscreenGrief(NemesisRecord dead, string by, WorldEvent ev, string onlyId = null, bool late = false)
	{
		foreach (var item2 in (from nemesisRecord in Roster
			where nemesisRecord.IsAlive && nemesisRecord != dead && (onlyId == null || nemesisRecord.Id == onlyId) && (nemesisRecord.KinId == dead.Id || dead.KinId == nemesisRecord.Id)
			select (k: nemesisRecord, (nemesisRecord.KinId == dead.Id) ? Traits.Inverse(nemesisRecord.KinRelation) : (dead.KinRelation ?? "kin"))).Concat<(NemesisRecord, string)>(from f in FriendsOf(dead)
			where f.IsAlive && (onlyId == null || f.Id == onlyId) && f.KinId != dead.Id && dead.KinId != f.Id
			select (f: f, "friend")).ToList())
		{
			NemesisRecord k = item2.Item1;
			string item = item2.Item2;
			bool flag = k.Personality == Personality.Hothead || k.Personality == Personality.Proud || k.Personality == Personality.Loyal;
			WorldPart worldPart = ((ev != null) ? new WorldPart
			{
				Id = k.Id,
				Name = k.Name,
				Gang = k.Gang,
				Outcome = "MOURNS",
				Note = "his " + item + " " + dead.Name
			} : null);
			AddThought(k, "Grieving", "lost his " + item + " " + dead.Name + " to " + by + (flag ? " - wants their blood" : ""), (item == "friend") ? (-2) : (-4), (item == "friend") ? 72 : 120, late ? GameMinutesNow() : (ev?.GameMin ?? GameMinutesNow()), worldPart);
			if (k.Personality == Personality.Coward)
			{
				int fear = k.Fear;
				k.AddFear(3);
				worldPart?.Effects.Add($"fear {fear} -> {k.Fear}");
			}
			History(k, "HEARD his " + item + " " + dead.Name + " was killed by " + by + ((ev != null) ? (" " + ev.Place) : ""), (150, 170, 210));
			if (ev != null && !ev.People.Any((WorldPart p) => p.Id == k.Id))
			{
				ev.People.Add(worldPart);
			}
		}
	}

	private WorldEvent SimFeud(long now)
	{
		foreach (Gang g in from _ in Gangs.All
			where _.Feuds
			orderby _rng.Next()
			select _)
		{
			List<NemesisRecord> list = (from _ in Roster
				where _.Gang == g.Id && OffStage(_) && !_.Paralyzed
				orderby _rng.Next()
				select _).ToList();
			if (list.Count >= 2)
			{
				NemesisRecord x = list[0];
				NemesisRecord nemesisRecord = list.Skip(1).FirstOrDefault((NemesisRecord n) => n.BossId == x.Id || x.BossId == n.Id || n.CampId == x.CampId) ?? list[1];
				WorldEvent worldEvent = NewEvent("FEUD", now);
				worldEvent.Gangs.Add(g.Id);
				string text = FeudCauses[_rng.Next(FeudCauses.Length)];
				CampSite campSite = Camps.ById(x.CampId ?? nemesisRecord.CampId ?? BossOf(x)?.CampId ?? "");
				worldEvent.Place = ((campSite != null) ? ("at " + campSite.Label) : "in camp");
				bool flag = _rng.Next(100) < 45;
				worldEvent.Title = x.Name + " and " + nemesisRecord.Name + " came to " + (flag ? "knives" : "blows") + " " + text;
				float num = x.Aggression + x.Rank * 10 + _rng.Next(60);
				float num2 = nemesisRecord.Aggression + nemesisRecord.Rank * 10 + _rng.Next(60);
				NemesisRecord nemesisRecord3;
				NemesisRecord nemesisRecord4;
				if (!(num >= num2))
				{
					NemesisRecord nemesisRecord2 = x;
					nemesisRecord3 = nemesisRecord2;
					nemesisRecord4 = nemesisRecord;
				}
				else
				{
					NemesisRecord nemesisRecord5 = x;
					NemesisRecord nemesisRecord2 = nemesisRecord;
					nemesisRecord3 = nemesisRecord2;
					nemesisRecord4 = nemesisRecord5;
				}
				WorldPart worldPart = new WorldPart
				{
					Id = nemesisRecord4.Id,
					Name = nemesisRecord4.Name,
					Gang = g.Id,
					Outcome = "WON",
					Note = "came out on top"
				};
				worldEvent.People.Add(worldPart);
				WorldPart worldPart2 = new WorldPart
				{
					Id = nemesisRecord3.Id,
					Name = nemesisRecord3.Name,
					Gang = g.Id
				};
				int num3 = _rng.Next(100);
				int num4 = (flag ? 10 : 3) * Config.WorldLethality / 100;
				if (num3 < num4)
				{
					worldEvent.People.Add(worldPart2);
					OffscreenDeath(nemesisRecord3, worldEvent, worldPart2, nemesisRecord4.Name, gun: false);
				}
				else if (num3 < num4 + (flag ? 50 : 35))
				{
					ScarOption[] array = (flag ? BladeScars : BeatingScars);
					ScarOption scarOption = array[_rng.Next(array.Length)];
					nemesisRecord3.Scars.Add(new Scar
					{
						DamagePack = scarOption.Pack,
						Description = scarOption.Description,
						Location = worldEvent.Place,
						ReceivedAt = DateTime.Now
					});
					worldPart2.Outcome = "WOUNDED";
					worldPart2.Note = scarOption.Description;
					worldEvent.People.Add(worldPart2);
					History(nemesisRecord3, (flag ? "CUT" : "BEATEN") + " by " + nemesisRecord4.Name + " " + text + ": " + scarOption.Description, Theme.Warn);
				}
				else
				{
					worldPart2.Outcome = "LOST";
					worldPart2.Note = "humiliated";
					worldEvent.People.Add(worldPart2);
					History(nemesisRecord3, "LOST A FIGHT to " + nemesisRecord4.Name + " " + text, Theme.Dim);
				}
				History(nemesisRecord4, "BESTED " + nemesisRecord3.Name + " " + text, Theme.Good);
				Mind.SetTie(nemesisRecord3, nemesisRecord4.Id, "enemy", Math.Min(Mind.TieTo(nemesisRecord3, nemesisRecord4.Id)?.Strength ?? 0, 0) - 50, text);
				Mind.SetTie(nemesisRecord4, nemesisRecord3.Id, "rival", Math.Min(Mind.TieTo(nemesisRecord4, nemesisRecord3.Id)?.Strength ?? 0, 0) - 25, text);
				if (nemesisRecord4.Needs != null)
				{
					nemesisRecord4.Needs["Violence"] = 0;
				}
				if (nemesisRecord3.Needs != null)
				{
					nemesisRecord3.Needs["Violence"] = 0;
				}
				AddThought(nemesisRecord4, "Proud", "put " + nemesisRecord3.Name + " in his place", 2, 48, now, worldPart);
				if (worldPart2.Outcome == "WOUNDED")
				{
					worldPart2.Effects.Add("scar: " + worldPart2.Note);
				}
				if (nemesisRecord3.IsAlive && !nemesisRecord3.PresumedDead)
				{
					AddThought(nemesisRecord3, (worldPart2.Outcome == "LOST") ? "Humiliated" : "Bitter", "holds it against " + nemesisRecord4.Name + " (" + text + ")", -3, 120, now, worldPart2);
				}
				if (nemesisRecord4.FriendIds.Remove(nemesisRecord3.Id) | nemesisRecord3.FriendIds.Remove(nemesisRecord4.Id))
				{
					worldEvent.Lines.Add("They were friends. Not any more.");
					worldPart.Effects.Add("friendship over");
					worldPart2.Effects.Add("friendship over");
				}
				return worldEvent;
			}
		}
		return null;
	}

	private string PickTitle(string[] pool, string kind)
	{
		_lastTitle.TryGetValue(kind, out var value);
		string text = pool[_rng.Next(pool.Length)];
		for (int i = 0; i < 4; i++)
		{
			if (!(text == value))
			{
				break;
			}
			if (pool.Length <= 1)
			{
				break;
			}
			text = pool[_rng.Next(pool.Length)];
		}
		_lastTitle[kind] = text;
		return text;
	}

	private WorldEvent SimBrawl(Ped player, long now)
	{
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.Gang == null && OffStage(n) && !n.Paralyzed).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		NemesisRecord nemesisRecord = list[_rng.Next(list.Count)];
		List<string> list2 = (from kv in TownSpots
			where kv.Value.Saloon.HasValue && (player == null || kv.Value.Saloon.Value.DistanceTo(player.Position) >= Config.WorldMinDistance)
			select kv.Key).ToList();
		if (list2.Count == 0)
		{
			return null;
		}
		string text = ((nemesisRecord.KillLocation != null && list2.Contains(nemesisRecord.KillLocation) && _rng.Next(2) == 0) ? nemesisRecord.KillLocation : list2[_rng.Next(list2.Count)]);
		int num = _rng.Next(100);
		bool flag = num < 30;
		bool flag2 = num >= 30 && num < 50;
		WorldEvent worldEvent = NewEvent(flag ? "DUEL" : "BRAWL", now);
		worldEvent.Gangs.Add("");
		worldEvent.Place = "in " + text;
		string text2 = NameBank.AmericanFirst[_rng.Next(NameBank.AmericanFirst.Length)] + " " + NameBank.AmericanLast[_rng.Next(NameBank.AmericanLast.Length)];
		worldEvent.Title = PickTitle(flag ? DuelTitles : (flag2 ? KnifeTitles : BrawlTitles), "brawl").Replace("{n}", nemesisRecord.Name).Replace("{o}", text2).Replace("{t}", text);
		float num2 = nemesisRecord.Aggression + _rng.Next(70);
		float num3 = 30 + _rng.Next(70);
		bool num4 = num2 >= num3;
		WorldPart worldPart = new WorldPart
		{
			Id = nemesisRecord.Id,
			Name = nemesisRecord.Name,
			Gang = null
		};
		if (num4)
		{
			worldPart.Outcome = "WON";
			worldPart.Note = (flag ? (text2 + " lies dead") : (flag2 ? (text2 + " ran, bleeding") : "the last man standing"));
			worldEvent.People.Add(worldPart);
			AddThought(nemesisRecord, "Proud", flag ? ("won a duel " + worldEvent.Place) : ("came out on top of a brawl " + worldEvent.Place), 3, 72, now, worldPart);
			History(nemesisRecord, (flag ? "WON A DUEL" : "WON A FIGHT") + " " + worldEvent.Place + " (" + worldPart.Note + ")", Theme.Good);
			if (_rng.Next(100) < 30)
			{
				AddThought(nemesisRecord, "Jailed", "spent a night in the " + text + " jail", -2, 24, now, worldPart);
				nemesisRecord.RestUntil = Math.Max(nemesisRecord.RestUntil, now + 1440);
				worldPart.Effects.Add("laid up 24h");
				worldEvent.Lines.Add("The law locked him up for the night.");
			}
		}
		else
		{
			int num5 = (flag ? 35 : (flag2 ? 12 : 3)) * Config.WorldLethality / 100;
			if (_rng.Next(100) < num5)
			{
				worldEvent.People.Add(worldPart);
				OffscreenDeath(nemesisRecord, worldEvent, worldPart, text2, flag);
			}
			else
			{
				worldEvent.People.Add(worldPart);
				OffscreenWound(nemesisRecord, worldEvent, worldPart, text2, flag);
			}
		}
		return worldEvent;
	}

	private WorldEvent SimLife(long now)
	{
		List<NemesisRecord> list = Roster.Where(OffStage).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		for (int i = 0; i < 8; i++)
		{
			NemesisRecord n = list[_rng.Next(list.Count)];
			LifeEvent le = WorldLife.Pick(n, _rng);
			if (le == null)
			{
				continue;
			}
			WorldLife.Recent.Add(le.Id);
			if (WorldLife.Recent.Count > 40)
			{
				WorldLife.Recent.RemoveAt(0);
			}
			Gang gang = Gangs.ById(n.Gang);
			string town = ((n.KillLocation != null && TownSpots.ContainsKey(n.KillLocation) && _rng.Next(2) == 0) ? n.KillLocation : TownSpots.Keys.ElementAt(_rng.Next(TownSpots.Count)));
			NemesisRecord mate = null;
			if ((le.Title + le.Thought).Contains("{f}"))
			{
				bool befriend = le.Effects.Contains("friend");
				List<NemesisRecord> list2 = Roster.Where((NemesisRecord m) => m != n && m.Gang == n.Gang && m.IsAlive && !m.PresumedDead && (!befriend || (!n.FriendIds.Contains(m.Id) && n.FriendIds.Count < 3 && m.FriendIds.Count < 3 && m.KinId != n.Id && n.KinId != m.Id)) && (le.Effects.Contains("sour") || true)).ToList();
				if (list2.Count == 0)
				{
					continue;
				}
				mate = list2[_rng.Next(list2.Count)];
			}
			NemesisRecord boss = BossOf(n);
			if ((!(le.Title + le.Thought).Contains("{b}") || boss != null) && (!(le.Title + le.Thought).Contains("{l}") || !LeaderOut(gang, n)))
			{
				WorldEvent worldEvent = NewEvent("LIFE", now);
				worldEvent.Gangs.Add(n.Gang ?? "");
				worldEvent.Title = Fill(le.Title);
				worldEvent.Place = ((gang != null) ? ("(" + gang.Name + ")") : "(rides alone)");
				WorldPart worldPart = new WorldPart
				{
					Id = n.Id,
					Name = n.Name,
					Gang = n.Gang,
					Outcome = ""
				};
				worldEvent.People.Add(worldPart);
				string[] effects = le.Effects;
				foreach (string fx in effects)
				{
					ApplyLifeEffect(n, fx, now, worldPart, mate, worldEvent);
				}
				AddThought(n, le.Mood, Fill(le.Thought), le.Weight, le.Hours, now, worldPart);
				MeetNeeds(n, worldEvent.Title + " " + le.Thought);
				History(n, worldEvent.Title, (le.Weight >= 0) ? Theme.Good : (150, 170, 210));
				return worldEvent;
			}
			string Fill(string x)
			{
				return LoreNow(x).Replace("{n}", n.Name).Replace("{t}", town).Replace("{g}", gang?.Name ?? "gang")
					.Replace("{f}", mate?.Name ?? "a friend")
					.Replace("{b}", boss?.Name ?? "the boss")
					.Replace("{l}", gang?.LeaderName ?? "the boss");
			}
		}
		return null;
	}

	private void ApplyLifeEffect(NemesisRecord n, string fx, long now, WorldPart part, NemesisRecord mate, WorldEvent ev)
	{
		string[] array = fx.Split(':');
		int.TryParse((array.Length > 1) ? array[1] : "0", out var result);
		string text = array[0];
		if (text == null)
		{
			return;
		}
		switch (text.Length)
		{
		case 4:
			switch (text[0])
			{
			case 'r':
				if (text == "rest")
				{
					n.RestUntil = Math.Max(n.RestUntil, now + (long)result * 60L);
					part.Effects.Add($"laid up {result}h");
				}
				break;
			case 'f':
				if (text == "fear")
				{
					int fear = n.Fear;
					n.AddFear(result);
					part.Effects.Add($"fear {fear} -> {n.Fear}");
				}
				break;
			case 'h':
				if (text == "hate")
				{
					int hate = n.Hate;
					if (result > 0)
					{
						n.AddHate(result);
						CapScuffle(n, hate);
					}
					else
					{
						n.Hate = Math.Max(0, n.Hate + result);
					}
					part.Effects.Add($"hate {hate} -> {n.Hate}");
				}
				break;
			case 's':
				if (!(text == "scar"))
				{
					if (text == "sour" && mate != null)
					{
						bool num = n.FriendIds.Remove(mate.Id) | mate.FriendIds.Remove(n.Id);
						WorldPart worldPart = new WorldPart
						{
							Id = mate.Id,
							Name = mate.Name,
							Gang = mate.Gang,
							Outcome = ""
						};
						AddThought(mate, "Bitter", n.Name + "'s accusations", -2, 72, now, worldPart);
						if (num)
						{
							part.Effects.Add("friendship over");
							worldPart.Effects.Add("friendship over");
						}
						ev.People.Add(worldPart);
						History(mate, "ACCUSED by " + n.Name, Theme.Warn);
					}
				}
				else
				{
					ScarOption[] array3 = ((array.Length > 1 && array[1] == "claw") ? ClawScars : ((array.Length > 1 && array[1] == "blade") ? BladeScars : BeatingScars));
					ScarOption scarOption = array3[_rng.Next(array3.Length)];
					n.Scars.Add(new Scar
					{
						DamagePack = scarOption.Pack,
						Description = scarOption.Description,
						Location = ev.Title,
						ReceivedAt = DateTime.Now
					});
					part.Effects.Add("scar: " + scarOption.Description);
				}
				break;
			}
			break;
		case 5:
			switch (text[0])
			{
			case 'c':
				if (text == "child")
				{
					n.Children++;
					part.Effects.Add(string.Format("{0} {1} now", n.Children, (n.Children == 1) ? "child" : "children"));
				}
				break;
			case 'm':
				if (text == "marry")
				{
					n.Family = "married";
					part.Effects.Add("married");
				}
				break;
			case 'w':
				if (text == "widow")
				{
					n.Family = "widower";
					part.Effects.Add("widower");
				}
				break;
			case 'n':
				if (text == "nopet")
				{
					part.Effects.Add("lost " + n.Pet);
					n.Pet = null;
				}
				break;
			}
			break;
		case 6:
			switch (text[0])
			{
			case 's':
				if (text == "single")
				{
					n.Family = "single";
					part.Effects.Add("wife gone");
				}
				break;
			case 'f':
				if (text == "friend" && mate != null)
				{
					n.FriendIds.Add(mate.Id);
					mate.FriendIds.Add(n.Id);
					part.Effects.Add("friends with " + mate.Name);
					WorldPart worldPart2 = new WorldPart
					{
						Id = mate.Id,
						Name = mate.Name,
						Gang = mate.Gang,
						Outcome = ""
					};
					AddThought(mate, "Joyful", "his new friend " + n.Name, 1, 48, now, worldPart2);
					worldPart2.Effects.Add("friends with " + n.Name);
					ev.People.Add(worldPart2);
					History(mate, "BECAME FRIENDS with " + n.Name, Theme.Good);
				}
				break;
			}
			break;
		case 3:
			if (text == "pet")
			{
				string[] array2 = new string[8] { "Scout", "Blue", "Duke", "Rusty", "Bell", "Jack", "Shep", "Lady" };
				n.Pet = "a stray dog called " + array2[_rng.Next(array2.Length)];
				part.Effects.Add(n.Pet);
			}
			break;
		}
	}

	private static string Dur(int hours)
	{
		if (hours < 24)
		{
			return $"{hours}h";
		}
		return $"{(hours + 12) / 24}d";
	}

	internal void AddThought(NemesisRecord n, string mood, string text, int weight, int hours, long now, WorldPart part = null)
	{
		if (n != null && mood != null)
		{
			part?.Effects.Add(mood + " " + Dur(hours));
			if (n.Thoughts == null)
			{
				n.Thoughts = new List<Thought>();
			}
			n.Thoughts.RemoveAll((Thought t) => t.Until <= now);
			n.Thoughts.Add(new Thought
			{
				Mood = mood,
				Text = text,
				Weight = weight,
				At = now,
				Until = now + (long)hours * 60L
			});
			while (n.Thoughts.Count > 8)
			{
				n.Thoughts.RemoveAt(0);
			}
			string text2 = EmotionOfMood(mood);
			if (text2 != null)
			{
				Remember(n, text2, Math.Min(90, Math.Abs(weight) * 15 + 15), text, null);
			}
		}
	}

	internal static (int r, int g, int b) MoodColor(string mood)
	{
		switch (mood)
		{
		case "Grieving":
		case "Homesick":
		case "Shaken":
			return (r: 150, g: 170, b: 210);
		case "Hurting":
			return Theme.Blood;
		case "Bitter":
		case "Jailed":
		case "Humiliated":
			return Theme.Warn;
		case "Sick":
			return (r: 150, g: 170, b: 110);
		case "Drunk":
			return (r: 205, g: 165, b: 95);
		case "Joyful":
		case "Flush":
		case "Proud":
			return Theme.Good;
		default:
			return Theme.Dim;
		}
	}

	internal (string mood, List<Thought> thoughts) MoodOf(NemesisRecord n)
	{
		long now = GameMinutesNow();
		List<Thought> list = (from t in n.Thoughts ?? new List<Thought>()
			where t.Until > now
			orderby Math.Abs(t.Weight) descending, t.At descending
			select t).ToList();
		return (mood: (list.Count == 0) ? "Steady" : list[0].Mood, thoughts: list);
	}

	private int MoodTalkBias(NemesisRecord n)
	{
		if (n == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Thought item in MoodOf(n).thoughts)
		{
			if (item.Mood == "Shaken" || item.Mood == "Grieving" || item.Mood == "Hurting" || item.Mood == "Sick")
			{
				num += 4;
			}
			else if (item.Mood == "Bitter" || item.Mood == "Humiliated")
			{
				num += 3;
			}
			else if (item.Mood == "Proud" || item.Mood == "Joyful")
			{
				num -= 4;
			}
		}
		return Math.Max(-8, Math.Min(10, num));
	}

	internal List<WorldEvent> ChronicleFor(int filter)
	{
		(int, bool, int, int) chronKey = (filter, Config.WorldKnowAll || _debugOn, Chronicle.Events.Count, Chronicle.Past?.Count ?? 0);
		if (_chronCache != null && chronKey.Equals(_chronKey) && UiDraw.RealMs - _chronAt < 250)
		{
			return _chronCache;
		}
		_chronKey = chronKey;
		_chronAt = UiDraw.RealMs;
		return _chronCache = ChronicleForNow(filter);
	}

	internal void StampEventShifts(long nowRef = 0L)
	{
		WorldState chronicle = Chronicle;
		long now = ((nowRef > 0) ? nowRef : GameMinutesNow());
		if (now <= 0)
		{
			return;
		}
		if (!chronicle.ShiftRepaired)
		{
			chronicle.ShiftRepaired = true;
			List<WorldEvent> list = chronicle.Events.Where((WorldEvent x) => !x.IsPast && x.GameMin > now + 60).ToList();
			long num = ((list.Count > 0) ? list.Max((WorldEvent x) => x.GameMin) : 0);
			foreach (WorldEvent @event in chronicle.Events)
			{
				@event.Shift = (list.Contains(@event) ? (now - num - 1440) : 0);
			}
			chronicle.ClockShift = 0L;
			foreach (WorldEvent event2 in chronicle.Events)
			{
				foreach (Gang item in Gangs.All.Where((Gang y) => y.Name.EndsWith("s")))
				{
					if (event2.Title != null && event2.Title.Contains(item.Name + "'s"))
					{
						event2.Title = event2.Title.Replace(item.Name + "'s", item.Name + "'");
					}
				}
			}
			GrudgeLog($"TIMELINE World tab order repaired: {list.Count} event(s) dated after the clock (written before it jumped back) put below the present");
			SaveWorld();
			return;
		}
		foreach (WorldEvent event3 in chronicle.Events)
		{
			if (!event3.Shift.HasValue)
			{
				event3.Shift = chronicle.ClockShift + ((event3.GameMin > now + 60) ? (now - event3.GameMin) : 0);
			}
		}
	}

	internal void ClockJumpedBack(long from, long to)
	{
		if (from > to)
		{
			StampEventShifts(from);
			Chronicle.ClockShift += from - to;
			SaveWorld();
			GrudgeLog($"TIMELINE the clock went back {(from - to) / 60}h {(from - to) % 60}m - the World tab keeps what comes next on top (shift {Chronicle.ClockShift})");
		}
	}

	private List<WorldEvent> ChronicleForNow(int filter)
	{
		StampEventShifts(0L);
		string id = ((filter <= 0) ? null : ((filter <= Gangs.All.Length) ? Gangs.All[filter - 1].Id : ((filter == Gangs.All.Length + 1) ? "vdl" : "")));
		return (from x in Chronicle.Events.Where(Show).Select((WorldEvent e, int i) => (e: e, i: i))
			orderby x.e.GameMin + (x.e.Shift ?? Chronicle.ClockShift) descending, x.i descending
			select x.e).Concat(from e in (Chronicle.Past ?? new List<WorldEvent>()).Where(Show)
			orderby e.GameMin descending
			select e).ToList();
		bool Show(WorldEvent e)
		{
			if (Config.WorldKnowAll || _debugOn || e.Heard)
			{
				if (id != null)
				{
					return e.Gangs.Contains(id);
				}
				return true;
			}
			return false;
		}
	}
}
