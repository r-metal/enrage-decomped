namespace NemesisSystem;

public static class Joaat
{
	public static uint Of(string s)
	{
		uint num = 0u;
		string text = s.ToLowerInvariant();
		foreach (char c in text)
		{
			num += c;
			num += num << 10;
			num ^= num >> 6;
		}
		num += num << 3;
		num ^= num >> 11;
		return num + (num << 15);
	}
}
