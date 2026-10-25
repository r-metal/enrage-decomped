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
	private static readonly string[] DayNames = new string[7] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

	private int _nextRoutineCheck;

	private int _nextFuneralTick;

	private readonly HashSet<string> _routineNobodySaid = new HashSet<string>();

	private void RollHabits(NemesisRecord n, string onlyKind = null, string notTown = null)
	{
		Gang gang = Gangs.ById(n.Gang);
		string[] array = (from t in JobTownsOf(gang)
			where t != notTown
			select t).ToArray();
		if (array.Length == 0)
		{
			array = JobTownsOf(gang);
		}
		if (array.Length == 0)
		{
			return;
		}
		Random random = new Random(Seeds.Stable("habits/" + n.Id + "/" + (n.Habits?.Count ?? 0) + "/" + onlyKind));
		List<Habit> list = n.Habits ?? new List<Habit>();
		if (Want("drink") && gang != Gangs.Wapiti)
		{
			bool num = onlyKind == "drink" || Quirks.Has(n, "drunk") || Quirks.Has(n, "gambler") || random.Next(100) < ((n.Personality == Personality.Cold || n.Personality == Personality.Loyal) ? 45 : 75);
			(Vector3?, Vector3?) value2;
			string[] array2 = array.Where((string t) => TownSpots.TryGetValue(t, out value2) && value2.Item2.HasValue).ToArray();
			if (num && array2.Length != 0)
			{
				string text = array2[random.Next(array2.Length)];
				Vector3 value = TownSpots[text].Saloon.Value;
				list.Add(new Habit
				{
					Kind = "drink",
					Town = text,
					Place = "the " + text + " saloon",
					X = value.X,
					Y = value.Y,
					Z = value.Z,
					Days = RollDays(random, Quirks.Has(n, "drunk") ? 3 : (1 + random.Next(2))),
					Hour = 19 + random.Next(3),
					Len = 240 + random.Next(121)
				});
			}
		}
		if (onlyKind == null || onlyKind != "drink")
		{
			bool flag = onlyKind == "farms" || (onlyKind == null && gang != Gangs.Wapiti && !gang.OrdersNotFeelings && (n.Personality == Personality.Greedy || n.Personality == Personality.Hothead || Quirks.Has(n, "sadist") || random.Next(3) == 0));
			string text2 = array[random.Next(array.Length)];
			Vector3 vector = JobSpot(text2);
			if (vector != Vector3.Zero)
			{
				if (flag)
				{
					list.Add(new Habit
					{
						Kind = "farms",
						Town = text2,
						Place = "the farms outside " + text2,
						X = vector.X,
						Y = vector.Y,
						Z = vector.Z,
						Days = RollDays(random, 1 + random.Next(2)),
						Hour = 7 + random.Next(3),
						Len = 180
					});
				}
				else
				{
					list.Add(new Habit
					{
						Kind = "ride",
						Town = text2,
						Place = ((gang == Gangs.Wapiti) ? ("the hills outside " + text2) : ("the road out of " + text2)),
						X = vector.X,
						Y = vector.Y,
						Z = vector.Z,
						Days = RollDays(random, 2),
						Hour = 17 + random.Next(2),
						Len = 150
					});
				}
			}
		}
		n.Habits = list;
		GrudgeLog("HABITS " + n.Name + ": " + ((list.Count == 0) ? "none" : string.Join("; ", list.Select((Habit h) => HabitText(h)))));
		bool Want(string k)
		{
			if (onlyKind != null)
			{
				return onlyKind == k;
			}
			return true;
		}
	}

	internal string HabitText(Habit h)
	{
		List<string> list = (from d in Enumerable.Range(0, 7)
			where (h.Days & (1 << d)) != 0
			select DayNames[d]).ToList();
		string text = ((list.Count == 1) ? list[0] : (string.Join(", ", list.Take(list.Count - 1)) + " and " + list.Last()));
		string text2 = ((h.Kind == "drink") ? "nights" : ((h.Kind == "farms") ? "mornings" : "evenings"));
		string text3 = ((h.Kind == "drink") ? ("drinks at " + h.Place) : ((h.Kind == "farms") ? ("leans on " + h.Place) : (h.Place.StartsWith("the hills") ? ("hunts " + h.Place) : ("rides " + h.Place))));
		return text3 + " on " + text + " " + text2;
	}

	internal string KnownHabitText(NemesisRecord n)
	{
		List<Habit> list = n?.Habits?.Where((Habit h) => h.Known).ToList();
		if (list == null || list.Count == 0 || !n.IsAlive)
		{
			return null;
		}
		string text = string.Join(";  ", list.Take(2).Select(HabitText));
		return char.ToUpperInvariant(text[0]) + text.Substring(1);
	}

	private long HabitStart(Habit h, long now)
	{
		DateTime dateTime = FromGameMinutes(now);
		long num = now - (dateTime.Hour * 60 + dateTime.Minute);
		for (int i = -1; i <= 7; i++)
		{
			long num2 = num + (long)i * 1440L + (long)h.Hour * 60L;
			if ((h.Days & (1 << (int)FromGameMinutes(num2).DayOfWeek)) != 0 && num2 + h.Len >= now)
			{
				return num2;
			}
		}
		return -1L;
	}

	private (NemesisRecord rec, Habit h) HabitOf(Plan p)
	{
		if (p == null || p.Habit == null)
		{
			return (rec: null, h: null);
		}
		string[] parts = p.Habit.Split('/');
		NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == parts[0]);
		if (nemesisRecord?.Habits == null || parts.Length < 2 || !int.TryParse(parts[1], out var result) || result < 0 || result >= nemesisRecord.Habits.Count)
		{
			return (rec: nemesisRecord, h: null);
		}
		return (rec: nemesisRecord, h: nemesisRecord.Habits[result]);
	}

	private string HabitTitle(NemesisRecord n, Habit h)
	{
		if (!(h.Kind == "drink"))
		{
			if (!(h.Kind == "farms"))
			{
				if (!h.Place.StartsWith("the hills"))
				{
					return n.Name + " riding " + h.Place;
				}
				return n.Name + " hunting " + h.Place;
			}
			return n.Name + " leaning on " + h.Place;
		}
		return n.Name + " drinking at " + h.Place;
	}

	private void UpdateRoutines(Ped player)
	{
		if (Game.GameTime < _nextRoutineCheck)
		{
			return;
		}
		_nextRoutineCheck = Game.GameTime + 15000;
		long num = GameMinutesNow();
		if (num <= 0)
		{
			return;
		}
		MakeFunerals(num);
		CleanOutfitFolk();
		FixOutfitModels();
		FixRallyKlan();
		if (!Chronicle.DefectorWeeksFixed)
		{
			Chronicle.DefectorWeeksFixed = true;
			List<NemesisRecord> list = Roster.Where((NemesisRecord r) => r.IsAlive && r.Habits != null && (r.History?.Any((HistoryEntry h) => h.Text != null && h.Text.StartsWith("DEFECTED from")) ?? false)).ToList();
			foreach (NemesisRecord item in list)
			{
				NewWeekFor(item);
			}
			if (list.Count > 0)
			{
				SaveRoster();
			}
			SaveWorld();
		}
		foreach (Plan p in Plans.Where((Plan x) => !x.Done && (x.Kind == "habit" || x.Kind == "funeral")).ToList())
		{
			NemesisRecord nemesisRecord = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.LeaderId);
			if (nemesisRecord != null && nemesisRecord.IsAlive && nemesisRecord.CanHunt && !nemesisRecord.PresumedDead)
			{
				continue;
			}
			if (p.Kind == "funeral")
			{
				NemesisRecord nemesisRecord2 = Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Dead);
				NemesisRecord nemesisRecord3 = ((nemesisRecord2 == null) ? null : ((from k in KinOf(nemesisRecord2)
					select k.who).FirstOrDefault((NemesisRecord k) => k.IsAlive && k.CanHunt && !k.PresumedDead) ?? ((p.Gang == null) ? null : (from n in Roster
					where n.Gang == p.Gang && n.IsAlive && n.CanHunt && !n.PresumedDead && n.Rank >= 2
					orderby n.Rank descending
					select n).FirstOrDefault()) ?? ((p.Gang == null) ? null : (from n in Roster
					where n.Gang == p.Gang && n.IsAlive && n.CanHunt && !n.PresumedDead
					orderby n.Rank descending, n.Id
					select n).FirstOrDefault())));
				if (nemesisRecord3 != null)
				{
					p.LeaderId = nemesisRecord3.Id;
					GrudgeLog("FUNERAL " + p.Title + ": " + (nemesisRecord?.Name ?? "the mourner") + " is gone - " + nemesisRecord3.Name + " leads it now");
					continue;
				}
				if (p.Known && !p.Live)
				{
					ReplyNotice("Nobody's left to bury " + ((FuneralDead(p).Count > 0) ? FuneralNames(p) : "him") + ". The burying won't happen.");
				}
			}
			if (!p.Live)
			{
				p.Done = true;
				p.Outcome = "he's gone";
				GrudgeLog(((p.Kind == "habit") ? "HABIT" : "FUNERAL") + " " + p.Title + ": " + (nemesisRecord?.Name ?? "he") + " is gone - it won't happen");
				if (p.Kind == "habit" && p.Known)
				{
					ReplyNotice((nemesisRecord?.Name ?? "He") + " is gone - " + p.Title.TrimEnd('.') + " won't happen.");
				}
			}
		}
		bool flag = false;
		bool flag2 = false;
		foreach (NemesisRecord item2 in Roster.ToList())
		{
			if (!KeepsHabits(item2))
			{
				continue;
			}
			if (item2.Habits == null)
			{
				RollHabits(item2);
				flag2 = true;
			}
			if (item2.Habits == null)
			{
				continue;
			}
			for (int num2 = 0; num2 < item2.Habits.Count; num2++)
			{
				Habit habit = item2.Habits[num2];
				long num3 = HabitStart(habit, num);
				if (num3 >= 0 && num3 - num <= 360)
				{
					string key = item2.Id + "/" + num2 + "/" + num3;
					if (!Plans.Any((Plan plan) => plan.Habit == key))
					{
						Plans.Add(new Plan
						{
							Id = NewId(10),
							Gang = item2.Gang,
							Kind = "habit",
							Habit = key,
							LeaderId = item2.Id,
							Title = HabitTitle(item2, habit),
							Place = habit.Place,
							X = habit.X,
							Y = habit.Y,
							Z = habit.Z,
							DueAt = num3,
							WindowMin = habit.Len,
							Crew = ((!(habit.Kind == "drink")) ? (2 + _rng.Next(2)) : 0),
							Known = habit.Known
						});
						flag = true;
						GrudgeLog("HABIT " + item2.Name + ": " + HabitTitle(item2, habit) + " - " + WhenText(num3) + (habit.Known ? " (you know it)" : ""));
					}
				}
			}
		}
		if (flag2)
		{
			SaveRoster();
		}
		if (flag)
		{
			SaveWorld();
			RefreshPlanBlips();
		}
	}

	private void CleanOutfitFolk()
	{
		if (Chronicle.OutfitFolkCleaned)
		{
			return;
		}
		Chronicle.OutfitFolkCleaned = true;
		List<Acquaintance> gone = People.Where((Acquaintance a) => !a.Met && Gangs.ForModel(a.Model) != null && !string.IsNullOrEmpty(a.Town) && a.Grudge <= 0 && a.Favours <= 0 && a.Like <= 0 && !a.Lookout && !a.Reported && (a.Witnessed == null || a.Witnessed.Count == 0)).ToList();
		foreach (Acquaintance item in gone)
		{
			People.Remove(item);
		}
		foreach (int item2 in (from kv in _acqOf
			where gone.Contains(kv.Value)
			select kv.Key).ToList())
		{
			_acqOf.Remove(item2);
		}
		if (gone.Count > 0)
		{
			_peopleDirty = true;
			_townRepCache.Clear();
			_townWordCache.Clear();
		}
		SaveWorld();
		GrudgeLog(string.Format("PEOPLE clean-up: {0} gang men you'd only fought, never met, taken off the people you know ({1}{2})", gone.Count, string.Join(", ", from a in gone.Take(12)
			select a.Name), (gone.Count > 12) ? "..." : ""));
	}

	internal void DressForOutfit(NemesisRecord n, Gang g)
	{
		if (n == null || g == null)
		{
			return;
		}
		Gang gang = OutfitOfModel(n.ModelHash, g);
		if (gang != null && gang != g)
		{
			PedHash[] array = (from s in g.Seed?.Where(((int Rank, int Count, PedHash[] Models) s) => s.Rank <= Math.Max(1, n.Rank))
				orderby s.Rank descending
				select s.Models).FirstOrDefault() ?? ((g.TroopModels != null && g.TroopModels.Length != 0) ? g.TroopModels : null);
			if (array != null && array.Length != 0)
			{
				uint modelHash = n.ModelHash;
				n.ModelHash = (uint)array[(uint)Seeds.Stable("dress/" + n.Id + "/" + g.Id) % (uint)array.Length];
				n.OutfitHash = 0u;
				n.PlainModel = null;
				GrudgeLog($"DRESS {n.Name}: now one of the {g.Name} - their clothes (model {modelHash:X8} -> {n.ModelHash:X8})");
			}
		}
	}

	internal void NewWeekFor(NemesisRecord n)
	{
		if (n != null)
		{
			n.Habits = null;
			Plans.RemoveAll((Plan p) => !p.Done && p.Kind == "habit" && p.LeaderId == n.Id);
			if (KeepsHabits(n))
			{
				RollHabits(n);
			}
			GrudgeLog("HABITS " + n.Name + ": a new outfit, a new week");
		}
	}

	private static bool WearsModel(Gang g, uint m)
	{
		if (g != null)
		{
			if (g.Seed == null || !g.Seed.Any(((int Rank, int Count, PedHash[] Models) s) => s.Models != null && s.Models.Any((PedHash x) => x == (PedHash)m)))
			{
				if (g.TroopModels != null)
				{
					return g.TroopModels.Any((PedHash x) => x == (PedHash)m);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	private static Gang OutfitOfModel(uint m, Gang own = null)
	{
		Gang gang = Gangs.ForModel(m);
		if (gang == null)
		{
			if (!WearsModel(own, m))
			{
				return Gangs.All.FirstOrDefault((Gang g) => WearsModel(g, m));
			}
			gang = own;
		}
		return gang;
	}

	private void FixRallyKlan()
	{
		if (Chronicle.RallyKlanFixed)
		{
			return;
		}
		Chronicle.RallyKlanFixed = true;
		List<NemesisRecord> list = Roster.Where((NemesisRecord n) => n.IsAlive && n.Gang == null && !n.Story && RallyModel(n.ModelHash)).ToList();
		foreach (NemesisRecord item in list)
		{
			item.Gang = Gangs.Klan.Id;
			item.Rank = Math.Max(1, item.Rank);
			item.BossId = null;
			item.CampId = null;
			item.CampKnown = false;
			History(item, "JOINED THE KLAN: the hood was always his", Theme.Warn);
			GrudgeLog("KLAN " + item.Name + ": a rally man on the roster as a loner - one of the Klan now");
		}
		if (list.Count > 0)
		{
			SaveRoster();
		}
		SaveWorld();
		GrudgeLog($"KLAN clean-up: {list.Count} rally men moved into the Klan");
	}

	private void FixOutfitModels()
	{
		if (Chronicle.OutfitModelsFixed2)
		{
			return;
		}
		Chronicle.OutfitModelsFixed2 = true;
		Chronicle.OutfitModelsFixed = true;
		int num = 0;
		foreach (NemesisRecord item in Roster.Where((NemesisRecord r) => r.IsAlive && r.Gang != null && !r.Story))
		{
			Gang gang = Gangs.ById(item.Gang);
			Gang gang2 = OutfitOfModel(item.ModelHash, gang);
			if (gang != null && gang2 != null && gang2 != gang)
			{
				DressForOutfit(item, gang);
				NewWeekFor(item);
				num++;
			}
		}
		if (num > 0)
		{
			SaveRoster();
		}
		SaveWorld();
		GrudgeLog($"DRESS clean-up: {num} men in another outfit's clothes re-dressed");
	}

	private bool RoutineMayPlay(Plan p, Ped player, float d)
	{
		if (p.Kind == "habit" && !p.Known)
		{
			if (d > 120f)
			{
				return false;
			}
			if (_wildCameAt != 0 && Game.GameTime - _wildCameAt < 300000)
			{
				return false;
			}
		}
		Vector3 vector = new Vector3(p.X, p.Y, p.Z);
		int num = INTERIOR.GET_INTERIOR_AT_COORDS(vector.X, vector.Y, vector.Z);
		if (num != 0 && !INTERIOR.IS_INTERIOR_READY(num))
		{
			STREAMING.REQUEST_COLLISION_AT_COORD(vector.X, vector.Y, vector.Z);
			return false;
		}
		return true;
	}

	private EncounterCard RoutineCard(Plan p)
	{
		if (p.Kind == "funeral")
		{
			string text = ((FuneralDead(p).Count > 0) ? FuneralNames(p) : "one of ours");
			return new EncounterCard
			{
				Id = "plan-funeral",
				Stage = "idle",
				Title = p.Title,
				Standoff = true,
				StandoffSeconds = 30,
				EndsPeacefully = true,
				Activity = "WORLD_HUMAN_GUARD_LAZY",
				SightRange = 12f,
				Cast = new CardCast
				{
					Crew = new List<int> { p.Crew, p.Crew }
				},
				Taunts = new List<string>
				{
					"We're burying " + text + " today. Turn that horse around.",
					"You've got some gall, showing your face here.",
					"Not today. Not over " + text + "'s grave."
				},
				History = "BURYING " + text.ToUpperInvariant() + " at {place} - you came"
			};
		}
		Habit item = HabitOf(p).h;
		bool flag = item?.Kind == "drink";
		return new EncounterCard
		{
			Id = (flag ? "plan-habit-drink" : "plan-habit"),
			Stage = "idle",
			Title = p.Title,
			Standoff = (flag ? new bool?(true) : ((bool?)null)),
			EndsPeacefully = flag,
			SightRange = (flag ? 10f : 0f),
			Activity = ((item?.Kind == "drink") ? "WORLD_HUMAN_DRINKING" : ((item?.Kind == "farms") ? "WORLD_HUMAN_SMOKE" : "patrol")),
			Cast = new CardCast
			{
				Crew = new List<int> { p.Crew, p.Crew }
			},
			History = ((item?.Kind == "drink") ? "FOUND DRINKING at {place}" : ((item?.Kind == "farms") ? "FOUND LEANING ON FOLK at {place}" : "FOUND ON HIS RIDE at {place}"))
		};
	}

	private bool ResolveRoutine(Plan p, Ped player)
	{
		if (p.Kind == "habit")
		{
			p.Done = true;
			p.Outcome = (p.Live ? "you were there" : "his usual");
			var (rec, habit) = HabitOf(p);
			if (!p.Live || rec == null || habit == null || !rec.IsAlive || rec.PresumedDead)
			{
				return true;
			}
			if (rec.Scars.Count > p.ScarsAt)
			{
				int index = rec.Habits.IndexOf(habit);
				rec.Habits.RemoveAt(index);
				RollHabits(rec, habit.Kind, habit.Town);
				History(rec, "CHANGED HIS WAYS: no more " + habit.Place + " after you found him there", Theme.Dim);
				GrudgeLog("HABIT " + rec.Name + ": hurt at " + habit.Place + " - he's stopped going (a new habit somewhere else)");
				Plans.RemoveAll((Plan x) => !x.Done && x.Habit != null && x.Habit.StartsWith(rec.Id + "/"));
				SaveRoster();
				SaveWorld();
				return true;
			}
			habit.Seen++;
			if (!habit.Known && habit.Seen >= 2)
			{
				habit.Known = true;
				ReplyNotice(rec.Name + " again - he " + HabitText(habit) + ", it seems. You'll know where to look.");
				History(rec, "HIS HABIT LEARNED: he " + HabitText(habit) + " - you found him there twice", Theme.Warn);
				GrudgeLog("HABIT " + rec.Name + ": seen at " + habit.Place + " twice - you know it now");
			}
			SaveRoster();
			return true;
		}
		if (p.Kind == "funeral")
		{
			ResolveFuneral(p, player);
			return true;
		}
		return false;
	}

	private string AnswerHabit(Interrogation it, bool lying)
	{
		NemesisRecord who = it.Who;
		Gang gang = it.Gang;
		NemesisRecord boss = BossOf(who);
		if (boss == null || !KeepsHabits(boss))
		{
			return Nothing(it, "Him? He don't go nowhere but camp. I swear it.");
		}
		if (boss.Habits == null)
		{
			RollHabits(boss);
		}
		if (boss.Habits == null || boss.Habits.Count == 0)
		{
			return Nothing(it, boss.Name + " keeps to camp. Never seen him take a drink in town.");
		}
		List<Habit> list = boss.Habits.Where((Habit h) => !h.Known).ToList();
		if (list.Count == 0)
		{
			return Nothing(it, "You know " + boss.Name + "'s ways as well as I do.");
		}
		if (lying)
		{
			(Vector3?, Vector3?) value2;
			string[] array = (from t in JobTownsOf(gang)
				where TownSpots.TryGetValue(t, out value2) && value2.Item2.HasValue
				select t).ToArray();
			string text = ((array.Length != 0) ? array[_rng.Next(array.Length)] : null);
			if (text == null)
			{
				return Nothing(it, "I don't know! He don't tell me nothing!");
			}
			Vector3 value = TownSpots[text].Saloon.Value;
			long num = GameMinutesNow();
			DateTime dateTime = FromGameMinutes(num);
			long num2 = num - (dateTime.Hour * 60 + dateTime.Minute) + (long)(1 + _rng.Next(2)) * 1440L + 1200;
			Plan p = MakeLie("where his boss drinks", who, gang, value, "the " + text + " saloon", num2, boss.Name + " drinking at the " + text + " saloon");
			IntelNotice(p);
			return boss.Name + "... drinks at the " + text + " saloon. " + DayNames[(int)FromGameMinutes(num2).DayOfWeek] + " night. Go see for yourself.";
		}
		Habit habit = list[0];
		habit.Known = true;
		int idx = boss.Habits.IndexOf(habit);
		foreach (Plan item in Plans.Where((Plan x) => !x.Done && x.Habit != null && x.Habit.StartsWith(boss.Id + "/" + idx + "/")))
		{
			item.Known = true;
		}
		History(boss, "HIS HABITS GIVEN AWAY by " + Speaker(it) + ": he " + HabitText(habit), Theme.Warn);
		ReplyNotice("INTEL: " + boss.Name + " " + HabitText(habit) + ". It goes on your map when it's near.");
		Feed("INTEL " + boss.Name + " " + HabitText(habit) + " (from " + Speaker(it) + ")", Theme.Gold);
		try
		{
			WorldPart worldPart = ((it.Rec != null) ? PartFor(it.Rec, "TALKED", "under questioning") : new WorldPart
			{
				Name = Speaker(it),
				Outcome = "TALKED",
				Note = "under questioning"
			});
			ChronicleDeed("intel", Speaker(it) + " gave away " + boss.Name + "'s habits", habit.Place, null, boss.Gang, worldPart, PartFor(boss, "GIVEN UP", HabitText(habit)));
		}
		catch
		{
		}
		SaveRoster();
		SaveWorld();
		RefreshPlanBlips();
		GrudgeLog("HABIT " + boss.Name + ": " + Speaker(it) + " gave it up - he " + HabitText(habit));
		return boss.Name + "... he " + HabitText(habit) + ". Don't you tell him where you heard it.";
	}
}
