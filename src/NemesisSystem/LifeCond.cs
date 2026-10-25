using System;
using System.Linq;
using System.Text;

namespace NemesisSystem;

internal static class LifeCond
{
	public static string Slug(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = (s ?? "").ToLowerInvariant();
		foreach (char c in text)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
			}
			else if (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] != '-')
			{
				stringBuilder.Append('-');
			}
		}
		return stringBuilder.ToString().Trim('-');
	}

	public static bool Eval(string cond, NemesisRecord r)
	{
		if (string.IsNullOrWhiteSpace(cond))
		{
			return true;
		}
		string[] array = cond.Split('&');
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].Split('|').Any((string t) => Term(t.Trim().ToLowerInvariant(), r)))
			{
				return false;
			}
		}
		return true;
	}

	private static bool Term(string t, NemesisRecord r)
	{
		if (t.StartsWith("rank>="))
		{
			return r.Rank >= Num(t.Substring(6), 0);
		}
		if (t.StartsWith("rank<="))
		{
			return r.Rank <= Num(t.Substring(6), 99);
		}
		if (t.StartsWith("hate>="))
		{
			return r.Hate >= Num(t.Substring(6), 0);
		}
		if (t.StartsWith("fear>="))
		{
			return r.Fear >= Num(t.Substring(6), 0);
		}
		if (t.StartsWith("hate<"))
		{
			return r.Hate < Num(t.Substring(5), 101);
		}
		if (t.StartsWith("fear<"))
		{
			return r.Fear < Num(t.Substring(5), 101);
		}
		if (t.StartsWith("aggr<"))
		{
			return r.Aggression < Num(t.Substring(5), 100);
		}
		if (t.StartsWith("aggr>="))
		{
			return r.Aggression >= Num(t.Substring(6), 0);
		}
		switch (t)
		{
		case "married":
			if (r.TraitsRolled)
			{
				return r.Family == "married";
			}
			return false;
		case "single":
			if (r.TraitsRolled)
			{
				return r.Family == "single";
			}
			return false;
		case "children":
			return r.Children > 0;
		case "notarmy":
			return Gangs.Of(r)?.GangLifeEvents ?? true;
		case "knows":
			if (r.YourNotoriety < 10 && r.Origin == GrudgeOrigin.GangMember)
			{
				return r.TimesAmbushed > 0;
			}
			return true;
		case "poor":
			return r.Wealth == Wealth.Poor;
		case "rich":
			return r.Wealth == Wealth.Rich;
		case "dim":
			return r.Smarts == Smarts.Dim;
		case "sharp":
			return r.Smarts == Smarts.Sharp;
		case "pet":
			return r.Pet != null;
		case "nopet":
			return r.Pet == null;
		case "friendly":
			return r.Disposition == Disposition.Friendly;
		case "surly":
			return r.Disposition == Disposition.Surly;
		case "devout":
			return r.Devotion == Devotion.Devout;
		case "godless":
			return r.Devotion == Devotion.Godless;
		case "notgodless":
			return r.Devotion != Devotion.Godless;
		case "catholic":
			return r.Faith == "Catholic";
		case "true":
			return true;
		default:
		{
			if (Quirks.All.Contains(t))
			{
				return Quirks.Has(r, t);
			}
			if (Enum.TryParse<Personality>(t, ignoreCase: true, out var result))
			{
				return r.Personality == result;
			}
			return false;
		}
		}
		static int Num(string s, int def)
		{
			if (!(s == "vengeful"))
			{
				if (!(s == "murderous"))
				{
					if (!int.TryParse(s, out var result2))
					{
						return def;
					}
					return result2;
				}
				return Hate.MurderousAt;
			}
			return Hate.VengefulAt;
		}
	}
}
