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
	public class LoreRow
	{
		public int Year { get; set; }

		public int YearsAgo { get; set; }

		public int Month { get; set; }

		public string Title { get; set; }

		public string Place { get; set; }

		public string Gang { get; set; }

		public List<string> Lines { get; set; } = new List<string>();

		public string When { get; set; }
	}

	internal static Dictionary<string, (string kind, string title, string[] places, string foe, int heard, string line)[]> GangTales = new Dictionary<string, (string, string, string[], string, int, string)[]>
	{
		{
			"odriscoll",
			new(string, string, string[], string, int, string)[5]
			{
				("RAID", "O'Driscolls hit a homestead near {place}", new string[4] { "Emerald Ranch", "the Heartlands", "Big Valley", "Cumberland Forest" }, null, 80, "They took the horses and whatever wasn't nailed down."),
				("RAID", "Stagecoach robbed on the road to {place}", new string[3] { "Valentine", "Strawberry", "Annesburg" }, null, 90, "The driver lived to tell the sheriff it was O'Driscolls."),
				("CLASH", "O'Driscolls and Van der Linde's men trade shots near {place}", new string[3] { "Big Valley", "the Heartlands", "Strawberry" }, null, 60, "The old feud flares up again; both sides ride off cursing."),
				("CLASH", "O'Driscolls and Raiders fight over a moonshine run near {place}", new string[2] { "Rhodes", "the Heartlands" }, "lemoyneraiders", 70, "Nobody got the moonshine."),
				("CLASH", "Army patrol ambushed by O'Driscolls in {place}", new string[2] { "Cumberland Forest", "the Grizzlies" }, "army", 85, "The Army swore it would be back with more men.")
			}
		},
		{
			"lemoyneraiders",
			new(string, string, string[], string, int, string)[4]
			{
				("RAID", "Raiders burn out a carpetbagger's store in {place}", new string[2] { "Rhodes", "Scarlett Meadows" }, null, 90, "They left a Union flag nailed to the door, burning."),
				("CLASH", "Raiders and the Army trade fire in {place}", new string[3] { "Bluewater Marsh", "Scarlett Meadows", "Bayou Nwa" }, "army", 85, "The soldiers held the road; the Raiders melted into the swamp."),
				("RAID", "Payroll wagon taken outside {place}", new string[2] { "Saint Denis", "Rhodes" }, null, 80, "The guards were found tied to a tree, alive and furious."),
				("RAID", "Raiders run a Yankee surveyor out of {place}", new string[2] { "Scarlett Meadows", "Bluewater Marsh" }, null, 60, "His instruments are still at the bottom of the river.")
			}
		},
		{
			"army",
			new(string, string, string[], string, int, string)[3]
			{
				("DUTY", "Army escorts a Cornwall payroll through {place}", new string[2] { "Roanoke Ridge", "Cumberland Forest" }, null, 50, "Not a shot fired - this time."),
				("CLASH", "Army column hunts O'Driscolls in {place}", new string[2] { "Cumberland Forest", "the Grizzlies" }, "odriscoll", 80, "They burned an empty camp and two men's pride."),
				("DUTY", "Fort Wallace drills new recruits through the winter", new string[1] { "Fort Wallace" }, null, 30, "Half of them had never held a rifle.")
			}
		},
		{
			"wapiti",
			new(string, string, string[], string, int, string)[3]
			{
				("RAID", "Wapiti riders take back horses from an Army corral near {place}", new string[2] { "Fort Wallace", "Cumberland Forest" }, "army", 75, "They were gone before the bugle sounded. Nobody was hurt."),
				("CLASH", "Soldiers and Wapiti trade arrows and bullets at {place}", new string[2] { "the Dakota River", "Cumberland Forest" }, "army", 80, "Nobody would say who fired first."),
				("DUTY", "A Wapiti hunting party rides out into {place}", new string[2] { "the Grizzlies", "Ambarino" }, null, 35, "Enough meat for the families, for a while.")
			}
		},
		{
			"klan",
			new(string, string, string[], string, int, string)[2]
			{
				("RAID", "Hooded riders burn a field in {place}", new string[2] { "Scarlett Meadows", "Bluewater Marsh" }, null, 70, "The farmer and his sons put the fire out by dawn."),
				("CLASH", "A Klan rally broken up in {place} - the hoods ran", new string[2] { "Scarlett Meadows", "Roanoke Ridge" }, null, 80, "Somebody fired one shot. That was enough.")
			}
		},
		{
			"bronte",
			new(string, string, string[], string, int, string)[4]
			{
				("RAID", "Bronte's men lean on a shopkeeper in {place}", new string[2] { "Saint Denis", "the Saint Denis docks" }, null, 70, "He paid. The ones who don't pay find their windows broken."),
				("CLASH", "Bronte's men and the Lemoyne Raiders shoot it out over a shipment near {place}", new string[2] { "Saint Denis", "Bayou Nwa" }, "lemoyneraiders", 75, "The crates went into the bayou. Both sides blamed the other."),
				("DUTY", "A dock foreman who talked to the papers is fished out of {place}", new string[2] { "the Lannahechee River", "the bayou" }, null, 85, "The police called it an accident. Everyone knew better."),
				("RAID", "Bronte's men take over a gambling house in {place}", new string[1] { "Saint Denis" }, null, 60, "The old owner left town the same night.")
			}
		},
		{
			"murfree",
			new(string, string, string[], string, int, string)[3]
			{
				("RAID", "Travellers go missing on the road through {place}", new string[3] { "Roanoke Ridge", "Brandywine Drop", "Beaver Hollow" }, null, 80, "Their wagon was found. They weren't."),
				("RAID", "The Brood drags a family off a homestead near {place}", new string[2] { "Roanoke Ridge", "Annesburg" }, null, 85, "The bounty posters went up in Annesburg. Nobody went in after them."),
				("CLASH", "Murfrees and O'Driscolls cross paths in {place}", new string[2] { "Roanoke Ridge", "Kamassa River" }, "odriscoll", 60, "The O'Driscolls didn't go back that way.")
			}
		},
		{
			"laramie",
			new(string, string, string[], string, int, string)[6]
			{
				("RAID", "Laramie boys run off a herd from {place}", new string[3] { "Hennigan's Stead", "the Great Plains", "Big Valley" }, null, 80, "The brands were changed before the owners woke."),
				("RAID", "Stagecoach held up on the road to {place}", new string[3] { "Blackwater", "Armadillo", "MacFarlane's Ranch" }, null, 85, "The driver said they laughed the whole time."),
				("CLASH", "Laramie men and Del Lobos shoot it out over stolen horses near {place}", new string[2] { "Cholla Springs", "Hennigan's Stead" }, "delobo", 75, "Nobody kept the horses."),
				("CLASH", "Laramie rustlers and the Skinner Brothers cross paths in {place}", new string[2] { "the Great Plains", "Tall Trees" }, "skinners", 60, "The rustlers rode south the next morning."),
				("CRIME", "A rancher near {place} finds his herd wearing somebody else's brand", new string[2] { "Hennigan's Stead", "the Great Plains" }, null, 70, "He couldn't prove a thing."),
				("CRIME", "A stock detective found dead on the trail near {place}", new string[2] { "Hennigan's Stead", "Big Valley" }, null, 60, "The cattlemen's association sent two more.")
			}
		},
		{
			"delobo",
			new(string, string, string[], string, int, string)[6]
			{
				("RAID", "Del Lobos rob a train near {place}", new string[3] { "Rio Bravo", "Cholla Springs", "Gaptooth Ridge" }, null, 85, "They were back across the river before the marshal heard."),
				("RAID", "A ranch outside {place} burned by the Del Lobos", new string[2] { "Armadillo", "Tumbleweed" }, null, 80, "The rancher wouldn't pay. Now he has no ranch."),
				("CLASH", "Del Lobos and the Army trade shots in {place}", new string[2] { "Rio Bravo", "Gaptooth Ridge" }, "army", 70, "The soldiers lost two men and the trail."),
				("RAID", "Guns run across the river near {place}", new string[2] { "Rio Bravo", "Gaptooth Ridge" }, null, 50, "Nobody saw anything. Nobody ever does."),
				("RAID", "The bank in {place} robbed in broad daylight", new string[2] { "Armadillo", "Tumbleweed" }, null, 75, "The Del Lobos rode out shooting in the air."),
				("CLASH", "Del Lobos and rurales fight at the river near {place}", new string[1] { "Rio Bravo" }, null, 55, "The river ran with it for a day.")
			}
		},
		{
			"nightfolk",
			new(string, string, string[], string, int, string)[7]
			{
				("RAID", "A traveller vanishes on the bayou road near {place}", new string[3] { "Bayou Nwa", "Lagras", "Saint Denis" }, null, 70, "His horse came home without him."),
				("RAID", "Bodies found hanging in the cypress near {place}", new string[2] { "Bayou Nwa", "Shady Belle" }, null, 80, "Nobody in Saint Denis would go and cut them down."),
				("CLASH", "Raiders ride into the swamp after the Night Folk near {place}", new string[1] { "Bayou Nwa" }, "lemoyneraiders", 60, "Fewer rode out than rode in."),
				("CRIME", "Lanterns in the trees along the road near {place}, three nights running", new string[2] { "Bayou Nwa", "Lagras" }, null, 60, "Nobody took that road after dark for a month."),
				("CRIME", "A fishing boat found drifting near {place}, nobody aboard", new string[2] { "Bayou Nwa", "Lagras" }, null, 65, "The nets were cut. The lantern was still lit."),
				("CRIME", "A grave dug up in the night near {place}", new string[2] { "Saint Denis", "Shady Belle" }, null, 55, "Whatever was taken, the family won't say."),
				("CRIME", "A Saint Denis family wakes to find something left on their doorstep", new string[1] { "Saint Denis" }, null, 45, "They left the city within the week.")
			}
		},
		{
			"skinners",
			new(string, string, string[], string, int, string)[3]
			{
				("RAID", "A trapper's camp found stripped in {place}", new string[3] { "Tall Trees", "Manzanita Post", "the Great Plains" }, null, 75, "The Skinner Brothers' mark was cut into a tree beside it."),
				("RAID", "Settlers scalped on the trail through {place}", new string[2] { "Tall Trees", "Aurora Basin" }, null, 85, "Blackwater put a price on the brothers' heads. Nobody collected."),
				("CLASH", "Skinner Brothers and bounty hunters fight in {place}", new string[2] { "Tall Trees", "the Great Plains" }, null, 70, "Only one of the hunters rode out.")
			}
		}
	};

	private Holdings _hold;

	private const long SupplyRestock = 2880L;

	internal static bool PastShown(PastEntry e)
	{
		if (e == null || e.Year <= 0)
		{
			return true;
		}
		int num = GameYear();
		int num2 = GameMonth();
		if (e.Year >= num)
		{
			if (e.Year == num)
			{
				return e.Month <= num2;
			}
			return false;
		}
		return true;
	}

	internal static int GameYear()
	{
		int num;
		try
		{
			num = CLOCK.GET_CLOCK_YEAR();
			if (num < 1850 || num > 1950)
			{
				num = 1899;
			}
		}
		catch
		{
			num = 1899;
		}
		if (Gangs.Era < 1907 || num >= 1907)
		{
			return num;
		}
		return 1907;
	}

	internal static int GameMonth()
	{
		try
		{
			return Math.Max(1, Math.Min(12, CLOCK.GET_CLOCK_MONTH() + 1));
		}
		catch
		{
			return 6;
		}
	}

	private void FixWomensTrades()
	{
		WorldState chronicle = Chronicle;
		if (chronicle.WomenTradesFixed)
		{
			return;
		}
		int num = 0;
		foreach (NemesisRecord item in Roster.Where((NemesisRecord x) => x.Gang == null && !x.Story && x.Trade != null && Backstory.Woman(x) && !Traits.IsWomansTrade(x.Trade)).ToList())
		{
			try
			{
				string trade = item.Trade;
				Random rng = Seeds.For(item, "woman");
				item.Trade = null;
				Traits.SetBackground(item, rng);
				if (item.PastRolled)
				{
					Backstory.Roll(item, rng, GameYear(), GameMonth(), derive: false);
					item.Woven = false;
				}
				num++;
				GrudgeLog("HISTORY " + item.Name + ": a woman's trade - " + item.Trade + " (was " + trade + "), her life written again");
			}
			catch (Exception ex)
			{
				GrudgeLog("HISTORY " + item.Name + ": the woman's-trade fix failed - " + ex.Message);
			}
		}
		chronicle.WomenTradesFixed = true;
		GrudgeLog($"HISTORY women's trades checked: {num} given a woman's trade");
		if (num > 0)
		{
			SaveRoster();
		}
		SaveWorld();
	}

	private void RollNewLife(NemesisRecord r, IList<NemesisRecord> roster, bool mind = true, int rules = 2)
	{
		Random rng = ((r.Id != null) ? Seeds.For(r, "life") : _rng);
		r.LifeRules = ((rules >= 2) ? 2 : 0);
		Traits.SetBackground(r, rng);
		BackstoryResult backstoryResult = Backstory.Roll(r, rng, GameYear(), GameMonth(), derive: true);
		r.Personality = Backstory.PickTemper(backstoryResult, r.Gang != null, rng);
		Traits.Roll(r, rng, roster);
		if (r.KinId != null)
		{
			NemesisRecord nemesisRecord = roster?.FirstOrDefault((NemesisRecord n) => n.Id == r.KinId);
			if (Backstory.FitKinAge(r, nemesisRecord, rng, GameYear()))
			{
				backstoryResult = Backstory.Roll(r, rng, GameYear(), GameMonth(), derive: true, keepBorn: true);
				GrudgeLog($"HISTORY {r.Name}: {r.KinRelation} of {nemesisRecord?.Name} - born {r.BornYear} (his {nemesisRecord?.BornYear}), life rewritten");
				if (rules >= 2)
				{
					Personality? personality = r.Personality;
					r.Personality = Backstory.PickTemper(backstoryResult, r.Gang != null, rng);
					if (r.Personality != personality)
					{
						r.Aggression = Math.Max(0, Math.Min(100, r.Aggression + Traits.AggressionBase(r.Personality) - Traits.AggressionBase(personality)));
						GrudgeLog($"HISTORY {r.Name}: his rewritten life makes him {r.Personality} (was {personality})");
					}
				}
			}
		}
		Backstory.Apply(r, backstoryResult);
		if (rules >= 2)
		{
			r.LifeFx = backstoryResult.Fx.Concat(from kv in backstoryResult.Tilt
				where kv.Value != 0
				select $"t:{kv.Key.ToString().ToLowerInvariant()}:{kv.Value}").ToList();
		}
		if (mind)
		{
			Mind.Roll(r, rng, GameMinutesNow());
		}
	}

	private void EnsurePast(NemesisRecord r)
	{
		if (r.PastRolled)
		{
			return;
		}
		try
		{
			Backstory.Roll(r, (r.Id != null) ? Seeds.For(r, "life") : _rng, GameYear(), GameMonth(), derive: false);
		}
		catch (Exception ex)
		{
			GrudgeLog("HISTORY life failed for " + r.Name + ": " + ex.Message);
		}
	}

	private void UpdateHistory()
	{
		if (Game.GameTime < _nextHistoryCheck)
		{
			return;
		}
		_nextHistoryCheck = Game.GameTime + 15000;
		if (!Chronicle.HistorySeeded)
		{
			EnsureWorldHistory();
			return;
		}
		int num = 0;
		foreach (NemesisRecord item in Roster.Where((NemesisRecord r) => r.PastRolled && !r.Woven).Take(25).ToList())
		{
			Weave(item);
			num++;
		}
		int year = GameYear();
		int month = GameMonth();
		Gang[] all = Gangs.All;
		foreach (Gang g in all)
		{
			if (Roster.Any((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && n.PastRolled) && Chronicle.Past.Count((WorldEvent e) => e.Gangs.Contains(g.Id) && (e.Kind == "RAID" || e.Kind == "CLASH" || e.Kind == "DUTY") && e.People.Any((WorldPart p) => Roster.Any((NemesisRecord x) => x.Id == p.Id && x.IsAlive))) < 2)
			{
				num += GangPast(g, 2, year, month);
			}
		}
		SyncTies();
		if (num > 0)
		{
			SaveRoster();
			SaveWorld();
		}
	}

	private void EnsureWorldHistory()
	{
		WorldState chronicle = Chronicle;
		if (chronicle.HistorySeeded || !Roster.Any((NemesisRecord n) => n.Gang != null && n.IsAlive))
		{
			return;
		}
		int num = GameYear();
		int month = GameMonth();
		chronicle.Past = new List<WorldEvent>();
		foreach (NemesisRecord item in Roster)
		{
			if (item.Past == null)
			{
				continue;
			}
			item.Past.RemoveAll((PastEntry e) => e.Kind == "world" || e.Kind == "kin" || e.Kind == "friend" || e.Kind == "weave");
			foreach (PastEntry item2 in item.Past)
			{
				item2.WorldId = null;
			}
			item.Woven = false;
			EnsurePast(item);
		}
		Lore(num, month);
		if (Gangs.Era >= 1907)
		{
			EraLore(fresh: true);
			Apply1907Seats(GameMinutesNow(), _rng, transition: false);
		}
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			GangPast(gang, (gang.Id == "odriscoll") ? 5 : ((gang.Id == "lemoyneraiders" || gang.Id == "delobo") ? 4 : ((gang.Id == "army" || gang.Id == "wapiti" || gang.Id == "laramie") ? 3 : 2)), num, month);
		}
		RankYears(num);
		chronicle.HistorySeeded = true;
		chronicle.HistoryYear = num;
		GrudgeLog($"HISTORY seeded for {num}: {chronicle.Past.Count} past events, {Roster.Count((NemesisRecord r) => r.PastRolled)} lives");
		Feed($"HISTORY the years before {num}: {chronicle.Past.Count} things the world remembers", Theme.Gold);
		SaveWorld();
		SaveRoster();
	}

	private WorldEvent PastEvent(string kind, int year, int month, string title, string place, bool heard, params string[] gangs)
	{
		int num = GameYear();
		int num2 = GameMonth();
		if (year > num)
		{
			year = num;
		}
		if (month < 1 || month > 12)
		{
			month = 1 + _rng.Next(12);
		}
		if (year == num && month > num2)
		{
			month = num2;
		}
		int maxValue = 27;
		if (year == num && month == num2)
		{
			int cLOCK_DAY_OF_MONTH = CLOCK.GET_CLOCK_DAY_OF_MONTH();
			if (cLOCK_DAY_OF_MONTH <= 1)
			{
				if (month > 1)
				{
					month--;
				}
				else
				{
					year--;
					month = 12;
				}
			}
			else
			{
				maxValue = Math.Min(27, cLOCK_DAY_OF_MONTH - 1);
			}
		}
		DateTime d = new DateTime(year, month, 1 + _rng.Next(maxValue), 6 + _rng.Next(16), _rng.Next(60), 0);
		WorldEvent worldEvent = new WorldEvent
		{
			Id = NewId(10),
			Kind = kind,
			GameMin = GameDate.ToMinutes(d),
			Day = GameDate.DayKey(d),
			When = $"{Months[d.Month - 1]} {d.Year}",
			Title = title,
			Place = place,
			Heard = heard,
			IsPast = true
		};
		worldEvent.Gangs.AddRange(gangs.Where((string g) => g != null));
		Chronicle.Past.Add(worldEvent);
		return worldEvent;
	}

	private void Lore(int year, int month)
	{
		int num = year - 6;
		foreach (LoreRow loreRow in LoreRows)
		{
			int num2 = ((loreRow.Year != 0) ? loreRow.Year : (year - loreRow.YearsAgo));
			if (num2 >= num - 1 && num2 <= year && !string.IsNullOrEmpty(loreRow.Title))
			{
				WorldEvent worldEvent = PastEvent("LORE", num2, loreRow.Month, loreRow.Title, loreRow.Place ?? "", true, loreRow.Gang);
				worldEvent.Lines.AddRange(loreRow.Lines ?? new List<string>());
				if (!string.IsNullOrEmpty(loreRow.When))
				{
					worldEvent.When = loreRow.When;
				}
			}
		}
	}

	private int JoinYearOf(NemesisRecord r)
	{
		int val = (r.Past?.FirstOrDefault((PastEntry p) => p.Kind == "gang"))?.Year ?? ((r.BornYear > 0) ? (r.BornYear + 18) : (GameYear() - 2));
		if (r.BornYear > 0)
		{
			val = Math.Max(val, r.BornYear + 15);
		}
		return Math.Min(val, GameYear());
	}

	private int GangPast(Gang g, int count, int year, int month)
	{
		if (!GangTales.TryGetValue(g.Id, out (string, string, string[], string, int, string)[] value) || value == null || value.Length == 0)
		{
			return 0;
		}
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.Gang == g.Id && n.IsAlive && n.PastRolled).ToList();
		if (list.Count == 0)
		{
			return 0;
		}
		HashSet<int> hashSet = new HashSet<int>();
		for (int num = 0; num < count; num++)
		{
			int num2 = _rng.Next(value.Length);
			for (int num3 = 0; num3 < 4; num3++)
			{
				if (!hashSet.Contains(num2))
				{
					break;
				}
				num2 = _rng.Next(value.Length);
			}
			hashSet.Add(num2);
			(string, string, string[], string, int, string) tuple = value[num2];
			List<NemesisRecord> list2 = list.Where((NemesisRecord n) => n.Rank >= 2).ToList();
			NemesisRecord lead = ((list2.Count > 0) ? list2 : list)[_rng.Next(((list2.Count > 0) ? list2 : list).Count)];
			int ey = Math.Max(year - 6, JoinYearOf(lead)) + _rng.Next(Math.Max(1, year - Math.Max(year - 6, JoinYearOf(lead)) + 1));
			ey = Math.Min(year, ey);
			List<NemesisRecord> list3 = (from _ in list
				where _ != lead && _.BossId == lead.Id && JoinYearOf(_) <= ey
				orderby _rng.Next()
				select _).Take(1 + _rng.Next(3)).ToList();
			if (list3.Count == 0)
			{
				list3 = (from _ in list
					where _ != lead && JoinYearOf(_) <= ey
					orderby _rng.Next()
					select _).Take(_rng.Next(3)).ToList();
			}
			string text = tuple.Item3[_rng.Next(tuple.Item3.Length)];
			WorldEvent worldEvent = PastEvent(tuple.Item1, ey, 1 + _rng.Next(12), tuple.Item2.Replace("{place}", text), text, _rng.Next(100) < tuple.Item5, g.Id, tuple.Item4);
			worldEvent.Lines.Add(tuple.Item6);
			foreach (NemesisRecord item in new NemesisRecord[1] { lead }.Concat(list3))
			{
				RideIn(worldEvent, item, tuple.Item1);
			}
			Gang foe = ((tuple.Item4 != null) ? Gangs.ById(tuple.Item4) : null);
			if (foe != null)
			{
				foreach (NemesisRecord item2 in (from _ in Roster
					where _.Gang == foe.Id && _.IsAlive && _.PastRolled && JoinYearOf(_) <= ey
					orderby _rng.Next()
					select _).Take(_rng.Next(3)))
				{
					RideIn(worldEvent, item2, tuple.Item1);
				}
			}
			if (tuple.Item1 == "CLASH" || tuple.Item1 == "RAID")
			{
				int num4 = _rng.Next(4);
				if (num4 > 0)
				{
					worldEvent.UnnamedDead[g.Id] = num4;
				}
				if (foe != null && _rng.Next(2) == 0)
				{
					worldEvent.UnnamedDead[foe.Id] = 1 + _rng.Next(3);
				}
			}
		}
		GrudgeLog($"HISTORY {g.Name}: {count} tales from the years before");
		return count;
	}

	private void RideIn(WorldEvent ev, NemesisRecord r, string kind)
	{
		if (ev.People.Any((WorldPart p) => p.Id == r.Id))
		{
			return;
		}
		int num = _rng.Next(100);
		string text = ((!(kind == "CLASH") && !(kind == "RAID")) ? "UNHURT" : ((num < 20) ? "WOUNDED" : ((num < 38) ? "DISTINGUISHED" : ((num < 46 && r.Personality == Personality.Coward) ? "FLED" : "UNHURT"))));
		WorldPart worldPart = new WorldPart
		{
			Id = r.Id,
			Name = r.Name,
			Gang = r.Gang,
			Outcome = text
		};
		string text2 = text switch
		{
			"FLED" => "Ran when the shooting started: " + ev.Title, 
			"DISTINGUISHED" => "Made his name: " + ev.Title, 
			"WOUNDED" => "Took a bullet: " + ev.Title, 
			_ => "Rode in it: " + ev.Title, 
		};
		bool flag = !r.TemperKnown;
		if (text == "WOUNDED")
		{
			worldPart.Effects.Add("old wound");
			if (flag)
			{
				r.Aggression = Math.Min(100, r.Aggression + 3);
			}
		}
		if (text == "DISTINGUISHED")
		{
			worldPart.Effects.Add("made his name");
		}
		if (text == "FLED")
		{
			worldPart.Effects.Add("ran");
			if (flag)
			{
				r.Fear = Math.Min(30, r.Fear + 3);
			}
		}
		ev.People.Add(worldPart);
		int month = Array.IndexOf(Months, (ev.When ?? "").Split(' ')[0]) + 1;
		r.Past.Add(new PastEntry
		{
			Year = ev.Day / 10000,
			Month = month,
			Text = text2,
			Kind = "world",
			WorldId = ev.Id,
			Public = ev.Heard
		});
		r.Past = (from e in r.Past
			orderby e.Year, e.Month
			select e).ToList();
	}

	private void RankYears(int year)
	{
		Gang[] all = Gangs.All;
		foreach (Gang g in all)
		{
			foreach (IGrouping<int, (NemesisRecord, PastEntry)> item in from x in Roster.Where((NemesisRecord n) => n.Gang == g.Id && n.Past != null).SelectMany((NemesisRecord n) => from e in n.Past
					where e.Kind == "rank" && e.Year >= year - 6
					select (n: n, e: e)).ToList()
				group x by x.e.Year into x
				orderby x.Key
				select x)
			{
				WorldEvent worldEvent = PastEvent("RANKS", item.Key, 12, $"{g.Name}: promotions, {item.Key}", "", false, g.Id);
				if (item.Key == year)
				{
					worldEvent.When = $"{item.Key} so far";
				}
				foreach (var (nemesisRecord, pastEntry) in item)
				{
					worldEvent.People.Add(new WorldPart
					{
						Id = nemesisRecord.Id,
						Name = nemesisRecord.Name,
						Gang = g.Id,
						Outcome = "PROMOTED",
						Note = pastEntry.Text
					});
					pastEntry.WorldId = worldEvent.Id;
				}
			}
		}
	}

	private void Weave(NemesisRecord r)
	{
		try
		{
			int year = GameYear();
			GameMonth();
			Gang gang = Gangs.ById(r.Gang);
			r.Past = r.Past ?? new List<PastEntry>();
			NemesisRecord nemesisRecord = ((r.BossId != null) ? Roster.FirstOrDefault((NemesisRecord b) => b.Id == r.BossId) : null);
			if (nemesisRecord != null && gang != null && !r.Past.Any((PastEntry e) => e.Kind == "weave"))
			{
				int year2 = JoinYearOf(r);
				int month = r.Past.FirstOrDefault((PastEntry e) => e.Kind == "gang")?.Month ?? 0;
				string text = ((r.Gang == "army") ? ("Served under " + nemesisRecord.Name + " from the start") : ((r.Gang == "wapiti") ? ("Learned the warrior's road from " + nemesisRecord.Name) : ("Brought into the " + gang.Name + " by " + nemesisRecord.Name)));
				r.Past.Add(new PastEntry
				{
					Year = year2,
					Month = month,
					Text = text,
					Kind = "weave"
				});
				if (nemesisRecord.Past != null && !nemesisRecord.Past.Any((PastEntry e) => e.Kind == "weave" && e.Text.Contains(r.Name)))
				{
					nemesisRecord.Past.Add(new PastEntry
					{
						Year = year2,
						Month = month,
						Text = ((r.Gang == "army") ? ("Took " + r.Name + " into his command") : ((r.Gang == "wapiti") ? ("Taught " + r.Name + " the warrior's road") : ("Brought " + r.Name + " into the " + gang.Name))),
						Kind = "weave"
					});
				}
			}
			NemesisRecord kin = ((r.KinId != null) ? Roster.FirstOrDefault((NemesisRecord k) => k.Id == r.KinId) : null);
			if (kin != null && !r.Past.Any((PastEntry e) => e.Kind == "kin" && e.Text.Contains(kin.Name)))
			{
				string text2 = r.KinRelation ?? "kin";
				string text3 = Traits.Inverse(text2);
				int year3 = Math.Min(year, Math.Max(r.BornYear, kin.BornYear) + 16 + _rng.Next(4));
				r.Past.Add(new PastEntry
				{
					Year = year3,
					Month = 0,
					Text = ((r.Gang != null && r.Gang == kin.Gang) ? ("Rides with his " + text3 + ", " + kin.Name) : ("Keeps in touch with his " + text3 + ", " + kin.Name)),
					Kind = "kin"
				});
				kin.Past?.Add(new PastEntry
				{
					Year = year3,
					Month = 0,
					Text = "Looks out for his " + text2 + ", " + r.Name,
					Kind = "kin"
				});
			}
			foreach (string fid in r.FriendIds ?? new List<string>())
			{
				NemesisRecord f = Roster.FirstOrDefault((NemesisRecord x) => x.Id == fid);
				if (f == null || f.Past == null || r.Past.Any((PastEntry e) => e.Kind == "friend" && e.Text.Contains(f.Name)))
				{
					continue;
				}
				PastEntry shared = r.Past.Where((PastEntry e) => e.WorldId != null && e.Kind == "world").FirstOrDefault((PastEntry e) => f.Past.Any((PastEntry x) => x.WorldId == e.WorldId));
				WorldEvent worldEvent = ((shared != null) ? Chronicle.Past.FirstOrDefault((WorldEvent x) => x.Id == shared.WorldId) : null);
				int num = shared?.Year ?? Math.Min(year, Math.Max(JoinYearOf(r), JoinYearOf(f)));
				string text4 = ((worldEvent != null) ? ("Friends with " + f.Name + " since " + Lower(worldEvent.Title)) : $"Friends with {{0}} since {num}");
				r.Past.Add(new PastEntry
				{
					Year = num,
					Month = (shared?.Month ?? 0),
					Text = text4.Replace("{0}", f.Name),
					Kind = "friend"
				});
				f.Past.Add(new PastEntry
				{
					Year = num,
					Month = (shared?.Month ?? 0),
					Text = ((worldEvent != null) ? ("Friends with " + r.Name + " since " + Lower(worldEvent.Title)) : $"Friends with {r.Name} since {num}"),
					Kind = "friend"
				});
			}
			foreach (PastEntry item in r.Past.Where((PastEntry e) => e.MirrorKind != null && e.WorldId == null && e.Year >= year - 6).ToList())
			{
				WorldEvent worldEvent2 = PastEvent(item.MirrorKind, item.Year, item.Month, (item.MirrorTitle ?? "").Replace("{name}", r.Name), item.MirrorPlace ?? "", item.MirrorHeard, r.Gang ?? "");
				worldEvent2.People.Add(new WorldPart
				{
					Id = r.Id,
					Name = r.Name,
					Gang = r.Gang,
					Outcome = ((item.MirrorKind == "BRAWL") ? "WON" : "INVOLVED"),
					Note = item.Text
				});
				item.WorldId = worldEvent2.Id;
				item.Public |= worldEvent2.Heard;
			}
			if (gang != null && !r.Past.Any((PastEntry e) => e.Kind == "world") && _rng.Next(100) < 45)
			{
				WorldEvent worldEvent3 = (from _ in Chronicle.Past
					where _.Gangs.Contains(gang.Id) && (_.Kind == "RAID" || _.Kind == "CLASH" || _.Kind == "DUTY") && _.Day / 10000 >= JoinYearOf(r) && _.People.Count < 7
					orderby _rng.Next()
					select _).FirstOrDefault();
				if (worldEvent3 != null)
				{
					RideIn(worldEvent3, r, worldEvent3.Kind);
				}
			}
			if (gang == null && _rng.Next(100) < 30)
			{
				string home = (r.Birthplace ?? "").Split(',')[0];
				WorldEvent worldEvent4 = Chronicle.Past.FirstOrDefault((WorldEvent x) => x.Kind == "LORE" && !string.IsNullOrEmpty(x.Place) && (x.Place == home || x.Place == r.KillLocation) && (r.BornYear <= 0 || x.Day / 10000 >= r.BornYear + 14));
				if (worldEvent4 != null && !worldEvent4.People.Any((WorldPart p) => p.Id == r.Id))
				{
					worldEvent4.People.Add(new WorldPart
					{
						Id = r.Id,
						Name = r.Name,
						Gang = null,
						Outcome = "WITNESS",
						Note = "was there"
					});
					int month2 = Array.IndexOf(Months, (worldEvent4.When ?? "").Split(' ')[0]) + 1;
					r.Past.Add(new PastEntry
					{
						Year = worldEvent4.Day / 10000,
						Month = month2,
						Text = "Was in " + worldEvent4.Place + " for it: " + worldEvent4.Title,
						Kind = "world",
						WorldId = worldEvent4.Id,
						Public = true
					});
				}
			}
			r.Past = (from e in r.Past
				orderby e.Year, e.Month
				select e).ToList();
			r.Woven = true;
		}
		catch (Exception ex)
		{
			r.Woven = true;
			GrudgeLog("HISTORY weave failed for " + r.Name + ": " + ex.Message);
		}
	}

	private static string Lower(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToLowerInvariant(s[0]) + s.Substring(1);
		}
		return s;
	}

	private void RevealPast(NemesisRecord r, string what, params string[] kinds)
	{
		if (r?.Past == null)
		{
			return;
		}
		List<PastEntry> list = r.Past.Where((PastEntry e) => !e.Known && !e.Public && kinds.Contains(e.Kind)).ToList();
		foreach (PastEntry item in list)
		{
			item.Known = true;
		}
		if (list.Count > 0)
		{
			Feed("PAST " + r.Name + " told you about " + what + ": " + string.Join("; ", from e in list.Take(3)
				select e.WhenText + " " + Lower(e.Text)) + ((list.Count > 3) ? $" (+{list.Count - 3})" : ""), Backstory.KindColor(list[0].Kind), r);
		}
	}

	internal bool PastShown(NemesisRecord n, PastEntry e)
	{
		if (!_debugOn && !e.Known)
		{
			if (e.Public)
			{
				return n.TemperKnown;
			}
			return false;
		}
		return true;
	}

	internal string HistoryDebugLine()
	{
		WorldState chronicle = Chronicle;
		return string.Format("history: {0} - {1} past events, {2} lives ({3} woven)", chronicle.HistorySeeded ? $"seeded for {chronicle.HistoryYear}" : "not seeded yet", chronicle.Past?.Count ?? 0, Roster.Count((NemesisRecord r) => r.PastRolled), Roster.Count((NemesisRecord r) => r.Woven));
	}
}
