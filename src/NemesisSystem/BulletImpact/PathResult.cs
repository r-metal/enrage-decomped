using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem.BulletImpact;

public sealed class PathResult
{
	public V3 Start;

	public bool HasEntry;

	public V3 Entry;

	public V3 End;

	public bool Exited;

	public bool Estimated;

	public bool Guessed;

	public bool Blunt;

	public BlowKind Blow;

	public string EntryText = "";

	public string EndText = "";

	public readonly List<DamageItem> Damage = new List<DamageItem>();

	public readonly HashSet<string> HitKeys = new HashSet<string>();

	public Severity Worst
	{
		get
		{
			if (Damage.Count != 0)
			{
				return Damage.Max((DamageItem d) => d.Severity);
			}
			return Severity.Minor;
		}
	}
}
