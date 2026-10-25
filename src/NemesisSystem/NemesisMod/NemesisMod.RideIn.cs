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
	private sealed class PlanRide
	{
		public Vector3 To;

		public bool OnFoot;

		public bool Patrol;

		public bool Arrived;

		public bool Settled;

		public string Scenario;

		public int StartedAt;

		public int ArrivedAt;

		public int NextRetask;

		public Vehicle Wagon;

		public int Coffin;

		public Plan Plan;

		public Ped Body;

		public Ped Carrier;

		public int CarryAt;

		public int NextCarryNav;

		public int PickTries;

		public bool Carrying;

		public bool BodyDown;

		public bool Shouldered;

		public int Standing;

		public Vector3 WalkTo;

		public int NextCheck;

		public float LastLeft;

		public Vector3 Road;

		public int WalkStart;
	}

	private PlanRide _rideIn;

	private int _rideInSettleAt;

	private PlanRide _rideInSettle;

	private RivalScene _rival;

	private uint _rivalGroup;

	internal bool RidingIn
	{
		get
		{
			if (_rideIn == null || _rideIn.Arrived)
			{
				if (_proc != null)
				{
					return _proc.Stage < 3;
				}
				return false;
			}
			return true;
		}
	}

	private Vector3 RideInStart(Vector3 dest, Ped player, bool onFoot, bool far = false)
	{
		Vector3 vector = dest - player.Position;
		vector.Z = 0f;
		if (vector.Length() < 1f)
		{
			vector = new Vector3(1f, 0f, 0f);
		}
		double num = Math.Atan2(vector.Y, vector.X);
		for (int i = 0; i < 16; i++)
		{
			float num2 = ((!onFoot) ? (60f + (float)_rng.NextDouble() * 60f) : ((!far) ? (25f + (float)_rng.NextDouble() * 20f) : ((i < 10) ? (25f + (float)_rng.NextDouble() * 15f) : (45f + (float)_rng.NextDouble() * 30f))));
			double num3 = num + (_rng.NextDouble() * 180.0 - 90.0) * Math.PI / 180.0;
			Vector3 vector2 = new Vector3(dest.X + (float)Math.Cos(num3) * num2, dest.Y + (float)Math.Sin(num3) * num2, dest.Z);
			if (OnLand(vector2))
			{
				vector2 = (onFoot ? GroundOnSpot(vector2) : SnapToRoad(vector2, 60f));
				if ((!onFoot || !Raised(vector2)) && !(vector2.DistanceTo(player.Position) < (onFoot ? 25f : 70f)) && !(vector2.DistanceTo(player.Position) > 250f) && HiddenFromView(player, vector2 + new Vector3(0f, 0f, 1.2f)))
				{
					return vector2;
				}
			}
		}
		return Vector3.Zero;
	}

	private List<Ped> EncounterMen()
	{
		return (from p in new Ped[1] { _activeNemesisPed }.Concat(_followers)
			where p != null && p.Exists() && !p.IsDead
			select p).ToList();
	}

	private void BeginRideIn(Plan p, Vector3 dest, bool onFoot, bool patrol, string scenario)
	{
		List<Ped> men = EncounterMen();
		if (men.Count == 0)
		{
			return;
		}
		_settling.RemoveAll(((Ped ped, int checkAt, string fallback, float heading) s) => s.ped != null && men.Any((Ped m) => m.Handle == s.ped.Handle));
		foreach (Ped item in men)
		{
			_settleFrom.Remove(item.Handle);
			TASK.CLEAR_PED_TASKS(item.Handle, p1: false, p2: false);
		}
		PlanRide planRide = new PlanRide
		{
			To = dest,
			OnFoot = onFoot,
			Patrol = patrol,
			Scenario = scenario,
			StartedAt = Game.GameTime,
			Plan = p,
			WalkTo = dest
		};
		if (onFoot)
		{
			Vector3 safeCoordForPed = World.GetSafeCoordForPed(dest, onGround: true, 0);
			if ((safeCoordForPed.X != 0f || safeCoordForPed.Y != 0f) && new Vector3(safeCoordForPed.X - dest.X, safeCoordForPed.Y - dest.Y, 0f).Length() < 10f && Math.Abs(safeCoordForPed.Z - dest.Z) < 4f)
			{
				planRide.WalkTo = safeCoordForPed;
			}
			foreach (Ped item2 in men)
			{
				item2.BlockPermanentEvents = true;
			}
		}
		if (p.Kind == "funeral")
		{
			StartBodyCarry(p, men, planRide);
		}
		if (!onFoot)
		{
			foreach (Ped item3 in men)
			{
				if (item3.IsOnMount || (planRide.Wagon != null && PED.IS_PED_IN_VEHICLE(item3.Handle, planRide.Wagon.Handle, atGetIn: false)))
				{
					continue;
				}
				Ped ped = MountOnHorse(item3);
				if (planRide.Wagon != null && planRide.Wagon.Exists())
				{
					ENTITY.SET_ENTITY_NO_COLLISION_ENTITY(planRide.Wagon.Handle, item3.Handle, thisFrameOnly: false);
					if (ped != null)
					{
						ENTITY.SET_ENTITY_NO_COLLISION_ENTITY(planRide.Wagon.Handle, ped.Handle, thisFrameOnly: false);
					}
				}
			}
		}
		_rideIn = planRide;
		RideInOrders(force: true);
		GrudgeLog(string.Format("RIDEIN {0}: {1} {2} from {3:0} m out, {4:0} m from you", p.Title, men.Count, onFoot ? "on foot" : ((planRide.Wagon != null) ? "with a wagon" : "riding"), men[0].Position.DistanceTo(dest), men[0].Position.DistanceTo(Game.Player.Character.Position)));
	}

	private unsafe Vehicle FuneralWagon(List<Ped> men, Vector3 dest, PlanRide ri)
	{
		Vector3 vector = SnapToRoad(men[0].Position, 30f);
		string[] array = new string[3] { "wagon04x", "wagon02x", "cart01" };
		foreach (string text in array)
		{
			uint model = MISC.GET_HASH_KEY(text);
			if (!STREAMING.IS_MODEL_IN_CDIMAGE(model) || !STREAMING.IS_MODEL_A_VEHICLE(model))
			{
				continue;
			}
			Vehicle vehicle = SpawnVehicle((VehicleHash)model, vector, HeadingTo(vector, dest));
			if (vehicle == null)
			{
				continue;
			}
			MakeOurs(vehicle.Handle);
			VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle.Handle, p1: false);
			Ped driver = men.Last();
			PED.SET_PED_INTO_VEHICLE(driver.Handle, vehicle.Handle, -1);
			driver.BlockPermanentEvents = true;
			Vector3 forwardVector = vehicle.ForwardVector;
			forwardVector.Z = 0f;
			int num = 0;
			foreach (Ped item in men.Where((Ped m) => m != driver))
			{
				Vector3 vector2 = Ground(vehicle.Position - forwardVector * (9f + 3f * (float)(num / 2)) + new Vector3((num % 2 == 0) ? 1.5f : (-1.5f), 0f, 0f));
				ENTITY.SET_ENTITY_COORDS(item.Handle, vector2.X, vector2.Y, vector2.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
				num++;
			}
			int num2 = MakeCheckpointProp(CoffinModels, vehicle.Position + new Vector3(0f, 0f, 3f), vehicle.Heading, ref _coffinModel, freeze: false);
			if (num2 != 0)
			{
				float num3 = ((text == "cart01") ? (-0.6f) : (-1.3f));
				Vector3 forwardVector2 = vehicle.ForwardVector;
				Vector3 vector3 = vehicle.Position + forwardVector2 * num3;
				float num4 = 0.55f;
				ENTITY.SET_ENTITY_COLLISION(num2, toggle: false, keepPhysics: false);
				if (Ray.Hit(vector3 + new Vector3(0f, 0f, 2.5f), vector3 - new Vector3(0f, 0f, 1.5f), 2, num2, out var at))
				{
					num4 = at.Z - vehicle.Position.Z;
				}
				float num5 = 0f;
				try
				{
					ulong* ptr = stackalloc ulong[4];
					ulong* maximum = stackalloc ulong[4];
					MISC.GET_MODEL_DIMENSIONS(ENTITY.GET_ENTITY_MODEL(num2), (Vector3*)ptr, (Vector3*)maximum);
					num5 = *(float*)(ptr + 2);
					if (num5 < -1.5f || num5 > 0.5f)
					{
						num5 = -0.25f;
					}
				}
				catch
				{
				}
				float num6 = num4 - num5 + 0.02f;
				ENTITY.ATTACH_ENTITY_TO_ENTITY(num2, vehicle.Handle, 0, 0f, num3, num6, 0f, 0f, 0f, p9: false, useSoftPinning: false, collision: false, isPed: false, 2, fixedRot: true, p15: false, p16: false);
				ri.Coffin = num2;
				GrudgeLog($"RIDEIN funeral wagon bed {num4:0.00} m over its origin, the coffin's bottom {num5:0.00} - set at {num6:0.00}");
			}
			GrudgeLog("RIDEIN funeral wagon: " + text + ((num2 != 0) ? (", the coffin (" + _coffinModel + ") on the back") : ", no coffin model"));
			return vehicle;
		}
		GrudgeLog("RIDEIN funeral wagon: none of the wagon models - they ride in");
		return null;
	}

	private void RideInOrders(bool force = false)
	{
		PlanRide ri = _rideIn;
		if (ri == null || ri.Arrived || (!force && Game.GameTime < ri.NextRetask))
		{
			return;
		}
		ri.NextRetask = Game.GameTime + ((ri.Wagon != null) ? 12000 : (ri.OnFoot ? 3000 : 8000));
		Ped activeNemesisPed = _activeNemesisPed;
		if (activeNemesisPed == null || !activeNemesisPed.Exists())
		{
			return;
		}
		Vector3 to = ri.To;
		if (ri.Wagon != null && ri.Wagon.Exists())
		{
			int num = VEHICLE.GET_PED_IN_VEHICLE_SEAT(ri.Wagon.Handle, -1);
			if (ri.Road == Vector3.Zero)
			{
				ri.Road = SnapToRoad(to, 60f);
			}
			Vector3 road = ri.Road;
			bool flag = ENTITY.GET_ENTITY_SPEED(ri.Wagon.Handle) > 0.8f;
			if (num != 0 && (force || !flag))
			{
				TASK.TASK_VEHICLE_DRIVE_TO_COORD(num, ri.Wagon.Handle, road.X, road.Y, road.Z, 5f, 0uL, ENTITY.GET_ENTITY_MODEL(ri.Wagon.Handle), 786603, 4f, 10f);
			}
			if (!force && flag)
			{
				return;
			}
			int num2 = 0;
			{
				foreach (Ped item in EncounterMen())
				{
					if (!PED.IS_PED_IN_VEHICLE(item.Handle, ri.Wagon.Handle, atGetIn: false))
					{
						TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(item.Handle, ri.Wagon.Handle, (num2 % 2 == 0) ? 2f : (-2f), -9f - 3f * (float)(num2 / 2), 0f, 2f, -1, 4f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
						num2++;
					}
				}
				return;
			}
		}
		if (ri.OnFoot)
		{
			bool flag2 = ENTITY.GET_ENTITY_SPEED(activeNemesisPed.Handle) < 0.3f;
			if (!force && !flag2)
			{
				ri.Standing = 0;
				return;
			}
			if (!force)
			{
				ri.Standing++;
			}
			Vector3 walkTo = ri.WalkTo;
			if (ri.Standing >= 2 && ri.Standing % 2 == 0)
			{
				TASK.TASK_GO_STRAIGHT_TO_COORD(activeNemesisPed.Handle, walkTo.X, walkTo.Y, walkTo.Z, 1f, -1, 40000f, 0.5f, 0);
			}
			else
			{
				TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(activeNemesisPed.Handle, walkTo.X, walkTo.Y, walkTo.Z, 1f, -1, 1f, 0, 0f);
			}
			if (ri.Standing == 2 || ri.Standing == 6)
			{
				GrudgeLog(string.Format("RIDEIN {0}: the leader stands - {1} ({2:0} m)", ri.Plan?.Title, (ri.Standing % 2 == 0) ? "straight at it" : "by the navmesh", activeNemesisPed.Position.DistanceTo2D(walkTo)));
			}
		}
		else
		{
			TASK.TASK_GO_TO_COORD_ANY_MEANS(activeNemesisPed.Handle, to.X, to.Y, to.Z, 6f, 0, p6: false, 786603, 0f);
		}
		int num3 = 0;
		foreach (Ped item2 in _followers.Where((Ped f) => f != null && f.Exists() && !f.IsDead && f != ri.Carrier))
		{
			TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(item2.Handle, activeNemesisPed.Handle, (num3 % 2 == 0) ? 1.5f : (-1.5f), -3f - 2f * (float)(num3 / 2), 0f, ri.OnFoot ? 1f : 2f, -1, 2f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
			num3++;
		}
	}

	private void RideInTick()
	{
		PlanRide ri = _rideIn;
		if (ri == null)
		{
			return;
		}
		Ped activeNemesisPed = _activeNemesisPed;
		if (_enc == null || activeNemesisPed == null || !activeNemesisPed.Exists() || activeNemesisPed.IsDead || _enc.Engaged || _enc.StandoffUntil > 0)
		{
			if (ri.Coffin != 0 && _funeral != null)
			{
				_funeral.Props.Add(ri.Coffin);
			}
			if (ri.Wagon != null && _funeral != null)
			{
				_funeral.Wagon = ri.Wagon;
			}
			_rideIn = null;
			return;
		}
		List<Ped> list = EncounterMen();
		if (!ri.Arrived)
		{
			Vector3 vector = ((ri.Wagon != null && ri.Wagon.Exists()) ? ri.Wagon.Position : ((ri.Carrying && ri.Carrier != null && ri.Carrier.Exists()) ? ri.Carrier.Position : activeNemesisPed.Position));
			bool flag = ((!(ri.Wagon != null)) ? (vector.DistanceTo2D(ri.To) < ((ri.Body != null) ? 8f : 12f)) : (vector.DistanceTo2D(ri.To) < 45f || (ri.Road != Vector3.Zero && vector.DistanceTo2D(ri.Road) < 12f)));
			BodyCarryTick(ri);
			bool flag2 = Game.GameTime - ri.StartedAt > 150000;
			if (!flag && !flag2)
			{
				if (Game.GameTime >= ri.NextCheck)
				{
					ri.NextCheck = Game.GameTime + 20000;
					float num = vector.DistanceTo2D(ri.To);
					bool flag3 = ri.LastLeft > 0f && ri.LastLeft - num < 5f && Game.GameTime - ri.StartedAt > 15000;
					GrudgeLog(string.Format("RIDEIN {0}: {1:0} m to go, {2:0} m from you{3}", ri.Plan?.Title, num, vector.DistanceTo(Game.Player.Character.Position), flag3 ? " - stuck" : ""));
					if (flag3 && HiddenFromView(Game.Player.Character, vector + new Vector3(0f, 0f, 1.5f)))
					{
						Vector3 vector2 = ri.To - vector;
						vector2.Z = 0f;
						float num2 = vector2.Length();
						if (num2 > 1f)
						{
							vector2 *= 1f / num2;
						}
						Vector3 vector3 = SnapToRoad(vector + vector2 * Math.Min(25f, Math.Max(0f, num2 - 15f)), 20f);
						if (!HiddenFromView(Game.Player.Character, vector3 + new Vector3(0f, 0f, 1.5f)))
						{
							vector3 = vector;
						}
						if (vector3 != vector)
						{
							if (ri.Wagon != null && ri.Wagon.Exists())
							{
								ENTITY.SET_ENTITY_COORDS(ri.Wagon.Handle, vector3.X, vector3.Y, vector3.Z + 0.3f, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
								VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(ri.Wagon.Handle, p1: false);
							}
							foreach (Ped item in list.Where((Ped m) => !(ri.Wagon != null) || !PED.IS_PED_IN_VEHICLE(m.Handle, ri.Wagon.Handle, atGetIn: false)))
							{
								int num3 = PED.GET_MOUNT(item.Handle);
								Vector3 vector4 = new Vector3((float)(_rng.NextDouble() * 6.0 - 3.0), (float)(_rng.NextDouble() * 6.0 - 3.0), 0f);
								ENTITY.SET_ENTITY_COORDS((num3 != 0) ? num3 : item.Handle, vector3.X + vector4.X, vector3.Y + vector4.Y, vector3.Z + 0.3f, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
							}
							GrudgeLog($"RIDEIN {ri.Plan?.Title}: moved along {vector3.DistanceTo(vector):0} m (out of your sight)");
							RideInOrders(force: true);
						}
					}
					ri.LastLeft = num;
				}
				RideInOrders();
				return;
			}
			ri.Arrived = true;
			ri.ArrivedAt = Game.GameTime;
			SetBodyDown(ri);
			if (!ri.Patrol)
			{
				foreach (Ped item2 in list)
				{
					if (ri.Wagon != null && PED.IS_PED_IN_VEHICLE(item2.Handle, ri.Wagon.Handle, atGetIn: false))
					{
						TASK.TASK_LEAVE_ANY_VEHICLE(item2.Handle, 0, 0);
					}
					else if (item2.IsOnMount)
					{
						TASK.TASK_DISMOUNT_ANIMAL(item2.Handle, 0, 0uL, 0uL, 0uL, 0);
					}
				}
			}
			GrudgeLog(string.Format("RIDEIN {0}: {1} after {2} s", ri.Plan?.Title, flag ? "there" : "out of time - here will do", (Game.GameTime - ri.StartedAt) / 1000));
			if (ri.Plan != null && ri.Plan.Known && ri.Plan.Kind != "trap" && Game.Player.Character.Position.DistanceTo(ri.To) < 200f)
			{
				GrudgeNotice("There they are: " + ri.Plan.Title + ".");
			}
		}
		else
		{
			if (ri.Settled || Game.GameTime - ri.ArrivedAt < 3500)
			{
				return;
			}
			ri.Settled = true;
			if (ri.Wagon != null && _funeral != null)
			{
				_funeral.Wagon = ri.Wagon;
			}
			if (ri.Patrol)
			{
				SetAtEase(activeNemesisPed, _followers.Where((Ped f) => f.Exists() && !f.IsDead).ToList(), ri.To, patrol: true);
			}
			else
			{
				foreach (Ped item3 in list.Where((Ped m) => !(m == ri.Carrier) || !PED.IS_PED_CARRYING_SOMETHING(m.Handle)))
				{
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item3.Handle, ri.WalkTo.X + (float)(_rng.NextDouble() * 4.0 - 2.0), ri.WalkTo.Y + (float)(_rng.NextDouble() * 4.0 - 2.0), ri.WalkTo.Z, 1f, -1, 1.5f, 0, 0f);
				}
				ri.WalkStart = Game.GameTime;
				_rideInSettleAt = Game.GameTime + 2000;
				_rideInSettle = ri;
			}
			GrudgeLog("RIDEIN " + ri.Plan?.Title + ": settling in (" + (ri.Scenario ?? (ri.Patrol ? "on the move" : "at ease")) + ")");
		}
	}

	private void StartBodyCarry(Plan p, List<Ped> men, PlanRide ri)
	{
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Dead);
		Ped ped = ((men.Count >= 2) ? men.Last() : men[0]);
		uint hash = (uint)(((int?)nemesisRecord?.ModelHash) ?? ped.Model.Hash);
		Model model = new Model((int)hash);
		if (!model.IsValid || !model.IsPed)
		{
			model = ped.Model;
		}
		Vector3 p2 = ped.Position + ped.ForwardVector * 1.2f;
		Ped ped2 = SpawnPed((PedHash)model.Hash, Ground(p2), ped.Heading);
		if (ped2 == null)
		{
			GrudgeLog("RIDEIN body: the dead man's body couldn't be made - they walk in without him");
			return;
		}
		MakeOurs(ped2.Handle);
		if (nemesisRecord != null)
		{
			try
			{
				ApplyScars(ped2, nemesisRecord);
			}
			catch
			{
			}
		}
		KlanBody(ped2, nemesisRecord?.Gang ?? p.Gang);
		ENTITY.SET_ENTITY_HEALTH(ped2.Handle, 0, 0);
		ri.Body = ped2;
		ri.Carrier = ped;
		ri.CarryAt = Game.GameTime;
		ped.BlockPermanentEvents = true;
		GrudgeLog("RIDEIN body: " + (nemesisRecord?.Name ?? "the dead man") + " made, " + WhoIs(ped.Handle) + " takes him up");
	}

	private bool HoldsBody(PlanRide ri)
	{
		if (!ri.Shouldered && !ENTITY.IS_ENTITY_ATTACHED_TO_ENTITY(ri.Body.Handle, ri.Carrier.Handle))
		{
			return PED._GET_CARRIER_AS_PED(ri.Body.Handle) == ri.Carrier.Handle;
		}
		return true;
	}

	private void ShoulderBody(PlanRide ri)
	{
		int num = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(ri.Carrier.Handle, "SKEL_R_Clavicle");
		if (num < 0)
		{
			num = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(ri.Carrier.Handle, "SKEL_Spine3");
		}
		ENTITY.SET_ENTITY_NO_COLLISION_ENTITY(ri.Body.Handle, ri.Carrier.Handle, thisFrameOnly: false);
		ENTITY.ATTACH_ENTITY_TO_ENTITY(ri.Body.Handle, ri.Carrier.Handle, Math.Max(0, num), 0.15f, 0.05f, 0f, 0f, 90f, 90f, p9: false, useSoftPinning: false, collision: false, isPed: false, 2, fixedRot: true, p15: false, p16: false);
		ri.Shouldered = true;
		ri.Carrying = true;
		TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ri.Carrier.Handle, ri.WalkTo.X, ri.WalkTo.Y, ri.WalkTo.Z, 1f, -1, 2f, 0, 0f);
		GrudgeLog($"RIDEIN body: the game's carry didn't take after {ri.PickTries} tries (carriable {TASK.GET_IS_CARRIABLE_ENTITY(ri.Body.Handle)}) - over his shoulder by hand (bone {num})");
	}

	private void BodyCarryTick(PlanRide ri)
	{
		if (ri.Body == null || ri.BodyDown || ri.Carrier == null || !ri.Carrier.Exists() || ri.Carrier.IsDead)
		{
			return;
		}
		bool flag = HoldsBody(ri);
		Vector3 walkTo = ri.WalkTo;
		if (flag && ri.Carrying && ENTITY.GET_ENTITY_SPEED(ri.Carrier.Handle) < 0.3f && Game.GameTime >= ri.NextCarryNav)
		{
			ri.NextCarryNav = Game.GameTime + 3000;
			ri.Standing++;
			if (ri.Standing % 2 == 0)
			{
				TASK.TASK_GO_STRAIGHT_TO_COORD(ri.Carrier.Handle, walkTo.X, walkTo.Y, walkTo.Z, 1f, -1, 40000f, 0.5f, 0);
			}
			else
			{
				TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ri.Carrier.Handle, walkTo.X, walkTo.Y, walkTo.Z, 1f, -1, 2f, 0, 0f);
			}
		}
		if (flag && !ri.Carrying)
		{
			ri.Carrying = true;
			ri.NextCarryNav = Game.GameTime + 3000;
			TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ri.Carrier.Handle, walkTo.X, walkTo.Y, walkTo.Z, 1f, -1, 2f, 0, 0f);
			GrudgeLog($"RIDEIN body: carried over the shoulder (the game's carry), {Game.GameTime - ri.CarryAt} ms to take him up");
		}
		else if (!flag && !ri.Carrying)
		{
			int num = Game.GameTime - ri.CarryAt;
			if ((ri.PickTries == 0 && num > 1500) || (ri.PickTries == 1 && num > 4500))
			{
				ri.PickTries++;
				TASK.CLEAR_PED_TASKS(ri.Carrier.Handle, p1: false, p2: false);
				TASK.TASK_PICKUP_CARRIABLE_ENTITY(ri.Carrier.Handle, ri.Body.Handle);
			}
			else if (num > 8000)
			{
				ShoulderBody(ri);
			}
		}
		else if (ri.Carrying && !flag)
		{
			ri.Carrying = false;
			ri.CarryAt = Game.GameTime;
			ri.PickTries = 0;
			GrudgeLog("RIDEIN body: dropped on the way - taken up again");
		}
	}

	private void SetBodyDown(PlanRide ri)
	{
		if (ri?.Body == null || ri.BodyDown || !ri.Body.Exists() || ri.Carrier == null || !ri.Carrier.Exists())
		{
			LayBodyDown(ri, "at the grave");
		}
		else if (!ri.Shouldered && PED.IS_PED_CARRYING_SOMETHING(ri.Carrier.Handle))
		{
			Vector3 vector = GroundOnSpot(ri.To + new Vector3(0.9f, -1f, 0f));
			ri.BodyDown = true;
			TASK.TASK_PLACE_CARRIED_ENTITY_AT_COORD(ri.Carrier.Handle, ri.Body.Handle, vector.X, vector.Y, vector.Z, 1f, 0);
			GrudgeLog("RIDEIN body: set down by the grave (the carrier lays him down)");
		}
		else
		{
			LayBodyDown(ri, ri.Shouldered ? "off his shoulder" : "at the grave");
		}
	}

	private void LayBodyDown(PlanRide ri, string how)
	{
		if (ri?.Body == null || !ri.Body.Exists())
		{
			return;
		}
		ri.BodyDown = true;
		if (ri.Carrier != null && ri.Carrier.Exists())
		{
			if (PED.IS_PED_CARRYING_SOMETHING(ri.Carrier.Handle))
			{
				PED.DETACH_CARRIABLE_ENTITY(ri.Body.Handle, p1: true, p2: true);
			}
			ENTITY.DETACH_ENTITY(ri.Body.Handle, p1: true, collision: true);
			ri.Shouldered = false;
		}
		Vector3 vector = GroundOnSpot(ri.To + new Vector3(0.9f, -1f, 0f));
		ENTITY.SET_ENTITY_COORDS(ri.Body.Handle, vector.X, vector.Y, vector.Z + 0.1f, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
		GrudgeLog($"RIDEIN body: set down by the grave ({how}) at z {vector.Z:0.0} (spot z {ri.To.Z:0.0})");
	}

	private void RideInSettleTick()
	{
		PlanRide rideInSettle = _rideInSettle;
		if (rideInSettle == null || Game.GameTime < _rideInSettleAt)
		{
			return;
		}
		Ped activeNemesisPed = _activeNemesisPed;
		if (_enc == null || activeNemesisPed == null || !activeNemesisPed.Exists() || _enc.Engaged || _enc.StandoffUntil > 0)
		{
			_rideInSettle = null;
			if (_rideIn == rideInSettle)
			{
				_rideIn = null;
			}
			return;
		}
		if (activeNemesisPed.Position.DistanceTo2D(rideInSettle.To) > 6f && Game.GameTime - rideInSettle.WalkStart < 30000)
		{
			_rideInSettleAt = Game.GameTime + 1000;
			if (Game.GameTime < rideInSettle.NextRetask)
			{
				return;
			}
			rideInSettle.NextRetask = Game.GameTime + 3000;
			{
				foreach (Ped item in EncounterMen())
				{
					if (item.IsOnMount)
					{
						TASK.TASK_DISMOUNT_ANIMAL(item.Handle, 0, 0uL, 0uL, 0uL, 0);
					}
					else if (PED.IS_PED_IN_ANY_VEHICLE(item.Handle, atGetIn: false))
					{
						TASK.TASK_LEAVE_ANY_VEHICLE(item.Handle, 0, 0);
					}
					else if (!(item.Position.DistanceTo2D(rideInSettle.To) < 5f) && (!(item == rideInSettle.Carrier) || !PED.IS_PED_CARRYING_SOMETHING(item.Handle)))
					{
						TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(item.Handle, rideInSettle.WalkTo.X + (float)(_rng.NextDouble() * 4.0 - 2.0), rideInSettle.WalkTo.Y + (float)(_rng.NextDouble() * 4.0 - 2.0), rideInSettle.WalkTo.Z, 1f, -1, 1.5f, 0, 0f);
					}
				}
				return;
			}
		}
		_rideInSettle = null;
		if (_rideIn == rideInSettle)
		{
			_rideIn = null;
		}
		if (rideInSettle.Body != null && rideInSettle.Body.Exists() && rideInSettle.Carrier != null && rideInSettle.Carrier.Exists() && (rideInSettle.Shouldered || PED.IS_PED_CARRYING_SOMETHING(rideInSettle.Carrier.Handle)))
		{
			LayBodyDown(rideInSettle, "still on his shoulder at the settle");
		}
		if (rideInSettle.OnFoot)
		{
			foreach (Ped item2 in EncounterMen())
			{
				item2.BlockPermanentEvents = false;
			}
		}
		if (rideInSettle.Coffin != 0 && ENTITY.DOES_ENTITY_EXIST(rideInSettle.Coffin))
		{
			ENTITY.DETACH_ENTITY(rideInSettle.Coffin, p1: false, collision: false);
			Vector3 vector = Ground(rideInSettle.To + new Vector3(0.8f, -1.2f, 0f));
			ENTITY.SET_ENTITY_COORDS(rideInSettle.Coffin, vector.X, vector.Y, vector.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
			ENTITY.SET_ENTITY_COLLISION(rideInSettle.Coffin, toggle: true, keepPhysics: true);
			OBJECT.PLACE_OBJECT_ON_GROUND_PROPERLY(rideInSettle.Coffin, p1: false);
			ENTITY.FREEZE_ENTITY_POSITION(rideInSettle.Coffin, toggle: true);
			if (_funeral != null && !_funeral.Props.Contains(rideInSettle.Coffin))
			{
				_funeral.Props.Add(rideInSettle.Coffin);
			}
			GrudgeLog("RIDEIN " + rideInSettle.Plan?.Title + ": the coffin set down by the grave");
		}
		SetAtEase(activeNemesisPed, _followers.Where((Ped f) => f.Exists() && !f.IsDead).ToList(), rideInSettle.To, patrol: false, rideInSettle.Scenario);
	}
}
