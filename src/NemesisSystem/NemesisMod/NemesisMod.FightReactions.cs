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
	private int _lastTownReactionAt = -100000;

	private const int FightOverDownMs = 5000;

	private const int FightOverFarMs = 5000;

	private const int FightOverWatchMs = 600000;

	private const float FightOverFarM = 150f;

	private const ulong IsEntityOnScreenFn = 7006499128492128287uL;

	private readonly Dictionary<int, int> _fightDownSince = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _fightFarSince = new Dictionary<int, int>();

	private uint _beatenGroup;

	private readonly HashSet<int> _fightEnded = new HashSet<int>();

	private int _nextFightOver;

	private bool _safeTownWas;

	internal static readonly Dictionary<int, int> ReleasedAt = new Dictionary<int, int>();

	private const ulong MetaAssetGuids = 12232485870226939222uL;

	private const ulong MetaAssetTint = 16688528139376343998uL;

	private void OnRestrained(int handle, string how)
	{
		if (_restrainedAt.TryGetValue(handle, out var value) && Game.GameTime - value < 20000)
		{
			return;
		}
		_tiedCounted.RemoveWhere((int h) => !ENTITY.DOES_ENTITY_EXIST(h) || !PED.IS_PED_HOGTIED(h));
		if (_tiedCounted.Contains(handle))
		{
			return;
		}
		_restrainedAt[handle] = Game.GameTime;
		NemesisRecord nemesisRecord = RecordAt(handle);
		if (nemesisRecord != null)
		{
			nemesisRecord.Touch();
			int hate = nemesisRecord.Hate;
			int fear = nemesisRecord.Fear;
			nemesisRecord.AddHate(10);
			nemesisRecord.AddFear(8);
			nemesisRecord.AddYourNotoriety(5);
			nemesisRecord.TimesHogtied++;
			HiddenSlip(nemesisRecord, "violence");
			CrimeSeen(handle, CrimeOf(GrudgeAction.Hogtie));
			_tiedCounted.Add(handle);
			Remember(nemesisRecord, "Shame", 55, "you tied him up like an animal");
			if (nemesisRecord.Origin == GrudgeOrigin.GangMember)
			{
				nemesisRecord.Origin = GrudgeOrigin.Kidnapped;
				nemesisRecord.KillLocation = GetZoneName(Game.Player.Character.Position);
			}
			SaveRoster();
			Feed($"{how.ToUpperInvariant()} {nemesisRecord.Name}: hate {Delta(hate, nemesisRecord.Hate)}  fear {Delta(fear, nemesisRecord.Fear)}  (tied up {nemesisRecord.TimesHogtied}x)", Theme.Warn, nemesisRecord);
			WorldPart worldPart = PartFor(nemesisRecord, "HOGTIED", (nemesisRecord.TimesHogtied > 1) ? $"tied up {nemesisRecord.TimesHogtied} times now" : nemesisRecord.Title);
			worldPart.Effects.Add($"hate {hate} -> {nemesisRecord.Hate}");
			worldPart.Effects.Add($"fear {fear} -> {nemesisRecord.Fear}");
			Vector3 position = Game.Player.Character.Position;
			ChronicleDeed("hogtie", "You " + how.ToLowerInvariant() + " " + nemesisRecord.Name, "near " + GetZoneName(position), position, nemesisRecord.Gang, worldPart);
			if (nemesisRecord.TimesHogtied == 1)
			{
				GrudgeNotice(nemesisRecord.FullName + " won't forget the rope.");
			}
			return;
		}
		Mark(handle, delegate(Provocation p)
		{
			if (!p.Kidnapped)
			{
				Grudge(handle, p, GrudgeAction.Hogtie);
			}
			p.Kidnapped = true;
		});
		if (PED.IS_PED_HOGTIED(handle))
		{
			_tiedCounted.Add(handle);
		}
	}

	private int NotorietyLevelOf(string gangId)
	{
		Gang gang = Gangs.ById(gangId);
		if (gang != null)
		{
			return Notoriety.Level(NotorietyOf(gang));
		}
		return 0;
	}

	internal void ApplyNotorietyStyle(Ped ped, string gangId, int bossRank = 0)
	{
		if (ped == null || !ped.Exists() || gangId == null)
		{
			return;
		}
		Gang gang = Gangs.ById(gangId);
		if (gang == null || gang.StoryOnly)
		{
			return;
		}
		int num = NotorietyLevelOf(gangId);
		int num2 = 0;
		if (num >= 3)
		{
			PED.SET_PED_COMBAT_MOVEMENT(ped.Handle, (gang.CombatMovement != 2) ? 1 : 2);
			PED.SET_PED_COMBAT_ABILITY(ped.Handle, 2);
			num2 += 8;
			if (!gang.TribalKit && !gang.UniformKit)
			{
				uint num3 = Function.Call<uint>(9548732433391192802uL, new InputArgument[3] { ped.Handle, false, false });
				bool flag = num3 != 0 && Function.Call<bool>(8096313897301948765uL, new InputArgument[1] { num3 });
				bool flag2 = Function.Call<bool>(10226742572059207868uL, new InputArgument[4] { ped.Handle, 4111948705u, 0, false }) || Function.Call<bool>(10226742572059207868uL, new InputArgument[4] { ped.Handle, 2823250668u, 0, false }) || Function.Call<bool>(10226742572059207868uL, new InputArgument[4] { ped.Handle, 1999408598u, 0, false }) || Function.Call<bool>(10226742572059207868uL, new InputArgument[4] { ped.Handle, 1676963302u, 0, false });
				if (flag && !flag2)
				{
					ped.GiveWeapon(eWeapon.RepeaterCarbine, 60, eWeaponAttachPoint.Rifle, bForceInHand: false, bForceInHolster: true);
				}
			}
		}
		if (bossRank >= 4)
		{
			num2 += 10;
		}
		if (num2 > 0)
		{
			ped.Accuracy = Math.Min(Math.Max(ped.Accuracy, 20) + num2, Math.Min(100, gang.AccuracyMax));
		}
	}

	internal void TownFightReaction(Ped him, Ped player, string town, bool gunfight)
	{
		if (him == null || !him.Exists() || town == null || Game.GameTime - _lastTownReactionAt < 30000)
		{
			return;
		}
		int num = TownReputation(town);
		if (num > -20 && num < 20)
		{
			return;
		}
		_lastTownReactionAt = Game.GameTime;
		Vector3 me = player.Position;
		Ped ped = (from p in (from t in PedSnapshot(player)
				where t.p != null && t.p.Exists() && !t.p.IsDead && t.p.IsHuman && t.p.Handle != him.Handle && !IsOurs(t.p.Handle) && !Kids.IsChild(t.p) && (t.pos - me).Length() < 40f
				select t.p).ToList()
			where !IsLawman(p) && !IsStoryPed(p) && !GameOwned(p)
			orderby p.Position.DistanceTo(me)
			select p).FirstOrDefault();
		Ped ped2 = (from t in PedSnapshot(player)
			where t.p != null && t.p.Exists() && !t.p.IsDead && IsLawman(t.p) && !IsOurs(t.p.Handle) && !GameOwned(t.p) && (t.pos - me).Length() < 60f
			select t.p into p
			orderby p.Position.DistanceTo(me)
			select p).FirstOrDefault();
		Named value;
		string text = ((ped != null && _named.TryGetValue(ped.Handle, out value)) ? value.Name : "A townsman");
		string text2 = RecordAt(him.Handle)?.Name ?? "him";
		if (num >= 20)
		{
			if (ped != null)
			{
				ShowSubtitle(gunfight ? (text + ": \"Look out, mister - he's drawin' on you!\"") : (text + ": \"Watch yourself - that one's lookin' for trouble!\""));
			}
			if (ped2 != null && (gunfight || num >= 60))
			{
				Crumbs.Native("FightFlavour law steps in");
				TASK.TASK_COMBAT_PED(ped2.Handle, him.Handle, 0, 16);
				GrudgeLog($"FIGHT in {town} (reputation {num:+0;-0}): the law steps in on {text2} (ped {ped2.Handle})");
				Feed($"FIGHT {town} likes you ({num:+0;-0}): a lawman steps in on {text2}", Theme.Good);
			}
			else
			{
				GrudgeLog(string.Format("FIGHT in {0} (reputation {1:+0;-0}): {2} warns you{3}", town, num, text, (ped2 == null) ? " - no law close by" : ""));
			}
			return;
		}
		if (ped != null)
		{
			ShowSubtitle((num <= -60) ? (text + ": \"Go on, " + text2 + "! Put that dog down!\"") : (text + ": \"Give it to him!\""));
		}
		if (num <= -60 && ped2 != null && !YouStartedIt(him.Handle, player, out var _))
		{
			GrudgeLog($"FIGHT in {town} (reputation {num:+0;-0}): the crowd is with {text2} - but he's the one starting it, so the law doesn't draw on you");
			return;
		}
		if (num <= -60 && ped2 != null)
		{
			Crumbs.Native("FightFlavour law draws on you");
			TASK.TASK_AIM_GUN_AT_ENTITY(ped2.Handle, player.Handle, 6000, p3: false, 0);
			Feed($"FIGHT {town} hates you ({num:+0;-0}): the law draws on YOU", Theme.Blood);
		}
		GrudgeLog(string.Format("FIGHT in {0} (reputation {1:+0;-0}): the crowd is with {2}{3}", town, num, text2, (num <= -60 && ped2 != null) ? " - a lawman draws on you" : ""));
	}

	internal float ReputationNerve(string temper, NemesisRecord rec, float infamy, float lasting)
	{
		if ((temper == "Proud" || (rec != null && Mind.F(rec, "Bravery") >= 65)) && infamy >= 15f)
		{
			return infamy / 8f;
		}
		if (temper == "Coward" && (infamy >= 30f || lasting >= 30f))
		{
			return (0f - infamy) / 5f - 15f;
		}
		return (0f - infamy) / 5f;
	}

	private IEnumerable<int> FightOverCandidates()
	{
		foreach (var (ped, ourRole) in OurPeds())
		{
			if (ped != null && ourRole != OurRole.Companion && ourRole != OurRole.Regular)
			{
				yield return ped.Handle;
			}
		}
		foreach (int item in _followerOf.Keys.ToList())
		{
			yield return item;
		}
		foreach (int item2 in _worldNemeses.Keys.ToList())
		{
			yield return item2;
		}
		foreach (KeyValuePair<int, int> item3 in _wentForYouAt.ToList())
		{
			if (Game.GameTime - item3.Value < 600000)
			{
				yield return item3.Key;
			}
		}
		foreach (KeyValuePair<int, int> item4 in ReleasedAt.ToList())
		{
			if (Game.GameTime - item4.Value > 600000 || !ENTITY.DOES_ENTITY_EXIST(item4.Key))
			{
				ReleasedAt.Remove(item4.Key);
			}
			else
			{
				yield return item4.Key;
			}
		}
	}

	private void UpdateFightOver(Ped player)
	{
		if (Game.GameTime < _nextFightOver)
		{
			return;
		}
		_nextFightOver = Game.GameTime + 500;
		if (MISC.GET_MISSION_FLAG())
		{
			_fightDownSince.Clear();
			_fightFarSince.Clear();
			return;
		}
		string text = StoryBansWeapons(player.Position);
		if (text != null != _safeTownWas)
		{
			_safeTownWas = text != null;
			GrudgeLog("SAFETOWN " + ((text != null) ? (text + " - nothing new starts here, a fight that follows you in breaks off") : "out of it"));
		}
		bool flag = _funeral != null && _funeral.Plan != null && ActiveRecord != null && ActiveRecord.Id == _funeral.Plan.LeaderId;
		if (text != null && _enc != null && (_enc.Engaged || _enc.Spotted) && !flag && _proc == null && !_aborting)
		{
			GrudgeLog("SAFETOWN " + (ActiveRecord?.Name ?? "the encounter") + " won't fight you in Rhodes - the encounter ends");
			if (ActiveRecord != null)
			{
				Feed(ActiveRecord.Name + " won't follow you into Rhodes. He'll keep.", Theme.Warn, ActiveRecord);
			}
			_encEnd = ("BACKED OFF", "wouldn't follow you into Rhodes");
			EndEncounter();
			ReleaseActive();
		}
		if (text != null && flag && _enc != null && _enc.Engaged && !_aborting)
		{
			_enc.Engaged = false;
			_enc.Spotted = false;
			_enc.StandoffUntil = -1;
			_enc.EngageAt = 0;
			_enc.Calmed = true;
			_enc.TalkedAt = Game.GameTime;
			_funeral.Fighting = false;
			_funeral.Ceasefire = true;
			if (_activeBlip != null && _activeBlip.Exists())
			{
				_activeBlip.Delete();
				_activeBlip = null;
			}
			foreach (Blip item in _followerBlips.Where((Blip b) => b?.Exists() ?? false))
			{
				item.Delete();
			}
			_followerBlips.Clear();
			MusicStandoffOver();
			GrudgeLog("SAFETOWN the funeral's fight breaks off in Rhodes - the mourners go back to the grave");
		}
		HashSet<int> seen = new HashSet<int>();
		foreach (int item2 in FightOverCandidates().ToList())
		{
			if (!seen.Add(item2) || !ENTITY.DOES_ENTITY_EXIST(item2) || !ENTITY.IS_ENTITY_A_PED(item2) || ENTITY.IS_ENTITY_DEAD(item2))
			{
				continue;
			}
			if (!PED.IS_PED_IN_COMBAT(item2, player.Handle) || IsCompanion(item2) || XRayHandsOff(item2))
			{
				_fightDownSince.Remove(item2);
				_fightFarSince.Remove(item2);
				continue;
			}
			Ped ped = new Ped(item2);
			if ((_interro?.Ped != null && _interro.Ped.Handle == item2) || (_convo?.Ped != null && _convo.Ped.Handle == item2) || ENTITY.IS_ENTITY_ATTACHED(item2))
			{
				_fightDownSince.Remove(item2);
				_fightFarSince.Remove(item2);
				continue;
			}
			if (text != null)
			{
				EndFight(ped, player, "you're in Rhodes (chapter 3)", ped.Position.DistanceTo(player.Position));
				if (ped.Exists() && !ped.IsDead && !CantWalk(item2) && !ped.IsHogtied && !TASK.IS_PED_IN_WRITHE(item2) && !BackToGrave(ped))
				{
					WalkOff(item2, player);
				}
				continue;
			}
			bool num = _bulletImpact.CrippleLevelOf(item2) >= CrippleLevel.Legless || TASK.IS_PED_IN_WRITHE(item2) || ped.IsHogtied || PED.IS_PED_PRONE(item2) || (ped.IsRagdoll && ped.HeightAboveGround < 0.7f);
			float num2 = ped.Position.DistanceTo(player.Position);
			if (num)
			{
				if (!_fightDownSince.ContainsKey(item2))
				{
					_fightDownSince[item2] = Game.GameTime;
				}
			}
			else
			{
				_fightDownSince.Remove(item2);
			}
			if (num2 > 150f)
			{
				if (!_fightFarSince.ContainsKey(item2))
				{
					_fightFarSince[item2] = Game.GameTime;
				}
			}
			else
			{
				_fightFarSince.Remove(item2);
			}
			int value2;
			if (_fightDownSince.TryGetValue(item2, out var value) && Game.GameTime - value >= 5000)
			{
				EndFight(ped, player, $"down {5} s", num2);
			}
			else if (_fightFarSince.TryGetValue(item2, out value2) && Game.GameTime - value2 >= 5000)
			{
				EndFight(ped, player, $"{num2:0} m off for {5} s", num2);
			}
		}
		foreach (int item3 in _fightDownSince.Keys.Where((int k) => !seen.Contains(k)).ToList())
		{
			_fightDownSince.Remove(item3);
		}
		foreach (int item4 in _fightFarSince.Keys.Where((int k) => !seen.Contains(k)).ToList())
		{
			_fightFarSince.Remove(item4);
		}
		if (Game.FrameCount % 60 == 0)
		{
			_fightEnded.RemoveWhere((int h) => !ENTITY.DOES_ENTITY_EXIST(h) || ENTITY.IS_ENTITY_DEAD(h));
		}
	}

	private void EndFight(Ped p, Ped player, string why, float dist)
	{
		int handle = p.Handle;
		_fightDownSince.Remove(handle);
		_fightFarSince.Remove(handle);
		bool flag = !_fightEnded.Add(handle);
		if (_fightEnded.Count > 300)
		{
			_fightEnded.Clear();
		}
		if (_fighters.TryGetValue(handle, out var value))
		{
			value.Done = true;
		}
		if (_beatenGroup == 0)
		{
			_beatenGroup = World.AddRelationshipGroup("NEMESIS_BEATEN");
		}
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _beatenGroup, player.RelationshipGroup);
		World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, player.RelationshipGroup, _beatenGroup);
		Crumbs.Native("FightOver end fight");
		p.RelationshipGroup = _beatenGroup;
		PED.SET_PED_COMBAT_ATTRIBUTES(handle, 5, enabled: false);
		PED.SET_PED_COMBAT_ATTRIBUTES(handle, 58, enabled: false);
		PED.SET_PED_KEEP_TASK(handle, toggle: false);
		p.BlockPermanentEvents = false;
		TASK.CLEAR_PED_TASKS(handle, p1: true, p2: false);
		string text = RecordAt(handle)?.Name ?? AttackerLabel(handle);
		bool flag2 = dist > 150f && !Function.Call<bool>(7006499128492128287uL, new InputArgument[1] { handle }) && SafeToDelete(p);
		if (!flag || flag2)
		{
			GrudgeLog(string.Format("FIGHT OVER {0} (ped {1}) - {2}: he no longer fights you{3}", text, handle, why, flag2 ? " - out of sight, removed" : ""));
		}
		if (flag2)
		{
			p.Delete();
		}
	}
}
