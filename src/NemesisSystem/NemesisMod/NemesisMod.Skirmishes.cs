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
	private readonly List<Ped> _skirmish = new List<Ped>();

	private int _skirmishEnds;

	private Vector3 _skirmishAt;

	private int _skirmishBlip;

	private const uint FightBlipStyle = 1291203949u;

	private uint _sideA;

	private uint _sideB;

	internal string SkirmishNote = "";

	private static Dictionary<string, PedHash[]> _factionModels;

	private static int _factionModelsVer = -1;

	private readonly Dictionary<int, NemesisRecord> _skirmishNamed = new Dictionary<int, NemesisRecord>();

	private (Gang g, Vector3 at)? _pendingSide;

	private int _pendingSideAt;

	private int _boomsLeft;

	private int _nextBoomAt;

	private bool _turnedA;

	private bool _turnedB;

	private int _nextTurnCheck;

	private readonly List<Ped> _hunters = new List<Ped>();

	private int _huntersEnd;

	private uint _huntersGroup;

	private readonly List<Ped> _hunterHorses = new List<Ped>();

	private readonly List<Blip> _hunterBlips = new List<Blip>();

	private NemesisRecord _huntersBy;

	private WorldState _chron;

	private int _nextWorldCheck;

	private static Dictionary<string, PedHash[]> FactionModels
	{
		get
		{
			if (_factionModels == null || _factionModelsVer != Gangs.Version)
			{
				_factionModels = Gangs.All.Where((Gang g) => g.TroopModels != null && g.TroopModels.Length != 0).ToDictionary((Gang g) => g.Id, (Gang g) => g.TroopModels);
				_factionModelsVer = Gangs.Version;
			}
			return _factionModels;
		}
	}

	private bool TrySkirmish(Ped player, bool town, bool test, bool rolled = false, (Gang a, Gang b)? force = null, bool distant = false)
	{
		if (_skirmish.Count > 0 || (town && !test))
		{
			return false;
		}
		if (force.HasValue)
		{
			rolled = true;
		}
		(Gang, Gang)? tuple = WarHere;
		if (tuple.HasValue && (!FactionModels.ContainsKey(tuple.Value.Item1.Id) || !FactionModels.ContainsKey(tuple.Value.Item2.Id)))
		{
			tuple = null;
		}
		if (!test && !rolled && _rng.Next(100) >= Balance.SkirmishChance * ((!tuple.HasValue) ? 1 : 3))
		{
			return false;
		}
		List<(Gang, Gang)> list = new List<(Gang, Gang)>();
		if (force.HasValue)
		{
			list.Add(force.Value);
		}
		else if (tuple.HasValue)
		{
			list.Add((tuple.Value.Item1, tuple.Value.Item2));
		}
		else
		{
			District currentDistrict = CurrentDistrict;
			if (currentDistrict != null)
			{
				Gang[] all = Gangs.All;
				foreach (Gang gang in all)
				{
					Gang[] all2 = Gangs.All;
					foreach (Gang gang2 in all2)
					{
						if (string.CompareOrdinal(gang.Id, gang2.Id) < 0 && !gang.StoryOnly && !gang2.StoryOnly && FactionModels.ContainsKey(gang.Id) && FactionModels.ContainsKey(gang2.Id) && Relation(gang.Id, gang2.Id) <= Config.ClashLine)
						{
							int num = HoldOf(currentDistrict, gang.Id);
							int num2 = HoldOf(currentDistrict, gang2.Id);
							if ((num >= 5 && num2 >= 5) || (gang.OrdersNotFeelings && num2 >= 10) || (gang2.OrdersNotFeelings && num >= 10))
							{
								list.Add((gang, gang2));
							}
						}
					}
				}
			}
		}
		if (test && list.Count == 0)
		{
			(Gang, Gang) tuple2 = (from x in Gangs.All
				where !x.StoryOnly && FactionModels.ContainsKey(x.Id) && !IsDestroyed(x)
				from y in from y in Gangs.All
					where string.CompareOrdinal(x.Id, y.Id) < 0 && !y.StoryOnly && FactionModels.ContainsKey(y.Id) && !IsDestroyed(y)
					select y
				select (x: x, y: y) into t
				orderby Relation(t.x.Id, t.y.Id)
				select t).FirstOrDefault();
			if (tuple2.Item1 != null)
			{
				list.Add((tuple2.Item1, tuple2.Item2));
			}
		}
		if (list.Count == 0)
		{
			return false;
		}
		list.RemoveAll(((Gang a, Gang b) p) => TruceBetween(p.a.Id, p.b.Id) || IsDestroyed(p.a) || IsDestroyed(p.b));
		if (list.Count == 0)
		{
			return false;
		}
		var (gang3, gang4) = list[_rng.Next(list.Count)];
		if (distant && _rng.Next(2) == 0)
		{
			Gang gang5 = gang4;
			gang4 = gang3;
			gang3 = gang5;
		}
		_skirmishGangA = gang3.Id;
		_skirmishGangB = gang4.Id;
		Vector3 fwd = Forward(player);
		Vector3 vector = new Vector3(fwd.Y, 0f - fwd.X, 0f);
		_skirmishAt = (distant ? DistantSpot(player) : GoodSpot(player, delegate
		{
			double num6 = Math.Atan2(fwd.Y, fwd.X) + (_rng.NextDouble() * 2.0 - 1.0) * 1.4;
			float num7 = 110f + (float)_rng.NextDouble() * 50f;
			return Ground(player.Position + new Vector3((float)Math.Cos(num6) * num7, (float)Math.Sin(num6) * num7, 0f));
		}, 14));
		if (_skirmishAt == Vector3.Zero)
		{
			GrudgeLog("SKIRMISH no dry ground near you - not sent");
			return false;
		}
		if (_sideA == 0)
		{
			_sideA = World.AddRelationshipGroup("SKIRMISH_A");
			_sideB = World.AddRelationshipGroup("SKIRMISH_B");
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _sideA, _sideB);
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _sideB, _sideA);
		int relationship = (distant ? 3 : 4);
		World.SetRelationshipBetweenGroups((eRelationshipType)relationship, _sideA, player.RelationshipGroup);
		World.SetRelationshipBetweenGroups((eRelationshipType)relationship, _sideB, player.RelationshipGroup);
		SkirmishDistant = distant;
		float num3 = (distant ? 22f : 12f);
		int num4 = SpawnSide(gang3, _sideA, _skirmishAt + vector * (0f - num3), distant ? 12 : 50, distant ? 2 : 0);
		int num5 = 0;
		if (distant)
		{
			_pendingSide = (gang4, _skirmishAt + vector * num3);
			_pendingSideAt = Game.GameTime + 2000;
		}
		else
		{
			num5 = SpawnSide(gang4, _sideB, _skirmishAt + vector * num3);
			foreach (Ped item in _skirmish.Where((Ped p) => p.Exists()))
			{
				TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(item.Handle, 80f, 0, 0uL);
			}
		}
		_skirmishEnds = Game.GameTime + (distant ? 480000 : 300000);
		SkirmishNote = string.Format("{0} ({1}) vs {2} ({3}) in {4}{5}", gang3.Name, num4, gang4.Name, num5, RegionName, distant ? $" - distant, {_skirmishAt.DistanceTo(player.Position):0} m off" : "");
		if (distant)
		{
			Feed("WAR gunfire, far off to the " + Compass(player.Position, _skirmishAt) + " - the " + gang3.Name + " and the " + gang4.Name, Theme.Warn);
			GrudgeLog("SKIRMISH " + SkirmishNote);
			return true;
		}
		_skirmishBlip = AddFightBlip(_skirmishAt, gang3.Name + " vs " + gang4.Name);
		GrudgeNotice("Gunfire ahead: the " + gang3.Name + " and the " + gang4.Name + " are at each other's throats.");
		GrudgeLog("SKIRMISH " + SkirmishNote);
		return true;
	}

	private void ChronicleSkirmish(Ped player)
	{
		Gang gang = Gangs.ById(_skirmishGangA);
		Gang gang2 = Gangs.ById(_skirmishGangB);
		if (gang == null || gang2 == null || !_skirmish.Any((Ped p) => p != null && p.Exists()))
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		foreach (Ped item in _skirmish.Where((Ped p) => p != null && p.Exists()))
		{
			bool num5 = ModGroupOf(item.Handle) == _sideA;
			bool flag = item.IsDead && !LooksKnockedOut(item);
			if (num5)
			{
				if (flag)
				{
					num3++;
				}
				else
				{
					num++;
				}
			}
			else if (flag)
			{
				num4++;
			}
			else
			{
				num2++;
			}
		}
		List<WorldPart> list = new List<WorldPart>();
		int num6 = 0;
		int num7 = 0;
		foreach (KeyValuePair<int, NemesisRecord> item2 in _skirmishNamed)
		{
			NemesisRecord value = item2.Value;
			Ped ped = Entity.FromHandle(item2.Key) as Ped;
			bool flag2 = !value.IsAlive || value.PresumedDead || (ped != null && ped.Exists() && ped.IsDead && !LooksKnockedOut(ped));
			bool flag3 = !flag2 && ped != null && ped.Exists() && (float)ped.Health < (float)ped.MaxHealth * 0.6f;
			if (flag2)
			{
				if (value.Gang == gang.Id)
				{
					num6++;
				}
				else
				{
					num7++;
				}
			}
			WorldPart worldPart = PartFor(value, flag2 ? "KILLED" : (flag3 ? "WOUNDED" : "UNHURT"), value.Title);
			worldPart.SecretlyLived = flag2 && value.IsAlive;
			list.Add(worldPart);
		}
		bool flag4 = _skirmish.Any((Ped p) => p != null && p.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true));
		List<string> list2 = new List<string>();
		list2.Add((num > num2) ? ("The " + gang.Name + " held the field.") : ((num2 > num) ? ("The " + gang2.Name + " held the field.") : "Neither side came out ahead."));
		list2.Add($"{gang.Name}: {num3} down.  {gang2.Name}: {num4} down.");
		List<string> list3 = list2;
		if (flag4)
		{
			list3.Add("You joined in.");
		}
		else if (SkirmishDistant)
		{
			list3.Add((player.Position.DistanceTo(_skirmishAt) > 120f) ? "You heard it from a way off." : "You heard it, and rode over to see.");
		}
		Dictionary<string, int> dictionary = new Dictionary<string, int>
		{
			[gang.Id] = Math.Max(0, num3 - num6),
			[gang2.Id] = Math.Max(0, num4 - num7)
		};
		LoseMen(gang, Math.Max(0, dictionary[gang.Id] - YoursOf(_sideA)), "a skirmish");
		LoseMen(gang2, Math.Max(0, dictionary[gang2.Id] - YoursOf(_sideB)), "a skirmish");
		bool flag5 = false;
		if (num != num2)
		{
			try
			{
				flag5 = SkirmishForWar(_skirmishAt, (num > num2) ? gang : gang2, (num > num2) ? gang2 : gang, (num > num2) ? num4 : num3, (num > num2) ? num3 : num4, list3);
			}
			catch (Exception ex)
			{
				GrudgeLog("WAR skirmish note failed: " + ex.Message);
			}
		}
		ChronicleWitness((SkirmishDistant && !flag4) ? ("Gunfire in the distance: the " + gang.Name + " and the " + gang2.Name) : ("The " + gang.Name + " and the " + gang2.Name + " fought"), "near " + GetZoneName(_skirmishAt), _skirmishAt, new string[2] { gang.Id, gang2.Id }, list, dictionary, list3);
		NoteLiveClash(gang.Id, gang2.Id, num6 + num7, "near " + GetZoneName(_skirmishAt), _skirmishAt);
		if (num == num2 || flag5)
		{
			return;
		}
		try
		{
			ControlAfterSkirmish(_skirmishAt, (num > num2) ? gang : gang2, (num > num2) ? gang2 : gang);
		}
		catch
		{
		}
		int YoursOf(uint grp)
		{
			return _skirmish.Count((Ped p) => p != null && p.Exists() && p.IsDead && ModGroupOf(p.Handle) == grp && PED.GET_PED_SOURCE_OF_DEATH(p.Handle) == player.Handle && !_worldNemeses.ContainsKey(p.Handle));
		}
	}

	private int SpawnSide(Gang g, uint group, Vector3 at, int namedPct = 50, int extra = 0)
	{
		int num = 3 + _rng.Next(3) + extra;
		PedHash[] array = FactionModels[g.Id];
		NemesisRecord nemesisRecord = ((_rng.Next(100) < namedPct) ? (from _ in Roster
			where _.Gang == g.Id && _.CanHunt && !_.PresumedDead && !_worldNemeses.ContainsValue(_) && _ != ActiveRecord && (_.Id == null || !_.Id.StartsWith("lore_")) && _.LoreModel == null && SeatOf(g)?.Successor != _.Id
			orderby _rng.Next()
			select _).FirstOrDefault() : null);
		int num2 = 0;
		for (int num3 = 0; num3 < num; num3++)
		{
			float num4 = ((extra > 0) ? 14f : 8f);
			Vector3 pos = Ground(at + new Vector3((float)_rng.NextDouble() * num4 - num4 / 2f, (float)_rng.NextDouble() * num4 - num4 / 2f, 0f));
			NemesisRecord nemesisRecord2 = ((num3 == 0) ? nemesisRecord : null);
			Crumbs.Native($"skirmish spawn {g.Id} {num3 + 1}/{num}");
			Ped ped = SpawnPed((PedHash)(((int?)nemesisRecord2?.ModelHash) ?? ((int)array[_rng.Next(array.Length)])), pos, _rng.Next(360));
			if (!(ped == null))
			{
				MakeOurs(ped.Handle);
				WEAPON.REMOVE_ALL_PED_WEAPONS(ped.Handle, p1: true, p2: true);
				if (g.TribalKit)
				{
					ArmTroop(ped, g);
				}
				else
				{
					ped.GiveWeapon(g.TroopLongGuns ? eWeapon.RepeaterCarbine : eWeapon.RevolverCattleman, 120, g.TroopLongGuns ? eWeaponAttachPoint.Rifle : eWeaponAttachPoint.PistolR, bForceInHand: true);
				}
				ped.RelationshipGroup = group;
				ped.BlockPermanentEvents = false;
				ApplyFactionStyle(ped, g.Id);
				if (nemesisRecord2 != null)
				{
					_worldNemeses[ped.Handle] = nemesisRecord2;
					_skirmishNamed[ped.Handle] = nemesisRecord2;
					ApplyScars(ped, nemesisRecord2);
				}
				_skirmish.Add(ped);
				num2++;
			}
		}
		if (num2 < num)
		{
			GrudgeLog($"SKIRMISH {g.Id}: {num2} of {num} men spawned");
		}
		return num2;
	}

	private int DistantExplosionTag => Config.DistantExplosionTag;

	private void HeatTheDistantBattle()
	{
		Gang ga = Gangs.ById(_skirmishGangA);
		WarRecord warRecord = ((ga != null) ? WarOf(ga, _skirmishAt) : null);
		if (warRecord == null && ga != null && _skirmishGangB != null)
		{
			warRecord = Wars.FirstOrDefault((WarRecord x) => x.Result == null && ((x.A == ga.Id && x.B == _skirmishGangB) || (x.B == ga.Id && x.A == _skirmishGangB)));
		}
		if (warRecord != null)
		{
			InitHeat(warRecord);
		}
		int num = warRecord?.Heat ?? 0;
		_boomsLeft = ((num >= 75) ? 4 : ((num >= 55) ? 3 : 2));
		_nextBoomAt = Game.GameTime + 6000 + _rng.Next(8000);
		if (num < 45)
		{
			GrudgeLog($"SKIRMISH distant: heat {num} - no riflemen, {_boomsLeft} explosion(s) to come");
			return;
		}
		int num2 = ((num < 70) ? 1 : 2);
		uint[] array = new uint[2] { _sideA, _sideB };
		foreach (uint grp in array)
		{
			foreach (Ped item in (from _ in _skirmish
				where _.Exists() && !_.IsDead && ModGroupOf(_.Handle) == grp
				orderby _rng.Next()
				select _).Take(num2))
			{
				item.GiveWeapon((_rng.Next(2) == 0) ? eWeapon.RifleBoltAction : eWeapon.RifleSpringfield, 60, eWeaponAttachPoint.Rifle, bForceInHand: true);
				PED.SET_PED_COMBAT_RANGE(item.Handle, 2);
				PED.SET_PED_ACCURACY(item.Handle, 25 + _rng.Next(15));
			}
		}
		GrudgeLog($"SKIRMISH distant: heat {num} - {num2} rifleman a side, {_boomsLeft} explosion(s) to come");
	}

	private void DistantBooms(Ped player)
	{
		if (_boomsLeft <= 0 || Game.GameTime < _nextBoomAt)
		{
			return;
		}
		if (player.Position.DistanceTo(_skirmishAt) < 120f || WorldBusy(player) || !MapSettled())
		{
			_nextBoomAt = Game.GameTime + 5000;
			return;
		}
		List<Ped> list = _skirmish.Where((Ped p) => p.Exists() && !p.IsDead).ToList();
		if (list.Count == 0)
		{
			_boomsLeft = 0;
			return;
		}
		Vector3 position = list[_rng.Next(list.Count)].Position;
		double num = _rng.NextDouble() * Math.PI * 2.0;
		float num2 = 5f + (float)_rng.NextDouble() * 4f;
		Vector3 vector = new Vector3(position.X + (float)Math.Cos(num) * num2, position.Y + (float)Math.Sin(num) * num2, position.Z);
		Ped owner = BoomOwner(list, vector);
		if (!ModExplosion(vector, 0f, owner, "skirmish explosion"))
		{
			_nextBoomAt = Game.GameTime + 5000;
			return;
		}
		QueueVolley(vector, owner, "skirmish explosion");
		_boomsLeft--;
		_nextBoomAt = Game.GameTime + 10000 + _rng.Next(15000);
		GrudgeLog($"SKIRMISH boom at {vector.X:0},{vector.Y:0} ({player.Position.DistanceTo(vector):0} m from you, tag {DistantExplosionTag}), {_boomsLeft} left");
	}

	private void SkirmishTurnsOnYou(Ped player)
	{
		if ((_turnedA && _turnedB) || Game.GameTime < _nextTurnCheck)
		{
			return;
		}
		_nextTurnCheck = Game.GameTime + 400;
		List<Ped> list = _skirmish.Where((Ped p) => p != null && p.Exists() && !p.IsDead).ToList();
		if (list.Count == 0)
		{
			return;
		}
		Vector3 me = player.Position;
		bool flag = false;
		bool flag2 = false;
		foreach (Ped item in _skirmish)
		{
			if (!(item == null) && item.Exists() && ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(item.Handle, player.Handle, p2: true, p3: true))
			{
				if (ModGroupOf(item.Handle) == _sideA)
				{
					flag = true;
				}
				else
				{
					flag2 = true;
				}
			}
		}
		if (!flag && !flag2 && (GunMelee.PlayerFiredRecently || PED.IS_PED_SHOOTING(player.Handle)) && !GameEventGunfire)
		{
			Ped ped = (from p in list
				where p.Position.DistanceTo(me) < 60f
				orderby p.Position.DistanceTo(me)
				select p).FirstOrDefault();
			if (ped != null)
			{
				if (ModGroupOf(ped.Handle) == _sideA)
				{
					flag = true;
				}
				else
				{
					flag2 = true;
				}
			}
		}
		if (flag && !_turnedA)
		{
			_turnedA = true;
			TurnSide(_sideA, player, list);
		}
		if (flag2 && !_turnedB)
		{
			_turnedB = true;
			TurnSide(_sideB, player, list);
		}
	}

	private void TurnSide(uint grp, Ped player, List<Ped> alive)
	{
		Crumbs.Native("skirmish turns on you");
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, grp, player.RelationshipGroup);
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, player.RelationshipGroup, grp);
		int num = 0;
		foreach (Ped item in alive)
		{
			if (ModGroupOf(item.Handle) == grp)
			{
				PED.SET_PED_ACCURACY(item.Handle, 25 + _rng.Next(15));
				if (!(item.Position.DistanceTo(player.Position) > 120f))
				{
					TASK.TASK_COMBAT_PED(item.Handle, player.Handle, 0, 16);
					num++;
				}
			}
		}
		string arg = ((grp == _sideA) ? "first" : "second");
		GrudgeLog(string.Format("SKIRMISH you joined in - the {0} side turns on you ({1} men coming{2})", arg, num, SkirmishDistant ? ", a distant battle" : ""));
	}

	private unsafe void UpdateSkirmish(Ped player)
	{
		if (_skirmish.Count == 0)
		{
			_pendingSide = null;
			_turnedA = (_turnedB = false);
			return;
		}
		if (_pendingSide.HasValue)
		{
			if (Game.GameTime < _pendingSideAt)
			{
				return;
			}
			(Gang, Vector3) value = _pendingSide.Value;
			_pendingSide = null;
			int num = SpawnSide(value.Item1, _sideB, value.Item2, 12, 2);
			Crumbs.Native("skirmish combat tasks");
			foreach (Ped item2 in _skirmish.Where((Ped p) => p.Exists()))
			{
				PED.SET_PED_ACCURACY(item2.Handle, 8 + _rng.Next(10));
				TASK.TASK_COMBAT_HATED_TARGETS_AROUND_PED(item2.Handle, 120f, 0, 0uL);
			}
			GrudgeLog($"SKIRMISH distant: the {value.Item1.Name} ({num}) are there too - fighting");
			HeatTheDistantBattle();
			return;
		}
		if (SkirmishDistant)
		{
			DistantBooms(player);
		}
		if (UpdateWarBuildUp(player))
		{
			return;
		}
		SkirmishTurnsOnYou(player);
		bool flag = (_turnedA || _turnedB) && _skirmish.Any((Ped p) => p.Exists() && !p.IsDead && p.Position.DistanceTo(player.Position) < 250f && ((_turnedA && ModGroupOf(p.Handle) == _sideA) || (_turnedB && ModGroupOf(p.Handle) == _sideB)));
		bool flag2 = !_skirmish.Any((Ped p) => p.Exists() && !p.IsDead && ModGroupOf(p.Handle) == _sideA) || !_skirmish.Any((Ped p) => p.Exists() && !p.IsDead && ModGroupOf(p.Handle) == _sideB);
		if ((Game.GameTime <= _skirmishEnds || (flag && Game.GameTime <= _skirmishEnds + 180000)) && !(player.Position.DistanceTo(_skirmishAt) > 400f) && (!flag2 || flag))
		{
			return;
		}
		int num2 = _skirmish.Count((Ped p) => p != null && p.Exists() && p.IsDead && ModGroupOf(p.Handle) == _sideA);
		int num3 = _skirmish.Count((Ped p) => p != null && p.Exists() && p.IsDead && ModGroupOf(p.Handle) == _sideB);
		GrudgeLog(string.Format("SKIRMISH ending{0}: {1} + {2} dead of {3}, {4:0} m from you, {5}", SkirmishDistant ? " (distant)" : "", num2, num3, _skirmish.Count, player.Position.DistanceTo(_skirmishAt), (Game.GameTime > _skirmishEnds) ? "time up" : ((player.Position.DistanceTo(_skirmishAt) > 400f) ? "you rode off" : "one side is down")));
		try
		{
			ChronicleSkirmish(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("WORLD skirmish chronicle failed: " + ex.Message);
		}
		_skirmishNamed.Clear();
		List<Ped> list = new List<Ped>();
		List<Ped> list2 = new List<Ped>();
		foreach (Ped item3 in _skirmish)
		{
			if (item3 == null || !item3.Exists())
			{
				continue;
			}
			int handle = item3.Handle;
			if (!item3.IsDead && !ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(handle, player.Handle, p2: true, p3: true))
			{
				try
				{
					((ModGroupOf(handle) == _sideA) ? list : list2).Add(item3);
					PED.SET_PED_RELATIONSHIP_GROUP_HASH(handle, PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(handle));
					ForgetSwitch(handle);
					TASK.CLEAR_PED_TASKS(handle, p1: false, p2: false);
					Flee(handle, player.Handle, 300f, -1, 1f);
				}
				catch
				{
				}
			}
			ReleaseIfOurs(&handle);
		}
		_skirmish.Clear();
		(List<Ped>, string)[] array = new(List<Ped>, string)[2]
		{
			(list, _skirmishGangA),
			(list2, _skirmishGangB)
		};
		for (int num4 = 0; num4 < array.Length; num4++)
		{
			(List<Ped>, string) tuple = array[num4];
			List<Ped> item = tuple.Item1;
			Gang gang = Gangs.ById(tuple.Item2);
			RegisterBand(item, gang, (gang != null) ? ("the " + OutfitWord(gang) + " men left from the skirmish") : "the men left from the skirmish");
		}
		if (_sideA != 0)
		{
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _sideA, player.RelationshipGroup);
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _sideB, player.RelationshipGroup);
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, player.RelationshipGroup, _sideA);
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, player.RelationshipGroup, _sideB);
		}
		_turnedA = (_turnedB = false);
		Crumbs.Native("World blip remove");
		if (_skirmishBlip != 0)
		{
			int skirmishBlip = _skirmishBlip;
			MAP.REMOVE_BLIP(&skirmishBlip);
			_skirmishBlip = 0;
		}
		ReleaseWarHorses();
		GrudgeLog("SKIRMISH over");
		SkirmishDistant = false;
		_boomsLeft = 0;
		SkirmishNote = "";
	}

	internal bool HiresHunters(NemesisRecord r)
	{
		if (r.CanHunt && !r.PresumedDead && r.Wealth == Wealth.Rich && r.HateTier != HateTier.Resentful && (r.Personality == Personality.Coward || EffectiveFear(r) >= 50))
		{
			return IntentOf(r) != Intent.Hunts;
		}
		return false;
	}

	private void SendBountyHunters(NemesisRecord rec, Ped player)
	{
		if (_hunters.Count > 0)
		{
			return;
		}
		_huntersBy = rec;
		NoteCame(rec);
		SetWorldPaused(paused: false);
		Vector3 fwd = Forward(player);
		Vector3 vector = GoodSpot(player, () => Ground(player.Position - fwd * 110f + new Vector3(fwd.Y, 0f - fwd.X, 0f) * ((float)_rng.NextDouble() * 60f - 30f)));
		if (vector == Vector3.Zero)
		{
			GrudgeLog("HUNTERS no dry ground behind you - not sent");
			return;
		}
		if (_huntersGroup == 0)
		{
			_huntersGroup = World.AddRelationshipGroup("NEMESIS_HUNTERS");
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _huntersGroup, player.RelationshipGroup);
		int num = 3 + CrewBonus(Gangs.ById(rec.Gang));
		for (int num2 = 0; num2 < num; num2++)
		{
			Ped ped = SpawnPed(PedHash.g_m_m_bountyhunters_01, Ground(vector + new Vector3((float)num2 * 2f, (float)num2 * 1.5f, 0f)), HeadingTo(vector, player.Position));
			if (!(ped == null))
			{
				MakeOurs(ped.Handle);
				WEAPON.REMOVE_ALL_PED_WEAPONS(ped.Handle, p1: true, p2: true);
				ped.GiveWeapon((num2 == 0) ? eWeapon.ShotgunPump : eWeapon.RepeaterWinchester, 120, eWeaponAttachPoint.Rifle, bForceInHand: true);
				ped.Accuracy = 35;
				ped.BlockPermanentEvents = true;
				ped.RelationshipGroup = _huntersGroup;
				Ped ped2 = MountOnHorse(ped);
				if (ped2 != null)
				{
					_encHorses.Remove(ped2);
					_hunterHorses.Add(ped2);
				}
				TASK.TASK_COMBAT_PED(ped.Handle, player.Handle, 0, 16);
				PED.SET_PED_KEEP_TASK(ped.Handle, toggle: true);
				_followerOf[ped.Handle] = rec;
				Crumbs.Native("World blip add (V2)");
				_hunterBlips.Add(ped.AddBlip(BlipType.BLIP_STYLE_ENEMY));
				_hunters.Add(ped);
			}
		}
		_huntersEnd = Game.GameTime + 240000;
		rec.TimesAmbushed++;
		rec.Touch();
		SaveRoster();
		GrudgeNotice("Bounty hunters on your trail - paid for by " + rec.FullName + ".");
		GrudgeLog($"HUNTERS x{num} hired by {rec.Name} (rich {rec.Personality}, fear {EffectiveFear(rec)})");
	}

	private unsafe void UpdateHunters(Ped player)
	{
		if (_hunters.Count == 0 || (Game.GameTime < _huntersEnd && _hunters.Any((Ped h) => h.Exists() && !h.IsDead && h.Position.DistanceTo(player.Position) < 350f)))
		{
			return;
		}
		List<Ped> list = _hunters.Where((Ped h) => h != null && h.Exists() && h.IsDead && PED.GET_PED_SOURCE_OF_DEATH(h.Handle) == player.Handle).ToList();
		if (list.Count > 0)
		{
			try
			{
				Vector3 position = list[0].Position;
				string text = ((_huntersBy != null) ? (" sent by " + _huntersBy.Name) : "");
				ChronicleDeed("hunters", (list.Count == 1) ? ("You killed a bounty hunter" + text) : $"You killed {list.Count} bounty hunters{text}", "near " + GetZoneName(position), position, _huntersBy?.Gang, (_huntersBy == null) ? new WorldPart[0] : new WorldPart[1] { PartFor(_huntersBy, "SENT THEM", "hired the posse") });
			}
			catch
			{
			}
		}
		_huntersBy = null;
		foreach (Ped hunter in _hunters)
		{
			if (!(hunter == null) && hunter.Exists())
			{
				int handle = hunter.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		_hunters.Clear();
		foreach (Ped hunterHorse in _hunterHorses)
		{
			if (!(hunterHorse == null) && hunterHorse.Exists())
			{
				int handle2 = hunterHorse.Handle;
				ReleaseIfOurs(&handle2);
			}
		}
		_hunterHorses.Clear();
		foreach (Blip hunterBlip in _hunterBlips)
		{
			if (hunterBlip != null && hunterBlip.Exists())
			{
				hunterBlip.Delete();
			}
		}
		_hunterBlips.Clear();
	}
}
