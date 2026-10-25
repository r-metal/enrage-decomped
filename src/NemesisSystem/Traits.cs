using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

public static class Traits
{
	private static readonly string[] DogBreeds = new string[8] { "hound", "collie", "bluetick coonhound", "mutt", "retriever", "shepherd dog", "terrier", "catahoula" };

	private static readonly string[] DogNames = new string[14]
	{
		"Blue", "Duke", "Shep", "Rusty", "Bo", "Molly", "Jack", "Rex", "Sadie", "Tick",
		"Old Dan", "Pepper", "Biscuit", "Ranger"
	};

	private static readonly (string trade, Wealth w)[] TownWomen = new(string, Wealth)[9]
	{
		("Seamstress", Wealth.Working),
		("Laundress", Wealth.Poor),
		("Schoolteacher", Wealth.Comfortable),
		("Shopkeeper", Wealth.Comfortable),
		("Cook", Wealth.Working),
		("Housemaid", Wealth.Poor),
		("Boarding-house keeper", Wealth.Working),
		("Milliner", Wealth.Working),
		("Telegraph operator", Wealth.Working)
	};

	private static readonly string[] TownWomenAlso = new string[5] { "Blacksmith", "Blacksmith", "Doctor", "Preacher", "Labourer" };

	private static readonly Dictionary<string, (int pct, string[] to)> WomenAlso = new Dictionary<string, (int, string[])>
	{
		["Seamstress"] = (8, TownWomenAlso),
		["Schoolteacher"] = (6, TownWomenAlso),
		["Shopkeeper"] = (8, TownWomenAlso),
		["Cook"] = (8, TownWomenAlso),
		["Housemaid"] = (8, TownWomenAlso),
		["Boarding-house keeper"] = (6, TownWomenAlso),
		["Milliner"] = (6, TownWomenAlso),
		["Telegraph operator"] = (6, TownWomenAlso),
		["Laundress"] = (8, new string[4] { "Blacksmith", "Labourer", "Miner", "Dockworker" }),
		["Homesteader"] = (15, new string[7] { "Farmer", "Rancher", "Rancher", "Cowhand", "Hunter", "Trapper", "Blacksmith" }),
		["Farm wife"] = (10, new string[4] { "Farmer", "Rancher", "Cowhand", "Blacksmith" }),
		["Camp cook"] = (12, new string[5] { "Miner", "Labourer", "Dockworker", "Cowhand", "Blacksmith" }),
		["Market seller"] = (8, new string[2] { "Dockworker", "Labourer" }),
		["Nurse"] = (15, new string[1] { "Doctor" }),
		["Drifter"] = (12, new string[3] { "Hunter", "Trapper", "Cowhand" })
	};

	private static readonly Dictionary<string, (int pct, string[] to)> MenAlso = new Dictionary<string, (int, string[])>
	{
		["Townsman"] = (22, new string[10] { "Blacksmith", "Blacksmith", "Blacksmith", "Blacksmith", "Shopkeeper", "Schoolteacher", "Telegraph operator", "Boarding-house keeper", "Tailor", "Cook" }),
		["Labourer"] = (22, new string[6] { "Blacksmith", "Blacksmith", "Blacksmith", "Cook", "Camp cook", "Market seller" }),
		["Farmer"] = (5, new string[1] { "Blacksmith" }),
		["Rancher"] = (5, new string[1] { "Blacksmith" }),
		["Cowhand"] = (6, new string[1] { "Camp cook" }),
		["Drifter"] = (6, new string[2] { "Camp cook", "Cook" })
	};

	private static readonly Dictionary<string, Wealth> AlsoWealth = new Dictionary<string, Wealth>
	{
		["Doctor"] = Wealth.Comfortable,
		["Preacher"] = Wealth.Comfortable,
		["Shopkeeper"] = Wealth.Comfortable,
		["Schoolteacher"] = Wealth.Comfortable
	};

	public static int KinChance = 22;

	private static readonly string[] KinKinds = new string[10] { "brother", "brother", "brother", "cousin", "cousin", "son", "father", "nephew", "uncle", "brother-in-law" };

	public static int FriendChanceGang = 45;

	public static int FriendChanceLoner = 12;

	public static int FriendOtherFaction = 12;

	internal static int AggressionBase(Personality? p)
	{
		if (p != Personality.Hothead)
		{
			if (p != Personality.Coward)
			{
				if (p != Personality.Proud)
				{
					return 40;
				}
				return 55;
			}
			return 20;
		}
		return 70;
	}

	public static void Roll(NemesisRecord r, Random rng, IList<NemesisRecord> roster)
	{
		Personality personality = r.Personality ?? Personality.Cold;
		string m = Models.Name(r.ModelHash);
		r.Aggression = Clamp(AggressionBase(personality) + rng.Next(-20, 21));
		NemesisRecord nemesisRecord = r;
		int num = rng.Next(100);
		int num2;
		switch (personality)
		{
		default:
			num2 = 50;
			break;
		case Personality.Hothead:
		case Personality.Proud:
			num2 = 30;
			break;
		case Personality.Cold:
		case Personality.Drifter:
			num2 = 70;
			break;
		}
		nemesisRecord.Sociability = ((num >= num2) ? Sociability.Outgoing : Sociability.Introvert);
		int num3 = rng.Next(100);
		r.Disposition = personality switch
		{
			Personality.Coward => (num3 >= 40) ? ((num3 < 85) ? Disposition.Neutral : Disposition.Surly) : Disposition.Friendly, 
			Personality.Hothead => (num3 >= 15) ? ((num3 < 50) ? Disposition.Neutral : Disposition.Surly) : Disposition.Friendly, 
			_ => (num3 >= 25) ? ((num3 < 70) ? Disposition.Neutral : Disposition.Surly) : Disposition.Friendly, 
		};
		int num4 = rng.Next(100);
		r.Smarts = ((personality != Personality.Cold) ? ((num4 >= 25) ? ((num4 < 80) ? Smarts.Average : Smarts.Sharp) : Smarts.Dim) : ((num4 >= 10) ? ((num4 < 50) ? Smarts.Average : Smarts.Sharp) : Smarts.Dim));
		if (r.Trade == null)
		{
			(r.Wealth, r.Trade) = FromModel(m, r.Gang, rng);
		}
		Gang gang = Gangs.ById(r.Gang);
		string text = ((gang?.Faiths != null && gang.Faiths.Length != 0) ? gang.Faiths[rng.Next(gang.Faiths.Length)] : (NameBank.IrishLast.Any((string l) => r.Name?.EndsWith(" " + l) ?? false) ? "Catholic" : ((r.Wealth != Wealth.Rich) ? (new string[7] { "Methodist", "Baptist", "Presbyterian", "Lutheran", "Catholic", "Methodist", "Baptist" })[rng.Next(7)] : ((rng.Next(3) == 0) ? "Presbyterian" : "Episcopalian"))));
		int num5 = rng.Next(100);
		r.Devotion = ((!(r.Trade == "Preacher")) ? ((num5 >= 18) ? ((num5 < 55) ? Devotion.Churchgoer : ((num5 < 88) ? Devotion.Lapsed : Devotion.Godless)) : Devotion.Devout) : Devotion.Devout);
		r.Faith = ((r.Devotion == Devotion.Godless) ? "none" : text);
		int num6 = rng.Next(100);
		r.Family = ((num6 < 45) ? "single" : ((num6 < 85) ? "married" : "widower"));
		r.Children = ((!(r.Family == "single")) ? rng.Next(5) : 0);
		if (roster != null && rng.Next(100) < KinChance)
		{
			NemesisRecord nemesisRecord3 = (from _ in roster
				where _ != r && _.IsAlive && _.Gang == r.Gang && _.Name != null && _.Name.Contains(' ')
				orderby rng.Next()
				select _).FirstOrDefault();
			if (nemesisRecord3 != null)
			{
				string text2 = NemesisMod.FamilyNameOf(nemesisRecord3);
				string name = r.Name;
				string text3 = ((name != null) ? name.Split(' ')[0] : null) ?? "Jim";
				r.KinId = nemesisRecord3.Id;
				r.KinRelation = RollKin(rng);
				Gang gang2 = Gangs.ById(r.Gang);
				if ((gang2 == null || gang2.FamilyNames) && SharesSurname(r.KinRelation, rng))
				{
					string text4 = text3 + " " + text2;
					if (Taken(text4) && !Models.Female(r.ModelHash))
					{
						string[] array = Gangs.ById(r.Gang)?.FirstNames ?? NameBank.AmericanFirst;
						for (int num7 = 0; num7 < 25; num7++)
						{
							if (!Taken(text4))
							{
								break;
							}
							text4 = array[rng.Next(array.Length)] + " " + text2;
						}
					}
					if (!Taken(text4))
					{
						r.Name = text4;
					}
				}
			}
		}
		bool flag = r.Trade == "Rancher" || r.Trade == "Farmer" || r.Trade == "Hunter" || r.Trade == "Cowhand" || r.Trade == "Trapper" || r.Trade == "Homesteader" || r.Trade == "Farm wife";
		if (rng.Next(100) < (flag ? 45 : 8))
		{
			r.Pet = "a " + DogBreeds[rng.Next(DogBreeds.Length)] + " called " + DogNames[rng.Next(DogNames.Length)];
		}
		r.TraitsRolled = true;
		bool Taken(string nm)
		{
			return roster.Any((NemesisRecord n) => n != r && n.Name == nm);
		}
	}

	public static void SetBackground(NemesisRecord r, Random rng)
	{
		string m = Models.Name(r.ModelHash);
		(r.Wealth, r.Trade) = FromModel(m, r.Gang, rng);
	}

	private static (Wealth, string) FromModel(string m, string gang, Random rng)
	{
		Gang gang2 = Gangs.ById(gang);
		if (gang2 != null)
		{
			int item = ((int?)gang2.MemberWealth) ?? ((rng.Next(3) == 0) ? 1 : 0);
			string item2 = ((gang2.MemberTrades != null && gang2.MemberTrades.Length != 0) ? gang2.MemberTrades[rng.Next(gang2.MemberTrades.Length)] : "Outlaw");
			return ((Wealth)item, item2);
		}
		if (m.Contains("_f_"))
		{
			return Also(WomanFromModel(m, rng), WomenAlso, rng);
		}
		return Also(ManFromModel(m, rng), MenAlso, rng);
		// Unused in 0.9.5.3 (restored from IL - ILSpy drops uncalled local functions).
		bool Has(string[] t)
		{
			return t.Any(m.Contains);
		}
	}

	private static (Wealth, string) ManFromModel(string m, Random rng)
	{
		if (Has(new string[7] { "upperclass", "fancy", "aristocrat", "wealthy", "gentleman", "banker", "richtraveller" }))
		{
			return (Wealth.Rich, "Gentleman");
		}
		if (Has(new string[4] { "preacher", "priest", "nun", "minister" }))
		{
			return (Wealth.Comfortable, "Preacher");
		}
		if (Has(new string[1] { "doctor" }))
		{
			return (Wealth.Comfortable, "Doctor");
		}
		if (Has(new string[2] { "gambler", "cardplayer" }))
		{
			return (Wealth.Comfortable, "Gambler");
		}
		if (Has(new string[1] { "rancher" }))
		{
			return (Wealth.Working, "Rancher");
		}
		if (Has(new string[3] { "farmer", "farmhand", "sharecrop" }))
		{
			return (Wealth.Working, "Farmer");
		}
		if (Has(new string[1] { "miner" }))
		{
			return (Wealth.Working, "Miner");
		}
		if (Has(new string[2] { "lumber", "logger" }))
		{
			return (Wealth.Working, "Lumberjack");
		}
		if (Has(new string[2] { "dock", "fisher" }))
		{
			return (Wealth.Working, Has(new string[1] { "fisher" }) ? "Fisherman" : "Dockworker");
		}
		if (Has(new string[1] { "trapper" }))
		{
			return (Wealth.Working, "Trapper");
		}
		if (Has(new string[1] { "hunter" }))
		{
			return (Wealth.Working, "Hunter");
		}
		if (Has(new string[4] { "cowboy", "cattle", "drover", "wrangler" }))
		{
			return (Wealth.Working, "Cowhand");
		}
		if (Has(new string[2] { "rail", "train" }))
		{
			return (Wealth.Working, "Railroad man");
		}
		if (Has(new string[4] { "laborer", "labourer", "worker", "bivworker" }))
		{
			return (Wealth.Working, "Labourer");
		}
		if (Has(new string[7] { "vagrant", "beggar", "tramp", "drunk", "poor", "homeless", "lowersd" }))
		{
			return (Wealth.Poor, "Vagrant");
		}
		if (Has(new string[5] { "criminal", "bandit", "outlaw", "thug", "bounty" }))
		{
			return (Wealth.Poor, "Outlaw");
		}
		if (Has(new string[6] { "townfolk", "townsfolk", "valtown", "rhdtown", "sdtown", "bwmtown" }))
		{
			return (Wealth.Comfortable, "Townsman");
		}
		return ((rng.Next(2) == 0) ? Wealth.Working : Wealth.Poor, "Drifter");
		bool Has(string[] t)
		{
			return t.Any(m.Contains);
		}
	}

	private static (Wealth, string) Also((Wealth w, string t) was, Dictionary<string, (int pct, string[] to)> table, Random rng)
	{
		if (was.t == null || !table.TryGetValue(was.t, out (int, string[]) value) || rng.Next(100) >= value.Item1)
		{
			return was;
		}
		string text = value.Item2[rng.Next(value.Item2.Length)];
		Wealth value2;
		return ((!AlsoWealth.TryGetValue(text, out value2)) ? Wealth.Working : value2, text);
	}

	internal static bool IsWomansTrade(string t)
	{
		if (!WomenAlso.Values.Any(((int pct, string[] to) v) => v.to.Contains(t)) && !TownWomen.Any(((string trade, Wealth w) x) => x.trade == t))
		{
			return new string[15]
			{
				"Lady", "Nun", "Preacher's wife", "Nurse", "Saloon girl", "Showgirl", "Gambler", "Homesteader", "Farm wife", "Market seller",
				"Camp cook", "Traveller", "Vagrant", "Outlaw", "Drifter"
			}.Contains(t);
		}
		return true;
	}

	private static (Wealth, string) WomanFromModel(string m, Random rng)
	{
		if (Has(new string[6] { "upperclass", "fancy", "aristocrat", "wealthy", "richtraveller", "socialite" }))
		{
			return (Wealth.Rich, "Lady");
		}
		if (Has(new string[1] { "nun" }))
		{
			return (Wealth.Poor, "Nun");
		}
		if (Has(new string[4] { "preacher", "priest", "minister", "church" }))
		{
			return (Wealth.Comfortable, "Preacher's wife");
		}
		if (Has(new string[2] { "doctor", "nurse" }))
		{
			return (Wealth.Comfortable, "Nurse");
		}
		if (Has(new string[8] { "prostitute", "whore", "showgirl", "dancer", "dancehall", "saloongirl", "burlesque", "singer" }))
		{
			return (Wealth.Poor, Has(new string[3] { "singer", "showgirl", "dancer" }) ? "Showgirl" : "Saloon girl");
		}
		if (Has(new string[2] { "gambler", "cardplayer" }))
		{
			return (Wealth.Comfortable, "Gambler");
		}
		if (Has(new string[10] { "rancher", "farmer", "farmhand", "sharecrop", "homestead", "cowboy", "cattle", "drover", "wrangler", "hillbilly" }))
		{
			return (Wealth.Working, (rng.Next(2) == 0) ? "Homesteader" : "Farm wife");
		}
		if (Has(new string[5] { "dock", "fisher", "market", "vendor", "seller" }))
		{
			return (Wealth.Working, "Market seller");
		}
		if (Has(new string[7] { "miner", "lumber", "logger", "laborer", "labourer", "worker", "bivworker" }))
		{
			if (rng.Next(2) != 0)
			{
				return (Wealth.Working, "Camp cook");
			}
			return (Wealth.Poor, "Laundress");
		}
		if (Has(new string[5] { "rail", "train", "passenger", "traveller", "traveler" }))
		{
			return (Wealth.Comfortable, "Traveller");
		}
		if (Has(new string[7] { "vagrant", "beggar", "tramp", "drunk", "poor", "homeless", "lowersd" }))
		{
			return (Wealth.Poor, "Vagrant");
		}
		if (Has(new string[5] { "criminal", "bandit", "outlaw", "thug", "bounty" }))
		{
			return (Wealth.Poor, "Outlaw");
		}
		if (Has(new string[8] { "townfolk", "townsfolk", "valtown", "rhdtown", "sdtown", "bwmtown", "strtown", "town" }))
		{
			return Town();
		}
		if (rng.Next(3) != 0)
		{
			return Town();
		}
		return (Wealth.Poor, "Drifter");
		bool Has(string[] t)
		{
			return t.Any(m.Contains);
		}
		(Wealth, string) Town()
		{
			(string, Wealth) tuple = TownWomen[rng.Next(TownWomen.Length)];
			return (tuple.Item2, tuple.Item1);
		}
	}

	private static int Clamp(int v)
	{
		return Math.Max(0, Math.Min(100, v));
	}

	public static float HateGain(NemesisRecord r)
	{
		if (!r.TraitsRolled)
		{
			return 1f;
		}
		float num = 0.8f + (float)r.Aggression / 250f;
		if (r.Disposition == Disposition.Friendly)
		{
			num *= 0.85f;
		}
		if (r.Disposition == Disposition.Surly)
		{
			num *= 1.15f;
		}
		if (r.Devotion == Devotion.Devout)
		{
			num *= 0.85f;
		}
		if (r.Devotion == Devotion.Godless)
		{
			num *= 1.1f;
		}
		return num;
	}

	public static float FearGain(NemesisRecord r)
	{
		float num = Tempers.Of(r.Personality).FearGain;
		if (r.Smarts == Smarts.Sharp)
		{
			num *= 1.2f;
		}
		if (r.Smarts == Smarts.Dim)
		{
			num *= 0.8f;
		}
		if (r.Wealth == Wealth.Rich)
		{
			num *= 1.2f;
		}
		return num;
	}

	public static int CoolPer10Min(NemesisRecord r)
	{
		if (r.Devotion != Devotion.Devout || !r.TraitsRolled)
		{
			return 0;
		}
		return 1;
	}

	public static int InterrogationBias(NemesisRecord r)
	{
		if (r == null || !r.TraitsRolled)
		{
			return 0;
		}
		int num = 0;
		if (r.Smarts == Smarts.Dim)
		{
			num += 10;
		}
		if (r.Smarts == Smarts.Sharp)
		{
			num -= 8;
		}
		if (r.Disposition == Disposition.Friendly)
		{
			num += 6;
		}
		if (r.Disposition == Disposition.Surly)
		{
			num -= 6;
		}
		if (r.Children > 0)
		{
			num += 5;
		}
		return num;
	}

	public static string RollKin(Random rng)
	{
		return KinKinds[rng.Next(KinKinds.Length)];
	}

	public static bool SharesSurname(string rel, Random rng)
	{
		switch (rel)
		{
		case "brother":
		case "son":
		case "father":
			return true;
		case "uncle":
		case "nephew":
		case "cousin":
			return rng.Next(2) == 0;
		default:
			return false;
		}
	}

	public static string Word(string rel, bool female)
	{
		if (!female || rel == null)
		{
			return rel;
		}
		return rel switch
		{
			"brother" => "sister", 
			"son" => "daughter", 
			"father" => "mother", 
			"nephew" => "niece", 
			"uncle" => "aunt", 
			"brother-in-law" => "sister-in-law", 
			_ => rel, 
		};
	}

	public static string Inverse(string rel)
	{
		object obj;
		switch (rel)
		{
		default:
			obj = rel;
			if (obj == null)
			{
				return "kin";
			}
			break;
		case "uncle":
			return "nephew";
		case "nephew":
			return "uncle";
		case "father":
			return "son";
		case "son":
			obj = "father";
			break;
		}
		return (string)obj;
	}

	public static float KinWeight(string rel)
	{
		switch (rel)
		{
		default:
			return 0.6f;
		case "friend":
			return 0.5f;
		case "cousin":
			return 0.7f;
		case "uncle":
		case "nephew":
			return 0.8f;
		case "brother":
		case "son":
		case "father":
			return 1f;
		}
	}

	public static Intent IntentOf(NemesisRecord r, int effectiveFear)
	{
		if (!r.CanHunt || r.PresumedDead)
		{
			return Intent.None;
		}
		bool flag = r.Personality == Personality.Hothead || r.Personality == Personality.Proud;
		if (r.TimesKilledPlayer > 0 && r.Origin == GrudgeOrigin.Killed)
		{
			if (effectiveFear < 85 || flag)
			{
				return Intent.Hunts;
			}
			return Intent.WontHunt;
		}
		if (r.Origin == GrudgeOrigin.GangMember)
		{
			if (effectiveFear < Balance.WontHuntFear + 15)
			{
				return Intent.Hunts;
			}
			return Intent.WontHunt;
		}
		if (effectiveFear >= Balance.ProvokedOnlyFear && !flag)
		{
			return Intent.ProvokedOnly;
		}
		switch (r.HateTier)
		{
		case HateTier.Murderous:
			if (effectiveFear >= Balance.WontHuntFear && !flag)
			{
				return Intent.WontHunt;
			}
			if (r.Sociability == Sociability.Introvert && r.Aggression < 35 && !flag)
			{
				return Intent.WontHunt;
			}
			return Intent.Hunts;
		case HateTier.Vengeful:
			if (flag && r.Aggression >= Balance.VengefulHuntAggression && r.Sociability != Sociability.Introvert && effectiveFear < Balance.WontHuntFear)
			{
				return Intent.Hunts;
			}
			return Intent.WontHunt;
		default:
			if (r.Personality != Personality.Hothead || r.Aggression < 75)
			{
				return Intent.ProvokedOnly;
			}
			return Intent.WontHunt;
		}
	}

	public static string Describe(Intent i)
	{
		return i switch
		{
			Intent.Hunts => "hunts you", 
			Intent.WontHunt => "wants you dead, but won't come looking - cross his path and he'll fight", 
			Intent.ProvokedOnly => "won't start it - but antagonize him in town and he'll fight", 
			_ => "can't come after you", 
		};
	}

	public static string Short(Intent i)
	{
		return i switch
		{
			Intent.ProvokedOnly => "If provoked", 
			Intent.WontHunt => "Waits", 
			Intent.Hunts => "Hunts", 
			_ => "-", 
		};
	}

	public static string Aggr(int a)
	{
		if (a < 67)
		{
			if (a < 34)
			{
				return "Calm";
			}
			return "Restless";
		}
		return "Violent";
	}
}
