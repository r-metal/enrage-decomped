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
	internal readonly List<(int h, NemesisRecord rec)> _keptTied = new List<(int, NemesisRecord)>();

	internal long GameMinutesNow()
	{
		if (_gmFrame == Game.FrameCount)
		{
			return _gmCache;
		}
		_gmFrame = Game.FrameCount;
		return _gmCache = GameMinutesRaw();
	}

	private long GameMinutesRaw()
	{
		try
		{
			return GameDate.ToMinutes(DayOf(GameDayKey())) + CLOCK.GET_CLOCK_HOURS() * 60 + CLOCK.GET_CLOCK_MINUTES();
		}
		catch
		{
			return 0L;
		}
	}

	internal void StartRest(NemesisRecord rec)
	{
		if (rec != null && Balance.NewNemesisRestHours > 0)
		{
			rec.RestUntil = GameMinutesNow() + (long)Balance.NewNemesisRestHours * 60L;
		}
	}

	internal bool Resting(NemesisRecord rec)
	{
		if (rec != null && rec.RestUntil > 0)
		{
			return GameMinutesNow() < rec.RestUntil;
		}
		return false;
	}

	private unsafe void Wipe(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return;
		}
		int handle = p.Handle;
		uint value;
		bool flag = _modelOf.TryGetValue(handle, out value) && value != (uint)p.Model.Hash;
		if (flag || !BelongsToUs(handle) || IsCompanionReal(handle))
		{
			_worldNemeses.Remove(handle);
			_followerOf.Remove(handle);
			_knockedOutWorld.Remove(handle);
			GrudgeLog(string.Format("WIPE ped {0} left alone ({1})", handle, flag ? "his handle belongs to someone else now" : "not ours any more"));
			return;
		}
		NemesisRecord value2;
		NemesisRecord nemesisRecord = (_worldNemeses.TryGetValue(p.Handle, out value2) ? value2 : (((_activeNemesisPed != null && p.Handle == _activeNemesisPed.Handle) ? ActiveRecord : null) ?? RecordAt(p.Handle)));
		_worldNemeses.Remove(p.Handle);
		_followerOf.Remove(p.Handle);
		_knockedOutWorld.Remove(p.Handle);
		int handle2 = p.Handle;
		Ped character = Game.Player.Character;
		if ((ENTITY.IS_ENTITY_ATTACHED(handle2) && !PED.IS_PED_ON_MOUNT(handle2) && !PED.IS_PED_IN_ANY_VEHICLE(handle2, atGetIn: false)) || p.IsHogtied || (character != null && character.Exists() && PED.GET_MOUNT(character.Handle) == handle2) || (character != null && character.Exists() && ENTITY.IS_ENTITY_ATTACHED_TO_ENTITY(handle2, character.Handle)))
		{
			if (nemesisRecord != null)
			{
				_worldNemeses[handle2] = nemesisRecord;
				_keptTied.Add((handle2, nemesisRecord));
			}
			int num = handle2;
			ReleaseIfOurs(&handle2);
			GrudgeLog(string.Format("WIPE ped {0} kept (tied up with you){1} - handed to the game", num, (nemesisRecord != null) ? (" - " + nemesisRecord.Name) : ""));
		}
		else
		{
			ENTITY.SET_ENTITY_AS_MISSION_ENTITY(handle2, p1: true, p2: true);
			p.Delete();
		}
	}

	private void WipeWorldAfterJump()
	{
		_lastJumpAt = Game.GameTime;
		_quietUntil = Math.Max(_quietUntil, Game.GameTime + 5000);
		WipeWorldAfterRespawn(respawn: false);
		try
		{
			ForgetTownRepBlips();
		}
		catch
		{
		}
	}

	private void WipeWorldAfterRespawn()
	{
		WipeWorldAfterRespawn(respawn: true);
	}

	private unsafe void WipeWorldAfterRespawn(bool respawn, string why = null)
	{
		_keptTied.Clear();
		try
		{
			ClearEnemyDots();
		}
		catch
		{
		}
		int n = 0;
		ForgetTiedLeaders();
		Guard("wipe prompts", delegate
		{
			QuietPrompts(respawn ? "respawn" : "fast travel");
		});
		Guard("wipe companion", delegate
		{
			if (respawn)
			{
				WipeCompanion();
			}
		});
		Guard("wipe saloon", delegate
		{
			EndSaloonFight(wipe: true);
		});
		_followBooms.Clear();
		if (_rival != null)
		{
			W(_rival.Ped);
			foreach (Ped man in _rival.Men)
			{
				W(man);
			}
			if (_rival.Blip != null && _rival.Blip.Exists())
			{
				_rival.Blip.Delete();
			}
			_rival = null;
		}
		Guard("wipe gestures", delegate
		{
			CleanupGestures();
			_reactions.Clear();
		});
		_settling.Clear();
		_seating.Clear();
		_currentTown = null;
		_heldTownEntry = null;
		_bulletImpact.ForgetCripples();
		if (_roadAmbush != null)
		{
			GrudgeLog("TOWNCALM " + _roadAmbush.Rec.Name + " stops waiting outside " + _roadAmbush.Town + " (" + (respawn ? "respawn" : "fast travel") + ")");
			_roadAmbush = null;
		}
		if (_convo != null)
		{
			EndConvo("respawn");
		}
		if (_interro != null)
		{
			EndInterrogation(released: false);
		}
		foreach (Ped item in _followers.ToList())
		{
			W(item);
		}
		W(_activeNemesisPed);
		if (_activeBlip != null && _activeBlip.Exists())
		{
			_activeBlip.Delete();
		}
		_activeBlip = null;
		foreach (Blip followerBlip in _followerBlips)
		{
			if (followerBlip != null && followerBlip.Exists())
			{
				followerBlip.Delete();
			}
		}
		_followerBlips.Clear();
		_followers.Clear();
		foreach (Ped item2 in _encHorses.ToList())
		{
			W(item2);
		}
		_encHorses.Clear();
		_tackChecks.Clear();
		_tackPending.Clear();
		_tackModel.Clear();
		if (_encVehicle != null && _encVehicle.Exists() && BelongsToUs(_encVehicle.Handle) && PED.GET_VEHICLE_PED_IS_IN(Game.Player.Character.Handle, lastVehicle: false) != _encVehicle.Handle)
		{
			_encVehicle.Delete();
		}
		_encVehicle = null;
		DropCampfire(force: true);
		_enc = null;
		_activeNemesisPed = null;
		ActiveRecord = null;
		foreach (TownMan item3 in _townMen.ToList())
		{
			W(item3.Ped);
			if (item3.Blip != null && item3.Blip.Exists())
			{
				item3.Blip.Delete();
			}
		}
		foreach (Ped item4 in _regulars.ToList())
		{
			_acqOf.Remove(item4?.Handle ?? 0);
			W(item4);
		}
		_regulars.Clear();
		_townMen.Clear();
		foreach (LiveCamp item5 in _liveCamps.Values.ToList())
		{
			foreach (Ped item6 in item5.Peds.ToList())
			{
				W(item6);
			}
			DespawnCamp(item5, delete: true);
		}
		foreach (Raid item7 in _raids.ToList())
		{
			foreach (Ped item8 in item7.Men.ToList())
			{
				W(item8);
			}
			foreach (Ped item9 in item7.Horses.ToList())
			{
				W(item9);
			}
			EndRaid(item7);
		}
		_raidPending.Clear();
		_sidedAgainst.Clear();
		_sidedPeds.Clear();
		foreach (Ped item10 in _skirmish.ToList())
		{
			W(item10);
		}
		EndCheckpoint(Game.Player.Character, wipe: true);
		EndSupplyWagon(Game.Player.Character, wipe: true);
		if (_skirmish.Count > 0)
		{
			_skirmish.Clear();
			SkirmishNote = "";
		}
		Crumbs.Native("Respawn blip remove");
		if (_skirmishBlip != 0)
		{
			int skirmishBlip = _skirmishBlip;
			MAP.REMOVE_BLIP(&skirmishBlip);
			_skirmishBlip = 0;
		}
		_skirmishNamed.Clear();
		if (_warWagon != null && _warWagon.Exists() && BelongsToUs(_warWagon.Handle) && PED.GET_VEHICLE_PED_IS_IN(Game.Player.Character.Handle, lastVehicle: false) != _warWagon.Handle)
		{
			_warWagon.Delete();
		}
		_warWagon = null;
		foreach (Ped item11 in _warHorses.ToList())
		{
			W(item11);
		}
		_warHorses.Clear();
		_warKind = null;
		_warTriggered = false;
		_provoked.Clear();
		_named.Clear();
		_interroMemory.Clear();
		_acqOf.Clear();
		_witnessBatches.Clear();
		_witnessShadow.Clear();
		_unease.Clear();
		_walkedOff.Clear();
		_maskUnease.Clear();
		_maskOnly.Clear();
		_stoodUp.Clear();
		_likeGreetAt.Clear();
		_recognisedAt.Clear();
		_toldToFlee.Clear();
		_maskedSeen.Clear();
		_pendingExits.Clear();
		_uneaseFearAt.Clear();
		_reMet.Clear();
		_reRode.Clear();
		_reNearSince.Clear();
		_reDeeds.Clear();
		_deedKillsSeen.Clear();
		_mercyHandleNotes.Clear();
		int handle;
		foreach (string item12 in MercyNotes.Keys.Where((string k) => HandleKey(k, out handle)).ToList())
		{
			MercyNotes.Remove(item12);
		}
		foreach (Fighter value2 in _fighters.Values)
		{
			EndGiveGround(value2);
		}
		_gameEventFirstSeen.Clear();
		_fpOf.Clear();
		_recentWitnesses.Clear();
		_fighters.Clear();
		_runners.Clear();
		_cardBlocked.Clear();
		_cardHead.Clear();
		_cardFirstSeen.Clear();
		_playerHealthWas = -1;
		_lawPed = 0;
		_lawSeenPed = 0;
		foreach (Ped item13 in _hunters.ToList())
		{
			W(item13);
		}
		foreach (Ped item14 in _hunterHorses.ToList())
		{
			W(item14);
		}
		_hunters.Clear();
		_hunterHorses.Clear();
		foreach (Blip hunterBlip in _hunterBlips)
		{
			if (hunterBlip != null && hunterBlip.Exists())
			{
				hunterBlip.Delete();
			}
		}
		_hunterBlips.Clear();
		if (_sightingPed != null)
		{
			W(_sightingPed);
			ClearSighting(delete: true);
		}
		ClearPendingSighting();
		if (_general != null || _genEscort.Count > 0)
		{
			foreach (Ped item15 in _genEscort.ToList())
			{
				W(item15);
			}
			W(_general);
			DespawnGeneral();
		}
		DespawnAftermath();
		foreach (int item16 in _worldNemeses.Keys.ToList())
		{
			W(Entity.FromHandle(item16) as Ped);
		}
		_worldNemeses.Clear();
		foreach (var (num, value) in _keptTied)
		{
			if (ENTITY.DOES_ENTITY_EXIST(num))
			{
				_worldNemeses[num] = value;
			}
		}
		_nemesisBodies.Clear();
		_combatants.Clear();
		if (!respawn)
		{
			GrudgeLog(string.Format("{0} cleared {1} peds the mod had in the world behind you; quiet 5 s after the screen is back", why ?? "FASTTRAVEL", n));
			return;
		}
		_nextAmbushCheck = Game.GameTime + Balance.RespawnQuietSeconds * 1000;
		_nextTensionTry = Game.GameTime + Balance.RespawnQuietSeconds * 1000;
		ResetTension();
		GrudgeLog($"RESPAWN cleared {n} peds the mod had in the world; next encounter roll in {Balance.RespawnQuietSeconds}s");
		Feed($"RESPAWN: the world moved on - {n} of them gone (they'll be back another day)", Theme.Dim);
		void W(Ped p)
		{
			if (p != null && p.Exists())
			{
				Wipe(p);
				n++;
			}
		}
	}
}
