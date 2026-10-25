using System;

namespace NemesisSystem;

public static class Tempers
{
	public static readonly TemperProfile Plain = new TemperProfile();

	public static readonly TemperProfile Hothead = new TemperProfile
	{
		FearGain = 0.5f,
		HateGain = 1.3f,
		WalkOffAt = 90,
		RunOdds = 5,
		BreakLine = 10,
		Surrender = 0,
		Nerve = 30,
		LikeGain = 0.8f,
		FadePerDay = 4f,
		StaresBack = true,
		HealPerDay = 2f
	};

	public static readonly TemperProfile Coward = new TemperProfile
	{
		FearGain = 2f,
		HateGain = 0.6f,
		WalkOffAt = 25,
		RunOdds = 70,
		BreakLine = 40,
		Surrender = 45,
		Nerve = -20,
		LikeGain = 1.1f,
		FadePerDay = 1.5f,
		HealPerDay = 6f
	};

	public static readonly TemperProfile Proud = new TemperProfile
	{
		FearGain = 0.6f,
		HateGain = 1.15f,
		WalkOffAt = 75,
		RunOdds = 5,
		BreakLine = 15,
		Surrender = 3,
		Nerve = 25,
		LikeGain = 0.7f,
		FadePerDay = 4f,
		StaresBack = true,
		HealPerDay = 1f
	};

	public static readonly TemperProfile Cold = new TemperProfile
	{
		FearGain = 0.8f,
		HateGain = 0.9f,
		WalkOffAt = 65,
		RunOdds = 8,
		BreakLine = 20,
		Surrender = 15,
		Nerve = 15,
		LikeGain = 0.6f,
		StaresBack = true
	};

	public static readonly TemperProfile Greedy = new TemperProfile
	{
		FearGain = 1f,
		HateGain = 0.85f,
		WalkOffAt = 45,
		RunOdds = 25,
		BreakLine = 30,
		Surrender = 35,
		LikeGain = 0.9f,
		HealPerDay = 4f
	};

	public static readonly TemperProfile Loyal = new TemperProfile
	{
		FearGain = 0.7f,
		WalkOffAt = 50,
		RunOdds = 20,
		BreakLine = 20,
		Surrender = 10,
		Nerve = 10,
		LikeGain = 1.2f,
		HealPerDay = 2f
	};

	public static readonly TemperProfile Drifter = new TemperProfile
	{
		FearGain = 1.2f,
		HateGain = 0.7f,
		WalkOffAt = 35,
		RunOdds = 35,
		BreakLine = 35,
		Surrender = 30,
		Nerve = -5,
		FadePerDay = 2f,
		HealPerDay = 6f
	};

	public static TemperProfile Of(Personality? p)
	{
		return p switch
		{
			Personality.Hothead => Hothead, 
			Personality.Coward => Coward, 
			Personality.Proud => Proud, 
			Personality.Cold => Cold, 
			Personality.Greedy => Greedy, 
			Personality.Loyal => Loyal, 
			Personality.Drifter => Drifter, 
			_ => Plain, 
		};
	}

	public static TemperProfile Of(string temper)
	{
		if (string.IsNullOrEmpty(temper) || !Enum.TryParse<Personality>(temper, out var result))
		{
			return Plain;
		}
		return Of(result);
	}
}
