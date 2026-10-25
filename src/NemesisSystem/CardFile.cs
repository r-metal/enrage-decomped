using System.Collections.Generic;

namespace NemesisSystem;

public class CardFile
{
	public string About { get; set; }

	public List<EncounterCard> Cards { get; set; } = new List<EncounterCard>();
}
