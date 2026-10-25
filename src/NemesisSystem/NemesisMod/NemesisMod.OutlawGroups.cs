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
	private const ulong GetRelationshipBetweenGroupsFn = 11415340851948334684uL;

	private static readonly string[] GameOutlawGroupNames = new string[9] { "rel_gang_odriscoll", "rel_gang_lemoyne_raiders", "rel_gang_murfree_brood", "rel_gang_skinner_brothers", "rel_gang_creole", "rel_gang_laramie_gang", "rel_gang_smugglers", "rel_criminals", "rel_gunslingers" };

	private const int SwitchBackMs = 8000;

	private const int AttackAimMs = 1000;

	private const int DeadEyeAimMs = 6000;

	private const ulong IsSpecialAbilityActiveFn = 12781818049324475888uL;

	private readonly Dictionary<int, int> _inSightsAt = new Dictionary<int, int>();

	private static readonly eInputType[] AttackInputs = new eInputType[18]
	{
		eInputType.Attack,
		eInputType.Attack2,
		eInputType.MeleeAttack,
		eInputType.MeleeGrappleAttack,
		eInputType.MeleeGrappleChoke,
		eInputType.HorseAttack,
		eInputType.HorseAttack2,
		eInputType.MeleeHorseAttackPrimary,
		eInputType.MeleeHorseAttackSecondary,
		eInputType.VehAttack,
		eInputType.VehAttack2,
		eInputType.VehPassengerAttack,
		eInputType.VehCarAttack,
		eInputType.VehCarAttack2,
		eInputType.VehDraftAttack,
		eInputType.VehDraftAttack2,
		eInputType.VehBoatAttack,
		eInputType.VehBoatAttack2
	};

	private Dictionary<uint, string> _gameOutlawGroups;

	private uint _relCriminals;

	private readonly Dictionary<int, (uint mod, uint game, int last)> _outlawSwitched = new Dictionary<int, (uint, uint, int)>();

	private readonly HashSet<string> _lawGroupSaid = new HashSet<string>();

	private int _nextOutlawGroups;

	private int _outlawSwitchLogs;

	private bool _lawGroupsProbed;

	private void EnsureGameOutlawGroups()
	{
		if (_gameOutlawGroups == null)
		{
			_gameOutlawGroups = new Dictionary<uint, string>();
			string[] gameOutlawGroupNames = GameOutlawGroupNames;
			foreach (string text in gameOutlawGroupNames)
			{
				_gameOutlawGroups[MISC.GET_HASH_KEY(text)] = text;
			}
			_relCriminals = MISC.GET_HASH_KEY("rel_criminals");
		}
	}

	private static string RelName(int r)
	{
		return r switch
		{
			7 => "dead", 
			6 => "hate", 
			5 => "wanted", 
			4 => "dislike", 
			3 => "ignore", 
			2 => "like", 
			1 => "respect", 
			0 => "none", 
			_ => r.ToString(), 
		};
	}

	private static int RelBetween(uint a, uint b)
	{
		try
		{
			return Function.Call<int>(11415340851948334684uL, new InputArgument[2] { a, b });
		}
		catch
		{
			return -1;
		}
	}

	private string GroupName(uint g)
	{
		if (_gameOutlawGroups == null || !_gameOutlawGroups.TryGetValue(g, out var value))
		{
			return $"0x{g:X8}";
		}
		return value;
	}

	internal void ForgetSwitch(int h)
	{
		_outlawSwitched.Remove(h);
	}

	internal uint ModGroupOf(int h)
	{
		uint num = PED.GET_PED_RELATIONSHIP_GROUP_HASH(h);
		if (!_outlawSwitched.TryGetValue(h, out (uint, uint, int) value) || value.Item1 == 0 || num != value.Item2)
		{
			return num;
		}
		return value.Item1;
	}

	internal bool OutlawMan(int h)
	{
		if (!ENTITY.DOES_ENTITY_EXIST(h) || (RoleOf(h) == OurRole.None && RecordAt(h) == null && !Registered(h) && BandOf(h) == null) || RoleOf(h) == OurRole.Companion)
		{
			return false;
		}
		return FairOutfit(Gangs.ById(RecordAt(h)?.Gang) ?? Gangs.ForModel(ENTITY.GET_ENTITY_MODEL(h)));
	}

	private bool DrewOnYouNow(int h)
	{
		if (!_combatants.TryGetValue(h, out var value) || Game.GameTime - value > 3000)
		{
			return false;
		}
		OurRole ourRole = RoleOf(h);
		if (ourRole == OurRole.Companion || (ourRole == OurRole.None && RecordAt(h) == null && !_provoked.ContainsKey(h)))
		{
			return false;
		}
		if ((Gangs.ById(RecordAt(h)?.Gang) ?? Gangs.ForModel(ENTITY.GET_ENTITY_MODEL(h))) != null)
		{
			return false;
		}
		if (IsLawman(new Ped(h)) || XRayHandsOff(h) || !CameAtYou(h))
		{
			return false;
		}
		if (!DrewOnYou(h))
		{
			return false;
		}
		_drewOnYouAt[h] = Game.GameTime;
		return true;
	}

	private uint GameGroupOf(int h, Gang g)
	{
		EnsureGameOutlawGroups();
		uint num = 0u;
		try
		{
			num = PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(h);
		}
		catch
		{
		}
		bool flag = _gameOutlawGroups.ContainsKey(num);
		uint result = (flag ? num : _relCriminals);
		if (_lawGroupSaid.Add(g.Id))
		{
			GrudgeLog("LAWGROUP " + g.Name + ": yours to kill, their men go in the game's " + (flag ? (GroupName(num) + " (their model's own)") : $"rel_criminals (their model's own, 0x{num:X8}, isn't one of the game's outlaw groups)"));
		}
		return result;
	}

	private IEnumerable<uint> ModGroups()
	{
		uint[] array = new uint[14]
		{
			_nemesisGroup, _campGroup, _checkpointGroup, _beatenGroup, _surrenderGroup, _rivalGroup, _saloonGrpA, _saloonGrpB, _survivorGroup, _townRelGroup,
			_townFightGroup, _huntersGroup, _generalGroup, _lawTestHateGroup
		};
		foreach (uint num in array)
		{
			if (num != 0)
			{
				yield return num;
			}
		}
		if (_sideA != 0)
		{
			yield return _sideA;
		}
		if (_sideB != 0)
		{
			yield return _sideB;
		}
		foreach (uint value in _factionCampGroups.Values)
		{
			if (value != 0)
			{
				yield return value;
			}
		}
		foreach (uint value2 in _patrolGroups.Values)
		{
			if (value2 != 0)
			{
				yield return value2;
			}
		}
	}

	private void CarryHostility(uint mod, uint game)
	{
		if (mod == 0 || mod == game)
		{
			return;
		}
		bool flag = _outlawSwitched.Values.Any(((uint mod, uint game, int last) s) => s.game == game && s.mod != 0 && s.mod != mod);
		if (!flag)
		{
			World.SetRelationshipBetweenGroups(eRelationshipType.Respect, game, mod);
			World.SetRelationshipBetweenGroups(eRelationshipType.Respect, mod, game);
		}
		foreach (uint item in ModGroups().Distinct())
		{
			if (item != mod && item != game)
			{
				int num = RelBetween(mod, item);
				int num2 = RelBetween(item, mod);
				bool flag2 = num == 4 || num == 6;
				bool flag3 = num2 == 4 || num2 == 6;
				if (!flag || flag2)
				{
					World.SetRelationshipBetweenGroups((num >= 1 && num <= 6) ? ((eRelationshipType)num) : eRelationshipType.Ignore, game, item);
				}
				if (!flag || flag3)
				{
					World.SetRelationshipBetweenGroups((num2 >= 1 && num2 <= 6) ? ((eRelationshipType)num2) : eRelationshipType.Ignore, item, game);
				}
			}
		}
	}

	private void SwitchOutlaw(int h, string why, int now)
	{
		if (h == 0 || !ENTITY.DOES_ENTITY_EXIST(h) || !ENTITY.IS_ENTITY_A_PED(h) || !PED.IS_PED_HUMAN(h))
		{
			return;
		}
		Ped ped = new Ped(h);
		if (ped.IsDead && !LooksKnockedOut(ped))
		{
			return;
		}
		bool flag = OutlawMan(h);
		if (!flag && !DrewOnYouNow(h))
		{
			return;
		}
		Gang g = (flag ? (Gangs.ById(RecordAt(h)?.Gang) ?? Gangs.ForModel((uint)ped.Model.Hash)) : null);
		uint num = PED.GET_PED_RELATIONSHIP_GROUP_HASH(h);
		(uint, uint, int) value;
		bool flag2 = _outlawSwitched.TryGetValue(h, out value);
		EnsureGameOutlawGroups();
		uint num2 = (flag2 ? value.Item2 : (flag ? GameGroupOf(h, g) : _relCriminals));
		uint num3 = (flag2 ? value.Item1 : 0u);
		if (num != num2)
		{
			num3 = num;
			CarryHostility(num3, num2);
			PED.SET_PED_RELATIONSHIP_GROUP_HASH(h, num2);
			if (!flag2 && _outlawSwitchLogs++ < 60)
			{
				GrudgeLog("LAWGROUP " + AttackerLabel(h) + " (" + (flag ? ShortOutfit(g) : "no outfit") + ") into the game's " + GroupName(num2) + " - " + (flag ? why : "he drew on you"));
			}
		}
		_outlawSwitched[h] = (num3, num2, now);
	}

	private void ProbeGameOutlawGroups(Ped player)
	{
		if (!_lawGroupsProbed)
		{
			_lawGroupsProbed = true;
			EnsureGameOutlawGroups();
			uint me = PED.GET_PED_RELATIONSHIP_GROUP_HASH(player.Handle);
			GrudgeLog("LAWGROUP the game's outlaw groups toward you: " + string.Join(", ", _gameOutlawGroups.Select((KeyValuePair<uint, string> kv) => kv.Value + " " + RelName(RelBetween(kv.Key, me)))));
		}
	}

	private unsafe void UpdateOutlawGroups(Ped player)
	{
		if (MISC.GET_MISSION_FLAG())
		{
			return;
		}
		int now = Game.GameTime;
		int handle = Game.Player.Handle;
		int num = 0;
		if (PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(handle, &num) && num != 0 && ENTITY.IS_ENTITY_A_PED(num))
		{
			_inSightsAt[num] = now;
		}
		num = 0;
		if (PLAYER.GET_PLAYER_TARGET_ENTITY(handle, &num) && num != 0 && ENTITY.IS_ENTITY_A_PED(num))
		{
			_inSightsAt[num] = now;
		}
		num = 0;
		if (PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(handle, &num, p2: false, p3: false) && num != 0 && ENTITY.IS_ENTITY_A_PED(num))
		{
			_inSightsAt[num] = now;
		}
		if (_inSightsAt.Count > 0 && AttackInputs.Any(Game.IsControlPressed))
		{
			int num2 = 1000;
			try
			{
				if (Function.Call<bool>(12781818049324475888uL, new InputArgument[1] { handle }))
				{
					num2 = 6000;
				}
			}
			catch
			{
			}
			foreach (KeyValuePair<int, int> item in _inSightsAt.ToList())
			{
				if (now - item.Value < num2)
				{
					SwitchOutlaw(item.Key, "you went for him", now);
				}
			}
		}
		if (Game.GameTime < _nextOutlawGroups)
		{
			return;
		}
		_nextOutlawGroups = Game.GameTime + 250;
		ProbeGameOutlawGroups(player);
		if (_inSightsAt.Count > 32)
		{
			foreach (int item2 in (from kv in _inSightsAt
				where now - kv.Value > 6000
				select kv.Key).ToList())
			{
				_inSightsAt.Remove(item2);
			}
		}
		Dictionary<int, string> why = new Dictionary<int, string>();
		foreach (KeyValuePair<int, int> combatant in _combatants)
		{
			if (now - combatant.Value < 3000)
			{
				Add(combatant.Key, "he's fighting you");
			}
		}
		foreach (KeyValuePair<int, int> item3 in _hitByYouAt)
		{
			if (now - item3.Value < 10000)
			{
				Add(item3.Key, "you hurt him");
			}
		}
		foreach (KeyValuePair<int, Fighter> fighter in _fighters)
		{
			if (fighter.Value != null && fighter.Value.Broke && !fighter.Value.Done)
			{
				Add(fighter.Key, "he gave up");
			}
		}
		if (_interro?.Ped != null)
		{
			Add(_interro.Ped.Handle, "you're questioning him");
		}
		Vector3 position = player.Position;
		foreach (var (ped, vector) in PedSnapshot(player))
		{
			if (ped == null || vector.DistanceTo(position) > 40f || why.ContainsKey(ped.Handle) || (RoleOf(ped.Handle) == OurRole.None && RecordAt(ped.Handle) == null && !Registered(ped.Handle) && BandOf(ped.Handle) == null))
			{
				continue;
			}
			if (ped.IsHogtied || TASK.IS_PED_IN_WRITHE(ped.Handle) || (ped.IsDead && LooksKnockedOut(ped)))
			{
				Add(ped.Handle, "he's down");
			}
			else
			{
				if (ped.IsDead || !PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle) || !OutlawMan(ped.Handle))
				{
					continue;
				}
				Add(ped.Handle, "he's squaring up to you");
				Gang g = Gangs.ById(RecordAt(ped.Handle)?.Gang) ?? Gangs.ForModel((uint)ped.Model.Hash);
				if (FairOutfit(g))
				{
					try
					{
						FairNow(g, AttackerLabel(ped.Handle));
					}
					catch
					{
					}
				}
			}
		}
		foreach (KeyValuePair<int, string> item4 in why)
		{
			SwitchOutlaw(item4.Key, item4.Value, now);
		}
		foreach (int item5 in _outlawSwitched.Keys.ToList())
		{
			if (why.ContainsKey(item5))
			{
				continue;
			}
			(uint, uint, int) tuple2 = _outlawSwitched[item5];
			if (!ENTITY.DOES_ENTITY_EXIST(item5))
			{
				_outlawSwitched.Remove(item5);
			}
			else if (!ENTITY.IS_ENTITY_DEAD(item5) && now - tuple2.Item3 >= 8000)
			{
				if (PED.GET_PED_RELATIONSHIP_GROUP_HASH(item5) == tuple2.Item2 && tuple2.Item1 != 0)
				{
					PED.SET_PED_RELATIONSHIP_GROUP_HASH(item5, tuple2.Item1);
				}
				_outlawSwitched.Remove(item5);
				if (_outlawSwitchLogs++ < 60)
				{
					GrudgeLog($"LAWGROUP {AttackerLabel(item5)} back in the mod's group (nothing for {8} s)");
				}
			}
		}
		void Add(int h, string w)
		{
			if (h != 0 && !why.ContainsKey(h))
			{
				why[h] = w;
			}
		}
	}

	private int NextRetarget(int h)
	{
		if (!_retargetAt.TryGetValue(h, out var value))
		{
			return 0;
		}
		return value;
	}
}
