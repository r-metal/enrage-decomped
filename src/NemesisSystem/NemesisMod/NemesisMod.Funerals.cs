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
	private sealed class LiveFuneral
	{
		public bool PropsPending;

		public int Arrived;

		public int WalkStartedAt;

		public readonly Dictionary<int, (Vector3 spot, int since, Vector3 last, int movedAt)> Walking = new Dictionary<int, (Vector3, int, Vector3, int)>();

		public readonly Dictionary<int, (Vector3 spot, int until)> Turning = new Dictionary<int, (Vector3, int)>();

		public Plan Plan;

		public Vector3 Grave;

		public List<Ped> Men = new List<Ped>();

		public bool YouShotFirst;

		public bool Fighting;

		public bool Watched;

		public bool Respects;

		public bool Ceasefire;

		public int RespectMs;

		public int LastTick;

		public List<int> Props = new List<int>();

		public Vehicle Wagon;

		public Ped Body;

		public float Heading;

		public float Ring = 2f;
	}

	private LiveFuneral _funeral;

	private string _coffinModel;

	private string _graveModel;

	private static readonly string[] CoffinModels = new string[4] { "p_coffin01x", "p_coffin02x", "p_coffin03x", "p_coffinlid01x" };

	private static readonly string[] GraveModels = new string[5] { "p_gravemarker01x", "p_gravemarker02x", "p_gravecross01x", "p_gravestone01x", "p_grave06x" };

	private static readonly (string town, Vector3 at)[] Cemeteries = new(string, Vector3)[9]
	{
		("Valentine", new Vector3(-238f, 822f, 123.5f)),
		("Rhodes", new Vector3(1285f, -1225f, 81f)),
		("Blackwater", new Vector3(-997f, -1195f, 58.7f)),
		("Strawberry", new Vector3(-1761f, -241.5f, 182.6f)),
		("Tumbleweed", new Vector3(-5453f, -2910f, 0.9f)),
		("Armadillo", new Vector3(-3326f, -2866f, -6f)),
		("Van Horn", new Vector3(2900f, 495f, 66f)),
		("Colter", new Vector3(-1312f, 2468f, 310f)),
		(null, new Vector3(2134f, 145f, 75.5f))
	};

	private const int FuneralMaxDead = 3;

	private const int FuneralHours = 5;

	private const int FuneralDaysAhead = 3;

	private const float FuneralReach = 3500f;

	private const float LonerFuneralReach = 4000f;

	private const float SameCemeteryM = 40f;

	private const ulong TurnPedToFaceCoordFn = 2151193443362375025uL;

	private List<Vector3> _plots;

	private static readonly Vector3[] MarkedPlots = new Vector3[2]
	{
		new Vector3(1281.9f, -1210.4f, 81.3f),
		new Vector3(-229.1f, 826f, 123.3f)
	};

	private const float RunnerGoneAt = 200f;

	private readonly Dictionary<int, Runner> _runners = new Dictionary<int, Runner>();

	private List<(string town, Vector3 at)> AllCemeteries()
	{
		List<(string, Vector3, bool)> list = Cemeteries.Select(((string town, Vector3 at) c) => (town: c.town, at: c.at, known: true)).ToList();
		foreach (Vector3 q in Plots)
		{
			if (!list.Any<(string, Vector3, bool)>(((string town, Vector3 at, bool known) c) => c.at.DistanceTo2D(q) < (c.known ? 200f : 90f)))
			{
				list.Add((TownAt(q), q, false));
			}
		}
		return list.Select<(string, Vector3, bool), (string, Vector3)>(((string town, Vector3 at, bool known) c) => (town: c.town, at: c.at)).ToList();
	}

	private Vector3? NearestCemetery(Vector3 from, float within)
	{
		(string, Vector3) tuple = (from c in AllCemeteries()
			orderby c.at.DistanceTo2D(@from)
			select c).First();
		if (!(tuple.Item2.DistanceTo2D(from) <= within))
		{
			return null;
		}
		return tuple.Item2;
	}

	private string CemeteryLabel(Vector3 c)
	{
		(string, Vector3) tuple = (from x in AllCemeteries()
			orderby x.at.DistanceTo2D(c)
			select x).First();
		if (tuple.Item1 == null)
		{
			return "the graveyard in " + GetZoneName(c);
		}
		return "the " + tuple.Item1 + " cemetery";
	}

	internal List<string> FuneralDead(Plan p)
	{
		return (from x in new string[1] { p.Dead }.Concat(p.MoreDead ?? new List<string>())
			where x != null
			select x).ToList();
	}

	private static int RankOfId(IEnumerable<NemesisRecord> roster, string id)
	{
		return roster.FirstOrDefault((NemesisRecord n) => n.Id == id)?.Rank ?? 0;
	}

	internal string FuneralNames(Plan p)
	{
		List<string> list = (from id in FuneralDead(p)
			select Roster.FirstOrDefault((NemesisRecord n) => n.Id == id)?.Name into n
			where n != null
			select n).ToList();
		if (list.Count != 0)
		{
			return JoinAnd(list);
		}
		return "one of their own";
	}

	private void RetitleFuneral(Plan p)
	{
		p.Title = FuneralNames(p) + " laid to rest";
		DropPlanBlip(p);
	}

	private unsafe void DropPlanBlip(Plan p)
	{
		if (_planBlips.TryGetValue(p.Id, out var value))
		{
			Crumbs.Native("Plans blip remove");
			if (MAP.DOES_BLIP_EXIST(value))
			{
				MAP.REMOVE_BLIP(&value);
			}
			_planBlips.Remove(p.Id);
		}
	}

	private void AddFuneralDead(Plan p, NemesisRecord d)
	{
		if (p.MoreDead == null)
		{
			p.MoreDead = new List<string>();
		}
		if (d.Rank > RankOfId(Roster, p.Dead))
		{
			if (p.Dead != null)
			{
				p.MoreDead.Insert(0, p.Dead);
			}
			p.Dead = d.Id;
		}
		else
		{
			p.MoreDead.Add(d.Id);
		}
		p.Crew = Math.Min(6, p.Crew + 1);
		RetitleFuneral(p);
	}

	private void RemoveFuneralDead(Plan p, string id)
	{
		if (p.Dead == id)
		{
			p.Dead = p.MoreDead?.FirstOrDefault();
			p.MoreDead?.Remove(p.Dead);
		}
		else
		{
			p.MoreDead?.Remove(id);
		}
		RetitleFuneral(p);
	}

	private bool FitIntoFuneral(NemesisRecord d, string gangId, long now, string who, Vector3? near = null)
	{
		List<Plan> list = Plans.Where((Plan x) => x.Kind == "funeral" && !x.Done && x.Gang == gangId && (gangId != null || (near.HasValue && new Vector3(x.X, x.Y, x.Z).DistanceTo2D(near.Value) < 300f))).ToList();
		Plan plan = (from x in list
			where !x.Live && x.DueAt > now && FuneralDead(x).Count < 3
			orderby x.DueAt
			select x).FirstOrDefault();
		if (plan != null)
		{
			AddFuneralDead(plan, d);
			SaveWorld();
			GrudgeLog("FUNERAL " + d.Name + " (" + who + "): buried with the others - " + plan.Title + ", " + WhenText(plan.DueAt) + ", " + plan.Place);
			return true;
		}
		if (gangId == null || list.Count < 2)
		{
			return false;
		}
		(Plan plan, string id, int rank) low = (from t in list.Where((Plan x) => !x.Live && x.DueAt > now && !x.Known).SelectMany((Plan x) => from id in FuneralDead(x)
				select (plan: x, id: id, rank: RankOfId(Roster, id)))
			orderby t.rank
			select t).FirstOrDefault();
		if (low.plan != null && low.rank < d.Rank)
		{
			string text = Roster.FirstOrDefault((NemesisRecord n) => n.Id == low.id)?.Name ?? "a man";
			RemoveFuneralDead(low.plan, low.id);
			AddFuneralDead(low.plan, d);
			SaveWorld();
			GrudgeLog($"FUNERAL {d.Name} ({who}, rank {d.Rank}): takes {text}'s place (rank {low.rank}, a quiet grave now) - {low.plan.Title}, {WhenText(low.plan.DueAt)}");
			return true;
		}
		GrudgeLog("FUNERAL " + d.Name + " (" + who + "): the funerals are full of better men - a quiet grave");
		return true;
	}

	private void MakeFunerals(long now)
	{
		WorldState chronicle = Chronicle;
		if (!chronicle.FuneralsSeeded)
		{
			chronicle.FuneralsSeeded = true;
			foreach (NemesisRecord item3 in Roster.Where((NemesisRecord n) => !n.IsAlive))
			{
				item3.Buried = true;
			}
			SaveRoster();
			SaveWorld();
			return;
		}
		foreach (Plan item4 in Plans.Where((Plan x) => x.Kind == "funeral" && !x.Done && x.WindowMin < 300))
		{
			item4.WindowMin = 300;
		}
		SpaceFunerals(now);
		foreach (NemesisRecord d in Roster.Where((NemesisRecord n) => !n.IsAlive && !n.Buried && !n.Story && n.Fate == null && n.StoryGone == null).ToList())
		{
			d.Buried = true;
			SaveRoster();
			if (d.Gang == null)
			{
				try
				{
					LonerFuneral(d, now);
				}
				catch (Exception ex)
				{
					GrudgeLog("FUNERAL loner failed: " + ex.Message);
				}
				continue;
			}
			Gang gang = Gangs.ById(d.Gang);
			if (gang == null || gang.StoryOnly || IsDestroyed(gang) || gang.Id == "nightfolk")
			{
				continue;
			}
			List<(NemesisRecord who, string rel)> list = (from k in KinOf(d)
				where k.who.IsAlive && !k.who.PresumedDead && k.who.CanHunt
				select k).ToList();
			if ((d.Rank < 2 && list.Count == 0) || FitIntoFuneral(d, gang.Id, now, gang.Name))
			{
				continue;
			}
			object obj = (from k in list
				where k.who.Gang == d.Gang
				select k.who).FirstOrDefault();
			if (obj == null)
			{
				obj = list.Select<(NemesisRecord, string), NemesisRecord>(((NemesisRecord who, string rel) k) => k.who).FirstOrDefault();
				if (obj == null)
				{
					NemesisRecord nemesisRecord = BossOf(d);
					obj = ((nemesisRecord != null && nemesisRecord.IsAlive && nemesisRecord.CanHunt && !nemesisRecord.PresumedDead) ? nemesisRecord : null) ?? (from n in Roster
						where n.Gang == d.Gang && n.IsAlive && n.CanHunt && !n.PresumedDead && n.Rank >= 2
						orderby n.Rank descending
						select n).FirstOrDefault();
				}
			}
			NemesisRecord mourner = (NemesisRecord)obj;
			if (mourner == null)
			{
				GrudgeLog("FUNERAL " + d.Name + ": nobody left to bury him");
				continue;
			}
			Vector3 vector = Vector3.Zero;
			string text = null;
			long num = -1L;
			CampSite campSite = CampOf(d);
			if (gang != Gangs.Wapiti)
			{
				Vector3 centre;
				float radius;
				Vector3 vector2 = ((campSite != null) ? SitePos(campSite) : (from t in JobTownsOf(gang)
					select (!TownCore(t, out centre, out radius)) ? Vector3.Zero : centre).FirstOrDefault((Vector3 v) => v != Vector3.Zero));
				if (vector2 != Vector3.Zero && NearestCemetery(vector2, 3500f).HasValue)
				{
					(Vector3, long)? tuple = FreeFuneralDay(vector2, 3500f, now);
					if (!tuple.HasValue)
					{
						GrudgeLog($"FUNERAL {d.Name} ({gang.Name}): every graveyard near is taken for the next {3} days - a quiet grave");
						continue;
					}
					Vector3 item = tuple.Value.Item1;
					num = tuple.Value.Item2;
					double num2 = _rng.NextDouble() * Math.PI * 2.0;
					float num3 = 2f + (float)_rng.NextDouble() * 5f;
					vector = item + new Vector3((float)Math.Cos(num2) * num3, (float)Math.Sin(num2) * num3, 0f);
					text = "at " + CemeteryLabel(item);
				}
			}
			if (vector != Vector3.Zero)
			{
				campSite = null;
			}
			if (campSite != null && SiteOwner(campSite) == gang.Id)
			{
				for (int num4 = 0; num4 < 6; num4++)
				{
					if (!(vector == Vector3.Zero))
					{
						break;
					}
					double num5 = _rng.NextDouble() * Math.PI * 2.0;
					float num6 = 50f + (float)_rng.NextDouble() * 40f;
					Vector3 vector3 = SitePos(campSite) + new Vector3((float)Math.Cos(num5) * num6, (float)Math.Sin(num5) * num6, 0f);
					if (OnLand(vector3))
					{
						vector = vector3;
					}
				}
				if (vector != Vector3.Zero)
				{
					text = "by " + campSite.Label;
				}
			}
			if (vector == Vector3.Zero && text == null)
			{
				string[] array = JobTownsOf(gang);
				string text2 = ((array.Length != 0) ? array[_rng.Next(array.Length)] : null);
				if (text2 != null)
				{
					vector = JobSpot(text2);
					text = "outside " + text2;
				}
			}
			if (vector == Vector3.Zero)
			{
				GrudgeLog("FUNERAL " + d.Name + ": no ground to bury him in");
				continue;
			}
			if (num < 0)
			{
				num = Midnight(now) + 1440 + (long)(10 + _rng.Next(6)) * 60L;
			}
			string item2 = list.FirstOrDefault<(NemesisRecord, string)>(((NemesisRecord who, string rel) k) => k.who == mourner).Item2;
			Plan plan = new Plan
			{
				Id = NewId(10),
				Gang = gang.Id,
				Kind = "funeral",
				Dead = d.Id,
				LeaderId = mourner.Id,
				Title = d.Name + " laid to rest",
				Place = text,
				X = vector.X,
				Y = vector.Y,
				Z = vector.Z,
				DueAt = num,
				WindowMin = 300,
				Crew = 3 + _rng.Next(3)
			};
			Plans.Add(plan);
			SaveWorld();
			GrudgeLog(string.Format("FUNERAL {0} ({1}): {2}, {3} - {4}{5} and {6} men", d.Name, gang.Name, WhenText(num), text, mourner.Name, (item2 != null) ? (" (" + Traits.Word(item2, female: false) + ")") : "", plan.Crew));
		}
	}

	private long Midnight(long minutes)
	{
		DateTime dateTime = FromGameMinutes(minutes);
		return minutes - (dateTime.Hour * 60 + dateTime.Minute);
	}

	private Vector3? CemeteryOf(Plan p)
	{
		Vector3 position = new Vector3(p.X, p.Y, p.Z);
		foreach (var item2 in AllCemeteries())
		{
			Vector3 item = item2.at;
			if (item.DistanceTo2D(position) < 40f)
			{
				return item2.at;
			}
		}
		return null;
	}

	private static bool FuneralHeld(Plan x)
	{
		if (x.Kind == "funeral")
		{
			if (x.Done)
			{
				if (x.Outcome != "he's gone")
				{
					return !(x.Outcome ?? "").StartsWith("a quiet grave");
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private bool FuneralThatDay(Vector3 cem, long midnight, Plan except)
	{
		return Plans.Any((Plan x) => FuneralHeld(x) && x != except && new Vector3(x.X, x.Y, x.Z).DistanceTo2D(cem) < 40f && x.DueAt >= midnight && x.DueAt < midnight + 1440);
	}

	private (Vector3 cem, long due)? FreeFuneralDay(Vector3 from, float reach, long now, Plan except = null)
	{
		long num = Midnight(now);
		foreach (Vector3 item in from c in AllCemeteries()
			select c.at into c
			where c.DistanceTo2D(@from) <= reach
			orderby c.DistanceTo2D(@from)
			select c)
		{
			for (int num2 = 1; num2 <= 3; num2++)
			{
				long num3 = num + (long)num2 * 1440L;
				if (!FuneralThatDay(item, num3, except))
				{
					return (item, num3 + (long)(10 + _rng.Next(6)) * 60L);
				}
			}
		}
		return null;
	}

	private void SpaceFunerals(long now)
	{
		List<Plan> list = Plans.Where((Plan x) => x.Kind == "funeral" && !x.Done).ToList();
		if (list.Count == 0)
		{
			return;
		}
		List<(Vector3, long)> list2 = (from x in Plans
			where x.Done && FuneralHeld(x)
			select (cem: CemeteryOf(x), midnight: Midnight(x.DueAt)) into t
			where t.cem.HasValue
			select (cem: t.cem.Value, midnight: t.midnight)).ToList();
		bool flag = false;
		foreach (Plan item2 in from x in list
			orderby (!x.Live && now < x.DueAt) ? 1 : 0, (!x.Known) ? 1 : 0, x.DueAt
			select x)
		{
			Vector3? vector = CemeteryOf(item2);
			if (!vector.HasValue)
			{
				continue;
			}
			Vector3 cem = vector.GetValueOrDefault();
			long midnight = Midnight(item2.DueAt);
			if (!list2.Any<(Vector3, long)>(((Vector3 cem, long midnight) t) => t.midnight == midnight && t.cem.DistanceTo2D(cem) < 1f) || item2.Live || now >= item2.DueAt)
			{
				list2.Add((cem, midnight));
				continue;
			}
			string text = WhenText(item2.DueAt) + ", " + item2.Place;
			(Vector3, long)? tuple = FreeFuneralDay(cem, (item2.Gang == null) ? 4000f : 3500f, now, item2);
			flag = true;
			if (!tuple.HasValue)
			{
				item2.Done = true;
				item2.Outcome = "a quiet grave (the graveyard was taken)";
				DropPlanBlip(item2);
				GrudgeLog($"FUNERAL {item2.Title}: another funeral has {CemeteryLabel(cem)} that day, and every graveyard near is taken for {3} days - a quiet grave");
				if (item2.Known)
				{
					ReplyNotice(item2.Title.TrimEnd('.') + " won't be at " + CemeteryLabel(cem) + " after all. They buried him quietly.");
				}
				continue;
			}
			Vector3 item = tuple.Value.Item1;
			if (item.DistanceTo2D(cem) > 1f)
			{
				double num = _rng.NextDouble() * Math.PI * 2.0;
				float num2 = 2f + (float)_rng.NextDouble() * 5f;
				Vector3 vector2 = item + new Vector3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, 0f);
				item2.X = vector2.X;
				item2.Y = vector2.Y;
				item2.Z = vector2.Z;
				item2.PlotSet = false;
				item2.Place = "at " + CemeteryLabel(item);
			}
			item2.DueAt = tuple.Value.Item2;
			DropPlanBlip(item2);
			list2.Add((item, Midnight(item2.DueAt)));
			GrudgeLog("FUNERAL " + item2.Title + ": another funeral has " + CemeteryLabel(cem) + " that day - moved from " + text + " to " + WhenText(item2.DueAt) + ", " + item2.Place);
			if (item2.Known)
			{
				ReplyNotice(item2.Title.TrimEnd('.') + ": moved to " + WhenText(item2.DueAt) + ", " + item2.Place + ".");
			}
		}
		if (flag)
		{
			SaveWorld();
		}
	}

	private void LonerFuneral(NemesisRecord d, long now)
	{
		Vector3 origin = new Vector3(d.OriginX, d.OriginY, d.OriginZ);
		if (origin == Vector3.Zero)
		{
			GrudgeLog("FUNERAL " + d.Name + " (no outfit): nowhere known to bury him");
			return;
		}
		Vector3? near = NearestCemetery(origin, 4000f);
		if (near.HasValue && FitIntoFuneral(d, null, now, "no outfit", near))
		{
			return;
		}
		List<(NemesisRecord, string)> source = (from k in KinOf(d)
			where Free(k.who)
			select k).ToList();
		NemesisRecord mourner = source.Select<(NemesisRecord, string), NemesisRecord>(((NemesisRecord who, string rel) k) => k.who).FirstOrDefault() ?? FriendsOf(d).FirstOrDefault(Free) ?? (from _ in Roster
			where Free(_) && _ != d && new Vector3(_.OriginX, _.OriginY, _.OriginZ).DistanceTo2D(origin) < 1500f
			orderby _rng.Next()
			select _).FirstOrDefault();
		if (mourner == null)
		{
			GrudgeLog("FUNERAL " + d.Name + " (no outfit): nobody on the roster to mourn him - buried without a word");
			return;
		}
		if (!NearestCemetery(origin, 4000f).HasValue)
		{
			GrudgeLog("FUNERAL " + d.Name + " (no outfit): no graveyard near " + (d.KillLocation ?? "where he lived"));
			return;
		}
		(Vector3, long)? tuple = FreeFuneralDay(origin, 4000f, now);
		if (!tuple.HasValue)
		{
			GrudgeLog($"FUNERAL {d.Name} (no outfit): every graveyard near is taken for the next {3} days - a quiet grave");
			return;
		}
		Vector3 item = tuple.Value.Item1;
		long item2 = tuple.Value.Item2;
		double num = _rng.NextDouble() * Math.PI * 2.0;
		float num2 = 2f + (float)_rng.NextDouble() * 5f;
		Vector3 vector = item + new Vector3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, 0f);
		Plan plan = new Plan
		{
			Id = NewId(10),
			Gang = null,
			Kind = "funeral",
			Dead = d.Id,
			LeaderId = mourner.Id,
			Title = d.Name + " laid to rest",
			Place = "at " + CemeteryLabel(item),
			X = vector.X,
			Y = vector.Y,
			Z = vector.Z,
			DueAt = item2,
			WindowMin = 300,
			Crew = 1 + _rng.Next(3)
		};
		Plans.Add(plan);
		SaveWorld();
		string item3 = source.FirstOrDefault<(NemesisRecord, string)>(((NemesisRecord who, string rel) k) => k.who == mourner).Item2;
		GrudgeLog(string.Format("FUNERAL {0} (no outfit): {1}, {2} - {3}{4} and {5} others", d.Name, WhenText(item2), plan.Place, mourner.Name, (item3 != null) ? (" (" + Traits.Word(item3, female: false) + ")") : "", plan.Crew));
		static bool Free(NemesisRecord n)
		{
			if (n != null && n.IsAlive && !n.PresumedDead && n.CanHunt)
			{
				return n.Gang == null;
			}
			return false;
		}
	}

	private unsafe void CleanupFuneral()
	{
		LiveFuneral funeral = _funeral;
		_funeral = null;
		if (funeral == null)
		{
			return;
		}
		foreach (int prop in funeral.Props)
		{
			if (ENTITY.DOES_ENTITY_EXIST(prop))
			{
				int num = prop;
				OBJECT.DELETE_OBJECT(&num);
			}
		}
		funeral.Props.Clear();
		if (funeral.Body != null && funeral.Body.Exists())
		{
			DeleteOrRelease(funeral.Body);
		}
		if (funeral.Wagon != null && funeral.Wagon.Exists())
		{
			int handle = funeral.Wagon.Handle;
			VEHICLE.DELETE_VEHICLE(&handle);
		}
	}

	private unsafe void DropFuneralProps(LiveFuneral f, Ped player)
	{
		if (f == null)
		{
			return;
		}
		foreach (int prop in f.Props)
		{
			if (ENTITY.DOES_ENTITY_EXIST(prop))
			{
				int num = prop;
				if (HiddenFromView(player, ENTITY.GET_ENTITY_COORDS(prop, alive: true, realCoords: false)))
				{
					OBJECT.DELETE_OBJECT(&num);
				}
				else
				{
					ENTITY.SET_ENTITY_AS_NO_LONGER_NEEDED(&num);
				}
			}
		}
		f.Props.Clear();
		if (f.Wagon != null && f.Wagon.Exists())
		{
			int handle = f.Wagon.Handle;
			if (HiddenFromView(player, f.Wagon.Position))
			{
				VEHICLE.DELETE_VEHICLE(&handle);
			}
			else
			{
				ReleaseIfOurs(&handle);
			}
		}
		f.Wagon = null;
		if (f.Body != null && f.Body.Exists())
		{
			DeleteOrRelease(f.Body);
		}
		f.Body = null;
	}

	private void FuneralStarted(Plan p, Ped player, Vector3 at, float walkFrom = 0f)
	{
		DropFuneralProps(_funeral, player);
		_funeral = new LiveFuneral
		{
			Plan = p,
			LastTick = Game.GameTime
		};
		if (_activeNemesisPed != null)
		{
			_funeral.Men.Add(_activeNemesisPed);
		}
		_funeral.Men.AddRange(_followers.Where((Ped f) => f != null && f.Exists()));
		_funeral.Grave = at;
		_funeral.Heading = (float)_rng.NextDouble() * 360f;
		int num = Math.Max(1, Math.Min(3, FuneralDead(p).Count));
		_funeral.Ring = ((num > 1) ? 3.4f : 2f);
		if (walkFrom > 0f)
		{
			_funeral.PropsPending = true;
			_funeral.WalkStartedAt = Game.GameTime;
			StandRoundCasket(walk: true);
			GrudgeLog($"FUNERAL {p.Title}: you're at the graves - {_funeral.Men.Count} mourners walk in from {walkFrom:0} m out of your sight, to {at.X:0.0},{at.Y:0.0},{at.Z:0.0}");
		}
		else
		{
			var (num2, num3) = PutFuneralProps(_funeral);
			StandRoundCasket();
			GrudgeLog(string.Format("FUNERAL {0} in front of you - {1} mourners round {2} {3} ({4}), {5} marker(s) ({6}), at {7:0.0},{8:0.0},{9:0.0}", p.Title, _funeral.Men.Count, num2, (num2 == 1) ? "casket" : "caskets", _coffinModel ?? "none", num3, _graveModel ?? "none", at.X, at.Y, at.Z));
		}
	}

	private (int coffins, int markers) PutFuneralProps(LiveFuneral f)
	{
		f.PropsPending = false;
		double num = (double)f.Heading * Math.PI / 180.0;
		Vector3 vector = new Vector3(0f - (float)Math.Sin(num), (float)Math.Cos(num), 0f);
		Vector3 vector2 = new Vector3(vector.Y, 0f - vector.X, 0f);
		int num2 = Math.Max(1, Math.Min(3, FuneralDead(f.Plan).Count));
		int num3 = 0;
		int num4 = 0;
		for (int i = 0; i < num2; i++)
		{
			float num5 = ((float)i - (float)(num2 - 1) / 2f) * 1.5f;
			Vector3 vector3 = ((i == 0 && num2 == 1) ? f.Grave : GroundOnSpot(f.Grave + vector2 * num5));
			if (vector3 == Vector3.Zero)
			{
				vector3 = f.Grave + vector2 * num5;
			}
			int num6 = MakeCheckpointProp(CoffinModels, vector3, f.Heading, ref _coffinModel);
			if (num6 != 0)
			{
				f.Props.Add(num6);
				num3++;
			}
			int num7 = MakeCheckpointProp(GraveModels, GroundOnSpot(vector3 + vector * 1.6f), f.Heading + 180f, ref _graveModel);
			if (num7 != 0)
			{
				f.Props.Add(num7);
				num4++;
			}
		}
		return (coffins: num3, markers: num4);
	}

	private void FuneralWalkTick(LiveFuneral f, Ped player)
	{
		Vector3 vector = new Vector3(0f, 0f, 0.8f);
		if (f.PropsPending && (HiddenFromView(player, f.Grave + vector) || f.Arrived > 0 || f.Fighting || Game.GameTime - f.WalkStartedAt > 120000))
		{
			var (num, num2) = PutFuneralProps(f);
			GrudgeLog(string.Format("FUNERAL {0}: the casket is down ({1} {2}, {3} marker(s)) - {4}", f.Plan.Title, num, (num == 1) ? "casket" : "caskets", num2, (f.Arrived > 0) ? "as the first of them got there" : (f.Fighting ? "a fight" : "out of your view")));
		}
		foreach (KeyValuePair<int, (Vector3, int, Vector3, int)> item in f.Walking.ToList())
		{
			Ped ped = Entity.FromHandle(item.Key) as Ped;
			if (ped == null || !ped.Exists() || ped.IsDead || f.Fighting || PED.IS_PED_IN_COMBAT(ped.Handle, player.Handle) || (_enc != null && _enc.Spotted && _activeNemesisPed != null && ped.Handle == _activeNemesisPed.Handle))
			{
				f.Walking.Remove(item.Key);
				continue;
			}
			var (vector2, num3, position, num4) = item.Value;
			if (FuneralHandsOff(ped))
			{
				f.Walking[item.Key] = (vector2, num3, ped.Position, Game.GameTime);
			}
			else if (ped.Position.DistanceTo2D(vector2) < 1.2f)
			{
				f.Walking.Remove(item.Key);
				f.Arrived++;
				FaceTheGrave(f, ped, vector2);
			}
			else if (ped.Position.DistanceTo(position) > 0.5f)
			{
				f.Walking[item.Key] = (vector2, num3, ped.Position, Game.GameTime);
			}
			else if (Game.GameTime - num4 >= 6000)
			{
				if (HiddenFromView(player, ped.Position + vector) && HiddenFromView(player, vector2 + vector))
				{
					f.Walking.Remove(item.Key);
					f.Arrived++;
					SettleAtGrave(f, ped, vector2, place: true);
					GrudgeLog("FUNERAL " + WhoIs(ped.Handle) + ": stuck on the walk in - put in his place (unseen)");
				}
				else if (Game.GameTime - num3 > 150000)
				{
					f.Walking.Remove(item.Key);
					FaceTheGrave(f, ped, ped.Position);
					GrudgeLog("FUNERAL " + WhoIs(ped.Handle) + ": couldn't get to his place - stands where he is");
				}
				else
				{
					TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ped.Handle, vector2.X, vector2.Y, vector2.Z, 1f, -1, 0.5f, 0, HeadingTo(vector2, f.Grave));
					f.Walking[item.Key] = (vector2, num3, ped.Position, Game.GameTime);
				}
			}
		}
		foreach (KeyValuePair<int, (Vector3, int)> item2 in f.Turning.ToList())
		{
			Ped ped2 = Entity.FromHandle(item2.Key) as Ped;
			if (ped2 == null || !ped2.Exists() || ped2.IsDead || f.Fighting || PED.IS_PED_IN_COMBAT(ped2.Handle, player.Handle))
			{
				f.Turning.Remove(item2.Key);
			}
			else if (FuneralHandsOff(ped2))
			{
				f.Turning[item2.Key] = (item2.Value.Item1, Game.GameTime + 2500);
			}
			else if (Game.GameTime >= item2.Value.Item2 || !(Math.Abs(HeadingOff(ped2, f.Grave)) > 20f))
			{
				f.Turning.Remove(item2.Key);
				SettleAtGrave(f, ped2, item2.Value.Item1, place: false);
			}
		}
	}

	private bool FuneralHandsOff(Ped m)
	{
		if ((!(_interro?.Ped != null) || _interro.Ped.Handle != m.Handle) && !m.IsHogtied)
		{
			return ENTITY.IS_ENTITY_ATTACHED(m.Handle);
		}
		return true;
	}

	private void FaceTheGrave(LiveFuneral f, Ped m, Vector3 spot)
	{
		if (Math.Abs(HeadingOff(m, f.Grave)) <= 20f)
		{
			SettleAtGrave(f, m, spot, place: false);
			return;
		}
		TASK.CLEAR_PED_TASKS(m.Handle, p1: true, p2: false);
		Function.Call(2151193443362375025uL, m.Handle, f.Grave.X, f.Grave.Y, f.Grave.Z, 2500);
		f.Turning[m.Handle] = (spot, Game.GameTime + 2500);
	}

	private static float HeadingOff(Ped m, Vector3 at)
	{
		float num = (ENTITY.GET_ENTITY_HEADING(m.Handle) - HeadingTo(m.Position, at)) % 360f;
		if (num > 180f)
		{
			num -= 360f;
		}
		if (num < -180f)
		{
			num += 360f;
		}
		return num;
	}

	internal void StandRoundCasket(bool walk = false)
	{
		LiveFuneral funeral = _funeral;
		if (funeral == null)
		{
			return;
		}
		List<Ped> men = funeral.Men.Where((Ped m) => m != null && m.Exists() && !m.IsDead).ToList();
		_settling.RemoveAll(((Ped ped, int checkAt, string fallback, float heading) s) => s.ped != null && men.Any((Ped m) => m.Handle == s.ped.Handle));
		Ped character = Game.Player.Character;
		Vector3 vector = new Vector3(0f, 0f, 0.8f);
		for (int num = 0; num < men.Count; num++)
		{
			Ped ped = men[num];
			_settleFrom.Remove(ped.Handle);
			ClaimBack(ped);
			Vector3 vector2 = GraveSpot(funeral, num, men.Count);
			bool flag = !ped.IsOnMount && ped.Position.DistanceTo(vector2) > 3f && (!HiddenFromView(character, ped.Position + vector) || !HiddenFromView(character, vector2 + vector));
			if (walk || flag)
			{
				TASK.CLEAR_PED_TASKS(ped.Handle, p1: true, p2: false);
				ped.BlockPermanentEvents = true;
				TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(ped.Handle, vector2.X, vector2.Y, vector2.Z, 1f, -1, 0.5f, 0, HeadingTo(vector2, funeral.Grave));
				funeral.Walking[ped.Handle] = (vector2, Game.GameTime, ped.Position, Game.GameTime);
			}
			else
			{
				SettleAtGrave(funeral, ped, vector2, place: true);
			}
		}
	}

	private Vector3 GraveSpot(LiveFuneral f, int i, int n)
	{
		double num = ((double)f.Heading + 90.0 + 60.0 + ((n == 1) ? 120.0 : (240.0 * (double)i / (double)(n - 1)))) * Math.PI / 180.0;
		return GroundOnSpot(f.Grave + new Vector3((float)Math.Cos(num) * f.Ring, (float)Math.Sin(num) * f.Ring, 0f));
	}

	internal bool FuneralInPlace(Plan p)
	{
		if (_funeral != null && _funeral.Plan == p && !_funeral.PropsPending && _funeral.Walking.Count == 0)
		{
			return _funeral.Turning.Count == 0;
		}
		return false;
	}

	internal bool IsMourner(Ped m)
	{
		if (_funeral != null && m != null && m.Exists())
		{
			return _funeral.Men.Any((Ped x) => x != null && x.Exists() && x.Handle == m.Handle);
		}
		return false;
	}

	internal bool BackToGrave(Ped m)
	{
		LiveFuneral funeral = _funeral;
		if (funeral == null || funeral.Grave == Vector3.Zero || m == null || !m.Exists() || m.IsDead)
		{
			return false;
		}
		List<Ped> list = funeral.Men.Where((Ped x) => x != null && x.Exists() && !x.IsDead).ToList();
		int num = list.FindIndex((Ped x) => x.Handle == m.Handle);
		if (num < 0)
		{
			return false;
		}
		Vector3 vector = GraveSpot(funeral, num, list.Count);
		_settling.RemoveAll(((Ped ped, int checkAt, string fallback, float heading) s) => s.ped != null && s.ped.Handle == m.Handle);
		_settleFrom.Remove(m.Handle);
		ClaimBack(m);
		TASK.CLEAR_PED_TASKS(m.Handle, p1: true, p2: false);
		m.BlockPermanentEvents = true;
		TASK.TASK_FOLLOW_NAV_MESH_TO_COORD(m.Handle, vector.X, vector.Y, vector.Z, 1f, -1, 0.5f, 0, HeadingTo(vector, funeral.Grave));
		funeral.Walking[m.Handle] = (vector, Game.GameTime, m.Position, Game.GameTime);
		return true;
	}

	private void SettleAtGrave(LiveFuneral f, Ped m, Vector3 spot, bool place)
	{
		if (place)
		{
			if (m.IsOnMount)
			{
				TASK.TASK_DISMOUNT_ANIMAL(m.Handle, 0, 0uL, 0uL, 0uL, 0);
			}
			TASK.CLEAR_PED_TASKS_IMMEDIATELY(m.Handle, p1: false, resetCrouch: false);
			ENTITY.SET_ENTITY_COORDS(m.Handle, spot.X, spot.Y, spot.Z, xAxis: false, yAxis: false, zAxis: false, clearArea: false);
		}
		else
		{
			TASK.CLEAR_PED_TASKS(m.Handle, p1: true, p2: false);
		}
		float heading = HeadingTo(place ? spot : m.Position, f.Grave);
		if (place)
		{
			ENTITY.SET_ENTITY_HEADING(m.Handle, heading);
		}
		TASK.TASK_START_SCENARIO_IN_PLACE_HASH(m.Handle, MISC.GET_HASH_KEY("WORLD_HUMAN_GUARD_LAZY"), -1, playEnterAnim: true, 0u, heading, p6: false);
		m.BlockPermanentEvents = false;
		HandToGame(m);
	}

	private string PlotsPath => Path.Combine(_saveDir, "plots.txt");

	private List<Vector3> Plots
	{
		get
		{
			if (_plots != null)
			{
				return _plots;
			}
			_plots = new List<Vector3>(MarkedPlots);
			try
			{
				if (File.Exists(PlotsPath))
				{
					string[] array = File.ReadAllLines(PlotsPath);
					foreach (string text in array)
					{
						string[] q = text.Split('|');
						if (q.Length >= 3)
						{
							Vector3 p = new Vector3(F(0), F(1), F(2));
							if (!_plots.Any((Vector3 o) => o.DistanceTo(p) < 1f))
							{
								_plots.Add(p);
							}
						}
						float F(int num)
						{
							if (!float.TryParse(q[num], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
							{
								return 0f;
							}
							return result;
						}
					}
				}
			}
			catch (Exception ex)
			{
				GrudgeLog("FUNERAL plots.txt unreadable: " + ex.Message);
			}
			return _plots;
		}
	}

	internal bool OnAMark(Vector3 cem, Vector3 plot)
	{
		List<Vector3> list = Plots.Where((Vector3 q) => q.DistanceTo2D(cem) < 200f).ToList();
		if (list.Count != 0)
		{
			return list.Any((Vector3 q) => q.DistanceTo(plot) < 1f);
		}
		return true;
	}

	internal Vector3 FuneralPlot(Vector3 c)
	{
		List<Vector3> list = Plots.Where((Vector3 q) => q.DistanceTo2D(c) < 200f).ToList();
		if (list.Count > 0)
		{
			Vector3 result = list[_rng.Next(list.Count)];
			GrudgeLog($"FUNERAL plot: a marked one ({list.Count} near), {result.DistanceTo2D(c):0} m from the cemetery's point");
			return result;
		}
		float[] array = new float[9] { 0f, 3f, 6f, 9f, 12f, 15f, 18f, 21f, 24f };
		foreach (float num2 in array)
		{
			for (int num3 = 0; num3 < ((num2 == 0f) ? 1 : 8); num3++)
			{
				double num4 = (double)num3 * Math.PI / 4.0 + (double)num2 * 0.1;
				Vector3 vector = GroundOnSpot(c + new Vector3((float)Math.Cos(num4) * num2, (float)Math.Sin(num4) * num2, 0f));
				if (PlotClear(vector, c))
				{
					GrudgeLog($"FUNERAL plot: open ground {num2:0} m from the cemetery's point (z {vector.Z:0.0}, point z {c.Z:0.0})");
					return vector;
				}
			}
		}
		GrudgeLog("FUNERAL plot: no open ground found near the cemetery's point - its point it is (mark a plot there from the spawner)");
		return GroundOnSpot(c);
	}

	private static bool PlotClear(Vector3 g, Vector3 c)
	{
		if (Math.Abs(g.Z - c.Z) > 5f || Raised(g))
		{
			return false;
		}
		if (Ray.Blocked(g + new Vector3(0f, 0f, 0.4f), g + new Vector3(0f, 0f, 12f), -1, 0))
		{
			return false;
		}
		for (int i = 0; i < 8; i++)
		{
			double num = (double)i * Math.PI / 4.0;
			Vector3 vector = g + new Vector3(0f, 0f, 1f);
			if (Ray.Blocked(vector, vector + new Vector3((float)Math.Cos(num) * 2.5f, (float)Math.Sin(num) * 2.5f, 0f), -1, 0))
			{
				return false;
			}
		}
		return true;
	}

	internal void DebugMarkPlot(Ped player)
	{
		Vector3? vector = NearestCemetery(player.Position, 200f);
		Vector3 at = player.Position;
		at.Z -= 1f;
		at = GroundOnSpot(at);
		if (Plots.Any((Vector3 q) => q.DistanceTo(at) < 2f))
		{
			SpawnerSays("that plot is already marked.");
			return;
		}
		Plots.Add(at);
		List<string> list = new List<string>();
		try
		{
			if (File.Exists(PlotsPath))
			{
				list.AddRange(File.ReadAllLines(PlotsPath));
			}
		}
		catch
		{
		}
		list.Add(string.Format(CultureInfo.InvariantCulture, "{0:0.0}|{1:0.0}|{2:0.0}", at.X, at.Y, at.Z));
		try
		{
			SafeFile.Write(PlotsPath, string.Join("\r\n", list));
		}
		catch (Exception ex)
		{
			GrudgeLog("FUNERAL plots.txt not saved: " + ex.Message);
		}
		Vector3 c2 = NearestCemetery(at, 200f).GetValueOrDefault(at);
		GrudgeLog(string.Format("FUNERAL plot marked at {0}{1}: {2:0.0},{3:0.0},{4:0.0}", CemeteryLabel(c2), (!vector.HasValue) ? " (a new graveyard)" : "", at.X, at.Y, at.Z));
		SpawnerSays(string.Format("funeral plot marked at {0}{1} ({2} there) - funerals there use it now.", CemeteryLabel(c2), (!vector.HasValue) ? " - a new graveyard" : "", Plots.Count((Vector3 q) => q.DistanceTo2D(c2) < 200f)));
	}

	private void FuneralTick(Ped player)
	{
		LiveFuneral funeral = _funeral;
		if (funeral == null || Game.GameTime < _nextFuneralTick)
		{
			return;
		}
		_nextFuneralTick = Game.GameTime + 250;
		int num = Math.Min(2000, Game.GameTime - funeral.LastTick);
		funeral.LastTick = Game.GameTime;
		if (funeral.PropsPending || funeral.Walking.Count > 0 || funeral.Turning.Count > 0)
		{
			FuneralWalkTick(funeral, player);
		}
		Vector3 position = ((funeral.Grave != Vector3.Zero) ? funeral.Grave : new Vector3(funeral.Plan.X, funeral.Plan.Y, funeral.Plan.Z));
		List<Ped> source = funeral.Men.Where((Ped m) => m != null && m.Exists()).ToList();
		float num2 = player.Position.DistanceTo(position);
		if (num2 < 150f)
		{
			funeral.Watched = true;
		}
		bool flag = source.Any((Ped m) => ENTITY.HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(m.Handle, player.Handle, p2: true, p3: true));
		if (flag && !funeral.Fighting && !funeral.YouShotFirst)
		{
			funeral.YouShotFirst = true;
			GrudgeLog("FUNERAL " + funeral.Plan.Title + ": you opened fire on the mourners");
		}
		if ((source.Any((Ped m) => !m.IsDead && PED.IS_PED_IN_COMBAT(m.Handle, player.Handle)) || flag) && !funeral.Ceasefire)
		{
			funeral.Fighting = true;
		}
		if (!funeral.Respects && !funeral.Fighting && num2 < 12f && !player.IsInVehicle && !player.IsOnMount && !PED.IS_PED_IN_COMBAT(player.Handle, 0) && WEAPON._GET_PED_CURRENT_HELD_WEAPON(player.Handle) == 2725352035u)
		{
			funeral.RespectMs += num;
			if (funeral.RespectMs >= 6000)
			{
				funeral.Respects = true;
				ReplyNotice("You stand a while at the graveside.");
				GrudgeLog("FUNERAL " + funeral.Plan.Title + ": you paid your respects");
			}
		}
		else if (!funeral.Respects)
		{
			funeral.RespectMs = 0;
		}
	}

	private void ResolveFuneral(Plan p, Ped player)
	{
		p.Done = true;
		Gang gang = Gangs.ById(p.Gang);
		Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Dead);
		List<NemesisRecord> list = (from id in FuneralDead(p)
			select Roster.FirstOrDefault((NemesisRecord n) => n.Id == id) into n
			where n != null
			select n).ToList();
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.LeaderId);
		string text = ((list.Count > 0) ? FuneralNames(p) : "one of their own");
		LiveFuneral liveFuneral = ((_funeral != null && _funeral.Plan == p) ? _funeral : null);
		if (liveFuneral != null)
		{
			_funeral = null;
			DropFuneralProps(liveFuneral, player);
		}
		foreach (NemesisRecord dd in list)
		{
			History(dd, "BURIED " + p.Place + ((list.Count > 1) ? (" alongside " + JoinAnd((from o in list
				where o != dd
				select o.Name).ToList())) : "") + ((nemesisRecord != null) ? (" - " + nemesisRecord.Name + " saw him into the ground") : ""), Theme.Dim);
		}
		if (liveFuneral != null && (liveFuneral.YouShotFirst || liveFuneral.Respects || liveFuneral.Watched))
		{
			string title;
			if (liveFuneral.YouShotFirst)
			{
				title = "You attacked " + text + "'s funeral";
				if (gang != null)
				{
					AddNotoriety(gang, 5, "you shot up " + text + "'s funeral");
				}
				if (nemesisRecord != null && nemesisRecord.IsAlive)
				{
					nemesisRecord.AddHate(25);
					History(nemesisRecord, "YOU SHOT UP THE FUNERAL of " + text, Theme.Blood);
				}
			}
			else if (liveFuneral.Respects)
			{
				title = "You paid your respects at " + text + "'s grave";
				if (nemesisRecord != null && nemesisRecord.IsAlive)
				{
					bool num = nemesisRecord.Personality == Personality.Proud || nemesisRecord.Personality == Personality.Loyal || nemesisRecord.Personality == Personality.Coward || Quirks.Has(nemesisRecord, "honest");
					bool flag = nemesisRecord.Personality == Personality.Hothead || nemesisRecord.Personality == Personality.Cold;
					if (num)
					{
						nemesisRecord.AddHate(-15);
						History(nemesisRecord, "YOU PAID YOUR RESPECTS at " + text + "'s grave - he won't forget it", Theme.Good);
					}
					else if (flag)
					{
						nemesisRecord.AddHate(10);
						History(nemesisRecord, "YOU CAME TO GLOAT at " + text + "'s grave, as he sees it", Theme.Blood);
					}
					else
					{
						History(nemesisRecord, "YOU CAME TO " + text.ToUpperInvariant() + "'S FUNERAL", Theme.Dim);
					}
				}
			}
			else
			{
				title = "You watched them bury " + text;
			}
			List<WorldPart> list2 = new List<WorldPart>();
			if (nemesisRecord != null)
			{
				list2.Add(PartFor(nemesisRecord, "MOURNED", "buried " + text));
			}
			try
			{
				ChronicleDeed("funeral", title, p.Place, new Vector3(p.X, p.Y, p.Z), p.Gang, list2.ToArray());
			}
			catch
			{
			}
			p.Outcome = (liveFuneral.YouShotFirst ? "you shot it up" : (liveFuneral.Respects ? "you paid your respects" : "you watched"));
			GrudgeLog("FUNERAL " + text + ": " + p.Outcome);
			SaveRoster();
			SaveWorld();
			return;
		}
		WorldEvent worldEvent = NewEvent("FUNERAL", p.DueAt);
		worldEvent.Gangs.Add(p.Gang ?? "");
		worldEvent.Title = ((gang != null) ? ("The " + gang.Name + " buried " + text) : (((nemesisRecord != null) ? nemesisRecord.Name : "His people") + " buried " + text));
		worldEvent.Place = p.Place;
		worldEvent.HasPos = true;
		worldEvent.X = p.X;
		worldEvent.Y = p.Y;
		worldEvent.Z = p.Z;
		if (nemesisRecord != null && nemesisRecord.IsAlive)
		{
			worldEvent.People.Add(PartFor(nemesisRecord, "MOURNED", "buried " + text));
		}
		foreach (NemesisRecord item in list)
		{
			worldEvent.People.Add(PartFor(item, "BURIED", p.Place));
		}
		worldEvent.Heard = p.Known || WordReachesYou(worldEvent, player);
		Chronicle.Events.Add(worldEvent);
		p.Outcome = "buried";
		GrudgeLog("FUNERAL " + text + " resolved off screen: " + worldEvent.Title + " " + worldEvent.Place);
		if (p.Known)
		{
			GrudgeNotice(worldEvent.Title + " " + worldEvent.Place + ".");
		}
		SaveRoster();
		SaveWorld();
	}

	internal void DebugHabit(Ped player)
	{
		long dueAt = GameMinutesNow();
		(NemesisRecord n, Habit h, int i) best = (from x in Roster.Where(KeepsHabits).Select(delegate(NemesisRecord n)
			{
				if (n.Habits == null)
				{
					RollHabits(n);
				}
				return n;
			}).SelectMany((NemesisRecord n) => (n.Habits ?? new List<Habit>()).Select((Habit h, int i) => (n: n, h: h, i: i)))
			orderby player.Position.DistanceTo2D(new Vector3(x.h.X, x.h.Y, x.h.Z))
			select x).FirstOrDefault();
		if (best.n == null)
		{
			SpawnerSays("no officer has a habit yet (no towns for his outfit?).");
			return;
		}
		Plans.RemoveAll((Plan p) => !p.Done && p.Kind == "habit" && p.LeaderId == best.n.Id);
		Plan plan = new Plan();
		plan.Id = NewId(10);
		plan.Gang = best.n.Gang;
		plan.Kind = "habit";
		plan.Habit = best.n.Id + "/" + best.i + "/" + dueAt;
		plan.LeaderId = best.n.Id;
		plan.Title = HabitTitle(best.n, best.h);
		plan.Place = best.h.Place;
		plan.X = best.h.X;
		plan.Y = best.h.Y;
		plan.Z = best.h.Z;
		plan.DueAt = dueAt;
		plan.WindowMin = 180;
		plan.Crew = ((!(best.h.Kind == "drink")) ? 2 : 0);
		plan.Known = true;
		Plan plan2 = plan;
		Plans.Add(plan2);
		SaveWorld();
		RefreshPlanBlips();
		SpawnerSays($"{plan2.Title} - on now for 3 hours, {player.Position.DistanceTo2D(new Vector3(plan2.X, plan2.Y, plan2.Z)):0} m off. Marked. (He {HabitText(best.h)}.)");
	}

	internal void DebugFuneral(Ped player)
	{
		NemesisRecord dead = Roster.LastOrDefault(delegate(NemesisRecord n)
		{
			if (!n.IsAlive && !n.Story && n.Fate == null && n.StoryGone == null)
			{
				Gang gang = Gangs.ById(n.Gang);
				if (gang != null)
				{
					return !gang.StoryOnly;
				}
			}
			return false;
		});
		if (dead == null)
		{
			SpawnerSays("nobody of an outfit has died yet - kill an officer first.");
			return;
		}
		NemesisRecord nemesisRecord = (from k in KinOf(dead)
			select k.who).FirstOrDefault((NemesisRecord k) => k.IsAlive && k.CanHunt && !k.PresumedDead) ?? (from n in Roster
			where n.Gang == dead.Gang && n.IsAlive && n.CanHunt && !n.PresumedDead && n.Rank >= 2
			orderby n.Rank descending
			select n).FirstOrDefault();
		if (nemesisRecord == null)
		{
			SpawnerSays("nobody left to bury " + dead.Name + ".");
			return;
		}
		Vector3? vector = NearestCemetery(player.Position, 1500f);
		Vector3 vector2 = vector ?? OutOfSightSpot(player, 90f, 150f);
		if (vector2 == Vector3.Zero)
		{
			SpawnerSays("no ground out of your sight ahead.");
			return;
		}
		long dueAt = GameMinutesNow();
		Plan plan = new Plan
		{
			Id = NewId(10),
			Gang = dead.Gang,
			Kind = "funeral",
			Dead = dead.Id,
			LeaderId = nemesisRecord.Id,
			Title = dead.Name + " laid to rest",
			Place = (vector.HasValue ? ("at " + CemeteryLabel(vector.Value)) : ("near " + GetZoneName(vector2))),
			X = vector2.X,
			Y = vector2.Y,
			Z = vector2.Z,
			DueAt = dueAt,
			WindowMin = 300,
			Crew = 3,
			Known = true
		};
		Plans.Add(plan);
		SaveWorld();
		RefreshPlanBlips();
		SpawnerSays($"{plan.Title} - {nemesisRecord.Name} and 3 men, {player.Position.DistanceTo(vector2):0} m ahead. Marked.");
	}

	private Plan FuneralToTell(Gang g)
	{
		if (g != null)
		{
			return (from p in Plans
				where p.Kind == "funeral" && !p.Done && !p.Known && p.Gang == g.Id && p.DueAt + p.WindowMin > GameMinutesNow()
				orderby p.DueAt
				select p).FirstOrDefault();
		}
		return null;
	}

	private string AnswerFuneral(Interrogation it, bool lying)
	{
		Gang gang = it.Gang;
		Plan p = FuneralToTell(gang);
		if (p == null)
		{
			return Nothing(it, "Nobody. Not this week. Not yet.");
		}
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.LeaderId);
		string text = ((FuneralDead(p).Count > 0) ? FuneralNames(p) : "one of ours");
		if (lying)
		{
			Vector3 vector = (from t in JobTownsOf(gang)
				select JobSpot(t)).FirstOrDefault((Vector3 s) => s != Vector3.Zero);
			if (vector == Vector3.Zero)
			{
				return Nothing(it, "I don't know where! They don't tell me!");
			}
			Plan p2 = MakeLie("the burying", it.Rec, gang, vector, "out past the town", p.DueAt, text + " laid to rest");
			IntelNotice(p2);
			return text + ". " + WhenText(p.DueAt) + ", out past the town. Go on, then.";
		}
		p.Known = true;
		IntelNotice(p);
		foreach (NemesisRecord item in from id in FuneralDead(p)
			select Roster.FirstOrDefault((NemesisRecord n) => n.Id == id) into n
			where n != null
			select n)
		{
			History(item, "HIS BURYING GIVEN AWAY by " + Speaker(it) + ": " + WhenText(p.DueAt) + ", " + p.Place, Theme.Dim);
		}
		SaveWorld();
		GrudgeLog("FUNERAL " + text + ": " + Speaker(it) + " told you - " + WhenText(p.DueAt) + ", " + p.Place);
		return text + ". They put " + ((FuneralDead(p).Count > 1) ? "them" : "him") + " in the ground " + WhenText(p.DueAt) + ", " + p.Place + "." + ((nemesisRecord == null) ? "" : ((nemesisRecord == it.Rec) ? " I'll be there myself." : (" " + nemesisRecord.Name + " will be there."))) + " Leave the dead be, mister.";
	}
}
