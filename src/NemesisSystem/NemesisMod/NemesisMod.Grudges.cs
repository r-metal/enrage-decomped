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
	private sealed class Provocation
	{
		public int Insults;

		public int InsultStreak;

		public int Greetings;

		public int Hits;

		public bool Escalated;

		public bool InsultLimitReached;

		public bool KnockedOut;

		public bool Wounded;

		public bool Slashed;

		public bool Robbed;

		public bool Kidnapped;

		public bool HorseKilled;

		public bool HorseStolen;

		public bool WagonStolen;

		public bool FriendKilled;

		public bool SawFace;

		public bool SeenMasked;

		public float Score;

		public Personality Temper;

		public NemesisRecord Shadow;

		public readonly List<string> Ledger = new List<string>();

		public int LastActionAt;

		public int LastSocialAt = -100000;

		public long SocialDoneAt;

		public bool Settled;

		public bool Insulted;

		public bool Threatened;

		public bool KoScored;

		public bool FoughtScored;

		public float SettledScore;

		public int LastHarmAt = -1000000;

		public int LastWoundAt = -100000;

		public int ShotAgainCount;

		public int DownSince = -1;

		public int LastHitTime = -100000;

		public int LastBumpTime = -100000;

		public int LastRobAttempt = -100000;

		public int LastRobbedTime = -100000;

		public int LastTime;

		public uint ModelHash;

		public string Zone;

		public GrudgeOrigin? RolledFor;

		public GrudgeOrigin? Pending;

		public int PendingSince;

		public bool Fled;

		public bool FledScored;

		public bool FoughtBack;

		public bool SettledFighting;

		public bool HeldForMission;

		public Vector3 Pos;

		public NemesisRecord Record;
	}

	private readonly Dictionary<int, int> _combatants = new Dictionary<int, int>();

	private readonly HashSet<int> _countedDeaths = new HashSet<int>();

	private readonly Dictionary<int, int> _shotAtAt = new Dictionary<int, int>();

	private int _nextCombatScan;

	private bool _wasShootingAny;

	private HashSet<int> _aliveLastScan = new HashSet<int>();

	private HashSet<int> _aliveNow = new HashSet<int>();

	private readonly HashSet<int> _downSeen = new HashSet<int>();

	private int _nextBumpScan;

	private readonly HashSet<int> _comradeScored = new HashSet<int>();

	private readonly Dictionary<int, int> _hitByYouAt = new Dictionary<int, int>();

	internal readonly List<(int at, Vector3 pos, string gang)> RecentKills = new List<(int, Vector3, string)>();

	private readonly HashSet<int> _killNoted = new HashSet<int>();

	private readonly HashSet<int> _knockedOutWorld = new HashSet<int>();

	private readonly Dictionary<string, int> _lastKoAt = new Dictionary<string, int>();

	private readonly Dictionary<int, long> _beatCounted = new Dictionary<int, long>();

	private void UpdateCombatGrudges(Ped player)
	{
		try
		{
			UpdateBumps(player);
		}
		catch (Exception ex)
		{
			GrudgeLog("BUMP failed: " + ex.Message);
		}
		UpdateNerve(player);
		bool flag = PED.IS_PED_SHOOTING(player.Handle);
		if (flag && !_wasShootingAny && _combatants.Count > 0)
		{
			var (cam, dir) = AimRay();
			foreach (int item in _combatants.Keys.ToList())
			{
				Ped ped = Entity.FromHandle(item) as Ped;
				if (!(ped == null) && ped.Exists() && !ped.IsDead && (!_shotAtAt.TryGetValue(item, out var value) || Game.GameTime - value >= 3000) && (!_hitByYouAt.TryGetValue(item, out var value2) || Game.GameTime - value2 >= 1200) && ShotPasses(cam, dir, ped, 2.5f))
				{
					_shotAtAt[item] = Game.GameTime;
					NerveNearMiss(item);
					Provocation provocation = RememberStranger(item);
					if (provocation != null && provocation.Record == null)
					{
						Grudge(item, provocation, GrudgeAction.ShotAt);
					}
				}
			}
		}
		_wasShootingAny = flag;
		if (Game.GameTime < _nextCombatScan)
		{
			return;
		}
		_nextCombatScan = Game.GameTime + 500;
		foreach (Ped item2 in PedsAround(Game.Player.Character))
		{
			if (item2 == null || !item2.Exists() || item2.IsPlayer || item2.Position.DistanceTo(player.Position) > 80f || !item2.IsHuman || Kids.IsChild(item2))
			{
				continue;
			}
			int handle = item2.Handle;
			if (item2.IsDead)
			{
				if (LooksKnockedOut(item2))
				{
					_aliveNow.Add(handle);
					continue;
				}
				if (_aliveLastScan.Contains(handle) && PED.GET_PED_SOURCE_OF_DEATH(handle) == player.Handle && _countedDeaths.Add(handle))
				{
					WitnessKill(item2);
				}
				if (!_combatants.ContainsKey(handle) || !_countedDeaths.Contains(handle) || LooksKnockedOut(item2) || PED.GET_PED_SOURCE_OF_DEATH(handle) != player.Handle || !_comradeScored.Add(handle))
				{
					continue;
				}
				foreach (int item3 in _combatants.Keys.ToList())
				{
					if (item3 == handle)
					{
						continue;
					}
					Ped ped2 = Entity.FromHandle(item3) as Ped;
					if (!(ped2 == null) && ped2.Exists() && !ped2.IsDead && !(ped2.Position.DistanceTo(item2.Position) > 40f))
					{
						Provocation provocation2 = RememberStranger(item3);
						if (provocation2 != null && provocation2.Record == null)
						{
							Grudge(item3, provocation2, GrudgeAction.ComradeKilled);
						}
					}
				}
				continue;
			}
			_aliveNow.Add(handle);
			if (!PED.IS_PED_IN_COMBAT(handle, player.Handle))
			{
				continue;
			}
			bool flag2 = !_combatants.ContainsKey(handle);
			if (flag2)
			{
				try
				{
					uint key = ModGroupOf(handle);
					int value3;
					bool flag3 = _cameAtYouGroup.TryGetValue(key, out value3) && Game.GameTime - value3 < 60000 && !ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(handle, player.Handle, p2: true, p3: true);
					if (!IsLawman(item2) && (flag3 || !YouStartedIt(handle, player, out var _)))
					{
						if (_cameAtYou.Count > 300)
						{
							_cameAtYou.Clear();
						}
						_cameAtYou.Add(handle);
						_cameAtYouGroup[key] = Game.GameTime;
					}
				}
				catch
				{
				}
			}
			_combatants[handle] = Game.GameTime;
			if (flag2)
			{
				RememberStranger(handle);
			}
			if (flag2)
			{
				try
				{
					NoteFirstBlow(item2, player);
				}
				catch (Exception ex2)
				{
					GrudgeLog("PEACE first blow failed: " + ex2.Message);
				}
			}
		}
		try
		{
			UpdatePeace(player);
		}
		catch (Exception ex3)
		{
			GrudgeLog("PEACE update failed: " + ex3.Message);
		}
		HashSet<int> aliveNow = _aliveNow;
		HashSet<int> aliveLastScan = _aliveLastScan;
		_aliveLastScan = aliveNow;
		_aliveNow = aliveLastScan;
		_aliveNow.Clear();
		foreach (int item4 in (from kv in _combatants
			where Game.GameTime - kv.Value > 60000
			select kv.Key).ToList())
		{
			_combatants.Remove(item4);
			_shotAtAt.Remove(item4);
		}
		if (_countedDeaths.Count > 200)
		{
			_countedDeaths.Clear();
			_comradeScored.Clear();
		}
	}

	internal void NoteKill(Ped dead, string gang = null)
	{
		if (!(dead == null) && dead.Exists() && _killNoted.Add(dead.Handle))
		{
			_tensionKills++;
			if (IsOurs(dead.Handle))
			{
				_tensionKillsOurs++;
			}
			RecentKills.Add((Game.GameTime, dead.Position, gang ?? RecordAt(dead.Handle)?.Gang ?? Gangs.ForModel((uint)dead.Model.Hash)?.Id));
			RecentKills.RemoveAll(((int at, Vector3 pos, string gang) k) => Game.GameTime - k.at > 600000);
		}
	}

	private void WitnessKill(Ped dead)
	{
		Ped character = Game.Player.Character;
		NoteKill(dead);
		string text = null;
		try
		{
			text = GoreOf(dead);
		}
		catch
		{
		}
		NemesisRecord nemesisRecord = RecordAt(dead.Handle);
		try
		{
			NerveAllyKilled(dead, nemesisRecord != null && (nemesisRecord == ActiveRecord || nemesisRecord.Rank >= 3), text);
		}
		catch (Exception ex)
		{
			GrudgeLog("NERVE failed: " + ex.Message);
		}
		if (text != null)
		{
			GrudgeLog("GORE you " + text + " (" + WhoIs(dead.Handle) + ")");
		}
		try
		{
			WitnessSilenced(dead);
			Witness((text != null) ? "gore" : "kill", dead.Handle, dead.Position, (text != null) ? ("saw you " + text) : null);
		}
		catch (Exception ex2)
		{
			GrudgeLog("WITNESS failed: " + ex2.Message);
		}
		try
		{
			AffiliateWronged(dead.Handle, (text != null) ? "gore" : "kill");
		}
		catch
		{
		}
		CountYourCrime("kill");
		if (text != null)
		{
			CountYourCrime("gore");
		}
		try
		{
			RememberMaskedKill(dead, character);
		}
		catch (Exception ex3)
		{
			GrudgeLog("MASKED KILL failed: " + ex3.Message);
		}
		NemesisRecord nemesisRecord2 = RecordAt(dead.Handle);
		string text2 = WhoIs(dead.Handle);
		bool flag = _followerOf.ContainsKey(dead.Handle) || (_activeNemesisPed != null && dead.Handle == _activeNemesisPed.Handle);
		foreach (Ped item in PedsAround(Game.Player.Character))
		{
			if (item == null || !item.Exists() || item.IsDead || item.IsPlayer || item.Handle == dead.Handle)
			{
				continue;
			}
			float num = item.Position.DistanceTo(dead.Position);
			if (num > 40f)
			{
				continue;
			}
			Interrogation interrogation = InterroFor(item.Handle);
			if (interrogation != null)
			{
				Terrify(interrogation, (text != null) ? GoreAt(num, Balance.TerrorComradeKilled) : Balance.TerrorComradeKilled, (text != null) ? ("SAW YOU " + text.ToUpperInvariant()) : "SAW YOU KILL");
			}
			NemesisRecord nemesisRecord3 = interrogation?.Rec ?? RecordAt(item.Handle);
			if (nemesisRecord3 != null && interrogation != _interro && (!_fighters.TryGetValue(item.Handle, out var value) || value.Done))
			{
				bool flag2 = flag || (nemesisRecord2 != null && (nemesisRecord2.Gang == nemesisRecord3.Gang || FriendsOf(nemesisRecord3).Contains(nemesisRecord2)));
				int fear = nemesisRecord3.Fear;
				int num2 = (flag2 ? (Balance.FearPerComradeKilled * 2) : Balance.FearPerComradeKilled);
				nemesisRecord3.AddFear((text != null) ? GoreAt(num, num2) : num2);
				if (nemesisRecord3.Fear != fear)
				{
					nemesisRecord3.Touch();
					Feed("SAW YOU KILL " + text2 + ": " + nemesisRecord3.Name + " fear " + Delta(fear, nemesisRecord3.Fear) + (flag2 ? "  (one of his own)" : ""), (150, 170, 210), nemesisRecord3);
				}
			}
		}
	}

	private void OnPlayerWounded(int victim)
	{
		Provocation provocation = RememberStranger(victim);
		if (provocation != null && !provocation.Wounded)
		{
			if (provocation.Record == null)
			{
				Grudge(victim, provocation, GrudgeAction.Wounded);
			}
			else
			{
				Feed("SHOT " + provocation.Record.Name, Theme.Warn, provocation.Record);
				CrimeSeen(victim, CrimeOf(GrudgeAction.Wounded));
			}
		}
		else if (provocation != null && provocation.Record == null && provocation.ShotAgainCount < 2 && Game.GameTime - provocation.LastWoundAt > 1500)
		{
			provocation.ShotAgainCount++;
			Grudge(victim, provocation, GrudgeAction.Wounded);
		}
		if (provocation != null)
		{
			provocation.Wounded = true;
			provocation.LastWoundAt = Game.GameTime;
		}
	}

	internal static bool LooksKnockedOut(Ped ped)
	{
		if (ped == null || !ped.Exists())
		{
			return false;
		}
		if (Downed.IsFinished(ped.Handle))
		{
			return false;
		}
		if (ped.IsIncapacitated)
		{
			return true;
		}
		if (!ped.IsDead)
		{
			return false;
		}
		if (GunMelee.KnockedOutByGunButt(ped))
		{
			return true;
		}
		uint num = PED.GET_PED_CAUSE_OF_DEATH(ped.Handle);
		if (num == 0)
		{
			return false;
		}
		uint num2 = WEAPON.GET_WEAPONTYPE_GROUP(num);
		if (num2 != 2685387236u)
		{
			return num2 == 308416707;
		}
		return true;
	}

	private static bool ReallyDead(Ped ped)
	{
		if (ped.IsDead)
		{
			return !LooksKnockedOut(ped);
		}
		return false;
	}

	private static bool Executed(int handle)
	{
		if (Downed.IsFinished(handle))
		{
			return !Downed.WasKicked(handle);
		}
		return false;
	}

	internal bool KoByYou(Ped ped, Ped player)
	{
		if (ped == null || player == null || !ped.Exists() || !player.Exists())
		{
			return false;
		}
		if (LawTook(ped.Handle))
		{
			return false;
		}
		if (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true))
		{
			return true;
		}
		if (_hitByYouAt.TryGetValue(ped.Handle, out var value) && Game.GameTime - value < 10000)
		{
			return true;
		}
		return GrappledBy(ped, player);
	}

	private void Mark(int handle, Action<Provocation> what)
	{
		Provocation provocation = RememberStranger(handle);
		if (provocation != null)
		{
			what(provocation);
		}
	}

	private bool OnHorseHurt(int handle)
	{
		Ped horse = Entity.FromHandle(handle) as Ped;
		if (horse == null || !horse.Exists() || horse.IsHuman || !horse.Model.IsHorse)
		{
			return false;
		}
		int num = PED._GET_RIDER_OF_MOUNT(handle, p1: true);
		if (num == 0)
		{
			num = (from p in World.GetAllPeds()
				where p.Position.DistanceTo(horse.Position) < 25f
				select p).FirstOrDefault((Ped p) => p.IsHuman && !p.IsPlayer && PED._GET_LAST_MOUNT(p.Handle) == handle)?.Handle ?? 0;
		}
		if (num != 0 && num != Game.Player.Character.Handle)
		{
			_horseRiders[handle] = num;
			_horseHurtAt[handle] = Game.GameTime;
		}
		return true;
	}

	private void UpdateHorses()
	{
		foreach (int item in _horseRiders.Keys.ToList())
		{
			Ped ped = Entity.FromHandle(item) as Ped;
			if (ped == null || !ped.Exists())
			{
				_horseRiders.Remove(item);
			}
			else
			{
				if (!ped.IsDead)
				{
					continue;
				}
				int rider = _horseRiders[item];
				int value;
				bool num = PED.GET_PED_SOURCE_OF_DEATH(item) == Game.Player.Character.Handle || (_horseHurtAt.TryGetValue(item, out value) && Game.GameTime - value < 60000);
				_horseHurtAt.Remove(item);
				if (!num)
				{
					_horseRiders.Remove(item);
					continue;
				}
				Mark(rider, delegate(Provocation p)
				{
					if (!p.HorseKilled)
					{
						Grudge(rider, p, GrudgeAction.HorseKilled);
					}
					p.HorseKilled = true;
				});
				_horseRiders.Remove(item);
			}
		}
	}

	private Provocation RememberStranger(int handle)
	{
		if (handle == 0)
		{
			return null;
		}
		if (_activeNemesisPed != null && handle == _activeNemesisPed.Handle)
		{
			return null;
		}
		Ped ped = Entity.FromHandle(handle) as Ped;
		if (ped == null || ped.IsPlayer || !ped.IsHuman || ReallyDead(ped) || Kids.IsChild(ped) || IsStoryPed(ped))
		{
			return null;
		}
		if (GameOwned(ped) && !MissionMemoryOk(ped))
		{
			return null;
		}
		if (IsLawman(ped) && RecordAt(handle) == null)
		{
			return null;
		}
		if (!_provoked.TryGetValue(handle, out var value))
		{
			NoteModel(ped);
			NemesisRecord nemesisRecord = RecordAt(handle);
			if (nemesisRecord == null)
			{
				eRelationshipType relationshipWithPed = ped.GetRelationshipWithPed(Game.Player.Character);
				if (relationshipWithPed == eRelationshipType.Like || relationshipWithPed == eRelationshipType.Respect)
				{
					return null;
				}
			}
			if (!_named.ContainsKey(handle) && Config.NamingEnabled && LookReadable(ped))
			{
				_named[handle] = NameByLook(ped);
				_namesGiven++;
			}
			if (_provoked.TryGetValue(handle, out var value2))
			{
				value = value2;
			}
			else
			{
				NemesisRecord nemesisRecord2 = ShadowFor(ped);
				Personality temper = KnownTemper(ped) ?? nemesisRecord2.Personality ?? Personalities.Pick(_rng, nemesisRecord2.Gang != null);
				value = (_provoked[handle] = new Provocation
				{
					Temper = temper,
					Shadow = nemesisRecord2,
					ModelHash = (uint)ped.Model.Hash,
					Zone = GetZoneName(ped.Position),
					Pos = ped.Position,
					Record = nemesisRecord
				});
				if (nemesisRecord == null)
				{
					RememberPerson(handle, met: false);
				}
			}
		}
		if (value.Shadow != null && value.Shadow.Personality != value.Temper)
		{
			value.Shadow.Personality = value.Temper;
			Named value3;
			string arg = (_named.TryGetValue(handle, out value3) ? value3.Name : handle.ToString());
			Traits.Roll(value.Shadow, Seeds.For(new NemesisRecord
			{
				Id = $"stranger:{arg}|{value.ModelHash:X8}"
			}, "traits"), null);
		}
		value.LastTime = Game.GameTime;
		bool num = PlayerMasked(Game.Player.Character);
		float num2 = ((_combatants.ContainsKey(handle) || PED.IS_PED_IN_COMBAT(handle, Game.Player.Character.Handle)) ? 60f : 25f);
		if (num)
		{
			value.SeenMasked = true;
		}
		else if (ped.Position.DistanceTo(Game.Player.Character.Position) < num2)
		{
			value.SawFace = true;
		}
		MarkNamed(handle, 3);
		return value;
	}

	private static GrudgeOrigin? Qualifies(Provocation p, NemesisSettings cfg)
	{
		if (p.Wounded)
		{
			return GrudgeOrigin.Wounded;
		}
		if (p.Slashed)
		{
			return GrudgeOrigin.Slashed;
		}
		if (p.HorseKilled)
		{
			return GrudgeOrigin.HorseKilled;
		}
		if (p.HorseStolen)
		{
			return GrudgeOrigin.HorseStolen;
		}
		if (p.WagonStolen)
		{
			return GrudgeOrigin.WagonStolen;
		}
		if (p.FriendKilled)
		{
			return GrudgeOrigin.FriendKilled;
		}
		if (p.Robbed)
		{
			return GrudgeOrigin.Robbed;
		}
		if (p.KnockedOut)
		{
			return GrudgeOrigin.KnockedOut;
		}
		if (p.Kidnapped)
		{
			return GrudgeOrigin.Kidnapped;
		}
		if (p.Hits >= cfg.BeatingHits)
		{
			return GrudgeOrigin.Beaten;
		}
		if (p.Threatened)
		{
			return GrudgeOrigin.Threatened;
		}
		if (p.Insulted)
		{
			return GrudgeOrigin.Insulted;
		}
		return null;
	}

	private void UpdateGrudges(Ped player)
	{
		UpdateHorses();
		UpdateCombatGrudges(player);
		foreach (int handle in _provoked.Keys.ToList())
		{
			Provocation provocation = _provoked[handle];
			Ped ped = Entity.FromHandle(handle) as Ped;
			if ((ped != null && ped.Exists() && ped.Model.Hash != (int)provocation.ModelHash) || (provocation.Record != null && !Roster.Contains(provocation.Record)))
			{
				_provoked.Remove(handle);
				continue;
			}
			bool flag = ped != null && ped.Exists() && ReallyDead(ped);
			if (provocation.HeldForMission && !flag && ped != null && ped.Exists() && !MissionHeld(ped) && !provocation.Settled && provocation.Record == null)
			{
				provocation.HeldForMission = false;
				GrudgeLog($"SETTLE ped={handle} {WhoIs(handle)}: the mission is over - deciding now");
				SettleGrudge(handle, provocation, ped);
				if (provocation.Record == null)
				{
					_provoked.Remove(handle);
					continue;
				}
			}
			if ((!provocation.HeldForMission || !(ped != null) || !ped.Exists() || flag || !MissionHeld(ped) || !(ped.Position.DistanceTo(player.Position) < 600f)) && (ped == null || !ped.Exists() || flag || ped.Position.DistanceTo(player.Position) > 150f))
			{
				if (!flag && provocation.Record == null && provocation.Score > 0f && !provocation.Settled)
				{
					SettleGrudge(handle, provocation, null);
				}
				_provoked.Remove(handle);
				continue;
			}
			if (!provocation.FoughtBack && PED.IS_PED_IN_COMBAT(handle, player.Handle))
			{
				provocation.FoughtBack = true;
			}
			if (!provocation.Fled && !provocation.FoughtBack && !ped.IsDead && PED.IS_PED_FLEEING(handle) && !ToldToFlee(handle) && !_townMen.Any((TownMan m) => m.Leaving && m.Ped != null && m.Ped.Handle == handle) && FleeingYou(ped, player))
			{
				provocation.Fled = true;
			}
			if (provocation.Fled && !provocation.FledScored)
			{
				provocation.FledScored = true;
				if (provocation.Record != null)
				{
					int hate = provocation.Record.Hate;
					int fear = provocation.Record.Fear;
					provocation.Record.Hate = Math.Max(Math.Min(provocation.Record.Hate, 10), provocation.Record.Hate - Balance.FledHateCut / 2);
					provocation.Record.AddFear(Balance.FearPerFlee);
					SaveRoster();
					Feed("FLED " + provocation.Record.Name + ": hate " + Delta(hate, provocation.Record.Hate) + "  fear " + Delta(fear, provocation.Record.Fear), Theme.Good, provocation.Record);
				}
				else
				{
					Feed(Pron.Her($"FLED {WhoIs(handle)}: ran from you - if it sticks, he'll be afraid of you (hate -{Balance.FledHateCut}, fear +{Balance.FearPerFlee})", Pron.FemaleAt(handle)), Theme.Good);
				}
			}
			Downed.Saw(ped, LooksKnockedOut(ped));
			bool flag2 = TASK.IS_PED_IN_WRITHE(handle) || (_bulletImpact.LatestWound(handle) != null && !GunMelee.KnockedOutByGunButt(ped));
			if (ped.IsDead && !provocation.KnockedOut && (provocation.Hits > 0 || KoByYou(ped, player)) && LooksKnockedOut(ped) && !flag2 && !LawTook(handle))
			{
				provocation.KnockedOut = true;
			}
			if (provocation.Hits > 0 && !provocation.KnockedOut && !LawTook(handle))
			{
				if ((!ped.IsIncapacitated && !ped.IsRagdoll) || Game.GameTime - provocation.LastHitTime >= 10000 || flag2)
				{
					provocation.DownSince = -1;
				}
				else if (provocation.DownSince < 0)
				{
					provocation.DownSince = Game.GameTime;
				}
				else if (Game.GameTime - provocation.DownSince >= 1500)
				{
					provocation.KnockedOut = true;
				}
			}
			if (provocation.KnockedOut && !provocation.KoScored)
			{
				provocation.KoScored = true;
				if (provocation.Record == null)
				{
					Grudge(handle, provocation, GrudgeAction.KnockOut);
				}
				else
				{
					CrimeSeen(handle, CrimeOf(GrudgeAction.KnockOut));
				}
			}
			if (provocation.FoughtBack && !provocation.FoughtScored)
			{
				provocation.FoughtScored = true;
				if (provocation.Record == null)
				{
					Grudge(handle, provocation, GrudgeAction.FoughtBack);
				}
			}
			if (provocation.Record != null)
			{
				GrudgeOrigin? rolledFor = Qualifies(provocation, Config);
				if (rolledFor.HasValue && (!provocation.RolledFor.HasValue || rolledFor.Value > provocation.RolledFor.Value))
				{
					provocation.RolledFor = rolledFor;
					DeepenGrudge(provocation.Record, rolledFor.Value, rolledFor.Value != GrudgeOrigin.KnockedOut);
				}
			}
			else if (!provocation.Settled && !(provocation.Score <= 0f) && (provocation.Score >= (float)Balance.GsThreshold || Game.GameTime - provocation.LastActionAt >= Balance.GsSettleSeconds * 1000))
			{
				SettleGrudge(handle, provocation, ped);
			}
		}
	}

	private void SettleGrudge(int handle, Provocation p, Ped ped)
	{
		Ped ped2 = ped ?? (Entity.FromHandle(handle) as Ped);
		if (ped2 != null && ped2.Exists() && !ReallyDead(ped2) && MissionHeld(ped2))
		{
			if (!p.HeldForMission)
			{
				p.HeldForMission = true;
				GrudgeLog($"SETTLE ped={handle} {WhoIs(handle)} score={p.Score:0}: a mission still has him - he decides when it's over");
			}
			return;
		}
		p.Settled = true;
		p.SettledScore = p.Score;
		if (_acqOf.TryGetValue(handle, out var value))
		{
			value.GrudgeSettled = true;
			value.SawFace = p.SawFace;
			value.Grudge = (int)p.Score;
			_peopleDirty = true;
		}
		GrudgeOrigin grudgeOrigin = (GrudgeOrigin)(((int?)Qualifies(p, Config)) ?? ((p.FoughtBack || _combatants.ContainsKey(handle)) ? 4 : 2));
		if (!p.SawFace)
		{
			GrudgeLog(string.Format("SETTLE ped={0} {1} score={2:0} -> {3}, no nemesis", handle, grudgeOrigin, p.Score, p.SeenMasked ? "DISGUISED" : "never got a look at you (too far)"));
			DisguiseSaves++;
			Feed(string.Format("GRUDGE {0} ({1:0}): never saw your face ({2}) -> no nemesis", WhoIs(handle), p.Score, p.SeenMasked ? "masked" : "too far off"), Theme.Good);
			return;
		}
		int num = SettleChance(p);
		int num2 = _rng.Next(100);
		bool flag = num2 < num;
		GrudgeLog(string.Format("SETTLE ped={0} {1} score={2:0} chance={3}% roll={4} -> {5}", handle, grudgeOrigin, p.Score, num, num2, flag ? "NEMESIS" : "lets it go"));
		Feed(string.Format("GRUDGE {0}: score {1:0} = {2}%{3} -> {4}", WhoIs(handle), p.Score, num, (num >= 100) ? "" : $", rolled {num2}", flag ? ("NEMESIS (" + grudgeOrigin.ToString() + ")") : "lets it go"), flag ? Theme.Blood : Theme.Dim);
		if (!flag || !MakeRoom())
		{
			return;
		}
		p.Record = CreateGrudgeNemesis(p, grudgeOrigin, handle);
		if (_followerOf.TryGetValue(handle, out var value2) && p.Record != null)
		{
			if (value2.Gang != null)
			{
				NemesisRecord record = p.Record;
				record.Gang = value2.Gang;
				record.Rank = 1;
				record.BossId = value2.Id;
				record.Hate = Math.Max(record.Hate, Hate.Initial(grudgeOrigin));
				if (record.KinId != null && FindRecord(record.KinId)?.Gang != record.Gang)
				{
					record.KinId = null;
					record.KinRelation = null;
				}
				History(record, "JOINED the " + Gangs.ById(record.Gang)?.Name + " under " + value2.Name + " - a hired gun with a grudge of his own", (170, 150, 210));
			}
			_followerOf.Remove(handle);
		}
		p.RolledFor = Qualifies(p, Config);
		if (p.Record != null)
		{
			if (p.Robbed && p.Record.TimesRobbed == 0)
			{
				p.Record.TimesRobbed = 1;
				WorldPart worldPart = PartFor(p.Record, "ROBBED", "and swore to get it back");
				Vector3 position = Game.Player.Character.Position;
				try
				{
					ChronicleDeed("rob", "You robbed " + p.Record.Name, "near " + GetZoneName(position), position, p.Record.Gang, worldPart);
				}
				catch
				{
				}
			}
			if (p.KnockedOut && p.Record.TimesKnockedOutByPlayer == 0)
			{
				p.Record.TimesKnockedOutByPlayer = 1;
			}
			if (p.Kidnapped && p.Record.TimesHogtied == 0)
			{
				p.Record.TimesHogtied = 1;
			}
		}
		if (ped != null && ped.Exists())
		{
			_worldNemeses[handle] = p.Record;
		}
		if (ped != null && ped.Exists() && ped.IsDead)
		{
			_knockedOutWorld.Add(handle);
		}
	}

	private bool MakeRoom()
	{
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.IsAlive && !n.PresumedDead && n.Origin != GrudgeOrigin.GangMember).ToList();
		if (list.Count < Config.MaxLivingNemeses)
		{
			return true;
		}
		NemesisRecord nemesisRecord = (from n in list
			where n.TimesAmbushed == 0 && n.TimesKilledPlayer == 0 && n.Scars.Count == 0 && n.Rank <= 1 && !_worldNemeses.ContainsValue(n) && n != ActiveRecord
			orderby n.Hate, n.KilledPlayerAt
			select n).FirstOrDefault();
		if (nemesisRecord == null)
		{
			GrudgeNotice("Too many men already want you dead - this one lets it go. (You can raise the limit in the settings - " + KeyCombo.SettingsText + ".)");
			return false;
		}
		RemoveRecord(nemesisRecord, "MakeRoom: the roster was full - least invested man dropped");
		GrudgeNotice(nemesisRecord.Name + " forgot about you (the roster was full).");
		return true;
	}

	private void DeepenGrudge(NemesisRecord rec, GrudgeOrigin origin, bool bump = true)
	{
		HiddenSlip(rec, (origin == GrudgeOrigin.Robbed) ? "rob" : "violence");
		rec.Touch();
		int hate = rec.Hate;
		if (bump)
		{
			int val = ((rec.Gang != null || !rec.Personality.HasValue) ? Hate.Initial(origin) : Personalities.StartingHate(rec.Personality.Value, origin));
			rec.Hate = Math.Max(rec.Hate, val);
			rec.AddHate(5);
			if (FistOrigin(origin))
			{
				CapScuffle(rec, hate);
			}
		}
		if (rec.Origin == GrudgeOrigin.GangMember || (rec.Origin != GrudgeOrigin.Killed && origin > rec.Origin))
		{
			if (rec.Origin == GrudgeOrigin.GangMember || string.IsNullOrEmpty(rec.KillLocation))
			{
				rec.KillLocation = GetZoneName(Game.Player.Character.Position);
			}
			rec.Origin = origin;
		}
		SaveRoster();
		Feed($"{origin.ToString().ToUpperInvariant()} {rec.Name}: hate {Delta(hate, rec.Hate)} ({rec.HateTier})", Theme.Warn, rec);
		if (Hate.TierOf(rec.Hate) > Hate.TierOf(hate))
		{
			GrudgeNotice(rec.FullName + "'s hatred deepens. He's " + rec.HateTier.ToString().ToLowerInvariant() + " now.");
		}
	}

	internal static bool FistOrigin(GrudgeOrigin o)
	{
		if (o != GrudgeOrigin.Greeted && o != GrudgeOrigin.Insulted && o != GrudgeOrigin.Threatened && o != GrudgeOrigin.Beaten)
		{
			return o == GrudgeOrigin.KnockedOut;
		}
		return true;
	}

	internal static int FistCap(NemesisRecord r)
	{
		bool flag = r.Personality == Personality.Hothead || r.Personality == Personality.Proud;
		if (flag && r.Aggression >= 75)
		{
			return Balance.FistCapRage;
		}
		if (flag && r.Aggression >= 60)
		{
			return Balance.FistCapViolent;
		}
		if (flag || r.Aggression >= 70)
		{
			return Balance.FistCapHot;
		}
		return Balance.FistCap;
	}

	internal static void CapScuffle(NemesisRecord r, int before)
	{
		if (!Gangs.Orders(r))
		{
			r.Hate = Math.Min(r.Hate, Math.Max(before, FistCap(r)));
		}
	}

	internal static int TraitSpread(NemesisRecord r)
	{
		return (r.Aggression - 50) / 3 + ((r.Disposition == Disposition.Surly) ? 8 : ((r.Disposition == Disposition.Friendly) ? (-8) : 0)) + ((r.Devotion == Devotion.Devout) ? (-4) : 0);
	}

	internal void KnockoutReaction(NemesisRecord rec)
	{
		int hate = rec.Hate;
		int value;
		bool flag = rec.Id != null && _lastKoAt.TryGetValue(rec.Id, out value) && Game.GameTime - value < 300000;
		if (rec.Id != null)
		{
			_lastKoAt[rec.Id] = Game.GameTime;
		}
		bool num = rec.Personality == Personality.Coward || rec.Personality == Personality.Cold || EffectiveFear(rec) * 2 >= rec.Hate;
		bool num2 = rec.Personality == Personality.Cold || EffectiveFear(rec) * 2 >= rec.Hate;
		int num3 = Balance.KoHate;
		if (flag)
		{
			num3 /= 2;
		}
		if (num2)
		{
			num3 /= 2;
		}
		rec.AddHate(num3);
		rec.AddFear(Balance.FearPerKnockout);
		if (num)
		{
			rec.Fear = Math.Min(100, rec.Fear + 5);
		}
		CapScuffle(rec, hate);
		Remember(rec, "Shame", flag ? 70 : 55, flag ? "you knocked him cold - again" : "you knocked him out cold");
		if (rec.Needs != null)
		{
			rec.Needs["Violence"] = Math.Max(0, Mind.N(rec, "Violence") - 30);
		}
	}

	internal void FitKinToLife(NemesisRecord rec)
	{
		if (rec.KinId == null)
		{
			return;
		}
		NemesisRecord nemesisRecord = FindRecord(rec.KinId);
		if (nemesisRecord != null)
		{
			string text = Backstory.RelationForAge(rec, nemesisRecord);
			if (text == null)
			{
				GrudgeLog($"HISTORY {rec.Name}: no kin tie to {nemesisRecord.Name} (born {rec.BornYear} / {nemesisRecord.BornYear})");
				rec.KinId = null;
				rec.KinRelation = null;
			}
			else if (text != rec.KinRelation)
			{
				GrudgeLog($"HISTORY {rec.Name}: {rec.KinRelation} -> {text} of {nemesisRecord.Name} (born {rec.BornYear} / {nemesisRecord.BornYear})");
				rec.KinRelation = text;
			}
		}
	}

	private NemesisRecord CreateGrudgeNemesis(Provocation p, GrudgeOrigin origin, int handle = 0)
	{
		NemesisRecord nemesisRecord = NewRecord(p.ModelHash, p.Zone);
		nemesisRecord.Touch();
		if (handle != 0 && Entity.FromHandle(handle) is Ped ped && ped.Exists())
		{
			KeepOutfit(ped, nemesisRecord);
		}
		StartRest(nemesisRecord);
		if (handle != 0 && _named.TryGetValue(handle, out var value))
		{
			GrudgeLog("NAMING new nemesis keeps the name " + value.Name);
			if (nemesisRecord.KinId != null && nemesisRecord.Name.LastIndexOf(' ') > 0 && value.Name.LastIndexOf(' ') > 0 && nemesisRecord.Name.Substring(nemesisRecord.Name.LastIndexOf(' ')) != value.Name.Substring(value.Name.LastIndexOf(' ')))
			{
				GrudgeLog("NAMING " + value.Name + ": no kin tie to " + FindRecord(nemesisRecord.KinId)?.Name + " (a different family name)");
				nemesisRecord.KinId = null;
				nemesisRecord.KinRelation = null;
			}
			nemesisRecord.Name = value.Name;
			if (value.Title != null)
			{
				nemesisRecord.Title = value.Title;
			}
			_named.Remove(handle);
		}
		nemesisRecord.TimesKilledPlayer = 0;
		nemesisRecord.Origin = origin;
		nemesisRecord.HasOrigin = true;
		nemesisRecord.OriginX = p.Pos.X;
		nemesisRecord.OriginY = p.Pos.Y;
		nemesisRecord.OriginZ = p.Pos.Z;
		nemesisRecord.TimesAntagonized = p.Insults;
		nemesisRecord.TimesGreeted = p.Greetings;
		nemesisRecord.Personality = p.Temper;
		CopyTraits(p.Shadow, nemesisRecord);
		FitKinToLife(nemesisRecord);
		nemesisRecord.Hate = ((nemesisRecord.Gang != null) ? Hate.Initial(origin) : Personalities.InitialHate(p.Temper, origin, _rng));
		nemesisRecord.Hate = Math.Min(100, nemesisRecord.Hate + (int)Math.Max(0f, (p.Score - (float)Balance.GsThreshold) / 3f));
		if (nemesisRecord.Gang == null && nemesisRecord.TraitsRolled)
		{
			nemesisRecord.Hate = Math.Max(5, Math.Min(100, nemesisRecord.Hate + TraitSpread(nemesisRecord)));
		}
		if (nemesisRecord.Gang == null && nemesisRecord.TraitsRolled && MoneyOrigin(origin))
		{
			nemesisRecord.Hate = Math.Max(5, Math.Min(100, nemesisRecord.Hate + ((nemesisRecord.Wealth == Wealth.Poor) ? Balance.RobbedPoorShift : ((nemesisRecord.Wealth == Wealth.Rich) ? (-Balance.RobbedRichShift) : 0))));
		}
		if (FistOrigin(origin))
		{
			nemesisRecord.Hate = Math.Min(nemesisRecord.Hate, FistCap(nemesisRecord));
		}
		if (p.Fled && !p.FoughtBack)
		{
			nemesisRecord.Hate = Math.Max(Math.Min(nemesisRecord.Hate, 10), nemesisRecord.Hate - Balance.FledHateCut);
			nemesisRecord.AddFear(Balance.FearPerFlee);
		}
		if (p.Ledger.Count > 0)
		{
			History(nemesisRecord, "as a stranger: " + string.Join(", ", p.Ledger), Theme.Dim);
		}
		History(nemesisRecord, string.Format("BECAME YOUR NEMESIS ({0}): grudge {1:0}  ->  hate {2}, fear {3}{4}", origin, p.Score, nemesisRecord.Hate, nemesisRecord.Fear, p.Fled ? "  (he ran)" : ""), Theme.Blood);
		Roster.Add(nemesisRecord);
		_bulletImpact.TagVictim(handle, nemesisRecord.Id, nemesisRecord.FullName ?? "");
		ForgetPerson(handle, nemesisRecord.Name);
		WorldPart worldPart = PartFor(nemesisRecord, "NEMESIS", nemesisRecord.GrudgeSummary);
		worldPart.Effects.Add($"hate {nemesisRecord.Hate} ({nemesisRecord.HateTier})");
		if (nemesisRecord.Fear > 0)
		{
			worldPart.Effects.Add($"fear {nemesisRecord.Fear}");
		}
		ChronicleDeed("nemesis", nemesisRecord.Name + " swore vengeance on you", (nemesisRecord.KillLocation != null) ? ("in " + nemesisRecord.KillLocation) : "", p.Pos, nemesisRecord.Gang, worldPart);
		SaveRoster();
		GrudgeNotice("A nemesis rises: " + nemesisRecord.FullName + ". " + origin switch
		{
			GrudgeOrigin.Beaten => "He'll remember that beating.", 
			GrudgeOrigin.KnockedOut => "He'll remember waking up in the dirt.", 
			GrudgeOrigin.WagonStolen => "You took his wagon - and his living with it.", 
			GrudgeOrigin.HorseStolen => "You rode off on his horse. He'll walk a long way to find you.", 
			GrudgeOrigin.Robbed => "He'll want back what you took, and then some.", 
			GrudgeOrigin.Kidnapped => "He'll remember the rope.", 
			GrudgeOrigin.HorseKilled => "You killed his horse. He won't let that go.", 
			GrudgeOrigin.Slashed => "He'll remember the feel of your blade.", 
			GrudgeOrigin.Wounded => "He'll remember who put that hole in him.", 
			_ => "He won't forget those words.", 
		});
		return nemesisRecord;
	}

	private void UpdateWorldNemeses(Ped player)
	{
		PollFactionKills();
		foreach (int item in _worldNemeses.Keys.ToList())
		{
			NemesisRecord nemesisRecord = _worldNemeses[item];
			Ped ped = Entity.FromHandle(item) as Ped;
			if (!Roster.Contains(nemesisRecord) || !nemesisRecord.IsAlive || ped == null || !ped.Exists() || (ped.Position.DistanceTo(player.Position) > 300f && !ENTITY.IS_ENTITY_A_MISSION_ENTITY(item)))
			{
				_worldNemeses.Remove(item);
				_knockedOutWorld.Remove(item);
				continue;
			}
			Downed.Saw(ped, LooksKnockedOut(ped));
			NoteHeld(ped);
			if (ReallyDead(ped))
			{
				_worldNemeses.Remove(item);
				_knockedOutWorld.Remove(item);
				if (!nemesisRecord.PresumedDead)
				{
					ResolveNemesisDeath(ped, nemesisRecord, Executed(item));
				}
			}
			else if (!ped.IsDead)
			{
				_knockedOutWorld.Remove(item);
			}
			else if (!_knockedOutWorld.Contains(item) && !KoByYou(ped, player))
			{
				_knockedOutWorld.Add(item);
			}
			else if (_knockedOutWorld.Add(item))
			{
				int hate = nemesisRecord.Hate;
				int fear = nemesisRecord.Fear;
				nemesisRecord.TimesKnockedOutByPlayer++;
				nemesisRecord.Touch();
				nemesisRecord.AddYourNotoriety(10);
				KnockoutReaction(nemesisRecord);
				Feed("KNOCKED OUT " + nemesisRecord.Name + ": hate " + Delta(hate, nemesisRecord.Hate) + "  fear " + Delta(fear, nemesisRecord.Fear), Theme.Warn, nemesisRecord);
				SaveRoster();
				GrudgeNotice("You knocked " + nemesisRecord.Name + " out cold. He'll wake up angrier.");
				WorldPart worldPart = PartFor(nemesisRecord, "KNOCKED OUT", nemesisRecord.Title);
				worldPart.Effects.Add($"hate {hate} -> {nemesisRecord.Hate}");
				worldPart.Effects.Add($"fear {fear} -> {nemesisRecord.Fear}");
				ChronicleDeed("ko", "You knocked out " + nemesisRecord.Name, "near " + GetZoneName(ped.Position), ped.Position, nemesisRecord.Gang, worldPart);
			}
		}
	}

	internal static bool MoneyOrigin(GrudgeOrigin o)
	{
		if (o != GrudgeOrigin.Robbed && o != GrudgeOrigin.HorseStolen)
		{
			return o == GrudgeOrigin.WagonStolen;
		}
		return true;
	}

	private GrudgeOrigin OriginForKiller(Provocation p)
	{
		if (p == null)
		{
			return GrudgeOrigin.Killed;
		}
		GrudgeOrigin? grudgeOrigin = Qualifies(p, Config);
		if (!grudgeOrigin.HasValue)
		{
			if (p.Insults <= 0 && !p.Escalated)
			{
				return GrudgeOrigin.Killed;
			}
			return GrudgeOrigin.Insulted;
		}
		return grudgeOrigin.GetValueOrDefault();
	}
}
