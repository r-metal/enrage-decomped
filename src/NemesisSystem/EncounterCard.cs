using System.Collections.Generic;

namespace NemesisSystem;

public class EncounterCard
{
	public string Id { get; set; }

	public string Title { get; set; }

	public string Notes { get; set; }

	public bool Enabled { get; set; } = true;

	public string Stage { get; set; }

	public string Faction { get; set; }

	public CardWhen When { get; set; } = new CardWhen();

	public int Weight { get; set; } = 10;

	public string IniWeight { get; set; }

	public int Chance { get; set; }

	public string IniChance { get; set; }

	public int CooldownMinutes { get; set; }

	public int TensionMin { get; set; }

	public int TensionMax { get; set; }

	public CardCast Cast { get; set; } = new CardCast();

	public List<string> Taunts { get; set; }

	public string Voice { get; set; }

	public bool? Standoff { get; set; }

	public int StandoffSeconds { get; set; }

	public float SightRange { get; set; }

	public string Activity { get; set; }

	public int BackDownHate { get; set; } = -5;

	public string History { get; set; }

	public bool EndsPeacefully { get; set; }
}
