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
	private bool _lastEncFought = true;

	private readonly Dictionary<int, (Blip blip, int lastFight)> _enemyDots = new Dictionary<int, (Blip, int)>();

	private void StartStandoff(Ped player, Ped ped, NemesisRecord rec)
	{
		if (_encVehicle != null && _encVehicle.Exists())
		{
			foreach (Ped item in from p in new Ped[1] { ped }.Concat(_followers)
				where p.Exists() && PED.IS_PED_IN_ANY_VEHICLE(p.Handle, atGetIn: false)
				select p)
			{
				item.BlockPermanentEvents = false;
				TASK.TASK_LEAVE_ANY_VEHICLE(item.Handle, 0, 0);
			}
		}
		if (rec.Needs != null)
		{
			rec.Needs["Violence"] = Math.Max(0, Mind.N(rec, "Violence") - 20);
		}
		int num = ((_enc.Card != null && _enc.Card.StandoffSeconds > 0) ? _enc.Card.StandoffSeconds : ((rec.HateTier == HateTier.Murderous) ? Balance.StandoffMurderous : ((rec.HateTier == HateTier.Vengeful) ? Balance.StandoffVengeful : Balance.StandoffResentful)));
		_enc.Spotted = true;
		_squaredUp = false;
		_leadWalking = false;
		_enc.StandoffUntil = Game.GameTime + num * 1000;
		_enc.NextRetask = 0;
		if (!_enc.Spoke)
		{
			_enc.Spoke = true;
			ShowSubtitle(rec.Name + ": \"" + _enc.Taunt + "\"");
			Say(ped, _enc.Card?.Voice ?? "CHALLENGE_THREATEN", player);
			TauntEmote(ped, rec);
		}
		MusicStandoff();
		Crumbs.Native("Encounters blip add (V2)");
		if (_activeBlip == null)
		{
			_activeBlip = ped.AddBlip(BlipType.BLIP_STYLE_ENEMY_PASSIVE_THREAT);
		}
		if (rec.OwesYou)
		{
			rec.OwesYou = false;
			ShowSubtitle(rec.Name + ": \"I owed you my life. That's paid. Next time, I won't stop.\"");
			History(rec, "PAID HIS DEBT - backed down because he owed you his life", Theme.Good);
			Feed("STANDOFF " + rec.Name + ": he owed you - he backs down", Theme.Good, rec);
			BackDown(ped, rec, "he paid his debt");
		}
		else
		{
			GrudgeLog($"ENCOUNTER standoff with {rec.Name} ({rec.HateTier}): {num}s to talk him down, or not");
			Feed($"STANDOFF {rec.Name}: {num}s - greet to talk him down ({BackDownChance(rec)}%), antagonize / draw / hit = fight", Theme.Warn, rec);
		}
	}

	private int BackDownChance(NemesisRecord r)
	{
		int num = ((r.HateTier != HateTier.Murderous) ? ((r.HateTier == HateTier.Vengeful) ? 15 : 35) : 0);
		num += Mind.BackDownBonus(r);
		num += EffectiveFear(r) / 2;
		switch (r.Personality)
		{
		case Personality.Coward:
			num += 25;
			break;
		case Personality.Drifter:
			num += 15;
			break;
		case Personality.Cold:
			num += 5;
			break;
		case Personality.Hothead:
			num -= 20;
			break;
		case Personality.Proud:
			num -= 15;
			break;
		}
		if (r.TraitsRolled && r.Disposition == Disposition.Friendly)
		{
			num += 10;
		}
		if (r.TraitsRolled && r.Disposition == Disposition.Surly)
		{
			num -= 10;
		}
		if (r.TimesKilledPlayer > 0)
		{
			num -= 20;
		}
		return Math.Max(0, Math.Min(85, num));
	}

	private void UpdateStandoff(Ped player, Ped ped, NemesisRecord rec, float d, bool attacked)
	{
		bool flag = PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, ped.Handle) && WEAPON.IS_PED_ARMED(player.Handle, 6);
		if (!attacked && !flag && Game.GameTime > _enc.StandoffUntil && _enc.Card?.Id == "plan-funeral" && _funeral != null)
		{
			GrudgeLog("ENCOUNTER standoff with " + rec.Name + " at the funeral ends peacefully - back to the grave");
			_enc.StandoffUntil = -1;
			_enc.Spotted = false;
			_enc.Calmed = true;
			MusicStandoffOver();
			if (_activeBlip != null && _activeBlip.Exists())
			{
				_activeBlip.Delete();
				_activeBlip = null;
			}
			StandRoundCasket();
			return;
		}
		if (!attacked && !flag && Game.GameTime > _enc.StandoffUntil)
		{
			EncounterCard card = _enc.Card;
			if (card != null && card.EndsPeacefully)
			{
				GrudgeLog("ENCOUNTER standoff with " + rec.Name + " ends peacefully (" + _enc.Card.Id + ")");
				int hate = rec.Hate;
				rec.AddHate(_enc.Card.BackDownHate);
				History(rec, "SAID HIS PIECE and went (hate " + Delta(hate, rec.Hate) + ")", Theme.Good);
				BackDown(ped, rec, "he said his piece and went");
				return;
			}
		}
		if (attacked || flag || Game.GameTime > _enc.StandoffUntil)
		{
			GrudgeLog("ENCOUNTER standoff with " + rec.Name + " over: " + (attacked ? "you hit / shot" : (flag ? "you drew on him" : "time ran out")));
			_enc.StandoffUntil = -1;
			MusicStandoffBroke();
			EngageEncounter(player, blip: true);
		}
		else if (Game.GameTime >= _enc.NextRetask)
		{
			_enc.NextRetask = Game.GameTime + 3000;
			SquareUp(player, ped, d);
		}
	}

	private void BackDown(Ped ped, NemesisRecord rec, string how)
	{
		foreach (Ped item in from p in new Ped[1] { ped }.Concat(_followers)
			where p != null && p.Exists()
			select p)
		{
			TASK.TASK_WANDER_STANDARD(item.Handle, 10f, 10);
		}
		BandFromEncounter();
		_encEnd = ("BACKED OFF", how);
		if (_enc != null)
		{
			_enc.StandoffUntil = -1;
		}
		BackDownEmote(ped);
		MusicStandoffOver();
		SaveRoster();
		EndEncounter();
		ReleaseActive();
	}

	private void TalkEndedQuietly(Ped man, bool letGo)
	{
		if (_enc == null || _enc.Engaged || man == null || !man.Exists() || _activeNemesisPed == null || (man.Handle != _activeNemesisPed.Handle && !_followers.Any((Ped f) => f != null && f.Exists() && f.Handle == man.Handle)))
		{
			return;
		}
		string text = WhoIs(man.Handle);
		string text2 = ActiveRecord?.Name ?? "the encounter";
		if (_enc.StandoffUntil > 0 || _enc.Spotted)
		{
			MusicStandoffOver();
		}
		if (_activeBlip != null && _activeBlip.Exists())
		{
			_activeBlip.Delete();
			_activeBlip = null;
		}
		_enc.StandoffUntil = -1;
		_enc.EngageAt = 0;
		if (_enc.Kind == EncounterKind.Idle)
		{
			_enc.Spotted = false;
			_enc.Calmed = true;
			_enc.TalkedAt = Game.GameTime;
			GrudgeLog("ENCOUNTER " + text2 + ": you questioned " + text + " and left it there - his crew leave it too (only an attack starts it now)");
			return;
		}
		if (letGo)
		{
			GrudgeLog("ENCOUNTER " + text2 + ": you questioned " + text + " and let him go - no fight starts of it");
			return;
		}
		GrudgeLog("ENCOUNTER " + text2 + ": you questioned " + text + " and left it there - they go their way");
		foreach (Ped item in from p in new Ped[1] { _activeNemesisPed }.Concat(_followers)
			where p != null && p.Exists() && !p.IsDead && p.Handle != man.Handle
			select p)
		{
			TASK.TASK_WANDER_STANDARD(item.Handle, 10f, 10);
		}
		if (_nemesisGroup != 0)
		{
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _nemesisGroup, Game.Player.Character.RelationshipGroup);
		}
		BandFromEncounter();
		_encEnd = ("BACKED OFF", "you questioned " + text + " and left it there");
		SaveRoster();
		EndEncounter();
		ReleaseActive();
	}

	private bool StandoffGreeted(NemesisRecord rec)
	{
		if (_enc == null || _enc.StandoffUntil < 0 || _enc.GreetRolled || _activeNemesisPed == null)
		{
			return false;
		}
		_enc.GreetRolled = true;
		int num = BackDownChance(rec);
		int num2 = _rng.Next(100);
		bool flag = num2 < num;
		Feed(string.Format("STANDOFF {0}: you greeted him - {1}%, rolled {2} -> {3}", rec.Name, num, num2, flag ? "he backs down" : "no"), flag ? Theme.Good : Theme.Blood, rec);
		if (flag)
		{
			ShowSubtitle(rec.Name + ": \"" + ((rec.Personality == Personality.Coward) ? "...Fine. Fine! Another time." : "Not worth it. Not today.") + "\"");
			int hate = rec.Hate;
			rec.AddHate(_enc.Card?.BackDownHate ?? (-5));
			History(rec, "BACKED DOWN when you greeted him (hate " + Delta(hate, rec.Hate) + ")", Theme.Good);
			Remember(rec, (Mind.F(rec, "Pride") >= 60) ? "Shame" : "Joy", 35, "backed down from you");
			BackDown(_activeNemesisPed, rec, "you talked him down");
		}
		else
		{
			ShowSubtitle(rec.Name + ": \"" + ((rec.HateTier == HateTier.Resentful) ? "Don't you 'howdy' me." : "Too late for pleasantries.") + "\"");
			_enc.StandoffUntil = Math.Min(_enc.StandoffUntil, Game.GameTime + 1500);
		}
		SaveRoster();
		return true;
	}

	private void UpdateEncounter(Ped player)
	{
		if (_enc == null)
		{
			return;
		}
		if (_activeNemesisPed == null || ActiveRecord == null)
		{
			EndEncounter();
			return;
		}
		if (!_enc.Engaged && !_enc.Spotted && _enc.StandoffUntil < 0 && Game.GameTime - _enc.Start > 480000)
		{
			GrudgeLog(string.Format("ENCOUNTER {0} with {1} timed out ({2})", _enc.Kind, ActiveRecord.Name, (_enc.TalkedAt > 0) ? "you'd talked with them and moved on" : "never crossed paths"));
			EndEncounter();
			ReleaseActive();
			return;
		}
		Ped activeNemesisPed = _activeNemesisPed;
		if (!activeNemesisPed.Exists() || activeNemesisPed.IsDead)
		{
			return;
		}
		NemesisRecord activeRecord = ActiveRecord;
		if (_enc.EngageAt > 0 && !_enc.Engaged && (Game.GameTime >= _enc.EngageAt || PED.IS_PED_IN_COMBAT(activeNemesisPed.Handle, player.Handle) || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(activeNemesisPed.Handle, player.Handle, p2: true, p3: true)))
		{
			_enc.EngageAt = 0;
			GrudgeLog("ENCOUNTER " + activeRecord.Name + ": after the words, the fight");
			EngageEncounter(player, blip: true);
		}
		float num = activeNemesisPed.Position.DistanceTo(player.Position);
		GetOffForAFight(player);
		if (_enc.Surrendered)
		{
			if ((!(_interro?.Ped != null) || !_interro.Ped.Exists() || _interro.Ped.Handle != activeNemesisPed.Handle) && (Game.GameTime - _enc.SurrenderedAt > 20000 || num > 40f))
			{
				TASK.CLEAR_PED_TASKS(activeNemesisPed.Handle, p1: true, p2: true);
				Flee(activeNemesisPed.Handle, player.Handle, 200f, -1, 3f);
				bool flag = SparedNotSlipped(activeNemesisPed.Handle, activeNemesisPed.Position);
				Feed("SURRENDERED " + activeRecord.Name + ": " + (flag ? "you let him run" : "he slipped away while the fight was on"), Theme.Dim, activeRecord);
				GrudgeLog("ENCOUNTER " + activeRecord.Name + " surrendered and ran" + (flag ? "" : " - slipped away, not spared"));
				if (flag)
				{
					SparedHim(activeRecord, "surrendered and you let him run");
				}
				else
				{
					SlippedAway(activeRecord, "surrendered, then ran");
				}
				_encEnd = ("SURRENDERED", "threw up his hands, then ran");
				EndEncounter();
				ReleaseActive();
			}
			return;
		}
		switch (_enc.Kind)
		{
		case EncounterKind.Ahead:
			if (!_enc.Engaged && EncounterAttacked(activeNemesisPed, player, num))
			{
				GrudgeLog("ENCOUNTER ahead: " + activeRecord.Name + "'s men were attacked / heard your gun");
				EngageEncounter(player, blip: true);
			}
			else if (!_enc.Engaged && num < 45f)
			{
				ShowSubtitle(activeRecord.Name + ": \"" + _enc.Taunt + "\"");
				Say(activeNemesisPed, _enc.Card?.Voice ?? "CHALLENGE_THREATEN", player);
				EngageEncounter(player, blip: true);
			}
			break;
		case EncounterKind.Callout:
			if (_enc.Engaged)
			{
				break;
			}
			if (_enc.StandoffUntil <= 0 && EncounterAttacked(activeNemesisPed, player, num))
			{
				_enc.Spotted = true;
				GrudgeLog($"ENCOUNTER callout: {activeRecord.Name} was attacked / heard your gun ({num:0} m)");
				EngageEncounter(player, blip: true);
				break;
			}
			if (!_enc.Spotted)
			{
				if (Sees(activeNemesisPed, player, SightFor(player, 30f), out var _))
				{
					_enc.Spotted = true;
					_enc.Start = Game.GameTime;
					TASK.CLEAR_PED_TASKS(activeNemesisPed.Handle, p1: true, p2: true);
					TASK.TASK_GO_TO_ENTITY(activeNemesisPed.Handle, player.Handle, -1, 3f, 1.2f, 0f, 0);
					if (_activeBlip != null && _activeBlip.Exists())
					{
						_activeBlip.Delete();
					}
					Crumbs.Native("Encounters blip add (V2)");
					_activeBlip = activeNemesisPed.AddBlip(BlipType.BLIP_STYLE_ENEMY);
					GrudgeNotice(activeRecord.FullName + " spotted you - he's coming over.");
				}
				else if (num > 250f)
				{
					EndEncounter();
					ReleaseActive();
				}
				break;
			}
			if (!_enc.Spoke && num < 12f)
			{
				_enc.Spoke = true;
				if (_currentTown != null && RollWaitOutside(activeRecord, _currentTown, activeNemesisPed, player, callout: true))
				{
					_encEnd = null;
					EndEncounter();
					ReleaseActive();
					break;
				}
				ShowSubtitle(activeRecord.Name + ": \"" + _enc.Taunt + "\"");
				Say(activeNemesisPed, _enc.Card?.Voice ?? "CHALLENGE_THREATEN", player);
			}
			if (_enc.StandoffUntil > 0)
			{
				UpdateStandoff(player, activeNemesisPed, activeRecord, num, PED.IS_PED_IN_COMBAT(activeNemesisPed.Handle, player.Handle) || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(activeNemesisPed.Handle, player.Handle, p2: true, p3: true));
			}
			else if (num < 6f || Game.GameTime - _enc.Start > 25000)
			{
				EncounterCard card = _enc.Card;
				if (card != null && card.Standoff == false)
				{
					EngageEncounter(player, blip: true);
				}
				else
				{
					StartStandoff(player, activeNemesisPed, activeRecord);
				}
			}
			else if (Game.GameTime >= _enc.NextRetask)
			{
				_enc.NextRetask = Game.GameTime + 3000;
				TASK.TASK_GO_TO_ENTITY(activeNemesisPed.Handle, player.Handle, -1, 3f, 1.2f, 0f, 0);
			}
			break;
		case EncounterKind.Idle:
		{
			RideInTick();
			RideInSettleTick();
			ProcessionTick();
			if (_enc.Engaged || (_enc.StandoffUntil < 0 && Game.GameTime < _enc.NextSpotCheck))
			{
				break;
			}
			_enc.NextSpotCheck = Game.GameTime + 200;
			List<Ped> list = (from p in new Ped[1] { activeNemesisPed }.Concat(_followers)
				where p.Exists() && !p.IsDead
				select p).ToList();
			Ped questioned = _interro?.Ped;
			bool talking = questioned != null && list.Any((Ped p) => p.Handle == questioned.Handle);
			Sight s = SightFor(player, (_enc.Card != null && _enc.Card.SightRange > 0f) ? _enc.Card.SightRange : 38f);
			Ped ped = null;
			float num2 = 0f;
			foreach (Ped item in list)
			{
				if (Sees(item, player, s, out var d))
				{
					ped = item;
					num2 = d;
					break;
				}
			}
			bool flag2 = ped != null;
			if (flag2)
			{
				_enc.SpotNote = string.Format("{0} at {1:0} m, you {2} (sight {3:0} m)", (ped == activeNemesisPed) ? activeRecord.Name : "one of his men", num2, s.Stance, s.Range);
			}
			bool flag3 = list.Any((Ped p) => ((!talking || p.Handle != questioned.Handle) && PED.IS_PED_IN_COMBAT(p.Handle, player.Handle)) || ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(p.Handle, player.Handle, p2: true, p3: true));
			bool flag4 = FiredInAnger && num < 90f && WEAPON.IS_PED_ARMED(player.Handle, 4);
			if (_enc.Card?.Id == "plan-funeral")
			{
				flag4 = flag4 && num < 35f;
				bool flag5 = WEAPON._GET_PED_CURRENT_HELD_WEAPON(player.Handle) != 2725352035u;
				bool flag6 = list.Any((Ped m) => PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, m.Handle));
				if (flag2 && !flag6 && (_enc.Calmed || !flag5))
				{
					flag2 = false;
				}
			}
			if (flag2 && (talking || (_enc.TalkedAt > 0 && (Game.GameTime - _enc.TalkedAt < 10000 || !list.Any((Ped m) => PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, m.Handle))))))
			{
				flag2 = false;
			}
			if (_enc.StandoffUntil > 0)
			{
				UpdateStandoff(player, activeNemesisPed, activeRecord, num, flag3 || flag4);
			}
			else if (flag2 || flag3 || flag4)
			{
				_enc.Spotted = true;
				ShowSubtitle(activeRecord.Name + ": \"" + _enc.Taunt + "\"");
				Say(activeNemesisPed, _enc.Card?.Voice ?? "CHALLENGE_THREATEN", player);
				GrudgeLog(string.Format("ENCOUNTER idle: {0}'s men {1} ({2:0} m from him)", activeRecord.Name, flag3 ? "were attacked" : (flag4 ? "heard your gun" : ("saw you - " + _enc.SpotNote)), num));
				bool flag7 = _enc.Card?.Standoff ?? (!IsArmy(activeRecord));
				if (flag3 || flag4 || !flag7)
				{
					EngageEncounter(player, blip: true);
				}
				else
				{
					StartStandoff(player, activeNemesisPed, activeRecord);
				}
			}
			else if (num > (RidingIn ? 520f : 300f))
			{
				GrudgeLog((_enc.TalkedAt > 0) ? ("ENCOUNTER idle: you rode off after talking with " + activeRecord.Name + "'s men - " + ((_funeral != null) ? "left them to their funeral" : "left them be")) : ("ENCOUNTER idle: you never crossed paths with " + activeRecord.Name));
				EndEncounter();
				ReleaseActive();
			}
			break;
		}
		case EncounterKind.Sniper:
			if (!_enc.Spotted && (PED.IS_PED_SHOOTING(activeNemesisPed.Handle) || num < 35f))
			{
				_enc.Spotted = true;
				Crumbs.Native("Encounters blip add (V2)");
				if (_activeBlip == null)
				{
					_activeBlip = activeNemesisPed.AddBlip(BlipType.BLIP_STYLE_ENEMY);
				}
				ShowSubtitle(activeRecord.Name + ": \"" + _enc.Taunt + "\"");
				GrudgeNotice("Sniper! " + activeRecord.FullName + " is on the high ground.");
			}
			break;
		case EncounterKind.Holdup:
			break;
		}
	}

	private unsafe void EndEncounter()
	{
		_lastEncFought = _enc?.Engaged ?? false;
		if (_rideIn != null && _funeral != null)
		{
			if (_rideIn.Wagon != null)
			{
				_funeral.Wagon = _rideIn.Wagon;
			}
			if (_rideIn.Coffin != 0 && !_funeral.Props.Contains(_rideIn.Coffin))
			{
				_funeral.Props.Add(_rideIn.Coffin);
			}
		}
		_rideIn = null;
		_rideInSettle = null;
		if (_proc != null)
		{
			foreach (Ped item in EncounterMen())
			{
				item.BlockPermanentEvents = false;
			}
			_proc = null;
		}
		if (_enc != null && _enc.Kind == EncounterKind.Callout && _enc.Engaged)
		{
			NoteTownFightOver();
		}
		_dismountOrderedAt.Clear();
		foreach (Ped encHorse in _encHorses)
		{
			if (!(encHorse == null) && encHorse.Exists())
			{
				int handle = encHorse.Handle;
				ReleaseIfOurs(&handle);
			}
		}
		_encHorses.Clear();
		if (_encVehicle != null && _encVehicle.Exists())
		{
			int handle2 = _encVehicle.Handle;
			ReleaseIfOurs(&handle2);
		}
		_encVehicle = null;
		DropCampfire();
		_enc = null;
	}
}
