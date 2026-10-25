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
	private Newspaper _paper;

	private Newspaper _paperShown;

	private int _pPaper;

	private int _pPaperFor;

	private int _pPaperGroup;

	private bool _paperArmed;

	private string _paperPromptText;

	private static readonly string[] Ads = new string[6] { "DR. HARLAN'S NERVE TONIC|Cures Melancholy, Catarrh & the Shakes. Sold at all good druggists.", "GUNSMITH|Repeaters, Pistols & Shotguns Bought, Sold & Repaired. Fair Prices, No Questions.", "FINE HORSES|Bought, Sold & Broken. Apply at the Stables. Cash Paid.", "THE TERRITORIAL STAGE LINE|Daily Service. Armed Guards on Every Coach. Book at the Depot.", "WANTED: HONEST MEN|For Ranch Work. Board & Wages. No Drunkards Need Apply.", "PHOTOGRAPHIC PORTRAITS|Taken in the Latest Style. Your Likeness for Posterity, Fifty Cents." };

	private static readonly string[] NumWords = new string[13]
	{
		"no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
		"ten", "eleven", "twelve"
	};

	private static readonly (int r, int g, int b) Paper = (r: 223, g: 211, b: 182);

	private static readonly (int r, int g, int b) Ink = (r: 30, g: 24, b: 18);

	private static readonly (int r, int g, int b) Faded = (r: 100, g: 86, b: 66);

	private static readonly Dictionary<string, List<string>> _wrapCache = new Dictionary<string, List<string>>();

	private int _nextNightBlastRoll;

	private int _nightBlastAllowed;

	private unsafe string GoreOf(Ped dead)
	{
		uint num = PED.GET_PED_CAUSE_OF_DEATH(dead.Handle);
		if (num == 0)
		{
			return null;
		}
		uint num2 = WEAPON.GET_WEAPONTYPE_GROUP(num);
		if (num2 == 1548507267 || num2 == 2725924767u)
		{
			object obj;
			if (!Enum.IsDefined(typeof(eWeapon), num))
			{
				obj = "";
			}
			else
			{
				eWeapon eWeapon = (eWeapon)num;
				obj = eWeapon.ToString();
			}
			string text = (string)obj;
			if (Ammo.MachineGunName(text))
			{
				return "riddle a man";
			}
			if (!text.Contains("Knife") && !text.Contains("Knives") && !text.Contains("Tomahawk") && !text.Contains("Hatchet"))
			{
				return "blow a man apart";
			}
		}
		Ped character = Game.Player.Character;
		float num3 = dead.Position.DistanceTo(character.Position);
		if (num2 == 3566412244u)
		{
			object obj2;
			if (!Enum.IsDefined(typeof(eWeapon), num))
			{
				obj2 = "";
			}
			else
			{
				eWeapon eWeapon = (eWeapon)num;
				obj2 = eWeapon.ToString();
			}
			string text2 = (string)obj2;
			if (text2.Contains("Knife") || text2.Contains("Machete") || text2.Contains("Hatchet") || text2.Contains("Cleaver") || text2.Contains("Tomahawk") || text2.Contains("Sword"))
			{
				int value;
				Fighter value2;
				bool num4 = (_combatants.TryGetValue(dead.Handle, out value) && Game.GameTime - value < 5000) || (_fighters.TryGetValue(dead.Handle, out value2) && !value2.Done);
				Vector3 vector = character.Position - dead.Position;
				vector.Z = 0f;
				Vector3 forwardVector = dead.ForwardVector;
				forwardVector.Z = 0f;
				bool flag = vector.Length() > 0.01f && forwardVector.Length() > 0.01f && Vector3.Dot(Vector3.Normalize(vector), Vector3.Normalize(forwardVector)) > 0.3f;
				if (num4 || flag)
				{
					return "butcher a man";
				}
				GrudgeLog("GORE not counted: a quiet blade kill from behind (" + WhoIs(dead.Handle) + ")");
				return null;
			}
		}
		int num5 = 0;
		PED.GET_PED_LAST_DAMAGE_BONE(dead.Handle, &num5);
		BoneID boneID = (BoneID)num5;
		if (boneID.ToString().Contains("HEAD") && (((num2 == 3082541095u || num2 == 970310034) && num3 < 25f) || (num2 == 860033945 && num3 < 15f)))
		{
			return "blow a man's head off";
		}
		return null;
	}

	internal static float GoreScale(float d)
	{
		if (!(d <= 10f))
		{
			if (!(d >= 40f))
			{
				return 1f - (d - 10f) / 30f;
			}
			return 0f;
		}
		return 1f;
	}

	internal static int GoreAt(float d, int plain)
	{
		return (int)Math.Round((float)plain + (float)(Balance.GoreFear - plain) * GoreScale(d));
	}

	private static string PaperName(string town)
	{
		switch (town)
		{
		case "Saint Denis":
		case "Rhodes":
			return "The Saint Denis Times";
		case "Blackwater":
		case "Strawberry":
			return "The Blackwater Ledger";
		case "Tumbleweed":
		case "Armadillo":
		case "Benedict Point":
			return "The New Austin Herald";
		default:
			return "The New Hanover Gazette";
		}
	}

	private string PaperTown(Ped player)
	{
		return _currentTown ?? TownAt(player.Position) ?? DeedTown(player.Position) ?? "Valentine";
	}

	private void PaperPrompt(Ped clerk, int group, Ped player)
	{
		string text = PaperTown(player);
		bool flag = _paper != null && _paper.Day == GameDayKey() && PaperName(_paper.Town) == PaperName(text);
		string text2 = (flag ? "Read Today's Paper" : "Buy a Newspaper - 5 cents");
		if (_pPaper == 0 || !PromptValid(_pPaper) || _pPaperFor != clerk.Handle || _pPaperGroup != group || _paperPromptText != text2)
		{
			Crumbs.Native("Paper prompt delete");
			if (PromptValid(_pPaper))
			{
				HUD._UI_PROMPT_DELETE(_pPaper);
			}
			_pPaper = MakePrompt(eInputType.InteractOption2, text2, hold: true, group);
			_pPaperFor = clerk.Handle;
			_pPaperGroup = group;
			_paperPromptText = text2;
		}
		HUD._UI_PROMPT_SET_VISIBLE(_pPaper, toggle: true);
		HUD._UI_PROMPT_SET_ENABLED(_pPaper, toggle: true);
		if (!HUD._UI_PROMPT_HAS_HOLD_MODE_COMPLETED(_pPaper))
		{
			_paperArmed = true;
		}
		else
		{
			if (!_paperArmed)
			{
				return;
			}
			_paperArmed = false;
			if (!flag)
			{
				if (MONEY._MONEY_GET_CASH_BALANCE() < 5)
				{
					ShowNotification("You can't spare a nickel for the paper.");
					return;
				}
				MONEY._MONEY_DECREMENT_CASH_BALANCE(5);
				_paper = BuildPaper(text, player);
				GrudgeLog($"PAPER bought {_paper.Name} in {text}: lead '{_paper.Lead.Head}', {_paper.Stories.Count} stories, {_paper.About.Count} about town");
			}
			OpenPaper(_paper);
		}
	}

	private void HidePaperPrompt()
	{
		if (_pPaper != 0)
		{
			Crumbs.Native("Paper prompt delete");
			if (PromptValid(_pPaper))
			{
				HUD._UI_PROMPT_DELETE(_pPaper);
			}
			_pPaper = 0;
			_pPaperFor = 0;
		}
	}

	internal void DebugPaper(Ped player)
	{
		string town = PaperTown(player);
		_paper = BuildPaper(town, player);
		GrudgeLog("PAPER (spawner) " + _paper.Name + ": lead '" + _paper.Lead.Head + "'");
		OpenPaper(_paper);
	}

	private void OpenPaper(Newspaper p)
	{
		_paperShown = p;
		UiSound("MENU_ENTER");
	}

	private void ClosePaper()
	{
		_paperShown = null;
		UiSound("MENU_CLOSE");
	}

	internal bool PaperOpen => _paperShown != null;

	private bool PaperKey(Keys key)
	{
		if (_paperShown == null)
		{
			return false;
		}
		if (key == Keys.Back || key == Keys.Escape || key == Keys.Return || key == Keys.Space)
		{
			ClosePaper();
		}
		return true;
	}

	private Newspaper BuildPaper(string town, Ped player)
	{
		long now = GameMinutesNow();
		DateTime dateTime = FromGameMinutes(now);
		Newspaper newspaper = new Newspaper();
		newspaper.Name = PaperName(town);
		newspaper.Town = town;
		newspaper.Day = GameDayKey();
		newspaper.Date = $"{town.ToUpperInvariant()}, {DayNames[(int)dateTime.DayOfWeek].ToUpperInvariant()}, {MonthNames[dateTime.Month - 1].ToUpperInvariant()} {dateTime.Day}, {dateTime.Year}";
		newspaper.Ad = Ads[(uint)Seeds.Stable("ad/" + town + "/" + GameDayKey()) % (uint)Ads.Length];
		Newspaper newspaper2 = newspaper;
		Vector3 core;
		float radius;
		bool hasCore = TownCore(town, out core, out radius);
		List<WorldEvent> source = (from e in Chronicle.Events.Where((WorldEvent e) => !e.IsPast && e.GameMin <= now + 60 && now - e.GameMin <= 7200 && Near(e)).ToList()
			select (e: e, s: PaperScore(e) + (int)Math.Max(0L, 30 - (now - e.GameMin) / 144)) into x
			where x.s > 30
			orderby x.s descending
			select x.e).ToList();
		List<WorldEvent> list = source.Where((WorldEvent e) => !SmallItem(e)).Take(6).ToList();
		if (list.Count > 0)
		{
			newspaper2.Banner = ((list[0].Kind == "YOU" && list[0].Tag == "leader") ? "EXTRA! EXTRA!" : ((list[0].Kind == "WAR") ? "WAR NEWS" : ((list[0].Kind == "YOU" || list[0].Kind == "CAMP OVERRUN" || list[0].Kind == "JOB") ? "OUTLAW NEWS" : ((list[0].Kind == "CLASH" || list[0].Kind == "WITNESSED" || list[0].Kind == "TURMOIL") ? "BLOODSHED IN THE TERRITORY" : "LATEST NEWS"))));
		}
		else
		{
			newspaper2.Banner = "THE WEEK IN REVIEW";
		}
		newspaper2.Lead = ((list.Count > 0) ? WriteUp(list[0], now) : new Article
		{
			Head = Pick<string>(new Random(Seeds.Stable("quiet/" + town + "/" + GameDayKey())), "A QUIET WEEK IN THE TERRITORY", "ALL QUIET", "PEACE, FOR NOW", "NOTHING TO REPORT"),
			Deck = "NO OUTRAGES OF NOTE REPORTED",
			Body = $"{town.ToUpperInvariant()}, {MonthNames[dateTime.Month - 1]} {dateTime.Day} - The editor is pleased to report little of consequence: no gunfights worth the ink, no outlaw camps put to the torch, and the roads as safe as roads ever are in this country. Readers with news are invited to call at the office."
		});
		foreach (WorldEvent item in list.Skip(1))
		{
			newspaper2.Stories.Add(WriteUp(item, now));
		}
		newspaper2.Layout = (int)((uint)Seeds.Stable("paperlayout/" + newspaper2.Name + "/" + newspaper2.Day) % 3u);
		Random r = new Random(Seeds.Stable("paperbanner/" + newspaper2.Name + "/" + newspaper2.Day));
		switch (newspaper2.Banner)
		{
		case "OUTLAW NEWS":
			newspaper2.Banner = Pick<string>(r, "OUTLAW NEWS", "THE OUTLAW QUESTION", "LAWLESSNESS ABROAD");
			break;
		case "LATEST NEWS":
			newspaper2.Banner = Pick<string>(r, "LATEST NEWS", "NEWS OF THE TERRITORY", "THE DAY'S NEWS");
			break;
		case "BLOODSHED IN THE TERRITORY":
			newspaper2.Banner = Pick<string>(r, "BLOODSHED IN THE TERRITORY", "GUNFIRE AND GRIEF", "BLOOD ON THE ROADS");
			break;
		case "THE WEEK IN REVIEW":
			newspaper2.Banner = Pick<string>(r, "THE WEEK IN REVIEW", "A QUIET WEEK", "NEWS OF THE COUNTY");
			break;
		}
		foreach (WorldEvent item2 in Chronicle.Events.Where((WorldEvent e) => !e.IsPast && e.GameMin <= now + 60 && now - e.GameMin <= 7200 && e.HasPos && !Near(e) && !SmallItem(e) && PaperScore(e) > 40).OrderByDescending(PaperScore).Take(5))
		{
			string text = BriefOf(WriteUp(item2, now));
			if (text != null)
			{
				newspaper2.Further.Add(text);
			}
		}
		foreach (Plan p in (from plan in Plans
			where plan.Kind == "funeral" && !plan.Done && plan.DueAt + plan.WindowMin > now && (!hasCore || new Vector3(plan.X - core.X, plan.Y - core.Y, 0f).Length() < 3000f)
			orderby plan.DueAt
			select plan).Take(2))
		{
			Roster.FirstOrDefault((NemesisRecord n) => n.Id == p.Dead);
			DateTime t = FromGameMinutes(p.DueAt);
			DateTime t2 = FromGameMinutes(p.DueAt + p.WindowMin);
			newspaper2.Funerals.Add((((FuneralDead(p).Count > 0) ? FuneralNames(p) : "One of their number").ToUpperInvariant(), (p.Gang != null) ? ("late of the " + (Gangs.ById(p.Gang)?.Name ?? "outfit")) : "a man of no outfit", $"{DayNames[(int)t.DayOfWeek]}, {MonthNames[t.Month - 1]} {t.Day}, {Hour(t)}", Cap1((p.Place ?? "").Trim()), "Mourners will keep the grave until about " + Hour(t2).Replace(" o'clock", "") + "."));
			newspaper2.FuneralPlans.Add(p);
		}
		foreach (WorldEvent item3 in source.Where(SmallItem).Take(7 - Math.Min(2, newspaper2.About.Count)))
		{
			newspaper2.About.Add(SmallNews(item3));
		}
		if (newspaper2.About.Count == 0 && newspaper2.Funerals.Count == 0)
		{
			newspaper2.About.Add("Nothing of note to report from the town this week. The editor would welcome gossip, delivered sober.");
		}
		int num = 0;
		try
		{
			num = BountyDollars;
		}
		catch
		{
		}
		if (num > 0)
		{
			newspaper2.Wanted.Add($"${num} REWARD for {InterroLines.PlayerName}, wanted for crimes against the peace of the territory. Apply to any sheriff.");
		}
		Gang gang = Gangs.All.Where((Gang g) => !g.StoryOnly && !g.OrdersNotFeelings && !IsDestroyed(g)).OrderByDescending(NotorietyOf).FirstOrDefault();
		if (gang != null && NotorietyOf(gang) >= 50)
		{
			newspaper2.Wanted.Add("It is said in the saloons that the " + gang.Name + " will pay well for a certain rider, dead.");
		}
		if (newspaper2.Wanted.Count == 0)
		{
			newspaper2.Wanted.Add("The law posts no new rewards this week.");
		}
		foreach (var item4 in WarsNow().Take(3))
		{
			newspaper2.Wars.Add("In " + item4.where + ", the " + item4.a.Name + " and the " + item4.b.Name + " are at open war. Travellers are warned.");
		}
		if (newspaper2.Wars.Count == 0)
		{
			newspaper2.Wars.Add("The outfits keep an uneasy peace. Long may it last.");
		}
		return newspaper2;
		static string Hour(DateTime dateTime2)
		{
			return string.Format("{0} o'clock{1}", (dateTime2.Hour % 12 == 0) ? 12 : (dateTime2.Hour % 12), (dateTime2.Hour < 12) ? " in the morning" : ((dateTime2.Hour < 17) ? " in the afternoon" : " in the evening"));
		}
		bool Near(WorldEvent e)
		{
			if (e.HasPos)
			{
				if (hasCore)
				{
					return new Vector3(e.X - core.X, e.Y - core.Y, 0f).Length() < 3500f;
				}
				return true;
			}
			if (!(e.Kind == "WAR"))
			{
				return e.Kind == "TURMOIL";
			}
			return true;
		}
	}

	private static bool SmallItem(WorldEvent e)
	{
		if (!(e.Kind == "LIFE") && !(e.Kind == "FEUD") && !(e.Kind == "RIVALRY") && !(e.Kind == "BRAWL") && !(e.Kind == "DUEL") && !(e.Kind == "RANKS") && !(e.Kind == "VDL"))
		{
			if (e.Kind == "YOU")
			{
				if (!(e.Tag == "rob") && !(e.Tag == "hogtie"))
				{
					return e.Tag == "ko";
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private int PaperScore(WorldEvent e)
	{
		int num = e.People.Count((WorldPart p) => p.Outcome == "KILLED") + e.UnnamedDead.Values.Sum();
		int num2;
		if (e.Kind == "YOU")
		{
			switch (e.Tag)
			{
			case "leader":
				num2 = 100;
				break;
			case "camp":
				num2 = 90;
				break;
			case "treasury":
				num2 = 85;
				break;
			case "funeral":
				num2 = ((e.Title != null && e.Title.Contains("attacked")) ? 85 : 0);
				break;
			case "supplies":
				num2 = 70;
				break;
			case "train":
				num2 = 80;
				break;
			case "roadblock":
				num2 = 55;
				break;
			case "hunters":
				num2 = 50;
				break;
			case "kill":
				num2 = 45;
				break;
			case "good":
				num2 = 40;
				break;
			case "hogtie":
			case "rob":
			case "ko":
				num2 = 35;
				break;
			default:
				num2 = 0;
				break;
			}
		}
		else
		{
			switch (e.Kind)
			{
			case "WAR":
				num2 = 80;
				break;
			case "CAMP OVERRUN":
				num2 = 75;
				break;
			case "CLASH":
				num2 = 60;
				break;
			case "TURMOIL":
				num2 = 55;
				break;
			case "WITNESSED":
				num2 = 50;
				break;
			case "JOB":
				num2 = 45;
				break;
			case "FUNERAL":
				num2 = 40;
				break;
			case "DUEL":
				num2 = 38;
				break;
			case "VDL":
				num2 = 36;
				break;
			case "BRAWL":
				num2 = 34;
				break;
			case "FEUD":
				num2 = 32;
				break;
			case "RANKS":
			case "RIVALRY":
			case "LIFE":
				num2 = 31;
				break;
			default:
				num2 = 0;
				break;
			}
		}
		if (num2 != 0)
		{
			return num2 + Math.Min(20, num * 3);
		}
		return 0;
	}

	private static T Pick<T>(Random r, params T[] a)
	{
		return a[r.Next(a.Length)];
	}

	private static string Num(int n)
	{
		if (n < 0 || n >= NumWords.Length)
		{
			return n.ToString();
		}
		return NumWords[n];
	}

	private static string JoinAnd(IList<string> l)
	{
		if (l.Count != 0)
		{
			if (l.Count != 1)
			{
				return string.Join(", ", l.Take(l.Count - 1)) + " and " + l.Last();
			}
			return l[0];
		}
		return "";
	}

	private static string GangOf(string id)
	{
		return Gangs.ById(id)?.Name;
	}

	private static string ShortGang(string id)
	{
		string text = GangOf(id) ?? "outlaws";
		if (text.StartsWith("The "))
		{
			text = text.Substring(4);
		}
		return id switch
		{
			"odriscoll" => "O'Driscolls", 
			"lemoyneraiders" => "Raiders", 
			"army" => "Army", 
			"bronte" => "Bronte men", 
			"murfree" => "Murfrees", 
			"skinners" => "Skinner Brothers", 
			"wapiti" => "Wapiti", 
			_ => text, 
		};
	}

	private string WhenPhrase(long at, long now)
	{
		DateTime dateTime = FromGameMinutes(at);
		int num = (int)(FromGameMinutes(now).Date - dateTime.Date).TotalDays;
		string text = ((dateTime.Hour < 5) ? "night" : ((dateTime.Hour < 12) ? "morning" : ((dateTime.Hour < 18) ? "afternoon" : "evening")));
		if (num <= 0)
		{
			if (!(text == "night"))
			{
				return "this " + text;
			}
			return "in the small hours";
		}
		if (num == 1)
		{
			if (!(text == "night") && !(text == "evening"))
			{
				return "yesterday " + text;
			}
			return "last night";
		}
		return "on " + DayNames[(int)dateTime.DayOfWeek] + " " + text;
	}

	private string Dateline(WorldEvent e)
	{
		DateTime dateTime = FromGameMinutes(e.GameMin);
		string text = (e.HasPos ? (DeedTown(new Vector3(e.X, e.Y, e.Z)) ?? GetZoneName(new Vector3(e.X, e.Y, e.Z))) : "THE TERRITORY");
		if (text.StartsWith("the "))
		{
			text = text.Substring(4);
		}
		return $"{text.ToUpperInvariant()}, {MonthNames[dateTime.Month - 1]} {dateTime.Day} - ";
	}

	private static string PlacePhrase(string place)
	{
		if (string.IsNullOrEmpty(place))
		{
			return "in these parts";
		}
		int num = place.IndexOf(" camp in ", StringComparison.Ordinal);
		if (place.StartsWith("at ") && num > 0)
		{
			return "in " + place.Substring(num + 9);
		}
		if (place.StartsWith("word got around "))
		{
			return "near " + place.Substring(16);
		}
		if (place.StartsWith("("))
		{
			return "in these parts";
		}
		return place;
	}

	private string Rider(WorldEvent e, Random r)
	{
		string id = e.Gangs.FirstOrDefault((string x) => !string.IsNullOrEmpty(x));
		Gang gang = Gangs.ById(id);
		int num = ((gang != null) ? NotorietyOf(gang) : 0);
		if (num >= 80)
		{
			return "the outlaw " + InterroLines.PlayerName + ", if the " + gang.Name + " are to be believed";
		}
		if (num >= 30)
		{
			return Pick<string>(r, "a lone rider the " + ShortGang(id) + " have reason to know", "a gunman whose description is well known to the " + ShortGang(id), "a rider the " + ShortGang(id) + " are said to have crossed before");
		}
		return Pick<string>(r, "a lone rider", "an unknown gunman", "a stranger on horseback", "a single rider, as yet unnamed");
	}

	private string DeadLine(WorldEvent e)
	{
		List<string> list = (from kv in e.UnnamedDead
			where kv.Value > 0
			select Num(kv.Value) + " of the " + (GangOf(kv.Key) ?? "outlaws")).ToList();
		int num = e.UnnamedDead.Values.Sum();
		List<string> list2 = (from p in e.People
			where p.Outcome == "KILLED" && p.Name != null
			select p.Name).Take(3).ToList();
		if (list.Count == 0 && list2.Count == 0)
		{
			return "";
		}
		string text = ((list.Count > 0) ? (Cap1(JoinAnd(list)) + " " + ((num == 1) ? "lies" : "lie") + " dead") : "");
		if (list2.Count > 0)
		{
			text = ((text == "") ? (JoinAnd(list2) + " " + ((list2.Count == 1) ? "is" : "are") + " counted among the dead") : (text + ", " + JoinAnd(list2) + " among them"));
		}
		return text + ".";
	}

	private static string Upshot(WorldEvent e)
	{
		foreach (string item in e.Lines ?? new List<string>())
		{
			if (!string.IsNullOrEmpty(item) && !item.Contains("->") && !item.Contains("Ground now") && !item.StartsWith("Part of") && !item.Contains(" down."))
			{
				if (item.Contains(" had the better of it"))
				{
					return item.Replace(" had the better of it", " are said to have had the better of it");
				}
				if (item.Contains(" held the field"))
				{
					return item.Replace(" held the field", " held the field when the smoke cleared");
				}
				if (item.StartsWith("Neither side"))
				{
					return "Neither side could claim the day.";
				}
				if (item.StartsWith("It lasted"))
				{
					return item.Replace("It lasted", "The fighting lasted").Replace(" that word got out about", " that came to the ears of this paper");
				}
				if (item.StartsWith("At war since"))
				{
					return item.Replace("At war since", "The two have been at war since");
				}
				if (item.StartsWith("The law locked"))
				{
					return "The law locked him up for the night.";
				}
			}
		}
		return "";
	}

	private static string Quote(Random r, string kind)
	{
		return kind switch
		{
			"fight" => Pick<string>(r, "\"It sounded like the Fourth of July,\" said a farmer who heard it from his porch.", "\"I kept my head down and my mouth shut,\" one traveller told this paper.", "\"Nobody with sense rides that road now,\" said a teamster, who would not give his name.", ""), 
			"camp" => Pick<string>(r, "\"Good riddance,\" was the verdict of one rancher nearby.", "Lawmen declined to comment.", "Settlers in the district are said to be sleeping easier.", ""), 
			"money" => Pick<string>(r, "It is not often that thieves are robbed, and few in these parts will mourn it.", "\"Serves 'em right,\" said one storekeeper.", ""), 
			"good" => Pick<string>(r, "\"I never got his name,\" the grateful party told this paper.", "There is good in the territory yet.", ""), 
			_ => "", 
		};
	}

	private Article WriteUp(WorldEvent e, long now)
	{
		Random r = new Random(Seeds.Stable("paper/" + (e.Id ?? e.Title ?? "")));
		string text = WhenPhrase(e.GameMin, now);
		string text2 = PlacePhrase(e.Place);
		string dl = Dateline(e);
		string text3 = e.Gangs.FirstOrDefault((string x) => !string.IsNullOrEmpty(x));
		string text4 = e.Gangs.Skip(1).FirstOrDefault((string x) => !string.IsNullOrEmpty(x));
		string text5 = GangOf(text3) ?? "outlaws";
		string text6 = GangOf(text4);
		string text7 = ShortGang(text3).ToUpperInvariant();
		string text8 = (e.HasPos ? GetZoneName(new Vector3(e.X, e.Y, e.Z)) : text2.Replace("near ", "").Replace("in ", "")).ToUpperInvariant();
		string text9 = DeadLine(e);
		string text10 = Upshot(e);
		string text11 = e.Title ?? "";
		List<string> list = (from p in e.People
			where p.Outcome == "KILLED" && p.Name != null
			select p.Name).ToList();
		if (e.Kind == "YOU")
		{
			string text12 = Rider(e, r);
			switch (e.Tag)
			{
			case "camp":
			{
				string text13 = list.FirstOrDefault();
				Article article = new Article();
				article.Head = Pick<string>(r, "OUTLAW CAMP PUT TO THE SWORD", "NEST OF OUTLAWS DESTROYED", text7 + " CAMP OVERRUN", "A DEN OF THIEVES NO MORE", "NO QUARTER FOR THE " + text7);
				article.Deck = Pick<string>(r, "GANG ROUTED " + text2.ToUpperInvariant() + " - LONE RIDER SOUGHT", "NOT A MAN LEFT STANDING", "THE TRAILS SAFER FOR IT, SAY SETTLERS");
				article.Body = B(new string[4]
				{
					"A camp kept by the " + text5 + " " + text2 + " was overrun " + text + " by " + text12 + ", and not a man of them was left standing.",
					(text13 != null) ? (text13 + ", said to have led the men there, is counted among the dead.") : "",
					"The camp had long been a terror to travellers on the trails nearby.",
					Quote(r, "camp")
				});
				return article;
			}
			case "train":
			{
				Article article = new Article();
				article.Head = Pick<string>(r, "TRAIN ROBBED", "BANDITS STOP THE LINE", "HOLDUP ON THE RAILS", "PASSENGERS ROBBED AT GUNPOINT");
				article.Deck = Pick<string>(r, "OUTRAGE " + text2.ToUpperInvariant(), "THE RAILWAY COUNTS ITS LOSSES", "THE LAW HAS NO NAME YET");
				article.Body = B(new string[4]
				{
					"A train was stopped and robbed " + text2 + " " + text + ".",
					text11.Contains("masked") ? "The robber wore a mask, and none aboard could say who he was." : ("Passengers name " + text12 + " as the man behind it."),
					"The railway company is said to be offering a reward for information.",
					Quote(r, "fight")
				});
				return article;
			}
			case "supplies":
			{
				Article article;
				if (text11.Contains("wagon"))
				{
					article = new Article();
					article.Head = Pick<string>(r, "SUPPLY WAGON LOST ON THE ROAD", "OUTLAW WAGON WRECKED", "AMBUSH ON THE SUPPLY ROAD", "A WAGON THAT NEVER ARRIVED");
					article.Deck = Pick<string>(r, text7 + " STORES NEVER REACHED THEIR CAMP", "POWDER, FEED AND DRINK LOST", "ITS DRIVERS FLED FOR THEIR LIVES");
					article.Body = B(new string[4]
					{
						"A wagon carrying powder, feed and drink for the " + text5 + " was " + (text11.Contains("made off") ? "driven off" : "wrecked") + " " + text2 + " " + text + ".",
						"Its drivers are said to have fled for their lives.",
						"The loss is thought to leave the " + ShortGang(text3) + " short for some days to come.",
						Quote(r, "fight")
					});
					return article;
				}
				if (text11.Contains("crates"))
				{
					article = new Article();
					article.Head = Pick<string>(r, "OUTLAW CRATES BROKEN OPEN", "OUTLAW GOODS IN THE GUTTER", "A GIFT FOR THE TOWNSFOLK");
					article.Deck = Pick<string>(r, "STORES SPOILED IN THE STREET", "THE TOWN HELPS ITSELF", text7 + " GOODS LEFT IN THE ROAD");
					article.Body = B(new string[3]
					{
						"Crates kept in town by the " + text5 + " were broken open and their contents spoiled " + text + ".",
						"The work is put down to " + text12 + ".",
						"Townsfolk are said to have helped themselves to what was left."
					});
					return article;
				}
				article = new Article();
				article.Head = Pick<string>(r, "OUTLAW STORES PUT TO THE TORCH", "SMOKE OVER THE OUTLAW CAMP", "FIRE IN THE OUTLAW STORES", "THE " + text7 + " GO HUNGRY");
				article.Deck = Pick<string>(r, text7 + " SUPPLY CAMP IN RUINS", "SMOKE SEEN FOR MILES", "CRATES BURST, HORSES SCATTERED");
				article.Body = B(new string[4]
				{
					"Smoke was seen for miles " + text + " as the stores of the " + text5 + " " + text2 + " went up.",
					"Crates were burst, wagons wrecked and horses scattered, the work, it is said, of " + text12 + ".",
					"How the " + ShortGang(text3) + " will feed and arm their men is now anybody's guess.",
					Quote(r, "camp")
				});
				return article;
			}
			case "treasury":
			{
				Article article = new Article();
				article.Head = Pick<string>(r, "OUTLAW STRONGBOX LOOTED", "THIEVES ROBBED", "THE ROBBERS ROBBED", text7 + " FORTUNE GONE");
				article.Deck = Pick<string>(r, text7 + " MONEY CHESTS EMPTIED " + text2.ToUpperInvariant(), "NO SMALL SUM, SAY THOSE WHO KNOW", "FEW IN THESE PARTS WILL MOURN IT");
				article.Body = B(new string[3]
				{
					"The money chests of the " + text5 + ", kept under guard " + text2 + ", were broken open " + text + " by " + text12 + ".",
					"The sum taken is not known, but men who keep company with the outfit say it was no small one.",
					Quote(r, "money")
				});
				return article;
			}
			case "leader":
			{
				string text17 = e.People.FirstOrDefault()?.Name ?? ("the leader of the " + text5);
				bool flag = text11.Contains("arrest") || text11.Contains("turned in");
				Article article = new Article();
				article.Head = (flag ? Pick<string>(r, text7 + " CHIEF IN CHAINS", text7 + " LEADER TAKEN ALIVE", "OUTLAW CHIEF BEHIND BARS") : Pick<string>(r, text7 + " LEADER IS DEAD", "THE " + text7 + " LOSE THEIR CHIEF", "AN OUTLAW KING FALLS", text7 + " CHIEF SHOT DOWN"));
				article.Deck = (flag ? Pick<string>(r, "OUTLAW CHIEF DELIVERED TO THE LAW", "THE GALLOWS AWAIT, SAY LAWMEN") : Pick<string>(r, "OUTLAW CHIEF FALLS - HIS MEN SCATTER", "WHO WILL LEAD THEM NOW?", "THE OUTFIT IN DISARRAY"));
				article.Body = B(new string[3]
				{
					flag ? (text17 + ", who led the " + text5 + ", was delivered to the law " + text + ".") : (text17 + ", who led the " + text5 + ", is dead, shot down " + text2 + " " + text + "."),
					"Who will lead the " + ShortGang(text3) + " now is the talk of every saloon in the district.",
					"Lawmen say they expect the outfit to fight among itself before it fights anyone else."
				});
				return article;
			}
			case "kill":
			{
				Article article = new Article();
				article.Head = ((list.Count == 1) ? Pick<string>(r, list[0].ToUpperInvariant() + " SHOT DEAD", list[0].ToUpperInvariant() + " GUNNED DOWN", "THE END OF " + list[0].ToUpperInvariant()) : Pick<string>(r, Num(Math.Max(2, list.Count)).ToUpperInvariant() + " MEN SHOT DEAD " + ("NEAR " + text8).Trim(), Num(Math.Max(2, list.Count)).ToUpperInvariant() + " OUTLAWS KILLED", "SHOOTING " + ("NEAR " + text8).Trim(), "DEATH ON THE ROAD"));
				article.Deck = Pick<string>(r, "MEN OF THE " + text5.ToUpperInvariant() + " FALL TO " + (text12.StartsWith("the outlaw") ? "A KNOWN OUTLAW" : "A LONE GUN"), "THE KILLER RODE ON", "THE " + text7 + " COUNT THEIR DEAD");
				article.Body = B(new string[3]
				{
					(list.Count > 0) ? (((list.Count <= 4) ? JoinAnd(list) : JoinAnd(list.Take(3).Concat(new string[1] { Num(list.Count - 3) + " others" }).ToList())) + ", of the " + text5 + ", " + ((list.Count == 1) ? "was" : "were") + " shot dead " + text2 + " " + text + ".") : ("Men of the " + text5 + " were shot dead " + text2 + " " + text + "."),
					"The killing is put down to " + text12 + ".",
					Quote(r, "fight")
				});
				return article;
			}
			case "roadblock":
			{
				Article article = new Article();
				article.Head = Pick<string>(r, "THE ROAD IS OPEN", "ROADBLOCK CLEARED", "TRAVELLERS MAY PASS AGAIN");
				article.Deck = Pick<string>(r, text7 + " ROADBLOCK SWEPT ASIDE", "THOSE WHO HELD IT DEAD OR FLED");
				article.Body = B(new string[3]
				{
					"Travellers " + text2 + " may pass freely once more after a roadblock kept by men of the " + text5 + " was cleared " + text + ".",
					"Those who held it are dead or fled; the work of " + text12 + ", by most accounts.",
					Quote(r, "fight")
				});
				return article;
			}
			case "hunters":
			{
				string text16 = e.People.FirstOrDefault((WorldPart p) => p.Outcome == "SENT THEM")?.Name;
				Article article = new Article();
				article.Head = Pick<string>(r, "BOUNTY HUNTERS COME TO GRIEF", "THE HUNTERS HUNTED", "MANHUNT ENDS IN BLOOD");
				article.Deck = Pick<string>(r, "THE HUNTED TURNS ON HIS PURSUERS", "THEIR QUARRY RIDES ON", "A BAD DAY FOR THE BOUNTY TRADE");
				article.Body = B(new string[3]
				{
					"A party of bounty hunters" + ((text16 != null) ? (" said to be in the pay of " + text16) : "") + " met their end " + text2 + " " + text + ".",
					"The man they rode after is said to have ridden on.",
					"Those who hire such men may wish to think again."
				});
				return article;
			}
			case "good":
			{
				string text14 = (text11.StartsWith("You ") ? text11.Substring(4) : text11);
				int num = text14.IndexOf(" (", StringComparison.Ordinal);
				if (num > 0)
				{
					text14 = text14.Substring(0, num);
				}
				string text15 = ((e.Place ?? "").StartsWith("word got around ") ? e.Place.Substring(16) : null);
				Article article = new Article();
				article.Head = Pick<string>(r, "A KINDNESS ON THE ROAD", "GOOD SAMARITAN" + ((text15 != null) ? (" NEAR " + text15.ToUpperInvariant()) : ""), "A STRANGER'S GOOD DEED", "DECENCY IN THE TERRITORY");
				article.Deck = Pick<string>(r, "A STRANGER'S GOOD TURN REMEMBERED", "HE WOULD NOT GIVE HIS NAME", "THE TOWN TALKS OF LITTLE ELSE");
				article.Body = B(new string[2]
				{
					"A stranger " + text14 + " " + text + ", and " + (text15 ?? "the town") + " has not stopped talking of it.",
					Quote(r, "good")
				});
				return article;
			}
			case "funeral":
			{
				Article article = new Article();
				article.Head = Pick<string>(r, "GUNFIRE AT THE GRAVESIDE", "MOURNERS SHOT AT THE GRAVE", "NO PEACE FOR THE DEAD");
				article.Deck = Pick<string>(r, "MOURNERS SET UPON - A SHAMEFUL BUSINESS", "A LOW THING TO DO, SAY DECENT FOLK", "BLOOD ON THE BURYING GROUND");
				article.Body = B(new string[2]
				{
					(text3 != null) ? ("Men of the " + text5 + " burying one of their own " + text2 + " were fired upon " + text + ".") : ("Mourners at a burying " + text2 + " were fired upon " + text + "."),
					"Whatever the dead man's sins, decent folk in the district are agreed it was a low thing to do."
				});
				return article;
			}
			}
		}
		switch (e.Kind)
		{
		case "CLASH":
		case "WITNESSED":
		{
			Article article = new Article();
			article.Head = Pick<string>(r, "GUNFIGHT NEAR " + text8, "OUTLAWS TRADE LEAD NEAR " + text8, "BLOODSHED IN " + text8, "BATTLE IN " + text8, "LEAD FLIES IN " + text8);
			article.Deck = ((text6 != null) ? Pick<string>(r, "THE " + ShortGang(text3).ToUpperInvariant() + " AND THE " + ShortGang(text4).ToUpperInvariant() + " AT IT AGAIN", ShortGang(text3).ToUpperInvariant() + " AGAINST " + ShortGang(text4).ToUpperInvariant(), "OLD ENEMIES MEET WITH GUNS") : Pick<string>(r, "SHOTS FIRED", "TRAVELLERS WARNED", "THE LAW SLOW TO ARRIVE"));
			article.Body = B(new string[4]
			{
				(text6 != null) ? ("Men of the " + text5 + " and the " + text6 + " met with guns " + text2 + " " + text + ".") : ("Shots were heard " + text2 + " " + text + "."),
				text9,
				text10,
				Quote(r, "fight")
			});
			return article;
		}
		case "CAMP OVERRUN":
		{
			Article article = new Article();
			article.Head = Pick<string>(r, "CAMP OVERRUN", "OUTLAW CAMP CHANGES HANDS", "DRIVEN FROM THEIR CAMP");
			article.Deck = ((text6 != null) ? ("THE " + ShortGang(text3).ToUpperInvariant() + " AND THE " + ShortGang(text4).ToUpperInvariant()) : "OUTLAWS DRIVEN OUT");
			article.Body = B(new string[3]
			{
				Sentence(text11),
				text9,
				text10
			});
			return article;
		}
		case "WAR":
		{
			int num5 = text11.IndexOf(':');
			bool flag2 = text11.Contains("drove") || text11.Contains("ended") || text11.Contains("gave up") || text11.Contains("truce");
			Article article = new Article();
			article.Head = (flag2 ? Pick<string>(r, "THE WAR IN " + text8 + " IS OVER", "PEACE IN " + text8, "THE GUNS FALL SILENT IN " + text8) : Pick<string>(r, "WAR IN " + text8, text8 + " AT WAR", "NO END TO THE FIGHTING IN " + text8));
			article.Deck = ((num5 > 0) ? text11.Substring(num5 + 1).Trim().ToUpperInvariant() : ((text6 != null) ? (text5.ToUpperInvariant() + " AND " + text6.ToUpperInvariant()) : ""));
			article.Body = B(new string[4]
			{
				flag2 ? Sentence(text11) : ((text6 != null) ? ("The fighting between the " + text5 + " and the " + text6 + " goes on " + text2 + ".") : Sentence(text11)),
				(num5 > 0 && !flag2) ? Sentence(text11.Substring(num5 + 1).Trim()) : "",
				text10,
				"Travellers are advised to keep to the main roads and to ride in company."
			});
			return article;
		}
		case "TURMOIL":
		{
			bool flag3 = text11.StartsWith("The Army") || text11.StartsWith("The U.S. Army");
			Article article = new Article();
			article.Head = (flag3 ? Pick<string>(r, "SOLDIERS MOVE AGAINST THE " + ShortGang(text4 ?? text3).ToUpperInvariant(), "THE ARMY STRIKES", "CAVALRY IN THE HILLS") : Pick<string>(r, "TROUBLE FOR THE " + text7, "THE " + text7 + " UNDER FIRE", "OUTLAW AGAINST OUTLAW"));
			article.Deck = (flag3 ? Pick<string>(r, "THE ARMY TAKES A HAND", "SOLDIERS SENT TO RESTORE ORDER") : Pick<string>(r, "BAD BLOOD AMONG THE OUTFITS", "THIEVES FALL OUT", "NO HONOUR AMONG THEM"));
			article.Body = B(new string[4]
			{
				Sentence(text11),
				text9,
				e.People.Any((WorldPart p) => p.Outcome == "WOUNDED") ? (JoinAnd((from p in e.People
					where p.Outcome == "WOUNDED"
					select p.Name).Take(2).ToList()) + " " + ((e.People.Count((WorldPart p) => p.Outcome == "WOUNDED") == 1) ? "was" : "were") + " wounded.") : "",
				text10
			});
			return article;
		}
		case "JOB":
		{
			int num2 = text11.IndexOf(": ", StringComparison.Ordinal);
			string text18 = ((num2 > 0) ? text11.Substring(num2 + 2) : text11);
			string text19 = e.People.FirstOrDefault()?.Name;
			Article article;
			if (text11.Contains(" tried to "))
			{
				int num3 = text11.LastIndexOf(" - ", StringComparison.Ordinal);
				int num4 = text11.IndexOf(" tried to ", StringComparison.Ordinal);
				string text20 = ((num4 > 0) ? text11.Substring(num4 + 10, ((num3 > num4) ? num3 : text11.Length) - num4 - 10) : text18);
				string text21 = ((num3 > 0) ? text11.Substring(num3 + 3) : "it came to nothing");
				article = new Article();
				article.Head = Pick<string>(r, "OUTLAWS FOILED", "A PLOT COMES TO NOTHING", "BAD LUCK FOR THE " + text7);
				article.Deck = Pick<string>(r, text7 + " PLANS COME TO NOTHING", "THE LAW PLEASED WITH ITSELF");
				article.Body = B(new string[2]
				{
					"An attempt by the " + text5 + " to " + text20 + " came to nothing " + text + ": " + text21 + ".",
					"The law is said to be pleased with itself."
				});
				return article;
			}
			article = new Article();
			article.Head = Pick<string>(r, "OUTRAGE NEAR " + text8, "OUTLAWS STRIKE AGAIN", "THE " + text7 + " AT WORK", "BOLD WORK BY OUTLAWS", "LAWLESSNESS NEAR " + text8);
			article.Deck = Pick<string>(r, "THE LAW NOWHERE TO BE SEEN", "AND NOT A LAWMAN IN SIGHT", "DONE BEFORE ANY LAWMAN COULD STIR");
			article.Body = B(new string[3]
			{
				"Men of the " + text5 + ((text19 != null) ? (", led by one " + text19 + ",") : "") + " are blamed for an outrage " + text2 + " " + text + ".",
				"They are said to have set out to " + text18 + ", and to have done it before any lawman could stir.",
				Quote(r, "fight")
			});
			return article;
		}
		case "FUNERAL":
		{
			string text22 = (e.People.Any((WorldPart p) => p.Outcome == "BURIED") ? JoinAnd((from p in e.People
				where p.Outcome == "BURIED"
				select p.Name).ToList()) : "one of their number");
			string text23 = e.People.FirstOrDefault((WorldPart p) => p.Outcome == "MOURNED")?.Name;
			Article article;
			if (text3 == null)
			{
				article = new Article();
				article.Head = Pick<string>(r, "A MAN LAID TO REST", "A QUIET BURIAL", "FEW TO MOURN HIM");
				article.Deck = Pick<string>(r, "FEW MOURNERS AT THE GRAVESIDE", "HE BELONGED TO NO OUTFIT");
				article.Body = B(new string[3]
				{
					text22 + " was laid to rest " + text2 + " " + text + ".",
					(text23 != null) ? (text23 + " stood at the grave.") : "",
					"He belonged to no outfit, and few came to see him off."
				});
				return article;
			}
			article = new Article();
			article.Head = Pick<string>(r, "AN OUTLAW LAID TO REST", "THE " + text7 + " BURY THEIR OWN", "A GRAVE FOR AN OUTLAW", "DUST TO DUST");
			article.Deck = Pick<string>(r, "THE " + text7 + " BURY THEIR OWN", "NO PREACHER WOULD SAY THE WORDS", "MOURNERS UNDER ARMS");
			article.Body = B(new string[3]
			{
				text22 + ", of the " + text5 + ", was laid to rest " + text2 + " " + text + ".",
				(text23 != null) ? (text23 + " led the mourners.") : "",
				"No preacher could be found to say the words."
			});
			return article;
		}
		default:
		{
			Article article = new Article();
			article.Head = ((text11.Length > 40) ? (text11.Substring(0, 38) + "...") : text11).ToUpperInvariant();
			article.Deck = "";
			article.Body = B(new string[3]
			{
				Sentence(text11),
				text9,
				text10
			});
			return article;
		}
		}
		string B(string[] parts)
		{
			return dl + string.Join(" ", parts.Where((string p) => !string.IsNullOrEmpty(p)));
		}
	}

	private static string BriefOf(Article a)
	{
		string text = a?.Body;
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		int num = text.IndexOf(" - ", StringComparison.Ordinal);
		string text2 = ((num > 0) ? text.Substring(0, num) : null);
		if (text2 != null && text2.Contains(","))
		{
			text2 = text2.Substring(0, text2.IndexOf(','));
		}
		string text3 = ((num > 0) ? text.Substring(num + 3) : text);
		int num2 = text3.IndexOf(". ", StringComparison.Ordinal);
		string text4 = ((num2 > 0) ? text3.Substring(0, num2 + 1) : text3);
		if (text2 == null)
		{
			return text4;
		}
		return text2 + " - " + text4;
	}

	private string SmallNews(WorldEvent e)
	{
		string text = e.Title ?? "";
		if (e.Kind == "YOU")
		{
			if (text.StartsWith("You robbed "))
			{
				text = text.Substring(11) + " was robbed on the road by a stranger";
			}
			else if (text.StartsWith("You hogtied "))
			{
				text = text.Substring(12) + " was found bound hand and foot";
			}
			else if (text.StartsWith("You knocked out "))
			{
				text = text.Substring(16) + " was knocked senseless by a stranger";
			}
		}
		if (e.Kind == "VDL")
		{
			if (text.Length != 0)
			{
				return "It is said: " + Sentence(char.ToLowerInvariant(text[0]) + text.Substring(1));
			}
			return "";
		}
		string text2 = Upshot(e);
		string text3 = ((e.Place != null && !text.Contains(e.Place) && !e.Place.StartsWith("(") && e.Place != "in camp") ? (" " + PlacePhrase(e.Place)) : "");
		return Sentence(text + text3) + ((text2 != "") ? (" " + text2) : "");
	}

	private static string Sentence(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		s = s.Trim();
		s = char.ToUpperInvariant(s[0]) + s.Substring(1);
		if (!s.EndsWith(".") && !s.EndsWith("!") && !s.EndsWith("?"))
		{
			return s + ".";
		}
		return s;
	}

	private static List<string> PaperWrap(string s, float scale, float width, bool title)
	{
		if (string.IsNullOrEmpty(s))
		{
			return new List<string>();
		}
		string key = $"{scale:0.000}|{width:0.0000}|{(title ? 1 : 0)}|{s}";
		if (_wrapCache.TryGetValue(key, out var value))
		{
			return new List<string>(value);
		}
		if (_wrapCache.Count > 600)
		{
			_wrapCache.Clear();
		}
		List<string> list = PaperWrapNow(s, scale, width, title);
		_wrapCache[key] = list;
		return new List<string>(list);
	}

	private static List<string> PaperWrapNow(string s, float scale, float width, bool title)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(s))
		{
			return list;
		}
		string text = "";
		string[] array = s.Split(' ');
		foreach (string text2 in array)
		{
			string text3 = ((text.Length == 0) ? text2 : (text + " " + text2));
			if ((title ? Theme.TitleWidth(text3, scale) : Theme.MixedWidth(text3, scale, 0.0222f)) > width && text.Length > 0)
			{
				list.Add(text);
				text = text2;
			}
			else
			{
				text = text3;
			}
		}
		if (text.Length > 0)
		{
			list.Add(text);
		}
		return list;
	}

	private static void PaperLines(List<string> lines, int from, int count, float x, float y, float scale, float line, bool cut, (int r, int g, int b)? c = null)
	{
		(int, int, int) tuple = c ?? Ink;
		for (int i = from; i < lines.Count && i < from + count; i++)
		{
			if (UiDraw.TextSlotsLeft < 6)
			{
				break;
			}
			UiDraw.TextFont((cut && i == from + count - 1 && lines.Count > from + count) ? (lines[i].TrimEnd('.', ',', ';') + "...") : lines[i], UiDraw.BodyFont, x, y + (float)(i - from) * line, scale, tuple.Item1, tuple.Item2, tuple.Item3);
		}
	}

	private static float PaperHead(string s, float x, float y, float width, float scale, int max, bool centre = true)
	{
		List<string> list = PaperWrap(s, scale, width, title: true);
		for (int i = 0; i < list.Count && i < max; i++)
		{
			float x2 = (centre ? (x + (width - Theme.TitleWidth(list[i], scale)) / 2f) : x);
			UiDraw.TextFont(list[i], UiDraw.TitleFont, x2, y, scale, Ink.r, Ink.g, Ink.b);
			y += scale * 0.082f;
		}
		return y;
	}

	private static float PaperStory(Article a, float x, float y, float w, float bottom, float hs = 0.3f)
	{
		if (a == null)
		{
			return y;
		}
		y = PaperHead(a.Head, x, y, w, hs, 2) + 0.002f;
		float num = w * 0.3f;
		UiDraw.HLine(x + (w - num) / 2f, x + (w + num) / 2f, y, Ink.r, Ink.g, Ink.b, 255);
		y += 0.006f;
		if (!string.IsNullOrEmpty(a.Deck))
		{
			y = PaperHead(a.Deck, x, y, w, 0.2f, 2) + 0.006f;
		}
		List<string> list = PaperWrap(a.Body, 0.205f, w, title: false);
		int num2 = Math.Max(1, (int)((bottom - y) / 0.0192f));
		PaperLines(list, 0, num2, x, y, 0.205f, 0.0192f, cut: true);
		return y + (float)Math.Min(num2, list.Count) * 0.0192f;
	}

	private void DrawNewspaper()
	{
		Newspaper p = _paperShown;
		if (p == null)
		{
			return;
		}
		float num = 0.285f;
		float num2 = 0.43f;
		float num3 = 0.03f;
		float num4 = 0.945f;
		float num5 = 0.026f;
		float ix = num + num5;
		float iw = num2 - 2f * num5;
		UiDraw.Rect(0f, 0f, 1f, 1f, 0, 0, 0, 165);
		UiDraw.Rect(num, num3, num2, num4 - num3, Paper.r, Paper.g, Paper.b, 255);
		UiDraw.Outline(num + 0.006f, num3 + 0.008f, num2 - 0.012f, num4 - num3 - 0.016f, 196, 182, 150, 255);
		Random vr = new Random(Seeds.Stable("paperstyle/" + p.Name + "/" + p.Day));
		float num6 = num3 + 0.024f;
		Rules(num6);
		num6 += 0.016f;
		string text = p.Name.ToUpperInvariant();
		float num7 = 1.3f;
		while (num7 > 0.4f && Theme.TitleWidth(text, num7) > iw * 0.94f)
		{
			num7 -= 0.02f;
		}
		float num8 = ix + (iw - Theme.TitleWidth(text, num7) - 0.00052083336f) / 2f;
		UiDraw.TextFont(text, UiDraw.TitleFont, num8, num6, num7, Ink.r, Ink.g, Ink.b);
		UiDraw.TextFont(text, UiDraw.TitleFont, num8 + 0.00052083336f, num6, num7, Ink.r, Ink.g, Ink.b);
		num6 += num7 * 0.082f + 0.008f;
		RulesUp(num6);
		num6 += 0.014f;
		DateTime dateTime = FromGameMinutes(GameMinutesNow());
		string text2 = Pick<string>(vr, "PRICE FIVE CENTS", "FIVE CENTS", "ESTABLISHED 1868");
		string s = $"VOL. XXXI, NO. {4000 + p.Day % 1000}     *     {p.Town.ToUpperInvariant()}     *     {dateTime.Day} {MonthNames[dateTime.Month - 1].ToUpperInvariant()} {dateTime.Year}     *     {text2}";
		float num9 = 0.2f;
		while (num9 > 0.13f && Theme.TitleWidth(s, num9) > iw)
		{
			num9 -= 0.01f;
		}
		Centre(s, num6, num9, ix, iw);
		num6 += 0.026f;
		UiDraw.HLine(ix, ix + iw, num6, Ink.r, Ink.g, Ink.b, 255, 0.0014f);
		num6 += 0.01f;
		float num10 = 0.8f;
		while (num10 > 0.36f && Theme.TitleWidth(p.Banner, num10) > iw)
		{
			num10 -= 0.02f;
		}
		Centre(p.Banner, num6, num10, ix, iw);
		num6 += num10 * 0.082f + 0.008f;
		Rules(num6);
		num6 += 0.022f;
		float cw = (iw - 0.022f) / 2f;
		float num11 = ix;
		float num12 = ix + cw + 0.022f;
		float num13 = 0.13f;
		float bottom = num4 - 0.03f - num13;
		int layout = p.Layout;
		float num14 = ((layout == 1) ? num12 : num11);
		_ = 1;
		float num15 = num6;
		float num16 = num6;
		string text3 = p.Lead.Body ?? "";
		string text4 = null;
		int num17 = text3.IndexOf('"');
		if (num17 >= 0)
		{
			int num18 = text3.IndexOf('"', num17 + 1);
			if (num18 > num17 + 8)
			{
				text4 = text3.Substring(num17, num18 - num17 + 1);
			}
		}
		if (text4 != null)
		{
			int num19 = text3.LastIndexOf(". ", Math.Max(0, num17), StringComparison.Ordinal);
			int num20 = text3.IndexOf('.', text3.IndexOf('"', num17 + 1));
			if (num20 > 0)
			{
				text3 = ((num19 >= 0) ? text3.Substring(0, num19 + 1) : "") + text3.Substring(Math.Min(text3.Length, num20 + 1));
			}
			text4 = "\"" + text4.Trim('"', ',', ' ').TrimEnd(',', '.') + ".\"";
		}
		else if (!string.IsNullOrEmpty(p.Lead.Deck))
		{
			text4 = "\"" + p.Lead.Deck + "\"";
		}
		List<string> ql = ((text4 == null) ? new List<string>() : PaperWrap(text4.ToUpperInvariant(), 0.22f, cw - 0.03f, title: true).Take(3).ToList());
		float quoteH = ((ql.Count > 0) ? (0.024f + (float)ql.Count * 0.022f) : 0f);
		if (layout == 2)
		{
			float num21 = num6;
			foreach (string item in PaperWrap(p.Lead.Head, 0.42f, iw, title: true).Take(2))
			{
				Centre(item, num21, 0.42f, ix, iw);
				num21 += 0.03444f;
			}
			if (!string.IsNullOrEmpty(p.Lead.Deck))
			{
				num21 += 0.002f;
				foreach (string item2 in PaperWrap(p.Lead.Deck, 0.22f, iw, title: true).Take(1))
				{
					Centre(item2, num21, 0.22f, ix, iw);
					num21 += 0.022f;
				}
			}
			UiDraw.HLine(ix + iw * 0.35f, ix + iw * 0.65f, num21 + 0.004f, Ink.r, Ink.g, Ink.b, 255);
			num21 += 0.014f;
			List<string> list = PaperWrap(text3.Trim(), 0.205f, cw, title: false);
			int num22 = Math.Min(9, (list.Count + 1) / 2);
			PaperLines(list, 0, num22, num11, num21, 0.205f, 0.0195f, cut: false);
			PaperLines(list, num22, num22, num12, num21, 0.205f, 0.0195f, list.Count > 2 * num22);
			UiDraw.VLine(ix + cw + 0.011f, num21 - 0.004f, num21 + (float)num22 * 0.0195f, Faded.r, Faded.g, Faded.b, 255);
			num6 = num21 + (float)num22 * 0.0195f + 0.012f;
			Rules(num6);
			num6 += 0.022f;
			num15 = QuoteBox(num11, num6);
			num16 = num6;
		}
		else
		{
			float num23 = num6;
			foreach (string item3 in PaperWrap(p.Lead.Head, 0.34f, cw, title: true).Take(3))
			{
				Centre(item3, num23, 0.34f, num14, cw);
				num23 += 0.027880002f;
			}
			num23 += 0.01f;
			List<string> list2 = PaperWrap(text3.Trim(), 0.205f, cw, title: false);
			int num24 = Math.Max(1, Math.Min(14, (int)((bottom - quoteH - 0.014f - num23) / 0.0195f)));
			PaperLines(list2, 0, num24, num14, num23, 0.205f, 0.0195f, cut: true);
			num23 += (float)Math.Min(num24, list2.Count) * 0.0195f + 0.016f;
			num23 = QuoteBox(num14, Math.Min(num23, bottom - quoteH));
			if (layout == 1)
			{
				num16 = num23;
			}
			else
			{
				num15 = num23;
			}
			if (layout == 1)
			{
				num15 = num6;
			}
			else
			{
				num16 = num6;
			}
		}
		if (layout == 1)
		{
			num15 = PosterAt(num11, num15);
		}
		else
		{
			num16 = PosterAt(num12, num16);
		}
		List<string> list3 = p.About.Where((string a) => !a.StartsWith("Services for ")).ToList();
		if (p.Funerals.Count > 0)
		{
			bool flag = num15 <= num16;
			float num25 = (flag ? num11 : num12);
			float num26 = (flag ? num15 : num16);
			float num27 = 0.106f;
			int num28 = Math.Max(0, Math.Min(p.Funerals.Count, (int)((bottom - num26 - 0.048f) / num27)));
			if (num28 > 0 && UiDraw.TextSlotsLeft > 30)
			{
				float num29 = 0.04f + (float)num28 * num27;
				UiDraw.Outline(num25, num26, cw, num29, Ink.r, Ink.g, Ink.b, 255, 0.0034f);
				UiDraw.Outline(num25 + 0.005f, num26 + 0.007f, cw - 0.01f, num29 - 0.014f, Ink.r, Ink.g, Ink.b, 255);
				float num30 = num26 + 0.012f;
				Centre("FUNERAL NOTICES", num30, 0.22f, num25, cw);
				num30 += 0.026f;
				for (int num31 = 0; num31 < num28; num31++)
				{
					(string, string, string, string, string) tuple = p.Funerals[num31];
					float num32 = 0.25f;
					while (num32 > 0.15f && Theme.TitleWidth(tuple.Item1, num32) > cw - 0.02f)
					{
						num32 -= 0.01f;
					}
					Centre(tuple.Item1, num30, num32, num25, cw);
					num30 += 0.024f;
					if (num31 < p.FuneralPlans.Count && !p.FuneralPlans[num31].Known)
					{
						p.FuneralPlans[num31].Known = true;
						GrudgeLog("PAPER " + p.Name + ": the funeral notice for " + tuple.Item1 + " - marked on your map");
						SaveWorld();
						RefreshPlanBlips();
					}
					string[] array = new string[4] { tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5 };
					for (int num33 = 0; num33 < array.Length; num33++)
					{
						List<string> list4 = PaperWrap(array[num33], 0.18f, cw - 0.02f, title: false);
						string text5 = ((list4.Count > 0) ? (list4[0] + ((list4.Count > 1) ? "..." : "")) : "");
						UiDraw.TextFont(text5, UiDraw.BodyFont, num25 + (cw - Theme.MixedWidth(text5, 0.18f, 0.0222f)) / 2f, num30, 0.18f, Ink.r, Ink.g, Ink.b);
						num30 += 0.0175f;
					}
					num30 += 0.01f;
					if (num31 < num28 - 1)
					{
						UiDraw.HLine(num25 + cw * 0.4f, num25 + cw * 0.6f, num30 - 0.006f, Ink.r, Ink.g, Ink.b, 255);
					}
				}
				if (flag)
				{
					num15 = num26 + num29 + 0.016f;
				}
				else
				{
					num16 = num26 + num29 + 0.016f;
				}
			}
		}
		int num34 = 0;
		List<string> list5 = new List<string>(p.Further);
		string title = Pick<string>(vr, "ABOUT THE TERRITORY", "TALK OF THE TOWN", "LOCAL INTELLIGENCE");
		string title2 = Pick<string>(vr, "FROM AROUND THE TERRITORY", "NEWS FROM ABROAD", "ELSEWHERE IN THE TERRITORY");
		bool flag2 = !BountyNow() && (p.Wars.Count <= 0 || p.Wars[0].StartsWith("The outfits keep"));
		for (int num35 = 0; num35 < 12; num35++)
		{
			bool num36 = num15 <= num16;
			float x = (num36 ? num11 : num12);
			float yy = (num36 ? num15 : num16);
			bool flag3 = false;
			if (num34 < p.Stories.Count)
			{
				flag3 = StoryAt(p.Stories[num34], x, ref yy);
				if (flag3)
				{
					num34++;
				}
			}
			if (!flag3 && list3.Count > 0)
			{
				flag3 = ItemsAt(title, list3, x, ref yy);
			}
			if (!flag3 && list5.Count > 0)
			{
				flag3 = ItemsAt(title2, list5, x, ref yy);
			}
			if (!flag3 && !flag2)
			{
				flag3 = AdAt(x, ref yy);
				if (flag3)
				{
					flag2 = true;
				}
			}
			if (num36)
			{
				num15 = (flag3 ? yy : bottom);
			}
			else
			{
				num16 = (flag3 ? yy : bottom);
			}
			if ((num15 >= bottom - 0.01f && num16 >= bottom - 0.01f) || (!flag3 && num34 >= p.Stories.Count && list3.Count == 0 && list5.Count == 0 && flag2))
			{
				break;
			}
		}
		UiDraw.VLine(ix + cw + 0.011f, num6 - 0.006f, bottom, Faded.r, Faded.g, Faded.b, 255);
		float num37 = bottom + 0.006f;
		RulesUp(num37);
		num37 += 0.016f;
		Centre(Pick<string>(vr, "IN BRIEF", "NOTES & NOTICES", "BRIEFLY"), num37, 0.22f, ix, iw);
		num37 += 0.026f;
		List<string> list6 = p.About.Where((string a) => a.StartsWith("Services for ")).ToList().Concat(from article in p.Stories.Skip(num34)
			where !string.IsNullOrEmpty(article.Head)
			select Sentence(article.Head.Substring(0, 1) + article.Head.Substring(1).ToLowerInvariant()))
			.Concat(list3)
			.Concat(list5)
			.Take(2)
			.ToList();
		for (int num38 = 0; num38 < list6.Count; num38++)
		{
			float num39 = ((num38 % 2 == 0) ? num11 : num12);
			List<string> list7 = PaperWrap(list6[num38], 0.185f, cw, title: false);
			int num40 = Math.Min(4, list7.Count);
			for (int num41 = 0; num41 < num40; num41++)
			{
				if (UiDraw.TextSlotsLeft < 6)
				{
					break;
				}
				string text6 = ((num41 == num40 - 1 && list7.Count > num40) ? (list7[num41].TrimEnd('.', ',', ';') + "...") : list7[num41]);
				UiDraw.TextFont(text6, UiDraw.BodyFont, num39 + (cw - Theme.MixedWidth(text6, 0.185f, 0.0222f)) / 2f, num37 + (float)num41 * 0.0172f, 0.185f, Ink.r, Ink.g, Ink.b);
			}
		}
		if (list6.Count > 1)
		{
			UiDraw.VLine(ix + cw + 0.011f, num37 - 0.004f, num37 + 0.0688f, Faded.r, Faded.g, Faded.b, 255);
		}
		Rules(num4 - 0.026f);
		string help = (XPad.RecentlyUsed ? "B  Put it down" : "Backspace  Put it down");
		Theme.Footer(num4 + 0.012f, help);
		bool AdAt(float x2, ref float reference)
		{
			float num42 = PaperAdHeight(p.Ad, cw);
			if (bottom - reference < num42 + 0.004f)
			{
				return false;
			}
			PaperAd(p.Ad, x2, reference, cw);
			reference += num42 + 0.016f;
			return true;
		}
		static void Centre(string text7, float y, float sc, float cx, float cwid)
		{
			float num42 = Theme.TitleWidth(text7, sc);
			UiDraw.TextFont(text7, UiDraw.TitleFont, cx + (cwid - num42) / 2f, y, sc, Ink.r, Ink.g, Ink.b);
		}
		bool ItemsAt(string s2, List<string> items, float num42, ref float reference)
		{
			if (items.Count == 0 || bottom - reference < 0.085f || UiDraw.TextSlotsLeft < 16)
			{
				return false;
			}
			Centre(s2, reference, 0.22f, num42, cw);
			reference += 0.024f;
			UiDraw.HLine(num42 + cw * 0.3f, num42 + cw * 0.7f, reference, Ink.r, Ink.g, Ink.b, 255);
			reference += 0.008f;
			bool result = false;
			while (items.Count > 0)
			{
				List<string> list8 = PaperWrap(items[0], 0.19f, cw, title: false);
				int num43 = Math.Min(3, list8.Count);
				if (reference + (float)num43 * 0.0175f > bottom || UiDraw.TextSlotsLeft < 10)
				{
					break;
				}
				PaperLines(list8, 0, num43, num42, reference, 0.19f, 0.0175f, list8.Count > num43);
				reference += (float)num43 * 0.0175f + 0.008f;
				items.RemoveAt(0);
				result = true;
			}
			reference += 0.01f;
			return result;
		}
		float PosterAt(float px, float py0)
		{
			float num42 = 0.235f;
			UiDraw.Rect(px, py0, cw, num42, 205, 190, 156, 255);
			UiDraw.Outline(px, py0, cw, num42, Ink.r, Ink.g, Ink.b, 255, 0.0022f);
			UiDraw.Outline(px + 0.006f, py0 + 0.01f, cw - 0.012f, num42 - 0.02f, Faded.r, Faded.g, Faded.b, 255);
			int num43 = 0;
			try
			{
				num43 = BountyDollars;
			}
			catch
			{
			}
			float py1 = py0 + 0.022f;
			bool flag4 = p.Wars.Count > 0 && !p.Wars[0].StartsWith("The outfits keep");
			if (num43 > 0)
			{
				PT("WANTED", 0.62f);
				PT(Pick<string>(vr, "DEAD OR ALIVE", "FOR MURDER & ROBBERY", "BY ORDER OF THE SHERIFF"), 0.24f);
				PT((InterroLines.PlayerName ?? "Arthur Morgan").ToUpperInvariant(), 0.34f);
				PT($"${num43} REWARD", 0.4f);
				PT("APPLY TO ANY SHERIFF", 0.17f);
			}
			else if (flag4)
			{
				PT("NOTICE", 0.56f);
				PT("TO TRAVELLERS", 0.24f);
				py1 += 0.006f;
				PB(p.Wars[0], 0.2f);
			}
			else
			{
				string[] array2 = Ads[Math.Max(0, Array.IndexOf(Ads, p.Ad))].Split('|');
				py1 += 0.02f;
				PT(array2[0], 0.34f);
				py1 += 0.008f;
				PB((array2.Length > 1) ? array2[1] : "", 0.2f);
			}
			return py0 + num42 + 0.016f;
			void PB(string s2, float sc)
			{
				foreach (string item4 in PaperWrap(s2, sc, cw - 0.03f, title: false).Take(4))
				{
					UiDraw.TextFont(item4, UiDraw.BodyFont, px + (cw - Theme.MixedWidth(item4, sc, 0.0222f)) / 2f, py1, sc, Ink.r, Ink.g, Ink.b);
					py1 += sc * 0.095f;
				}
			}
			void PT(string s2, float sc)
			{
				foreach (string item5 in PaperWrap(s2, sc, cw - 0.03f, title: true).Take(2))
				{
					UiDraw.TextFont(item5, UiDraw.TitleFont, px + (cw - Theme.TitleWidth(item5, sc)) / 2f, py1, sc, Ink.r, Ink.g, Ink.b);
					py1 += sc * 0.082f;
				}
				py1 += 0.004f;
			}
		}
		float QuoteBox(float num43, float num42)
		{
			if (ql.Count == 0)
			{
				return num42;
			}
			UiDraw.Outline(num43 + 0.004f, num42, cw - 0.008f, quoteH, Ink.r, Ink.g, Ink.b, 255, 0.0016f);
			float num44 = num42 + 0.011f;
			foreach (string item6 in ql)
			{
				UiDraw.TextFont(item6, UiDraw.TitleFont, num43 + (cw - Theme.TitleWidth(item6, 0.22f)) / 2f, num44, 0.22f, Ink.r, Ink.g, Ink.b);
				num44 += 0.022f;
			}
			return num42 + quoteH + 0.02f;
		}
		void Rules(float num42)
		{
			UiDraw.HLine(ix, ix + iw, num42, Ink.r, Ink.g, Ink.b, 255, 0.0032f);
			UiDraw.HLine(ix, ix + iw, num42 + 0.0075f, Ink.r, Ink.g, Ink.b, 255, 0.0014f);
		}
		void RulesUp(float num42)
		{
			UiDraw.HLine(ix, ix + iw, num42, Ink.r, Ink.g, Ink.b, 255, 0.0014f);
			UiDraw.HLine(ix, ix + iw, num42 + 0.0055f, Ink.r, Ink.g, Ink.b, 255, 0.0032f);
		}
		bool StoryAt(Article a, float num42, ref float reference)
		{
			if (a == null || bottom - reference < 0.09182f || UiDraw.TextSlotsLeft < 16)
			{
				return false;
			}
			foreach (string item7 in PaperWrap(a.Head, 0.26f, cw, title: true).Take(2))
			{
				Centre(item7, reference, 0.26f, num42, cw);
				reference += 0.02132f;
			}
			UiDraw.HLine(num42 + cw * 0.375f, num42 + cw * 0.625f, reference + 0.002f, Ink.r, Ink.g, Ink.b, 255);
			reference += 0.01f;
			List<string> list8 = PaperWrap(a.Body, 0.205f, cw, title: false);
			int num43 = Math.Max(1, Math.Min(12, (int)((bottom - reference) / 0.0195f)));
			PaperLines(list8, 0, num43, num42, reference, 0.205f, 0.0195f, cut: true);
			reference += (float)Math.Min(num43, list8.Count) * 0.0195f + 0.02f;
			return true;
		}
	}

	private bool BountyNow()
	{
		try
		{
			return BountyDollars > 0;
		}
		catch
		{
			return false;
		}
	}

	private static float PaperAdHeight(string ad, float w)
	{
		string[] array = (ad ?? "").Split('|');
		int num = Math.Min(4, PaperWrap((array.Length > 1) ? array[1] : "", 0.19f, w - 0.016f, title: false).Count);
		return 0.04f + (float)num * 0.0178f + 0.01f;
	}

	private static void PaperAd(string ad, float x, float y, float w)
	{
		if (!string.IsNullOrEmpty(ad))
		{
			string[] array = ad.Split('|');
			List<string> list = PaperWrap((array.Length > 1) ? array[1] : "", 0.19f, w - 0.016f, title: false);
			int num = Math.Min(4, list.Count);
			float num2 = 0.04f + (float)num * 0.0178f + 0.01f;
			UiDraw.Outline(x + 0.002f, y, w - 0.004f, num2, Ink.r, Ink.g, Ink.b, 255);
			UiDraw.Outline(x + 0.005f, y + 0.005f, w - 0.01f, num2 - 0.01f, Faded.r, Faded.g, Faded.b, 255);
			float num3 = PaperHead(array[0], x + 0.008f, y + 0.01f, w - 0.016f, 0.22f, 1);
			for (int i = 0; i < num; i++)
			{
				float x2 = x + (w - Theme.MixedWidth(list[i], 0.19f, 0.0222f)) / 2f;
				UiDraw.TextFont(list[i], UiDraw.BodyFont, x2, num3 + 0.004f + (float)i * 0.0178f, 0.19f, Ink.r, Ink.g, Ink.b);
			}
		}
	}
}
