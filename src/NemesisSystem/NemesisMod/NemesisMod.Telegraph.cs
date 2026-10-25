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
	private sealed class TgOption
	{
		public string Key;

		public string Text;

		public string Note;

		public int Cost;

		public bool Ok = true;
	}

	private static readonly string[] ClerkTokens = new string[4] { "trainstationworker", "stationclerk", "ticketseller", "freightstationowner" };

	private TelegraphPanel _tg;

	private int _pTelegraph;

	private int _pTelegraphFor;

	private int _pTelegraphGroup;

	private int _nextTelegraphCheck;

	private int _nextClerkScan;

	private bool _tgArmed;

	private Ped _clerkNear;

	internal string TelegraphNote = "";

	private long _nextUnaskedAt;

	private List<Telegram> Telegrams => Chronicle.Telegrams ?? (Chronicle.Telegrams = new List<Telegram>());

	private void UpdateTelegraph(Ped player)
	{
		if (Game.GameTime >= _nextTelegraphCheck)
		{
			_nextTelegraphCheck = Game.GameTime + 5000;
			DeliverTelegrams();
		}
		if (_tg != null)
		{
			UpdateTelegraphPanel(player);
			return;
		}
		if (_paperShown != null)
		{
			HideTelegraphPrompt();
			return;
		}
		if (Game.GameTime >= _nextClerkScan)
		{
			_nextClerkScan = Game.GameTime + 400;
			_clerkNear = (from p in PedsAround(player)
				where p != null && p.Exists() && !p.IsDead && p.IsHuman && p.Position.DistanceTo(player.Position) < 3.2f && ClerkTokens.Any(Models.Name(p).Contains)
				orderby p.Position.DistanceTo(player.Position)
				select p).FirstOrDefault();
		}
		Ped clerkNear = _clerkNear;
		if (clerkNear == null || !clerkNear.Exists() || clerkNear.IsDead || clerkNear.Position.DistanceTo(player.Position) > 3.5f || player.IsInCombat || _menu.IsOpen || _convo != null)
		{
			HideTelegraphPrompt();
			return;
		}
		int num = HUD._UI_PROMPT_GET_GROUP_ID_FOR_TARGET_ENTITY(clerkNear.Handle);
		if (_pTelegraph == 0 || !PromptValid(_pTelegraph) || _pTelegraphFor != clerkNear.Handle || _pTelegraphGroup != num)
		{
			Crumbs.Native("Telegraph prompt delete");
			if (PromptValid(_pTelegraph))
			{
				HUD._UI_PROMPT_DELETE(_pTelegraph);
			}
			int num2 = Telegrams.Count((Telegram t) => t.Arrived && !t.Read);
			_pTelegraph = MakePrompt(eInputType.InteractOption1, (num2 > 0) ? $"Telegraph ({num2} new)" : "Telegraph", hold: true, num);
			_pTelegraphFor = clerkNear.Handle;
			_pTelegraphGroup = num;
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pTelegraph, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pTelegraph, toggle: true);
		PaperPrompt(clerkNear, num, player);
		if (_paperShown == null)
		{
			if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(_pTelegraph))
			{
				_tgArmed = true;
			}
			else if (_tgArmed)
			{
				_tgArmed = false;
				OpenTelegraph(clerkNear, player);
			}
		}
	}

	private void HideTelegraphPrompt()
	{
		HidePaperPrompt();
		if (_pTelegraph != 0)
		{
			Crumbs.Native("Telegraph prompt delete");
			if (PromptValid(_pTelegraph))
			{
				HUD._UI_PROMPT_DELETE(_pTelegraph);
			}
			_pTelegraph = 0;
			_pTelegraphFor = 0;
		}
	}

	private bool OnTelegraphKey()
	{
		if (_tg != null || _clerkNear == null || !_clerkNear.Exists() || _clerkNear.Position.DistanceTo(Game.Player.Character.Position) > 3.5f)
		{
			return false;
		}
		OpenTelegraph(_clerkNear, Game.Player.Character);
		return true;
	}

	private void OpenTelegraph(Ped clerk, Ped player)
	{
		HideTelegraphPrompt();
		string text = TownAt(player.Position) ?? _currentTown ?? GetZoneName(player.Position) ?? "the station";
		_tg = new TelegraphPanel
		{
			Clerk = clerk,
			Town = text
		};
		int num = Telegrams.Count((Telegram t) => t.Arrived && !t.Read);
		_tg.Line = ((num > 0) ? $"Wire came in for you - {num} of 'em. Or you sending?" : "Wire's open. Who you sending to?");
		TelegraphRows();
		TASK.TASK_TURN_PED_TO_FACE_ENTITY(clerk.Handle, player.Handle, 2000, 0f, 0f, 0f);
		GrudgeLog($"TELEGRAPH opened at {text} station ({num} unread, {Telegrams.Count((Telegram t) => !t.Arrived)} on the wire)");
	}

	private void CloseTelegraph()
	{
		_tg = null;
		_panelClosedAt = Game.GameTime;
	}

	private void TelegraphRows()
	{
		TelegraphPanel tg = _tg;
		tg.Rows.Clear();
		tg.Info.Clear();
		switch (tg.Page)
		{
		case "main":
		{
			int num = Telegrams.Count((Telegram telegram2) => telegram2.Arrived && !telegram2.Read);
			int num2 = Telegrams.Count((Telegram telegram2) => telegram2.Arrived);
			bool item = CashDollars >= 1;
			Row((key: "send", text: "Send a telegram  ($1)", enabled: item, note: "not enough money"));
			Row((key: "inbox", text: (num2 == 0) ? "Check telegrams" : $"Check telegrams  ({num} new)", enabled: num2 > 0, note: "nothing for you"));
			Row((key: "sentlist", text: "Recently sent", enabled: Telegrams.Any((Telegram telegram2) => !telegram2.Spontaneous), note: "nothing sent yet"));
			int num3 = Telegrams.Count((Telegram telegram2) => !telegram2.Arrived);
			if (num3 > 0)
			{
				Telegram telegram = (from telegram2 in Telegrams
					where !telegram2.Arrived
					orderby telegram2.ArriveAt
					select telegram2).First();
				Row((key: "wire", text: $"On the wire: {num3} waiting for an answer  -  the next about {GameDate.FromMinutes(telegram.ArriveAt):MMM d}", enabled: false, note: null));
			}
			Row((key: "leave", text: "Leave", enabled: true, note: null));
			break;
		}
		case "to":
		{
			List<NemesisRecord> list = RecipientMen().ToList();
			List<Acquaintance> list2 = RecipientFolk().ToList();
			if (list.Count > 0)
			{
				foreach (NemesisRecord r in list)
				{
					bool flag6 = Telegrams.Any((Telegram telegram2) => !telegram2.Arrived && telegram2.ToId == r.Id);
					var (b3, c, bc3) = OutfitAndRank(r);
					Row((key: "to:n:" + r.Id, text: r.FullName, enabled: !flag6, note: "already on the wire"), new TgInfo
					{
						A = r.FullName,
						B = b3,
						Bc = bc3,
						C = c,
						Cc = Theme.Text,
						D = r.HateTier.ToString(),
						Dc = UiDraw.HateColor(r.HateTier),
						Wait = flag6,
						Detail = RecipientDetail(r, flag6)
					});
				}
			}
			if (list2.Count > 0)
			{
				foreach (Acquaintance a in list2)
				{
					bool flag7 = Telegrams.Any((Telegram telegram2) => !telegram2.Arrived && telegram2.ToId == a.Id);
					var (d3, dc) = FeelingOf(a);
					var (b4, c2, bc4) = OutfitAndRank(a);
					Row((key: "to:p:" + a.Id, text: a.FullName, enabled: !flag7, note: "already on the wire"), new TgInfo
					{
						A = a.FullName,
						B = b4,
						Bc = bc4,
						C = c2,
						Cc = Theme.Text,
						D = d3,
						Dc = dc,
						Wait = flag7,
						Detail = RecipientDetail(a, flag7)
					});
				}
			}
			if (tg.Rows.Count == 0)
			{
				Row((key: "none", text: "Nobody you know well enough to wire", enabled: false, note: null));
			}
			break;
		}
		case "what":
			foreach (TgOption item2 in TelegramOptions(tg.To))
			{
				string text3 = OptionPurpose(item2.Key, tg.To.nemesis);
				bool flag8 = CashDollars >= 1 + item2.Cost;
				bool flag9 = item2.Ok && flag8;
				string text4 = item2.Text;
				string input = item2.Text;
				int num4 = item2.Text.LastIndexOf("\"  - ", StringComparison.Ordinal);
				if (num4 >= 0)
				{
					text4 = item2.Text.Substring(0, num4 + 1);
					input = item2.Text.Substring(num4 + 5);
				}
				input = Regex.Replace(input, ",\\s*\\$\\d+$", "").Trim();
				if (input.Length > 0)
				{
					input = char.ToUpperInvariant(input[0]) + input.Substring(1);
				}
				Row((key: "what:" + item2.Key, text: item2.Text, enabled: flag9, note: (!item2.Ok) ? item2.Note : "not enough money"), new TgInfo
				{
					A = input,
					B = (flag9 ? text4 : ((!item2.Ok) ? item2.Note : "not enough money")),
					Bc = (flag9 ? Theme.Dim : Theme.Faint),
					C = $"${1 + item2.Cost}",
					Cc = ((item2.Cost > 0) ? (r: 215, g: 178, b: 95) : Theme.Dim),
					Right = true,
					Detail = ((text3 == null) ? null : Pron.Her(text3, ToFemale(tg.To)))
				});
			}
			break;
		case "inbox":
			foreach (Telegram t in (from telegram2 in Telegrams
				where telegram2.Arrived
				orderby telegram2.Read, telegram2.ArriveAt descending
				select telegram2).Take(30))
			{
				string d = InboxNote(t);
				bool fem = (t.ToNemesis ? Models.Female(Roster.FirstOrDefault((NemesisRecord nemesisRecord) => nemesisRecord.Id == t.ToId)?.ModelHash ?? 0) : Models.Female(People.FirstOrDefault((Acquaintance acquaintance) => acquaintance.Id == t.ToId)?.Model ?? 0));
				string arg = (t.Spontaneous ? TelegramContext(t, fem) : string.Format("You wrote {0} from {1} on {2:MMM d}", Quote(t.Text), t.From ?? "a station", GameDate.FromMinutes(t.SentAt)));
				string b;
				string text;
				(int, int, int) bc;
				if (!t.ToNemesis)
				{
					(b, text, bc) = OutfitAndRank(People.FirstOrDefault((Acquaintance acquaintance) => acquaintance.Id == t.ToId));
				}
				else
				{
					(b, text, bc) = OutfitAndRank(Roster.FirstOrDefault((NemesisRecord nemesisRecord) => nemesisRecord.Id == t.ToId));
				}
				bool flag3 = CorrespondentDead(t);
				Row((key: "read:" + t.Id, text: "From " + t.ToName, enabled: true, note: null), new TgInfo
				{
					A = t.ToName,
					B = b,
					Bc = bc,
					C = (flag3 ? "Dead" : text),
					Cc = (flag3 ? Theme.Blood : Theme.Text),
					D = d,
					Dc = ((t.Money > 0 && !t.Read) ? (r: 215, g: 178, b: 95) : (t.NoAnswer ? Theme.Faint : (t.Spontaneous ? Theme.Text : Theme.Dim))),
					New = !t.Read,
					Detail = $"Arrived {GameDate.FromMinutes(t.ArriveAt):MMM d}  -  {arg}"
				});
			}
			break;
		case "sent":
			foreach (Telegram t2 in (from telegram2 in Telegrams
				where !telegram2.Spontaneous
				orderby telegram2.SentAt descending
				select telegram2).Take(30))
			{
				string b2;
				string text2;
				(int, int, int) bc2;
				if (!t2.ToNemesis)
				{
					(b2, text2, bc2) = OutfitAndRank(People.FirstOrDefault((Acquaintance acquaintance) => acquaintance.Id == t2.ToId));
				}
				else
				{
					(b2, text2, bc2) = OutfitAndRank(Roster.FirstOrDefault((NemesisRecord nemesisRecord) => nemesisRecord.Id == t2.ToId));
				}
				bool flag4 = CorrespondentDead(t2);
				bool flag5 = t2.Arrived && (t2.Effect == "gone" || (t2.Reply ?? "").StartsWith("WESTERN UNION REGRETS"));
				string d2 = ((!t2.Arrived) ? $"On the wire - due {GameDate.FromMinutes(t2.ArriveAt):MMM d}" : (flag5 ? "Never delivered" : (t2.NoAnswer ? "No reply came" : string.Format("Answered {0:MMM d}{1}", GameDate.FromMinutes(t2.ArriveAt), t2.Read ? "" : " - unread"))));
				Row((key: "sent:" + t2.Id, text: t2.ToName, enabled: true, note: null), new TgInfo
				{
					A = t2.ToName,
					B = b2,
					Bc = bc2,
					C = (flag4 ? "Dead" : text2),
					Cc = (flag4 ? Theme.Blood : Theme.Text),
					D = d2,
					Dc = ((!t2.Arrived) ? Theme.Dim : ((flag5 || t2.NoAnswer) ? Theme.Faint : (t2.Read ? Theme.Text : (r: 215, g: 178, b: 95)))),
					Detail = string.Format("You wrote {0} from {1} on {2:MMM d}", Quote(t2.Text), t2.From ?? "a station", GameDate.FromMinutes(t2.SentAt))
				});
			}
			break;
		case "read":
		{
			Telegram rt = tg.Reading;
			bool flag = rt != null && (rt.ToNemesis ? Roster.Any((NemesisRecord nemesisRecord) => nemesisRecord.Id == rt.ToId && nemesisRecord.IsAlive && !nemesisRecord.PresumedDead) : People.Any((Acquaintance acquaintance) => acquaintance.Id == rt.ToId && !acquaintance.Dead));
			bool flag2 = rt != null && Telegrams.Any((Telegram x) => !x.Arrived && x.ToId == rt.ToId);
			Row((key: "reply", text: "Write back  ($1)", enabled: flag && !flag2 && CashDollars >= 1, note: (!flag) ? "can't be reached" : (flag2 ? "already on the wire" : "not enough money")));
			Row((key: "back", text: "Back", enabled: true, note: null));
			break;
		}
		}
		tg.Sel = Math.Max(0, Math.Min(tg.Sel, tg.Rows.Count - 1));
		while (tg.Sel < tg.Rows.Count && !tg.Rows[tg.Sel].enabled && tg.Rows.Any(((string key, string text, bool enabled, string note) tuple8) => tuple8.enabled))
		{
			tg.Sel = (tg.Sel + 1) % tg.Rows.Count;
		}
		void Row((string key, string text, bool enabled, string note) item2, TgInfo info = null)
		{
			tg.Rows.Add(item2);
			tg.Info.Add(info);
		}
	}

	private IEnumerable<NemesisRecord> RecipientMen()
	{
		return (from n in Roster
			where n.IsAlive && !n.PresumedDead && n.Id != null && !IsArmy(n) && (n.Gang == null || n.TimesAmbushed > 0 || n.TimesKilledPlayer > 0 || n.Origin != GrudgeOrigin.GangMember || n.TemperKnown)
			orderby n.LastInteraction descending
			select n).Take(40);
	}

	private IEnumerable<Acquaintance> RecipientFolk()
	{
		return (from a in People
			where a.Met && !a.Dead && a.Id != null && Roster.All((NemesisRecord r) => r.Name != a.Name)
			orderby a.LastSeen descending
			select a).Take(40);
	}

	private static (string, (int r, int g, int b)) FeelingOf(Acquaintance a)
	{
		if (a.Like < 40)
		{
			if (a.Fear < 40)
			{
				if (a.Grudge < 25 || a.GrudgeSettled)
				{
					if (a.Like < 15)
					{
						if (a.Like < 5)
						{
							return ("Neutral", Theme.Dim);
						}
						return ("Warming to you", (160, 180, 145));
					}
					return ("Likes you", (170, 200, 150));
				}
				return ("Holds a grudge", Theme.Warn);
			}
			return ("Afraid of you", (150, 170, 210));
		}
		return ("Likes you a lot", Theme.Good);
	}

	private string TierOf(NemesisRecord r, Gang g)
	{
		if (r.Id != null && r.Id == SuccessorId(g))
		{
			return "Leader";
		}
		string text = OfficerTitle(r);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return null;
	}

	private string AgoText(long gm)
	{
		if (gm <= 0)
		{
			return null;
		}
		long num = (GameMinutesNow() - gm) / 1440;
		if (num > 0)
		{
			if (num != 1)
			{
				return $"{num} days ago";
			}
			return "yesterday";
		}
		return "today";
	}

	private string RecipientDetail(NemesisRecord r, bool busy)
	{
		List<string> list = new List<string>();
		if (busy)
		{
			list.Add("Waiting for his answer");
		}
		list.Add(IntentOf(r) switch
		{
			Intent.WontHunt => "Won't come looking", 
			Intent.Hunts => "Hunts you", 
			_ => "Only if you start it", 
		});
		if (r.TemperKnown && r.Personality.HasValue)
		{
			list.Add(r.Personality.Value.ToString());
		}
		if (!string.IsNullOrEmpty(r.KillLocation))
		{
			list.Add("from " + r.KillLocation);
		}
		string text = AgoText(r.LastGameMin);
		if (text != null)
		{
			list.Add("last dealt with " + text);
		}
		return Pron.Her(string.Join("  -  ", list), Models.Female(r.ModelHash));
	}

	private string RecipientDetail(Acquaintance a, bool busy)
	{
		List<string> list = new List<string>();
		if (busy)
		{
			list.Add("Waiting for his answer");
		}
		if (!string.IsNullOrEmpty(a.Temper))
		{
			list.Add(a.Temper);
		}
		if (IsPenPal(a))
		{
			list.Add("Pen pal");
		}
		if (_debugOn)
		{
			list.Add($"like {a.Like} / fear {a.Fear}");
		}
		string text = AgoText(a.LastGameMin);
		if (text != null)
		{
			list.Add("last seen " + text);
		}
		if (a.Favours > 0)
		{
			list.Add("you did him " + ((a.Favours == 1) ? "a favour" : (a.Favours + " favours")));
		}
		if (a.Lookout)
		{
			list.Add("watching for trouble");
		}
		return Pron.Her(string.Join("  -  ", list), Models.Female(a.Model));
	}

	private (string outfit, string rank, (int r, int g, int b) c) OutfitAndRank(NemesisRecord r)
	{
		if (r == null)
		{
			return (outfit: "", rank: "", c: Theme.Dim);
		}
		Gang gang = ((r.Gang != null) ? Gangs.ById(r.Gang) : null);
		if (gang == null)
		{
			return (outfit: "Rides alone", rank: "Loner", c: Theme.Dim);
		}
		return (outfit: gang.Name, rank: TierOf(r, gang) ?? "Member", c: gang.Accent);
	}

	private bool CorrespondentDead(Telegram t)
	{
		if (t.ToNemesis)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord x) => x.Id == t.ToId);
			if (nemesisRecord != null)
			{
				if (nemesisRecord.IsAlive)
				{
					return nemesisRecord.PresumedDead;
				}
				return true;
			}
			return false;
		}
		Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == t.ToId);
		if (acquaintance != null)
		{
			return acquaintance.Dead;
		}
		NemesisRecord nemesisRecord2 = Roster.FirstOrDefault((NemesisRecord x) => x.Name == t.ToName);
		if (nemesisRecord2 != null)
		{
			if (nemesisRecord2.IsAlive)
			{
				return nemesisRecord2.PresumedDead;
			}
			return true;
		}
		return false;
	}

	private (string outfit, string rank, (int r, int g, int b) c) OutfitAndRank(Acquaintance a)
	{
		if (a != null)
		{
			return (outfit: a.Town ?? "Met on the road", rank: "Townsfolk", c: Theme.Dim);
		}
		return (outfit: "", rank: "", c: Theme.Dim);
	}

	private static string OptionPurpose(string key, bool nemesis)
	{
		if (nemesis)
		{
			switch (key)
			{
			case "peace":
				return "Asks him to let it go. A scared or tired man may take it and ease off; a proud or hot-headed one gets angrier.";
			case "sorry":
				return "Owns up to it. If he takes it he eases off, and his outfit thinks a little better of you.";
			case "money":
				return "$10 for his trouble - a small softening, more for a greedy man. Proud men send it back. Each one this week is worth less.";
			case "payoff":
				return "$25 to call it square. Greedy, poor or scared men may take it and ease right off; if not, the money comes back.";
			case "threat":
				return "Tries to scare him off. Cowards back away; braver men get angrier.";
			case "coming":
				return "Tells him you're on your way. Scares a coward; makes a braver man ready for you.";
			case "taunt":
				return "Mocks him. A proud man or a hothead takes it hard; others shrug it off.";
			case "family":
				return "Threatens his family. Terrifies a frightened man - and enrages a brave one.";
			case "boss":
				return "Asks where his boss rides from. A greedy, scared or disloyal man may give the camp away.";
			case "distrust":
				return "Says his boss is selling him out. A disloyal or ambitious man may turn on him.";
			case "kin":
				return "Gloats about the relative you killed. Pure hate - or grief, if he's scared enough.";
			case "wish":
				return "No hard feelings. Plain men soften a little; proud men and hotheads scorn it.";
			case "condole":
				return "Condolences for someone he lost. He eases off - a loyal man most of all.";
			case "gift":
				return "$25 for his outfit's leader. A greedy leader takes it gladly; a proud one sends it back. Can lower your notoriety with them.";
			}
		}
		else
		{
			switch (key)
			{
			case "regards":
				return "Keeps you on good terms - he'll like you a little more.";
			case "news":
				return "Asks what's going on round his way - fights, men with a grudge, the talk of the town.";
			case "asking":
				return "Finds out if anyone's been asking after you in his town.";
			case "thanks":
				return "Thanks him for what he did - he'll like you more.";
			case "drink":
				return "$2 for a drink on you - he'll like you more, unless he holds a grudge.";
			case "loan":
				return "Asks him to wire you $10. A friend who owes you may; others won't - and it costs a little goodwill.";
			case "lookout":
				return "Asks him to watch for trouble - if he agrees, he'll wire you when a nemesis turns up near him.";
			case "sorry":
				return "Apologises - eases a grudge and some of his fear.";
			case "checkin":
				return "A friendly word - he'll like you a little more and hold a little less against you.";
			case "letter":
				return "A proper letter - if he likes you he writes back with his own news. Write too often and he tires of it.";
			case "pgift":
				return "$5 for him - he'll like you more. A proud man may send it back.";
			case "pcondole":
				return "Condolences for a friend he lost - unless he knows it was you.";
			case "warn":
				return "Warns him to keep quiet about what he saw. He's more afraid of you and less likely to tell the law - but he'll like you less.";
			}
		}
		return null;
	}

	private bool ToFemale((string id, bool nemesis, string name) to)
	{
		if (!to.nemesis)
		{
			return Models.Female(People.FirstOrDefault((Acquaintance x) => x.Id == to.id)?.Model ?? 0);
		}
		return Models.Female(Roster.FirstOrDefault((NemesisRecord x) => x.Id == to.id)?.ModelHash ?? 0);
	}

	private static string Quote(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}
		int num = text.LastIndexOf("\"  - ", StringComparison.Ordinal);
		if (num < 0)
		{
			return text;
		}
		return text.Substring(0, num + 1);
	}

	private static string MessageLabel(string key)
	{
		switch (key)
		{
		case "peace":
			return "peace";
		case "sorry":
			return "an apology";
		case "money":
		case "payoff":
			return "money";
		case "threat":
		case "coming":
		case "family":
		case "kin":
			return "a threat";
		case "taunt":
			return "a taunt";
		case "boss":
		case "distrust":
			return "his boss";
		case "regards":
			return "regards";
		case "news":
			return "news";
		case "asking":
			return "who's asking";
		case "thanks":
			return "thanks";
		case "drink":
			return "a drink";
		case "loan":
			return "a loan";
		case "lookout":
			return "a favour";
		case "warn":
			return "a warning";
		case "pgift":
		case "gift":
			return "a gift";
		case "wish":
			return "good wishes";
		case "pcondole":
		case "condole":
			return "condolences";
		case "checkin":
			return "a friendly word";
		case "letter":
			return "a letter";
		default:
			return key;
		}
	}

	private List<TgOption> TelegramOptions((string id, bool nemesis, string name) to)
	{
		List<TgOption> o = new List<TgOption>();
		bool fem = (to.nemesis ? Models.Female(Roster.FirstOrDefault((NemesisRecord x) => x.Id == to.id)?.ModelHash ?? 0) : Models.Female(People.FirstOrDefault((Acquaintance x) => x.Id == to.id)?.Model ?? 0));
		if (to.nemesis)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord x) => x.Id == to.id);
			Add("peace", "\"Let it go. I'll do the same.\"  - make peace");
			Add("sorry", "\"I was wrong. I'm sorry for it.\"  - apologise");
			Add("money", "\"For your trouble.\"  - send $10", 10);
			Add("payoff", "\"Take it and we're square.\"  - pay him off, $25", 25);
			Add("threat", "\"Stay away from me or I'll put you in the ground.\"  - threaten him");
			Add("coming", "\"I know where you are. I'm coming.\"  - tell him you're coming");
			Add("taunt", "\"Heard you ran like a rabbit. Folks are laughing.\"  - mock him");
			List<(NemesisRecord, string)> source = (List<(NemesisRecord, string)>)((nemesisRecord != null) ? ((IList)KinOf(nemesisRecord)) : ((IList)new List<(NemesisRecord, string)>()));
			bool ok = nemesisRecord != null && (nemesisRecord.Family == "married" || nemesisRecord.Children > 0 || source.Any<(NemesisRecord, string)>(((NemesisRecord who, string rel) k) => k.who.IsAlive && k.rel != "friend"));
			Add("family", "\"I know where your family sleeps.\"  - threaten his family", 0, ok, "no family you know of");
			if (nemesisRecord != null && nemesisRecord.Gang != null)
			{
				NemesisRecord nemesisRecord2 = BossOf(nemesisRecord);
				bool flag = nemesisRecord2 != null && nemesisRecord2.IsAlive && !nemesisRecord2.PresumedDead;
				Add("boss", "\"Where does " + (flag ? nemesisRecord2.Name : "your boss") + " ride from? Worth your while.\"  - ask after his boss", 0, flag && nemesisRecord2.CampId != null && !nemesisRecord2.CampKnown, (!flag) ? "no boss over him" : (nemesisRecord2.CampKnown ? "you know where he rides from" : "his boss has no camp"));
				Add("distrust", "\"" + (flag ? nemesisRecord2.Name : "Your boss") + " is selling you out. Ask him why.\"  - turn him on his boss", 0, flag, "no boss over him");
			}
			(NemesisRecord, string) tuple = source.FirstOrDefault<(NemesisRecord, string)>(((NemesisRecord who, string rel) k) => !k.who.IsAlive && k.who.KilledByPlayer);
			if (tuple.Item1 != null)
			{
				Add("kin", "\"" + tuple.Item1.Name + " died squealing. You're next.\"  - taunt him about his " + tuple.Item2);
			}
			Add("wish", "\"No hard feelings. Look after yourself.\"  - wish him well");
			(NemesisRecord, string) tuple2 = source.FirstOrDefault<(NemesisRecord, string)>(((NemesisRecord who, string rel) k) => !k.who.IsAlive && !k.who.KilledByPlayer && !k.who.Story);
			if (tuple2.Item1 != null)
			{
				Add("condole", "\"Heard about " + tuple2.Item1.Name + ". I'm sorry.\"  - your condolences for his " + tuple2.Item2);
			}
			Gang gang = ((nemesisRecord != null) ? Gangs.Of(nemesisRecord) : null);
			if (gang != null && !gang.OrdersNotFeelings)
			{
				bool flag2 = Telegrams.Any((Telegram x) => x.Message == "gift" && x.GiftGang == gang.Id && GameMinutesNow() - x.SentAt < 10080);
				bool flag3 = IsLeader(gang, nemesisRecord);
				bool item = LeaderCard(gang).vacant;
				Add("gift", "\"For " + (flag3 ? "you" : (gang.LeaderName ?? "your boss")) + ", with my respects.\"  - a gift for the " + gang.Name + ", $25", 25, !flag2 && !item, item ? "nobody leads them right now" : "you sent them a gift this week");
			}
		}
		else
		{
			Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == to.id);
			Add("regards", "\"Thinking of you. Hope you're well.\"  - send regards");
			Add("news", "\"Any news round your way?\"  - ask after news");
			Add("asking", "\"Anyone been asking after me?\"  - ask who's looking for you");
			Add("thanks", "\"I won't forget what you did.\"  - thank him", 0, acquaintance != null && (acquaintance.Favours > 0 || acquaintance.Like >= 25), "nothing to thank him for");
			Add("drink", "\"Next round's on me.\"  - buy him a drink, $2", 2);
			Add("loan", "\"Could you wire me ten dollars? I'm good for it.\"  - ask for a loan");
			Add("lookout", "\"Keep an eye out for me, would you?\"  - ask him to watch for trouble", 0, acquaintance != null && !acquaintance.Lookout, "already watching for you");
			Add("sorry", "\"I'm sorry for the trouble I caused.\"  - apologise");
			Add("checkin", "\"How are things? Write back when you can.\"  - a friendly word");
			Add("letter", "\"Thought I'd write. Things are well enough out here.\"  - a letter with your news");
			Add("pgift", "\"Something for you. No need to pay it back.\"  - send a gift, $5", 5);
			string text = PersonLost(acquaintance);
			if (text != null)
			{
				Add("pcondole", "\"Heard about " + text + ". I'm sorry.\"  - your condolences");
			}
			Add("warn", "\"Keep your mouth shut about what you saw.\"  - a warning");
		}
		return o;
		void Add(string k, string text2, int cost = 0, bool ok2 = true, string note = null)
		{
			o.Add(new TgOption
			{
				Key = k,
				Text = Her(text2),
				Cost = cost,
				Ok = ok2,
				Note = ((fem && !to.nemesis) ? Pron.Her(note, female: true) : note)
			});
		}
		string Her(string text2)
		{
			int num = text2.LastIndexOf("\"  - ", StringComparison.Ordinal);
			if (fem && num >= 0)
			{
				return text2.Substring(0, num + 4) + Pron.Her(text2.Substring(num + 4), female: true);
			}
			return text2;
		}
	}

	private bool HandleTelegraphKey(Keys key)
	{
		TelegraphPanel tg = _tg;
		if (tg == null)
		{
			return false;
		}
		int count = tg.Rows.Count;
		if (tg.Page == "read" && count > 0 && (key == Keys.Left || key == Keys.Right))
		{
			tg.Sel = (tg.Sel + ((key == Keys.Right) ? 1 : (count - 1))) % count;
			return true;
		}
		switch (key)
		{
		case Keys.Up:
			if (count > 0)
			{
				do
				{
					tg.Sel = (tg.Sel - 1 + count) % count;
				}
				while (IsHeading(tg, tg.Sel) && tg.Rows.Any(((string key, string text, bool enabled, string note) r) => !r.key.StartsWith("hdr:")));
			}
			return true;
		case Keys.Down:
			if (count > 0)
			{
				do
				{
					tg.Sel = (tg.Sel + 1) % count;
				}
				while (IsHeading(tg, tg.Sel) && tg.Rows.Any(((string key, string text, bool enabled, string note) r) => !r.key.StartsWith("hdr:")));
			}
			return true;
		case Keys.Back:
		case Keys.Left:
			TelegraphBack();
			return true;
		case Keys.Return:
		case Keys.Right:
			if (count == 0 || tg.Sel >= count || !tg.Rows[tg.Sel].enabled)
			{
				return true;
			}
			TelegraphPick(tg.Rows[tg.Sel].key);
			return true;
		default:
			return key == Config.TelegraphKey;
		case Keys.Escape:
			return true;
		}
	}

	private void TelegraphBack()
	{
		TelegraphPanel tg = _tg;
		switch (tg.Page)
		{
		case "main":
			CloseTelegraph();
			return;
		case "what":
			if (tg.ReplyTo != null && tg.Reading != null)
			{
				tg.Page = "read";
				tg.ReplyTo = null;
				tg.Sel = 0;
				tg.Top = 0;
				tg.Line = null;
				TelegraphRows();
				return;
			}
			tg.Page = "to";
			break;
		case "read":
			tg.Page = tg.ReadFrom ?? "inbox";
			tg.Reading = null;
			tg.ReadFrom = null;
			break;
		default:
			tg.Page = "main";
			break;
		}
		tg.Sel = 0;
		tg.Top = 0;
		tg.Line = ((tg.Page == "to") ? "Who's it going to?" : ((tg.Page == "inbox") ? "Here's what came in." : ((tg.Page == "sent") ? "Here's what you've sent." : "Anything else?")));
		TelegraphRows();
	}

	private void TelegraphPick(string key)
	{
		TelegraphPanel tg = _tg;
		if (key == "leave")
		{
			CloseTelegraph();
			return;
		}
		if (key == "send")
		{
			tg.Page = "to";
			tg.Sel = 0;
			tg.Top = 0;
			tg.ReplyTo = null;
			tg.Line = "Who's it going to?";
		}
		else if (key == "inbox")
		{
			tg.Page = "inbox";
			tg.Sel = 0;
			tg.Top = 0;
			tg.Line = "Here's what came in.";
		}
		else if (key == "sentlist")
		{
			tg.Page = "sent";
			tg.Sel = 0;
			tg.Top = 0;
			tg.Line = "Here's what you've sent.";
		}
		else
		{
			if (key.StartsWith("sent:"))
			{
				Telegram telegram = Telegrams.FirstOrDefault((Telegram x) => x.Id == key.Substring(5));
				if (telegram == null)
				{
					return;
				}
				if (!telegram.Arrived)
				{
					tg.Line = $"That one's still on the wire. Due {GameDate.FromMinutes(telegram.ArriveAt):MMM d}.";
					return;
				}
				TelegraphPick("read:" + telegram.Id);
				if (tg.Page == "read")
				{
					tg.ReadFrom = "sent";
				}
				return;
			}
			if (key.StartsWith("to:"))
			{
				string[] parts = key.Split(new char[1] { ':' }, 3);
				bool flag = parts[1] == "n";
				string text = ((!flag) ? People.FirstOrDefault((Acquaintance a) => a.Id == parts[2])?.FullName : Roster.FirstOrDefault((NemesisRecord r) => r.Id == parts[2])?.FullName);
				tg.To = (id: parts[2], nemesis: flag, name: text ?? "?");
				tg.Page = "what";
				tg.Sel = 0;
				tg.Top = 0;
				tg.Line = $"To {tg.To.name}. What's it say?  Answers take {Config.TelegraphMinDays}-{Config.TelegraphMaxDays} days.";
			}
			else if (key.StartsWith("what:"))
			{
				SendTelegram(key.Substring(5));
			}
			else if (key.StartsWith("read:"))
			{
				Telegram t = Telegrams.FirstOrDefault((Telegram x) => x.Id == key.Substring(5));
				if (t == null)
				{
					return;
				}
				if (!t.Read && t.Money > 0)
				{
					PayPlayer(t.Money * 100);
					Feed($"TELEGRAPH ${t.Money} from {t.ToName}", Theme.Gold);
				}
				if (!t.Read && t.TrackId != null)
				{
					Roster.FirstOrDefault((NemesisRecord x) => x.Id == t.TrackId);
				}
				bool flag2 = !t.Read;
				if (flag2 && t.TreasuryGang != null)
				{
					try
					{
						Gang gang = Gangs.ById(t.TreasuryGang);
						CampSite campSite = TreasuryOf(gang);
						if (t.TreasurySite == null || campSite?.Id == t.TreasurySite)
						{
							RevealTreasury(gang, t.ToName);
						}
						else
						{
							ReplyNotice("By the time the wire reached you, the " + gang?.Name + " had moved their money from " + (Camps.ById(t.TreasurySite)?.Label ?? "there") + ".");
							GrudgeLog("TREASURY tip from " + t.ToName + ": stale - the money moved since");
						}
					}
					catch (Exception ex)
					{
						GrudgeLog("TREASURY tip failed: " + ex.Message);
					}
				}
				t.Read = true;
				SaveWorld();
				tg.Reading = t;
				tg.Page = "read";
				tg.Sel = 0;
				tg.ReadFrom = null;
				tg.Line = null;
				if (flag2)
				{
					GrudgeLog("TELEGRAPH read: from " + t.ToName + " - " + t.Reply);
				}
			}
			else
			{
				if (key == "back")
				{
					TelegraphBack();
					return;
				}
				if (key == "reply" && tg.Reading != null)
				{
					Telegram reading = tg.Reading;
					tg.To = (id: reading.ToId, nemesis: reading.ToNemesis, name: reading.ToName);
					tg.ReplyTo = reading.Id;
					tg.Page = "what";
					tg.Sel = 0;
					tg.Top = 0;
					tg.Line = $"Writing back to {reading.ToName}. What's it say?  Answers take {Config.TelegraphMinDays}-{Config.TelegraphMaxDays} days.";
				}
			}
		}
		TelegraphRows();
	}

	private void SendTelegram(string what)
	{
		TelegraphPanel tg = _tg;
		TgOption tgOption = TelegramOptions(tg.To).FirstOrDefault((TgOption m) => m.Key == what);
		if (tgOption != null && tgOption.Ok && CashDollars >= 1 + tgOption.Cost)
		{
			MONEY._MONEY_DECREMENT_CASH_BALANCE((1 + tgOption.Cost) * 100);
			long num = GameMinutesNow();
			int num2 = Config.TelegraphMinDays + _rng.Next(Math.Max(1, Config.TelegraphMaxDays - Config.TelegraphMinDays + 1));
			Telegram t = new Telegram
			{
				Id = NewId(8),
				ToId = tg.To.id,
				ToNemesis = tg.To.nemesis,
				ToName = tg.To.name,
				Message = what,
				Text = tgOption.Text,
				From = tg.Town,
				InReplyTo = tg.ReplyTo,
				SentAt = num,
				ArriveAt = num - num % 1440 + (long)num2 * 1440L + 360 + _rng.Next(1080)
			};
			NemesisRecord nemesisRecord = (t.ToNemesis ? Roster.FirstOrDefault((NemesisRecord r) => r.Id == t.ToId) : null);
			if (what == "gift")
			{
				t.GiftGang = nemesisRecord?.Gang;
			}
			Telegrams.Add(t);
			SaveWorld();
			if (nemesisRecord != null)
			{
				History(nemesisRecord, "TELEGRAM from you: " + t.Text.Split(new string[1] { "  - " }, StringSplitOptions.None)[0], Theme.Dim);
			}
			Feed(string.Format("TELEGRAPH sent to {0} from {1}: {2} - an answer in about {3} day{4}", t.ToName, t.From, what, num2, (num2 == 1) ? "" : "s"), Theme.Gold);
			GrudgeLog(string.Format("TELEGRAPH sent to {0} ({1}, {2}) from {3}, answer due {4:MMM d HH:mm}", t.ToName, t.ToNemesis ? "nemesis" : "person", what, t.From, GameDate.FromMinutes(t.ArriveAt)));
			tg.Page = "main";
			tg.Sel = 0;
			tg.Top = 0;
			tg.ReplyTo = null;
			tg.Reading = null;
			tg.Line = "It's on the wire. Answer'll come here, or any station on the line.";
		}
	}

	private void DeliverTelegrams()
	{
		List<Telegram> telegrams = Telegrams;
		long now = GameMinutesNow();
		int num = 0;
		foreach (Telegram item in telegrams.Where((Telegram x) => !x.Arrived && now >= x.ArriveAt).ToList())
		{
			_historyAt = item.ArriveAt;
			try
			{
				AnswerTelegram(item);
			}
			finally
			{
				_historyAt = null;
			}
			item.Arrived = true;
			num++;
		}
		if (telegrams.Count((Telegram x) => x.Read) > 60)
		{
			foreach (Telegram item2 in (from x in telegrams
				where x.Read
				orderby x.ArriveAt
				select x).Take(telegrams.Count((Telegram x) => x.Read) - 60).ToList())
			{
				telegrams.Remove(item2);
			}
		}
		num += UnaskedTelegrams(now);
		if (num != 0)
		{
			SaveWorld();
			SaveRoster();
			_peopleDirty = true;
			ReplyNotice((num == 1) ? "A telegram is waiting for you at the station." : $"{num} telegrams are waiting for you at the station.");
		}
	}

	private void AnswerTelegram(Telegram t)
	{
		if (t.ToNemesis)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord x) => x.Id == t.ToId);
			if (nemesisRecord == null || !nemesisRecord.IsAlive || nemesisRecord.PresumedDead)
			{
				t.Reply = "WESTERN UNION REGRETS. " + t.ToName.ToUpperInvariant() + " COULD NOT BE REACHED.";
				t.Effect = ((nemesisRecord != null && (!nemesisRecord.IsAlive || nemesisRecord.PresumedDead)) ? "he's dead" : "gone");
				t.Result = "It never reached him.";
			}
			else if (PenAnswers(t, nemesisRecord, null))
			{
				AnswerNemesis(t, nemesisRecord);
				PenSpent(t, nemesisRecord, null);
			}
		}
		else
		{
			Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == t.ToId);
			if (acquaintance == null || acquaintance.Dead)
			{
				t.Reply = "WESTERN UNION REGRETS. " + t.ToName.ToUpperInvariant() + " COULD NOT BE REACHED.";
				t.Effect = "gone";
				t.Result = "It never reached him.";
			}
			else if (PenAnswers(t, null, acquaintance))
			{
				AnswerPerson(t, acquaintance);
				PenSpent(t, null, acquaintance);
			}
		}
		GrudgeLog("TELEGRAPH answer from " + t.ToName + " (" + t.Message + "): " + t.Reply + " [" + t.Effect + "]");
		Feed("TELEGRAPH " + t.ToName + " answered (" + t.Message + "): " + t.Effect, Theme.Gold);
	}

	private static int Facet(NemesisRecord r, string k)
	{
		if (r.Facets == null || !r.Facets.TryGetValue(k, out var value))
		{
			return 50;
		}
		return value;
	}

	private (bool hunts, double weight, HateTier tier) HuntState(NemesisRecord r)
	{
		bool item = r.IsAlive && !r.PresumedDead && r.CanHunt && IntentOf(r) == Intent.Hunts;
		double item2 = 0.0;
		try
		{
			item2 = AmbushWeightOf(r);
		}
		catch
		{
		}
		return (hunts: item, weight: item2, tier: r.HateTier);
	}

	private static string OutlookOf((bool hunts, double weight, HateTier tier) a, (bool hunts, double weight, HateTier tier) b, bool female)
	{
		string text = (female ? "She" : "He");
		if (a.hunts && !b.hunts)
		{
			return text + " won't come looking for you now.";
		}
		if (!a.hunts && b.hunts)
		{
			return text + "'ll come looking for you now.";
		}
		if (!b.hunts)
		{
			if (a.tier == b.tier || b.tier <= a.tier)
			{
				return null;
			}
			return text + " holds it closer - but won't ride out for it.";
		}
		List<string> list = new List<string>();
		if (a.weight > 0.0 && b.weight > 0.0)
		{
			double num = b.weight / a.weight;
			if (num >= 1.15)
			{
				list.Add("sooner");
			}
			else if (num <= 0.87)
			{
				list.Add("less often");
			}
		}
		if (b.tier > a.tier)
		{
			list.Add("further from home");
		}
		else if (b.tier < a.tier)
		{
			list.Add("not so far from home");
		}
		if (list.Count != 0)
		{
			return text + "'ll come for you " + string.Join(", and ", list) + ".";
		}
		return null;
	}

	private void AnswerNemesis(Telegram t, NemesisRecord r)
	{
		Personality temper = r.Personality ?? Personality.Cold;
		(bool, double, HateTier) a = HuntState(r);
		int h0 = r.Hate;
		int fear = r.Fear;
		int num = EffectiveFear(r);
		Dictionary<string, string> dictionary = new Dictionary<string, string> { ["name"] = r.Name };
		string why = "";
		string text;
		switch (t.Message)
		{
		case "peace":
			if (Roll(40 - (r.Hate - 50) / 2 + num / 4 + ((Is(Personality.Coward) || Is(Personality.Drifter)) ? 20 : (Is(Personality.Greedy) ? 5 : (Is(Personality.Loyal) ? (-5) : (Is(Personality.Hothead) ? (-20) : (Is(Personality.Proud) ? (-25) : 0))))) - ((r.TimesKilledPlayer > 0) ? 10 : 0), "peace"))
			{
				r.Hate = Floor(8);
				Remember(r, "Joy", 25, "you wrote to make peace");
				text = "peace_yes";
				break;
			}
			if (Is(Personality.Proud) || Is(Personality.Hothead))
			{
				r.AddHate(3);
			}
			text = "peace_no";
			break;
		case "sorry":
			if (Roll(30 - (r.Hate - 50) / 2 + num / 5 + (Is(Personality.Loyal) ? 10 : ((Is(Personality.Drifter) || Is(Personality.Coward)) ? 15 : (Is(Personality.Proud) ? (-15) : ((Is(Personality.Hothead) || Is(Personality.Cold)) ? (-10) : 0)))) + ((Facet(r, "Pride") < 40) ? 10 : 0) + ((r.TimesKilledPlayer > 0) ? 5 : 0), "apology"))
			{
				r.Hate = Floor(6);
				Remember(r, "Joy", 20, "you wrote to say sorry");
				text = "sorry_yes";
				break;
			}
			if (Is(Personality.Proud) || Is(Personality.Hothead))
			{
				r.AddHate(2);
			}
			text = "sorry_no";
			break;
		case "threat":
			if (Is(Personality.Cold))
			{
				r.AddFear(6);
				text = "threat_cold";
			}
			else if (Is(Personality.Coward) || Is(Personality.Greedy) || Is(Personality.Drifter) || num >= 50 || Facet(r, "Bravery") < 35)
			{
				r.AddFear(12);
				Remember(r, "Fear", 40, "you threatened him by wire");
				text = "threat_scared";
			}
			else
			{
				r.AddFear(4);
				r.AddHate(6);
				Remember(r, "Anger", 40, "you threatened him by wire");
				text = "threat_angry";
			}
			break;
		case "family":
			if (Is(Personality.Coward) || num >= 55 || Facet(r, "Bravery") < 35)
			{
				r.AddFear(25);
				r.AddHate(4);
				Remember(r, "Fear", 60, "you threatened his family");
				text = "family_scared";
			}
			else
			{
				r.AddHate(Is(Personality.Loyal) ? 16 : 12);
				Remember(r, "Anger", 70, "you threatened his family");
				text = "family_rage";
			}
			break;
		case "coming":
			if (Is(Personality.Coward) || num >= 50)
			{
				r.AddFear(10);
				text = "coming_scared";
			}
			else
			{
				r.AddFear(4);
				r.AddHate(4);
				text = "coming_ready";
			}
			break;
		case "taunt":
			if (Is(Personality.Proud) || Is(Personality.Hothead) || Facet(r, "Pride") >= 65)
			{
				r.AddHate(8);
				Remember(r, "Shame", 45, "you mocked him by wire");
				text = "taunt_rage";
			}
			else
			{
				r.AddHate(2);
				text = "taunt_shrug";
			}
			break;
		case "money":
		{
			if (Is(Personality.Proud) && _rng.Next(100) < 70)
			{
				r.AddHate(4);
				t.Money = 10;
				text = "money_insult";
				why = "proud - sent it back";
				break;
			}
			long now7 = GameMinutesNow() - 10080;
			int num4 = Chronicle.Telegrams?.Count((Telegram x) => x != t && x.ToId == t.ToId && x.Message == "money" && x.Arrived && x.ArriveAt >= now7) ?? 0;
			float num5 = num4 switch
			{
				2 => 0.17f, 
				1 => 0.5f, 
				0 => 1f, 
				_ => 0f, 
			};
			r.Hate = Floor((int)Math.Round((float)(Is(Personality.Greedy) ? 12 : 6) * num5));
			if (num5 < 1f)
			{
				why = ((num4 >= 3) ? "money again - it buys nothing now" : $"money again - worth {num5:P0}");
			}
			if (r.Needs != null && r.Needs.ContainsKey("Money"))
			{
				r.Needs["Money"] = Math.Max(0, r.Needs["Money"] - 30);
			}
			text = "money_thanks";
			break;
		}
		case "payoff":
			if (Roll(25 + (Is(Personality.Greedy) ? 40 : (Is(Personality.Coward) ? 25 : (Is(Personality.Drifter) ? 15 : (Is(Personality.Proud) ? (-30) : (Is(Personality.Loyal) ? (-20) : (Is(Personality.Hothead) ? (-10) : 0)))))) + num / 4 - (r.Hate - 50) / 3 + ((r.Wealth == Wealth.Rich) ? (-15) : ((r.Wealth == Wealth.Poor) ? 15 : 0)), "payoff"))
			{
				r.Hate = Floor(Is(Personality.Greedy) ? 25 : 18);
				if (r.Needs != null && r.Needs.ContainsKey("Money"))
				{
					r.Needs["Money"] = 0;
				}
				Remember(r, "Joy", 30, "you paid him off");
				text = "payoff_yes";
			}
			else
			{
				t.Money = 25;
				if (Is(Personality.Proud) || Is(Personality.Hothead))
				{
					r.AddHate(4);
				}
				text = "payoff_no";
			}
			break;
		case "boss":
		{
			NemesisRecord nemesisRecord = BossOf(r);
			dictionary["boss"] = nemesisRecord?.Name ?? Gangs.Of(r)?.LeaderName ?? "THE BOSS";
			bool flag = r.Quirks != null && r.Quirks.Contains("traitor");
			if (nemesisRecord != null && nemesisRecord.IsAlive && nemesisRecord.CampId != null && !nemesisRecord.CampKnown && Roll(10 + num / 2 + (Is(Personality.Coward) ? 20 : (Is(Personality.Greedy) ? 15 : (Is(Personality.Drifter) ? 10 : (Is(Personality.Loyal) ? (-30) : (Is(Personality.Proud) ? (-15) : 0))))) - Facet(r, "Loyalty") / 4 + (flag ? 30 : 0) - (r.Hate - 50) / 4, "gives up his boss"))
			{
				dictionary["camp"] = Camps.ById(nemesisRecord.CampId)?.Label ?? "THE OLD PLACE";
				RevealCamp(nemesisRecord, found: false);
				nemesisRecord.CampUnsure = false;
				t.TrackId = nemesisRecord.Id;
				History(nemesisRecord, r.FullName + " gave away where he rides from - by telegram", Theme.Warn);
				Remember(r, "Fear", 30, "he gave up his boss by wire");
				text = "boss_tell";
			}
			else
			{
				if (Is(Personality.Loyal) || Is(Personality.Proud))
				{
					r.AddHate(3);
				}
				text = "boss_no";
			}
			break;
		}
		case "distrust":
		{
			NemesisRecord boss = BossOf(r);
			dictionary["boss"] = boss?.Name ?? "HIM";
			bool flag2 = r.Quirks != null && r.Quirks.Contains("traitor");
			if (boss != null)
			{
				int num3 = 15 + (Is(Personality.Greedy) ? 20 : (Is(Personality.Proud) ? 15 : (Is(Personality.Hothead) ? 10 : (Is(Personality.Loyal) ? (-25) : (Is(Personality.Cold) ? (-10) : 0))))) - Facet(r, "Loyalty") / 3 + ((r.Stress >= 60) ? 10 : 0) + (flag2 ? 25 : 0);
				List<Goal> goals = r.Goals;
				if (Roll(num3 + ((goals != null && goals.Any((Goal g) => g.Type == "Rise")) ? 10 : 0), "turns on his boss"))
				{
					r.Ties = r.Ties ?? new List<Tie>();
					Tie tie = r.Ties.FirstOrDefault((Tie x) => x.Id == boss.Id);
					if (tie == null)
					{
						r.Ties.Add(new Tie
						{
							Id = boss.Id,
							Type = "rival",
							Strength = -30,
							Note = "your telegram"
						});
					}
					else
					{
						tie.Strength = Math.Max(-100, tie.Strength - 30);
						if (tie.Strength < 0 && tie.Type != "kin")
						{
							tie.Type = "rival";
						}
					}
					r.Stress = Math.Min(100, r.Stress + 15);
					Remember(r, "Anger", 40, "you wrote that " + boss.Name + " was selling him out", boss.Id);
					History(r, "TURNED ON " + boss.FullName.ToUpperInvariant() + " after your telegram", Theme.Warn);
					text = "distrust_yes";
					break;
				}
			}
			r.AddHate(3);
			text = "distrust_no";
			break;
		}
		case "kin":
		{
			(NemesisRecord, string) tuple = KinOf(r).FirstOrDefault(((NemesisRecord who, string rel) k) => !k.who.IsAlive && k.who.KilledByPlayer);
			dictionary["kin"] = tuple.Item1?.Name ?? "HIM";
			dictionary["rel"] = tuple.Item2 ?? "KIN";
			if (Is(Personality.Coward) || num >= 60)
			{
				r.AddFear(15);
				Remember(r, "Grief", 50, "you wrote to gloat about " + dictionary["kin"]);
				text = "kin_grief";
			}
			else
			{
				r.AddHate(15);
				Remember(r, "Anger", 75, "you wrote to gloat about " + dictionary["kin"]);
				text = "kin_rage";
			}
			break;
		}
		case "wish":
			if (Is(Personality.Proud) || Is(Personality.Hothead))
			{
				if (_rng.Next(100) < 50)
				{
					r.AddHate(1);
				}
				text = "wish_scorn";
			}
			else if (Roll(55 - (r.Hate - 50) / 2 + ((Is(Personality.Coward) || Is(Personality.Drifter) || Is(Personality.Loyal)) ? 15 : 0), "takes it kindly"))
			{
				r.Hate = Floor(4);
				Remember(r, "Joy", 15, "you wrote to wish him well");
				text = "wish_yes";
			}
			else
			{
				text = "wish_no";
			}
			break;
		case "condole":
		{
			(NemesisRecord, string) tuple2 = KinOf(r).FirstOrDefault(((NemesisRecord who, string rel) k) => !k.who.IsAlive && !k.who.KilledByPlayer && !k.who.Story);
			dictionary["kin"] = tuple2.Item1?.Name ?? "HIM";
			dictionary["rel"] = tuple2.Item2 ?? "KIN";
			if (tuple2.Item1 == null)
			{
				text = "wish_no";
				break;
			}
			r.Hate = Floor(Is(Personality.Loyal) ? 10 : (Is(Personality.Cold) ? 4 : 7));
			Remember(r, "Joy", 25, "you wrote when " + tuple2.Item1.Name + " died");
			text = (Is(Personality.Cold) ? "condole_cold" : "condole_yes");
			break;
		}
		case "gift":
		{
			Gang gang = Gangs.Of(r);
			Personality? personality = ((gang != null) ? LeaderTemper(gang) : ((Personality?)null));
			dictionary["leader"] = gang?.LeaderName ?? "THE BOSS";
			if (gang == null)
			{
				text = "gift_back";
				t.Money = 25;
			}
			else if (personality == Personality.Proud && _rng.Next(100) < 60)
			{
				t.Money = 25;
				text = "gift_back";
				why = "a proud leader sent it back";
			}
			else if (personality == Personality.Hothead && _rng.Next(100) < 35)
			{
				text = "gift_scorn";
				why = "a hothead leader took it as an insult";
			}
			else
			{
				int amount = ((personality == Personality.Greedy) ? 8 : ((personality == Personality.Coward) ? 6 : ((personality == Personality.Cold || personality == Personality.Drifter) ? 4 : ((personality == Personality.Loyal) ? 3 : 5))));
				int num2 = LowerNotoriety(gang, amount, "you sent the " + gang.Name + " a gift", null, 0, byWire: true);
				why = string.Format("notoriety -{0} ({1} leader)", num2, personality?.ToString().ToLowerInvariant() ?? "no");
				r.Hate = Floor(2);
				text = "gift_yes";
			}
			break;
		}
		default:
			text = "peace_no";
			break;
		}
		if (text == "sorry_yes")
		{
			Gang gang2 = Gangs.Of(r);
			if (gang2 != null && !gang2.OrdersNotFeelings)
			{
				int num6 = LowerNotoriety(gang2, (Is(Personality.Loyal) || Is(Personality.Cold) || Is(Personality.Coward)) ? 3 : 2, r.Name + " took your apology to the " + gang2.Name, "apology", 6, byWire: true);
				if (num6 > 0)
				{
					why = ((why != "") ? (why + ", ") : "") + $"notoriety -{num6}";
				}
			}
		}
		if (text != null && text.StartsWith("gift_"))
		{
			Gang gang3 = Gangs.Of(r);
			if (gang3 != null && IsLeader(gang3, r))
			{
				text += "_self";
			}
		}
		string text2 = TelegramLines.Pick(text, r.Gang, temper, _rng, dictionary, IsLeader(Gangs.Of(r), r)) ?? "RECEIVED.";
		if (t.Money > 0)
		{
			text2 += $"  (${t.Money} ENCLOSED)";
		}
		t.Reply = text2;
		t.Effect = string.Format("hate {0} -> {1}, fear {2} -> {3}{4}", h0, r.Hate, fear, r.Fear, (why != "") ? (" - " + why) : "");
		(bool, double, HateTier) b = HuntState(r);
		List<string> list = new List<string>();
		if (r.Hate <= h0 - 3)
		{
			list.Add("has eased off you");
		}
		else if (r.Hate >= h0 + 3)
		{
			list.Add("hates you more");
		}
		if (r.Fear >= fear + 3)
		{
			list.Add("is more afraid of you");
		}
		else if (r.Fear <= fear - 3)
		{
			list.Add("is less afraid of you");
		}
		List<string> list2 = new List<string>();
		list2.Add((list.Count > 0) ? ("He " + string.Join(" and ", list) + ".") : ((text.EndsWith("_no") || text.EndsWith("_shrug") || text.EndsWith("_scorn")) ? "It didn't move him." : "Nothing much changed between you."));
		if (text == "boss_tell")
		{
			list2.Add("Where his boss rides from is marked on your map.");
		}
		if (text == "distrust_yes")
		{
			list2.Add("He's turned against his boss.");
		}
		if (why.Contains("notoriety -"))
		{
			Gang gang4 = Gangs.Of(r);
			if (gang4 != null)
			{
				list2.Add("The " + gang4.Name + " think a little better of you.");
			}
		}
		if (t.Money > 0)
		{
			list2.Add("Your money came back.");
		}
		t.Result = Pron.Her(string.Join(" ", list2), Models.Female(r.ModelHash));
		t.Outlook = OutlookOf(a, b, Models.Female(r.ModelHash));
		if (t.Outlook != null)
		{
			t.Effect += string.Format(" | {0} -> {1}, weight {2:0.00} -> {3:0.00}", a.Item1 ? "hunts" : "doesn't hunt", b.Item1 ? "hunts" : "doesn't hunt", a.Item2, b.Item2);
		}
		r.Touch();
		History(r, $"ANSWERED YOUR TELEGRAM: \"{text2}\" - hate {h0} -> {r.Hate}, fear {fear} -> {r.Fear}", (r.Hate < h0) ? Theme.Good : Theme.Warn);
		int Floor(int cut)
		{
			return Math.Max(Math.Min(h0, SocialFloor(r)), h0 - cut);
		}
		bool Is(Personality p)
		{
			return temper == p;
		}
		bool Roll(int chance, string what)
		{
			chance = Clamp(chance, 2, 90);
			bool flag3 = _rng.Next(100) < chance;
			why = string.Format("{0} {1}% - {2}", what, chance, flag3 ? "yes" : "no");
			return flag3;
		}
	}

	private void AnswerPerson(Telegram t, Acquaintance a)
	{
		TemperProfile temperProfile = Tempers.Of(a.Temper);
		Personality result;
		Personality? personality = (Enum.TryParse<Personality>(a.Temper ?? "", out result) ? new Personality?(result) : ((Personality?)null));
		int like = a.Like;
		int fear = a.Fear;
		int grudge = a.Grudge;
		float num = PenWorth(t);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string text = null;
		string text2 = null;
		string text3 = "";
		if (num < 1f && FriendlyKeys.Contains(t.Message ?? ""))
		{
			text3 = $"letters this week - worth {num:P0}";
		}
		switch (t.Message)
		{
		case "regards":
			a.Like = Math.Min(100, a.Like + Gain(6, temperProfile.LikeGain * num));
			text2 = ((a.Fear >= 40) ? "regards_wary" : ((a.Like >= 40) ? "regards_warm" : "regards_plain"));
			break;
		case "news":
			a.Like = Math.Min(100, a.Like + 2);
			text = TownNewsFor(a.Town);
			break;
		case "sorry":
			a.Grudge = Math.Max(0, a.Grudge - 15);
			a.Fear = Math.Max(0, a.Fear - 5);
			a.Like = Math.Min(100, a.Like + Gain(4, temperProfile.LikeGain));
			text2 = ((a.Grudge > 20) ? "sorry_cold" : "sorry_ok");
			break;
		case "warn":
			a.Fear = Math.Min(100, a.Fear + Gain(15, temperProfile.FearGain));
			a.Grudge = Math.Min(100, a.Grudge + 10);
			a.Like = Math.Max(0, a.Like - 10);
			if (a.Reported)
			{
				text2 = "warn_late";
				break;
			}
			a.KeptQuiet = a.Fear >= 40 || a.KeptQuiet;
			text2 = ((a.Fear >= 40) ? "warn_quiet" : "warn_defy");
			break;
		case "thanks":
			a.Like = Math.Min(100, a.Like + Gain(8, temperProfile.LikeGain * num));
			text2 = ((a.Like >= 40) ? "thanks_warm" : "thanks_plain");
			break;
		case "drink":
			if (_rng.Next(100) < 85 - a.Grudge / 2)
			{
				a.Like = Math.Min(100, a.Like + Gain(10, temperProfile.LikeGain * num));
				text2 = "drink_yes";
			}
			else
			{
				t.Money = 2;
				text2 = "drink_no";
			}
			break;
		case "loan":
		{
			int num4 = Clamp((a.Like - 30) * 2 + a.Favours * 15 - a.Fear / 2 - a.Grudge, 0, 90);
			bool num5 = Telegrams.Any((Telegram x) => x != t && x.ToId == t.ToId && x.Message == "loan" && x.Arrived && x.Money >= 10 && t.SentAt - x.ArriveAt < 10080);
			if (num5)
			{
				num4 = 0;
			}
			bool flag2 = _rng.Next(100) < num4;
			text3 = (num5 ? "loan refused - he lent you money this week" : string.Format("loan {0}% - {1}", num4, flag2 ? "yes" : "no"));
			if (flag2)
			{
				t.Money = 10;
				a.Like = Math.Max(0, a.Like - 3);
				text2 = "loan_yes";
			}
			else
			{
				a.Like = Math.Max(0, a.Like - 2);
				text2 = "loan_no";
			}
			break;
		}
		case "asking":
		{
			NemesisRecord nemesisRecord = HunterNear(a.Town);
			if (nemesisRecord != null)
			{
				dictionary["man"] = nemesisRecord.Name;
				dictionary["town"] = a.Town ?? "TOWN";
				text2 = "asking_name";
				text3 = "named " + nemesisRecord.Name;
			}
			else
			{
				text2 = "asking_none";
			}
			break;
		}
		case "letter":
			a.Like = Math.Min(100, a.Like + Gain(5, temperProfile.LikeGain * num));
			if (a.Grudge > 0)
			{
				a.Grudge = Math.Max(0, a.Grudge - 2);
			}
			text2 = ((a.Fear >= 40) ? "regards_wary" : ((a.Like >= 40) ? "checkin_warm" : "checkin_plain"));
			break;
		case "checkin":
			a.Like = Math.Min(100, a.Like + Gain(4, temperProfile.LikeGain * num));
			if (a.Grudge > 0)
			{
				a.Grudge = Math.Max(0, a.Grudge - 3);
			}
			text2 = ((a.Fear >= 40) ? "regards_wary" : ((a.Like >= 40) ? "checkin_warm" : "checkin_plain"));
			break;
		case "pgift":
		{
			if (personality == Personality.Proud && _rng.Next(100) < 40)
			{
				t.Money = 5;
				a.Like = Math.Min(100, a.Like + 2);
				text2 = "pgift_back";
				text3 = "proud - sent it back";
				break;
			}
			long now7 = GameMinutesNow() - 10080;
			int num3 = Telegrams.Count((Telegram x) => x != t && x.ToId == t.ToId && x.Message == "pgift" && x.Arrived && x.ArriveAt >= now7);
			a.Like = Math.Min(100, a.Like + Gain((int)Math.Round(12f * num3 switch
			{
				1 => 0.5f, 
				0 => 1f, 
				_ => 0.2f, 
			}), temperProfile.LikeGain * ((personality == Personality.Greedy) ? 1.4f : 1f)));
			a.Grudge = Math.Max(0, a.Grudge - 5);
			a.Fear = Math.Max(0, a.Fear - 3);
			text2 = "pgift_yes";
			break;
		}
		case "pcondole":
		{
			dictionary["kin"] = PersonLost(a) ?? "HIM";
			List<string> witnessed = a.Witnessed;
			if (witnessed != null && witnessed.Any((string w) => w.Contains("killed his friend") || w.Contains("killed her friend")))
			{
				a.Grudge = Math.Min(100, a.Grudge + 10);
				a.Like = Math.Max(0, a.Like - 10);
				text2 = "pcondole_rage";
				text3 = "he knows it was you";
			}
			else
			{
				a.Like = Math.Min(100, a.Like + Gain(10, temperProfile.LikeGain));
				a.Grudge = Math.Max(0, a.Grudge - 8);
				text2 = "pcondole_yes";
			}
			break;
		}
		case "lookout":
		{
			int num2 = Clamp((int)((float)a.Like * 1.2f) + a.Favours * 15 - a.Fear / 3 - a.Grudge + 10, 0, 95);
			bool flag = _rng.Next(100) < num2;
			text3 = string.Format("lookout {0}% - {1}", num2, flag ? "yes" : "no");
			if (flag)
			{
				a.Lookout = true;
				text2 = "lookout_yes";
			}
			else
			{
				text2 = "lookout_no";
			}
			break;
		}
		}
		text = text ?? TelegramLines.Pick(text2 ?? "regards_plain", null, personality, _rng, dictionary) ?? "RECEIVED.";
		if (FriendlyKeys.Contains(t.Message ?? "") && a.Like >= 35 && a.Fear < 40)
		{
			text = text + "  " + PenLetterLine(a);
		}
		if (t.Money > 0)
		{
			text += $"  (${t.Money} ENCLOSED)";
		}
		t.Reply = text;
		t.Effect = string.Format("likes you {0} -> {1}, fear {2} -> {3}{4}{5}", like, a.Like, fear, a.Fear, (a.Grudge != grudge) ? $", grudge {grudge} -> {a.Grudge}" : "", (text3 != "") ? (" - " + text3) : "");
		List<string> list = new List<string>();
		if (a.Like >= like + 2)
		{
			list.Add("likes you more");
		}
		else if (a.Like <= like - 2)
		{
			list.Add("likes you less");
		}
		if (a.Fear >= fear + 3)
		{
			list.Add("is more afraid of you");
		}
		else if (a.Fear <= fear - 3)
		{
			list.Add("is less afraid of you");
		}
		if (a.Grudge <= grudge - 3)
		{
			list.Add("holds less against you");
		}
		else if (a.Grudge >= grudge + 3)
		{
			list.Add("holds it against you");
		}
		List<string> list2 = new List<string> { (list.Count > 0) ? ("He " + string.Join(" and ", list) + ".") : "Nothing much changed between you." };
		if (t.Message == "lookout" && a.Lookout)
		{
			list2.Add("He'll wire you if trouble turns up near " + (a.Town ?? "him") + ".");
		}
		if (t.Message == "warn" && a.KeptQuiet && !a.Reported)
		{
			list2.Add("He'll keep quiet.");
		}
		if (t.Message == "loan" && t.Money > 0)
		{
			list2.Add("He sent the money.");
		}
		else if (t.Money > 0)
		{
			list2.Add("Your money came back.");
		}
		if (text2 == "asking_name")
		{
			list2.Add("Someone's been asking after you.");
		}
		t.Result = Pron.Her(string.Join(" ", list2), Models.Female(a.Model));
		_peopleDirty = true;
		static int Gain(int n, float mult)
		{
			return Math.Max(1, (int)Math.Round((float)n * mult));
		}
	}

	private string PersonLost(Acquaintance a)
	{
		if (a?.Circle == null)
		{
			return null;
		}
		foreach (string k in a.Circle)
		{
			if (k.StartsWith("n:"))
			{
				NemesisRecord nemesisRecord = FindRecord(k.Substring(2));
				if (nemesisRecord != null && !nemesisRecord.IsAlive)
				{
					return nemesisRecord.Name;
				}
				continue;
			}
			Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == k);
			if (acquaintance != null && acquaintance.Dead)
			{
				return acquaintance.Name;
			}
		}
		return null;
	}

	private NemesisRecord HunterNear(string town)
	{
		if (string.IsNullOrEmpty(town))
		{
			return null;
		}
		return (from n in Roster
			where n.IsAlive && !n.PresumedDead && !IsArmy(n) && n.HateTier >= HateTier.Vengeful && (n.KillLocation == town || TownAt(new Vector3(n.OriginX, n.OriginY, n.OriginZ)) == town)
			orderby IntentOf(n) == Intent.Hunts descending, n.Hate descending
			select n).FirstOrDefault();
	}

	private int UnaskedTelegrams(long now)
	{
		if (_nextUnaskedAt == 0L || _nextUnaskedAt - now > 60)
		{
			_nextUnaskedAt = now + 60;
			return 0;
		}
		if (now < _nextUnaskedAt)
		{
			return 0;
		}
		_nextUnaskedAt = now + 60;
		if (Telegrams.Any((Telegram x) => x.Spontaneous && now - x.ArriveAt < 1440))
		{
			return 0;
		}
		Telegram telegram = null;
		int num = _rng.Next(1000);
		List<NemesisRecord> source = Roster.Where((NemesisRecord n) => n.IsAlive && !n.PresumedDead && n.Id != null && !IsArmy(n) && (n.Gang == null || n.TimesAmbushed > 0 || n.TimesKilledPlayer > 0 || n.TemperKnown)).ToList();
		if (num < 15)
		{
			List<NemesisRecord> list = source.Where((NemesisRecord n) => n.HateTier >= HateTier.Vengeful && n.Personality != Personality.Coward).ToList();
			if (list.Count > 0)
			{
				NemesisRecord nemesisRecord = list.OrderByDescending((NemesisRecord n) => n.Hate + _rng.Next(40)).First();
				bool flag = nemesisRecord.TimesKilledPlayer > 0 && _rng.Next(100) < 35;
				telegram = Make(nemesisRecord.Id, nem: true, nemesisRecord.FullName, flag ? "in_gloat" : "in_threat", TelegramLines.Pick(flag ? "in_gloat" : "in_threat", nemesisRecord.Gang, nemesisRecord.Personality, _rng, null, IsLeader(Gangs.Of(nemesisRecord), nemesisRecord)));
				nemesisRecord.Touch();
				History(nemesisRecord, "WIRED YOU: \"" + telegram.Reply + "\"", Theme.Warn);
			}
		}
		else if (num < 25)
		{
			NemesisRecord nemesisRecord2 = (from _ in source
				where _.Fear >= 70
				orderby _rng.Next()
				select _).FirstOrDefault();
			if (nemesisRecord2 != null)
			{
				telegram = Make(nemesisRecord2.Id, nem: true, nemesisRecord2.FullName, "in_plea", TelegramLines.Pick("in_plea", nemesisRecord2.Gang, nemesisRecord2.Personality, _rng));
				History(nemesisRecord2, "WIRED YOU, BEGGING: \"" + telegram.Reply + "\"", Theme.Good);
			}
		}
		else if (num < 45)
		{
			foreach (Acquaintance item in from _ in People
				where _.Lookout && !_.Dead && _.Id != null
				orderby _rng.Next()
				select _)
			{
				NemesisRecord nemesisRecord3 = HunterNear(item.Town);
				if (nemesisRecord3 != null)
				{
					telegram = Make(item.Id, nem: false, item.FullName, "in_tip", TelegramLines.Pick("in_tip", null, null, _rng, new Dictionary<string, string>
					{
						["man"] = nemesisRecord3.Name,
						["town"] = item.Town
					}));
					break;
				}
			}
		}
		else if (num < 55)
		{
			Acquaintance acquaintance = (from _ in People
				where _.Like >= 50 && !_.Dead && _.Id != null
				orderby _rng.Next()
				select _).FirstOrDefault();
			if (acquaintance != null)
			{
				telegram = Make(acquaintance.Id, nem: false, acquaintance.FullName, "in_regards", TelegramLines.Pick("in_regards", null, null, _rng));
			}
		}
		else if (num < 85)
		{
			telegram = PenPalLetter(now);
		}
		else if (num < 95)
		{
			var (acquaintance2, gang, campSite) = TreasuryTraitor();
			if (acquaintance2 != null)
			{
				telegram = Make(acquaintance2.Id, nem: false, acquaintance2.FullName, "in_treasury", "THE " + UiDraw.TheFix(gang.Name).ToUpperInvariant() + " KEEP THEIR MONEY AT " + campSite.Label.ToUpperInvariant() + ". STRONGBOXES. YOU DID NOT HEAR IT FROM ME.");
				telegram.TreasuryGang = gang.Id;
				telegram.TreasurySite = campSite.Id;
			}
		}
		if (telegram == null || telegram.Reply == null)
		{
			return 0;
		}
		Telegrams.Add(telegram);
		GrudgeLog("TELEGRAPH unasked from " + telegram.ToName + " (" + telegram.Message + "): " + telegram.Reply);
		Feed("TELEGRAPH " + telegram.ToName + " wired you: " + SpontLabel(telegram.Message).ToLowerInvariant(), Theme.Gold);
		return 1;
		Telegram Make(string id, bool nem, string name, string kind, string reply)
		{
			return new Telegram
			{
				Id = NewId(8),
				ToId = id,
				ToNemesis = nem,
				ToName = name,
				Message = kind,
				SentAt = now,
				ArriveAt = now,
				Arrived = true,
				Spontaneous = true,
				Reply = reply,
				Effect = ""
			};
		}
	}

	private string TownNewsFor(string town)
	{
		if (!string.IsNullOrEmpty(town))
		{
			NemesisRecord nemesisRecord = (from n in Roster
				where n.IsAlive && !n.PresumedDead && !IsArmy(n) && (n.KillLocation == town || TownAt(new Vector3(n.OriginX, n.OriginY, n.OriginZ)) == town)
				orderby n.Hate descending
				select n).FirstOrDefault();
			if (nemesisRecord != null)
			{
				return nemesisRecord.Name.ToUpperInvariant() + " HAS BEEN SEEN ABOUT " + town.ToUpperInvariant() + ". HE ASKS AFTER YOU. WATCH YOURSELF.";
			}
			WorldEvent worldEvent = (from e in Chronicle.Events
				where e.Place != null && e.Place.Contains(town)
				orderby e.GameMin descending
				select e).FirstOrDefault();
			if (worldEvent != null)
			{
				return "PEOPLE ARE TALKING ABOUT IT HERE: " + worldEvent.Title.ToUpperInvariant() + ".";
			}
		}
		return "QUIET HERE. NOTHING WORTH THE WIRE.";
	}

	private void UpdateTelegraphPanel(Ped player)
	{
		TelegraphPanel tg = _tg;
		if (tg.Clerk == null || !tg.Clerk.Exists() || tg.Clerk.IsDead || tg.Clerk.Position.DistanceTo(player.Position) > 6f || player.IsDead || player.IsInCombat)
		{
			CloseTelegraph();
			return;
		}
		LockPlayerInPlace();
		PAD.DISABLE_ALL_CONTROL_ACTIONS(0);
		PAD.ENABLE_CONTROL_ACTION(0, 2844205919u, enableRelatedActions: true);
		PAD.ENABLE_CONTROL_ACTION(0, 3523508616u, enableRelatedActions: true);
		HUD._UI_PROMPT_DISABLE_PROMPTS_THIS_FRAME();
		UiDraw.FrameReserve = 60;
		DrawTelegraph(tg);
	}

	private static bool IsHeading(TelegraphPanel tg, int i)
	{
		if (i >= 0 && i < tg.Info.Count && tg.Info[i] != null)
		{
			return tg.Info[i].Heading;
		}
		return false;
	}

	private static (string a, string b, string c, string d, float bx, float cx, float dx) TgColumns(string page)
	{
		return page switch
		{
			"what" => (a: "MESSAGE", b: "WHAT THEY READ", c: "COST", d: null, bx: 0.36f, cx: 0f, dx: 0f), 
			"sent" => (a: "TO", b: "OUTFIT / TOWN", c: "RANK", d: "STATUS", bx: 0.33f, cx: 0.56f, dx: 0.73f), 
			"inbox" => (a: "FROM", b: "OUTFIT / TOWN", c: "RANK", d: "WHAT IT IS", bx: 0.33f, cx: 0.56f, dx: 0.73f), 
			"to" => (a: "NAME", b: "OUTFIT / TOWN", c: "RANK", d: "FEELING", bx: 0.33f, cx: 0.56f, dx: 0.73f), 
			_ => (a: null, b: null, c: null, d: null, bx: 0f, cx: 0f, dx: 0f), 
		};
	}

	private static void BoldTitle(string s, float x, float y, float scale, (int r, int g, int b) c)
	{
		UiDraw.TextFont(s, UiDraw.TitleFont, x, y, scale, c.r, c.g, c.b);
		UiDraw.TextFont(s, UiDraw.TitleFont, x + 0.00052083336f, y, scale, c.r, c.g, c.b);
	}

	private void DrawTelegraph(TelegraphPanel tg)
	{
		if (tg.Page == "read" && tg.Reading != null)
		{
			DrawTelegramSheet(tg, tg.Reading);
			return;
		}
		bool flag = tg.Info.Any((TgInfo i) => i != null && !i.Heading);
		(string a, string b, string c, string d, float bx, float cx, float dx) tuple = TgColumns(tg.Page);
		string item = tuple.a;
		string item2 = tuple.b;
		string item3 = tuple.c;
		string item4 = tuple.d;
		float item5 = tuple.bx;
		float item6 = tuple.cx;
		float item7 = tuple.dx;
		flag = flag && item != null;
		bool flag2 = flag && item4 != null;
		float num = ((!flag) ? 0.46f : (flag2 ? 0.7f : 0.58f));
		float num2 = 0.5f - num / 2f;
		if (tg.Sel < tg.Top)
		{
			tg.Top = tg.Sel;
		}
		if (tg.Sel >= tg.Top + 8)
		{
			tg.Top = tg.Sel - 8 + 1;
		}
		int num3 = Math.Min(8, Math.Max(1, tg.Rows.Count));
		List<string> list = WrapW(tg.Line ?? "", 0.27f, num - 0.028f, (tg.Page == "read") ? 5 : 2);
		bool flag3 = tg.Page == "read" && !string.IsNullOrEmpty(tg.Reading?.Outlook);
		bool flag4 = tg.Page == "read" && tg.Reading?.Effect != null && _debugOn;
		int num4 = (flag3 ? 1 : 0) + (flag4 ? 1 : 0);
		float num5 = (float)(list.Count - 1 + num4) * 0.025f;
		int num6;
		if (!(tg.Page == "to") && !(tg.Page == "what") && !(tg.Page == "inbox"))
		{
			num6 = ((tg.Page == "sent") ? 1 : 0);
			if (num6 == 0)
			{
				goto IL_024f;
			}
		}
		else
		{
			num6 = 1;
		}
		if (tg.Sel < 0 || tg.Sel >= tg.Info.Count)
		{
			goto IL_024f;
		}
		object obj = tg.Info[tg.Sel]?.Detail;
		goto IL_026f;
		IL_026f:
		string text = (string)obj;
		List<string> list2 = (string.IsNullOrEmpty(text) ? new List<string>() : WrapW(text, 0.21f, num - 0.05f, 2));
		float num7 = (flag ? 0.024f : 0f);
		float num8 = ((num6 != 0) ? (0.006f + (float)Math.Max(1, (!(tg.Page == "what")) ? 1 : 2) * 0.023f) : 0f);
		float num9 = 0.088f + num5 + num7 + (float)num3 * 0.027f + num8 + 0.03f;
		float num10 = 0.958f - num9;
		Theme.Brush(num2, num10, num, num9, 255);
		string s = FitW("WESTERN UNION  -  " + tg.Town.ToUpperInvariant(), 0.3f, num - 0.06f, caps: true);
		BoldTitle(s, num2 + num / 2f - Theme.CapsWidth(s, 0.3f) / 2f, num10 + 0.01f, 0.3f, Theme.Title);
		for (int num11 = 0; num11 < list.Count; num11++)
		{
			UiDraw.Text(list[num11], num2 + num / 2f - Theme.MixedWidth(list[num11], 0.27f, 0.0222f) / 2f, num10 + 0.04f + (float)num11 * 0.025f, 0.27f, 248, 244, 232);
		}
		float num12 = num10 + 0.04f + (float)list.Count * 0.025f;
		if (flag3)
		{
			string text2 = FitW(tg.Reading.Outlook, 0.23f, num - 0.03f);
			UiDraw.Text(text2, num2 + num / 2f - Theme.MixedWidth(text2, 0.23f, 0.0222f) / 2f, num12, 0.23f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
			num12 += 0.025f;
		}
		if (flag4)
		{
			string text3 = FitW("(" + tg.Reading.Effect + ")", 0.2f, num - 0.03f);
			UiDraw.Text(text3, num2 + num / 2f - Theme.MixedWidth(text3, 0.2f, 0.0222f) / 2f, num12, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		Theme.CenterRule(num2, num10 + 0.07f + num5, num, 0.02f, 150);
		float num13 = num10 + 0.078f + num5;
		float num14 = num2 + 0.022f;
		float num15 = num2 + num * item5;
		float num16 = num2 + num * item6;
		float num17 = num2 + num * item7;
		float num18 = num2 + num - 0.026f;
		if (flag)
		{
			BoldTitle(item, num14, num13 - 0.002f, 0.19f, Theme.Gold);
			BoldTitle(item2, num15, num13 - 0.002f, 0.19f, Theme.Gold);
			float x = ((tg.Page == "what") ? (num18 - Theme.CapsWidth(item3, 0.19f)) : num16);
			BoldTitle(item3, x, num13 - 0.002f, 0.19f, Theme.Gold);
			if (flag2)
			{
				BoldTitle(item4, num17, num13 - 0.002f, 0.19f, Theme.Gold);
			}
			Theme.HRule(num2 + 0.016f, num13 + 0.019f, num - 0.032f, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 110);
			num13 += num7;
		}
		for (int num19 = tg.Top; num19 < Math.Min(tg.Rows.Count, tg.Top + 8); num19++)
		{
			(string key, string text, bool enabled, string note) tuple2 = tg.Rows[num19];
			string item8 = tuple2.text;
			bool item9 = tuple2.enabled;
			string item10 = tuple2.note;
			TgInfo tgInfo = ((num19 < tg.Info.Count) ? tg.Info[num19] : null);
			if (tgInfo != null && tgInfo.Heading)
			{
				float num20 = Theme.CapsWidth(item8, 0.21f);
				float num21 = num2 + num / 2f - num20 / 2f;
				BoldTitle(item8, num21, num13 + 0.002f, 0.21f, Theme.Title);
				float num22 = num2 + 0.022f;
				float num23 = num21 - 0.012f;
				float num24 = num21 + num20 + 0.012f;
				if (num23 > num22)
				{
					Theme.HRule(num22, num13 + 0.013f, num23 - num22, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 120);
				}
				if (num18 > num24)
				{
					Theme.HRule(num24, num13 + 0.013f, num18 - num24, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 120);
				}
				num13 += 0.027f;
				continue;
			}
			bool flag5 = (Theme.MarqueeOn = num19 == tg.Sel);
			int ti = num19;
			UiMouse.Region(num2 + 0.01f, num13 - 0.002f, num - 0.02f, 0.026f, delegate
			{
				if (_tg == null || _tg.Sel == ti)
				{
					return false;
				}
				_tg.Sel = ti;
				return true;
			}, delegate
			{
				UiMouse.Send(Keys.Return);
			});
			if (flag5)
			{
				Theme.SelectBox(num2 + 0.01f, num13 - 0.002f, num - 0.02f, 0.026f);
			}
			(int, int, int) tuple3 = ((!item9 && (tgInfo == null || !tgInfo.Wait)) ? Theme.Faint : (flag5 ? Theme.SelectedText : Theme.Text));
			if (tgInfo == null || !flag)
			{
				UiDraw.Text(FitW(item8 + ((!item9 && item10 != null) ? ("  (" + item10 + ")") : ""), 0.26f, num - 0.04f), num14, num13 + 0.001f, 0.26f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
				num13 += 0.027f;
				continue;
			}
			if (flag2)
			{
				bool num25 = !item9 && !tgInfo.Wait;
				UiDraw.Text(FitW(tgInfo.A ?? "", 0.25f, num15 - num14 - 0.012f), num14, num13 + 0.001f, 0.25f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
				(int, int, int) tuple4 = (num25 ? Theme.Faint : tgInfo.Bc);
				UiDraw.Text(FitW(tgInfo.B ?? "", 0.22f, num16 - num15 - 0.012f), num15, num13 + 0.003f, 0.22f, tuple4.Item1, tuple4.Item2, tuple4.Item3);
				(int, int, int) tuple5 = (num25 ? Theme.Faint : (flag5 ? Theme.SelectedText : tgInfo.Cc));
				UiDraw.Text(FitW(tgInfo.C ?? "", 0.22f, num17 - num16 - 0.012f), num16, num13 + 0.003f, 0.22f, tuple5.Item1, tuple5.Item2, tuple5.Item3);
				(int, int, int) tuple6 = (num25 ? Theme.Faint : tgInfo.Dc);
				float num26 = num17;
				if (tgInfo.New)
				{
					num26 += Theme.Tag("NEW", num26, num13 + 0.001f, Theme.Blood) + 0.008f;
				}
				float num27 = (tgInfo.Wait ? (Theme.MixedWidth("WAITING", 0.17f, 0.0222f) + 0.014f) : 0f);
				float num28 = num18 - num27 - (tgInfo.Wait ? 0.008f : 0f);
				if (tg.Page == "to")
				{
					UiDraw.TextFont(FitW(tgInfo.D ?? "", 0.2f, num28 - num26, caps: true), UiDraw.TitleFont, num26, num13 + 0.004f, 0.2f, tuple6.Item1, tuple6.Item2, tuple6.Item3);
				}
				else
				{
					UiDraw.Text(FitW(tgInfo.D ?? "", 0.22f, num28 - num26), num26, num13 + 0.003f, 0.22f, tuple6.Item1, tuple6.Item2, tuple6.Item3);
				}
				if (tgInfo.Wait)
				{
					Theme.Tag("WAITING", num18 - num27, num13 + 0.001f, Theme.Dim);
				}
				num13 += 0.027f;
				continue;
			}
			float num29 = num15 - 0.012f;
			UiDraw.Text(FitW(tgInfo.A ?? "", 0.25f, num29 - num14), num14, num13 + 0.001f, 0.25f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
			(int, int, int) tuple7 = ((!item9 && !tgInfo.Wait) ? Theme.Faint : tgInfo.Bc);
			float num30 = ((tg.Page == "what") ? (num18 - 0.05f) : (num16 - 0.012f));
			if (tg.Page == "to")
			{
				UiDraw.TextFont(FitW(tgInfo.B ?? "", 0.21f, num30 - num15, caps: true), UiDraw.TitleFont, num15, num13 + 0.004f, 0.21f, tuple7.Item1, tuple7.Item2, tuple7.Item3);
			}
			else
			{
				UiDraw.Text(FitW(tgInfo.B ?? "", 0.22f, num30 - num15), num15, num13 + 0.003f, 0.22f, tuple7.Item1, tuple7.Item2, tuple7.Item3);
			}
			(int, int, int) tuple8 = ((!item9 && !tgInfo.Wait) ? Theme.Faint : tgInfo.Cc);
			if (tgInfo.Right)
			{
				float num31 = Theme.MixedWidth(tgInfo.C ?? "", 0.23f, 0.0222f);
				UiDraw.Text(tgInfo.C ?? "", num18 - num31, num13 + 0.002f, 0.23f, tuple8.Item1, tuple8.Item2, tuple8.Item3);
			}
			else
			{
				float num32 = num16;
				if (tgInfo.New)
				{
					num32 += Theme.Tag("NEW", num32, num13 + 0.001f, Theme.Blood) + 0.008f;
				}
				float num33 = (tgInfo.Wait ? (Theme.MixedWidth("WAITING", 0.17f, 0.0222f) + 0.014f) : 0f);
				if (!string.IsNullOrEmpty(tgInfo.C))
				{
					UiDraw.Text(FitW(tgInfo.C, 0.22f, num18 - num32 - num33 - 0.008f), num32, num13 + 0.003f, 0.22f, tuple8.Item1, tuple8.Item2, tuple8.Item3);
				}
				if (tgInfo.Wait)
				{
					Theme.Tag("WAITING", num18 - num33, num13 + 0.001f, Theme.Dim);
				}
			}
			num13 += 0.027f;
		}
		Theme.MarqueeOn = true;
		Theme.ScrollBar(num2 + num - 0.008f, num13 - (float)Math.Min(8, tg.Rows.Count) * 0.027f, (float)Math.Min(8, tg.Rows.Count) * 0.027f, tg.Top, 8, tg.Rows.Count);
		if (num8 > 0f)
		{
			for (int num34 = 0; num34 < list2.Count && num34 < ((!(tg.Page == "what")) ? 1 : 2); num34++)
			{
				string text4 = list2[num34];
				(int, int, int) tuple9 = ((tg.Page == "what") ? Theme.Text : Theme.Dim);
				UiDraw.Text(text4, num2 + num / 2f - Theme.MixedWidth(text4, 0.21f, 0.0222f) / 2f, num13 + 0.006f + (float)num34 * 0.023f, 0.21f, tuple9.Item1, tuple9.Item2, tuple9.Item3);
			}
			num13 += num8;
		}
		Theme.CenterRule(num2, num13 + 0.004f, num, 0.02f, 120);
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)>
		{
			("D-pad", "Navigate"),
			("A", "Select"),
			("B", (tg.Page == "main") ? "Close" : "Back")
		} : new List<(string, string)>
		{
			("Up/Down", "Navigate"),
			("Enter", "Select"),
			("Backspace", (tg.Page == "main") ? "Close" : "Back")
		});
		Theme.HintRow(num2 + 0.016f, num2 + num - 0.016f, num13 + 0.011f, hints, 0.22f, Theme.Dim);
		return;
		IL_024f:
		obj = null;
		goto IL_026f;
	}

	private void DrawTelegramSheet(TelegraphPanel tg, Telegram t)
	{
		float num = 0.27f;
		float num2 = 0.45999998f;
		string text = TelegramGreeting(t);
		List<string> list = WrapW(t.Reply ?? "", 0.3f, num2 - 0.02f, 7, title: true);
		string text2 = (t.NoAnswer ? "WESTERN UNION" : t.ToName.ToUpperInvariant());
		string text3 = (t.Spontaneous ? null : (t.NoAnswer ? $"YOURS OF {GameDate.FromMinutes(t.SentAt):MMM d} - NO ANSWER".ToUpperInvariant() : $"REPLY TO YOURS OF {GameDate.FromMinutes(t.SentAt):MMM d}".ToUpperInvariant()));
		uint num3 = 1000 + (uint)Seeds.Stable("telegram/" + (t.Id ?? t.ToId ?? "") + "/" + t.ArriveAt) % 9000u;
		string s = FitW($"RECEIVED AT {tg.Town}   *   {GameDate.FromMinutes(t.ArriveAt):MMM d, h:mm tt}   *   NO. {num3}".ToUpperInvariant(), 0.16f, num2, caps: true);
		float num4 = 0.26000002f;
		float num5 = 0.34f;
		float width = 0.74f - num5;
		List<(string label, string text, (int r, int g, int b) c)> info = new List<(string, string, (int, int, int))>();
		if (!t.Spontaneous && !string.IsNullOrEmpty(t.Text))
		{
			Add("YOU WROTE", WrapW(Quote(t.Text), 0.19f, width, 2), Theme.Dim);
		}
		string label = "WHAT IT DID";
		if (!string.IsNullOrEmpty(t.Result))
		{
			Add(label, WrapW(t.Result, 0.19f, width, 2), Theme.Text);
			label = null;
		}
		if (!string.IsNullOrEmpty(t.Outlook))
		{
			Add(label, WrapW(t.Outlook, 0.19f, width, 2), Theme.Gold);
			label = null;
		}
		(string, (int, int, int))? tuple = PenStatus(t);
		if (tuple.HasValue)
		{
			Add(label, WrapW(tuple.Value.Item1, 0.19f, width, 2), tuple.Value.Item2);
			label = null;
		}
		if (_debugOn && !string.IsNullOrEmpty(t.Effect))
		{
			Add(label, new List<string> { FitW(t.Effect, 0.17f, width) }, Theme.Faint);
		}
		float num6 = 0.12000001f + ((text != null) ? 0.027f : 0f) + (float)list.Count * 0.029f + 0.008f + 0.026f + ((text3 != null) ? 0.02f : 0f) + ((t.Money > 0) ? 0.026f : 0f) + 0.012f + 0.015f + 0.014f;
		float num7 = 0.01f + (float)info.Count * 0.02f + ((info.Count > 0) ? 0.012f : 0f) + 0.024f + 0.01f;
		float num8 = num6 + 0.005f + num7;
		float num9 = Math.Max(0.03f, 0.958f - num8);
		UiDraw.Rect(0.24000001f, num9, 0.52f, num6, Paper.r, Paper.g, Paper.b, 255);
		UiDraw.Outline(0.246f, num9 + 0.008f, 0.50799996f, num6 - 0.016f, 196, 182, 150, 255);
		float num10 = num9 + 0.016f;
		OnPaper("WESTERN UNION", 0.4f, num10, Ink, bold: true);
		num10 += 0.034f;
		OnPaper("TELEGRAM", 0.19f, num10, Faded);
		num10 += 0.02f;
		UiDraw.HLine(num, num + num2, num10, Ink.r, Ink.g, Ink.b, 255, 0.0032f);
		UiDraw.HLine(num, num + num2, num10 + 0.0065f, Ink.r, Ink.g, Ink.b, 255, 0.0014f);
		num10 += 0.016f;
		OnPaper(s, 0.16f, num10, Faded);
		num10 += 0.022f;
		UiDraw.HLine(num, num + num2, num10, Faded.r, Faded.g, Faded.b, 255);
		num10 += 0.012f;
		if (text != null)
		{
			UiDraw.TextFont(text, UiDraw.TitleFont, num, num10, 0.23f, Ink.r, Ink.g, Ink.b);
			num10 += 0.027f;
		}
		foreach (string item4 in list)
		{
			OnPaper(item4, 0.3f, num10, t.NoAnswer ? Faded : Ink);
			num10 += 0.029f;
		}
		num10 += 0.008f;
		string text4 = "- " + text2;
		float num11 = num + num2 - 0.01f;
		UiDraw.TextFont(text4, UiDraw.TitleFont, num11 - Theme.CapsWidth(text4, 0.23f), num10, 0.23f, Ink.r, Ink.g, Ink.b);
		num10 += 0.026f;
		if (text3 != null)
		{
			UiDraw.TextFont(text3, UiDraw.TitleFont, num11 - Theme.CapsWidth(text3, 0.15f), num10, 0.15f, Faded.r, Faded.g, Faded.b);
			num10 += 0.02f;
		}
		if (t.Money > 0)
		{
			OnPaper(string.Format("${0} ENCLOSED{1}", t.Money, t.Read ? "  -  PAID TO YOU" : ""), 0.21f, num10 + 0.004f, (r: 46, g: 84, b: 40));
			num10 += 0.026f;
		}
		UiDraw.HLine(num, num + num2, num10 + 0.004f, Faded.r, Faded.g, Faded.b, 255);
		num10 += 0.012f;
		OnPaper(FitW("ALL MESSAGES TAKEN SUBJECT TO THE COMPANY'S TERMS", 0.12f, num2, caps: true), 0.12f, num10, Faded);
		float num12 = num9 + num6 + 0.005f;
		Theme.Brush(0.24000001f, num12, 0.52f, num7, 255);
		num10 = num12 + 0.01f;
		foreach (var (text5, text6, tuple3) in info)
		{
			if (text5 != null)
			{
				BoldTitle(text5, num4, num10 + 0.002f, 0.18f, Theme.Gold);
			}
			UiDraw.Text(text6, num5, num10, 0.19f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
			num10 += 0.02f;
		}
		if (info.Count > 0)
		{
			UiDraw.HLine(num4, 0.74f, num10 + 0.003f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 110, 0.001f);
			num10 += 0.012f;
		}
		float num13 = num4;
		for (int i = 0; i < tg.Rows.Count; i++)
		{
			(string key, string text, bool enabled, string note) tuple4 = tg.Rows[i];
			string item = tuple4.text;
			bool item2 = tuple4.enabled;
			string item3 = tuple4.note;
			float num14 = num13 + (float)i * 0.13f;
			int ti = i;
			UiMouse.Region(num14, num10, 0.12f, 0.024f, delegate
			{
				if (_tg == null || _tg.Sel == ti)
				{
					return false;
				}
				_tg.Sel = ti;
				return true;
			}, delegate
			{
				UiMouse.Send(Keys.Return);
			});
			bool flag = i == tg.Sel;
			if (flag)
			{
				Theme.SelectBox(num14, num10, 0.12f, 0.024f);
			}
			(int, int, int) tuple5 = ((!item2) ? Theme.Faint : (flag ? Theme.SelectedText : Theme.Text));
			string t2 = ((!item2 && item3 != null && flag) ? item3 : item);
			t2 = FitW(t2, 0.2f, 0.11f);
			UiDraw.Text(t2, num14 + 0.06f - Theme.MixedWidth(t2, 0.2f, 0.0222f) / 2f, num10 + 0.003f, 0.2f, tuple5.Item1, tuple5.Item2, tuple5.Item3);
		}
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)>
		{
			("D-pad", "Choose"),
			("A", "Select"),
			("B", "Back")
		} : new List<(string, string)>
		{
			("Left/Right", "Choose"),
			("Enter", "Select"),
			("Backspace", "Back")
		});
		Theme.HintRow(num13 + (float)tg.Rows.Count * 0.13f + 0.012f, 0.74f, num10 + 0.003f, hints, 0.19f, Theme.Dim);
		void Add(string text7, List<string> lines, (int r, int g, int b) c)
		{
			for (int j = 0; j < lines.Count; j++)
			{
				info.Add(((j == 0) ? text7 : null, lines[j], c));
			}
		}
		static void OnPaper(string text7, float sc, float yy, (int r, int g, int b) c, bool bold = false)
		{
			float num15 = Theme.TitleWidth(text7, sc) + (bold ? 0.00052083336f : 0f);
			float num16 = 0.24000001f + (0.52f - num15) / 2f;
			UiDraw.TextFont(text7, UiDraw.TitleFont, num16, yy, sc, c.r, c.g, c.b);
			if (bold)
			{
				UiDraw.TextFont(text7, UiDraw.TitleFont, num16 + 0.00052083336f, yy, sc, c.r, c.g, c.b);
			}
		}
	}

	private string TelegramGreeting(Telegram t)
	{
		if (t.NoAnswer)
		{
			return null;
		}
		string text = (InterroLines.PlayerName ?? "Arthur Morgan").Split(' ')[0].ToUpperInvariant();
		string text2 = (InterroLines.PlayerName ?? "Arthur Morgan").Split(' ').Last().ToUpperInvariant();
		if (t.Message == "in_plea")
		{
			return "MR " + text2 + " -";
		}
		if (t.Message == "in_regards" || t.Message == "in_letter" || t.Message == "in_tip" || t.Message == "in_treasury")
		{
			return text + ",";
		}
		if (!t.ToNemesis)
		{
			Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == t.ToId);
			if (acquaintance != null && acquaintance.Like >= 25)
			{
				return text + ",";
			}
			if (t.Message == "regards" || t.Message == "letter" || t.Message == "checkin" || t.Message == "thanks" || t.Message == "pcondole")
			{
				return text + ",";
			}
			return null;
		}
		if (t.Message == "condole" || t.Message == "wish" || t.Message == "sorry" || t.Message == "peace")
		{
			return text2 + " -";
		}
		return null;
	}

	internal string TelegraphDebugLine()
	{
		List<Telegram> telegrams = Chronicle.Telegrams;
		int num = telegrams?.Count((Telegram t) => !t.Arrived) ?? 0;
		int num2 = telegrams?.Count((Telegram t) => t.Arrived && !t.Read) ?? 0;
		Telegram telegram = (from t in telegrams?.Where((Telegram t) => !t.Arrived)
			orderby t.ArriveAt
			select t).FirstOrDefault();
		return string.Format("telegraph: {0} on the wire, {1} unread{2}{3}", num, num2, (telegram != null) ? $" - next from {telegram.ToName} {GameDate.FromMinutes(telegram.ArriveAt):MMM d HH:mm}" : "", (_clerkNear != null) ? " - clerk here" : "");
	}
}
