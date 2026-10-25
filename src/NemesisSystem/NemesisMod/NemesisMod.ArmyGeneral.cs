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
	private const uint FortWallaceZone = 4195085991u;

	private const int ZoneTypePrinted = 11;

	private static readonly Vector3 FortGate = new Vector3(358.5f, 1465f, 179f);

	private static readonly uint[] FortGateDoors = new uint[2] { 385812466u, 3093514944u };

	private static readonly Vector3 FortCommanderSpot = new Vector3(356f, 1502f, 181f);

	private static readonly Vector3[] FortGuardSpots = new Vector3[7]
	{
		new Vector3(352f, 1478f, 180f),
		new Vector3(344f, 1487f, 181f),
		new Vector3(366f, 1494f, 181f),
		new Vector3(338f, 1497f, 181f),
		new Vector3(361f, 1508f, 181f),
		new Vector3(354f, 1457f, 179f),
		new Vector3(363f, 1459f, 179f)
	};

	private Ped _general;

	private readonly List<Ped> _genEscort = new List<Ped>();

	private Blip _genBlip;

	private int _nextGeneralCheck;

	private int _generalCooldownUntil;

	private NemesisRecord _generalRec;

	private bool _generalProtected;

	private int _generalHitLogged;

	private int _nextFortHostileCheck;

	private int _generalHeadshots;

	private readonly HashSet<uint> _gatesAdded = new HashSet<uint>();

	private readonly HashSet<uint> _gatesWeAdded = new HashSet<uint>();

	private readonly Dictionary<uint, int> _gateStateWas = new Dictionary<uint, int>();

	private int _nextGateOrder;

	private uint _generalGroup;

	private int _fortBlip;

	private string _fortBlipLabel;

	private int _fortLabelCheckAt;

	private void UpdateGeneral(Ped player)
	{
		float num = player.Position.DistanceTo(FortGate);
		bool mISSION_FLAG = MISC.GET_MISSION_FLAG();
		RefreshFortBlip();
		if (!Chronicle.FortKnown)
		{
			if (_gatesAdded.Count > 0)
			{
				ReleaseFortGates();
			}
			return;
		}
		if (num < 200f && !mISSION_FLAG)
		{
			OpenFortGates();
		}
		else if (_gatesAdded.Count > 0 && (num > 300f || mISSION_FLAG))
		{
			ReleaseFortGates();
		}
		if (_general != null || _genEscort.Count > 0)
		{
			if (mISSION_FLAG)
			{
				GrudgeLog("ARMY fort: a mission started - the garrison goes");
				DespawnGeneral();
				return;
			}
			if (_general == null)
			{
				_genEscort.RemoveAll((Ped p) => p == null || !p.Exists());
				if (_genEscort.Count == 0 || num > 320f)
				{
					DespawnGeneral();
				}
				else if (Game.GameTime >= _nextFortHostileCheck)
				{
					_nextFortHostileCheck = Game.GameTime + 1000;
					if (AfterYou(Gangs.Army) || _genEscort.Any((Ped p) => p.Exists() && !p.IsDead && PED.IS_PED_IN_COMBAT(p.Handle, player.Handle)))
					{
						World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _generalGroup, player.RelationshipGroup);
					}
				}
				return;
			}
			if (!_general.Exists())
			{
				DespawnGeneral();
				return;
			}
			if (_general.IsDead && _generalProtected)
			{
				GrudgeLog("ARMY fort: the story-protected commander read as dead - his seat stays, he's gone from the fort");
				_generalCooldownUntil = Game.GameTime + 3600000;
				DespawnGeneral();
				return;
			}
			if (_general.IsDead)
			{
				bool flag = PED.GET_PED_SOURCE_OF_DEATH(_general.Handle) == player.Handle || PED.GET_PED_SOURCE_OF_DEATH(_general.Handle) == 0;
				string text = _generalRec?.FullName ?? Gangs.Army.LeaderName;
				if (_generalRec == null && SeatOf(Gangs.Army)?.Fallen == null)
				{
					Fall(Gangs.Army, text, "killed at Fort Wallace", flag, null, "cs_colfavours");
				}
				DeathNotice(text + " is dead at Fort Wallace. The Army will never forget your name.");
				if (flag)
				{
					Chronicle.FortCleared = true;
					Chronicle.FortClearedAt = GameMinutesNow();
					SaveWorld();
					GrudgeLog("ARMY fort: hideout cleared - the commander is dead");
					Feed("HIDEOUT Fort Wallace cleared - its commander is dead", Theme.Gold);
					GrudgeNotice("Fort Wallace hideout cleared.");
					RefreshFortBlip();
				}
				_generalCooldownUntil = Game.GameTime + 3600000;
				DespawnGeneral();
				return;
			}
			if (!_generalProtected && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(_general.Handle, player.Handle, p2: true, p3: true))
			{
				int num2 = LastDamageBone(_general.Handle);
				bool num3 = _general.IsRagdoll || _general.IsIncapacitated || PED.IS_PED_HOGTIED(_general.Handle) || PED.IS_PED_FATALLY_INJURED(_general.Handle);
				ENTITY.CLEAR_ENTITY_LAST_DAMAGE_ENTITY(_general.Handle);
				string text2 = null;
				if (num3 && GunMelee.PlayerFiredRecently)
				{
					text2 = "executed on the ground";
				}
				else if (HeadBone(num2) && ++_generalHeadshots >= 2)
				{
					text2 = "a second shot to the head";
				}
				else if (HeadBone(num2))
				{
					_general.Health = Math.Min(_general.Health, _general.MaxHealth / 3);
					GrudgeLog($"ARMY fort: the commander took a shot to the head - health {_general.Health}");
				}
				if (text2 != null && !_general.IsDead)
				{
					GrudgeLog("ARMY fort: the commander - " + text2);
					Crumbs.Native("Army commander killed");
					PED.APPLY_DAMAGE_TO_PED(_general.Handle, 5000, 0, (num2 != 0) ? num2 : 21030, player.Handle);
					if (!_general.IsDead)
					{
						_general.Health = 0;
					}
				}
			}
			if (_generalProtected && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(_general.Handle, player.Handle, p2: true, p3: true))
			{
				ENTITY.CLEAR_ENTITY_LAST_DAMAGE_ENTITY(_general.Handle);
				if (!_general.IsRagdoll)
				{
					PED.SET_PED_TO_RAGDOLL(_general.Handle, 3000, 4000, 0, p4: false, p5: false, "");
				}
				if (Game.GameTime - _generalHitLogged > 10000)
				{
					_generalHitLogged = Game.GameTime;
					GrudgeLog("ARMY fort: the commander was hit - story-protected, he goes down but lives");
				}
			}
			if (Game.GameTime >= _nextFortHostileCheck)
			{
				_nextFortHostileCheck = Game.GameTime + 1000;
				if (AfterYou(Gangs.Army) || _genEscort.Concat(new Ped[1] { _general }).Any((Ped p) => p != null && p.Exists() && !p.IsDead && PED.IS_PED_IN_COMBAT(p.Handle, player.Handle)))
				{
					World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _generalGroup, player.RelationshipGroup);
				}
			}
			if (num > 320f || mISSION_FLAG)
			{
				DespawnGeneral();
			}
		}
		else if (Game.GameTime >= _nextGeneralCheck)
		{
			_nextGeneralCheck = Game.GameTime + 3000;
			if (!(!Config.ArmyGeneral || Game.GameTime < _generalCooldownUntil || mISSION_FLAG) && !(num > 180f) && !Chronicle.FortCleared && PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle) && NoSpawnReason(player, place: true) == null && !IsDestroyed(Gangs.Army))
			{
				SpawnFortHQ(player);
			}
		}
	}

	private void OpenFortGates()
	{
		if (Game.GameTime < _nextGateOrder && _gatesAdded.Count == FortGateDoors.Length)
		{
			return;
		}
		_nextGateOrder = Game.GameTime + 5000;
		uint[] fortGateDoors = FortGateDoors;
		foreach (uint num in fortGateDoors)
		{
			try
			{
				if (!_gatesAdded.Contains(num))
				{
					if (!Function.Call<bool>(13930693845672184001uL, new InputArgument[1] { num }))
					{
						Crumbs.Native("Army door");
						Function.Call(15677639426206622342uL, num, true, true, false, 0, 0, false);
						_gatesAdded.Add(num);
						_gatesWeAdded.Add(num);
					}
					else
					{
						_gatesAdded.Add(num);
						Crumbs.Native("Army door");
						try
						{
							_gateStateWas[num] = OBJECT.DOOR_SYSTEM_GET_DOOR_STATE(num);
						}
						catch
						{
						}
					}
					GrudgeLog($"ARMY fort gate {num}: in the door system, unlocking and opening");
				}
				Crumbs.Native("Army door");
				Function.Call(7758457796463198035uL, num, 0);
				Crumbs.Native("Army door");
				Function.Call(13179498064365429932uL, num, (num == FortGateDoors[0]) ? 1f : (-1f), true);
			}
			catch (Exception ex)
			{
				GrudgeLog("ARMY fort gate failed: " + ex.Message);
			}
		}
	}

	private void ReleaseFortGates()
	{
		if (_gatesAdded.Count == 0)
		{
			return;
		}
		foreach (uint item in _gatesAdded)
		{
			try
			{
				Crumbs.Native("Army door");
				int value;
				if (_gatesWeAdded.Contains(item))
				{
					Function.Call(5065861373067882468uL, item);
				}
				else if (_gateStateWas.TryGetValue(item, out value))
				{
					Function.Call(7758457796463198035uL, item, value);
				}
			}
			catch
			{
			}
		}
		_gatesAdded.Clear();
		_gatesWeAdded.Clear();
		_gateStateWas.Clear();
		GrudgeLog("ARMY fort gates handed back to the game");
	}

	private void SpawnFortHQ(Ped player)
	{
		_generalCooldownUntil = Game.GameTime + 600000;
		if (_generalGroup == 0)
		{
			_generalGroup = World.AddRelationshipGroup("NEMESIS_GENERAL");
		}
		LeaderSeat leaderSeat = SeatOf(Gangs.Army);
		bool flag = leaderSeat == null || leaderSeat.Fallen == null;
		_generalRec = (flag ? null : FindRecord(leaderSeat.Successor));
		if (!flag && _generalRec == null)
		{
			GrudgeLog("ARMY fort: the commander's seat is empty - the garrison holds without him");
		}
		_generalProtected = false;
		_generalHeadshots = 0;
		uint num = (flag ? MISC.GET_HASH_KEY("cs_colfavours") : (_generalRec?.ModelHash ?? 722236839));
		if (!STREAMING.IS_MODEL_IN_CDIMAGE(num))
		{
			num = 722236839u;
		}
		Model m = new Model((int)num);
		if (!LoadModelSafe(m, 3000))
		{
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED(num);
			return;
		}
		bool flag2 = AfterYou(Gangs.Army);
		if (flag || _generalRec != null)
		{
			float heading;
			int point;
			Vector3 vector = FortSpot(player, FortCommanderSpot, 25f, out heading, out point);
			_general = SpawnPed((PedHash)num, vector, heading);
			if (_general != null)
			{
				MakeOurs(_general.Handle);
				_general.RelationshipGroup = _generalGroup;
				_general.BlockPermanentEvents = false;
				_general.GiveWeapon(eWeapon.RevolverSchofield, 60, eWeaponAttachPoint.PistolR, bForceInHand: true);
				_general.Accuracy = 55;
				_general.MaxHealth = 1200;
				_general.Health = 1200;
				if (_generalProtected)
				{
					ENTITY.SET_ENTITY_INVINCIBLE(_general.Handle, toggle: true);
					Kids.StoryProtected.Add(_general.Handle);
				}
				if (_generalRec != null)
				{
					_worldNemeses[_general.Handle] = _generalRec;
					ApplyScars(_general, _generalRec);
				}
				if (point != 0)
				{
					SitAt(_general, point, vector, heading);
				}
				else
				{
					TASK.TASK_START_SCENARIO_IN_PLACE_HASH(_general.Handle, MISC.GET_HASH_KEY("WORLD_HUMAN_GUARD_LAZY"), -1, playEnterAnim: true, 0u, -1f, p6: false);
				}
				GrudgeLog(string.Format("ARMY fort: the commander at {0:0},{1:0},{2:0} ({3})", vector.X, vector.Y, vector.Z, (point != 0) ? "a scenario point" : "his spot"));
				string text = _generalRec?.FullName ?? Gangs.Army.LeaderName;
				Crumbs.Native("Army blip add (V2)");
				_genBlip = _general.AddBlip(flag2 ? BlipType.BLIP_STYLE_ENEMY : BlipType.BLIP_STYLE_NEUTRAL);
				if (_genBlip != null)
				{
					Function.Call(11290983183885202434uL, _genBlip.Handle, new InputArgument(UiDraw.VarString(text)));
				}
			}
		}
		STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED(num);
		Vector3[] fortGuardSpots = FortGuardSpots;
		for (int i = 0; i < fortGuardSpots.Length; i++)
		{
			Vector3 vector2 = fortGuardSpots[i];
			bool num2 = vector2.DistanceTo(FortGate) < 12f;
			int point2 = 0;
			Vector3 vector3;
			float heading2;
			if (num2)
			{
				vector3 = GroundAbove(vector2);
				heading2 = HeadingTo(vector2, FortGate) + 180f;
			}
			else
			{
				vector3 = FortSpot(player, vector2, 18f, out heading2, out point2);
			}
			Ped ped = SpawnPed(Troopers[_rng.Next(Troopers.Length)], vector3, heading2);
			if (!(ped == null))
			{
				MakeOurs(ped.Handle);
				ped.GiveWeapon(eWeapon.RepeaterCarbine, 100, eWeaponAttachPoint.Rifle, bForceInHand: true);
				ped.RelationshipGroup = _generalGroup;
				ped.BlockPermanentEvents = false;
				ApplyFactionStyle(ped, Gangs.Army.Id);
				if (point2 != 0)
				{
					SitAt(ped, point2, vector3, heading2);
				}
				else
				{
					TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY((_rng.Next(2) == 0) ? "WORLD_HUMAN_GUARD_SCOUT" : "WORLD_HUMAN_GUARD_LAZY"), -1, playEnterAnim: true, 0u, -1f, p6: false);
				}
				_genEscort.Add(ped);
			}
		}
		World.SetRelationshipBetweenGroups(flag2 ? eRelationshipType.Hate : eRelationshipType.Ignore, _generalGroup, player.RelationshipGroup);
		if (flag2)
		{
			foreach (Ped item in _genEscort)
			{
				TASK.TASK_COMBAT_PED(item.Handle, player.Handle, 0, 16);
			}
		}
		GrudgeLog(string.Format("ARMY fort: {0} + {1} soldiers at Fort Wallace, hostile={2}, story-protected={3}", (!(_general != null)) ? "no commander" : (_generalRec?.Name ?? Gangs.Army.LeaderName), _genEscort.Count, flag2, _generalProtected));
		if (flag2)
		{
			GrudgeNotice("Fort Wallace is up in arms - they know who you are.");
		}
	}

	private bool KnowsFort(NemesisRecord rec)
	{
		(string, int, int)[] tiers = Gangs.Army.Tiers;
		int num = ((tiers != null && tiers.Length != 0) ? tiers[0].Item2 : 4);
		if (rec != null && rec.Gang == Gangs.Army.Id)
		{
			return rec.Rank >= num;
		}
		return false;
	}

	private string FortNotMine()
	{
		string[] array = new string[3] { "The Colonel's quarters? Mister, I muck out stables. Ask an officer.", "I don't get past the parade ground. Only the officers know the fort.", "Orders come down from Fort Wallace - that's all a man my rank hears." };
		return array[_rng.Next(array.Length)];
	}

	internal string FortHedge()
	{
		string[] array = new string[3] { "The Colonel? He moves about. Different quarters every week. I don't know which.", "There's the fort, sure - and a hundred men in it. Which door's his? I couldn't say.", "He don't sleep where the men can find him. Nobody knows. Not even us." };
		return array[_rng.Next(array.Length)];
	}

	internal bool FortTells()
	{
		return _rng.Next(100) < 60;
	}

	internal string RevealFort(string speaker, NemesisRecord by = null)
	{
		if (Chronicle.FortKnown)
		{
			return null;
		}
		Chronicle.FortKnown = true;
		SaveWorld();
		bool flag = IsLeader(Gangs.Army, by);
		string text = (flag ? Gangs.Army.LeaderName : LeaderAsSaidBy(Gangs.Army, by)) ?? "The commander";
		Feed("INTEL " + speaker + ": " + text + " keeps his quarters at Fort Wallace - it's marked on your map", Theme.Gold);
		GrudgeNotice("INTEL: " + text + " holds Fort Wallace. It's marked on your map.");
		GrudgeLog("ARMY fort revealed by " + speaker);
		RefreshFortBlip();
		if (!flag)
		{
			return "Fort Wallace. " + text + " runs the whole of it from the yard. You'd never get near him.";
		}
		return "Fort Wallace. I run the whole of it from the yard. You'd never get near me.";
	}

	private unsafe void RemoveFortBlip()
	{
		if (_fortBlip != 0)
		{
			Crumbs.Native("Army blip remove (abort)");
			int fortBlip = _fortBlip;
			_fortBlip = 0;
			if (MAP.DOES_BLIP_EXIST(fortBlip))
			{
				MAP.REMOVE_BLIP(&fortBlip);
			}
		}
	}

	private unsafe void RefreshFortBlip()
	{
		if (!MapSettled())
		{
			_mapPending.Add(RefreshFortBlip);
		}
		else if (!Chronicle.FortKnown || Chronicle.FortCleared || IsDestroyed(Gangs.Army) || _mapHidden)
		{
			if (_fortBlip != 0)
			{
				Crumbs.Native("Army blip remove");
				int fortBlip = _fortBlip;
				MAP.REMOVE_BLIP(&fortBlip);
				_fortBlip = 0;
			}
		}
		else
		{
			if (_fortBlip != 0 && Game.GameTime < _fortLabelCheckAt)
			{
				return;
			}
			_fortLabelCheckAt = Game.GameTime + 2000;
			string text = "Fort Wallace - " + (LeaderAsSaidBy(Gangs.Army, null) ?? "the commander") + "'s quarters";
			if (_fortBlip == 0 || !(text == _fortBlipLabel))
			{
				if (_fortBlip != 0)
				{
					Crumbs.Native("Army blip remove (relabel)");
					int fortBlip2 = _fortBlip;
					MAP.REMOVE_BLIP(&fortBlip2);
					_fortBlip = 0;
				}
				Crumbs.Native("Army blip add");
				_fortBlip = MAP.BLIP_ADD_FOR_COORDS(2339406903u, FortCommanderSpot.X, FortCommanderSpot.Y, FortCommanderSpot.Z);
				Crumbs.Native("Army blip sprite");
				MAP.SET_BLIP_SPRITE(_fortBlip, MISC.GET_HASH_KEY("blip_region_hideout"), p2: true);
				Function.Call(11290983183885202434uL, _fortBlip, new InputArgument(UiDraw.ForeverVarString(text)));
				_fortBlipLabel = text;
			}
		}
	}

	private Vector3 FortSpot(Ped player, Vector3 near, float radius, out float heading, out int point)
	{
		heading = HeadingTo(near, FortGate);
		point = 0;
		(Vector3, float, int)? tuple = FindSeat(player, 0f, 400f, null, null, 0f, near, radius, 6f);
		if (tuple.HasValue && tuple.Value.Item1.DistanceTo(near) <= radius && InFortWallace(tuple.Value.Item1))
		{
			heading = tuple.Value.Item2;
			point = tuple.Value.Item3;
			return StandIn(tuple.Value.Item1, heading);
		}
		return GroundAbove(near);
	}

	private unsafe static Vector3 GroundAbove(Vector3 p)
	{
		float num = default(float);
		if (MISC.GET_GROUND_Z_FOR_3D_COORD(p.X, p.Y, p.Z + 1.5f, &num, p4: false) && Math.Abs(num - p.Z) < 3f)
		{
			p.Z = num;
		}
		return p;
	}

	private unsafe void DespawnGeneral()
	{
		if (_general != null)
		{
			Kids.StoryProtected.Remove(_general.Handle);
		}
		if (_genBlip != null && _genBlip.Exists())
		{
			_genBlip.Delete();
		}
		_genBlip = null;
		foreach (Ped item in _genEscort.Concat(new Ped[1] { _general }))
		{
			if (!(item == null) && item.Exists())
			{
				_worldNemeses.Remove(item.Handle);
				int handle = item.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		_genEscort.Clear();
		_general = null;
		_generalRec = null;
		if (_gatesAdded.Count > 0 && Game.Player.Character.Position.DistanceTo(FortGate) > 300f)
		{
			ReleaseFortGates();
		}
	}
}
