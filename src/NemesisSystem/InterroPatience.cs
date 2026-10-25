namespace NemesisSystem;

internal static class InterroPatience
{
	internal static float For(Personality? t)
	{
		switch (t)
		{
		case Personality.Hothead:
		case Personality.Coward:
			return 3f;
		case Personality.Proud:
		case Personality.Loyal:
			return 4f;
		case Personality.Greedy:
		case Personality.Drifter:
			return 5f;
		case Personality.Cold:
			return 6f;
		default:
			return 4f;
		}
	}

	internal static AfterTalk After(float terror, Personality? t, bool calm, bool letGo, bool warned)
	{
		if (terror >= 60f || (t == Personality.Coward && terror >= 40f))
		{
			return AfterTalk.Run;
		}
		if (terror >= 40f || letGo || !calm)
		{
			return AfterTalk.WalkOff;
		}
		if (!warned)
		{
			return AfterTalk.Business;
		}
		if (t != Personality.Hothead && t != Personality.Proud)
		{
			return AfterTalk.Mutter;
		}
		return AfterTalk.StareDown;
	}
}
