using Newtonsoft.Json;
using RDR2.Math;

namespace NemesisSystem;

internal sealed class StashSpot
{
	[JsonIgnore]
	public bool BuiltIn;

	public string Town { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public float Z { get; set; }

	public float H { get; set; }

	[JsonIgnore]
	public Vector3 Pos => new Vector3(X, Y, Z);
}
