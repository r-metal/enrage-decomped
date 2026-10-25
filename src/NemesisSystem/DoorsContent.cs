using System.Collections.Generic;

namespace NemesisSystem;

public class DoorsContent
{
	public string About { get; set; }

	public List<DoorRow> Doors { get; set; } = new List<DoorRow>();
}
