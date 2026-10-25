using System;
using System.Collections.Generic;
using System.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem;

internal sealed class CampKit
{
	internal enum KitZone
	{
		Inner,
		Mid,
		Outer,
		Line
	}

	internal enum KitShape
	{
		Ring,
		Row,
		Cluster,
		Scatter
	}

	internal sealed class KitPiece
	{
		public string Piece;

		public string[] Names;

		public int Count;

		public int Chance;

		public KitZone Zone;

		public float Size;

		public bool Faces;
	}

	internal sealed class CampLook
	{
		public bool Fire = true;

		public KitShape Shape;

		public KitPiece[] Pieces;
	}

	private sealed class Rig
	{
		public readonly List<Vehicle> Wagons = new List<Vehicle>();

		public readonly List<Ped> Horses = new List<Ped>();
	}

	private readonly NemesisMod _mod;

	private static readonly string[] KitTents = new string[7] { "p_ambtentscrub01b", "p_ambtentgrass01b", "p_ambtentmulch01b", "p_ambtentdirt01x", "p_tentarmy01x", "s_tentcaravan01x", "p_tent_leento01x" };

	private static readonly string[] KitBedrolls = new string[4] { "p_bedrollopen01x", "p_bedrollopen02x", "p_bedroll01x", "p_bedrollclosed01x" };

	private static readonly string[] KitSeats = new string[5] { "p_stool01x", "p_woodenchair01x", "p_chair05x", "p_bench_log01x", "p_log02x" };

	private static readonly string[] KitLanterns = new string[4] { "p_lantern05x", "p_lantern09x", "p_lantern01x", "p_lantern02x" };

	private static readonly string[] KitWood = new string[4] { "p_woodpile01x", "p_woodpile02x", "p_firewood01x", "p_logpile01x" };

	private static readonly string[] KitCrates = new string[3] { "p_cratestack01x", "p_crate03x", "p_crate14x" };

	private static readonly string[] KitBarrels = new string[3] { "p_barrel02x", "p_barrel01x", "p_barrel_wood01x" };

	private static readonly string[] KitGrassTent = new string[2] { "p_ambtentgrass01b", "p_ambtentscrub01b" };

	private static readonly string[] KitArmyTent = new string[3] { "p_tentarmy01x", "p_tentarmy02x", "p_ambtentscrub01b" };

	private static readonly string[] KitCaravan = new string[4] { "s_tentcaravan01x", "s_tentcaravan02x", "p_tentarmy01x", "p_ambtentscrub01b" };

	private static readonly string[] KitLeanTo = new string[4] { "p_tent_leento01x", "p_tentleanto01x", "p_leanto01x", "p_ambtentgrass01b" };

	private static readonly string[] KitTipi = new string[5] { "p_tipi01x", "p_teepee01x", "p_tepee01x", "p_ambtentnative01x", "p_ambtentscrub01b" };

	private static readonly string[] KitLogSeat = new string[3] { "p_bench_log01x", "p_log02x", "p_stool01x" };

	private static readonly string[] KitCrateStack = new string[2] { "p_cratestack02x", "p_cratestack01x" };

	private static readonly string[] KitHayStack = new string[2] { "p_haybalestack01x", "p_haybale02x" };

	private static readonly string[] KitHay = new string[3] { "p_haybale01x", "p_haybale02x", "p_haybale03x" };

	private static readonly string[] KitSaddle = new string[4] { "p_saddle01x", "p_saddle02x", "p_saddle03x", "p_saddlestand01x" };

	private static readonly string[] KitChest = new string[3] { "p_chest01x", "p_chest02x", "p_trunk01x" };

	private static readonly string[] KitWhiskey = new string[4] { "p_bottlewhiskey01x", "p_whiskeybottle01x", "p_bottle008x", "p_bottle01x" };

	private static readonly string[] KitStill = new string[4] { "p_still01x", "p_still02x", "p_moonshinestill01x", "p_still03x" };

	private static readonly string[] KitJug = new string[4] { "p_jug01x", "p_jug02x", "p_moonshinejug01x", "p_jugmoonshine01x" };

	private static readonly string[] KitPelt = new string[1] { "p_bearskin01x" };

	private static readonly string[] KitSkull = new string[4] { "p_humanskull01x", "p_skull01x", "p_skullhuman01x", "p_skull02x" };

	private static readonly string[] KitCoffin = new string[2] { "p_coffin01x", "p_coffin02x" };

	private static readonly string[] KitGrave = new string[3] { "p_gravecross01x", "p_gravemarker01x", "p_gravemarker02x" };

	private static readonly string[] KitBlanket = new string[4] { "p_blanket01x", "p_blanketpile01x", "p_blanketfolded01x", "p_blankets01x" };

	private static readonly string[] KitTable = new string[4] { "p_table01x", "p_table05x", "p_tablesmall01x", "p_tablewood01x" };

	private static readonly string[] KitChair = new string[3] { "p_woodenchair01x", "p_chair05x", "p_stool01x" };

	private static readonly string[] KitCandle = new string[4] { "p_candlegroup01x", "p_candle01x", "p_candlebot01x", "p_candles01x" };

	private static readonly string[] KitTrunk = new string[2] { "p_trunk01x", "p_trunk04x" };

	private const KitZone ZIn = KitZone.Inner;

	private const KitZone ZMid = KitZone.Mid;

	private const KitZone ZOut = KitZone.Outer;

	private const KitZone ZLine = KitZone.Line;

	private static readonly CampLook PlainCamp = new CampLook
	{
		Shape = KitShape.Ring,
		Pieces = new KitPiece[8]
		{
			P("tent", KitTents, 1, KitZone.Outer, 2.5f, faces: true),
			P("tent", KitTents, 1, KitZone.Outer, 2.5f, faces: true, 60),
			P("bedroll", KitBedrolls, 3, KitZone.Inner, 0.9f, faces: true),
			P("seat", KitSeats, 1, KitZone.Inner, 0.5f, faces: true),
			P("lantern", KitLanterns, 1, KitZone.Mid, 0.3f),
			P("wood", KitWood, 1, KitZone.Mid, 0.9f),
			P("crates", KitCrates, 1, KitZone.Mid, 1f),
			P("barrel", KitBarrels, 2, KitZone.Mid, 0.5f)
		}
	};

	private static readonly Dictionary<string, CampLook> CampLooks = new Dictionary<string, CampLook>
	{
		["odriscoll"] = new CampLook
		{
			Shape = KitShape.Cluster,
			Pieces = new KitPiece[9]
			{
				P("grass tent", KitGrassTent, 2, KitZone.Outer, 2.5f, faces: true),
				P("bedroll", KitBedrolls, 3, KitZone.Inner, 0.9f, faces: true),
				P("seat", KitSeats, 2, KitZone.Inner, 0.5f, faces: true),
				P("lantern", KitLanterns, 1, KitZone.Mid, 0.3f),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f),
				P("crate stack", KitCrateStack, 3, KitZone.Outer, 1.2f),
				P("chest", KitChest, 2, KitZone.Mid, 0.8f),
				P("barrel", KitBarrels, 3, KitZone.Mid, 0.5f),
				P("whiskey", KitWhiskey, 2, KitZone.Inner, 0.3f)
			}
		},
		["lemoyneraiders"] = new CampLook
		{
			Shape = KitShape.Row,
			Pieces = new KitPiece[9]
			{
				P("army tent", KitArmyTent, 2, KitZone.Outer, 3f, faces: true),
				P("bedroll", KitBedrolls, 3, KitZone.Inner, 0.9f, faces: true),
				P("seat", KitSeats, 1, KitZone.Inner, 0.5f, faces: true),
				P("lantern", KitLanterns, 1, KitZone.Mid, 0.3f),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f),
				P("still", KitStill, 1, KitZone.Line, 1.5f),
				P("barrel", KitBarrels, 4, KitZone.Line, 0.5f),
				P("hay stack", KitHayStack, 1, KitZone.Line, 1.6f),
				P("jug", KitJug, 2, KitZone.Line, 0.3f)
			}
		},
		["wapiti"] = new CampLook
		{
			Shape = KitShape.Ring,
			Pieces = new KitPiece[6]
			{
				P("tipi", KitTipi, 2, KitZone.Outer, 3f, faces: true),
				P("bedroll", KitBedrolls, 2, KitZone.Inner, 0.9f, faces: true),
				P("log seat", KitLogSeat, 3, KitZone.Inner, 0.9f, faces: true),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f),
				P("blanket", KitBlanket, 2, KitZone.Mid, 0.6f),
				P("pelt", KitPelt, 1, KitZone.Mid, 0.9f)
			}
		},
		["bronte"] = new CampLook
		{
			Shape = KitShape.Cluster,
			Pieces = new KitPiece[7]
			{
				P("caravan tent", KitCaravan, 1, KitZone.Outer, 3.5f, faces: true),
				P("table", KitTable, 1, KitZone.Inner, 1.3f),
				P("chair", KitChair, 4, KitZone.Inner, 0.6f, faces: true),
				P("lantern", KitLanterns, 3, KitZone.Mid, 0.3f),
				P("trunk", KitTrunk, 2, KitZone.Mid, 0.9f),
				P("bedroll", KitBedrolls, 2, KitZone.Mid, 0.9f, faces: true),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f)
			}
		},
		["murfree"] = new CampLook
		{
			Shape = KitShape.Scatter,
			Pieces = new KitPiece[8]
			{
				P("grave", KitGrave, 3, KitZone.Outer, 0.8f, faces: true),
				P("coffin", KitCoffin, 1, KitZone.Mid, 1.3f),
				P("bedroll", KitBedrolls, 2, KitZone.Inner, 0.9f, faces: true),
				P("log seat", KitLogSeat, 1, KitZone.Inner, 0.9f, faces: true),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f),
				P("barrel", KitBarrels, 2, KitZone.Mid, 0.5f),
				P("lean-to", KitLeanTo, 1, KitZone.Outer, 2.2f, faces: true),
				P("skull", KitSkull, 3, KitZone.Mid, 0.4f)
			}
		},
		["skinners"] = new CampLook
		{
			Shape = KitShape.Scatter,
			Pieces = new KitPiece[8]
			{
				P("lean-to", KitLeanTo, 1, KitZone.Outer, 2.2f, faces: true),
				P("grass tent", KitGrassTent, 2, KitZone.Outer, 2.5f, faces: true),
				P("tent", KitTents, 1, KitZone.Outer, 2.5f, faces: true),
				P("pelt", KitPelt, 1, KitZone.Mid, 0.9f),
				P("barrel", KitBarrels, 3, KitZone.Mid, 0.5f),
				P("crates", KitCrates, 1, KitZone.Mid, 1f),
				P("bedroll", KitBedrolls, 2, KitZone.Inner, 0.9f, faces: true),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f)
			}
		},
		["laramie"] = new CampLook
		{
			Shape = KitShape.Row,
			Pieces = new KitPiece[9]
			{
				P("tent", KitTents, 3, KitZone.Outer, 2.5f, faces: true),
				P("hay stack", KitHayStack, 3, KitZone.Line, 1.6f),
				P("hay", KitHay, 2, KitZone.Line, 1f),
				P("saddle", KitSaddle, 2, KitZone.Mid, 0.6f),
				P("barrel", KitBarrels, 3, KitZone.Mid, 0.5f),
				P("bedroll", KitBedrolls, 3, KitZone.Inner, 0.9f, faces: true),
				P("seat", KitSeats, 1, KitZone.Inner, 0.5f, faces: true),
				P("lantern", KitLanterns, 1, KitZone.Mid, 0.3f),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f)
			}
		},
		["delobo"] = new CampLook
		{
			Shape = KitShape.Ring,
			Pieces = new KitPiece[7]
			{
				P("bedroll", KitBedrolls, 6, KitZone.Inner, 0.9f, faces: true),
				P("log seat", KitLogSeat, 3, KitZone.Mid, 0.9f, faces: true),
				P("blanket", KitBlanket, 2, KitZone.Mid, 0.6f),
				P("trunk", KitTrunk, 1, KitZone.Mid, 0.9f),
				P("jug", KitJug, 2, KitZone.Mid, 0.3f),
				P("lantern", KitLanterns, 2, KitZone.Mid, 0.3f),
				P("wood", KitWood, 1, KitZone.Mid, 0.9f)
			}
		},
		["nightfolk"] = new CampLook
		{
			Fire = false,
			Shape = KitShape.Scatter,
			Pieces = new KitPiece[6]
			{
				P("lean-to", KitLeanTo, 2, KitZone.Outer, 2.2f, faces: true),
				P("grave", KitGrave, 2, KitZone.Outer, 0.8f, faces: true),
				P("candles", KitCandle, 4, KitZone.Inner, 0.3f),
				P("skull", KitSkull, 3, KitZone.Mid, 0.4f),
				P("lantern", KitLanterns, 3, KitZone.Mid, 0.3f),
				P("bedroll", KitBedrolls, 2, KitZone.Inner, 0.9f, faces: true)
			}
		}
	};

	private static readonly Dictionary<string, (string[] Wagons, int Count, int Horses)> CampRigs = new Dictionary<string, (string[], int, int)>
	{
		["odriscoll"] = (new string[3] { "wagon04x", "wagon02x", "cart03" }, 2, 2),
		["lemoyneraiders"] = (new string[3] { "wagon05x", "wagon06x", "cart06" }, 2, 2),
		["wapiti"] = (new string[0], 0, 3),
		["bronte"] = (new string[3] { "coach2", "buggy01", "wagon02x" }, 2, 2),
		["murfree"] = (new string[2] { "cart03", "cart01" }, 1, 1),
		["skinners"] = (new string[3] { "cart01", "wagon04x", "cart03" }, 2, 2),
		["laramie"] = (new string[3] { "chuckwagon000x", "wagon05x", "chuckwagon002x" }, 2, 3),
		["delobo"] = (new string[3] { "cart06", "cart03", "wagon04x" }, 2, 3),
		["nightfolk"] = (new string[0], 0, 0)
	};

	private static readonly (string[] Wagons, int Count, int Horses) PlainRig = (Wagons: new string[2] { "wagon04x", "cart03" }, Count: 1, Horses: 2);

	private readonly Dictionary<string, List<int>> _kits = new Dictionary<string, List<int>>();

	private readonly Dictionary<string, List<(Vector3 At, float R)>> _spots = new Dictionary<string, List<(Vector3, float)>>();

	private readonly Dictionary<string, Rig> _rigs = new Dictionary<string, Rig>();

	private readonly Dictionary<string, Vector3> _centres = new Dictionary<string, Vector3>();

	private readonly Dictionary<string, (Vector3 at, int until)> _held = new Dictionary<string, (Vector3, int)>();

	private readonly Dictionary<string, string> _model = new Dictionary<string, string>();

	private readonly HashSet<string> _logged = new HashSet<string>();

	internal IEnumerable<string> Keys => _kits.Keys;

	internal CampKit(NemesisMod mod)
	{
		_mod = mod;
	}

	private static KitPiece P(string piece, string[] names, int count, KitZone zone, float size = 0.7f, bool faces = false, int chance = 100)
	{
		return new KitPiece
		{
			Piece = piece,
			Names = names,
			Count = count,
			Zone = zone,
			Size = size,
			Faces = faces,
			Chance = chance
		};
	}

	internal static CampLook LookOf(string gang)
	{
		if (gang == null || !CampLooks.TryGetValue(gang, out var value))
		{
			return PlainCamp;
		}
		return value;
	}

	internal bool Has(string key)
	{
		return _kits.ContainsKey(key);
	}

	internal Vector3? FirstSpot(string key)
	{
		if (!_spots.TryGetValue(key, out List<(Vector3, float)> value) || value.Count <= 0)
		{
			return null;
		}
		return value[0].Item1;
	}

	internal void ForgetCentre(string siteId)
	{
		_centres.Remove(siteId);
	}

	internal bool ClearOf(string siteId, Vector3 at, float r)
	{
		if (siteId != null && _spots.TryGetValue(siteId, out List<(Vector3, float)> value))
		{
			return value.All<(Vector3, float)>(((Vector3 At, float R) x) => x.At.DistanceTo2D(at) >= x.R + r);
		}
		return true;
	}

	internal unsafe Vector3 Centre(CampSite site)
	{
		Vector3 vector = new Vector3(site.X, site.Y, site.Z);
		if (site.Imap != 0 || site.Hideout)
		{
			return vector;
		}
		if (_mod.SiteInAnyTown(site))
		{
			return _mod.TownCampCentre(site) ?? vector;
		}
		if (_centres.TryGetValue(site.Id, out var value))
		{
			return value;
		}
		if (_held.TryGetValue(site.Id, out (Vector3, int) value2) && Game.GameTime < value2.Item2)
		{
			return value2.Item1;
		}
		float num = default(float);
		if (!MISC.GET_GROUND_Z_FOR_3D_COORD(vector.X, vector.Y, site.Z + 3f, &num, p4: false))
		{
			STREAMING.REQUEST_COLLISION_AT_COORD(vector.X, vector.Y, vector.Z);
			_held[site.Id] = (vector, Game.GameTime + 120000);
			return vector;
		}
		Vector3? vector2 = Try(vector);
		int num2 = 0;
		while (!vector2.HasValue && num2 < 4)
		{
			float num3 = (new float[4] { 10f, 16f, 22f, 30f })[num2];
			for (int i = 0; i < 12; i++)
			{
				if (vector2.HasValue)
				{
					break;
				}
				double num4 = (double)i * Math.PI / 6.0 + (double)num2 * 0.26;
				vector2 = Try(vector + new Vector3((float)Math.Cos(num4) * num3, (float)Math.Sin(num4) * num3, 0f));
			}
			num2++;
		}
		Vector3 vector3 = vector2 ?? vector;
		if (vector2.HasValue)
		{
			_centres[site.Id] = vector3;
		}
		if (!vector2.HasValue)
		{
			_mod.GrudgeLog("CAMPKIT " + site.Id + ": no open ground within 30 m - the site as it is");
		}
		else if (vector3.DistanceTo2D(vector) > 1f)
		{
			_mod.GrudgeLog($"CAMPKIT {site.Id}: its fire moved {vector3.DistanceTo2D(vector):0} m to open ground");
		}
		return vector3;
		unsafe Vector3? Try(Vector3 c)
		{
			float num5 = default(float);
			if (!MISC.GET_GROUND_Z_FOR_3D_COORD(c.X, c.Y, site.Z + 3f, &num5, p4: false))
			{
				return null;
			}
			if (Math.Abs(num5 - site.Z) > 4f)
			{
				return null;
			}
			Vector3 vector4 = new Vector3(c.X, c.Y, num5);
			if (!NemesisMod.Outdoors(vector4) || !NemesisMod.Dry(vector4))
			{
				return null;
			}
			return vector4;
		}
	}

	internal static List<(Vector3 At, float R)> Plan(CampLook look, Vector3 centre, int seed, bool stores, Func<KitPiece, Vector3, float, bool> place)
	{
		List<(Vector3 At, float R)> spots = new List<(Vector3, float)>();
		Random rng = new Random(seed);
		float a0 = (float)(rng.NextDouble() * Math.PI * 2.0);
		Vector3 dir = new Vector3((float)Math.Cos(a0), (float)Math.Sin(a0), 0f);
		Vector3 perp = new Vector3(0f - dir.Y, dir.X, 0f);
		spots.Add((centre, look.Fire ? 1.3f : 0.8f));
		List<(KitPiece, int)> list = (from p in look.Pieces
			where (look.Fire || p.Piece != "wood") && (stores || !Store(p.Piece))
			select (p: p, n: (p.Chance >= 100 || rng.Next(100) < p.Chance) ? p.Count : 0)).ToList();
		Dictionary<KitZone, int> dictionary = new Dictionary<KitZone, int>();
		foreach (var (kitPiece, num) in list)
		{
			dictionary[kitPiece.Zone] = (dictionary.TryGetValue(kitPiece.Zone, out var value) ? value : 0) + num;
		}
		Dictionary<KitZone, int> dictionary2 = new Dictionary<KitZone, int>();
		foreach (var item3 in list)
		{
			KitPiece item = item3.Item1;
			int item2 = item3.Item2;
			for (int num2 = 0; num2 < item2; num2++)
			{
				int value2;
				int num3 = (dictionary2.TryGetValue(item.Zone, out value2) ? value2 : 0);
				dictionary2[item.Zone] = num3 + 1;
				Vector3 vector = Spot(item.Zone, num3, Math.Max(1, dictionary[item.Zone]));
				if (!Free(vector, item.Size))
				{
					double num4 = Math.Atan2(vector.Y - centre.Y, vector.X - centre.X);
					float num5 = vector.DistanceTo2D(centre);
					for (int num6 = 1; num6 <= 10; num6++)
					{
						if (Free(vector, item.Size))
						{
							break;
						}
						vector = Polar(num4 + 0.4 * (double)((num6 + 1) / 2) * (double)((num6 % 2 != 0) ? 1 : (-1)), num5 + 0.3f * (float)num6);
					}
					if (!Free(vector, item.Size))
					{
						continue;
					}
				}
				if (place(item, vector, item.Faces ? Facing(vector) : ((float)(rng.NextDouble() * 360.0))))
				{
					spots.Add((vector, item.Size));
				}
			}
		}
		return spots;
		float Facing(Vector3 at)
		{
			return (float)(Math.Atan2(0f - (centre.X - at.X), centre.Y - at.Y) * 180.0 / Math.PI);
		}
		bool Free(Vector3 p, float r)
		{
			return spots.All(((Vector3 At, float R) s) => s.At.DistanceTo2D(p) >= s.R + r);
		}
		Vector3 Polar(double ang, float r)
		{
			return centre + new Vector3((float)Math.Cos(ang) * r, (float)Math.Sin(ang) * r, 0f);
		}
		static float R(KitZone z)
		{
			return z switch
			{
				KitZone.Outer => 8.8f, 
				KitZone.Mid => 6f, 
				KitZone.Inner => 3.4f, 
				_ => 6.5f, 
			};
		}
		Vector3 Spot(KitZone z, int k, int n)
		{
			float num7 = (float)(rng.NextDouble() - 0.5) * 0.5f;
			switch (look.Shape)
			{
			case KitShape.Row:
				switch (z)
				{
				case KitZone.Outer:
					return centre + perp * R(z) + dir * (((float)k - (float)(n - 1) / 2f) * 5.5f);
				case KitZone.Line:
					return centre - perp * R(z) + dir * (((float)k - (float)(n - 1) / 2f) * 2.4f);
				}
				break;
			case KitShape.Cluster:
				if (z != KitZone.Inner)
				{
					return Polar(a0 - 1.3f + ((float)k + 0.5f) * 2.6f / (float)n, R(z) * 0.85f + num7);
				}
				break;
			case KitShape.Scatter:
				return Polar(rng.NextDouble() * Math.PI * 2.0, R(z) * 1.4f + (float)rng.NextDouble() * 2.5f);
			}
			double num8 = z switch
			{
				KitZone.Mid => Math.PI / (double)n, 
				KitZone.Inner => 0.0, 
				_ => 0.5, 
			};
			return Polar((double)a0 + num8 + (double)k * Math.PI * 2.0 / (double)n, R(z) + num7);
		}
		static bool Store(string piece)
		{
			if (!(piece == "crates") && !(piece == "barrel"))
			{
				return piece == "crate stack";
			}
			return true;
		}
	}

	internal void Spawn(CampSite site, string gang, bool supply)
	{
		if (site == null || site.Imap != 0 || site.Hideout || _kits.ContainsKey(site.Id))
		{
			return;
		}
		List<int> list = new List<int>();
		_kits[site.Id] = list;
		Vector3 centre = Centre(site);
		Lay(site.Id, centre, Seeds.Stable("campkit/" + site.Id + "/" + gang), gang, !supply, list);
		if (!supply)
		{
			try
			{
				SpawnRig(site.Id, gang, Centre(site));
			}
			catch (Exception ex)
			{
				_mod.GrudgeLog("CAMPKIT rig failed: " + ex.Message);
			}
		}
		_mod.GrudgeLog(string.Format("CAMPKIT {0}: {1} props laid out ({2})", site.Id, list.Count, CampLooks.ContainsKey(gang ?? "") ? (NemesisMod.ShortOutfit(Gangs.ById(gang)) + " look") : "plain look"));
	}

	internal int LayAt(string key, Vector3 centre, int seed, string gang)
	{
		List<int> list = new List<int>();
		_kits[key] = list;
		Lay(key, centre, seed, gang, stores: true, list);
		return list.Count;
	}

	private void Lay(string key, Vector3 centre, int seed, string gang, bool stores, List<int> made)
	{
		CampLook campLook = LookOf(gang);
		if (campLook.Fire)
		{
			NemesisMod mod = _mod;
			Vector3 vector = NemesisMod.GroundOnSpot(centre);
			int num = mod.MakeFire((vector != Vector3.Zero) ? vector : centre);
			if (num != 0)
			{
				made.Add(num);
			}
		}
		_spots[key] = Plan(campLook, centre, seed, stores, delegate(KitPiece p, Vector3 at, float heading)
		{
			int num2 = Prop(p.Piece, p.Names, at, heading);
			if (num2 != 0)
			{
				made.Add(num2);
			}
			return num2 != 0;
		});
	}

	private int Prop(string piece, string[] names, Vector3 at, float heading)
	{
		if (_logged.Contains(piece))
		{
			return 0;
		}
		Vector3 vector = NemesisMod.GroundOnSpot(at);
		if (vector == Vector3.Zero)
		{
			return 0;
		}
		string value;
		string[] array = ((!_model.TryGetValue(piece, out value)) ? names : new string[1] { value });
		foreach (string text in array)
		{
			Model m = new Model(text);
			if (!m.IsValid || !m.IsInCdImage || !_mod.LoadModelSafe(m, 1500))
			{
				continue;
			}
			Crumbs.Native("camp kit: create " + piece);
			int num = OBJECT.CREATE_OBJECT((uint)m.Hash, vector.X, vector.Y, vector.Z, isNetwork: false, bScriptHostObj: false, dynamic: false, p7: false, p8: true);
			STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED((uint)m.Hash);
			if (num != 0)
			{
				ENTITY.SET_ENTITY_HEADING(num, heading);
				OBJECT.PLACE_OBJECT_ON_GROUND_PROPERLY(num, p1: false);
				ENTITY.FREEZE_ENTITY_POSITION(num, toggle: true);
				ENTITY.SET_ENTITY_AS_MISSION_ENTITY(num, p1: true, p2: true);
				if (!_model.ContainsKey(piece))
				{
					_model[piece] = text;
					_mod.GrudgeLog("CAMPKIT " + piece + ": " + text);
				}
				return num;
			}
		}
		if (_logged.Add(piece))
		{
			_mod.GrudgeLog("CAMPKIT " + piece + ": none of " + ((names.Length <= 6) ? string.Join(", ", names) : $"{names.Length} names ({names[0]}, {names[1]}...)") + " could be made - left out");
		}
		return 0;
	}

	private void SpawnRig(string siteId, string gang, Vector3 centre)
	{
		string[] array;
		int num;
		int num2;
		if (gang != null && CampRigs.TryGetValue(gang, out (string[], int, int) value))
		{
			(array, num, num2) = value;
		}
		else
		{
			(array, num, num2) = PlainRig;
		}
		if (array.Length == 0)
		{
			num = 0;
		}
		if (num == 0 && num2 == 0)
		{
			return;
		}
		Rig rig = new Rig();
		_rigs[siteId] = rig;
		Random random = new Random(Seeds.Stable("camprig/" + siteId + "/" + gang));
		double num3 = random.NextDouble() * Math.PI * 2.0;
		List<string> list = new List<string>();
		for (int i = 0; i < num; i++)
		{
			Vector3 vector = Spot(num3 + (double)i * 0.9, 12.5f, 3.2f);
			if (!(vector != Vector3.Zero))
			{
				continue;
			}
			foreach (string item in array.Skip(i % array.Length).Concat(array.Take(i % array.Length)))
			{
				uint num4 = MISC.GET_HASH_KEY(item);
				if (!STREAMING.IS_MODEL_IN_CDIMAGE(num4) || !STREAMING.IS_MODEL_A_VEHICLE(num4) || !_mod.LoadModelSafe(new Model((int)num4), 1500))
				{
					continue;
				}
				Crumbs.Native("camp rig wagon");
				Vehicle vehicle = NemesisMod.SpawnVehicle((VehicleHash)num4, vector + new Vector3(0f, 0f, 0.3f), NemesisMod.HeadingTo(vector, centre) + 90f);
				STREAMING.SET_MODEL_AS_NO_LONGER_NEEDED(num4);
				if (!(vehicle == null))
				{
					NemesisMod.MakeOurs(vehicle.Handle);
					VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle.Handle, p1: false);
					rig.Wagons.Add(vehicle);
					list.Add(item);
					if (_spots.TryGetValue(siteId, out List<(Vector3, float)> value2))
					{
						value2.Add((vector, 3.2f));
					}
					break;
				}
			}
		}
		int num5 = 0;
		for (int j = 0; j < num2; j++)
		{
			Vector3 vector2 = Spot(num3 + Math.PI + (double)j * 0.2, 13f, 1.6f);
			if (vector2 == Vector3.Zero)
			{
				continue;
			}
			Crumbs.Native("camp rig horse");
			Ped ped = NemesisMod.SpawnPed(NemesisMod.RiderHorses[random.Next(NemesisMod.RiderHorses.Length)], vector2, NemesisMod.HeadingTo(vector2, centre) + 90f);
			if (!(ped == null))
			{
				NemesisMod.MakeOurs(ped.Handle);
				TASK.TASK_STAND_STILL(ped.Handle, -1);
				rig.Horses.Add(ped);
				num5++;
				if (_spots.TryGetValue(siteId, out List<(Vector3, float)> value3))
				{
					value3.Add((vector2, 1.6f));
				}
			}
		}
		_mod.GrudgeLog(string.Format("CAMPKIT {0}: {1}, {2} horse(s) tied up", siteId, (list.Count > 0) ? (string.Join(" and ", list.Select((string m) => "a " + m)) + ((list.Count < num) ? $" ({num - list.Count} more: no room / no model)" : "")) : ((num > 0) ? "no wagon (no room / no model)" : "no wagon"), num5));
		Vector3 Spot(double a, float r0, float room)
		{
			for (int k = 0; k < 12; k++)
			{
				double num6 = a + (double)k * 0.35 * (double)((k % 2 == 0) ? 1 : (-1));
				Vector3 vector3 = NemesisMod.GroundOnSpot(centre + new Vector3((float)Math.Cos(num6) * r0, (float)Math.Sin(num6) * r0, 0f));
				if (!(vector3 == Vector3.Zero) && NemesisMod.Dry(vector3) && NemesisMod.OpenGround(vector3, room) && ClearOf(siteId, vector3, room) && !(Math.Abs(vector3.Z - centre.Z) > 3f))
				{
					return vector3;
				}
			}
			return Vector3.Zero;
		}
	}

	internal unsafe void Despawn(string key)
	{
		try
		{
			DespawnRig(key);
		}
		catch (Exception ex)
		{
			_mod.GrudgeLog("CAMPKIT rig delete failed: " + ex.Message);
		}
		if (key == null || !_kits.TryGetValue(key, out var value))
		{
			return;
		}
		_kits.Remove(key);
		_spots.Remove(key);
		foreach (int item in value)
		{
			int current = item;
			if (current != 0 && ENTITY.DOES_ENTITY_EXIST(current))
			{
				Crumbs.Native("camp kit: delete");
				OBJECT.DELETE_OBJECT(&current);
			}
		}
	}

	private unsafe void DespawnRig(string siteId)
	{
		if (siteId == null || !_rigs.TryGetValue(siteId, out var value))
		{
			return;
		}
		_rigs.Remove(siteId);
		Ped character = Game.Player.Character;
		foreach (Vehicle wagon in value.Wagons)
		{
			if (wagon != null && wagon.Exists())
			{
				if (!PED.IS_PED_IN_VEHICLE(character.Handle, wagon.Handle, atGetIn: false) && _mod.SafeToDeleteVehicle(wagon))
				{
					wagon.Delete();
					continue;
				}
				int handle = wagon.Handle;
				NemesisMod.ReleaseIfOurs(&handle);
			}
		}
		foreach (Ped horse in value.Horses)
		{
			if (horse != null && horse.Exists())
			{
				if ((!PED.IS_PED_ON_MOUNT(character.Handle) || PED.GET_MOUNT(character.Handle) != horse.Handle) && !horse.IsDead && _mod.SafeToDelete(horse))
				{
					horse.Delete();
					continue;
				}
				int handle2 = horse.Handle;
				NemesisMod.ReleaseIfOurs(&handle2);
			}
		}
	}
}
