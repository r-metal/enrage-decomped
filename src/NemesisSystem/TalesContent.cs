using System.Collections.Generic;

namespace NemesisSystem;

public class TalesContent
{
	public string About { get; set; } = "The world's past (History.cs). lore: Year (absolute) or YearsAgo, Month, Title, Place, Gang (faction id or null), Lines, When (text instead of a date). tales: per faction id, the raids / fights its men rode in - Kind RAID / CLASH / DUTY, Title with {place}, Places, Foe (faction id or null), HeardPercent, Line.";

	public List<NemesisMod.LoreRow> Lore { get; set; } = new List<NemesisMod.LoreRow>();

	public Dictionary<string, List<TaleRow>> Tales { get; set; } = new Dictionary<string, List<TaleRow>>();
}
