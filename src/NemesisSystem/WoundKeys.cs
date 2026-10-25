namespace NemesisSystem;

public static class WoundKeys
{
	public static string Base(string key)
	{
		if (key == null || key.Length <= 2 || key[key.Length - 2] != '_')
		{
			return key;
		}
		return key.Substring(0, key.Length - 2);
	}

	public static string Side(string key)
	{
		if (key != null)
		{
			if (!key.EndsWith("_l"))
			{
				if (!key.EndsWith("_r"))
				{
					return "";
				}
				return "right";
			}
			return "left";
		}
		return "";
	}
}
