using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

public static class Mind
{
	public static readonly string[] FacetNames = new string[10] { "Anger", "Bravery", "Greed", "Loyalty", "Pride", "Honesty", "Piety", "Cruelty", "Humour", "Ambition" };

	public static readonly string[] ValueNames = new string[6] { "Family", "Gang", "Money", "Honour", "Law", "God" };

	public static readonly string[] NeedNames = new string[5] { "Drink", "Company", "Faith", "Money", "Violence" };

	public static readonly string[] Bad = new string[4] { "Anger", "Fear", "Shame", "Grief" };

	public static int F(NemesisRecord r, string facet)
	{
		if (r?.Facets == null || !r.Facets.TryGetValue(facet, out var value))
		{
			return 50;
		}
		return value;
	}

	public static int V(NemesisRecord r, string value)
	{
		if (r?.Values == null || !r.Values.TryGetValue(value, out var value2))
		{
			return 0;
		}
		return value2;
	}

	public static int N(NemesisRecord r, string need)
	{
		if (r?.Needs == null || !r.Needs.TryGetValue(need, out var value))
		{
			return 0;
		}
		return value;
	}

	public static bool Has(NemesisRecord r, string goal)
	{
		if (r?.Goals != null)
		{
			return r.Goals.Any((Goal g) => g.Type == goal);
		}
		return false;
	}

	public static bool HasAgainstYou(NemesisRecord r, string goal)
	{
		if (r?.Goals != null)
		{
			return r.Goals.Any((Goal g) => g.Type == goal && g.Target == "you");
		}
		return false;
	}

	internal static int AvengeLine(NemesisRecord r, int usual)
	{
		if (r?.Personality == Personality.Coward)
		{
			return Clamp(usual + 25);
		}
		return usual;
	}

	private static int Clamp(int v, int lo = 0, int hi = 100)
	{
		return Math.Max(lo, Math.Min(hi, v));
	}

	public static void Roll(NemesisRecord r, Random rng, long now)
	{
		Dictionary<string, int> f = FacetNames.ToDictionary((string n) => n, (string n) => 50);
		switch (r.Personality)
		{
		case Personality.Hothead:
			Add("Anger", 30);
			Add("Bravery", 10);
			Add("Pride", 5);
			Add("Humour", -5);
			break;
		case Personality.Coward:
			Add("Bravery", -35);
			Add("Anger", -10);
			Add("Honesty", -5);
			break;
		case Personality.Proud:
			Add("Pride", 30);
			Add("Bravery", 10);
			Add("Ambition", 10);
			break;
		case Personality.Cold:
			Add("Anger", -15);
			Add("Humour", -20);
			Add("Cruelty", 12);
			Add("Bravery", 5);
			break;
		case Personality.Greedy:
			Add("Greed", 35);
			Add("Honesty", -15);
			Add("Ambition", 10);
			break;
		case Personality.Loyal:
			Add("Loyalty", 35);
			Add("Honesty", 10);
			break;
		case Personality.Drifter:
			Add("Ambition", -25);
			Add("Loyalty", -15);
			Add("Humour", 10);
			break;
		}
		if (r.TraitsRolled)
		{
			Add("Anger", (r.Aggression - 50) / 2);
			Add("Cruelty", (r.Aggression - 50) / 3);
			if (r.Disposition == Disposition.Friendly)
			{
				Add("Humour", 15);
				Add("Cruelty", -10);
			}
			if (r.Disposition == Disposition.Surly)
			{
				Add("Humour", -15);
				Add("Anger", 5);
			}
			Add("Piety", (r.Devotion == Devotion.Devout) ? 35 : ((r.Devotion == Devotion.Churchgoer) ? 10 : ((r.Devotion == Devotion.Lapsed) ? (-10) : (-35))));
			if (r.Smarts == Smarts.Sharp)
			{
				Add("Ambition", 8);
			}
			if (r.Wealth == Wealth.Rich)
			{
				Add("Greed", 10);
				Add("Ambition", 10);
			}
			if (r.Wealth == Wealth.Poor)
			{
				Add("Greed", 8);
			}
		}
		using (List<PastEntry>.Enumerator enumerator = (r.Past ?? new List<PastEntry>()).GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				switch (enumerator.Current.Kind)
				{
				case "crime":
					Add("Honesty", -8);
					Add("Cruelty", 4);
					break;
				case "war":
					Add("Bravery", 6);
					Add("Cruelty", 4);
					break;
				case "loss":
					Add("Humour", -4);
					Add("Anger", 3);
					break;
				case "rank":
					Add("Ambition", 6);
					break;
				case "gang":
					if (r.Gang != null)
					{
						Add("Loyalty", 6);
					}
					break;
				}
			}
		}
		if (r.LifeRules >= 2)
		{
			LifeMarks(r, Add);
		}
		Add("Ambition", r.Rank * 3);
		string[] facetNames = FacetNames;
		foreach (string key in facetNames)
		{
			f[key] = Clamp(f[key] + rng.Next(-10, 11));
		}
		r.Facets = f;
		Dictionary<string, int> dictionary = ValueNames.ToDictionary((string n) => n, (string n) => 0);
		dictionary["Family"] = ((!(r.Family == "married")) ? ((r.Family == "widower") ? 1 : ((r.Personality == Personality.Drifter) ? (-1) : 0)) : ((r.Children > 0) ? 3 : 2));
		dictionary["Gang"] = ((r.Gang == null) ? (-1) : ((r.Personality == Personality.Loyal) ? 3 : ((r.Personality == Personality.Drifter) ? (-1) : (1 + (f["Loyalty"] - 50) / 25))));
		dictionary["Money"] = (f["Greed"] - 50) / 16;
		dictionary["Honour"] = (f["Pride"] - 50) / 16 + (f["Honesty"] - 50) / 25;
		bool flag = r.Gang == "odriscoll" || r.Gang == "lemoyneraiders" || r.Trade == "Outlaw" || (r.Past?.Any((PastEntry e) => e.Kind == "crime") ?? false);
		dictionary["Law"] = ((r.Gang == "army") ? 3 : (flag ? (-2) : ((r.Trade == "Townsman" || r.Trade == "Gentleman" || r.Trade == "Lady" || r.Trade == "Schoolteacher" || r.Trade == "Nun") ? 1 : 0)));
		dictionary["God"] = (f["Piety"] - 50) / 16;
		facetNames = ValueNames;
		foreach (string key2 in facetNames)
		{
			dictionary[key2] = Clamp(dictionary[key2], -3, 3);
		}
		r.Values = dictionary;
		r.Needs = NeedNames.ToDictionary((string n) => n, (string n) => rng.Next(10, 45));
		r.Stress = rng.Next(5, 25);
		r.Memories = r.Memories ?? new List<Memory>();
		r.Ties = r.Ties ?? new List<Tie>();
		r.Goals = new List<Goal>();
		r.GoalsSet = false;
		r.MindRolled = true;
		void Add(string n, int d)
		{
			f[n] += d;
		}
	}

	private static void LifeMarks(NemesisRecord r, Action<string, int> add)
	{
		if (r.LifeFx == null || r.LifeFx.Count == 0)
		{
			return;
		}
		Dictionary<string, int> dictionary = FacetNames.ToDictionary((string n) => n, (string n) => 0);
		foreach (string item in r.LifeFx)
		{
			if (item.StartsWith("fear:") && int.TryParse(item.Substring(5), out var result))
			{
				dictionary["Bravery"] -= result * 2;
				continue;
			}
			if (item.StartsWith("aggr:") && int.TryParse(item.Substring(5), out var result2))
			{
				dictionary["Anger"] += result2;
				dictionary["Cruelty"] += result2 / 2;
				continue;
			}
			switch (item)
			{
			case "friendly":
				dictionary["Humour"] += 4;
				dictionary["Cruelty"] -= 3;
				continue;
			case "surly":
				dictionary["Humour"] -= 4;
				dictionary["Anger"] += 2;
				continue;
			case "devout":
				dictionary["Piety"] += 8;
				continue;
			case "godless":
				dictionary["Piety"] -= 8;
				continue;
			case "sharp":
				dictionary["Ambition"] += 4;
				continue;
			case "dim":
				dictionary["Ambition"] -= 4;
				continue;
			}
			if (!item.StartsWith("t:"))
			{
				continue;
			}
			string[] array = item.Split(':');
			if (array.Length == 3 && int.TryParse(array[2], out var result3))
			{
				switch (array[1])
				{
				case "coward":
					dictionary["Bravery"] -= result3;
					break;
				case "hothead":
					dictionary["Anger"] += result3;
					dictionary["Bravery"] += result3 / 2;
					break;
				case "proud":
					dictionary["Pride"] += result3;
					break;
				case "greedy":
					dictionary["Greed"] += result3;
					dictionary["Honesty"] -= result3 / 2;
					break;
				case "loyal":
					dictionary["Loyalty"] += result3;
					break;
				case "cold":
					dictionary["Humour"] -= result3;
					dictionary["Cruelty"] += result3 / 2;
					break;
				case "drifter":
					dictionary["Ambition"] -= result3;
					dictionary["Loyalty"] -= result3 / 2;
					break;
				}
			}
		}
		foreach (KeyValuePair<string, int> item2 in dictionary)
		{
			if (item2.Value != 0 && FacetNames.Contains(item2.Key))
			{
				add(item2.Key, Math.Max(-15, Math.Min(15, item2.Value)));
			}
		}
	}

	public static void InitGoals(NemesisRecord r, Random rng, long now)
	{
		List<Goal> list = new List<Goal>();
		int num = ((r.BornYear > 0) ? (NemesisMod.GameYear() - r.BornYear) : 30);
		Gang gang = Gangs.Of(r);
		int num2 = gang?.Tiers[0].MinRank ?? 5;
		if ((r.TimesKilledPlayer > 0 || r.HateTier == HateTier.Murderous) && F(r, "Anger") >= AvengeLine(r, 55) && r.Gang != "army")
		{
			list.Add(new Goal
			{
				Type = "Avenge",
				Target = "you",
				Text = "settle it with you, for good",
				Since = now
			});
		}
		if (F(r, "Pride") >= 70 && num < 36 && r.Origin != GrudgeOrigin.GangMember && r.Gang != "army")
		{
			list.Add(new Goal
			{
				Type = "Prove",
				Target = "you",
				Text = "prove himself - best you, in front of everyone",
				Since = now
			});
		}
		if (gang != null && F(r, "Ambition") >= 60 && r.Rank < num2)
		{
			list.Add(new Goal
			{
				Type = "Rise",
				Text = "climb the ranks of the " + gang.Name,
				Since = now
			});
		}
		if (F(r, "Greed") >= 70)
		{
			list.Add(new Goal
			{
				Type = "Rich",
				Text = "a big score, one he can retire on",
				Since = now
			});
		}
		if (V(r, "Family") >= 2 && num >= 35 && F(r, "Ambition") < 55)
		{
			list.Add(new Goal
			{
				Type = "Settle",
				Text = "get out of this life, back to his family",
				Since = now
			});
		}
		if (r.KinId != null && F(r, "Loyalty") >= 60)
		{
			list.Add(new Goal
			{
				Type = "Protect",
				Target = r.KinId,
				Text = "keep his kin alive",
				Since = now
			});
		}
		r.Goals = list.Take(2).ToList();
		r.GoalsSet = true;
	}

	public static bool Remember(NemesisRecord r, string emotion, int intensity, string text, string cause, long now, Random rng)
	{
		if (r == null || !r.MindRolled || intensity <= 0)
		{
			return false;
		}
		r.Memories = r.Memories ?? new List<Memory>();
		Memory memory = r.Memories.FirstOrDefault((Memory m) => m.Emotion == emotion && m.Cause != null && m.Cause == cause && !m.LongTerm);
		if (memory != null)
		{
			memory.Intensity = Clamp(memory.Intensity + intensity / 2 + 5);
			memory.At = now;
			memory.Text = text;
		}
		else
		{
			List<Memory> memories = r.Memories;
			Memory obj = new Memory
			{
				Text = text,
				Emotion = emotion,
				Intensity = Clamp(intensity),
				Cause = cause,
				At = now
			};
			memory = obj;
			memories.Add(obj);
		}
		if (Bad.Contains(emotion))
		{
			r.Stress = Clamp(r.Stress + intensity / 8);
		}
		else
		{
			r.Stress = Clamp(r.Stress - intensity / 10);
		}
		bool result = false;
		if (!memory.LongTerm && memory.Intensity >= 65)
		{
			memory.LongTerm = true;
			result = true;
			Drift(r, emotion, cause);
		}
		if (r.Memories.Count > 12)
		{
			r.Memories = r.Memories.OrderByDescending((Memory m) => (!m.LongTerm) ? m.Intensity : (m.Intensity + 50)).Take(12).ToList();
		}
		return result;
	}

	private static void Drift(NemesisRecord r, string emotion, string cause)
	{
		switch (emotion)
		{
		case "Anger":
			Add("Anger", Math.Max(0, Math.Min(5, Baseline(r, "Anger") + 25 - F(r, "Anger"))));
			Add("Cruelty", 2);
			break;
		case "Fear":
			Add("Bravery", -6);
			break;
		case "Shame":
			if (F(r, "Pride") >= 55)
			{
				Add("Pride", 3);
				Add("Anger", Math.Max(0, Math.Min(4, Baseline(r, "Anger") + 25 - F(r, "Anger"))));
			}
			else
			{
				Add("Bravery", -4);
				Add("Pride", -3);
			}
			break;
		case "Grief":
			Add("Anger", Math.Max(0, Math.Min(3, Baseline(r, "Anger") + 25 - F(r, "Anger"))));
			Add("Humour", -5);
			Add("Loyalty", 2);
			break;
		case "Joy":
			Add("Humour", 4);
			Add("Anger", -3);
			break;
		case "Pride":
			Add("Pride", 3);
			Add("Bravery", 3);
			break;
		}
		if (cause == "you")
		{
			switch (emotion)
			{
			case "Anger":
			case "Shame":
			case "Grief":
				if (F(r, "Anger") >= AvengeLine(r, 50) && !HasAgainstYou(r, "Avenge") && r.Gang != "army")
				{
					SetGoal(r, new Goal
					{
						Type = "Avenge",
						Target = "you",
						Text = ((emotion == "Grief") ? "make you pay for who you took" : "make you pay for what you did")
					});
				}
				break;
			}
		}
		if (cause == "you" && emotion == "Fear" && F(r, "Bravery") < 35 && !Has(r, "Settle"))
		{
			SetGoal(r, new Goal
			{
				Type = "Settle",
				Text = "get away from you - out of this life"
			});
		}
		void Add(string n, int d)
		{
			if (r.Facets != null)
			{
				r.Facets[n] = Clamp(F(r, n) + d);
			}
		}
	}

	public static void SetGoal(NemesisRecord r, Goal g)
	{
		r.Goals = r.Goals ?? new List<Goal>();
		r.Goals.RemoveAll((Goal x) => x.Type == g.Type);
		if (g.Type == "Avenge")
		{
			r.Goals.RemoveAll((Goal x) => x.Type == "Settle");
		}
		if (g.Type == "Settle")
		{
			r.Goals.RemoveAll((Goal x) => x.Type == "Prove" || x.Type == "Rise");
		}
		r.Goals.Insert(0, g);
		while (r.Goals.Count > 2)
		{
			r.Goals.RemoveAt(r.Goals.Count - 1);
		}
	}

	public static void Fade(NemesisRecord r)
	{
		if (r.Memories == null)
		{
			return;
		}
		foreach (Memory memory in r.Memories)
		{
			memory.Intensity = (memory.LongTerm ? Math.Max(30, memory.Intensity - 1) : (memory.Intensity - 10));
		}
		r.Memories.RemoveAll((Memory m) => m.Intensity <= 0);
	}

	public static Memory Strongest(NemesisRecord r)
	{
		return r.Memories?.OrderByDescending((Memory m) => m.Intensity + (m.LongTerm ? 20 : 0)).FirstOrDefault();
	}

	public static Tie TieTo(NemesisRecord r, string id)
	{
		return r?.Ties?.FirstOrDefault((Tie t) => t.Id == id);
	}

	public static void SetTie(NemesisRecord r, string id, string type, int strength, string note = null)
	{
		if (r != null && id != null && !(id == r.Id))
		{
			r.Ties = r.Ties ?? new List<Tie>();
			Tie tie = r.Ties.FirstOrDefault((Tie x) => x.Id == id);
			if (tie == null)
			{
				List<Tie> ties = r.Ties;
				Tie obj = new Tie
				{
					Id = id
				};
				tie = obj;
				ties.Add(obj);
			}
			if (!(tie.Type == "kin") || !(type != "kin"))
			{
				tie.Type = type;
			}
			tie.Strength = Clamp(strength, -100);
			if (note != null)
			{
				tie.Note = note;
			}
		}
	}

	public static float HateGain(NemesisRecord r)
	{
		if (r != null && r.MindRolled)
		{
			return 1f + Math.Max(-0.2f, Math.Min(0.2f, (float)(F(r, "Anger") - Baseline(r, "Anger")) / 100f));
		}
		return 1f;
	}

	public static float FearGain(NemesisRecord r)
	{
		if (r != null && r.MindRolled)
		{
			return 1f - Math.Max(-0.3f, Math.Min(0.3f, (float)(F(r, "Bravery") - Baseline(r, "Bravery")) / 100f));
		}
		return 1f;
	}

	public static int Baseline(NemesisRecord r, string facet)
	{
		int num = 50;
		if (facet == "Anger")
		{
			num += ((r.Personality == Personality.Hothead) ? 30 : ((r.Personality == Personality.Coward) ? (-10) : ((r.Personality == Personality.Cold) ? (-15) : 0)));
			if (r.TraitsRolled)
			{
				num += (r.Aggression - 50) / 2;
				if (r.Disposition == Disposition.Surly)
				{
					num += 5;
				}
			}
		}
		else if (facet == "Bravery")
		{
			num += ((r.Personality == Personality.Hothead) ? 10 : ((r.Personality == Personality.Coward) ? (-35) : ((r.Personality == Personality.Proud) ? 10 : ((r.Personality == Personality.Cold) ? 5 : 0))));
		}
		return Clamp(num);
	}

	public static double AmbushMult(NemesisRecord r)
	{
		if (r == null || !r.MindRolled)
		{
			return 1.0;
		}
		double num = 1.0;
		if (HasAgainstYou(r, "Avenge"))
		{
			num *= 1.5;
		}
		if (HasAgainstYou(r, "Prove"))
		{
			num *= 1.4;
		}
		if (Has(r, "Settle"))
		{
			num *= 0.5;
		}
		if (r.Stress >= 70)
		{
			num *= 1.2;
		}
		return num;
	}

	public static Intent ShiftIntent(NemesisRecord r, Intent i, int effectiveFear)
	{
		if (r == null || !r.MindRolled || i == Intent.None)
		{
			return i;
		}
		if (Has(r, "Settle") && i == Intent.Hunts)
		{
			return Intent.WontHunt;
		}
		if ((HasAgainstYou(r, "Avenge") || HasAgainstYou(r, "Prove")) && i != Intent.Hunts && effectiveFear < Balance.WontHuntFear && r.HateTier != HateTier.Resentful)
		{
			return Intent.Hunts;
		}
		return i;
	}

	public static int BackDownBonus(NemesisRecord r)
	{
		if (r != null && r.MindRolled)
		{
			return (50 - F(r, "Pride")) / 4 + (50 - F(r, "Bravery")) / 5 + (Has(r, "Settle") ? 15 : 0) - (HasAgainstYou(r, "Prove") ? 15 : 0);
		}
		return 0;
	}

	public static (int bias, string why) TalkBias(NemesisRecord r, Func<string, NemesisRecord> byId)
	{
		if (r == null || !r.MindRolled)
		{
			return (bias: 0, why: "");
		}
		int num = (50 - F(r, "Loyalty")) / 6 + (r.Stress - 40) / 8 - V(r, "Gang") * 3;
		List<string> list = new List<string>
		{
			string.Format("loyalty {0:+0;-0;0}", (50 - F(r, "Loyalty")) / 6),
			$"stress {(r.Stress - 40) / 8:+0;-0;0}"
		};
		if (V(r, "Gang") != 0)
		{
			list.Add(string.Format("gang {0:+0;-0;0}", -V(r, "Gang") * 3));
		}
		NemesisRecord nemesisRecord = ((r.BossId != null) ? byId(r.BossId) : null);
		Tie tie = ((nemesisRecord != null) ? TieTo(r, nemesisRecord.Id) : null);
		if (tie != null && tie.Strength <= -30)
		{
			num += 12;
			list.Add("hates his boss +12");
		}
		List<Tie> ties = r.Ties;
		if (ties != null && ties.Any((Tie t) => t.Type == "rival"))
		{
			num += 5;
			list.Add("a rival to sell out +5");
		}
		return (bias: num, why: string.Join(" ", list));
	}

	public static (int bonus, string why) StandingBonus(NemesisRecord r)
	{
		if (r == null || !r.MindRolled)
		{
			return (bonus: 0, why: "");
		}
		return (bonus: F(r, "Ambition") / 5 - 10 + (Has(r, "Rise") ? 15 : 0) - (Has(r, "Settle") ? 15 : 0), why: Has(r, "Rise") ? "wants to rise" : ((F(r, "Ambition") >= 65) ? "ambitious" : ""));
	}

	public static string Felt(string emotion)
	{
		return emotion switch
		{
			"Pride" => "proud", 
			"Joy" => "glad", 
			"Grief" => "grieving", 
			"Shame" => "ashamed", 
			"Fear" => "afraid", 
			"Anger" => "angry", 
			_ => emotion?.ToLowerInvariant(), 
		};
	}

	public static (int r, int g, int b) EmotionColor(string emotion)
	{
		return emotion switch
		{
			"Anger" => (r: 200, g: 80, b: 64), 
			"Fear" => (r: 150, g: 170, b: 210), 
			"Shame" => (r: 200, g: 150, b: 90), 
			"Grief" => (r: 160, g: 126, b: 160), 
			"Joy" => (r: 130, g: 190, b: 110), 
			"Pride" => (r: 215, g: 178, b: 95), 
			_ => (r: 176, g: 164, b: 138), 
		};
	}

	public static string Describe(NemesisRecord r, int take = 4)
	{
		if (r?.Facets == null)
		{
			return "-";
		}
		return string.Join(", ", from kv in r.Facets.OrderByDescending((KeyValuePair<string, int> kv) => Math.Abs(kv.Value - 50)).Take(take)
			select ((Math.Abs(kv.Value - 50) >= 35) ? "very " : "") + Word(kv.Key, kv.Value));
		static string Word(string n, int v)
		{
			bool flag = v >= 50;
			switch (n)
			{
			case "Anger":
				if (!flag)
				{
					return "slow to anger";
				}
				return "quick to anger";
			case "Bravery":
				if (!flag)
				{
					return "timid";
				}
				return "brave";
			case "Greed":
				if (!flag)
				{
					return "generous";
				}
				return "greedy";
			case "Loyalty":
				if (!flag)
				{
					return "disloyal";
				}
				return "loyal";
			case "Pride":
				if (!flag)
				{
					return "humble";
				}
				return "proud";
			case "Honesty":
				if (!flag)
				{
					return "dishonest";
				}
				return "honest";
			case "Piety":
				if (!flag)
				{
					return "faithless";
				}
				return "pious";
			case "Cruelty":
				if (!flag)
				{
					return "kind";
				}
				return "cruel";
			case "Humour":
				if (!flag)
				{
					return "humourless";
				}
				return "jokes a lot";
			case "Ambition":
				if (!flag)
				{
					return "unambitious";
				}
				return "ambitious";
			default:
				return n;
			}
		}
	}
}
