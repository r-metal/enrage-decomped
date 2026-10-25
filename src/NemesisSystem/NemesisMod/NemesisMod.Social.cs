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
	private sealed class SocialOption
	{
		public string Key;

		public string Text;

		public int Cost;

		public int Hate;

		public string Need;

		public string Reply;
	}

	private int _pSocial;

	private int _pSocialTarget;

	private int _pSocialGroup;

	private int _socialFiredAt;

	private readonly Dictionary<int, int> _shookAt = new Dictionary<int, int>();

	private readonly Dictionary<int, long> _gaveAt = new Dictionary<int, long>();

	private bool _socialArmed = true;

	private string _pSocialText;

	internal string SocialNote = "";

	private readonly Dictionary<string, int> _hatCountedDay = new Dictionary<string, int>();

	private readonly Dictionary<int, int> _tippedAt = new Dictionary<int, int>();

	private readonly Dictionary<int, long> _drinkAt = new Dictionary<int, long>();

	private bool _atBar;

	private int _atBarCheckAt;

	private bool _pSocialMounted;

	private bool _hatWorn;

	private bool _hatLogged;

	private int _hatCheckAt;

	private const string HatDict = "ai_gestures@arthur@standing@speaker";

	private const string HatClip = "greet_hat_tip_r_001";

	private int _hatAnimAt;

	private int _hatAnimStage;

	private Ped _hatTarget;

	private bool _hatMoving;

	private bool _hatStopped;

	internal int _socialShownFor;

	private readonly List<Leaving> _leaving = new List<Leaving>();

	private static SocialOption MoneyFor(NemesisRecord r, Personality t)
	{
		if (t == Personality.Proud || (r.Wealth == Wealth.Rich && t != Personality.Greedy))
		{
			return null;
		}
		return new SocialOption
		{
			Key = "money",
			Text = "Give Him a Few Dollars ($5)",
			Cost = 5,
			Hate = ((t == Personality.Proud) ? 3 : (-8)),
			Need = "Money",
			Reply = t switch
			{
				Personality.Greedy => "Five? ...Fine. It's a start.", 
				Personality.Proud => "I don't need your charity.", 
				_ => "...That's decent of you. Real decent.", 
			}
		};
	}

	private SocialOption SocialFor(NemesisRecord r)
	{
		if (r == null || Gangs.Orders(r))
		{
			return null;
		}
		Personality personality = r.Personality ?? Personality.Cold;
		SocialOption socialOption = MoneyFor(r, personality);
		if (socialOption != null)
		{
			return socialOption;
		}
		if (Quirks.Has(r, "drunk") || Mind.N(r, "Drink") >= 60)
		{
			return new SocialOption
			{
				Key = "drink",
				Text = "Buy Him a Drink ($2)",
				Cost = 2,
				Hate = -6,
				Need = "Drink",
				Reply = ((personality == Personality.Proud) ? "...I'll drink it. Don't mean we're friends." : "Well now. Don't mind if I do.")
			};
		}
		SocialOption socialOption2 = MoneyFor(r, personality);
		if (socialOption2 != null)
		{
			return socialOption2;
		}
		if (personality == Personality.Proud)
		{
			return new SocialOption
			{
				Key = "respect",
				Text = "Show Him Respect",
				Hate = -6,
				Reply = "...Hm. At least you know who you're talking to."
			};
		}
		if (r.TraitsRolled && r.Sociability == Sociability.Outgoing)
		{
			return new SocialOption
			{
				Key = "smoke",
				Text = "Share a Smoke",
				Hate = -4,
				Need = "Company",
				Reply = "Obliged. ...You're alright, for a son of a bitch."
			};
		}
		return new SocialOption
		{
			Key = "shake",
			Text = "Shake His Hand",
			Hate = -3,
			Reply = ((personality == Personality.Hothead) ? "...Fine. Don't make me regret it." : "Alright. We're square, for now.")
		};
	}

	private unsafe (Ped ped, NemesisRecord rec, Provocation p) SocialTarget(Ped player)
	{
		if (_convo != null || _interro != null || PLAYER.IS_PLAYER_FREE_AIMING(Game.Player.Handle))
		{
			return (ped: null, rec: null, p: null);
		}
		int num = 0;
		if (!PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(Game.Player.Handle, &num, p2: false, p3: false) || num == 0)
		{
			return (ped: null, rec: null, p: null);
		}
		Ped ped = Entity.FromHandle(num) as Ped;
		if (ped == null || !ped.Exists() || ped.IsDead || !ped.IsHuman || ped.IsPlayer || ped.IsHogtied || Kids.IsChild(ped))
		{
			return (ped: null, rec: null, p: null);
		}
		if (ped.Position.DistanceTo(player.Position) > 6f || PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle))
		{
			return (ped: null, rec: null, p: null);
		}
		if (GameEventPed(ped) || IsStoryPed(ped) || GameOwned(ped))
		{
			return (ped: null, rec: null, p: null);
		}
		if (_lastPromptPed != null && _lastPromptPed.Handle == ped.Handle && Game.GameTime - _lastPromptAt < 800)
		{
			return (ped: null, rec: null, p: null);
		}
		NemesisRecord nemesisRecord = RecordAt(ped.Handle);
		if (nemesisRecord != null)
		{
			if (_enc != null && _enc.Engaged && ped == _activeNemesisPed)
			{
				return (ped: null, rec: null, p: null);
			}
			if (_townMen.Any((TownMan t) => t.Ped == ped && t.Engaged))
			{
				return (ped: null, rec: null, p: null);
			}
			return (ped: ped, rec: nemesisRecord, p: null);
		}
		if (_provoked.TryGetValue(ped.Handle, out var value) && value.Record == null && value.Score > 0f && !value.Settled)
		{
			return (ped: ped, rec: value.Shadow, p: value);
		}
		if (_named.TryGetValue(ped.Handle, out var value2) && value2.Met && !value2.Story && !IsOurs(ped.Handle) && !IsLawman(ped) && !GameOwned(ped))
		{
			return (ped: ped, rec: ShadowOf(ped), p: null);
		}
		return (ped: null, rec: null, p: null);
	}

	private void UpdateSocial(Ped player)
	{
		try
		{
			UpdateHatAnim(player);
		}
		catch (Exception ex)
		{
			_hatAnimStage = 0;
			GrudgeLog("SOCIAL hat tip failed: " + ex.Message);
		}
		(Ped ped, NemesisRecord rec, Provocation p) tuple = SocialTarget(player);
		Ped item = tuple.ped;
		NemesisRecord item2 = tuple.rec;
		Provocation item3 = tuple.p;
		bool flag = item != null && item3 == null && item2 != null && item2.Id == null;
		long value;
		bool flag2 = flag && _gaveAt.TryGetValue(item.Handle, out value) && GameMinutesNow() - value < 1440 && GameMinutesNow() >= value;
		if (flag && _named.TryGetValue(item.Handle, out var value2))
		{
			_ = value2.Name;
		}
		SocialOption socialOption = ((flag && !flag2 && item2.Wealth != Wealth.Rich) ? new SocialOption
		{
			Key = "money",
			Text = "Give Him a Few Dollars ($5)",
			Cost = 5,
			Hate = 0,
			Reply = "...That's kind of you, mister. Real kind."
		} : (flag ? new SocialOption
		{
			Key = "shake",
			Text = "Shake His Hand",
			Hate = 0,
			Reply = "Good to see you."
		} : ((item2 != null) ? SocialFor(item2) : null)));
		if (socialOption != null && socialOption.Key != "money")
		{
			socialOption = null;
		}
		long num = GameMinutesNow();
		long num2 = item3?.SocialDoneAt ?? item2?.SocialDoneAt ?? 0;
		if (socialOption != null && item2 != null && !flag && num2 > 0 && num - num2 < 1440 && num >= num2)
		{
			socialOption = null;
		}
		bool flag3 = player.IsOnMount || PED.GET_MOUNT(player.Handle) != 0;
		if (socialOption != null && socialOption.Key == "money" && flag3)
		{
			socialOption = null;
		}
		if (socialOption == null && item != null && !flag3 && AtABar(player) && (!_drinkAt.TryGetValue(item.Handle, out var value3) || GameMinutesNow() - value3 >= 1440 || GameMinutesNow() < value3))
		{
			socialOption = new SocialOption
			{
				Key = "drink",
				Text = "Buy Him a Drink ($2)",
				Cost = 2,
				Hate = -6,
				Need = "Drink",
				Reply = (((item2?.Personality ?? Personality.Cold) == Personality.Proud) ? "...I'll drink it. Don't mean we're friends." : "Well now. Don't mind if I do.")
			};
		}
		bool flag4 = false;
		if (socialOption != null && socialOption.Key == "money" && item != null && PlayerWearsHat(player) && (!_tippedAt.TryGetValue(item.Handle, out var value4) || Game.GameTime - value4 >= 180000) && (uint)Seeds.Stable($"mix/{item.Handle}/{GameDate.TodayKey()}") % 2u == 0)
		{
			socialOption = null;
		}
		if (socialOption == null && item != null && PlayerWearsHat(player) && (!_tippedAt.TryGetValue(item.Handle, out var value5) || Game.GameTime - value5 >= 180000))
		{
			socialOption = new SocialOption
			{
				Key = "hat",
				Text = "Tip Your Hat",
				Hate = -1
			};
			flag4 = true;
		}
		long num3 = GameMinutesNow();
		long num4 = item3?.SocialDoneAt ?? item2?.SocialDoneAt ?? 0;
		if (flag && _shookAt.TryGetValue(item.Handle, out var value6) && Game.GameTime - value6 < 300000)
		{
			num4 = num3;
		}
		bool flag5 = !flag4 && item2 != null && num4 > 0 && num3 - num4 < 1440 && num3 >= num4;
		if (item == null || socialOption == null || flag5)
		{
			HideSocialPrompt();
			SocialNote = ((item == null) ? "" : ((socialOption == null) ? "no option for him" : "used today"));
			return;
		}
		if (!PED.IS_PED_MALE(item.Handle))
		{
			socialOption = Her(socialOption);
		}
		bool flag6 = socialOption.Cost == 0 || CashDollars >= socialOption.Cost;
		int num5 = HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(item.Handle);
		if (_pSocial == 0 || !PromptValid(_pSocial) || _pSocialTarget != item.Handle || _pSocialText != socialOption.Text || _pSocialGroup != num5 || _pSocialMounted != flag3)
		{
			HideSocialPrompt();
			Crumbs.Native("Social prompt delete");
			if (PromptValid(_pSocial))
			{
				HUD._UI_PROMPT_DELETE(_pSocial);
			}
			_pSocial = MakePrompt(flag3 ? eInputType.FrontendUp : eInputType.InteractOption1, socialOption.Text, hold: true, num5);
			_pSocialMounted = flag3;
			_pSocialTarget = item.Handle;
			_pSocialText = socialOption.Text;
			_pSocialGroup = num5;
			GrudgeLog(string.Format("SOCIAL prompt '{0}' on {1} (ped {2})", socialOption.Text, (item3 != null) ? "stranger" : ((!string.IsNullOrEmpty(item2.Name)) ? item2.Name : (_named.TryGetValue(item.Handle, out var value7) ? value7.Name : "someone you've met")), item.Handle));
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pSocial, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pSocial, flag6);
		_socialShownFor = item.Handle;
		SocialNote = socialOption.Text + " on " + ((item3 != null) ? "a stranger" : item2.Name) + (flag6 ? "" : " (no money)");
		if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(_pSocial))
		{
			_socialArmed = true;
		}
		else if (_socialArmed && Game.GameTime - _socialFiredAt >= 1200)
		{
			_socialArmed = false;
			_socialFiredAt = Game.GameTime;
			DoSocial(item, item2, item3, socialOption, player);
		}
	}

	private bool AtABar(Ped player)
	{
		if (Game.GameTime < _atBarCheckAt)
		{
			return _atBar;
		}
		_atBarCheckAt = Game.GameTime + 1000;
		int atBar;
		if (INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) != 0)
		{
			Ped ped = NearestBartender(player);
			if ((object)ped != null)
			{
				atBar = ((ped.Position.DistanceTo(player.Position) < 20f) ? 1 : 0);
				goto IL_005c;
			}
		}
		atBar = 0;
		goto IL_005c;
		IL_005c:
		_atBar = (byte)atBar != 0;
		return _atBar;
	}

	private bool PlayerWearsHat(Ped player)
	{
		if (Game.GameTime < _hatCheckAt)
		{
			return _hatWorn;
		}
		_hatCheckAt = Game.GameTime + 1000;
		bool flag = false;
		List<string> list = new List<string>();
		try
		{
			uint num = MISC.GET_HASH_KEY("HATS");
			int num2 = Function.Call<int>(11971384123627577485uL, new InputArgument[1] { player.Handle });
			for (int i = 0; i < num2 && i < 48; i++)
			{
				uint num3 = Function.Call<uint>(14751957644553774639uL, new InputArgument[2] { player.Handle, i });
				if (num3 == num)
				{
					flag = true;
				}
				list.Add(CategoryNames.TryGetValue(num3, out var value) ? value : $"{num3:X8}");
			}
			int num4 = Function.Call<int>(10394376663872179841uL, new InputArgument[1] { player.Handle });
			for (int j = 0; j < num4 && j < 64; j++)
			{
				if (flag)
				{
					break;
				}
				if (Function.Call<uint>(11209604758464182439uL, new InputArgument[3] { player.Handle, j, 0 }) == num)
				{
					flag = true;
				}
			}
		}
		catch
		{
		}
		if (flag != _hatWorn || !_hatLogged)
		{
			_hatLogged = true;
			GrudgeLog("SOCIAL hat check: " + (flag ? "wearing a hat" : "no hat") + " (categories: " + string.Join(", ", list.Take(20)) + ")");
		}
		return _hatWorn = flag;
	}

	private void TipYourHat(Ped ped, NemesisRecord rec, Provocation prov, Ped player)
	{
		_tippedAt[ped.Handle] = Game.GameTime;
		_hatAnimAt = Game.GameTime;
		_hatAnimStage = 1;
		_hatTarget = ped;
		Personality personality = rec?.Personality ?? Personality.Cold;
		if (personality != Personality.Proud && personality != Personality.Hothead)
		{
			_rng.Next(2);
		}
		Named value;
		string text = (_named.TryGetValue(ped.Handle, out value) ? value.Name : (rec?.Name ?? "the stranger"));
		string key = rec?.Id ?? ((value != null) ? (value.Name + "|" + ped.Model.Hash) : ("h" + ped.Handle));
		int num = GameDate.TodayKey();
		if (_hatCountedDay.TryGetValue(key, out var value2) && value2 == num)
		{
			GrudgeLog("SOCIAL hat tip to " + text + " - already counted today (no effect)");
			return;
		}
		_hatCountedDay[key] = num;
		if (prov != null)
		{
			float score = prov.Score;
			bool flag = (float)(Game.GameTime - prov.LastHarmAt) < Balance.GsKindWaitMin * 60000f;
			if (!flag)
			{
				prov.Score = Math.Max(0f, prov.Score - 2f);
			}
			prov.Ledger.Add(flag ? "hat tip - too soon after what you did" : "hat tip -2");
			Feed($"SOCIAL you tipped your hat to {text}: grudge score {score:0} -> {prov.Score:0}", Theme.Good);
		}
		else if (rec != null && rec.Id != null)
		{
			int hate = rec.Hate;
			SoftenHate(rec, 1);
			Feed("SOCIAL you tipped your hat to " + rec.Name + ": hate " + Delta(hate, rec.Hate), Theme.Good, rec);
			SaveRoster();
		}
		else
		{
			LikeBy(ped.Handle, 2, "you tipped your hat");
			Feed("SOCIAL you tipped your hat to " + text, Theme.Good);
		}
		GrudgeLog("SOCIAL hat tip to " + text + (player.IsOnMount ? " from the saddle" : ""));
	}

	private void UpdateHatAnim(Ped player)
	{
		if (_hatAnimStage == 0)
		{
			return;
		}
		if (_hatAnimStage == 1)
		{
			if (!STREAMING.DOES_ANIM_DICT_EXIST("ai_gestures@arthur@standing@speaker"))
			{
				_hatAnimStage = 0;
				GrudgeLog("SOCIAL hat tip: no ai_gestures@arthur@standing@speaker");
				return;
			}
			Crumbs.Native("Social hat dict");
			STREAMING.REQUEST_ANIM_DICT("ai_gestures@arthur@standing@speaker");
			if (!STREAMING.HAS_ANIM_DICT_LOADED("ai_gestures@arthur@standing@speaker"))
			{
				if (Game.GameTime - _hatAnimAt > 2500)
				{
					_hatAnimStage = 0;
					GrudgeLog("SOCIAL hat tip: the clip didn't load");
				}
				return;
			}
			bool flag = ENTITY.GET_ENTITY_SPEED(player.Handle) > 0.3f && !player.IsOnMount;
			if (flag && !_hatStopped)
			{
				_hatStopped = true;
				Crumbs.Native("Social hat stand still");
				TASK.TASK_STAND_STILL(player.Handle, 1000);
			}
			else if (!flag || Game.GameTime - _hatAnimAt >= 1500)
			{
				Crumbs.Native("Social hat clip");
				TASK.TASK_PLAY_ANIM(player.Handle, "ai_gestures@arthur@standing@speaker", "greet_hat_tip_r_001", 8f, -4f, -1, 48, 0f, p8: false, 0, p10: false, "", p12: false);
				_hatMoving = _hatStopped;
				_hatStopped = false;
				_hatAnimStage = 2;
				_hatAnimAt = Game.GameTime;
			}
		}
		else
		{
			if (Game.GameTime - _hatAnimAt < 300)
			{
				return;
			}
			bool flag2 = ENTITY.IS_ENTITY_PLAYING_ANIM(player.Handle, "ai_gestures@arthur@standing@speaker", "greet_hat_tip_r_001", 3);
			bool flag3 = player.IsOnMount || PED.GET_MOUNT(player.Handle) != 0;
			if (!flag2 && _hatAnimStage < 4)
			{
				Crumbs.Native("Social hat clip (again)");
				TASK.TASK_PLAY_ANIM(player.Handle, "ai_gestures@arthur@standing@speaker", (_hatAnimStage == 3) ? "greet_hat_tip_l_001" : "greet_hat_tip_r_001", 8f, -4f, -1, 48, 0f, p8: false, 0, p10: false, "", p12: false);
				_hatAnimStage++;
				_hatAnimAt = Game.GameTime;
				return;
			}
			if (!flag2 && flag3)
			{
				Crumbs.Native("Social hat emote");
				Function.Call(12905671096162301951uL, player.Handle, 3, 0, 2837946383u, true, true, false, true, false);
			}
			GrudgeLog("SOCIAL hat tip" + (_hatMoving ? " [stopped him first]" : "") + ": " + (flag2 ? $"playing (try {_hatAnimStage - 1})" : (flag3 ? "the clip did NOT play in the saddle - the emote tried" : "did NOT play in 3 tries")) + (flag3 ? " (in the saddle)" : "") + ((ENTITY.GET_ENTITY_SPEED(player.Handle) > 0.5f) ? " (on the move)" : ""));
			_hatAnimStage = 0;
		}
	}

	private void DoSocial(Ped ped, NemesisRecord rec, Provocation prov, SocialOption opt, Ped player)
	{
		if (opt.Key == "hat")
		{
			TipYourHat(ped, rec, prov, player);
			return;
		}
		if (opt.Cost > 0)
		{
			if (CashDollars < opt.Cost)
			{
				return;
			}
			MONEY._MONEY_DECREMENT_CASH_BALANCE(opt.Cost * 100);
		}
		string text = opt.Reply;
		int num = opt.Hate;
		Named value;
		string text2 = (_named.TryGetValue(ped.Handle, out value) ? value.Name : (rec?.Name ?? "The stranger"));
		if (opt.Key == "money" || opt.Key == "drink")
		{
			GesturePay(ped, youPay: true);
		}
		if (opt.Key == "money")
		{
			Acquaintance value2;
			NemesisRecord nemesisRecord = rec ?? (_acqOf.TryGetValue(ped.Handle, out value2) ? PersonRecord(value2) : null) ?? prov?.Shadow;
			if (nemesisRecord != null && nemesisRecord.TraitsRolled && nemesisRecord.Wealth == Wealth.Poor)
			{
				GoodDeed(ped.Position, "you gave a poor " + (PED.IS_PED_MALE(ped.Handle) ? "man" : "woman") + " a few dollars", 0.6f, "charity");
			}
			else
			{
				GrudgeLog("SOCIAL money to " + text2 + ": not charity in the town's eyes - " + ((nemesisRecord == null || !nemesisRecord.TraitsRolled) ? "nothing known of his means" : ("he's " + nemesisRecord.Wealth.ToString().ToLowerInvariant() + ", not poor")));
			}
		}
		if (opt.Key == "smoke")
		{
			ShareSmoke(ped);
		}
		if (opt.Key == "shake" || opt.Key == "respect")
		{
			float num2 = prov?.Score ?? ((float)((rec != null && rec.Id != null) ? rec.Hate : 0));
			string text3 = RefusesHand(rec?.Personality, rec, num2);
			if (text3 != null)
			{
				ShowSubtitle(text2 + ": \"" + text3 + "\"");
				if (rec != null && rec.Personality == Personality.Proud && prov != null)
				{
					prov.Score = Math.Min(100f, prov.Score + 3f);
					prov.Ledger.Add("refused your hand +3");
				}
				if (prov != null)
				{
					prov.SocialDoneAt = GameMinutesNow();
				}
				else if (rec != null && rec.Id != null)
				{
					rec.SocialDoneAt = GameMinutesNow();
				}
				_shookAt[ped.Handle] = Game.GameTime;
				GrudgeLog($"GESTURE {text2} refused your hand ({rec?.Personality}, grudge {num2:0})");
				return;
			}
			if (opt.Key == "respect")
			{
				GestureTipHat(ped);
			}
			else
			{
				GestureShake(ped);
			}
			_shookAt[ped.Handle] = Game.GameTime;
		}
		try
		{
			EaseFear(ped, rec, opt.Key, rec?.Personality?.ToString() ?? TemperOf(ped));
		}
		catch (Exception ex)
		{
			GrudgeLog("SOCIAL fear ease failed: " + ex.Message);
		}
		if (opt.Key == "drink")
		{
			_drinkAt[ped.Handle] = GameMinutesNow();
		}
		if (prov == null && rec != null && rec.Id == null && opt.Key == "drink")
		{
			ShowSubtitle(text2 + ": \"" + opt.Reply + "\"");
			LikeBy(ped.Handle, 8, "you bought them a drink");
			Feed("SOCIAL " + text2 + ": you bought " + (PED.IS_PED_MALE(ped.Handle) ? "him" : "her") + " a drink", Theme.Good);
			return;
		}
		if (prov == null && rec != null && rec.Id == null && opt.Key == "money")
		{
			_gaveAt[ped.Handle] = GameMinutesNow();
			ShowSubtitle(text2 + ": \"" + opt.Reply + "\"");
			LikeBy(ped.Handle, 10, "you gave them money");
			Feed("SOCIAL " + text2 + ": you gave " + (PED.IS_PED_MALE(ped.Handle) ? "him" : "her") + " $5", Theme.Good);
			return;
		}
		if (opt.Key == "shake" && prov == null && (rec == null || rec.Id == null))
		{
			ShowSubtitle(text2 + ": \"" + ((rec != null && rec.Personality == Personality.Cold) ? "...Likewise." : ((rec != null && rec.Personality == Personality.Coward) ? "O-oh. Pleasure." : opt.Reply)) + "\"");
			LikeBy(ped.Handle, 5, "shook your hand");
			Feed("SOCIAL " + text2 + ": shook your hand", Theme.Good);
			return;
		}
		if (opt.Key == "cards")
		{
			if (_rng.Next(100) < 50)
			{
				PayPlayer(1000);
				text = "Damn. You're luckier than you look. Again sometime.";
			}
			else
			{
				num--;
				text = "Ha! Better luck next time, friend.";
			}
		}
		if (opt.Key != "smoke")
		{
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(ped.Handle, player.Handle, 2000, 0f, 0f, 0f);
		}
		Say(ped, (num < 0) ? "GREET_GENERAL_FAMILIAR" : "GENERIC_INSULT_HIGH_NEUTRAL", player);
		Named value3;
		string text4 = ((prov == null) ? rec.Name : (_named.TryGetValue(ped.Handle, out value3) ? value3.Name : "the stranger"));
		ShowSubtitle(text4 + ": \"" + text + "\"");
		if (prov != null)
		{
			prov.SocialDoneAt = GameMinutesNow();
			float score = prov.Score;
			float num3 = Math.Max(0f - Balance.GsKindMax, num * 2);
			bool flag = (float)(Game.GameTime - prov.LastHarmAt) < Balance.GsKindWaitMin * 60000f;
			if (flag)
			{
				num3 = 0f;
			}
			prov.Score = Math.Max(0f, prov.Score + num3);
			prov.Ledger.Add(flag ? (opt.Key + " - too soon after what you did") : $"{opt.Key} {num3:+0;-0}");
			Feed($"SOCIAL {text4} ({opt.Key}): grudge score {score:0} -> {prov.Score:0}", Theme.Good);
			RememberPerson(ped.Handle, met: true);
			LikeBy(ped.Handle, 6, "social: " + opt.Key);
			GrudgeLog($"SOCIAL {opt.Key} on stranger {text4}: score {score:0} -> {prov.Score:0}");
			return;
		}
		int hate = rec.Hate;
		if (num < 0)
		{
			SoftenHate(rec, -num);
		}
		else
		{
			rec.Hate = Math.Min(100, hate + num);
			CapScuffle(rec, hate);
		}
		if (opt.Need != null && rec.Needs != null && rec.Needs.ContainsKey(opt.Need))
		{
			rec.Needs[opt.Need] = Math.Max(0, rec.Needs[opt.Need] - 40);
		}
		if (num < 0)
		{
			Remember(rec, "Joy", 30, (opt.Key == "drink") ? "you bought him a drink" : ("you " + opt.Text.ToLowerInvariant()));
		}
		rec.SocialDoneAt = GameMinutesNow();
		rec.Touch();
		History(rec, string.Format("YOU {0} - hate {1} -> {2}", opt.Text.ToUpperInvariant().Replace(" ($2)", "").Replace(" ($5)", ""), hate, rec.Hate), (num < 0) ? Theme.Good : Theme.Warn);
		Feed("SOCIAL " + rec.Name + " (" + opt.Key + "): hate " + Delta(hate, rec.Hate) + ((rec.Hate == SocialFloor(rec) && num < 0) ? $" (won't go under {SocialFloor(rec)})" : ""), (num < 0) ? Theme.Good : Theme.Warn, rec);
		GrudgeLog($"SOCIAL {opt.Key} on {rec.Name}: hate {hate} -> {rec.Hate}");
		SaveRoster();
	}

	private void EaseFear(Ped ped, NemesisRecord rec, string key, string temper)
	{
		float num;
		switch (key)
		{
		default:
			num = 5f;
			break;
		case "shake":
		case "respect":
			num = 6f;
			break;
		case "smoke":
			num = 8f;
			break;
		case "drink":
			num = 10f;
			break;
		case "money":
			num = 12f;
			break;
		}
		float num2 = num;
		float num3;
		switch (temper)
		{
		default:
			num3 = 1f;
			break;
		case "Proud":
			num3 = 0.4f;
			break;
		case "Coward":
		case "Cold":
			num3 = 0.6f;
			break;
		case "Drifter":
			num3 = 1.3f;
			break;
		case "Greedy":
			num3 = ((key == "money") ? 2f : 1.2f);
			break;
		}
		float num4 = num3;
		int num5 = Math.Max(1, (int)Math.Round(num2 * num4));
		_unease.Remove(ped.Handle);
		string text = null;
		Acquaintance value;
		if (rec != null && rec.Id != null)
		{
			int fear = rec.Fear;
			rec.Fear = Math.Max(0, rec.Fear - num5);
			if (rec.Fear != fear)
			{
				text = $"fear {fear} -> {rec.Fear}";
			}
		}
		else if (_acqOf.TryGetValue(ped.Handle, out value))
		{
			int fear2 = value.Fear;
			value.Fear = Math.Max(0, value.Fear - num5);
			if (value.Fear != fear2)
			{
				text = $"fear {fear2} -> {value.Fear}";
				_peopleDirty = true;
			}
		}
		if (text != null)
		{
			Named value2;
			string text2 = (_named.TryGetValue(ped.Handle, out value2) ? value2.Name : (rec?.Name ?? "he"));
			GrudgeLog($"SOCIAL {key} eases {text2}'s fear ({temper}, -{num5}): {text}");
			Feed("SOCIAL " + text2 + " is less afraid of you (" + key + ", " + temper.ToLowerInvariant() + "): " + text, Theme.Good);
		}
	}

	private static SocialOption Her(SocialOption o)
	{
		string text = string.Join(" ", from w in (o.Text ?? "").Split(' ')
			select (!(w == "Him") && !(w == "His")) ? w : "Her");
		string reply = (o.Reply ?? "").Replace("mister", "mister").Replace("My youngest is walking now", "My youngest is walking now");
		return new SocialOption
		{
			Key = o.Key,
			Text = text,
			Cost = o.Cost,
			Hate = o.Hate,
			Need = o.Need,
			Reply = reply
		};
	}

	private void HideSocialPrompt()
	{
		_socialShownFor = 0;
		if (_pSocial != 0)
		{
			if (!PromptValid(_pSocial))
			{
				_pSocial = 0;
				_pSocialTarget = 0;
			}
			else
			{
				HUD._UI_PROMPT_SET_VISIBLE(_pSocial, toggle: false);
				HUD._UI_PROMPT_SET_ENABLED(_pSocial, toggle: false);
			}
		}
	}

	internal void DropSocialPrompt()
	{
		Crumbs.Native("Social prompt delete");
		if (PromptValid(_pSocial))
		{
			HUD._UI_PROMPT_DELETE(_pSocial);
		}
		_pSocial = 0;
		_pSocialTarget = 0;
		_pSocialText = null;
	}
}
