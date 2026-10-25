using System.Collections.Generic;
using System.Linq;
using RDR2;
using RDR2.Native;

namespace NemesisSystem;

internal static class GunMelee
{
	private const int ShotWindowMs = 500;

	private const float ReachMetres = 3f;

	private const int KnockoutMemoryMs = 30000;

	private static int _lastShot = -100000;

	private static readonly Dictionary<int, int> Struck = new Dictionary<int, int>();

	private static readonly Dictionary<int, int> Wounded = new Dictionary<int, int>();

	private static readonly HashSet<int> OutCold = new HashSet<int>();

	public static bool PlayerFiredRecently => Game.GameTime - _lastShot < 500;

	public static int LastShotAt => _lastShot;

	public static void Tick(Ped player)
	{
		if (PED.IS_PED_SHOOTING(player.Handle) && WEAPON.GET_WEAPONTYPE_GROUP(WEAPON._GET_PED_CURRENT_HELD_WEAPON(player.Handle)) != 308416707)
		{
			_lastShot = Game.GameTime;
		}
		if (Game.FrameCount % 600 != 0)
		{
			return;
		}
		foreach (int item in Struck.Keys.Where((int h) => !ENTITY.DOES_ENTITY_EXIST(h)).ToList())
		{
			Struck.Remove(item);
			OutCold.Remove(item);
		}
		foreach (int item2 in Wounded.Keys.Where((int h) => !ENTITY.DOES_ENTITY_EXIST(h)).ToList())
		{
			Wounded.Remove(item2);
		}
		foreach (int item3 in OutCold.Where((int h) => !ENTITY.DOES_ENTITY_EXIST(h)).ToList())
		{
			OutCold.Remove(item3);
		}
	}

	public static bool IsGun(uint weapon)
	{
		uint num = WEAPON.GET_WEAPONTYPE_GROUP(weapon);
		if (num != 416676503 && num != 3193669993u && num != 3700405225u && num != 970310034 && num != 3082541095u)
		{
			return num == 860033945;
		}
		return true;
	}

	public static bool IsGunMeleeHit(Ped player, int victim, uint weapon)
	{
		if (!IsGun(weapon) || Game.GameTime - _lastShot < 500)
		{
			return false;
		}
		Entity entity = Entity.FromHandle(victim);
		if (entity == null || !entity.Exists() || entity.Position.DistanceTo(player.Position) > 3f)
		{
			return false;
		}
		Struck[victim] = Game.GameTime;
		return true;
	}

	public static void MarkWounded(int victim)
	{
		Wounded[victim] = Game.GameTime;
	}

	public static void Forget(int h)
	{
		Struck.Remove(h);
		Wounded.Remove(h);
		OutCold.Remove(h);
	}

	public static bool KnockedOutByGunButt(Ped p)
	{
		if (!Struck.TryGetValue(p.Handle, out var value))
		{
			return false;
		}
		if (Wounded.TryGetValue(p.Handle, out var value2) && value2 > value)
		{
			OutCold.Remove(p.Handle);
			return false;
		}
		if (OutCold.Contains(p.Handle))
		{
			return true;
		}
		if (Game.GameTime - value > 30000)
		{
			return false;
		}
		if (p.IsDead)
		{
			int num = PED.GET_PED_SOURCE_OF_DEATH(p.Handle);
			if (num != 0 && num != Game.Player.Character.Handle)
			{
				return false;
			}
			OutCold.Add(p.Handle);
		}
		return true;
	}
}
