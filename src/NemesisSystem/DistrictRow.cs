using System.Collections.Generic;

namespace NemesisSystem;

public class DistrictRow
{
	public string Key { get; set; }

	public string Name { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public List<string> Hashes { get; set; } = new List<string>();

	public Dictionary<string, int> Lore { get; set; } = new Dictionary<string, int>();

	public Dictionary<string, int> Lore1907 { get; set; }

	public string Region { get; set; }
}
