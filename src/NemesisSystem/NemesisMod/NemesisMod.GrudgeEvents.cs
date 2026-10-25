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
	private const int PromptTypeInsult = 4;

	private static readonly int[] PromptTypesRob = new int[3] { 7, 19, 20 };

	private const int StrangerMemoryMs = 600000;

	private const int ProvokeDedupeMs = 1500;

	private const int PunchDedupeMs = 250;

	private const int KnockoutDownMs = 1500;

	private const float ForgetDistance = 150f;

	private readonly Dictionary<int, Provocation> _provoked = new Dictionary<int, Provocation>();

	private readonly Dictionary<int, int> _horseRiders = new Dictionary<int, int>();

	private readonly Dictionary<int, int> _horseHurtAt = new Dictionary<int, int>();

	private int _lastNemesisProvokeTime = -100000;

	private int _nextPruneTime;

	private readonly Dictionary<int, int> _robbedAt = new Dictionary<int, int>();

	private const int RobTargetMemoryMs = 6000;

	private int _lastCash = int.MinValue;

	private int _robTarget;

	private int _robTargetTime;

	private int _paidPending;

	private int _paidAt;

	private int _cashRoseAt = -1000000;

	private int _lootedAt = -100000;

	private readonly StringBuilder _logBuf = new StringBuilder();

	private int _nextLogFlush;

	private static readonly string[] ChattyPrefixes = new string[16]
	{
		"TEXT ", "SOCIAL ped", "GAMESYS mind", "SEAT ", "SETTLE ", "PROMPT type", "INTERRO prompt target", "INTERRO no prompt", "REPUTATION map", "CONTROL ",
		"RELATION ", "UNEASE ", "NERVETICK ", "CASH ", "FEED ", "MISSION ped"
	};

	private int _lastPunchAt = -100000;

	private unsafe void PollInteractionEvents(Ped player)
	{
		DetectRobberyByCash(player);
		int num = SCRIPTS.GET_NUMBER_OF_EVENTS(0);
		if (num <= 0)
		{
			return;
		}
		ulong* ptr = stackalloc ulong[10];
		for (int i = 0; i < num; i++)
		{
			switch (SCRIPTS.GET_EVENT_AT_INDEX(0, i))
			{
			case 2787876538u:
				*ptr = 0uL;
				if (SCRIPTS.GET_EVENT_DATA(0, i, ptr, 1) && GunMelee.PlayerFiredRecently)
				{
					Downed.Hit((int)(*ptr), "bullet");
					if (_nemesisBodies.TryGetValue((int)(*ptr), out var value3))
					{
						MakeSure((int)(*ptr), value3);
					}
				}
				break;
			case 1387172233u:
			{
				for (int num4 = 0; num4 < 10; num4++)
				{
					ptr[num4] = 0uL;
				}
				if (SCRIPTS.GET_EVENT_DATA(0, i, ptr, 10))
				{
					int num5 = (int)(*ptr);
					int num6 = (int)ptr[2];
					GrudgeLog($"PROMPT type={num5} target={num6}");
					OnInterrogationGamePrompt(num6, num5);
					CampPrompt(num6, num5);
					switch (num5)
					{
					case 3:
						OnProvoked(num6, insult: false);
						break;
					case 4:
					case 17:
						OnProvoked(num6, insult: true);
						break;
					case 16:
						OnSocial(num6, GrudgeAction.Defuse);
						break;
					case 37:
						OnSocial(num6, GrudgeAction.Taunt);
						break;
					case 38:
						OnSocial(num6, GrudgeAction.EmoteGreet);
						break;
					case 21:
						OnSocial(num6, GrudgeAction.Intimidate);
						WitnessIntimidated(num6);
						break;
					case 8:
						OnSocial(num6, GrudgeAction.DrawOn);
						break;
					case 7:
					case 20:
						OnRobAttempt(num6);
						break;
					case 19:
						OnRobAttempt(num6, calm: true);
						break;
					}
				}
				break;
			}
			case 1376140891u:
				*ptr = (ptr[1] = (ptr[2] = 0uL));
				if (SCRIPTS.GET_EVENT_DATA(0, i, ptr, 3) && (int)(*ptr) == player.Handle)
				{
					_lootedAt = Game.GameTime;
					GrudgeLog($"LOOT looted={(int)ptr[1]} success={(int)(ptr[2] & 0xFFFFFFFFu)}");
					OnRobbed((int)ptr[1], "looted");
				}
				break;
			case 1794914733u:
				*ptr = (ptr[1] = (ptr[2] = 0uL));
				if (SCRIPTS.GET_EVENT_DATA(0, i, ptr, 3) && (int)ptr[1] == player.Handle)
				{
					OnRestrained((int)(*ptr), "hogtied");
				}
				break;
			case 3607700738u:
			{
				for (int num3 = 0; num3 < 4; num3++)
				{
					ptr[num3] = 0uL;
				}
				if (SCRIPTS.GET_EVENT_DATA(0, i, ptr, 4) && (int)(*ptr) == player.Handle)
				{
					OnRestrained((int)ptr[1], "carried off");
				}
				break;
			}
			case 2478244655u:
				*ptr = (ptr[1] = 0uL);
				if (SCRIPTS.GET_EVENT_DATA(0, i, ptr, 2))
				{
					Provocation provocation = RememberStranger((int)ptr[1]);
					if (provocation != null)
					{
						provocation.Escalated = true;
					}
				}
				break;
			case 402722103u:
			{
				for (int j = 0; j < 9; j++)
				{
					ptr[j] = 0uL;
				}
				if (!SCRIPTS.GET_EVENT_DATA(0, i, ptr, 9))
				{
					break;
				}
				if ((int)(*ptr) == player.Handle && _provoked.TryGetValue((int)ptr[1], out var value))
				{
					value.FoughtBack = true;
				}
				if ((int)ptr[1] != player.Handle || (int)ptr[2] == 0)
				{
					break;
				}
				int victim = (int)(*ptr);
				_hitByYouAt[victim] = Game.GameTime;
				Downed.Hit(victim, "damage event");
				NoteFactionHit(victim);
				if (_nemesisBodies.TryGetValue(victim, out var value2))
				{
					MakeSure(victim, value2);
				}
				uint num2 = WEAPON.GET_WEAPONTYPE_GROUP((uint)ptr[2]);
				if (num2 == 308416707)
				{
					break;
				}
				Ped ped = Entity.FromHandle(victim) as Ped;
				if (ped != null && ped.Exists() && ped.IsHuman && ped.IsDead && !LooksKnockedOut(ped) && !Kids.IsChild(ped) && _countedDeaths.Add(victim))
				{
					try
					{
						WitnessKill(ped);
					}
					catch (Exception ex)
					{
						GrudgeLog("WITNESS failed: " + ex.Message);
					}
				}
				bool flag = GunMelee.IsGunMeleeHit(player, victim, (uint)ptr[2]);
				bool flag2 = num2 == 2685387236u || flag;
				if (!flag2)
				{
					GunMelee.MarkWounded(victim);
				}
				if (OnHorseHurt(victim))
				{
					break;
				}
				if (_activeNemesisPed != null && victim == _activeNemesisPed.Handle)
				{
					if (flag2)
					{
						_activeLastPunchTime = Game.GameTime;
					}
					else
					{
						EscalateToGuns();
					}
				}
				else if (flag2 && !flag && Bumped(player))
				{
					OnPlayerBumped(victim);
				}
				else if (flag2)
				{
					OnInterrogationHit(victim);
					OnPlayerPunched(victim);
				}
				else if (num2 == 3566412244u)
				{
					OnInterrogationHit(victim, 1);
					Mark(victim, delegate(Provocation p)
					{
						if (!p.Slashed)
						{
							Grudge(victim, p, GrudgeAction.Slashed);
						}
						p.Slashed = true;
					});
				}
				else
				{
					OnInterrogationHit(victim, 2);
					OnPlayerWounded(victim);
				}
				break;
			}
			}
		}
	}

	private bool PanelButtonsHeld
	{
		get
		{
			if (_convo == null || _convo.Kind != ConvoKind.Interrogate)
			{
				return Game.GameTime - _panelClosedAt < 1500;
			}
			return true;
		}
	}

	private void OnProvoked(int target, bool insult)
	{
		if (target == 0)
		{
			return;
		}
		if (PanelButtonsHeld)
		{
			GrudgeLog(string.Format("PROVOKE {0} on ped {1} ignored - the questioning panel's buttons", insult ? "antagonize" : "greet", target));
			return;
		}
		if (!_named.ContainsKey(target) && ChildPed(target, out var p))
		{
			if (Config.NamingEnabled)
			{
				MeetKid(target, KidFor(p));
			}
			return;
		}
		if (insult)
		{
			NoteAntagonized(target);
		}
		Introduce(target, insult);
		try
		{
			if (insult)
			{
				GroupInsulted(target);
			}
			else
			{
				GroupGreet(target);
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("GROUP failed: " + ex.Message);
		}
		if ((!insult && GreetNemesis(target, GrudgeAction.Greet)) || TownProvoked(target, insult))
		{
			return;
		}
		if (_activeNemesisPed != null && ActiveRecord != null && target == _activeNemesisPed.Handle)
		{
			NemesisProvoked(insult);
			return;
		}
		Provocation provocation = RememberStranger(target);
		if (provocation != null)
		{
			Grudge(target, provocation, insult ? GrudgeAction.Insult : GrudgeAction.Greet);
			ReactByTemper(target, insult);
		}
	}

	private bool GreetNemesis(int target, GrudgeAction a)
	{
		if (_activeNemesisPed != null && target == _activeNemesisPed.Handle)
		{
			return false;
		}
		if (GreetHidden(target))
		{
			return true;
		}
		NemesisRecord nemesisRecord = RecordAt(target);
		if (nemesisRecord == null || !nemesisRecord.IsAlive)
		{
			return false;
		}
		TownMan townMan = TownManFor(target);
		if (townMan != null && townMan.Engaged)
		{
			return false;
		}
		if (Entity.FromHandle(target) is Ped ped && ped.Exists() && PED.IS_PED_IN_COMBAT(ped.Handle, Game.Player.Character.Handle))
		{
			return false;
		}
		_lastGreetDrop = 0;
		ApplyToRecord(nemesisRecord, a);
		if (a == GrudgeAction.Greet)
		{
			string text = ((_lastGreetDrop >= 5) ? "...Alright. Evenin'." : ((_lastGreetDrop >= 2) ? "Hm. Evenin'." : ((_lastGreetDrop >= 1) ? "Don't push it." : Taunts.GreetReply(_rng))));
			ShowSubtitle(nemesisRecord.Name + ": \"" + text + "\"");
		}
		return true;
	}

	private void OnSocial(int target, GrudgeAction a)
	{
		if (target == 0)
		{
			return;
		}
		Introduce(target, a == GrudgeAction.Taunt || a == GrudgeAction.Intimidate || a == GrudgeAction.DrawOn);
		if (a == GrudgeAction.EmoteGreet)
		{
			try
			{
				GroupGreet(target);
			}
			catch (Exception ex)
			{
				GrudgeLog("GROUP failed: " + ex.Message);
			}
		}
		NemesisRecord nemesisRecord = ((_activeNemesisPed != null && ActiveRecord != null && target == _activeNemesisPed.Handle) ? ActiveRecord : null);
		if (nemesisRecord != null)
		{
			ApplyToRecord(nemesisRecord, a);
		}
		else if ((a != GrudgeAction.EmoteGreet && a != GrudgeAction.Defuse) || !GreetNemesis(target, a))
		{
			Provocation provocation = RememberStranger(target);
			if (provocation != null && Game.GameTime - provocation.LastSocialAt >= 1500)
			{
				provocation.LastSocialAt = Game.GameTime;
				Grudge(target, provocation, a);
			}
		}
	}

	private void OnRobAttempt(int target, bool calm = false)
	{
		if (target == 0)
		{
			return;
		}
		NoDeed(target, "you stuck them up");
		Introduce(target, hostile: true);
		GrudgeAction a = (calm ? GrudgeAction.StickUpCalm : GrudgeAction.StickUp);
		if (_activeNemesisPed != null && ActiveRecord != null && target == _activeNemesisPed.Handle)
		{
			ApplyToRecord(ActiveRecord, a);
			return;
		}
		Provocation provocation = RememberStranger(target);
		if (provocation != null && Game.GameTime - provocation.LastRobAttempt >= 1500)
		{
			provocation.LastRobAttempt = Game.GameTime;
			Grudge(target, provocation, a);
		}
	}

	private void OnRobbed(int victim, string how)
	{
		OnInterrogationLoot(victim);
		NoDeed(victim, "you robbed them");
		if (Entity.FromHandle(victim) is Ped ped && ped.Exists() && !ped.IsDead)
		{
			Introduce(victim, hostile: true);
		}
		Provocation provocation = RememberStranger(victim);
		try
		{
			if (Entity.FromHandle(victim) is Ped ped2 && ped2.Exists() && !ped2.IsDead)
			{
				PurseRobbed(victim);
			}
		}
		catch
		{
		}
		GrudgeLog(string.Format("ROBBED ped={0} via {1} -> {2}", victim, how, (provocation == null) ? "ignored (dead, friend or not a person)" : ((provocation.Record != null) ? ("existing nemesis " + provocation.Record.Name) : "stranger, grudge pending")));
		NemesisRecord nemesisRecord;
		if (provocation == null)
		{
			nemesisRecord = RecordAt(victim);
			if (nemesisRecord == null || (_robbedAt.TryGetValue(victim, out var value) && Game.GameTime - value < 10000))
			{
				return;
			}
			_robbedAt[victim] = Game.GameTime;
		}
		else
		{
			if (Game.GameTime - provocation.LastRobbedTime < 10000)
			{
				return;
			}
			provocation.LastRobbedTime = Game.GameTime;
			bool flag = provocation.Record == null;
			if (flag)
			{
				Grudge(victim, provocation, GrudgeAction.Robbed);
			}
			else
			{
				CrimeSeen(victim, CrimeOf(GrudgeAction.Robbed));
			}
			provocation.Robbed = true;
			nemesisRecord = provocation.Record;
			if (nemesisRecord == null)
			{
				return;
			}
			if (flag && Roster.Contains(nemesisRecord))
			{
				nemesisRecord.TimesRobbed++;
				if (!provocation.RolledFor.HasValue || provocation.RolledFor.Value < GrudgeOrigin.Robbed)
				{
					provocation.RolledFor = GrudgeOrigin.Robbed;
				}
				WorldPart worldPart = PartFor(nemesisRecord, "ROBBED", "and swore to get it back");
				worldPart.Effects.Add($"hate {nemesisRecord.Hate} ({nemesisRecord.HateTier})");
				Vector3 position = Game.Player.Character.Position;
				ChronicleDeed("rob", "You robbed " + nemesisRecord.Name, "near " + GetZoneName(position), position, nemesisRecord.Gang, worldPart);
				SaveRoster();
				return;
			}
			if (!provocation.RolledFor.HasValue || provocation.RolledFor.Value < GrudgeOrigin.Robbed)
			{
				provocation.RolledFor = GrudgeOrigin.Robbed;
			}
		}
		int hate = nemesisRecord.Hate;
		HiddenSlip(nemesisRecord, "rob");
		nemesisRecord.TimesRobbed++;
		Remember(nemesisRecord, (Mind.F(nemesisRecord, "Greed") >= 60) ? "Anger" : "Shame", 45 + Math.Min(30, nemesisRecord.TimesRobbed * 8), (nemesisRecord.TimesRobbed > 1) ? $"you robbed him - {nemesisRecord.TimesRobbed} times now" : "you robbed him");
		int fear = nemesisRecord.Fear;
		int val = Balance.RobbedHateGain + ((nemesisRecord.Personality == Personality.Greedy) ? Balance.RobbedGreedyExtra : 0) + ((nemesisRecord.TraitsRolled && nemesisRecord.Wealth == Wealth.Poor) ? 5 : ((nemesisRecord.TraitsRolled && nemesisRecord.Wealth == Wealth.Rich) ? (-4) : 0)) + Math.Min(12, 4 * (nemesisRecord.TimesRobbed - 1));
		nemesisRecord.AddHate(Math.Max(3, val));
		nemesisRecord.AddFear(Balance.FearPerRobbery);
		nemesisRecord.AddYourNotoriety(5);
		Feed($"ROBBED {nemesisRecord.Name}: hate {Delta(hate, nemesisRecord.Hate)}  fear {Delta(fear, nemesisRecord.Fear)}  (robbed {nemesisRecord.TimesRobbed}x)", Theme.Warn, nemesisRecord);
		WorldPart worldPart2 = PartFor(nemesisRecord, "ROBBED", (nemesisRecord.TimesRobbed > 1) ? $"robbed {nemesisRecord.TimesRobbed} times now" : nemesisRecord.Title);
		worldPart2.Effects.Add($"hate {hate} -> {nemesisRecord.Hate}");
		worldPart2.Effects.Add($"fear {fear} -> {nemesisRecord.Fear}");
		Vector3 position2 = Game.Player.Character.Position;
		ChronicleDeed("rob", "You robbed " + nemesisRecord.Name, "near " + GetZoneName(position2), position2, nemesisRecord.Gang, worldPart2);
		SaveRoster();
		GrudgeNotice((Hate.TierOf(nemesisRecord.Hate) > Hate.TierOf(hate)) ? ("You robbed " + nemesisRecord.FullName + ". He's " + nemesisRecord.HateTier.ToString().ToLowerInvariant() + " now.") : ("You robbed " + nemesisRecord.FullName + " again. He won't forget it."));
	}

	internal void PayPlayer(int cents)
	{
		if (cents > 0)
		{
			try
			{
				MONEY._MONEY_INCREMENT_CASH_BALANCE(cents, MISC.GET_HASH_KEY("ADD_REASON_DEFAULT"));
			}
			catch
			{
				return;
			}
			_paidPending += cents;
			_paidAt = Game.GameTime;
		}
	}

	private unsafe void DetectRobberyByCash(Ped player)
	{
		int num = 0;
		if (((PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(Game.Player.Handle, &num) && num != 0) || (PLAYER.GET_PLAYER_TARGET_ENTITY(Game.Player.Handle, &num) && num != 0)) && Entity.FromHandle(num) is Ped { IsHuman: not false, IsPlayer: false, IsDead: false })
		{
			_robTarget = num;
			_robTargetTime = Game.GameTime;
			if (WEAPON.IS_PED_ARMED(player.Handle, 4))
			{
				_gunOnAt[num] = Game.GameTime;
			}
		}
		int num2 = MONEY._MONEY_GET_CASH_BALANCE();
		if (_lastCash == int.MinValue)
		{
			_lastCash = num2;
			return;
		}
		int num3 = num2 - _lastCash;
		_lastCash = num2;
		if (_paidPending > 0 && Game.GameTime - _paidAt > 5000)
		{
			_paidPending = 0;
		}
		if (num3 > 0 && (_paidPending <= 0 || num3 > _paidPending))
		{
			_cashRoseAt = Game.GameTime;
			ArrestOnPayout(player);
		}
		if (num3 > 0 && _paidPending > 0)
		{
			int num4 = Math.Min(num3, _paidPending);
			num3 -= num4;
			_paidPending -= num4;
			if (num3 <= 0)
			{
				GrudgeLog($"CASH +{num4} paid by the mod - not a robbery");
			}
		}
		if (num3 > 0)
		{
			Ped ped2 = ((_robTarget != 0 && Game.GameTime - _robTargetTime < 6000) ? (Entity.FromHandle(_robTarget) as Ped) : null);
			bool flag = ped2 != null && ped2.Exists() && !ped2.IsDead && !ped2.IsHogtied && !ENTITY.IS_ENTITY_ATTACHED(ped2.Handle) && ped2.Position.DistanceTo(player.Position) < 8f;
			string text = null;
			if (flag && (!PLAYER.IS_PLAYER_CONTROL_ON(Game.Player.Handle) || Game.GameTime - _quietEndedAt < 3000))
			{
				text = "the game was paying out (no control)";
			}
			else if (flag && Game.GameTime - _lootedAt < 2000)
			{
				text = "you were looting (a body / a satchel)";
			}
			else if (flag && GameEventPed(ped2))
			{
				text = WhoIs(ped2.Handle) + " is part of a game event (a bet / a reward)";
			}
			GrudgeLog(string.Format("CASH +{0} recentTarget={1} {2}{3}", num3, _robTarget, flag ? "near and alive" : "none near", (text != null) ? (" - not a robbery: " + text) : ""));
			if (flag && text == null)
			{
				OnRobbed(_robTarget, $"cash +{num3}");
			}
		}
	}

	internal void MenuLog(string line)
	{
		GrudgeLog(line);
	}

	private bool FleeingYou(Ped ped, Ped player)
	{
		float num = ped.Position.DistanceTo(player.Position);
		if (num < 15f)
		{
			return true;
		}
		if (num < 50f && (WEAPON.IS_PED_ARMED(player.Handle, 4) || WEAPON.IS_PED_ARMED(player.Handle, 1)))
		{
			return true;
		}
		if (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true))
		{
			return true;
		}
		if (_combatants.TryGetValue(ped.Handle, out var value))
		{
			return Game.GameTime - value < 10000;
		}
		return false;
	}

	internal void GrudgeLog(string line)
	{
		if (line == null)
		{
			return;
		}
		bool flag = line.StartsWith("FEED ASKED", StringComparison.Ordinal) || line.StartsWith("FEED INTEL", StringComparison.Ordinal) || line.StartsWith("FEED BRIBED", StringComparison.Ordinal) || line.StartsWith("FEED THREATENED", StringComparison.Ordinal) || line.StartsWith("FEED LET GO", StringComparison.Ordinal);
		if (Config != null && !Config.Verbose && !flag)
		{
			string[] chattyPrefixes = ChattyPrefixes;
			foreach (string value in chattyPrefixes)
			{
				if (line.StartsWith(value, StringComparison.Ordinal))
				{
					return;
				}
			}
		}
		line = UiDraw.TheFix(line);
		_logBuf.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ")
			.Append(line)
			.Append("\r\n");
		if (_logBuf.Length > 16000)
		{
			FlushGrudgeLog(force: false, later: true);
		}
	}

	private void FlushGrudgeLog(bool force = false, bool later = false)
	{
		if (_logBuf.Length == 0 || (!force && _logBuf.Length < 16000 && Environment.TickCount < _nextLogFlush))
		{
			return;
		}
		_nextLogFlush = Environment.TickCount + 1000;
		string path = Path.Combine(_saveDir, "grudges.log");
		string text = _logBuf.ToString();
		_logBuf.Clear();
		if (later)
		{
			SafeFile.AppendLater(path, text);
			return;
		}
		SafeFile.WaitWrites(500);
		try
		{
			SafeFile.Append(path, text);
		}
		catch
		{
		}
	}

	private bool Bumped(Ped player)
	{
		if (PED.IS_PED_IN_MELEE_COMBAT(player.Handle))
		{
			return false;
		}
		if (!PED.IS_PED_ON_MOUNT(player.Handle))
		{
			return ENTITY.GET_ENTITY_SPEED(player.Handle) > 2.6f;
		}
		return true;
	}

	private void OnPlayerBumped(int victim)
	{
		Provocation provocation = RememberStranger(victim);
		if (provocation != null && Game.GameTime - provocation.LastBumpTime >= 3000)
		{
			provocation.LastBumpTime = Game.GameTime;
			if (provocation.Record != null)
			{
				int hate = provocation.Record.Hate;
				provocation.Record.AddHate(3);
				CapScuffle(provocation.Record, hate);
				provocation.Record.Touch();
				SaveRoster();
				Feed("BUMPED " + provocation.Record.Name + " - knocked him over: hate " + Delta(hate, provocation.Record.Hate), Theme.Dim, provocation.Record);
			}
			else
			{
				Grudge(victim, provocation, GrudgeAction.Bumped);
			}
		}
	}

	private void OnPlayerPunched(int victim)
	{
		Provocation provocation = RememberStranger(victim);
		if (provocation != null && Game.GameTime - provocation.LastHitTime >= 250)
		{
			provocation.LastHitTime = Game.GameTime;
			_lastPunchAt = Game.GameTime;
			provocation.Hits++;
			if (provocation.Record != null)
			{
				Feed($"PUNCHED {provocation.Record.Name} ({provocation.Hits} hits)", null, provocation.Record);
				CrimeSeen(victim, CrimeOf(GrudgeAction.Punch));
			}
			else
			{
				Grudge(victim, provocation, GrudgeAction.Punch);
			}
		}
	}

	private void UpdateBumps(Ped player)
	{
		if (Game.GameTime < _nextBumpScan)
		{
			return;
		}
		_nextBumpScan = Game.GameTime + 200;
		bool flag = PED.IS_PED_ON_MOUNT(player.Handle);
		bool flag2 = flag || ENTITY.GET_ENTITY_SPEED(player.Handle) > 2.6f;
		float num = (flag ? 4f : 2.5f);
		Vector3 position = player.Position;
		foreach (var item in PedSnapshot(player))
		{
			var (ped, _) = item;
			if (ped == null || !ped.Exists() || ped.IsDead || ped.Handle == player.Handle || !ped.IsHuman)
			{
				continue;
			}
			int handle = ped.Handle;
			if (!ped.IsRagdoll)
			{
				_downSeen.Remove(handle);
			}
			else if (!_downSeen.Contains(handle))
			{
				_downSeen.Add(handle);
				if (flag2 && !((item.pos - position).Length() > num) && !PED.IS_PED_IN_MELEE_COMBAT(player.Handle) && !PED.IS_PED_IN_COMBAT(handle, player.Handle) && (!IsOurs(handle) || RecordAt(handle) != null) && !Kids.IsChild(ped) && !IsStoryPed(ped) && !GameOwned(ped) && !IsCompanion(handle))
				{
					GrudgeLog(string.Format("BUMP you knocked {0} over ({1}, {2:0.0} m)", NameOf(handle), flag ? "riding" : $"{ENTITY.GET_ENTITY_SPEED(player.Handle):0.0} m/s", (item.pos - position).Length()));
					OnPlayerBumped(handle);
				}
			}
		}
		if (_downSeen.Count > 200)
		{
			_downSeen.Clear();
		}
	}
}
