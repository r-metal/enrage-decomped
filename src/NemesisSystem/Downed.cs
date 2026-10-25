using System;
using System.Collections.Generic;
using RDR2;
using RDR2.Native;

namespace NemesisSystem;

internal static class Downed
{
	private const int FinishAfterMs = 800;

	private static readonly Dictionary<int, int> Since = new Dictionary<int, int>();

	private static readonly HashSet<int> Finished = new HashSet<int>();

	private static readonly Dictionary<int, int> HealthWhenDown = new Dictionary<int, int>();

	public static Action<string> Log;

	private static readonly HashSet<int> Kicked = new HashSet<int>();

	private static readonly Dictionary<int, string> HowKilled = new Dictionary<int, string>();

	public static void Saw(Ped p, bool down)
	{
		int handle = p.Handle;
		if (!down)
		{
			Since.Remove(handle);
			HealthWhenDown.Remove(handle);
			if (!p.IsDead && Kicked.Remove(handle))
			{
				Finished.Remove(handle);
				Log?.Invoke($"FINISHED ped={handle} undone - he's up again (kicked, not killed)");
			}
			return;
		}
		int health = p.Health;
		if (!Since.ContainsKey(handle))
		{
			Since[handle] = Game.GameTime;
			HealthWhenDown[handle] = health;
			Log?.Invoke($"DOWN ped={handle} health={health} dead={p.IsDead} incapacitated={p.IsIncapacitated}");
		}
		else if (!Finished.Contains(handle) && Game.GameTime - Since[handle] >= 800 && health < HealthWhenDown[handle] - 3)
		{
			Finished.Add(handle);
			Log?.Invoke($"FINISHED ped={handle} (health {HealthWhenDown[handle]} -> {health})");
		}
		else if (health > HealthWhenDown[handle])
		{
			HealthWhenDown[handle] = health;
		}
	}

	public static void Hit(int handle, string how)
	{
		if (!Finished.Contains(handle) && Since.TryGetValue(handle, out var value) && Game.GameTime - value >= 800)
		{
			Finished.Add(handle);
			Log?.Invoke($"FINISHED ped={handle} ({how})");
		}
	}

	public static bool IsFinished(int handle)
	{
		return Finished.Contains(handle);
	}

	public static string How(int handle)
	{
		if (!HowKilled.TryGetValue(handle, out var value))
		{
			return null;
		}
		return value;
	}

	public static void KickedToDeath(int handle)
	{
		if (Finished.Add(handle))
		{
			Kicked.Add(handle);
			HowKilled[handle] = "stomped";
			Log?.Invoke($"FINISHED ped={handle} (stomped to death lying out cold - the game killed him)");
		}
	}

	public static bool WasKicked(int handle)
	{
		return Kicked.Contains(handle);
	}

	public static void ChokedToDeath(int handle)
	{
		if (Finished.Add(handle))
		{
			Kicked.Add(handle);
			HowKilled[handle] = "choked";
			Log?.Invoke($"FINISHED ped={handle} (choked to death in a fight - presumed dead)");
		}
	}

	public static void DiedOutCold(int handle)
	{
		if (Finished.Add(handle))
		{
			Log?.Invoke($"FINISHED ped={handle} (died lying out cold - not your stomp)");
		}
	}

	public static void Forget(int handle)
	{
		Since.Remove(handle);
		Finished.Remove(handle);
		HealthWhenDown.Remove(handle);
		Kicked.Remove(handle);
		HowKilled.Remove(handle);
	}

	public static void Prune()
	{
		foreach (int item in new List<int>(Since.Keys))
		{
			if (!(Entity.FromHandle(item) is Ped ped) || !ped.Exists())
			{
				Since.Remove(item);
			}
		}
		Finished.RemoveWhere((int h) => !(Entity.FromHandle(h) is Ped ped2) || !ped2.Exists());
		Kicked.RemoveWhere((int h) => !Finished.Contains(h));
		foreach (int item2 in new List<int>(HowKilled.Keys))
		{
			if (!Finished.Contains(item2))
			{
				HowKilled.Remove(item2);
			}
		}
		foreach (int item3 in new List<int>(HealthWhenDown.Keys))
		{
			if (!ENTITY.DOES_ENTITY_EXIST(item3))
			{
				HealthWhenDown.Remove(item3);
			}
		}
	}
}
