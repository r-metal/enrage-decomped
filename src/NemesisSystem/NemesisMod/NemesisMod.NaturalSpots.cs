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
	private const ulong SetPedPromptName = 5352729231319872684uL;

	private readonly List<(Ped ped, int checkAt, string fallback, float heading)> _settling = new List<(Ped, int, string, float)>();

	private static readonly string[] IdleInPlace = new string[3] { "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_GUARD_LAZY" };

	private static readonly string[] TownIdle = new string[4] { "WORLD_HUMAN_SMOKE", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_GUARD_LAZY", "WORLD_HUMAN_SMOKE" };

	internal string NaturalNote = "";

	private static readonly Dictionary<string, float> BuiltInTownCore = new Dictionary<string, float>
	{
		{ "Valentine", 130f },
		{ "Saint Denis", 290f },
		{ "Rhodes", 120f },
		{ "Blackwater", 150f },
		{ "Armadillo", 110f },
		{ "Tumbleweed", 90f },
		{ "Strawberry", 100f },
		{ "Annesburg", 120f },
		{ "Van Horn", 100f }
	};

	private static readonly Dictionary<string, float> TownCoreRadius = new Dictionary<string, float>(BuiltInTownCore);

	private static readonly Dictionary<string, Vector3> BuiltInTownCentre = new Dictionary<string, Vector3> { ["Saint Denis"] = new Vector3(2660f, -1240f, 47f) };

	private static readonly Dictionary<string, Vector3> TownCentre = new Dictionary<string, Vector3>(BuiltInTownCentre);

	private readonly Dictionary<int, Vector3> _settleFrom = new Dictionary<int, Vector3>();

	internal static bool Travelling(Ped player, out Vector3 dir)
	{
		Vector3 vector = ENTITY.GET_ENTITY_VELOCITY((player.IsOnMount && PED.GET_MOUNT(player.Handle) != 0) ? PED.GET_MOUNT(player.Handle) : player.Handle, 0);
		vector.Z = 0f;
		float num = vector.Length();
		dir = ((num > 0.1f) ? (vector * (1f / num)) : Vector3.Zero);
		return num > 2.5f;
	}

	internal unsafe static bool LoadedGround(Vector3 p)
	{
		STREAMING.REQUEST_COLLISION_AT_COORD(p.X, p.Y, p.Z);
		float num = default(float);
		if (!MISC.GET_GROUND_Z_FOR_3D_COORD(p.X, p.Y, p.Z + 50f, &num, p4: false))
		{
			return false;
		}
		return Math.Abs(num - p.Z) < 4f;
	}

	private Vector3 OutOfSightSpot(Ped player, float minD = 35f, float maxD = 75f, bool ahead = true, bool allowSeen = false)
	{
		double num = (double)CAM.GET_GAMEPLAY_CAM_ROT(2).Z * Math.PI / 180.0;
		Vector3 dir;
		bool flag = Travelling(player, out dir);
		double num2 = (flag ? Math.Atan2(0f - dir.X, dir.Y) : num);
		Vector3 vector = Vector3.Zero;
		for (int i = 0; i < 22; i++)
		{
			double num3;
			float num4;
			if (ahead && flag && i < 16)
			{
				num3 = num2 + (_rng.NextDouble() - 0.5) * ((i < 10) ? 2.6 : 4.2);
				num4 = minD + (0.4f + 0.6f * (float)_rng.NextDouble()) * (maxD - minD);
			}
			else if (!ahead && i < 8)
			{
				num3 = num + Math.PI + (_rng.NextDouble() - 0.5) * 2.2;
				num4 = minD + (float)_rng.NextDouble() * (maxD - minD);
			}
			else if (i < 20)
			{
				num3 = _rng.NextDouble() * Math.PI * 2.0;
				num4 = minD + (float)_rng.NextDouble() * (maxD - minD);
			}
			else
			{
				num3 = num + Math.PI + (_rng.NextDouble() - 0.5) * 2.2;
				num4 = minD + (float)_rng.NextDouble() * (maxD - minD);
			}
			Vector3 vector2 = player.Position + new Vector3((0f - (float)Math.Sin(num3)) * num4, (float)Math.Cos(num3) * num4, 0f);
			Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector2, onGround: true, 0);
			Vector3 vector3 = (((safeCoordForPed.X == 0f && safeCoordForPed.Y == 0f) || safeCoordForPed.DistanceTo(vector2) > 20f) ? Ground(vector2) : safeCoordForPed);
			if (Dry(vector3) && (!(num4 > 100f) || LoadedGround(vector3)))
			{
				if (vector == Vector3.Zero)
				{
					vector = vector3;
				}
				if (HiddenFromView(player, vector3))
				{
					return vector3;
				}
			}
		}
		if (!allowSeen)
		{
			return Vector3.Zero;
		}
		return vector;
	}

	internal bool TownCore(string town, out Vector3 centre, out float radius)
	{
		centre = Vector3.Zero;
		radius = 0f;
		if (town == null || !TownSpots.TryGetValue(town, out (Vector3?, Vector3?) value))
		{
			return false;
		}
		if (TownCentre.TryGetValue(town, out var value2))
		{
			centre = value2;
		}
		else if (value.Item1.HasValue && value.Item2.HasValue)
		{
			centre = (value.Item1.Value + value.Item2.Value) * 0.5f;
		}
		else
		{
			centre = value.Item1 ?? value.Item2.Value;
		}
		radius = (TownCoreRadius.TryGetValue(town, out var value3) ? value3 : 110f) * Balance.TownCoreScale;
		return true;
	}

	internal bool NearTownCore(Ped player, string town, float margin = 60f)
	{
		if (town != null && TownCore(town, out var centre, out var radius))
		{
			return player.Position.DistanceTo2D(centre) < ((radius > 150f) ? (radius * 0.75f) : (radius + margin));
		}
		return false;
	}

	internal string SpotNote(Vector3 p, string town, Ped player)
	{
		string text = TownAt(p) ?? "no town zone";
		Vector3 centre;
		float radius;
		string text2 = (TownCore(town, out centre, out radius) ? string.Format("{0:0}/{1:0} m from {2} centre{3}", p.DistanceTo2D(centre), radius, town, (p.DistanceTo2D(centre) > radius) ? " (OUTSIDE)" : "") : "no core");
		return $"{p.X:0},{p.Y:0} - {p.DistanceTo(player.Position):0} m from you, {text2}, zone {text}";
	}

	internal static bool Outdoors(Vector3 p)
	{
		return INTERIOR.GET_INTERIOR_AT_COORDS(p.X, p.Y, p.Z + 1f) == 0;
	}

	private (Vector3 pos, string where) NaturalTownSpot(Ped player, string town)
	{
		if (town != null && TownSpots.TryGetValue(town, out (Vector3?, Vector3?) value))
		{
			Vector3? item = value.Item2;
			if (item.HasValue)
			{
				Vector3 valueOrDefault = item.GetValueOrDefault();
				if (_rng.Next(100) < 30 && valueOrDefault.DistanceTo(player.Position) > 12f && valueOrDefault.DistanceTo(player.Position) < 120f && (INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) == 0 || HiddenFromView(player, valueOrDefault)))
				{
					return (pos: valueOrDefault, where: "at the saloon bar");
				}
			}
			if (_townSpots.TryGetValue(town, out var value2))
			{
				foreach (Vector3 item2 in value2.About.OrderBy((Vector3 _) => _rng.Next()))
				{
					float num = item2.DistanceTo(player.Position);
					if (num > 15f && num < 140f && StockedSpotLow(item2, town) && !OurPeopleNear(item2, TownSpread(town)) && HiddenFromView(player, item2))
					{
						return (pos: item2, where: "about town");
					}
				}
			}
			foreach (Vector3 item3 in from v in new Vector3?[2] { value.Item2, value.Item1 }
				where v.HasValue
				select v.Value into _
				orderby _rng.Next()
				select _)
			{
				for (int num2 = 0; num2 < 6; num2++)
				{
					double num3 = _rng.NextDouble() * Math.PI * 2.0;
					float num4 = 6f + (float)_rng.NextDouble() * 30f;
					Vector3 vector = GroundOnSpot(item3 + new Vector3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, 0f));
					float num5 = vector.DistanceTo(player.Position);
					if (num5 > 15f && num5 < 140f && !UpHigh(vector) && Outdoors(vector) && !OurPeopleNear(vector, TownSpread(town)) && HiddenFromView(player, vector))
					{
						return (pos: vector, where: "about town");
					}
				}
			}
		}
		if (TownCore(town, out var centre, out var radius))
		{
			if (_townSpots.TryGetValue(town, out var value3) && value3.Street.Count > 0)
			{
				Vector3 vector2 = Vector3.Zero;
				foreach (Vector3 item4 in value3.Street.OrderBy((Vector3 _) => _rng.Next()))
				{
					float num6 = item4.DistanceTo(player.Position);
					if (!(num6 < 15f) && !(num6 > 220f) && StockedSpotLow(item4, town) && !OurPeopleNear(item4, TownSpread(town)))
					{
						if (vector2 == Vector3.Zero)
						{
							vector2 = item4;
						}
						if (HiddenFromView(player, item4))
						{
							GrudgeLog("TOWNSPOT street spot in " + town + " (stocked): " + SpotNote(item4, town, player));
							return (pos: item4, where: "in the street");
						}
					}
				}
				if (vector2 != Vector3.Zero && value3.Street.Count >= 12)
				{
					GrudgeLog("TOWNSPOT street spot in " + town + " (stocked, in view - nothing hidden): " + SpotNote(vector2, town, player));
					return (pos: vector2, where: "in the street");
				}
			}
			Vector3 vector3 = Vector3.Zero;
			for (int num7 = 0; num7 < 16; num7++)
			{
				double num8 = _rng.NextDouble() * Math.PI * 2.0;
				float num9 = (float)Math.Sqrt(_rng.NextDouble()) * radius * 0.85f;
				Vector3 vector4 = centre + new Vector3((float)Math.Cos(num8) * num9, (float)Math.Sin(num8) * num9, 0f);
				Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector4, onGround: true, 0);
				Vector3 vector5 = (((safeCoordForPed.X == 0f && safeCoordForPed.Y == 0f) || safeCoordForPed.DistanceTo2D(vector4) > 15f) ? GroundOnSpot(vector4) : safeCoordForPed);
				if (vector5.DistanceTo2D(centre) > radius || UpHigh(vector5) || !Outdoors(vector5))
				{
					continue;
				}
				float num10 = vector5.DistanceTo(player.Position);
				if (!(num10 < 15f) && !(num10 > 220f) && !OurPeopleNear(vector5, TownSpread(town)))
				{
					if (vector3 == Vector3.Zero)
					{
						vector3 = vector5;
					}
					if (HiddenFromView(player, vector5))
					{
						GrudgeLog("TOWNSPOT street spot in " + town + ": " + SpotNote(vector5, town, player));
						return (pos: vector5, where: "in the street");
					}
				}
			}
			if (vector3 != Vector3.Zero)
			{
				GrudgeLog("TOWNSPOT street spot in " + town + " (in view - nothing hidden): " + SpotNote(vector3, town, player));
				return (pos: vector3, where: "in the street");
			}
		}
		GrudgeLog("TOWNSPOT no town spot for " + (town ?? "?") + " (" + ((player != null && town != null && TownCore(town, out var centre2, out var _)) ? $"you are {player.Position.DistanceTo2D(centre2):0} m from its centre" : "no core") + ") - nobody placed");
		return (pos: Vector3.Zero, where: "nowhere");
	}

	private void SettleInWorld(Ped ped, float radius, string fallback, float heading)
	{
		Vector3 position = ped.Position;
		radius = Math.Min(radius, 18f);
		Crumbs.Native("seat task: natural nearest scenario");
		NoteSeatTask();
		TASK.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP(ped.Handle, position.X, position.Y, position.Z, radius, -1, p6: true, p7: false, p8: false, p9: false);
		_settleFrom[ped.Handle] = position;
		_settling.Add((ped, Game.GameTime + 1500, fallback, heading));
	}

	private void PlaceNaturally(Ped ped, Vector3 spot, bool allowWander = true)
	{
		if (allowWander && _rng.Next(100) < 15)
		{
			TASK.TASK_WANDER_STANDARD(ped.Handle, 10f, 10);
			NaturalNote = "wandering";
			return;
		}
		Crumbs.Native("seat task: natural scenario 35 m");
		NoteSeatTask();
		TASK.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP(ped.Handle, spot.X, spot.Y, spot.Z, 35f, -1, p6: true, p7: false, p8: false, p9: false);
		_settleFrom[ped.Handle] = spot;
		_settling.Add((ped, Game.GameTime + 1500, (allowWander && _rng.Next(100) < 50) ? null : IdleInPlace[_rng.Next(IdleInPlace.Length)], -1f));
		NaturalNote = "using a scenario point";
	}

	private void UpdateSettling()
	{
		UpdateSeating();
		for (int num = _settling.Count - 1; num >= 0; num--)
		{
			var (ped, num2, text, heading) = _settling[num];
			if (Game.GameTime >= num2)
			{
				_settling.RemoveAt(num);
				if (!(ped == null) && ped.Exists() && !ped.IsDead)
				{
					bool flag = PED.IS_PED_USING_ANY_SCENARIO(ped.Handle);
					Named value;
					NemesisRecord value2;
					string text2 = (_named.TryGetValue(ped.Handle, out value) ? value.Name : (_worldNemeses.TryGetValue(ped.Handle, out value2) ? value2.Name : $"ped {ped.Handle}"));
					Vector3 position = ped.Position;
					Vector3 value3;
					bool flag2 = flag && _settleFrom.TryGetValue(ped.Handle, out value3) && new Vector3(position.X - value3.X, position.Y - value3.Y, 0f).Length() > 10f && !HiddenFromView(Game.Player.Character, position) && HiddenFromView(Game.Player.Character, value3);
					Vector3 value4;
					bool flag3 = _settleFrom.TryGetValue(ped.Handle, out value4) && position.Z < value4.Z - 2.5f;
					if (flag && !flag3 && _settleFrom.TryGetValue(ped.Handle, out var value5) && (Math.Abs(position.Z - value5.Z) > 2.5f || flag2))
					{
						TASK.CLEAR_PED_TASKS_IMMEDIATELY(ped.Handle, p1: false, resetCrouch: true);
						ENTITY.SET_ENTITY_COORDS(ped.Handle, value5.X, value5.Y, value5.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
						string text3 = IdleInPlace[_rng.Next(IdleInPlace.Length)];
						TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(text3), -1, playEnterAnim: true, 0u, -1f, p6: false);
						GrudgeLog(string.Format("SETTLE {0}: the scenario was {1:+0;-0} m off the ground he was put on ({2:0},{3:0},{4:0}) - back on the street, {5}", text2, position.Z - value5.Z, position.X, position.Y, position.Z, text3.Replace("WORLD_HUMAN_", "").ToLowerInvariant()));
						_settleFrom.Remove(ped.Handle);
					}
					else
					{
						if (flag3)
						{
							GrudgeLog($"SETTLE {text2}: put up high, the scenario brought him down to {position.Z:0} - left there");
						}
						_settleFrom.Remove(ped.Handle);
						GrudgeLog(string.Format("SETTLE {0} at {1:0},{2:0},{3:0}: {4}{5}", text2, position.X, position.Y, position.Z, flag ? "in a scenario" : ((text != null) ? ("no scenario - " + text) : "no scenario - wanders"), (INTERIOR.GET_INTERIOR_FROM_ENTITY(ped.Handle) != 0) ? " (indoors)" : ""));
						if (!flag && !PED.IS_PED_IN_COMBAT(ped.Handle, Game.Player.Character.Handle))
						{
							if (text != null)
							{
								TASK.TASK_START_SCENARIO_IN_PLACE_HASH(ped.Handle, MISC.GET_HASH_KEY(text), -1, playEnterAnim: true, 0u, heading, p6: false);
								NaturalNote = "no scenario near - " + text.Replace("WORLD_HUMAN_", "").ToLowerInvariant();
							}
							else
							{
								TASK.TASK_WANDER_STANDARD(ped.Handle, 10f, 10);
								NaturalNote = "no scenario near - wandering";
							}
						}
					}
				}
			}
		}
	}
}
