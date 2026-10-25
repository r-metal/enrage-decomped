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
	private sealed class DoorWas
	{
		public uint Hash;

		public bool WeAdded;

		public int State;
	}

	private static List<DoorRow> _doors = new List<DoorRow>();

	private readonly Dictionary<string, List<DoorWas>> _campDoors = new Dictionary<string, List<DoorWas>>();

	private int _nextDoorRefresh;

	private readonly Dictionary<string, List<(Vector3 pos, float heading, int pt)>> _hideoutPoints = new Dictionary<string, List<(Vector3, float, int)>>();

	private readonly Dictionary<string, Vector3> _hideoutIndoorProbe = new Dictionary<string, Vector3>();

	private bool _indoorOnly;

	private bool _forceIndoor;

	internal static IEnumerable<DoorRow> AllDoors => _doors;

	private static DoorsContent BuiltInDoors()
	{
		try
		{
			using Stream stream = typeof(NemesisMod).Assembly.GetManifestResourceStream("doors.json");
			using StreamReader streamReader = new StreamReader(stream);
			return JsonConvert.DeserializeObject<DoorsContent>(streamReader.ReadToEnd()) ?? new DoorsContent();
		}
		catch
		{
			return new DoorsContent();
		}
	}

	private void LoadDoors()
	{
		int num = LoadOrWrite(Path.Combine(ContentDir, "doors.json"), BuiltInDoors, delegate(DoorsContent c)
		{
			if (c?.Doors == null || c.Doors.Count == 0)
			{
				return 0;
			}
			_doors = c.Doors.Where((DoorRow d) => d.Hash != 0).ToList();
			return _doors.Count;
		});
		GrudgeLog($"CONTENT doors.json: {num} doors");
	}

	private void UnlockHideout(CampSite site)
	{
		if (site == null || !site.Hideout || site.Imap != 0 || _campDoors.ContainsKey(site.Id) || MISC.GET_MISSION_FLAG())
		{
			return;
		}
		List<DoorWas> list = new List<DoorWas>();
		foreach (DoorRow door in _doors)
		{
			float num = door.X - site.X;
			float num2 = door.Y - site.Y;
			if ((float)Math.Sqrt(num * num + num2 * num2) > ((door.Model != null && door.Model.IndexOf("gate", StringComparison.OrdinalIgnoreCase) >= 0) ? 90f : 45f))
			{
				continue;
			}
			try
			{
				DoorWas doorWas = new DoorWas
				{
					Hash = door.Hash
				};
				if (!Function.Call<bool>(13930693845672184001uL, new InputArgument[1] { door.Hash }))
				{
					Crumbs.Native("CampDoors door add");
					Function.Call(15677639426206622342uL, door.Hash, true, true, false, 0, 0, false);
					doorWas.WeAdded = true;
				}
				else
				{
					doorWas.State = Function.Call<int>(1588259609567639992uL, new InputArgument[1] { door.Hash });
					Crumbs.Native("CampDoors door owner");
					Function.Call(10977356922902395972uL, door.Hash);
				}
				Crumbs.Native("CampDoors door unlock");
				Function.Call(7758457796463198035uL, door.Hash, 0);
				list.Add(doorWas);
			}
			catch (Exception ex)
			{
				GrudgeLog($"CAMPDOORS {site.Label}: door {door.Hash} failed - {ex.Message}");
			}
		}
		_campDoors[site.Id] = list;
		if (list.Count > 0)
		{
			GrudgeLog($"CAMPDOORS {site.Label}: {list.Count} doors / gates unlocked ({list.Count((DoorWas x) => x.WeAdded)} registered by us)");
		}
	}

	private void RefreshHideoutDoors(Ped player)
	{
		if (Game.GameTime < _nextDoorRefresh || _campDoors.Count == 0)
		{
			return;
		}
		_nextDoorRefresh = Game.GameTime + 3000;
		if (MISC.GET_MISSION_FLAG())
		{
			return;
		}
		foreach (KeyValuePair<string, List<DoorWas>> campDoor in _campDoors)
		{
			CampSite campSite = Camps.ById(campDoor.Key);
			if (campSite == null || new Vector3(campSite.X - player.Position.X, campSite.Y - player.Position.Y, 0f).Length() > 120f)
			{
				continue;
			}
			int num = 0;
			foreach (DoorWas item in campDoor.Value)
			{
				try
				{
					if (Function.Call<int>(1588259609567639992uL, new InputArgument[1] { item.Hash }) != 0)
					{
						Crumbs.Native("CampDoors door re-unlock");
						Function.Call(7758457796463198035uL, item.Hash, 0);
						num++;
					}
				}
				catch
				{
				}
			}
			if (num > 0)
			{
				GrudgeLog($"CAMPDOORS {campSite.Label}: {num} doors had locked again - unlocked");
			}
		}
	}

	private void RelockHideout(CampSite site, bool missionStart = false)
	{
		if (site == null || !_campDoors.TryGetValue(site.Id, out var value))
		{
			return;
		}
		_campDoors.Remove(site.Id);
		bool flag = false;
		try
		{
			flag = CAM.IS_SCREEN_FADED_OUT() || (MISC.GET_MISSION_FLAG() && !missionStart);
		}
		catch
		{
		}
		if (flag)
		{
			return;
		}
		foreach (DoorWas item in value)
		{
			try
			{
				Crumbs.Native("CampDoors door restore");
				if (item.WeAdded)
				{
					Function.Call(5065861373067882468uL, item.Hash);
				}
				else
				{
					Function.Call(7758457796463198035uL, item.Hash, item.State);
				}
			}
			catch
			{
			}
		}
		GrudgeLog($"CAMPDOORS {site.Label}: {value.Count} doors put back");
	}

	internal void RelockAllHideouts()
	{
		RelockAllHideouts(missionStart: false);
	}

	internal void RelockAllHideouts(bool missionStart)
	{
		foreach (string item in _campDoors.Keys.ToList())
		{
			RelockHideout(Camps.ById(item), missionStart);
		}
	}

	private unsafe void FindHideoutPoints(CampSite site)
	{
		List<(Vector3, float, int)> list = new List<(Vector3, float, int)>();
		_hideoutPoints[site.Id] = list;
		ulong* ptr = stackalloc ulong[272];
		for (int i = 0; i < 272; i++)
		{
			ptr[i] = 0uL;
		}
		*ptr = 256uL;
		Crumbs.Native("CampDoors scenario points");
		int num = TASK.GET_SCENARIO_POINTS_IN_AREA(site.X, site.Y, site.Z, _indoorOnly ? 55f : 85f, ptr, 256);
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		_hideoutIndoorProbe.Remove(site.Id);
		HashSet<int> hashSet = new HashSet<int>();
		for (int j = 1; j <= Math.Min(num + 1, 256); j++)
		{
			int num5 = (int)(ptr[j] & 0xFFFFFFFFu);
			if (num5 == 0 || !hashSet.Add(num5) || !TASK.DOES_SCENARIO_POINT_EXIST(num5))
			{
				continue;
			}
			if (TASK._GET_PED_USING_SCENARIO_POINT(num5) != 0)
			{
				num4++;
				continue;
			}
			Vector3 c = TASK._GET_SCENARIO_POINT_COORDS(num5, p1: true);
			int num6 = INTERIOR.GET_INTERIOR_AT_COORDS(c.X, c.Y, c.Z);
			if (num6 != 0)
			{
				if (!INTERIOR.IS_INTERIOR_READY(num6))
				{
					num3++;
					if (!_hideoutIndoorProbe.ContainsKey(site.Id))
					{
						_hideoutIndoorProbe[site.Id] = c;
					}
					continue;
				}
				_ = _indoorOnly;
				num2++;
			}
			if ((!_indoorOnly || (num6 != 0 && Enclosed(c))) && !list.Any(((Vector3, float, int) x) => x.Item1.DistanceTo(c) < 3f))
			{
				list.Add((c, TASK._GET_SCENARIO_POINT_HEADING(num5, p1: true), num5));
			}
		}
		List<(Vector3, float, int)> list2 = list.ToList();
		List<(Vector3, float, int)> order = new List<(Vector3, float, int)>();
		if (list2.Count > 0)
		{
			int index = _rng.Next(list2.Count);
			order.Add(list2[index]);
			list2.RemoveAt(index);
		}
		while (list2.Count > 0)
		{
			List<(int, float)> list3 = (from t in list2.Select(((Vector3, float, int) x, int k) => (k: k, d: order.Min(((Vector3, float, int) o) => o.Item1.DistanceTo(x.Item1))))
				orderby t.d descending
				select t).Take(3).ToList();
			int item = list3[_rng.Next(list3.Count)].Item1;
			order.Add(list2[item]);
			list2.RemoveAt(item);
		}
		list.Clear();
		list.AddRange(order);
		GrudgeLog($"CAMP {site.Label}: {list.Count} places for its men ({num2} inside, {num3} inside but not loaded yet, {num4} in use) of {num} points");
	}

	internal int HoldBackForIndoors(CampSite site, int hired)
	{
		if (!_hideoutIndoorProbe.ContainsKey(site.Id))
		{
			return 0;
		}
		return Math.Min(hired, Math.Max(2, (hired + 1) / 2));
	}

	private void CheckHideoutIndoors(LiveCamp lc)
	{
		try
		{
			RefreshHideoutDoors(Game.Player.Character);
		}
		catch
		{
		}
		if (lc.IndoorPending <= 0 || lc.Engaged || !_hideoutIndoorProbe.TryGetValue(lc.Site.Id, out var value))
		{
			return;
		}
		int num = INTERIOR.GET_INTERIOR_AT_COORDS(value.X, value.Y, value.Z);
		if (num == 0 || !INTERIOR.IS_INTERIOR_READY(num))
		{
			return;
		}
		_indoorOnly = true;
		try
		{
			FindHideoutPoints(lc.Site);
		}
		finally
		{
			_indoorOnly = false;
		}
		int indoorPending = lc.IndoorPending;
		lc.IndoorPending = 0;
		int num2 = 0;
		int num3 = ((indoorPending >= 3) ? (1 + _rng.Next(2)) : ((indoorPending == 2) ? 1 : 0));
		int num4 = 0;
		for (int i = 0; i < indoorPending; i++)
		{
			if (!_hideoutPoints.TryGetValue(lc.Site.Id, out List<(Vector3, float, int)> value2) || value2.Count == 0)
			{
				break;
			}
			_forceIndoor = true;
			int count = lc.Peds.Count;
			try
			{
				SpawnCampPed(lc, lc.Boss, SitePos(lc.Site), lc.Peds.Count, named: false);
			}
			finally
			{
				_forceIndoor = false;
			}
			bool num5 = lc.Peds.Count > count;
			if (num5)
			{
				num2++;
			}
			if (!num5 || num4 >= num3)
			{
				continue;
			}
			Ped w = lc.Peds[lc.Peds.Count - 1];
			if ((object)w != null && w.Exists())
			{
				_seating.RemoveAll((Seating s) => s.Ped != null && s.Ped.Handle == w.Handle);
				Vector3 position = w.Position;
				TASK.CLEAR_PED_TASKS(w.Handle, p1: true, p2: true);
				Function.Call(16164602603745899285uL, w.Handle, position.X, position.Y, position.Z, 6f, 2f, 8f, 0);
				num4++;
			}
		}
		GrudgeLog($"CAMP {lc.Site.Label}: the house is loaded - {num2} of his men inside ({num4} walking the rooms)");
	}

	private static bool Enclosed(Vector3 c)
	{
		Vector3 vector = c + new Vector3(0f, 0f, 1.2f);
		if (!Ray.Blocked(vector, vector + new Vector3(0f, 0f, 5f), 17, 0))
		{
			return false;
		}
		int num = 0;
		Vector3[] array = new Vector3[4]
		{
			new Vector3(8f, 0f, 0f),
			new Vector3(-8f, 0f, 0f),
			new Vector3(0f, 8f, 0f),
			new Vector3(0f, -8f, 0f)
		};
		foreach (Vector3 vector2 in array)
		{
			if (Ray.Blocked(vector, vector + vector2, 17, 0))
			{
				num++;
			}
		}
		return num >= 3;
	}

	private (Vector3 pos, float heading, int pt)? NextHideoutPlace(CampSite site)
	{
		if (!_hideoutPoints.TryGetValue(site.Id, out List<(Vector3, float, int)> value) || value.Count == 0)
		{
			return null;
		}
		(Vector3, float, int) value2 = value[0];
		value.RemoveAt(0);
		return value2;
	}
}
