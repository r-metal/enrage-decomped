using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem;

internal sealed class District
{
	public string Key;

	public string Name;

	public uint[] Hashes;

	public Dictionary<string, int> Lore1899;

	public Dictionary<string, int> Lore1907;

	private Dictionary<string, int> _lore1907From;

	public float X;

	public float Y;

	public string Region;

	public Dictionary<string, int> Lore
	{
		get
		{
			Dictionary<string, int> dictionary;
			if (Gangs.Era >= 1907)
			{
				dictionary = Lore1907;
				if (dictionary == null)
				{
					dictionary = _lore1907From;
					if (dictionary == null)
					{
						return _lore1907From = Lore1899.Where((KeyValuePair<string, int> kv) => Gangs.InEra(Gangs.ById(kv.Key))).ToDictionary((KeyValuePair<string, int> kv) => kv.Key, (KeyValuePair<string, int> kv) => kv.Value);
					}
				}
			}
			else
			{
				dictionary = Lore1899;
			}
			return dictionary;
		}
	}
}
