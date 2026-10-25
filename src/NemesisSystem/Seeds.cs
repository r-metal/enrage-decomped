using System;

namespace NemesisSystem;

internal static class Seeds
{
	public static int Stable(string s)
	{
		uint num = 2166136261u;
		string text = s ?? "";
		foreach (char c in text)
		{
			num ^= c;
			num *= 16777619;
		}
		return (int)num;
	}

	public static Random For(NemesisRecord r, string salt)
	{
		return new Random(Stable((r?.Id ?? r?.Name ?? "") + "/" + salt));
	}

	public static int NewSessionSeed()
	{
		return Stable(DateTime.Now.Ticks + "/" + Environment.TickCount);
	}
}
