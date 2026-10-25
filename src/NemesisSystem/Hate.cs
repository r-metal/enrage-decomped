namespace NemesisSystem;

public static class Hate
{
	public const int Max = 100;

	public static int VengefulAt = 40;

	public static int MurderousAt = 70;

	public static HateTier TierOf(int hate)
	{
		if (hate < MurderousAt)
		{
			if (hate < VengefulAt)
			{
				return HateTier.Resentful;
			}
			return HateTier.Vengeful;
		}
		return HateTier.Murderous;
	}

	public static int Initial(GrudgeOrigin origin)
	{
		return origin switch
		{
			GrudgeOrigin.Greeted => 15, 
			GrudgeOrigin.Insulted => 15, 
			GrudgeOrigin.Threatened => 25, 
			GrudgeOrigin.Gunfight => 40, 
			GrudgeOrigin.FriendKilled => 50, 
			GrudgeOrigin.Beaten => 27, 
			GrudgeOrigin.KnockedOut => 30, 
			GrudgeOrigin.Robbed => 46, 
			GrudgeOrigin.HorseStolen => 62, 
			GrudgeOrigin.WagonStolen => 58, 
			GrudgeOrigin.Kidnapped => 55, 
			GrudgeOrigin.HorseKilled => 72, 
			GrudgeOrigin.Slashed => 65, 
			GrudgeOrigin.Wounded => 75, 
			GrudgeOrigin.GangMember => 70, 
			_ => 80, 
		};
	}

	public static string Intent(HateTier tier)
	{
		return tier switch
		{
			HateTier.Vengeful => "brings a blade", 
			HateTier.Murderous => "wants you dead", 
			_ => "wants a fistfight", 
		};
	}
}
