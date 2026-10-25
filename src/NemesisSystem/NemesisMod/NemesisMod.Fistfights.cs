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
	private int _peaceAggressor;

	private int _peaceLaw;

	private int _peaceSince;

	private int _peaceStep;

	private int _peaceQuietSince;

	private int _peaceCooldownUntil;

	private bool _peaceBaited;

	private bool _peaceArmed;

	private bool _peaceBlade;

	private bool _peaceHogtieTried;

	private bool _peaceArrested;

	private int _peaceArrestAt;

	private int _lawOnYou;

	private int _lawOnYouAt;

	private int _lawOnYouCooldown;

	private int _peaceTieAt;

	private readonly HashSet<int> _lawTook = new HashSet<int>();

	private readonly HashSet<int> _selfDefence = new HashSet<int>();

	internal readonly Dictionary<int, int> _gunOnAt = new Dictionary<int, int>();

	private int _yourCrimeAt = -100000;

	private string _yourCrimeWhat;

	private Vector3 _yourCrimePos;

	private int _peaceTieOrderAt;

	private int _peaceCarryAt;

	private int _peaceWarnedAt;

	private int _peaceBeatenAt;

	private int _peaceLastHitSeen;

	private bool _peaceTieFallback;

	private bool _peaceCarrying;

	private int _peaceBeatAt;

	private Vector3 _peaceCarryTo;

	private string _peaceName;

	private readonly Dictionary<int, (bool baited, int at)> _peaceWaiting = new Dictionary<int, (bool, int)>();

	private int _peaceMarchAt;

	private int _peaceMarchNudge;

	internal bool UnderArrest(int h)
	{
		if (h != 0)
		{
			if (h != _peaceAggressor || (_peaceStep < 3 && !_peaceArrested))
			{
				return _lawTook.Contains(h);
			}
			return true;
		}
		return false;
	}

	internal bool LawTook(int h)
	{
		if (h != 0)
		{
			if (!_lawTook.Contains(h))
			{
				if (h == _peaceAggressor)
				{
					return _peaceStep >= 3;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private string NameOf(int h)
	{
		object obj = RecordAt(h)?.Name;
		if (obj == null)
		{
			if (!_named.TryGetValue(h, out var value))
			{
				return "a stranger";
			}
			obj = value.Name;
		}
		return (string)obj;
	}

	internal void NoteAntagonized(int target)
	{
		if (target != 0)
		{
			_antagAt[target] = Game.GameTime;
		}
	}

	internal bool YouStartedIt(int h, Ped player, out string why)
	{
		why = null;
		if (WentForYouQuestioned(h))
		{
			return false;
		}
		int value;
		int value2;
		if (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(h, player.Handle, p2: true, p3: true) || _hitByYouAt.ContainsKey(h))
		{
			why = "you hurt him first";
		}
		else if (_shotAtAt.TryGetValue(h, out value) && Game.GameTime - value < 30000)
		{
			why = "you shot at him";
		}
		else if (WEAPON.IS_PED_ARMED(player.Handle, 4) && (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, h) || PLAYER.IS_PLAYER_TARGETTING_ENTITY(Game.Player.Handle, h, p2: false)))
		{
			why = "you had a gun on him";
		}
		else if (_gunOnAt.TryGetValue(h, out value2) && Game.GameTime - value2 < 10000)
		{
			why = "you had a gun on him a moment ago";
		}
		else if (Game.GameTime - GunMelee.LastShotAt < 10000 && ENTITY.GET_ENTITY_COORDS(h, alive: true, realCoords: true).DistanceTo(player.Position) < 50f)
		{
			why = "you'd just fired your gun near him";
		}
		else if (_gunOnAt.Any((KeyValuePair<int, int> kv) => kv.Key != h && Game.GameTime - kv.Value < 10000 && ENTITY.DOES_ENTITY_EXIST(kv.Key) && ENTITY.GET_ENTITY_COORDS(kv.Key, alive: true, realCoords: true).DistanceTo(ENTITY.GET_ENTITY_COORDS(h, alive: true, realCoords: true)) < 50f))
		{
			why = "you'd drawn on someone near him";
		}
		else if (Game.GameTime - _yourCrimeAt < 30000 && ENTITY.GET_ENTITY_COORDS(h, alive: true, realCoords: true).DistanceTo(_yourCrimePos) < 40f)
		{
			why = "you were in the middle of a crime (" + _yourCrimeWhat + ")";
		}
		return why != null;
	}

	private void NoteFirstBlow(Ped him, Ped player)
	{
		if (him == null || !him.Exists() || him.IsDead)
		{
			return;
		}
		int handle = him.Handle;
		string text = NameOf(handle);
		if (_peaceAggressor != 0)
		{
			GrudgeLog("PEACE " + text + " is fighting you - the law's already busy with " + _peaceName);
			return;
		}
		if (Game.GameTime < _peaceCooldownUntil)
		{
			GrudgeLog($"PEACE {text} is fighting you - a fight just ended here ({(_peaceCooldownUntil - Game.GameTime) / 1000} s)");
			return;
		}
		if (IsLawman(him))
		{
			GrudgeLog("PEACE " + text + " is a lawman - not his own case");
			return;
		}
		if (!InsideAnyTown(player.Position))
		{
			OurRole ourRole = RoleOf(handle);
			if ((ourRole != OurRole.None && ourRole != OurRole.Regular) || Gangs.ForModel((uint)him.Model.Hash) != null || RecordAt(handle)?.Gang != null)
			{
				return;
			}
		}
		if (OutfitFighter(handle))
		{
			return;
		}
		Gang gang = Gangs.ById(RecordAt(handle)?.Gang) ?? Gangs.ForModel((uint)him.Model.Hash);
		if (gang != null && gang.OrdersNotFeelings)
		{
			GrudgeLog("PEACE " + text + " is a soldier - not the town law's business");
			return;
		}
		if (Kids.IsChild(him) || (IsStoryPed(him) && RecordAt(handle) == null) || GameOwned(him))
		{
			GrudgeLog("PEACE " + text + " is fighting you - a child / story / mission ped, left to the game");
			return;
		}
		if (PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) > 0)
		{
			GrudgeLog("PEACE " + text + " is fighting you - you're already wanted, the law stays out");
			return;
		}
		if (YouStartedIt(handle, player, out var why))
		{
			LawComesForYou(him, player, why);
			return;
		}
		_selfDefence.Add(handle);
		try
		{
			Gang gang2 = Gangs.ById(RecordAt(handle)?.Gang) ?? Gangs.ForModel((uint)him.Model.Hash);
			FairNow(FairOutfit(gang2) ? gang2 : null, text);
			SwitchOutlaw(handle, "he swung at you", Game.GameTime);
		}
		catch (Exception ex)
		{
			GrudgeLog("PEACE fair window failed: " + ex.Message);
		}
		if (LAW.IS_LAW_INCIDENT_ACTIVE(Game.Player.Handle))
		{
			GrudgeLog("PEACE note: a law incident is open (the game's own) - siding with you anyway, he swung first");
		}
		int value;
		bool flag = _antagAt.TryGetValue(handle, out value) && Game.GameTime - value < 120000;
		bool flag2 = WEAPON.IS_PED_ARMED(handle, 5);
		Ped ped = FreeLawman(player, flag2 ? 110f : 70f);
		if (ped == null)
		{
			_peaceWaiting[handle] = (flag, Game.GameTime);
			GrudgeLog("PEACE " + text + " swung first" + (flag ? " (you baited him)" : "") + " - no lawman near who saw it yet, watching for one");
		}
		else
		{
			StartPeace(him, ped, flag, flag2, player);
		}
	}

	private Ped FreeLawman(Ped player, float range)
	{
		Vector3 me = player.Position;
		return (from t in PedSnapshot(player)
			where t.p != null && t.p.Exists() && !t.p.IsDead && IsLawman(t.p) && !IsOurs(t.p.Handle) && !GameOwned(t.p) && !IsStoryPed(t.p) && !GameEventPed(t.p) && !PED.IS_PED_IN_COMBAT(t.p.Handle, 0) && (t.pos - me).Length() < range
			select t.p into p
			orderby p.Position.DistanceTo(me)
			select p).FirstOrDefault((Ped p) => p.Position.DistanceTo(me) < 15f || ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(p.Handle, player.Handle, 17));
	}

	private void CheckPeaceWaiting(Ped player)
	{
		foreach (KeyValuePair<int, (bool, int)> item in _peaceWaiting.ToList())
		{
			int key = item.Key;
			Ped ped = new Ped(key);
			if (!ENTITY.DOES_ENTITY_EXIST(key) || ped.IsDead || Game.GameTime - item.Value.Item2 > 60000 || !PED.IS_PED_IN_COMBAT(key, player.Handle) || PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) > 0)
			{
				_peaceWaiting.Remove(key);
				continue;
			}
			bool flag = WEAPON.IS_PED_ARMED(key, 5);
			bool flag2 = PED.IS_PED_SHOOTING(key);
			Ped ped2 = FreeLawman(player, flag2 ? 130f : (flag ? 110f : 70f));
			if (ped2 == null)
			{
				continue;
			}
			_peaceWaiting.Remove(key);
			GrudgeLog(string.Format("PEACE a lawman came in range of {0}'s fight ({1} s in{2})", NameOf(key), (Game.GameTime - item.Value.Item2) / 1000, flag2 ? ", he's shooting" : ""));
			StartPeace(ped, ped2, item.Value.Item1, flag, player);
			break;
		}
	}

	private void StartPeace(Ped him, Ped law, bool baited, bool armed, Ped player)
	{
		int handle = him.Handle;
		Vector3 position = player.Position;
		_peaceAggressor = handle;
		_peaceLaw = law.Handle;
		_peaceSince = Game.GameTime;
		_peaceStep = 0;
		_peaceQuietSince = 0;
		bool flag = WEAPON.IS_PED_ARMED(handle, 4) || PED.IS_PED_SHOOTING(handle);
		bool flag2 = !flag && WEAPON.IS_PED_ARMED(handle, 1);
		_peaceBaited = baited;
		_peaceArmed = flag;
		_peaceBlade = flag2;
		_peaceName = NameOf(handle);
		_peaceHogtieTried = false;
		_peaceArrested = false;
		_peaceArrestAt = 0;
		_peaceBeatenAt = 0;
		_peaceWarnedAt = 0;
		_peaceCarrying = false;
		_peaceTieOrderAt = 0;
		_peaceTieFallback = false;
		_peaceLastHitSeen = (_hitByYouAt.TryGetValue(handle, out var value) ? value : 0);
		float num = law.Position.DistanceTo(position);
		string text = (flag ? "WITH A GUN" : (flag2 ? "with a blade" : "with his fists"));
		GrudgeLog(string.Format("PEACE {0} swung first{1} {2} - lawman {3} {4:0} m off {5}", _peaceName, baited ? " (you baited him)" : "", text, law.Handle, num, flag ? "goes for him" : "comes to arrest him"));
		Feed("PEACE " + _peaceName + " went for you " + text + (baited ? " - you goaded him" : "") + ": the law " + (flag ? "steps in" : "comes to arrest him"), Theme.Good);
		if (flag)
		{
			LawTakesHim(law, "Drop it!");
			return;
		}
		Crumbs.Native("FirstBlow law runs over");
		TASK.TASK_GO_TO_ENTITY(law.Handle, handle, 20000, 2.5f, flag2 ? 2.6f : 2f, 0f, 0);
		CrowdWatches(player, him);
	}

	private void LawTakesHim(Ped law, string line)
	{
		Crumbs.Native("FirstBlow law combat");
		ShowSubtitle("Lawman: " + line);
		TASK.TASK_COMBAT_PED(law.Handle, _peaceAggressor, 0, 16);
		_peaceStep = 2;
	}

	private void CrowdWatches(Ped player, Ped him)
	{
		Vector3 mid = (player.Position + him.Position) * 0.5f;
		int num = 0;
		foreach (var item in from t in PedSnapshot(player)
			orderby (t.pos - mid).Length()
			select t)
		{
			if (num >= 4)
			{
				break;
			}
			var (ped, _) = item;
			if (!(ped == null) && ped.Exists() && !ped.IsDead && ped.IsHuman && ped.Handle != him.Handle && ped.Handle != _peaceLaw && !((item.pos - mid).Length() > 25f) && !IsOurs(ped.Handle) && !IsLawman(ped) && !Kids.IsChild(ped) && !IsStoryPed(ped) && !GameOwned(ped) && !GameEventPed(ped) && !PED.IS_PED_IN_COMBAT(ped.Handle, 0) && !ped.IsHogtied && !PED.IS_PED_IN_ANY_VEHICLE(ped.Handle, atGetIn: false) && !PED.IS_PED_ON_MOUNT(ped.Handle))
			{
				Crumbs.Native("FirstBlow crowd looks");
				TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, him.Handle, 3000, 0f, 0f, 0f);
				TASK.TASK_LOOK_AT_ENTITY(ped.Handle, him.Handle, 12000, 0, 51, 0);
				num++;
			}
		}
		if (num > 0)
		{
			GrudgeLog($"PEACE {num} townsfolk stop to watch");
		}
	}

	private void UpdatePeace(Ped player)
	{
		if (_lawOnYou != 0 && (Game.GameTime - _lawOnYouAt > 30000 || !ENTITY.DOES_ENTITY_EXIST(_lawOnYou)))
		{
			GrudgeLog("PEACE the lawman who came for you is done");
			_lawOnYou = 0;
		}
		if (_peaceAggressor == 0)
		{
			if (_peaceWaiting.Count > 0 && Game.GameTime >= _peaceCooldownUntil)
			{
				CheckPeaceWaiting(player);
			}
			return;
		}
		Ped ped = new Ped(_peaceAggressor);
		Ped ped2 = new Ped(_peaceLaw);
		bool flag = !ENTITY.DOES_ENTITY_EXIST(_peaceLaw) || ped2.IsDead;
		bool flag2 = !ENTITY.DOES_ENTITY_EXIST(_peaceAggressor);
		bool flag3 = PLAYER.GET_PLAYER_WANTED_LEVEL(Game.Player.Handle) > 0;
		if (flag || (Game.GameTime - _peaceSince > 120000 && _peaceStep != 4 && _peaceStep != 5) || flag3)
		{
			EndPeace(flag ? "the lawman is gone" : (flag3 ? "you became wanted - the law's business now" : "2 minutes passed"), clean: false);
		}
		else if (_peaceStep == 4)
		{
			UpdateCarryOff(ped, ped2, player);
		}
		else
		{
			if (YouKeptAtHim(ped, ped2, player))
			{
				return;
			}
			bool flag4 = flag2 || ped.IsDead || ped.IsHogtied || LooksKnockedOut(ped);
			if (!flag2 && ped.IsDead && _peaceStep == 3 && !_peaceArrested)
			{
				int num = PED.GET_PED_SOURCE_OF_DEATH(_peaceAggressor);
				GrudgeLog("PEACE " + _peaceName + " died during the arrest - killed by " + ((num == player.Handle) ? "you" : ((num == _peaceLaw) ? "the lawman" : ((num == 0) ? "nobody known" : ("ped " + num)))));
				EndPeace("he died", clean: false);
				return;
			}
			bool flag5 = !flag4 && PED.IS_PED_IN_COMBAT(_peaceAggressor, player.Handle);
			if (_peaceStep != 2 && !flag4 && !_peaceArrested && (WEAPON.IS_PED_ARMED(_peaceAggressor, 4) || PED.IS_PED_SHOOTING(_peaceAggressor)))
			{
				GrudgeLog("PEACE " + _peaceName + " pulled a gun - the law goes for him");
				LawTakesHim(ped2, "Drop that gun!");
			}
			if (_peaceStep == 0 && !flag4 && ped2.Position.DistanceTo(ped.Position) < 6f)
			{
				StartArrest(ped, ped2);
			}
			if (_peaceStep == 3)
			{
				if (Game.GameTime - _peaceBeatAt >= 5000)
				{
					_peaceBeatAt = Game.GameTime;
					GrudgeLog($"PEACE arresting {_peaceName}: {(Game.GameTime - _peaceArrestAt) / 1000} s - fighting {flag5}, lawman {ped2.Position.DistanceTo(ped.Position):0.0} m");
				}
				if (flag5 && Game.GameTime - _peaceArrestAt > 4000)
				{
					GrudgeLog("PEACE " + _peaceName + " won't come quietly - the law goes for him");
					LawTakesHim(ped2, "Fine - the hard way!");
				}
				else if (ped2.Position.DistanceTo(ped.Position) < 2.4f || Game.GameTime - _peaceArrestAt > 10000)
				{
					StartMarch(ped, ped2, player);
				}
			}
			else if (_peaceStep == 5)
			{
				UpdateMarch(ped, ped2, player, flag5);
			}
			else if (flag5)
			{
				_peaceQuietSince = 0;
			}
			else if (_peaceQuietSince == 0)
			{
				_peaceQuietSince = Game.GameTime;
			}
			else if (Game.GameTime - _peaceQuietSince >= 3000 || flag4)
			{
				string text = ((_currentTown != null && TownReputation(_currentTown) <= -20) ? (new string[3] { "Can't say I care for you, mister. But he swung first. Go on.", "Folks here want you gone, and so do I. Still - he started it. Move along.", "I know who you are. But I saw him throw first. Get out of my sight." })[_rng.Next(3)] : (_peaceBaited ? (new string[3] { "You goaded him, mister - but he swung first. Move along.", "I heard you rile him. Still, he started it. Go on.", "Don't go baiting folk in my town. He threw first, so go on." })[_rng.Next(3)] : (new string[3] { "I saw it - he swung first. Go on about your business.", "He started it. You're free to go.", "That was self-defence. Move along." })[_rng.Next(3)]));
				if (ped2.Position.DistanceTo(player.Position) < 25f)
				{
					ShowSubtitle("Lawman: " + text);
				}
				EndPeace(flag4 ? "he's down" : "it's over", clean: true);
			}
		}
	}

	private void StartArrest(Ped him, Ped law)
	{
		_peaceStep = 3;
		_peaceArrestAt = Game.GameTime;
		ShowSubtitle("Lawman: " + (_peaceBlade ? (new string[3] { "Drop the knife! Drop it - now!", "Put that blade down! You're under arrest!", "Knife on the ground! Hands where I can see 'em!" })[_rng.Next(3)] : (new string[3] { "That's enough! You're under arrest!", "Hands up! You're coming with me!", "Hold it right there - you're under arrest!" })[_rng.Next(3)]));
		int handle = him.Handle;
		Crumbs.Native("FirstBlow arrest");
		if (_peaceBlade)
		{
			WEAPON.MAKE_PED_DROP_WEAPON(handle, p1: true, 0, p3: true, p4: true);
		}
		him.BlockPermanentEvents = true;
		him.RelationshipGroup = PED.GET_PED_RELATIONSHIP_GROUP_DEFAULT_HASH(handle);
		ForgetSwitch(handle);
		TASK.CLEAR_PED_TASKS_IMMEDIATELY(handle, p1: false, resetCrouch: true);
		TASK.TASK_HANDS_UP(handle, 15000, law.Handle, -1, p4: false);
		TASK.TASK_GO_TO_ENTITY(law.Handle, handle, 10000, 1f, 1.8f, 0f, 0);
		_peaceTieAt = 0;
		_peaceTieOrderAt = 0;
		_peaceTieFallback = false;
		_peaceCarrying = false;
		if (_peaceBeatenAt == 0)
		{
			_peaceBeatenAt = Game.GameTime;
		}
		_lawTook.Add(handle);
		GrudgeLog(string.Format("PEACE lawman {0} arrests {1}{2}", law.Handle, _peaceName, _peaceBlade ? " (knife dropped)" : ""));
	}

	private void ArrestedByLaw(Ped him)
	{
		NemesisRecord nemesisRecord = RecordAt(him.Handle);
		string text = _currentTown ?? TownAt(him.Position) ?? "town";
		GrudgeLog("PEACE " + _peaceName + " ARRESTED in " + text);
		Feed("ARRESTED " + _peaceName + " - the law took him in for going for you", Theme.Good, nemesisRecord);
		if (nemesisRecord != null)
		{
			nemesisRecord.RestUntil = Math.Max(nemesisRecord.RestUntil, GameMinutesNow() + 2880);
			History(nemesisRecord, "ARRESTED in " + text + " - the law saw him go for you first", Theme.Warn);
			Remember(nemesisRecord, "Shame", 50, "arrested in " + text + " for going for you");
			nemesisRecord.Touch();
			SaveRoster();
		}
	}

	private void LawComesForYou(Ped him, Ped player, string why)
	{
		string text = NameOf(him.Handle);
		if (WEAPON.IS_PED_ARMED(player.Handle, 5))
		{
			GrudgeLog("PEACE " + text + " is fighting you - you started it (" + why + ") with a weapon: the game's own law");
			return;
		}
		if (_lawOnYou != 0 || Game.GameTime < _lawOnYouCooldown)
		{
			GrudgeLog("PEACE " + text + " is fighting you - you started it (" + why + ")");
			return;
		}
		Vector3 me = player.Position;
		Ped ped = (from t in PedSnapshot(player)
			where t.p != null && t.p.Exists() && !t.p.IsDead && IsLawman(t.p) && !IsOurs(t.p.Handle) && !GameOwned(t.p) && !IsStoryPed(t.p) && !GameEventPed(t.p) && !PED.IS_PED_IN_COMBAT(t.p.Handle, 0) && (t.pos - me).Length() < 40f && ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(t.p.Handle, player.Handle, 17)
			select t.p into p
			orderby p.Position.DistanceTo(me)
			select p).FirstOrDefault();
		if (ped == null)
		{
			GrudgeLog("PEACE " + text + " is fighting you - you started it (" + why + "), no lawman saw it");
			return;
		}
		_lawOnYou = ped.Handle;
		_lawOnYouAt = Game.GameTime;
		_lawOnYouCooldown = Game.GameTime + 90000;
		ShowSubtitle("Lawman: " + (new string[3] { "You there! I saw that - you're under arrest!", "Hold it! You threw the first punch, mister!", "That's enough! You're coming with me!" })[_rng.Next(3)]);
		Crumbs.Native("FirstBlow law arrests you");
		TASK.TASK_ARREST_PED(ped.Handle, player.Handle);
		GrudgeLog($"PEACE you started it with {text} ({why}) - lawman {ped.Handle} {ped.Position.DistanceTo(me):0} m off saw it and comes to arrest you");
		Feed("PEACE you swung first at " + text + " - the law saw it", Theme.Warn);
	}

	private void StartMarch(Ped him, Ped law, Ped player)
	{
		_peaceStep = 5;
		_peaceMarchAt = Game.GameTime;
		_peaceMarchNudge = Game.GameTime;
		Vector3 vector = him.Position - player.Position;
		vector.Z = 0f;
		if (vector.Length() < 0.5f)
		{
			vector = new Vector3(1f, 0f, 0f);
		}
		vector.Normalize();
		Vector3 vector2 = him.Position + vector * (70f + (float)_rng.NextDouble() * 20f);
		Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector2, onGround: true, 0);
		_peaceCarryTo = ((safeCoordForPed.X == 0f && safeCoordForPed.Y == 0f) ? vector2 : safeCoordForPed);
		Crumbs.Native("FirstBlow march");
		TASK.CLEAR_PED_TASKS(_peaceAggressor, p1: true, p2: true);
		TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(_peaceAggressor, _peaceCarryTo.X, _peaceCarryTo.Y, _peaceCarryTo.Z, 1f, -1, 1f, 0, 0f);
		TASK.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_peaceLaw, _peaceAggressor, 0f, -1.6f, 0f, 1.2f, -1, 1f, persistFollowing: true, p9: false, walkOnly: false, p11: false, p12: false, p13: false);
		ShowSubtitle("Lawman: " + (new string[3] { "Walk. Nice and slow.", "Move it - a night in the cells'll cool you off.", "Go on. The judge can sort you out." })[_rng.Next(3)]);
		if (!_peaceArrested)
		{
			_peaceArrested = true;
			ArrestedByLaw(him);
		}
		GrudgeLog($"PEACE the lawman marches {_peaceName} off ({him.Position.DistanceTo(_peaceCarryTo):0} m to go)");
	}

	private void UpdateMarch(Ped him, Ped law, Ped player, bool fighting)
	{
		if (!ENTITY.DOES_ENTITY_EXIST(_peaceAggressor) || him.IsDead)
		{
			EndPeace("marched off", clean: true);
			return;
		}
		if (fighting)
		{
			GrudgeLog("PEACE " + _peaceName + " broke loose and fights - the law goes for him");
			LawTakesHim(law, "Fine - the hard way!");
			return;
		}
		if (Game.GameTime - _peaceMarchNudge > 5000 && ENTITY.GET_ENTITY_SPEED(_peaceAggressor) < 0.3f && him.Position.DistanceTo(_peaceCarryTo) > 3f)
		{
			_peaceMarchNudge = Game.GameTime;
			Crumbs.Native("FirstBlow march on");
			TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(_peaceAggressor, _peaceCarryTo.X, _peaceCarryTo.Y, _peaceCarryTo.Z, 1f, -1, 1f, 0, 0f);
		}
		float num = law.Position.DistanceTo(player.Position);
		bool flag = num > 45f && HiddenFromView(player, him.Position + new Vector3(0f, 0f, 1f));
		if (flag || Game.GameTime - _peaceMarchAt >= 90000)
		{
			if (BelongsToUs(_peaceAggressor))
			{
				Crumbs.Native("FirstBlow cells");
				him.Delete();
			}
			GrudgeLog(string.Format("PEACE {0} marched off to the cells ({1:0} m from you, {2})", _peaceName, num, flag ? "out of sight" : "90 s"));
			EndPeace("marched off to the cells", clean: true);
		}
	}

	private void StartCarryOff(Ped him, Ped law, Ped player)
	{
		_peaceStep = 4;
		_peaceCarryAt = Game.GameTime;
		_peaceCarrying = false;
		Crumbs.Native("FirstBlow pick up");
		TASK.TASK_PICKUP_CARRIABLE_ENTITY(_peaceLaw, _peaceAggressor);
		Vector3 vector = him.Position - player.Position;
		vector.Z = 0f;
		if (vector.Length() < 0.5f)
		{
			vector = new Vector3(1f, 0f, 0f);
		}
		vector.Normalize();
		Vector3 vector2 = him.Position + vector * (70f + (float)_rng.NextDouble() * 20f);
		Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector2, onGround: true, 0);
		_peaceCarryTo = ((safeCoordForPed.X == 0f && safeCoordForPed.Y == 0f) ? vector2 : safeCoordForPed);
		GrudgeLog("PEACE the lawman picks " + _peaceName + " up to carry him off");
	}

	private void UpdateCarryOff(Ped him, Ped law, Ped player)
	{
		if (!ENTITY.DOES_ENTITY_EXIST(_peaceAggressor))
		{
			EndPeace("carried off", clean: true);
			return;
		}
		bool flag = ENTITY.IS_ENTITY_ATTACHED(_peaceAggressor);
		if (!_peaceCarrying)
		{
			if (flag)
			{
				_peaceCarrying = true;
				Crumbs.Native("FirstBlow carry off");
				TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(_peaceLaw, _peaceCarryTo.X, _peaceCarryTo.Y, _peaceCarryTo.Z, 1f, -1, 1.5f, 0, 0f);
				ShowSubtitle("Lawman: " + (new string[3] { "Come on, you. A night in the cells'll cool you off.", "Let's go. The judge can sort you out.", "You're done for today." })[_rng.Next(3)]);
				GrudgeLog($"PEACE the lawman carries {_peaceName} off ({law.Position.DistanceTo(_peaceCarryTo):0} m to go)");
			}
			else if (Game.GameTime - _peaceCarryAt > 6000)
			{
				GrudgeLog("PEACE the lawman couldn't pick " + _peaceName + " up - he stays tied where he lies");
				EndPeace("arrested", clean: true);
			}
			return;
		}
		float num = law.Position.DistanceTo(player.Position);
		bool flag2 = num > 45f && HiddenFromView(player, law.Position + new Vector3(0f, 0f, 1f));
		if (!flag2 && Game.GameTime - _peaceCarryAt < 90000)
		{
			return;
		}
		if (BelongsToUs(_peaceAggressor))
		{
			Crumbs.Native("FirstBlow cells");
			ENTITY.DETACH_ENTITY(_peaceAggressor, p1: true, collision: true);
			Ped ped = new Ped(_peaceAggressor);
			if (ped.Exists())
			{
				ped.Delete();
			}
		}
		GrudgeLog(string.Format("PEACE {0} carried off to the cells ({1:0} m from you, {2})", _peaceName, num, flag2 ? "out of sight" : "90 s"));
		EndPeace("carried off to the cells", clean: true);
	}

	private bool YouKeptAtHim(Ped him, Ped law, Ped player)
	{
		int peaceAggressor = _peaceAggressor;
		if (_peaceBeatenAt == 0 && _fighters.TryGetValue(peaceAggressor, out var value) && (value.Broke || value.State == "fled" || value.State == "surrendered"))
		{
			_peaceBeatenAt = Game.GameTime;
		}
		if (_peaceBeatenAt == 0)
		{
			return false;
		}
		if (!_hitByYouAt.TryGetValue(peaceAggressor, out var value2) || value2 <= _peaceLastHitSeen || value2 < _peaceBeatenAt + 300)
		{
			return false;
		}
		_peaceLastHitSeen = value2;
		if (law.Position.DistanceTo(player.Position) > 40f || !ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(_peaceLaw, player.Handle, 17))
		{
			return false;
		}
		if (_peaceWarnedAt == 0)
		{
			_peaceWarnedAt = Game.GameTime;
			ShowSubtitle("Lawman: " + (new string[3] { "That's enough - he's done!", "Back off! He's had enough!", "Leave him be, mister - I've got him!" })[_rng.Next(3)]);
			GrudgeLog("PEACE you hit " + _peaceName + " after he was beaten - the lawman warns you");
			Feed("PEACE the lawman warned you off " + _peaceName, Theme.Warn);
			return false;
		}
		if (Game.GameTime - _peaceWarnedAt < 800)
		{
			return false;
		}
		GrudgeLog("PEACE you kept at " + _peaceName + " after the warning - the lawman comes for you");
		Feed("PEACE you wouldn't leave " + _peaceName + " be - the law comes for you", Theme.Blood);
		ShowSubtitle("Lawman: " + (new string[3] { "I warned you! You're under arrest!", "That's it - you're coming with me!", "Told you to back off. Hands up!" })[_rng.Next(3)]);
		Crumbs.Native("FirstBlow law arrests you");
		_lawOnYou = _peaceLaw;
		_lawOnYouAt = Game.GameTime;
		_lawOnYouCooldown = Game.GameTime + 90000;
		int peaceLaw = _peaceLaw;
		EndPeace("you kept hitting him", clean: false);
		TASK.TASK_ARREST_PED(peaceLaw, player.Handle);
		return true;
	}

	private void EndPeace(string why, bool clean)
	{
		GrudgeLog("PEACE over (" + why + ")" + (clean ? (" - the lawman saw " + _peaceName + " start it" + (_peaceBaited ? " (you baited him)" : "")) : ""));
		if (ENTITY.DOES_ENTITY_EXIST(_peaceLaw) && !IsOurs(_peaceLaw) && !GameOwned(new Ped(_peaceLaw)) && !PED.IS_PED_IN_COMBAT(_peaceLaw, 0))
		{
			Crumbs.Native("FirstBlow law released");
			TASK.CLEAR_PED_TASKS(_peaceLaw, p1: false, p2: false);
		}
		_peaceAggressor = 0;
		_peaceLaw = 0;
		_peaceStep = 0;
		_peaceCooldownUntil = Game.GameTime + 20000;
	}
}
