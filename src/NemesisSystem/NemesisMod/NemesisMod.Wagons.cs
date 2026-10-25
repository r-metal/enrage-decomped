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
	private const ulong GetDraftSpeedFn = 14328164591691327233uL;

	private const ulong IsDraftVehicleFn = 16880874006136878045uL;

	private const ulong DriveToDestinationFn = 9161476185454364035uL;

	private const int GtaDriveFlags = 786603;

	private const int RockstarDriveFlags = 524564;

	internal const int WagonTries = 5;

	internal const int RoadWagonTries = 3;

	private static int _wagonDriveWay;

	private static readonly string[] WagonWays = new string[3] { "the old order (GTA V's flags)", "R*'s driving flags", "R*'s drive-to-destination order" };

	internal void WagonDrive(Ped driver, Vehicle v, Vector3 to, float speed = 5f, int way = -1)
	{
		if (way < 0)
		{
			way = _wagonDriveWay;
		}
		if (way == 2)
		{
			Function.Call(9161476185454364035uL, driver.Handle, v.Handle, to.X, to.Y, to.Z, speed, 524564, 6, 8f, 8f, false);
		}
		else
		{
			TASK.TASK_VEHICLE_DRIVE_TO_COORD(driver.Handle, v.Handle, to.X, to.Y, to.Z, speed, 0uL, ENTITY.GET_ENTITY_MODEL(v.Handle), (way == 1) ? 524564 : 786603, 8f, 10f);
		}
	}

	internal void WagonWander(Ped driver, Vehicle v, float speed = 5f)
	{
		Function.Call(5188501456062479616uL, driver.Handle, v.Handle, speed, (_wagonDriveWay == 0) ? 786603 : 524564);
	}

	internal unsafe string WagonState(Vehicle v, Ped driver)
	{
		string text = "?";
		try
		{
			ulong* ptr = stackalloc ulong[2];
			*ptr = (ptr[1] = 0uL);
			if (Function.Call<bool>(11643009930546700959uL, new InputArgument[3]
			{
				v.Handle,
				new InputArgument((ulong)ptr),
				new InputArgument((ulong)(ptr + 1))
			}))
			{
				text = $"{(int)ptr[1]} of {(int)(*ptr)}";
			}
		}
		catch
		{
		}
		string text2 = "?";
		try
		{
			int num = VEHICLE.GET_PED_IN_VEHICLE_SEAT(v.Handle, -1);
			text2 = ((num == 0) ? "NOBODY" : ((driver != null && num == driver.Handle) ? "the driver" : "someone else"));
		}
		catch
		{
		}
		float num2 = -1f;
		try
		{
			num2 = Function.Call<float>(14328164591691327233uL, new InputArgument[1] { v.Handle });
		}
		catch
		{
		}
		bool flag = true;
		try
		{
			flag = Function.Call<bool>(16880874006136878045uL, new InputArgument[1] { v.Handle });
		}
		catch
		{
		}
		return string.Format("speed {0:0.0} m/s, horses in harness {1}, reins: {2}, team speed {3:0.0}{4}, driven with {5}", ENTITY.GET_ENTITY_SPEED(v.Handle), text, text2, num2, flag ? "" : ", NOT a draft vehicle", WagonWays[_wagonDriveWay]);
	}

	internal string WagonTry(int n, Ped driver, Vehicle v, ref Vector3 to, ref bool wandering)
	{
		switch (n)
		{
		case 1:
			if (wandering)
			{
				WagonWander(driver, v);
				return "the wander again";
			}
			WagonDrive(driver, v, to);
			return "the order again";
		case 2:
			WagonWander(driver, v);
			wandering = true;
			return "driving on wherever the road goes (no goal)";
		case 3:
			Function.Call(882441092585915516uL, v.Handle, 5f);
			if (wandering)
			{
				WagonWander(driver, v);
			}
			else
			{
				WagonDrive(driver, v, to);
			}
			return "the team's own speed set, the order again";
		case 4:
		{
			float heading;
			Vector3? vector = RoadNodeNear(to, 120f, out heading, 0);
			string text = (vector.HasValue ? $"a main road {vector.Value.DistanceTo(to):0} m from where it was going" : "no main road near - the same place");
			if (vector.HasValue)
			{
				to = vector.Value;
			}
			WagonDrive(driver, v, to, 5f, 1);
			return "R*'s driving flags, to " + text;
		}
		case 5:
			WagonDrive(driver, v, to, 5f, 2);
			return "R*'s own drive-to-destination order";
		default:
			return null;
		}
	}

	internal Vector3? WagonGoal(Vector3 at, float reach)
	{
		float heading;
		return RoadNodeNear(at, reach, out heading, 0);
	}

	internal void WagonRolled(string who, int n, string what)
	{
		GrudgeLog($"WAGONDIAG {who}: rolling again after try {n} - {what}");
		int num = n switch
		{
			5 => 2, 
			4 => 1, 
			_ => -1, 
		};
		if (num > 0 && num != _wagonDriveWay)
		{
			_wagonDriveWay = num;
			GrudgeLog("WAGONDIAG the mod's wagons drive with " + WagonWays[num] + " from now on (this session)");
		}
	}
}
