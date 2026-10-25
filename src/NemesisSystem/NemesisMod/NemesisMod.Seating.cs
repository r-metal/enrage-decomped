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
	private sealed class Seating
	{
		public Ped Ped;

		public int Point;

		public Vector3 Seat;

		public Vector3 Stand;

		public float Heading;

		public int Stage;

		public int CheckAt;
	}

	private readonly Dictionary<int, (bool blocked, int at)> _cardBlocked = new Dictionary<int, (bool, int)>();

	internal string SeatNote = "";

	private readonly List<Seating> _seating = new List<Seating>();

	internal int SeatTried;

	internal int SeatOnPoint;

	internal int SeatNear;

	internal int SeatWide;

	internal int SeatFailed;

	private uint _donorPersonality;

	private readonly Dictionary<int, (float alpha, float x, float y, int seenFrame)> _cards = new Dictionary<int, (float, float, float, int)>();

	private unsafe bool TrySpawnRegular(Ped player, string town, bool force = false)
	{
		if (!Config.RememberPeople || town == null || _regulars.Count((Ped r) => r.Exists()) >= Config.RegularsMax)
		{
			return false;
		}
		List<Acquaintance> here = _acqOf.Values.ToList();
		List<Acquaintance> list = People.Where((Acquaintance a) => !a.Dead && a.Town == town && !here.Contains(a) && (force || (DateTime.Now - a.LastSpawned).TotalMinutes > 20.0)).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		Acquaintance acquaintance = list[_rng.Next(list.Count)];
		Model m = new Model(acquaintance.Model);
		if (!m.IsValid || !m.IsPed)
		{
			acquaintance.Dead = true;
			_peopleDirty = true;
			return false;
		}
		if (!LoadModelSafe(m, 2000))
		{
			return false;
		}
		(Vector3, float, int)? tuple = FindTownSeat(player, town);
		Vector3 vector = (tuple.HasValue ? StandIn(tuple.Value.Item1, tuple.Value.Item2) : NaturalTownSpot(player, town).pos);
		if (vector == Vector3.Zero)
		{
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
			return false;
		}
		GrudgeLog("PEOPLE spot for " + acquaintance.Name + ": " + SpotNote(vector, town, player));
		Ped ped = SpawnPed((PedHash)m.Hash, vector, tuple.HasValue ? tuple.Value.Item2 : ((float)_rng.Next(360)));
		STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
		if (ped == null)
		{
			return false;
		}
		MakeOurs(ped.Handle);
		if (tuple.HasValue)
		{
			SitAt(ped, tuple.Value.Item3, tuple.Value.Item1, tuple.Value.Item2);
		}
		else
		{
			PlaceNaturally(ped, vector);
		}
		MakeSociable(ped);
		if (Config.RegularsAmbient)
		{
			int handle = ped.Handle;
			ReleaseIfOurs(&handle);
		}
		_named[ped.Handle] = new Named
		{
			Name = acquaintance.Name,
			Title = acquaintance.Title,
			Met = acquaintance.Met,
			Importance = 3,
			SeenAt = Game.GameTime,
			NamedAt = Game.GameTime
		};
		_acqOf[ped.Handle] = acquaintance;
		if (acquaintance.Grudge > 0)
		{
			RestoreGrudge(ped.Handle, acquaintance);
		}
		acquaintance.LastSpawned = DateTime.Now;
		acquaintance.TimesSeen++;
		_peopleDirty = true;
		_regulars.Add(ped);
		GrudgeLog(string.Format("PEOPLE {0} turns up in {1} ({2}, grudge {3})", acquaintance.Name, town, tuple.HasValue ? "on a seat" : "in the street", acquaintance.Grudge));
		TownHappening(acquaintance.Name + " turned up in " + town);
		Feed("TOWN " + acquaintance.Name + " is around " + town + (acquaintance.Met ? "" : " (you never got his name)") + ((acquaintance.Grudge > 0) ? $" - still sore: grudge {acquaintance.Grudge}" : ""), Theme.Dim);
		return true;
	}

	internal float TownSpread(string town)
	{
		if (town == null || !TownCore(town, out var _, out var radius))
		{
			return Balance.TownSpreadMeters;
		}
		return Balance.TownSpreadMeters * Math.Max(1f, radius / 130f);
	}

	internal (Vector3 pos, float heading, int point)? FindTownSeat(Ped player, string town, float minD = 12f)
	{
		if (town == null || !TownCore(town, out var centre, out var radius))
		{
			return FindSeat(player, minD, 110f, town);
		}
		float spread = TownSpread(town);
		Vector3? townToward = TownToward;
		if (townToward.HasValue)
		{
			Vector3 valueOrDefault = townToward.GetValueOrDefault();
			if (new Vector3(valueOrDefault.X - centre.X, valueOrDefault.Y - centre.Y, 0f).Length() < radius)
			{
				(Vector3, float, int)? result = FindSeat(player, minD, 220f, town, centre, radius, valueOrDefault, 70f, spread);
				if (result.HasValue)
				{
					return result;
				}
			}
		}
		if (radius > 150f)
		{
			Vector3 position = player.Position;
			for (int i = 0; i < 3; i++)
			{
				Vector3 vector = Vector3.Zero;
				for (int j = 0; j < 10; j++)
				{
					if (!(vector == Vector3.Zero))
					{
						break;
					}
					double num = _rng.NextDouble() * Math.PI * 2.0;
					double num2 = (double)radius * 0.75 * Math.Sqrt(_rng.NextDouble());
					Vector3 vector2 = centre + new Vector3((float)(Math.Cos(num) * num2), (float)(Math.Sin(num) * num2), 0f);
					if (vector2.DistanceTo2D(position) < 200f)
					{
						vector = vector2;
					}
				}
				if (vector == Vector3.Zero)
				{
					break;
				}
				(Vector3, float, int)? result2 = FindSeat(player, minD, 220f, town, centre, radius, vector, 90f, spread);
				if (result2.HasValue)
				{
					return result2;
				}
			}
		}
		return FindSeat(player, minD, 220f, town, centre, radius, null, 0f, spread);
	}

	internal unsafe (Vector3 pos, float heading, int point)? FindSeat(Ped player, float minD, float maxD, string town = null, Vector3? centre = null, float coreRadius = 0f, Vector3? searchAt = null, float searchRadius = 0f, float spread = 0f)
	{
		ulong* ptr = stackalloc ulong[272];
		for (int i = 0; i < 272; i++)
		{
			ptr[i] = 0uL;
		}
		*ptr = 256uL;
		Vector3 position = player.Position;
		Vector3 vector = searchAt ?? centre ?? position;
		Crumbs.Native("People scenario points");
		int num = TASK.GET_SCENARIO_POINTS_IN_AREA(vector.X, vector.Y, vector.Z, (searchRadius > 0f) ? searchRadius : (centre.HasValue ? coreRadius : maxD), ptr, 256);
		int num2 = INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle);
		List<(Vector3, float, int)> list = new List<(Vector3, float, int)>();
		HashSet<int> hashSet = new HashSet<int>();
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = (centre.HasValue ? 999 : 6);
		int saloonInterior = 0;
		if (centre.HasValue && town != null && TownSpots.TryGetValue(town, out (Vector3?, Vector3?) value))
		{
			Vector3? item = value.Item2;
			if (item.HasValue)
			{
				Vector3 valueOrDefault = item.GetValueOrDefault();
				if (valueOrDefault.DistanceTo(position) < 110f)
				{
					saloonInterior = INTERIOR.GET_INTERIOR_AT_COORDS(valueOrDefault.X, valueOrDefault.Y, valueOrDefault.Z);
				}
			}
		}
		for (int j = 1; j <= Math.Min(num + 1, 256); j++)
		{
			if (list.Count >= num9)
			{
				break;
			}
			int num10 = (int)(ptr[j] & 0xFFFFFFFFu);
			if (num10 == 0 || !hashSet.Add(num10) || !TASK.DOES_SCENARIO_POINT_EXIST(num10))
			{
				continue;
			}
			num3++;
			if (TASK._GET_PED_USING_SCENARIO_POINT(num10) != 0)
			{
				num4++;
				continue;
			}
			Vector3 vector2 = TASK._GET_SCENARIO_POINT_COORDS(num10, p1: true);
			float num11 = vector2.DistanceTo(position);
			if (num11 < minD || num11 > maxD)
			{
				num8++;
				continue;
			}
			if (_seatTaken.TryGetValue(num10, out var value2) && Game.GameTime - value2 < 120000)
			{
				num4++;
				continue;
			}
			if (centre.HasValue && vector2.DistanceTo2D(centre.Value) > coreRadius)
			{
				num7++;
				continue;
			}
			if (town != null && TownAt(vector2) != town)
			{
				num7++;
				continue;
			}
			int num12 = INTERIOR.GET_INTERIOR_AT_COORDS(vector2.X, vector2.Y, vector2.Z);
			if (num12 != 0 && num12 != num2 && num12 != saloonInterior)
			{
				num5++;
				continue;
			}
			bool flag = saloonInterior != 0 && num12 == saloonInterior;
			if (OurPeopleNear(vector2, (!centre.HasValue) ? 6f : (flag ? 3f : ((spread > 0f) ? spread : Balance.TownSpreadMeters))))
			{
				num4++;
			}
			else if (centre.HasValue)
			{
				list.Add((vector2, TASK._GET_SCENARIO_POINT_HEADING(num10, p1: true), num10));
			}
			else if (!HiddenFromView(player, vector2))
			{
				num6++;
			}
			else
			{
				list.Add((vector2, TASK._GET_SCENARIO_POINT_HEADING(num10, p1: true), num10));
			}
		}
		string text = "";
		List<Vector3> folk;
		if (centre.HasValue)
		{
			folk = (from p in PedsAround(player)
				where p != null && p.Exists() && !p.IsDead && p.Handle != player.Handle && PED.IS_PED_HUMAN(p.Handle) && !IsOurs(p.Handle) && p.Position.DistanceTo2D(centre.Value) < coreRadius * 1.2f
				select p.Position).ToList();
			List<((Vector3, float, int), double, int, bool)> list2 = list.Select(delegate((Vector3, float, int) f)
			{
				int near;
				bool saloon;
				double item2 = Weight(f, out near, out saloon);
				return (f: f, w: item2, nr: near, sl: saloon);
			}).ToList();
			int count = list2.Count;
			int num13 = list2.Count<((Vector3, float, int), double, int, bool)>((((Vector3, float, int) f, double w, int nr, bool sl) x) => x.nr > 0);
			int num14 = list2.Count<((Vector3, float, int), double, int, bool)>((((Vector3, float, int) f, double w, int nr, bool sl) x) => x.sl);
			int num15 = 0;
			int num16 = 0;
			list = new List<(Vector3, float, int)>();
			for (int num17 = 0; num17 < 20; num17++)
			{
				if (list2.Count <= 0)
				{
					break;
				}
				double num18 = _rng.NextDouble() * list2.Sum<((Vector3, float, int), double, int, bool)>((((Vector3, float, int) f, double w, int nr, bool sl) x) => x.w);
				int num19;
				for (num19 = 0; num19 < list2.Count - 1; num19++)
				{
					num18 -= list2[num19].Item2;
					if (num18 <= 0.0)
					{
						break;
					}
				}
				((Vector3, float, int), double, int, bool) tuple = list2[num19];
				list2.RemoveAt(num19);
				if (tuple.Item3 == 0 && !tuple.Item4 && num13 >= 1)
				{
					continue;
				}
				float num20 = tuple.Item1.Item1.DistanceTo2D(centre.Value) / coreRadius;
				if (!tuple.Item4 && (num20 > 0.9f || (num20 > 0.75f && tuple.Item3 < 2)))
				{
					num15++;
					continue;
				}
				if (!HiddenFromView(player, tuple.Item1.Item1))
				{
					num6++;
					continue;
				}
				if (!tuple.Item4 && !Walkable(tuple.Item1.Item1))
				{
					num16++;
					continue;
				}
				list.Add(tuple.Item1);
				text = string.Format(" | picked: {0} townsfolk within 20 m{1}, weight {2:0.0}", tuple.Item3, tuple.Item4 ? ", IN THE SALOON" : "", tuple.Item2);
				break;
			}
			SeatNoteExtra = $", {count} candidates in the core ({num13} near townsfolk, {num14} in the saloon; {folk.Count} townsfolk about; {num15} at the edge skipped, {num16} off the walkways)";
		}
		else
		{
			SeatNoteExtra = "";
		}
		SeatNote = string.Format("{0}{1} returned, {2} points, {3} too near/far, {4} in use, {5} outside town, {6} in other buildings, {7} in view, {8} usable{9}", searchAt.HasValue ? $"district {searchAt.Value.X:0},{searchAt.Value.Y:0} ({searchRadius:0} m), spread {spread:0} m - " : "", num, num3, num8, num4, num7, num5, num6, list.Count, SeatNoteExtra);
		if (list.Count == 0)
		{
			return null;
		}
		(Vector3, float, int) tuple2 = list[_rng.Next(list.Count)];
		_seatTaken[tuple2.Item3] = Game.GameTime;
		foreach (int item3 in (from kv in _seatTaken
			where Game.GameTime - kv.Value > 300000
			select kv.Key).ToList())
		{
			_seatTaken.Remove(item3);
		}
		GrudgeLog(string.Format("SEAT {0} at {1:0},{2:0} ({3:0} m from you{4}) - {5}{6}", tuple2.Item3, tuple2.Item1.X, tuple2.Item1.Y, tuple2.Item1.DistanceTo(position), centre.HasValue ? $", {tuple2.Item1.DistanceTo2D(centre.Value):0}/{coreRadius:0} m from {town} centre" : ((town != null) ? (", zone " + town) : ", wild"), SeatNote, text));
		return (tuple2.Item1, tuple2.Item2, tuple2.Item3);
		double Weight((Vector3, float, int) f, out int near, out bool saloon)
		{
			Vector3 c = f.Item1;
			near = folk.Count((Vector3 q) => q.DistanceTo(c) < 20f);
			saloon = saloonInterior != 0 && INTERIOR.GET_INTERIOR_AT_COORDS(c.X, c.Y, c.Z) == saloonInterior;
			double num21 = ((near == 0) ? 0.05 : Math.Pow(Math.Min(near, 8), 1.5));
			if (saloon)
			{
				num21 += 10.0;
			}
			float num22 = c.DistanceTo2D(centre.Value);
			return num21 * ((num22 < coreRadius * 0.5f) ? 1.0 : ((num22 < coreRadius * 0.75f) ? 0.45 : 0.12));
		}
	}

	private bool OurPeopleNear(Vector3 c, float r)
	{
		foreach (var item in OurPeds())
		{
			var (ped, _) = item;
			if (item.role != OurRole.Aftermath && ped != null && ped.Exists() && !ped.IsDead && ped.Position.DistanceTo(c) < r)
			{
				return true;
			}
		}
		return false;
	}

	private bool CardOccluded(Vector3 point)
	{
		Crumbs.Native("NameCards occlusion ray");
		return Ray.Blocked(CAM.GET_FINAL_RENDERED_CAM_COORD(), point, 17, Game.Player.Character.Handle);
	}

	internal static bool Walkable(Vector3 c)
	{
		try
		{
			Vector3 safeCoordForPed = World.GetSafeCoordForPed(c, onGround: true, 0);
			if (safeCoordForPed == Vector3.Zero)
			{
				return false;
			}
			return safeCoordForPed.DistanceTo2D(c) < 2.5f && Math.Abs(safeCoordForPed.Z - c.Z) < 1.5f;
		}
		catch
		{
			return true;
		}
	}

	internal bool HiddenFromView(Ped player, Vector3 spot)
	{
		Vector3 vector = spot + new Vector3(0f, 0f, 1.2f);
		if (!Projection.ToScreen(vector, out var x, out var y) || x < -0.02f || x > 1.02f || y < -0.02f || y > 1.02f)
		{
			return true;
		}
		return Ray.Blocked(CAM.GET_FINAL_RENDERED_CAM_COORD(), vector, 1, player.Handle);
	}

	internal unsafe Vector3 StandIn(Vector3 seat, float heading)
	{
		double num = (double)heading * Math.PI / 180.0;
		Vector3 vector = seat + new Vector3((0f - (float)Math.Sin(num)) * 1.1f, (float)Math.Cos(num) * 1.1f, 0f);
		Vector3 vector2 = new Vector3(0f, 0f, 0.6f);
		if (Ray.Blocked(seat + vector2, vector + vector2, 17, 0))
		{
			return seat;
		}
		float num2 = default(float);
		if (MISC.GET_GROUND_Z_FOR_3D_COORD(vector.X, vector.Y, seat.Z + 1.2f, &num2, p4: false) && Math.Abs(num2 - seat.Z) < 0.8f)
		{
			vector.Z = num2;
			return vector;
		}
		return seat;
	}

	private void SitAt(Ped ped, int point, Vector3 seat, float heading)
	{
		SeatTried++;
		Crumbs.Native("seat task: SitAt point");
		NoteSeatTask();
		TASK.TASK_USE_SCENARIO_POINT(ped.Handle, point, "", -1, p4: true, p5: false, 0u, p7: false, -1f, p9: true);
		_seating.Add(new Seating
		{
			Ped = ped,
			Point = point,
			Seat = seat,
			Stand = ped.Position,
			Heading = heading,
			Stage = 0,
			CheckAt = Game.GameTime + 1500
		});
		NaturalNote = "on one of the town's seats";
	}

	private void UpdateSeating()
	{
		for (int num = _seating.Count - 1; num >= 0; num--)
		{
			Seating seating = _seating[num];
			Ped ped = seating.Ped;
			if (ped == null || !ped.Exists() || ped.IsDead || PED.IS_PED_IN_COMBAT(ped.Handle, Game.Player.Character.Handle))
			{
				_seating.RemoveAt(num);
			}
			else
			{
				Named value;
				NemesisRecord value2;
				string text = (_named.TryGetValue(ped.Handle, out value) ? value.Name : (_worldNemeses.TryGetValue(ped.Handle, out value2) ? value2.Name : $"ped {ped.Handle}"));
				if (PED.IS_PED_USING_ANY_SCENARIO(ped.Handle))
				{
					string text2 = ((seating.Stage == 0) ? "sat on the point" : ((seating.Stage == 1) ? "took the nearest seat (2.5 m)" : "took a seat within 10 m"));
					if (seating.Stage == 0)
					{
						SeatOnPoint++;
					}
					else if (seating.Stage == 1)
					{
						SeatNear++;
					}
					else
					{
						SeatWide++;
					}
					GrudgeLog($"SEAT {text}: {text2} at {ped.Position.X:0},{ped.Position.Y:0},{ped.Position.Z:0}");
					_seating.RemoveAt(num);
				}
				else if (Game.GameTime >= seating.CheckAt)
				{
					if (seating.Stage == 0)
					{
						Crumbs.Native("seat task: seating nearest 2.5 m");
						NoteSeatTask();
						TASK.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP(ped.Handle, seating.Seat.X, seating.Seat.Y, seating.Seat.Z, 2.5f, -1, p6: true, p7: false, p8: false, p9: false);
						seating.Stage = 1;
						seating.CheckAt = Game.GameTime + 1200;
					}
					else if (seating.Stage == 1)
					{
						Crumbs.Native("seat task: seating nearest 10 m");
						NoteSeatTask();
						TASK.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP(ped.Handle, seating.Seat.X, seating.Seat.Y, seating.Seat.Z, 10f, -1, p6: true, p7: false, p8: false, p9: false);
						seating.Stage = 2;
						seating.CheckAt = Game.GameTime + 1200;
					}
					else
					{
						SeatFailed++;
						if (ped.Position.DistanceTo(seating.Stand) > 0.8f || Math.Abs(ped.Position.Z - seating.Stand.Z) > 0.5f)
						{
							ENTITY.SET_ENTITY_COORDS(ped.Handle, seating.Stand.X, seating.Stand.Y, seating.Stand.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
						}
						string text3 = TownIdle[_rng.Next(TownIdle.Length)];
						float face;
						string note;
						Vector3 vector = OpenSpot(seating.Stand, seating.Heading, ped.Handle, out face, out note);
						if (vector.DistanceTo(ped.Position) > 0.3f)
						{
							ENTITY.SET_ENTITY_COORDS(ped.Handle, vector.X, vector.Y, vector.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
						}
						ENTITY.SET_ENTITY_HEADING(ped.Handle, face);
						GrudgeLog("SEAT " + text + ": standing - " + note);
						TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(text3), -1, playEnterAnim: true, 0u, face, p6: false);
						GrudgeLog(string.Format("SEAT {0}: no seat took (point {1}) - standing in front of it, {2}", text, seating.Point, text3.Replace("WORLD_HUMAN_", "").ToLowerInvariant()));
						_seating.RemoveAt(num);
					}
				}
			}
		}
	}

	private unsafe Vector3 OpenSpot(Vector3 at, float preferred, int self, out float face, out string note)
	{
		float num = preferred;
		float num2 = -1f;
		for (int i = 0; i < 8; i++)
		{
			float num3 = preferred + 45f * (float)i;
			float num4 = Room(at, num3);
			if (num4 > num2 + 0.2f)
			{
				num = num3;
				num2 = num4;
			}
		}
		Vector3 vector = at;
		note = $"room {num2:0.0} m";
		if (num2 < 2.5f)
		{
			double num5 = (double)num * Math.PI / 180.0;
			Vector3 vector2 = new Vector3(0f - (float)Math.Sin(num5), (float)Math.Cos(num5), 0f) * Math.Min(1.2f, Math.Max(0.3f, num2 - 0.6f));
			Vector3 vector3 = at + vector2;
			float num6 = default(float);
			if (MISC.GET_GROUND_Z_FOR_3D_COORD(vector3.X, vector3.Y, at.Z + 1.2f, &num6, p4: false) && Math.Abs(num6 - at.Z) < 1f)
			{
				vector3.Z = num6;
				vector = vector3;
				note += $", cramped - stepped {vector2.Length():0.0} m out";
			}
		}
		if (Room(vector, preferred) >= 3f)
		{
			face = preferred;
			note += ", faces the seat's way";
			return vector;
		}
		face = num;
		float num7 = -1f;
		for (int j = 0; j < 8; j++)
		{
			float num8 = preferred + 45f * (float)j;
			float num9 = Room(vector, num8);
			if (num9 > num7 + 0.2f)
			{
				face = num8;
				num7 = num9;
			}
		}
		note += $", faces the open side ({num7:0.0} m)";
		return vector;
		float Room(Vector3 from0, float h)
		{
			double num10 = (double)h * Math.PI / 180.0;
			Vector3 vector4 = new Vector3(0f - (float)Math.Sin(num10), (float)Math.Cos(num10), 0f);
			Vector3 vector5 = from0 + new Vector3(0f, 0f, 1f) + vector4 * 0.3f;
			if (!Ray.Hit(vector5, vector5 + vector4 * 4f, 17, self, out var at2))
			{
				return 4.3f;
			}
			return at2.DistanceTo(vector5) + 0.3f;
		}
	}

	internal string SeatingDebugLine()
	{
		if (SeatTried != 0)
		{
			return string.Format("seating: {0}/{1} sat ({2} on the point, {3} nearest, {4} within 10 m), {5} standing in front{6}", SeatOnPoint + SeatNear + SeatWide, SeatTried, SeatOnPoint, SeatNear, SeatWide, SeatFailed, (_seating.Count > 0) ? $", {_seating.Count} still going" : "");
		}
		return "seating: none yet";
	}

	internal void MakeSociable(Ped ped)
	{
		if (ped == null || !ped.Exists())
		{
			return;
		}
		uint num = PED._GET_PED_INTERACTION_PERSONALITY(ped.Handle);
		uint num2 = _donorPersonality;
		string text = "remembered";
		Ped character = Game.Player.Character;
		foreach (Ped item in PedsAround(character))
		{
			if (!(item == null) && item.Exists() && !item.IsPlayer && item.IsHuman && !item.IsDead && item.Handle != ped.Handle && !IsOurs(item.Handle) && !ENTITY.IS_ENTITY_A_MISSION_ENTITY(item.Handle) && !Kids.IsChild(item) && !(item.Position.DistanceTo(character.Position) > 80f))
			{
				uint num3 = PED._GET_PED_INTERACTION_PERSONALITY(item.Handle);
				if (num3 != 0)
				{
					num2 = num3;
					text = $"ped {item.Handle} ({item.Model.Hash:X8})";
					_donorPersonality = num3;
					break;
				}
			}
		}
		if (num2 != 0 && num2 != num)
		{
			PED._SET_PED_INTERACTION_PERSONALITY(ped.Handle, num2);
		}
		PED.SET_PED_CONFIG_FLAG(ped.Handle, 297, value: true);
		PED.SET_PED_CONFIG_FLAG(ped.Handle, 301, value: false);
		PED.SET_PED_CONFIG_FLAG(ped.Handle, 130, value: false);
		GrudgeLog(string.Format("SOCIAL ped {0}: interaction personality 0x{1:X8} -> 0x{2:X8} ({3})", ped.Handle, num, (num2 != 0) ? num2 : num, (num2 != 0) ? text : "no townsfolk near to copy"));
	}

	internal unsafe void HandToGame(Ped ped)
	{
		if (Config.SocialAmbient && !(ped == null) && ped.Exists())
		{
			int handle = ped.Handle;
			ReleaseIfOurs(&handle);
			GrudgeLog($"SOCIAL ped {ped.Handle} handed to the game (greet / antagonize / rob)");
		}
	}

	internal static void ClaimBack(Ped ped)
	{
		if (!(ped == null) && ped.Exists() && !ENTITY.IS_ENTITY_A_MISSION_ENTITY(ped.Handle))
		{
			MakeOurs(ped.Handle);
		}
	}
}
