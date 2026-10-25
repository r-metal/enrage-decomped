using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem.BulletImpact;

public sealed class WoundReport
{
	public List<DamageItem> Damage = new List<DamageItem>();

	public string EntryText = "";

	public string WeaponName = "";

	public float Distance;

	public DamageItem Worst => Damage.FirstOrDefault();

	public bool Has(string key)
	{
		return Damage.Any((DamageItem d) => d.Key == key);
	}
}
