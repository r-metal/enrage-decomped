using System.Collections.Generic;

namespace NemesisSystem;

public class TaleRow
{
	public string Kind { get; set; }

	public string Title { get; set; }

	public List<string> Places { get; set; } = new List<string>();

	public string Foe { get; set; }

	public int HeardPercent { get; set; }

	public string Line { get; set; }
}
