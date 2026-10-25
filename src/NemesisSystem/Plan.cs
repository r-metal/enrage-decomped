using System.Collections.Generic;

namespace NemesisSystem;

internal class Plan
{
	public string Id { get; set; }

	public string Gang { get; set; }

	public string Kind { get; set; }

	public string Title { get; set; }

	public string Place { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public float Z { get; set; }

	public long DueAt { get; set; }

	public int WindowMin { get; set; } = 180;

	public string LeaderId { get; set; }

	public int Crew { get; set; } = 3;

	public bool Known { get; set; }

	public bool Lie { get; set; }

	public string ToldBy { get; set; }

	public string ToldAbout { get; set; }

	public bool Live { get; set; }

	public bool Visited { get; set; }

	public bool Done { get; set; }

	public string Outcome { get; set; }

	public string Habit { get; set; }

	public string Dead { get; set; }

	public List<string> MoreDead { get; set; }

	public int ScarsAt { get; set; }

	public bool Desperate { get; set; }

	public bool Engaged { get; set; }

	public bool PlotSet { get; set; }

	public float PlotX { get; set; }

	public float PlotY { get; set; }

	public float PlotZ { get; set; }
}
