using System;

namespace NemesisSystem;

internal static class RecruitRules
{
	// D-001: odds (0..75) that a spared man offers to ride with you
	public static int OfferChance(Personality? personality, int hate, int fear, int rank, bool ordersNotFeelings, bool avengesYou, bool storyOrLore, int chancePercent)
	{
		if (storyOrLore || avengesYou || rank >= 5)
		{
			return 0;
		}
		int num;
		switch (personality)
		{
		case Personality.Coward:
			num = 45;
			break;
		case Personality.Drifter:
			num = 40;
			break;
		case Personality.Greedy:
			num = 30;
			break;
		case Personality.Cold:
			num = 15;
			break;
		case Personality.Hothead:
			num = 8;
			break;
		case Personality.Loyal:
			num = 5;
			break;
		case Personality.Proud:
			num = 3;
			break;
		default:
			num = 20;
			break;
		}
		hate = Math.Max(0, Math.Min(100, hate));
		fear = Math.Max(0, Math.Min(100, fear));
		int num2 = num + Math.Min(20, fear / 5) - hate / 4;
		if (rank >= 4 || ordersNotFeelings)
		{
			num2 /= 2;
		}
		num2 = num2 * Math.Max(0, chancePercent) / 100;
		return Math.Max(0, Math.Min(75, num2));
	}
}
