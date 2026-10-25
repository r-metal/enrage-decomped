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
	internal string InterroNote = "";

	internal int DebugMode;

	internal static readonly string[] DebugModeNames = new string[7] { "off", "ALL", "TENSION / ENCOUNTERS", "GRUDGES", "WORLD EVENTS", "REPUTATION", "BARS ONLY" };

	private const ulong NumComponentCategories = 11971384123627577485uL;

	private const ulong ComponentCategoryAt = 14751957644553774639uL;

	private static readonly string[] KnownCategories = new string[34]
	{
		"MASKS", "MASKS_LARGE", "HATS", "NECKWEAR", "NECKTIES", "EYEWEAR", "COATS", "COATS_CLOSED", "SHIRTS_FULL", "VESTS",
		"PANTS", "BOOTS", "GLOVES", "GUNBELTS", "SATCHELS", "CLOAKS", "PONCHOS", "BANDANAS", "SPATS", "HAIR",
		"BEARDS_COMPLETE", "HEADS", "BODIES_UPPER", "BODIES_LOWER", "TEETH", "EYES", "LOADOUTS", "SUSPENDERS", "BELTS", "BELT_BUCKLES",
		"HOLSTERS_LEFT", "HOLSTERS_RIGHT", "JEWELRY_RINGS_RIGHT", "CHAPS"
	};

	private static Dictionary<uint, string> _categoryNames;

	private bool _masked;

	private HashSet<uint> _lastPieceCats;

	internal bool DebugOn => _debugOn;

	internal static string Compass(Vector3 from, Vector3 to)
	{
		double num = Math.Atan2(to.X - from.X, to.Y - from.Y) * 180.0 / Math.PI;
		if (num < 0.0)
		{
			num += 360.0;
		}
		return (new string[8] { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" })[(int)Math.Round(num / 45.0) % 8];
	}

	private void ToggleDebug()
	{
		DebugMode = (DebugMode + 1) % DebugModeNames.Length;
		_debugOn = DebugMode != 0;
		XRayViewer.DebugOn = _debugOn;
		NemesisRecord.ShowSecrets = _debugOn;
		if (!_debugOn)
		{
			HideMapIcons();
		}
		ShowNotification(_debugOn ? ("enRAGE debug: " + DebugModeNames[DebugMode] + " (Ctrl+F11: next)") : "enRAGE debug overlay off.");
	}

	private bool DebugShows(string section)
	{
		switch (DebugMode)
		{
		case 1:
			if (section != "WORLD EVENTS" && section != "REPUTATION" && section != "PEOPLE NEAR YOU")
			{
				return section != "RECENT";
			}
			return false;
		case 2:
			switch (section)
			{
			default:
				return section == "NEARBY NEMESES";
			case "TENSION":
			case "ENCOUNTERS":
			case "REGION / FACTIONS":
				return true;
			}
		case 3:
			switch (section)
			{
			default:
				return section == "NAMING / SYSTEM";
			case "NEARBY NEMESES":
			case "RECENT ACTIONS":
			case "WITNESSES":
			case "NERVE":
				return true;
			}
		case 4:
			if (!(section == "WORLD"))
			{
				return section == "WORLD EVENTS";
			}
			return true;
		case 5:
			switch (section)
			{
			default:
				return section == "WITNESSES";
			case "REPUTATION":
			case "PEOPLE NEAR YOU":
			case "RECENT":
			case "NERVE":
				return true;
			}
		default:
			return false;
		}
	}

	internal static string Away(Vector3 me, Vector3 at)
	{
		return $"{at.DistanceTo(me):0} m {Compass(me, at)}";
	}

	private List<(string, (int r, int g, int b))> HappeningNow(Ped player)
	{
		List<(string, (int, int, int))> list = new List<(string, (int, int, int))>();
		Vector3 me = player.Position;
		if (_enc != null && ActiveRecord != null && _activeNemesisPed != null && _activeNemesisPed.Exists())
		{
			_activeNemesisPed.Position.DistanceTo(me);
			string text = (_enc.Surrendered ? "SURRENDERED" : (_enc.Engaged ? "FIGHTING YOU" : (_enc.Spotted ? "spotted you" : "hasn't seen you")));
			list.Add(("NOW " + _enc.Kind.ToString().ToUpperInvariant() + " [" + _enc.Card?.Id + "]: " + ActiveRecord.FullName + ((_followers.Count > 0) ? $" + {_followers.Count((Ped f) => f.Exists() && !f.IsDead)} men" : "") + ((_enc.Activity != "") ? (", " + _enc.Activity) : "") + " - " + Away(me, _activeNemesisPed.Position) + " - " + text, _enc.Engaged ? Theme.Blood : Theme.Warn));
		}
		foreach (TownMan item in _townMen.Where((TownMan t) => t.Ped != null && t.Ped.Exists()))
		{
			list.Add(("NOW TOWN: " + item.Rec.Name + " in " + item.Town + " (" + (item.Hunter ? "hunting you" : item.Intent.ToString()) + ") - " + Away(me, item.Ped.Position) + " - " + (item.Engaged ? "FIGHTING YOU" : (item.Spotted ? "spotted you" : "hasn't seen you")), item.Engaged ? Theme.Blood : Theme.Gold));
		}
		foreach (LiveCamp value in _liveCamps.Values)
		{
			Vector3 at = SitePos(value.Site);
			list.Add((string.Format("NOW CAMP: {0} at {1} - {2}{3} - {4} men - {5}", value.Boss.Name, value.Site.Label, Away(me, at), (value.Site.Id != null && value.Boss.CampKnown) ? "" : " (not on your map)", value.Peds.Count((Ped p) => p.Exists() && !p.IsDead), value.Engaged ? "FIGHTING" : $"suspicion {value.Suspicion:0}"), value.Engaged ? Theme.Blood : Theme.Gold));
		}
		list.AddRange(PatrolLines(player));
		string text2 = WarEncounterLine();
		if (text2 != null)
		{
			list.Add((text2, Theme.Warn));
		}
		if (_skirmish.Count > 0)
		{
			List<Ped> list2 = _skirmish.Where((Ped p) => p != null && p.Exists() && !p.IsDead).ToList();
			if (list2.Count > 0)
			{
				Vector3 at2 = new Vector3(list2.Average((Ped p) => p.Position.X), list2.Average((Ped p) => p.Position.Y), list2.Average((Ped p) => p.Position.Z));
				list.Add(($"NOW SKIRMISH: {SkirmishNote} - {Away(me, at2)} - {list2.Count} still standing", Theme.Warn));
			}
			else
			{
				list.Add(("NOW SKIRMISH: " + SkirmishNote + " - all down", Theme.Warn));
			}
		}
		List<Ped> list3 = (from h in _hunters
			where h != null && h.Exists() && !h.IsDead
			orderby h.Position.DistanceTo(me)
			select h).ToList();
		if (list3.Count > 0)
		{
			list.Add(($"NOW BOUNTY HUNTERS: {list3.Count} riding - {Away(me, list3[0].Position)}", Theme.Blood));
		}
		if (_general != null && _general.Exists() && !_general.IsDead)
		{
			list.Add(("NOW THE GENERAL: " + Gangs.Army.LeaderName + "'s column - " + Away(me, _general.Position), Gangs.Army.Accent));
		}
		if (_sightingPed != null && _sightingPed.Exists() && _sightingRec != null)
		{
			list.Add(("NOW SIGHTING: " + _sightingRec.Name + " - " + Away(me, _sightingPed.Position), Theme.Gold));
		}
		return list;
	}

	private void DrawDebug(Ped player)
	{
		if (!_debugOn || _menu.IsOpen || _bulletImpact.ViewerOpen || _iniPanel.IsOpen || DebugMode == 6)
		{
			return;
		}
		List<(Ped ped, NemesisRecord rec)> list = new List<(Ped ped, NemesisRecord rec)>();
		if (DebugShows("NEARBY NEMESES"))
		{
			foreach (KeyValuePair<int, NemesisRecord> worldNemese in _worldNemeses)
			{
				Ped ped = new Ped(worldNemese.Key);
				if (ped.Exists() && ped.Position.DistanceTo(player.Position) < 90f)
				{
					list.Add((ped, worldNemese.Value));
				}
			}
		}
		if (DebugShows("NEARBY NEMESES") && _activeNemesisPed != null && _activeNemesisPed.Exists() && ActiveRecord != null)
		{
			list.Add((_activeNemesisPed, ActiveRecord));
		}
		list = (from x in list
			group x by x.ped.Handle into g
			select g.First() into x
			orderby x.ped.Position.DistanceTo(player.Position)
			select x).Take(10).ToList();
		List<(int kind, string text, (int r, int g, int b) c)> lines = new List<(int, string, (int, int, int))>();
		long nowM = GameMinutesNow();
		if (Config.TensionEnabled && DebugShows("TENSION"))
		{
			H("TENSION");
			L(TensionDebugLine(), (Tension >= FireLevel) ? Theme.Blood : ((Tension >= FireLevel * 0.75f) ? Theme.Warn : Theme.Title));
			lines.Add((3, "", Theme.Dim));
			L(TensionMarksLine(), Theme.Faint);
		}
		if (DebugShows("ENCOUNTERS"))
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			foreach (NemesisRecord item6 in Roster.Where((NemesisRecord r) => r.IsAlive))
			{
				switch (IntentOf(item6))
				{
				case Intent.Hunts:
					num++;
					break;
				case Intent.WontHunt:
					num2++;
					break;
				case Intent.ProvokedOnly:
					num3++;
					break;
				default:
					num4++;
					break;
				}
				if (HiresHunters(item6))
				{
					num5++;
				}
			}
			int num6 = Math.Max(0, (_nextAmbushCheck - Game.GameTime) / 1000);
			H("ENCOUNTERS");
			List<(string, (int r, int g, int b))> list2 = HappeningNow(player);
			if (list2.Count == 0)
			{
				L("nothing happening around you", Theme.Dim);
			}
			foreach (var (t, c) in list2)
			{
				B(t, c);
			}
			if (ActiveRecord != null)
			{
				L(ActiveNerveLine(), Theme.Warn);
			}
			bool flag = _currentTown != null;
			foreach (string item7 in CardReportLines())
			{
				L(item7, (item7.StartsWith("   ") && item7.Contains("no:")) ? Theme.Faint : Theme.Dim);
			}
			if (TownCalmNote != "")
			{
				L("town calm: " + TownCalmNote, Theme.Dim);
			}
			if (!Config.TensionEnabled)
			{
				L(string.Format("next roll in {0}s  -  {1} chance{2}", num6, flag ? $"in town every {Balance.TownEncounterSeconds}s, {Balance.TownEncounterChance}%" : $"every {EncounterIntervalMs() / 1000}s, {Config.AmbushChancePercent}%", Config.RandomAmbushes ? "" : "  -  RANDOM ENCOUNTERS OFF"));
			}
			if (flag)
			{
				L($"town {_currentTown}: {_townMen.Count}/{Balance.TownMaxMen} nemeses, {_regulars.Count((Ped r) => r.Exists())}/{Config.RegularsMax} people you know", Theme.Gold);
			}
			string text = CalmDebugLine(player);
			L(text, text.StartsWith("HELD") ? Theme.Warn : Theme.Dim);
			long num7 = (long)Balance.MurderousCooldownHours * 60L - (nowM - _lastMurderousAt);
			int num8 = Roster.Count((NemesisRecord n) => n.IsAlive && ManCooling(n));
			L(string.Format("cooldowns: murderous {0}  -  {1} men came lately", (num7 > 0) ? $"{num7 / 60}h {num7 % 60:00}m left" : "ready", num8), (num7 > 0) ? Theme.Warn : Theme.Dim);
			L("last roll: " + (string.IsNullOrEmpty(LastEncounterNote) ? "-" : LastEncounterNote), Theme.Dim);
			L($"roster: {num} hunt  {num2} wait  {num3} only if provoked  {num4} can't  -  {num5} would hire guns  -  {Roster.Count((NemesisRecord n) => n.IsAlive && Resting(n))} lying low");
			List<(NemesisRecord, double)> list3 = (from n in Roster
				where n.CanHunt && IntentOf(n) == Intent.Hunts && !CoolingOff(n)
				select (n: n, w: AmbushWeightOf(n)) into tuple6
				where tuple6.w > 0.0
				orderby tuple6.w descending
				select tuple6).Take(4).ToList();
			double tot = Roster.Where((NemesisRecord n) => n.CanHunt && IntentOf(n) == Intent.Hunts && !CoolingOff(n)).Sum((Func<NemesisRecord, double>)AmbushWeightOf);
			if (list3.Count > 0)
			{
				L("likeliest out here: " + string.Join(",  ", list3.Select<(NemesisRecord, double), string>(((NemesisRecord n, double w) tuple6) => $"{tuple6.n.Name.Split(' ')[0]} ({tuple6.n.HateTier.ToString().Substring(0, 4).ToLowerInvariant()}) {tuple6.w / Math.Max(0.0001, tot) * 100.0:0}%")), Theme.Dim);
			}
		}
		if (DebugShows("REGION / FACTIONS"))
		{
			H("REGION / FACTIONS");
			L(string.Format("{0}{1}  -  bounty ${2}  -  Army after you: {3}", RegionName, _nearFort ? "  -  Fort Wallace area (Army x3)" : "", BountyDollars, ArmyAfterYou ? "YES" : "no"));
			L(RegionRaw, Theme.Faint);
			string text2 = ControlHereLine();
			(Gang, Gang)? warHere = WarHere;
			L(text2 + (warHere.HasValue ? ("  -  WAR ZONE: " + warHere.Value.Item1.Name + " vs " + warHere.Value.Item2.Name + " (skirmishes x3)") : ""), Theme.Dim);
			Gang[] all = Gangs.All;
			foreach (Gang gang in all)
			{
				int num10 = NotorietyOf(gang);
				L($"{gang.Name}: {Notoriety.LevelName(num10)} {num10}  -  encounters x{((CurrentDistrict != null) ? gang.EncounterRate : gang.AmbushBase) * Notoriety.Ambush(num10) * RegionMult(gang):0.00} (base {((CurrentDistrict != null) ? gang.EncounterRate : gang.AmbushBase):0.00}, notoriety {Notoriety.Ambush(num10):0.0}, here {RegionMult(gang):0.0})  +{CrewBonus(gang)} men", gang.Accent);
			}
		}
		if (DebugShows("WORLD"))
		{
			H("WORLD");
			if (!string.IsNullOrEmpty(CampAwareNote))
			{
				L("camp: " + CampAwareNote, Theme.Warn);
			}
			int num11 = Chronicle.Events.Count((WorldEvent e) => e.HasPos && e.AftermathUntil > nowM && !e.Visited && (e.Heard || Config.WorldKnowAll));
			L(HistoryDebugLine(), Theme.Dim);
			L(DeathNewsLine(), Theme.Dim);
			L(GroupsDebugLine(), Theme.Dim);
			L(SavePointDebugLine(), Theme.Dim);
			L(GameSystemsDebugLine(), Theme.Dim);
			L(StoryDebugLine(), Theme.Dim);
			L(TelegraphDebugLine(), Theme.Dim);
			L(EntityDebugLine(), Theme.Dim);
			L(CardsDebugLine(), Theme.Dim);
			L(MindDebugLine(), Theme.Dim);
			L($"world events: next in {NextWorldEventText()}  -  {Chronicle.Events.Count} on record  -  {num11} battlefields to find", Theme.Dim);
			L(string.Format("people: {0} remembered ({1} met), {2} regulars around  -  seats: {3}", People.Count((Acquaintance a) => !a.Dead), People.Count((Acquaintance a) => a.Met && !a.Dead), _regulars.Count((Ped r) => r.Exists()), (SeatNote == "") ? "-" : SeatNote), Theme.Dim);
			L("theft: " + (string.IsNullOrEmpty(TheftNote) ? "-" : TheftNote), Theme.Dim);
			L("placement: " + (string.IsNullOrEmpty(NaturalNote) ? "-" : NaturalNote) + "  -  " + SeatingDebugLine(), Theme.Dim);
			L("disguise: " + (PlayerMasked(player) ? "ON - face covered" : "off") + ((DisguiseSaves > 0) ? $"  (saved you {DisguiseSaves}x)" : ""), PlayerMasked(player) ? Theme.Good : Theme.Dim);
		}
		if (list.Count > 0)
		{
			H("NEARBY NEMESES");
			foreach (var (ped2, nemesisRecord) in list.Take(5))
			{
				L($"{nemesisRecord.Name}  -  {nemesisRecord.Personality}, {Traits.Aggr(nemesisRecord.Aggression).ToLowerInvariant()}  -  hate {nemesisRecord.Hate}  fear {EffectiveFear(nemesisRecord)}  -  {Traits.Short(IntentOf(nemesisRecord))}  -  {ped2.Position.DistanceTo(player.Position):0} m  -  {MindShort(nemesisRecord)}", Theme.Title);
			}
		}
		if (DebugShows("RECENT ACTIONS"))
		{
			H("RECENT ACTIONS");
			if (_convo != null)
			{
				L("talking: " + ConvoNote, Theme.Warn);
			}
			if (_feed.Count == 0 && _convo == null)
			{
				L("nothing yet", Theme.Dim);
			}
			foreach (var item8 in _feed.AsEnumerable().Reverse().Take(12))
			{
				int item = item8.at;
				string item2 = item8.text;
				(int, int, int) item3 = item8.c;
				int num12 = Math.Max(0, (UiDraw.RealMs - item) / 1000);
				L(string.Format("{0,4}   {1}", (num12 < 60) ? (num12 + "s") : (num12 / 60 + "m"), item2), item3);
			}
		}
		if (DebugShows("WORLD EVENTS"))
		{
			H("WORLD EVENTS");
			L(WarZonesLine(), Theme.Warn);
			L(RelationsLine(), Theme.Dim);
			L(GrievanceLine(), Theme.Dim);
			L(SandboxLine(), Theme.Dim);
			L(PurseLine(), Theme.Dim);
			L(MenLine(), Theme.Dim);
			L(AgendaLine(), Theme.Dim);
			foreach (string item9 in ControlLines())
			{
				L(item9, Theme.Dim);
			}
			L($"next batch in {NextWorldEventText()}  -  {Chronicle.Events.Count} events on record, {Chronicle.Past?.Count ?? 0} from the years before", Theme.Text);
			foreach (WorldEvent item10 in (from e in Chronicle.Events
				where e.HasPos && e.AftermathUntil > nowM && !e.Reached
				orderby new Vector3(e.X, e.Y, e.Z).DistanceTo(player.Position)
				select e).Take(4))
			{
				float num13 = new Vector3(item10.X, item10.Y, item10.Z).DistanceTo(player.Position);
				long num14 = item10.AftermathUntil - nowM;
				L(string.Format("battlefield {0:0} m: {1}  -  {2}h left{3}{4}{5}", num13, item10.Title, num14 / 60, item10.Visited ? ", bodies out" : "", item10.Marked ? ", marked" : "", (num13 < Config.WorldMarkerRadius) ? ", ? showing" : $", ? at {Config.WorldMarkerRadius:0} m"), KindColor(item10.Kind));
			}
			foreach (WorldEvent item11 in Chronicle.Events.AsEnumerable().Reverse().Take(10))
			{
				int num15 = item11.People.Count((WorldPart p) => p.Outcome == "KILLED");
				L((item11.When ?? "").Replace("  ", " ") + "  " + item11.Kind + "  " + item11.Title + ((num15 > 0) ? $"  ({num15} dead)" : "") + (item11.Heard ? "" : "  [not heard]"), KindColor(item11.Kind));
			}
		}
		if (DebugMode == 5)
		{
			H("REPUTATION");
			foreach (string item12 in ReputationLines())
			{
				L(item12);
			}
			H("PEOPLE NEAR YOU");
			foreach (string item13 in PeopleNearLines(player))
			{
				L(item13, Theme.Dim);
			}
			H("RECENT");
			foreach (string item14 in RecentRepLines())
			{
				L(item14, Theme.Dim);
			}
		}
		if (DebugShows("NERVE"))
		{
			H("NERVE");
			foreach (string item15 in NerveDebugLines(player))
			{
				L(item15, Theme.Dim);
			}
		}
		if (DebugShows("WITNESSES"))
		{
			H("WITNESSES");
			foreach (string item16 in WitnessDebugLines(player))
			{
				L(item16, Theme.Dim);
			}
		}
		if (DebugShows("NAMING / SYSTEM"))
		{
			H("NAMING / SYSTEM");
			int num16 = _named.Values.Count((Named n) => n.Importance >= 1);
			L($"{_named.Count} named ({num16} remembered, {_named.Values.Count((Named n) => n.Met)} met)  -  given {_namesGiven}, forgotten {_namesDropped}  -  radius {Config.NamingRadius:0} m");
			L(NamingScanNote, Theme.Dim);
			L(FingerprintDebugLine(), Theme.Dim);
			List<(Named, float)> list4 = (from kv in _named
				where ENTITY.DOES_ENTITY_EXIST(kv.Key)
				select (Value: kv.Value, d: new Ped(kv.Key).Position.DistanceTo(player.Position)) into tuple6
				orderby tuple6.d
				select tuple6).Take(16).ToList();
			if (list4.Count > 0)
			{
				L("closest (* met): " + string.Join(", ", list4.Select<(Named, float), string>(((Named Value, float d) tuple6) => string.Format("{0}{1} {2:0}m", tuple6.Value.Met ? "*" : "", tuple6.Value.Name, tuple6.d))), Theme.Dim);
			}
			if (ErrorCount > 0)
			{
				L($"ERRORS {ErrorCount} - last: {LastError} (grudges.log)", Theme.Blood);
			}
			bool flag2 = WorldBusy(player);
			L(flag2 ? ("QUIET (" + QuietWhy + ") - paused: grudges, world nemeses, naming, camps, interrogation, settling, sightings, theft, region, General, cripple holds  -  still running: events, encounters' own bookkeeping, UI, X-ray capture, prompt cleanup") : "quiet: no - every system running", flag2 ? Theme.Warn : Theme.Faint);
			L(string.Format("ownership: {0}  -  {1} entities registered as ours", (OwnCheckState > 0) ? "the game's script check works" : ((OwnCheckState < 0) ? "script check reads false - the mod's own registry decides" : "not checked yet"), OwnRegistered), (OwnCheckState < 0) ? Theme.Warn : Theme.Faint);
			L(string.Format("text slots {0}/{1}{2}", UiDraw.LastFrameSlots, UiDraw.TextSlotLimit, (UiDraw.LastFrameSkipped > 0) ? $"  -  {UiDraw.LastFrameSkipped} texts cut (too much on screen)" : ""), (UiDraw.LastFrameSkipped > 0) ? Theme.Warn : Theme.Faint);
		}
		if (DebugMode == 1 || DebugMode == 3)
		{
			DrawGrudgeMeters(player);
		}
		List<(int, string, (int, int, int))> list5 = new List<(int, string, (int, int, int))>();
		string section = "";
		foreach (var item17 in lines)
		{
			if (item17.kind == 1)
			{
				section = item17.text;
			}
			if (DebugShows(section))
			{
				list5.Add(item17);
			}
		}
		lines.Clear();
		lines.AddRange(list5);
		int num17 = 112;
		List<(int, string, (int, int, int))> list6 = new List<(int, string, (int, int, int))>();
		foreach (var (num18, text3, item4) in lines)
		{
			int num19;
			switch (num18)
			{
			case 1:
			case 3:
				list6.Add((num18, text3, item4));
				continue;
			default:
				num19 = num17;
				break;
			case 2:
				num19 = (int)((float)num17 * 0.85f);
				break;
			}
			int num20 = num19;
			string text4 = text3;
			bool flag3 = true;
			while (text4.Length > 0)
			{
				int num21 = ((text4.Length <= num20) ? text4.Length : Math.Max(num20 / 2, text4.LastIndexOf(' ', Math.Min(text4.Length - 1, num20))));
				list6.Add((num18, (flag3 ? "" : "     ") + text4.Substring(0, num21).TrimEnd(), item4));
				text4 = text4.Substring(num21).TrimStart();
				flag3 = false;
			}
		}
		float num22 = 0.898f;
		float num23 = 0f;
		int num24 = UiDraw.TextSlotsLeft - 8;
		int num25 = 0;
		foreach (var item18 in list6)
		{
			int item5 = item18.Item1;
			float num26 = item5 switch
			{
				3 => 0.042f, 
				1 => 0.021f, 
				_ => 0.015f, 
			};
			int num27 = ((item5 != 3) ? ((item5 == 0 && !UiDraw.GameFont) ? 1 : 2) : 0);
			if (num23 + num26 > num22 || num24 - num27 < 0)
			{
				break;
			}
			num23 += num26;
			num24 -= num27;
			num25++;
		}
		if (num25 < list6.Count)
		{
			list6 = list6.Take(num25).ToList();
		}
		float h = 0.034f + num23 + 0.008f;
		Theme.DebugPanel(0.01f, 0.04f, 0.335f, h);
		UiDraw.TextFont((DebugMode == 1) ? "enRAGE DEBUG" : ("enRAGE DEBUG - " + DebugModeNames[DebugMode]), UiDraw.TitleFont, 0.02f, 0.047f, 0.26f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.Text("Ctrl+F11 next mode", 0.335f - Theme.TextWidth("Ctrl+F11 next mode", 0.16f), 0.051f, 0.16f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b);
		float num28 = 0.074f;
		foreach (var (num29, text5, tuple5) in list6)
		{
			switch (num29)
			{
			case 1:
			{
				num28 += 0.006f;
				UiDraw.TextFont(text5, UiDraw.TitleFont, 0.02f, num28 - 0.002f, 0.19f, tuple5.Item1, tuple5.Item2, tuple5.Item3);
				float num30 = 0.02f + Theme.CapsWidth(text5, 0.19f) + 0.01f;
				Theme.HRule(num30, num28 + 0.008f, 0.335f - num30, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 200);
				break;
			}
			case 3:
				DrawTensionGraph(0.02f, num28 + 0.002f, 0.315f, 0.034f);
				num28 += 0.042f;
				continue;
			case 2:
				UiDraw.TextFont(text5, UiDraw.TitleFont, 0.02f, num28 - 0.001f, 0.17300001f, tuple5.Item1, tuple5.Item2, tuple5.Item3);
				break;
			default:
				UiDraw.Text(text5, 0.02f, num28, 0.158f, tuple5.Item1, tuple5.Item2, tuple5.Item3);
				break;
			}
			num28 += 0.015f;
		}
		void B(string item6, (int r, int g, int b) item7)
		{
			lines.Add((2, item6, item7));
		}
		void H(string item6)
		{
			lines.Add((1, item6, Theme.Gold));
		}
		void L(string item6, (int r, int g, int b)? tuple6 = null)
		{
			lines.Add((0, item6, tuple6 ?? Theme.Text));
		}
	}

	private static Dictionary<uint, string> CategoryNames => _categoryNames ?? (_categoryNames = KnownCategories.ToDictionary((string n) => MISC.GET_HASH_KEY(n), (string n) => n));
}
