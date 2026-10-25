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
	private readonly Dictionary<string, (int n, DateTime last)> _greetStreak = new Dictionary<string, (int, DateTime)>();

	private int _lastGreetDrop;

	private HisPeopleHold _hisPeople;

	private readonly List<(int h, int at, Raid patrol)> _lowerGuns = new List<(int, int, Raid)>();

	private void PruneProvocations()
	{
		if (Game.GameTime < _nextPruneTime)
		{
			return;
		}
		_nextPruneTime = Game.GameTime + 10000;
		Downed.Prune();
		foreach (int item in _nemesisBodies.Keys.ToList())
		{
			if (!(Entity.FromHandle(item) is Ped ped) || !ped.Exists())
			{
				_nemesisBodies.Remove(item);
			}
		}
		foreach (int item2 in (from kv in _provoked
			where (kv.Value.Record == null && Game.GameTime - kv.Value.LastTime > 600000) || !ENTITY.DOES_ENTITY_EXIST(kv.Key)
			select kv.Key).ToList())
		{
			_provoked.Remove(item2);
		}
		foreach (int item3 in _knockedOutWorld.Where(Gone).ToList())
		{
			_knockedOutWorld.Remove(item3);
		}
		foreach (int item4 in _restrainedAt.Keys.Where((int h) => Gone(h) || Game.GameTime - _restrainedAt[h] > 60000).ToList())
		{
			_restrainedAt.Remove(item4);
		}
		foreach (int item5 in _robbedAt.Keys.Where((int h) => Gone(h) || Game.GameTime - _robbedAt[h] > 60000).ToList())
		{
			_robbedAt.Remove(item5);
		}
		foreach (string item6 in _lastKoAt.Keys.Where((string k) => Game.GameTime - _lastKoAt[k] > 300000).ToList())
		{
			_lastKoAt.Remove(item6);
		}
		foreach (int item7 in _combatants.Keys.Where(Gone).ToList())
		{
			_combatants.Remove(item7);
			_shotAtAt.Remove(item7);
		}
		if (_errorLoggedAt.Count > 200)
		{
			_errorLoggedAt.Clear();
		}
		static bool Gone(int h)
		{
			return !ENTITY.DOES_ENTITY_EXIST(h);
		}
	}

	private void NemesisProvoked(bool insult)
	{
		if (Game.GameTime - _lastNemesisProvokeTime < 1500)
		{
			return;
		}
		_lastNemesisProvokeTime = Game.GameTime;
		NemesisRecord activeRecord = ActiveRecord;
		int hate = activeRecord.Hate;
		if (!insult && StandoffGreeted(activeRecord))
		{
			return;
		}
		if (insult && _enc != null && _enc.StandoffUntil > 0)
		{
			_enc.StandoffUntil = -1;
		}
		if (insult && _enc != null && !_enc.Engaged && !_enc.Surrendered && _activeNemesisPed != null && _enc.EngageAt == 0)
		{
			_enc.Spotted = true;
			_enc.EngageAt = Game.GameTime + 2200;
			GrudgeLog("ENCOUNTER " + activeRecord.Name + " antagonized - words first, the fight in 2 s");
		}
		if (insult)
		{
			activeRecord.TimesAntagonized++;
			activeRecord.Fury++;
			activeRecord.AddHate(5);
			CapScuffle(activeRecord, hate);
			if (activeRecord.Fury >= 3)
			{
				activeRecord.Fury = 0;
				activeRecord.AddHate(5);
				CapScuffle(activeRecord, hate);
				ShowSubtitle(activeRecord.Name + " (enraged): \"That's it. No more talkin'.\"");
			}
			else
			{
				ShowSubtitle(activeRecord.Name + ": \"" + Taunts.AntagonizeReply(_rng) + "\"");
			}
			SaveRoster();
			Feed(insult ? string.Format("ANTAGONIZE {0} (fighting you): hate {1}  fury {2}/{3}{4}", activeRecord.Name, Delta(hate, activeRecord.Hate), activeRecord.Fury, 3, (activeRecord.Fury == 0) ? "  -> ENRAGED" : "") : $"GREET {activeRecord.Name} (fighting you): hate {Delta(hate, activeRecord.Hate)}  fury {activeRecord.Fury}/{3}", insult ? Theme.Warn : Theme.Good, activeRecord);
		}
		else
		{
			activeRecord.TimesGreeted++;
			activeRecord.Fury = Math.Max(0, activeRecord.Fury - 1);
			ApplyToRecord(activeRecord, GrudgeAction.Greet);
			ShowSubtitle(activeRecord.Name + ": \"" + Taunts.GreetReply(_rng) + "\"");
		}
	}

	private unsafe void DetectAntagonizeFallback()
	{
		if (_enc == null || _enc.Engaged)
		{
			return;
		}
		int num = 0;
		bool num2 = PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(Game.Player.Handle, &num, p2: false, p3: false) && num == _activeNemesisPed.Handle;
		bool flag = PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, _activeNemesisPed.Handle);
		if (!num2 && !flag)
		{
			return;
		}
		if (PanelButtonsHeld)
		{
			if (JustPressed(eInputType.InteractLockonNeg) || JustPressed(eInputType.InteractLockonPos))
			{
				GrudgeLog("PROVOKE a press on him ignored - the questioning panel's buttons");
			}
		}
		else if (JustPressed(eInputType.InteractLockonNeg))
		{
			NemesisProvoked(insult: true);
		}
		else if (JustPressed(eInputType.InteractLockonPos))
		{
			NemesisProvoked(insult: false);
		}
	}

	private static bool JustPressed(eInputType input)
	{
		if (!Game.IsControlJustPressed(input))
		{
			return Game.IsDisabledControlJustPressed(input);
		}
		return true;
	}

	internal static float WeightOf(GrudgeAction a)
	{
		return a switch
		{
			GrudgeAction.Greet => Balance.GsGreet, 
			GrudgeAction.EmoteGreet => Balance.GsGreet * 0.66f, 
			GrudgeAction.Defuse => Balance.GsDefuse, 
			GrudgeAction.Insult => Balance.GsInsult, 
			GrudgeAction.Taunt => Balance.GsInsult * 0.55f, 
			GrudgeAction.DrawOn => Balance.GsDrawOn, 
			GrudgeAction.Intimidate => Balance.GsIntimidate, 
			GrudgeAction.StickUp => Balance.GsStickUp, 
			GrudgeAction.StickUpCalm => Balance.GsStickUp * 0.7f, 
			GrudgeAction.Punch => Balance.GsPunch, 
			GrudgeAction.KnockOut => Balance.GsKnockOut, 
			GrudgeAction.Hogtie => Balance.GsHogtie, 
			GrudgeAction.Robbed => Balance.GsRobbed, 
			GrudgeAction.HorseStolen => Balance.GsHorseStolen, 
			GrudgeAction.WagonStolen => Balance.GsHorseStolen * 0.875f, 
			GrudgeAction.Slashed => Balance.GsSlashed, 
			GrudgeAction.HorseKilled => Balance.GsSlashed, 
			GrudgeAction.Wounded => Balance.GsWounded, 
			GrudgeAction.FoughtBack => Balance.GsFoughtBack, 
			GrudgeAction.ShotAt => Balance.GsShotAt, 
			GrudgeAction.ComradeKilled => Balance.GsComradeKilled, 
			GrudgeAction.FriendInsulted => 5f, 
			GrudgeAction.FriendHurt => 22f, 
			GrudgeAction.FriendKilled => 65f, 
			GrudgeAction.FriendThreatened => 12f, 
			GrudgeAction.Bumped => 10f, 
			_ => 0f, 
		};
	}

	private static bool Social(GrudgeAction a)
	{
		return a <= GrudgeAction.StickUpCalm;
	}

	private static (float mult, string why) FriendMult(Provocation p)
	{
		string item = p.Temper.ToString().ToLowerInvariant();
		float num;
		switch (p.Temper)
		{
		case Personality.Loyal:
			num = 1.4f;
			break;
		case Personality.Hothead:
			num = 1.3f;
			break;
		case Personality.Proud:
			num = 1.2f;
			break;
		case Personality.Greedy:
			num = 0.9f;
			break;
		case Personality.Cold:
		case Personality.Drifter:
			num = 0.8f;
			break;
		case Personality.Coward:
			num = 0.6f;
			break;
		default:
			num = 1f;
			break;
		}
		if (p.Shadow != null && p.Shadow.TraitsRolled)
		{
			num *= 0.9f + (float)p.Shadow.Aggression / 500f;
		}
		return (mult: num, why: item);
	}

	internal static float TemperMult(Personality t, GrudgeAction a)
	{
		bool flag = a == GrudgeAction.Insult || a == GrudgeAction.Taunt;
		bool flag2 = a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet || a == GrudgeAction.Defuse;
		bool flag3 = flag || a == GrudgeAction.KnockOut || a == GrudgeAction.Hogtie || a == GrudgeAction.Punch;
		bool flag4 = a == GrudgeAction.Robbed || a == GrudgeAction.StickUp || a == GrudgeAction.StickUpCalm || a == GrudgeAction.HorseStolen || a == GrudgeAction.WagonStolen;
		switch (t)
		{
		case Personality.Proud:
			if (!flag3)
			{
				if (!flag2)
				{
					return 1f;
				}
				return 0.7f;
			}
			return 1.5f;
		case Personality.Hothead:
			if (!flag2)
			{
				return 1.3f;
			}
			return 0.5f;
		case Personality.Greedy:
			if (!flag4)
			{
				if (!flag2)
				{
					return 0.85f;
				}
				return 1f;
			}
			return 1.6f;
		case Personality.Coward:
			if (!flag2)
			{
				if (!flag)
				{
					return 0.8f;
				}
				return 0.6f;
			}
			return 1.5f;
		case Personality.Drifter:
			if (!flag2)
			{
				return 0.7f;
			}
			return 1.5f;
		case Personality.Cold:
			if (!flag2)
			{
				return 0.9f;
			}
			return 0.8f;
		default:
			return 1f;
		}
	}

	internal void CrimeSeen(int handle, string crime)
	{
		if (crime == null)
		{
			return;
		}
		Ped ped = Entity.FromHandle(handle) as Ped;
		try
		{
			Witness(crime, handle, (ped != null && ped.Exists()) ? ped.Position : Game.Player.Character.Position);
		}
		catch (Exception ex)
		{
			GrudgeLog("WITNESS failed: " + ex.Message);
		}
		bool flag = false;
		try
		{
			flag = PlayerMasked(Game.Player.Character);
		}
		catch
		{
		}
		if (flag)
		{
			return;
		}
		try
		{
			AffiliateWronged(handle, crime);
		}
		catch
		{
		}
		if (crime == "punch")
		{
			long num = GameMinutesNow();
			if (_beatCounted.TryGetValue(handle, out var value) && num >= value && num - value < 5)
			{
				_beatCounted[handle] = num;
				return;
			}
			_beatCounted[handle] = num;
		}
		CountYourCrime(crime);
	}

	private void Grudge(int handle, Provocation p, GrudgeAction a)
	{
		if (p == null)
		{
			return;
		}
		if (p.Record == null)
		{
			try
			{
				Liking(handle, a);
			}
			catch
			{
			}
		}
		if (p.Record == null && !Social(a) && a < GrudgeAction.FriendInsulted)
		{
			try
			{
				LinkGroupOnce(handle);
			}
			catch
			{
			}
		}
		if (a == GrudgeAction.FriendKilled)
		{
			p.FriendKilled = true;
		}
		if (a > GrudgeAction.Defuse && a < GrudgeAction.FriendInsulted)
		{
			NoDeed(handle, $"you dealt with them badly ({a})");
		}
		CrimeSeen(handle, CrimeOf(a));
		if (a < GrudgeAction.FriendInsulted || a == GrudgeAction.Bumped)
		{
			NoteDealt(handle);
		}
		if (p.Record != null)
		{
			p.LastActionAt = Game.GameTime;
			ApplyToRecord(p.Record, a);
			return;
		}
		float score = p.Score;
		float num;
		string text;
		if (p.Shadow != null)
		{
			(num, text) = ReactionMult(p.Shadow, a);
		}
		else
		{
			float num2 = TemperMult(p.Temper, a);
			string text2 = p.Temper.ToString().ToLowerInvariant();
			text = text2;
			num = num2;
		}
		if (a == GrudgeAction.FriendKilled || a == GrudgeAction.FriendHurt || a == GrudgeAction.FriendThreatened)
		{
			(num, text) = FriendMult(p);
		}
		float num3 = (float)Math.Round(((a == GrudgeAction.Wounded && p.Wounded) ? Balance.GsShotAgain : WeightOf(a)) * num);
		bool flag = a == GrudgeAction.Wounded || a == GrudgeAction.Slashed || a == GrudgeAction.HorseKilled || a == GrudgeAction.KnockOut || a == GrudgeAction.Robbed || a == GrudgeAction.FriendKilled;
		if (num3 > 0f && !flag)
		{
			Ped ped = Entity.FromHandle(handle) as Ped;
			float num4 = ((ped != null && ped.Exists()) ? RepAt(ped.Position) : 0f);
			if (num4 >= 1f)
			{
				float num5 = RepGrudgeMult(num4, p.Temper.ToString());
				num3 = (float)Math.Round(num3 * num5);
				text += $", infamy x{num5:0.00}";
			}
			string town = ((ped != null && ped.Exists()) ? (_currentTown ?? TownAt(ped.Position)) : null);
			float num6 = GoodRepGrudgeMult(town);
			if (num6 < 0.999f)
			{
				num3 = (float)Math.Round(num3 * num6);
				text += $", good name x{num6:0.00}";
			}
		}
		p.Score = Math.Max(0f, Math.Min(100f, p.Score + num3));
		bool flag2 = p.Wounded || p.Slashed || p.KnockedOut || p.Robbed || p.Kidnapped || p.HorseKilled || p.HorseStolen || p.WagonStolen || p.FriendKilled || p.Hits > 0;
		bool flag3 = a == GrudgeAction.ShotAt || a == GrudgeAction.ComradeKilled || a == GrudgeAction.FoughtBack;
		if (flag3 && !flag2 && p.Score > Balance.GsUnhitCap)
		{
			p.Score = Math.Max(score, Balance.GsUnhitCap);
			text += ", never hit - capped";
		}
		bool flag4 = p.Slashed || p.KnockedOut || p.Robbed || p.Kidnapped || p.HorseKilled || p.HorseStolen || p.WagonStolen || p.FriendKilled || p.Hits > 0;
		if ((p.Wounded || a == GrudgeAction.Wounded) && p.ShotAgainCount == 0 && !flag4 && (a == GrudgeAction.Wounded || flag3) && p.Score > Balance.GsWoundCap)
		{
			p.Score = Math.Max(score, Balance.GsWoundCap);
			text += ", one wound - a roll";
		}
		p.LastActionAt = Game.GameTime;
		if (num3 > 0f && !Social(a))
		{
			p.LastHarmAt = Game.GameTime;
		}
		if (num3 > 0f && (!p.Settled || p.Score - p.SettledScore >= Balance.GsReopen))
		{
			p.Settled = false;
		}
		if (a == GrudgeAction.Insult || a == GrudgeAction.Taunt)
		{
			p.Insults++;
			p.Insulted = true;
		}
		if (a == GrudgeAction.StickUp || a == GrudgeAction.StickUpCalm || a == GrudgeAction.Intimidate || a == GrudgeAction.DrawOn)
		{
			p.Threatened = true;
		}
		if (a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet)
		{
			p.Greetings++;
		}
		p.Ledger.Add(string.Format("{0}{1:0} {2}", (num3 >= 0f) ? "+" : "", num3, ActionName(a)));
		if (p.Ledger.Count > 6)
		{
			p.Ledger.RemoveAt(0);
		}
		Feed(Pron.Her(string.Format("{0} {1}: grudge {2:0} -> {3:0}  (x{4:0.00} {5}){6}", ActionName(a).ToUpperInvariant(), WhoIs(handle), score, p.Score, num, text, (p.Score >= (float)Balance.GsThreshold) ? "  NEMESIS LINE" : ""), Pron.FemaleAt(handle)), (num3 < 0f) ? Theme.Good : ((p.Score >= (float)Balance.GsThreshold) ? Theme.Blood : Theme.Warn));
	}

	private (int n, float bonus, string note) GreetStreak(NemesisRecord rec, GrudgeAction a)
	{
		if (rec.Id == null || (a != GrudgeAction.Greet && a != GrudgeAction.EmoteGreet && a != GrudgeAction.Defuse))
		{
			return (n: 0, bonus: 0f, note: null);
		}
		DateTime now = DateTime.Now;
		_greetStreak.TryGetValue(rec.Id, out (int, DateTime) value);
		if (value.Item1 > 0 && (now - value.Item2).TotalSeconds < 1.5)
		{
			return (n: -1, bonus: 0f, note: null);
		}
		int num = ((value.Item1 <= 0 || !((now - value.Item2).TotalMinutes < (double)Balance.GreetStreakMinutes)) ? 1 : (value.Item1 + 1));
		_greetStreak[rec.Id] = (num, now);
		if (num == 3)
		{
			return (n: num, bonus: Balance.GreetStreakBonus, note: "3rd greet - warming to you");
		}
		if (num > 6)
		{
			return (n: num, bonus: 0f, note: "greeted to death - nothing more");
		}
		if (num > 3)
		{
			return (n: num, bonus: 0f, note: $"greet {num} - worth less now");
		}
		return (n: num, bonus: 0f, note: (num == 2) ? "2nd greet" : null);
	}

	private void ApplyToRecord(NemesisRecord rec, GrudgeAction a)
	{
		switch (a)
		{
		case GrudgeAction.Insult:
		case GrudgeAction.Taunt:
			HiddenSlip(rec, "antagonize");
			break;
		case GrudgeAction.DrawOn:
		case GrudgeAction.StickUp:
		case GrudgeAction.StickUpCalm:
			HiddenSlip(rec, "violence");
			break;
		}
		if (!Social(a))
		{
			return;
		}
		(int, float, string) tuple = GreetStreak(rec, a);
		if (tuple.Item1 < 0)
		{
			return;
		}
		rec.Touch();
		int hate = rec.Hate;
		int fear = rec.Fear;
		float num = 0f;
		float num2;
		switch (a)
		{
		case GrudgeAction.Greet:
			num2 = ((tuple.Item1 > 6) ? 0f : ((tuple.Item1 > 3) ? (-1f) : (-2f - tuple.Item2)));
			break;
		case GrudgeAction.EmoteGreet:
			num2 = ((tuple.Item1 > 6) ? 0f : ((tuple.Item1 > 3) ? (-1f) : (-1.5f - tuple.Item2)));
			break;
		case GrudgeAction.Defuse:
			num2 = ((tuple.Item1 > 6) ? 0f : ((tuple.Item1 > 3) ? (-1.5f) : (-5f)));
			break;
		case GrudgeAction.Insult:
			num2 = 5f;
			rec.TimesAntagonized++;
			break;
		case GrudgeAction.Taunt:
			num2 = 3f;
			break;
		case GrudgeAction.DrawOn:
			num2 = 3f;
			num = 2f;
			break;
		case GrudgeAction.Intimidate:
			num2 = 4f;
			num = 3f;
			break;
		case GrudgeAction.FriendThreatened:
			num2 = 4f;
			break;
		default:
			num2 = 6f;
			num = 2f;
			break;
		}
		var (num3, text) = ReactionMult(rec, a);
		if (rec.Gang != null && Gangs.Orders(rec) && num2 < 0f)
		{
			num2 = 0f;
		}
		if (num2 < 0f)
		{
			num3 = Math.Max(0.5f, Math.Min(1.4f, num3));
		}
		int num4 = (int)Math.Round(num2 * num3 * ((num2 > 0f) ? Balance.HateGainScale : 1f));
		if (num4 == 0)
		{
			num4 = Math.Sign(num2);
		}
		int num5 = ((num4 < 0) ? SocialFloor(rec) : 0);
		if (num4 < 0 && rec.Hate + num4 < num5)
		{
			num4 = Math.Min(0, num5 - rec.Hate);
			if (num4 == 0)
			{
				tuple.Item3 = $"words won't move him below {num5} ({FloorWhy(rec)})";
			}
		}
		rec.Hate = Math.Max(0, Math.Min(100, rec.Hate + num4));
		if (num4 > 0)
		{
			CapScuffle(rec, hate);
		}
		if (num > 0f)
		{
			rec.AddFear((int)Math.Round(num));
		}
		SaveRoster();
		Feed(string.Format("{0} {1}: hate {2}{3}  (x{4:0.00} {5}){6}", ActionName(a).ToUpperInvariant(), rec.Name, Delta(hate, rec.Hate), (rec.Fear != fear) ? ("  fear " + Delta(fear, rec.Fear)) : "", num3, text, (tuple.Item3 != null) ? ("  - " + tuple.Item3) : ""), (rec.Hate < hate) ? Theme.Good : Theme.Warn, rec);
		_lastGreetDrop = hate - rec.Hate;
	}

	internal static int SocialFloor(NemesisRecord r)
	{
		int val = Hate.Initial(r.Origin) / 2;
		if (r.TimesKilledPlayer > 0 || r.Origin == GrudgeOrigin.Killed)
		{
			val = Math.Max(val, Hate.VengefulAt);
		}
		if (r.ScarredByYou)
		{
			val = Math.Max(val, Hate.VengefulAt - 10);
		}
		if (r.TimesRobbed > 0)
		{
			val = Math.Max(val, 25);
		}
		return Math.Min(val, Hate.MurderousAt - 5);
	}

	internal static void SoftenHate(NemesisRecord r, int cut)
	{
		if (r != null && cut > 0)
		{
			Gang gang = ((r.Gang == null) ? null : Gangs.ById(r.Gang));
			if (gang == null || !gang.OrdersNotFeelings)
			{
				int feltHate = r.FeltHate;
				r.Hate = Math.Max(Math.Min(feltHate, SocialFloor(r)), feltHate - cut);
			}
		}
	}

	internal static string FloorWhy(NemesisRecord r)
	{
		if (r.TimesKilledPlayer <= 0 && r.Origin != GrudgeOrigin.Killed)
		{
			if (!r.ScarredByYou)
			{
				if (r.TimesRobbed <= 0)
				{
					return r.Origin.ToString().ToLowerInvariant() + " grudge";
				}
				return "you robbed him";
			}
			return "you scarred him";
		}
		return "he killed you";
	}

	private static (float mult, string why) ReactionMult(NemesisRecord r, GrudgeAction a)
	{
		List<string> why = new List<string>();
		float m = 1f;
		Personality t = r.Personality ?? Personality.Cold;
		Mul(TemperMult(t, a), t.ToString().ToLowerInvariant());
		if (r.TraitsRolled)
		{
			bool num = a == GrudgeAction.Greet || a == GrudgeAction.EmoteGreet || a == GrudgeAction.Defuse;
			bool flag = a == GrudgeAction.StickUp || a == GrudgeAction.StickUpCalm;
			if (num)
			{
				Mul((r.Disposition == Disposition.Friendly) ? 1.5f : ((r.Disposition == Disposition.Surly) ? 0.5f : 1f), r.Disposition.ToString().ToLowerInvariant());
				Mul((r.Devotion == Devotion.Devout) ? 1.3f : ((r.Devotion == Devotion.Godless) ? 0.8f : 1f), r.Devotion.ToString().ToLowerInvariant());
				if (a != GrudgeAction.Defuse)
				{
					Mul((r.Sociability == Sociability.Outgoing) ? 1.2f : 1f, "outgoing");
				}
				if (a == GrudgeAction.Defuse)
				{
					Mul((r.Smarts == Smarts.Sharp) ? 1.1f : 1f, "sharp");
				}
			}
			else
			{
				Mul((r.Disposition == Disposition.Surly) ? 1.3f : ((r.Disposition == Disposition.Friendly) ? 0.8f : 1f), r.Disposition.ToString().ToLowerInvariant());
				Mul(0.8f + (float)r.Aggression / 250f, Traits.Aggr(r.Aggression).ToLowerInvariant());
				if (flag)
				{
					Mul((r.Wealth == Wealth.Poor) ? 1.3f : ((r.Wealth == Wealth.Rich) ? 0.8f : 1f), r.Wealth.ToString().ToLowerInvariant());
				}
			}
		}
		return (mult: m, why: (why.Count == 0) ? "no modifiers" : string.Join(", ", why));
		void Mul(float f, string w)
		{
			if (Math.Abs(f - 1f) > 0.01f)
			{
				m *= f;
				why.Add(w);
			}
		}
	}

	internal static string ActionName(GrudgeAction a)
	{
		return a switch
		{
			GrudgeAction.EmoteGreet => "greet (emote)", 
			GrudgeAction.Insult => "antagonize", 
			GrudgeAction.DrawOn => "draw on", 
			GrudgeAction.StickUp => "stick-up", 
			GrudgeAction.StickUpCalm => "stick-up (calm)", 
			GrudgeAction.KnockOut => "knocked out", 
			GrudgeAction.Hogtie => "hogtied", 
			GrudgeAction.HorseStolen => "horse stolen", 
			GrudgeAction.WagonStolen => "wagon stolen", 
			GrudgeAction.Slashed => "cut", 
			GrudgeAction.HorseKilled => "horse killed", 
			GrudgeAction.Wounded => "shot", 
			GrudgeAction.FoughtBack => "fought back", 
			GrudgeAction.ShotAt => "shot at", 
			GrudgeAction.ComradeKilled => "saw you kill a man beside him", 
			GrudgeAction.FriendInsulted => "you insulted his friend", 
			GrudgeAction.FriendHurt => "you hurt his friend", 
			GrudgeAction.FriendKilled => "you killed his friend", 
			GrudgeAction.FriendThreatened => "you held a gun on his friend", 
			_ => a.ToString().ToLowerInvariant(), 
		};
	}

	private NemesisRecord ShadowFor(Ped ped, bool mind = true)
	{
		Gang gang = Gangs.ForModel((uint)ped.Model.Hash) ?? (RallyModel((uint)ped.Model.Hash) ? Gangs.Klan : null);
		NemesisRecord nemesisRecord = new NemesisRecord
		{
			Name = "",
			ModelHash = (uint)ped.Model.Hash,
			Gang = gang?.Id
		};
		if (_named.TryGetValue(ped.Handle, out var n) && !string.IsNullOrEmpty(n.Name))
		{
			nemesisRecord.Id = $"stranger:{n.Name}|{(uint)ped.Model.Hash:X8}";
		}
		Acquaintance acquaintance = ((n != null && !string.IsNullOrEmpty(n.Name)) ? People.FirstOrDefault((Acquaintance a) => a.Name == n.Name && a.Model == (uint)ped.Model.Hash) : null);
		RollNewLife(nemesisRecord, null, mind, (acquaintance != null && acquaintance.LifeRules < 2) ? 1 : 2);
		nemesisRecord.Id = null;
		return nemesisRecord;
	}

	private Personality? KnownTemper(Ped ped)
	{
		string text = null;
		if (_acqOf.TryGetValue(ped.Handle, out var value))
		{
			text = value.Temper;
		}
		if (text == null && _named.TryGetValue(ped.Handle, out var n))
		{
			text = People.FirstOrDefault((Acquaintance x) => x.Name == n.Name && x.Model == (uint)ped.Model.Hash)?.Temper;
		}
		if (text == null && _fpOf.TryGetValue(ped.Handle, out var value2) && value2 != "" && Prints.TryGetValue(value2, out var value3))
		{
			text = value3.Temper;
		}
		if (text == null || !Enum.TryParse<Personality>(text, out var result))
		{
			return null;
		}
		return result;
	}

	private static void CopyTraits(NemesisRecord from, NemesisRecord to)
	{
		if (from == null || !from.TraitsRolled)
		{
			return;
		}
		to.Aggression = from.Aggression;
		to.Sociability = from.Sociability;
		to.Disposition = from.Disposition;
		to.Smarts = from.Smarts;
		to.Wealth = from.Wealth;
		to.Trade = from.Trade;
		to.Faith = from.Faith;
		to.Devotion = from.Devotion;
		to.Family = from.Family;
		to.Children = from.Children;
		to.Pet = from.Pet;
		to.TraitsRolled = true;
		to.LifeRules = from.LifeRules;
		to.LifeFx = from.LifeFx?.ToList();
		if (from.PastRolled)
		{
			to.Past = from.Past.Select((PastEntry e) => new PastEntry
			{
				Year = e.Year,
				Month = e.Month,
				Text = e.Text,
				Kind = e.Kind,
				Public = e.Public,
				Known = e.Known,
				MirrorKind = e.MirrorKind,
				MirrorTitle = e.MirrorTitle,
				MirrorPlace = e.MirrorPlace,
				MirrorHeard = e.MirrorHeard
			}).ToList();
			to.BornYear = from.BornYear;
			to.Birthplace = from.Birthplace;
			to.PastRolled = true;
			to.Woven = false;
			to.Fear = Math.Max(to.Fear, from.Fear);
		}
		if (from.MindRolled)
		{
			to.Facets = new Dictionary<string, int>(from.Facets);
			to.Values = new Dictionary<string, int>(from.Values);
			to.Needs = new Dictionary<string, int>(from.Needs);
			to.Stress = from.Stress;
			to.Memories = from.Memories.ToList();
			to.Ties = new List<Tie>();
			to.Goals = from.Goals.ToList();
			to.MindRolled = true;
		}
	}

	private static int SettleChance(Provocation p)
	{
		if (!(p.Score >= (float)Balance.GsThreshold))
		{
			return Math.Max(0, (int)(p.Score - (float)Balance.GsFloor));
		}
		return 100;
	}

	private float DrawGrudgeMeters(Ped player)
	{
		List<(int, Provocation, float)> list = (from kv in (from kv in _provoked
				where kv.Value.Record == null && kv.Value.Score > 0f && ENTITY.DOES_ENTITY_EXIST(kv.Key)
				orderby kv.Value.LastActionAt descending
				select kv).Take((_combatants.Count > 0) ? 5 : 3)
			select (Key: kv.Key, Value: kv.Value, d: new Ped(kv.Key).Position.DistanceTo(player.Position))).ToList();
		Dictionary<NemesisRecord, float> men = new Dictionary<NemesisRecord, float>();
		if (ActiveRecord != null)
		{
			Add(ActiveRecord, _activeNemesisPed);
		}
		if (_interro?.Rec != null)
		{
			Add(_interro.Rec, _interro.Ped);
		}
		if (_convo != null && Roster.Contains(_convo.Rec))
		{
			Add(_convo.Rec, _convo.Ped);
		}
		foreach (KeyValuePair<int, Provocation> item4 in _provoked.Where((KeyValuePair<int, Provocation> kv) => kv.Value.Record != null))
		{
			Add(item4.Value.Record, Entity.FromHandle(item4.Key) as Ped);
		}
		foreach (KeyValuePair<int, NemesisRecord> worldNemese in _worldNemeses)
		{
			if (ENTITY.DOES_ENTITY_EXIST(worldNemese.Key))
			{
				Ped ped = new Ped(worldNemese.Key);
				if (ped.Position.DistanceTo(player.Position) < 50f)
				{
					Add(worldNemese.Value, ped);
				}
			}
		}
		List<KeyValuePair<NemesisRecord, float>> list2 = (from m in men
			orderby m.Key.LastDealt descending, m.Value
			select m).Take(3).ToList();
		float h = ((list.Count > 0) ? (0.034f + (float)list.Count * 0.074f) : 0f) + ((list2.Count > 0) ? (0.034f + (float)list2.Count * 0.092f) : 0f);
		float num = DrawInterroDebug(0f, measure: true);
		if (num > 0f)
		{
			_ = 0f;
		}
		float num2 = 0.04f;
		float num3 = ((num > 0f) ? DrawInterroDebug(num2) : num2);
		if (list.Count == 0 && list2.Count == 0)
		{
			return 1f;
		}
		float num4 = num3;
		Theme.DebugPanel(0.68f, num4, 0.31f, h);
		float num5 = num4;
		if (list2.Count > 0)
		{
			UiDraw.TextFont("HATE METER", UiDraw.TitleFont, 0.688f, num5 + 0.006f, 0.24f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
			UiDraw.Text("ticks = HIS blade / gun lines  -  blue = fear", 0.982f - Theme.TextWidth("ticks = HIS blade / gun lines  -  blue = fear", 0.165f), num5 + 0.01f, 0.165f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			num5 += 0.034f;
			foreach (KeyValuePair<NemesisRecord, float> item5 in list2)
			{
				DrawNemesisRow(item5.Key, item5.Value, 0.68f, num5, 0.31f);
				num5 += 0.092f;
			}
		}
		if (list.Count == 0)
		{
			return num4;
		}
		UiDraw.TextFont("GRUDGE METER", UiDraw.TitleFont, 0.688f, num5 + 0.006f, 0.24f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
		string text = $"nemesis at {Balance.GsThreshold}, else a roll of score minus {Balance.GsFloor}";
		UiDraw.Text(text, 0.982f - Theme.TextWidth(text, 0.165f), num5 + 0.01f, 0.165f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		num5 += 0.034f;
		foreach (var item6 in list)
		{
			int item = item6.Item1;
			Provocation item2 = item6.Item2;
			float item3 = item6.Item3;
			int num6 = Math.Max(0, Balance.GsSettleSeconds - (Game.GameTime - item2.LastActionAt) / 1000);
			UiDraw.TextFont(WhoIs(item), UiDraw.TitleFont, 0.688f, num5, 0.2f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
			NemesisRecord shadow = item2.Shadow;
			string text2 = ((shadow != null && shadow.TraitsRolled) ? (" " + shadow.Disposition.ToString().ToLowerInvariant() + ", " + Traits.Aggr(shadow.Aggression).ToLowerInvariant()) : "");
			string text3 = string.Format("{0}{1}  -  {2:0} m  -  {3}{4}", item2.Temper, text2, item3, item2.Settled ? "let go" : $"decides in {num6}s", item2.SawFace ? "" : (item2.SeenMasked ? "  -  MASKED" : "  -  hasn't seen your face"));
			UiDraw.Text(text3, 0.982f - Theme.TextWidth(text3, 0.165f), num5 + 0.004f, 0.165f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			float num7 = 0.294f;
			float num8 = num5 + 0.028f;
			float num9 = 0.005555555f;
			UiDraw.Rect(0.688f, num8, num7, num9, 40, 38, 35, 255);
			(int, int, int) tuple = ((item2.Score >= (float)Balance.GsThreshold) ? Theme.Blood : ((item2.Score >= (float)Balance.GsFloor) ? Theme.Warn : (170, 165, 120)));
			UiDraw.Rect(0.688f, num8, num7 * item2.Score / 100f, num9, tuple.Item1, tuple.Item2, tuple.Item3, 255);
			UiDraw.Rect(0.688f + num7 * (float)Balance.GsFloor / 100f - 0.00052083336f, num8 - 0.0027777776f, 0.0010416667f, num9 + 0.005555555f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b, 255);
			UiDraw.Rect(0.688f + num7 * (float)Balance.GsThreshold / 100f - 0.00052083336f, num8 - 0.0027777776f, 0.0010416667f, num9 + 0.005555555f, Theme.Blood.r, Theme.Blood.g, Theme.Blood.b, 255);
			string text4 = $"score {item2.Score:0}  =  {SettleChance(item2)}% nemesis   |   " + string.Join(",  ", item2.Ledger.AsEnumerable().Reverse().Take(4));
			int num10 = 96;
			UiDraw.Text((text4.Length > num10) ? (text4.Substring(0, num10 - 1) + ".") : text4, 0.688f, num8 + 0.014f, 0.172f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			num5 += 0.074f;
		}
		return num4;
		void Add(NemesisRecord r, Ped ped2)
		{
			if (r != null && ped2 != null && ped2.Exists() && !men.ContainsKey(r))
			{
				men[r] = ped2.Position.DistanceTo(player.Position);
			}
		}
	}

	private float DrawInterroDebug(float y, bool measure = false)
	{
		Interrogation interro = _interro;
		if (interro == null || !interro.Ped.Exists())
		{
			if (!measure)
			{
				return y;
			}
			return 0f;
		}
		List<(string, (int r, int g, int b))> lines = new List<(string, (int, int, int))>();
		L(string.Format("{0}  -  {1}{2}", Speaker(interro), interro.Temper, (interro.Rec != null) ? $", rank {interro.Rec.Rank}, hate {interro.Rec.Hate}" : ", unnamed"), Theme.Title);
		L($"fear now {interro.Terror:0}   started {interro.StartTerror:0}", (150, 170, 210));
		L("start: " + interro.StartNote, Theme.Dim);
		object arg = interro.CampNeed;
		string arg2 = ((interro.Terror < (float)interro.CampNeed) ? "not scared enough" : (interro.LastChance + "% he talks"));
		object arg3;
		if (!interro.CampDone)
		{
			NemesisRecord rec = interro.Rec;
			if (rec == null || !rec.GaveCamp)
			{
				arg3 = ((interro.CampLockAt >= 0f) ? $"  (greyed until {interro.CampLockAt:0})" : "");
				goto IL_017c;
			}
		}
		arg3 = "  (given)";
		goto IL_017c;
		IL_017c:
		L($"camp: needs fear {arg}  -  {arg2}{arg3}", Theme.Gold);
		object arg4 = interro.FamilyNeed;
		string arg5 = ((interro.Terror < (float)interro.FamilyNeed) ? "not scared enough" : (interro.FamilyChance + "% he talks"));
		object arg6;
		if (!interro.KinDone)
		{
			NemesisRecord rec2 = interro.Rec;
			if (rec2 == null || !rec2.GaveKin)
			{
				arg6 = ((interro.KinLockAt >= 0f) ? $"  (greyed until {interro.KinLockAt:0})" : "");
				goto IL_0219;
			}
		}
		arg6 = "  (given)";
		goto IL_0219;
		IL_0219:
		L($"family: needs fear {arg4}  -  {arg5}{arg6}", Theme.Gold);
		L($"hits {interro.Hits}  cuts {interro.Stabs}  shots {interro.Shots}  near {interro.NearShots}  threats {interro.ThreatsUsed}/3  camp tries {interro.CampTries}  family tries {interro.KinTries}");
		string[] array = (InterroNote ?? "").Split('|');
		for (int i = 0; i < array.Length; i++)
		{
			L(array[i].Trim(), Theme.Dim);
		}
		int num = 96;
		List<(string, (int, int, int))> list = new List<(string, (int, int, int))>();
		foreach (var item3 in lines)
		{
			string item = item3.Item1;
			(int, int, int) item2 = item3.Item2;
			string text = item;
			bool flag = true;
			while (text.Length > 0)
			{
				int num2 = ((text.Length <= num) ? text.Length : Math.Max(num / 2, text.LastIndexOf(' ', Math.Min(text.Length - 1, num))));
				list.Add(((flag ? "" : "   ") + text.Substring(0, num2).TrimEnd(), item2));
				text = text.Substring(num2).TrimStart();
				flag = false;
			}
		}
		float num3 = 0.034f + (float)list.Count * 0.0172f + 0.008f;
		if (measure)
		{
			return num3;
		}
		Theme.DebugPanel(0.68f, y, 0.31f, num3);
		UiDraw.TextFont("INTERROGATION", UiDraw.TitleFont, 0.688f, y + 0.007f, 0.24f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
		float num4 = y + 0.034f;
		foreach (var (text2, tuple2) in list)
		{
			UiDraw.Text(text2, 0.688f, num4, 0.172f, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			num4 += 0.0172f;
		}
		return y + num3 + 0.008f;
		void L(string t, (int r, int g, int b)? c = null)
		{
			lines.Add((t, c ?? Theme.Text));
		}
	}

	private void DrawNemesisRow(NemesisRecord r, float d, float x, float ry, float w)
	{
		UiDraw.TextFont(r.Name, UiDraw.TitleFont, x + 0.008f, ry, 0.2f, Theme.Blood.r, Theme.Blood.g, Theme.Blood.b);
		string text = string.Format("{0}  -  {1}  -  {2:0} m", r.Personality, (r.Gang == null) ? "loner" : Gangs.ById(r.Gang)?.Name, d);
		UiDraw.Text(text, x + w - 0.008f - Theme.TextWidth(text, 0.165f), ry + 0.004f, 0.165f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		float num = x + 0.008f;
		float num2 = w - 0.016f;
		float num3 = ry + 0.028f;
		float num4 = 0.005555555f;
		UiDraw.Rect(num, num3, num2, num4, 40, 38, 35, 255);
		(int, int, int) tuple = ((r.HateTier == HateTier.Murderous) ? Theme.Blood : ((r.HateTier == HateTier.Vengeful) ? Theme.Warn : (170, 165, 120)));
		UiDraw.Rect(num, num3, num2 * (float)r.Hate / 100f, num4, tuple.Item1, tuple.Item2, tuple.Item3, 255);
		UiDraw.Rect(num, num3 + num4, num2 * (float)r.Fear / 100f, 0.0018518518f, 120, 150, 200, 255);
		UiDraw.Rect(num + num2 * (float)Hate.VengefulAt / 100f, num3 + num4, 0.00052083336f, 0.0027777776f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 255);
		UiDraw.Rect(num + num2 * (float)Hate.MurderousAt / 100f, num3 + num4, 0.00052083336f, 0.0027777776f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 255);
		if (!Gangs.Orders(r))
		{
			UiDraw.Rect(num + num2 * (float)Loadout.BladeAt(r) / 100f - 0.00052083336f, num3 - 0.0027777776f, 0.0010416667f, num4 + 0.005555555f, Theme.Warn.r, Theme.Warn.g, Theme.Warn.b, 255);
			UiDraw.Rect(num + num2 * (float)Loadout.GunAt(r) / 100f - 0.00052083336f, num3 - 0.0027777776f, 0.0010416667f, num4 + 0.005555555f, Theme.Blood.r, Theme.Blood.g, Theme.Blood.b, 255);
		}
		UiDraw.Text(string.Format("hate {0} ({1}) - {2}   fear {3}{4}", r.Hate, r.HateTier, Loadout.Describe(r), r.Fear, CoolingOff(r) ? "   cooling off" : ""), x + 0.008f, num3 + 0.014f, 0.172f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		UiDraw.Text(Gangs.Orders(r) ? (Gangs.Of(r).Name + ": orders, not feelings (always guns)") : $"lines {Loadout.BladeAt(r)} / {Loadout.GunAt(r)}: {Loadout.ShiftWhy(r)}  -  gains x{HateGainOf(r):0.00}{HateGainWhy(r)}  -  {Traits.Short(IntentOf(r))}", x + 0.008f, num3 + 0.034f, 0.162f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
	}
}
