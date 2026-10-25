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
	private sealed class Roadblock
	{
		public string Town;

		public int Entrance = -1;

		public Gang Gang;

		public Vector3 At;

		public float Road;

		public readonly List<Ped> Men = new List<Ped>();

		public readonly List<int> Props = new List<int>();

		public int Blip;

		public bool Spoke;

		public bool Hostile;

		public bool Spawner;

		public bool OnRoad;

		public string Tier = "warm";

		public bool Warned;

		public int WarnedAt;
	}

	private sealed class Walkers
	{
		public string Town;

		public Gang Gang;

		public readonly List<Ped> Men = new List<Ped>();

		public int NextMove;

		public bool Hostile;

		public bool Stared;
	}

	private readonly List<Roadblock> _roadblocks = new List<Roadblock>();

	private readonly List<Walkers> _walkers = new List<Walkers>();

	private readonly Dictionary<string, List<(Vector3 at, float road)>> _entrances = new Dictionary<string, List<(Vector3, float)>>();

	private uint _checkpointGroup;

	private int _nextCheckpoint;

	private int _nextWalkerSpawn;

	private int _nextRoadBlockRoll;

	private int _roadBlockAllowed;

	private const int MaxRoadblocks = 3;

	private const int MaxWalkerGroups = 2;

	private bool _forceRoadBlock;

	private static readonly Dictionary<string, string[]> CheckpointLines = new Dictionary<string, string[]>
	{
		[""] = new string[5] { "Hold it. Where you headed?", "Easy, mister. We're watching this road.", "Keep your hands where I can see 'em and move along.", "There's a war on. Don't make yourself part of it.", "Nobody rides through here without us seeing. Go on." },
		["odriscoll"] = new string[3] { "Colm's watching this town now. Mind yourself.", "Move along, friend - this is O'Driscoll business.", "Eyes front and keep riding." },
		["lemoyneraiders"] = new string[3] { "Lemoyne's ours. You just passing through?", "Move along - and don't go telling no Yankees what you seen.", "Keep riding, stranger." },
		["army"] = new string[3] { "Halt. State your business - never mind, move along.", "This town's under watch. Keep it peaceful.", "Move along, citizen." },
		["bronte"] = new string[2] { "Mr Bronte's men are minding the street. Be on your way.", "Nothing to see here. Keep walking." },
		["klan"] = new string[2] { "You'd do well to keep riding.", "Ain't no business of yours what goes on here." },
		["murfree"] = new string[2] { "Hehe. Keep walkin', pretty.", "You lost? Best get lost further." },
		["skinners"] = new string[2] { "Keep moving. We're hunting.", "Don't stop here." }
	};

	private static readonly string[] TruceLines = new string[3] { "Truce holds. Come on through.", "Evenin'. Quiet tonight, thank God.", "Go on through, friend. Nobody's shooting today." };

	private static readonly string[] TurnBackLines = new string[3] { "That's far enough. Turn around - we know who you are.", "Turn back. Your kind don't ride through here.", "Not another step. Go back the way you came." };

	private readonly Dictionary<string, int> _wayClearedAt = new Dictionary<string, int>();

	private float _checkpointRoad = float.NaN;

	private static readonly string[] BigCrateModels = new string[6] { "p_cratestack01x", "p_cratestack02x", "p_crate14x", "p_crate06bx", "p_crate03x", "p_crate01x" };

	private static readonly string[] BarrelModels = new string[5] { "p_barrel01x", "p_barrel02x", "p_barrel03x", "p_barrel05bx", "p_barrel_wood01x" };

	private static readonly string[] HayModels = new string[4] { "p_haybale01x", "p_haybale02x", "p_haybale03x", "p_haybalestack01x" };

	private readonly Dictionary<string, string> _propModel = new Dictionary<string, string>();

	private readonly HashSet<string> _propMissLogged = new HashSet<string>();

	private IEnumerable<Ped> CheckpointMen => _roadblocks.SelectMany((Roadblock r) => r.Men).Concat(_walkers.SelectMany((Walkers w) => w.Men));

	private Gang TownHolder(string town, out District d)
	{
		d = null;
		if (!TownCore(town, out var centre, out var _))
		{
			return null;
		}
		d = DistrictAt(centre);
		(Gang, Gang)? tuple = ((d == null) ? (((Gang, Gang)?)null) : WarIn(d));
		if (!tuple.HasValue)
		{
			return null;
		}
		District dd = d;
		return (from g in new Gang[2]
			{
				tuple.Value.Item1,
				tuple.Value.Item2
			}
			where g != null && !g.StoryOnly && !IsDestroyed(g) && FactionModels.ContainsKey(g.Id) && HoldOf(dd, g.Id) > 0
			orderby HoldOf(dd, g.Id) descending
			select g).FirstOrDefault();
	}

	private string TownNear(Vector3 me)
	{
		if (_currentTown != null)
		{
			return _currentTown;
		}
		foreach (string repTown in RepTowns)
		{
			if (TownCore(repTown, out var centre, out var radius) && new Vector3(me.X - centre.X, me.Y - centre.Y, 0f).Length() < radius * 1.8f)
			{
				return repTown;
			}
		}
		return null;
	}

	private List<(Vector3 at, float road)> EntrancesOf(string town)
	{
		if (_entrances.TryGetValue(town, out List<(Vector3, float)> value))
		{
			return value;
		}
		if (!TownCore(town, out var c, out var radius))
		{
			return new List<(Vector3, float)>();
		}
		List<(Vector3, float)> list = new List<(Vector3, float)>();
		for (int i = 0; i < 360; i += 6)
		{
			if (list.Count >= 6)
			{
				break;
			}
			double num = (double)i * Math.PI / 180.0;
			Vector3 at = new Vector3(c.X + (float)Math.Cos(num) * radius * 1.15f, c.Y + (float)Math.Sin(num) * radius * 1.15f, c.Z);
			float heading;
			Vector3? node = RoadNodeNear(at, 30f, out heading, 0);
			if (node.HasValue)
			{
				float num2 = new Vector3(node.Value.X - c.X, node.Value.Y - c.Y, 0f).Length();
				if (!(num2 < radius * 0.85f) && !(num2 > radius * 1.5f) && !list.Any<(Vector3, float)>(((Vector3 at, float road) e) => e.at.DistanceTo(node.Value) < 70f) && Dry(node.Value))
				{
					list.Add((node.Value, heading));
				}
			}
		}
		if (list.Count > 0)
		{
			_entrances[town] = list;
			GrudgeLog($"CHECKPOINT {town}: {list.Count} ways in - " + string.Join("; ", list.Select<(Vector3, float), string>(((Vector3 at, float road) e) => $"{e.at.X:0},{e.at.Y:0} ({CompassTo(c, e.at)})")));
		}
		return list;
	}

	private void UpdateCheckpoint(Ped player)
	{
		if (Game.GameTime < _nextCheckpoint)
		{
			return;
		}
		_nextCheckpoint = Game.GameTime + 1000;
		Vector3 me = player.Position;
		bool flag = MISC.GET_MISSION_FLAG() || player.IsDead;
		District d;
		foreach (Roadblock item in _roadblocks.ToList())
		{
			List<Ped> list = item.Men.Where((Ped p) => p.Exists() && !p.IsDead).ToList();
			float num = item.At.DistanceTo(me);
			int num2;
			if (!item.Spawner)
			{
				if (!item.OnRoad)
				{
					Gang gang = TownHolder(item.Town, out d);
					num2 = ((gang != null && gang == item.Gang) ? 1 : 0);
				}
				else
				{
					num2 = (RoadBlockHolds(item) ? 1 : 0);
				}
			}
			else
			{
				num2 = 1;
			}
			bool flag2 = (byte)num2 != 0;
			if (flag || list.Count == 0 || (item.Spawner && TownNear(me) != item.Town) || (!item.Hostile && (!flag2 || (num > 280f && (HiddenFromView(player, item.At) || num > 350f)))))
			{
				DropRoadblock(item, player, wipe: false);
			}
			else
			{
				RunRoadblock(item, player, list);
			}
		}
		foreach (Walkers item2 in _walkers.ToList())
		{
			List<Ped> list2 = item2.Men.Where((Ped p) => p.Exists() && !p.IsDead).ToList();
			Gang gang2 = TownHolder(item2.Town, out d);
			bool flag3 = gang2 != null && gang2 == item2.Gang;
			if (flag || list2.Count == 0 || (!item2.Hostile && (_currentTown != item2.Town || !flag3)))
			{
				DropWalkers(item2, player, wipe: false);
			}
			else
			{
				RunWalkers(item2, player, list2);
			}
		}
		if (flag || NoSpawnReason(player, place: true) != null || WorldBusy(player))
		{
			return;
		}
		string town = TownNear(me);
		if (town == null)
		{
			TryRoadBlock(player);
			return;
		}
		District d2;
		Gang gang3 = TownHolder(town, out d2);
		if (gang3 == null)
		{
			return;
		}
		string text = TownWarTier(town);
		int num3 = ((text == "cold") ? 1 : 3);
		int num4 = _roadblocks.Count((Roadblock r) => !r.Spawner);
		List<(Vector3 at, float road)> ways = EntrancesOf(town);
		foreach (int i in from k in Enumerable.Range(0, ways.Count)
			orderby ways[k].at.DistanceTo(me)
			select k)
		{
			if (num4 >= num3)
			{
				break;
			}
			(Vector3, float) tuple = ways[i];
			if (_roadblocks.Any((Roadblock r) => r.Town == town && r.Entrance == i) || (_wayClearedAt.TryGetValue(town + "|" + i, out var value) && Game.GameTime - value < 600000))
			{
				continue;
			}
			float num5 = tuple.Item1.DistanceTo(me);
			if (!(num5 > 200f) && !(num5 < 35f) && (!(num5 < 140f) || HiddenFromView(player, tuple.Item1 + new Vector3(0f, 0f, 1f))))
			{
				Roadblock roadblock = BuildRoadblock(player, gang3, tuple.Item1, tuple.Item2, town, i, spawner: false, text);
				if (roadblock != null)
				{
					num4++;
					GrudgeLog($"CHECKPOINT {town}: the {gang3.Name} hold the way in {CompassTo(TownCoreCentre(town), tuple.Item1)} ({roadblock.Men.Count} men, {text}, {num5:0} m from you) - the war in {d2?.Name}");
				}
				break;
			}
		}
		int num6 = ((text == "cold" || text == "truce") ? 1 : 2);
		if (_currentTown == town && Game.GameTime >= _nextWalkerSpawn && _walkers.Count((Walkers w) => w.Town == town) < num6)
		{
			_nextWalkerSpawn = Game.GameTime + 20000;
			if (text == "cold" && _rng.Next(2) == 0)
			{
				_nextWalkerSpawn = Game.GameTime + 120000;
			}
			else
			{
				SpawnWalkers(player, gang3, town);
			}
		}
	}

	private Vector3 TownCoreCentre(string town)
	{
		if (!TownCore(town, out var centre, out var _))
		{
			return Vector3.Zero;
		}
		return centre;
	}

	private bool RoadBlockHolds(Roadblock rb)
	{
		District d = DistrictAt(rb.At);
		WarRecord warRecord = ((d == null) ? null : Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key));
		if (warRecord == null || warRecord.Paused)
		{
			return false;
		}
		InitHeat(warRecord);
		if (warRecord.Heat >= 45)
		{
			if (!(warRecord.A == rb.Gang.Id))
			{
				return warRecord.B == rb.Gang.Id;
			}
			return true;
		}
		return false;
	}

	internal void DebugRoadBlock(Ped player)
	{
		if (TownNear(player.Position) != null)
		{
			SpawnerSays("ride out of town first - this is a roadblock out on the roads.");
			return;
		}
		if (!WarHere.HasValue)
		{
			SpawnerSays("no war in this district - use 'Start a war here' first.");
			return;
		}
		_forceRoadBlock = true;
		_nextRoadBlockRoll = 0;
		_roadBlockAllowed = 0;
		foreach (Roadblock item in _roadblocks.Where((Roadblock r) => r.OnRoad).ToList())
		{
			DropRoadblock(item, player, wipe: false);
		}
		SpawnerSays("a roadblock goes up on the road ahead (out of your view).");
	}

	private void TryRoadBlock(Ped player)
	{
		if (Game.GameTime < _nextRoadBlockRoll)
		{
			return;
		}
		_nextRoadBlockRoll = Game.GameTime + (_forceRoadBlock ? 3000 : 120000);
		if (_roadblocks.Any((Roadblock r) => r.OnRoad) || Game.GameTime < _roadBlockAllowed || player.IsDead)
		{
			return;
		}
		District d = CurrentDistrict;
		WarRecord warRecord = ((d == null) ? null : Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key));
		if (warRecord == null || warRecord.Paused)
		{
			return;
		}
		InitHeat(warRecord);
		if (warRecord.Heat < 60 && !_forceRoadBlock)
		{
			return;
		}
		int num = _rng.Next(100);
		if (num >= 30 && !_forceRoadBlock)
		{
			GrudgeLog($"CHECKPOINT road roll in {d.Name} (heat {warRecord.Heat:0}): 30%, rolled {num} - not now");
			return;
		}
		Gang gang = Gangs.ById(warRecord.A);
		Gang gang2 = Gangs.ById(warRecord.B);
		List<Gang> list = (from x in new Gang[2] { gang, gang2 }
			where x != null && FactionModels.ContainsKey(x.Id) && FieldCap(x) >= 3
			orderby HoldOf(d, x.Id) descending
			select x).ToList();
		if (list.Count == 0)
		{
			return;
		}
		Gang gang3 = list[0];
		Vector3 position = player.Position;
		double num2 = Math.Atan2(Forward(player).Y, Forward(player).X);
		for (int num3 = 0; num3 < 24; num3++)
		{
			double num4 = num2 + (_rng.NextDouble() - 0.5) * Math.PI * ((num3 < 16) ? 0.5 : 1.2);
			float num5 = 140f + (float)_rng.NextDouble() * 80f;
			float heading;
			Vector3? vector = RoadNodeNear(new Vector3(position.X + (float)Math.Cos(num4) * num5, position.Y + (float)Math.Sin(num4) * num5, position.Z), 30f, out heading, (num3 >= 16) ? 1 : 0);
			if (!vector.HasValue)
			{
				continue;
			}
			Vector3 vector2 = Ground(vector.Value);
			if (Math.Abs(vector2.Z - vector.Value.Z) > 3f || !Dry(vector.Value) || InsideAnyTown(vector.Value))
			{
				continue;
			}
			float num6 = vector.Value.DistanceTo(position);
			if (!(num6 < 120f) && !(num6 > 240f) && HiddenFromView(player, vector.Value + new Vector3(0f, 0f, 1.5f)))
			{
				Vector3 vector3 = new Vector3(vector.Value.X, vector.Value.Y, vector2.Z);
				Roadblock roadblock = BuildRoadblock(player, gang3, vector3, heading, d.Name, -1, spawner: false, "hot", onRoad: true);
				if (roadblock != null)
				{
					_roadBlockAllowed = Game.GameTime + 480000;
					_forceRoadBlock = false;
					GrudgeLog($"CHECKPOINT road: the {gang3.Name} hold the road in {d.Name} ({roadblock.Men.Count} men, heat {warRecord.Heat:0}) - {num6:0} m {CompassTo(position, vector3)} of you");
				}
				return;
			}
		}
		GrudgeLog("CHECKPOINT road: no road out of your view 140-220 m ahead in " + d.Name);
	}

	private string TownWarTier(string town)
	{
		District d = DistrictAt(TownCoreCentre(town));
		WarRecord warRecord = ((d == null) ? null : Wars.FirstOrDefault((WarRecord x) => x.Result == null && x.District == d.Key));
		if (warRecord == null)
		{
			return "warm";
		}
		if (warRecord.Paused)
		{
			return "truce";
		}
		InitHeat(warRecord);
		if (warRecord.Heat >= 30)
		{
			if (warRecord.Heat >= 60)
			{
				return "hot";
			}
			return "warm";
		}
		return "cold";
	}

	private void EnsureCheckpointGroup(Ped player)
	{
		if (_checkpointGroup == 0)
		{
			_checkpointGroup = World.AddRelationshipGroup("NEMESIS_CHECKPOINT");
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _checkpointGroup, player.RelationshipGroup);
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, player.RelationshipGroup, _checkpointGroup);
		_nextRelations = 0;
	}

	private Ped CheckpointMan(Gang g, Vector3 pos, float heading, string scenario, bool rifle = false)
	{
		PedHash[] array = FactionModels[g.Id];
		Crumbs.Native("checkpoint man " + g.Id);
		Ped ped = SpawnPed(array[_rng.Next(array.Length)], pos, heading);
		if (ped == null)
		{
			return null;
		}
		MakeOurs(ped.Handle);
		WEAPON.REMOVE_ALL_PED_WEAPONS(ped.Handle, p1: true, p2: true);
		eWeapon eWeapon = (rifle ? eWeapon.RifleBoltAction : (((g.TroopLongGuns || _rng.Next(3) > 0) && !ShortOfArms(g)) ? ((_rng.Next(2) == 0) ? eWeapon.RepeaterCarbine : eWeapon.RepeaterWinchester) : eWeapon.RevolverCattleman));
		if (g.TribalKit && !rifle)
		{
			ArmTroop(ped, g);
		}
		else
		{
			ped.GiveWeapon(eWeapon, 120, (eWeapon == eWeapon.RevolverCattleman) ? eWeaponAttachPoint.PistolR : eWeaponAttachPoint.Rifle, bForceInHand: true);
		}
		ped.RelationshipGroup = GroupFor(g);
		ped.BlockPermanentEvents = false;
		ApplyFactionStyle(ped, g.Id);
		ENTITY.SET_ENTITY_HEADING(ped.Handle, heading);
		if (scenario == "ARMED")
		{
			try
			{
				WEAPON.SET_CURRENT_PED_WEAPON(ped.Handle, (uint)eWeapon, equipNow: true, 0, p4: false, p5: false);
			}
			catch
			{
			}
			TASK.TASK_STAND_STILL(ped.Handle, -1);
		}
		else if (scenario != null)
		{
			TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(scenario), -1, playEnterAnim: true, 0u, -1f, p6: false);
		}
		return ped;
	}

	private Roadblock BuildRoadblock(Ped player, Gang g, Vector3 at, float roadHeading, string town, int entrance, bool spawner = false, string tier = null, bool onRoad = false)
	{
		EnsureCheckpointGroup(player);
		tier = tier ?? TownWarTier(town);
		Roadblock rb = new Roadblock
		{
			Town = town,
			Entrance = entrance,
			Gang = g,
			At = at,
			Road = roadHeading,
			Spawner = spawner,
			Tier = tier,
			OnRoad = onRoad
		};
		double num = (double)roadHeading * Math.PI / 180.0;
		Vector3 side = new Vector3((float)Math.Cos(num), (float)Math.Sin(num), 0f);
		Vector3 vector = new Vector3(0f - (float)Math.Sin(num), (float)Math.Cos(num), 0f);
		Vector3 vector2 = player.Position - at;
		vector2.Z = 0f;
		Vector3 vector3 = at - TownCoreCentre(town);
		vector3.Z = 0f;
		Vector3 vector4 = ((spawner || onRoad || vector3.Length() < 1f) ? vector2 : vector3);
		Vector3 front = vector * ((vector.X * vector4.X + vector.Y * vector4.Y >= 0f) ? 1f : (-1f));
		for (int i = 0; i < 2; i++)
		{
			float num2 = ((i == 0) ? 1f : (-1f));
			Prop(BigCrateModels, "crate", Shoulder(5.5f * num2, 0f), roadHeading + (float)_rng.Next(-10, 11));
			Prop(BarrelModels, "barrel", Shoulder(6.3f * num2, 1.3f), _rng.Next(360));
			if (i == 0)
			{
				Prop(HayModels, "hay", Shoulder(7.2f * num2, -1f), roadHeading + 90f);
			}
		}
		Vector3? vector5 = ((tier == "hot") ? ((Vector3?)null) : Shoulder(9.5f, -3.5f));
		if (vector5.HasValue)
		{
			int num3 = MakeFire(vector5.Value);
			if (num3 != 0)
			{
				rb.Props.Add(num3);
			}
		}
		int num4 = ((tier == "cold") ? (2 + _rng.Next(2)) : ((tier == "hot") ? (4 + _rng.Next(2)) : ((tier == "truce") ? 3 : (3 + _rng.Next(2)))));
		float num5 = HeadingTo(at, at + front * 10f);
		string[] array = Stances();
		for (int j = 0; j < num4; j++)
		{
			string text = array[Math.Min(j, array.Length - 1)];
			bool flag = text.StartsWith("WORLD_HUMAN_FIRE") && vector5.HasValue;
			Vector3? vector6 = (flag ? new Vector3?(vector5.Value - side * 1.3f + front * ((float)_rng.NextDouble() - 0.5f)) : (j switch
			{
				4 => Shoulder(6f, -2.6f), 
				3 => Shoulder(-7.6f, -2.8f), 
				1 => Shoulder(-6.7f, -0.7f), 
				0 => Shoulder(6.7f, -0.7f), 
				_ => Shoulder(7.6f, -2.8f), 
			}));
			if (!vector6.HasValue)
			{
				vector6 = Shoulder((j % 2 == 0) ? 6.5f : (-6.5f), -2.5f);
			}
			if (!vector6.HasValue)
			{
				continue;
			}
			if (text.StartsWith("WORLD_HUMAN_FIRE") && !vector5.HasValue)
			{
				text = ((tier == "cold" || tier == "truce") ? "WORLD_HUMAN_SMOKE" : "ARMED");
			}
			float heading = (flag ? HeadingTo(vector6.Value, vector5.Value) : ((j == 3) ? faceLane(vector6.Value) : (num5 + (float)_rng.Next(-30, 31))));
			bool rifle = tier == "hot" && j == 0;
			Ped ped = CheckpointMan(g, vector6.Value, heading, text, rifle);
			if (!(ped != null))
			{
				continue;
			}
			if (tier == "truce" || tier == "cold")
			{
				try
				{
					WEAPON.SET_CURRENT_PED_WEAPON(ped.Handle, 2725352035u, equipNow: true, 0, p4: false, p5: false);
				}
				catch
				{
				}
			}
			rb.Men.Add(ped);
		}
		if (rb.Men.Count == 0)
		{
			DropProps(rb.Props, player, wipe: true);
			GrudgeLog("CHECKPOINT " + town + ": nobody spawned");
			return null;
		}
		if (_debugOn)
		{
			try
			{
				Crumbs.Native("checkpoint debug blip");
				rb.Blip = MAP.BLIP_ADD_FOR_COORDS(2545348716u, at.X, at.Y, at.Z);
				Function.Call(11290983183885202434uL, rb.Blip, new InputArgument(UiDraw.ForeverVarString(MemberWord(g) + " roadblock (debug)")));
			}
			catch (Exception ex)
			{
				GrudgeLog("CHECKPOINT debug blip failed: " + ex.Message);
			}
		}
		_roadblocks.Add(rb);
		return rb;
		void Prop(string[] list, string key, Vector3? v, float hd)
		{
			if (v.HasValue)
			{
				_propModel.TryGetValue(key, out var value);
				int num6 = MakeCheckpointProp(list, v.Value, hd, ref value);
				if (value != null)
				{
					_propModel[key] = value;
				}
				if (num6 != 0)
				{
					rb.Props.Add(num6);
				}
			}
		}
		Vector3? Shoulder(float lat, float fwd)
		{
			float num6 = Math.Sign(lat);
			float num7 = Math.Abs(lat);
			for (float num8 = 0f; num8 <= 3f; num8 += 1f)
			{
				Vector3 vector7 = GroundOnSpot(at + side * (num6 * (num7 + num8)) + front * fwd);
				if (vector7 != Vector3.Zero && Math.Abs(vector7.Z - at.Z) <= 1.2f && Dry(vector7))
				{
					return vector7;
				}
			}
			return null;
		}
		string[] Stances()
		{
			return tier switch
			{
				"cold" => new string[3] { "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_FIRE_SIT" }, 
				"truce" => new string[3] { "WORLD_HUMAN_FIRE_SIT", "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_FIRE_STAND" }, 
				"hot" => new string[5] { "ARMED", "ARMED", "ARMED", "ARMED", "WORLD_HUMAN_GUARD_SCOUT" }, 
				_ => new string[4]
				{
					"ARMED",
					(_rng.Next(2) == 0) ? "WORLD_HUMAN_GUARD_SCOUT" : "ARMED",
					"WORLD_HUMAN_FIRE_STAND",
					(_rng.Next(2) == 0) ? "WORLD_HUMAN_SMOKE" : "ARMED"
				}, 
			};
		}
		float faceLane(Vector3 p)
		{
			return HeadingTo(p, at);
		}
	}

	private void RunRoadblock(Roadblock rb, Ped player, List<Ped> alive)
	{
		Vector3 me = player.Position;
		if (!rb.Hostile && rb.Men.Any((Ped p) => p.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true)))
		{
			GroupFight(rb.Men, player, ref rb.Hostile, "CHECKPOINT " + rb.Town + ": a fight at the roadblock - you hurt one of them");
		}
		if (!rb.Hostile && rb.Men.Any((Ped p) => p.Exists() && !p.IsDead && PED.IS_PED_IN_COMBAT(p.Handle, player.Handle)))
		{
			GroupFight(rb.Men, player, ref rb.Hostile, "CHECKPOINT " + rb.Town + ": the roadblock opened fire - their outfit is after you");
		}
		if (rb.Hostile)
		{
			return;
		}
		if (rb.Warned)
		{
			if (Game.GameTime - rb.WarnedAt > 5000 && me.DistanceTo(rb.At) < 6f)
			{
				ShowSubtitle(rb.Gang.Name + " man: I said turn back!");
				GroupFight(rb.Men, player, ref rb.Hostile, "CHECKPOINT " + rb.Town + ": you rode through after they turned you back");
			}
			else if (me.DistanceTo(rb.At) > 40f)
			{
				rb.Warned = false;
			}
		}
		else
		{
			if (rb.Spoke)
			{
				return;
			}
			Ped ped = alive.OrderBy((Ped p) => p.Position.DistanceTo(me)).First();
			if (ped.Position.DistanceTo(me) > ((rb.Tier == "hot") ? 14f : 10f))
			{
				return;
			}
			rb.Spoke = true;
			if (_raids.Any((Raid r) => r.Gang == rb.Gang && r.Hostile))
			{
				ShowSubtitle(rb.Gang.Name + " man: That's him! That's the one!");
				GroupFight(rb.Men, player, ref rb.Hostile, "CHECKPOINT " + rb.Town + ": a fight at the roadblock - a patrol of theirs is after you");
			}
			else if (rb.Tier == "cold")
			{
				foreach (Ped item in alive)
				{
					TASK.TASK_LOOK_AT_ENTITY(item.Handle, player.Handle, 4000, 0, 51, 0);
				}
				GrudgeLog("CHECKPOINT " + rb.Town + ": the " + rb.Gang.Name + " watched you by (cold war)");
			}
			else if (rb.Tier == "truce")
			{
				TASK.TASK_LOOK_AT_ENTITY(ped.Handle, player.Handle, 5000, 0, 51, 0);
				ShowSubtitle(rb.Gang.Name + " man: " + TruceLines[_rng.Next(TruceLines.Length)]);
				GrudgeLog("CHECKPOINT " + rb.Town + ": the " + rb.Gang.Name + " waved you through (truce)");
			}
			else if (rb.Tier == "hot" && !AfterYou(rb.Gang) && NotorietyOf(rb.Gang) >= 20)
			{
				TASK.TASK_AIM_GUN_AT_ENTITY(ped.Handle, player.Handle, 5000, p3: false, 0);
				ShowSubtitle(rb.Gang.Name + " man: " + TurnBackLines[_rng.Next(TurnBackLines.Length)]);
				rb.Warned = true;
				rb.WarnedAt = Game.GameTime;
				GrudgeLog($"CHECKPOINT {rb.Town}: the {rb.Gang.Name} turned you back (hot war, notoriety {NotorietyOf(rb.Gang)})");
			}
			else if (AfterYou(rb.Gang))
			{
				ShowSubtitle(rb.Gang.Name + " man: That's him! That's the one!");
				GroupFight(rb.Men, player, ref rb.Hostile, "CHECKPOINT " + rb.Town + ": a fight at the roadblock - they know your face");
			}
			else
			{
				TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, player.Handle, 3000, 0f, 0f, 0f);
				TASK.TASK_LOOK_AT_ENTITY(ped.Handle, player.Handle, 6000, 0, 51, 0);
				string[] value;
				string[] array = (CheckpointLines.TryGetValue(rb.Gang.Id, out value) ? value : new string[0]).Concat(CheckpointLines[""]).ToArray();
				ShowSubtitle(rb.Gang.Name + " man: " + array[_rng.Next(array.Length)]);
				GrudgeLog("CHECKPOINT " + rb.Town + ": the " + rb.Gang.Name + " stopped you and let you by");
			}
		}
	}

	private void GroupFight(List<Ped> men, Ped player, ref bool hostile, string log)
	{
		hostile = true;
		foreach (Ped item in men.Where((Ped p) => p.Exists() && !p.IsDead))
		{
			item.BlockPermanentEvents = false;
			item.RelationshipGroup = PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(item.Handle);
			ForgetSwitch(item.Handle);
			TASK.CLEAR_PED_TASKS(item.Handle, p1: true, p2: false);
			TASK.TASK_COMBAT_PED(item.Handle, player.Handle, 0, 16);
		}
		GrudgeLog(log);
	}

	private unsafe void DropRoadblock(Roadblock rb, Ped player, bool wipe)
	{
		int num = rb.Men.Count((Ped p) => p.Exists() && p.IsDead);
		if (num > 0 && !rb.Men.Any((Ped p) => p.Exists() && !p.IsDead))
		{
			_wayClearedAt[rb.Town + "|" + rb.Entrance] = Game.GameTime;
			if (rb.Men.Count((Ped p) => p.Exists() && p.IsDead && PED.GET_PED_SOURCE_OF_DEATH(p.Handle) == player.Handle) > 0 && rb.Gang != null)
			{
				try
				{
					string text = (rb.OnRoad ? "roadblock on the road" : ("checkpoint" + ((rb.Town != null) ? (" outside " + rb.Town) : "")));
					ChronicleDeed("roadblock", "You cleared the " + OutfitPoss(rb.Gang) + " " + text, (rb.OnRoad || rb.Town == null) ? ("near " + GetZoneName(rb.At)) : ("by " + rb.Town), rb.At, rb.Gang.Id);
				}
				catch
				{
				}
			}
		}
		foreach (Ped item in rb.Men.ToList())
		{
			if (!(item == null) && item.Exists())
			{
				if (wipe)
				{
					Wipe(item);
				}
				else
				{
					DeleteOrRelease(item);
				}
			}
		}
		GrudgeLog($"CHECKPOINT {rb.Town}: roadblock down ({num} dead)");
		rb.Men.Clear();
		if (rb.Blip != 0)
		{
			int blip = rb.Blip;
			rb.Blip = 0;
			try
			{
				MAP.REMOVE_BLIP(&blip);
			}
			catch
			{
			}
		}
		DropProps(rb.Props, player, wipe);
		_roadblocks.Remove(rb);
	}

	private void SpawnWalkers(Ped player, Gang g, string town)
	{
		if (!TownCore(town, out var centre, out var radius))
		{
			return;
		}
		EnsureCheckpointGroup(player);
		Vector3? vector = null;
		for (int i = 0; i < 20; i++)
		{
			if (vector.HasValue)
			{
				break;
			}
			double num = _rng.NextDouble() * Math.PI * 2.0;
			float num2 = radius * (0.2f + (float)_rng.NextDouble() * 0.6f);
			float heading;
			Vector3? vector2 = RoadNodeNear(new Vector3(centre.X + (float)Math.Cos(num) * num2, centre.Y + (float)Math.Sin(num) * num2, centre.Z), 25f, out heading);
			if (!vector2.HasValue)
			{
				continue;
			}
			double num3 = (double)heading * Math.PI / 180.0;
			Vector3 vector3 = GroundOnSpot(vector2.Value + new Vector3((float)Math.Cos(num3), (float)Math.Sin(num3), 0f) * 4.5f);
			if (!(vector3 == Vector3.Zero) && Dry(vector3))
			{
				float num4 = vector3.DistanceTo(player.Position);
				if (!(num4 < 40f) && !(num4 > 150f) && HiddenFromView(player, vector3 + new Vector3(0f, 0f, 1f)) && !OurPeopleNear(vector3, 12f))
				{
					vector = vector3;
				}
			}
		}
		if (!vector.HasValue)
		{
			return;
		}
		Walkers walkers = new Walkers
		{
			Town = town,
			Gang = g,
			NextMove = 0
		};
		int num5 = 2 + _rng.Next(2);
		for (int j = 0; j < num5; j++)
		{
			Ped ped = CheckpointMan(g, vector.Value + new Vector3((float)j * 1.2f, (float)(-j) * 0.8f, 0f), _rng.Next(360), null);
			if (ped != null)
			{
				walkers.Men.Add(ped);
			}
		}
		if (walkers.Men.Count != 0)
		{
			_walkers.Add(walkers);
			GrudgeLog($"CHECKPOINT {town}: {walkers.Men.Count} {g.Name} men walk the streets ({vector.Value.DistanceTo(player.Position):0} m from you)");
		}
	}

	private void RunWalkers(Walkers w, Ped player, List<Ped> alive)
	{
		Vector3 me = player.Position;
		if (!w.Hostile && w.Men.Any((Ped p) => p.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true)))
		{
			GroupFight(w.Men, player, ref w.Hostile, "CHECKPOINT " + w.Town + ": a fight with the men walking the street - you hurt one of them");
		}
		if (w.Hostile)
		{
			return;
		}
		Ped ped = alive[0];
		float num = alive.Min((Ped p) => p.Position.DistanceTo(me));
		float num2 = ((TownWarTier(w.Town) == "hot") ? 22f : 12f);
		if (num < num2 && !w.Stared)
		{
			w.Stared = true;
			if (AfterYou(w.Gang))
			{
				ShowSubtitle(w.Gang.Name + " man: Hey - that's him!");
				GroupFight(w.Men, player, ref w.Hostile, "CHECKPOINT " + w.Town + ": the men walking the street know your face");
				return;
			}
			foreach (Ped item in alive)
			{
				TASK.TASK_LOOK_AT_ENTITY(item.Handle, player.Handle, 5000, 0, 51, 0);
			}
		}
		else if (num > 25f)
		{
			w.Stared = false;
		}
		if (Game.GameTime < w.NextMove)
		{
			return;
		}
		w.NextMove = Game.GameTime + 25000 + _rng.Next(20000);
		if (!TownCore(w.Town, out var centre, out var radius))
		{
			return;
		}
		for (int num3 = 0; num3 < 10; num3++)
		{
			double num4 = _rng.NextDouble() * Math.PI * 2.0;
			float num5 = radius * (0.1f + (float)_rng.NextDouble() * 0.7f);
			float heading;
			Vector3? vector = RoadNodeNear(new Vector3(centre.X + (float)Math.Cos(num4) * num5, centre.Y + (float)Math.Sin(num4) * num5, centre.Z), 25f, out heading);
			if (!vector.HasValue)
			{
				continue;
			}
			double num6 = (double)heading * Math.PI / 180.0;
			Vector3 vector2 = vector.Value + new Vector3((float)Math.Cos(num6), (float)Math.Sin(num6), 0f) * 4.5f;
			if (!(vector2.DistanceTo(ped.Position) < 30f))
			{
				Crumbs.Native("walkers move");
				TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ped.Handle, vector2.X, vector2.Y, vector2.Z, 1f, -1, 1.5f, 0, 0f);
				for (int num7 = 1; num7 < alive.Count; num7++)
				{
					TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(alive[num7].Handle, ped.Handle, (num7 % 2 == 0) ? 1.2f : (-1.2f), -1.5f * (float)num7, 0f, 1f, -1, 1.5f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
				}
				break;
			}
		}
	}

	private void DropWalkers(Walkers w, Ped player, bool wipe)
	{
		foreach (Ped item in w.Men.ToList())
		{
			if (!(item == null) && item.Exists())
			{
				if (wipe)
				{
					Wipe(item);
				}
				else
				{
					DeleteOrRelease(item);
				}
			}
		}
		w.Men.Clear();
		_walkers.Remove(w);
	}

	private void EndCheckpoint(Ped player, bool wipe)
	{
		foreach (Roadblock item in _roadblocks.ToList())
		{
			DropRoadblock(item, player, wipe);
		}
		foreach (Walkers item2 in _walkers.ToList())
		{
			DropWalkers(item2, player, wipe);
		}
	}

	private unsafe void DropProps(List<int> props, Ped player, bool wipe)
	{
		foreach (int prop in props)
		{
			int current = prop;
			if (ENTITY.DOES_ENTITY_EXIST(current))
			{
				Vector3 vector = ENTITY.GET_ENTITY_COORDS(current, alive: false, realCoords: false);
				Crumbs.Native("checkpoint prop drop");
				if (wipe || player == null || !player.Exists() || player.Position.DistanceTo(vector) > 80f || HiddenFromView(player, vector))
				{
					OBJECT.DELETE_OBJECT(&current);
				}
				else
				{
					ENTITY.SET_ENTITY_AS_NO_LONGER_NEEDED(&current);
				}
			}
		}
		props.Clear();
	}

	private Vector3? CheckpointSpot(Ped player, string town, bool mustHide, out string why)
	{
		Vector3 position = player.Position;
		Vector3 centre;
		float radius;
		bool flag = TownCore(town, out centre, out radius);
		double num = Math.Atan2(Forward(player).Y, Forward(player).X);
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		for (int i = 0; i < 36; i++)
		{
			double num8 = num + (_rng.NextDouble() - 0.5) * Math.PI * ((i < 24) ? 1.2 : 2.0);
			float num9 = 40f + (float)_rng.NextDouble() * 60f;
			Vector3 at = new Vector3(position.X + (float)Math.Cos(num8) * num9, position.Y + (float)Math.Sin(num8) * num9, position.Z);
			float heading;
			Vector3? vector = RoadNodeNear(at, 30f, out heading, (i >= 24) ? 1 : 0);
			if (!vector.HasValue)
			{
				num2++;
				continue;
			}
			Vector3 vector2 = GroundOnSpot(vector.Value);
			if (vector2 == Vector3.Zero)
			{
				vector2 = vector.Value;
			}
			if (flag && new Vector3(vector2.X - centre.X, vector2.Y - centre.Y, 0f).Length() > radius * 1.35f)
			{
				num3++;
				continue;
			}
			float num10 = vector2.DistanceTo(position);
			if (num10 < 30f || num10 > 115f)
			{
				num4++;
				continue;
			}
			if (!Dry(vector2))
			{
				num5++;
				continue;
			}
			if (OurPeopleNear(vector2, 10f))
			{
				num6++;
				continue;
			}
			if (mustHide && !HiddenFromView(player, vector2 + new Vector3(0f, 0f, 1f)))
			{
				num7++;
				continue;
			}
			why = null;
			_checkpointRoad = heading;
			return vector2;
		}
		why = $"36 tries: no road {num2}, out of town {num3}, too near / far {num4}, water {num5}, our people {num6}, in your view {num7}";
		return null;
	}

	private int MakeCheckpointProp(string[] models, Vector3 at, float heading, ref string cache, bool freeze = true)
	{
		string[] array = ((cache == null) ? models : new string[1] { cache });
		foreach (string text in array)
		{
			Model m = new Model(text);
			if (!m.IsValid || !m.IsInCdImage || !LoadModelSafe(m, 1500))
			{
				continue;
			}
			Crumbs.Native("checkpoint prop");
			int num = OBJECT.CREATE_OBJECT((uint)m.Hash, at.X, at.Y, at.Z, isNetwork: false, bScriptHostObj: false, dynamic: false, p7: false, p8: true);
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
			if (num != 0)
			{
				OBJECT.PLACE_OBJECT_ON_GROUND_PROPERLY(num, p1: false);
				ENTITY.SET_ENTITY_HEADING(num, heading);
				if (freeze)
				{
					ENTITY.FREEZE_ENTITY_POSITION(num, toggle: true);
				}
				ENTITY.SET_ENTITY_AS_MISSION_ENTITY(num, p1: true, p2: true);
				if (cache == null)
				{
					GrudgeLog("CHECKPOINT prop model: " + text);
				}
				cache = text;
				return num;
			}
		}
		if (_propMissLogged.Add(models[0]))
		{
			GrudgeLog("CHECKPOINT none of these props could be made: " + string.Join(", ", models));
		}
		return 0;
	}

	private static string CompassTo(Vector3 from, Vector3 to)
	{
		double num = (Math.Atan2(to.X - from.X, to.Y - from.Y) * 180.0 / Math.PI + 360.0) % 360.0;
		return (new string[8] { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" })[(int)Math.Round(num / 45.0) % 8];
	}
}
