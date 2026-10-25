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
	private const ulong PromptIsValid = 3779762517737572777uL;

	private const ulong PromptSetTextHash = 6759949783219178967uL;

	private const ulong PromptActiveGroupHash = 14292813145410643495uL;

	private const int PromptGroupId = 5127761;

	private int _pInterrogate;

	private int _pPress;

	private int _pRelease;

	private int _pAntag;

	private int _lastPromptFire;

	private int _lastDownAntagAt;

	private bool _promptsShown;

	private eInputType _ownCtrl;

	private const ulong PlaySpeechHash = 10233584479118828642uL;

	private static readonly string[] SpeechParams = new string[2] { "SPEECH_PARAMS_FORCE_NORMAL_CLEAR", "SPEECH_PARAMS_FORCE" };

	internal void CheckLockPrompt()
	{
		if (_pSocial != 0 && Gone(_pSocialTarget))
		{
			DropSocialPrompt();
		}
		if (_pAsk != 0 && Gone(_pAskFor))
		{
			HideAskPrompt();
		}
		if (_pTalk != 0 && Gone(_pTalkFor))
		{
			HideTalkPrompt();
		}
		if (_pTelegraph != 0 && Gone(_pTelegraphFor))
		{
			HideTelegraphPrompt();
		}
		if (_pLock != 0)
		{
			if (_pLockTarget == 0 || !ENTITY.DOES_ENTITY_EXIST(_pLockTarget) || (ENTITY.IS_ENTITY_DEAD(_pLockTarget) && !PED.IS_PED_INCAPACITATED(_pLockTarget)))
			{
				DropLockPrompt("its man is gone");
			}
			else if (!PromptValid(_pLock))
			{
				_pLock = 0;
				_pLockTarget = 0;
			}
		}
		static bool Gone(int h)
		{
			if (h != 0 && ENTITY.DOES_ENTITY_EXIST(h))
			{
				return ENTITY.IS_ENTITY_DEAD(h);
			}
			return true;
		}
	}

	private bool StaysBlocked(Ped p)
	{
		bool flag = _enc != null && _enc.Engaged;
		if (!(p == _activeNemesisPed && flag) && !(_followerOf.ContainsKey(p.Handle) && flag) && !_townMen.Any((TownMan t) => t.Ped == p && t.Engaged))
		{
			return _raids.Any((Raid r) => r.Men.Contains(p));
		}
		return true;
	}

	internal void QuietPrompts(string why)
	{
		DropLockPrompt(why);
		DropSocialPrompt();
		HideAskPrompt();
		HideRecruitPrompt();
		HideTelegraphPrompt();
		HidePrompts();
	}

	private void EnsurePrompts()
	{
		EnsurePrompts((_pInterrogate != 0) ? _ownCtrl : eInputType.InteractOption1);
	}

	private void EnsurePrompts(eInputType ctrl)
	{
		if (_pInterrogate != 0 && ctrl == _ownCtrl && PromptValid(_pInterrogate) && PromptValid(_pPress) && PromptValid(_pRelease) && PromptValid(_pAntag))
		{
			return;
		}
		Crumbs.Native("Interrogation prompt delete");
		int[] array = new int[4] { _pInterrogate, _pPress, _pRelease, _pAntag };
		foreach (int num in array)
		{
			if (PromptValid(num))
			{
				HUD._UI_PROMPT_DELETE(num);
			}
		}
		_ownCtrl = ctrl;
		_pInterrogate = MakePrompt(ctrl, "Interrogate", hold: true);
		_pPress = MakePrompt(ctrl, "Interrogate", hold: true);
		_pRelease = MakePrompt(Config.InterrogateReleaseControl, "Dismiss", hold: false);
		_pAntag = MakePrompt(eInputType.InteractLockonNeg, "Antagonize", hold: false);
	}

	private static int MakePrompt(eInputType control, string text, bool hold, int group = 5127761)
	{
		Crumbs.Native("Interrogation prompt create");
		int num = HUD._UI_PROMPT_REGISTER_BEGIN();
		HUD._UI_PROMPT_SET_CONTROL_ACTION(num, (uint)control);
		Function.Call(6759949783219178967uL, num, new InputArgument(UiDraw.ForeverVarString(text)));
		if (hold)
		{
			HUD._UI_PROMPT_SET_STANDARDIZED_HOLD_MODE(num, 1704213876u);
		}
		else
		{
			HUD._UI_PROMPT_SET_STANDARD_MODE(num, releaseMode: false);
		}
		HUD._UI_PROMPT_SET_GROUP(num, group, 0);
		HUD._UI_PROMPT_REGISTER_END(num);
		HUD._UI_PROMPT_SET_VISIBLE(num, toggle: false);
		HUD._UI_PROMPT_SET_ENABLED(num, toggle: false);
		return num;
	}

	private string PromptTitle(NemesisRecord rec, Ped p = null)
	{
		if (rec != null)
		{
			return rec.FullName;
		}
		if (p != null && _named.TryGetValue(p.Handle, out var value) && !string.IsNullOrEmpty(value.Name))
		{
			return value.Name;
		}
		Gang gang = ((p != null) ? OutfitOfPed(p) : null);
		if (gang == null)
		{
			return "Stranger";
		}
		return MemberWord(gang);
	}

	private static string MemberWord(Gang g)
	{
		string text = g.Name ?? "";
		if (text.StartsWith("The "))
		{
			text = text.Substring(4);
		}
		if (text.EndsWith(" Boys"))
		{
			text = text.Substring(0, text.Length - 5);
		}
		else if (text.EndsWith("s") && !text.EndsWith("ss"))
		{
			text = text.Substring(0, text.Length - 1);
		}
		return text;
	}

	private void ShowPrompts(string title, params int[] show)
	{
		EnsurePrompts();
		int[] array = new int[4] { _pInterrogate, _pPress, _pRelease, _pAntag };
		foreach (int num in array)
		{
			bool toggle = show.Contains(num);
			HUD._UI_PROMPT_SET_VISIBLE(num, toggle);
			HUD._UI_PROMPT_SET_ENABLED(num, toggle);
		}
		Function.Call(14292813145410643495uL, 5127761u, new InputArgument(UiDraw.VarString(title)), 0, 0, 0, 0);
		_promptsShown = true;
	}

	private void HidePrompts()
	{
		if (!_promptsShown)
		{
			return;
		}
		int[] array = new int[4] { _pInterrogate, _pPress, _pRelease, _pAntag };
		foreach (int num in array)
		{
			if (PromptValid(num))
			{
				HUD._UI_PROMPT_SET_VISIBLE(num, toggle: false);
				HUD._UI_PROMPT_SET_ENABLED(num, toggle: false);
			}
		}
		_promptsShown = false;
	}

	private bool Fired(int p)
	{
		if (p == 0 || Game.GameTime - _lastPromptFire < 900)
		{
			return false;
		}
		if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(p) && !HUD._UI_PROMPT_HAS_STANDARD_MODE_COMPLETED(p, 0))
		{
			return false;
		}
		_lastPromptFire = Game.GameTime;
		return true;
	}

	private void DeletePrompts()
	{
		Crumbs.Native("Interrogation prompt delete");
		int[] array = new int[6] { _pInterrogate, _pPress, _pRelease, _pAntag, _pLock, _pLockAntag };
		foreach (int num in array)
		{
			if (PromptValid(num))
			{
				HUD._UI_PROMPT_DELETE(num);
			}
		}
		_pInterrogate = (_pPress = (_pRelease = (_pAntag = (_pLock = (_pLockAntag = 0)))));
		_pLockAntagGroup = 0;
		_pLockTarget = 0;
		_promptsShown = false;
	}

	private void Say(Ped speaker, string line, Ped listener)
	{
		if (!TrySay(speaker, line, listener))
		{
			GrudgeLog($"SPEECH '{line}' did not play for ped {speaker?.Handle}");
		}
	}

	private bool SayAny(Ped speaker, string set, string[] lines, Ped listener)
	{
		if (speaker == null || !speaker.Exists())
		{
			return false;
		}
		string key = (uint)speaker.Model.Hash + "|" + set;
		if (_voiceWorks.TryGetValue(key, out var value) && TrySay(speaker, value, listener))
		{
			return true;
		}
		string[] array = GoodFirst(lines);
		foreach (string text in array)
		{
			if (TrySay(speaker, text, listener))
			{
				_voiceWorks[key] = text;
				NoteGoodLine(text);
				GrudgeLog("SPEECH " + set + ": '" + text + "' played for " + Models.Name(speaker));
				return true;
			}
		}
		GrudgeLog($"SPEECH {set}: none of {lines.Length} lines played for {Models.Name(speaker)}");
		return false;
	}

	private unsafe bool TrySay(Ped speaker, string line, Ped listener)
	{
		if (speaker == null || !speaker.Exists() || DeadNow(speaker))
		{
			return false;
		}
		ulong* ptr = stackalloc ulong[8];
		string[] speechParams = SpeechParams;
		foreach (string text in speechParams)
		{
			int[] array = new int[2] { 0, 1 };
			foreach (int num in array)
			{
				for (int k = 0; k < 8; k++)
				{
					ptr[k] = 0uL;
				}
				*ptr = UiDraw.CString(line);
				ptr[2] = (ulong)num;
				ptr[3] = MISC.GET_HASH_KEY(text);
				ptr[4] = ((listener != null && listener.Exists()) ? ((uint)listener.Handle) : 0);
				ptr[6] = 1uL;
				ptr[7] = 1uL;
				if (Function.Call<bool>(10233584479118828642uL, new InputArgument[2]
				{
					speaker.Handle,
					new InputArgument((ulong)ptr)
				}))
				{
					return true;
				}
			}
		}
		string text2 = BorrowedVoice((uint)speaker.Model.Hash);
		if (text2 != null && TrySayVoice(speaker, line, text2, listener))
		{
			return true;
		}
		return false;
	}

	private NemesisRecord RecordFor(Ped p)
	{
		if (!(p == null))
		{
			return RecordAt(p.Handle);
		}
		return null;
	}

	private Ped FindInterrogationTarget(Ped player)
	{
		Ped result = null;
		float num = 2.8f;
		foreach (Ped item in PedsAround(Game.Player.Character))
		{
			if (!(item == null) && item.Exists() && !item.IsPlayer)
			{
				float num2 = item.Position.DistanceTo(player.Position);
				if (!(num2 > num) && item.IsHuman && !DeadNow(item) && !Kids.IsChild(item) && (RecordFor(item)?.Gang != null || OutfitOfPed(item) != null) && !IsStoryPed(item) && !GameOwned(item) && (!GameEventPed(item) || RallyManFree(item)) && AtMercy(item, player))
				{
					result = item;
					num = num2;
				}
			}
		}
		return result;
	}

	private static bool DeadNow(Ped p)
	{
		if (p.IsDead)
		{
			return !p.IsIncapacitated;
		}
		return false;
	}

	private bool WoundedDown(Ped p)
	{
		if (p == null || !p.Exists() || (PED.IS_PED_FATALLY_INJURED(p.Handle) && !p.IsIncapacitated))
		{
			return false;
		}
		if (!p.IsIncapacitated)
		{
			return false;
		}
		if (GunMelee.KnockedOutByGunButt(p))
		{
			return false;
		}
		return _bulletImpact.LatestWound(p.Handle) != null;
	}

	private bool AtMercy(Ped p, Ped player)
	{
		if (LooksKnockedOut(p) && !p.IsHogtied && !WoundedDown(p))
		{
			return false;
		}
		if (!p.IsHogtied && !WoundedDown(p) && (!PED.GET_PED_IS_BEING_GRAPPLED(p.Handle) || PED._GET_PED_GRAPPLER(p.Handle) != player.Handle) && !p.IsRagdoll && !TASK.IS_PED_IN_WRITHE(p.Handle))
		{
			return _bulletImpact.CrippleLevelOf(p.Handle) >= CrippleLevel.Legless;
		}
		return true;
	}

	private void OnInterrogateKey()
	{
		HideLockPrompt();
		Ped character = Game.Player.Character;
		if (_interro == null && (WorldBusy(character) || MISC.GET_MISSION_FLAG()))
		{
			return;
		}
		if (_interro != null)
		{
			if (_interro.Stage == 2)
			{
				_interro.Round++;
				_interro.Stage = 0;
			}
			return;
		}
		Ped ped = LockedInterrogable(character) ?? _interroCandidate;
		if (ped == null || !ped.Exists() || DeadNow(ped))
		{
			return;
		}
		NemesisRecord nemesisRecord = RecordFor(ped);
		if (_interroMemory.TryGetValue(ped.Handle, out var value) && value.Ped.Exists() && !DeadNow(value.Ped))
		{
			if (nemesisRecord != null && value.Rec != nemesisRecord)
			{
				GrudgeLog("INTERRO " + nemesisRecord.Name + ": resumed on his nemesis record now");
				value.Rec = nemesisRecord;
				value.Shown = nemesisRecord;
				value.Temper = nemesisRecord.Personality ?? value.Temper;
			}
			value.Regrabs = 0;
			value.RegrabAt = -1;
			value.WasGrappled = false;
			value.Frozen = (value.HeldByUs = (value.HandsUp = (value.Defiant = false)));
			value.GotAway = false;
			value.SlippedAt = -1;
			value.PendingShotAt = -1;
			value.NextAt = 0;
			value.WasGrappled = GrappledBy(ped, character);
			value.StartedDown = !value.WasGrappled && (ped.IsRagdoll || ped.IsHogtied || TASK.IS_PED_IN_WRITHE(ped.Handle));
			if (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, ped.Handle))
			{
				value.LastAimedAt = Game.GameTime;
			}
			value.HoldPos = ped.Position;
			if (value.Calm)
			{
				string text = FoughtYouWhy(ped.Handle);
				if (text != null)
				{
					value.Calm = false;
					GrudgeLog("INTERRO " + Speaker(value) + ": he's fought you since the last questioning (" + text + ") - no patience rules now");
				}
			}
			_interro = value;
			ped.BlockPermanentEvents = true;
			value.LastChance = ChanceToTalk(value, character);
			Feed($"INTERROGATION {Speaker(value)}: picked up again (fear {value.Terror:0}, camp {value.LastChance}%, family {value.FamilyChance}%)", Theme.Gold, nemesisRecord);
			if (_convo != null)
			{
				EndConvo("interrogation");
			}
			FaceSeenInQuestioning(ped, character);
			StartConvo(ConvoKind.Interrogate, ped, value.Shown, value);
			HoldHisPeople(ped);
			return;
		}
		_interro = new Interrogation
		{
			Ped = ped,
			Rec = nemesisRecord,
			Gang = ((nemesisRecord != null) ? Gangs.ById(nemesisRecord.Gang) : OutfitOfPed(ped)),
			LastAimedAt = Game.GameTime,
			StartedAt = Game.GameTime
		};
		_interroMemory[ped.Handle] = _interro;
		ped.BlockPermanentEvents = true;
		Personality? personality = null;
		if (nemesisRecord == null && Enum.TryParse<Personality>(TemperOf(ped), out var result))
		{
			personality = result;
		}
		_interro.Temper = nemesisRecord?.Personality ?? personality ?? Personalities.Pick(_rng, gang: true);
		if (nemesisRecord != null)
		{
			nemesisRecord.PersonalityKnown = true;
			nemesisRecord.AddFear(Balance.FearPerInterrogation);
			nemesisRecord.AddYourNotoriety(5);
			SaveRoster();
		}
		_interro.WasGrappled = GrappledBy(ped, character);
		_interro.StartedDown = !_interro.WasGrappled && (ped.IsRagdoll || ped.IsHogtied || TASK.IS_PED_IN_WRITHE(ped.Handle));
		_interro.HoldPos = ped.Position;
		if (nemesisRecord != null)
		{
			nemesisRecord.TimesInterrogated++;
			nemesisRecord.Touch();
		}
		HidePrompts();
		HideLockPrompt();
		NemesisRecord nemesisRecord2 = nemesisRecord;
		if (nemesisRecord2 == null)
		{
			_named.TryGetValue(ped.Handle, out var _);
			nemesisRecord2 = StandInFor(ped, _interro);
			_interro.Stand = nemesisRecord2;
		}
		_interro.Shown = nemesisRecord2;
		_interro.Terror = (_interro.StartTerror = Math.Min(60f, (nemesisRecord != null) ? (5f + (float)EffectiveFear(nemesisRecord) / 3f) : 8f));
		float terror = _interro.Terror;
		var (num, text2) = BattleShock(ped, nemesisRecord, nemesisRecord2, character);
		_interro.StartNote = string.Format("base {0:0} (his fear of you / 3 + 5){1}", terror, (num > 0f) ? $" + fight {num:0} ({text2})" : " + no fight shock");
		_interro.StartTags.Add(_interro.WasGrappled ? "IN YOUR GRIP" : (ped.IsHogtied ? "HOGTIED" : (GunOn(ped) ? "AT GUNPOINT" : (_interro.StartedDown ? "ON THE GROUND" : "HELD"))));
		string[] array = (text2 ?? "").Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			string text3 = array[i].Trim();
			if (text3.Length != 0 && !text3.StartsWith("x"))
			{
				_interro.StartTags.Add(text3.EndsWith("killed") ? ("SAW " + text3.Split(' ')[0] + " DIE") : (text3.EndsWith("his own") ? (text3.Split(' ')[0] + " OF HIS OWN") : text3.ToUpperInvariant()));
			}
		}
		if (num > 0f)
		{
			_interro.Terror = (_interro.StartTerror = Math.Min(90f, _interro.Terror + num));
			Feed($"INTERROGATION {nemesisRecord2.Name}: shaken by the fight +{num:0} fear ({text2})", (150, 170, 210), nemesisRecord);
		}
		if (_fighters.TryGetValue(ped.Handle, out var value3) && value3.Broke)
		{
			int item = Breaking(_interro.Temper).need;
			float num2 = Math.Min(90f, (float)(item + 5) + Math.Max(0f, (float)BreakLine(value3.Temper) - value3.Lowest) / 2f);
			if (_interro.Terror < num2)
			{
				Feed(string.Format("INTERROGATION {0}: {1} in the fight - fear {2:0} -> {3:0}", nemesisRecord2.Name, (value3.State == "surrendered") ? "gave up" : "broke", _interro.Terror, num2), (150, 170, 210), nemesisRecord);
				_interro.Terror = (_interro.StartTerror = num2);
			}
			_interro.StartTags.Add((value3.State == "surrendered") ? "SURRENDERED" : "BROKE AND RAN");
		}
		if (nemesisRecord != null && nemesisRecord.LiedToYou > 0)
		{
			_interro.Terror = (_interro.StartTerror = Math.Min(95f, _interro.Terror + 8f * (float)Math.Min(2, nemesisRecord.LiedToYou)));
			_interro.StartTags.Add("CAUGHT LYING BEFORE");
		}
		string text4 = ((num > 0f) ? ("shaken by one: " + text2) : FoughtYouWhy(ped.Handle));
		_interro.Calm = text4 == null;
		if (_interro.Calm)
		{
			_interro.Patience = (_interro.PatienceMax = InterroPatience.For(_interro.Temper));
		}
		bool flag = GunOn(ped);
		if (flag)
		{
			_interro.LastAimedAt = Game.GameTime;
			_interro.Terror = Math.Min(100f, _interro.Terror + 8f * Traits.FearGain(nemesisRecord ?? nemesisRecord2) * Balance.FearGainScale);
		}
		_interro.LastChance = ChanceToTalk(_interro, character);
		Feed(string.Format("INTERROGATION {0}: {1}  fear {2:0}  camp {3}%  family {4}%{5}", nemesisRecord2.Name, _interro.WasGrappled ? "in your grip" : (ped.IsHogtied ? "hogtied" : (flag ? "at gunpoint" : (_interro.StartedDown ? "on the ground" : "held"))), _interro.Terror, _interro.LastChance, _interro.FamilyChance, _interro.Calm ? $"  patience {_interro.Patience:0}" : ""), Theme.Gold, nemesisRecord);
		GrudgeLog(string.Format("INTERRO start ped={0} grappled={1} hogtied={2} chance={3} {4}", ped.Handle, _interro.WasGrappled, ped.IsHogtied, _interro.LastChance, _interro.Calm ? $"calm (no fight with you): patience {_interro.Patience:0}, fear line {_interro.CampNeed}" : ("from a fight (" + text4 + "): no patience rules")));
		if (_convo != null)
		{
			EndConvo("interrogation");
		}
		FaceSeenInQuestioning(ped, character);
		StartConvo(ConvoKind.Interrogate, ped, nemesisRecord2, _interro);
		HoldHisPeople(ped);
		Say(character, PED.GET_PED_IS_BEING_GRAPPLED(ped.Handle) ? "ROB_TOUGH_GRAPPLE_THREATEN" : (ped.IsHogtied ? "INSULT_HOGTIED_PED_CONV_PART1" : "WITNESS_THREATEN_MALE"), ped);
	}

	private bool HoldHim(Interrogation it, Ped player)
	{
		Ped ped = it.Ped;
		if (GrappledBy(ped, player))
		{
			if (it.SlippedAt >= 0)
			{
				it.SlippedAt = -1;
				it.Regrabs++;
				GrudgeLog($"INTERRO you grabbed him again ({it.Regrabs}) - the questioning goes on");
				if (_convo == null)
				{
					StartConvo(ConvoKind.Interrogate, ped, it.Shown, it);
				}
			}
			it.WasGrappled = true;
			it.RegrabAt = -1;
			return true;
		}
		if (ped.IsHogtied || TASK.IS_PED_IN_WRITHE(ped.Handle) || _bulletImpact.CrippleLevelOf(ped.Handle) >= CrippleLevel.Legless || (!it.WasGrappled && ped.IsRagdoll))
		{
			if (it.SlippedAt >= 0)
			{
				it.SlippedAt = -1;
				GrudgeLog("INTERRO he slipped your grip, then went down - the questioning goes on");
				if (_convo == null)
				{
					StartConvo(ConvoKind.Interrogate, ped, it.Shown, it);
				}
			}
			return true;
		}
		if (!it.WasGrappled)
		{
			bool num = PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, ped.Handle) || PlayerTargets(ped);
			bool flag = _convo != null && _convo.It == it && _convo.EndAt >= 0;
			if (num || flag || Unscared(it))
			{
				it.LastAimedAt = Game.GameTime;
			}
			if (Game.GameTime - it.LastAimedAt > 4000)
			{
				GrudgeLog("INTERRO he's on his feet and no gun on him 4 s - the questioning's over (where he goes is his fear's: AfterQuestioning)");
				return false;
			}
			if (!it.HandsUp)
			{
				it.HandsUp = (it.HeldByUs = true);
				it.HoldPos = ped.Position;
				TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: true);
				bool flag2 = it.Temper == Personality.Coward || it.Terror >= 60f;
				it.Defiant = !flag2 && Unscared(it);
				if (flag2)
				{
					TASK.TASK_COWER(ped.Handle, -1, player.Handle, "");
				}
				else if (it.Defiant)
				{
					FaceYouDown(ped, player);
				}
				else
				{
					TASK.TASK_HANDS_UP(ped.Handle, -1, player.Handle, -1, p4: false);
				}
				string text = (flag2 ? "cowering" : (it.Defiant ? "stands his ground, eyes on you" : "hands up"));
				GrudgeLog("INTERRO on his feet " + (GunOn(ped) ? "at gunpoint" : "face to face (no gun in your hand)") + " - " + text);
				Feed("INTERROGATION " + Speaker(it) + ": " + (flag2 ? "cowers" : (it.Defiant ? "stands there, eyes on you" : "throws his hands up")), Theme.Dim, it.Rec);
			}
			if (ped.Position.DistanceTo(it.HoldPos) > 4f)
			{
				GrudgeLog("INTERRO he backed off / ran");
				return false;
			}
			return true;
		}
		if (it.WasGrappled && ped.Position.DistanceTo(player.Position) > 2.6f)
		{
			GrudgeLog($"INTERRO he broke out of the grapple and got {ped.Position.DistanceTo(player.Position):0.0} m away");
			return false;
		}
		if (it.WasGrappled && Game.GameTime - it.LastGamePromptAt < 1500)
		{
			GrudgeLog("INTERRO released through the game's own prompt (Dismiss)");
			return false;
		}
		if (it.WasGrappled)
		{
			if (it.SlippedAt < 0)
			{
				it.SlippedAt = Game.GameTime;
				GrudgeLog($"INTERRO he slipped your grip - {2} s to grab him again");
				if (_convo != null && _convo.It == it)
				{
					_convo = null;
					_convoClosedAt = Game.GameTime;
					_panelClosedAt = -100000;
				}
				return true;
			}
			if (Game.GameTime - it.SlippedAt < 2000)
			{
				return true;
			}
			GrudgeLog("INTERRO he broke out of your grip - you didn't grab him again");
			return false;
		}
		return false;
	}

	private void OnInterrogationGamePrompt(int target, int promptType)
	{
		Interrogation interro = _interro;
		if (interro == null || !interro.Ped.Exists() || interro.Ped.Handle != target)
		{
			return;
		}
		interro.LastGamePromptAt = Game.GameTime;
		if (promptType != 3)
		{
			if (Game.GameTime - interro.LastGameThreatAt < 2500)
			{
				return;
			}
			if (interro.ThreatsUsed >= 3 || !interro.ScaredSinceThreat)
			{
				GrudgeLog(string.Format("INTERRO game prompt type={0} ignored ({1})", promptType, (interro.ThreatsUsed >= 3) ? "threats used up" : "scare him first"));
				return;
			}
			interro.LastGameThreatAt = Game.GameTime;
			interro.ThreatsUsed++;
			interro.ScaredSinceThreat = false;
			interro.Threats++;
			Terrify(interro, Balance.TerrorThreat, "THREATENED");
		}
		GrudgeLog($"INTERRO game prompt type={promptType} on him (threats {interro.Threats})");
	}

	private static bool GunOn(Ped p)
	{
		if (p != null && p.Exists() && PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, p.Handle))
		{
			return WEAPON.IS_PED_ARMED(Game.Player.Character.Handle, 6);
		}
		return false;
	}

	private static void LockPlayerInPlace()
	{
		eInputType[] array = new eInputType[8]
		{
			eInputType.MoveLR,
			eInputType.MoveUD,
			eInputType.Sprint,
			eInputType.Jump,
			eInputType.Enter,
			eInputType.Cover,
			eInputType.Dive,
			eInputType.Duck
		};
		for (int i = 0; i < array.Length; i++)
		{
			Game.DisableControlThisFrame(array[i]);
		}
	}

	private Interrogation InterroFor(int handle)
	{
		if (_interro != null && _interro.Ped.Exists() && _interro.Ped.Handle == handle)
		{
			return _interro;
		}
		if (!_interroMemory.TryGetValue(handle, out var value) || !value.Ped.Exists() || DeadNow(value.Ped))
		{
			return null;
		}
		return value;
	}

	private void OnInterrogationHit(int victim, int kind = 0)
	{
		Interrogation interrogation = InterroFor(victim);
		if (interrogation == null)
		{
			return;
		}
		if (kind == interrogation.LastHitKind && Game.GameTime - interrogation.LastHitKindAt < 600)
		{
			interrogation.LastDamageAt = Game.GameTime;
			return;
		}
		interrogation.LastHitKind = kind;
		interrogation.LastHitKindAt = Game.GameTime;
		interrogation.LastDamageAt = Game.GameTime;
		interrogation.PendingShotAt = -1;
		switch (kind)
		{
		case 2:
			interrogation.Shots++;
			Terrify(interrogation, Balance.TerrorShot, "SHOT");
			return;
		case 1:
			interrogation.Stabs++;
			Terrify(interrogation, Balance.TerrorStab, "STABBED");
			return;
		}
		interrogation.Hits++;
		if (interrogation.Temper == Personality.Proud && interrogation.Rec != null)
		{
			int hate = interrogation.Rec.Hate;
			interrogation.Rec.AddHate(2);
			CapScuffle(interrogation.Rec, hate);
		}
		Terrify(interrogation, Balance.TerrorHit, "HIT");
	}

	private static bool ShotPasses(Vector3 cam, Vector3 dir, Ped p, float reach)
	{
		Vector3[] array = new Vector3[3]
		{
			p.Position,
			p.Position + new Vector3(0f, 0f, 0.6f),
			p.Position - new Vector3(0f, 0f, 0.8f)
		};
		foreach (Vector3 vector in array)
		{
			Vector3 vector2 = vector - cam;
			float num = vector2.X * dir.X + vector2.Y * dir.Y + vector2.Z * dir.Z;
			if (!(num <= 0f) && (cam + dir * num).DistanceTo(vector) < reach)
			{
				return true;
			}
		}
		return false;
	}

	private static (Vector3 cam, Vector3 dir) AimRay()
	{
		Vector3 gAMEPLAY_CAM_COORD = CAM.GET_GAMEPLAY_CAM_COORD();
		Vector3 vector = CAM.GET_GAMEPLAY_CAM_ROT(2);
		double num = (double)vector.X * Math.PI / 180.0;
		double num2 = (double)vector.Z * Math.PI / 180.0;
		return (cam: gAMEPLAY_CAM_COORD, dir: new Vector3((float)((0.0 - Math.Sin(num2)) * Math.Cos(num)), (float)(Math.Cos(num2) * Math.Cos(num)), (float)Math.Sin(num)));
	}

	private unsafe static uint CurrentWeaponOf(int ped)
	{
		uint result = 0u;
		WEAPON.GET_CURRENT_PED_WEAPON(ped, &result, p2: true, 0, p4: false);
		return result;
	}

	private void OnInterrogationLoot(int victim)
	{
		Interrogation interrogation = InterroFor(victim);
		if (interrogation != null)
		{
			Terrify(interrogation, Balance.TerrorLoot, "LOOTED");
		}
	}

	private void WatchMenace(Interrogation it, Ped player)
	{
		Ped ped = it.Ped;
		if (PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, ped.Handle) && Game.GameTime - it.LastAimTick >= 1000)
		{
			bool num = Game.GameTime - it.LastAimTick > 2500;
			it.LastAimTick = Game.GameTime;
			it.LastAimedAt = Game.GameTime;
			if (num)
			{
				it.Act("GUN ON HIM");
				it.ScaredSinceThreat = true;
				Feed($"AIMED AT {Speaker(it)}: fear {it.Terror:0} (+{Balance.TerrorAimPerSec}/s while you hold it, up to 60)", Theme.Dim);
			}
			if (it.Terror < 60f)
			{
				it.Terror = Math.Min(60f, it.Terror + (float)Balance.TerrorAimPerSec * Traits.FearGain(it.Rec ?? it.Shown) * Balance.FearGainScale);
			}
		}
		bool flag = PED.IS_PED_SHOOTING(player.Handle);
		if (flag)
		{
			it.LastShotAt = Game.GameTime;
		}
		int health = ped.Health;
		if (it.LastHealth > 0 && health < it.LastHealth - 1 && Game.GameTime - it.LastDamageAt > 300 && (GrappledBy(ped, player) || (ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(ped.Handle, player.Handle, p2: true, p3: true) && (ped.Position.DistanceTo(player.Position) < 3.5f || Game.GameTime - it.LastShotAt < 600))))
		{
			uint num2 = CurrentWeaponOf(player.Handle);
			uint num3 = ((num2 != 0) ? WEAPON.GET_WEAPONTYPE_GROUP(num2) : 0u);
			int num4 = ((Game.GameTime - it.LastShotAt < 600) ? 2 : ((num3 == 3566412244u) ? 1 : 0));
			GrudgeLog(string.Format("INTERRO health {0} -> {1} with no damage event -> {2}", it.LastHealth, health, num4 switch
			{
				1 => "cut", 
				2 => "shot", 
				_ => "hit", 
			}));
			OnInterrogationHit(ped.Handle, num4);
		}
		it.LastHealth = health;
		if (flag && !it.WasShooting && it.PendingShotAt < 0)
		{
			Vector3 gAMEPLAY_CAM_COORD = CAM.GET_GAMEPLAY_CAM_COORD();
			Vector3 vector = CAM.GET_GAMEPLAY_CAM_ROT(2);
			double num5 = (double)vector.X * Math.PI / 180.0;
			double num6 = (double)vector.Z * Math.PI / 180.0;
			Vector3 dir = new Vector3((float)((0.0 - Math.Sin(num6)) * Math.Cos(num5)), (float)(Math.Cos(num6) * Math.Cos(num5)), (float)Math.Sin(num5));
			if (ShotPasses(gAMEPLAY_CAM_COORD, dir, ped, (ped.IsRagdoll || ped.IsHogtied) ? 3.5f : 3f))
			{
				it.PendingShotAt = Game.GameTime;
			}
		}
		it.WasShooting = flag;
		if (it.PendingShotAt < 0 || Game.GameTime - it.PendingShotAt <= 300)
		{
			return;
		}
		it.PendingShotAt = -1;
		if (Game.GameTime - it.LastDamageAt > 400)
		{
			it.NearShots++;
			int val = ((it.NearShots <= 3) ? Balance.TerrorNearShot : ((Balance.TerrorNearShot + 1) / 2));
			val = Math.Min(val, Math.Max(0, 30 - it.NearFear));
			it.NearFear += val;
			if (val > 0)
			{
				Terrify(it, val, "SHOT NEAR");
			}
		}
	}

	private void StepInterrogation(Ped player)
	{
		Interrogation interro = _interro;
		HoldHisPeopleTick(player);
		if (!interro.Ped.Exists() || DeadNow(interro.Ped) || player.IsDead || interro.Ped.Position.DistanceTo(player.Position) > 14f)
		{
			if (_convo != null && _convo.It == interro)
			{
				_convo = null;
			}
			EndInterrogation(released: false);
		}
		else if (!HoldHim(interro, player))
		{
			if (Game.GameTime - interro.StartedAt < 2000)
			{
				GrudgeLog("INTERRO " + Speaker(interro) + " slipped away before it started - nothing recorded");
				interro.GotAway = true;
				if (_convo != null)
				{
					EndConvo("broke out");
				}
				else
				{
					EndInterrogation(released: true);
				}
				return;
			}
			Feed("INTERROGATION " + Speaker(interro) + ": " + (interro.WasGrappled ? "broke out of your grip" : "got up and away from you"), Theme.Warn, interro.Rec);
			if (interro.Rec != null)
			{
				History(interro.Rec, interro.WasGrappled ? "BROKE OUT of your grip during questioning" : "GOT AWAY during questioning", Theme.Warn);
			}
			ShowSubtitle(interro.WasGrappled ? (Speaker(interro) + " broke loose!") : (Speaker(interro) + " got away from you."));
			interro.GotAway = true;
			if (_convo != null)
			{
				EndConvo("broke out");
			}
			else
			{
				EndInterrogation(released: true);
			}
		}
		else
		{
			if (interro.SlippedAt >= 0)
			{
				return;
			}
			if (_convo == null || _convo.Kind != ConvoKind.Interrogate || _convo.It != interro)
			{
				EndInterrogation(released: true);
				return;
			}
			WatchMenace(interro, player);
			if (interro.Defiant && interro.HandsUp && !Unscared(interro) && !interro.GoNow)
			{
				interro.Defiant = false;
				bool flag = interro.Temper == Personality.Coward || interro.Terror >= 60f;
				TASK.CLEAR_PED_TASKS(interro.Ped.Handle, p1: true, p2: false);
				if (flag)
				{
					TASK.TASK_COWER(interro.Ped.Handle, -1, player.Handle, "");
				}
				else
				{
					TASK.TASK_HANDS_UP(interro.Ped.Handle, -1, player.Handle, -1, p4: false);
				}
				GrudgeLog(string.Format("INTERRO {0} is scared of you now (fear {1:0}, line {2}) - {3}", Speaker(interro), interro.Terror, interro.CampNeed, flag ? "cowers" : "hands up"));
				Feed("INTERROGATION " + Speaker(interro) + ": " + (flag ? "cowers" : "puts his hands up"), Theme.Dim, interro.Rec);
			}
			if (Game.GameTime < interro.NextAt)
			{
				return;
			}
			interro.NextAt = Game.GameTime + 300;
			int lastChance = interro.LastChance;
			interro.LastChance = ChanceToTalk(interro, player);
			ReopenQuestions(interro);
			int num = GateState(interro);
			if (Math.Abs(lastChance - interro.LastChance) >= 5 || num != interro.LastGates)
			{
				interro.LastGates = num;
				BuildOptions(_convo);
				if (lastChance != interro.LastChance)
				{
					GrudgeLog($"INTERRO will {lastChance}% -> {interro.LastChance}% ({InterroNote})");
				}
			}
		}
	}

	private string Speaker(Interrogation it)
	{
		return it.Rec?.Name ?? it.Shown?.Name ?? PromptTitle(null, it.Ped);
	}
}
