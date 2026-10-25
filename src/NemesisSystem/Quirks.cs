using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class Quirks
{
	public static readonly string[] All = new string[8] { "drunk", "gambler", "braggart", "traitor", "honest", "superstitious", "sadist", "family man" };

	private static readonly (string quirk, string[] words)[] LifeWords = new(string, string[])[7]
	{
		("drunk", new string[4] { "drinking", "the bottle", "drank the lot", "moonshine" }),
		("gambler", new string[6] { "faro", "card games", "card tables", "over cards", "card sharp", "Owes money" }),
		("braggart", new string[3] { "bare-knuckle", "fifty paces", "his first stake" }),
		("sadist", new string[4] { "Shot a man dead", "Burned out", "used his fists", "end up in the swamp" }),
		("superstitious", new string[4] { "revival", "Snakebit", "cave-in", "Famine" }),
		("honest", new string[3] { "God-fearing", "Found God", "and wept" }),
		("traitor", new string[3] { "blamed everyone", "the wrong people", "Ran from his first" })
	};

	public static bool Has(NemesisRecord r, string q)
	{
		if (r?.Quirks != null)
		{
			return r.Quirks.Contains(q);
		}
		return false;
	}

	public static void Roll(NemesisRecord r, Random rng)
	{
		r.Quirks = r.Quirks ?? new List<string>();
		if (r.QuirksRolled || r.Quirks.Count > 0)
		{
			r.QuirksRolled = true;
			return;
		}
		r.QuirksRolled = true;
		Dictionary<string, int> dictionary = new Dictionary<string, int>
		{
			["drunk"] = 8 + ((Mind.N(r, "Drink") >= 60) ? 20 : 0) + ((r.Personality == Personality.Drifter) ? 8 : 0),
			["gambler"] = 6 + ((r.Personality == Personality.Greedy) ? 12 : 0) + ((Mind.N(r, "Money") >= 60) ? 6 : 0),
			["braggart"] = 6 + ((r.Personality == Personality.Proud) ? 14 : 0) + ((r.Personality == Personality.Hothead) ? 6 : 0),
			["traitor"] = 3 + ((Mind.F(r, "Loyalty") < 30) ? 14 : 0) + ((r.Personality == Personality.Greedy) ? 6 : 0) - ((r.Personality == Personality.Loyal) ? 3 : 0),
			["honest"] = 3 + ((Mind.F(r, "Honesty") >= 70) ? 16 : 0) + ((r.Devotion == Devotion.Devout) ? 6 : 0),
			["superstitious"] = 5 + ((r.Devotion == Devotion.Devout) ? 6 : 0) + ((r.Smarts == Smarts.Dim) ? 6 : 0),
			["sadist"] = 2 + ((Mind.F(r, "Cruelty") >= 70) ? 14 : 0) + ((r.Personality == Personality.Cold) ? 4 : 0),
			["family man"] = ((r.TraitsRolled && r.Family == "married" && r.Children > 0) ? 18 : 0)
		};
		if (r.LifeRules >= 2)
		{
			LifeLeans(r, dictionary);
		}
		int num = ((rng.Next(100) < 55) ? 1 : ((rng.Next(100) < 40) ? 2 : 0));
		for (int i = 0; i < num; i++)
		{
			List<KeyValuePair<string, int>> list = dictionary.Where((KeyValuePair<string, int> kv) => kv.Value > 0 && !r.Quirks.Contains(kv.Key) && (!(kv.Key == "honest") || !r.Quirks.Contains("braggart")) && (!(kv.Key == "braggart") || !r.Quirks.Contains("honest")) && (!(kv.Key == "traitor") || r.Personality != Personality.Loyal)).ToList();
			int num2 = list.Sum((KeyValuePair<string, int> kv) => kv.Value);
			if (num2 <= 0)
			{
				break;
			}
			int num3 = rng.Next(num2);
			foreach (KeyValuePair<string, int> item in list)
			{
				num3 -= item.Value;
				if (num3 < 0)
				{
					r.Quirks.Add(item.Key);
					break;
				}
			}
		}
	}

	private static void LifeLeans(NemesisRecord r, Dictionary<string, int> w)
	{
		foreach (PastEntry e in r.Past ?? new List<PastEntry>())
		{
			if (e.Text == null || e.Kind == "world" || e.Kind == "kin" || e.Kind == "friend" || e.Kind == "weave")
			{
				continue;
			}
			(string, string[])[] lifeWords = LifeWords;
			for (int i = 0; i < lifeWords.Length; i++)
			{
				var (key, source) = lifeWords[i];
				if (w.ContainsKey(key) && source.Any((string x) => e.Text.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					w[key] += 12;
				}
			}
		}
	}

	public static string Tell(string q)
	{
		return q switch
		{
			"drunk" => "drinks like a fish", 
			"gambler" => "can't walk past a card game", 
			"braggart" => "never shuts up about himself", 
			"traitor" => "would sell his own mother", 
			"honest" => "can't tell a lie to save his life", 
			"superstitious" => "scared of the dark, believe it or not", 
			"sadist" => "likes hurting people", 
			"family man" => "lives for his kids", 
			_ => q, 
		};
	}

	public static string Label(string q)
	{
		return q.ToUpperInvariant();
	}

	public static bool HasSkill(NemesisRecord r, string s)
	{
		if (r?.Skills != null)
		{
			return r.Skills.Contains(s);
		}
		return false;
	}

	public static List<string> EarnSkills(NemesisRecord r, string lastWeaponBand = null)
	{
		r.Skills = r.Skills ?? new List<string>();
		List<string> got = new List<string>();
		if (!r.SkillsV2)
		{
			r.Skills.RemoveAll((string s) => (!(s == "marksman") || r.TimesKilledPlayer <= 0) && (!(s == "brawler") || r.TimesKilledPlayer <= 0) && (!(s == "tough") || r.TimesCheatedDeath < 2) && (!(s == "tracker") || r.TimesAmbushed < 3) && (!(s == "leader") || r.Rank < 4 || r.TimesAmbushed < 2));
			r.SkillsV2 = true;
		}
		Give("marksman", r.TimesKilledPlayer > 0 && lastWeaponBand == "Gun");
		Give("brawler", r.TimesKilledPlayer > 0 && lastWeaponBand == "Fists");
		Give("tough", r.TimesCheatedDeath >= 2 || (r.TimesCheatedDeath == 1 && (uint)Seeds.Stable("tough/" + (r.Id ?? r.Name)) % 100u < 50));
		Give("tracker", r.TimesAmbushed >= 3);
		Give("leader", r.Rank >= 4 && r.TimesAmbushed >= 2);
		int num = ((r.Rank >= 4) ? 3 : ((r.Rank < 2) ? 1 : 2));
		for (int num2 = r.SkillTierRolled + 1; num2 <= num; num2++)
		{
			Random random = Seeds.For(r, "skilltier" + num2);
			int num3 = num2 switch
			{
				2 => 40, 
				3 => 100, 
				_ => 15, 
			};
			if (random.Next(100) >= num3)
			{
				continue;
			}
			List<(string, int)> list = ((num2 != 3) ? new(string, int)[3]
			{
				("marksman", 35),
				("tracker", 35),
				("brawler", 30)
			} : new(string, int)[3]
			{
				("leader", 40),
				("marksman", 30),
				("tracker", 30)
			}).Where(((string, int) p) => !r.Skills.Contains(p.Item1)).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			int num4 = random.Next(list.Sum(((string, int) p) => p.Item2));
			foreach (var item3 in list)
			{
				string item = item3.Item1;
				int item2 = item3.Item2;
				num4 -= item2;
				if (num4 < 0)
				{
					Give(item, when: true);
					break;
				}
			}
		}
		r.SkillTierRolled = Math.Max(r.SkillTierRolled, num);
		return got;
		void Give(string s, bool when)
		{
			if (when && !r.Skills.Contains(s))
			{
				r.Skills.Add(s);
				got.Add(s);
			}
		}
	}

	public static string SkillText(string s)
	{
		return s switch
		{
			"marksman" => "a dead shot (accuracy +12)", 
			"brawler" => "a brawler (harder to put down)", 
			"tough" => "too tough to die (+60 health)", 
			"tracker" => "a tracker (hunts you from further off)", 
			"horseman" => "a fine horseman (rides you down)", 
			"leader" => "a leader of men (+1 man)", 
			_ => s, 
		};
	}
}
