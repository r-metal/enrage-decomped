using System;
using RDR2;

namespace NemesisSystem.BulletImpact;

public static class Ammo
{
	public static AmmoKind FromHash(uint hash)
	{
		if (hash == 0)
		{
			return AmmoKind.Regular;
		}
		object obj;
		if (!Enum.IsDefined(typeof(eAmmoType), hash))
		{
			obj = "";
		}
		else
		{
			eAmmoType eAmmoType = (eAmmoType)hash;
			obj = eAmmoType.ToString();
		}
		string text = (string)obj;
		if (text == "")
		{
			return AmmoKind.Regular;
		}
		if (text.Contains("Explosive") || text == "ArrowDynamite")
		{
			return AmmoKind.Explosive;
		}
		if (text.Contains("Incendiary"))
		{
			return AmmoKind.Incendiary;
		}
		if (text.Contains("SplitPoint"))
		{
			return AmmoKind.SplitPoint;
		}
		if (text.Contains("HighVelocity"))
		{
			return AmmoKind.HighVelocity;
		}
		if (text.Contains("Express"))
		{
			return AmmoKind.Express;
		}
		if (text.Contains("Slug"))
		{
			return AmmoKind.Slug;
		}
		return text switch
		{
			"ArrowFire" => AmmoKind.FireArrow, 
			"ArrowPoison" => AmmoKind.PoisonArrow, 
			"ArrowSmallGame" => AmmoKind.SmallGameArrow, 
			"ArrowImproved" => AmmoKind.ImprovedArrow, 
			_ => AmmoKind.Regular, 
		};
	}

	public static (float budget, float resist, float cord) Mods(AmmoKind a)
	{
		return a switch
		{
			AmmoKind.SplitPoint => (budget: 0.75f, resist: 1.2f, cord: 1f), 
			AmmoKind.HighVelocity => (budget: 1.4f, resist: 0.7f, cord: 0.6f), 
			AmmoKind.Express => (budget: 1.15f, resist: 0.9f, cord: 0.9f), 
			AmmoKind.Slug => (budget: 5f, resist: 0.9f, cord: 0.7f), 
			AmmoKind.SmallGameArrow => (budget: 0.5f, resist: 1.3f, cord: 1.2f), 
			AmmoKind.ImprovedArrow => (budget: 1.3f, resist: 0.9f, cord: 0.9f), 
			AmmoKind.MachineGun => (budget: 1.4f, resist: 0.6f, cord: 0.5f), 
			_ => (budget: 1f, resist: 1f, cord: 1f), 
		};
	}

	public static string Label(AmmoKind a)
	{
		return a switch
		{
			AmmoKind.SplitPoint => "split point", 
			AmmoKind.Express => "express", 
			AmmoKind.HighVelocity => "high velocity", 
			AmmoKind.Explosive => "explosive", 
			AmmoKind.Incendiary => "incendiary", 
			AmmoKind.Slug => "slug", 
			AmmoKind.FireArrow => "fire arrow", 
			AmmoKind.PoisonArrow => "poison arrow", 
			AmmoKind.SmallGameArrow => "small game arrow", 
			AmmoKind.ImprovedArrow => "improved arrow", 
			AmmoKind.MachineGun => "machine-gun round", 
			_ => null, 
		};
	}

	public static bool MachineGunName(string weaponName)
	{
		if (weaponName != null && weaponName.StartsWith("Turret"))
		{
			return !weaponName.Contains("Cannon");
		}
		return false;
	}

	public static AmmoKind Of(XRayEntry e)
	{
		if (e.Ammo != AmmoKind.Regular || !MachineGunName(e.WeaponName))
		{
			return e.Ammo;
		}
		return AmmoKind.MachineGun;
	}
}
