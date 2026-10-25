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
	private sealed class Raid
	{
		public Gang Gang;

		public string CampId;

		public Vector3 Target;

		public readonly List<Ped> Men = new List<Ped>();

		public int Coming;

		public int NextOrder;

		public bool Arrived;

		public bool ArrivedFirst;

		public Vector3 MovingTo;

		public int MoveOrderAt = -100000;

		public bool Mounted;

		public int StuckTries;

		public int StuckAt = -100000;

		public bool Wandering;

		public bool Hurry;

		public readonly List<Ped> Horses = new List<Ped>();

		public Vector3 Alert;

		public int AlertAt = -100000;

		public string AlertWhat = "";

		public bool Hostile;

		public Blip Dot;

		public int DotOn;

		public bool Responding;

		public int SpawnedAt;

		public int NextProgressLog;

		public bool Watch;

		public int WatchUntil;

		public readonly List<(Ped ped, NemesisRecord rec, string gang)> Seen = new List<(Ped, NemesisRecord, string)>();

		public string SiteLabel;

		public Vector3 SitePos;

		public readonly Dictionary<int, Ped> HorseOf = new Dictionary<int, Ped>();

		public bool YourShots;

		public Vector3 ShotAt;

		public int StillSince;

		public int BroughtIn;

		public int NextMountCheck;

		public int WatchYouUntil;

		public bool NavRide;

		public int HurryOrderAt;

		public readonly Dictionary<int, Vector3> Spot = new Dictionary<int, Vector3>();

		public readonly Dictionary<int, int> SpotOrderAt = new Dictionary<int, int>();

		public readonly Dictionary<int, int> FanSince = new Dictionary<int, int>();

		public int QuietFrom;

		public int ArrivedAt;

		public bool RidingOff;

		public bool SidedWithYou;

		public readonly Dictionary<int, int> FollowAt = new Dictionary<int, int>();

		public readonly HashSet<int> GotDown = new HashSet<int>();

		public readonly HashSet<int> Engaged = new HashSet<int>();

		public float ApproachAngle = float.NaN;

		public int Riders => Men.Count + Coming;
	}

	private readonly List<Raid> _raids = new List<Raid>();

	private readonly Dictionary<int, int> _retargetAt = new Dictionary<int, int>();

	private readonly Dictionary<string, int> _raidPending = new Dictionary<string, int>();

	private int _nextRespondRoll;

	private int _respondCooldownUntil;

	private int _nextRelations;

	internal string _skirmishGangA;

	internal string _skirmishGangB;

	private static readonly PedHash[] RaiderModels = new PedHash[3]
	{
		PedHash.g_m_y_uniexconfeds_01,
		PedHash.g_m_y_uniexconfeds_02,
		PedHash.g_m_o_uniexconfeds_01
	};

	private readonly Dictionary<string, uint> _patrolGroups = new Dictionary<string, uint>();

	private readonly Dictionary<string, string> _raidBy = new Dictionary<string, string>();

	private readonly Dictionary<int, (Vector3 pos, int at)> _patrolMoved = new Dictionary<int, (Vector3, int)>();

	private readonly Dictionary<int, (int n, int first, int until)> _remountTries = new Dictionary<int, (int, int, int)>();

	private static readonly HashSet<string> PatrolCrimes = new HashSet<string> { "kill", "gore", "shot", "cut", "rob", "stickup", "hogtie", "horse", "wagon", "horsekill" };

	private readonly HashSet<int> _patrolOpen = new HashSet<int>();

	private int _nextTalkWhy;

	private int _holsteredSince;

	private readonly Dictionary<int, uint> _patrolGroupWas = new Dictionary<int, uint>();

	private int _nextGunfireScan;

	private Vector3? _lastGunfire;

	private int _lastGunfireAt = -100000;

	private string _lastGunfireWhat = "";

	private readonly Dictionary<Raid, PatrolScene> _scenes = new Dictionary<Raid, PatrolScene>();

	private readonly Dictionary<Raid, int> _nextSideCheck = new Dictionary<Raid, int>();

	private Gang PatrolFaction => Gangs.All.FirstOrDefault((Gang g) => (g.PatrolsIn == "north" && North) || (g.PatrolsIn == "south" && South));

	private uint GroupFor(Gang g)
	{
		if (!_patrolGroups.TryGetValue(g.Id, out var value))
		{
			value = (_patrolGroups[g.Id] = World.AddRelationshipGroup((g == Gangs.Army) ? "NEMESIS_PATROL" : ("NEMESIS_PATROL_" + g.Id.ToUpperInvariant())));
		}
		return value;
	}

	private static IEnumerable<Gang> PatrolFactions => Gangs.All.Where((Gang g) => g.PatrolsIn != null);

	private bool AfterYou(Gang g)
	{
		if (!g.NeedsReason)
		{
			return NotorietyOf(g) >= Balance.ArmyNotorietyGate;
		}
		return ReasonMet(g);
	}

	private void SetPatrolRelations(Ped player)
	{
		if (_campGroup == 0)
		{
			_campGroup = World.AddRelationshipGroup("NEMESIS_CAMP");
		}
		List<Gang> list = (from gang in PatrolFactions.Concat(_raids.Select((Raid r) => r.Gang)).Concat(_roadblocks.Select((Roadblock r) => r.Gang)).Concat(_walkers.Select((Walkers w) => w.Gang))
				.Concat(new Gang[1] { _supplyWagonGang })
			where gang != null
			select gang).Distinct().ToList();
		foreach (Gang g in list)
		{
			uint num = GroupFor(g);
			int relationship = (AtWar(g, Gangs.ODriscolls) ? 6 : 3);
			World.SetRelationshipBetweenGroups((eRelationshipType)relationship, num, _campGroup);
			World.SetRelationshipBetweenGroups((eRelationshipType)relationship, _campGroup, num);
			World.SetRelationshipBetweenGroups((_raids.Any((Raid r) => r.Gang == g && r.Hostile) || AfterYou(g)) ? eRelationshipType.Hate : eRelationshipType.Ignore, num, player.RelationshipGroup);
			if (_sideA != 0)
			{
				int relationship2 = ((_skirmishGangA == g.Id) ? 1 : 6);
				eRelationshipType relationship3 = ((_skirmishGangB == g.Id) ? eRelationshipType.Respect : eRelationshipType.Hate);
				World.SetRelationshipBetweenGroups((eRelationshipType)relationship2, num, _sideA);
				World.SetRelationshipBetweenGroups((eRelationshipType)relationship2, _sideA, num);
				World.SetRelationshipBetweenGroups(relationship3, num, _sideB);
				World.SetRelationshipBetweenGroups(relationship3, _sideB, num);
			}
		}
		foreach (Gang item in list)
		{
			foreach (Gang item2 in list)
			{
				if (item != item2)
				{
					World.SetRelationshipBetweenGroups(AtWar(item, item2) ? eRelationshipType.Hate : eRelationshipType.Ignore, GroupFor(item), GroupFor(item2));
				}
			}
		}
		foreach (Raid raid in _raids)
		{
			if (raid.CampId != null && _liveCamps.TryGetValue(raid.CampId, out var value))
			{
				HateCamp(raid.Gang, value);
			}
		}
	}

	private void RaidArrived(Raid r)
	{
		r.Arrived = true;
		r.ArrivedAt = Game.GameTime;
		GrudgeLog("PATROL reached " + r.CampId);
		r.ArrivedFirst = !_liveCamps.TryGetValue(r.CampId, out var value) || !value.Engaged;
		if (_liveCamps.TryGetValue(r.CampId, out var value2))
		{
			Gang gang = Gangs.ById(value2.Boss?.Gang ?? SiteOwner(value2.Site));
			Feed("PATROL the " + ShortOutfit(r.Gang) + " hit " + ((gang != null) ? ("the " + ShortOutfit(gang) + " camp") : "a camp"), r.Gang.Accent);
			foreach (Ped item in value2.Peds.Where((Ped cp) => cp.Exists() && !cp.IsDead))
			{
				if (!NerveOut(item.Handle) && !item.IsHogtied)
				{
					TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(item.Handle, 90f, 0, 0uL);
				}
				r.Seen.Add((item, _worldNemeses.TryGetValue(item.Handle, out var value3) ? value3 : null, value2.Boss?.Gang ?? Gangs.ODriscolls.Id));
			}
			r.SiteLabel = value2.Site.Label;
			r.SitePos = SitePos(value2.Site);
		}
		else
		{
			CampSite campSite = Camps.ById(r.CampId);
			if (campSite != null)
			{
				r.SiteLabel = campSite.Label;
				r.SitePos = SitePos(campSite);
			}
		}
		foreach (Ped item2 in r.Men.Where((Ped sm) => sm.Exists()))
		{
			r.Seen.Add((item2, _worldNemeses.TryGetValue(item2.Handle, out var value4) ? value4 : null, r.Gang.Id));
		}
	}

	private void HateCamp(Gang by, LiveCamp lc)
	{
		if (by != null && lc != null)
		{
			uint num = CampGroupOf(lc);
			uint num2 = GroupFor(by);
			World.SetRelationshipBetweenGroups(eRelationshipType.Hate, num2, num);
			World.SetRelationshipBetweenGroups(eRelationshipType.Hate, num, num2);
		}
	}

	internal bool SpawnRoadPatrol(Ped player, bool force = false, Gang faction = null)
	{
		faction = faction ?? PatrolFaction ?? (force ? Gangs.Army : null);
		if (faction == null || (!force && _raids.Any((Raid r) => r.CampId == null && r.Gang == faction)))
		{
			return false;
		}
		if (faction == Gangs.Army)
		{
			LiveCamp nearCamp = (from c in _liveCamps.Values
				where c.Boss?.Gang == Gangs.ODriscolls.Id && SitePos(c.Site).DistanceTo(player.Position) < 300f
				orderby SitePos(c.Site).DistanceTo(player.Position)
				select c).FirstOrDefault();
			if (nearCamp != null && !_raids.Any((Raid r) => r.CampId == nearCamp.Site.Id))
			{
				SpawnRaid(nearCamp, player);
				return true;
			}
		}
		Vector3 vector = OutOfSightSpot(player, 120f, 170f);
		if (vector == Vector3.Zero)
		{
			return false;
		}
		Vector3 vector2 = player.Position - vector;
		vector2.Normalize();
		Vector3 target = PatrolPoint(player.Position + vector2 * 160f + new Vector3((float)(_rng.NextDouble() * 60.0 - 30.0), (float)(_rng.NextDouble() * 60.0 - 30.0), 0f));
		Raid raid = SpawnPatrolMen(vector, target, player, null, faction, PatrolRides(vector) && (!ShortOfArms(faction) || _rng.Next(100) >= 40));
		if (raid == null)
		{
			return false;
		}
		GrudgeLog($"PATROL {faction.Id} on the road: {raid.Riders} riders, {vector.DistanceTo(player.Position):0} m off, after you: {AfterYou(faction)}");
		Feed(string.Format("ENCOUNTER {0} PATROL: {1} {2} on the road ({3}), {4:0} m off", faction.Name.ToUpperInvariant(), raid.Riders, raid.Mounted ? "riders" : "men on foot", raid.Hurry ? "in a hurry" : "taking it slow", vector.DistanceTo(player.Position)), faction.Accent);
		return true;
	}

	internal bool SpawnRidgeWatch(Ped player, Gang faction)
	{
		if (faction == null || faction.TroopModels.Length == 0 || _raids.Any((Raid r) => r.Watch))
		{
			return false;
		}
		Vector3 vector = Vector3.Zero;
		float num = 0f;
		for (int num2 = 0; num2 < 24; num2++)
		{
			double num3 = _rng.NextDouble() * Math.PI * 2.0;
			float num4 = 100f + (float)_rng.NextDouble() * 70f;
			Vector3 vector2 = Ground(player.Position + new Vector3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, 0f));
			float num5 = vector2.Z - player.Position.Z;
			if ((!Projection.ToScreen(vector2 + new Vector3(0f, 0f, 1.5f), out var x, out var y) || !(x > -0.05f) || !(x < 1.05f) || !(y > -0.05f) || !(y < 1.05f) || num2 >= 18) && num5 > num)
			{
				num = num5;
				vector = vector2;
			}
		}
		if (vector == Vector3.Zero || num < 6f)
		{
			GrudgeLog($"RIDGE no high ground near you (best {num:0.0} m above you)");
			return false;
		}
		Raid raid = SpawnPatrolMen(vector, vector, player, null, faction, mounted: true, 2 + _rng.Next(3));
		if (raid == null)
		{
			return false;
		}
		raid.Watch = true;
		raid.Hurry = false;
		raid.WatchUntil = Game.GameTime + 50000 + _rng.Next(40000);
		raid.NextOrder = 0;
		GrudgeLog($"RIDGE {faction.Id}: {raid.Riders} riders on high ground {vector.DistanceTo(player.Position):0} m off, {num:0} m above you, after you: {AfterYou(faction)}");
		Feed($"ENCOUNTER {faction.Name.ToUpperInvariant()}: {raid.Riders} riders watching from a ridge, {vector.DistanceTo(player.Position):0} m off", faction.Accent);
		return true;
	}

	private bool UpdateRidgeWatch(Raid r, Ped lead, Ped player, Vector3? shot)
	{
		if (!r.Watch || lead == null)
		{
			return false;
		}
		float num = lead.Position.DistanceTo(player.Position);
		if (!r.Hostile && AfterYou(r.Gang) && num < 160f)
		{
			r.Watch = false;
			r.Hostile = true;
			SetPatrolRelations(player);
			GrudgeLog($"RIDGE {r.Gang.Id}: they know you - coming down the ridge at you ({num:0} m)");
			Feed("RIDGE " + r.Gang.Name + ": they know you - they're coming down the ridge", Theme.Blood);
			return false;
		}
		if (r.Hostile)
		{
			r.Watch = false;
			return false;
		}
		string text = ((Game.GameTime > r.WatchUntil) ? "seen enough" : ((num < 30f) ? "you came too close" : ((shot.HasValue && !r.Gang.AnswersGunfire && shot.Value.DistanceTo(lead.Position) < 250f) ? "shooting nearby" : null)));
		if (text != null)
		{
			Vector3 vector = lead.Position - player.Position;
			vector.Z = 0f;
			if (vector.Length() < 1f)
			{
				vector = new Vector3(1f, 0f, 0f);
			}
			vector.Normalize();
			r.Watch = false;
			r.Target = PatrolPoint(lead.Position + vector * 350f);
			r.Hurry = text != "seen enough";
			r.MovingTo = Vector3.Zero;
			r.MoveOrderAt = -100000;
			r.NextOrder = 0;
			GrudgeLog($"RIDGE {r.Gang.Id}: riding off ({text}), {num:0} m from you");
			return false;
		}
		if (Game.GameTime >= r.NextOrder)
		{
			r.NextOrder = Game.GameTime + 2500;
			foreach (Ped item in r.Men.Where((Ped m) => m.Exists() && !m.IsDead))
			{
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(item.Handle, player.Handle, 3000, 0f, 0f, 0f);
				TASK.TASK_LOOK_AT_ENTITY(item.Handle, player.Handle, 3000, 0, 51, 0);
			}
		}
		return true;
	}

	private void SpawnRespondingPatrol(Ped player, Vector3 at, string what, Gang faction)
	{
		Vector3 vector = Vector3.Zero;
		for (int i = 0; i < 24; i++)
		{
			if (!(vector == Vector3.Zero))
			{
				break;
			}
			double num = _rng.NextDouble() * Math.PI * 2.0;
			float num2 = (float)Balance.PatrolRespondMin + (float)_rng.NextDouble() * (float)Math.Max(0, Balance.PatrolRespondMax - Balance.PatrolRespondMin);
			Vector3 raw = at + new Vector3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, 0f);
			STREAMING.REQUEST_COLLISION_AT_COORD(raw.X, raw.Y, raw.Z);
			if (GroundOk(raw, out var p) && !(p.DistanceTo(player.Position) < 120f) && HiddenFromView(player, p))
			{
				vector = p;
			}
		}
		if (vector == Vector3.Zero)
		{
			GrudgeLog($"PATROL {faction.Id}: no loaded ground {Balance.PatrolRespondMin}-{Balance.PatrolRespondMax} m from the shooting - none sent");
			return;
		}
		Raid raid = SpawnPatrolMen(vector, at, player, null, faction, PatrolRides(vector) && (!ShortOfArms(faction) || _rng.Next(100) >= 40));
		if (raid != null)
		{
			raid.Responding = true;
			raid.SpawnedAt = Game.GameTime;
			raid.YourShots = what?.Contains("your") ?? false;
			raid.ShotAt = at;
			raid.Alert = at;
			raid.AlertAt = Game.GameTime;
			raid.AlertWhat = what;
			raid.NextOrder = 0;
			GrudgeLog($"PATROL {faction.Id} riding in from {vector.DistanceTo(at):0} m - heard {what}");
			Feed($"PATROL {faction.Name}: {raid.Riders} riders coming from {vector.DistanceTo(at):0} m - they heard {what}", faction.Accent);
		}
	}

	private void MaybeRaid(LiveCamp lc)
	{
		if (_raids.Any((Raid r) => r.CampId == lc.Site.Id) || _raidPending.ContainsKey(lc.Site.Id))
		{
			return;
		}
		string owner = lc.Boss?.Gang ?? SiteOwner(lc.Site);
		District sd = DistrictAt(SitePos(lc.Site));
		WarRecord warRecord = (from w in Wars
			where w.A == owner || w.B == owner
			orderby w.District == sd?.Key descending, w.Goal == "camp" && w.GoalSite == lc.Site.Id descending
			select w).FirstOrDefault();
		Gang gang = ((warRecord == null) ? null : Gangs.ById((warRecord.A == owner) ? warRecord.B : warRecord.A));
		if (gang != null && gang.TroopModels != null && gang.TroopModels.Length != 0 && !IsDestroyed(gang) && !TruceBetween(owner, gang.Id))
		{
			bool flag = sd != null && warRecord.District == sd.Key;
			int num = (flag ? Balance.WarCampRaidChance : Balance.WarCampRaidOutside) + ((warRecord.GoalSite == lc.Site.Id) ? 25 : 0);
			if (_rng.Next(100) < num)
			{
				_raidPending[lc.Site.Id] = Game.GameTime + 15000 + _rng.Next(45000);
				_raidBy[lc.Site.Id] = gang.Id;
				GrudgeLog($"PATROL war raid: the {gang.Name} are coming for {lc.Site.Id} ({Gangs.ById(owner)?.Name}, {num}%)");
				return;
			}
			GrudgeLog(string.Format("PATROL war raid on {0}: the {1} could come - rolled no ({2}%{3})", lc.Site.Id, gang.Name, num, flag ? "" : ", outside the war's district"));
		}
		if (!(owner != Gangs.ODriscolls.Id) && North && !TruceBetween(Gangs.Army.Id, owner) && !IsDestroyed(Gangs.Army) && _rng.Next(100) < Balance.CampRaidChance)
		{
			_raidPending[lc.Site.Id] = Game.GameTime + 15000 + _rng.Next(45000);
			_raidBy[lc.Site.Id] = Gangs.Army.Id;
			GrudgeLog("PATROL an Army patrol is coming for " + lc.Site.Id);
		}
	}

	private void SpawnRaid(LiveCamp lc, Ped player)
	{
		Vector3 vector = SitePos(lc.Site);
		Vector3 vector2 = Vector3.Zero;
		Vector3 vector3 = vector - player.Position;
		vector3.Z = 0f;
		double num = ((vector3.Length() > 1f) ? Math.Atan2(vector3.Y, vector3.X) : (_rng.NextDouble() * Math.PI * 2.0));
		for (int i = 0; i < 16; i++)
		{
			if (!(vector2 == Vector3.Zero))
			{
				break;
			}
			double num2 = num + (_rng.NextDouble() - 0.5) * ((i < 8) ? (Math.PI * 9.0 / 10.0) : (Math.PI * 2.0));
			float num3 = 70f + (float)_rng.NextDouble() * 30f;
			Vector3 vector4 = Ground(vector + new Vector3((float)Math.Cos(num2) * num3, (float)Math.Sin(num2) * num3, 0f));
			if (!(vector4.DistanceTo(player.Position) < 60f) && HiddenFromView(player, vector4) && Dry(vector4))
			{
				vector2 = vector4;
			}
		}
		if (vector2 == Vector3.Zero)
		{
			GrudgeLog("PATROL raid on " + lc.Site.Id + ": no spot out of sight 70-100 m from it - none sent");
			return;
		}
		string value;
		Gang gang = (_raidBy.TryGetValue(lc.Site.Id, out value) ? (Gangs.ById(value) ?? Gangs.Army) : Gangs.Army);
		_raidBy.Remove(lc.Site.Id);
		Raid raid = SpawnPatrolMen(vector2, vector, player, lc.Site.Id, gang, mounted: true);
		if (raid == null)
		{
			return;
		}
		raid.Hurry = true;
		HateCamp(gang, lc);
		foreach (Ped man in raid.Men)
		{
			MoveTo(raid, man, vector, fast: true);
		}
		Gang gang2 = Gangs.ById(lc.Boss?.Gang ?? SiteOwner(lc.Site));
		GrudgeLog($"PATROL {raid.Riders} {gang.Name} men riding on {lc.Site.Id} from {vector2.DistanceTo(vector):0} m; after you: {AfterYou(gang)}");
		GrudgeNotice((gang == Gangs.Army) ? "Soldiers are moving on the O'Driscoll camp." : ("The " + gang.Name + " are riding on " + ((gang2 != null) ? ("a camp of the " + gang2.Name) : "the camp") + "."));
	}

	private bool PatrolRides(Vector3 at)
	{
		if (_nearFort || InFortWallace(at))
		{
			return _rng.Next(100) < Balance.PatrolMountedPct;
		}
		return true;
	}

	private bool Stuck(Ped m)
	{
		if (_patrolMoved.Count > 64)
		{
			foreach (int item in _patrolMoved.Keys.Where((int k) => !ENTITY.DOES_ENTITY_EXIST(k)).ToList())
			{
				_patrolMoved.Remove(item);
			}
		}
		Vector3 position = m.Position;
		if (!_patrolMoved.TryGetValue(m.Handle, out (Vector3, int) value) || value.Item1.DistanceTo(position) > 1f)
		{
			_patrolMoved[m.Handle] = (position, Game.GameTime);
			return false;
		}
		if (Game.GameTime - value.Item2 < 6000)
		{
			return false;
		}
		_patrolMoved[m.Handle] = (position, Game.GameTime);
		return true;
	}

	internal void ArmTroop(Ped p, Gang faction, eWeapon? longGun = null)
	{
		WEAPON.REMOVE_ALL_PED_WEAPONS(p.Handle, p1: true, p2: true);
		if (faction != null && faction.TribalKit)
		{
			p.GiveWeapon(faction.KitBlade, 1, eWeaponAttachPoint.Melee, bForceInHand: false, bForceInHolster: true);
			p.GiveWeapon(faction.KitBow, 60, eWeaponAttachPoint.Bow, bForceInHand: true);
			Loadout.GiveArrows(p);
		}
		else
		{
			eWeapon weapon = (eWeapon)(((int?)longGun) ?? ((faction != null && (faction.TroopLongGuns || _rng.Next(2) == 0)) ? (-183018591) : 379542007));
			p.GiveWeapon(weapon, 100, eWeaponAttachPoint.Rifle, bForceInHand: true);
		}
	}

	private Raid SpawnPatrolMen(Vector3 from, Vector3 target, Ped player, string campId, Gang faction, bool mounted, int count = 0)
	{
		if (faction?.TroopModels == null || faction.TroopModels.Length == 0)
		{
			return null;
		}
		int num = ((count > 0) ? count : (4 + _rng.Next(2)));
		int num2 = FieldCap(faction);
		if (num > num2)
		{
			if (num2 < 2)
			{
				GrudgeLog($"PATROL {faction.Id}: {MenOf(faction)} men left - no patrol to send");
				return null;
			}
			GrudgeLog($"PATROL {faction.Id}: {MenOf(faction)} men left - {num2} ride, not {num}");
			num = num2;
		}
		uint grp = GroupFor(faction);
		Raid raid = new Raid
		{
			CampId = campId,
			Target = target,
			Gang = faction,
			Mounted = mounted,
			NavRide = true,
			Hurry = (_rng.Next(100) < Balance.PatrolHurryPct),
			SpawnedAt = Game.GameTime
		};
		_raids.Add(raid);
		SetPatrolRelations(player);
		float heading = HeadingTo(from, target);
		NemesisRecord named = ((_rng.Next(100) < Config.CrewRosterChance) ? (from _ in Roster
			where _.Gang == faction.Id && _.Rank < 4 && _.CanHunt && !_.PresumedDead && !Resting(_) && !_worldNemeses.ContainsValue(_) && _ != ActiveRecord
			orderby _rng.Next()
			select _).FirstOrDefault() : null);
		List<uint> loaded = new List<uint>();
		foreach (PedHash item in faction.TroopModels.Distinct())
		{
			Model m = new Model((int)item);
			if (!m.IsLoaded)
			{
				RequestModel(m, 2000);
				loaded.Add((uint)item);
			}
		}
		Model m2 = new Model((int)faction.TroopModels[0]);
		if (!LoadModelSafe(m2, 1))
		{
			Release();
			_raids.Remove(raid);
			return null;
		}
		int failed = 0;
		for (int num3 = 0; num3 < Math.Min(num, 2); num3++)
		{
			One(num3, late: false);
		}
		if (failed > 0)
		{
			GrudgeLog($"PATROL {faction.Id}: {failed} of {Math.Min(num, 2)} men didn't spawn");
		}
		if (raid.Men.Count == 0)
		{
			Release();
			_raids.Remove(raid);
			return null;
		}
		if (num <= 2)
		{
			Release();
			return raid;
		}
		raid.Coming = num - 2;
		for (int num4 = 2; num4 < num; num4++)
		{
			int k = num4;
			Stage(delegate
			{
				One(k, late: true);
			}, () => _raids.Contains(raid) && !WorldBusy(player), (k == num - 1) ? new Action(Release) : ((Action)delegate
			{
				if (!_raids.Contains(raid))
				{
					raid.Coming = 0;
				}
			}));
		}
		return raid;
		void One(int i, bool late)
		{
			Vector3 pos = Ground(from + new Vector3(((i % 2 == 0) ? 1.5f : (-1.5f)) * (float)(i / 2 + 1), -2.5f * (float)i, 0f));
			Ped ped;
			if (i == 0 && named != null)
			{
				ped = SpawnNemesisPed(named, pos, heading, grp, out var _, (uint)faction.TroopModels[0]);
				if (ped != null)
				{
					_worldNemeses[ped.Handle] = named;
					named.Touch();
					History(named, "ON PATROL near " + GetZoneName(target), faction.Accent);
				}
			}
			else
			{
				ped = SpawnPed(faction.TroopModels[_rng.Next(faction.TroopModels.Length)], pos, heading);
				if (ped != null)
				{
					MakeOurs(ped.Handle);
					ArmTroop(ped, faction);
					ped.Accuracy = faction.TroopAccuracy;
					ped.RelationshipGroup = grp;
					ApplyFactionStyle(ped, faction.Id);
				}
			}
			if (late)
			{
				raid.Coming = Math.Max(0, raid.Coming - 1);
			}
			if (ped == null)
			{
				failed++;
				if (late)
				{
					GrudgeLog("PATROL " + faction.Id + ": a rider coming on behind didn't spawn");
				}
			}
			else
			{
				ped.BlockPermanentEvents = true;
				PED.SET_PED_CONFIG_FLAG(ped.Handle, 297, value: true);
				PED.SET_PED_CONFIG_FLAG(ped.Handle, 130, value: false);
				raid.Men.Add(ped);
				if (mounted)
				{
					Ped ped2 = MountOnHorse(ped);
					if (ped2 != null)
					{
						_encHorses.Remove(ped2);
						raid.Horses.Add(ped2);
						raid.HorseOf[ped.Handle] = ped2;
					}
				}
				MoveTo(raid, ped, target, late && raid.Hurry && raid.CampId != null);
			}
		}
		void Release()
		{
			foreach (uint item2 in loaded)
			{
				STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED(item2);
			}
		}
	}

	private unsafe static bool GroundOk(Vector3 raw, out Vector3 p)
	{
		p = raw;
		float z = default(float);
		if (!MISC.GET_GROUND_Z_FOR_3D_COORD(raw.X, raw.Y, raw.Z + 100f, &z, p4: false))
		{
			return false;
		}
		p.Z = z;
		return true;
	}

	private static bool alertedOrThere(Raid r)
	{
		if (!r.Arrived && Game.GameTime - r.AlertAt >= 45000)
		{
			return r.Responding;
		}
		return true;
	}

	private void KeepPatrolMounted(Raid r, Ped player)
	{
		if (FightingNear(r, player))
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		foreach (Ped item in r.Men.Where((Ped m) => m.Exists() && !m.IsDead))
		{
			if (item.IsOnMount || PED.IS_PED_IN_COMBAT(item.Handle, 0) || NerveOut(item.Handle) || !r.HorseOf.TryGetValue(item.Handle, out var value) || value == null || !value.Exists() || value.IsDead)
			{
				continue;
			}
			if (_remountTries.TryGetValue(item.Handle, out (int, int, int) value2))
			{
				if (Game.GameTime < value2.Item3)
				{
					continue;
				}
				if (Game.GameTime - value2.Item2 > 30000)
				{
					value2 = (0, Game.GameTime, 0);
				}
			}
			else
			{
				value2 = (0, Game.GameTime, 0);
			}
			value2.Item1++;
			if (value2.Item1 > 3)
			{
				_remountTries[item.Handle] = (0, Game.GameTime, Game.GameTime + 60000);
				GrudgeLog($"PATROL {r.Gang.Id}: ped {item.Handle} keeps getting down - left on foot for a minute");
				continue;
			}
			_remountTries[item.Handle] = value2;
			float num3 = item.Position.DistanceTo(value.Position);
			if (item.Position.DistanceTo(player.Position) > 50f && HiddenFromView(player, item.Position))
			{
				if (num3 > 4f)
				{
					value.Position = item.Position + new Vector3(1.2f, 0f, 0f);
				}
				Crumbs.Native("Patrols onto mount");
				PED.SET_PED_ONTO_MOUNT(item.Handle, value.Handle, -1, p3: true);
				num++;
			}
			else if (num3 < 40f)
			{
				TASK.TASK_MOUNT_ANIMAL(item.Handle, value.Handle, 20000, -1, 2f, 1, 0uL, 0uL);
				num2++;
			}
		}
		if (num + num2 > 0)
		{
			r.MoveOrderAt = -100000;
			GrudgeLog($"PATROL {r.Gang.Id}: back in the saddle - {num} put on, {num2} climbing on");
		}
	}

	private void BringPatrolCloser(Raid r, Ped lead, Ped player)
	{
		Vector3 position = player.Position;
		Vector3 vector = lead.Position - position;
		vector.Z = 0f;
		float num = vector.Length();
		if (num < 1f)
		{
			return;
		}
		vector *= 1f / num;
		Vector3 vector2 = Vector3.Zero;
		for (int i = 0; i < 16; i++)
		{
			if (!(vector2 == Vector3.Zero))
			{
				break;
			}
			float num2 = Math.Min(130f + (float)_rng.Next(40), num * 0.7f);
			double num3 = Math.Atan2(vector.Y, vector.X) + (_rng.NextDouble() - 0.5) * ((i < 8) ? 0.8 : 2.4);
			Vector3 raw = position + new Vector3((float)Math.Cos(num3) * num2, (float)Math.Sin(num3) * num2, 0f);
			STREAMING.REQUEST_COLLISION_AT_COORD(raw.X, raw.Y, raw.Z);
			if (GroundOk(raw, out var p) && HiddenFromView(player, p))
			{
				vector2 = p;
			}
		}
		r.StillSince = 0;
		if (vector2 == Vector3.Zero)
		{
			r.BroughtIn++;
			GrudgeLog($"PATROL {r.Gang.Id}: stalled {num:0} m out and no hidden ground nearer - left as is");
			return;
		}
		int num4 = 0;
		foreach (Ped item in r.Men.Where((Ped m) => m.Exists() && !m.IsDead))
		{
			Vector3 position2 = Ground(vector2 + new Vector3(((num4 % 2 == 0) ? 2f : (-2f)) * (float)(num4 / 2 + 1), -3f * (float)num4, 0f));
			num4++;
			if (r.Mounted && r.HorseOf.TryGetValue(item.Handle, out var value) && value != null && value.Exists() && !value.IsDead)
			{
				value.Position = position2;
				Crumbs.Native("Patrols onto mount");
				PED.SET_PED_ONTO_MOUNT(item.Handle, value.Handle, -1, p3: true);
			}
			else
			{
				item.Position = position2;
			}
		}
		r.BroughtIn++;
		r.MoveOrderAt = -100000;
		r.NextOrder = 0;
		GrudgeLog($"PATROL {r.Gang.Id}: stalled {num:0} m out (the game wasn't moving them) - brought in to {vector2.DistanceTo(position):0} m, out of view");
	}

	internal Vector3 PatrolPoint(Vector3 want)
	{
		Vector3 vector = SnapToRoad(want, 90f);
		if (vector != Vector3.Zero && Dry(vector))
		{
			return vector;
		}
		return Ground(want);
	}

	private static void MoveTo(Raid r, Ped m, Vector3 to, bool fast)
	{
		float speedMultiplier = (fast ? 3f : (r.Hurry ? 2f : 1f));
		TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(m.Handle, to.X, to.Y, to.Z, speedMultiplier, -1, 6f, 0, 0f);
	}

	private void MountedCheck(Raid r, Ped lead)
	{
		if (r.Mounted && !r.NavRide && !(lead == null) && lead.IsOnMount && r.HurryOrderAt != 0 && Game.GameTime - r.HurryOrderAt >= 10000)
		{
			float num = ENTITY.GET_ENTITY_SPEED(lead.Handle);
			if (num >= 6f)
			{
				r.HurryOrderAt = 0;
				GrudgeLog($"PATROL {r.Gang.Id}: riding at {num:0.0} m/s - galloping");
				return;
			}
			r.NavRide = true;
			r.MoveOrderAt = -100000;
			r.NextOrder = 0;
			r.HurryOrderAt = 0;
			GrudgeLog($"PATROL {r.Gang.Id}: still only {num:0.0} m/s at a gallop order - switching to the nav-mesh ride");
		}
	}

	private void UpdateRaids(Ped player)
	{
		foreach (KeyValuePair<string, int> kv in _raidPending.ToList())
		{
			if (Game.GameTime < kv.Value)
			{
				continue;
			}
			string text = NoSpawnReason(player);
			bool flag = text != null && (text.StartsWith("on a mission") || text.StartsWith("settling") || text.StartsWith("on a train"));
			if (!flag && InAFight() && PatrolInTheFight(player))
			{
				flag = true;
			}
			if (flag)
			{
				_raidPending[kv.Key] = Game.GameTime + 10000;
				continue;
			}
			_raidPending.Remove(kv.Key);
			if (_liveCamps.TryGetValue(kv.Key, out var value) && !_raids.Any((Raid r) => r.CampId == kv.Key))
			{
				SpawnRaid(value, player);
			}
		}
		(Vector3? at, string what) tuple = Gunfire(player);
		Vector3? item = tuple.at;
		string item2 = tuple.what;
		Gang faction = PatrolFaction;
		(Gang, Gang)? warHere = WarHere;
		bool warNight = WarNight;
		if (warHere.HasValue)
		{
			List<Gang> list = new Gang[2]
			{
				warHere.Value.Item1,
				warHere.Value.Item2
			}.Where((Gang g) => g != null && FactionModels.ContainsKey(g.Id) && !_raids.Any((Raid r) => r.Gang == g && r.CampId == null)).ToList();
			faction = ((list.Count == 0) ? null : list[_rng.Next(list.Count)]);
		}
		if (item.HasValue && faction != null && Game.GameTime >= _nextRespondRoll && Game.GameTime >= _respondCooldownUntil && NoSpawnReason(player) == null && !InsideAnyTown(item.Value) && !InsideAnyTown(player.Position) && !_raids.Any((Raid r) => r.Gang == faction && r.CampId == null) && (!warHere.HasValue || _raids.Count((Raid r) => r.CampId == null) < 2) && (!InAFight() || !PatrolInTheFight(player)))
		{
			_nextRespondRoll = Game.GameTime + (warHere.HasValue ? 45000 : 60000);
			int num = ((!warHere.HasValue) ? Balance.PatrolRespondChance : (warNight ? 55 : 40));
			if (_rng.Next(100) < num)
			{
				_respondCooldownUntil = Game.GameTime + ((!warHere.HasValue) ? (Balance.PatrolRespondCooldown * 1000) : (warNight ? 120000 : 180000));
				if (warHere.HasValue)
				{
					GrudgeLog(string.Format("PATROL war: the {0} ride to the shooting ({1}%{2})", faction.Name, num, warNight ? ", night" : ""));
				}
				SpawnRespondingPatrol(player, item.Value, item2, faction);
			}
		}
		if ((_raids.Count > 0 || _roadblocks.Count > 0 || _walkers.Count > 0 || _supplyWagonMen.Count > 0) && Game.GameTime >= _nextRelations)
		{
			_nextRelations = Game.GameTime + 3000;
			SetPatrolRelations(player);
		}
		foreach (Raid item3 in _raids.ToList())
		{
			Ped ped = item3.Men.FirstOrDefault((Ped m) => m.Exists() && !m.IsDead);
			if (item.HasValue && ped != null && item3.Gang.AnswersGunfire && item.Value.DistanceTo(ped.Position) < Balance.PatrolHearingRange && (Game.GameTime - item3.AlertAt > 6000 || item.Value.DistanceTo(item3.Alert) > 40f))
			{
				bool num2 = Game.GameTime - item3.AlertAt > 60000;
				item3.Alert = item.Value;
				item3.AlertAt = Game.GameTime;
				item3.AlertWhat = item2;
				item3.NextOrder = 0;
				if (num2)
				{
					GrudgeLog(string.Format("PATROL {0} heard {1} {2:0} m off - {3} to it", item3.Gang.Id, item2, item.Value.DistanceTo(ped.Position), item3.Mounted ? "riding" : "running"));
					Feed(string.Format("PATROL {0} heard {1} - {2} there, {3:0} m", item3.Gang.Name, item2, item3.Mounted ? "riding" : "running", item.Value.DistanceTo(ped.Position)), item3.Gang.Accent);
				}
			}
			bool flag2 = !item3.Hostile && item3.Horses.Any((Ped h) => h != null && h.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(h.Handle, player.Handle, p2: true, p3: true));
			if (flag2)
			{
				GrudgeLog("PATROL " + item3.Gang.Id + ": you shot one of their horses");
			}
			if (!item3.Hostile && (flag2 || item3.Men.Any((Ped m) => m.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(m.Handle, player.Handle, p2: true, p3: true))))
			{
				item3.Hostile = true;
				SetPatrolRelations(player);
				GrudgeLog("PATROL " + item3.Gang.Id + ": you shot one of them - they're after you");
				Feed("PATROL " + item3.Gang.Name + ": you shot one of them - they're after you", Theme.Blood);
			}
			if (alertedOrThere(item3))
			{
				JudgeTheFight(item3, ped, player);
			}
			bool flag3 = item3.Hostile || AfterYou(item3.Gang) || item3.Men.Any((Ped m) => m.Exists() && !m.IsDead && PED.IS_PED_IN_COMBAT(m.Handle, player.Handle));
			if (item3.Dot != null && item3.Dot.Exists() && ped != null && item3.DotOn != ped.Handle)
			{
				item3.Dot.Delete();
				item3.Dot = null;
			}
			if (!flag3 && ped != null && (item3.Dot == null || !item3.Dot.Exists()))
			{
				Crumbs.Native("Patrols blip add (V2)");
				item3.Dot = ped.AddBlip(BlipType.BLIP_STYLE_NEUTRAL);
				item3.DotOn = ped.Handle;
				if (item3.Dot != null)
				{
					Function.Call(11290983183885202434uL, item3.Dot.Handle, new InputArgument(UiDraw.ForeverVarString(item3.Watch ? (item3.Gang.Name + " riders") : (item3.Gang.Name + " patrol"))));
				}
			}
			else if (flag3 && item3.Dot != null)
			{
				if (item3.Dot.Exists())
				{
					item3.Dot.Delete();
				}
				item3.Dot = null;
			}
			if (UpdateRidgeWatch(item3, ped, player, item))
			{
				continue;
			}
			bool flag4 = item3.Responding && !item3.Arrived && Game.GameTime - item3.SpawnedAt < 180000;
			bool flag5 = Game.GameTime - item3.AlertAt < 45000 || flag4;
			if (flag4 && ped != null && item3.YourShots && player.Position.DistanceTo(item3.ShotAt) < 300f && player.Position.DistanceTo(item3.Alert) > 40f && Game.GameTime - item3.MoveOrderAt > 8000)
			{
				item3.Alert = player.Position;
			}
			if (flag4 && ped != null)
			{
				float num3 = ped.Position.DistanceTo(item3.Alert);
				if (ENTITY.GET_ENTITY_SPEED(ped.Handle) > (item3.Mounted ? 3f : 1f) || num3 < 60f)
				{
					item3.StillSince = 0;
				}
				else if (item3.StillSince == 0)
				{
					item3.StillSince = Game.GameTime;
				}
				else if (Game.GameTime - item3.StillSince > 12000 && ped.Position.DistanceTo(player.Position) > 180f && item3.BroughtIn < 2)
				{
					BringPatrolCloser(item3, ped, player);
				}
				if (num3 < 50f)
				{
					item3.Arrived = true;
					if (item3.YourShots && !item3.Hostile && !AfterYou(item3.Gang) && ped.Position.DistanceTo(player.Position) < 80f)
					{
						item3.WatchYouUntil = Game.GameTime + 20000;
					}
					GrudgeLog($"PATROL {item3.Gang.Id} (responding) got there after {(Game.GameTime - item3.SpawnedAt) / 1000} s, {ped.Position.DistanceTo(player.Position):0} m from you");
				}
				else if (Game.GameTime >= item3.NextProgressLog)
				{
					item3.NextProgressLog = Game.GameTime + 10000;
					GrudgeLog(string.Format("PATROL {0} (responding) {1} s: {2:0} m to go, {3:0} m from you, {4}, speed {5:0.0} m/s", item3.Gang.Id, (Game.GameTime - item3.SpawnedAt) / 1000, num3, ped.Position.DistanceTo(player.Position), (!item3.Mounted) ? "on foot" : (ped.IsOnMount ? "riding" : "OFF HIS HORSE"), ENTITY.GET_ENTITY_SPEED(ped.Handle)));
				}
			}
			bool flag6 = item3.CampId == null || !_liveCamps.ContainsKey(item3.CampId);
			float num4 = (from m in item3.Men
				where m.Exists()
				select m.Position.DistanceTo(player.Position)).DefaultIfEmpty(9999f).Min();
			if (ped == null || (flag6 && !flag5 && num4 > 300f) || num4 > (flag5 ? 900f : 450f))
			{
				EndRaid(item3);
				continue;
			}
			if (item3.Wandering && !flag5 && !item3.Hostile && num4 > 120f)
			{
				GrudgeLog($"PATROL {item3.Gang.Id}: wandered off - gone ({num4:0} m off)");
				EndRaid(item3);
				continue;
			}
			if (item3.RidingOff && flag5)
			{
				item3.RidingOff = false;
				GrudgeLog("PATROL " + item3.Gang.Id + ": shooting again - they turn back");
			}
			if (item3.RidingOff && num4 > 160f)
			{
				GrudgeLog($"PATROL {item3.Gang.Id}: rode off after the fight - gone ({num4:0} m off)");
				EndRaid(item3);
				continue;
			}
			if (item3.RidingOff && ped != null && ped.Position.DistanceTo(item3.Target) < 40f)
			{
				Vector3 vector = ped.Position - player.Position;
				vector.Z = 0f;
				if (vector.Length() < 1f)
				{
					vector = new Vector3(1f, 0f, 0f);
				}
				vector.Normalize();
				item3.Target = PatrolPoint(ped.Position + vector * 300f);
				item3.MovingTo = Vector3.Zero;
				item3.NextOrder = 0;
			}
			if (item3.CampId == null && !item3.Responding && !item3.Watch && !flag5 && !item3.Hostile && ped != null && (ped.Position.DistanceTo(item3.Target) < 40f || Game.GameTime - item3.SpawnedAt > 360000))
			{
				if (num4 > 150f && Game.GameTime - item3.SpawnedAt > 120000)
				{
					GrudgeLog($"PATROL {item3.Gang.Id}: done with its road - gone ({num4:0} m off)");
					EndRaid(item3);
					continue;
				}
				if (ped.Position.DistanceTo(item3.Target) < 40f)
				{
					Vector3 vector2 = ped.Position - player.Position;
					vector2.Z = 0f;
					if (vector2.Length() < 1f)
					{
						vector2 = new Vector3(1f, 0f, 0f);
					}
					vector2.Normalize();
					item3.Target = PatrolPoint(ped.Position + vector2 * 300f);
					item3.MovingTo = Vector3.Zero;
					item3.NextOrder = 0;
					GrudgeLog($"PATROL {item3.Gang.Id}: reached its point {num4:0} m from you - riding on");
				}
			}
			if (item3.CampId != null && !item3.Arrived && ped != null && ped.Position.DistanceTo(item3.Target) < 70f)
			{
				RaidArrived(item3);
			}
			bool flag7 = item3.CampId != null && ped != null && ped.Position.DistanceTo2D(item3.Target) < 140f;
			if (item3.Mounted && (flag5 || flag4) && !flag7 && !_scenes.ContainsKey(item3) && Game.GameTime >= item3.NextMountCheck)
			{
				item3.NextMountCheck = Game.GameTime + 2000;
				KeepPatrolMounted(item3, player);
			}
			if (Game.GameTime < item3.WatchYouUntil && !item3.Hostile && FiredInAnger && ped != null && ped.Position.DistanceTo(player.Position) < 60f)
			{
				item3.Hostile = true;
				item3.WatchYouUntil = 0;
				SetPatrolRelations(player);
				GrudgeLog("PATROL " + item3.Gang.Id + ": you fired again while they had you covered - they're after you");
				Feed("PATROL " + item3.Gang.Name + ": you fired again in front of them - they're after you", Theme.Blood);
			}
			if (Game.GameTime < item3.WatchYouUntil && !item3.Hostile)
			{
				if (WEAPON.IS_PED_ARMED(player.Handle, 4) || WEAPON.IS_PED_ARMED(player.Handle, 1))
				{
					_holsteredSince = 0;
				}
				else if (_holsteredSince == 0)
				{
					_holsteredSince = Game.GameTime;
				}
				else if (Game.GameTime - _holsteredSince > 3000)
				{
					item3.WatchYouUntil = 0;
					item3.AlertAt = Math.Min(item3.AlertAt, Game.GameTime - 10001);
					foreach (Ped item4 in item3.Men.Where((Ped m) => m.Exists() && !m.IsDead && !PED.IS_PED_IN_COMBAT(m.Handle, 0)))
					{
						TASK.CLEAR_PED_TASKS(item4.Handle, p1: false, p2: false);
					}
					GrudgeLog("PATROL " + item3.Gang.Id + ": you put your gun away - they lower theirs");
				}
			}
			if (_hisPeople?.Patrol == item3 || UpdatePatrolScene(item3, ped, player) || Game.GameTime < item3.NextOrder)
			{
				continue;
			}
			item3.NextOrder = Game.GameTime + (flag5 ? 1500 : 3000);
			bool flag8 = ped == null || ENTITY.GET_ENTITY_SPEED(ped.Handle) < (item3.Mounted ? 6f : 3f);
			bool flag9 = flag5 && (item3.MovingTo.DistanceTo(item3.Alert) > 20f || (Game.GameTime - item3.MoveOrderAt > 8000 && flag8));
			if (flag9)
			{
				item3.MovingTo = item3.Alert;
				item3.MoveOrderAt = Game.GameTime;
				if (item3.HurryOrderAt == 0 && !item3.NavRide)
				{
					item3.HurryOrderAt = Game.GameTime;
				}
			}
			MountedCheck(item3, ped);
			bool flag10 = ped == null || ENTITY.GET_ENTITY_SPEED(ped.Handle) < 0.3f;
			bool flag11 = !flag5 && (item3.MovingTo.DistanceTo(item3.Target) > 20f || (Game.GameTime - item3.MoveOrderAt > 9000 && flag10));
			if (flag11)
			{
				item3.MovingTo = item3.Target;
				item3.MoveOrderAt = Game.GameTime;
			}
			bool flag12 = !item3.Hostile && !AfterYou(item3.Gang) && Game.GameTime - item3.AlertAt > 10000 && Game.GameTime >= item3.WatchYouUntil;
			if (!flag12 && Game.GameTime >= _nextTalkWhy && item3.Men.Any((Ped m) => m.Exists() && !m.IsDead && m.Position.DistanceTo(player.Position) < 25f))
			{
				_nextTalkWhy = Game.GameTime + 5000;
				List<string> list2 = new List<string>();
				if (item3.Hostile)
				{
					list2.Add("you hurt one of them / fired in front of them");
				}
				if (AfterYou(item3.Gang))
				{
					list2.Add(item3.Gang.NeedsReason ? $"they have a reason (notoriety {NotorietyOf(item3.Gang)}, bounty ${BountyDollars})" : $"notoriety {NotorietyOf(item3.Gang)}");
				}
				if (Game.GameTime - item3.AlertAt <= 10000)
				{
					list2.Add($"shooting {(Game.GameTime - item3.AlertAt) / 1000}s ago ({item3.AlertWhat})");
				}
				if (Game.GameTime < item3.WatchYouUntil)
				{
					list2.Add($"covering you {(item3.WatchYouUntil - Game.GameTime) / 1000}s more");
				}
				GrudgeLog("PATROL " + item3.Gang.Id + ": not open to talk - " + string.Join("; ", list2));
			}
			foreach (Ped item5 in item3.Men.Where((Ped m) => m.Exists() && !m.IsDead))
			{
				if (item3.RidingOff && !flag5)
				{
					PatrolTalkable(item3, item5, player, calm: false);
					RideOffTogether(item3, item5, ped, flag11);
				}
				else
				{
					if (PatrolTalkable(item3, item5, player, flag12) || (!NerveOut(item5.Handle) && GetDownToFight(item3, item5, player)))
					{
						continue;
					}
					if (item3.Hostile && !NerveOut(item5.Handle) && item5.Position.DistanceTo(player.Position) < 120f && PED.IS_PED_IN_COMBAT(item5.Handle, 0) && !PED.IS_PED_IN_COMBAT(item5.Handle, player.Handle) && Game.GameTime >= NextRetarget(item5.Handle))
					{
						_retargetAt[item5.Handle] = Game.GameTime + 4000;
						PED.REGISTER_HATED_TARGETS_AROUND_PED(item5.Handle, 120f);
						if (item5.Position.DistanceTo(player.Position) < 60f)
						{
							TASK.TASK_COMBAT_PED(item5.Handle, player.Handle, 0, 16);
						}
					}
					else
					{
						if (PED.IS_PED_IN_COMBAT(item5.Handle, 0) || NerveOut(item5.Handle))
						{
							continue;
						}
						if (item3.Hostile && item5.Position.DistanceTo(player.Position) < 120f)
						{
							TASK.TASK_COMBAT_PED(item5.Handle, player.Handle, 0, 16);
						}
						else if (Game.GameTime < item3.WatchYouUntil && item5.Position.DistanceTo(player.Position) < 70f)
						{
							TASK.TASK_AIM_GUN_AT_ENTITY(item5.Handle, player.Handle, 2500, p3: false, 0);
						}
						else if (item3.CampId != null && item5.Position.DistanceTo2D(item3.Target) < 140f && (item3.Arrived || item5.Position.DistanceTo(item3.Target) >= 70f))
						{
							AssaultCamp(item3, item5, player);
						}
						else if (flag5)
						{
							if (item5.Position.DistanceTo(item3.Alert) < 50f)
							{
								if (Game.GameTime - item3.AlertAt < 10000)
								{
									TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(item5.Handle, 100f, 0, 0uL);
								}
							}
							else if (flag9 || (Stuck(item5) && !item3.Mounted))
							{
								MoveTo(item3, item5, item3.Alert, fast: true);
							}
						}
						else if (item3.CampId == null)
						{
							if (item3.Wandering)
							{
								continue;
							}
							float num5 = item5.Position.DistanceTo(item3.Target);
							if (num5 > 12f && flag11)
							{
								MoveTo(item3, item5, item3.Target, fast: false);
							}
							else
							{
								if (!(num5 > 12f) || !Stuck(item5))
								{
									continue;
								}
								if (Game.GameTime - item3.StuckAt > 8000)
								{
									item3.StuckAt = Game.GameTime;
									item3.StuckTries++;
									if (item3.StuckTries > 3)
									{
										item3.Wandering = true;
										foreach (Ped item6 in item3.Men.Where((Ped w) => w.Exists() && !w.IsDead && !PED.IS_PED_IN_COMBAT(w.Handle, 0)))
										{
											TASK.TASK_WANDER_STANDARD(item6.Handle, 10f, 10);
										}
										GrudgeLog("PATROL " + item3.Gang.Id + ": couldn't get on to its point - they wander off");
										continue;
									}
									Vector3 vector3 = item3.Target - item5.Position;
									vector3.Z = 0f;
									if (vector3.Length() > 1f)
									{
										vector3.Normalize();
									}
									Vector3 vector4 = SnapToRoad(item5.Position + vector3 * 70f, 80f);
									if (vector4 != Vector3.Zero)
									{
										item3.Target = vector4;
									}
									item3.MovingTo = Vector3.Zero;
									item3.MoveOrderAt = -100000;
									item3.NextOrder = 0;
									GrudgeLog($"PATROL {item3.Gang.Id}: standing still {num5:0} m from its point - a nearer spot on the road ({item5.Position.DistanceTo(item3.Target):0} m, try {item3.StuckTries})");
								}
								if (item3.StuckTries % 2 == 0)
								{
									TASK.TASK_GO_STRAIGHT_TO_COORD(item5.Handle, item3.Target.X, item3.Target.Y, item3.Target.Z, item3.Mounted ? 2f : 1f, -1, 40000f, 0.5f, 0);
								}
								else
								{
									TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item5.Handle, item3.Target.X, item3.Target.Y, item3.Target.Z, item3.Mounted ? 2f : 1f, -1, 6f, 0, 0f);
								}
							}
						}
						else if (item5.Position.DistanceTo(item3.Target) < 70f)
						{
							if (!item3.Arrived)
							{
								RaidArrived(item3);
							}
							TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(item5.Handle, 90f, 0, 0uL);
						}
						else if (flag11)
						{
							MoveTo(item3, item5, item3.Target, fast: false);
						}
					}
				}
			}
		}
	}

	private void LawfulPatrolsSee(string crime, int victim, Vector3 at)
	{
		if (!PatrolCrimes.Contains(crime) || _raids.Count == 0)
		{
			return;
		}
		Ped ped = Entity.FromHandle(victim) as Ped;
		if (ped == null || !ped.Exists() || !ped.IsHuman || Gangs.ForModel((uint)ped.Model.Hash) != null || RecordAt(victim)?.Gang != null || _combatants.ContainsKey(victim) || PED.IS_PED_IN_COMBAT(victim, Game.Player.Character.Handle))
		{
			return;
		}
		Ped player = Game.Player.Character;
		int num;
		switch (crime)
		{
		default:
			num = ((crime == "horsekill") ? 1 : 0);
			break;
		case "kill":
		case "gore":
		case "shot":
			num = 1;
			break;
		}
		bool loud = (byte)num != 0;
		foreach (Raid item in _raids.Where((Raid r) => r.Gang == Gangs.Army && !r.Hostile))
		{
			Ped ped2 = item.Men.FirstOrDefault((Ped m) => m.Exists() && !m.IsDead && m.Position.DistanceTo(player.Position) < (loud ? 90f : 35f) && ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(m.Handle, player.Handle, 17));
			if (ped2 == null)
			{
				continue;
			}
			item.Hostile = true;
			item.WatchYouUntil = 0;
			item.Watch = false;
			SetPatrolRelations(player);
			foreach (Ped item2 in item.Men.Where((Ped m) => m.Exists() && !m.IsDead))
			{
				if (_patrolOpen.Remove(item2.Handle))
				{
					item2.BlockPermanentEvents = true;
				}
				TASK.TASK_COMBAT_PED(item2.Handle, player.Handle, 0, 16);
			}
			GrudgeLog($"PATROL {item.Gang.Id}: saw you commit a crime ({crime}) from {ped2.Position.DistanceTo(player.Position):0} m - they're after you");
			Feed("PATROL " + item.Gang.Name + " saw that (" + crime + ") - they're coming for you", Theme.Blood);
			GrudgeNotice("The " + item.Gang.Name + " saw that.");
		}
	}

	private void AssaultCamp(Raid r, Ped m, Ped player)
	{
		int handle = m.Handle;
		if (r.Engaged.Contains(handle))
		{
			if (!PED.IS_PED_IN_COMBAT(handle, 0))
			{
				TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(handle, 90f, 0, 0uL);
			}
			return;
		}
		if (float.IsNaN(r.ApproachAngle))
		{
			Vector3 vector = (from x in r.Men
				where x.Exists()
				select x.Position).Aggregate(Vector3.Zero, (Vector3 a, Vector3 b) => a + b) / Math.Max(1, r.Men.Count((Ped x) => x.Exists()));
			r.ApproachAngle = (float)Math.Atan2(vector.Y - r.Target.Y, vector.X - r.Target.X);
		}
		if (!r.Spot.TryGetValue(handle, out var value))
		{
			int num = r.Men.IndexOf(m);
			int num2 = Math.Max(2, r.Men.Count);
			double num3 = (double)r.ApproachAngle + (-75.0 + 150.0 * (double)num / (double)(num2 - 1) + (double)_rng.Next(-12, 13)) * Math.PI / 180.0;
			float num4 = 26f + (float)_rng.NextDouble() * 18f;
			value = GroundOnSpot(new Vector3(r.Target.X + (float)Math.Cos(num3) * num4, r.Target.Y + (float)Math.Sin(num3) * num4, r.Target.Z));
			r.Spot[handle] = value;
			r.FanSince[handle] = Game.GameTime + _rng.Next(0, 3500);
		}
		if (Game.GameTime < r.FanSince[handle])
		{
			return;
		}
		float num5 = m.Position.DistanceTo2D(r.Target);
		float num6 = m.Position.DistanceTo2D(value);
		bool isOnMount = m.IsOnMount;
		if (isOnMount && num5 < 110f && !r.GotDown.Contains(handle) && handle % 4 != 0)
		{
			r.GotDown.Add(handle);
			TASK.TASK_DISMOUNT_ANIMAL(handle, 0, 0uL, 0uL, 0uL, 0);
		}
		else
		{
			if (isOnMount && r.GotDown.Contains(handle))
			{
				return;
			}
			LiveCamp value2;
			bool flag = _liveCamps.TryGetValue(r.CampId, out value2) && value2.Peds.Any((Ped cp) => cp.Exists() && !cp.IsDead && cp.Position.DistanceTo(m.Position) < 35f);
			int value3;
			if (num6 < 8f || flag || Game.GameTime - r.FanSince[handle] > 30000 || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ANY_PED(handle))
			{
				r.Engaged.Add(handle);
				PED.SET_PED_COMBAT_MOVEMENT(handle, (r.Men.IndexOf(m) % 2 != 0) ? 1 : 2);
				TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(handle, 90f, 0, 0uL);
			}
			else if (!r.SpotOrderAt.TryGetValue(handle, out value3) || Game.GameTime - value3 > 6000)
			{
				r.SpotOrderAt[handle] = Game.GameTime;
				if (isOnMount)
				{
					TASK.TASK_GO_TO_COORD_ANY_MEANS(handle, value.X, value.Y, value.Z, 6f, 0, p6: false, 786603, 0f);
				}
				else
				{
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(handle, value.X, value.Y, value.Z, 2.2f, -1, 3f, 0, 0f);
				}
			}
		}
	}

	private bool PatrolTalkable(Raid r, Ped m, Ped player, bool calm)
	{
		float num = m.Position.DistanceTo(player.Position);
		bool flag = _patrolOpen.Contains(m.Handle);
		bool flag2 = calm && num < (flag ? 35f : 20f) && !PED.IS_PED_IN_COMBAT(m.Handle, 0) && !NerveOut(m.Handle);
		if (calm && !flag2 && num < 20f && PED.IS_PED_IN_COMBAT(m.Handle, 0) && Game.GameTime >= _nextTalkWhy)
		{
			_nextTalkWhy = Game.GameTime + 5000;
			GrudgeLog($"PATROL {r.Gang.Id}: ped {m.Handle} {num:0} m from you is still in combat (the game's) - not open to talk");
		}
		if (flag2 && !flag)
		{
			_patrolOpen.Add(m.Handle);
			m.BlockPermanentEvents = false;
			PED.SET_PED_CONFIG_FLAG(m.Handle, 297, value: true);
			PED.SET_PED_CONFIG_FLAG(m.Handle, 301, value: false);
			PED.SET_PED_CONFIG_FLAG(m.Handle, 130, value: false);
			_patrolGroupWas[m.Handle] = ModGroupOf(m.Handle);
			m.RelationshipGroup = PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(m.Handle);
			ForgetSwitch(m.Handle);
			GrudgeLog($"PATROL {r.Gang.Id}: ped {m.Handle} is calm and {num:0} m from you - open to the game's prompts");
		}
		else if (!flag2 && flag)
		{
			_patrolOpen.Remove(m.Handle);
			m.BlockPermanentEvents = true;
			if (_patrolGroupWas.TryGetValue(m.Handle, out var value))
			{
				m.RelationshipGroup = value;
				_patrolGroupWas.Remove(m.Handle);
			}
		}
		return flag2;
	}

	private static bool FiresGun(Ped p)
	{
		if (p == null || !p.Exists() || p.IsDead || !PED.IS_PED_SHOOTING(p.Handle))
		{
			return false;
		}
		return GunMelee.IsGun(WEAPON._GET_PED_CURRENT_HELD_WEAPON(p.Handle));
	}

	private string ShooterLabel(Ped p)
	{
		switch (RoleOf(p.Handle))
		{
		case OurRole.Skirmish:
			return "a skirmish";
		case OurRole.Camp:
			return "a camp fight";
		case OurRole.Active:
		case OurRole.Crew:
		case OurRole.HiredGun:
			return "a gunfight";
		case OurRole.Town:
			return "a fight in town";
		default:
			return "shooting";
		}
	}

	private (Vector3? at, string what) Gunfire(Ped player)
	{
		if (_raids.Count == 0 && PatrolFaction == null)
		{
			return (at: null, what: "");
		}
		if (MISC.GET_MISSION_FLAG())
		{
			return (at: null, what: "");
		}
		if (FiresGun(player))
		{
			Hit(player.Position, "your shots");
		}
		else if (Game.GameTime >= _nextGunfireScan)
		{
			_nextGunfireScan = Game.GameTime + 100;
			foreach (Ped item in (from x in OurPeds()
				where x.role != OurRole.Patrol
				select x.p).Concat(PedsAround(player)))
			{
				if (!(item == null) && !item.IsPlayer && RoleOf(item.Handle) != OurRole.Patrol && FiresGun(item))
				{
					Hit(item.Position, ShooterLabel(item));
					break;
				}
			}
		}
		if (Game.GameTime - _lastGunfireAt >= 2000)
		{
			return (at: null, what: "");
		}
		return (at: _lastGunfire, what: _lastGunfireWhat);
		void Hit(Vector3 at, string what)
		{
			_lastGunfire = at;
			_lastGunfireAt = Game.GameTime;
			_lastGunfireWhat = what;
		}
	}

	private IEnumerable<(string, (int r, int g, int b))> PatrolLines(Ped player)
	{
		foreach (Raid raid in _raids)
		{
			Ped ped = raid.Men.FirstOrDefault((Ped m) => m.Exists() && !m.IsDead);
			if (!(ped == null))
			{
				int num = raid.Men.Count((Ped m) => m.Exists() && !m.IsDead);
				bool flag = Game.GameTime - raid.AlertAt < 45000;
				string text = ((raid.CampId != null) ? ("RAID on " + raid.CampId) : (raid.Watch ? "RIDERS ON A RIDGE" : (raid.Responding ? "PATROL (responding)" : "PATROL")));
				string text2 = (flag ? string.Format("heard {0} - {1} there, {2:0} m to go", raid.AlertWhat, raid.Mounted ? "riding" : "running", ped.Position.DistanceTo(raid.Alert)) : ((raid.CampId == null) ? (raid.Watch ? $"watching you, {Math.Max(0, (raid.WatchUntil - Game.GameTime) / 1000)} s more" : "on the road") : (raid.Arrived ? "fighting the camp" : $"marching, {ped.Position.DistanceTo(raid.Target):0} m to go")));
				bool flag2 = raid.Hostile || AfterYou(raid.Gang);
				yield return (string.Format("NOW {0} {1}: {2} {3} ({4}) - {5} - {6}{7}", raid.Gang.Name.ToUpperInvariant(), text, num, raid.Mounted ? "riders" : "men", raid.Hurry ? "fast" : "slow", Away(player.Position, ped.Position), text2, flag2 ? " - AFTER YOU" : ""), flag ? Theme.Warn : raid.Gang.Accent);
			}
		}
	}

	private void ChronicleRaid(Raid r)
	{
		if (!r.Arrived || r.Seen.Count == 0 || !r.Seen.Any(((Ped ped, NemesisRecord rec, string gang) s) => s.ped != null && s.ped.Exists()))
		{
			return;
		}
		List<WorldPart> parts = new List<WorldPart>();
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		foreach (var item4 in r.Seen)
		{
			Ped item = item4.ped;
			NemesisRecord item2 = item4.rec;
			string item3 = item4.gang;
			bool flag = item != null && item.Exists();
			bool flag2 = ((item2 == null) ? (flag && item.IsDead && !LooksKnockedOut(item)) : (!item2.IsAlive || item2.PresumedDead || (flag && item.IsDead && !LooksKnockedOut(item))));
			if (flag2)
			{
				dictionary[item3] = (dictionary.TryGetValue(item3, out var value) ? value : 0) + 1;
			}
			else if (flag)
			{
				dictionary2[item3] = (dictionary2.TryGetValue(item3, out var value2) ? value2 : 0) + 1;
			}
			if (item2 != null)
			{
				WorldPart worldPart = PartFor(item2, flag2 ? "KILLED" : ((flag && (float)item.Health < (float)item.MaxHealth * 0.6f) ? "WOUNDED" : "UNHURT"), item2.Title);
				worldPart.SecretlyLived = flag2 && item2.IsAlive;
				parts.Add(worldPart);
			}
		}
		object obj;
		if (r.CampId != null)
		{
			CampSite campSite = Camps.ById(r.CampId);
			if (campSite != null)
			{
				obj = SiteOwner(campSite) ?? Gangs.ODriscolls.Id;
				goto IL_01f7;
			}
		}
		obj = Gangs.ODriscolls.Id;
		goto IL_01f7;
		IL_01f7:
		string text = (string)obj;
		int value3;
		int num = (dictionary2.TryGetValue(r.Gang.Id, out value3) ? value3 : 0);
		int value4;
		int num2 = (dictionary2.TryGetValue(text, out value4) ? value4 : 0);
		List<string> list = new List<string>();
		list.Add((num2 == 0) ? "Nobody in the camp was left standing." : ((num == 0) ? ("The " + r.Gang.Name + " were beaten back.") : "Both sides pulled back."));
		list.Add($"{r.Gang.Name}: {(dictionary.TryGetValue(r.Gang.Id, out var value5) ? value5 : 0)} down.  {Gangs.ById(text)?.Name ?? text}: {(dictionary.TryGetValue(text, out var value6) ? value6 : 0)} down.");
		List<string> list2 = list;
		if (r.Seen.Any(((Ped ped, NemesisRecord rec, string gang) s) => s.ped != null && s.ped.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(s.ped.Handle, Game.Player.Character.Handle, p2: true, p3: true)))
		{
			list2.Add("You joined in.");
		}
		Dictionary<string, int> dictionary3 = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> kv in dictionary)
		{
			dictionary3[kv.Key] = kv.Value - r.Seen.Count(((Ped ped, NemesisRecord rec, string gang) s) => s.gang == kv.Key && s.rec != null && parts.Any((WorldPart p) => p.Id == s.rec.Id && p.Outcome == "KILLED"));
		}
		ChronicleWitness("The " + r.Gang.Name + " raided " + r.SiteLabel, "", r.SitePos, new string[2]
		{
			r.Gang.Id,
			text
		}, parts, dictionary3, list2);
		NoteLiveClash(r.Gang.Id, text, parts.Count((WorldPart p) => p.Outcome == "KILLED"), "at " + r.SiteLabel, r.SitePos);
	}

	private unsafe void EndRaid(Raid r)
	{
		_scenes.Remove(r);
		_nextSideCheck.Remove(r);
		try
		{
			ChronicleRaid(r);
		}
		catch (Exception ex)
		{
			GrudgeLog("WORLD raid chronicle failed: " + ex.Message);
		}
		if (r.Dot != null && r.Dot.Exists())
		{
			r.Dot.Delete();
		}
		foreach (Ped man in r.Men)
		{
			if (!(man == null) && man.Exists())
			{
				int handle = man.Handle;
				if (!man.IsDead && SafeToDelete(man) && man.Position.DistanceTo(Game.Player.Character.Position) > 200f)
				{
					_worldNemeses.Remove(handle);
					man.Delete();
				}
				else
				{
					ReleaseIfOurs(&handle);
				}
			}
		}
		foreach (Ped horse in r.Horses)
		{
			if (!(horse == null) && horse.Exists())
			{
				int handle2 = horse.Handle;
				ReleaseIfOurs(&handle2);
			}
		}
		_raids.Remove(r);
		GrudgeLog("PATROL " + r.Gang?.Id + " " + (r.CampId ?? "on the road") + " over");
	}
}
