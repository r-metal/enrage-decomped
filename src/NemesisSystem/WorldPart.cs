using System.Collections.Generic;

namespace NemesisSystem;

public class WorldPart
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Gang { get; set; }

	public string Outcome { get; set; }

	public string Note { get; set; }

	public bool SecretlyLived { get; set; }

	public List<string> Effects { get; set; } = new List<string>();
}
