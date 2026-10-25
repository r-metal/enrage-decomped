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
	internal static readonly PedHash[] RiderHorses = new PedHash[4]
	{
		PedHash.a_c_horse_americanstandardbred_black,
		PedHash.a_c_horse_americanstandardbred_buckskin,
		PedHash.a_c_horse_americanpaint_overo,
		PedHash.a_c_horse_americanpaint_tobiano
	};

	private const ulong ClosestNodeWithHeading = 2580559617774072660uL;

	private (Vector3 pos, float heading, int point)? _calloutSeat;

	private Vehicle _encVehicle;

	private static readonly string[] RoadWagonModels = new string[3] { "wagon02x", "wagon04x", "cart01" };

	private Vector3 _roadWagonTo;

	private int _roadWagonCheck;

	private int _roadWagonStill;

	private bool _roadWagonWalked;

	private bool _roadWagonWalkSent;

	private int _roadWagonTryN;

	private string _roadWagonTryWhat;

	private bool _roadWagonWander;

	private bool _encForced;

	private Vector3 _wagonRoad;

	private static readonly (string what, string lead, string[] crew)[] Activities = new(string, string, string[])[4]
	{
		("round a fire", "WORLD_HUMAN_FIRE_SIT", new string[3] { "WORLD_HUMAN_FIRE_SIT", "WORLD_HUMAN_FIRE_STAND", "WORLD_HUMAN_DRINKING" }),
		("having a smoke", "WORLD_HUMAN_SMOKE", new string[3] { "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_GUARD_LAZY", "WORLD_HUMAN_DRINKING" }),
		("drinking", "WORLD_HUMAN_DRINKING", new string[3] { "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_FIRE_STAND", "WORLD_HUMAN_SMOKE" }),
		("keeping watch", "WORLD_HUMAN_GUARD_SCOUT", new string[3] { "WORLD_HUMAN_GUARD_LAZY", "WORLD_HUMAN_GUARD_SCOUT", "WORLD_HUMAN_SMOKE" })
	};

	private NemesisRecord PickTownCaller(List<NemesisRecord> pool)
	{
		double num = ((IEnumerable<NemesisRecord>)pool).Sum((Func<NemesisRecord, double>)W);
		if (num <= 0.0)
		{
			return null;
		}
		double num2 = _rng.NextDouble() * num;
		foreach (NemesisRecord item in pool)
		{
			num2 -= W(item);
			if (num2 <= 0.0)
			{
				return item;
			}
		}
		return pool[pool.Count - 1];
		double W(NemesisRecord n)
		{
			return (n.Personality.HasValue ? Personalities.AmbushWeight(n.Personality.Value) : 1.0) * (1.0 - Math.Min(0.8, (double)EffectiveFear(n) / 150.0)) * (double)((n.HateTier == HateTier.Murderous) ? Balance.TownMurderousW : ((n.HateTier == HateTier.Vengeful) ? Balance.TownVengefulW : Balance.TownResentful));
		}
	}

	private void RunRandomEncounter(Ped player, bool test)
	{
		string text = (test ? null : NoSpawnReason(player));
		bool lawOnly = text == "the law is after you";
		if (text != null && !lawOnly)
		{
			LastEncounterNote = "held: " + text;
		}
		else
		{
			if (!EncounterContextOk(player, out var town, out var mounted) && !test)
			{
				return;
			}
			if (!PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle))
			{
				LastEncounterNote = "no player control (cutscene / mission)";
			}
			else
			{
				if (_spawnForce == null && RunWorldCards(player, town, lawOnly))
				{
					return;
				}
				List<NemesisRecord> source;
				NemesisRecord rec;
				if (town && !test && !lawOnly)
				{
					string here = _currentTown ?? TownAt(player.Position);
					if (!NearTownCore(player, here))
					{
						LastEncounterNote = "edge of " + here + ": waiting till you're in town";
						return;
					}
					if (Game.GameTime - _visitStartedAt < 60000)
					{
						LastEncounterNote = $"{here}: letting you settle in ({(60000 - (Game.GameTime - _visitStartedAt)) / 1000}s)";
						return;
					}
					source = Roster.Where((NemesisRecord n) => n.CanHunt && !n.PresumedDead && !Resting(n) && !CoolingOff(n) && !IsArmy(n) && !NoTowns(n) && IntentOf(n) != Intent.None && !_worldNemeses.ContainsValue(n) && n != ActiveRecord && (n.HateTier != HateTier.Murderous || n.Hate >= Balance.TownMurderousMinHate) && (IntentOf(n) == Intent.Hunts || n.KillLocation == here || (n.HasOrigin && new Vector3(n.OriginX, n.OriginY, n.OriginZ).DistanceTo(player.Position) < 600f))).ToList();
					source = source.Where((NemesisRecord n) => n.HateTier == HateTier.Murderous && HasReason(n)).ToList();
					if (source.Count == 0)
					{
						LastEncounterNote = "town: no murderous man free to call you out";
						return;
					}
					if (_roadAmbush != null)
					{
						LastEncounterNote = "town: " + _roadAmbush.Rec.Name + " is waiting outside";
						return;
					}
					rec = PickTownCaller(source);
					if (rec == null)
					{
						LastEncounterNote = "town: nobody picked";
						return;
					}
				}
				else
				{
					source = Roster.Where((NemesisRecord n) => n.CanHunt && !_nemesisBodies.ContainsValue(n) && (test || (!Resting(n) && !CoolingOff(n) && InHuntingRange(n, player.Position) && (IntentOf(n) == Intent.Hunts || HiresHunters(n)))) && (!lawOnly || HiresHunters(n))).ToList();
					if (lawOnly && source.Count == 0)
					{
						LastEncounterNote = "held: the law is after you";
						return;
					}
					if (source.Count == 0)
					{
						LastEncounterNote = $"no hunters in range ({Roster.Count((NemesisRecord n) => n.CanHunt)} able)";
						return;
					}
					rec = PickAmbusher(source);
				}
				if (_spawnForce != null && source.Count > 0)
				{
					List<NemesisRecord> list = source.Where((NemesisRecord n) => Cards.Any((EncounterCard c) => c.Enabled && c.Stage == _spawnForce && CardFits(c, n, town, mounted, test: true))).ToList();
					if (list.Count > 0)
					{
						rec = PickAmbusher(list) ?? list[_rng.Next(list.Count)];
					}
					else if (rec == null)
					{
						rec = source[_rng.Next(source.Count)];
					}
				}
				if (rec == null)
				{
					LastEncounterNote = $"{source.Count} in range, none with a reason (weights 0)";
					return;
				}
				LastEncounterNote = $"{source.Count} hunters -> {rec.Name} ({rec.Personality}, hate {rec.Hate}, fear {EffectiveFear(rec)})";
				if (_spawnForce == null && HiresHunters(rec) && IntentOf(rec) != Intent.Hunts)
				{
					if (!HuntersCardOn)
					{
						LastEncounterNote += " - would hire hunters (card off)";
						return;
					}
					LastEncounterNote += " - hired bounty hunters";
					SendBountyHunters(rec, player);
					return;
				}
				EncounterCard encounterCard = PickCard(rec, town, mounted, test);
				if (_spawnForce != null)
				{
					encounterCard = (from c in Cards
						where c.Enabled && c.Stage == _spawnForce
						orderby CardFits(c, rec, town, mounted, test: true) descending
						select c).FirstOrDefault() ?? BuiltInCards().FirstOrDefault((EncounterCard c) => c.Stage == _spawnForce) ?? encounterCard;
					LastCardNote = "spawner: card " + encounterCard?.Id + " for " + rec.Name;
				}
				if (encounterCard == null && test)
				{
					encounterCard = Cards.FirstOrDefault((EncounterCard c) => c.Enabled && c.Stage == (town ? "callout" : "idle")) ?? BuiltInCards().First((EncounterCard c) => c.Stage == (town ? "callout" : "idle"));
				}
				if (encounterCard == null)
				{
					LastEncounterNote = LastEncounterNote + " - " + LastCardNote;
					return;
				}
				EncounterKind encounterKind = StageKind(encounterCard.Stage);
				GrudgeLog($"ENCOUNTER {encounterKind} {rec.FullName} town={town} mounted={mounted} test={test} - {LastCardNote}");
				if (town && !test && !lawOnly && !TakeAggressorSlot(out var why))
				{
					LastEncounterNote = "town: no call-out (" + why + ")";
					return;
				}
				_encForced = test;
				try
				{
					SpawnEncounter(rec, encounterKind, player, encounterCard);
				}
				finally
				{
					_encForced = false;
				}
				if (town && !test && !lawOnly && ActiveRecord != rec)
				{
					_visitAggressors = Math.Max(0, _visitAggressors - 1);
					GrudgeLog("TOWNCALM " + rec.Name + "'s call-out never made it into " + _currentTown + " - the slot is free again");
				}
			}
		}
	}

	private unsafe static Vector3 Ground(Vector3 p)
	{
		float z = default(float);
		if (MISC.GET_GROUND_Z_FOR_3D_COORD(p.X, p.Y, p.Z + 100f, &z, p4: false))
		{
			p.Z = z;
		}
		return p;
	}

	internal unsafe static bool Raised(Vector3 p)
	{
		float[] array = new float[3] { 1f, 2.5f, 4f };
		float num2 = default(float);
		foreach (float num in array)
		{
			if (MISC.GET_GROUND_Z_FOR_3D_COORD(p.X, p.Y, p.Z - num, &num2, p4: false) && num2 < p.Z - 1.5f && num2 > p.Z - 40f)
			{
				return true;
			}
		}
		Vector3 safeCoordForPed = World.GetSafeCoordForPed(p, onGround: true, 0);
		if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && new Vector3(safeCoordForPed.X - p.X, safeCoordForPed.Y - p.Y, 0f).Length() < 4f)
		{
			return safeCoordForPed.Z < p.Z - 2f;
		}
		return false;
	}

	private bool UpHigh(Vector3 p)
	{
		if (Raised(p))
		{
			return true;
		}
		string text = TownAt(p) ?? _currentTown;
		if (text == null || !TownCore(text, out var centre, out var _) || centre.Z == 0f)
		{
			return false;
		}
		float z = Game.Player.Character.Position.Z;
		if (p.Z > centre.Z + 6f)
		{
			return p.Z > z + 6f;
		}
		return false;
	}

	internal unsafe static Vector3 GroundOnSpot(Vector3 p)
	{
		float num = default(float);
		if (MISC.GET_GROUND_Z_FOR_3D_COORD(p.X, p.Y, p.Z + 4f, &num, p4: false) && Math.Abs(num - p.Z) < 12f)
		{
			p.Z = num;
			return p;
		}
		Vector3 safeCoordForPed = World.GetSafeCoordForPed(p, onGround: true, 0);
		if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && new Vector3(safeCoordForPed.X - p.X, safeCoordForPed.Y - p.Y, 0f).Length() < 8f && Math.Abs(safeCoordForPed.Z - p.Z) < 12f)
		{
			return safeCoordForPed;
		}
		return Ground(p);
	}

	internal static float HeadingTo(Vector3 from, Vector3 to)
	{
		return (float)(Math.Atan2(0f - (to.X - from.X), to.Y - from.Y) * 180.0 / Math.PI);
	}

	private static Vector3 Forward(Ped p)
	{
		double num = (double)p.Heading * Math.PI / 180.0;
		return new Vector3(0f - (float)Math.Sin(num), (float)Math.Cos(num), 0f);
	}

	private Vector3 HighGround(Vector3 around, Ped player = null)
	{
		Vector3 vector = Vector3.Zero;
		Vector3 result = Ground(around + new Vector3(90f, 0f, 0f));
		for (int i = 0; i < 12; i++)
		{
			double num = (double)i * Math.PI / 6.0 + _rng.NextDouble() * 0.3;
			float num2 = 80f + (float)_rng.NextDouble() * 30f;
			Vector3 vector2 = Ground(around + new Vector3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, 0f));
			if (vector2.Z > result.Z)
			{
				result = vector2;
			}
			if ((!(player != null) || (Dry(vector2) && HiddenFromView(player, vector2))) && (vector == Vector3.Zero || vector2.Z > vector.Z))
			{
				vector = vector2;
			}
		}
		if (!(player == null))
		{
			return vector;
		}
		return result;
	}

	private unsafe (Vector3 pos, float heading, string note)? RoadAhead(Ped player)
	{
		Vector3 position = player.Position;
		Vector3 vector = Forward(player);
		double num = Math.Atan2(vector.Y, vector.X);
		float[] array = new float[4] { 120f, 100f, 145f, 160f };
		float[] array2 = new float[7] { 0f, 25f, -25f, 50f, -50f, 80f, -80f };
		(Vector3, float, string)? tuple = null;
		ulong* ptr = stackalloc ulong[3];
		ulong* ptr2 = stackalloc ulong[1];
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		float[] array3 = array2;
		foreach (float num6 in array3)
		{
			float[] array4 = array;
			foreach (float num7 in array4)
			{
				num2++;
				double num8 = num + (double)num6 * Math.PI / 180.0;
				Vector3 vector2 = position + new Vector3((float)Math.Cos(num8) * num7, (float)Math.Sin(num8) * num7, 0f);
				*ptr = (ptr[1] = (ptr[2] = 0uL));
				*ptr2 = 0uL;
				if (!Function.Call<bool>(2580559617774072660uL, new InputArgument[8]
				{
					vector2.X,
					vector2.Y,
					vector2.Z,
					new InputArgument((ulong)ptr),
					new InputArgument((ulong)ptr2),
					1,
					3f,
					0f
				}))
				{
					num3++;
					continue;
				}
				Vector3 vector3 = new Vector3(*(float*)ptr, *(float*)(ptr + 1), *(float*)(ptr + 2));
				float num9 = vector3.DistanceTo(position);
				if (vector3.DistanceTo(new Vector3(vector2.X, vector2.Y, vector3.Z)) > 45f || num9 < 80f || num9 > 190f)
				{
					num4++;
					continue;
				}
				Vector3 vector4 = Ground(vector3);
				if (Math.Abs(vector4.Z - vector3.Z) > 3f)
				{
					num5++;
					continue;
				}
				float num10 = *(float*)ptr2;
				double num11 = (double)num10 * Math.PI / 180.0;
				Vector3 vector5 = new Vector3(0f - (float)Math.Sin(num11), (float)Math.Cos(num11), 0f);
				Vector3 vector6 = position - vector3;
				vector6.Z = 0f;
				if (vector5.X * vector6.X + vector5.Y * vector6.Y < 0f)
				{
					num10 += 180f;
				}
				(Vector3, float, string) tuple2 = (new Vector3(vector3.X, vector3.Y, vector4.Z + 0.3f), num10, $"road {num9:0} m off, {num6:+0;-0;0} deg from where you face");
				if (HiddenFromView(player, vector3))
				{
					return tuple2;
				}
				tuple = tuple ?? tuple2;
			}
		}
		GrudgeLog(string.Format("ROAD none ahead out of view: {0} points - {1} no node, {2} too far / near, {3} on a ledge{4}", num2, num3, num4, num5, tuple.HasValue ? ", the rest in view" : ""));
		return null;
	}

	private Vector3? RoadNodeNear(Vector3 at, float maxOff)
	{
		float heading;
		return RoadNodeNear(at, maxOff, out heading);
	}

	private unsafe Vector3? RoadNodeNear(Vector3 at, float maxOff, out float heading, int nodeType = 1)
	{
		heading = 0f;
		ulong* ptr = stackalloc ulong[3];
		ulong* ptr2 = stackalloc ulong[1];
		*ptr = (ptr[1] = (ptr[2] = 0uL));
		*ptr2 = 0uL;
		if (!Function.Call<bool>(2580559617774072660uL, new InputArgument[8]
		{
			at.X,
			at.Y,
			at.Z,
			new InputArgument((ulong)ptr),
			new InputArgument((ulong)ptr2),
			nodeType,
			3f,
			0f
		}))
		{
			return null;
		}
		Vector3 value = new Vector3(*(float*)ptr, *(float*)(ptr + 1), *(float*)(ptr + 2));
		heading = *(float*)ptr2;
		if (!(value.DistanceTo(new Vector3(at.X, at.Y, value.Z)) <= maxOff))
		{
			return null;
		}
		return value;
	}

	private void DriveRoadWagon(Ped driver, Vehicle veh, Ped player)
	{
		WagonDrive(driver, veh, _roadWagonTo = RoadNodeNear(player.Position, 40f) ?? WagonGoal(player.Position, 150f) ?? RoadNodeNear(player.Position, 150f) ?? player.Position);
	}

	private void UpdateRoadWagon(Ped player)
	{
		if (_encVehicle == null || !_encVehicle.Exists() || _enc == null || _enc.Spotted || _enc.Engaged || Game.GameTime < _roadWagonCheck)
		{
			return;
		}
		_roadWagonCheck = Game.GameTime + 8000;
		if (_roadWagonWalked)
		{
			if (_roadWagonWalkSent)
			{
				return;
			}
			_roadWagonWalkSent = true;
			{
				foreach (Ped item in from p in new Ped[1] { _activeNemesisPed }.Concat(_followers)
					where p != null && p.Exists() && !p.IsDead && !PED.IS_PED_IN_ANY_VEHICLE(p.Handle, atGetIn: false)
					select p)
				{
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item.Handle, player.Position.X, player.Position.Y, player.Position.Z, 1f, -1, 6f, 0, 0f);
				}
				return;
			}
		}
		int num = VEHICLE.GET_PED_IN_VEHICLE_SEAT(_encVehicle.Handle, -1);
		float num2 = ENTITY.GET_ENTITY_SPEED(_encVehicle.Handle);
		float num3 = _encVehicle.Position.DistanceTo(player.Position);
		bool flag = num2 < 1f;
		if (flag != _roadWagonStill > 0 || (flag && _roadWagonStill == 2))
		{
			GrudgeLog(string.Format("WAGON {0:0} m from you, {1}{2}", num3, flag ? $"stopped ({(_roadWagonStill + 1) * 8} s)" : $"moving {num2:0.0} m/s", (num == 0) ? " - NO DRIVER" : ""));
		}
		_roadWagonStill = (flag ? (_roadWagonStill + 1) : 0);
		if (!flag && _roadWagonTryN > 0)
		{
			WagonRolled("road wagon", _roadWagonTryN, _roadWagonTryWhat);
			_roadWagonTryN = 0;
		}
		if (_roadWagonStill > 3 && num3 > 15f)
		{
			_roadWagonWalked = true;
			foreach (Ped item2 in from p in new Ped[1] { _activeNemesisPed }.Concat(_followers)
				where p != null && p.Exists() && !p.IsDead && PED.IS_PED_IN_VEHICLE(p.Handle, _encVehicle.Handle, atGetIn: false)
				select p)
			{
				Crumbs.Native("road wagon: get down");
				TASK.TASK_LEAVE_ANY_VEHICLE(item2.Handle, 0, 0);
			}
			GrudgeLog($"WAGON stuck {_roadWagonStill * 8} s, {num3:0} m from you - they get down and come on foot");
		}
		else
		{
			if (num == 0 || num3 < 15f)
			{
				return;
			}
			Ped ped = Entity.FromHandle(num) as Ped;
			if (ped == null)
			{
				return;
			}
			if (flag && _roadWagonStill >= 1 && _roadWagonStill <= 3)
			{
				if (_roadWagonTo == Vector3.Zero)
				{
					_roadWagonTo = RoadNodeNear(player.Position, 40f) ?? player.Position;
				}
				Vector3 to = _roadWagonTo;
				bool wandering = _roadWagonWander;
				string text = WagonTry(_roadWagonStill, ped, _encVehicle, ref to, ref wandering);
				_roadWagonWander = wandering;
				_roadWagonTo = to;
				_roadWagonTryN = _roadWagonStill;
				_roadWagonTryWhat = text;
				GrudgeLog($"WAGONDIAG road wagon stopped {_roadWagonStill * 8} s, try {_roadWagonStill}: {text} | {WagonState(_encVehicle, ped)}");
			}
			else if (!flag && !_roadWagonWander)
			{
				Vector3? vector = RoadNodeNear(player.Position, 40f);
				if (vector.HasValue && vector.GetValueOrDefault().DistanceTo(_roadWagonTo) > 25f)
				{
					DriveRoadWagon(ped, _encVehicle, player);
				}
			}
		}
	}

	private bool PutOnWagon(Ped ped, NemesisRecord rec, Vector3 pos, float heading, Ped player)
	{
		string text = ((rec.Wealth == Wealth.Rich || rec.Rank >= 4) ? "stagecoach001x" : RoadWagonModels[_rng.Next(RoadWagonModels.Length)]);
		uint model = MISC.GET_HASH_KEY(text);
		if (!STREAMING.IS_MODEL_IN_CDIMAGE(model))
		{
			GrudgeLog("WAGON " + text + " not in the game files");
			return false;
		}
		Vehicle vehicle = SpawnVehicle((VehicleHash)model, pos, heading);
		if (vehicle == null)
		{
			GrudgeLog("WAGON could not create " + text);
			return false;
		}
		MakeOurs(vehicle.Handle);
		VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle.Handle, p1: false);
		_encVehicle = vehicle;
		PED.SET_PED_INTO_VEHICLE(ped.Handle, vehicle.Handle, -1);
		int num = 0;
		foreach (Ped item in _followers.Where((Ped f) => f.Exists()).Take((text == "stagecoach001x") ? 5 : 2))
		{
			PED.SET_PED_INTO_VEHICLE(item.Handle, vehicle.Handle, num++);
		}
		ped.BlockPermanentEvents = true;
		_roadWagonTo = Vector3.Zero;
		_roadWagonCheck = Game.GameTime + 8000;
		_roadWagonStill = 0;
		_roadWagonWalked = false;
		_roadWagonWalkSent = false;
		_roadWagonTryN = 0;
		_roadWagonTryWhat = null;
		_roadWagonWander = false;
		DriveRoadWagon(ped, vehicle, player);
		GrudgeLog($"WAGON {rec.Name} drives a {text} with {num} men aboard, {pos.DistanceTo(player.Position):0} m ahead - coming your way");
		return true;
	}

	private bool HeadSeesHead(Ped watcher, Ped target)
	{
		Vector3 vector = PED.GET_PED_BONE_COORDS(watcher.Handle, 21030, 0f, 0f, 0f);
		Vector3 to = PED.GET_PED_BONE_COORDS(target.Handle, 21030, 0f, 0f, 0f);
		if (vector.X == 0f && vector.Y == 0f)
		{
			vector = watcher.Position + new Vector3(0f, 0f, 0.7f);
		}
		if (to.X == 0f && to.Y == 0f)
		{
			to = target.Position + new Vector3(0f, 0f, 0.3f);
		}
		return !Ray.Blocked(vector, to, 17, watcher.Handle);
	}
}
