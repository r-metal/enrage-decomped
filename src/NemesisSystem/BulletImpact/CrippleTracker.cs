using System;
using System.Collections.Generic;
using System.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem.BulletImpact;

internal sealed class CrippleTracker
{
	private sealed class State
	{
		public CrippleLevel Level;

		public int NextDropTime;

		public int Model;
	}

	private const int PcfForceInjuredMovement = 336;

	private const int MaxTracked = 64;

	public static bool Hold;

	private readonly Action<string> _log;

	private readonly Dictionary<int, State> _peds = new Dictionary<int, State>();

	public CrippleTracker(Action<string> log = null)
	{
		_log = log;
	}

	public CrippleLevel Apply(Ped ped, CrippleLevel level)
	{
		if (level == CrippleLevel.None || ped == null || !ped.Exists() || ped.IsDead || Kids.IsChild(ped) || Kids.Protected(ped))
		{
			return CrippleLevel.None;
		}
		if (!_peds.TryGetValue(ped.Handle, out var value))
		{
			if (_peds.Count >= 64)
			{
				Vector3 position = Game.Player.Character.Position;
				int num = 0;
				float num2 = -1f;
				foreach (KeyValuePair<int, State> ped3 in _peds)
				{
					Ped ped2 = new Ped(ped3.Key);
					float num3 = (ped2.Exists() ? ped2.Position.DistanceTo(position) : float.MaxValue);
					if (num3 > num2)
					{
						num2 = num3;
						num = ped3.Key;
					}
				}
				_peds.Remove(num);
				_log?.Invoke(string.Format("CRIPPLE tracker full ({0}) - let go of ped {1} ({2}) to track ped {3}", 64, num, (num2 == float.MaxValue) ? "gone" : $"{num2:0} m off", ped.Handle));
			}
			value = (_peds[ped.Handle] = new State
			{
				Model = ped.Model.Hash
			});
		}
		if (level <= value.Level)
		{
			return value.Level;
		}
		value.Level = level;
		int handle = ped.Handle;
		switch (level)
		{
		case CrippleLevel.Limping:
			InjuredGait(handle);
			PED._SET_PED_DESIRED_LOCO_FOR_MODEL(handle, "injured_general");
			break;
		case CrippleLevel.Legless:
			Drop(ped, value);
			InjuredGait(handle);
			PED.SET_PED_INJURED_ON_GROUND_BEHAVIOUR(handle, 100000f);
			_log?.Invoke($"CRIPPLE ped={handle} Legless health={ped.Health}");
			break;
		case CrippleLevel.SpineBroken:
			InjuredGait(handle);
			PED.SET_PED_TO_RAGDOLL(handle, 10000, 10000, 0, p4: false, p5: false, "");
			break;
		case CrippleLevel.NeckBroken:
			TASK.TASK_KNOCKED_OUT(handle, 0f, permanently: true);
			break;
		}
		return level;
	}

	private static void InjuredGait(int h)
	{
		PED.SET_PED_CONFIG_FLAG(h, 336, value: true);
		PED._FAKE_SET_PED_LOCO_INJURED(h, enabled: true);
	}

	public CrippleLevel LevelOf(int handle)
	{
		if (!_peds.TryGetValue(handle, out var value))
		{
			return CrippleLevel.None;
		}
		return value.Level;
	}

	public void Forget(int handle)
	{
		_peds.Remove(handle);
	}

	public void Clear()
	{
		_peds.Clear();
	}

	private static void Drop(Ped ped, State st)
	{
		PED.SET_PED_TO_RAGDOLL(ped.Handle, 2500, 5000, 0, p4: false, p5: false, "");
		st.NextDropTime = Game.GameTime + 1500;
	}

	private static bool IsDown(Ped ped)
	{
		if (!ped.IsRagdoll && !TASK.IS_PED_IN_WRITHE(ped.Handle) && !PED.IS_PED_PRONE(ped.Handle))
		{
			return ped.HeightAboveGround < 0.7f;
		}
		return true;
	}

	public void Tick(Ped player)
	{
		if (_peds.Count == 0)
		{
			return;
		}
		foreach (int item in _peds.Keys.ToList())
		{
			State state = _peds[item];
			Ped ped = Entity.FromHandle(item) as Ped;
			if (ped == null || !ped.Exists() || ped.IsDead)
			{
				_peds.Remove(item);
			}
			else if (state.Model != 0 && ped.Model.Hash != state.Model)
			{
				_peds.Remove(item);
				_log?.Invoke($"CRIPPLE handle {item} reused by another model - dropped");
			}
			else
			{
				if (Hold || ENTITY.IS_ENTITY_ATTACHED(item) || ped.IsHogtied || PED.IS_PED_ON_MOUNT(item) || PED.IS_PED_IN_ANY_VEHICLE(item, atGetIn: false) || Kids.IsChild(ped))
				{
					continue;
				}
				switch (state.Level)
				{
				case CrippleLevel.Limping:
					PED.SET_PED_MAX_MOVE_BLEND_RATIO(item, 1f);
					break;
				case CrippleLevel.Legless:
					if (!IsDown(ped) && Game.GameTime >= state.NextDropTime)
					{
						Drop(ped, state);
					}
					break;
				case CrippleLevel.SpineBroken:
					if (ped.IsRagdoll)
					{
						PED.RESET_PED_RAGDOLL_TIMER(item);
					}
					else
					{
						PED.SET_PED_TO_RAGDOLL(item, 10000, 10000, 0, p4: false, p5: false, "");
					}
					break;
				case CrippleLevel.NeckBroken:
					if (Game.GameTime >= state.NextDropTime && !ped.IsRagdoll && !(ped.HeightAboveGround < 0.5f))
					{
						TASK.TASK_KNOCKED_OUT(item, 0f, permanently: true);
						state.NextDropTime = Game.GameTime + 1500;
					}
					break;
				}
			}
		}
	}
}
