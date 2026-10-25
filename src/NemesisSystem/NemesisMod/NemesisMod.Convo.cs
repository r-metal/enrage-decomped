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
	private sealed class ConvoOption
	{
		public string Key;

		public string Text;

		public int Odds = -1;

		public bool Enabled = true;

		public string Note;

		public int Indent;

		public bool Cat;

		public bool Open;
	}

	private sealed class Convo
	{
		public ConvoKind Kind;

		public Ped Ped;

		public NemesisRecord Rec;

		public string Line;

		public List<ConvoOption> Options = new List<ConvoOption>();

		public int Sel;

		public int EndAt = -1;

		public int PayAmount;

		public readonly HashSet<string> Used = new HashSet<string>();

		public Interrogation It;
	}

	private Convo _convo;

	internal string ConvoNote = "";

	private static readonly string[] NightFolkSilence = new string[5] { "....", ".....", "...........", ".......", "*tries to whistle*" };

	private string _convoAfter = "none";

	private int _nextConvoTextLog;

	private int _panelClosedAt = -100000;

	private static readonly eInputType[] PanelKeep = new eInputType[34]
	{
		eInputType.LookLR,
		eInputType.LookUD,
		eInputType.LookBehind,
		eInputType.Attack,
		eInputType.Attack2,
		eInputType.Aim,
		eInputType.Reload,
		eInputType.MeleeAttack,
		eInputType.MeleeBlock,
		eInputType.MeleeModifier,
		eInputType.MeleeGrapple,
		eInputType.MeleeGrappleAttack,
		eInputType.MeleeGrappleChoke,
		eInputType.MeleeGrappleReversal,
		eInputType.MeleeGrappleBreakout,
		eInputType.MeleeGrappleStandSwitch,
		eInputType.MeleeGrappleMountSwitch,
		eInputType.Hogtie,
		eInputType.ContextB,
		eInputType.ContextX,
		eInputType.ContextY,
		eInputType.ContextLT,
		eInputType.ContextRT,
		eInputType.Context,
		eInputType.ContextSecondary,
		eInputType.ContextAction,
		eInputType.InteractLockon,
		eInputType.InteractLockonPos,
		eInputType.InteractLockonNeg,
		eInputType.InteractLockonY,
		eInputType.InteractLockonRob,
		eInputType.InteractOption2,
		eInputType.InteractPos,
		eInputType.InteractNeg
	};

	private static readonly HashSet<eInputType> MeleeKeep = new HashSet<eInputType>
	{
		eInputType.MeleeAttack,
		eInputType.MeleeBlock,
		eInputType.MeleeModifier,
		eInputType.MeleeGrapple,
		eInputType.MeleeGrappleAttack,
		eInputType.MeleeGrappleChoke,
		eInputType.MeleeGrappleReversal,
		eInputType.MeleeGrappleBreakout,
		eInputType.MeleeGrappleStandSwitch,
		eInputType.MeleeGrappleMountSwitch,
		eInputType.ContextB
	};

	private static readonly eInputType[] PanelControls = new eInputType[43]
	{
		eInputType.FrontendUp,
		eInputType.FrontendDown,
		eInputType.FrontendAccept,
		eInputType.Jump,
		eInputType.Enter,
		eInputType.ContextA,
		eInputType.Whistle,
		eInputType.WhistleHorseback,
		eInputType.OpenWheelMenu,
		eInputType.SelectNextWeapon,
		eInputType.SelectPrevWeapon,
		eInputType.QuickSelectInspect,
		eInputType.ToggleHolster,
		eInputType.TwirlPistol,
		eInputType.EmoteTwirlGunHold,
		eInputType.EmoteTwirlGunVarA,
		eInputType.EmoteTwirlGunVarB,
		eInputType.EmoteTwirlGunVarC,
		eInputType.EmoteTwirlGunVarD,
		eInputType.Inspect,
		eInputType.InspectLR,
		eInputType.InspectUD,
		eInputType.InspectZoom,
		eInputType.WeaponInspectZoom,
		eInputType.EmoteAction,
		eInputType.EmoteTaunt,
		eInputType.EmoteComm,
		eInputType.EmoteGreet,
		eInputType.OpenEmoteWheel,
		eInputType.RevealHud,
		eInputType.QuickSelectSetForSwap,
		eInputType.SelectQuickselectUnarmed,
		eInputType.SelectQuickselectSidearmsLeft,
		eInputType.SelectQuickselectSidearmsRight,
		eInputType.SelectQuickselectPrimaryLongarm,
		eInputType.SelectQuickselectSecondaryLongarm,
		eInputType.SelectQuickselectThrown,
		eInputType.SelectQuickselectMeleeNoUnarmed,
		eInputType.SelectQuickselectDualwield,
		eInputType.HorseCommandFollow,
		eInputType.HorseCommandStay,
		eInputType.HorseCommandFlee,
		eInputType.InteractLockonCallAnimal
	};

	private void StartConvo(ConvoKind kind, Ped ped, NemesisRecord rec, Interrogation it = null)
	{
		HiddenSlip(rec, "questioned");
		Convo convo = new Convo
		{
			Kind = kind,
			Ped = ped,
			Rec = rec,
			It = it
		};
		Ped character = Game.Player.Character;
		InterroLines.PlayerName = ((Models.Name(character) == "player_three") ? "John Marston" : "Arthur Morgan");
		ped.BlockPermanentEvents = true;
		TASK.TASK_LOOK_AT_ENTITY(ped.Handle, character.Handle, -1, 0, 0, 0);
		Personality personality = rec.Personality ?? Personality.Cold;
		if (kind == ConvoKind.Interrogate)
		{
			convo.Line = (string)(InterroLines.Pick((it != null && it.Terror >= 50f) ? "open_scared" : "open", it?.Gang ?? Gangs.ById(rec.Gang), it?.Temper ?? rec.Personality, _rng, null, null, LeaderOut(it?.Gang ?? Gangs.ById(rec.Gang), it?.Rec ?? rec)) ?? (personality switch
			{
				Personality.Hothead => "You're a dead man, you hear me? DEAD.", 
				Personality.Proud => "Get your hands off me.", 
				Personality.Coward => "W-what do you want? I don't know nothin'!", 
				_ => "...What.", 
			}));
		}
		_convo = convo;
		BuildOptions(convo);
		GrudgeLog($"CONVO {kind} with {rec.Name} ({rec.Personality}, hate {rec.Hate}, fear {EffectiveFear(rec)})");
		Feed($"INTERROGATION {rec.Name}: conversation opened  (hate {rec.Hate}, fear {EffectiveFear(rec)}, {rec.Personality})", Theme.Gold);
	}

	private int CashDollars => MONEY._MONEY_GET_CASH_BALANCE() / 100;

	private static int Clamp(int v, int lo = 5, int hi = 92)
	{
		return Math.Max(lo, Math.Min(hi, v));
	}

	private void BuildOptions(Convo c)
	{
		if (c.EndAt >= 0)
		{
			c.Options.Clear();
			return;
		}
		string keepKey = ((c.Sel >= 0 && c.Sel < c.Options.Count) ? c.Options[c.Sel].Key : null);
		NemesisRecord rec = c.Rec;
		rec.Touch();
		_ = rec.Personality ?? Personality.Cold;
		EffectiveFear(rec);
		Math.Max(rec.YourNotoriety, NotorietyOf(Gangs.ById(rec.Gang)));
		c.Options.Clear();
		BuildInterroOptions(c);
		foreach (string u in c.Used)
		{
			c.Options.RemoveAll((ConvoOption x) => x.Key == u);
		}
		int num = ((keepKey != null) ? c.Options.FindIndex((ConvoOption o) => o.Key == keepKey) : (-1));
		if (num >= 0)
		{
			c.Sel = num;
		}
		else if (keepKey == "ibribe")
		{
			c.Sel = Math.Max(0, c.Options.FindIndex((ConvoOption o) => o.Key == "ithreat") - 1);
		}
		c.Sel = Math.Max(0, Math.Min(c.Sel, c.Options.Count - 1));
		ConvoNote = $"{c.Kind}: " + string.Join("  ", c.Options.Select((ConvoOption x) => x.Key + " " + ((x.Odds >= 0) ? (x.Odds + "%") : "-") + (x.Enabled ? "" : "(x)")));
	}

	private bool HandleConvoKey(Keys key)
	{
		Convo convo = _convo;
		if (convo == null || convo.EndAt >= 0)
		{
			return convo != null;
		}
		int num = -1;
		switch (key)
		{
		case Keys.Up:
			convo.Sel = (convo.Sel - 1 + convo.Options.Count) % convo.Options.Count;
			return true;
		case Keys.Down:
			convo.Sel = (convo.Sel + 1) % convo.Options.Count;
			return true;
		case Keys.Return:
			num = convo.Sel;
			break;
		case Keys.Right:
			if (convo.Kind == ConvoKind.Interrogate && convo.Sel < convo.Options.Count && convo.Options[convo.Sel].Cat && !convo.Options[convo.Sel].Open && convo.Options[convo.Sel].Enabled)
			{
				ToggleCategory(convo, convo.Options[convo.Sel].Key.Substring(4), true);
			}
			return true;
		case Keys.Left:
			if (convo.Kind == ConvoKind.Interrogate && convo.It?.OpenCat != null)
			{
				ToggleCategory(convo, convo.It.OpenCat, false);
			}
			return true;
		case Keys.D1:
		case Keys.D2:
		case Keys.D3:
		case Keys.D4:
		case Keys.D5:
		case Keys.D6:
		case Keys.D7:
		case Keys.D8:
		case Keys.D9:
			num = (int)(key - 49);
			break;
		case Keys.Back:
			if (convo.Kind == ConvoKind.Interrogate)
			{
				Feed("INTERROGATION " + convo.Rec.Name + ": you closed it", Theme.Dim, convo.It?.Rec);
				EndConvo("closed");
				return true;
			}
			return true;
		default:
			return false;
		}
		if (num >= 0 && num < convo.Options.Count && (convo.Options[num].Enabled || convo.Options[num].Cat))
		{
			Choose(convo, convo.Options[num]);
		}
		return true;
	}

	private bool Roll(ConvoOption o)
	{
		if (o.Odds >= 0)
		{
			return _rng.Next(100) < o.Odds;
		}
		return true;
	}

	private void Choose(Convo c, ConvoOption o)
	{
		ChooseInterro(c, o);
	}

	private void Close(Convo c, bool walkAway = false, bool flee = false, bool fight = false)
	{
		c.EndAt = Game.GameTime + 2200;
		c.Options.Clear();
		_convoAfter = (fight ? "fight" : (flee ? "flee" : (walkAway ? "walk" : "none")));
	}

	private void EndConvo(string why)
	{
		Convo convo = _convo;
		_convo = null;
		if (convo == null)
		{
			return;
		}
		_convoClosedAt = Game.GameTime;
		_upLatchAfterClose = true;
		GrudgeLog("CONVO end (" + why + ", then " + _convoAfter + ")");
		if (convo.Ped != null && convo.Ped.Exists())
		{
			convo.Ped.BlockPermanentEvents = StaysBlocked(convo.Ped);
		}
		if (convo.Kind == ConvoKind.Interrogate)
		{
			_convoAfter = "none";
			if (_interro != null)
			{
				EndInterrogation(released: true, why != "respawn" && why != "interrupted");
			}
			return;
		}
		Ped character = Game.Player.Character;
		if (!convo.Ped.Exists() || convo.Ped.IsDead)
		{
			return;
		}
		TASK.TASK_CLEAR_LOOK_AT(convo.Ped.Handle);
		string text = ((why == "leave") ? "walk" : _convoAfter);
		_convoAfter = "none";
		if (!(text == "fight"))
		{
			if (text == "flee")
			{
				TASK.CLEAR_PED_TASKS(convo.Ped.Handle, p1: true, p2: true);
				Flee(convo.Ped.Handle, character.Handle, 150f, -1, 3f);
				EndPeacefully(convo);
			}
			else
			{
				TASK.CLEAR_PED_TASKS(convo.Ped.Handle, p1: true, p2: true);
				TASK.TASK_WANDER_STANDARD(convo.Ped.Handle, 10f, 10);
				EndPeacefully(convo);
			}
			return;
		}
		TownMan townMan = TownManFor(convo.Ped.Handle);
		if (townMan != null)
		{
			EngageTownMan(townMan, character);
			return;
		}
		if (_activeNemesisPed != null && convo.Ped.Handle == _activeNemesisPed.Handle && _enc != null)
		{
			EngageEncounter(character, blip: true);
			return;
		}
		if (_nemesisGroup == 0)
		{
			_nemesisGroup = World.AddRelationshipGroup("NEMESIS_GROUP");
		}
		convo.Ped.RelationshipGroup = _nemesisGroup;
		World.SetRelationshipBetweenGroups(eRelationshipType.Hate, _nemesisGroup, character.RelationshipGroup);
		ArmForHate(convo.Ped, convo.Rec, drawNow: true);
		TASK.TASK_COMBAT_PED(convo.Ped.Handle, character.Handle, 0, 16);
	}

	private void EndPeacefully(Convo c)
	{
		if (_activeNemesisPed != null && c.Ped.Handle == _activeNemesisPed.Handle)
		{
			World.SetRelationshipBetweenGroups(eRelationshipType.Ignore, _nemesisGroup, Game.Player.Character.RelationshipGroup);
			BandFromEncounter();
			_encEnd = ("BACKED OFF", "it ended without a fight");
			EndEncounter();
			ReleaseActive();
		}
	}

	private void UpdateConvo(Ped player)
	{
		Convo convo = _convo;
		AfterPanelControls();
		if (convo == null)
		{
			return;
		}
		if (!convo.Ped.Exists() || convo.Ped.IsDead || player.IsDead || convo.Ped.Position.DistanceTo(player.Position) > 15f)
		{
			_convoAfter = "none";
			EndConvo("broken off");
			return;
		}
		if (convo.EndAt >= 0 && Game.GameTime >= convo.EndAt)
		{
			EndConvo("done");
			return;
		}
		LockPlayerInPlace();
		if (convo.Kind != ConvoKind.Interrogate)
		{
			Game.DisableControlThisFrame(eInputType.Attack);
			Game.DisableControlThisFrame(eInputType.Aim);
		}
		if (convo.Kind == ConvoKind.Interrogate)
		{
			HoldPanelButtons(grappled: false);
			_panelClosedAt = Game.GameTime;
		}
		else
		{
			PAD.DISABLE_ALL_CONTROL_ACTIONS(0);
			PAD.ENABLE_CONTROL_ACTION(0, 2844205919u, enableRelatedActions: true);
			PAD.ENABLE_CONTROL_ACTION(0, 3523508616u, enableRelatedActions: true);
			HUD._UI_PROMPT_DISABLE_PROMPTS_THIS_FRAME();
		}
		UiDraw.FrameReserve = 60;
		DrawConvo(convo);
		if (UiDraw.LastFrameSkipped > 0 && Game.GameTime >= _nextConvoTextLog)
		{
			_nextConvoTextLog = Game.GameTime + 5000;
			GrudgeLog($"TEXT panel open: {UiDraw.LastFrameSlots} text slots last frame, {UiDraw.LastFrameSkipped} texts cut (reserve {UiDraw.FrameReserve})");
		}
	}

	private void AfterPanelControls()
	{
		if (_convo == null && Game.GameTime - _panelClosedAt <= 700)
		{
			PAD.DISABLE_ALL_CONTROL_ACTIONS(0);
			PAD.ENABLE_CONTROL_ACTION(0, 2844205919u, enableRelatedActions: true);
			PAD.ENABLE_CONTROL_ACTION(0, 3523508616u, enableRelatedActions: true);
			PAD.ENABLE_CONTROL_ACTION(0, 4165969743u, enableRelatedActions: true);
		}
	}

	private static void HoldPanelButtons(bool grappled)
	{
		PAD.DISABLE_ALL_CONTROL_ACTIONS(0);
		eInputType[] panelKeep = PanelKeep;
		foreach (eInputType eInputType in panelKeep)
		{
			if (grappled || !MeleeKeep.Contains(eInputType))
			{
				PAD.ENABLE_CONTROL_ACTION(0, (uint)eInputType, enableRelatedActions: true);
			}
		}
	}

	private List<(string, (int r, int g, int b), bool)> InterroChips(Interrogation it)
	{
		List<(string, (int, int, int), bool)> list = new List<(string, (int, int, int), bool)>();
		if (it.Rec != null && it.Rec.TemperKnown)
		{
			foreach (string item2 in it.Rec.Quirks ?? new List<string>())
			{
				list.Add((Quirks.Label(item2), (170, 150, 220), false));
			}
		}
		if (TooScaredToLie(it))
		{
			list.Add(("TOO SCARED TO LIE", Theme.Good, true));
		}
		foreach (string startTag in it.StartTags)
		{
			list.Add((startTag, (150, 170, 210), false));
		}
		foreach (string item3 in it.ActOrder)
		{
			int num = it.ActCount[item3];
			(int r, int g, int b) obj;
			switch (item3)
			{
			default:
				obj = (170, 168, 160);
				break;
			case "THREATENED":
				obj = Theme.Gold;
				break;
			case "HIT":
			case "LOOTED":
				obj = (r: 206, g: 190, b: 140);
				break;
			case "STABBED":
			case "SHOT NEAR HIM":
				obj = Theme.Warn;
				break;
			case "SHOT":
			case "SAW A KILL":
				obj = Theme.Blood;
				break;
			}
			(int, int, int) item = obj;
			list.Add(((num > 1) ? $"{item3}  x{num}" : item3, item, true));
		}
		return list;
	}

	internal static (int r, int g, int b) TemperColor(Personality p)
	{
		return p switch
		{
			Personality.Cold => (r: 170, g: 190, b: 200), 
			Personality.Loyal => (r: 140, g: 180, b: 120), 
			Personality.Greedy => (r: 200, g: 170, b: 90), 
			Personality.Proud => Theme.Gold, 
			Personality.Coward => (r: 150, g: 170, b: 210), 
			Personality.Hothead => Theme.Blood, 
			_ => (r: 180, g: 170, b: 150), 
		};
	}

	private static void RightChevron(float x, float cy, int r, int g, int b)
	{
		for (int i = 0; i < 5; i++)
		{
			float num = 0.016f * (float)(5 - i) / 5f;
			UiDraw.Rect(x + (float)i * 0.0014f, cy - num / 2f, 0.0014f, num, r, g, b, 230);
		}
	}

	private static void DownChevron(float x, float cy, int r, int g, int b)
	{
		for (int i = 0; i < 5; i++)
		{
			float num = 0.01f * (float)(5 - i) / 5f;
			UiDraw.Rect(x + (0.01f - num) / 2f, cy - 0.006f + (float)i * 0.0026f, num, 0.0026f, r, g, b, 230);
		}
	}

	private static float BodyW(string t, float scale)
	{
		return Theme.MixedWidth(t, scale, 0.0222f);
	}

	private static string FitW(string t, float scale, float width, bool caps = false)
	{
		if (string.IsNullOrEmpty(t))
		{
			return t;
		}
		Func<string, float> func = (string s) => (!caps) ? BodyW(s, scale) : Theme.CapsWidth(s, scale);
		if (func(t) <= width)
		{
			return t;
		}
		string text = Theme.Marquee(t, width, func);
		if (text != null)
		{
			return text;
		}
		int num = t.Length;
		while (num > 1 && func(t.Substring(0, num) + "...") > width)
		{
			num--;
		}
		return t.Substring(0, num).TrimEnd() + "...";
	}

	private static List<string> WrapW(string t, float scale, float width, int maxLines, bool title = false)
	{
		Func<string, float> W = (string text4) => (!title) ? BodyW(text4, scale) : Theme.MixedWidth(text4, scale, 0.0205f);
		List<string> list = new List<string>();
		string[] array = (t ?? "").Split(' ');
		string text = "";
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			string text3 = ((text.Length == 0) ? text2 : (text + " " + text2));
			if (W(text3) <= width || text.Length == 0)
			{
				text = text3;
				continue;
			}
			list.Add(text);
			text = text2;
		}
		if (text.Length > 0)
		{
			list.Add(text);
		}
		if (list.Count > maxLines)
		{
			string s = string.Join(" ", list.Skip(maxLines - 1));
			list = list.Take(maxLines - 1).ToList();
			list.Add(Fit(s));
		}
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			list[num2] = Fit(list[num2]);
		}
		return list;
		string Fit(string text4)
		{
			if (W(text4) <= width)
			{
				return text4;
			}
			int num3 = text4.Length;
			while (num3 > 1 && W(text4.Substring(0, num3) + "...") > width)
			{
				num3--;
			}
			return text4.Substring(0, num3).TrimEnd() + "...";
		}
	}

	private List<(string text, (int r, int g, int b) col)> InterroNature(Convo c)
	{
		List<(string, (int, int, int))> list = new List<(string, (int, int, int))>();
		Interrogation it = c.It;
		NemesisRecord nemesisRecord = it?.Rec ?? c.Rec;
		Personality? personality = it?.Temper ?? nemesisRecord?.Personality;
		if (personality.HasValue)
		{
			list.Add((personality.Value.ToString().ToUpperInvariant(), TemperColor(personality.Value)));
		}
		if (nemesisRecord == null || (!nemesisRecord.TemperKnown && !_debugOn))
		{
			return list;
		}
		(int, int, int) good = Theme.Good;
		(int, int, int) blood = Theme.Blood;
		(int, int, int) gold = Theme.Gold;
		if (nemesisRecord.TraitsRolled)
		{
			if (nemesisRecord.Smarts == Smarts.Dim)
			{
				list.Add(("DIM", good));
			}
			else if (nemesisRecord.Smarts == Smarts.Sharp)
			{
				list.Add(("SHARP", blood));
			}
			if (nemesisRecord.Disposition == Disposition.Friendly)
			{
				list.Add(("FRIENDLY", good));
			}
			else if (nemesisRecord.Disposition == Disposition.Surly)
			{
				list.Add(("SURLY", blood));
			}
			if (nemesisRecord.Children > 0)
			{
				list.Add(("HAS CHILDREN", gold));
			}
		}
		foreach (string item in nemesisRecord.Quirks ?? new List<string>())
		{
			switch (item)
			{
			case "drunk":
			case "traitor":
			case "superstitious":
				list.Add((Quirks.Label(item), good));
				break;
			case "sadist":
			case "braggart":
				list.Add((Quirks.Label(item), blood));
				break;
			case "honest":
			case "family man":
				list.Add((Quirks.Label(item), gold));
				break;
			}
		}
		return list;
	}

	private static List<List<(string text, (int r, int g, int b) col)>> WordLines(List<(string text, (int r, int g, int b) col)> words, float width, float scale, int maxLines)
	{
		List<List<(string, (int, int, int))>> list = new List<List<(string, (int, int, int))>>();
		List<(string, (int, int, int))> list2 = new List<(string, (int, int, int))>();
		float num = 0f;
		foreach (var word in words)
		{
			float num2 = Theme.CapsWidth(word.text, scale) + ((list2.Count > 0) ? 0.022f : 0f);
			if (list2.Count > 0 && num + num2 > width)
			{
				list.Add(list2);
				list2 = new List<(string, (int, int, int))>();
				num = 0f;
				num2 = Theme.CapsWidth(word.text, scale);
			}
			list2.Add(word);
			num += num2;
		}
		if (list2.Count > 0)
		{
			list.Add(list2);
		}
		return list.Take(maxLines).ToList();
	}

	private static void DrawWordLine(List<(string text, (int r, int g, int b) col)> line, float mx, float cy, float scale)
	{
		float num = line.Sum(((string text, (int r, int g, int b) col) t) => Theme.CapsWidth(t.text, scale)) + (float)(line.Count - 1) * 0.022f;
		float num2 = mx - num / 2f;
		for (int num3 = 0; num3 < line.Count; num3++)
		{
			var (text, tuple2) = line[num3];
			UiDraw.TextFont(text, UiDraw.TitleFont, num2, cy - scale * 0.0375f, scale, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			num2 += Theme.CapsWidth(text, scale);
			if (num3 < line.Count - 1)
			{
				UiDraw.Rect(num2 + 0.01f, cy - 0.005f, 0.0010416667f, 0.01f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 200);
				num2 += 0.022f;
			}
		}
	}

	private void DrawConvo(Convo c)
	{
		NemesisRecord rec = c.Rec;
		bool flag = c.Kind == ConvoKind.Interrogate;
		Gang gang = Gangs.ById(rec.Gang);
		bool flag2 = flag && c.It != null && Unscared(c.It);
		float num = (flag2 ? 0.7f : 0.56f);
		float x = 0.5f - num / 2f;
		float num2 = 0.5f;
		List<(string, (int, int, int))> list = new List<(string, (int, int, int))>(InterroNature(c));
		List<(string, (int, int, int), bool)> list2 = ((flag && c.It != null) ? InterroChips(c.It) : null);
		if (list2 != null)
		{
			foreach (var item in list2.Where<(string, (int, int, int), bool)>(((string, (int r, int g, int b), bool) ch) => !(c.It?.Rec?.Quirks ?? new List<string>()).Any((string q) => Quirks.Label(q) == ch.Item1)).Take(5))
			{
				list.Add((item.Item1, item.Item2));
			}
		}
		List<List<(string, (int, int, int))>> list3 = WordLines(list, num - 0.04f, 0.2f, 1);
		float h = 0.042000003f + (float)list3.Count * 0.024f + 0.006f;
		float num3 = 0.026f;
		Theme.Brush(x, num3, num, h, 255);
		float num4 = num3 + 0.01f;
		string text = FitW((rec.FullName ?? "").ToUpperInvariant(), 0.29f, flag2 ? 0.22f : (num * 0.45f), caps: true);
		string text2 = (gang?.Name ?? "no gang").ToUpperInvariant();
		float num5 = Theme.CapsWidth(text, 0.29f);
		float num6 = Theme.CapsWidth(text2, 0.2f);
		float num7 = 0.018f;
		float num8 = (flag2 ? 0.09f : 0.12f);
		bool flag3 = flag && c.It != null;
		string text3 = ((flag3 && _debugOn) ? $"FEAR {(int)Math.Round(c.It.Terror)}" : "FEAR");
		float num9 = (flag3 ? Theme.CapsWidth(text3, 0.2f) : 0f);
		string text4 = ((flag2 && _debugOn) ? $"PATIENCE {c.It.Patience:0}/{c.It.PatienceMax:0}" : "PATIENCE");
		float num10 = (flag2 ? Theme.CapsWidth(text4, 0.2f) : 0f);
		float num11 = num5 + num7 + num6 + (flag3 ? (num7 + num9 + 0.008f + num8) : 0f) + (flag2 ? (num7 + num10 + 0.008f + num8) : 0f);
		float num12 = num2 - num11 / 2f;
		float mid = num4 + 0.010875f;
		UiDraw.TextFont(text, UiDraw.TitleFont, num12, num4, 0.29f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		num12 += num5 + num7;
		Divider(num12 - num7 / 2f);
		UiDraw.TextFont(text2, UiDraw.TitleFont, num12, mid - 0.0075000003f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		num12 += num6 + num7;
		if (flag3)
		{
			Interrogation it = c.It;
			int num13 = (int)Math.Round(it.Terror);
			Divider(num12 - num7 / 2f);
			UiDraw.TextFont(text3, UiDraw.TitleFont, num12, mid - 0.0075000003f, 0.2f, 150, 170, 210);
			float num14 = num12 + num9 + 0.008f;
			float num15 = mid - 0.0027777778f + 0.001f;
			UiDraw.Rect(num14, num15, num8, 1f / 180f, 40, 38, 35, 255);
			(int, int, int) tuple = ((num13 >= 70) ? (210, 225, 255) : ((num13 >= 40) ? (150, 170, 210) : (96, 110, 140)));
			UiDraw.Rect(num14, num15, num8 * (float)num13 / 100f, 1f / 180f, tuple.Item1, tuple.Item2, tuple.Item3, 255);
			UiDraw.Rect(num14 + num8 * it.StartTerror / 100f - 0.00052083336f, num15 - 0.0018518518f, 0.0010416667f, 1f / 108f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b, 255);
			UiDraw.Rect(num14 + num8 * (float)it.CampNeed / 100f - 0.00052083336f, num15 - 0.0027777776f, 0.0010416667f, 0.01111111f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 255);
			num12 = num14 + num8 + num7;
		}
		if (flag2)
		{
			Interrogation it2 = c.It;
			float num16 = ((it2.PatienceMax > 0f) ? Math.Max(0f, Math.Min(1f, it2.Patience / it2.PatienceMax)) : 0f);
			Divider(num12 - num7 / 2f);
			UiDraw.TextFont(text4, UiDraw.TitleFont, num12, mid - 0.0075000003f, 0.2f, 210, 170, 120);
			float x2 = num12 + num10 + 0.008f;
			float y = mid - 0.0027777778f + 0.001f;
			UiDraw.Rect(x2, y, num8, 1f / 180f, 40, 38, 35, 255);
			(int, int, int) tuple2 = ((num16 > 0.5f) ? (210, 170, 120) : ((num16 > 0.25f) ? Theme.Warn : (200, 70, 60)));
			UiDraw.Rect(x2, y, num8 * num16, 1f / 180f, tuple2.Item1, tuple2.Item2, tuple2.Item3, 255);
		}
		num4 += 0.032f;
		foreach (List<(string, (int, int, int))> item2 in list3)
		{
			DrawWordLine(item2, num2, num4 + 0.009f, 0.2f);
			num4 += 0.024f;
		}
		float num17 = 0.25f;
		List<string> list4 = WrapW(SaidText(c), 0.37f, 0.45f, 3, title: true);
		int count = c.Options.Count;
		float num18 = 0.012f + (float)list4.Count * 0.033f + 0.012f + (float)count * 0.027f + 0.029f;
		float num19 = 0.962f - num18;
		Theme.Brush(num17, num19, 0.5f, num18, 255);
		float num20 = num19 + 0.012f;
		foreach (string item3 in list4)
		{
			UiDraw.TextFont(item3, UiDraw.TitleFont, 0.5f - Theme.MixedWidth(item3, 0.37f, 0.0205f) / 2f, num20, 0.37f, 255, 252, 244);
			num20 += 0.033f;
		}
		UiDraw.Rect(0.37f, num20 + 0.002f, 0.26f, 0.0018518518f, Theme.NativeRed.r, Theme.NativeRed.g, Theme.NativeRed.b, 230);
		num20 += 0.01f;
		float num21 = 0.36f;
		float num22 = 0.5f - num21 / 2f;
		for (int num23 = 0; num23 < count; num23++)
		{
			ConvoOption convoOption = c.Options[num23];
			bool flag4 = num23 == c.Sel;
			float num24 = num20 + (float)num23 * 0.027f;
			if (flag4)
			{
				Theme.SelectBox(num22 - 0.01f, num24 - 0.002f, num21 + 0.02f, 0.025f);
			}
			float num25 = num22;
			if (!XPad.RecentlyUsed && num23 < 9)
			{
				num25 += Theme.Glyph((num23 + 1).ToString(), num25, num24 + 0.002f) + 0.008f;
			}
			num25 += (float)convoOption.Indent * 0.022f;
			(int, int, int) tuple3 = ((!convoOption.Enabled) ? Theme.Faint : (flag4 ? Theme.SelectedText : Theme.Text));
			float num26 = num22 + num21;
			if (convoOption.Cat)
			{
				(int, int, int) c2 = (flag4 ? Theme.SelectedText : (convoOption.Enabled ? Theme.Gold : Theme.Faint));
				UiDraw.TextFont(convoOption.Text.ToUpperInvariant(), UiDraw.TitleFont, num25, num24 + 0.002f, 0.25f, c2.Item1, c2.Item2, c2.Item3);
				float num27 = num25 + Theme.CapsWidth(convoOption.Text.ToUpperInvariant(), 0.25f) + 0.01f;
				float cy = num24 + 0.013f;
				if (!Theme.MenuArrow((!convoOption.Open) ? Theme.ArrowDir.Right : Theme.ArrowDir.Down, num27 + 0.004f, cy, 0.016f, c2))
				{
					if (Config.MenuArrowText)
					{
						UiDraw.Glyph(convoOption.Open ? "v" : ">", UiDraw.TitleFont, num27 - 0.002f, num24 + 0.002f, 0.25f, c2.Item1, c2.Item2, c2.Item3);
					}
					else if (convoOption.Open)
					{
						DownChevron(num27, cy, c2.Item1, c2.Item2, c2.Item3);
					}
					else
					{
						RightChevron(num27, cy, c2.Item1, c2.Item2, c2.Item3);
					}
				}
				if (!convoOption.Enabled && convoOption.Note != null)
				{
					UiDraw.Text(FitW("(" + convoOption.Note + ")", 0.22f, num26 - num27 - 0.02f), num27 + 0.02f, num24 + 0.004f, 0.22f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b);
				}
			}
			else
			{
				if (convoOption.Indent > 0)
				{
					UiDraw.Rect(num25 - 0.012f, num24, 0.00078125007f, 0.023f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 200);
				}
				string text5 = ((convoOption.Indent <= 0 || !convoOption.Enabled || convoOption.Odds < 0 || !convoOption.Key.StartsWith("q:")) ? "" : (_debugOn ? $"  {convoOption.Odds}%" : ("  - " + ((convoOption.Odds < 20) ? "won't say" : ((convoOption.Odds < 55) ? "might" : "likely")))));
				UiDraw.Text(FitW(convoOption.Text + text5 + ((convoOption.Enabled || convoOption.Note == null) ? "" : ("  (" + convoOption.Note + ")")), 0.25f, num26 - num25), num25, num24 + 0.002f, 0.25f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
			}
		}
		num20 += (float)count * 0.027f;
		Theme.CenterRule(num17, num20 + 0.004f, 0.5f, 0.04f, 120);
		bool flag5 = c.It?.OpenCat != null;
		List<(string, string)> hints = ((!XPad.RecentlyUsed) ? (flag5 ? new List<(string, string)>
		{
			("Up/Down", "Navigate"),
			("Enter", "Select"),
			("Left", "Back"),
			("Backspace", "Close")
		} : new List<(string, string)>
		{
			("Up/Down", "Navigate"),
			("Enter", "Select"),
			("Backspace", "Close")
		}) : (flag5 ? new List<(string, string)>
		{
			("D-pad", "Navigate"),
			("A", "Select"),
			("Left", "Back"),
			("B", "Close")
		} : new List<(string, string)>
		{
			("D-pad", "Navigate"),
			("A", "Select"),
			("B", "Close")
		}));
		Theme.HintRow(num17 + 0.09f, num17 + 0.5f - 0.09f, num20 + 0.011f, hints, 0.2f, Theme.Dim);
		void Divider(float dx)
		{
			UiDraw.Rect(dx - 0.00052083336f, mid - 0.007f, 0.0010416667f, 0.014f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 220);
		}
	}
}
