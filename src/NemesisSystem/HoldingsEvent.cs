namespace NemesisSystem;

internal sealed class HoldingsEvent
{
	public HoldingsAct Act;

	public string Text;

	public string SiteId;

	public NemesisRecord Who;

	public Gang From;

	public Gang To;

	public int Amount;

	public string Place;

	public bool Stash;

	public bool Big;

	public override string ToString()
	{
		return string.Format("{0}{1}{2}", Act, (Text != null) ? (" " + Text) : "", (SiteId != null) ? (" @" + SiteId) : "");
	}
}
