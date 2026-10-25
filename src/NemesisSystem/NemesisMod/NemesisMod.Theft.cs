using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NemesisSystem.BulletImpact;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;

namespace NemesisSystem;

public partial class NemesisMod
{
	private int _nextTheftCheck;

	private readonly Dictionary<int, (int driver, int at)> _vehicleDrivers = new Dictionary<int, (int, int)>();

	internal string TheftNote = "";

	private static readonly string[] FireModels = new string[4] { "p_campfire05x", "p_campfire01x", "p_campfire02x", "p_campfirecombined01x" };

	private static readonly string[] FireCrew = new string[6] { "WORLD_HUMAN_FIRE_SIT", "WORLD_HUMAN_FIRE_SIT", "WORLD_HUMAN_FIRE_SIT", "WORLD_HUMAN_FIRE_STAND", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_SMOKE" };

	private void UpdateTheft(Ped player)
	{
		if (Game.GameTime < _nextTheftCheck)
		{
			return;
		}
		_nextTheftCheck = Game.GameTime + 400;
		Vehicle[] allVehicles = World.GetAllVehicles();
		foreach (Vehicle vehicle in allVehicles)
		{
			if (!(vehicle == null) && vehicle.Exists() && !(vehicle.Position.DistanceTo(player.Position) > 10f))
			{
				int num = VEHICLE.GET_PED_IN_VEHICLE_SEAT(vehicle.Handle, -1);
				if (num != 0 && num != player.Handle)
				{
					_vehicleDrivers[vehicle.Handle] = (num, Game.GameTime);
				}
			}
		}
		int mount = PED.GET_MOUNT(player.Handle);
		if (mount != _lastMount)
		{
			_lastMount = mount;
			if (mount != 0 && mount != PLAYER._GET_ACTIVE_HORSE_FOR_PLAYER(Game.Player.Handle) && mount != PLAYER._GET_SADDLE_HORSE_FOR_PLAYER(Game.Player.Handle))
			{
				Ped ped = (from p in World.GetAllPeds()
					where p != null && p.Exists() && !p.IsPlayer && p.IsHuman && !p.IsDead && p.Position.DistanceTo(player.Position) < 40f && PED._GET_LAST_MOUNT(p.Handle) == mount
					orderby p.Position.DistanceTo(player.Position)
					select p).FirstOrDefault();
				TheftNote = ((ped != null) ? $"horse {mount}: owner {ped.Handle} -> grudge" : $"horse {mount}: no owner near");
				GrudgeLog("THEFT " + TheftNote);
				if (ped != null)
				{
					Provocation provocation = RememberStranger(ped.Handle);
					if (provocation != null)
					{
						if (!provocation.HorseStolen)
						{
							Grudge(ped.Handle, provocation, GrudgeAction.HorseStolen);
						}
						provocation.HorseStolen = true;
					}
				}
			}
		}
		int num2 = (PED.IS_PED_IN_ANY_VEHICLE(player.Handle, atGetIn: false) ? PED.GET_VEHICLE_PED_IS_IN(player.Handle, lastVehicle: false) : 0);
		if (num2 != _lastVehicle)
		{
			_lastVehicle = num2;
			if (num2 != 0 && VEHICLE.GET_PED_IN_VEHICLE_SEAT(num2, -1) == player.Handle && _vehicleDrivers.TryGetValue(num2, out (int, int) value) && Game.GameTime - value.Item2 < 8000)
			{
				Ped ped2 = Entity.FromHandle(value.Item1) as Ped;
				bool flag = ped2 != null && ped2.Exists() && !ped2.IsDead;
				TheftNote = string.Format("wagon {0}: driver {1} {2}", num2, value.Item1, flag ? "-> grudge" : "(dead)");
				GrudgeLog("THEFT " + TheftNote);
				if (flag)
				{
					Provocation provocation2 = RememberStranger(value.Item1);
					if (provocation2 != null)
					{
						if (!provocation2.WagonStolen)
						{
							Grudge(value.Item1, provocation2, GrudgeAction.WagonStolen);
						}
						provocation2.WagonStolen = true;
					}
				}
			}
		}
		foreach (int item in (from kv in _vehicleDrivers
			where Game.GameTime - kv.Value.at > 30000
			select kv.Key).ToList())
		{
			_vehicleDrivers.Remove(item);
		}
	}
}
