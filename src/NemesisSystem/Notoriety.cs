namespace NemesisSystem;

public static class Notoriety
{
	private static readonly int[] Thresholds = new int[5] { 0, 10, 25, 50, 75 };

	private static readonly string[] Names = new string[5] { "Unknown", "Noticed", "Wanted", "Hunted", "Blood Feud" };

	private static readonly double[] AmbushMult = new double[5] { 0.4, 1.0, 1.6, 2.4, 3.5 };

	public static readonly int[] LevelStarts = new int[5] { 0, 10, 25, 50, 75 };

	public static readonly string[] LevelNames = new string[5] { "Unknown", "Noticed", "Wanted", "Hunted", "Blood Feud" };

	public static readonly (int r, int g, int b)[] LevelColors = new(int, int, int)[5]
	{
		(128, 126, 120),
		(206, 190, 140),
		(218, 152, 62),
		(212, 98, 48),
		(196, 42, 36)
	};

	public static int Level(int value)
	{
		int result = 0;
		for (int i = 0; i < Thresholds.Length; i++)
		{
			if (value >= Thresholds[i])
			{
				result = i;
			}
		}
		return result;
	}

	public static string LevelName(int value)
	{
		return Names[Level(value)];
	}

	public static double Ambush(int value)
	{
		return AmbushMult[Level(value)];
	}

	public static int ForKill(int rank)
	{
		if (rank > 0)
		{
			if (rank != 1)
			{
				if (rank > 3)
				{
					if (rank != 4)
					{
						return 6;
					}
					return 4;
				}
				return 2;
			}
			return 1;
		}
		return 1;
	}
}
