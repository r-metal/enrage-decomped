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
	private static readonly Dictionary<string, string> BuiltInRegions = new Dictionary<string, string>
	{
		["heartlands"] = "DISTRICT_HEARTLAND",
		["cumberland"] = "DISTRICT_CUMBERLAND_FOREST",
		["grizzlies_east"] = "DISTRICT_GRIZZLIES_EAST",
		["grizzlies_west"] = "DISTRICT_GRIZZLIES_WEST",
		["grizzlies"] = "DISTRICT_GRIZZLIES_WEST",
		["roanoke"] = "DISTRICT_ROANOKE_RIDGE",
		["big_valley"] = "DISTRICT_BIG_VALLEY",
		["tall_trees"] = "DISTRICT_TALL_TREES",
		["great_plains"] = "DISTRICT_GREAT_PLAINS",
		["scarlett_meadows"] = "DISTRICT_SCARLETT_MEADOWS",
		["bluewater_marsh"] = "DISTRICT_BLUEWATER_MARSH",
		["bayou_nwa"] = "DISTRICT_BAYOU_NWA",
		["bluegill_marsh"] = "DISTRICT_BLUEGILL_MARSH",
		["hennigans_stead"] = "DISTRICT_HENNIGANS_STEAD",
		["cholla_springs"] = "DISTRICT_CHOLLA_SPRINGS",
		["gaptooth_ridge"] = "DISTRICT_GAPTOOTH_RIDGE",
		["rio_bravo"] = "DISTRICT_RIO_BRAVO"
	};

	internal static readonly District[] BuiltInDistricts = new District[17]
	{
		E7(D("heartlands", 364f, 396f, "the Heartlands", new uint[2] { 131399519u, 3633221156u }, ("odriscoll", 29), ("army", 27)), ("army", 30), ("laramie", 8)),
		E7(D("cumberland", 316f, 1264f, "Cumberland Forest", new uint[3] { 1835499550u, 4062017125u, 2804123861u }, ("army", 27), ("odriscoll", 29), ("wapiti", 9)), ("army", 30)),
		E7(D("grizzlies_east", 1168f, 1452f, "the Grizzlies East", new uint[2] { 2311828277u, 4174810561u }, ("odriscoll", 29), ("army", 27), ("wapiti", 27)), ("army", 30)),
		E7(D("grizzlies_west", -1212f, 1720f, "the Grizzlies West", new uint[2] { 2721678695u, 1645618177u }, ("odriscoll", 29), ("army", 27)), ("army", 22)),
		E7(D("grizzlies", -1218f, 1923f, "the Grizzlies", new uint[1] { 326175668u }, ("odriscoll", 29), ("army", 27)), ("army", 22)),
		E7(D("roanoke", 2464f, 1072f, "Roanoke Ridge", new uint[3] { 178647645u, 1921384287u, 2101155660u }, ("murfree", 36), ("odriscoll", 15), ("army", 27)), ("murfree", 40), ("army", 25)),
		E7(D("big_valley", -1784f, -128f, "Big Valley", new uint[2] { 822658194u, 3485840081u }, ("odriscoll", 18), ("army", 7)), ("laramie", 22), ("army", 8)),
		E7(D("tall_trees", -2024f, -1656f, "Tall Trees", new uint[2] { 1684533001u, 1706859953u }, ("skinners", 36), ("odriscoll", 7), ("army", 7)), ("skinners", 40), ("army", 7)),
		E7(D("great_plains", -1232f, -1476f, "the Great Plains", new uint[2] { 210917480u, 476637847u }, ("odriscoll", 18), ("skinners", 11), ("army", 7)), ("laramie", 36), ("skinners", 12), ("army", 7)),
		E7(D("scarlett_meadows", 1416f, -1160f, "Scarlett Meadows", new uint[2] { 687704893u, 3430691604u }, ("lemoyneraiders", 45), ("klan", 27), ("army", 5), ("odriscoll", 3)), ("lemoyneraiders", 40), ("klan", 25), ("army", 6)),
		E7(D("bluewater_marsh", 2082f, -604f, "Bluewater Marsh", new uint[2] { 3824751532u, 1308232528u }, ("lemoyneraiders", 45), ("klan", 27), ("army", 5), ("odriscoll", 3)), ("lemoyneraiders", 40), ("klan", 25), ("army", 6)),
		E7(D("bayou_nwa", 2232f, -1064f, "Bayou Nwa", new uint[2] { 2025841068u, 3374368891u }, ("lemoyneraiders", 45), ("bronte", 36), ("army", 5), ("odriscoll", 3)), ("bronte", 36), ("lemoyneraiders", 30), ("nightfolk", 20), ("army", 6)),
		E7(D("bluegill_marsh", 3120f, -84f, "Bluegill Marsh", new uint[2] { 3436390235u, 238611628u }, ("lemoyneraiders", 20), ("army", 5), ("odriscoll", 3)), ("lemoyneraiders", 20), ("nightfolk", 6), ("army", 6)),
		E7(D("hennigans_stead", -2208f, -2644f, "Hennigan's Stead", new uint[2] { 892930832u, 4241435510u }), ("laramie", 40), ("delobo", 8), ("army", 5)),
		E7(D("cholla_springs", -4128f, -2654f, "Cholla Springs", new uint[2] { 4186119282u, 736160934u }), ("delobo", 40), ("laramie", 8), ("army", 7)),
		E7(D("gaptooth_ridge", -5568f, -3044f, "Gaptooth Ridge", new uint[2] { 2228727054u, 3946348553u }), ("delobo", 45), ("army", 5)),
		E7(D("rio_bravo", -4528f, -3604f, "Rio Bravo", new uint[2] { 2148975167u, 779743808u }), ("delobo", 45))
	};

	internal static District[] Districts = BuiltInDistricts;

	private readonly Dictionary<string, int> _controlHeard = new Dictionary<string, int>();

	private bool _quietControl;

	private int _nextWarCheck;

	private int _nextWarDayPeek;

	private long _warDayIdx = -1L;

	private int _warMissedDays;

	private CampKit _kit;

	private readonly Dictionary<string, int> _campHeldOff = new Dictionary<string, int>();

	private static District D(string key, float x, float y, string name, uint[] hashes, params (string g, int hold)[] lore)
	{
		string value;
		return new District
		{
			Key = key,
			X = x,
			Y = y,
			Name = name,
			Hashes = hashes,
			Lore1899 = lore.ToDictionary(((string g, int hold) tuple) => tuple.g, ((string g, int hold) tuple) => tuple.hold),
			Region = (BuiltInRegions.TryGetValue(key, out value) ? value : null)
		};
	}

	private static District E7(District d, params (string g, int hold)[] lore)
	{
		d.Lore1907 = lore.ToDictionary(((string g, int hold) x2) => x2.g, ((string g, int hold) x2) => x2.hold);
		return d;
	}

	internal (List<string> holds, List<string> strong, List<string> foothold) GroundOf(Gang g)
	{
		List<(string, int)> list = new List<(string, int)>();
		List<(string t, int r)> list2 = new List<(string t, int r)>();
		List<(string, int)> list3 = new List<(string, int)>();
		if (g != null)
		{
			District[] districts = Districts;
			foreach (District district in districts)
			{
				if (ControlMap.TryGetValue(district.Key, out var value) && value.TryGetValue(g.Id, out var value2) && value2 >= 10)
				{
					int num = (from kv in value
						where kv.Key != g.Id && !IsDestroyed(Gangs.ById(kv.Key))
						select kv.Value).DefaultIfEmpty(0).Max();
					((value2 > num) ? list : ((value2 >= 30) ? list2 : list3)).Add((district.Name, value2));
				}
			}
		}
		return (holds: S(list), strong: S(list2), foothold: S(list3));
		static List<string> S(List<(string n, int v)> l)
		{
			return (from x in l
				orderby x.v descending
				select x.n).ToList();
		}
	}

	internal static District DistrictOf(uint zoneHash)
	{
		if (zoneHash != 0)
		{
			return Districts.FirstOrDefault((District d) => d.Hashes.Contains(zoneHash));
		}
		return null;
	}

	internal District DistrictAt(Vector3 p)
	{
		try
		{
			return DistrictOf(ZONE._GET_MAP_ZONE_AT_COORDS(p.X, p.Y, p.Z, 10));
		}
		catch
		{
			return null;
		}
	}

	internal District CurrentDistrict => DistrictOf(_regionDistrict);

	private Dictionary<string, Dictionary<string, int>> ControlMap
	{
		get
		{
			WorldState chronicle = Chronicle;
			if (chronicle.Control == null)
			{
				chronicle.Control = new Dictionary<string, Dictionary<string, int>>();
			}
			District[] districts = Districts;
			foreach (District district in districts)
			{
				if (!chronicle.Control.ContainsKey(district.Key))
				{
					chronicle.Control[district.Key] = new Dictionary<string, int>(district.Lore);
					SaveWorld();
				}
			}
			return chronicle.Control;
		}
	}

	internal int HoldOf(District d, string gangId)
	{
		if (d == null || gangId == null || !ControlMap[d.Key].TryGetValue(gangId, out var value))
		{
			return 0;
		}
		return value;
	}

	internal double HoldMult(District d, Gang g)
	{
		return (double)HoldOf(d, g.Id) / 18.0;
	}

	internal void ShiftControl(District d, Gang g, int delta, string why)
	{
		if (d == null || g == null || delta == 0 || g.StoryOnly)
		{
			return;
		}
		Dictionary<string, int> m = ControlMap[d.Key];
		m.TryGetValue(g.Id, out var value);
		int num = Math.Max(0, Math.Min(100, value + delta));
		if (num == value)
		{
			return;
		}
		m[g.Id] = num;
		int num2 = m.Values.Sum() - 100;
		if (num2 > 0)
		{
			foreach (string item in (from k in m.Keys
				where k != g.Id
				orderby m[k] descending
				select k).ToList())
			{
				if (num2 <= 0)
				{
					break;
				}
				int num3 = Math.Min(num2, m[item]);
				m[item] -= num3;
				num2 -= num3;
			}
		}
		SaveWorld();
		GrudgeLog($"CONTROL {g.Name} {value} -> {num} in {d.Name} ({why})");
		string key = d.Key + "|" + g.Id;
		if (!_controlHeard.TryGetValue(key, out var value2))
		{
			value2 = (d.Lore.TryGetValue(g.Id, out var value3) ? value3 : 0);
		}
		if (Math.Abs(num - value2) >= 10)
		{
			_controlHeard[key] = num;
			string text = ((num > value2) ? ("The " + g.Name + " are gaining ground in " + d.Name + ".") : ("The " + g.Name + " are losing their grip on " + d.Name + "."));
			Feed($"TERRITORY {text} ({value2} to {num})", (num > value2) ? Theme.Warn : Theme.Good);
			if (!_quietControl && !_worldCatchingUp)
			{
				GrudgeNotice(text);
			}
		}
	}

	private void ControlAfterClash(WorldEvent ev, Gang winner, Gang loser, bool overrun)
	{
		if (ev != null && ev.HasPos)
		{
			District district = DistrictAt(new Vector3(ev.X, ev.Y, ev.Z));
			if (district != null)
			{
				int num = 5 + (overrun ? 3 : 0);
				ShiftControl(district, winner, num, "won a clash with the " + loser.Name + (overrun ? ", took a camp" : ""));
				ShiftControl(district, loser, -LossDown(district, loser, num), "lost a clash to the " + winner.Name);
				ev.Lines.Add(district.Name + ": the " + winner.Name + " hold more of it now.");
				PurseForClash(winner, loser);
			}
		}
	}

	private int LossDown(District d, Gang g, int amt)
	{
		int value;
		int num = ((Manpower(g) >= 0.5 && d.Lore.TryGetValue(g.Id, out value)) ? (value / 3) : 0);
		int num2 = HoldOf(d, g.Id);
		return Math.Max(0, Math.Min(amt, num2 - num));
	}

	private void ControlAfterKill(Gang g, int rank, Vector3 where)
	{
		if (g == null)
		{
			return;
		}
		ShiftControl(DistrictAt(where), g, -Math.Max(1, rank), (rank > 0) ? $"you killed one of their rank {rank} men" : "you killed one of their men");
		LoseMen(g, NamedWorth(rank), (rank > 0) ? $"you killed one of their rank {rank} men" : "you killed one of their men");
		try
		{
			NoteWarKill(g, rank, where);
		}
		catch (Exception ex)
		{
			GrudgeLog("WAR kill note failed: " + ex.Message);
		}
		try
		{
			NoteEnemyHurt(g, rank);
		}
		catch (Exception ex2)
		{
			GrudgeLog("NOTORIETY enemy note failed: " + ex2.Message);
		}
		try
		{
			NoteTownDefence(g, where);
		}
		catch (Exception ex3)
		{
			GrudgeLog("DEED town defence failed: " + ex3.Message);
		}
	}

	private void DriftControl(int days)
	{
		if (days <= 0)
		{
			return;
		}
		try
		{
			DailyPurses(days);
		}
		catch (Exception ex)
		{
			GrudgeLog("PURSE daily failed: " + ex.Message);
		}
		try
		{
			DailyAgendas();
		}
		catch (Exception ex2)
		{
			GrudgeLog("AGENDA daily failed: " + ex2.Message);
		}
		try
		{
			DriftRelations(days);
		}
		catch (Exception ex3)
		{
			GrudgeLog("RELATION drift failed: " + ex3.Message);
		}
		try
		{
			MaybeRemnants();
		}
		catch (Exception ex4)
		{
			GrudgeLog("SANDBOX remnants failed: " + ex4.Message);
		}
		District[] districts = Districts;
		foreach (District district in districts)
		{
			Dictionary<string, int> dictionary = ControlMap[district.Key];
			foreach (string g in dictionary.Keys.Union(district.Lore.Keys).ToList())
			{
				dictionary.TryGetValue(g, out var value);
				Gang gang = Gangs.ById(g);
				int num = (district.Lore.TryGetValue(g, out var value2) ? value2 : 0);
				Dictionary<string, Dictionary<string, double>> dictionary2 = Chronicle.ControlBase ?? (Chronicle.ControlBase = new Dictionary<string, Dictionary<string, double>>());
				if (!dictionary2.TryGetValue(district.Key, out var value3))
				{
					value3 = (dictionary2[district.Key] = new Dictionary<string, double>());
				}
				double value4;
				double num2 = (value3.TryGetValue(g, out value4) ? value4 : ((double)num));
				for (int j = 0; j < Math.Min(days, 30); j++)
				{
					num2 += ((double)value - num2) * 0.04;
				}
				value3[g] = Math.Round(num2, 2);
				num = (int)Math.Round(num2);
				int num3 = (int)Math.Round((double)num * HoldStrength(gang));
				if (gang != null && gang.OrdersNotFeelings)
				{
					num3 = Math.Min(num3, (int)Math.Round((double)Math.Max(value2, 5) * 1.1));
				}
				if (Chronicle.Beaten != null && Chronicle.Beaten.TryGetValue(district.Key + "|" + g, out var value5))
				{
					if (value5 > GameMinutesNow())
					{
						num3 /= 2;
					}
					else
					{
						Chronicle.Beaten.Remove(district.Key + "|" + g);
					}
				}
				int num4 = value;
				for (int k = 0; k < Math.Min(days, 30); k++)
				{
					int num5 = (int)Math.Round((double)(num3 - num4) * 0.15);
					if (num5 == 0 && num3 != num4)
					{
						num5 = Math.Sign(num3 - num4);
					}
					num4 += num5;
				}
				if (num4 > value)
				{
					num4 = Math.Min(num4, value + Math.Max(0, 100 - dictionary.Values.Sum()));
				}
				if (num4 == value)
				{
					continue;
				}
				dictionary[g] = Math.Max(0, num4);
				GrudgeLog(string.Format("CONTROL {0} {1} -> {2} in {3} (a day passes - toward {4}{5})", g, value, dictionary[g], district.Name, num3, (num3 != num) ? $" of {num}: its strength" : ""));
				if (num4 < value)
				{
					KeyValuePair<string, int> keyValuePair = (from kv in dictionary
						where kv.Key != g && !IsDestroyed(Gangs.ById(kv.Key))
						orderby kv.Value descending
						select kv).FirstOrDefault();
					if (keyValuePair.Key != null && dictionary.Values.Sum() < 100)
					{
						dictionary[keyValuePair.Key] = Math.Min(100, keyValuePair.Value + Math.Min(value - num4, 100 - dictionary.Values.Sum()));
					}
				}
			}
		}
		SaveWorld();
	}

	internal string ControlHereLine()
	{
		District currentDistrict = CurrentDistrict;
		if (currentDistrict == null)
		{
			return "control here: (not a district the mod knows)";
		}
		Dictionary<string, int> dictionary = ControlMap[currentDistrict.Key];
		return "control in " + currentDistrict.Name + ": " + string.Join(", ", from kv in dictionary
			where kv.Value > 0
			orderby kv.Value descending
			select $"{Gangs.ById(kv.Key)?.Name ?? kv.Key} {kv.Value}") + $", nobody {Math.Max(0, 100 - dictionary.Values.Sum())}";
	}

	internal IEnumerable<string> ControlLines()
	{
		District[] districts = Districts;
		foreach (District d in districts)
		{
			Dictionary<string, int> source = ControlMap[d.Key];
			yield return d.Name + ": " + string.Join(", ", (from kv in source
				where kv.Value > 0
				orderby kv.Value descending
				select kv).Select(delegate(KeyValuePair<string, int> kv)
			{
				int value;
				int num = (d.Lore.TryGetValue(kv.Key, out value) ? value : 0);
				return string.Format("{0} {1}{2}", Gangs.ById(kv.Key)?.Name ?? kv.Key, kv.Value, (kv.Value != num) ? string.Format(" ({0}{1})", (kv.Value > num) ? "+" : "", kv.Value - num) : "");
			}));
		}
	}

	internal bool AtWarWith(string a, string b)
	{
		if (a != null && b != null && a != b)
		{
			if (Relation(a, b) > Config.WarLine)
			{
				return Wars.Any((WarRecord w) => (w.A == a && w.B == b) || (w.A == b && w.B == a));
			}
			return true;
		}
		return false;
	}

	internal (Gang a, Gang b)? WarIn(District d)
	{
		if (d == null)
		{
			return null;
		}
		Dictionary<string, int> dictionary = ControlMap[d.Key];
		string text = Chronicle.WarZones?.FirstOrDefault((string t) => t.StartsWith(d.Key + ":"));
		if (text != null)
		{
			string[] array = text.Substring(d.Key.Length + 1).Split('|');
			if (array.Length == 2)
			{
				dictionary.TryGetValue(array[0], out var value);
				dictionary.TryGetValue(array[1], out var value2);
				bool flag = Relation(array[0], array[1]) <= Config.ClashLine + 10 && !TruceBetween(array[0], array[1]) && !IsDestroyed(Gangs.ById(array[0])) && !IsDestroyed(Gangs.ById(array[1]));
				WarRecord w = Wars.FirstOrDefault((WarRecord x) => x.District == d.Key);
				bool flag2 = false;
				try
				{
					flag2 = WarSettled(w, d);
				}
				catch
				{
				}
				if (value >= 5 && value2 >= 5 && flag && !flag2)
				{
					Gang gang = Gangs.ById(array[0]);
					if (gang != null)
					{
						Gang gang2 = Gangs.ById(array[1]);
						if (gang2 != null && gang.TurfWars && gang2.TurfWars)
						{
							return (gang, gang2);
						}
					}
				}
			}
		}
		return null;
	}

	internal (Gang a, Gang b)? WarHere => WarIn(CurrentDistrict);

	private bool WarPaused(District d, string a, string b)
	{
		WarRecord warRecord = Wars.FirstOrDefault((WarRecord x) => x.District == d.Key);
		if (warRecord == null || warRecord.Result != null)
		{
			return false;
		}
		if (IsDestroyed(Gangs.ById(a)) || IsDestroyed(Gangs.ById(b)))
		{
			return false;
		}
		if (HoldOf(d, a) < 5 || HoldOf(d, b) < 5)
		{
			return false;
		}
		bool flag = false;
		try
		{
			flag = WarSettled(warRecord, d);
		}
		catch
		{
		}
		return !flag;
	}

	private void UpdateWarZones()
	{
		bool flag = false;
		if (Game.GameTime >= _nextWarDayPeek)
		{
			_nextWarDayPeek = Game.GameTime + 1000;
			long num = GameMinutesNow() / 1440;
			if (_warDayIdx >= 0 && num > _warDayIdx)
			{
				flag = true;
				_warMissedDays = (int)Math.Min(6L, num - _warDayIdx - 1);
			}
			if (num != _warDayIdx)
			{
				_warDayIdx = num;
			}
		}
		if (Game.GameTime < _nextWarCheck && !flag)
		{
			return;
		}
		_nextWarCheck = Game.GameTime + 30000;
		PruneDeals();
		List<string> told = Chronicle.WarZones ?? (Chronicle.WarZones = new List<string>());
		int num2 = told.RemoveAll((string t) => string.IsNullOrEmpty(t) || t.IndexOf(':') <= 0 || !Known(t.Substring(0, t.IndexOf(':'))));
		int num3 = Wars.RemoveAll((WarRecord x) => x == null || !Known(x.District));
		if (num2 + num3 > 0)
		{
			GrudgeLog($"WAR dropped {num2} malformed war-zone entries and {num3} wars in districts that no longer exist");
			SaveWorld();
		}
		foreach (string item in told)
		{
			string dk = item.Substring(0, item.IndexOf(':'));
			string[] array = item.Substring(dk.Length + 1).Split('|');
			if (array.Length == 2 && !Wars.Any((WarRecord x) => x.District == dk))
			{
				District district = Districts.FirstOrDefault((District x) => x.Key == dk);
				if (district != null)
				{
					Wars.Add(new WarRecord
					{
						District = dk,
						A = array[0],
						B = array[1],
						StartMin = GameMinutesNow(),
						HoldA0 = HoldOf(district, array[0]),
						HoldB0 = HoldOf(district, array[1]),
						MenA0 = Math.Max(1, MenOf(Gangs.ById(array[0]))),
						MenB0 = Math.Max(1, MenOf(Gangs.ById(array[1]))),
						LastReportDay = GameDayKey(),
						Cause = RelationCause(array[0], array[1])
					});
				}
			}
		}
		int num4 = GameDayKey();
		int warMissedDays = _warMissedDays;
		_warMissedDays = 0;
		if (warMissedDays > 0)
		{
			GrudgeLog($"WAR {warMissedDays} game day(s) went by unchecked (a time skip) - their war days run now");
		}
		foreach (WarRecord wr in Wars.ToList())
		{
			if (wr.LastReportDay == num4)
			{
				continue;
			}
			District district2 = Districts.FirstOrDefault((District x) => x.Key == wr.District);
			if (district2 == null)
			{
				continue;
			}
			wr.LastReportDay = num4;
			for (int num5 = 0; num5 <= warMissedDays; num5++)
			{
				if (!Wars.Contains(wr))
				{
					break;
				}
				if (wr.Result != null)
				{
					break;
				}
				try
				{
					WarReport(district2, wr);
				}
				catch (Exception ex)
				{
					GrudgeLog("WAR report failed: " + ex.Message);
				}
			}
		}
		try
		{
			RefreshWarMarkers();
		}
		catch (Exception ex2)
		{
			GrudgeLog("WAR markers failed: " + ex2.Message);
		}
		try
		{
			RollWarDeclarations();
		}
		catch (Exception ex3)
		{
			GrudgeLog("WAR roll failed: " + ex3.Message);
		}
		for (int num6 = 0; num6 < warMissedDays; num6++)
		{
			try
			{
				RollWarDeclarations(missedDay: true);
			}
			catch (Exception ex4)
			{
				GrudgeLog("WAR roll failed: " + ex4.Message);
			}
		}
		District[] districts = Districts;
		foreach (District d in districts)
		{
			(Gang, Gang)? tuple = WarIn(d);
			string text = (tuple.HasValue ? (d.Key + ":" + tuple.Value.Item1.Id + "|" + tuple.Value.Item2.Id) : null);
			string text2 = told.FirstOrDefault((string t) => t.StartsWith(d.Key + ":"));
			if (text != null && text2 == null && !BeatenThere(d, tuple.Value.Item1) && PairWar(tuple.Value.Item1.Id, tuple.Value.Item2.Id) == null)
			{
				told.Add(text);
				SaveWorld();
				try
				{
					WarStarted(d, tuple.Value.Item1, tuple.Value.Item2);
				}
				catch (Exception ex5)
				{
					GrudgeLog("WAR event failed: " + ex5.Message);
				}
				string text3 = "War in " + d.Name + ": the " + tuple.Value.Item1.Name + " and the " + tuple.Value.Item2.Name + " are fighting over it.";
				Feed("TERRITORY " + text3, Theme.Warn);
				GrudgeNotice(text3);
				GrudgeLog("WAR " + text3);
			}
			else
			{
				if (text != null || text2 == null)
				{
					continue;
				}
				string[] array2 = text2.Substring(d.Key.Length + 1).Split('|');
				if (array2.Length == 2 && TruceBetween(array2[0], array2[1]) && WarPaused(d, array2[0], array2[1]))
				{
					continue;
				}
				if (array2.Length == 2 && TruceBetween(array2[0], array2[1]))
				{
					WarRecord warRecord = Wars.FirstOrDefault((WarRecord x) => x.District == d.Key);
					GrudgeLog(string.Format("WAR {0}: a truce, and the war ends rather than pauses - record {1}, holds {2} / {3}{4}", d.Name, (warRecord == null) ? "missing" : (warRecord.Result ?? "open"), HoldOf(d, array2[0]), HoldOf(d, array2[1]), (IsDestroyed(Gangs.ById(array2[0])) || IsDestroyed(Gangs.ById(array2[1]))) ? ", a side is finished" : ""));
				}
				told.Remove(text2);
				SaveWorld();
				string[] array3 = text2.Substring(d.Key.Length + 1).Split('|');
				if (array3.Length == 2)
				{
					try
					{
						WarEnded(d, array3[0], array3[1]);
					}
					catch (Exception ex6)
					{
						GrudgeLog("WAR event failed: " + ex6.Message);
					}
				}
				(Chronicle.WarEndedAt ?? (Chronicle.WarEndedAt = new Dictionary<string, long>()))[d.Key] = GameMinutesNow();
				KeyValuePair<string, int> keyValuePair = ControlMap[d.Key].OrderByDescending((KeyValuePair<string, int> kv) => kv.Value).FirstOrDefault();
				WarRecord warRecord2 = Chronicle.PastWars?.LastOrDefault((WarRecord x) => x.District == d.Key);
				string text4 = ((!string.IsNullOrEmpty(warRecord2?.EndTitle)) ? (warRecord2.EndTitle + ".") : (d.Name + " quiets down - the " + (Gangs.ById(keyValuePair.Key)?.Name ?? "nobody") + " hold it."));
				Feed("TERRITORY " + text4, Theme.Dim);
				GrudgeNotice(text4);
				GrudgeLog("WAR " + text4);
			}
		}
		foreach (WarRecord orphan in Wars.Where((WarRecord x) => !told.Any((string t) => t.StartsWith(x.District + ":"))).ToList())
		{
			GrudgeLog("WAR " + orphan.District + ": the record outlived its war - ended");
			District district3 = Districts.FirstOrDefault((District x) => x.Key == orphan.District);
			if (district3 != null)
			{
				try
				{
					WarEnded(district3, orphan.A, orphan.B);
				}
				catch (Exception ex7)
				{
					GrudgeLog("WAR event failed: " + ex7.Message);
					Wars.Remove(orphan);
				}
			}
			else
			{
				Wars.Remove(orphan);
			}
			(Chronicle.WarEndedAt ?? (Chronicle.WarEndedAt = new Dictionary<string, long>()))[orphan.District] = GameMinutesNow();
			SaveWorld();
		}
		static bool Known(string key)
		{
			return Districts.Any((District x) => x.Key == key);
		}
	}

	private void ControlAfterSkirmish(Vector3 at, Gang winner, Gang loser)
	{
		District district = DistrictAt(at);
		if (district != null && winner != null && loser != null)
		{
			ShiftControl(district, winner, 5, "held the field against the " + loser.Name);
			ShiftControl(district, loser, -LossDown(district, loser, 5), "lost a skirmish to the " + winner.Name);
		}
	}

	private (Gang a, Gang b, string place, Vector3 pos)? WarClash(Ped player)
	{
		foreach (var (district, tuple2) in (from d in Districts
			select (d: d, w: WarIn(d)) into _
			where _.w.HasValue
			orderby _rng.Next()
			select _).ToList())
		{
			if (!(_rng.NextDouble() > Math.Min(TargetWeight(tuple2.Value.Item1), TargetWeight(tuple2.Value.Item2))))
			{
				double num = _rng.NextDouble() * Math.PI * 2.0;
				float num2 = 150f + (float)_rng.NextDouble() * 450f;
				Vector3 vector = new Vector3(district.X + (float)Math.Cos(num) * num2, district.Y + (float)Math.Sin(num) * num2, 100f);
				if ((!(player != null) || !(vector.DistanceTo(new Vector3(player.Position.X, player.Position.Y, 100f)) < Config.WorldMinDistance)) && !InsideAnyTown(vector) && OnLand(vector))
				{
					bool flag = _rng.Next(2) == 0;
					return (flag ? tuple2.Value.Item2 : tuple2.Value.Item1, flag ? tuple2.Value.Item1 : tuple2.Value.Item2, "in " + district.Name, vector);
				}
			}
		}
		return null;
	}

	internal List<(string where, Gang a, Gang b)> WarsNow()
	{
		return (from d in Districts
			select (d: d, w: WarIn(d)) into t
			where t.w.HasValue
			select (Name: t.d.Name, a: t.w.Value.a, b: t.w.Value.b)).ToList();
	}

	internal string WarZonesLine()
	{
		List<string> list = (from d in Districts
			select (d: d, w: WarIn(d)) into t
			where t.w.HasValue
			select t.d.Name + " (" + t.w.Value.a.Name + " vs " + t.w.Value.b.Name + ")").ToList();
		return "war zones: " + ((list.Count == 0) ? "none" : string.Join(", ", list));
	}
}
