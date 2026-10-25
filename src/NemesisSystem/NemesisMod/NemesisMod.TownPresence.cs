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
	private sealed class TownMan
	{
		public Ped Ped;

		public NemesisRecord Rec;

		public Intent Intent;

		public bool Engaged;

		public int EngageAt;

		public Blip Blip;

		public string Town;

		public bool Hunter;

		public bool Spotted;

		public int SpawnedAt;

		public bool Leaving;

		public int LeftAt;

		public bool Disguised;

		public string DotName;

		public bool Comes;

		public bool Approaching;

		public bool Spoke;

		public int ApproachAt;

		public int EngagedAt;

		public int StuckTries;

		public int StayedOutAt;

		public bool BackupDecided;

		public bool Calmed;
	}

	private readonly Dictionary<string, List<string>> _townLastMen = new Dictionary<string, List<string>>();

	private uint _townRelGroup;

	private uint _townFightGroup;

	private string _heldTownEntry;

	private int _regularsToCome;

	private Vector3 _arrivalFrom;

	private float _arrivalStep;

	internal static Vector3? TownToward;

	private readonly List<NemesisRecord> _townArrivals = new List<NemesisRecord>();

	private bool _loadInVisit;

	private string _townArrivalsFor;

	private int _nextTownArrival;

	private int _lastArrivalAt;

	private readonly HashSet<string> _arrivalRetried = new HashSet<string>();

	private int _nextTownDots;

	private int _nextTownTopUp;

	private int _nextTownScan;

	private readonly Dictionary<int, int> _folkOut = new Dictionary<int, int>();

	private bool _folkRolled;

	private bool _folkMayJoin;

	private int _folkJoined;

	private int _nextFolkScan;

	private const int TownFolkJoinPercent = 25;

	private const int TownComesPercent = 5;

	private const int TownComesMurderousShare = 30;

	private TownMan TownManFor(int handle)
	{
		return _townMen.FirstOrDefault((TownMan t) => t.Ped != null && t.Ped.Exists() && t.Ped.Handle == handle);
	}

	private void OnEnterTownPresence(string town, Ped player)
	{
		if (NoSpawnReason(player) != null)
		{
			_heldTownEntry = town;
			return;
		}
		if (!InTownForArrivals(player, town))
		{
			_heldTownEntry = town;
			GrudgeLog("TOWN arrival in " + town + " waits - you're at the edge of its zone");
			return;
		}
		if (_townCooldown.TryGetValue(town, out var value) && Game.GameTime < value)
		{
			int num = 0;
			_townArrivals.Clear();
			_townArrivalsFor = town;
			_arrivalStep = 0f;
			_lastArrivalAt = Game.GameTime;
			_nextTownArrival = Game.GameTime + 2000 + _rng.Next(4000);
			if (_townLastMen.TryGetValue(town, out var value2))
			{
				foreach (string id in value2.ToList())
				{
					if (_townMen.Count >= Balance.TownMaxMen)
					{
						break;
					}
					NemesisRecord rec = Roster.FirstOrDefault((NemesisRecord n) => n.Id == id);
					if (rec != null && rec.IsAlive && !rec.PresumedDead && rec.CanHunt && !IsArmy(rec) && !_worldNemeses.ContainsValue(rec) && rec != ActiveRecord && !_townMen.Any((TownMan t) => t.Rec == rec) && _roadAmbush?.Rec != rec)
					{
						_townArrivals.Add(rec);
						num++;
					}
				}
			}
			GrudgeLog($"TOWN back in {town} within its cooldown ({(value - Game.GameTime) / 1000}s left): {num} of the same men still about, no new arrivals");
			return;
		}
		_townLastMen[town] = new List<string>();
		_regularsToCome = 0;
		for (int num2 = 0; num2 < 3; num2++)
		{
			if ((float)_rng.Next(100) < (float)Config.RegularChance * NightTownMult())
			{
				_regularsToCome++;
			}
		}
		_townCooldown[town] = Game.GameTime + 480000;
		int num3 = Math.Min(Balance.TownMaxMen, _rng.Next(Balance.TownArrivalMin, Balance.TownArrivalMax + 1));
		if (NightTownMult() < 1f)
		{
			num3 = Math.Max(1, num3 / 2);
		}
		List<NemesisRecord> list = (from n in TownCandidates(town, player)
			where TownTierPercent(n) > 0
			select n).ToList();
		_townArrivals.Clear();
		_townArrivalsFor = town;
		_arrivalStep = 0f;
		_lastArrivalAt = Game.GameTime;
		_nextTownArrival = Game.GameTime + 2000 + _rng.Next(4000);
		int num4 = 0;
		while (_townArrivals.Count + _townMen.Count < num3 && list.Count > 0 && num4++ < 20)
		{
			List<NemesisRecord> pool = list;
			if (list.Any((NemesisRecord n) => HomeTown(n, town, player)) && _rng.Next(100) >= Balance.TownPassingPercent)
			{
				pool = list.Where((NemesisRecord n) => HomeTown(n, town, player)).ToList();
			}
			NemesisRecord nemesisRecord = PickTownMan(pool);
			list.Remove(nemesisRecord);
			if (LoreTownRoll(nemesisRecord, town))
			{
				_townArrivals.Add(nemesisRecord);
			}
		}
		if (_townArrivals.Count + _townMen.Count < num3)
		{
			foreach (NemesisRecord item in (from n in TownCandidates(town, player)
				where !_townArrivals.Contains(n) && CoolingOff(n) && n.HateTier != HateTier.Murderous
				orderby HomeTown(n, town, player) descending, _rng.Next()
				select n).ToList())
			{
				if (_townArrivals.Count + _townMen.Count >= num3)
				{
					break;
				}
				if (LoreTownRoll(item, town))
				{
					_townArrivals.Add(item);
					GrudgeLog("TOWN " + item.Name + " came lately, but " + (HomeTown(item, town, player) ? "he lives here" : "he's about") + " - in town again");
				}
			}
		}
		_loadInVisit = Game.GameTime - Math.Max(Math.Max(_loadedAt, _loadScreenAt), _lastJumpAt) < 60000;
		_nextTownArrival = Game.GameTime + 2000 + _rng.Next(4000);
		TryHiddenInTown(town, player);
		GrudgeLog(string.Format("TOWN arrival in {0}: {1} of {2} will turn up as you walk in ({3}; {4} left over)", town, _townArrivals.Count, num3, string.Join(", ", _townArrivals.Select((NemesisRecord r) => r.Name)), list.Count));
		if (_townArrivals.Count < num3)
		{
			GrudgeLog("TOWN pool for " + town + ": " + TownPoolNote(town, player));
		}
	}

	internal bool InTownForArrivals(Ped player, string town)
	{
		if (town != null && TownCore(town, out var centre, out var radius))
		{
			return new Vector3(player.Position.X - centre.X, player.Position.Y - centre.Y, 0f).Length() < radius * 0.95f + 20f;
		}
		return false;
	}

	private void UpdateTownArrivals(Ped player)
	{
		if ((_townArrivals.Count == 0 && _regularsToCome == 0) || Game.GameTime < _nextTownArrival)
		{
			return;
		}
		if (_currentTown != _townArrivalsFor)
		{
			_townArrivals.Clear();
			_regularsToCome = 0;
			return;
		}
		if (NoSpawnReason(player) != null || !InTownForArrivals(player, _currentTown))
		{
			_nextTownArrival = Game.GameTime + 3000;
			return;
		}
		if (Game.GameTime - _quietEndedAt < 8000 || Game.GameTime - _lastJumpAt < 15000)
		{
			_nextTownArrival = Game.GameTime + 1000;
			return;
		}
		if (_arrivalStep > 0f)
		{
			Vector3 position = player.Position;
			bool num = new Vector3(position.X - _arrivalFrom.X, position.Y - _arrivalFrom.Y, 0f).Length() >= _arrivalStep;
			bool flag = Game.GameTime - _lastArrivalAt > (_loadInVisit ? (12 + _rng.Next(10)) : Balance.TownArrivalIdleSeconds) * 1000;
			if ((!num && !flag) || Game.GameTime - _lastArrivalAt < 10000)
			{
				_nextTownArrival = Game.GameTime + 1000;
				return;
			}
		}
		if (!Travelling(player, out var dir))
		{
			if (_loadInVisit)
			{
				double num2 = _rng.NextDouble() * Math.PI * 2.0;
				dir = new Vector3((float)Math.Cos(num2), (float)Math.Sin(num2), 0f);
			}
			else
			{
				Vector3 direction = GameplayCamera.Direction;
				dir = new Vector3(direction.X, direction.Y, 0f);
			}
		}
		if (dir.Length() > 0.01f)
		{
			dir.Normalize();
			TownToward = player.Position + dir * 70f;
		}
		else
		{
			TownToward = null;
		}
		Vector3? townToward = TownToward;
		if (townToward.HasValue)
		{
			Vector3 valueOrDefault = townToward.GetValueOrDefault();
			if (TownCore(_currentTown, out var centre, out var radius))
			{
				Vector3 vector = new Vector3(valueOrDefault.X - centre.X, valueOrDefault.Y - centre.Y, 0f);
				if (vector.Length() > radius * 0.85f)
				{
					vector.Normalize();
					TownToward = new Vector3(centre.X + vector.X * radius * 0.85f, centre.Y + vector.Y * radius * 0.85f, player.Position.Z);
				}
			}
		}
		_arrivalFrom = player.Position;
		_arrivalStep = Balance.TownArrivalWalkMin + _rng.Next(Math.Max(1, Balance.TownArrivalWalkMax - Balance.TownArrivalWalkMin + 1));
		_lastArrivalAt = Game.GameTime;
		try
		{
			UpdateTownArrivalsSpawn(player);
		}
		finally
		{
			TownToward = null;
		}
	}

	private void UpdateTownArrivalsSpawn(Ped player)
	{
		if (_regularsToCome > 0 && (_townArrivals.Count == 0 || _rng.Next(2) == 0))
		{
			_regularsToCome--;
			TrySpawnRegular(player, _currentTown);
			_nextTownArrival = Game.GameTime + 1000;
			return;
		}
		NemesisRecord nemesisRecord = _townArrivals[0];
		_townArrivals.RemoveAt(0);
		if (nemesisRecord.IsAlive && !nemesisRecord.PresumedDead && !_worldNemeses.ContainsValue(nemesisRecord) && nemesisRecord != ActiveRecord && _townMen.Count < Balance.TownMaxMen)
		{
			GrudgeLog($"TOWN arrival: {nemesisRecord.Name} turns up ({_townArrivals.Count} still to come)");
			if (!SpawnTownPresence(nemesisRecord, _currentTown, player, hunter: false, arrival: true) && _arrivalRetried.Add(nemesisRecord.Id))
			{
				_townArrivals.Add(nemesisRecord);
			}
		}
		_nextTownArrival = Game.GameTime + 1000;
	}

	private bool LoreTownRoll(NemesisRecord rec, string town)
	{
		Gang gang = Gangs.Of(rec);
		if (gang == null || town == null)
		{
			return true;
		}
		District d;
		Gang gang2 = TownHolder(town, out d);
		if (gang2 != null && gang2 != gang && AtWarNow(gang.Id, gang2.Id))
		{
			GrudgeLog("TOWN " + rec.Name + " (" + gang.Name + ") in " + town + "? the " + gang2.Name + " hold it and they're at war - stays away");
			return false;
		}
		if ((gang.JobTowns != null && gang.JobTowns.Contains(town)) || gang.HomeTown == town || rec.KillLocation == town)
		{
			return true;
		}
		if (gang.OnlyDistricts != null && TownCore(town, out var centre, out var _))
		{
			District district = DistrictAt(centre);
			if (district != null && !district.Hashes.Any(gang.OnlyDistricts.Contains))
			{
				GrudgeLog("TOWN " + rec.Name + " (" + gang.Name + ") in " + town + "? outside the outfit's own country (" + district.Name + ") - stays away");
				return false;
			}
		}
		bool flag = _rng.Next(100) < Balance.TownOffLorePercent;
		GrudgeLog(string.Format("TOWN {0} ({1}) in {2}? not one of his outfit's towns - {3} ({4}%)", rec.Name, gang.Name, town, flag ? "comes anyway" : "stays away", Balance.TownOffLorePercent));
		return flag;
	}

	private IEnumerable<NemesisRecord> TownCandidates(string town, Ped player)
	{
		return from n in Roster.Where((NemesisRecord n) => n.CanHunt && !n.PresumedDead && !Resting(n) && !_worldNemeses.ContainsValue(n) && n != ActiveRecord && !IsArmy(n) && !NoTowns(n) && n != _roadAmbush?.Rec).Where(delegate(NemesisRecord n)
			{
				Intent intent = IntentOf(n);
				return intent == Intent.WontHunt || intent == Intent.ProvokedOnly || intent == Intent.Hunts;
			})
			where HomeTown(n, town, player) || !n.HasOrigin || new Vector3(n.OriginX, n.OriginY, n.OriginZ).DistanceTo(player.Position) < Balance.TownPassingRange
			select n;
	}

	private string TownPoolNote(string town, Ped player)
	{
		List<NemesisRecord> source = Roster.Where((NemesisRecord n) => n.IsAlive && !n.PresumedDead).ToList();
		int num = source.Count((NemesisRecord n) => n.CanHunt);
		int num2 = source.Count((NemesisRecord n) => n.CanHunt && IsArmy(n));
		List<NemesisRecord> source2 = source.Where((NemesisRecord n) => n.CanHunt && !IsArmy(n)).ToList();
		int num3 = source2.Count(Resting);
		int num4 = source2.Count((NemesisRecord n) => !Resting(n) && (_worldNemeses.ContainsValue(n) || n == ActiveRecord));
		int num5 = source2.Count(NoTowns);
		List<NemesisRecord> list = (from n in source2.Where((NemesisRecord n) => !NoTowns(n)).ToList()
			where !Resting(n) && !_worldNemeses.ContainsValue(n) && n != ActiveRecord
			select n).ToList();
		int num6 = list.Count((NemesisRecord n) => IntentOf(n) == Intent.None);
		List<NemesisRecord> list2 = list.Where((NemesisRecord n) => IntentOf(n) != Intent.None && (HomeTown(n, town, player) || !n.HasOrigin || new Vector3(n.OriginX, n.OriginY, n.OriginZ).DistanceTo(player.Position) < Balance.TownPassingRange)).ToList();
		int num7 = list2.Count(CoolingOff);
		int num8 = list2.Count((NemesisRecord n) => !CoolingOff(n) && n.HateTier == HateTier.Murderous && n.Hate < Balance.TownMurderousMinHate);
		return $"{num} can come ({num2} Army/squads never, {num5} never in towns), {num3} lying low, {num4} already out, {num6} no grudge, {list.Count - num6 - list2.Count} too far away, " + $"{list2.Count} near: {num7} cooling off (came lately), {num8} murderous under {Balance.TownMurderousMinHate}, {list2.Count((NemesisRecord n) => TownTierPercent(n) > 0)} could come";
	}

	private static bool HomeTown(NemesisRecord n, string town, Ped player)
	{
		if (!(n.KillLocation == town))
		{
			if (n.HasOrigin)
			{
				return new Vector3(n.OriginX, n.OriginY, n.OriginZ).DistanceTo(player.Position) < 600f;
			}
			return false;
		}
		return true;
	}

	private int TownTierPercent(NemesisRecord r)
	{
		if (CoolingOff(r))
		{
			return 0;
		}
		if (r.HateTier == HateTier.Murderous && r.Hate < Balance.TownMurderousMinHate)
		{
			return 0;
		}
		int num = ((r.HateTier == HateTier.Murderous) ? Balance.TownMurderousPct : ((r.HateTier == HateTier.Vengeful) ? Balance.TownVengefulPct : 100));
		if (r.HateTier == HateTier.Murderous && _townMen.Any((TownMan t) => t.Rec.HateTier == HateTier.Murderous))
		{
			num = num * Balance.TownSecondMurderousPct / 100;
		}
		return num;
	}

	private NemesisRecord PickTownMan(List<NemesisRecord> pool)
	{
		double num = ((IEnumerable<NemesisRecord>)pool).Sum((Func<NemesisRecord, double>)((NemesisRecord n) => TownTierPercent(n)));
		if (num <= 0.0)
		{
			return pool[_rng.Next(pool.Count)];
		}
		double num2 = _rng.NextDouble() * num;
		foreach (NemesisRecord item in pool)
		{
			num2 -= (double)TownTierPercent(item);
			if (num2 <= 0.0)
			{
				return item;
			}
		}
		return pool[pool.Count - 1];
	}

	private static float NightTownMult()
	{
		int cLOCK_HOURS = CLOCK.GET_CLOCK_HOURS();
		if (cLOCK_HOURS < 22 && cLOCK_HOURS >= 5)
		{
			return 1f;
		}
		return 0.5f;
	}

	internal bool SpawnTownHunter(Ped player)
	{
		string text = _currentTown ?? TownAt(player.Position);
		if (text == null)
		{
			return false;
		}
		if (_townMen.Count >= Balance.TownMaxMen)
		{
			return SpawnTestRegular(player, text, $"{_townMen.Count} nemeses are already in {text}");
		}
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.CanHunt && !n.PresumedDead && !IsArmy(n) && !NoTowns(n) && !_worldNemeses.ContainsValue(n) && n != ActiveRecord).ToList();
		if (list.Count == 0)
		{
			return SpawnTestRegular(player, text, "no nemesis free to come into town");
		}
		if (list.Any((NemesisRecord n) => TownTierPercent(n) > 0))
		{
			list = list.Where((NemesisRecord n) => TownTierPercent(n) > 0).ToList();
		}
		NemesisRecord rec = PickTownMan(list);
		SpawnTownPresence(rec, text, player);
		return true;
	}

	private bool SpawnTestRegular(Ped player, string town, string why)
	{
		if (TrySpawnRegular(player, town, force: true))
		{
			return true;
		}
		int num = People.Count((Acquaintance a) => !a.Dead && a.Town == town);
		string text = ((!Config.RememberPeople) ? "remembering people is off" : ((_regulars.Count((Ped r) => r.Exists()) >= Config.RegularsMax) ? $"{Config.RegularsMax} people you know are already around" : ((num == 0) ? ("you don't know anyone from " + town + " yet") : ("everyone you know from " + town + " is already here"))));
		ShowNotification(char.ToUpper(why[0]) + why.Substring(1) + " - and " + text + ".");
		return true;
	}

	private bool SpawnTownPresence(NemesisRecord rec, string town, Ped player, bool hunter = false, bool arrival = false)
	{
		bool flag = hunter || IntentOf(rec) == Intent.WontHunt;
		int num = _rng.Next(100);
		bool num2 = num >= Balance.TownSeatPercent && num < Balance.TownSeatPercent + Balance.TownStrollPercent;
		bool flag2 = num >= Balance.TownSeatPercent + Balance.TownStrollPercent;
		(Vector3, float, int)? tuple = ((num2 || flag2) ? (((Vector3, float, int)?)null) : FindTownSeat(player, town, flag ? (Math.Max(Balance.WontHuntSpotRange, 32f) + 12f) : 12f));
		string text;
		Vector3 vector2;
		if (!tuple.HasValue)
		{
			Vector3? vector;
			if (flag2)
			{
				vector = TownEdgeSpot(player, town);
				if (vector.HasValue)
				{
					Vector3 valueOrDefault = vector.GetValueOrDefault();
					if (valueOrDefault != Vector3.Zero)
					{
						text = "on the edge of town";
						vector2 = valueOrDefault;
						goto IL_0171;
					}
				}
			}
			vector = AheadStreetSpot(player, town);
			if (vector.HasValue)
			{
				Vector3 valueOrDefault2 = vector.GetValueOrDefault();
				text = "about town";
				vector2 = valueOrDefault2;
			}
			else
			{
				vector = NearStreetSpot(player, town);
				if (!vector.HasValue)
				{
					(vector2, text) = NaturalTownSpot(player, town);
				}
				else
				{
					Vector3 valueOrDefault3 = vector.GetValueOrDefault();
					text = "about town";
					vector2 = valueOrDefault3;
				}
			}
		}
		else
		{
			Vector3 vector3 = StandIn(tuple.Value.Item1, tuple.Value.Item2);
			text = "on a seat";
			vector2 = vector3;
		}
		goto IL_0171;
		IL_0171:
		if (num2 && vector2 != Vector3.Zero)
		{
			text = "strolling the streets";
		}
		if (vector2 == Vector3.Zero)
		{
			GrudgeLog("TOWN " + rec.Name + ": no spot in " + town + " - not placed");
			return false;
		}
		if (arrival && vector2.DistanceTo(player.Position) > 110f)
		{
			GrudgeLog($"TOWN {rec.Name}: the only spot was {vector2.DistanceTo(player.Position):0} m off - he turns up later");
			return false;
		}
		GrudgeLog("TOWN spot for " + rec.Name + " (" + (hunter ? "hunter" : IntentOf(rec).ToString()) + "): " + text + " - " + SpotNote(vector2, town, player));
		if (_townRelGroup == 0)
		{
			_townRelGroup = World.AddRelationshipGroup("NEMESIS_TOWN");
		}
		bool flag3 = NoTowns(rec);
		uint modelOverride = (flag3 ? PlainModelOf(rec) : 0u);
		Model model;
		Ped ped = SpawnNemesisPed(rec, vector2, tuple.HasValue ? tuple.Value.Item2 : ((float)_rng.Next(360)), _townRelGroup, out model, 424175505u, releaseModel: true, blockEvents: false, factionStyle: false, modelOverride);
		if (ped == null)
		{
			return false;
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Dislike, _townRelGroup, player.RelationshipGroup);
		if (rec.Gang == null || flag3)
		{
			ped.RelationshipGroup = PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(ped.Handle);
			ForgetSwitch(ped.Handle);
		}
		Vector3 centre;
		float radius;
		if (tuple.HasValue)
		{
			SitAt(ped, tuple.Value.Item3, tuple.Value.Item1, tuple.Value.Item2);
		}
		else if (text == "strolling the streets" && TownCore(town, out centre, out radius))
		{
			Function.Call(16164602603745899285uL, ped.Handle, centre.X, centre.Y, centre.Z, radius * 0.7f, 4f, 10f, 0);
		}
		else
		{
			PlaceNaturally(ped, vector2, text != "at the saloon bar");
		}
		MakeSociable(ped);
		if (!hunter)
		{
			HandToGame(ped);
		}
		TownMan townMan = new TownMan
		{
			Ped = ped,
			Rec = rec,
			Intent = IntentOf(rec),
			Town = town,
			Hunter = hunter,
			SpawnedAt = Game.GameTime,
			Disguised = flag3
		};
		if (!flag3 && HasReason(rec) && _rng.Next(100) < 5 && (rec.HateTier != HateTier.Murderous || _rng.Next(100) < 30))
		{
			townMan.Comes = true;
			GrudgeLog($"TOWN {rec.Name} ({rec.HateTier}) will come for you when he sees you");
		}
		NoteCame(rec);
		if (hunter)
		{
			rec.Touch();
			Feed("ENCOUNTER TOWN: " + rec.FullName + " is in " + town + ", looking for you (" + text + ")", Theme.Warn, rec);
		}
		_townMen.Add(townMan);
		_worldNemeses[ped.Handle] = rec;
		if (rec.Id != null)
		{
			if (!_townLastMen.TryGetValue(town, out var value))
			{
				value = (_townLastMen[town] = new List<string>());
			}
			if (!value.Contains(rec.Id))
			{
				value.Add(rec.Id);
			}
		}
		if (flag3 && !rec.CoverBlown)
		{
			townMan.DotName = null;
		}
		else if (flag3)
		{
			townMan.DotName = rec.FullName + " (" + Gangs.ById(rec.Gang)?.Name + ", out of his robe)";
		}
		else
		{
			townMan.DotName = (HasReason(rec) ? (rec.FullName + " (holds a grudge)") : (rec.FullName + " (" + Gangs.ById(rec.Gang)?.Name + ")"));
			if (HasReason(rec))
			{
				GrudgeNotice(rec.FullName + " is in " + town + ". He won't start anything - but he hasn't forgotten.");
			}
		}
		GrudgeLog($"TOWN {rec.Name} in {town} {text}: {townMan.Intent} (hate {rec.Hate}, fear {EffectiveFear(rec)}) - {_townMen.Count} in town");
		TownHappening(rec.Name + " turned up in " + town);
		return true;
	}

	private Vector3? AheadStreetSpot(Ped player, string town)
	{
		Vector3? townToward = TownToward;
		if (townToward.HasValue)
		{
			Vector3 valueOrDefault = townToward.GetValueOrDefault();
			if (TownCore(town, out var centre, out var radius))
			{
				for (int i = 0; i < 16; i++)
				{
					double num = _rng.NextDouble() * Math.PI * 2.0;
					float num2 = 5f + (float)_rng.NextDouble() * 40f;
					Vector3 position = new Vector3(valueOrDefault.X + (float)Math.Cos(num) * num2, valueOrDefault.Y + (float)Math.Sin(num) * num2, player.Position.Z);
					if (!(new Vector3(position.X - centre.X, position.Y - centre.Y, 0f).Length() > radius))
					{
						Vector3 safeCoordForPed = World.GetSafeCoordForPed(position, onGround: true, 0);
						if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && !(safeCoordForPed.DistanceTo(position) > 20f) && Dry(safeCoordForPed) && !UpHigh(safeCoordForPed) && Outdoors(safeCoordForPed) && !(safeCoordForPed.DistanceTo(player.Position) < 20f) && HiddenFromView(player, safeCoordForPed) && !OurPeopleNear(safeCoordForPed, TownSpread(town)))
						{
							return safeCoordForPed;
						}
					}
				}
				return null;
			}
		}
		return null;
	}

	private Vector3? NearStreetSpot(Ped player, string town, int tries = 20, bool hidden = true)
	{
		if (!TownCore(town, out var centre, out var radius))
		{
			return null;
		}
		double num = Math.Atan2(Forward(player).Y, Forward(player).X);
		for (int i = 0; i < tries; i++)
		{
			double num2 = ((i < tries * 3 / 5) ? (num + (_rng.NextDouble() - 0.5) * Math.PI * 1.4) : (_rng.NextDouble() * Math.PI * 2.0));
			float num3 = 35f + (float)_rng.NextDouble() * 65f;
			Vector3 position = new Vector3(player.Position.X + (float)Math.Cos(num2) * num3, player.Position.Y + (float)Math.Sin(num2) * num3, player.Position.Z);
			if (new Vector3(position.X - centre.X, position.Y - centre.Y, 0f).Length() > radius)
			{
				continue;
			}
			Vector3 safeCoordForPed = World.GetSafeCoordForPed(position, onGround: true, 0);
			if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && !(safeCoordForPed.DistanceTo2D(position) > 20f) && Dry(safeCoordForPed) && !UpHigh(safeCoordForPed) && Outdoors(safeCoordForPed))
			{
				float num4 = safeCoordForPed.DistanceTo(player.Position);
				if (!(num4 < 30f) && !(num4 > 105f) && (!hidden || HiddenFromView(player, safeCoordForPed)) && !OurPeopleNear(safeCoordForPed, TownSpread(town)))
				{
					return safeCoordForPed;
				}
			}
		}
		return null;
	}

	private Vector3? TownEdgeSpot(Ped player, string town)
	{
		if (!TownCore(town, out var centre, out var radius))
		{
			return null;
		}
		for (int i = 0; i < 10; i++)
		{
			double num = _rng.NextDouble() * Math.PI * 2.0;
			float num2 = radius * (0.85f + (float)_rng.NextDouble() * 0.25f);
			Vector3 position = new Vector3(centre.X + (float)Math.Cos(num) * num2, centre.Y + (float)Math.Sin(num) * num2, centre.Z);
			Vector3 safeCoordForPed = World.GetSafeCoordForPed(position, onGround: true, 0);
			if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && !(safeCoordForPed.DistanceTo(position) > 25f) && Dry(safeCoordForPed) && !UpHigh(safeCoordForPed) && Outdoors(safeCoordForPed) && !(safeCoordForPed.DistanceTo(player.Position) < 25f) && HiddenFromView(player, safeCoordForPed) && !OurPeopleNear(safeCoordForPed, TownSpread(town)))
			{
				return safeCoordForPed;
			}
		}
		return null;
	}

	private void UpdateTownDots(Ped player)
	{
		if (Game.GameTime < _nextTownDots)
		{
			return;
		}
		_nextTownDots = Game.GameTime + 400;
		foreach (TownMan townMan in _townMen)
		{
			if (townMan.Engaged || townMan.Leaving || townMan.Ped == null || !townMan.Ped.Exists())
			{
				continue;
			}
			if (townMan.Disguised && townMan.Rec.CoverBlown && townMan.DotName == null)
			{
				townMan.DotName = townMan.Rec.FullName + " (" + Gangs.ById(townMan.Rec.Gang)?.Name + ", out of his robe)";
			}
			if (townMan.DotName == null)
			{
				continue;
			}
			bool flag = townMan.Blip != null && townMan.Blip.Exists();
			float num = townMan.Ped.Position.DistanceTo(player.Position);
			bool flag2 = _debugOn || num < Balance.TownDotNear || (flag && num < Balance.TownDotFar);
			if (flag2 && !flag)
			{
				Crumbs.Native("TownPresence blip add (V2)");
				townMan.Blip = townMan.Ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
				if (townMan.Blip != null)
				{
					Function.Call(11290983183885202434uL, townMan.Blip.Handle, new InputArgument(UiDraw.ForeverVarString(townMan.DotName)));
				}
			}
			else if (!flag2 && flag)
			{
				Crumbs.Native("TownPresence blip remove (V2)");
				townMan.Blip.Delete();
				townMan.Blip = null;
			}
		}
	}

	private void UpdateTownPresence(Ped player)
	{
		TrackTownVisit();
		UpdateTownDots(player);
		UpdateTownArrivals(player);
		UpdateRoadAmbush(player);
		if (_currentTown != null && Game.GameTime >= _nextTownTopUp && InTownForArrivals(player, _currentTown))
		{
			_nextTownTopUp = Game.GameTime + Balance.TownTopUpSeconds * 1000;
			if (_townArrivalsFor != _currentTown)
			{
				_townArrivals.Clear();
				_regularsToCome = 0;
				_townArrivalsFor = _currentTown;
				_arrivalStep = 0f;
				_lastArrivalAt = Game.GameTime;
			}
			if (NoSpawnReason(player) == null && (float)_rng.Next(100) < (float)Config.RegularChance * NightTownMult() && _regularsToCome < 2)
			{
				_regularsToCome++;
			}
			if (_townMen.Count < Balance.TownMaxMen && NoSpawnReason(player) == null && (float)_rng.Next(100) < (float)Balance.TownTopUpChance * NightTownMult())
			{
				List<NemesisRecord> list = (from n in TownCandidates(_currentTown, player)
					where TownTierPercent(n) > 0
					select n).ToList();
				if (list.Count > 0 && list.Any((NemesisRecord n) => HomeTown(n, _currentTown, player)) && _rng.Next(100) >= Balance.TownPassingPercent)
				{
					list = list.Where((NemesisRecord n) => HomeTown(n, _currentTown, player)).ToList();
				}
				list.RemoveAll((NemesisRecord n) => _townArrivals.Contains(n));
				if (list.Count > 0 && _townArrivals.Count + _townMen.Count < Balance.TownMaxMen)
				{
					NemesisRecord nemesisRecord = PickTownMan(list);
					if (LoreTownRoll(nemesisRecord, _currentTown))
					{
						GrudgeLog("TOWN top-up: " + nemesisRecord.Name + " will turn up as you walk about " + _currentTown);
						_townArrivals.Add(nemesisRecord);
					}
				}
			}
		}
		if (_heldTownEntry != null && _currentTown != _heldTownEntry)
		{
			_heldTownEntry = null;
		}
		if (_heldTownEntry != null && NoSpawnReason(player) == null && InTownForArrivals(player, _heldTownEntry))
		{
			string heldTownEntry = _heldTownEntry;
			_heldTownEntry = null;
			OnEnterTownPresence(heldTownEntry, player);
		}
		if (Game.GameTime < _nextTownScan)
		{
			return;
		}
		_nextTownScan = Game.GameTime + 250;
		try
		{
			TownFolkStayOut(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("TOWN folk check failed: " + ex.Message);
		}
		for (int num = _townMen.Count - 1; num >= 0; num--)
		{
			TownMan townMan = _townMen[num];
			if (townMan.Ped == null || !townMan.Ped.Exists() || !Roster.Contains(townMan.Rec) || !townMan.Rec.IsAlive || townMan.Ped.IsDead)
			{
				if (townMan.Engaged)
				{
					NoteTownFightOver();
				}
				ClearTownMan(townMan, delete: false);
				continue;
			}
			float num2 = townMan.Ped.Position.DistanceTo(player.Position);
			if (num2 > 220f && !townMan.Engaged)
			{
				ClearTownMan(townMan, delete: true);
			}
			else if (townMan.Engaged && num2 > 300f)
			{
				NoteTownFightOver();
				ClearTownMan(townMan, delete: false);
			}
			else
			{
				if (townMan.Engaged && UnderArrest(townMan.Ped.Handle))
				{
					continue;
				}
				if (townMan.Engaged)
				{
					try
					{
						UnstickTownFighter(townMan, player, num2);
					}
					catch (Exception ex2)
					{
						GrudgeLog("TOWN unstick failed: " + ex2.Message);
					}
					continue;
				}
				if (townMan.EngageAt > 0 && (Game.GameTime >= townMan.EngageAt || PED.IS_PED_IN_COMBAT(townMan.Ped.Handle, player.Handle) || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(townMan.Ped.Handle, player.Handle, p2: true, p3: true)))
				{
					townMan.EngageAt = 0;
					GrudgeLog("TOWN " + townMan.Rec.Name + ": after the words, the fight");
					EngageTownMan(townMan, player);
					continue;
				}
				try
				{
					if (TownBackup(townMan, player) || TownNoPanic(townMan, player))
					{
						continue;
					}
					goto IL_0525;
				}
				catch (Exception ex3)
				{
					GrudgeLog("TOWN backup failed: " + ex3.Message);
					goto IL_0525;
				}
			}
			continue;
			IL_0525:
			if (Game.GameTime - townMan.SpawnedAt < 10000)
			{
				continue;
			}
			if (townMan.Leaving)
			{
				if (num2 > 70f || Game.GameTime - townMan.LeftAt > 45000)
				{
					GrudgeLog("TOWNCALM " + townMan.Rec.Name + " left " + townMan.Town);
					ClearTownMan(townMan, delete: true);
				}
				else if (PED.IS_PED_IN_COMBAT(townMan.Ped.Handle, player.Handle) && JoinsTheFight(townMan, player))
				{
					townMan.Leaving = false;
					if (_roadAmbush?.Rec == townMan.Rec)
					{
						_roadAmbush = null;
					}
					EngageTownMan(townMan, player);
				}
				continue;
			}
			if (townMan.Approaching)
			{
				TownManApproach(townMan, player, num2);
				continue;
			}
			if (townMan.Comes && !townMan.Spotted && num2 < 50f && Sees(townMan.Ped, player, SightFor(player, 30f), out var d))
			{
				townMan.Spotted = true;
				string why = "a man is already waiting outside";
				if (_roadAmbush == null && TakeAggressorSlot(out why))
				{
					townMan.Approaching = true;
					townMan.ApproachAt = Game.GameTime;
					Crumbs.Native("Town man comes over");
					TASK.CLEAR_PED_TASKS(townMan.Ped.Handle, p1: true, p2: true);
					TASK.TASK_GO_TO_ENTITY(townMan.Ped.Handle, player.Handle, -1, 3f, 1.4f, 0f, 0);
					GrudgeLog($"TOWN {townMan.Rec.Name} saw you in {townMan.Town} - he comes over ({num2:0} m, {townMan.Rec.HateTier})");
					Feed("TOWN " + townMan.Rec.FullName + " saw you - he's coming over", Theme.Blood, townMan.Rec);
				}
				else
				{
					GrudgeLog("TOWN " + townMan.Rec.Name + " would have come for you - " + why);
				}
				continue;
			}
			if (!townMan.Spotted && !townMan.Disguised && townMan.Rec.HateTier == HateTier.Murderous && HasReason(townMan.Rec) && num2 < 50f && Sees(townMan.Ped, player, SightFor(player, 30f), out d))
			{
				townMan.Spotted = true;
				if (_roadAmbush == null && TakeAggressorSlot(out var _))
				{
					if (RollWaitOutside(townMan.Rec, townMan.Town, townMan.Ped, player))
					{
						townMan.Leaving = true;
						townMan.LeftAt = Game.GameTime;
						if (townMan.Blip != null && townMan.Blip.Exists())
						{
							townMan.Blip.Delete();
						}
						townMan.Blip = null;
					}
					else
					{
						_visitAggressors--;
					}
				}
			}
			if (PED.IS_PED_IN_COMBAT(townMan.Ped.Handle, player.Handle) && JoinsTheFight(townMan, player))
			{
				EngageTownMan(townMan, player);
			}
		}
	}

	private bool JoinsTheFight(TownMan tm, Ped player)
	{
		int handle = tm.Ped.Handle;
		if (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(handle, player.Handle, p2: true, p3: true))
		{
			BystanderLog(tm, "joined: you hurt him");
			return true;
		}
		if (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, handle) || PLAYER.IS_PLAYER_TARGETTING_ENTITY(Game.Player.Handle, handle, p2: false))
		{
			BystanderLog(tm, "joined: you drew on him");
			return true;
		}
		if (tm.Calmed)
		{
			return false;
		}
		if (tm.StayedOutAt != 0)
		{
			if (Game.GameTime - tm.StayedOutAt > 3000)
			{
				tm.StayedOutAt = Game.GameTime;
				TASK.CLEAR_PED_TASKS(handle, p1: true, p2: true);
				if (tm.Leaving)
				{
					TASK.TASK_WANDER_STANDARD(handle, 10f, 10);
				}
			}
			return false;
		}
		List<NemesisRecord> list = (from o in _townMen
			where o != tm && o.Engaged && o.Rec != null
			select o.Rec).ToList();
		NemesisRecord nemesisRecord = list.FirstOrDefault((NemesisRecord o) => tm.Rec.Gang != null && o.Gang == tm.Rec.Gang);
		if (nemesisRecord != null)
		{
			BystanderLog(tm, "joined: backs up " + nemesisRecord.Name + ", his outfit's man");
			return true;
		}
		NemesisRecord nemesisRecord2 = list.FirstOrDefault(delegate(NemesisRecord o)
		{
			List<string> friendIds = tm.Rec.FriendIds;
			if (friendIds == null || !friendIds.Contains(o.Id))
			{
				List<string> friendIds2 = o.FriendIds;
				if ((friendIds2 == null || !friendIds2.Contains(tm.Rec.Id)) && !(o.KinId == tm.Rec.Id))
				{
					return tm.Rec.KinId == o.Id;
				}
			}
			return true;
		});
		if (nemesisRecord2 != null)
		{
			BystanderLog(tm, "joined: backs up " + nemesisRecord2.Name + ", a friend");
			return true;
		}
		if (list.Count == 0 && _peaceAggressor == 0)
		{
			BystanderLog(tm, "joined: no other fight on - the game's combat stands");
			return true;
		}
		bool num = Mind.F(tm.Rec, "Bravery") >= 60;
		tm.StayedOutAt = Game.GameTime;
		Crumbs.Native("Town bystander stays out");
		TASK.CLEAR_PED_TASKS(handle, p1: true, p2: true);
		tm.Ped.BlockPermanentEvents = true;
		if (num)
		{
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(handle, player.Handle, 4000, 0f, 0f, 0f);
			BystanderLog(tm, "stayed out: not his fight (" + FightersText(list) + ") - he stands and watches");
		}
		else
		{
			TASK.TASK_WANDER_STANDARD(handle, 10f, 10);
			tm.Leaving = true;
			tm.LeftAt = Game.GameTime;
			if (tm.Blip != null && tm.Blip.Exists())
			{
				tm.Blip.Delete();
			}
			tm.Blip = null;
			BystanderLog(tm, "stayed out: not his fight (" + FightersText(list) + ") - he leaves town");
		}
		return false;
	}

	private bool TownBackup(TownMan tm, Ped player)
	{
		if (tm.Engaged || tm.Leaving || tm.BackupDecided || tm.Disguised || (tm.Rec.Gang == null && (tm.Rec.FriendIds == null || tm.Rec.FriendIds.Count == 0) && tm.Rec.KinId == null))
		{
			return false;
		}
		TownMan townMan = _townMen.FirstOrDefault((TownMan o) => o != tm && o.Engaged && o.Rec != null && o.Ped != null && o.Ped.Exists() && !o.Ped.IsDead && Tied(tm.Rec, o.Rec) != null);
		if (townMan == null)
		{
			return false;
		}
		Sight s = SightFor(player, 60f);
		float d;
		bool flag = Sees(tm.Ped, player, s, out d);
		float d2;
		bool flag2 = !flag && Sees(tm.Ped, townMan.Ped, s, out d2);
		if (!flag && !flag2)
		{
			return false;
		}
		tm.BackupDecided = true;
		string text = Tied(tm.Rec, townMan.Rec);
		int num = Mind.F(tm.Rec, "Loyalty");
		int num2 = Mind.F(tm.Rec, "Bravery");
		if (num >= 40 || num2 >= 40)
		{
			GrudgeLog(string.Format("TOWN backup {0} saw {1} ({2:0} m) - joins {3}, {4} (loyalty {5}, bravery {6})", tm.Rec.Name, flag ? "you" : townMan.Rec.Name, d, townMan.Rec.Name, text, num, num2));
			Feed("TOWN " + tm.Rec.FullName + " comes to back up " + townMan.Rec.Name, Theme.Blood, tm.Rec);
			EngageTownMan(tm, player);
			return true;
		}
		GrudgeLog(string.Format("TOWN backup {0} saw {1} ({2:0} m) - won't back {3}, {4} (loyalty {5}, bravery {6}) - hangs back", tm.Rec.Name, flag ? "you" : townMan.Rec.Name, d, townMan.Rec.Name, text, num, num2));
		tm.Calmed = true;
		KeepOutOfIt(tm, player, num2 < 25);
		return true;
	}

	private static string Tied(NemesisRecord a, NemesisRecord b)
	{
		if (a.Gang != null && a.Gang == b.Gang)
		{
			return "his outfit's man";
		}
		if (a.KinId == b.Id || b.KinId == a.Id)
		{
			return "his kin";
		}
		List<string> friendIds = a.FriendIds;
		if (friendIds == null || !friendIds.Contains(b.Id))
		{
			List<string> friendIds2 = b.FriendIds;
			if (friendIds2 == null || !friendIds2.Contains(a.Id))
			{
				return null;
			}
		}
		return "a friend";
	}

	private bool TownNoPanic(TownMan tm, Ped player)
	{
		if (tm.Engaged || tm.Leaving || tm.Calmed || !PED.IS_PED_FLEEING(tm.Ped.Handle))
		{
			return false;
		}
		if (!_townMen.Any((TownMan o) => o.Engaged) && _peaceAggressor == 0 && !PED.IS_PED_IN_MELEE_COMBAT(player.Handle) && PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) <= 0 && !GunMelee.PlayerFiredRecently)
		{
			return false;
		}
		tm.Calmed = true;
		bool flag = Mind.F(tm.Rec, "Bravery") >= 60;
		GrudgeLog("TOWN " + tm.Rec.Name + " started to run from the fight (the game's townsfolk panic) - " + (flag ? "he stands and watches instead" : "he walks off instead"));
		KeepOutOfIt(tm, player, !flag);
		return true;
	}

	private void KeepOutOfIt(TownMan tm, Ped player, bool leave)
	{
		int handle = tm.Ped.Handle;
		Crumbs.Native("Town man keeps out of it");
		TASK.CLEAR_PED_TASKS(handle, p1: true, p2: true);
		tm.Ped.BlockPermanentEvents = true;
		if (leave)
		{
			TASK.TASK_WANDER_STANDARD(handle, 10f, 10);
			tm.Leaving = true;
			tm.LeftAt = Game.GameTime;
			if (tm.Blip != null && tm.Blip.Exists())
			{
				tm.Blip.Delete();
			}
			tm.Blip = null;
		}
		else
		{
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(handle, player.Handle, 4000, 0f, 0f, 0f);
		}
	}

	private void TownFolkStayOut(Ped player)
	{
		if (!_townMen.Any((TownMan t) => t.Engaged))
		{
			if (_folkRolled || _folkOut.Count > 0)
			{
				_folkRolled = false;
				_folkMayJoin = false;
				_folkJoined = 0;
				_folkOut.Clear();
			}
		}
		else
		{
			if (Game.GameTime < _nextFolkScan)
			{
				return;
			}
			_nextFolkScan = Game.GameTime + 500;
			if (!_folkRolled)
			{
				_folkRolled = true;
				_folkMayJoin = _rng.Next(100) < 25;
			}
			foreach (Ped item in PedsAround(player))
			{
				if (item == null || !item.Exists() || item.IsDead || item.IsPlayer || !item.IsHuman)
				{
					continue;
				}
				int handle = item.Handle;
				if (item.Position.DistanceTo(player.Position) > 80f || !PED.IS_PED_IN_COMBAT(handle, player.Handle) || TownManFor(handle) != null || RecordAt(handle) != null || IsOurs(handle) || IsLawman(item) || Kids.IsChild(item) || IsStoryPed(item) || GameOwned(item) || OutfitFighter(handle) || Gangs.ForModel((uint)item.Model.Hash) != null || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(handle, player.Handle, p2: true, p3: true) || PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, handle))
				{
					continue;
				}
				_fighters.TryGetValue(handle, out var value);
				string text = NameOf(handle);
				string text2 = value?.Temper ?? "Plain";
				bool flag = Tempers.Of(text2).Nerve >= 15;
				if (_folkOut.TryGetValue(handle, out var value2))
				{
					if (value2 != int.MinValue && Game.GameTime - value2 > 3000)
					{
						_folkOut[handle] = Game.GameTime;
						TASK.CLEAR_PED_TASKS(handle, p1: true, p2: true);
					}
					continue;
				}
				if (_folkMayJoin && _folkJoined == 0 && flag)
				{
					_folkJoined = 1;
					_folkOut[handle] = int.MinValue;
					GrudgeLog($"TOWN folk {text} ({text2}) stays in the fight - the one townsman this fight ({25}% roll)");
					continue;
				}
				_folkOut[handle] = Game.GameTime;
				if (value != null)
				{
					value.Done = true;
					value.DoneAt = Game.GameTime;
				}
				_peaceWaiting.Remove(handle);
				Crumbs.Native("Town folk stays out");
				TASK.CLEAR_PED_TASKS(handle, p1: true, p2: true);
				item.BlockPermanentEvents = true;
				if (flag)
				{
					TASK.TASK_TURN_PED_TO_FACE_ENTITY(handle, player.Handle, 4000, 0f, 0f, 0f);
				}
				else
				{
					TASK.TASK_WANDER_STANDARD(handle, 10f, 10);
				}
				GrudgeLog("TOWN folk " + text + " (" + text2 + ") was pulled into the fight by the game - stays out, " + (flag ? "stands and watches" : "backs off") + ((!_folkMayJoin) ? " (no townsman joins this fight)" : ((_folkJoined > 0) ? " (this fight's one townsman is already in)" : " (not a tough temper)")));
			}
		}
	}

	private string FightersText(List<NemesisRecord> f)
	{
		if (f.Count != 0)
		{
			return "you're fighting " + string.Join(", ", f.Select((NemesisRecord x) => x.Name));
		}
		return "a stranger's fight";
	}

	private void BystanderLog(TownMan tm, string what)
	{
		GrudgeLog("TOWN bystander " + tm.Rec.Name + " (" + (Gangs.ById(tm.Rec.Gang)?.Name ?? "no outfit") + "): " + what);
	}

	private void TownManApproach(TownMan tm, Ped player, float d)
	{
		if (!tm.Spoke && d < 12f)
		{
			tm.Spoke = true;
			string[] array = ((tm.Rec.HateTier != HateTier.Resentful) ? ((tm.Rec.HateTier != HateTier.Vengeful) ? new string[3] { "Nobody's going to save you this time.", "I'm going to bury you in this street.", "Say your prayers, mister." } : new string[3] { "Thought I'd never find you? Here I am.", "You don't walk away from me twice.", "This ends now, you hear?" }) : new string[3] { "You and me. Right here.", "I been waiting on you. Put 'em up.", "Remember me? I sure remember you." });
			ShowSubtitle(tm.Rec.Name + ": \"" + array[_rng.Next(array.Length)] + "\"");
			Say(tm.Ped, "CHALLENGE_THREATEN", player);
		}
		if (d < 4f || Game.GameTime - tm.ApproachAt > 25000 || PED.IS_PED_IN_COMBAT(tm.Ped.Handle, player.Handle))
		{
			tm.Approaching = false;
			GrudgeLog($"TOWN {tm.Rec.Name} has it out with you ({d:0} m)");
			EngageTownMan(tm, player);
		}
		else if (Game.GameTime - tm.ApproachAt > 1000 && Game.GameTime / 1000 % 4 == 0)
		{
			TASK.TASK_GO_TO_ENTITY(tm.Ped.Handle, player.Handle, -1, 3f, 1.4f, 0f, 0);
		}
	}

	private bool TownProvoked(int target, bool insult)
	{
		TownMan townMan = TownManFor(target);
		if (townMan == null || townMan.Engaged)
		{
			return false;
		}
		if (!insult)
		{
			ShowSubtitle(townMan.Rec.Name + ": \"" + Taunts.AntagonizeReply(_rng) + "\"");
			return true;
		}
		if (townMan.Disguised)
		{
			townMan.Rec.TimesAntagonized++;
			HiddenSlip(townMan.Rec, "antagonize");
			if (townMan.Disguised)
			{
				ShowSubtitle(townMan.Rec.Name + ": \"" + Taunts.AntagonizeReply(_rng) + "\"");
			}
			SaveRoster();
			return true;
		}
		int allies = townMan.Rec.TimesAntagonized++;
		int hate = townMan.Rec.Hate;
		townMan.Rec.AddHate(5);
		CapScuffle(townMan.Rec, hate);
		string temper;
		float lasting;
		float infamy;
		float repPart;
		float num = OpeningNerve(townMan.Ped, townMan.Rec, out temper, out lasting, out allies, out infamy, out repPart);
		if (num < 30f)
		{
			int fear = townMan.Rec.Fear;
			townMan.Rec.AddFear(3);
			string[] array = ((!(temper == "Coward")) ? new string[3] { "Not here. Not like this.", "Another time, mister.", "You'll keep. I'm leaving." } : new string[3] { "Not here, mister... not today.", "I- I got no quarrel with you. Not now.", "You ain't worth it. I'm going." });
			ShowSubtitle(townMan.Rec.Name + ": \"" + array[_rng.Next(array.Length)] + "\"");
			GrudgeLog($"TOWN {townMan.Rec.Name} ({temper}) backs down at the words - nerve {num:0}, under 30: he walks off (hate {Delta(hate, townMan.Rec.Hate)}, fear {Delta(fear, townMan.Rec.Fear)})");
			Feed("ANTAGONIZE " + townMan.Rec.Name + " in " + townMan.Town + ": he won't take the bait - he walks off", Theme.Warn, townMan.Rec);
			townMan.Calmed = true;
			KeepOutOfIt(townMan, Game.Player.Character, leave: true);
			SaveRoster();
			return true;
		}
		Feed("ANTAGONIZE " + townMan.Rec.Name + " in " + townMan.Town + ": hate " + Delta(hate, townMan.Rec.Hate) + " -> he fights", Theme.Blood, townMan.Rec);
		if (townMan.EngageAt == 0)
		{
			townMan.EngageAt = Game.GameTime + 2200;
		}
		return true;
	}

	private void EngageTownMan(TownMan tm, Ped player)
	{
		HiddenSlip(tm.Rec, "fight");
		tm.Engaged = true;
		ClaimBack(tm.Ped);
		if (_townFightGroup == 0)
		{
			_townFightGroup = World.AddRelationshipGroup("NEMESIS_TOWN_FIGHT");
		}
		tm.Ped.RelationshipGroup = _townFightGroup;
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _townFightGroup, player.RelationshipGroup);
		tm.Ped.BlockPermanentEvents = true;
		tm.EngagedAt = Game.GameTime;
		tm.StuckTries = 0;
		bool flag = PED.IS_PED_USING_ANY_SCENARIO(tm.Ped.Handle);
		bool flag2 = ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(tm.Ped.Handle, player.Handle, p2: true, p3: true);
		if (flag && flag2)
		{
			Crumbs.Native("TownPresence out of seat");
			TASK.CLEAR_PED_TASKS_IMMEDIATELY(tm.Ped.Handle, p1: false, resetCrouch: true);
			GrudgeLog("TOWN " + tm.Rec.Name + " gets up out of his seat to fight (hurt - at once)");
		}
		else if (flag)
		{
			Crumbs.Native("TownPresence out of seat");
			TASK.CLEAR_PED_TASKS(tm.Ped.Handle, p1: false, p2: false);
			GrudgeLog("TOWN " + tm.Rec.Name + " gets up out of his seat to fight");
		}
		if (tm.Blip != null && tm.Blip.Exists())
		{
			tm.Blip.Delete();
		}
		Crumbs.Native("TownPresence blip add (V2)");
		tm.Blip = tm.Ped.AddBlip(BlipType.BLIP_STYLE_ENEMY);
		if (flag && !flag2)
		{
			QueueFight(tm.Ped, player, 900);
		}
		else
		{
			TASK.TASK_COMBAT_PED(tm.Ped.Handle, player.Handle, 0, 16);
		}
		PED.SET_PED_KEEP_TASK(tm.Ped.Handle, toggle: true);
		tm.Rec.TimesAmbushed++;
		tm.Rec.Touch();
		SaveRoster();
		GrudgeLog("TOWN " + tm.Rec.Name + " engaged");
		try
		{
			TownFightReaction(tm.Ped, player, tm.Town, Loadout.BandOf(tm.Rec) == Loadout.Band.Gun);
		}
		catch (Exception ex)
		{
			GrudgeLog("FIGHT town reaction failed: " + ex.Message);
		}
		Feed($"FIGHT {tm.Rec.Name} in {tm.Town} ({tm.Intent})", Theme.Blood, tm.Rec);
	}

	private void UnstickTownFighter(TownMan tm, Ped player, float d)
	{
		int handle = tm.Ped.Handle;
		if (tm.EngagedAt == 0 || tm.StuckTries >= 2 || d < 3f || d > 60f || NerveOut(handle) || _runners.ContainsKey(handle) || tm.Ped.IsHogtied || tm.Ped.IsRagdoll || LooksKnockedOut(tm.Ped) || PED.IS_PED_FLEEING(handle) || (_fighters.TryGetValue(handle, out var value) && (value.Broke || value.Done)) || (PED.IS_PED_IN_COMBAT(handle, player.Handle) && !PED.IS_PED_USING_ANY_SCENARIO(handle)))
		{
			return;
		}
		int num = Game.GameTime - tm.EngagedAt;
		if (tm.StuckTries == 0 && num > 5000)
		{
			tm.StuckTries = 1;
			Crumbs.Native("TownPresence unstick 1");
			TASK.CLEAR_PED_TASKS_IMMEDIATELY(handle, p1: false, resetCrouch: true);
			TASK.TASK_COMBAT_PED(handle, player.Handle, 0, 16);
			GrudgeLog(string.Format("TOWN {0} engaged 5 s ago but isn't fighting ({1:0} m, {2}) - pulled out and sent again", tm.Rec.Name, d, PED.IS_PED_USING_ANY_SCENARIO(handle) ? "still in a scenario" : "standing"));
		}
		else
		{
			if (tm.StuckTries != 1 || num <= 10000)
			{
				return;
			}
			tm.StuckTries = 2;
			Vector3 position = tm.Ped.Position;
			bool flag = !Walkable(position) || position.Z - player.Position.Z > 2.5f;
			if (flag)
			{
				Vector3 safeCoordForPed = World.GetSafeCoordForPed(player.Position + (position - player.Position) * 0.5f, onGround: true, 0);
				if (safeCoordForPed != Vector3.Zero && Outdoors(safeCoordForPed) && HiddenFromView(player, safeCoordForPed + new Vector3(0f, 0f, 1f)))
				{
					Crumbs.Native("TownPresence unstick move");
					tm.Ped.Position = safeCoordForPed;
				}
				else
				{
					GrudgeLog("TOWN " + tm.Rec.Name + ": no hidden walkable spot to move him to");
				}
			}
			TASK.CLEAR_PED_TASKS_IMMEDIATELY(handle, p1: false, resetCrouch: true);
			TASK.TASK_COMBAT_PED(handle, player.Handle, 0, 16);
			GrudgeLog(string.Format("TOWN {0} still not fighting after 10 s ({1:0} m, {2}) - sent again, last try", tm.Rec.Name, d, flag ? "off the walkways - moved" : "on walkable ground"));
		}
	}

	private unsafe void ClearTownMan(TownMan tm, bool delete)
	{
		if (tm.Blip != null && tm.Blip.Exists())
		{
			tm.Blip.Delete();
		}
		tm.Blip = null;
		if (tm.Ped != null && tm.Ped.Exists())
		{
			if (delete && SafeToDelete(tm.Ped))
			{
				_worldNemeses.Remove(tm.Ped.Handle);
				_knockedOutWorld.Remove(tm.Ped.Handle);
				tm.Ped.Delete();
			}
			else
			{
				int handle = tm.Ped.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		else if (tm.Ped != null)
		{
			_worldNemeses.Remove(tm.Ped.Handle);
			_knockedOutWorld.Remove(tm.Ped.Handle);
		}
		_townMen.Remove(tm);
	}

	private void ClearTownPresence(bool delete)
	{
		foreach (TownMan item in _townMen.ToList())
		{
			ClearTownMan(item, delete);
		}
	}
}
