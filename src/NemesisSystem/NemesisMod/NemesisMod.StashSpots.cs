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
	private sealed class WitnessBatch
	{
		public string Crime;

		public string Text;

		public Vector3 At;

		public int Victim;

		public bool SelfDefence;

		public int Count;

		public int Masked;

		public List<string> Seen;

		public readonly Queue<(Ped p, float d, int fear)> Jobs = new Queue<(Ped, float, int)>();
	}

	private sealed class TownSpotStock
	{
		public readonly List<Vector3> About = new List<Vector3>();

		public readonly List<Vector3> Street = new List<Vector3>();

		public int Tries;
	}

	private const int WitnessNow = 3;

	private const int WitnessPerFrame = 2;

	private readonly Queue<WitnessBatch> _witnessBatches = new Queue<WitnessBatch>();

	private readonly Dictionary<string, TownSpotStock> _townSpots = new Dictionary<string, TownSpotStock>();

	private const int TownSpotStockMax = 16;

	private const int TownSpotTriesMax = 400;

	private const float TownSpotFillRange = 180f;

	private bool _townSpotAbout;

	private static readonly StashSpot[] BuiltInStashSpots = new StashSpot[2]
	{
		new StashSpot
		{
			Town = "Saint Denis",
			X = 2714.83f,
			Y = -1488.71f,
			Z = 44.96f,
			H = 58.6f
		},
		new StashSpot
		{
			Town = "Saint Denis",
			X = 2882.4f,
			Y = -1242.68f,
			Z = 44.97f,
			H = 80.4f
		}
	};

	private List<StashSpot> _stashSpots;

	private static readonly (int ch, string name, float x, float y)[] ChapterCamps = new(int, string, float, float)[5]
	{
		(1, "Colter", -1350f, 2420f),
		(2, "Horseshoe Overlook", -138f, -22f),
		(3, "Clemens Point", 676f, -1228f),
		(4, "Shady Belle", 1875f, -1850f),
		(6, "Beaver Hollow", 2375f, 1335f)
	};

	private static readonly (string key, int ch, bool dead, string how)[] StoryFates = new(string, int, bool, string)[18]
	{
		("sean", 4, true, "Shot dead in Rhodes by the Grays"),
		("kieran", 6, true, "Killed by Colm O'Driscoll's men - his body sent back to the camp at Beaver Hollow"),
		("hosea", 5, true, "Shot by Agent Milton after the Saint Denis bank job"),
		("lenny", 5, true, "Killed in the Saint Denis bank job"),
		("arthur", 7, true, "Died on the mountain, the morning the gang came apart"),
		("molly", 7, true, "Shot by Susan Grimshaw at Beaver Hollow"),
		("grimshaw", 7, true, "Killed by Micah Bell the night the gang came apart"),
		("dutch", 7, false, "Gone - walked away from all of it on the mountain"),
		("micah", 7, false, "Gone - the rat, turned on the gang"),
		("bill", 7, false, "Gone - rode off with Dutch and Micah"),
		("javier", 7, false, "Gone - rode off with Dutch"),
		("strauss", 7, false, "Gone - sent away by Arthur"),
		("pearson", 7, false, "Left the gang - ran a store back east, they say"),
		("swanson", 7, false, "Left the gang - found his faith again"),
		("tilly", 7, false, "Left the gang - a new life"),
		("marybeth", 7, false, "Left the gang - writing books, they say"),
		("karen", 7, false, "Left the gang"),
		("trelawny", 7, false, "Left the gang - went back to his family")
	};

	private void WitnessQueueTick()
	{
		for (int i = 0; i < 2; i++)
		{
			if (_witnessBatches.Count <= 0)
			{
				break;
			}
			WitnessBatch witnessBatch = _witnessBatches.Peek();
			if (witnessBatch.Jobs.Count > 0)
			{
				var (p, d, fearHere) = witnessBatch.Jobs.Dequeue();
				try
				{
					WitnessOne(witnessBatch, p, d, fearHere);
				}
				catch (Exception ex)
				{
					GrudgeLog("WITNESS failed: " + ex.Message);
				}
			}
			if (witnessBatch.Jobs.Count == 0)
			{
				_witnessBatches.Dequeue();
				WitnessDone(witnessBatch);
			}
		}
	}

	private void TownSpotTick(Ped player)
	{
		string currentTown = _currentTown;
		if (currentTown == null || !NearTownCore(player, currentTown) || WorldBusy(player))
		{
			return;
		}
		if (!_townSpots.TryGetValue(currentTown, out var value))
		{
			value = (_townSpots[currentTown] = new TownSpotStock());
		}
		if (value.Tries >= 400 || (value.About.Count >= 16 && value.Street.Count >= 16))
		{
			return;
		}
		_townSpotAbout = !_townSpotAbout;
		Vector3 position = player.Position;
		if (_townSpotAbout && value.About.Count < 16 && TownSpots.TryGetValue(currentTown, out (Vector3?, Vector3?) value2))
		{
			List<Vector3> list = (from v in new Vector3?[2] { value2.Item2, value2.Item1 }
				where v.HasValue
				select v.Value).ToList();
			if (list.Count == 0)
			{
				return;
			}
			Vector3 vector = list[_rng.Next(list.Count)];
			double num = _rng.NextDouble() * Math.PI * 2.0;
			float num2 = 6f + (float)_rng.NextDouble() * 30f;
			Vector3 p = vector + new Vector3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, 0f);
			if (!(p.DistanceTo2D(position) > 180f))
			{
				value.Tries++;
				Vector3 p2 = GroundOnSpot(p);
				if (!UpHigh(p2) && Outdoors(p2) && !value.About.Any((Vector3 x) => x.DistanceTo(p2) < 4f))
				{
					value.About.Add(p2);
				}
			}
		}
		else
		{
			if (value.Street.Count >= 16 || !TownCore(currentTown, out var centre, out var radius))
			{
				return;
			}
			double num3 = _rng.NextDouble() * Math.PI * 2.0;
			float num4 = (float)Math.Sqrt(_rng.NextDouble()) * radius * 0.85f;
			Vector3 vector2 = centre + new Vector3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, 0f);
			if (!(vector2.DistanceTo2D(position) > 180f))
			{
				value.Tries++;
				Vector3 safeCoordForPed = World.GetSafeCoordForPed(vector2, onGround: true, 0);
				Vector3 p3 = (((safeCoordForPed.X == 0f && safeCoordForPed.Y == 0f) || safeCoordForPed.DistanceTo2D(vector2) > 15f) ? GroundOnSpot(vector2) : safeCoordForPed);
				if (p3.DistanceTo2D(centre) <= radius && !UpHigh(p3) && Outdoors(p3) && !value.Street.Any((Vector3 x) => x.DistanceTo(p3) < 4f))
				{
					value.Street.Add(p3);
				}
			}
		}
	}

	private bool StockedSpotLow(Vector3 p, string town)
	{
		if (town == null || !TownCore(town, out var centre, out var _) || centre.Z == 0f)
		{
			return true;
		}
		if (p.Z > centre.Z + 6f)
		{
			return !(p.Z > Game.Player.Character.Position.Z + 6f);
		}
		return true;
	}

	private string StashSpotsPath => Path.Combine(ContentDir, "stash_spots.json");

	private List<StashSpot> StashSpots => _stashSpots ?? (_stashSpots = LoadStashSpots());

	private List<StashSpot> LoadStashSpots()
	{
		List<StashSpot> list = BuiltInStashSpots.Select(delegate(StashSpot stashSpot)
		{
			stashSpot.BuiltIn = true;
			return stashSpot;
		}).ToList();
		try
		{
			if (File.Exists(StashSpotsPath))
			{
				List<StashSpot> list2 = JsonConvert.DeserializeObject<List<StashSpot>>(File.ReadAllText(StashSpotsPath)) ?? new List<StashSpot>();
				foreach (StashSpot s in list2.Where((StashSpot stashSpot) => stashSpot?.Town != null))
				{
					if (!list.Any((StashSpot o) => o.Town == s.Town && o.Pos.DistanceTo(s.Pos) < 3f))
					{
						list.Add(s);
					}
				}
				GrudgeLog(string.Format("STASHSPOT {0} placed spot(s) loaded ({1})", list2.Count, string.Join(", ", from stashSpot in list
					group stashSpot by stashSpot.Town into gr
					select $"{gr.Key} {gr.Count()}")));
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("STASHSPOT stash_spots.json unreadable: " + ex.Message);
		}
		return list;
	}

	private void SaveStashSpots()
	{
		try
		{
			SafeFile.Write(StashSpotsPath, JsonConvert.SerializeObject(StashSpots.Where((StashSpot s) => !s.BuiltIn).ToList(), Formatting.Indented));
		}
		catch (Exception ex)
		{
			GrudgeLog("STASHSPOT save failed: " + ex.Message);
		}
	}

	internal void DebugPlaceStashSpot(Ped p)
	{
		Vector3 position = p.Position;
		string town = _currentTown ?? TownNear(position);
		if (town == null)
		{
			SpawnerSays("stand in a town to place a stash spot.");
			return;
		}
		Vector3 g = GroundOnSpot(position);
		if (g == Vector3.Zero)
		{
			g = position;
		}
		if (StashSpots.Any((StashSpot s) => s.Town == town && s.Pos.DistanceTo2D(g) < 3f))
		{
			SpawnerSays("there's a stash spot here already.");
			return;
		}
		StashSpot item = new StashSpot
		{
			Town = town,
			X = g.X,
			Y = g.Y,
			Z = g.Z,
			H = p.Heading
		};
		StashSpots.Add(item);
		SaveStashSpots();
		int num = StashSpots.Count((StashSpot s) => s.Town == town);
		GrudgeLog($"STASHSPOT placed in {town} at {g.X:0.0},{g.Y:0.0},{g.Z:0.0} heading {p.Heading:0} ({num} in {town})");
		SpawnerSays($"stash spot placed - {num} in {town}. A stash piles up here, facing the way you face.");
		if (_iconBlips.ContainsKey(IconKind.Supplies))
		{
			ShowIcons(IconKind.Supplies);
		}
	}

	internal void DebugRemoveStashSpot(Ped p)
	{
		Vector3 pos = p.Position;
		StashSpot near = (from s in StashSpots
			where s.Pos.DistanceTo2D(pos) < 15f
			orderby s.Pos.DistanceTo2D(pos)
			select s).FirstOrDefault();
		if (near == null)
		{
			SpawnerSays("no stash spot within 15 m.");
			return;
		}
		if (near.BuiltIn)
		{
			SpawnerSays("that spot is built into the mod - it can't be removed here.");
			return;
		}
		StashSpots.Remove(near);
		SaveStashSpots();
		GrudgeLog($"STASHSPOT removed in {near.Town} at {near.X:0.0},{near.Y:0.0}");
		SpawnerSays($"stash spot removed - {StashSpots.Count((StashSpot s) => s.Town == near.Town)} left in {near.Town}.");
		if (_iconBlips.ContainsKey(IconKind.Supplies))
		{
			ShowIcons(IconKind.Supplies);
		}
	}
}
