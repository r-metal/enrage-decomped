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
	private List<Acquaintance> _people;

	private Dictionary<string, PenPal> Pens => Chronicle.PenPals ?? (Chronicle.PenPals = new Dictionary<string, PenPal>());

	private static string PenKey(Telegram t)
	{
		return (t.ToNemesis ? "n:" : "p:") + t.ToId;
	}

	private static int TemperPen(Personality? p)
	{
		if (p != Personality.Loyal)
		{
			if (p != Personality.Drifter)
			{
				if (p != Personality.Cold)
				{
					if (p != Personality.Coward && p != Personality.Hothead && p != Personality.Proud)
					{
						return 0;
					}
					return -5;
				}
				return -20;
			}
			return 5;
		}
		return 15;
	}

	private static int TemperPenCost(Personality? p)
	{
		if (p != Personality.Cold)
		{
			if (p != Personality.Proud && p != Personality.Hothead)
			{
				if (p != Personality.Greedy && p != Personality.Drifter)
				{
					if (p != Personality.Coward)
					{
						if (p != Personality.Loyal)
						{
							return 12;
						}
						return 8;
					}
					return 10;
				}
				return 14;
			}
			return 16;
		}
		return 22;
	}

	private static Personality? TemperOfAcq(Acquaintance a)
	{
		if (!Enum.TryParse<Personality>(a?.Temper ?? "", out var result))
		{
			return null;
		}
		return result;
	}

	private PenPal PenOf(Telegram t, NemesisRecord r, Acquaintance a)
	{
		long num = GameMinutesNow();
		if (!Pens.TryGetValue(PenKey(t), out var value))
		{
			int v = ((r != null) ? (25 - (r.Hate - 50) / 2 + EffectiveFear(r) / 4 + TemperPen(r.Personality)) : ((a != null) ? (40 + a.Like / 2 - a.Fear / 3 - a.Grudge / 2 + TemperPen(TemperOfAcq(a))) : 30));
			value = new PenPal
			{
				Interest = Clamp(v, 0, 100),
				LastAt = num
			};
			Pens[PenKey(t)] = value;
			GrudgeLog($"PENPAL {t.ToName}: interest in your letters starts at {value.Interest}");
		}
		long num2 = Math.Max(0L, (num - value.LastAt) / 1440);
		if (num2 > 0)
		{
			int num3 = ((a != null && a.Like >= 20) ? 5 : 2);
			value.Interest = Math.Min(100, value.Interest + (int)Math.Min(100L, num2 * num3));
			value.LastAt += num2 * 1440;
			if (value.Stopped && value.Interest >= 30)
			{
				value.Stopped = false;
				GrudgeLog($"PENPAL {t.ToName} would answer you again ({value.Interest})");
			}
		}
		if (a != null && a.Like < 10 && a.Grudge > 40)
		{
			value.Interest = 0;
		}
		return value;
	}

	private bool PenAnswers(Telegram t, NemesisRecord r, Acquaintance a)
	{
		PenPal penPal = PenOf(t, r, a);
		if (penPal.Interest > 0 && !penPal.Stopped)
		{
			return true;
		}
		penPal.Stopped = true;
		t.NoAnswer = true;
		t.Reply = "NO REPLY CAME.";
		t.Effect = $"no answer - interest {penPal.Interest}";
		bool female = ((r != null) ? Models.Female(r.ModelHash) : (a != null && Models.Female(a.Model)));
		t.Result = Pron.Her("He didn't write back. He's had enough of your wires for now.", female);
		GrudgeLog($"PENPAL {t.ToName}: no answer - he's stopped writing back (interest {penPal.Interest})");
		return false;
	}

	private void PenSpent(Telegram t, NemesisRecord r, Acquaintance a)
	{
		PenPal penPal = PenOf(t, r, a);
		int num;
		if (r != null)
		{
			num = TemperPenCost(r.Personality) + ((r.HateTier == HateTier.Murderous) ? 6 : 0);
		}
		else
		{
			num = Math.Max(4, TemperPenCost(TemperOfAcq(a)) - (a?.Like ?? 0) / 10);
			if (t.Message == "warn")
			{
				num += 15;
			}
		}
		int interest = penPal.Interest;
		penPal.Interest = Math.Max(0, penPal.Interest - num);
		penPal.Letters++;
		penPal.LastAt = GameMinutesNow();
		if (penPal.Interest == 0)
		{
			penPal.Stopped = true;
		}
		GrudgeLog(string.Format("PENPAL {0}: interest {1} -> {2} ({3} letters){4}", t.ToName, interest, penPal.Interest, penPal.Letters, penPal.Stopped ? " - he'll stop answering" : ""));
	}

	private float PenWorth(Telegram t)
	{
		long since = GameMinutesNow() - 10080;
		return Telegrams.Count((Telegram x) => x != t && x.ToId == t.ToId && !x.ToNemesis && x.Arrived && !x.NoAnswer && x.ArriveAt >= since && FriendlyKeys.Contains(x.Message ?? "")) switch
		{
			2 => 0.35f, 
			1 => 0.6f, 
			0 => 1f, 
			_ => 0.2f, 
		};
	}

	internal bool IsPenPal(Acquaintance a)
	{
		if (a != null && a.Like >= 40 && Pens.TryGetValue("p:" + a.Id, out var value) && value.Letters >= 2 && !value.Stopped)
		{
			return value.Interest > 0;
		}
		return false;
	}

	private (string text, (int r, int g, int b) c)? PenStatus(Telegram t)
	{
		if (!Pens.TryGetValue(PenKey(t), out var value))
		{
			return null;
		}
		bool female;
		string s;
		(int, int, int) item;
		if (t.ToNemesis)
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord x) => x.Id == t.ToId);
			female = nemesisRecord != null && Models.Female(nemesisRecord.ModelHash);
			if (value.Stopped || value.Interest <= 0)
			{
				s = "He's stopped answering your wires.";
				item = Theme.Warn;
			}
			else
			{
				if (value.Interest >= 25)
				{
					return null;
				}
				s = "He's tiring of your wires - he may not answer many more.";
				item = Theme.Warn;
			}
		}
		else
		{
			Acquaintance acquaintance = People.FirstOrDefault((Acquaintance x) => x.Id == t.ToId);
			female = acquaintance != null && Models.Female(acquaintance.Model);
			if (value.Stopped || value.Interest <= 0)
			{
				s = "He's stopped answering your letters for now.";
				item = Theme.Warn;
			}
			else if (value.Interest < 25)
			{
				s = "He's tiring of your letters - give it a few days.";
				item = Theme.Warn;
			}
			else if (IsPenPal(acquaintance))
			{
				s = $"Pen pals - he's written to you {value.Letters} times and is glad to hear from you.";
				item = Theme.Good;
			}
			else
			{
				if (value.Interest < 60)
				{
					return null;
				}
				s = "He's glad to hear from you.";
				item = Theme.Good;
			}
		}
		return (Pron.Her(s, female), item);
	}

	private string PenLetterLine(Acquaintance a)
	{
		string text = PenLife[_rng.Next(PenLife.Length)];
		if (_rng.Next(100) < 35)
		{
			string text2 = TownNewsFor(a.Town);
			if (!text2.StartsWith("QUIET"))
			{
				text = text + "  " + text2;
			}
		}
		return text;
	}

	private Telegram PenPalLetter(long now)
	{
		Acquaintance acquaintance = (from _ in People
			where !_.Dead && _.Id != null && IsPenPal(_) && !Telegrams.Any((Telegram t) => t.ToId == _.Id && t.Spontaneous && now - t.ArriveAt < 4320) && !Telegrams.Any((Telegram t) => t.ToId == _.Id && !t.Arrived)
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (acquaintance == null)
		{
			return null;
		}
		string text = (new string[4] { "HOPE THIS FINDS YOU WELL.", "THOUGHT I WOULD WRITE.", "BEEN A WHILE. HOW ARE YOU KEEPING.", "NO NEWS. WRITING ANYWAY." })[_rng.Next(4)];
		GrudgeLog("PENPAL " + acquaintance.FullName + " wrote to you first");
		return new Telegram
		{
			Id = NewId(8),
			ToId = acquaintance.Id,
			ToNemesis = false,
			ToName = acquaintance.FullName,
			Message = "in_letter",
			SentAt = now,
			ArriveAt = now,
			Arrived = true,
			Spontaneous = true,
			Reply = text + "  " + PenLetterLine(acquaintance) + "  WRITE WHEN YOU CAN.",
			Effect = ""
		};
	}

	private static string SpontLabel(string kind)
	{
		return kind switch
		{
			"in_threat" => "A threat", 
			"in_gloat" => "A gloating letter", 
			"in_plea" => "A plea for mercy", 
			"in_tip" => "A warning", 
			"in_treasury" => "A tip-off", 
			"in_regards" => "A friendly note", 
			"in_letter" => "A letter", 
			_ => "A letter", 
		};
	}

	private static string AnswerTo(string key)
	{
		switch (key)
		{
		case "peace":
			return "peace offer";
		case "sorry":
		case "pcondole":
			if (!(key == "sorry"))
			{
				return "condolences";
			}
			return "apology";
		case "money":
			return "money";
		case "payoff":
			return "offer to pay him off";
		case "threat":
		case "family":
			return "threat";
		case "coming":
			return "warning that you're coming";
		case "taunt":
		case "kin":
			return "taunt";
		case "boss":
			return "question about his boss";
		case "distrust":
			return "word about his boss";
		case "wish":
			return "good wishes";
		case "condole":
			return "condolences";
		case "pgift":
		case "gift":
			return "gift";
		case "regards":
			return "regards";
		case "news":
			return "question about the news";
		case "asking":
			return "question";
		case "thanks":
			return "thanks";
		case "drink":
			return "drink";
		case "loan":
			return "request for a loan";
		case "lookout":
			return "request";
		case "warn":
			return "warning";
		case "letter":
		case "checkin":
			return "letter";
		default:
			return "wire";
		}
	}

	private static string InboxNote(Telegram t)
	{
		string text = (t.NoAnswer ? "No reply came" : (t.Spontaneous ? SpontLabel(t.Message) : ("Reply to your " + AnswerTo(t.Message))));
		if (t.Money > 0 && !t.Read)
		{
			text += $"  -  ${t.Money} enclosed";
		}
		return text;
	}

	private string TelegramContext(Telegram t, bool fem)
	{
		string text = $"{GameDate.FromMinutes(t.Spontaneous ? t.ArriveAt : t.SentAt):MMM d}";
		return Pron.Her(t.NoAnswer ? ("You wired him from " + (t.From ?? "a station") + " on " + text + ". Western Union could not raise an answer.") : (t.Spontaneous ? (SpontLabel(t.Message) + " he sent you on " + text + ".") : ("His answer to your " + AnswerTo(t.Message) + ", wired from " + (t.From ?? "a station") + " on " + text + ".")), fem);
	}
}
