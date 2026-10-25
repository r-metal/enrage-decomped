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
	private uint _nemesisGroup;

	private readonly List<Ped> _followers = new List<Ped>();

	private readonly List<Blip> _followerBlips = new List<Blip>();

	private readonly Dictionary<int, NemesisRecord> _followerOf = new Dictionary<int, NemesisRecord>();

	private const ulong GetMetaOutfit = 3483146409871567450uL;

	private const ulong EquipMetaOutfit = 1802219197667468668uL;

	private const ulong UpdateVariation = 14739335899491263887uL;

	private readonly List<uint> _crewModels = new List<uint>();

	private int? _crewOverride;

	private bool _aborting;

	private const int CaAlwaysFight = 5;

	private const int CaCanFightArmedPedsWhenNotArmed = 46;

	private const int CaDisableFleeFromCombat = 58;

	private const int CaPreferMelee = 93;

	private int _activeLastPunchTime = -100000;

	private int _activeDownSince = -1;

	private int CrewRoll(NemesisRecord rec)
	{
		Gang gang = Gangs.ById(rec.Gang);
		int num = FollowerCount(rec.Rank) + CrewBonus(gang) + (Quirks.HasSkill(rec, "leader") ? 1 : 0);
		int num3;
		if (rec.Gang != null)
		{
			int num2 = gang?.CrewExtra ?? 0;
			num3 = ((_rng.Next(100) >= Config.GangAlonePercent) ? Math.Max((rec.Rank < 2) ? 1 : 2, Math.Max(1, num) + _rng.Next(-1, 3) + num2) : 0);
		}
		else
		{
			num3 = ((_rng.Next(100) >= Config.LonerAlonePercent) ? Math.Max(1, num + _rng.Next(1, 3)) : 0);
		}
		if (rec.Gang != null && num3 < 2 && NotorietyLevelOf(rec.Gang) >= 4)
		{
			num3 = 2;
			GrudgeLog("CREW " + rec.Name + ": Blood Feud - won't come without his friends");
		}
		if (rec.Gang != null && num3 > 0)
		{
			int num4 = PurseCrewShift(gang);
			if (num4 != 0)
			{
				num3 = Math.Max(1, num3 + num4);
				GrudgeLog("CREW " + rec.Name + ": " + ((num4 < 0) ? "his outfit is broke - one man fewer" : "his outfit is rich - one man more"));
			}
		}
		if (rec.Gang != null && num3 > FieldCap(gang))
		{
			GrudgeLog($"CREW {rec.Name}: his outfit has {MenOf(gang)} men left - {FieldCap(gang)} with him, not {num3}");
			num3 = FieldCap(gang);
		}
		GrudgeLog(string.Format("CREW {0} (rank {1}{2}): {3} (usual {4})", rec.Name, rec.Rank, (rec.Gang != null) ? (", " + rec.Gang) : ", loner", (num3 == 0) ? "alone" : (num3 + " men"), num));
		return num3;
	}

	internal int FollowerCount(int rank)
	{
		int[] array = new int[6] { 0, Config.FollowersRank1, Config.FollowersRank2, Config.FollowersRank3, Config.FollowersRank4, Config.FollowersRank5 };
		return Math.Max(0, array[Math.Max(1, Math.Min(5, rank))]);
	}

	internal void KeepOutfit(Ped ped, NemesisRecord rec)
	{
		if (ped == null || !ped.Exists() || rec == null)
		{
			return;
		}
		try
		{
			if (ped.Model.Hash != (int)rec.ModelHash)
			{
				return;
			}
			if (rec.OutfitHash != 0)
			{
				Function.Call(1802219197667468668uL, ped.Handle, rec.OutfitHash);
				Function.Call(14739335899491263887uL, ped.Handle, false, true, true, true, false);
			}
			else
			{
				uint num = Function.Call<uint>(3483146409871567450uL, new InputArgument[1] { ped.Handle });
				if (num != 0)
				{
					rec.OutfitHash = num;
					SaveRoster();
					GrudgeLog($"OUTFIT {rec.Name}: 0x{num:X8} kept");
				}
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("OUTFIT " + rec.Name + " failed: " + ex.Message);
		}
	}

	internal Ped SpawnNemesisPed(NemesisRecord rec, Vector3 pos, float heading, uint group, out Model model, uint fallback = 424175505u, bool releaseModel = true, bool blockEvents = true, bool factionStyle = true, uint modelOverride = 0u)
	{
		model = new Model((modelOverride != 0) ? modelOverride : rec.ModelHash);
		if (!model.IsValid || !model.IsPed)
		{
			model = new Model(fallback);
		}
		if (!releaseModel && !LoadModelSafe(model, 2000))
		{
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)model.Hash);
			return null;
		}
		Ped ped = SpawnPed((PedHash)model.Hash, pos, heading);
		if (releaseModel || ped == null)
		{
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)model.Hash);
		}
		if (ped == null)
		{
			return null;
		}
		NoteModel(ped);
		MakeOurs(ped.Handle);
		ArmForHate(ped, rec, drawNow: false);
		ApplyRankStats(ped, rec, fullHeal: true);
		ApplyScars(ped, rec);
		if (modelOverride == 0)
		{
			KeepOutfit(ped, rec);
		}
		if (rec.Limp)
		{
			_bulletImpact.ApplyCondition(ped, CrippleLevel.Limping);
		}
		if (blockEvents)
		{
			ped.BlockPermanentEvents = true;
		}
		if (group != 0)
		{
			ped.RelationshipGroup = group;
		}
		if (factionStyle)
		{
			ApplyFactionStyle(ped, rec.Gang);
			ApplyNotorietyStyle(ped, rec.Gang);
		}
		ApplyMindAi(ped, rec);
		return ped;
	}

	private uint NemesisGroup()
	{
		if (_nemesisGroup == 0)
		{
			_nemesisGroup = World.AddRelationshipGroup("NEMESIS_GROUP");
		}
		return _nemesisGroup;
	}

	private void SpawnFollowers(NemesisRecord rec, Model model, Vector3 leaderPos, float heading)
	{
		int num = _crewOverride ?? CrewRoll(rec);
		List<NemesisRecord> list = new List<NemesisRecord>();
		List<NemesisRecord> list2 = ((num > 0) ? Roster.Where((NemesisRecord n) => Free(n) && Tied(n) && (n.Gang == rec.Gang || n.Gang == null) && !Gangs.Squads(n)).ToList() : new List<NemesisRecord>());
		int num2 = Config.CrewRosterChance + ((list2.Count > 0) ? 25 : 0);
		if (num > 0 && (rec.Gang != null || list2.Count > 0) && _rng.Next(100) < num2)
		{
			List<NemesisRecord> source = Roster.Where((NemesisRecord n) => n.Gang == rec.Gang && rec.Gang != null && n.Rank < rec.Rank && Free(n)).ToList();
			NemesisRecord nemesisRecord = list2.OrderBy((NemesisRecord _) => _rng.Next()).FirstOrDefault() ?? (from _ in source
				where _.BossId == rec.Id
				orderby _rng.Next()
				select _).FirstOrDefault() ?? source.OrderByDescending((NemesisRecord n) => n.BossId != null && BossOf(n)?.BossId == rec.Id).ThenBy((NemesisRecord _) => _rng.Next()).FirstOrDefault();
			if (nemesisRecord != null)
			{
				list.Add(nemesisRecord);
				if (Tied(nemesisRecord))
				{
					GrudgeLog("CREW " + rec.Name + " rides with his " + ((nemesisRecord.KinId == rec.Id) ? nemesisRecord.KinRelation : ((rec.KinId == nemesisRecord.Id) ? Traits.Inverse(rec.KinRelation) : "friend")) + " " + nemesisRecord.Name);
				}
			}
		}
		foreach (NemesisRecord item in list)
		{
			double num3 = _rng.NextDouble() * Math.PI * 2.0;
			float num4 = 2.5f + (float)_rng.NextDouble() * 5f;
			Vector3 p = leaderPos + new Vector3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, 0f);
			p = GroundOnSpot(p);
			Model model2;
			Ped ped = SpawnNemesisPed(item, p, heading, NemesisGroup(), out model2, (uint)model.Hash);
			if (!(ped == null))
			{
				_followers.Add(ped);
				_worldNemeses[ped.Handle] = item;
				item.TimesAmbushed++;
				item.Touch();
			}
		}
		foreach (uint crewModel in _crewModels)
		{
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED(crewModel);
		}
		_crewModels.Clear();
		if (list.Count > 0)
		{
			GrudgeLog(string.Format("CREW {0} (rank {1}) rides with {2}; {3} hired gun(s)", rec.Name, rec.Rank, string.Join(", ", list.Select((NemesisRecord c) => c.Name)), num - list.Count));
		}
		for (int num5 = list.Count; num5 < num; num5++)
		{
			double num6 = _rng.NextDouble() * Math.PI * 2.0;
			float num7 = 2.5f + (float)_rng.NextDouble() * 5f;
			Vector3 p2 = leaderPos + new Vector3((float)Math.Cos(num6) * num7, (float)Math.Sin(num6) * num7, 0f);
			p2 = GroundOnSpot(p2);
			Ped ped2 = SpawnPed((PedHash)model.Hash, p2, heading);
			if (!(ped2 == null))
			{
				MakeOurs(ped2.Handle);
				ArmForHate(ped2, rec, drawNow: false);
				ped2.Accuracy = 20;
				ped2.BlockPermanentEvents = true;
				ped2.RelationshipGroup = _nemesisGroup;
				ApplyFactionStyle(ped2, rec.Gang);
				ApplyNotorietyStyle(ped2, rec.Gang, rec.Rank);
				_followers.Add(ped2);
				_followerOf[ped2.Handle] = rec;
			}
		}
		bool Free(NemesisRecord n)
		{
			if (n != rec && n.CanHunt && !n.PresumedDead && !Resting(n) && !_worldNemeses.ContainsValue(n))
			{
				return n != ActiveRecord;
			}
			return false;
		}
		bool Tied(NemesisRecord n)
		{
			if (!(n.KinId == rec.Id) && !(rec.KinId == n.Id))
			{
				List<string> friendIds = rec.FriendIds;
				if (friendIds == null || !friendIds.Contains(n.Id))
				{
					return n.FriendIds?.Contains(rec.Id) ?? false;
				}
			}
			return true;
		}
	}

	private void EnlistHiredGuns()
	{
		if (!Config.HiredGunsJoin)
		{
			return;
		}
		Ped character = Game.Player.Character;
		List<string> list = new List<string>();
		foreach (Ped follower in _followers)
		{
			if (follower == null || !follower.Exists() || _worldNemeses.ContainsKey(follower.Handle) || !_followerOf.TryGetValue(follower.Handle, out var value) || value == null || value.Gang == null || (follower.IsDead && !LooksKnockedOut(follower)) || follower.Position.DistanceTo(character.Position) > 150f)
			{
				continue;
			}
			Gang gang = Gangs.ById(value.Gang);
			if (gang?.Tiers != null)
			{
				var (label, rank, num) = gang.Tiers[gang.Tiers.Length - 1];
				if (Roster.Count((NemesisRecord n) => n.Gang == gang.Id && n.IsAlive && !n.PresumedDead && n.Rank < ((gang.Tiers.Length > 1) ? gang.Tiers[gang.Tiers.Length - 2].MinRank : 99)) >= num + num / 2)
				{
					break;
				}
				NemesisRecord nemesisRecord = NewRecord((uint)follower.Model.Hash, GetZoneName(character.Position), gang);
				if (_named.TryGetValue(follower.Handle, out var known) && !Roster.Any((NemesisRecord n) => n.Name == known.Name))
				{
					nemesisRecord.Name = known.Name;
					_named.Remove(follower.Handle);
				}
				nemesisRecord.Gang = gang.Id;
				nemesisRecord.Rank = rank;
				nemesisRecord.Origin = GrudgeOrigin.GangMember;
				nemesisRecord.SeedMember = true;
				nemesisRecord.TimesKilledPlayer = 0;
				nemesisRecord.TimesAmbushed = 1;
				nemesisRecord.BossId = value.Id;
				nemesisRecord.KilledPlayerAt = DateTime.Now;
				StartRest(nemesisRecord);
				nemesisRecord.Hate = Math.Max(10, Math.Min(100, value.Hate - 8 + _rng.Next(12)));
				if (follower.IsDead)
				{
					nemesisRecord.TimesKnockedOutByPlayer = 1;
					nemesisRecord.AddHate(10);
				}
				nemesisRecord.Touch();
				Roster.Add(nemesisRecord);
				_worldNemeses[follower.Handle] = nemesisRecord;
				_followerOf.Remove(follower.Handle);
				if (_provoked.TryGetValue(follower.Handle, out var value2))
				{
					value2.Record = nemesisRecord;
					value2.Settled = true;
				}
				ForgetPerson(follower.Handle, nemesisRecord.Name);
				list.Add(nemesisRecord.Name);
				Feed($"JOINED {nemesisRecord.Name}: hired gun -> {gang.Name} {Singular(label)}  (hate {nemesisRecord.Hate}, under {value.Name})", gang.Accent, nemesisRecord);
			}
		}
		if (list.Count != 0)
		{
			SaveRoster();
			GrudgeLog("ENLIST " + string.Join(", ", list));
			GrudgeNotice((list.Count == 1) ? (list[0] + " lived through it. He's riding with the gang now - and he knows your face.") : (string.Join(", ", list) + " lived through it. They ride with the gang now."));
		}
	}

	private unsafe void ReleaseFollowers()
	{
		foreach (Blip followerBlip in _followerBlips)
		{
			if (followerBlip != null && followerBlip.Exists())
			{
				followerBlip.Delete();
			}
		}
		_followerBlips.Clear();
		foreach (Ped follower in _followers)
		{
			if (follower.Exists())
			{
				int handle = follower.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		_followers.Clear();
	}

	private void PruneFollowerMap()
	{
		foreach (int item in _followerOf.Keys.ToList())
		{
			Ped ped = Entity.FromHandle(item) as Ped;
			if (ped == null || !ped.Exists() || ReallyDead(ped))
			{
				_followerOf.Remove(item);
			}
		}
	}

	private static void ArmForHate(Ped ped, NemesisRecord rec, bool drawNow)
	{
		WEAPON.REMOVE_ALL_PED_WEAPONS(ped.Handle, p1: true, p2: true);
		Gang gang = Gangs.Of(rec);
		if (gang != null && gang.UniformKit)
		{
			ped.GiveWeapon(gang.UniformLong, 120, eWeaponAttachPoint.Rifle, drawNow, !drawNow);
			ped.GiveWeapon(gang.UniformSide, 60, eWeaponAttachPoint.PistolR);
			PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 5, enabled: true);
			PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 93, enabled: false);
		}
		else
		{
			Loadout.Arm(ped, rec, drawNow);
			bool enabled = Loadout.BandOf(rec) != Loadout.Band.Gun;
			PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 5, enabled: true);
			PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 58, enabled: true);
			PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 46, enabled);
			PED.SET_PED_COMBAT_ATTRIBUTES(ped.Handle, 93, enabled);
		}
	}

	private void EscalateToGuns()
	{
		NemesisRecord activeRecord = ActiveRecord;
		if (activeRecord != null && Loadout.BandOf(activeRecord) != Loadout.Band.Gun)
		{
			activeRecord.Hate = Math.Max(activeRecord.Hate, Loadout.GunAt(activeRecord) + 3);
			SaveRoster();
			ArmForHate(_activeNemesisPed, activeRecord, drawNow: true);
			TASK.TASK_COMBAT_PED(_activeNemesisPed.Handle, Game.Player.Character.Handle, 0, 16);
			ShowSubtitle(activeRecord.Name + ": \"" + Taunts.EscalateReply(_rng) + "\"");
		}
	}

	private bool ActiveNemesisKnockedOut()
	{
		if ((!_activeNemesisPed.IsIncapacitated && !_activeNemesisPed.IsRagdoll) || Game.GameTime - _activeLastPunchTime >= 10000)
		{
			_activeDownSince = -1;
			return false;
		}
		if (_activeDownSince < 0)
		{
			_activeDownSince = Game.GameTime;
		}
		return Game.GameTime - _activeDownSince >= 3000;
	}

	private static void ApplyRankStats(Ped ped, NemesisRecord rec, bool fullHeal)
	{
		ped.MaxHealth = 300 + rec.Rank * 100 + (Quirks.HasSkill(rec, "tough") ? 60 : 0) + (Quirks.HasSkill(rec, "brawler") ? 40 : 0);
		if (fullHeal)
		{
			ped.Health = ped.MaxHealth;
		}
		ped.Accuracy = Math.Min(92, 25 + rec.Rank * 10 + (Quirks.HasSkill(rec, "marksman") ? 12 : 0));
	}
}
