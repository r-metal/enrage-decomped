using System;

namespace NemesisSystem;

public class Scar
{
	public string DamagePack { get; set; }

	public string Description { get; set; }

	public string Wound { get; set; }

	public string Location { get; set; }

	public DateTime ReceivedAt { get; set; }

	public bool? FromYou { get; set; }
}
