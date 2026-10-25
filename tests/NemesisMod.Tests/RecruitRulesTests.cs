using Xunit;

namespace NemesisSystem.Tests;

public class RecruitRulesTests
{
	// rank 1, not law, not avenging, not story, ChancePercent 100 unless a case says otherwise
	private static int Odds(Personality? p, int hate = 0, int fear = 0, int rank = 1, bool orders = false, bool avenges = false, bool story = false, int chance = 100)
	{
		return RecruitRules.OfferChance(p, hate, fear, rank, orders, avenges, story, chance);
	}

	[Fact]
	public void CowardBase()
	{
		Assert.Equal(45, Odds(Personality.Coward));
	}

	[Fact]
	public void ProudBase()
	{
		Assert.Equal(3, Odds(Personality.Proud));
	}

	[Fact]
	public void NoPersonalityBase()
	{
		Assert.Equal(20, Odds(null));
	}

	[Theory]
	[InlineData(Personality.Coward, 45)]
	[InlineData(Personality.Drifter, 40)]
	[InlineData(Personality.Greedy, 30)]
	[InlineData(Personality.Cold, 15)]
	[InlineData(Personality.Hothead, 8)]
	[InlineData(Personality.Loyal, 5)]
	[InlineData(Personality.Proud, 3)]
	[InlineData((Personality)99, 20)]
	public void BaseByPersonality(Personality p, int expected)
	{
		Assert.Equal(expected, Odds(p));
	}

	[Fact]
	public void LoyalWhoHatesYouNeverOffers()
	{
		Assert.Equal(0, Odds(Personality.Loyal, hate: 40));
	}

	[Fact]
	public void FearAddsAtMostTwenty()
	{
		Assert.Equal(60, Odds(Personality.Drifter, fear: 100));
	}

	[Fact]
	public void FearAndHateTogether()
	{
		Assert.Equal(50, Odds(Personality.Coward, hate: 20, fear: 50));
	}

	[Fact]
	public void OfficerHalved()
	{
		Assert.Equal(22, Odds(Personality.Coward, rank: 4));
	}

	[Fact]
	public void OrdersNotFeelingsHalved()
	{
		Assert.Equal(22, Odds(Personality.Coward, orders: true));
	}

	[Fact]
	public void CappedAtSeventyFive()
	{
		Assert.Equal(75, Odds(Personality.Coward, fear: 100, chance: 200));
	}

	[Fact]
	public void BossNeverOffers()
	{
		Assert.Equal(0, Odds(Personality.Coward, fear: 100, rank: 5));
	}

	[Fact]
	public void AvengerNeverOffers()
	{
		Assert.Equal(0, Odds(Personality.Coward, fear: 100, avenges: true));
	}

	[Fact]
	public void StoryOrLoreNeverOffers()
	{
		Assert.Equal(0, Odds(Personality.Coward, fear: 100, story: true));
	}

	[Fact]
	public void FriendlyHateClampsToZero()
	{
		Assert.Equal(45, Odds(Personality.Coward, hate: -30));
	}

	[Fact]
	public void ChanceZeroDisables()
	{
		Assert.Equal(0, Odds(Personality.Coward, fear: 100, chance: 0));
	}

	[Fact]
	public void FearRoundsDown()
	{
		Assert.Equal(31, Odds(Personality.Greedy, fear: 7));
	}

	[Fact]
	public void NegativeChanceIsZero()
	{
		Assert.Equal(0, Odds(Personality.Coward, chance: -50));
	}

	[Fact]
	public void OverHundredFearAndHateClamp()
	{
		// fear 500 counts as 100 (+20), hate 500 as 100 (-25)
		Assert.Equal(40, Odds(Personality.Coward, hate: 500, fear: 500));
	}
}
