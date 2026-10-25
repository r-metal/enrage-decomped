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
	private const long TreasuryMoveAfter = 1440L;

	private const long CacheGuardDelay = 360L;

	private const double TreasuryShare = 0.4;

	private bool _brawlQuietNote;

	private bool _lassoQuietNote;

	private const int RegrabWindowMs = 2000;

	private const float InterrogateReach = 2.8f;

	private int _rbHeldSince = -1;

	private bool _rbFired;

	private int _convoClosedAt = -100000;

	private bool _upLatchAfterClose;

	private int _lockedFor;

	private static readonly eInputType[] DpadUpGameActions = new eInputType[26]
	{
		eInputType.Whistle,
		eInputType.WhistleHorseback,
		eInputType.ToggleHolster,
		eInputType.EmoteAction,
		eInputType.EmoteTaunt,
		eInputType.EmoteComm,
		eInputType.EmoteGreet,
		eInputType.EmoteTwirlGunHold,
		eInputType.TwirlPistol,
		eInputType.RevealHud,
		eInputType.OpenEmoteWheel,
		eInputType.SelectQuickselectUnarmed,
		eInputType.AimInAir,
		eInputType.OpenEmoteWheelHorse,
		eInputType.EmoteGroupLinkHorse,
		eInputType.HorseCommandFollow,
		eInputType.HorseCommandStay,
		eInputType.EmoteTwirlGunVarA,
		eInputType.EmoteTwirlGunVarB,
		eInputType.EmoteTwirlGunVarC,
		eInputType.EmoteTwirlGunVarD,
		eInputType.QuickSelectToggleShortcutItem,
		eInputType.WeaponInspectZoom,
		eInputType.InteractLockonCallAnimal,
		eInputType.HorseCommandFlee,
		eInputType.InteractLockonDetachHorse
	};

	private int _interroPromptSeenAt = -100000;

	private int _dpadOursAt = -100000;

	private bool _lockedAiming;

	private Interrogation _interro;

	private readonly Dictionary<int, Interrogation> _interroMemory = new Dictionary<int, Interrogation>();

	private Ped _interroCandidate;

	private int _nextInterroScan;

	private static readonly string[] Demands = new string[4] { "Where's your boss? Talk.", "Who do you ride for, and where are they holed up?", "I ain't gonna ask twice. Where's your outfit camped?", "Start talking, or start praying." };

	private static readonly string[] Pressure = new string[4] { "Last chance. Where is he?", "You think that hurt? I'm just getting started.", "Nobody's coming for you. Talk.", "I got all day. You don't." };

	private static readonly string[] Defiance = new string[6] { "Go to hell.", "I ain't no rat.", "The boss'll skin you alive for this.", "You'll have to kill me first.", "Spit on you, Van der Linde.", "I don't know nothin'!" };

	private readonly Dictionary<int, int> _sparedBound = new Dictionary<int, int>();

	private readonly Dictionary<int, Walker> _walkOffs = new Dictionary<int, Walker>();

	private Ped _aimStick;

	private int _aimStickUntil;

	private string _modeWant;

	private int _modeWantSince;

	private const float LockInterrogateRange = 12f;

	private int _pLock;

	private int _pLockTarget;

	private int _refusedFor;

	private string _refusedWhy;

	private string _pLockText;

	private int _pLockGroup;

	private int _pLockMadeAt;

	private eInputType _pLockCtrl;

	private const ulong PromptIsActive = 6083857518828614095uL;

	private const ulong ControlInUse = 2009046912299164313uL;

	private const eInputType KnownGood = eInputType.InteractOption1;

	private int _panelChoiceFor;

	private int _panelChoiceSince;

	private int _panelChoice;

	private int _pLockAntag;

	private int _pLockAntagGroup;

	private eInputType _ridingCtrl = eInputType.FrontendUp;

	private int _ridingUnseenSince = -1;

	private eInputType _ownShownOn;

	private int _ownShownFor;

	private int _dpadToggleLogs;

	private readonly Placement _held = new Placement
	{
		Name = "held"
	};

	private readonly Placement _locked = new Placement
	{
		Name = "lock-on"
	};

	private readonly Placement _near = new Placement
	{
		Name = "standing over"
	};

	private readonly Placement _social = new Placement
	{
		Name = "greet panel"
	};

	private Placement _lastPanelPlacement;

	private Ped _lastPromptPed;

	private string _lastPromptMode = "none";

	private int _lastPromptAt;

	private int _ownPanelFor;

	private void UpdateInterrogation(Ped player)
	{
		if (_sparedBound.Count > 0)
		{
			UpdateSparedBound(player);
		}
		if (_walkOffs.Count > 0)
		{
			UpdateWalkOffs(player);
		}
		if (_interroMemory.Count > 0)
		{
			foreach (KeyValuePair<int, Interrogation> item in _interroMemory.ToList())
			{
				Interrogation value = item.Value;
				if (!value.Ped.Exists() || DeadNow(value.Ped))
				{
					_interroMemory.Remove(item.Key);
				}
				else if (value != _interro && value.Ped.Position.DistanceTo(player.Position) < 40f)
				{
					WatchMenace(value, player);
				}
			}
		}
		if (_interro != null)
		{
			StepInterrogation(player);
			return;
		}
		if (Game.GameTime >= _nextInterroScan)
		{
			_nextInterroScan = Game.GameTime + 250;
			_interroCandidate = FindInterrogationTarget(player);
		}
		if (_interroCandidate != null && (!_interroCandidate.Exists() || DeadNow(_interroCandidate)))
		{
			_interroCandidate = null;
		}
		if (_menu.IsOpen || _bulletImpact.ViewerOpen)
		{
			HidePrompts();
			HideLockPrompt();
			return;
		}
		if (_upLatchAfterClose && (!XPad.Connected || !XPad.Held(1)) && Game.GameTime - _convoClosedAt > 1200)
		{
			_upLatchAfterClose = false;
		}
		if (_upLatchAfterClose)
		{
			HidePrompts();
			HideLockPrompt();
			_rbHeldSince = -1;
			_rbFired = false;
			return;
		}
		Ped ped = ((_interroCandidate != null && GrappledBy(_interroCandidate, player)) ? _interroCandidate : null);
		Ped ped2 = ((ped == null) ? LooseAtFeet(player) : null);
		if (ped2 == null && ped == null && !player.IsOnMount)
		{
			ped2 = TiedAtFeet(player);
		}
		Ped ped3 = ((ped2 == null) ? AimedInterrogable(player) : null);
		Ped ped4 = ped2 ?? ((ped == null && ped3 == null) ? StandingOver(player) : null);
		Ped ped5 = ((ped == null && ped3 == null && ped4 == null) ? FocusedInterrogable(player) : null);
		if (ped5 != null && _socialShownFor == ped5.Handle)
		{
			ped5 = null;
		}
		bool flag = false;
		if (ped == null && ped3 == null && ped4 == null && ped5 == null && _lastPromptPed != null && Game.GameTime - _lastPromptAt < ((_lastPromptMode == "aim") ? 1200 : 700) && _lastPromptPed.Exists() && !DeadNow(_lastPromptPed) && _lastPromptPed.Position.DistanceTo2D(player.Position) < ((_lastPromptMode == "aim") ? 14f : 4f))
		{
			if (_lastPromptMode == "aim")
			{
				ped3 = _lastPromptPed;
			}
			else if (_lastPromptMode == "over")
			{
				ped4 = _lastPromptPed;
			}
			else if (_lastPromptMode == "focus")
			{
				ped5 = _lastPromptPed;
			}
			flag = true;
		}
		Ped ped6 = ped3 ?? ped4;
		string text = ((ped3 != null) ? "aim" : ((ped4 != null) ? "over" : null));
		if (ped == null && ped6 != null && ped6 == _lastPromptPed && text != null && text != _lastPromptMode && (_lastPromptMode == "aim" || _lastPromptMode == "over"))
		{
			if (_modeWant != text)
			{
				_modeWant = text;
				_modeWantSince = Game.GameTime;
			}
			if (Game.GameTime - _modeWantSince < 800)
			{
				if (_lastPromptMode == "aim")
				{
					ped3 = ped6;
					ped4 = null;
				}
				else
				{
					ped4 = ped6;
					ped3 = null;
				}
			}
		}
		else
		{
			_modeWant = null;
		}
		bool num = Game.GameTime - _lastPunchAt < 4000 || PED.IS_PED_IN_MELEE_COMBAT(player.Handle);
		if (PED._GET_LASSO_TARGET(player.Handle) == 0)
		{
			Ped ped7 = ped3 ?? ped4 ?? ped5 ?? ped;
			if ((object)ped7 == null || !PED.IS_PED_LASSOED(ped7.Handle))
			{
				_lassoQuietNote = false;
				goto IL_0529;
			}
		}
		if (!_lassoQuietNote)
		{
			_lassoQuietNote = true;
			GrudgeLog("INTERRO someone's on your rope - the game's own panel only");
		}
		ped3 = null;
		ped4 = null;
		ped5 = null;
		ped = null;
		goto IL_0529;
		IL_0529:
		if (num)
		{
			if (ped3 != null || ped4 != null || ped5 != null)
			{
				_brawlQuietNote = true;
			}
			ped3 = null;
			ped4 = null;
			ped5 = null;
			if (ped != null && PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle))
			{
				ped = null;
			}
		}
		else if (_brawlQuietNote)
		{
			_brawlQuietNote = false;
			GrudgeLog("INTERRO the fist fight's over - prompts back");
		}
		Ped ped8 = ped ?? ped3 ?? ped4 ?? ped5;
		string text2 = ((ped != null) ? "grapple" : ((ped3 != null) ? "aim" : ((ped4 != null) ? "over" : ((ped5 != null) ? "focus" : "none"))));
		if (ped8 != null && !flag)
		{
			if (ped8 != _lastPromptPed || text2 != _lastPromptMode)
			{
				GrudgeLog(string.Format("INTERRO prompt target: ped {0} ({1}, {2:0.0} m{3}{4}{5})", ped8.Handle, text2, ped8.Position.DistanceTo(player.Position), ped8.IsHogtied ? ", hogtied" : "", player.IsOnMount ? ", you're mounted" : "", ped8.IsOnMount ? ", he's mounted" : ""));
			}
			_lastPromptPed = ped8;
			_lastPromptMode = text2;
			_lastPromptAt = Game.GameTime;
		}
		else if (ped8 == null && _lastPromptPed != null && Game.GameTime - _lastPromptAt >= ((_lastPromptMode == "aim") ? 1200 : 700))
		{
			GrudgeLog($"INTERRO prompt target: none (was ped {_lastPromptPed.Handle}, {_lastPromptMode})");
			_lastPromptPed = null;
			_lastPromptMode = "none";
		}
		if (ped8 != null)
		{
			_dpadOursAt = Game.GameTime;
		}
		if (ped8 != null)
		{
			bool flag2 = Game.GameTime - _interroPromptSeenAt < 250;
			if (XPad.Connected && XPad.Pressed(1) && flag2)
			{
				_rbHeldSince = Game.GameTime;
				_rbFired = false;
			}
			if (XPad.Connected && XPad.Held(1) && flag2 && _rbHeldSince >= 0)
			{
				if (Game.GameTime - _rbHeldSince > 400 && !_rbFired)
				{
					_rbFired = true;
					_interroCandidate = ped8;
					GrudgeLog($"INTERRO D-pad Up held -> interrogate ped={ped8.Handle}");
					OnInterrogateKey();
					return;
				}
			}
			else
			{
				_rbHeldSince = -1;
				_rbFired = false;
			}
		}
		else
		{
			_rbHeldSince = -1;
			_rbFired = false;
		}
		if (_interroCandidate != null && GrappledBy(_interroCandidate, player))
		{
			if (ShowHeldPrompt(_interroCandidate, player, enabled: true))
			{
				OnInterrogateKey();
			}
			return;
		}
		if (ped8 != null && player.IsOnMount && ped == null && (NoGamePanel(ped8, player) || (ped8.IsHogtied && !LockedOnto(ped8))))
		{
			HideLockPrompt();
			ShowOwnPanel(ped8, player);
			return;
		}
		if (ped4 != null || ped5 != null)
		{
			Ped ped9 = ped4 ?? ped5;
			Placement placement = ((ped4 != null) ? _near : _social);
			if (_lockedFor != ped9.Handle || _lockedAiming || _lastPanelPlacement != placement)
			{
				_lockedFor = ped9.Handle;
				_lockedAiming = false;
				_lastPanelPlacement = placement;
				placement.NotShownSince = -1;
			}
			if (placement == _near && DownMan(ped9, player))
			{
				DownManPrompt(ped9, player, placement);
				return;
			}
			HidePrompts();
			PlacePrompt(placement, ped9, HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(ped9.Handle), enabled: true);
			if (Fired(_pLock))
			{
				_interroCandidate = ped9;
				OnInterrogateKey();
			}
			return;
		}
		Ped ped10 = ped3;
		if (ped10 != null)
		{
			bool flag3 = PLAYER.IS_PLAYER_FREE_AIMING(Game.Player.Handle);
			if (_lockedFor != ped10.Handle || flag3 != _lockedAiming)
			{
				_lockedFor = ped10.Handle;
				_lockedAiming = flag3;
				_locked.NotShownSince = -1;
				_ownPanelFor = 0;
			}
			Placement locked = _locked;
			if (DownMan(ped10, player))
			{
				if (flag3 && !LockedOnto(ped10))
				{
					HideLockPrompt();
					ShowOwnPanel(ped10, player, force: true);
				}
				else
				{
					DownManPrompt(ped10, player, locked);
				}
				return;
			}
			if (_ownPanelFor == ped10.Handle)
			{
				HideLockPrompt();
				ShowOwnPanel(ped10, player, force: true);
				return;
			}
			HidePrompts();
			int num2 = HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(ped10.Handle);
			PlacePrompt(locked, ped10, num2, enabled: true);
			if (ped10.IsHogtied)
			{
				ShowLockAntag(num2);
				if (Fired(_pLockAntag))
				{
					AntagonizeDowned(ped10, player);
				}
			}
			else
			{
				HideLockAntag();
			}
			if (flag3 && !LockedOnto(ped10) && locked.NotShownSince >= 0 && Game.GameTime - locked.NotShownSince > 300)
			{
				_ownPanelFor = ped10.Handle;
				GrudgeLog($"INTERRO free aim: no panel for ped {ped10.Handle} - our own panel (D-pad Up)");
			}
			if (Fired(_pLock))
			{
				_interroCandidate = ped10;
				OnInterrogateKey();
			}
		}
		else
		{
			HideLockPrompt();
			HidePrompts();
		}
	}

	private Ped AimedInterrogable(Ped player)
	{
		int handle = Game.Player.Handle;
		Ped ped = LockedInterrogable(player);
		int num = ((ped != null && ped.IsOnMount) ? PED.GET_MOUNT(ped.Handle) : 0);
		bool flag = ped != null && (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(handle, ped.Handle) || PLAYER.IS_PLAYER_TARGETTING_ENTITY(handle, ped.Handle, p2: false) || (num != 0 && (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(handle, num) || PLAYER.IS_PLAYER_TARGETTING_ENTITY(handle, num, p2: false))));
		if (ped != null && flag)
		{
			_aimStick = ped;
			_aimStickUntil = Game.GameTime + 1200;
			return ped;
		}
		if (ped == null)
		{
			NoteAimRefusal(player);
		}
		if (_aimStick != null && PLAYER.IS_PLAYER_FREE_AIMING(handle) && Interrogable(_aimStick, player, 14f))
		{
			if (NearCrosshair(_aimStick, 0.13f))
			{
				_aimStickUntil = Game.GameTime + 1200;
				return _aimStick;
			}
			if (Game.GameTime < _aimStickUntil)
			{
				return _aimStick;
			}
		}
		_aimStick = null;
		return null;
	}

	private Ped StandingOver(Ped player)
	{
		if (PLAYER.IS_PLAYER_FREE_AIMING(Game.Player.Handle))
		{
			return null;
		}
		Ped ped = LockedInterrogable(player) ?? _interroCandidate;
		if (ped == null || !Interrogable(ped, player, player.IsOnMount ? 4.5f : 3f))
		{
			return null;
		}
		if (ENTITY.IS_ENTITY_ATTACHED(ped.Handle))
		{
			return null;
		}
		if (!ped.IsHogtied && !AtMercy(ped, player))
		{
			return null;
		}
		return ped;
	}

	private Ped TiedAtFeet(Ped player)
	{
		Ped interroCandidate = _interroCandidate;
		if (interroCandidate == null || !interroCandidate.IsHogtied || ENTITY.IS_ENTITY_ATTACHED(interroCandidate.Handle) || DeadNow(interroCandidate))
		{
			return null;
		}
		if (!(interroCandidate.Position.DistanceTo2D(player.Position) <= 3f) || !Interrogable(interroCandidate, player, 3.5f))
		{
			return null;
		}
		return interroCandidate;
	}

	private Ped LooseAtFeet(Ped player)
	{
		Ped interroCandidate = _interroCandidate;
		if (interroCandidate == null || interroCandidate.IsHogtied || !Interrogable(interroCandidate, player, 4f) || ENTITY.IS_ENTITY_ATTACHED(interroCandidate.Handle))
		{
			return null;
		}
		if (!AtMercy(interroCandidate, player) || GrappledBy(interroCandidate, player))
		{
			return null;
		}
		return interroCandidate;
	}

	private unsafe Ped FocusedInterrogable(Ped player)
	{
		if (PLAYER.IS_PLAYER_FREE_AIMING(Game.Player.Handle))
		{
			return null;
		}
		int num = 0;
		if (!PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(Game.Player.Handle, &num, p2: false, p3: false) || num == 0)
		{
			return null;
		}
		Ped ped = Entity.FromHandle(num) as Ped;
		if (ped == null || !Interrogable(ped, player, 12f) || ENTITY.IS_ENTITY_ATTACHED(ped.Handle))
		{
			return null;
		}
		return ped;
	}

	private static bool NearCrosshair(Ped p, float r)
	{
		float[] array = new float[3] { 0.3f, 0f, -0.6f };
		foreach (float z in array)
		{
			if (Projection.ToScreen(p.Position + new Vector3(0f, 0f, z), out var x, out var y))
			{
				float num = (x - 0.5f) * UiDraw.ScreenAspect;
				float num2 = y - 0.5f;
				if (num * num + num2 * num2 < r * r)
				{
					return true;
				}
			}
		}
		return false;
	}

	private unsafe void NoteAimRefusal(Ped player)
	{
		int num = 0;
		int handle = Game.Player.Handle;
		if ((!PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(handle, &num) || num == 0) && (!PLAYER.GET_PLAYER_TARGET_ENTITY(handle, &num) || num == 0))
		{
			return;
		}
		Ped ped = Entity.FromHandle(num) as Ped;
		if (ped == null || !ped.Exists())
		{
			return;
		}
		if (!ped.IsHuman)
		{
			int num2 = PED._GET_RIDER_OF_MOUNT(num, p1: true);
			ped = ((num2 != 0) ? (Entity.FromHandle(num2) as Ped) : null);
			if (ped == null)
			{
				return;
			}
		}
		string text = (DeadNow(ped) ? "dead" : ((!ped.IsHuman) ? "not a person" : ((ped.Position.DistanceTo(player.Position) > 12f) ? $"too far ({ped.Position.DistanceTo(player.Position):0} m)" : (MISC.GET_MISSION_FLAG() ? "a mission is on" : (IsStoryPed(ped) ? "a story character" : (GameOwned(ped) ? "a mission's man" : (GameEventPed(ped) ? "part of a game event" : ((RecordFor(ped)?.Gang == null && OutfitOfPed(ped) == null) ? "not in an outfit" : ((LooksKnockedOut(ped) && !ped.IsHogtied && !WoundedDown(ped)) ? "out cold" : ((PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle) && !AtMercy(ped, player)) ? "he's fighting you (not at your mercy)" : (Interrogable(ped, player, 12f) ? "the game's interaction / lock-on target is someone else (he'd qualify)" : "no check matched")))))))))));
		string text2 = (text.StartsWith("too far") ? "too far" : text);
		if (ped.Handle != _refusedFor || !(text2 == _refusedWhy))
		{
			_refusedFor = ped.Handle;
			_refusedWhy = text2;
			GrudgeLog(string.Format("INTERRO no prompt for ped {0} ({1}{2}{3}): {4}", ped.Handle, Models.Name(ped), player.IsOnMount ? ", you're mounted" : "", ped.IsOnMount ? ", he's mounted" : "", text));
		}
	}

	private unsafe Ped LockedInterrogable(Ped player)
	{
		int handle = Game.Player.Handle;
		int num = (player.IsOnMount ? PED.GET_MOUNT(player.Handle) : 0);
		for (int i = 0; i < 3; i++)
		{
			int num2 = 0;
			if (i switch
			{
				1 => PLAYER.GET_PLAYER_TARGET_ENTITY(handle, &num2), 
				0 => PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(handle, &num2, p2: false, p3: false), 
				_ => PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(handle, &num2), 
			} && num2 != 0 && num2 != num && num2 != player.Handle)
			{
				Ped ped = Entity.FromHandle(num2) as Ped;
				if (ped != null && ped.Exists() && !ped.IsHuman)
				{
					int num3 = PED._GET_RIDER_OF_MOUNT(num2, p1: true);
					ped = ((num3 != 0 && num3 != player.Handle) ? (Entity.FromHandle(num3) as Ped) : null);
				}
				if (Interrogable(ped, player, 12f))
				{
					return ped;
				}
			}
		}
		return null;
	}

	private Gang OutfitOfPed(Ped p)
	{
		object obj;
		if (!(p == null))
		{
			obj = Gangs.ForModel((uint)p.Model.Hash);
			if (obj == null)
			{
				if (!IsOurs(p.Handle))
				{
					if (!RallyManFree(p))
					{
						return null;
					}
					return Gangs.Klan;
				}
				return OutfitOfModel((uint)p.Model.Hash);
			}
		}
		else
		{
			obj = null;
		}
		return (Gang)obj;
	}

	private bool Interrogable(Ped p, Ped player, float range)
	{
		if (p == null || !p.Exists() || p.IsPlayer || !p.IsHuman || DeadNow(p))
		{
			return false;
		}
		Vector3 position = p.Position;
		Vector3 position2 = player.Position;
		if (position.DistanceTo2D(position2) > range || Math.Abs(position.Z - position2.Z) > Math.Max(2.5f, range * 0.5f))
		{
			return false;
		}
		if (MISC.GET_MISSION_FLAG() || IsStoryPed(p) || GameOwned(p) || (GameEventPed(p) && !RallyManFree(p)))
		{
			return false;
		}
		if (RecordFor(p)?.Gang == null && OutfitOfPed(p) == null)
		{
			return false;
		}
		if (LooksKnockedOut(p) && !p.IsHogtied && !WoundedDown(p))
		{
			return false;
		}
		Fighter value;
		bool flag = (_enc != null && _enc.Surrendered && _activeNemesisPed != null && _activeNemesisPed.Handle == p.Handle) || (_fighters.TryGetValue(p.Handle, out value) && !value.Done && value.State == "surrendered");
		if (PED.IS_PED_IN_COMBAT(p.Handle, player.Handle) && !AtMercy(p, player) && !flag)
		{
			return false;
		}
		return true;
	}

	private unsafe static bool PlayerTargets(Ped p)
	{
		int num = 0;
		if (PLAYER.GET_PLAYER_TARGET_ENTITY(Game.Player.Handle, &num))
		{
			return num == p.Handle;
		}
		return false;
	}

	private static bool GrappledBy(Ped p, Ped player)
	{
		if (p != null && p.Exists() && PED.GET_PED_IS_BEING_GRAPPLED(p.Handle))
		{
			return PED._GET_PED_GRAPPLER(p.Handle) == player.Handle;
		}
		return false;
	}

	private void ShowLockPrompt(Ped target, string text, bool enabled, int group, eInputType ctrl)
	{
		if (_pLockTarget != target.Handle || _pLock == 0 || _pLockText != text || _pLockGroup != group || _pLockCtrl != ctrl)
		{
			HideLockPrompt();
			Crumbs.Native("Interrogation prompt delete");
			if (_pLock != 0)
			{
				HUD._UI_PROMPT_DELETE(_pLock);
			}
			_pLock = MakePrompt(ctrl, text, hold: true, group);
			_pLockTarget = target.Handle;
			_pLockText = text;
			_pLockGroup = group;
			_pLockCtrl = ctrl;
			_pLockMadeAt = Game.GameTime;
		}
		if (!PromptValid(_pLock))
		{
			_pLock = 0;
			_pLockTarget = 0;
		}
		else
		{
			HUD._UI_PROMPT_SET_VISIBLE(_pLock, toggle: true);
			HUD._UI_PROMPT_SET_ENABLED(_pLock, enabled);
		}
	}

	private bool DownMan(Ped p, Ped player)
	{
		if (!p.IsHogtied)
		{
			if (!_fighters.TryGetValue(p.Handle, out var value) || value.Done || !(value.State == "surrendered"))
			{
				return AtMercy(p, player);
			}
			return true;
		}
		return false;
	}

	private void DownManPrompt(Ped who, Ped player, Placement pl)
	{
		if (_panelChoiceFor != who.Handle)
		{
			_panelChoiceFor = who.Handle;
			_panelChoiceSince = Game.GameTime;
			_panelChoice = 0;
		}
		if (_panelChoice == 2)
		{
			HideLockPrompt();
			ShowOwnPanel(who, player, force: true);
			return;
		}
		HidePrompts();
		int num = HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(who.Handle);
		int num2 = PlacePrompt(pl, who, num, enabled: true);
		ShowLockAntag(num);
		if (_panelChoice == 0)
		{
			if (num2 != 0 && Function.Call<bool>(6083857518828614095uL, new InputArgument[1] { num2 }))
			{
				_panelChoice = 1;
				GrudgeLog($"INTERRO ped {who.Handle} down: the game shows a panel for him - Interrogate + Antagonize go in it");
			}
			else if (Game.GameTime - _panelChoiceSince > 1000)
			{
				_panelChoice = 2;
				GrudgeLog($"INTERRO ped {who.Handle} down: no game panel for him in 1 s - our own panel (Interrogate + Antagonize)");
				HideLockPrompt();
				ShowOwnPanel(who, player, force: true);
				return;
			}
		}
		if (Fired(_pLock))
		{
			_interroCandidate = who;
			OnInterrogateKey();
		}
		else if (Fired(_pLockAntag))
		{
			AntagonizeDowned(who, player);
		}
	}

	private void ShowLockAntag(int group)
	{
		if (!PromptValid(_pLockAntag) || _pLockAntagGroup != group)
		{
			if (PromptValid(_pLockAntag))
			{
				Crumbs.Native("Interrogation prompt delete");
				HUD._UI_PROMPT_DELETE(_pLockAntag);
			}
			_pLockAntag = MakePrompt(eInputType.InteractLockonNeg, "Antagonize", hold: false, group);
			_pLockAntagGroup = group;
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pLockAntag, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pLockAntag, toggle: true);
	}

	private void HideLockAntag()
	{
		if (PromptValid(_pLockAntag))
		{
			HUD._UI_PROMPT_SET_VISIBLE(_pLockAntag, toggle: false);
			HUD._UI_PROMPT_SET_ENABLED(_pLockAntag, toggle: false);
		}
	}

	private bool NoGamePanel(Ped p, Ped player)
	{
		if (!LockedOnto(p))
		{
			if (!_fighters.TryGetValue(p.Handle, out var value) || value.Done || !(value.State == "surrendered"))
			{
				if (!p.IsHogtied)
				{
					return AtMercy(p, player);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private unsafe bool LockedOnto(Ped p)
	{
		int handle = Game.Player.Handle;
		if (PLAYER.IS_PLAYER_TARGETTING_ENTITY(handle, p.Handle, p2: false))
		{
			return true;
		}
		int num = 0;
		if (PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(handle, &num, p2: false, p3: false))
		{
			return num == p.Handle;
		}
		return false;
	}

	private void ShowOwnPanel(Ped who, Ped player, bool force = false)
	{
		if (!force && LockedOnto(who) && _panelChoiceFor == who.Handle && _panelChoice == 2)
		{
			force = true;
		}
		if (!force && LockedOnto(who))
		{
			HidePrompts();
			PlacePrompt(_locked, who, HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(who.Handle), enabled: true);
			if (Fired(_pLock))
			{
				_interroCandidate = who;
				OnInterrogateKey();
			}
			return;
		}
		HideLockPrompt();
		eInputType eInputType = (player.IsOnMount ? _ridingCtrl : eInputType.InteractOption1);
		EnsurePrompts(eInputType);
		ShowPrompts(PromptTitle(RecordFor(who), who), _pInterrogate, _pAntag);
		if (Fired(_pAntag))
		{
			AntagonizeDowned(who, player);
		}
		if (PromptValid(_pInterrogate) && Function.Call<bool>(6083857518828614095uL, new InputArgument[1] { _pInterrogate }))
		{
			_interroPromptSeenAt = Game.GameTime;
			_ridingUnseenSince = -1;
			if (_ownShownOn != eInputType || _ownShownFor != who.Handle)
			{
				_ownShownOn = eInputType;
				_ownShownFor = who.Handle;
				GrudgeLog(string.Format("INTERRO own panel showing for ped {0} on {1}{2}", who.Handle, eInputType, player.IsOnMount ? " (mounted)" : ""));
			}
		}
		else if (player.IsOnMount)
		{
			if (_ridingUnseenSince < 0)
			{
				_ridingUnseenSince = Game.GameTime;
			}
			else if (Game.GameTime - _ridingUnseenSince > 2000)
			{
				eInputType ridingCtrl = _ridingCtrl;
				_ridingCtrl = ((_ridingCtrl == eInputType.FrontendUp) ? eInputType.InteractOption1 : eInputType.FrontendUp);
				_ridingUnseenSince = -1;
				if (_dpadToggleLogs++ < 20)
				{
					GrudgeLog($"INTERRO mounted: {ridingCtrl} not showing for 2 s - trying {_ridingCtrl} (both D-pad Up)");
				}
			}
		}
		if (Fired(_pInterrogate))
		{
			_interroCandidate = who;
			OnInterrogateKey();
		}
	}

	private void AntagonizeDowned(Ped who, Ped player)
	{
		if (Game.GameTime - _lastDownAntagAt >= 2000)
		{
			_lastDownAntagAt = Game.GameTime;
			GrudgeLog($"INTERRO antagonized ped {who.Handle} on the ground");
			Say(player, "INSULT_HOGTIED_PED_CONV_PART1", who);
			OnProvoked(who.Handle, insult: true);
		}
	}

	private int PlacePrompt(Placement pl, Ped target, int group, bool enabled)
	{
		ShowLockPrompt(target, "Interrogate", enabled, group, Game.Player.Character.IsOnMount ? eInputType.FrontendUp : eInputType.InteractOption1);
		if (_pLock == 0)
		{
			return 0;
		}
		bool flag = Function.Call<bool>(6083857518828614095uL, new InputArgument[1] { _pLock });
		if (flag)
		{
			_interroPromptSeenAt = Game.GameTime;
			pl.NotShownSince = -1;
		}
		else if (pl.NotShownSince < 0)
		{
			pl.NotShownSince = Game.GameTime;
		}
		if (pl.LoggedShowing != flag && (flag || Game.GameTime - pl.NotShownSince > 400))
		{
			pl.LoggedShowing = flag;
			GrudgeLog(string.Format("INTERRO {0} prompt (D-pad Up) {1} for ped {2}", pl.Name, flag ? "showing" : "not showing", target.Handle));
		}
		return _pLock;
	}

	private bool ShowHeldPrompt(Ped held, Ped player, bool enabled)
	{
		if (Config.HeldPanel != "Own")
		{
			int num = HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY((Config.HeldPanel == "Player") ? player.Handle : held.Handle);
			HidePrompts();
			int num2 = PlacePrompt(_held, held, num, enabled);
			if (num2 != 0)
			{
				if (enabled)
				{
					return Fired(num2);
				}
				return false;
			}
		}
		HideLockPrompt();
		EnsurePrompts(eInputType.InteractOption1);
		ShowPrompts(PromptTitle(RecordFor(held), held), _pInterrogate, _pRelease);
		HUD._UI_PROMPT_SET_ENABLED(_pInterrogate, enabled);
		if (Fired(_pRelease) && _interro != null)
		{
			if (Unscared(_interro))
			{
				SayAny(player, "done talking", ArthurDone, held);
				GrudgeLog("INTERRO " + Speaker(_interro) + " dismissed - not scared of you, so nothing spared: the talk's over");
			}
			else
			{
				Say(player, "WITNESS_DISMISS", held);
				LetGoAccounting(_interro);
			}
			EndInterrogation(released: true);
			return false;
		}
		if (enabled)
		{
			return Fired(_pInterrogate);
		}
		return false;
	}

	private void HideLockPrompt()
	{
		HideLockAntag();
		if (_pLock != 0)
		{
			if (!PromptValid(_pLock))
			{
				_pLock = 0;
				_pLockTarget = 0;
			}
			else
			{
				HUD._UI_PROMPT_SET_VISIBLE(_pLock, toggle: false);
				HUD._UI_PROMPT_SET_ENABLED(_pLock, toggle: false);
			}
		}
	}

	private static bool ShownNow(int p)
	{
		try
		{
			return p != 0 && Function.Call<bool>(3779762517737572777uL, new InputArgument[1] { p }) && Function.Call<bool>(6083857518828614095uL, new InputArgument[1] { p });
		}
		catch
		{
			return false;
		}
	}

	private static bool PromptValid(int p)
	{
		try
		{
			return p != 0 && Function.Call<bool>(3779762517737572777uL, new InputArgument[1] { p });
		}
		catch
		{
			return false;
		}
	}

	internal void DropLockPrompt(string why)
	{
		if (_pLock != 0)
		{
			Crumbs.Native("Interrogation prompt delete");
			if (PromptValid(_pLock))
			{
				HUD._UI_PROMPT_DELETE(_pLock);
			}
			GrudgeLog("INTERRO lock prompt dropped (" + why + ")");
		}
		_pLock = 0;
		_pLockTarget = 0;
		_pLockText = null;
	}
}
