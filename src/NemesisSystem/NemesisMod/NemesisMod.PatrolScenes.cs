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
	private sealed class PatrolScene
	{
		public int StartedAt;

		public int EndsAt;

		public int MountAt;

		public int RemountAt;

		public int AlertSeen;

		public readonly Dictionary<int, (Ped man, Ped body, int stage)> Lookers = new Dictionary<int, (Ped, Ped, int)>();

		public Vector3 Center;
	}

	private readonly HashSet<(string patrol, uint group)> _sidedAgainst = new HashSet<(string, uint)>();

	private readonly HashSet<int> _sidedPeds = new HashSet<int>();

	private static uint _relCivMale;

	private static uint _relCivFemale;

	private static uint _relPlayer;

	private readonly HashSet<int> _gotDown = new HashSet<int>();

	private const int PedCap = 30;

	private int _pedCapAt;

	private static readonly HashSet<string> FriendlyKeys = new HashSet<string> { "regards", "checkin", "letter", "thanks", "drink" };

	private static readonly string[] PenLife = new string[22]
	{
		"THE WEATHER TURNED COLD HERE. ROOF NEEDS MENDING BEFORE THE RAINS.", "BOUGHT A NEW MULE. STUBBORN AS SIN BUT SHE PULLS.", "MY SISTER IS POORLY. DOCTOR SAYS IT WILL PASS.", "WON FOUR DOLLARS AT CARDS SATURDAY. LOST THREE SUNDAY.", "THE CREEK FLOODED THE LOWER FIELD. WE WILL MANAGE.", "CHURCH HAD A SUPPER. THE PIES WERE A SIGHT.", "A STRANGER CAME THROUGH ASKING DIRECTIONS. NOT YOU, I CHECKED.", "WORK IS STEADY. BACK ACHES. CANT COMPLAIN.", "SAW A BEAR BY THE OLD MILL. KEPT MY DISTANCE.", "THE SALOON GOT A NEW PIANO. NOBODY CAN PLAY IT.",
		"MY BOY LOST A TOOTH. PROUD AS A GENERAL ABOUT IT.", "PRICES AT THE STORE ARE A CRIME. SOMEBODY OUGHT TO ROB IT. JOKING.", "BEEN READING A DIME NOVEL. YOU WOULD FIT RIGHT IN IT.", "DOG HAD PUPPIES. SAVING YOU ONE IF YOU WANT IT.", "THE TRAIN WAS LATE THREE DAYS RUNNING. NOBODY KNOWS WHY.", "FIXED THE FENCE. COWS FOUND A NEW HOLE BY MORNING.", "SHERIFF IS NEW. SEEMS DECENT. TIME WILL TELL.", "HAD A FEVER LAST WEEK. ON MY FEET AGAIN.", "SOLD A HORSE FOR MORE THAN SHE WAS WORTH. DONT TELL THE BUYER.", "PLANTED A GARDEN. THE RABBITS THANK ME.",
		"WROTE TO MY BROTHER BACK EAST. NO ANSWER YET.", "THE NIGHTS ARE QUIET HERE. GIVES A MAN TIME TO THINK."
	};

	private bool UpdatePatrolScene(Raid r, Ped lead, Ped player)
	{
		_scenes.TryGetValue(r, out var value);
		bool flag = r.Hostile || AfterYou(r.Gang);
		if (value != null && (flag || r.AlertAt != value.AlertSeen || lead == null))
		{
			foreach (var value5 in value.Lookers.Values)
			{
				if (value5.man.Exists() && !value5.man.IsDead && !PED.IS_PED_IN_COMBAT(value5.man.Handle, 0))
				{
					TASK.CLEAR_PED_TASKS(value5.man.Handle, p1: false, p2: false);
				}
			}
			_scenes.Remove(r);
			GrudgeLog("PATROL " + r.Gang.Id + ": done looking the scene over - " + (flag ? "it turned ugly" : "more shooting"));
			return false;
		}
		if (value == null)
		{
			if (r.RidingOff || r.Watch || flag || lead == null)
			{
				return false;
			}
			if ((r.CampId != null || r.Responding || r.AlertAt <= r.SpawnedAt || !(lead.Position.DistanceTo(r.Alert) < 80f) || (r.QuietFrom == 0 && !r.SidedWithYou)) && (!r.Arrived || (r.CampId == null && !r.Responding)))
			{
				return false;
			}
			if (r.CampId != null && r.QuietFrom == 0 && Game.GameTime - r.ArrivedAt < 60000)
			{
				return false;
			}
			if (r.Men.Any((Ped m) => m.Exists() && !m.IsDead && PED.IS_PED_IN_COMBAT(m.Handle, 0)))
			{
				r.QuietFrom = Game.GameTime;
				return false;
			}
			if (Game.GameTime - Math.Max(r.AlertAt, r.QuietFrom) < 15000 || Game.GameTime < r.WatchYouUntil)
			{
				return false;
			}
			value = StartPatrolScene(r, lead, player, _rng.Next(100) < 30);
			_scenes[r] = value;
		}
		bool calm = Game.GameTime - r.AlertAt > 10000 && Game.GameTime >= r.WatchYouUntil;
		foreach (Ped item in r.Men.Where((Ped m) => m.Exists() && !m.IsDead))
		{
			PatrolTalkable(r, item, player, calm);
		}
		if (value.MountAt == 0 && Game.GameTime >= value.EndsAt)
		{
			value.MountAt = Game.GameTime;
			foreach (Ped item2 in r.Men.Where((Ped m) => m.Exists() && !m.IsDead && !m.IsOnMount))
			{
				TASK.CLEAR_PED_TASKS(item2.Handle, p1: false, p2: false);
				if (r.HorseOf.TryGetValue(item2.Handle, out var value2) && value2 != null && value2.Exists() && !value2.IsDead)
				{
					TASK.TASK_MOUNT_ANIMAL(item2.Handle, value2.Handle, 20000, -1, 1f, 1, 0uL, 0uL);
				}
			}
			GrudgeLog("PATROL " + r.Gang.Id + ": done looking the scene over - mounting up");
			return true;
		}
		if (value.MountAt != 0)
		{
			bool flag2 = !r.Mounted || r.Men.All((Ped m) => !m.Exists() || m.IsDead || m.IsOnMount);
			if (!flag2)
			{
				long num = Game.GameTime - value.MountAt;
				bool flag3 = Game.GameTime >= value.RemountAt;
				if (flag3)
				{
					value.RemountAt = Game.GameTime + 4000;
				}
				foreach (Ped item3 in r.Men.Where((Ped m) => m.Exists() && !m.IsDead && !m.IsOnMount))
				{
					if (r.HorseOf.TryGetValue(item3.Handle, out var value3) && !(value3 == null) && value3.Exists() && !value3.IsDead)
					{
						if (num > 25000 || (num > 12000 && HiddenFromView(player, item3.Position)))
						{
							Crumbs.Native("Patrols onto mount");
							PED.SET_PED_ONTO_MOUNT(item3.Handle, value3.Handle, -1, p3: true);
						}
						else if (flag3 && item3.Position.DistanceTo(value3.Position) > 2.5f)
						{
							TASK.TASK_MOUNT_ANIMAL(item3.Handle, value3.Handle, 20000, -1, 1f, 1, 0uL, 0uL);
						}
					}
				}
				flag2 = r.Men.All((Ped m) => !m.Exists() || m.IsDead || m.IsOnMount || !r.HorseOf.TryGetValue(m.Handle, out var value4) || value4 == null || !value4.Exists() || value4.IsDead);
			}
			if (!flag2)
			{
				return true;
			}
			_scenes.Remove(r);
			Vector3 vector = lead.Position - player.Position;
			vector.Z = 0f;
			if (vector.Length() < 1f)
			{
				vector = new Vector3(1f, 0f, 0f);
			}
			vector.Normalize();
			r.Responding = false;
			r.Arrived = true;
			r.RidingOff = true;
			r.FollowAt.Clear();
			r.AlertAt = Math.Min(r.AlertAt, Game.GameTime - 45001);
			r.Target = PatrolPoint(lead.Position + vector * 300f);
			r.Hurry = true;
			r.MovingTo = Vector3.Zero;
			r.MoveOrderAt = -100000;
			r.NextOrder = 0;
			GrudgeLog($"PATROL {r.Gang.Id}: riding off together after the fight ({r.Men.Count((Ped m) => m.Exists() && !m.IsDead)} men, {lead.Position.DistanceTo(player.Position):0} m from you)");
			return false;
		}
		foreach (int item4 in value.Lookers.Keys.ToList())
		{
			var (ped, ped2, num2) = value.Lookers[item4];
			if (!ped.Exists() || ped.IsDead || PED.IS_PED_IN_COMBAT(ped.Handle, 0))
			{
				continue;
			}
			if (num2 == 0)
			{
				if (ped.IsOnMount)
				{
					continue;
				}
				num2 = 1;
				Vector3 vector2 = BodySide(ped2);
				TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ped.Handle, vector2.X, vector2.Y, vector2.Z, 1f, -1, 0.6f, 0, 0f);
			}
			else if (num2 == 1 && !ped2.Exists())
			{
				num2 = 2;
				TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY("WORLD_HUMAN_GUARD_SCOUT"), -1, playEnterAnim: true, 0u, -1f, p6: false);
			}
			else if (num2 == 1 && ped.Position.DistanceTo(ped2.Position) < 1.8f)
			{
				num2 = 2;
				ENTITY.SET_ENTITY_HEADING(ped.Handle, HeadingTo(ped.Position, ped2.Position));
				TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY("WORLD_HUMAN_CROUCH_INSPECT"), -1, playEnterAnim: true, 0u, -1f, p6: false);
			}
			else if (num2 == 1 && Stuck(ped))
			{
				Vector3 vector3 = BodySide(ped2);
				TASK.TASK_GO_TO_COORD_ANY_MEANS(ped.Handle, vector3.X, vector3.Y, vector3.Z, 1f, 0, p6: false, 786603, 0f);
			}
			value.Lookers[item4] = (ped, ped2, num2);
		}
		return true;
	}

	private PatrolScene StartPatrolScene(Raid r, Ped lead, Ped player, bool look = true)
	{
		Vector3 centre = ((r.Alert != Vector3.Zero) ? r.Alert : ((r.CampId != null) ? r.Target : lead.Position));
		PatrolScene sc = new PatrolScene
		{
			StartedAt = Game.GameTime,
			EndsAt = Game.GameTime + 35000 + _rng.Next(10000),
			AlertSeen = r.AlertAt,
			Center = centre
		};
		if (r.SidedWithYou && !AfterYou(r.Gang) && lead.Position.DistanceTo(player.Position) < 45f && !HiddenFromView(player, lead.Position + new Vector3(0f, 0f, 1f)))
		{
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(lead.Handle, player.Handle, 2500, 0f, 0f, 0f);
			TrySay(lead, "GREET_GENERAL_STRANGER", player);
			GrudgeNotice("The " + r.Gang.Name + " ride on.");
		}
		HashSet<int> ours = new HashSet<int>(from m in r.Men
			where m.Exists()
			select m.Handle);
		List<Ped> bodies = new List<Ped>();
		try
		{
			bodies = (from p in World.GetAllPeds().Concat(NearbyPeds(player))
				where p != null && p.Exists() && p.IsDead && p.IsHuman && !p.IsPlayer && !ours.Contains(p.Handle) && p.Position.DistanceTo(centre) < 45f
				group p by p.Handle into g
				select g.First() into p
				orderby p.Position.DistanceTo(lead.Position)
				select p).ToList();
		}
		catch (Exception ex)
		{
			GrudgeLog("PATROL scene: the dead couldn't be found - " + ex.Message);
		}
		List<Ped> list = (from m in r.Men
			where m.Exists() && !m.IsDead && m != lead && !PED.IS_PED_IN_COMBAT(m.Handle, 0) && !NerveOut(m.Handle)
			orderby (bodies.Count <= 0) ? 0f : m.Position.DistanceTo(bodies[0].Position)
			select m).ToList();
		for (int num = 0; num < Math.Min((!look) ? 1 : 2, Math.Min(list.Count, bodies.Count)); num++)
		{
			Ped ped = list[num];
			if (_patrolOpen.Remove(ped.Handle))
			{
				ped.BlockPermanentEvents = true;
			}
			if (ped.IsOnMount)
			{
				TASK.TASK_DISMOUNT_ANIMAL(ped.Handle, 0, 0uL, 0uL, 0uL, 0);
			}
			sc.Lookers[ped.Handle] = (ped, bodies[num], 0);
		}
		if (bodies.Count > 0)
		{
			sc.Center = bodies[0].Position;
		}
		foreach (Ped item in r.Men.Where((Ped m) => m.Exists() && !m.IsDead && !sc.Lookers.ContainsKey(m.Handle) && !PED.IS_PED_IN_COMBAT(m.Handle, 0)))
		{
			Function.Call(8044667063384373619uL, item.Handle, sc.Center.X, sc.Center.Y, sc.Center.Z, 4000, 0, 51, 0);
			Function.Call(16164602603745899285uL, item.Handle, sc.Center.X, sc.Center.Y, sc.Center.Z, 14f, 2f, 5f, 0);
		}
		sc.EndsAt = Game.GameTime + ((look && bodies.Count > 0) ? (25000 + _rng.Next(10000)) : (15000 + _rng.Next(10000)));
		GrudgeLog(string.Format("PATROL {0}: quiet 15 s - looking the place over, moving about: {1} to {2} dead, {3} s, {4:0} m from you{5}", r.Gang.Id, sc.Lookers.Count, bodies.Count, (sc.EndsAt - Game.GameTime) / 1000, lead.Position.DistanceTo(player.Position), r.SidedWithYou ? ", they took your side" : ""));
		return sc;
	}

	private void RideOffTogether(Raid r, Ped m, Ped lead, bool march)
	{
		if (lead == null || m == lead)
		{
			if (march || Stuck(m))
			{
				MoveTo(r, m, r.Target, fast: false);
			}
			return;
		}
		int num = r.Men.Where((Ped x) => x != lead).ToList().IndexOf(m);
		float num2 = 3.5f * (float)(num / 2 + 1);
		float num3 = ((num % 2 == 0) ? 2f : (-2f));
		bool flag = m.Position.DistanceTo(lead.Position) > (float)Math.Sqrt(num2 * num2 + num3 * num3) + 5f;
		if (!r.FollowAt.TryGetValue(m.Handle, out var value) || (flag && Game.GameTime - value > 6000))
		{
			r.FollowAt[m.Handle] = Game.GameTime;
			TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(m.Handle, lead.Handle, num3, 0f - num2, 0f, 3f, -1, 2.5f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
		}
	}

	private Vector3 BodySide(Ped body)
	{
		double num = _rng.NextDouble() * Math.PI * 2.0;
		return GroundOnSpot(body.Position + new Vector3((float)Math.Cos(num) * 0.9f, (float)Math.Sin(num) * 0.9f, 0f));
	}

	private bool AtWar(Gang a, Gang b)
	{
		if (a == null || b == null || a == b)
		{
			return false;
		}
		if (IsDestroyed(a) || IsDestroyed(b) || TruceBetween(a.Id, b.Id))
		{
			return false;
		}
		if ((a != Gangs.Army || (b != Gangs.LemoyneRaiders && b != Gangs.ODriscolls)) && (b != Gangs.Army || (a != Gangs.LemoyneRaiders && a != Gangs.ODriscolls)))
		{
			return AtWarNow(a.Id, b.Id);
		}
		return true;
	}

	private Gang FactionOfPed(Ped p)
	{
		NemesisRecord nemesisRecord = RecordAt(p.Handle);
		if (nemesisRecord != null && nemesisRecord.Gang != null)
		{
			return Gangs.ById(nemesisRecord.Gang);
		}
		return Gangs.ForModel((uint)p.Model.Hash);
	}

	private static bool CivilGroup(uint g)
	{
		if (_relCivMale == 0)
		{
			_relCivMale = Joaat.Of("REL_CIVMALE");
			_relCivFemale = Joaat.Of("REL_CIVFEMALE");
			_relPlayer = Joaat.Of("PLAYER");
		}
		if (g != _relCivMale && g != _relCivFemale)
		{
			return g == _relPlayer;
		}
		return true;
	}

	private bool SharedModGroup(uint g)
	{
		if (g != 0)
		{
			if (g != _nemesisGroup && g != _townFightGroup && g != _townRelGroup && g != _rivalGroup && g != _survivorGroup && g != _huntersGroup && g != _surrenderGroup && g != _generalGroup && g != _sideA)
			{
				return g == _sideB;
			}
			return true;
		}
		return false;
	}

	private void JudgeTheFight(Raid r, Ped lead, Ped player)
	{
		if (r.Hostile || lead == null || !lead.Exists() || (_nextSideCheck.TryGetValue(r, out var value) && Game.GameTime < value))
		{
			return;
		}
		_nextSideCheck[r] = Game.GameTime + 1000;
		if (lead.Position.DistanceTo(player.Position) > 150f)
		{
			return;
		}
		Vector3 position = player.Position;
		string text = null;
		List<(Gang, uint)> list = new List<(Gang, uint)>();
		List<(Gang, Ped)> list2 = new List<(Gang, Ped)>();
		foreach (Ped item in PedsAround(player))
		{
			if (item == null || !item.Exists() || item.IsPlayer || !item.IsHuman || r.Men.Contains(item) || item.Position.DistanceTo(position) > 120f || !(item.IsDead ? (PED.GET_PED_SOURCE_OF_DEATH(item.Handle) == player.Handle) : PED.IS_PED_IN_COMBAT(item.Handle, player.Handle)))
			{
				continue;
			}
			Gang gang = FactionOfPed(item);
			if (gang == null)
			{
				continue;
			}
			if (gang == r.Gang)
			{
				text = (item.IsDead ? ("you killed one of their own (" + gang.Name + ")") : ("you're fighting their own (" + gang.Name + ")"));
				break;
			}
			if (AtWar(r.Gang, gang) && !item.IsDead)
			{
				uint num = ModGroupOf(item.Handle);
				if (SharedModGroup(num) || CivilGroup(num))
				{
					list2.Add((gang, item));
				}
				else
				{
					list.Add((gang, num));
				}
			}
		}
		if (text != null)
		{
			r.Hostile = true;
			r.WatchYouUntil = 0;
			r.Watch = false;
			SetPatrolRelations(player);
			GrudgeLog("PATROL " + r.Gang.Id + " sides: " + text + " - they're after you");
			Feed("PATROL " + r.Gang.Name + ": " + text + " - they're after you", Theme.Blood);
			return;
		}
		uint num2 = GroupFor(r.Gang);
		foreach (var (gang2, ped) in list2)
		{
			if (!_sidedPeds.Add(ped.Handle))
			{
				continue;
			}
			foreach (Ped man in r.Men)
			{
				if (man != null && man.Exists() && !man.IsDead && man.Position.DistanceTo(ped.Position) < 120f)
				{
					TASK.TASK_COMBAT_PED(man.Handle, ped.Handle, 0, 16);
				}
			}
			r.WatchYouUntil = 0;
			r.SidedWithYou = true;
			GrudgeLog($"PATROL {r.Gang.Id} sides: you're fighting the {gang2.Name} - they take on ped {ped.Handle}");
			Feed("PATROL " + r.Gang.Name + " rides in against the " + gang2.Name, r.Gang.Accent);
		}
		foreach (var (gang3, num3) in list.Distinct())
		{
			if (num3 != 0 && num3 != player.RelationshipGroup && _sidedAgainst.Add((r.Gang.Id, num3)))
			{
				World.SetRelationshipBetweenGroups(eRelationshipType.Hate, num2, num3);
				World.SetRelationshipBetweenGroups(eRelationshipType.Hate, num3, num2);
				r.WatchYouUntil = 0;
				r.SidedWithYou = true;
				GrudgeLog("PATROL " + r.Gang.Id + " sides: you're fighting the " + gang3.Name + " - they take them on");
				Feed("PATROL " + r.Gang.Name + " rides in against the " + gang3.Name, r.Gang.Accent);
			}
		}
	}

	private bool GetDownToFight(Raid r, Ped m, Ped player)
	{
		if (!r.Hostile || !m.IsOnMount || _gotDown.Contains(m.Handle))
		{
			return false;
		}
		if (m.Position.DistanceTo(player.Position) > 45f)
		{
			return false;
		}
		_gotDown.Add(m.Handle);
		if ((uint)m.Handle % 10u >= 7)
		{
			return false;
		}
		TASK.TASK_DISMOUNT_ANIMAL(m.Handle, 0, 0uL, 0uL, 0uL, 0);
		GrudgeLog($"PATROL {r.Gang.Id}: a rider gets down to fight ({m.Position.DistanceTo(player.Position):0} m from you)");
		return true;
	}

	private bool FightingNear(Raid r, Ped player)
	{
		if (!r.Hostile && Game.GameTime >= r.WatchYouUntil)
		{
			return r.Men.Any((Ped m) => m.Exists() && !m.IsDead && PED.IS_PED_IN_COMBAT(m.Handle, 0) && m.Position.DistanceTo(player.Position) < 120f);
		}
		return true;
	}

	private void CapOurPeds()
	{
		if (Game.GameTime < _pedCapAt)
		{
			return;
		}
		_pedCapAt = Game.GameTime + 5000;
		Ped character = Game.Player.Character;
		if (character == null || !character.Exists())
		{
			return;
		}
		Vector3 position = character.Position;
		int num = 0;
		HashSet<int> hashSet = new HashSet<int>();
		foreach (var (ped, ourRole) in OurPeds())
		{
			if (ped != null && hashSet.Add(ped.Handle) && ped.Exists() && !ped.IsDead && ourRole != OurRole.Companion)
			{
				num++;
			}
		}
		int num2 = _aftermathPeds.Count((Ped ped2) => ped2 != null && ped2.Exists());
		int num3 = num + num2 / 2 - 30;
		if (num3 <= 0)
		{
			return;
		}
		List<(Ped p, int tier, float d, TownMan tm)> list = new List<(Ped p, int tier, float d, TownMan tm)>();
		foreach (Ped aftermathPed in _aftermathPeds)
		{
			if (aftermathPed != null && aftermathPed.Exists())
			{
				list.Add((aftermathPed, 0, aftermathPed.Position.DistanceTo(position), null));
			}
		}
		foreach (Ped regular in _regulars)
		{
			if (regular != null && regular.Exists() && !regular.IsDead)
			{
				list.Add((regular, 1, regular.Position.DistanceTo(position), null));
			}
		}
		foreach (Ped survivor in _survivors)
		{
			if (survivor != null && survivor.Exists() && !survivor.IsDead && !_worldNemeses.ContainsKey(survivor.Handle))
			{
				list.Add((survivor, 2, survivor.Position.DistanceTo(position), null));
			}
		}
		foreach (TownMan townMan in _townMen)
		{
			if (townMan.Ped != null && townMan.Ped.Exists() && !townMan.Ped.IsDead && !townMan.Engaged && !townMan.Leaving)
			{
				list.Add((townMan.Ped, 3, townMan.Ped.Position.DistanceTo(position), townMan));
			}
		}
		int num4 = 0;
		foreach (var item in from c in list
			where c.d > 80f
			orderby c.tier, c.d descending
			select c)
		{
			if (num4 >= num3)
			{
				break;
			}
			if (SafeToDelete(item.Item1) && HiddenFromView(character, item.Item1.Position + new Vector3(0f, 0f, 1f)) && !PED.IS_PED_IN_COMBAT(item.Item1.Handle, 0))
			{
				if (item.Item4 != null)
				{
					ClearTownMan(item.Item4, delete: true);
				}
				else
				{
					var (p, _, _, _) = item;
					DeleteOrRelease(p);
				}
				num4++;
			}
		}
		if (num4 > 0)
		{
			GrudgeLog($"PEDCAP {num} living + {num2} bodies of ours (cap {30}) - let {num4} far-off, out-of-sight people go");
		}
	}
}
