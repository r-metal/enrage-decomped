using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem.BulletImpact;

internal static class CrippleRules
{
	public static CrippleLevel Assess(IEnumerable<DamageItem> damage, out string effect)
	{
		HashSet<string> hashSet = new HashSet<string>(from d in damage
			where !d.Chipped
			select d.Key);
		if (hashSet.Contains("cervical"))
		{
			effect = "Neck broken - paralyzed";
			return CrippleLevel.NeckBroken;
		}
		if (hashSet.Contains("spine_t") || hashSet.Contains("spine_l"))
		{
			effect = "Spine severed - paralyzed";
			return CrippleLevel.SpineBroken;
		}
		if (hashSet.Contains("pelvis"))
		{
			effect = "Pelvis shattered - can't stand";
			return CrippleLevel.Legless;
		}
		(string, string)[] array = new(string, string)[2]
		{
			("knee", "Knee shattered"),
			("femur", "Leg broken")
		};
		for (int num = 0; num < array.Length; num++)
		{
			var (text, text2) = array[num];
			if (hashSet.Contains(text + "_l") || hashSet.Contains(text + "_r"))
			{
				effect = text2 + " - can't stand";
				return CrippleLevel.Legless;
			}
		}
		bool flag = hashSet.Contains("foot_l");
		bool flag2 = hashSet.Contains("foot_r");
		if (flag && flag2)
		{
			effect = "Both feet shattered - can't stand";
			return CrippleLevel.Legless;
		}
		if (flag || flag2)
		{
			effect = "Foot shattered - limping";
			return CrippleLevel.Limping;
		}
		bool flag3 = hashSet.Contains("tibia_l");
		bool flag4 = hashSet.Contains("tibia_r");
		if (flag3 && flag4)
		{
			effect = "Both shins broken - can't stand";
			return CrippleLevel.Legless;
		}
		if (flag3 || flag4)
		{
			effect = "Shin broken - limping";
			return CrippleLevel.Limping;
		}
		effect = null;
		return CrippleLevel.None;
	}
}
