using System.Collections.Generic;

namespace NemesisSystem;

internal class LifeT
{
	public string Kind;

	public string Who;

	public string Text;

	public string Fx;

	public string MirrorKind;

	public string MirrorTitle;

	public string TiltText;

	public int MinAge;

	public int MaxAge;

	public int W;

	public bool Public;

	public bool MirrorHeard;

	public int BornFrom;

	public int BornTo;

	public string BornIn;

	public Dictionary<Personality, int> Tilt = new Dictionary<Personality, int>();

	public bool Allows(NemesisRecord r, Backstory.Ctx c)
	{
		if ((BornFrom == 0 || r.BornYear >= BornFrom) && (BornTo == 0 || r.BornYear <= BornTo))
		{
			if (!string.IsNullOrEmpty(BornIn))
			{
				return (c.Born ?? "").Contains(BornIn);
			}
			return true;
		}
		return false;
	}
}
