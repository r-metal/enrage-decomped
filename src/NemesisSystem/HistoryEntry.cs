using System;

namespace NemesisSystem;

public class HistoryEntry
{
	public DateTime At { get; set; }

	public string When { get; set; }

	public string Text { get; set; }

	public (int r, int g, int b) Color { get; set; }

	public bool Secret { get; set; }

	public bool Cover { get; set; }
}
