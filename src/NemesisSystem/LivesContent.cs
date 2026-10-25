using System.Collections.Generic;

namespace NemesisSystem;

public class LivesContent
{
	public string About { get; set; } = "Life events for every man's past (Backstory.cs). Edit, add rows, reload the mod (Insert). who: any / man / woman / gang / loner / irish / irishborn / south / north / rich / poor / a faction id / trade:<Trade> (| = or). tilt: temper:points. fx: aggr:+N fear:+N friendly surly devout godless sharp dim. text tokens: {home} {town} {n}. mirrorTitle tokens: {name} {town}.";

	public Dictionary<string, List<string>> Places { get; set; } = new Dictionary<string, List<string>>();

	public List<Battle> GreyBattles { get; set; } = new List<Battle>();

	public List<Battle> BlueBattles { get; set; } = new List<Battle>();

	public Dictionary<string, List<LifeRow>> Pools { get; set; } = new Dictionary<string, List<LifeRow>>();

	public List<string> Removed { get; set; } = new List<string>();
}
