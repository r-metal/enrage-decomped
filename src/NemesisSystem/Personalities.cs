using System;
using System.Collections.Generic;

namespace NemesisSystem;

public static class Personalities
{
	private static readonly (Personality p, int w)[] LonerOdds = new(Personality, int)[6]
	{
		(Personality.Hothead, 18),
		(Personality.Coward, 18),
		(Personality.Proud, 16),
		(Personality.Cold, 14),
		(Personality.Greedy, 14),
		(Personality.Drifter, 20)
	};

	private static readonly (Personality p, int w)[] GangOdds = new(Personality, int)[6]
	{
		(Personality.Hothead, 22),
		(Personality.Coward, 14),
		(Personality.Proud, 14),
		(Personality.Cold, 16),
		(Personality.Greedy, 14),
		(Personality.Loyal, 20)
	};

	public static readonly Dictionary<Personality, string[]> Refusals = new Dictionary<Personality, string[]>
	{
		[Personality.Hothead] = new string[3] { "Hit me again. Go on, I dare you!", "I'll gut you for this, Morgan!", "That all you got?" },
		[Personality.Coward] = new string[3] { "Please... I can't, they'll kill me!", "Don't hurt me, mister, please!", "I- I don't know nothin', I swear!" },
		[Personality.Proud] = new string[3] { "You think fists'll make me crawl? Not me.", "I don't bow to the likes of you.", "Beat me all you want. I'm better than you." },
		[Personality.Cold] = new string[3] { "...", "You're wasting your time.", "I've had worse from better men." },
		[Personality.Greedy] = new string[3] { "What's in it for me, huh?", "Information ain't free, friend.", "A man's gotta eat. Talking don't feed me." },
		[Personality.Loyal] = new string[3] { "I ain't no rat. Never.", "The boss is family. You ain't.", "Kill me then. I ain't talking." },
		[Personality.Drifter] = new string[3] { "Ain't my fight, mister.", "Why should I care?", "Leave me be." }
	};

	public static Personality PickWeighted(Random rng, bool gang, Func<Personality, double> mult)
	{
		(Personality, int)[] array = (gang ? GangOdds : LonerOdds);
		double num = 0.0;
		(Personality, int)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(Personality, int) tuple = array2[i];
			num += (double)tuple.Item2 * Math.Max(0.0, mult(tuple.Item1));
		}
		if (num <= 0.0)
		{
			return Pick(rng, gang);
		}
		double num2 = rng.NextDouble() * num;
		array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(Personality, int) tuple2 = array2[i];
			num2 -= (double)tuple2.Item2 * Math.Max(0.0, mult(tuple2.Item1));
			if (num2 <= 0.0)
			{
				return tuple2.Item1;
			}
		}
		return array[array.Length - 1].Item1;
	}

	public static Personality Pick(Random rng, bool gang)
	{
		(Personality, int)[] array = (gang ? GangOdds : LonerOdds);
		int num = 0;
		(Personality, int)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(Personality, int) tuple = array2[i];
			num += tuple.Item2;
		}
		int num2 = rng.Next(num);
		array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(Personality, int) tuple2 = array2[i];
			num2 -= tuple2.Item2;
			if (num2 < 0)
			{
				return tuple2.Item1;
			}
		}
		return array[0].Item1;
	}

	private static bool Humiliation(GrudgeOrigin o)
	{
		if (o != GrudgeOrigin.Insulted && o != GrudgeOrigin.Beaten && o != GrudgeOrigin.KnockedOut)
		{
			return o == GrudgeOrigin.Kidnapped;
		}
		return true;
	}

	public static int StartShift(Personality p, GrudgeOrigin o)
	{
		switch (p)
		{
		case Personality.Hothead:
			return 12;
		case Personality.Coward:
			return -25;
		case Personality.Proud:
			if (!Humiliation(o))
			{
				return 0;
			}
			return 15;
		case Personality.Cold:
			return -8;
		case Personality.Greedy:
			if (o != GrudgeOrigin.Robbed && o != GrudgeOrigin.HorseStolen && o != GrudgeOrigin.WagonStolen)
			{
				return -12;
			}
			return 20;
		case Personality.Drifter:
			return -20;
		default:
			return 0;
		}
	}

	public static int InitialHate(Personality p, GrudgeOrigin o, Random rng)
	{
		return Math.Max(10, Math.Min(100, StartingHate(p, o) + rng.Next(-4, 5)));
	}

	public static int StartingHate(Personality p, GrudgeOrigin o)
	{
		float num = (float)StartShift(p, o) * Balance.LonerShiftScale;
		if (NemesisMod.FistOrigin(o))
		{
			num /= 2f;
		}
		return Math.Max(10, Math.Min(100, Hate.Initial(o) + (int)Math.Round(num)));
	}

	public static float GainMult(Personality p)
	{
		return Tempers.Of(p).HateGain;
	}

	public static int CoolPer10Min(Personality p)
	{
		return p switch
		{
			Personality.Coward => 1, 
			Personality.Drifter => 2, 
			_ => 0, 
		};
	}

	public static double AmbushWeight(Personality p)
	{
		return p switch
		{
			Personality.Hothead => 1.5, 
			Personality.Proud => 1.2, 
			Personality.Coward => 0.5, 
			Personality.Drifter => 0.6, 
			Personality.Greedy => 0.8, 
			_ => 1.0, 
		};
	}

	public static (int bias, int hit, int threat, int round) Interrogation(Personality? p)
	{
		return p switch
		{
			Personality.Coward => (bias: 25, hit: 12, threat: 10, round: 15), 
			Personality.Hothead => (bias: -5, hit: 3, threat: 12, round: 15), 
			Personality.Proud => (bias: -10, hit: -4, threat: -2, round: 25), 
			Personality.Cold => (bias: -15, hit: 6, threat: 4, round: 12), 
			Personality.Greedy => (bias: 5, hit: 8, threat: 8, round: 15), 
			Personality.Loyal => (bias: -20, hit: 6, threat: 6, round: 10), 
			Personality.Drifter => (bias: 10, hit: 8, threat: 8, round: 15), 
			_ => (bias: 0, hit: 8, threat: 8, round: 15), 
		};
	}

	public static string Describe(Personality p)
	{
		return p switch
		{
			Personality.Hothead => "Hothead - quick to rage, slow to forget", 
			Personality.Coward => "Coward - sulks more than he hunts; breaks easily", 
			Personality.Proud => "Proud - humiliation cuts deepest; won't be bullied into talking", 
			Personality.Cold => "Cold - steady and calculating; never cools", 
			Personality.Greedy => "Greedy - money is everything to him", 
			Personality.Loyal => "Loyal - would rather die than give up his boss", 
			Personality.Drifter => "Drifter - the grudge fades; he may let it go", 
			_ => p.ToString(), 
		};
	}
}
