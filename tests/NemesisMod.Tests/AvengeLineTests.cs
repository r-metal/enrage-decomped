using Xunit;

namespace NemesisSystem.Tests;

public class AvengeLineTests
{
	private static NemesisRecord Man(Personality? p)
	{
		return new NemesisRecord { Id = "t1", Name = "Test Man", Personality = p };
	}

	[Theory]
	[InlineData(45, 70)]
	[InlineData(50, 75)]
	[InlineData(55, 80)]
	public void CowardNeedsMoreAnger(int usual, int coward)
	{
		Assert.Equal(coward, Mind.AvengeLine(Man(Personality.Coward), usual));
	}

	[Theory]
	[InlineData(Personality.Hothead)]
	[InlineData(Personality.Loyal)]
	[InlineData(Personality.Drifter)]
	[InlineData(Personality.Proud)]
	public void OthersKeepTheLine(Personality p)
	{
		Assert.Equal(50, Mind.AvengeLine(Man(p), 50));
	}

	[Fact]
	public void NoTemperKeepsTheLine()
	{
		Assert.Equal(45, Mind.AvengeLine(Man(null), 45));
	}

	[Fact]
	public void NeverPastHundred()
	{
		Assert.Equal(100, Mind.AvengeLine(Man(Personality.Coward), 90));
	}
}
