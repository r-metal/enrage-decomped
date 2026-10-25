using Newtonsoft.Json;

namespace NemesisSystem;

public class PastEntry
{
	public int Year { get; set; }

	public int Month { get; set; }

	public string Text { get; set; }

	public string Kind { get; set; }

	public string WorldId { get; set; }

	public bool Public { get; set; }

	public bool Known { get; set; }

	public string MirrorKind { get; set; }

	public string MirrorTitle { get; set; }

	public string MirrorPlace { get; set; }

	public bool MirrorHeard { get; set; }

	[JsonIgnore]
	public string WhenText
	{
		get
		{
			if (Month < 1 || Month > 12)
			{
				return $"{Year}";
			}
			return $"{GameDate.Month(Month)} {Year}";
		}
	}
}
