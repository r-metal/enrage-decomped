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
	private const int RelSwingCap = 20;

	private readonly Dictionary<string, int> _relSwing = new Dictionary<string, int>();

	private bool QuestionFits(Interrogation it, QuestionRow q)
	{
		string[] array = (q.Requires ?? "").Split(new char[1] { '&' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			switch (array[i].Trim())
			{
			case "gang":
				if (it.Gang == null)
				{
					return false;
				}
				break;
			case "named":
				if (it.Who == null)
				{
					return false;
				}
				break;
			case "boss":
				if (it.Who == null || BossOf(it.Who) == null)
				{
					return false;
				}
				break;
			case "officer":
				if (it.Who == null || !IsOfficer(it.Who))
				{
					return false;
				}
				break;
			case "top":
				if (it.Who == null || it.Gang?.Tiers == null || it.Gang.Tiers.Length == 0 || it.Who.Rank < it.Gang.Tiers[0].MinRank)
				{
					return false;
				}
				break;
			case "camps":
				if (it.Gang == null || Camps.ForGang(it.Gang.Id).Length == 0)
				{
					return false;
				}
				break;
			case "kin":
				if (it.Who == null || KinOf(it.Who).Count == 0)
				{
					return false;
				}
				break;
			case "funeral":
				if (FuneralToTell(it.Gang) == null)
				{
					return false;
				}
				break;
			case "news":
				if (FuneralToTell(it.Gang) == null && !Roster.Any((NemesisRecord n) => n.Gang == it.Gang?.Id && n.IsAlive && !n.PresumedDead && n != it.Who && n.Rank >= 1 && n.Rank < 4))
				{
					return false;
				}
				break;
			}
		}
		return true;
	}

	private static bool IsBigIntel(QuestionRow q)
	{
		if (q != null)
		{
			return BigIntel.Contains(q.Answer);
		}
		return false;
	}

	private int IntelCap(Interrogation it)
	{
		Personality personality = it.Temper ?? Personality.Cold;
		if (personality == Personality.Coward || personality == Personality.Greedy || personality == Personality.Drifter || (it.Rec != null && Quirks.Has(it.Rec, "traitor")))
		{
			return 2;
		}
		if (personality == Personality.Loyal || personality == Personality.Proud || personality == Personality.Cold)
		{
			return 1;
		}
		return (int)(1 + (uint)Seeds.Stable("intel/" + (it.Rec?.Name ?? it.Ped?.Handle.ToString() ?? "")) % 2u);
	}

	private int IntelSoFar(Interrogation it)
	{
		return Math.Max(it.IntelGiven, it.Rec?.IntelGiven ?? 0);
	}

	private bool IntelSpent(Interrogation it)
	{
		return IntelSoFar(it) >= IntelCap(it);
	}

	private bool Answered(Interrogation it, QuestionRow q)
	{
		if (!it.AnsweredNow.Contains(q.Id))
		{
			if (q.Once && it.Rec?.Asked != null)
			{
				string value;
				if (!it.Rec.Asked.Contains(q.Id))
				{
					return it.Rec.Asked.Any((string a) => Questions.Merged.TryGetValue(a, out value) && value == q.Id);
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private int QuestionNeed(Interrogation it, QuestionRow q)
	{
		return Math.Max(0, Math.Min(95, it.CampNeed + q.Need));
	}

	private int QuestionChance(Interrogation it, QuestionRow q)
	{
		int num = QuestionNeed(it, q);
		if (it.Terror < (float)num)
		{
			return 0;
		}
		int num2 = (int)Math.Round((it.Terror - (float)num) * it.Tw);
		return Math.Max(3, Math.Min(90, it.Common + num2 + q.Odds + it.BribeBonus));
	}

	private int GateState(Interrogation it)
	{
		int num = (it.ScaredSinceThreat ? 1 : 0) + ((Game.GameTime < it.NextAskAt) ? 2 : 0);
		QuestionRow[] all = Questions.All;
		foreach (QuestionRow questionRow in all)
		{
			it.Locks.TryGetValue(questionRow.Id, out var value);
			num = num * 31 + ((it.Terror >= (float)QuestionNeed(it, questionRow)) ? 1 : 0) * 2 + ((value > 0f && it.Terror < value) ? 1 : 0) + (TooScaredToLie(it) ? 4 : 0);
		}
		return num;
	}

	private bool TooScaredToLie(Interrogation it)
	{
		return it.Terror >= (float)(it.CampNeed + 50);
	}

	private int LieChance(Interrogation it, QuestionRow q, out string why)
	{
		why = "";
		if (!q.Lies)
		{
			return 0;
		}
		if (TooScaredToLie(it))
		{
			why = "too scared to lie";
			return 0;
		}
		NemesisRecord rec = it.Rec;
		if (rec == null)
		{
			why = "a nobody, 15";
			return 15;
		}
		if (Quirks.Has(rec, "honest"))
		{
			why = "honest to a fault";
			return 0;
		}
		List<string> parts = new List<string>();
		int c = 10;
		Add((50 - Mind.F(rec, "Honesty")) / 2, "honesty");
		if (it.Temper == Personality.Loyal)
		{
			Add(15, "loyal");
		}
		if (it.Temper == Personality.Coward)
		{
			Add(-10, "coward");
		}
		Add((rec.Hate - 50) / 6, "hate");
		Add(-(int)((it.Terror - (float)QuestionNeed(it, q)) / 2f), "fear");
		if (it.Bribed && (it.Temper == Personality.Greedy || Mind.N(rec, "Money") >= 60))
		{
			Add(-20, "paid");
		}
		if (Quirks.Has(rec, "braggart"))
		{
			Add(10, "braggart");
		}
		if (Quirks.Has(rec, "traitor"))
		{
			Add(-15, "traitor");
		}
		Add(-10 * Math.Min(3, rec.LiedToYou), "caught before");
		c = Math.Max(0, Math.Min(75, c));
		why = string.Join(", ", parts);
		return c;
		void Add(int v, string w)
		{
			if (v != 0)
			{
				c += v;
				parts.Add($"{w} {v:+0;-0}");
			}
		}
	}

	private void BuildQuestionOptions(Convo c)
	{
		Interrogation it = c.It;
		string[] cats = Questions.Cats;
		foreach (string cat in cats)
		{
			List<QuestionRow> list = Questions.All.Where((QuestionRow q) => q.Cat == cat && QuestionFits(it, q)).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			bool flag = it.OpenCat == cat;
			bool spent = IntelSpent(it);
			int num = list.Count((QuestionRow q) => !Answered(it, q) && (!spent || !IsBigIntel(q)));
			list = list.Where((QuestionRow q) => !Answered(it, q) && (!spent || !IsBigIntel(q))).ToList();
			c.Options.Add(new ConvoOption
			{
				Key = "cat:" + cat,
				Text = cat + "...",
				Cat = true,
				Open = flag,
				Enabled = true,
				Note = ((num > 0) ? null : (spent ? "he won't say another word" : "nothing more he'd tell"))
			});
			if (!flag)
			{
				continue;
			}
			foreach (QuestionRow item in list)
			{
				int num2 = QuestionNeed(it, item);
				int num3 = RollOdds(it, item);
				it.Locks.TryGetValue(item.Id, out var value);
				string text = (Answered(it, item) ? "answered" : ((spent && IsBigIntel(item)) ? "he won't say another word" : ((it.Terror < (float)num2 && !Unscared(it)) ? "not scared enough" : ((value > 0f && it.Terror < value) ? "scare him more" : null))));
				bool num4 = text == "answered" || text == "he won't say another word";
				if (text == null && Game.GameTime < it.NextAskAt)
				{
					text = "he's answering";
				}
				bool flag2 = text == "scare him more" || text == "he's answering";
				if (!num4 && !flag2 && it.Terror >= 85f)
				{
					text = null;
				}
				c.Options.Add(new ConvoOption
				{
					Key = "q:" + item.Id,
					Text = item.Text,
					Indent = 1,
					Odds = ((text == null) ? num3 : (-1)),
					Enabled = (text == null),
					Note = text
				});
			}
		}
		if (!it.Bribed && it.Rec != null)
		{
			int num5 = BribeCost(it);
			bool female = it.Ped != null && it.Ped.Exists() && !PED.IS_PED_MALE(it.Ped.Handle);
			bool flag3 = Unscared(it);
			c.Options.Add(new ConvoOption
			{
				Key = "ibribe",
				Text = Pron.Her($"[Pay him ${num5}]", female),
				Enabled = (!flag3 && CashDollars >= num5),
				Note = (flag3 ? "not scared of you" : ((CashDollars >= num5) ? null : "not enough money"))
			});
		}
		string text2 = ((it.ThreatsUsed >= 3) ? "used up" : ((!it.ScaredSinceThreat) ? "scare him first" : null));
		c.Options.Add(new ConvoOption
		{
			Key = "ithreat",
			Text = (_debugOn ? $"[Threaten]  {it.ThreatsUsed}/3" : "[Threaten]"),
			Enabled = (text2 == null),
			Note = text2
		});
		c.Options.Add(Unscared(it) ? new ConvoOption
		{
			Key = "idone",
			Text = "That's all."
		} : new ConvoOption
		{
			Key = "irelease",
			Text = "Let him go."
		});
	}

	private int BribeCost(Interrogation it)
	{
		return 10 + 5 * Math.Max(1, it.Rec?.Rank ?? 1);
	}

	private bool ToggleCategory(Convo c, string cat, bool? open = null)
	{
		Interrogation it = c.It;
		bool flag = open ?? (it.OpenCat != cat);
		it.OpenCat = (flag ? cat : null);
		string key = "cat:" + cat;
		BuildOptions(c);
		c.Sel = Math.Max(0, c.Options.FindIndex((ConvoOption o) => o.Key == key) + (flag ? 1 : 0));
		if (c.Sel >= c.Options.Count)
		{
			c.Sel = c.Options.Count - 1;
		}
		return true;
	}

	private int RollOdds(Interrogation it, QuestionRow q)
	{
		if (q == null)
		{
			return 0;
		}
		if (Unscared(it))
		{
			return 0;
		}
		int num = (it.AskedAny ? QuestionChance(it, q) : FirstAskOdds(it, q, QuestionChance(it, q)));
		if (it.Terror >= 85f)
		{
			num = Math.Max(num, (int)Math.Round(40f + (it.Terror - 85f) * 3f));
		}
		return num;
	}

	private int FirstAskOdds(Interrogation it, QuestionRow q, int odds)
	{
		float num = (it.Temper ?? Personality.Cold) switch
		{
			Personality.Loyal => -0.2f, 
			Personality.Hothead => 0f, 
			Personality.Greedy => 0.15f, 
			Personality.Drifter => 0.2f, 
			Personality.Coward => 0.35f, 
			_ => -0.1f, 
		};
		float num2 = Math.Max(0f, it.Terror - (float)QuestionNeed(it, q)) / 100f;
		float num3 = Math.Max(0.15f, Math.Min(1f, 0.35f + num + num2));
		return Math.Max(0, (int)Math.Round((float)odds * num3));
	}

	private static bool MaxFear(Interrogation it)
	{
		return it.Terror >= 99.5f;
	}

	private string ReasonHeWontSay(Interrogation it, QuestionRow q)
	{
		Personality personality = it.Temper ?? Personality.Cold;
		string text = LeaderAsSaidBy(it.Gang, it.Rec) ?? ((it.Gang != null && IsLeader(it.Gang, it.Rec)) ? "my own men" : "the boss");
		if (Answered(it, q))
		{
			string[] array = new string[3] { "I already told you that. Nothing's changed since.", "I said it once. Saying it twice won't make it truer.", "You heard me the first time." };
			return array[_rng.Next(array.Length)];
		}
		string[] array2 = personality switch
		{
			Personality.Drifter => new string[2] { "That's the whole of what I know. I just ride with 'em, I don't run 'em.", "Nobody tells a man like me more than that." }, 
			Personality.Hothead => new string[2] { "I'm done talking! You hear me? Done!", "That's it! That's all of it, damn you!" }, 
			Personality.Greedy => new string[2] { "That's all I know that's worth anything - and you got it for free.", "There's nothing else to sell, mister. You cleaned me out." }, 
			Personality.Proud => new string[2] { "You've had all you're getting. Shoot me if you like.", "I've shamed myself enough for one day. No more." }, 
			Personality.Loyal => new string[2]
			{
				"I've said more than I should have. The rest dies with me.",
				"I gave you what I gave you. I won't sell out " + text + " any further."
			}, 
			Personality.Coward => new string[2]
			{
				"I can't - I told you too much already. " + text + " finds out, I'm a dead man either way.",
				"Please - that's all I had. I swear on my mother, there's nothing more."
			}, 
			_ => new string[2]
			{
				"That's everything they ever told me. They don't tell men like me the rest.",
				"Any more than that and " + text + " would know it came from me."
			}, 
		};
		return array2[_rng.Next(array2.Length)];
	}

	private void AskQuestion(Convo c, ConvoOption o)
	{
		Interrogation it = c.It;
		QuestionRow questionRow = Questions.All.FirstOrDefault((QuestionRow x) => "q:" + x.Id == o.Key);
		if (questionRow == null)
		{
			return;
		}
		NemesisRecord rec = it.Rec;
		Personality personality = it.Temper ?? Personality.Cold;
		Ped character = Game.Player.Character;
		if (Game.GameTime < it.NextAskAt)
		{
			return;
		}
		it.NextAskAt = Game.GameTime + 2500;
		if (Answered(it, questionRow) || (IntelSpent(it) && IsBigIntel(questionRow)))
		{
			c.Line = ReasonHeWontSay(it, questionRow);
			Feed("ASKED " + Speaker(it) + " (" + questionRow.Cat.ToLowerInvariant() + ": " + questionRow.Answer + "): he's done with that - \"" + c.Line + "\"", Theme.Dim, rec);
			if (!Unscared(it) || !Strain(c, it, 1f, "too many questions"))
			{
				BuildOptions(c);
			}
			return;
		}
		int num = _rng.Next(100);
		bool flag = !it.AskedAny;
		it.AskedAny = true;
		bool flag2 = num < o.Odds;
		bool flag3 = !flag2 && questionRow.Lies && (personality == Personality.Cold || personality == Personality.Loyal) && !TooScaredToLie(it) && it.Rec != null && !Unscared(it) && !Quirks.Has(it.Rec, "honest") && _rng.Next(100) < 45;
		if (flag3)
		{
			flag2 = true;
		}
		if (flag2)
		{
			string why;
			int num2 = LieChance(it, questionRow, out why);
			bool flag4 = flag3 || (num2 > 0 && _rng.Next(100) < num2);
			if (flag3)
			{
				why = ((why == "") ? "" : (why + ", ")) + "lied instead of refusing";
			}
			Say(it.Ped, it.Ped.IsHogtied ? "PLEAD_HOGTIED" : "PLEAD", character);
			it.AnsweredNow.Add(questionRow.Id);
			it.Empty = false;
			c.Line = Answer(it, questionRow, flag4);
			if (questionRow.Once && rec != null && !it.Empty && !rec.Asked.Contains(questionRow.Id))
			{
				rec.Asked.Add(questionRow.Id);
			}
			if (!it.Empty && !flag4 && IsBigIntel(questionRow))
			{
				it.IntelGiven = IntelSoFar(it) + 1;
				if (rec != null)
				{
					rec.IntelGiven = it.IntelGiven;
				}
				if (IntelSpent(it))
				{
					string text = InterroLines.Pick("lock", it.Gang, personality, _rng, null, null, LeaderOut(it.Gang, it.Rec)) ?? "That's all you're getting. I won't say another word.";
					c.Line = c.Line + " " + text;
					Feed($"INTEL {Speaker(it)} has given up all he will ({IntelSoFar(it)} of {IntelCap(it)}) - he won't say another word about his outfit", Theme.Dim, rec);
				}
			}
			if (c.Line != null && c.Line.Length < 70 && _rng.Next(100) < 55)
			{
				string text2 = InterroLines.Pick((it.Terror >= 60f) ? "lead_scared" : "lead", it.Gang, personality, _rng, null, null, LeaderOut(it.Gang, it.Rec));
				if (text2 != null)
				{
					c.Line = text2 + " " + c.Line;
				}
			}
			Feed(string.Format("ASKED {0} ({1}: {2}): {3}%, rolled {4} -> TALKS{5}", Speaker(it), questionRow.Cat.ToLowerInvariant(), questionRow.Answer, o.Odds, num, questionRow.Lies ? string.Format(" - lie {0}%{1}{2}", num2, (why != "") ? (" (" + why + ")") : "", flag4 ? " LIED" : "") : ""), (flag4 && _debugOn) ? Theme.Warn : Theme.Good, rec);
			if (questionRow.Answer == "bargain" && it.BargainStruck)
			{
				EndConvoSoon(c, "bargain");
				return;
			}
		}
		else
		{
			it.Round++;
			if (rec != null)
			{
				int hate = rec.Hate;
				rec.AddHate(2);
				CapScuffle(rec, hate);
			}
			c.Line = InterroLines.Pick((it.Terror >= 55f) ? "refuse_scared" : "refuse", it.Gang, personality, _rng, BossOf(rec)?.Name, null, LeaderOut(it.Gang, it.Rec)) ?? Defiance[_rng.Next(Defiance.Length)];
			Say(it.Ped, DefiantLines[_rng.Next(DefiantLines.Length)], character);
			it.Tries.TryGetValue(questionRow.Id, out var value);
			value++;
			it.Tries[questionRow.Id] = value;
			if (value >= Balance.InterroTriesPerScare)
			{
				it.Locks[questionRow.Id] = it.Terror + (float)Balance.InterroScareToRetry;
				if (!Unscared(it))
				{
					c.Line = c.Line + " " + (InterroLines.Pick("lock", it.Gang, personality, _rng, null, null, LeaderOut(it.Gang, it.Rec)) ?? "...I ain't saying another word about that.");
				}
			}
			Feed(string.Format("ASKED {0} ({1}: {2}): {3}%, {4} -> refuses{5}", Speaker(it), questionRow.Cat.ToLowerInvariant(), questionRow.Answer, o.Odds, flag ? $"first question, rolled {num}" : $"rolled {num}", Unscared(it) ? " (not scared of you)" : ""), Theme.Dim, rec);
			if (Unscared(it) && Strain(c, it, 1f, "too many questions"))
			{
				return;
			}
		}
		it.LastChance = ChanceToTalk(it, character);
		if (rec != null)
		{
			SaveRoster();
		}
		BuildOptions(c);
	}

	private void EndConvoSoon(Convo c, string why)
	{
		if (c.It?.Rec != null)
		{
			SaveRoster();
		}
		c.EndAt = Game.GameTime + 3500;
		c.Options.Clear();
		GrudgeLog("INTERRO ends soon (" + why + ")");
	}

	private void ReopenQuestions(Interrogation it, bool atCap = false)
	{
		foreach (string item in (from kv in it.Locks
			where it.Terror >= kv.Value || (atCap && kv.Value > 100f)
			select kv.Key).ToList())
		{
			it.Locks.Remove(item);
			it.Tries[item] = Balance.InterroTriesPerScare - 1;
		}
	}

	private string Answer(Interrogation it, QuestionRow q, bool lying)
	{
		NemesisRecord rec = it.Who;
		Gang g = it.Gang;
		float num = it.Terror - (float)QuestionNeed(it, q);
		switch (q.Answer)
		{
		case "catch":
		{
			string[] array2 = ((!(num >= 30f) && !(it.Terror >= 85f)) ? ((!(num >= 12f)) ? new string[3] { "ride", "habit", "alone" } : new string[3] { "habit", "alone", "ride" }) : new string[3] { "alone", "habit", "ride" });
			string result = null;
			for (int num2 = 0; num2 < array2.Length; num2++)
			{
				result = Try(array2[num2], lying && num2 == 0);
				if (!it.Empty || lying)
				{
					break;
				}
			}
			GrudgeLog(string.Format("INTERROGATION catch your boss: {0} (fear {1:+0;-0} past the line - tried {2})", it.Empty ? "nothing" : "told", num, string.Join(" / ", array2)));
			return result;
		}
		case "news":
		{
			Plan plan2 = FuneralToTell(g);
			string[] array4 = ((plan2 == null || plan2.DueAt - GameMinutesNow() >= 2880) ? new string[2] { "shakeup", "funeral" } : new string[2] { "funeral", "shakeup" });
			foreach (string key in array4)
			{
				string result3 = Try(key, lie: false);
				if (!it.Empty)
				{
					return result3;
				}
			}
			return Nothing(it, "Nothing. Same as always.");
		}
		case "holedup":
			if (lying)
			{
				return Try("camp", lie: true);
			}
			if (rec != null && IsOfficer(rec) && num >= 20f)
			{
				string text3 = Try("hideout", lie: false);
				if (!it.Empty)
				{
					if (num < 40f)
					{
						return text3;
					}
					string text4 = Try("camp", lie: false);
					bool empty = it.Empty;
					it.Empty = false;
					if (!empty)
					{
						return text3 + " " + text4;
					}
					return text3;
				}
			}
			return Try("camp", lie: false);
		case "stores":
		{
			string[] array3 = ((!(num >= 10f)) ? new string[2] { "supplies", "treasury" } : new string[2] { "treasury", "supplies" });
			string text7 = Try(array3[0], lie: false);
			bool empty2 = it.Empty;
			if (!empty2 && num < 25f)
			{
				return text7;
			}
			string text8 = Try(array3[1], lie: false);
			bool empty3 = it.Empty;
			if (empty2 && empty3)
			{
				it.Empty = true;
				return text7;
			}
			it.Empty = false;
			if (!empty2)
			{
				if (!empty3)
				{
					return text7 + " " + text8;
				}
				return text7;
			}
			return text8;
		}
		case "orders":
			return AnswerOrders(it, lying);
		case "wants":
		{
			List<Goal> goals = rec.Goals;
			if (goals != null && goals.Count > 0 && !rec.WantsKnown)
			{
				rec.WantsKnown = true;
				Feed("WANTS " + rec.Name + ": " + string.Join("; ", rec.Goals.Select((Goal x) => x.Text)), Theme.Gold, rec);
			}
			Goal goal = rec.Goals?.FirstOrDefault();
			if (goal != null)
			{
				if (!(goal.Type == "Avenge") || !(goal.Target == "you"))
				{
					if (!(goal.Type == "Settle"))
					{
						if (!(goal.Type == "Rise"))
						{
							if (!(goal.Type == "Rich"))
							{
								if (!(goal.Type == "Prove"))
								{
									return "I just want to " + goal.Text + ".";
								}
								return "To show 'em I ain't nobody. Starting with you.";
							}
							return "Money. Enough to stop running.";
						}
						return "To be more than a gun for hire. I'll run my own men one day.";
					}
					return "Out. I want out of all this. Somewhere nobody knows me.";
				}
				return "What do I want? You. In the ground.";
			}
			return "Get through the winter. Same as any man.";
		}
		case "bossnature":
		{
			NemesisRecord nemesisRecord2 = BossOf(rec);
			if (nemesisRecord2 == null)
			{
				return Nothing(it, "I answer to nobody.");
			}
			nemesisRecord2.PersonalityKnown = true;
			List<string> quirks = nemesisRecord2.Quirks;
			string text5 = ((quirks != null && quirks.Count > 0) ? Quirks.Tell(nemesisRecord2.Quirks[0]) : "");
			string text6 = ((nemesisRecord2.Personality == Personality.Hothead) ? "Hot. Shoots first, thinks never." : ((nemesisRecord2.Personality == Personality.Coward) ? "Scared of his own shadow - talks big when he's got men around him." : ((nemesisRecord2.Personality == Personality.Proud) ? "Proud. Won't back down from nobody, not ever." : ((nemesisRecord2.Personality == Personality.Cold) ? "Cold. He'll wait a month to put a knife in you." : ((nemesisRecord2.Personality == Personality.Greedy) ? "Anything for a dollar. Anything." : ((nemesisRecord2.Personality == Personality.Loyal) ? "He'd die for the gang. Thinks we would too." : "Hard to say. Keeps to himself."))))));
			Feed(string.Format("INTEL {0}: {1}{2} (from {3})", nemesisRecord2.Name, nemesisRecord2.Personality, (text5 != "") ? (" - " + text5) : "", Speaker(it)), Theme.Gold);
			History(nemesisRecord2, "HIS NATURE GIVEN AWAY by " + Speaker(it), Theme.Dim);
			return nemesisRecord2.Name + "? " + text6 + ((text5 != "") ? (" " + char.ToUpper(text5[0]) + text5.Substring(1) + ".") : "");
		}
		case "alone":
		case "ride":
		{
			NemesisRecord nemesisRecord = BossOf(rec);
			if (nemesisRecord == null)
			{
				return Nothing(it, "Nobody rides with me. Nobody tells me nothing.");
			}
			Plan plan;
			if (lying)
			{
				string[] array = JobTownsOf(g);
				string text2 = ((array.Length != 0) ? array[_rng.Next(array.Length)] : null);
				Vector3 vector = ((text2 != null) ? JobSpot(text2) : Vector3.Zero);
				if (vector == Vector3.Zero)
				{
					return Nothing(it, "I don't know! I swear!");
				}
				long dueAt = GameMinutesNow() + (long)(6 + _rng.Next(25)) * 60L;
				plan = MakeLie((q.Answer == "ride") ? "when his crew rides" : "when his boss is alone", rec, g, vector, (q.Answer == "ride") ? ("the road out of " + text2) : ("outside " + text2 + ", alone"), dueAt, (q.Answer == "ride") ? (nemesisRecord.Name + "'s crew rides") : (nemesisRecord.Name + " without his men"));
			}
			else
			{
				plan = MakeCrewPlan(q.Answer, nemesisRecord, rec);
			}
			if (plan == null)
			{
				return Nothing(it, "I don't know where they're headed! I swear it!");
			}
			IntelNotice(plan);
			if (!(q.Answer == "ride"))
			{
				return nemesisRecord.Name + "... " + WhenText(plan.DueAt) + ", " + plan.Place + ". He goes off alone sometimes. Don't tell him it was me!";
			}
			return nemesisRecord.Name + " takes us out " + WhenText(plan.DueAt) + " - " + plan.Place + ". That's all of it!";
		}
		case "habit":
			return AnswerHabit(it, lying);
		case "funeral":
			return AnswerFuneral(it, lying);
		case "shakeup":
		{
			int num4 = ShakeUpDayOf(g);
			string text9 = ((num4 > 0) ? DayText(num4) : "soon");
			NemesisRecord nemesisRecord7 = (from n in Roster
				where n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n != rec && n.Rank >= 1 && n.Rank < 4
				orderby Standing(n).score descending
				select n).FirstOrDefault();
			if (nemesisRecord7 != null)
			{
				History(nemesisRecord7, "NAMED NEXT IN LINE by " + Speaker(it), Theme.Dim);
				Feed($"INTEL {g.Name}: next shake-up {text9}; {nemesisRecord7.Name} is next in line (standing {Standing(nemesisRecord7).score})", Theme.Gold);
				ReplyNotice("INTEL: the " + g.Name + " pick new officers " + text9 + ". " + nemesisRecord7.Name + " is next in line.");
			}
			if (nemesisRecord7 == null)
			{
				return Nothing(it, text9 + ". Who's next? Nobody I'd bet on.");
			}
			return text9 + ", they say. " + nemesisRecord7.Name + "'s next - everybody knows it.";
		}
		case "camp":
			if (g == Gangs.Army && !Chronicle.FortKnown && !KnowsFort(rec))
			{
				return Nothing(it, FortNotMine());
			}
			if (!lying && g == Gangs.Army && !Chronicle.FortKnown)
			{
				if (!FortTells())
				{
					return Nothing(it, FortHedge());
				}
				return RevealFort(Speaker(it), it.Rec);
			}
			if (lying)
			{
				return LieAboutCamp(it, BossOf(rec) ?? IntelTarget(it), "his camp");
			}
			return ConfessCampUnsure(it);
		case "hideout":
		{
			if (g == Gangs.Army && !Chronicle.FortKnown && !KnowsFort(rec))
			{
				return Nothing(it, FortNotMine());
			}
			if (!lying && g == Gangs.Army && !Chronicle.FortKnown)
			{
				if (!FortTells())
				{
					return Nothing(it, FortHedge());
				}
				return RevealFort(Speaker(it), it.Rec);
			}
			NemesisRecord nemesisRecord6 = (from n in Roster
				where n.Gang == g.Id && n != rec && n.IsAlive && !n.PresumedDead && n.CampId != null && n.Rank >= 4
				orderby n.Rank descending, n.Id
				select n).FirstOrDefault();
			if (nemesisRecord6 == null)
			{
				return Nothing(it, IsLeader(g, rec) ? "Me? You're looking at the top of it. And you'll get no further." : (LeaderOut(g, rec) ? "Nobody sits at the top right now. Ask me after the next shake-up." : ((g.LeaderName ?? "The boss") + "? Nobody knows where he sleeps. Nobody.")));
			}
			if (lying)
			{
				return LieAboutCamp(it, nemesisRecord6, "the top man's hideout");
			}
			if (nemesisRecord6.CampKnown)
			{
				return Nothing(it, "You already know where " + nemesisRecord6.Name + " is. That's the top of it.");
			}
			nemesisRecord6.CampUnsure = true;
			RevealCamp(nemesisRecord6, found: false);
			History(nemesisRecord6, "GIVEN UP: " + Speaker(it) + " told you where he hides (" + CampOf(nemesisRecord6)?.Label + ")", Theme.Warn);
			ReplyNotice("INTEL (unconfirmed): " + nemesisRecord6.Name + "'s hideout - " + CampOf(nemesisRecord6)?.Label + ". Marked on your map.");
			try
			{
				WorldPart worldPart4 = ((it.Rec != null) ? PartFor(it.Rec, "TALKED", "under questioning") : new WorldPart
				{
					Name = Speaker(it),
					Outcome = "TALKED",
					Note = "under questioning"
				});
				ChronicleDeed("intel", Speaker(it) + " gave up " + nemesisRecord6.Name + "'s hideout", CampOf(nemesisRecord6)?.Label ?? "", null, nemesisRecord6.Gang, worldPart4, PartFor(nemesisRecord6, "GIVEN UP", CampOf(nemesisRecord6)?.Label));
			}
			catch
			{
			}
			rec.AddHate(10);
			return nemesisRecord6.Name + "... " + CampOf(nemesisRecord6)?.Label + ". If he finds out I told you, I'm dead. You hear me? Dead.";
		}
		case "treasury":
		{
			if (!KnowsWhere(rec, g, "money"))
			{
				return Nothing(it, NotToldMe(rec, "the money"));
			}
			CampSite campSite = TreasuryOf(g);
			if (campSite == null)
			{
				return Nothing(it, (g == Gangs.Army) ? "The Army's money is the government's. Go rob a bank." : "We don't keep it in one place. Not any more.");
			}
			if (TreasuryKnown(g))
			{
				return Nothing(it, "You know where it is. Don't make me say it twice.");
			}
			if (TreasuryRobbedNow(g))
			{
				return Nothing(it, "After what you did? It's not where it was. Nobody's told me where it went.");
			}
			RevealTreasury(g, Speaker(it));
			rec.AddHate(15);
			NemesisRecord nemesisRecord3 = HolderOf(campSite);
			if (nemesisRecord3 != null)
			{
				History(nemesisRecord3, (nemesisRecord3 == it.Rec) ? ("GAVE UP the money he keeps (" + campSite.Label + ")") : ("GIVEN UP: " + Speaker(it) + " told you where the money's kept (" + campSite.Label + ")"), Theme.Warn);
			}
			try
			{
				WorldPart worldPart = ((it.Rec != null) ? PartFor(it.Rec, "TALKED", "under questioning") : new WorldPart
				{
					Name = Speaker(it),
					Outcome = "TALKED",
					Note = "under questioning"
				});
				ChronicleDeed("intel", Speaker(it) + " gave up where the " + g.Name + " keep their money", campSite.Label, null, g.Id, worldPart);
			}
			catch
			{
			}
			if (nemesisRecord3 == null || nemesisRecord3 != it.Rec)
			{
				return "Strongboxes... " + campSite.Label + "." + ((nemesisRecord3 != null) ? (" " + nemesisRecord3.Name + " sits on them.") : "") + " They'll skin me for this.";
			}
			return "Strongboxes... " + campSite.Label + ". Mine to sit on - till you came along. They'll skin me for this.";
		}
		case "supplies":
		{
			if (!KnowsWhere(rec, g, "supplies"))
			{
				return Nothing(it, NotToldMe(rec, "the supplies"));
			}
			CampSite campSite2 = SupplyCampOf(g);
			if (campSite2 == null)
			{
				return Nothing(it, (g == Gangs.Army) ? "The Army's fed by the government. Wagons come in from the railheads." : "We ain't got stores to speak of. Not any more.");
			}
			NemesisRecord nemesisRecord4 = HolderOf(campSite2);
			if (nemesisRecord4 == null && IsSupplyCache(g))
			{
				if (SupplyCacheKnown(g))
				{
					return Nothing(it, "You know where. " + campSite2.Label + ". Nobody's camp now.");
				}
				RevealSupplyCache(g, campSite2, Speaker(it));
				rec.AddHate(10);
				ReplyNotice("INTEL: the " + g.Name + " supplies are still at " + campSite2.Label + ", with no camp round them. Marked on your map.");
				try
				{
					WorldPart worldPart2 = ((it.Rec != null) ? PartFor(it.Rec, "TALKED", "under questioning") : new WorldPart
					{
						Name = Speaker(it),
						Outcome = "TALKED",
						Note = "under questioning"
					});
					ChronicleDeed("intel", Speaker(it) + " gave up where the " + g.Name + " keep their supplies", campSite2.Label, null, g.Id, worldPart2);
				}
				catch
				{
				}
				NemesisRecord nemesisRecord5 = SupplyKeeper(g);
				NemesisRecord rec2 = it.Rec;
				if (rec2 != null && rec2.Gang == g.Id && rec2.CampId == null && rec2.IsAlive && !rec2.Paralyzed && (nemesisRecord5 == null || rec2.Rank > nemesisRecord5.Rank || (rec2.Rank == nemesisRecord5.Rank && string.CompareOrdinal(rec2.Id ?? "", nemesisRecord5.Id ?? "") < 0)))
				{
					nemesisRecord5 = rec2;
				}
				if (nemesisRecord5 == null || nemesisRecord5 != it.Rec)
				{
					return "Crates, feed, the guns... still at " + campSite2.Label + ". Nobody's camp now" + ((nemesisRecord5 != null) ? (" - " + nemesisRecord5.Name + " will be watching it") : " - nobody left to watch it") + ".";
				}
				return "Crates, feed, the guns... still at " + campSite2.Label + ". Nobody's camp now. They'll be wanting me to watch it.";
			}
			if (nemesisRecord4 == null)
			{
				return Nothing(it, "They moved it all. Don't know where.");
			}
			bool flag = nemesisRecord4 == it.Rec;
			if (nemesisRecord4.CampKnown)
			{
				return Nothing(it, flag ? ("You know the place. My camp - " + campSite2.Label + ".") : ("You know the place. " + nemesisRecord4.Name + "'s camp - " + campSite2.Label + "."));
			}
			RevealCamp(nemesisRecord4, found: false);
			Hold.KnowSupplies(g, campSite2);
			RedoCampBlip(campSite2.Id);
			rec.AddHate(10);
			History(nemesisRecord4, flag ? ("GAVE UP the supplies he keeps (" + campSite2.Label + ")") : ("GIVEN UP: " + Speaker(it) + " told you where the supplies are kept (" + campSite2.Label + ")"), Theme.Warn);
			ReplyNotice("INTEL: the " + g.Name + " keep their supplies at " + campSite2.Label + ". Marked on your map.");
			GrudgeLog("SUPPLY " + g.Name + ": you know the supply camp now - " + campSite2.Label + " (from " + Speaker(it) + ")");
			try
			{
				WorldPart worldPart3 = ((it.Rec != null) ? PartFor(it.Rec, "TALKED", "under questioning") : new WorldPart
				{
					Name = Speaker(it),
					Outcome = "TALKED",
					Note = "under questioning"
				});
				ChronicleDeed("intel", Speaker(it) + " gave up where the " + g.Name + " keep their supplies", campSite2.Label, null, g.Id, worldPart3);
			}
			catch
			{
			}
			if (!flag)
			{
				return "Crates, feed, the guns... it's all at " + campSite2.Label + ". " + nemesisRecord4.Name + " keeps it.";
			}
			return "Crates, feed, the guns... it's all at " + campSite2.Label + ". My camp. I keep it.";
		}
		case "people":
			return ConfessKin(it);
		case "past":
		{
			RevealPast(rec, "why he rides", "born", "youth", "work", "gang", "war", "crime", "loss");
			int hate = rec.Hate;
			SoftenHate(rec, 3);
			object result2;
			if (Mind.V(rec, "Money") < 2)
			{
				if (Mind.V(rec, "Gang") < 2)
				{
					if (Mind.V(rec, "Family") < 2)
					{
						PastEntry pastEntry = rec.Past?.LastOrDefault((PastEntry e) => e.Kind == "crime" || e.Kind == "war");
						result2 = ((pastEntry != null) ? ("After " + LowerFirst(pastEntry.Text).TrimEnd('.') + "... where else was I gonna go?") : "Ain't nothing else for a man like me.");
					}
					else
					{
						result2 = "I got people to feed. You think I'd be out here otherwise?";
					}
				}
				else
				{
					result2 = "They're all I got. They took me in when nobody would.";
				}
			}
			else
			{
				result2 = "Money. Same as you, don't lie.";
			}
			Feed("PAST " + rec.Name + ": why he rides - hate " + Delta(hate, rec.Hate), Theme.Gold, rec);
			return (string)result2;
		}
		case "worstgrudge":
		{
			List<NemesisRecord> source = (from n in Roster
				where n.Gang == g.Id && n.IsAlive && !n.PresumedDead && n != rec
				orderby n.Hate descending
				select n).ToList();
			NemesisRecord man = source.FirstOrDefault();
			if (man != null && man.Paralyzed && _rng.Next(100) >= 25)
			{
				man = source.FirstOrDefault((NemesisRecord n) => !n.Paralyzed) ?? man;
			}
			if (man == null)
			{
				return Nothing(it, "Nobody. They're all dead, thanks to you.");
			}
			man.Touch();
			string text = ((man.TimesKilledPlayer > 0) ? "he's killed you before and he'll do it again" : ((man.KinId != null && Roster.Any((NemesisRecord k) => k.Id == man.KinId && !k.IsAlive)) ? "you killed his kin" : (man.ScarredByYou ? "you marked him" : ((man.Origin == GrudgeOrigin.GangMember) ? "you've crossed his outfit one time too many" : (Regex.Replace(man.Origin.ToString(), "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant() + " - he don't forget")))));
			Intent intent = IntentOf(man);
			Feed(string.Format("INTEL {0}: hates you most in the {1} (hate {2}{3}) - {4}", man.Name, g.Name, man.Hate, (intent.ToString() != "None") ? $", {intent}" : "", text), Theme.Gold);
			History(man, "NAMED by " + Speaker(it) + " as the man who wants you dead most", Theme.Dim);
			ReplyNotice("INTEL: " + man.Name + " wants you dead more than any of them.");
			if (man.Paralyzed)
			{
				return man.Name + ". " + char.ToUpper(text[0]) + text.Substring(1) + ". Not that he'll ever walk again - but he'll pay someone who can.";
			}
			return man.Name + ". " + char.ToUpper(text[0]) + text.Substring(1) + ". He won't stop.";
		}
		case "bargain":
			return Bargain(it);
		default:
			return "...";
		}
		string Try(string answer, bool lie)
		{
			it.Empty = false;
			return Answer(it, new QuestionRow
			{
				Id = q.Id,
				Cat = q.Cat,
				Answer = answer,
				Lies = q.Lies,
				Need = q.Need
			}, lie);
		}
	}

	private int GrudgeScoreFloor(NemesisRecord r)
	{
		return SocialFloor(r);
	}

	private static string LowerFirst(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToLowerInvariant(s[0]) + s.Substring(1);
		}
		return s;
	}

	private static string Nothing(Interrogation it, string line)
	{
		it.Empty = true;
		return line;
	}

	private bool KnowsWhere(NemesisRecord rec, Gang g, string what)
	{
		if (rec == null || g == null)
		{
			return false;
		}
		bool flag = g.Tiers != null && g.Tiers.Length != 0 && rec.Rank >= g.Tiers[0].MinRank;
		bool flag2 = what == "money";
		int num = ((!flag) ? ((!IsOfficer(rec)) ? (flag2 ? 4 : 15) : (flag2 ? 15 : 40)) : (flag2 ? 80 : 90));
		int num2 = (int)((uint)Seeds.Stable("knows-" + what + "/" + rec.Name) % 100u);
		bool flag3 = num2 < num;
		GrudgeLog(string.Format("INTERROGATION {0} knows where the {1} {2} kept? {3}% ({4}), rolled {5} - {6}", rec.Name, what, (what == "money") ? "is" : "are", num, flag ? "under the leader" : (IsOfficer(rec) ? "an officer" : "low down"), num2, flag3 ? "yes" : "no"));
		return flag3;
	}

	private string NotToldMe(NemesisRecord rec, string what)
	{
		string[] array = new string[4]
		{
			"They don't tell men like me where " + what + "'s kept! I swear it!",
			char.ToUpper(what[0]) + what.Substring(1) + "? Only the boss's men know that. Not me.",
			"I don't know! Ask the ones up top - they never told me nothin'.",
			"You think they'd trust me with " + what + "? Look at me."
		};
		return array[(uint)Seeds.Stable("nottold/" + rec?.Name) % (uint)array.Length];
	}

	private string AnswerOrders(Interrogation it, bool lying)
	{
		Gang gang = it.Gang;
		NemesisRecord rec = it.Rec;
		if (lying)
		{
			(string, string, int)[] array = JobsOf(gang);
			string[] array2 = JobTownsOf(gang);
			if (array.Length == 0 || array2.Length == 0)
			{
				return Nothing(it, "Orders? We ride where we're told.");
			}
			(string, string, int) tuple = array[_rng.Next(array.Length)];
			string text = array2[_rng.Next(array2.Length)];
			Vector3 vector = JobSpot(text);
			if (vector == Vector3.Zero)
			{
				return Nothing(it, "Orders? We ride where we're told.");
			}
			Plan plan = MakeLie("their orders", rec, gang, vector, tuple.Item2.Replace("{t}", text), GameMinutesNow() + (long)(10 + _rng.Next(30)) * 60L, tuple.Item1.Replace("{t}", text));
			IntelNotice(plan);
			return "We're to " + plan.Title + ". " + char.ToUpper(WhenText(plan.DueAt)[0]) + WhenText(plan.DueAt).Substring(1) + ". That's the truth!";
		}
		Plan plan2 = FactionJob(gang);
		if (plan2 == null)
		{
			return Nothing(it, "Orders? We ride where we're told, when we're told.");
		}
		if (plan2.Known)
		{
			return Nothing(it, "You know already - " + plan2.Title + ". " + char.ToUpper(WhenText(plan2.DueAt)[0]) + WhenText(plan2.DueAt).Substring(1) + ".");
		}
		plan2.Known = true;
		plan2.ToldBy = rec?.Id;
		plan2.ToldAbout = "their orders";
		SaveWorld();
		IntelNotice(plan2);
		string text2 = AgendaText(gang);
		object obj;
		if (!IsLeader(gang, rec))
		{
			string text3 = LeaderAsSaidBy(gang, rec);
			obj = ((text3 != null) ? (text3 + " means") : null);
		}
		else
		{
			obj = "I mean";
		}
		string text4 = (string)obj;
		return "We're to " + plan2.Title + ". " + char.ToUpper(WhenText(plan2.DueAt)[0]) + WhenText(plan2.DueAt).Substring(1) + "." + ((text2 != null && text4 != null) ? (" " + text4 + " to " + text2 + ".") : "") + " Now let me be!";
	}

	private void IntelNotice(Plan p)
	{
		ReplyNotice("INTEL (unconfirmed): " + (Gangs.ById(p.Gang)?.Name ?? "they") + " - " + p.Title + ", " + p.Place + ", " + WhenText(p.DueAt) + ". Marked on your map.");
		Feed("INTEL ? " + p.Title + " - " + p.Place + " - " + WhenText(p.DueAt), Theme.Gold);
		RefreshPlanBlips();
	}

	private string DayText(int key)
	{
		_ = key / 10000;
		int val = key / 100 % 100;
		int num = key % 100;
		return $"{Months[Math.Max(0, Math.Min(11, val))]} {num}";
	}

	private string ConfessCampUnsure(Interrogation it)
	{
		NemesisRecord nemesisRecord = IntelTarget(it);
		bool flag = nemesisRecord?.CampKnown ?? true;
		string result = ConfessCamp(it);
		if (nemesisRecord != null && !flag && nemesisRecord.CampKnown)
		{
			nemesisRecord.CampUnsure = true;
			RedoCampBlip(nemesisRecord.CampId);
			return result;
		}
		it.Empty = true;
		return result;
	}

	private string LieAboutCamp(Interrogation it, NemesisRecord target, string about)
	{
		Gang gang = it.Gang;
		List<CampSite> list = (from s in Camps.ForGang(gang.Id)
			where !Roster.Any((NemesisRecord n) => n.CampId == s.Id && n.IsAlive) && !IsCleared(s) && !WetSite(s)
			select s).ToList();
		if (target == null || list.Count == 0)
		{
			return Nothing(it, "I don't know nothin' about the camps! I just ride where I'm told!");
		}
		CampSite campSite = list[_rng.Next(list.Count)];
		Plan plan = MakeLie(about, it.Rec, gang, SitePos(campSite), campSite.Label, GameMinutesNow(), target.Name + "'s " + (campSite.Hideout ? "hideout" : "camp"));
		plan.WindowMin = 4320;
		SaveWorld();
		if (it.Rec != null)
		{
			it.Rec.GaveCamp = true;
		}
		it.CampDone = true;
		IntelNotice(plan);
		return target.Name + "... he's at " + campSite.Label + ". Go see for yourself!";
	}

	private string Bargain(Interrogation it)
	{
		NemesisRecord r = it.Rec;
		Personality t = it.Temper ?? Personality.Cold;
		it.BargainStruck = true;
		bool she = it.Ped != null && it.Ped.Exists() && Models.Female((uint)it.Ped.Model.Hash);
		if (r == null)
		{
			return Line("bargain_plead", "I got a family! Please! I'll ride out of here tonight!");
		}
		long num = GameMinutesNow();
		try
		{
			KeeperLeft(r, "bought his life");
		}
		catch (Exception ex)
		{
			GrudgeLog("HOLDINGS keeper left failed: " + ex.Message);
		}
		if (r.BargainAt > 0 && num >= r.BargainAt && num - r.BargainAt < 10080)
		{
			it.BargainStruck = false;
			return Line("bargain_again", "I already bought my life off you. You want more, you'll have to kill me.");
		}
		r.BargainAt = num;
		bool flag = Quirks.Has(r, "traitor") || (t == Personality.Coward && it.Terror >= 60f) || Mind.F(r, "Loyalty") < 30;
		if ((t == Personality.Greedy || Mind.N(r, "Money") >= 60) && r.Wealth != Wealth.Poor)
		{
			int num2 = 5 + r.Rank * 5 + ((r.Wealth == Wealth.Rich) ? 25 : 5);
			PayPlayer(num2 * 100);
			if (it.Ped != null && it.Ped.Exists() && !it.Ped.IsHogtied)
			{
				GesturePay(it.Ped, youPay: false);
			}
			History(r, $"BOUGHT HIS LIFE from you: ${num2}", Theme.Warn);
			Feed($"BARGAIN {r.Name}: paid you ${num2} for his life", Theme.Gold, r);
			Remember(r, "Shame", 45, "paid for his life");
			return Line("bargain_pay", "Here - ${dollars}. All I got on me. Take it and we never met.").Replace("{dollars}", num2.ToString());
		}
		if (flag && r.Gang != null)
		{
			r.Informant = true;
			r.InformedDay = GameDayKey();
			History(r, "TURNED INFORMER for you - to save his skin", Theme.Warn);
			Feed("BARGAIN " + r.Name + ": he'll send you word of what the " + Gangs.ById(r.Gang)?.Name + " are planning", Theme.Gold, r);
			Remember(r, "Fear", 60, "turned informer");
			return Line("bargain_inform", "I'll tell you what they're planning. Every job, before they ride. Just let me live.");
		}
		if (t == Personality.Proud || t == Personality.Loyal || t == Personality.Hothead)
		{
			r.OwesYou = true;
			History(r, "OWES YOU HIS LIFE - he'll back down once", Theme.Warn);
			Feed("BARGAIN " + r.Name + ": he owes you his life (backs down the next time)", Theme.Gold, r);
			Remember(r, "Shame", 55, "owes you his life");
			return Line("bargain_owe", "Then I owe you. I pay my debts. Once. After that, we're square.");
		}
		r.Fear = Math.Max(r.Fear, 90);
		Mind.SetGoal(r, new Goal
		{
			Type = "Settle",
			Text = "get away from you - out of this life",
			Since = GameMinutesNow()
		});
		History(r, "SWORE TO LEAVE THE TERRITORY to save his life", Theme.Warn);
		Feed("BARGAIN " + r.Name + ": swore he'd leave the territory (fear 90, wants out)", Theme.Gold, r);
		return Line("bargain_leave", "I'll go. Tonight. You'll never see my face again, I swear on my mother.");
		string Line(string sit, string fallback)
		{
			return InterroLines.Pick(sit, it.Gang, t, _rng, null, null, LeaderOut(it.Gang, r), she) ?? fallback;
		}
	}

	private void UpdateInformants()
	{
		int num = GameDayKey();
		foreach (NemesisRecord item in Roster.Where((NemesisRecord n) => n.Informant && n.IsAlive && !n.PresumedDead).ToList())
		{
			if (DaysBetweenKeys(item.InformedDay, num) < 2)
			{
				continue;
			}
			item.InformedDay = num;
			if (OffStage(item) && _rng.Next(100) < 10)
			{
				item.Informant = false;
				WorldEvent worldEvent = NewEvent("FEUD", GameMinutesNow());
				worldEvent.Gangs.Add(item.Gang ?? "");
				worldEvent.Place = "in camp";
				worldEvent.Title = "The " + Gangs.ById(item.Gang)?.Name + " found out " + item.Name + " was talking to you";
				WorldPart worldPart = PartFor(item, "FOUND OUT", "an informer");
				worldEvent.People.Add(worldPart);
				OffscreenDeath(item, worldEvent, worldPart, "his own gang", gun: true);
				worldEvent.Heard = true;
				Chronicle.Events.Add(worldEvent);
				DeathNotice("The " + Gangs.ById(item.Gang)?.Name + " found out " + item.Name + " was your informer.");
				SaveWorld();
				SaveRoster();
				continue;
			}
			Plan plan = FactionJob(Gangs.ById(item.Gang));
			if (plan != null && !plan.Known)
			{
				plan.Known = true;
				plan.ToldBy = item.Id;
				plan.ToldAbout = "their orders (letter)";
				SaveWorld();
				ReplyNotice("A letter from " + item.Name + ": the " + Gangs.ById(item.Gang)?.Name + " mean to " + plan.Title + " - " + WhenText(plan.DueAt) + ".");
				Feed("INFORMER " + item.Name + ": " + plan.Title + " - " + WhenText(plan.DueAt), Theme.Gold, item);
				RefreshPlanBlips();
			}
		}
	}

	private static int DaysBetweenKeys(int a, int b)
	{
		if (a <= 0)
		{
			return 99;
		}
		return (int)(A(b) - A(a)).TotalDays;
		static DateTime A(int k)
		{
			try
			{
				return new DateTime(k / 10000, k / 100 % 100 + 1, Math.Max(1, k % 100));
			}
			catch
			{
				return DateTime.MinValue;
			}
		}
	}
}
