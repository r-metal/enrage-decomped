using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal static class Questions
{
	public static readonly string[] Cats = new string[4] { "What", "When", "Where", "Why" };

	public static readonly QuestionRow[] BuiltIn = new QuestionRow[11]
	{
		new QuestionRow
		{
			Id = "what-orders",
			Cat = "What",
			Text = "\"What are your orders? What's the outfit planning?\"",
			Need = 5,
			Lies = true,
			Requires = "gang",
			Answer = "orders"
		},
		new QuestionRow
		{
			Id = "what-wants",
			Cat = "What",
			Text = "\"What are you after, really?\"",
			Need = 0,
			Odds = 10,
			Requires = "named",
			Answer = "wants"
		},
		new QuestionRow
		{
			Id = "what-boss",
			Cat = "What",
			Text = "\"What kind of man is your boss?\"",
			Need = 5,
			Requires = "boss",
			Answer = "bossnature"
		},
		new QuestionRow
		{
			Id = "when-boss",
			Cat = "When",
			Text = "\"When can I catch your boss? Where?\"",
			Need = 10,
			Lies = true,
			Requires = "gang&boss",
			Answer = "catch"
		},
		new QuestionRow
		{
			Id = "when-news",
			Cat = "When",
			Text = "\"What's going on in the outfit? Anything coming?\"",
			Need = 0,
			Odds = 10,
			Requires = "gang&news",
			Answer = "news"
		},
		new QuestionRow
		{
			Id = "where-camp",
			Cat = "Where",
			Text = "\"Where's your boss holed up? Talk.\"",
			Need = 0,
			Lies = true,
			Requires = "gang",
			Answer = "holedup"
		},
		new QuestionRow
		{
			Id = "where-stores",
			Cat = "Where",
			Text = "\"Where do they keep the money? The guns and feed?\"",
			Need = 15,
			Odds = -5,
			Requires = "gang&named",
			Answer = "stores"
		},
		new QuestionRow
		{
			Id = "where-people",
			Cat = "Where",
			Text = "\"Your family. Your friends. Where are they?\"",
			Need = 15,
			Requires = "named",
			Answer = "people"
		},
		new QuestionRow
		{
			Id = "why-past",
			Cat = "Why",
			Text = "\"Why do you ride with these men?\"",
			Need = -10,
			Odds = 15,
			Requires = "named",
			Answer = "past"
		},
		new QuestionRow
		{
			Id = "why-grudge",
			Cat = "Why",
			Text = "\"Who wants me dead the most? Why?\"",
			Need = 5,
			Requires = "gang",
			Answer = "worstgrudge"
		},
		new QuestionRow
		{
			Id = "why-live",
			Cat = "Why",
			Text = "\"Give me one reason to let you live.\"",
			Need = 10,
			Odds = 5,
			Requires = "",
			Answer = "bargain",
			Once = false
		}
	};

	public static QuestionRow[] All = BuiltIn;

	public static readonly Dictionary<string, string> Merged = new Dictionary<string, string>
	{
		["when-ride"] = "when-boss",
		["when-alone"] = "when-boss",
		["when-habit"] = "when-boss",
		["when-shakeup"] = "when-news",
		["when-funeral"] = "when-news",
		["where-hideout"] = "where-camp",
		["where-money"] = "where-stores",
		["where-supplies"] = "where-stores"
	};

	public const int ContentVersion = 4;

	public static int AddedFromBuiltIn;

	public static QuestionsContent Export()
	{
		return new QuestionsContent
		{
			Version = 4,
			Rows = BuiltIn.ToList()
		};
	}

	public static int Import(QuestionsContent c)
	{
		if (c?.Rows == null || c.Rows.Count == 0)
		{
			return 0;
		}
		List<QuestionRow> list = c.Rows.Where((QuestionRow r) => !string.IsNullOrEmpty(r.Id) && !string.IsNullOrEmpty(r.Text) && Cats.Contains(r.Cat)).ToList();
		if (c.Version < 2)
		{
			list.RemoveAll((QuestionRow r) => r.Id == "where-money");
		}
		if (c.Version < 3)
		{
			foreach (QuestionRow item in list.Where((QuestionRow r) => r.Id == "why-live"))
			{
				item.Once = false;
			}
		}
		if (c.Version < 4)
		{
			list.RemoveAll((QuestionRow r) => Merged.ContainsKey(r.Id) || r.Id == "where-camp");
		}
		HashSet<string> have = new HashSet<string>(list.Select((QuestionRow r) => r.Id));
		HashSet<string> removed = new HashSet<string>(c.Removed ?? new List<string>());
		List<QuestionRow> list2 = BuiltIn.Where((QuestionRow r) => !have.Contains(r.Id) && !removed.Contains(r.Id)).ToList();
		AddedFromBuiltIn = list2.Count;
		list.AddRange(list2);
		All = list.ToArray();
		return All.Length;
	}
}
