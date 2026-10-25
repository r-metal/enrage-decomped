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
	private Vehicle _supplyWagon;

	private Gang _supplyWagonGang;

	private readonly List<Ped> _supplyWagonMen = new List<Ped>();

	private readonly List<Ped> _supplyWagonHorses = new List<Ped>();

	private Blip _supplyWagonBlip;

	private Vector3 _supplyWagonLastAt;

	private int _supplyWagonMovedAt;

	private int _supplyWagonNudges;

	private int _supplyWagonTryN;

	private string _supplyWagonTryWhat;

	private static readonly string[] RoadSupplyWagonModels = new string[12]
	{
		"supplywagon", "wagon05x", "wagon06x", "wagonwork01x", "utilliwag", "chuckwagon000x", "chuckwagon002x", "wagondairy01x", "cart06", "cart07",
		"cart08", "wagon04x"
	};

	private static readonly string[] ArmySupplyWagonModels = new string[6] { "armysupplywagon", "supplywagon", "wagon05x", "wagon06x", "wagonwork01x", "chuckwagon000x" };

	private Vector3 _supplyWagonTo;

	private Vector3 _supplyTakenAt;

	private bool _supplyWagonWander;

	private bool _supplyWagonHostile;

	private bool _supplyWagonCounted;

	private bool _supplyWagonTaken;

	private int _supplyWagonAt;

	private int _nextSupplyWagonRoll;

	private int _supplyWagonAllowed;

	private int _nextSupplyWagonCheck;

	private string _townCratesFor;

	private int _supplyWagonPushAt;

	private IEnumerable<Ped> SupplyWagonMen => _supplyWagonMen;

	private void UpdateSupplyWagons(Ped player)
	{
		RunSupplyWagon(player);
		UpdateTownCrates(player);
		if (Game.GameTime < _nextSupplyWagonRoll)
		{
			return;
		}
		_nextSupplyWagonRoll = Game.GameTime + 120000;
		if (_supplyWagon != null || Game.GameTime < _supplyWagonAllowed || MISC.GET_MISSION_FLAG() || WorldBusy(player) || InsideAnyTown(player.Position) || NoSpawnReason(player) != null || player.IsDead)
		{
			return;
		}
		Gang gang = TopHolderHere(player);
		District district = DistrictAt(player.Position);
		if (gang != null && district != null && HoldOf(district, gang.Id) >= 10 && FactionModels.ContainsKey(gang.Id))
		{
			double num = ((WarsOf(gang) > 0) ? 18 : 12);
			double num2 = _rng.NextDouble() * 100.0;
			if (!(num2 >= num) && SpawnSupplyWagon(player, gang))
			{
				GrudgeLog($"SUPPLY wagon roll: {num:0}%, rolled {num2:0} - the {gang.Name} run supplies through {district.Name}");
			}
		}
	}

	private bool SpawnSupplyWagon(Ped player, Gang g)
	{
		Vector3 position = player.Position;
		double num = Math.Atan2(Forward(player).Y, Forward(player).X);
		Vector3? vector = null;
		float heading = 0f;
		for (int i = 0; i < 30; i++)
		{
			if (vector.HasValue)
			{
				break;
			}
			double num2 = num + (_rng.NextDouble() - 0.5) * Math.PI * ((i < 20) ? 0.6 : 1.4);
			float num3 = 120f + (float)_rng.NextDouble() * 60f;
			float heading2;
			Vector3? vector2 = RoadNodeNear(new Vector3(position.X + (float)Math.Cos(num2) * num3, position.Y + (float)Math.Sin(num2) * num3, position.Z), 35f, out heading2, (i >= 20) ? 1 : 0);
			if (!vector2.HasValue)
			{
				continue;
			}
			Vector3 vector3 = Ground(vector2.Value);
			if (!(Math.Abs(vector3.Z - vector2.Value.Z) > 3f) && Dry(vector2.Value) && HiddenFromView(player, vector2.Value + new Vector3(0f, 0f, 1.5f)))
			{
				float num4 = vector2.Value.DistanceTo(position);
				if (!(num4 < 100f) && !(num4 > 200f))
				{
					vector = new Vector3(vector2.Value.X, vector2.Value.Y, vector3.Z + 0.3f);
					heading = HeadingTo(vector2.Value, position);
				}
			}
		}
		if (!vector.HasValue)
		{
			GrudgeLog("SUPPLY wagon: no road out of your view 120-180 m ahead - none");
			return false;
		}
		Vector3 vector4 = position - vector.Value;
		vector4.Z = 0f;
		vector4.Normalize();
		Vector3? vector5 = WagonGoal(position + vector4 * 250f, 150f);
		_supplyWagonWander = !vector5.HasValue;
		_supplyWagonTo = vector5 ?? (position + vector4 * 250f);
		Vehicle vehicle = null;
		string text = null;
		for (int j = 0; j < 2; j++)
		{
			if (!(vehicle == null))
			{
				break;
			}
			string[] array = ((j != 1) ? ((g == Gangs.Army) ? ArmySupplyWagonModels : RoadSupplyWagonModels).OrderBy((string _) => _rng.Next()).ToArray() : new string[3] { "wagon04x", "wagon02x", "cart01" });
			foreach (string text2 in array)
			{
				uint num6 = MISC.GET_HASH_KEY(text2);
				if (!STREAMING.IS_MODEL_IN_CDIMAGE(num6) || !STREAMING.IS_MODEL_A_VEHICLE(num6))
				{
					continue;
				}
				if (((j == 1 || VEHICLE._GET_NUM_DRAFT_VEHICLE_HARNESS_PED(num6) != 0) ? 1 : 0) <= (false ? 1 : 0))
				{
					GrudgeLog("SUPPLY wagon: " + text2 + " has no horses to pull it - skipped");
					continue;
				}
				Crumbs.Native("supply wagon (road)");
				vehicle = SpawnVehicle((VehicleHash)num6, vector.Value, heading);
				if (vehicle != null)
				{
					text = text2;
					break;
				}
			}
		}
		if (vehicle == null)
		{
			GrudgeLog("SUPPLY wagon: no wagon model could be made");
			return false;
		}
		MakeOurs(vehicle.Handle);
		VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle.Handle, p1: false);
		EnsureCheckpointGroup(player);
		for (int num7 = 0; num7 < 2; num7++)
		{
			Ped ped = CheckpointMan(g, vector.Value + new Vector3(2f + (float)num7, 0f, 0f), heading, null);
			if (!(ped == null))
			{
				PED.SET_PED_INTO_VEHICLE(ped.Handle, vehicle.Handle, num7 - 1);
				_supplyWagonMen.Add(ped);
			}
		}
		if (_supplyWagonMen.Count == 0)
		{
			vehicle.Delete();
			return false;
		}
		int num8 = 0;
		for (int num9 = 0; num9 < 2; num9++)
		{
			float num10 = ((num9 == 0) ? 1.8f : (-1.8f));
			float num11 = -8f - 3f * (float)num9;
			Vector3 pos = vector.Value + vehicle.ForwardVector * num11 + vehicle.RightVector * num10;
			Ped ped2 = CheckpointMan(g, pos, heading, null);
			if (!(ped2 == null))
			{
				_supplyWagonMen.Add(ped2);
				Ped ped3 = MountOnHorse(ped2);
				if (!(ped3 == null))
				{
					_encHorses.Remove(ped3);
					_supplyWagonHorses.Add(ped3);
					ENTITY.SET_ENTITY_COORDS(ped3.Handle, pos.X, pos.Y, pos.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
					ENTITY.SET_ENTITY_HEADING(ped3.Handle, heading);
					Crumbs.Native("supply rider follow");
					TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(ped2.Handle, vehicle.Handle, num10, num11, 0f, 3f, -1, 3f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
					num8++;
				}
			}
		}
		Ped ped4 = _supplyWagonMen[0];
		ped4.BlockPermanentEvents = true;
		Crumbs.Native("supply wagon drive");
		if (_supplyWagonWander)
		{
			WagonWander(ped4, vehicle);
		}
		else
		{
			WagonDrive(ped4, vehicle, _supplyWagonTo);
		}
		_supplyWagonTryN = 0;
		_supplyWagonTryWhat = null;
		_supplyWagon = vehicle;
		_supplyWagonGang = g;
		_supplyWagonAt = Game.GameTime;
		_supplyWagonLastAt = vector.Value;
		_supplyWagonMovedAt = Game.GameTime;
		_supplyWagonNudges = 0;
		_supplyWagonHostile = (_supplyWagonCounted = (_supplyWagonTaken = false));
		GrudgeLog(string.Format("SUPPLY wagon: the {0} supplies on the road - a {1}, {2} aboard + {3} riding with it, {4:0} m {5} of you{6}", OutfitPoss(g), text, _supplyWagonMen.Count - num8, num8, vector.Value.DistanceTo(position), CompassTo(position, vector.Value), _supplyWagonWander ? " - no main road behind you: it drives on along its road" : ""));
		return true;
	}

	private void RunSupplyWagon(Ped player)
	{
		if (_supplyWagon == null || Game.GameTime < _nextSupplyWagonCheck)
		{
			return;
		}
		_nextSupplyWagonCheck = Game.GameTime + 1000;
		Vector3 me = player.Position;
		Gang supplyWagonGang = _supplyWagonGang;
		bool flag = _supplyWagon.Exists();
		if (!_supplyWagonCounted)
		{
			bool flag2 = !flag || VEHICLE.IS_VEHICLE_WRECKED(_supplyWagon.Handle) || ENTITY.GET_ENTITY_HEALTH(_supplyWagon.Handle) <= 0;
			if (flag && PED.IS_PED_IN_VEHICLE(player.Handle, _supplyWagon.Handle, atGetIn: false) && !_supplyWagonTaken)
			{
				_supplyWagonTaken = true;
				_supplyTakenAt = _supplyWagon.Position;
			}
			bool flag3 = _supplyWagonTaken && flag && _supplyWagon.Position.DistanceTo(_supplyTakenAt) > 150f;
			if (flag2 || flag3)
			{
				_supplyWagonCounted = true;
				AddSupplies(supplyWagonGang, -12, flag2 ? "you wrecked their supply wagon on the road" : "you made off with their supply wagon");
				AddNotoriety(supplyWagonGang, 2, "you hit their supply wagon");
				ShowNotification((flag2 ? "You destroyed" : "You made off with") + " the " + ShortOutfit(supplyWagonGang) + " supply wagon.");
				DropSupplyWagonBlip();
				Feed("SUPPLIES " + supplyWagonGang.Name + ": -12 (their wagon)", supplyWagonGang.Accent);
				Vector3 vector = (flag ? _supplyWagon.Position : me);
				try
				{
					ChronicleDeed("supplies", "You " + (flag2 ? "destroyed" : "made off with") + " the " + OutfitPoss(supplyWagonGang) + " supply wagon", "near " + GetZoneName(vector), vector, supplyWagonGang.Id);
				}
				catch
				{
				}
			}
		}
		if (flag && !_supplyWagonCounted && _supplyWagonBlip == null && !_mapHidden && _supplyWagon.Position.DistanceTo(me) < 150f && !HiddenFromView(player, _supplyWagon.Position + new Vector3(0f, 0f, 1.5f)))
		{
			Crumbs.Native("supply wagon blip");
			_supplyWagonBlip = _supplyWagon.AddBlip(BlipType.BLIP_STYLE_CAMP_ENEMY);
			if (_supplyWagonBlip != null)
			{
				try
				{
					MAP.SET_BLIP_SPRITE(_supplyWagonBlip.Handle, MISC.GET_HASH_KEY("blip_chest"), p2: true);
				}
				catch
				{
				}
				try
				{
					Function.Call(15242226327205421655uL, _supplyWagonBlip.Handle, 0.7f);
				}
				catch
				{
				}
				Function.Call(11290983183885202434uL, _supplyWagonBlip.Handle, new InputArgument(UiDraw.ForeverVarString(ShortOutfit(supplyWagonGang) + " supply wagon")));
				GrudgeLog("SUPPLY wagon: you've seen it - on the map");
			}
		}
		if (_supplyWagonCounted)
		{
			DropSupplyWagonBlip();
		}
		Vector3 position;
		Ped drv;
		int num;
		float num2;
		string text;
		bool flag4;
		bool flag5;
		if (flag && !_supplyWagonHostile && !_supplyWagonTaken)
		{
			position = _supplyWagon.Position;
			if (position.DistanceTo(_supplyWagonLastAt) > 3f)
			{
				_supplyWagonLastAt = position;
				_supplyWagonMovedAt = Game.GameTime;
				if (_supplyWagonNudges < 99 && Game.GameTime - _supplyWagonPushAt > 20000)
				{
					_supplyWagonNudges = 0;
				}
				if (_supplyWagonTryN > 0)
				{
					WagonRolled("supply wagon", _supplyWagonTryN, _supplyWagonTryWhat);
					_supplyWagonTryN = 0;
				}
			}
			else if (Game.GameTime - _supplyWagonMovedAt > 12000)
			{
				_supplyWagonMovedAt = Game.GameTime;
				drv = _supplyWagonMen.FirstOrDefault((Ped p) => p.Exists() && !p.IsDead && PED.IS_PED_IN_VEHICLE(p.Handle, _supplyWagon.Handle, atGetIn: false));
				num = VEHICLE._GET_PED_IN_DRAFT_HARNESS(_supplyWagon.Handle, 0);
				num2 = position.DistanceTo(_supplyWagonTo);
				text = "left";
				flag4 = num != 0 && (PED.IS_PED_FLEEING(num) || PED.IS_PED_RAGDOLL(num));
				flag5 = position.DistanceTo(me) < 15f || _supplyWagonMen.Any((Ped p) => p.Exists() && InterroFor(p.Handle) != null);
				if (flag5)
				{
					text = "you're with it - it waits";
				}
				if (drv != null && !flag5 && _supplyWagonNudges < 12)
				{
					_supplyWagonNudges++;
					PED.SET_PED_INTO_VEHICLE(drv.Handle, _supplyWagon.Handle, -1);
					drv.BlockPermanentEvents = true;
					if (flag4)
					{
						TASK.CLEAR_PED_TASKS(num, p1: true, p2: true);
					}
					ENTITY.GET_ENTITY_MODEL(_supplyWagon.Handle);
					_supplyWagonTryN = 0;
					if (num2 < 40f && !_supplyWagonWander)
					{
						Crumbs.Native("supply wagon drive on");
						Vector3? vector2 = WagonGoal(position + _supplyWagon.ForwardVector * 300f, 150f);
						if (vector2.HasValue)
						{
							Vector3 to = (_supplyWagonTo = vector2.GetValueOrDefault());
							Drive(to);
							text = $"there - on along the road ({to.DistanceTo(position):0} m)";
						}
						else
						{
							_supplyWagonWander = true;
							Drive(position);
							text = "there - on along the road it's on (no main road ahead)";
						}
					}
					else if (_supplyWagonNudges <= 5)
					{
						Crumbs.Native("supply wagon try " + _supplyWagonNudges);
						Vector3 to2 = _supplyWagonTo;
						text = $"try {_supplyWagonNudges}: " + WagonTry(_supplyWagonNudges, drv, _supplyWagon, ref to2, ref _supplyWagonWander);
						_supplyWagonTo = to2;
						_supplyWagonTryN = _supplyWagonNudges;
						_supplyWagonTryWhat = text;
					}
					else
					{
						if (HiddenFromView(player, position + new Vector3(0f, 0f, 1.5f)))
						{
							float heading;
							Vector3? vector2 = RoadNodeNear(position + _supplyWagon.ForwardVector * 25f, 30f, out heading);
							if (vector2.HasValue)
							{
								Vector3 valueOrDefault = vector2.GetValueOrDefault();
								Crumbs.Native("supply wagon hop");
								ENTITY.SET_ENTITY_COORDS(_supplyWagon.Handle, valueOrDefault.X, valueOrDefault.Y, valueOrDefault.Z + 0.3f, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
								ENTITY.SET_ENTITY_HEADING(_supplyWagon.Handle, heading);
								VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(_supplyWagon.Handle, p1: false);
								_supplyWagonLastAt = valueOrDefault;
								_supplyWagonPushAt = Game.GameTime;
								Drive(_supplyWagonTo);
								text = $"out of sight - moved {valueOrDefault.DistanceTo(position):0} m on along the road and told to drive again";
								goto IL_080f;
							}
						}
						Crumbs.Native("supply wagon push");
						Function.Call(18012475414774216423uL, _supplyWagon.Handle, 3f);
						_supplyWagonPushAt = Game.GameTime;
						Drive(_supplyWagonTo);
						text = "pushed forward and told to drive again";
					}
					goto IL_080f;
				}
				goto IL_0821;
			}
		}
		goto IL_0907;
		IL_0821:
		if (!flag5 && (!(text == "left") || _supplyWagonNudges < 99))
		{
			if (text == "left")
			{
				_supplyWagonNudges = 99;
			}
			GrudgeLog(string.Format("SUPPLY wagon: standing 12 s, {0:0} m from where it was going, {1:0} m from you - driver {2}, horse in harness {3}: {4}{5}", num2, position.DistanceTo(me), (drv != null) ? "aboard" : "none", (num != 0) ? "yes" : "NO", text, (drv != null && _supplyWagon.Exists()) ? (" | WAGONDIAG " + WagonState(_supplyWagon, drv)) : ""));
		}
		goto IL_0907;
		IL_0907:
		if (!_supplyWagonHostile)
		{
			bool flag6 = _supplyWagonMen.Any((Ped p) => p.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true)) || (flag && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(_supplyWagon.Handle, player.Handle, p2: true, p3: true));
			bool flag7 = AfterYou(supplyWagonGang) && _supplyWagonMen.Any((Ped p) => p.Exists() && !p.IsDead && p.Position.DistanceTo(me) < 25f);
			if (flag6 || flag7 || _supplyWagonTaken)
			{
				GroupFight(_supplyWagonMen, player, ref _supplyWagonHostile, "SUPPLY wagon: the " + supplyWagonGang.Name + " guards fight - " + (flag6 ? "you hit them" : (_supplyWagonTaken ? "you took the wagon" : "they know your face")));
			}
		}
		bool num3 = !flag || (_supplyWagon.Position.DistanceTo(me) > 350f && HiddenFromView(player, _supplyWagon.Position));
		bool flag8 = flag && _supplyWagon.Position.DistanceTo(_supplyWagonTo) < 15f && (HiddenFromView(player, _supplyWagon.Position) || _supplyWagon.Position.DistanceTo(me) > 120f);
		bool flag9 = Game.GameTime - _supplyWagonAt > 360000 && !_supplyWagonHostile && (!flag || HiddenFromView(player, _supplyWagon.Position));
		if (num3 || flag8 || flag9 || (_supplyWagonMen.All((Ped p) => !p.Exists() || p.IsDead) && (!flag || _supplyWagon.Position.DistanceTo(me) > 120f)))
		{
			EndSupplyWagon(player, wipe: false);
		}
		return;
		IL_080f:
		if (flag4)
		{
			text += " (its horse was spooked - calmed)";
		}
		goto IL_0821;
		void Drive(Vector3 to3)
		{
			if (_supplyWagonWander)
			{
				WagonWander(drv, _supplyWagon);
			}
			else
			{
				WagonDrive(drv, _supplyWagon, to3);
			}
		}
	}

	private unsafe void EndSupplyWagon(Ped player, bool wipe)
	{
		if (_supplyWagon == null && _supplyWagonMen.Count == 0)
		{
			return;
		}
		foreach (Ped item in _supplyWagonMen.ToList())
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
		_supplyWagonMen.Clear();
		foreach (Ped supplyWagonHorse in _supplyWagonHorses)
		{
			if (supplyWagonHorse == null || !supplyWagonHorse.Exists())
			{
				continue;
			}
			if (wipe)
			{
				if (BelongsToUs(supplyWagonHorse.Handle))
				{
					Wipe(supplyWagonHorse);
				}
			}
			else
			{
				int handle = supplyWagonHorse.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		_supplyWagonHorses.Clear();
		DropSupplyWagonBlip();
		if (_supplyWagon != null && _supplyWagon.Exists())
		{
			if (!PED.IS_PED_IN_VEHICLE(player.Handle, _supplyWagon.Handle, atGetIn: false) && BelongsToUs(_supplyWagon.Handle) && (wipe || SafeToDeleteVehicle(_supplyWagon)))
			{
				_supplyWagon.Delete();
			}
			else
			{
				int handle2 = _supplyWagon.Handle;
				ReleaseIfOurs(&handle2);
			}
		}
		GrudgeLog("SUPPLY wagon: over" + (_supplyWagonCounted ? " (hit)" : ""));
		_supplyWagon = null;
		_supplyWagonGang = null;
		_supplyWagonAllowed = Game.GameTime + 360000;
	}

	private void DropSupplyWagonBlip()
	{
		if (_supplyWagonBlip == null)
		{
			return;
		}
		Crumbs.Native("supply wagon blip remove");
		try
		{
			if (_supplyWagonBlip.Exists())
			{
				_supplyWagonBlip.Delete();
			}
		}
		catch
		{
		}
		_supplyWagonBlip = null;
	}
}
