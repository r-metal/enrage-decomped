using System.ComponentModel;

namespace NemesisSystem;

public class QuestionRow
{
	public string Id { get; set; }

	public string Cat { get; set; }

	public string Text { get; set; }

	public int Need { get; set; }

	public int Odds { get; set; }

	public bool Lies { get; set; }

	public string Requires { get; set; }

	public string Answer { get; set; }

	[DefaultValue(true)]
	public bool Once { get; set; } = true;
}
