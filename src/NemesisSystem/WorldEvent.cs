using System.Collections.Generic;

namespace NemesisSystem;

public class WorldEvent
{
	public string Id { get; set; }

	public long GameMin { get; set; }

	public long? Shift { get; set; }

	public int Day { get; set; }

	public string When { get; set; }

	public string Kind { get; set; }

	public string Title { get; set; }

	public string Place { get; set; }

	public bool HasPos { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public float Z { get; set; }

	public List<string> Gangs { get; set; } = new List<string>();

	public List<WorldPart> People { get; set; } = new List<WorldPart>();

	public List<string> Lines { get; set; } = new List<string>();

	public Dictionary<string, int> UnnamedDead { get; set; } = new Dictionary<string, int>();

	public bool Heard { get; set; }

	public long AftermathUntil { get; set; }

	public bool Visited { get; set; }

	public string Tag { get; set; }

	public bool Marked { get; set; }

	public bool IsPast { get; set; }

	public bool Reached { get; set; }

	public bool ForceSurvivors { get; set; }
}
