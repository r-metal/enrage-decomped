using Newtonsoft.Json;

namespace NemesisSystem;

public class MercyNote
{
	[JsonIgnore]
	public uint Model;

	public string Gang { get; set; }

	public int Notoriety { get; set; }

	public string Town { get; set; }

	public float Good { get; set; }

	public long At { get; set; }
}
