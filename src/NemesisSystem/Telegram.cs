namespace NemesisSystem;

public class Telegram
{
	public string Id { get; set; }

	public string ToId { get; set; }

	public bool ToNemesis { get; set; }

	public string ToName { get; set; }

	public string Message { get; set; }

	public string GiftGang { get; set; }

	public string Text { get; set; }

	public string From { get; set; }

	public long SentAt { get; set; }

	public long ArriveAt { get; set; }

	public bool Arrived { get; set; }

	public bool Read { get; set; }

	public string Reply { get; set; }

	public string Effect { get; set; }

	public string Outlook { get; set; }

	public string Result { get; set; }

	public int Money { get; set; }

	public string TreasuryGang { get; set; }

	public string TreasurySite { get; set; }

	public string TrackId { get; set; }

	public bool Spontaneous { get; set; }

	public string InReplyTo { get; set; }

	public bool NoAnswer { get; set; }
}
