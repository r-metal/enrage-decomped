using System.Collections.Generic;

namespace NemesisSystem;

public class CampsContent
{
	public string About { get; set; } = "Camp sites for factions other than the O'Driscolls (theirs are the game's own camps). Learned in game where 3+ of a faction's men gather (seen twice), or added with H on a faction's hierarchy page (debug on). Delete a row to remove a site. hideout = officers rank 4+ prefer it.";

	public List<CampRow> Sites { get; set; } = new List<CampRow>();

	public List<CampCandidateRow> Candidates { get; set; } = new List<CampCandidateRow>();
}
