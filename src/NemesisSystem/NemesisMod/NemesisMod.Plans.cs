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
	private const float FuneralNearM = 60f;

	private const float FuneralFarM = 120f;

	private const int FuneralUnseenMs = 20000;

	private const int PlanHoldAheadMin = 30;

	private const int PlanWaitGraceMin = 120;

	private readonly Dictionary<string, string> _planWaitSaid = new Dictionary<string, string>();

	private readonly HashSet<string> _planEncWait = new HashSet<string>();

	private int _nextPlanCheck;

	private bool _plansLiveCleared;

	private readonly Dictionary<string, int> _planBlips = new Dictionary<string, int>();

	private Vector3? _spawnAt;

	internal string PlanNote = "";

	private static readonly (string title, string place, int crew)[] CommonJobs = new(string, string, int)[3]
	{
		("rob travellers on the road by {t}", "the road by {t}", 3),
		("hit a homestead outside {t}", "a homestead outside {t}", 3),
		("run off horses from a ranch near {t}", "a ranch near {t}", 3)
	};

	private static readonly int[] WaterZoneTypes = new int[5] { 2, 3, 6, 7, 8 };

	private readonly Dictionary<string, Vector3> _planBlipAt = new Dictionary<string, Vector3>();

	private Procession _proc;

	private const ulong GetCarryConfig = 3814558401569769835uL;

	internal string PlanDueHere(Ped player)
	{
		long num = GameMinutesNow();
		foreach (Plan plan in Plans)
		{
			if (!plan.Done && !plan.Live && !plan.Lie && !(plan.Kind == "empty") && num >= plan.DueAt - 30 && num <= plan.DueAt + plan.WindowMin && !(player.Position.DistanceTo2D(new Vector3(plan.X, plan.Y, plan.Z)) > 220f))
			{
				return ((plan.Kind == "funeral") ? "a funeral" : ((plan.Kind == "habit") ? "a habit" : "a plan")) + " is due here (" + plan.Title + ")";
			}
		}
		return null;
	}

	private void NotePlanWait(Plan p, string why, bool byEncounter)
	{
		if (byEncounter)
		{
			_planEncWait.Add(p.Id);
		}
		if (!_planWaitSaid.TryGetValue(p.Id, out var value) || !(value == why))
		{
			_planWaitSaid[p.Id] = why;
			GrudgeLog("PLAN " + p.Kind + " " + p.Title + ": you're here and it's time - it waits: " + why + (byEncounter ? $" (up to {2} game hours past its time while you stay)" : ""));
		}
	}

	private string EncounterOnText()
	{
		if (_enc == null)
		{
			return (ActiveRecord?.Name ?? "a nemesis") + " is out there";
		}
		return (ActiveRecord?.Name ?? "someone") + "'s " + _enc.Kind.ToString().ToLowerInvariant() + " encounter is on";
	}

	private bool NoteGraveSight(Plan p, Ped player)
	{
		Vector3 spot = (p.PlotSet ? new Vector3(p.PlotX, p.PlotY, p.PlotZ) : new Vector3(p.X, p.Y, p.Z));
		if (HiddenFromView(player, spot))
		{
			return false;
		}
		_funeralSeenAt[p.Id] = Game.GameTime;
		return true;
	}

	private List<Plan> Plans => Chronicle.Plans ?? (Chronicle.Plans = new List<Plan>());

	internal static (string title, string place, int crew)[] JobsOf(Gang g)
	{
		if (g?.Jobs == null || g.Jobs.Length == 0)
		{
			return CommonJobs;
		}
		return g.Jobs;
	}

	internal string[] JobTownsOf(Gang g)
	{
		if (g == null)
		{
			return new string[0];
		}
		if (g.JobTowns != null && g.JobTowns.Length != 0)
		{
			return g.JobTowns.Where((string t) => TownSpots.ContainsKey(t)).ToArray();
		}
		if (!string.IsNullOrEmpty(g.HomeTown) && TownSpots.ContainsKey(g.HomeTown))
		{
			return new string[1] { g.HomeTown };
		}
		Vector3 c;
		float radius;
		return TownNames.Where((string t) => TownCore(t, out c, out radius) && Districts.Any((District d) => HoldOf(d, g.Id) >= 10 && new Vector3(c.X - d.X, c.Y - d.Y, 0f).Length() < 1600f)).ToArray();
	}

	private Vector3 JobSpot(string town)
	{
		if (!TownCore(town, out var centre, out var radius))
		{
			return Vector3.Zero;
		}
		for (int i = 0; i < 12; i++)
		{
			double num = _rng.NextDouble() * Math.PI * 2.0;
			float num2 = radius + 220f + (float)_rng.NextDouble() * 260f;
			Vector3 vector = new Vector3(centre.X + (float)Math.Cos(num) * num2, centre.Y + (float)Math.Sin(num) * num2, centre.Z);
			if (OnLand(vector))
			{
				return vector;
			}
		}
		return Vector3.Zero;
	}

	internal static bool OnLand(Vector3 p)
	{
		try
		{
			if (ZONE._GET_MAP_ZONE_AT_COORDS(p.X, p.Y, p.Z, 10) == 0)
			{
				return false;
			}
			int[] waterZoneTypes = WaterZoneTypes;
			foreach (int type in waterZoneTypes)
			{
				if (ZONE._GET_MAP_ZONE_AT_COORDS(p.X, p.Y, p.Z, type) != 0)
				{
					return false;
				}
			}
			return true;
		}
		catch
		{
			return true;
		}
	}

	private void RepairPlansOffMap()
	{
		int num = 0;
		int num2 = 0;
		foreach (Plan p in Plans.Where((Plan x) => !x.Done).ToList())
		{
			if (!OnLand(new Vector3(p.X, p.Y, p.Z)))
			{
				string text = TownNames.FirstOrDefault((string t) => (p.Place ?? "").Contains(t) || (p.Title ?? "").Contains(t));
				Vector3 vector = ((text != null) ? JobSpot(text) : Vector3.Zero);
				if (vector != Vector3.Zero)
				{
					p.X = vector.X;
					p.Y = vector.Y;
					p.Z = vector.Z;
					num++;
				}
				else
				{
					Plans.Remove(p);
					num2++;
				}
			}
		}
		if (num + num2 > 0)
		{
			GrudgeLog($"PLAN off the map: {num} moved onto land, {num2} dropped");
			SaveWorld();
			try
			{
				RefreshPlanBlips();
			}
			catch
			{
			}
		}
	}

	internal Plan FactionJob(Gang g, bool create = true)
	{
		if (g == null)
		{
			return null;
		}
		Plan plan = Plans.FirstOrDefault((Plan x) => x.Kind == "job" && x.Gang == g.Id && !x.Done && !x.Lie);
		if (plan != null || !create)
		{
			return plan;
		}
		if (IsDestroyed(g))
		{
			return null;
		}
		(string, string, int)[] array = JobsOf(g);
		string[] array2 = JobTownsOf(g);
		if (array.Length == 0 || array2.Length == 0)
		{
			return null;
		}
		(string, string, int) tuple = array[_rng.Next(array.Length)];
		string text = array2[_rng.Next(array2.Length)];
		Vector3 vector = JobSpot(text);
		if (vector == Vector3.Zero)
		{
			return null;
		}
		NemesisRecord nemesisRecord = (from _ in Roster
			where _.Gang == g.Id && _.CanHunt && !_.PresumedDead && _.Rank >= 2 && OffStage(_)
			orderby _rng.Next()
			select _).FirstOrDefault();
		bool flag = HasPurse(g) && !g.OrdersNotFeelings && PurseOf(g) < 100;
		plan = new Plan
		{
			Id = NewId(10),
			Gang = g.Id,
			Kind = "job",
			Title = tuple.Item1.Replace("{t}", text),
			Place = tuple.Item2.Replace("{t}", text),
			X = vector.X,
			Y = vector.Y,
			Z = vector.Z,
			DueAt = GameMinutesNow() + (long)(flag ? (6 + _rng.Next(18)) : ((AgendaOf(g)?.Kind == "rich") ? (6 + _rng.Next(25)) : (12 + _rng.Next(37)))) * 60L,
			WindowMin = 180,
			LeaderId = nemesisRecord?.Id,
			Crew = tuple.Item3,
			Desperate = flag
		};
		Plans.Add(plan);
		SaveWorld();
		GrudgeLog("PLAN job for the " + g.Name + ": " + plan.Title + " - due " + WhenText(plan.DueAt) + ((nemesisRecord != null) ? (", led by " + nemesisRecord.Name) : "") + (flag ? $" - a desperate one (purse ${PurseOf(g)})" : ""));
		return plan;
	}

	internal Plan MakeCrewPlan(string kind, NemesisRecord boss, NemesisRecord teller)
	{
		Gang gang = Gangs.Of(boss);
		if (boss == null || gang == null)
		{
			return null;
		}
		Plan plan = Plans.FirstOrDefault((Plan x) => x.Kind == kind && x.LeaderId == boss.Id && !x.Done);
		if (plan != null)
		{
			return plan;
		}
		CampSite campSite = CampOf(boss);
		string[] array = JobTownsOf(gang);
		string text = ((array.Length != 0) ? array[_rng.Next(array.Length)] : null);
		Vector3 vector;
		string place;
		if (kind == "alone" && campSite != null && _rng.Next(2) == 0)
		{
			vector = SitePos(campSite) + new Vector3((float)_rng.NextDouble() * 160f - 80f, (float)_rng.NextDouble() * 160f - 80f, 0f);
			place = "near " + campSite.Label;
		}
		else if (text != null && (vector = JobSpot(text)) != Vector3.Zero)
		{
			place = ((kind == "alone") ? ("outside " + text + ", alone") : ("the road out of " + text));
		}
		else
		{
			if (campSite == null)
			{
				return null;
			}
			vector = SitePos(campSite);
			place = campSite.Label;
		}
		if (vector == Vector3.Zero)
		{
			return null;
		}
		Plan plan2 = new Plan
		{
			Id = NewId(10),
			Gang = gang.Id,
			Kind = kind,
			LeaderId = boss.Id,
			ToldBy = teller?.Id,
			Title = ((kind == "alone") ? (boss.Name + " without his men") : (boss.Name + "'s crew rides")),
			Place = place,
			X = vector.X,
			Y = vector.Y,
			Z = vector.Z,
			DueAt = GameMinutesNow() + (long)(6 + _rng.Next(25)) * 60L,
			WindowMin = ((kind == "alone") ? 120 : 150),
			Crew = ((kind == "alone") ? _rng.Next(2) : (2 + _rng.Next(3))),
			Known = true
		};
		Plans.Add(plan2);
		SaveWorld();
		GrudgeLog("PLAN " + kind + ": " + plan2.Title + " at " + plan2.Place + ", " + WhenText(plan2.DueAt) + " (told by " + (teller?.Name ?? "?") + ")");
		return plan2;
	}

	internal Plan MakeLie(string about, NemesisRecord teller, Gang g, Vector3 spot, string place, long dueAt, string title)
	{
		bool flag = _rng.Next(100) < (((teller != null && teller.Personality == Personality.Cold) || (teller != null && teller.Personality == Personality.Loyal)) ? 55 : 35);
		Plan plan = new Plan
		{
			Id = NewId(10),
			Gang = g?.Id,
			Kind = (flag ? "trap" : "empty"),
			Lie = true,
			Known = true,
			ToldBy = teller?.Id,
			ToldAbout = about,
			Title = title,
			Place = place,
			X = spot.X,
			Y = spot.Y,
			Z = spot.Z,
			DueAt = dueAt,
			WindowMin = 240,
			Crew = 3 + _rng.Next(3),
			LeaderId = BossOf(teller)?.Id
		};
		Plans.Add(plan);
		SaveWorld();
		GrudgeLog("PLAN LIE (" + plan.Kind + ") from " + (teller?.Name ?? "?") + " about " + about + ": " + title + " at " + place + ", " + WhenText(dueAt));
		return plan;
	}

	internal string WhenText(long at)
	{
		DateTime dateTime = FromGameMinutes(at);
		GameMinutesNow();
		int num = ((dateTime.Hour % 12 == 0) ? 12 : (dateTime.Hour % 12));
		return string.Format("{0} {1} at {2}:{3:00} {4}", Months[dateTime.Month - 1], dateTime.Day, num, dateTime.Minute, (dateTime.Hour < 12) ? "AM" : "PM");
	}

	private void UpdatePlans(Ped player)
	{
		FuneralTick(player);
		if (Game.GameTime < _nextPlanCheck)
		{
			return;
		}
		_nextPlanCheck = Game.GameTime + 2000;
		long now = GameMinutesNow();
		if (!_plansLiveCleared)
		{
			_plansLiveCleared = true;
			int num = 0;
			foreach (Plan item in Plans.Where((Plan x) => !x.Done && x.Live && !x.Engaged && (x.Kind == "habit" || x.Kind == "funeral") && now <= x.DueAt + x.WindowMin))
			{
				item.Live = false;
				num++;
			}
			if (num > 0)
			{
				SaveWorld();
				GrudgeLog($"PLAN {num} plan(s) that were playing when the scripts reloaded can play again");
			}
		}
		Gang[] all = Gangs.All;
		foreach (Gang g in all)
		{
			if (Config.WorldEvents)
			{
				FactionJob(g);
			}
		}
		UpdateInformants();
		UpdateRoutines(player);
		int num3 = 0;
		int num4 = 0;
		foreach (Plan p in Plans.Where((Plan x) => !x.Done).ToList())
		{
			if (p.Done || !Plans.Contains(p))
			{
				continue;
			}
			if (p.Kind != "job" && p.Kind != "funeral" && !p.Lie && p.LeaderId != null)
			{
				NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.LeaderId);
				if (nemesisRecord != null && !nemesisRecord.IsAlive)
				{
					p.Done = true;
					p.Outcome = "he's dead";
					GrudgeLog("PLAN " + p.Kind + " " + p.Title + ": " + nemesisRecord.Name + " is dead - it won't happen" + (p.Known ? " (its ? comes off the map)" : ""));
					if (p.Known)
					{
						Feed("PLAN " + p.Title + ": " + nemesisRecord.Name + " is dead - the mark is gone", Theme.Dim);
						ReplyNotice(nemesisRecord.Name + " is dead - " + p.Title.TrimEnd('.') + " won't happen.");
					}
					SaveWorld();
					continue;
				}
			}
			Vector3 position = new Vector3(p.X, p.Y, p.Z);
			float num5 = player.Position.DistanceTo2D(position);
			long num6 = p.DueAt + p.WindowMin + ((_planEncWait.Contains(p.Id) && num5 < 220f) ? 120 : 0);
			bool flag = now >= p.DueAt && now <= num6;
			if (p.Known)
			{
				num4++;
			}
			if (flag && num5 < ((p.Kind == "funeral") ? 50f : 150f))
			{
				bool num7;
				if (!(p.Kind == "funeral"))
				{
					if (p.Live || p.Lie)
					{
						goto IL_04a8;
					}
					num7 = p.Kind == "empty";
				}
				else
				{
					num7 = FuneralInPlace(p);
				}
				if (num7)
				{
					goto IL_04a8;
				}
			}
			goto IL_04b5;
			IL_04b5:
			if (p.Live && !p.Engaged && _enc != null && ActiveRecord?.Id == p.LeaderId && (_enc.Engaged || _enc.StandoffUntil != 0))
			{
				p.Engaged = true;
				SaveWorld();
			}
			if (p.Lie && p.Kind == "empty" && now >= p.DueAt && num5 < 60f)
			{
				CaughtLying(p);
				continue;
			}
			if (p.Kind == "funeral" && flag && !p.Live && num5 < 220f)
			{
				NoteGraveSight(p, player);
			}
			if (flag && !p.Live && num5 < 220f && p.Kind != "empty")
			{
				string text = NoSpawnReason(player);
				if (p.Kind == "funeral" && text != null && text == StoryBansWeapons(player.Position))
				{
					text = ((INTERIOR.GET_INTERIOR_FROM_ENTITY(player.Handle) != 0) ? "indoors" : ((Config.CalmWhileWanted && LawAfterYou()) ? "the law is after you" : null));
				}
				bool flag2 = _enc != null || _activeNemesisPed != null;
				string text2 = text ?? (flag2 ? EncounterOnText() : null);
				if (text2 != null)
				{
					NotePlanWait(p, text2, text == null && flag2);
				}
				else if (((p.Kind != "habit" && p.Kind != "funeral") || RoutineMayPlay(p, player, num5)) && PlayPlan(p, player))
				{
					num3++;
					_funeralSeenAt.Remove(p.Id);
					_planWaitSaid.Remove(p.Id);
				}
			}
			if (now > num6)
			{
				ResolvePlan(p, player);
				_planEncWait.Remove(p.Id);
			}
			continue;
			IL_04a8:
			p.Visited = true;
			goto IL_04b5;
		}
		Plans.RemoveAll((Plan x) => x.Done && now - x.DueAt > ((x.Kind == "habit") ? 1440 : 10080));
		RefreshPlanBlips();
		PlanNote = $"{Plans.Count((Plan x) => !x.Done)} coming ({num4} known) - next: " + string.Join("; ", from x in (from x in Plans
				where !x.Done
				orderby x.DueAt
				select x).Take(3)
			select string.Format("{0} {1} {2} {3:0} m{4}", x.Kind, Gangs.ById(x.Gang)?.Name ?? "?", WhenText(x.DueAt), player.Position.DistanceTo2D(new Vector3(x.X, x.Y, x.Z)), x.Lie ? " LIE" : ""));
	}

	private bool PlayPlan(Plan p, Ped player)
	{
		Vector3 vector = Vector3.Zero;
		bool flag = p.Kind == "habit" || p.Kind == "funeral";
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.LeaderId && n.CanHunt && !n.PresumedDead && OffStage(n)) ?? (flag ? null : (from n in Roster
			where n.Gang == p.Gang && n.CanHunt && !n.PresumedDead && n.Rank >= 2 && OffStage(n)
			orderby n.Rank descending
			select n).FirstOrDefault());
		if (nemesisRecord == null)
		{
			if (!flag || _routineNobodySaid.Add(p.Id))
			{
				GrudgeLog("PLAN " + p.Kind + " " + p.Title + ": nobody free to do it right now");
			}
			return false;
		}
		Vector3 vector2 = ((flag && (p.Kind == "funeral" || HabitOf(p).h?.Kind != "ride")) ? Ground(new Vector3(p.X, p.Y, p.Z)) : SnapToRoad(new Vector3(p.X, p.Y, p.Z), 150f));
		if (p.Kind == "funeral")
		{
			Vector3 cem = new Vector3(p.X, p.Y, p.Z);
			if (p.PlotSet && !OnAMark(cem, new Vector3(p.PlotX, p.PlotY, p.PlotZ)))
			{
				p.PlotSet = false;
				GrudgeLog("FUNERAL " + p.Title + ": its plot wasn't on a marked one - picked again");
			}
			if (!p.PlotSet)
			{
				bool flag2 = Plots.Any((Vector3 q) => q.DistanceTo2D(cem) < 200f);
				if (player.Position.DistanceTo2D(cem) > (flag2 ? 220f : 160f))
				{
					return false;
				}
				Vector3 vector3 = FuneralPlot(cem);
				p.PlotX = vector3.X;
				p.PlotY = vector3.Y;
				p.PlotZ = vector3.Z;
				p.PlotSet = true;
				SaveWorld();
			}
			vector2 = new Vector3(p.PlotX, p.PlotY, p.PlotZ);
			float num = player.Position.DistanceTo(vector2);
			NoteGraveSight(p, player);
			int value;
			bool flag3 = _funeralSeenAt.TryGetValue(p.Id, out value) && Game.GameTime - value < 20000;
			if (num < 120f && (num < 60f || flag3))
			{
				vector = RideInStart(vector2, player, onFoot: true, far: true);
				if (vector == Vector3.Zero)
				{
					return false;
				}
			}
		}
		if (flag && p.Kind == "habit" && HabitOf(p).h?.Kind == "drink")
		{
			vector2 = new Vector3(p.X, p.Y, p.Z);
		}
		bool onFoot = flag && p.Kind == "habit" && HabitOf(p).h?.Kind == "drink";
		Vector3 vector4 = ((p.Kind != "trap" && p.Kind != "funeral") ? RideInStart(vector2, player, onFoot) : vector);
		if (p.Kind != "funeral" && vector4 == Vector3.Zero && !HiddenFromView(player, vector2))
		{
			return false;
		}
		EncounterCard encounterCard = (flag ? RoutineCard(p) : new EncounterCard
		{
			Id = "plan-" + p.Kind,
			Stage = "idle",
			Title = p.Title,
			Standoff = ((p.Kind == "trap") ? new bool?(false) : ((bool?)null)),
			SightRange = ((p.Kind == "trap") ? 70f : 0f),
			Activity = ((p.Kind == "ride" || (p.Gang == "army" && p.Kind == "job")) ? "patrol" : null),
			Cast = new CardCast
			{
				Crew = new List<int> { p.Crew, p.Crew }
			},
			Taunts = ((p.Kind == "trap") ? new List<string> { "Told you he'd come. Now!", "Right where we wanted you.", "Walked right into it, didn't you?" } : null),
			History = ((p.Kind == "trap") ? "SPRANG A TRAP on you at {place}" : ((p.Kind == "alone") ? "CAUGHT WITHOUT HIS MEN at {place}" : "CAUGHT AT A JOB ({kind}) at {place}"))
		});
		string scenario = ((encounterCard.Activity == "patrol") ? null : encounterCard.Activity);
		bool patrol = encounterCard.Activity == "patrol";
		if (vector4 != Vector3.Zero)
		{
			encounterCard.Activity = "patrol";
		}
		_spawnAt = ((vector4 != Vector3.Zero) ? vector4 : vector2);
		try
		{
			SpawnEncounter(nemesisRecord, EncounterKind.Idle, player, encounterCard);
		}
		finally
		{
			_spawnAt = null;
		}
		if (ActiveRecord != nemesisRecord)
		{
			GrudgeLog("PLAN " + p.Kind + " " + p.Title + ": spawn failed - tried again later");
			return false;
		}
		p.Live = true;
		if (p.Kind == "trap")
		{
			p.Engaged = true;
		}
		p.ScarsAt = nemesisRecord.Scars.Count;
		if (vector4 != Vector3.Zero && p.Kind != "funeral")
		{
			try
			{
				BeginRideIn(p, vector2, onFoot, patrol, scenario);
			}
			catch (Exception ex)
			{
				GrudgeLog("RIDEIN failed: " + ex.Message);
			}
		}
		if (flag)
		{
			_wildCameAt = Game.GameTime;
		}
		if (p.Kind == "funeral")
		{
			FuneralStarted(p, player, vector2, (vector4 != Vector3.Zero) ? vector4.DistanceTo(vector2) : 0f);
		}
		GrudgeLog($"PLAN {p.Kind} {p.Title} is happening at {vector2.X:0},{vector2.Y:0} - {nemesisRecord.Name} + {p.Crew}");
		Feed("PLAN " + (p.Lie ? "TRAP" : p.Kind.ToUpperInvariant()) + ": " + p.Title + " - " + p.Place, (p.Kind == "trap") ? Theme.Blood : Theme.Gold, nemesisRecord);
		if (p.Kind == "trap")
		{
			GrudgeNotice("It's a trap.");
		}
		if (p.Known && p.Kind != "trap" && vector4 == Vector3.Zero)
		{
			GrudgeNotice("There they are: " + p.Title + ".");
		}
		if (p.Lie && p.Kind == "trap")
		{
			ExposeLiar(p, "it was a trap");
		}
		return true;
	}

	private unsafe Vector3 SnapToRoad(Vector3 target, float within)
	{
		ulong* ptr = stackalloc ulong[3];
		try
		{
			if (PATHFIND.GET_CLOSEST_VEHICLE_NODE(target.X, target.Y, target.Z, (Vector3*)ptr, 0, 3f, 0f))
			{
				Vector3 p = new Vector3(*(float*)ptr, *(float*)(ptr + 1), *(float*)(ptr + 2));
				if (p.DistanceTo2D(target) < within)
				{
					return Ground(p);
				}
			}
		}
		catch
		{
		}
		return Ground(target);
	}

	private void ResolvePlan(Plan p, Ped player)
	{
		_historyAt = Math.Min(p.DueAt, GameMinutesNow());
		try
		{
			ResolvePlanAt(p, player);
		}
		finally
		{
			_historyAt = null;
		}
	}

	private string JobFailure(Gang g)
	{
		string[] array = ((!(g?.Id == "wapiti")) ? ((!(g?.Id == "laramie")) ? ((!(g?.Id == "delobo")) ? ((!(g?.Id == "nightfolk")) ? ((g != null && g.OrdersNotFeelings) ? new string[3] { "the men they were sent for were long gone", "the column came back empty-handed", "bad orders and worse luck" } : new string[6] { "the law was waiting", "a posse rode them down", "the mark had hired guns", "somebody talked", "they were seen coming", "it fell apart at the last minute" }) : new string[6] { "the moon was too bright", "a lantern was seen and the road emptied", "the dogs in Lagras woke the whole town", "the Raiders were on that road that night", "the water was wrong, the elder said", "nobody would go past the old church" }) : new string[5] { "the Army was on the river that night", "the rurales were waiting on the other side", "the train carried soldiers, not money", "the marshal from Armadillo rode them down", "a horse went lame in the canyon" }) : new string[5] { "the rancher's hands were sitting up with the herd", "a stock detective was waiting", "the herd stampeded the wrong way", "the cattlemen's association had men on the road", "a storm scattered the stock" }) : new string[3] { "the herd had moved on", "soldiers found them first", "the hunt came back empty" });
		return array[_rng.Next(array.Length)];
	}

	private void ResolvePlanAt(Plan p, Ped player)
	{
		p.Done = true;
		Gang gang = Gangs.ById(p.Gang);
		if (ResolveRoutine(p, player))
		{
			return;
		}
		if (IsDestroyed(gang))
		{
			GrudgeLog("PLAN " + p.Title + ": the " + gang.Name + " are finished - it never happened");
			return;
		}
		if (p.Lie)
		{
			if (p.Visited && !p.Live)
			{
				CaughtLying(p);
			}
			p.Outcome = (p.Visited ? "a lie" : "never checked");
			return;
		}
		if (p.Live)
		{
			p.Outcome = "you were there";
			if (p.Kind == "job")
			{
				PurseForJob(gang, ok: false, youThere: true);
			}
			return;
		}
		if (p.Kind == "job" && gang != null && !gang.OrdersNotFeelings && MenOf(gang) == 0)
		{
			p.Outcome = "never happened";
			GrudgeLog("PLAN " + p.Title + ": the " + gang.Name + " have no men left to do it");
			return;
		}
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.LeaderId);
		WorldEvent worldEvent = NewEvent("JOB", p.DueAt);
		worldEvent.Gangs.Add(p.Gang ?? "");
		worldEvent.Place = p.Place;
		worldEvent.HasPos = true;
		worldEvent.X = p.X;
		worldEvent.Y = p.Y;
		worldEvent.Z = p.Z;
		bool flag = _rng.Next(100) < ((p.Kind == "alone") ? 100 : (p.Desperate ? 55 : 70));
		bool flag2 = nemesisRecord != null && nemesisRecord.IsAlive && !nemesisRecord.PresumedDead;
		string text = (flag2 ? nemesisRecord.Name : ("the " + (gang?.Name ?? "outfit")));
		if (p.Kind == "job")
		{
			worldEvent.Title = ((!(gang?.Id == "nightfolk")) ? ((!flag) ? (flag2 ? (text + "'s men tried to " + p.Title + " - " + JobFailure(gang)) : (Cap(text) + " tried to " + p.Title + " - " + JobFailure(gang))) : (flag2 ? (text + " and his men did it: " + p.Title) : (Cap(text) + " did it: " + p.Title))) : (flag ? ("In the night, " + (flag2 ? (text + "'s folk") : "the Night Folk") + " went out to " + p.Title + ". By morning it was done, and nobody saw a thing") : ((flag2 ? (text + "'s folk") : "The Night Folk") + " went out to " + p.Title + " and came back with nothing - " + JobFailure(gang))));
			if (nemesisRecord != null && nemesisRecord.IsAlive && !nemesisRecord.PresumedDead && OffStage(nemesisRecord))
			{
				WorldPart worldPart = PartFor(nemesisRecord, flag ? "PULLED IT OFF" : "WENT WRONG", p.Title);
				worldEvent.People.Add(worldPart);
				if (flag)
				{
					AddThought(nemesisRecord, "Flush", "the job that paid", 2, 48, p.DueAt, worldPart);
					GainRankXp(nemesisRecord, 1, "a job that paid");
				}
				else if (_rng.Next(100) < 25 * Config.WorldLethality / 100)
				{
					OffscreenDeath(nemesisRecord, worldEvent, worldPart, "the law", gun: true);
				}
				else
				{
					AddThought(nemesisRecord, "Bitter", "the job that went wrong", -2, 48, p.DueAt, worldPart);
				}
			}
			if (!flag)
			{
				worldEvent.UnnamedDead[gang?.Id ?? "outlaws"] = 1 + _rng.Next(2);
			}
		}
		else
		{
			worldEvent.Title = ((p.Kind == "ride") ? (text + "'s crew rode " + p.Place) : (text + " went about alone " + p.Place));
		}
		worldEvent.Heard = p.Known || WordReachesYou(worldEvent, player);
		Chronicle.Events.Add(worldEvent);
		p.Outcome = (flag ? "done" : "went wrong");
		if (p.Kind == "job")
		{
			PurseForJob(gang, flag, youThere: false, p.Desperate);
		}
		GrudgeLog("PLAN " + p.Kind + " resolved off screen: " + worldEvent.Title);
		if (p.Known)
		{
			GrudgeNotice(worldEvent.Title + ".");
		}
		SaveWorld();
		SaveRoster();
	}

	private void CaughtLying(Plan p)
	{
		p.Done = true;
		p.Outcome = "a lie";
		ExposeLiar(p, (p.Kind == "empty") ? "nobody was there" : "it was a trap");
		string text = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.ToldBy)?.Name ?? "He";
		GrudgeNotice((p.Kind == "trap") ? (text + " sent you into a trap.") : ("Nobody here. " + text + " lied to you."));
		SaveWorld();
	}

	private void ExposeLiar(Plan p, string how)
	{
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.ToldBy);
		if (nemesisRecord != null)
		{
			nemesisRecord.LiedToYou++;
			History(nemesisRecord, "LIED TO YOU about " + (p.ToldAbout ?? "his outfit") + " - " + how, Theme.Warn);
			Feed("LIED " + nemesisRecord.Name + ": about " + (p.ToldAbout ?? "his outfit") + " - " + how, Theme.Warn);
			Remember(nemesisRecord, "Pride", 40, "fooled you");
			SaveRoster();
		}
	}

	private unsafe void RefreshPlanBlips()
	{
		if (!MapSettled())
		{
			_mapPending.Add(RefreshPlanBlips);
			return;
		}
		if (_mapHidden)
		{
			ClearPlanBlips();
			return;
		}
		Dictionary<string, Plan> want = (from plan in Plans
			where plan.Known && !plan.Done && !plan.Visited && plan.Id != null
			group plan by plan.Id).ToDictionary((IGrouping<string, Plan> g) => g.Key, (IGrouping<string, Plan> g) => g.First());
		foreach (string item in _planBlips.Keys.Where((string k) => !want.ContainsKey(k)).ToList())
		{
			int blip = _planBlips[item];
			Crumbs.Native("Plans blip remove");
			if (MAP.DOES_BLIP_EXIST(blip))
			{
				MAP.REMOVE_BLIP(&blip);
			}
			_planBlips.Remove(item);
			_planBlipAt.Remove(item);
		}
		foreach (Plan value5 in want.Values)
		{
			if (_planBlips.TryGetValue(value5.Id, out var value) && !MAP.DOES_BLIP_EXIST(value))
			{
				_planBlips.Remove(value5.Id);
			}
			Vector3 vector = PlanMarkAt(value5);
			if (_planBlips.TryGetValue(value5.Id, out var value2) && _planBlipAt.TryGetValue(value5.Id, out var value3) && value3.DistanceTo2D(vector) > 3f)
			{
				Crumbs.Native("Plans blip remove");
				if (MAP.DOES_BLIP_EXIST(value2))
				{
					MAP.REMOVE_BLIP(&value2);
				}
				_planBlips.Remove(value5.Id);
			}
			if (!_planBlips.ContainsKey(value5.Id))
			{
				_planBlipAt[value5.Id] = vector;
				float x = vector.X;
				float y = vector.Y;
				float z = vector.Z;
				Gang gang = Gangs.ById(value5.Gang);
				int value4 = AddMarkerBlip(x, y, z, ((gang != null) ? (gang.Name + ": ") : ((value5.Kind == "funeral") ? "" : "Outlaws: ")) + value5.Title + " - " + WhenText(value5.DueAt));
				_planBlips[value5.Id] = value4;
			}
		}
	}

	private unsafe void ClearPlanBlips()
	{
		Crumbs.Native("Plans blip remove");
		foreach (string item in _planBlips.Keys.ToList())
		{
			int blip = _planBlips[item];
			if (MAP.DOES_BLIP_EXIST(blip))
			{
				MAP.REMOVE_BLIP(&blip);
			}
		}
		_planBlips.Clear();
		_planBlipAt.Clear();
	}

	private Vector3 PlanMarkAt(Plan p)
	{
		Vector3 spot = new Vector3(p.X, p.Y, p.Z);
		if (p.Kind != "funeral")
		{
			return spot;
		}
		if (!p.PlotSet && Plots.Any((Vector3 q) => q.DistanceTo2D(spot) < 200f))
		{
			Vector3 vector = FuneralPlot(spot);
			p.PlotX = vector.X;
			p.PlotY = vector.Y;
			p.PlotZ = vector.Z;
			p.PlotSet = true;
			SaveWorld();
		}
		if (!p.PlotSet)
		{
			return spot;
		}
		return new Vector3(p.PlotX, p.PlotY, p.PlotZ);
	}
}
