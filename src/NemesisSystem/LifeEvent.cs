namespace NemesisSystem;

internal sealed class LifeEvent
{
	public readonly string Who;

	public readonly string Title;

	public readonly string Thought;

	public readonly string Mood;

	public readonly int Weight;

	public readonly int Hours;

	public readonly int Odds;

	public readonly string[] Effects;

	public readonly string When;

	public readonly string Id;

	public LifeEvent(string who, string title, string thought, string mood, int weight, int hours, string when = null, int odds = 10, params string[] effects)
		: this(null, who, title, thought, mood, weight, hours, when, odds, effects)
	{
	}

	public LifeEvent(string id, string who, string title, string thought, string mood, int weight, int hours, string when, int odds, string[] effects)
	{
		Who = who;
		Title = title;
		Thought = thought;
		Mood = mood;
		Weight = weight;
		Hours = hours;
		When = when;
		Odds = odds;
		Effects = effects ?? new string[0];
		Id = (string.IsNullOrEmpty(id) ? LifeCond.Slug(who + " " + title) : id);
	}

	public bool Fits(NemesisRecord r)
	{
		switch (Who)
		{
		case "any":
		{
			Gang gang = Gangs.Of(r);
			if (gang != null && gang.OwnLifeOnly)
			{
				return false;
			}
			break;
		}
		case "gang":
		{
			Gang gang2 = Gangs.Of(r);
			if (gang2 == null || !gang2.GangLifeEvents)
			{
				return false;
			}
			break;
		}
		case "loner":
			if (r.Gang != null)
			{
				return false;
			}
			break;
		case "raider":
			if (r.Gang != "lemoyneraiders")
			{
				return false;
			}
			break;
		default:
			if (r.Gang != Who)
			{
				return false;
			}
			break;
		}
		return LifeCond.Eval(When, r);
	}
}
