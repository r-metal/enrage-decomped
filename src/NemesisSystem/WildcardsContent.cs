using System.Collections.Generic;

namespace NemesisSystem;

public class WildcardsContent
{
	public string About { get; set; } = "TURMOIL world events (Sandbox.cs): Weight = how often turmoil comes up next to the [World] weights (clash 20, life 60...), Kinds = each wildcard's weight (0 = never). Incident lines: {a} {b} = the two factions, {p} / {d} = the district.";

	public int Weight { get; set; } = 10;

	public Dictionary<string, int> Kinds { get; set; } = new Dictionary<string, int>();

	public List<string> BadBlood { get; set; } = new List<string>();

	public List<string> Deals { get; set; } = new List<string>();
}
