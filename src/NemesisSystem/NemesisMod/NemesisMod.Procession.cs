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
	private sealed class Procession
	{
		public Plan Plan;

		public Vector3 Grave;

		public Vector3 Road;

		public Vector3 BodyAt;

		public string Scenario;

		public Ped Body;

		public Ped Carrier;

		public Ped Horse;

		public int Stage;

		public int StartedAt;

		public int StageAt;

		public int NextOrder;

		public int NextCheck;

		public int LoadAt;

		public int CheckAt;

		public int PickAt;

		public int PlaceAt;

		public int Standing;

		public float LastLeft;

		public bool LoadTried;

		public bool OnHorse;

		public bool ByHand;

		public bool Picked;

		public bool Shouldered;

		public bool Placing;

		public bool BodyDown;

		public bool PickRetried;

		public readonly Dictionary<int, Vector3> Ring = new Dictionary<int, Vector3>();
	}

	private const int LieProofMargin = 50;

	private static readonly HashSet<string> BigIntel = new HashSet<string>
	{
		"camp", "hideout", "treasury", "supplies", "orders", "ride", "alone", "habit", "shakeup", "worstgrudge",
		"people", "catch", "news", "holedup", "stores"
	};

	private void BeginProcession(Plan p, Vector3 grave, string scenario)
	{
		List<Ped> men = EncounterMen();
		if (men.Count == 0)
		{
			return;
		}
		_settling.RemoveAll(((Ped ped, int checkAt, string fallback, float heading) s) => s.ped != null && men.Any((Ped m) => m.Handle == s.ped.Handle));
		Procession procession = new Procession
		{
			Plan = p,
			Grave = grave,
			Scenario = (scenario ?? "WORLD_HUMAN_GUARD_LAZY"),
			StartedAt = Game.GameTime,
			StageAt = Game.GameTime
		};
		procession.Road = SnapToRoad(grave, 60f);
		if (procession.Road == Vector3.Zero || procession.Road.DistanceTo2D(grave) > 60f)
		{
			procession.Road = grave;
		}
		procession.BodyAt = GroundOnSpot(grave + new Vector3(0.9f, -1f, 0f));
		foreach (Ped item in men)
		{
			_settleFrom.Remove(item.Handle);
			ClaimBack(item);
			item.BlockPermanentEvents = true;
			TASK.CLEAR_PED_TASKS(item.Handle, p1: false, p2: false);
			if (!item.IsOnMount)
			{
				MountOnHorse(item);
			}
		}
		procession.Carrier = ((men.Count >= 2) ? men.Last() : men[0]);
		int num = PED.GET_MOUNT(procession.Carrier.Handle);
		procession.Horse = ((num != 0) ? new Ped(num) : null);
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Dead);
		Model model = ((nemesisRecord != null) ? new Model((int)nemesisRecord.ModelHash) : ((Model)null));
		if (model == null || !model.IsValid || !model.IsPed)
		{
			model = procession.Carrier.Model;
		}
		Vector3 pos = GroundOnSpot(procession.Carrier.Position - procession.Carrier.ForwardVector * 2.5f + new Vector3(1.5f, 0f, 0f));
		Ped ped = SpawnPed((PedHash)model.Hash, pos, procession.Carrier.Heading);
		if (ped != null)
		{
			MakeOurs(ped.Handle);
			if (nemesisRecord != null)
			{
				try
				{
					ApplyScars(ped, nemesisRecord);
				}
				catch
				{
				}
			}
			KlanBody(ped, nemesisRecord?.Gang ?? p.Gang);
			ENTITY.SET_ENTITY_HEALTH(ped.Handle, 0, 0);
			procession.Body = ped;
			procession.LoadAt = Game.GameTime + 1200;
			if (procession.Horse != null)
			{
				ENTITY.SET_ENTITY_NO_COLLISION_ENTITY(ped.Handle, procession.Horse.Handle, thisFrameOnly: false);
				ENTITY.SET_ENTITY_NO_COLLISION_ENTITY(ped.Handle, procession.Carrier.Handle, thisFrameOnly: false);
			}
		}
		_proc = procession;
		ProcessionOrders(force: true);
		GrudgeLog(string.Format("PROCESSION {0}: {1} riding in at a walk from {2:0} m out ({3:0} m from you), the road point {4:0} m from the grave (z {5:0.0}){6}", p.Title, men.Count, men[0].Position.DistanceTo(grave), men[0].Position.DistanceTo(Game.Player.Character.Position), procession.Road.DistanceTo2D(grave), grave.Z, (ped == null) ? " - the body couldn't be made" : ""));
	}

	private void ProcessionOrders(bool force = false)
	{
		Procession proc = _proc;
		if (proc == null || proc.Stage != 0 || (!force && Game.GameTime < proc.NextOrder))
		{
			return;
		}
		proc.NextOrder = Game.GameTime + 3000;
		Ped activeNemesisPed = _activeNemesisPed;
		if (activeNemesisPed == null || !activeNemesisPed.Exists())
		{
			return;
		}
		Vector3 road = proc.Road;
		bool flag = ENTITY.GET_ENTITY_SPEED(activeNemesisPed.Handle) < 0.5f;
		if (force || flag)
		{
			if (!force)
			{
				proc.Standing++;
			}
			TASK.TASK_GO_TO_COORD_ANY_MEANS(activeNemesisPed.Handle, road.X, road.Y, road.Z, 2.2f, 0, p6: false, 786603, 0f);
			if (proc.Standing == 3)
			{
				GrudgeLog($"PROCESSION {proc.Plan?.Title}: the leader keeps stopping ({activeNemesisPed.Position.DistanceTo2D(road):0} m to go) - told again");
			}
		}
		int num = 0;
		foreach (Ped item in _followers.Where((Ped f) => f != null && f.Exists() && !f.IsDead))
		{
			if (force || ENTITY.GET_ENTITY_SPEED(item.Handle) < 0.5f)
			{
				TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(item.Handle, activeNemesisPed.Handle, (num % 2 == 0) ? 1.2f : (-1.2f), -4f - 3.5f * (float)(num / 2), 0f, 2.2f, -1, 2.5f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
			}
			num++;
		}
	}

	private void ProcessionTick()
	{
		Procession proc = _proc;
		if (proc == null)
		{
			return;
		}
		Ped activeNemesisPed = _activeNemesisPed;
		List<Ped> list = EncounterMen();
		if (_enc == null || activeNemesisPed == null || !activeNemesisPed.Exists() || activeNemesisPed.IsDead || _enc.Engaged || _enc.StandoffUntil > 0)
		{
			if (proc.Body != null && proc.Body.Exists() && (proc.ByHand || proc.Shouldered))
			{
				ENTITY.DETACH_ENTITY(proc.Body.Handle, p1: true, collision: true);
			}
			foreach (Ped item in list)
			{
				item.BlockPermanentEvents = false;
			}
			GrudgeLog($"PROCESSION {proc.Plan?.Title}: interrupted at stage {proc.Stage} - the encounter's rules now");
			_proc = null;
			return;
		}
		LoadBodyOnHorse(proc);
		switch (proc.Stage)
		{
		case 0:
			RideInStage(proc, activeNemesisPed, list);
			break;
		case 1:
			if (!list.Any((Ped m) => m.IsOnMount) || Game.GameTime - proc.StageAt >= 6000)
			{
				StartWalkIn(proc, list);
			}
			break;
		case 2:
			WalkInStage(proc, list);
			break;
		}
	}

	private void LoadBodyOnHorse(Procession pr)
	{
		if (pr.Body == null || !pr.Body.Exists() || pr.Stage != 0 || pr.OnHorse || pr.ByHand || Game.GameTime < pr.LoadAt)
		{
			return;
		}
		if (pr.Horse == null || !pr.Horse.Exists())
		{
			pr.LoadAt = int.MaxValue;
			return;
		}
		if (!pr.LoadTried)
		{
			pr.LoadTried = true;
			pr.CheckAt = Game.GameTime + 1500;
			uint carryConfig = 0u;
			try
			{
				carryConfig = Function.Call<uint>(3814558401569769835uL, new InputArgument[2]
				{
					pr.Body.Handle,
					0
				});
			}
			catch
			{
			}
			try
			{
				TASK.TASK_CARRIABLE(pr.Body.Handle, carryConfig, pr.Horse.Handle, 24, 512);
				return;
			}
			catch (Exception ex)
			{
				GrudgeLog("PROCESSION body onto the horse (game's carry) failed: " + ex.Message);
				return;
			}
		}
		if (Game.GameTime < pr.CheckAt)
		{
			return;
		}
		if (PED._GET_CARRIER_AS_MOUNT(pr.Body.Handle) == pr.Horse.Handle || ENTITY.IS_ENTITY_ATTACHED_TO_ENTITY(pr.Body.Handle, pr.Horse.Handle))
		{
			pr.OnHorse = true;
			GrudgeLog("PROCESSION body: across the horse's back (the game's carry)");
			return;
		}
		int num = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(pr.Horse.Handle, "SKEL_Spine3");
		if (num < 0)
		{
			num = 0;
		}
		ENTITY.ATTACH_ENTITY_TO_ENTITY(pr.Body.Handle, pr.Horse.Handle, num, 0f, (num == 0) ? (-0.45f) : 0f, (num == 0) ? 0.75f : 0.35f, 0f, 0f, 90f, p9: false, useSoftPinning: false, collision: false, isPed: false, 2, fixedRot: true, p15: false, p16: false);
		pr.ByHand = true;
		GrudgeLog($"PROCESSION body: the game's carry onto the horse didn't take (carriable {TASK.GET_IS_CARRIABLE_ENTITY(pr.Body.Handle)}) - tied across its back by hand (bone {num})");
	}

	private void RideInStage(Procession pr, Ped lead, List<Ped> men)
	{
		Vector3 position = lead.Position;
		bool flag = position.DistanceTo2D(pr.Road) < 10f || position.DistanceTo2D(pr.Grave) < 25f;
		bool flag2 = Game.GameTime - pr.StartedAt > 200000;
		if (!flag && !flag2)
		{
			if (Game.GameTime >= pr.NextCheck)
			{
				pr.NextCheck = Game.GameTime + 20000;
				float num = position.DistanceTo2D(pr.Road);
				bool flag3 = pr.LastLeft > 0f && pr.LastLeft - num < 5f && Game.GameTime - pr.StartedAt > 15000;
				GrudgeLog(string.Format("PROCESSION {0}: {1:0} m to go, {2:0} m from you, {3:0.0} m/s{4}", pr.Plan?.Title, num, position.DistanceTo(Game.Player.Character.Position), ENTITY.GET_ENTITY_SPEED(lead.Handle), flag3 ? " - stuck" : ""));
				if (flag3 && HiddenFromView(Game.Player.Character, position + new Vector3(0f, 0f, 1.5f)))
				{
					Vector3 vector = pr.Road - position;
					vector.Z = 0f;
					float num2 = vector.Length();
					if (num2 > 1f)
					{
						vector *= 1f / num2;
					}
					Vector3 vector2 = SnapToRoad(position + vector * Math.Min(25f, Math.Max(0f, num2 - 10f)), 20f);
					if (vector2 != Vector3.Zero && HiddenFromView(Game.Player.Character, vector2 + new Vector3(0f, 0f, 1.5f)))
					{
						int num3 = 0;
						foreach (Ped man in men)
						{
							int num4 = PED.GET_MOUNT(man.Handle);
							Vector3 vector3 = vector2 - vector * (4f * (float)num3);
							num3++;
							ENTITY.SET_ENTITY_COORDS((num4 != 0) ? num4 : man.Handle, vector3.X, vector3.Y, vector3.Z + 0.3f, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
						}
						GrudgeLog($"PROCESSION {pr.Plan?.Title}: moved along {vector2.DistanceTo(position):0} m (out of your sight)");
						ProcessionOrders(force: true);
					}
				}
				pr.LastLeft = num;
			}
			ProcessionOrders();
			return;
		}
		pr.Stage = 1;
		pr.StageAt = Game.GameTime;
		foreach (Ped man2 in men)
		{
			if (man2.IsOnMount)
			{
				TASK.TASK_DISMOUNT_ANIMAL(man2.Handle, 0, 0uL, 0uL, 0uL, 0);
			}
		}
		GrudgeLog(string.Format("PROCESSION {0}: {1} after {2} s, {3:0} m from the grave", pr.Plan?.Title, flag ? "there" : "out of time - they get down here", (Game.GameTime - pr.StartedAt) / 1000, position.DistanceTo2D(pr.Grave)));
		if (pr.Plan != null && pr.Plan.Known && Game.Player.Character.Position.DistanceTo(pr.Grave) < 200f)
		{
			GrudgeNotice("There they are: " + pr.Plan.Title + ".");
		}
	}

	private void StartWalkIn(Procession pr, List<Ped> men)
	{
		pr.Stage = 2;
		pr.StageAt = Game.GameTime;
		pr.PickAt = Game.GameTime;
		double num = _rng.NextDouble() * Math.PI * 2.0;
		for (int i = 0; i < men.Count; i++)
		{
			double num2 = num + (double)i * (Math.PI * 2.0 / (double)Math.Max(3, men.Count));
			Vector3 vector = GroundOnSpot(pr.BodyAt + new Vector3((float)Math.Cos(num2) * 2.6f, (float)Math.Sin(num2) * 2.6f, 0f));
			pr.Ring[men[i].Handle] = vector;
			if (men[i] != pr.Carrier || pr.Body == null)
			{
				WalkTo(men[i], vector, straight: false);
			}
		}
		if (pr.Body == null || !pr.Body.Exists())
		{
			pr.BodyDown = true;
			return;
		}
		if (pr.ByHand)
		{
			ENTITY.DETACH_ENTITY(pr.Body.Handle, p1: true, collision: true);
		}
		TASK.CLEAR_PED_TASKS(pr.Carrier.Handle, p1: false, p2: false);
		TASK.TASK_PICKUP_CARRIABLE_ENTITY(pr.Carrier.Handle, pr.Body.Handle);
		GrudgeLog(string.Format("PROCESSION {0}: off their horses - {1} takes the body down ({2}), {3} walk in", pr.Plan?.Title, WhoIs(pr.Carrier.Handle), pr.OnHorse ? "from the horse" : (pr.ByHand ? "untied" : "off the ground"), men.Count - 1));
	}

	private static void WalkTo(Ped m, Vector3 p, bool straight)
	{
		if (straight)
		{
			TASK.TASK_GO_STRAIGHT_TO_COORD(m.Handle, p.X, p.Y, p.Z, 1f, -1, 40000f, 0.3f, 0);
		}
		else
		{
			TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(m.Handle, p.X, p.Y, p.Z, 1f, -1, 0.6f, 0, 0f);
		}
	}

	private bool CarrierHolds(Procession pr)
	{
		if (!pr.Shouldered && !PED.IS_PED_CARRYING_SOMETHING(pr.Carrier.Handle))
		{
			return PED._GET_CARRIER_AS_PED(pr.Body.Handle) == pr.Carrier.Handle;
		}
		return true;
	}

	private void WalkInStage(Procession pr, List<Ped> men)
	{
		bool flag = Game.GameTime >= pr.NextOrder;
		if (flag)
		{
			pr.NextOrder = Game.GameTime + 3000;
			pr.Standing++;
		}
		bool straight = pr.Standing % 2 == 0;
		Ped carrier = pr.Carrier;
		if (!pr.BodyDown && carrier != null && carrier.Exists() && !carrier.IsDead && pr.Body != null && pr.Body.Exists())
		{
			bool flag2 = CarrierHolds(pr);
			if (!pr.Picked)
			{
				int num = Game.GameTime - pr.PickAt;
				if (flag2)
				{
					pr.Picked = true;
					WalkTo(carrier, pr.BodyAt, straight: false);
					GrudgeLog($"PROCESSION body: taken up (the game's carry) after {num} ms - carried in");
				}
				else if (!pr.PickRetried && num > 3500)
				{
					pr.PickRetried = true;
					TASK.CLEAR_PED_TASKS(carrier.Handle, p1: false, p2: false);
					TASK.TASK_PICKUP_CARRIABLE_ENTITY(carrier.Handle, pr.Body.Handle);
				}
				else if (num > 7000)
				{
					int num2 = ENTITY.GET_ENTITY_BONE_INDEX_BY_NAME(carrier.Handle, "SKEL_R_Clavicle");
					ENTITY.ATTACH_ENTITY_TO_ENTITY(pr.Body.Handle, carrier.Handle, Math.Max(0, num2), 0.15f, 0.05f, 0f, 0f, 90f, 90f, p9: false, useSoftPinning: false, collision: false, isPed: false, 2, fixedRot: true, p15: false, p16: false);
					pr.Shouldered = true;
					pr.Picked = true;
					TASK.CLEAR_PED_TASKS(carrier.Handle, p1: false, p2: false);
					WalkTo(carrier, pr.BodyAt, straight: false);
					GrudgeLog($"PROCESSION body: the game's pick-up didn't take - over his shoulder by hand (bone {num2})");
				}
			}
			else if (!pr.Placing)
			{
				if (carrier.Position.DistanceTo2D(pr.BodyAt) < 1.8f)
				{
					pr.Placing = true;
					pr.PlaceAt = Game.GameTime;
					if (!pr.Shouldered && flag2)
					{
						TASK.TASK_PLACE_CARRIED_ENTITY_AT_COORD(carrier.Handle, pr.Body.Handle, pr.BodyAt.X, pr.BodyAt.Y, pr.BodyAt.Z, 1f, 0);
					}
					else
					{
						LayBodyAt(pr, "off his shoulder");
					}
				}
				else if (flag && ENTITY.GET_ENTITY_SPEED(carrier.Handle) < 0.3f)
				{
					WalkTo(carrier, pr.BodyAt, straight);
				}
				if (!flag2 && !pr.Placing)
				{
					pr.Picked = false;
					pr.PickRetried = false;
					pr.PickAt = Game.GameTime;
					TASK.TASK_PICKUP_CARRIABLE_ENTITY(carrier.Handle, pr.Body.Handle);
					GrudgeLog("PROCESSION body: dropped on the way - taken up again");
				}
			}
			else if (!flag2)
			{
				pr.BodyDown = true;
				GrudgeLog("PROCESSION body: laid down by the grave (the carrier set him down)");
				WalkTo(carrier, pr.Ring.TryGetValue(carrier.Handle, out var value) ? value : pr.BodyAt, straight: false);
			}
			else if (Game.GameTime - pr.PlaceAt > 6000)
			{
				LayBodyAt(pr, "the setting down didn't finish");
			}
		}
		else if (!pr.BodyDown)
		{
			LayBodyAt(pr, "the carrier was gone");
		}
		bool flag3 = true;
		foreach (Ped man in men)
		{
			Vector3 value2;
			if (man == carrier && !pr.BodyDown)
			{
				flag3 = false;
			}
			else if (pr.Ring.TryGetValue(man.Handle, out value2) && !(man.Position.DistanceTo2D(value2) < 1.3f))
			{
				flag3 = false;
				if (flag && ENTITY.GET_ENTITY_SPEED(man.Handle) < 0.3f)
				{
					WalkTo(man, value2, straight);
				}
			}
		}
		int num3 = Game.GameTime - pr.StageAt;
		if ((pr.BodyDown && flag3) || num3 > 45000)
		{
			Gather(pr, men, flag3 ? "all there" : $"{num3 / 1000} s on");
		}
	}

	private void LayBodyAt(Procession pr, string how)
	{
		pr.BodyDown = true;
		if (!(pr.Body == null) && pr.Body.Exists())
		{
			if (pr.Carrier != null && pr.Carrier.Exists() && PED.IS_PED_CARRYING_SOMETHING(pr.Carrier.Handle))
			{
				PED.DETACH_CARRIABLE_ENTITY(pr.Body.Handle, p1: true, p2: true);
			}
			ENTITY.DETACH_ENTITY(pr.Body.Handle, p1: true, collision: true);
			pr.Shouldered = false;
			ENTITY.SET_ENTITY_COORDS(pr.Body.Handle, pr.BodyAt.X, pr.BodyAt.Y, pr.BodyAt.Z + 0.1f, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
			if (pr.Carrier != null && pr.Carrier.Exists())
			{
				WalkTo(pr.Carrier, pr.Ring.TryGetValue(pr.Carrier.Handle, out var value) ? value : pr.BodyAt, straight: false);
			}
			GrudgeLog($"PROCESSION body: laid down by the grave ({how}) at z {pr.BodyAt.Z:0.0}");
		}
	}

	private void Gather(Procession pr, List<Ped> men, string why)
	{
		if (!pr.BodyDown)
		{
			LayBodyAt(pr, "it was time");
		}
		foreach (Ped man in men)
		{
			float heading = HeadingTo(man.Position, pr.BodyAt);
			ENTITY.SET_ENTITY_HEADING(man.Handle, heading);
			TASK.TASK_START_SCENARIO_IN_PLACE_HASH(man.Handle, MISC.GET_HASH_KEY(pr.Scenario), -1, playEnterAnim: true, 0u, heading, p6: false);
			man.BlockPermanentEvents = false;
		}
		pr.Stage = 3;
		GrudgeLog("PROCESSION " + pr.Plan?.Title + ": gathered round the body (" + why + ") - " + pr.Scenario);
		_proc = null;
	}
}
