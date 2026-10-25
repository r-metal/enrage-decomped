using System;
using System.Collections.Generic;
using System.Linq;
using RDR2;
using RDR2.Native;

namespace NemesisSystem;

internal static class Loadout
{
	public enum Band
	{
		Fists,
		Blade,
		Gun
	}

	public sealed class Kit
	{
		public Band Band;

		public eWeapon? Hand;

		public string HandName;

		public eWeapon? Long;

		public string LongName;

		public bool PreferLong;
	}

	private static int Seed(NemesisRecord r)
	{
		int num = 17;
		string text = r.Id ?? r.Name ?? "";
		foreach (char c in text)
		{
			num = num * 31 + c;
		}
		return num;
	}

	public static int Luck(NemesisRecord r)
	{
		return new Random(Seed(r)).Next(-6, 7);
	}

	public static int TemperShift(NemesisRecord r)
	{
		return r.Personality switch
		{
			Personality.Hothead => -8, 
			Personality.Coward => -6, 
			Personality.Cold => -4, 
			Personality.Drifter => 4, 
			Personality.Proud => 6, 
			_ => 0, 
		};
	}

	public static int AggressionShift(NemesisRecord r)
	{
		if (!r.TraitsRolled)
		{
			return 0;
		}
		return (50 - r.Aggression) / 6;
	}

	public static int Shift(NemesisRecord r)
	{
		return TemperShift(r) + AggressionShift(r) + Luck(r);
	}

	public static int BladeAt(NemesisRecord r)
	{
		return Math.Max(20, Math.Min(60, Hate.VengefulAt + Shift(r)));
	}

	public static int GunAt(NemesisRecord r)
	{
		return Math.Max(BladeAt(r) + 12, Math.Min(88, Hate.MurderousAt + Shift(r)));
	}

	public static Band BandOf(NemesisRecord r)
	{
		if (!Gangs.Orders(r) && r.Hate < GunAt(r))
		{
			if (r.Hate < BladeAt(r))
			{
				return Band.Fists;
			}
			return Band.Blade;
		}
		return Band.Gun;
	}

	public static string ShiftWhy(NemesisRecord r)
	{
		List<string> list = new List<string>();
		int num = TemperShift(r);
		int num2 = AggressionShift(r);
		int num3 = Luck(r);
		if (num != 0)
		{
			list.Add($"{r.Personality?.ToString().ToLowerInvariant()} {num:+0;-0}");
		}
		if (num2 != 0)
		{
			list.Add($"{Traits.Aggr(r.Aggression).ToLowerInvariant()} {num2:+0;-0}");
		}
		if (num3 != 0)
		{
			list.Add($"luck {num3:+0;-0}");
		}
		if (list.Count != 0)
		{
			return string.Join(", ", list);
		}
		return "no shift";
	}

	private static T Pick<T>(Random rng, IList<(T item, int w)> opts)
	{
		int val = opts.Sum(((T item, int w) o) => Math.Max(0, o.w));
		int num = rng.Next(Math.Max(1, val));
		foreach (var opt in opts)
		{
			T item = opt.item;
			int item2 = opt.w;
			num -= Math.Max(0, item2);
			if (num < 0)
			{
				return item;
			}
		}
		return opts[opts.Count - 1].item;
	}

	private static bool TradeHas(NemesisRecord r, params string[] words)
	{
		if (r.Trade != null)
		{
			return words.Any((string w) => r.Trade.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
		}
		return false;
	}

	public static Kit For(NemesisRecord r)
	{
		Band band = BandOf(r);
		Gang gang = Gangs.Of(r);
		if (gang != null && gang.TribalKit)
		{
			return new Kit
			{
				Band = ((band != Band.Gun) ? Band.Blade : Band.Gun),
				Hand = gang.KitBlade,
				HandName = gang.KitBladeName,
				Long = gang.KitBow,
				LongName = gang.KitBowName,
				PreferLong = (band == Band.Gun)
			};
		}
		Kit kit = new Kit
		{
			Band = band
		};
		if (band == Band.Fists)
		{
			return kit;
		}
		Random rng = new Random(Seed(r) + (int)band * 7919);
		if (band == Band.Blade)
		{
			List<((eWeapon, string), int)> list = new List<((eWeapon, string), int)>();
			list.Add(((eWeapon.MeleeKnife, "a hunting knife"), 5));
			list.Add(((eWeapon.MeleeKnifeTrader, "a trader's knife"), 2));
			list.Add(((eWeapon.MeleeKnifeCivilWar, "an old army knife"), Gangs.Of(r)?.OldArmyKnifeWeight ?? 1));
			list.Add(((eWeapon.MeleeMachete, "a machete"), (r.Aggression >= 65) ? 4 : 2));
			list.Add(((eWeapon.MeleeHatchet, "a hatchet"), TradeHas(r, "farm", "ranch", "hunt", "wood", "trapper", "homestead") ? 5 : 2));
			list.Add(((eWeapon.MeleeCleaver, "a cleaver"), (!TradeHas(r, "butcher", "cook")) ? 1 : 6));
			List<((eWeapon, string), int)> opts = list;
			(eWeapon, string) tuple = Pick(rng, opts);
			eWeapon item = tuple.Item1;
			string item2 = tuple.Item2;
			kit.Hand = item;
			kit.HandName = item2;
			return kit;
		}
		Gang gang2 = Gangs.Of(r);
		if (gang2 != null && gang2.UniformKit)
		{
			kit.Hand = gang2.UniformSide;
			kit.HandName = gang2.UniformSideName;
			kit.Long = gang2.UniformLong;
			kit.LongName = gang2.UniformLongName;
			kit.PreferLong = true;
			return kit;
		}
		List<((eWeapon, string), int)> list2 = new List<((eWeapon, string), int)>();
		switch ((!r.TraitsRolled) ? Wealth.Working : r.Wealth)
		{
		case Wealth.Poor:
			list2.Add(((eWeapon.RevolverCattleman, "a worn Cattleman"), 5));
			list2.Add(((eWeapon.RevolverDoubleAction, "a Double-Action"), 1));
			break;
		case Wealth.Rich:
			list2.Add(((eWeapon.PistolVolcanic, "a Volcanic pistol"), 2));
			list2.Add(((eWeapon.PistolMauser, "a Mauser pistol"), 1));
			list2.Add(((eWeapon.RevolverSchofield, "a Schofield"), 2));
			list2.Add(((eWeapon.RevolverDoubleActionGambler, "a gambler's Double-Action"), 1));
			break;
		case Wealth.Comfortable:
			list2.Add(((eWeapon.RevolverSchofield, "a Schofield"), 3));
			list2.Add(((eWeapon.RevolverDoubleAction, "a Double-Action"), 2));
			list2.Add(((eWeapon.RevolverCattleman, "a Cattleman"), 1));
			break;
		default:
			list2.Add(((eWeapon.RevolverCattleman, "a Cattleman"), 4));
			list2.Add(((eWeapon.RevolverDoubleAction, "a Double-Action"), 2));
			list2.Add(((eWeapon.RevolverSchofield, "a Schofield"), 1));
			break;
		}
		if (gang2 != null)
		{
			(eWeapon, string, int, int)[] extraSidearms = gang2.ExtraSidearms;
			for (int i = 0; i < extraSidearms.Length; i++)
			{
				(eWeapon, string, int, int) tuple2 = extraSidearms[i];
				if (r.Rank >= tuple2.Item4)
				{
					list2.Add(((tuple2.Item1, tuple2.Item2), tuple2.Item3));
				}
			}
		}
		Kit kit2 = kit;
		(eWeapon, string) tuple3 = Pick(rng, list2);
		eWeapon? hand = tuple3.Item1;
		kit2.Hand = hand;
		kit.HandName = tuple3.Item2;
		bool flag = r.Personality == Personality.Coward || r.Personality == Personality.Cold;
		if (r.Hate >= GunAt(r) + 15 || r.Rank >= 3 || flag)
		{
			List<((eWeapon, string), int)> list = new List<((eWeapon, string), int)>();
			list.Add(((eWeapon.RepeaterCarbine, "a carbine repeater"), 3));
			list.Add(((eWeapon.RepeaterWinchester, "a Lancaster-style repeater"), 2));
			list.Add(((eWeapon.ShotgunDoubleBarrel, "a double-barrel shotgun"), (r.TraitsRolled && r.Wealth == Wealth.Poor) ? 5 : 2));
			list.Add(((eWeapon.ShotgunPump, "a pump shotgun"), 1));
			list.Add(((eWeapon.RifleVarmint, "a varmint rifle"), (!TradeHas(r, "hunt", "trapper")) ? 1 : 4));
			list.Add(((eWeapon.RifleBoltAction, "a bolt-action rifle"), (r.Personality != Personality.Cold) ? 1 : 3));
			List<((eWeapon, string), int)> list3 = list;
			if (gang2 != null)
			{
				(eWeapon, string, int)[] extraLongGuns = gang2.ExtraLongGuns;
				for (int i = 0; i < extraLongGuns.Length; i++)
				{
					(eWeapon, string, int) tuple4 = extraLongGuns[i];
					list3.Add(((tuple4.Item1, tuple4.Item2), tuple4.Item3));
				}
			}
			kit2 = kit;
			tuple3 = Pick(rng, list3);
			hand = tuple3.Item1;
			kit2.Long = hand;
			kit.LongName = tuple3.Item2;
			kit.PreferLong = flag;
		}
		return kit;
	}

	public static string Describe(NemesisRecord r)
	{
		Kit kit = For(r);
		if (kit.Band == Band.Fists)
		{
			return "comes with his fists";
		}
		if (kit.Band == Band.Blade)
		{
			return "carries " + kit.HandName;
		}
		if (!kit.Long.HasValue)
		{
			return "carries " + kit.HandName;
		}
		return "carries " + kit.HandName + " and " + kit.LongName;
	}

	private static bool IsKnife(eWeapon w)
	{
		return w.ToString().StartsWith("MeleeKnife");
	}

	private static bool IsHandToHand(eWeapon w)
	{
		string text = w.ToString();
		if (!text.StartsWith("Melee"))
		{
			return text.StartsWith("Thrown");
		}
		return true;
	}

	public static bool IsBow(eWeapon w)
	{
		return w.ToString().StartsWith("Bow");
	}

	public static void GiveArrows(Ped ped, int n = 40)
	{
		try
		{
			WEAPON._ADD_AMMO_TO_PED_BY_TYPE(ped.Handle, 954660191u, n, 752097756u);
		}
		catch
		{
		}
	}

	public static void Arm(Ped ped, NemesisRecord r, bool drawNow)
	{
		Kit kit = For(r);
		if (kit.Hand.HasValue)
		{
			bool flag = IsHandToHand(kit.Hand.Value);
			eWeaponAttachPoint attachPoint = ((!flag) ? eWeaponAttachPoint.PistolR : (IsKnife(kit.Hand.Value) ? eWeaponAttachPoint.Knife : eWeaponAttachPoint.Melee));
			bool flag2 = drawNow && (!kit.PreferLong || !kit.Long.HasValue);
			ped.GiveWeapon(kit.Hand.Value, flag ? 1 : 100, attachPoint, flag2, !flag2);
		}
		if (kit.Long.HasValue)
		{
			bool flag3 = drawNow && kit.PreferLong;
			bool flag4 = IsBow(kit.Long.Value);
			ped.GiveWeapon(kit.Long.Value, 60, flag4 ? eWeaponAttachPoint.Bow : eWeaponAttachPoint.Rifle, flag3, !flag3);
			if (flag4)
			{
				GiveArrows(ped);
			}
		}
	}
}
