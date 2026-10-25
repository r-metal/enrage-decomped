using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NemesisSystem;

public class FactionsContent
{
	public string About { get; set; } = "Every faction (Gangs.cs fields by name). Leave a field out to keep the built-in value; a new Id = a new faction. Name lists: a NameBank name (\"IrishFirst\") or a list. Districts: zone hashes as hex. Tiers: Label / MinRank / Seats, top first. Seed: Rank / Count / Models. Relations: other faction id -> -100..100 (either side may say it). Jobs: Title / Place with {t} = the town, Crew; JobTowns: where they happen.";

	public List<JObject> Factions { get; set; } = new List<JObject>();
}
