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
	private string _warKind;

	private bool _warTriggered;

	private int _warSince;

	private Vector3 _warSpot;

	private Vector3 _warDir;

	private readonly List<Ped> _warHorses = new List<Ped>();

	private Vehicle _warWagon;

	private int _wagonOrderAt;

	private int _forceWar;

	private int _warTestStep;

	private bool _forceWarWagon;

	private static readonly string[] BattleTitles = new string[6] { "Gunfire in the hills: the {a} and the {b} {p}", "The {a} and the {b} traded shots {p}", "A running fight on the road {p}: the {a} and the {b}", "The {a} caught a band of the {b} {p}", "The {b} fought off the {a} {p}", "Riders of the {a} and the {b} shot it out {p}" };

	private static readonly string[] NightTitles = new string[3] { "Gunfire in the night {p}: the {a} and the {b}", "The {a} hit the {b} in the dark {p}", "Muzzle flashes {p} - the {a} and the {b}" };

	private bool WarCardStage(string stage)
	{
		if (!(stage == "warband") && !(stage == "ambush"))
		{
			return stage == "aftermath";
		}
		return true;
	}

	private bool RunWarCard(string stage, Ped player)
	{
		(Gang, Gang)? warHere = WarHere;
		if (!warHere.HasValue)
		{
			return false;
		}
		Gang gang;
		Gang gang2;
		if (_rng.Next(2) != 0)
		{
			Gang item = warHere.Value.Item2;
			Gang item2 = warHere.Value.Item1;
			gang = item2;
			gang2 = item;
		}
		else
		{
			Gang item3 = warHere.Value.Item1;
			Gang item2 = warHere.Value.Item2;
			gang = item2;
			gang2 = item3;
		}
		if (stage == "aftermath")
		{
			return StartJustAfter(player, gang2, gang);
		}
		if (_skirmish.Count > 0 || _warKind != null)
		{
			return false;
		}
		if (!FactionModels.ContainsKey(gang2.Id) || !FactionModels.ContainsKey(gang.Id))
		{
			return false;
		}
		if (!(stage == "warband"))
		{
			return StartAmbush(player, gang2, gang);
		}
		return StartWarband(player, gang2, gang);
	}

	private void PrepareWarSides(Ped player, Gang a, Gang b, bool hateNow)
	{
		if (_sideA == 0)
		{
			_sideA = World.AddRelationshipGroup("SKIRMISH_A");
			_sideB = World.AddRelationshipGroup("SKIRMISH_B");
		}
		int relationship = (hateNow ? 6 : 3);
		World.SetRelationshipBetweenGroups((eRelationshipType)relationship, _sideA, _sideB);
		World.SetRelationshipBetweenGroups((eRelationshipType)relationship, _sideB, _sideA);
		World.SetRelationshipBetweenGroups(eRelationshipType.Dislike, _sideA, player.RelationshipGroup);
		World.SetRelationshipBetweenGroups(eRelationshipType.Dislike, _sideB, player.RelationshipGroup);
		_skirmishGangA = a.Id;
		_skirmishGangB = b.Id;
	}

	private List<Ped> SideMen(uint grp)
	{
		return _skirmish.Where((Ped p) => p != null && p.Exists() && !p.IsDead && ModGroupOf(p.Handle) == grp).ToList();
	}

	private void Mount(List<Ped> men)
	{
		foreach (Ped man in men)
		{
			Ped ped = MountOnHorse(man);
			if (!(ped == null))
			{
				_encHorses.Remove(ped);
				_warHorses.Add(ped);
			}
		}
	}

	private void RideTo(List<Ped> men, Vector3 to, float speed)
	{
		int num = 0;
		foreach (Ped man in men)
		{
			Vector3 vector = new Vector3((float)((num % 2 == 0) ? 1 : (-1)) * 2.5f * (float)(num / 2 + 1), -2f * (float)num, 0f);
			TASK.TASK_GO_TO_COORD_ANY_MEANS(man.Handle, to.X + vector.X, to.Y + vector.Y, to.Z, speed, 0, p6: false, 786603, 0f);
			num++;
		}
	}

	private bool StartWarband(Ped player, Gang a, Gang b)
	{
		Vector3 fwd = Forward(player);
		Vector3 vector = GoodSpot(player, delegate
		{
			double num2 = Math.Atan2(fwd.Y, fwd.X) + Math.PI * (0.55 + _rng.NextDouble() * 0.9) * (double)((_rng.Next(2) == 0) ? 1 : (-1));
			float num3 = 140f + (float)_rng.NextDouble() * 50f;
			return Ground(player.Position + new Vector3((float)Math.Cos(num2) * num3, (float)Math.Sin(num2) * num3, 0f));
		}, 16);
		if (vector == Vector3.Zero)
		{
			GrudgeLog("WAR warband: no hidden dry ground - not sent");
			return false;
		}
		Vector3 vector2 = player.Position - vector;
		vector2.Z = 0f;
		vector2.Normalize();
		_warSpot = Ground(player.Position + vector2 * 200f);
		_warDir = vector2;
		PrepareWarSides(player, a, b, hateNow: true);
		int num = SpawnSide(a, _sideA, vector);
		List<Ped> list = SideMen(_sideA);
		if (list.Count == 0)
		{
			GrudgeLog("WAR warband: nobody spawned");
			return false;
		}
		Mount(list);
		RideTo(list, _warSpot, 6f);
		_warKind = "warband";
		_warTriggered = false;
		_warSince = Game.GameTime;
		_skirmishAt = _warSpot;
		_skirmishEnds = Game.GameTime + 480000;
		SkirmishNote = $"{a.Name} warband ({num}) riding to hit the {b.Name}";
		Feed($"ENCOUNTER WARBAND: {num} {a.Name} ride past you to hit the {b.Name} - follow them", a.Accent);
		GrudgeNotice("Riders of the " + a.Name + " go by - they're out for the " + b.Name + ".");
		GrudgeLog($"WAR warband {a.Name} x{num} from {vector.DistanceTo(player.Position):0} m, riding to {_warSpot.X:0},{_warSpot.Y:0} ({_warSpot.DistanceTo(player.Position):0} m) for the {b.Name}");
		return true;
	}

	private bool StartAmbush(Ped player, Gang a, Gang b)
	{
		(Vector3, float, string)? tuple = RoadAhead(player);
		if (!tuple.HasValue)
		{
			GrudgeLog("WAR ambush: no road ahead");
			return false;
		}
		(Vector3, float, string) value = tuple.Value;
		Vector3 spot = value.Item1;
		float item = value.Item2;
		string item2 = value.Item3;
		float num = item * (float)Math.PI / 180f;
		Vector3 along = new Vector3(0f - (float)Math.Sin(num), (float)Math.Cos(num), 0f);
		Vector3 across = new Vector3((float)Math.Cos(num), (float)Math.Sin(num), 0f);
		if (_rng.Next(2) == 0)
		{
			across = -across;
		}
		_warSpot = spot;
		_warDir = along;
		Vector3 vector = GoodSpot(player, () => Ground(spot + across * (14f + (float)_rng.NextDouble() * 10f) * ((_rng.Next(2) == 0) ? 1f : (-1f)) + along * ((float)_rng.NextDouble() * 16f - 8f)), 12);
		Vector3 vector2 = Vector3.Zero;
		foreach (float item3 in new float[2] { 1f, -1f }.OrderByDescending((float s) => (spot + along * (130f * s)).DistanceTo(player.Position)))
		{
			float[] array = new float[3] { 130f, 110f, 150f };
			foreach (float num3 in array)
			{
				Vector3 vector3 = Ground(spot + along * (num3 * item3));
				float num4 = vector3.DistanceTo(player.Position);
				if (!(num4 < 80f) && !(num4 > 210f) && Dry(vector3) && HiddenFromView(player, vector3))
				{
					vector2 = vector3;
					break;
				}
			}
			if (vector2 != Vector3.Zero)
			{
				break;
			}
		}
		if (vector == Vector3.Zero || vector2 == Vector3.Zero)
		{
			GrudgeLog("WAR ambush: no spot out of your sight for " + ((vector == Vector3.Zero) ? "the men lying in wait" : "the riders") + " - not now");
			return false;
		}
		PrepareWarSides(player, a, b, hateNow: false);
		int num5 = SpawnSide(a, _sideA, vector);
		foreach (Ped item4 in SideMen(_sideA))
		{
			TASK.CLEAR_PED_TASKS(item4.Handle, p1: true, p2: false);
			ENTITY.SET_ENTITY_HEADING(item4.Handle, HeadingTo(item4.Position, spot));
			TASK.TASK_START_SCENARIO_IN_PLACE_HASH(item4.Handle, MISC.GET_HASH_KEY((_rng.Next(2) == 0) ? "WORLD_HUMAN_GUARD_SCOUT" : "WORLD_HUMAN_CROUCH_INSPECT"), -1, playEnterAnim: true, 0u, -1f, p6: false);
		}
		int num6 = SpawnSide(b, _sideB, vector2);
		List<Ped> list = SideMen(_sideB);
		_warWagon = null;
		bool num7 = _forceWarWagon || _rng.Next(100) < 40;
		_forceWarWagon = false;
		(Vector3, float)? tuple2 = (num7 ? WagonRoadStart(player, spot, along, vector2) : (((Vector3, float)?)null));
		if (num7 && !tuple2.HasValue)
		{
			GrudgeLog("WAR ambush: no road out of your sight for the wagon - riders instead");
		}
		if (tuple2.HasValue && list.Count > 0 && WarWagon(list, tuple2.Value.Item1, tuple2.Value.Item2, spot))
		{
			List<Ped> second = list.Where((Ped p) => PED.IS_PED_IN_ANY_VEHICLE(p.Handle, atGetIn: false)).ToList();
			List<Ped> list2 = list.Except(second).ToList();
			Mount(list2);
			int num8 = 0;
			foreach (Ped item5 in list2)
			{
				Vector3 vector4 = _warWagon.Position - _warWagon.ForwardVector * (7f + 3f * (float)num8) + _warWagon.RightVector * ((num8 % 2 == 0) ? 1.5f : (-1.5f));
				ENTITY.SET_ENTITY_COORDS((PED.GET_MOUNT(item5.Handle) != 0) ? PED.GET_MOUNT(item5.Handle) : item5.Handle, vector4.X, vector4.Y, vector4.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
				TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(item5.Handle, _warWagon.Handle, (num8 % 2 == 0) ? 1.5f : (-1.5f), -7f - 3f * (float)num8, 0f, 3f, -1, 3f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
				num8++;
			}
		}
		else
		{
			Mount(list);
			RideTo(list, spot, 4.5f);
		}
		if (num5 == 0 || num6 == 0)
		{
			GrudgeLog("WAR ambush: a side didn't spawn");
		}
		_warKind = "ambush";
		_warTriggered = false;
		_warSince = Game.GameTime;
		_skirmishAt = spot;
		_skirmishEnds = Game.GameTime + 480000;
		SkirmishNote = $"{a.Name} ({num5}) lying in wait for the {b.Name} ({num6})";
		Feed($"ENCOUNTER AMBUSH: {num5} {a.Name} wait by the road ahead for {num6} {b.Name} riders - warn them, join in or ride on", a.Accent);
		GrudgeLog($"WAR ambush {a.Name} x{num5} by the road at {spot.X:0},{spot.Y:0} ({spot.DistanceTo(player.Position):0} m, {item2}), {b.Name} x{num6} riding in from {vector2.DistanceTo(spot):0} m");
		return true;
	}

	private bool UpdateWarBuildUp(Ped player)
	{
		if (_warKind == null || _warTriggered)
		{
			return false;
		}
		Vector3 me = player.Position;
		List<Ped> list = SideMen(_sideA);
		List<Ped> list2 = SideMen(_sideB);
		if ((list.Count == 0 && list2.Count == 0) || (me.DistanceTo(_warSpot) > 450f && (list.Count == 0 || list.Min((Ped p) => p.Position.DistanceTo(me)) > 450f)))
		{
			GrudgeLog("WAR " + _warKind + " left behind - it plays out without you");
			EndWarBuildUp(player);
			return true;
		}
		int num = Game.GameTime - _warSince;
		if (_warKind == "warband")
		{
			Ped ped = list.OrderBy((Ped p) => p.Position.DistanceTo(_warSpot)).FirstOrDefault();
			bool flag = ped != null && ped.Position.DistanceTo(_warSpot) < 50f;
			bool flag2 = num > 180000;
			if ((flag || flag2) && me.DistanceTo(ped?.Position ?? _warSpot) < 300f)
			{
				Vector3 at = (flag ? _warSpot : ped.Position);
				Gang gang = Gangs.ById(_skirmishGangB);
				Vector3 vector = GoodSpot(player, delegate
				{
					double num6 = _rng.NextDouble() * Math.PI * 2.0;
					float num7 = 22f + (float)_rng.NextDouble() * 23f;
					return Ground(at + new Vector3((float)Math.Cos(num6) * num7, (float)Math.Sin(num6) * num7, 0f));
				}, 16);
				if (vector == Vector3.Zero)
				{
					if (num > 240000)
					{
						GrudgeLog("WAR warband: nowhere out of your sight for the other side - it never happens");
						EndWarBuildUp(player);
					}
					return true;
				}
				int num2 = SpawnSide(gang, _sideB, vector);
				TriggerWarFight(player, string.Format("the {0} warband hit {1} {2} {3}", Gangs.ById(_skirmishGangA)?.Name, num2, gang?.Name, flag ? "where they were headed" : "on the way"));
				_skirmishAt = at;
			}
			else if (flag2 && me.DistanceTo(ped?.Position ?? _warSpot) >= 300f)
			{
				GrudgeLog("WAR warband rode out of your life");
				EndWarBuildUp(player);
				return true;
			}
		}
		else if (_warKind == "ambush")
		{
			if (_warWagon != null && _warWagon.Exists() && _warWagon.Position.DistanceTo(_warSpot) < 25f)
			{
				Vector3 forwardVector = _warWagon.ForwardVector;
				Vector3 vector2 = _warWagon.Position + new Vector3(forwardVector.X, forwardVector.Y, 0f) * 5f;
				ModExplosion(vector2, (me.DistanceTo(vector2) < 60f) ? 0.3f : 0f, BoomOwner(_skirmish, vector2), "war ambush dynamite");
				GrudgeLog($"WAR ambush: dynamite by the wagon ({me.DistanceTo(vector2):0} m from you)");
				TriggerWarFight(player, "dynamite on the road - they hit the wagon");
				return true;
			}
			bool flag3 = _warWagon != null && _warWagon.Exists() && !VEHICLE.IS_VEHICLE_WRECKED(_warWagon.Handle);
			if (flag3 && Game.GameTime - _wagonOrderAt > 10000)
			{
				_wagonOrderAt = Game.GameTime;
				float num3 = ENTITY.GET_ENTITY_SPEED(_warWagon.Handle);
				float num4 = _warWagon.Position.DistanceTo(_warSpot);
				int num5 = VEHICLE.GET_PED_IN_VEHICLE_SEAT(_warWagon.Handle, -1);
				GrudgeLog(string.Format("WAR ambush: wagon {0:0} m from the spot, {1:0.0} m/s, {2:0} m from you{3}", num4, num3, me.DistanceTo(_warWagon.Position), (num5 == 0) ? " - NO DRIVER" : ""));
				if (num3 < 1f && num5 != 0)
				{
					TASK.TASK_VEHICLE_DRIVE_TO_COORD(num5, _warWagon.Handle, _warSpot.X, _warSpot.Y, _warSpot.Z, 6f, 0uL, ENTITY.GET_ENTITY_MODEL(_warWagon.Handle), 786603, 4f, 10f);
				}
			}
			bool flag4 = !flag3 && list2.Any((Ped p) => p.Position.DistanceTo(_warSpot) < 30f);
			bool flag5 = list.Concat(list2).Any((Ped p) => ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true)) || (PED.IS_PED_SHOOTING(player.Handle) && me.DistanceTo(_warSpot) < 120f);
			if (flag4 || flag5 || num > 120000)
			{
				TriggerWarFight(player, flag4 ? "the riders rode into it" : (flag5 ? "you fired first" : "the riders smelled it - it starts early"));
			}
		}
		return true;
	}

	private void TriggerWarFight(Ped player, string why)
	{
		_warTriggered = true;
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _sideA, _sideB);
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _sideB, _sideA);
		foreach (Ped item in _skirmish.Where((Ped p) => p.Exists() && !p.IsDead))
		{
			TASK.CLEAR_PED_TASKS(item.Handle, p1: false, p2: false);
			TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(item.Handle, 90f, 0, 0uL);
		}
		if (_skirmishBlip == 0)
		{
			_skirmishBlip = AddFightBlip(_skirmishAt, Gangs.ById(_skirmishGangA)?.Name + " vs " + Gangs.ById(_skirmishGangB)?.Name);
		}
		_skirmishEnds = Game.GameTime + 300000;
		Feed("WAR the fight starts: " + why, Theme.Warn);
		GrudgeLog("WAR " + _warKind + " -> skirmish: " + why);
		_warKind = null;
	}

	private unsafe void EndWarBuildUp(Ped player)
	{
		_warKind = null;
		_warTriggered = false;
		foreach (Ped item in _skirmish)
		{
			if (!(item == null) && item.Exists())
			{
				int handle = item.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		_skirmish.Clear();
		_skirmishNamed.Clear();
		ReleaseWarHorses();
		SkirmishNote = "";
	}

	private unsafe (Vector3 pos, float heading)? WagonRoadStart(Ped player, Vector3 spot, Vector3 along, Vector3 fallback)
	{
		ulong* ptr = stackalloc ulong[3];
		ulong* ptr2 = stackalloc ulong[1];
		Vector3 me = player.Position;
		foreach (float item in new float[2] { 1f, -1f }.OrderByDescending((float s) => (spot + along * (140f * s)).DistanceTo(me)))
		{
			float[] array = new float[4] { 70f, 55f, 90f, 40f };
			foreach (float num2 in array)
			{
				Vector3 position = spot + along * (num2 * item);
				*ptr = (ptr[1] = (ptr[2] = 0uL));
				*ptr2 = 0uL;
				if (!Function.Call<bool>(2580559617774072660uL, new InputArgument[8]
				{
					position.X,
					position.Y,
					position.Z,
					new InputArgument((ulong)ptr),
					new InputArgument((ulong)ptr2),
					1,
					3f,
					0f
				}))
				{
					continue;
				}
				Vector3 vector = new Vector3(*(float*)ptr, *(float*)(ptr + 1), *(float*)(ptr + 2));
				if (!(vector.DistanceTo(position) > 30f) && !(vector.DistanceTo(me) < 80f) && !(vector.DistanceTo(me) > 200f))
				{
					Vector3 vector2 = Ground(vector);
					if (!(Math.Abs(vector2.Z - vector.Z) > 3f) && HiddenFromView(player, vector))
					{
						return (new Vector3(vector.X, vector.Y, vector2.Z + 0.3f), HeadingTo(vector, spot));
					}
				}
			}
		}
		return null;
	}

	private bool WarWagon(List<Ped> men, Vector3 at, float heading, Vector3 to)
	{
		string text = (new string[3] { "wagon02x", "wagon04x", "cart01" })[_rng.Next(3)];
		uint num = MISC.GET_HASH_KEY(text);
		if (!STREAMING.IS_MODEL_IN_CDIMAGE(num))
		{
			GrudgeLog("WAR ambush: " + text + " not in the game files - riders instead");
			return false;
		}
		Vehicle vehicle = SpawnVehicle((VehicleHash)num, at, heading);
		if (vehicle == null)
		{
			GrudgeLog("WAR ambush: no " + text + " - riders instead");
			return false;
		}
		MakeOurs(vehicle.Handle);
		VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle.Handle, p1: false);
		_warWagon = vehicle;
		PED.SET_PED_INTO_VEHICLE(men[0].Handle, vehicle.Handle, -1);
		for (int i = 1; i < men.Count && i <= 2; i++)
		{
			PED.SET_PED_INTO_VEHICLE(men[i].Handle, vehicle.Handle, i - 1);
		}
		men[0].BlockPermanentEvents = true;
		TASK.TASK_VEHICLE_DRIVE_TO_COORD(men[0].Handle, vehicle.Handle, to.X, to.Y, to.Z, 6f, 0uL, num, 786603, 4f, 10f);
		_wagonOrderAt = Game.GameTime;
		GrudgeLog($"WAR ambush: the other side comes on a {text} ({Math.Min(3, men.Count)} aboard), {at.DistanceTo(to):0} m down the road, {at.DistanceTo(Game.Player.Character.Position):0} m from you");
		return true;
	}

	private unsafe void ReleaseWarHorses()
	{
		if (_warWagon != null && _warWagon.Exists() && BelongsToUs(_warWagon.Handle) && PED.GET_VEHICLE_PED_IS_IN(Game.Player.Character.Handle, lastVehicle: false) != _warWagon.Handle)
		{
			int handle = _warWagon.Handle;
			ReleaseIfOurs(&handle);
		}
		_warWagon = null;
		foreach (Ped warHorse in _warHorses)
		{
			if (!(warHorse == null) && warHorse.Exists())
			{
				int handle2 = warHorse.Handle;
				ReleaseIfOurs(&handle2);
			}
		}
		_warHorses.Clear();
	}

	private bool StartJustAfter(Ped player, Gang a, Gang b)
	{
		Vector3 fwd = Forward(player);
		Vector3 vector = GoodSpot(player, delegate
		{
			double num3 = Math.Atan2(fwd.Y, fwd.X) + (_rng.NextDouble() * 2.0 - 1.0) * 1.0;
			float num4 = 150f + (float)_rng.NextDouble() * 40f;
			return Ground(player.Position + new Vector3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, 0f));
		}, 16);
		if (vector == Vector3.Zero)
		{
			GrudgeLog("WAR aftermath: no hidden dry ground");
			return false;
		}
		long num = GameMinutesNow();
		_historyAt = num;
		WorldEvent worldEvent;
		try
		{
			worldEvent = SimClash(player, num, (a, b, "near " + GetZoneName(vector), vector));
		}
		finally
		{
			_historyAt = null;
		}
		if (worldEvent == null)
		{
			return false;
		}
		worldEvent.Heard = true;
		worldEvent.ForceSurvivors = true;
		if (worldEvent.AftermathUntil <= num)
		{
			worldEvent.AftermathUntil = num + (long)Config.WorldAftermathHours * 60L;
		}
		worldEvent.Lines.Add("You came upon it as the shooting stopped.");
		Chronicle.Events.Add(worldEvent);
		SaveWorld();
		int num2 = worldEvent.People.Count((WorldPart p) => p.Outcome == "KILLED") + worldEvent.UnnamedDead.Values.Sum();
		Feed($"ENCOUNTER JUST AFTER: {worldEvent.Title} - {num2} dead, {vector.DistanceTo(player.Position):0} m ahead", a.Accent);
		GrudgeNotice("Shots ahead - then quiet. " + worldEvent.Title + ".");
		GrudgeLog($"WAR aftermath: {worldEvent.Title} at {vector.X:0},{vector.Y:0} ({vector.DistanceTo(player.Position):0} m), {num2} dead");
		return true;
	}

	private void TestWarCard(int step, Ped player)
	{
		string text = step switch
		{
			2 => "ambush", 
			1 => "warband", 
			_ => "aftermath", 
		};
		GrudgeLog("WAR test (Alt+F10): " + text + " - war here: " + (WarHere.HasValue ? (WarHere.Value.a.Id + " vs " + WarHere.Value.b.Id) : "none") + ", district " + (CurrentDistrict?.Name ?? "unknown"));
		(Gang, Gang)? warHere = WarHere;
		(Gang, Gang) tuple;
		if (warHere.HasValue)
		{
			tuple = warHere.Value;
		}
		else
		{
			District currentDistrict = CurrentDistrict;
			Dictionary<string, int> holds = ((currentDistrict != null) ? ControlMap[currentDistrict.Key] : new Dictionary<string, int>());
			int value;
			int value2;
			(Gang, Gang) tuple2 = (from x in Gangs.All
				from y in Gangs.All
				select (x: x, y: y) into t
				where t.x.Id != t.y.Id && AtWarWith(t.x.Id, t.y.Id) && FactionModels.ContainsKey(t.x.Id) && FactionModels.ContainsKey(t.y.Id)
				orderby (holds.TryGetValue(t.x.Id, out value) ? value : 0) + (holds.TryGetValue(t.y.Id, out value2) ? value2 : 0) descending
				select t).FirstOrDefault();
			tuple = ((tuple2.Item1 != null) ? (tuple2.Item1, tuple2.Item2) : (Gangs.InEra(Gangs.ODriscolls) ? (Gangs.ODriscolls, Gangs.LemoyneRaiders) : (Gangs.LaramieGang, Gangs.DelLobos)));
		}
		if (text != "aftermath" && (_skirmish.Count > 0 || _warKind != null))
		{
			ShowNotification("A skirmish is already on - wait for it to end.");
			return;
		}
		Gang gang;
		Gang gang2;
		if (_rng.Next(2) == 0)
		{
			(gang, gang2) = tuple;
		}
		else
		{
			Gang item = tuple.Item2;
			Gang item2 = tuple.Item1;
			gang2 = item2;
			gang = item;
		}
		bool flag = ((text == "warband") ? StartWarband(player, gang, gang2) : ((text == "ambush") ? StartAmbush(player, gang, gang2) : StartJustAfter(player, gang, gang2)));
		ShowNotification(flag ? ("Test: " + text + " - the " + gang.Name + " and the " + gang2.Name + ".") : ("Test: " + text + " couldn't start here (see grudges.log)."));
	}

	internal string WarEncounterLine()
	{
		if (_warKind != null)
		{
			return $"NOW WAR {_warKind.ToUpperInvariant()}: {SkirmishNote} - {(Game.GameTime - _warSince) / 1000}s, the spot {Game.Player.Character.Position.DistanceTo(_warSpot):0} m from you";
		}
		return null;
	}
}
