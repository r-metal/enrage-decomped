using System.Collections.Generic;

namespace NemesisSystem;

public class CardWhen
{
	public string Place { get; set; } = "any";

	public bool? Mounted { get; set; }

	public List<string> Tiers { get; set; }

	public List<string> Intents { get; set; }

	public List<string> Factions { get; set; }

	public List<string> NotFactions { get; set; }

	public List<string> Tempers { get; set; }

	public int MinRank { get; set; }

	public int MaxRank { get; set; }

	public List<int> Hours { get; set; }

	public List<string> Regions { get; set; }

	public int MinNotoriety { get; set; }

	public int MinHate { get; set; }

	public bool KilledYou { get; set; }

	public List<string> LastOutcome { get; set; }

	public int MinArcStep { get; set; }

	public List<string> Weather { get; set; }
}
