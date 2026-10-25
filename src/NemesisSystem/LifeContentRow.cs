using System.Collections.Generic;

namespace NemesisSystem;

public class LifeContentRow
{
	public string Id { get; set; }

	public string Who { get; set; }

	public string Title { get; set; }

	public string Thought { get; set; }

	public string Mood { get; set; }

	public int Weight { get; set; }

	public int Hours { get; set; }

	public string When { get; set; }

	public int Odds { get; set; }

	public List<string> Effects { get; set; }
}
