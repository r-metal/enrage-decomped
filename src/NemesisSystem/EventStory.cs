using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class EventStory
{
	private static T Pick<T>(Random r, params T[] xs)
	{
		return xs[r.Next(xs.Length)];
	}

	public static List<string> Build(WorldEvent ev, Func<string, NemesisRecord> find, long now, bool debug)
	{
		Random r = new Random(Seeds.Stable("story/" + (ev.Id ?? ev.Title ?? "") + "/" + ev.GameMin));
		List<string> list = new List<string>();
		int h = HourOf(ev.When);
		string text = TimeOfDay(r, h);
		string text2 = PlacePhrase(ev.Place);
		List<string> list2 = (from x in ev.Gangs.Select(SideName)
			where x != null
			select x).Distinct().ToList();
		List<WorldPart> list3 = ev.People.Where((WorldPart p) => p.Outcome == "KILLED").ToList();
		List<WorldPart> list4 = ev.People.Where((WorldPart p) => p.Outcome == "WOUNDED").ToList();
		List<WorldPart> source = ev.People.Where((WorldPart p) => p.Outcome == "DISTINGUISHED").ToList();
		List<WorldPart> source2 = ev.People.Where((WorldPart p) => p.Outcome == "FLED" || p.Outcome == "RAN").ToList();
		List<WorldPart> list5 = ev.People.Where((WorldPart p) => p.Outcome == "MOURNS").ToList();
		List<WorldPart> list6 = ev.People.Where((WorldPart p) => p.Outcome == "UNHURT" || p.Outcome == "WON").ToList();
		int num = ev.UnnamedDead.Values.Sum();
		switch (ev.Kind)
		{
		case "CLASH":
		case "RAID":
		case "CAMP OVERRUN":
		{
			string text3 = ((list2.Count > 0) ? list2[0] : "two outfits");
			string text4 = ((list2.Count > 1) ? list2[1] : "their rivals");
			list.Add(Pick<string>(r, Cap(text) + ", " + text3 + " and " + text4 + " ran into each other " + text2 + ".", "It started " + text + " " + text2 + ", when " + text3 + " came upon " + text4 + ".", Cap(text2) + ", " + text + ": " + text3 + " against " + text4 + ".", "Nobody can agree who fired first. " + Cap(text) + ", " + text2 + ", " + text3 + " and " + text4 + " were shooting at each other.", Cap(text3) + " had been looking for " + text4 + " for weeks. " + Cap(text) + ", " + text2 + ", they found them."));
			list.Add(Pick<string>(r, "What followed was short and ugly - men behind wagons, behind rocks, behind each other.", "The shooting went on longer than anyone there would admit.", "Horses bolted at the first shots; most of the men were left to fight it out on foot.", "Powder smoke hung low over the ground long after the last shot.", "It was over in minutes. The quiet afterwards lasted longer."));
			if (ev.Kind == "CAMP OVERRUN")
			{
				list.Add(Pick<string>(r, "By the end the camp had changed hands - tents cut down, the fire kicked out, what could be carried carried off.", "The camp did not hold. Whoever was left standing took what they could and left the rest to burn.", "The defenders broke and scattered into the trees, and the camp was theirs no longer."));
			}
			break;
		}
		case "FEUD":
			list.Add(Pick<string>(r, "Bad blood inside " + ((list2.Count > 0) ? list2[0] : "the outfit") + " came to a head " + text + " " + text2 + ".", "It had been building for a while. " + Cap(text) + ", " + text2 + ", it finally boiled over.", "Men who ride together don't always like each other. " + Cap(text) + ", " + text2 + ", that showed.", "A card game, a word too many, an old debt - the reason changes depending on who tells it. It happened " + text + " " + text2 + "."));
			list.Add(Pick<string>(r, "The rest of the outfit stood back and let them settle it.", "Somebody tried to pull them apart and caught a fist for his trouble.", "Knives came out before anyone could stop it.", "Their boss has said nothing about it since, which says plenty."));
			break;
		case "BRAWL":
		case "DUEL":
		case "RIVALRY":
			list.Add(Pick<string>(r, Cap(text) + ", " + text2 + ", two men decided they'd had enough of one another.", "It began over nothing much, " + text + " " + text2 + ", and it didn't stay that way.", "There'd been words between them for a while. " + Cap(text) + ", " + text2 + ", the words ran out.", "Half the street turned out to watch, " + text + " " + text2 + "."));
			list.Add(Pick<string>(r, "Folk still argue about who threw the first punch.", "The sheriff showed up late, the way he always does.", "A bartender swept up the glass and said it was the third time that month.", "By the time anyone thought to stop it, it was finished."));
			break;
		case "LIFE":
		{
			WorldPart worldPart = ev.People.FirstOrDefault();
			if (worldPart != null)
			{
				list.Add(Pick<string>(r, Who(worldPart) + " - " + LifeOpen(r, text, text2), Cap(text) + ", " + text2 + ": news of " + worldPart.Name + ".", "This is the sort of thing that happens to " + worldPart.Name + " when nobody's shooting at him."));
			}
			break;
		}
		case "JOB":
			list.Add(Pick<string>(r, ((list2.Count > 0) ? Cap(list2[0]) : "They") + " had been planning it for days. " + Cap(text) + ", " + text2 + ", they went ahead.", Cap(text) + ", " + text2 + ", " + ((list2.Count > 0) ? list2[0] : "the outfit") + " did the job they'd been talking about.", "The word was out on the road for a week before it happened - " + text + ", " + text2 + "."));
			break;
		case "WORD":
			list.Add(Pick<string>(r, "Talk travels faster than a horse in this country.", "Someone talked. Someone always does.", Cap(text) + ", in a saloon " + text2 + ", a story about you went round the room.", "It started with one man who'd seen it, and grew with every telling."));
			break;
		case "YOU":
			list.Add(Pick<string>(r, "That was your doing, " + text + " " + text2 + ".", Cap(text) + ", " + text2 + " - you were there, and so was this.", "You'll remember it one way. The people who saw it will remember it another.", "It happened " + text + " " + text2 + ", and it has your name on it."));
			break;
		case "WITNESSED":
			list.Add(Pick<string>(r, "You saw it with your own eyes, " + text + " " + text2 + ".", Cap(text) + ", " + text2 + ", you watched it happen.", "You weren't the reason for it this time - you only saw it."));
			break;
		case "RANKS":
			list.Add(Pick<string>(r, ((list2.Count > 0) ? Cap(list2[0]) : "The outfit") + " shuffled its men about " + text + ".", "Every outfit has its pecking order. It changed.", "Somebody moved up. Somebody was told to wait his turn.", "Men who'd been passed over before are counting the days again."));
			break;
		case "VDL":
			if (Gangs.Era >= 1907)
			{
				list.Add(Pick<string>(r, "Eight years on, the Van der Linde gang is a story told in saloons.", "Most of them are dead, and the rest have new names.", "Some of it is true. Most of it grew in the telling."));
			}
			else
			{
				list.Add(Pick<string>(r, "Stories about Dutch's gang are everywhere this spring.", "Folk talk about the Van der Linde boys like they're a ghost story.", "Some of it is true. Some of it is Dutch's own legend, told back to him."));
			}
			break;
		case "TURMOIL":
			list.Add(Pick<string>(r, "Word went round " + text + " that something had shifted " + text2 + ".", Cap(text) + ", " + text2 + ", the ground moved under " + ((list2.Count > 0) ? list2[0] : "the outfits") + ".", "Nobody planned for it. Everybody had to live with it.", "It's the kind of news men stop talking about when a stranger walks in.", "Loyalties in this country last about as long as a bottle."));
			list.Add(Pick<string>(r, "There will be men who profit from it, and men who don't live to see how it ends.", "Old debts got called in. New ones were made.", "Some saw it coming. Most didn't.", "By the next morning every camp in the territory had heard a version of it."));
			break;
		case "WAR":
			list.Add(Pick<string>(r, Cap(text) + ", " + text2 + ", the war took another turn.", "The fighting " + text2 + " went on " + text + ", the way it has for days.", ((list2.Count > 1) ? (Cap(list2[0]) + " and " + list2[1]) : "Both sides") + " kept at it " + text + ".", "Nobody in that country sleeps easy while this goes on.", "Farmers keep their doors barred and their children inside now."));
			list.Add(Pick<string>(r, "Graves are being dug faster than anyone can mark them.", "The roads there aren't safe for anyone wearing the wrong colours.", "Every man in it swears he knows how it ends.", "Each side says the other started it.", "Somewhere a leader is counting men he doesn't have."));
			break;
		default:
			list.Add(Pick<string>(r, "It happened " + text + " " + text2 + ".", Cap(text) + ", " + text2 + ".", "Not everyone remembers it the same way."));
			break;
		}
		foreach (WorldPart item in list3.Take(3))
		{
			NemesisRecord nemesisRecord = find(item.Id);
			bool flag = debug && item.SecretlyLived;
			list.Add(Pick<string>(r, Who(item) + " went down and didn't get up.", Who(item) + " was killed " + Pick<string>(r, "where he stood", "trying to reach his horse", "in the first exchange", "near the end", "covering the others") + ".", "They buried " + item.Name + " " + Pick<string>(r, "the next morning", "where he fell", "without a marker", "in a shallow grave", "with his boots on") + ".", item.Name + " died there" + ((nemesisRecord != null && nemesisRecord.Personality == Personality.Proud) ? ", too proud to run" : ((nemesisRecord != null && nemesisRecord.Personality == Personality.Hothead) ? ", charging when he should have been crouching" : ((nemesisRecord != null && nemesisRecord.Personality == Personality.Coward) ? ", shot in the back as he ran" : ""))) + ".") + (flag ? " (Debug: he lived.)" : ""));
		}
		if (list3.Count > 3)
		{
			list.Add($"{list3.Count - 3} more named men fell with them.");
		}
		foreach (WorldPart item2 in list4.Take(2))
		{
			list.Add(Pick<string>(r, Who(item2) + " took a wound but walked away from it.", item2.Name + " was hit and patched up afterwards - he'll carry that one.", item2.Name + " came out of it bleeding, but alive.", "A doctor dug lead out of " + item2.Name + " that night."));
		}
		foreach (WorldPart item3 in source.Take(2))
		{
			list.Add(Pick<string>(r, Who(item3) + " made a name for himself that day.", "Men are still telling how " + item3.Name + " held his ground.", item3.Name + " was the one they'll talk about - he didn't flinch.", "If anyone came out of it better than they went in, it was " + item3.Name + "."));
		}
		foreach (WorldPart item4 in source2.Take(2))
		{
			list.Add(Pick<string>(r, item4.Name + " ran. Nobody who saw it will let him forget.", item4.Name + " was gone before the smoke cleared.", item4.Name + " lit out at the first chance."));
		}
		if (list6.Count > 0 && list3.Count + list4.Count > 0 && r.Next(2) == 0)
		{
			list.Add(string.Join(", ", from p in list6.Take(3)
				select p.Name) + " came through without a scratch" + ((list6.Count > 3) ? ", among others" : "") + ".");
		}
		if (num > 0)
		{
			list.Add(Pick<string>(r, $"{num} other men nobody bothered to name died with them.", $"Besides those, {num} more lay dead when it was done.", $"The undertaker counted {num} more. Nobody came to claim them."));
		}
		if (list5.Count > 0)
		{
			list.Add(Pick<string>(r, string.Join(" and ", from p in list5.Take(2)
				select p.Name) + " took the news hard.", "Far from there, " + list5[0].Name + " heard and went quiet for a long while.", "Word reached " + list5[0].Name + " a day later. He hasn't been the same."));
		}
		WorldPart worldPart2 = ev.People.FirstOrDefault((WorldPart p) => p.Effects != null && p.Effects.Any((string e) => e.StartsWith("scar") || e.StartsWith("Grieving") || e.StartsWith("fear") || e.Contains("rank")));
		if (worldPart2 != null)
		{
			string text5 = worldPart2.Effects.First((string e) => e.StartsWith("scar") || e.StartsWith("Grieving") || e.StartsWith("fear") || e.Contains("rank"));
			if (text5.StartsWith("scar"))
			{
				list.Add(worldPart2.Name + " carries " + text5.Substring(Math.Min(text5.Length, 6)).Trim().TrimStart(':')
					.Trim() + " from it now.");
			}
			else if (text5.StartsWith("Grieving"))
			{
				list.Add(worldPart2.Name + " is grieving.");
			}
			else if (text5.StartsWith("fear"))
			{
				list.Add("It left " + worldPart2.Name + " jumpier than he was.");
			}
			else
			{
				list.Add(worldPart2.Name + "'s standing changed because of it.");
			}
		}
		if (ev.AftermathUntil > 0)
		{
			list.Add(ev.Visited ? "You've been to the spot since." : ((ev.AftermathUntil > now) ? Pick<string>(r, "The bodies are still out there for anyone who goes looking.", "Nobody has cleared the ground yet.", "The buzzards found it before the law did.") : "Whatever was left there has been cleared away."));
		}
		if (ev.Kind == "CLASH" || ev.Kind == "CAMP OVERRUN" || ev.Kind == "FEUD" || ev.Kind == "RAID")
		{
			list.Add(Pick<string>(r, "Neither side will forget it.", "It won't be the last time.", "Somebody will want to even the score.", "The next time these men meet, it'll be worse.", "In this country a fight like that is never really finished."));
		}
		return list;
		string Who(WorldPart p)
		{
			return Describe(r, p, find);
		}
	}

	private static string LifeOpen(Random r, string tod, string place)
	{
		return Pick<string>(r, tod + " " + place + ", this is what was going on with him.", "life went on for him, the way it does.", "word of him came from " + place.Replace("near ", "").Replace("at ", "") + ".", "even outlaws have the rest of their lives to live.");
	}

	private static string Describe(Random r, WorldPart p, Func<string, NemesisRecord> find)
	{
		NemesisRecord nemesisRecord = find(p.Id);
		if (nemesisRecord == null)
		{
			return p.Name;
		}
		List<string> list = new List<string>();
		if (!string.IsNullOrEmpty(nemesisRecord.Trade) && r.Next(3) > 0)
		{
			list.Add("a " + nemesisRecord.Trade.ToLowerInvariant());
		}
		else if (nemesisRecord.Personality.HasValue && r.Next(2) == 0)
		{
			list.Add(TemperWord(nemesisRecord.Personality.Value));
		}
		if (!string.IsNullOrEmpty(nemesisRecord.Birthplace) && r.Next(2) == 0)
		{
			list.Add("out of " + nemesisRecord.Birthplace);
		}
		if (list.Count != 0)
		{
			return p.Name + ", " + string.Join(" ", list) + ",";
		}
		return p.Name;
	}

	private static string TemperWord(Personality p)
	{
		return p switch
		{
			Personality.Hothead => "a hothead", 
			Personality.Coward => "never the bravest", 
			Personality.Proud => "a proud man", 
			Personality.Cold => "a cold one", 
			Personality.Greedy => "always counting his money", 
			Personality.Loyal => "loyal to a fault", 
			_ => "a drifter", 
		};
	}

	private static string SideName(string id)
	{
		if (id == null)
		{
			return null;
		}
		if (id == "")
		{
			return "some loners";
		}
		Gang gang = Gangs.ById(id);
		if (gang != null)
		{
			if (!gang.Name.StartsWith("The "))
			{
				return "the " + gang.Name;
			}
			return "t" + gang.Name.Substring(1);
		}
		return null;
	}

	private static string PlacePhrase(string place)
	{
		if (string.IsNullOrEmpty(place))
		{
			return "out in the country";
		}
		string text = place.Trim();
		string text2 = text.ToLowerInvariant();
		if (text2.StartsWith("near ") || text2.StartsWith("outside ") || text2.StartsWith("in ") || text2.StartsWith("at ") || text2.StartsWith("on ") || text2.StartsWith("by "))
		{
			return text;
		}
		return "near " + text;
	}

	private static int HourOf(string when)
	{
		if (string.IsNullOrEmpty(when))
		{
			return -1;
		}
		int num = when.LastIndexOf(':');
		if (num < 2)
		{
			return -1;
		}
		if (!int.TryParse(when.Substring(num - 2, 2).Trim(), out var result))
		{
			return -1;
		}
		return result;
	}

	private static string TimeOfDay(Random r, int h)
	{
		if (h < 0)
		{
			return "one day";
		}
		if (h < 5)
		{
			return Pick<string>(r, "in the dead of night", "a little before dawn", "in the small hours");
		}
		if (h < 8)
		{
			return Pick<string>(r, "at first light", "early in the morning", "as the sun came up");
		}
		if (h < 12)
		{
			return Pick<string>(r, "in the morning", "mid-morning", "before noon");
		}
		if (h < 14)
		{
			return Pick<string>(r, "around noon", "in the heat of the day", "at midday");
		}
		if (h < 18)
		{
			return Pick<string>(r, "in the afternoon", "late in the afternoon", "while the sun was still high");
		}
		if (h < 21)
		{
			return Pick<string>(r, "towards evening", "as the light was going", "at sundown");
		}
		return Pick<string>(r, "after dark", "late at night", "in the evening");
	}

	private static string Cap(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1);
		}
		return s;
	}
}
