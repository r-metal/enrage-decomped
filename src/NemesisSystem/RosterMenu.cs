using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace NemesisSystem;

internal sealed class RosterMenu
{
	private enum Tier
	{
		Leader,
		Boss,
		Members,
		Fallen
	}

	private sealed class Row
	{
		public Tier Tier;

		public string Label;

		public string Standing;

		public string LoreName;

		public string LoreBlurb;

		public bool LoreVacant;

		public float Y;

		public int Slots;

		public bool FixedSeats;

		public List<NemesisRecord> Items;

		public int VisibleCount
		{
			get
			{
				if (!FixedSeats)
				{
					return Math.Min(Slots, Items.Count);
				}
				return Slots;
			}
		}
	}

	private sealed class ListEntry
	{
		public NemesisRecord Rec;

		public Acquaintance Acq;
	}

	private static readonly (int r, int g, int b) PriceGold = (r: 215, g: 178, b: 95);

	private int _crimeScroll;

	private int _crimeTotal;

	internal const float StatTileH = 0.056f;

	private const float PanelX = 0.07f;

	private const float PanelW = 0.86f;

	private const float Top = 0.07f;

	private const float HeaderH = 0.065f;

	private const float Pad = 0.01f;

	private const float CenterX = 0.5f;

	private const float CardW = 0.13f;

	private const float CardH = 0.075f;

	private const float CardGap = 0.012f;

	private const float FooterY = 0.868f;

	private const float DetailFooterY = 0.868f;

	private const int BossSeats = 1;

	private const int LieutenantSeats = 4;

	private const int ScrollSlots = 5;

	private const int LieutenantMinRank = 3;

	private static readonly (int r, int g, int b) LineColor = Theme.Line;

	private static readonly (int r, int g, int b) LabelColor = Theme.Gold;

	private readonly NemesisMod _mod;

	private readonly int[] _offsets = new int[8];

	private int _faction;

	private int _listIndex;

	private int _listScroll;

	private string _listKey;

	private const int ListRows = 16;

	private const float BarH = 0.12599999f;

	private const float BodyTop = 0.26099998f;

	private const float ListTop = 0.291f;

	private const float ListRowH = 0.032f;

	private static readonly (int r, int g, int b) UnaffiliatedGrey = (r: 138, g: 138, b: 136);

	private int _row = -1;

	private int _col;

	private NemesisRecord _detail;

	private int _historyScroll;

	private int _action;

	private bool _confirmForget;

	private bool _resetOpen;

	private bool _resetConfirm;

	private int _resetSel;

	private int _resetFaction;

	private static readonly string[] ResetRows = new string[7] { "Reset EVERYTHING", "Reset the X-ray log", "Reset world events", "Reset one faction", "Add 12 test loners", "Forget safe camps (Arthur's gang)", "Close" };

	private bool _worldPage;

	private int _worldIndex;

	private int _worldFilter;

	private int _worldPerson = -1;

	private WorldEvent _peopleFor;

	private int _peopleStart;

	private int _worldMouseTop = -1;

	private readonly Dictionary<string, (string s, int at)> _standingCache = new Dictionary<string, (string, int)>();

	private static readonly float[] ZoomLevels = new float[7] { 0.55f, 0.65f, 0.75f, 0.85f, 1f, 1.15f, 1.3f };

	private int _zoomIndex = -1;

	private float _z = 1f;

	private float _scrollY;

	private const float AreaBottom = 0.848f;

	private const float AreaTop = 0.27299997f;

	private bool _listAll;

	private List<ListEntry> _entriesCache;

	private (bool all, int count) _entriesKey;

	private int _entriesAt = -100000;

	private float _rowH = 0.032f;

	private float _rowGrow;

	private const string StarDict = "generic_textures";

	private bool _leanStat;

	private bool _leanTitle;

	private bool _rawNames;

	private double _textLogAt;

	private string _textLogFor;

	private float _leftEnd;

	private bool _mindView;

	private string _gapFor;

	private float _rowGap;

	private float _secGap;

	private bool _crimeView;

	private const int HistHoldSlots = 44;

	private int _histHold;

	private readonly HashSet<string> _pageNoted = new HashSet<string>();

	private const float WorldRowH = 0.032f;

	private WorldEvent _storyFor;

	private (WorldEvent, int, bool, long) _storyKey;

	private List<string> _story = new List<string>();

	private int _warSel;

	private int _warEvScroll;

	private int _warEvFor = -1;

	private int _warEvCount;

	private Gang Faction
	{
		get
		{
			if (_faction >= Gangs.All.Length)
			{
				if (_faction != Gangs.All.Length)
				{
					return null;
				}
				return Gangs.VanDerLinde;
			}
			return Gangs.All[_faction];
		}
	}

	private static int FactionCount => Gangs.All.Length + 2;

	public bool IsOpen { get; private set; }

	public bool InWorld
	{
		get
		{
			if (IsOpen)
			{
				return _worldPage;
			}
			return false;
		}
	}

	private float CW => 0.13f * _z;

	private float CH => 0.075f * _z;

	private float Gap => 0.012f * _z;

	private float LineGap => 0.016f * _z;

	private float RowGap => Math.Max(0.05f, 0.062f * _z);

	private int PerLine => Math.Max(1, (int)((0.8f + Gap) / (CW + Gap)));

	private string NextViewName
	{
		get
		{
			if (!_mindView)
			{
				if (!_crimeView)
				{
					if (!HerPage)
					{
						return "His Mind";
					}
					return "Her Mind";
				}
				return "History";
			}
			if (!HerPage)
			{
				return "His Crimes";
			}
			return "Her Crimes";
		}
	}

	private bool HerPage
	{
		get
		{
			if (_detail != null)
			{
				return Models.Female(_detail.ModelHash);
			}
			return false;
		}
	}

	private string ResetFactionName
	{
		get
		{
			if (_resetFaction >= Gangs.All.Length)
			{
				return "Unaffiliated";
			}
			return Gangs.All[_resetFaction].Name;
		}
	}

	private static int WorldRows => 20;

	private static bool UsingPad => XPad.RecentlyUsed;

	private int WarsFilter => Gangs.All.Length + 3;

	private float DrawCrimeSheet(NemesisRecord n, float rx, float colW, float y, float bottom, (int r, int g, int b) acc)
	{
		CrimeProfile crimeProfile = _mod.CrimeSheet(n);
		float right = rx + colW;
		int skip = _crimeScroll;
		int total = 0;
		int drawn = 0;
		bool over = false;
		Theme.Section(crimeProfile.You ? "YOUR CRIMINAL RECORD" : "CRIMINAL PROFILE", rx, y, right, acc);
		y += 0.032f;
		float top = y;
		if (Unit(0.066f, 10))
		{
			UiDraw.Rect(rx, y, colW, 0.06f, Theme.Card.r, Theme.Card.g, Theme.Card.b, 235);
			Theme.Hairline(rx, y, colW, 0.06f, crimeProfile.BannerCol.r, crimeProfile.BannerCol.g, crimeProfile.BannerCol.b, 170);
			UiDraw.Rect(rx, y, 0.0015625001f, 0.06f, crimeProfile.BannerCol.r, crimeProfile.BannerCol.g, crimeProfile.BannerCol.b, 255);
			UiDraw.TextFont(crimeProfile.BannerLabel, UiDraw.TitleFont, rx + 0.014f, y + 0.009f, 0.24f, crimeProfile.BannerCol.r, crimeProfile.BannerCol.g, crimeProfile.BannerCol.b);
			UiDraw.Text(FitText(crimeProfile.BannerWord ?? "", 0.19f, 0.15f, name: false), rx + 0.014f, y + 0.034f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			if (!string.IsNullOrEmpty(crimeProfile.BannerValue))
			{
				(int, int, int) tuple = (crimeProfile.You ? crimeProfile.BannerCol : ((crimeProfile.BannerValue == "-") ? Theme.Dim : PriceGold));
				UiDraw.TextFont(crimeProfile.BannerValue, UiDraw.TitleFont, rx + 0.17f, y + 0.03f - 0.017250001f, 0.46f, tuple.Item1, tuple.Item2, tuple.Item3);
			}
			if (crimeProfile.You && crimeProfile.Rep != int.MinValue)
			{
				float num = 0.11f;
				float num2 = right - num - 0.012f;
				(int, int, int) c = NemesisMod.RepColor(crimeProfile.Rep);
				UiDraw.TextFont("REPUTATION", UiDraw.TitleFont, num2, y + 0.008f, 0.17f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				string text = NemesisMod.RepWord(crimeProfile.Rep).ToUpperInvariant();
				UiDraw.TextFont(text, UiDraw.TitleFont, num2 + num - Theme.CapsWidth(text, 0.2f), y + 0.007f, 0.2f, c.Item1, c.Item2, c.Item3);
				TwoSided(num2, y + 0.04f, num, crimeProfile.Rep, c);
			}
			else if (!string.IsNullOrEmpty(crimeProfile.Side))
			{
				float num3 = Theme.CapsWidth(crimeProfile.Side, 0.21f) + 0.014f;
				float num4 = right - (rx + 0.27f) - 0.012f;
				string obj = ((num3 > num4) ? FitText(crimeProfile.Side, 0.21f, num4 - 0.014f, name: false) : crimeProfile.Side);
				num3 = Theme.CapsWidth(obj, 0.21f) + 0.014f;
				Theme.Chip(obj, right - num3 - 0.012f, y + 0.03f - 0.011f, crimeProfile.SideCol, crimeProfile.SideFilled);
			}
			y += 0.074f;
		}
		if (!crimeProfile.You)
		{
			if (Unit(0.06f))
			{
				Sec(crimeProfile.CrimesTitle);
			}
			foreach (var (text2, text3, text4) in crimeProfile.Crimes)
			{
				if (Unit(0.027f))
				{
					UiDraw.Text(text2 ?? "", rx + 0.008f, y + 0.002f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
					float num5 = rx + 0.07f;
					UiDraw.Text(FitText(text3 ?? "", 0.22f, right - 0.088f - 0.012f - num5, name: false), num5, y + 0.001f, 0.22f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
					Theme.Badge(text4, right - 0.088f - 0.004f, y - 0.002f, 0.088f, Theme.Warn, filled: false);
					y += 0.028f;
				}
			}
			Note(crimeProfile.CrimesNote);
			y += 0.008f;
			if (Unit(0.06f))
			{
				Sec("IN THE WORLD");
			}
			foreach (var (text5, color, s, text6, color2) in crimeProfile.Events)
			{
				if (Unit(0.027f))
				{
					Theme.Badge(text5, rx + 0.004f, y - 0.002f, 0.078f, color, filled: false);
					float num6 = rx + 0.078f + 0.014f;
					UiDraw.Text(FitText(s, 0.21f, right - 0.092f - 0.012f - num6, name: false), num6, y + 0.002f, 0.21f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
					if (!string.IsNullOrEmpty(text6))
					{
						Theme.Badge(text6, right - 0.092f - 0.004f, y - 0.002f, 0.092f, color2, text6 == "KILLED");
					}
					y += 0.028f;
				}
			}
			Note(crimeProfile.EventsNote);
			y += 0.008f;
		}
		if (crimeProfile.You && crimeProfile.Events.Count > 0)
		{
			if (Unit(0.06f))
			{
				Sec("YOUR STORY");
			}
			foreach (var (text7, color3, s2, text8, _) in crimeProfile.Events)
			{
				if (Unit(0.027f))
				{
					Theme.Badge(text7, rx + 0.004f, y - 0.002f, 0.078f, color3, filled: false);
					float num7 = rx + 0.078f + 0.014f;
					UiDraw.Text(FitText(s2, 0.21f, right - 0.06f - 0.012f - num7, name: false), num7, y + 0.002f, 0.21f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
					UiDraw.Text(text8 ?? "", right - 0.06f, y + 0.003f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
					y += 0.028f;
				}
			}
			Note(crimeProfile.EventsNote);
			y += 0.008f;
		}
		if (crimeProfile.Tiles.Count > 0)
		{
			if (Unit(0.1f, 20))
			{
				Sec(crimeProfile.TilesTitle);
				y = StatTiles(rx, colW, y, crimeProfile.Tiles, crimeProfile.You ? 4 : 3, bottom);
			}
		}
		else if (crimeProfile.You && Unit(0.06f))
		{
			Sec(crimeProfile.TilesTitle);
			if (Room(0.022f, 6))
			{
				UiDraw.Text("Nothing counted yet", rx + 0.01f, y, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				y += 0.022f;
			}
		}
		if (!crimeProfile.You)
		{
			return CrimeScrollBar(rx, colW, top, bottom, ref y, total, drawn, over);
		}
		if (Unit(0.06f))
		{
			Sec("NAMED DEAD");
			if (crimeProfile.Dead.Count == 0)
			{
				if (Room(0.022f, 6))
				{
					UiDraw.Text("No one on the rosters", rx + 0.01f, y, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
					y += 0.022f;
				}
			}
			else
			{
				float num8 = rx + 0.006f;
				foreach (string item in crimeProfile.Dead)
				{
					string text9 = item.ToUpperInvariant();
					float num9 = Theme.CapsWidth(text9, 0.21f) + 0.014f;
					if (num8 + num9 > right)
					{
						num8 = rx + 0.006f;
						y += 0.028f;
					}
					if (!Room(0.028f, 6))
					{
						break;
					}
					num8 += Theme.Chip(text9, num8, y, Theme.Blood) + 0.006f;
				}
				if (crimeProfile.DeadMore > 0 && Room(0.028f, 4))
				{
					string text10 = $"+{crimeProfile.DeadMore} MORE";
					float num10 = Theme.CapsWidth(text10, 0.21f) + 0.014f;
					if (num8 + num10 > right)
					{
						num8 = rx + 0.006f;
						y += 0.028f;
					}
					Theme.Chip(text10, num8, y, Theme.Dim);
				}
				y += 0.034f;
			}
		}
		if (Unit(0.1f, 20))
		{
			Sec("WITNESSES");
			y = StatTiles(rx, colW, y, crimeProfile.Witnesses, 3, bottom);
		}
		if (crimeProfile.Towns.Count > 0)
		{
			if (Unit(0.06f))
			{
				Sec("BY TOWN");
			}
			foreach (var (text11, num11) in crimeProfile.Towns)
			{
				if (Unit(0.026f, 6))
				{
					(int, int, int) c2 = NemesisMod.RepColor(num11);
					UiDraw.TextFont(FitTitle(text11.ToUpperInvariant(), 0.2f, 0.1f, name: false), UiDraw.TitleFont, rx + 0.008f, y + 0.002f, 0.2f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
					UiDraw.TextFont(NemesisMod.RepWord(num11).ToUpperInvariant(), UiDraw.TitleFont, rx + 0.115f, y + 0.002f, 0.2f, c2.Item1, c2.Item2, c2.Item3);
					TwoSided(rx + 0.195f, y + 0.01f, right - rx - 0.205f, num11, c2);
					y += 0.026f;
				}
			}
			y += 0.008f;
		}
		if (crimeProfile.Factions.Count > 0)
		{
			if (Unit(0.06f))
			{
				Sec("FACTIONS");
			}
			foreach (var (text12, num12, tuple7) in crimeProfile.Factions)
			{
				if (Unit(0.026f, 6))
				{
					UiDraw.TextFont(FitTitle(text12.ToUpperInvariant(), 0.2f, 0.15f, name: false), UiDraw.TitleFont, rx + 0.008f, y + 0.002f, 0.2f, tuple7.Item1, tuple7.Item2, tuple7.Item3);
					(int, int, int) tuple8 = Notoriety.LevelColors[Notoriety.Level(num12)];
					UiDraw.TextFont(Notoriety.LevelName(num12).ToUpperInvariant(), UiDraw.TitleFont, rx + 0.165f, y + 0.002f, 0.2f, tuple8.Item1, tuple8.Item2, tuple8.Item3);
					float num13 = rx + 0.255f;
					float num14 = right - num13 - 0.01f;
					float h = 0.0046296297f;
					UiDraw.Rect(num13, y + 0.008f, num14, h, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 235);
					UiDraw.Rect(num13, y + 0.008f, num14 * (float)Math.Max(0, Math.Min(100, num12)) / 100f, h, tuple8.Item1, tuple8.Item2, tuple8.Item3, 255);
					y += 0.026f;
				}
			}
		}
		return CrimeScrollBar(rx, colW, top, bottom, ref y, total, drawn, over);
		void Note(string text13)
		{
			if (!string.IsNullOrEmpty(text13) && Unit(0.022f, 6))
			{
				UiDraw.Text(FitText(text13, 0.2f, colW - 0.02f, name: false), rx + 0.01f, y, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				y += 0.022f;
			}
		}
		bool Room(float need, int slots = 14)
		{
			if (y + need <= bottom)
			{
				return UiDraw.TextSlotsLeft >= slots;
			}
			return false;
		}
		void Sec(string title)
		{
			Theme.Section(title, rx, y, right, acc);
			y += 0.03f;
		}
		bool Unit(float need, int slots = 14)
		{
			total++;
			if (skip > 0)
			{
				skip--;
				return false;
			}
			if (over || !Room(need, slots))
			{
				over = true;
				return false;
			}
			drawn++;
			return true;
		}
	}

	private float CrimeScrollBar(float rx, float colW, float top, float bottom, ref float y, int total, int drawn, bool over)
	{
		_crimeTotal = total;
		if (_crimeScroll > 0 && drawn == 0)
		{
			_crimeScroll = Math.Max(0, total - 1);
		}
		if (over || _crimeScroll > 0)
		{
			Theme.ScrollBar(rx + colW + 0.004f, top, Math.Max(0.02f, bottom - top), _crimeScroll, Math.Max(1, drawn), Math.Max(1, total));
		}
		return y;
	}

	private static float StatTiles(float x, float w, float y, IList<(string num, string label, (int r, int g, int b) col)> tiles, int perRow, float bottom = 1f)
	{
		int num = Math.Min(perRow, Math.Max(1, tiles.Count));
		float num2 = (w - 0.008f * (float)(num - 1)) / (float)num;
		for (int i = 0; i < tiles.Count; i++)
		{
			int num3 = i % num;
			if (num3 == 0 && i > 0)
			{
				y += 0.064f;
			}
			if (y + 0.056f > bottom || UiDraw.TextSlotsLeft < 6)
			{
				return y;
			}
			float num4 = x + (float)num3 * (num2 + 0.008f);
			var (text, text2, tuple2) = tiles[i];
			UiDraw.Rect(num4, y, num2, 0.056f, Theme.Card.r, Theme.Card.g, Theme.Card.b, 235);
			Theme.Hairline(num4, y, num2, 0.056f, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 235);
			float num5 = 0.17f;
			while (num5 > 0.12f && Theme.CapsWidth(text2, num5) > num2 - 0.01f)
			{
				num5 -= 0.01f;
			}
			float num6 = 0.0285f;
			float num7 = num5 * 0.075f;
			float num8 = y + (0.056f - num6 - 0.002f - num7) / 2f;
			UiDraw.TextFont(text, UiDraw.TitleFont, num4 + num2 / 2f - Theme.CapsWidth(text, 0.38f) / 2f - 0.0008f, num8, 0.38f, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			UiDraw.TextFont(text2, UiDraw.TitleFont, num4 + num2 / 2f - Theme.CapsWidth(text2, num5) / 2f - 0.0008f, num8 + num6 + 0.002f, num5, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		return y + 0.056f + 0.014f;
	}

	private static void Stars(float x, float cy, int rank)
	{
		float num = 0.016f;
		float num2 = num / UiDraw.Aspect;
		float num3 = 0.0012f;
		bool flag = UiDraw.EnsureTextureDict("generic_textures");
		for (int i = 0; i < 5; i++)
		{
			float num4 = x + (float)i * (num2 + num3) + num2 / 2f;
			bool flag2 = i < Math.Max(0, Math.Min(5, rank));
			if (flag)
			{
				UiDraw.Sprite("generic_textures", "star", num4, cy, num2, num, 0f, flag2 ? Theme.Gold.r : 78, flag2 ? Theme.Gold.g : 75, flag2 ? Theme.Gold.b : 70, flag2 ? 255 : 235);
			}
			else
			{
				UiDraw.Rect(num4 - num2 * 0.3f, cy - num * 0.3f, num2 * 0.6f, num * 0.6f, flag2 ? Theme.Gold.r : 70, flag2 ? Theme.Gold.g : 68, flag2 ? Theme.Gold.b : 64, 230);
			}
		}
	}

	private static void TwoSided(float x, float y, float w, int value, (int r, int g, int b) c)
	{
		float num = 0.0046296297f;
		float num2 = x + w / 2f;
		UiDraw.Rect(x, y, w, num, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 235);
		float num3 = (float)Math.Max(-100, Math.Min(100, value)) / 100f * (w / 2f);
		if (num3 >= 0f)
		{
			UiDraw.Rect(num2, y, num3, num, c.r, c.g, c.b, 255);
		}
		else
		{
			UiDraw.Rect(num2 + num3, y, 0f - num3, num, c.r, c.g, c.b, 255);
		}
		UiDraw.Rect(num2 - 0.00052083336f, y - 0.0018518518f, 0.0010416667f, num + 0.0037037036f, 235, 230, 220, 220);
	}

	private static string KeyOf(ListEntry e)
	{
		if (e?.Rec == null)
		{
			return "a:" + (e?.Acq?.Id ?? e?.Acq?.Name);
		}
		return "r:" + (e.Rec.Id ?? e.Rec.Name);
	}

	private List<NemesisRecord> MembersOf(Gang g)
	{
		if (g == null || !g.StoryOnly)
		{
			return _mod.Roster.Where((NemesisRecord n) => n.Gang == g?.Id).ToList();
		}
		return _mod.StoryRoster;
	}

	private static string FactionName(Gang g)
	{
		return g?.Name ?? "Unaffiliated";
	}

	private static (int r, int g, int b) AccentOf(Gang g)
	{
		return g?.Accent ?? UnaffiliatedGrey;
	}

	private void DrawFactionBar(string status)
	{
		List<string> names = Gangs.All.Select((Gang g) => g.Name).Concat(new string[2] { "Van der Linde", "Unaffiliated" }).ToList();
		Theme.FactionBar(0.13499999f, names, _faction, Faction != null, AccentOf(Faction), status, null, chipAsText: false, PickFaction);
		float num = 0.17499998f;
		Gang faction = Faction;
		if (faction != null && faction.StoryOnly)
		{
			Theme.NoteBand(num, "Your own gang, as the story has it - their lives and minds on each page. Nobody here will come for you.");
			Theme.StrengthRow(num + 0.032f, -1, null, null);
			Theme.GroundRow(num + 0.032f + 0.028f, null);
		}
		else if (faction != null)
		{
			Theme.NotorietyBand(num, _mod.NotorietyOf(faction), null, null, null, _mod.AgendaText(faction));
			(double, double, double, double) tuple = _mod.StrengthParts(faction);
			int num2 = _mod.Strength(faction);
			Theme.StrengthRow(num + 0.032f, num2, NemesisMod.StrengthWord(num2), new(string, double)[4]
			{
				("HOLDING", tuple.Item1),
				("MEN", tuple.Item2),
				("MONEY", tuple.Item3),
				("SUPPLIES", tuple.Item4)
			});
			var (item, item2, item3) = _mod.GroundOf(faction);
			Theme.GroundRow(num + 0.032f + 0.028f, new(string, List<string>, (int, int, int))[3]
			{
				("HOLDS", item, Theme.Good),
				("STRONG IN", item2, Theme.Text),
				("A FOOTHOLD IN", item3, Theme.Dim)
			}, _mod.IsDestroyed(faction) ? "Finished - no ground left" : "No ground of their own");
		}
		else
		{
			Theme.NoteBand(num, "No notoriety here - each of these men holds his own grudge.");
			Theme.StrengthRow(num + 0.032f, -1, null, null);
			Theme.GroundRow(num + 0.032f + 0.028f, null);
		}
	}

	private void PickFaction(int i)
	{
		if (i != _faction)
		{
			NemesisMod.UiSound((i > _faction) ? "BUMPER_RIGHT" : "BUMPER_LEFT");
			_faction = i;
			_row = -1;
			_col = 0;
			_scrollY = 0f;
			Array.Clear(_offsets, 0, _offsets.Length);
		}
	}

	private bool HoverCard(int r, int c)
	{
		if (_row == r && _col == c)
		{
			return false;
		}
		_row = r;
		_col = c;
		return true;
	}

	public void ShowWorld()
	{
		_worldPage = true;
		_detail = null;
		_worldPerson = -1;
		_confirmForget = false;
	}

	public bool ShowArthur()
	{
		_mod.MarkYouInStoryGang();
		NemesisRecord nemesisRecord = _mod.StoryRoster.FirstOrDefault((NemesisRecord r) => r.Id == NemesisMod.YouId);
		if (nemesisRecord == null)
		{
			return false;
		}
		_worldPage = false;
		_resetOpen = false;
		_worldPerson = -1;
		_confirmForget = false;
		_faction = Gangs.All.Length;
		_detail = nemesisRecord;
		_action = 0;
		_historyScroll = 0;
		_crimeScroll = 0;
		return true;
	}

	public void ShowHierarchy()
	{
		_worldPage = false;
		_detail = null;
		_worldPerson = -1;
		_confirmForget = false;
		_resetOpen = false;
	}

	public void ShowRecord(NemesisRecord r)
	{
		if (r != null)
		{
			_worldPage = false;
			_resetOpen = false;
			_worldPerson = -1;
			_confirmForget = false;
			int num = Array.FindIndex(Gangs.All, (Gang g) => g.Id == r.Gang);
			_faction = ((num >= 0) ? num : ((r.Story || r.Gang == Gangs.VanDerLinde.Id) ? Gangs.All.Length : (Gangs.All.Length + 1)));
			_detail = r;
			_action = 0;
			_historyScroll = 0;
			_crimeScroll = 0;
		}
	}

	public void ShowEvent(WorldEvent e)
	{
		ShowWorld();
		_worldFilter = 0;
		_worldMouseTop = -1;
		_worldIndex = Math.Max(0, _mod.ChronicleFor(0).IndexOf(e));
	}

	public void ShowWar(WarRecord w)
	{
		ShowWorld();
		_worldFilter = WarsFilter;
		_worldMouseTop = -1;
		_warSel = Math.Max(0, _mod.WarList().FindIndex(((WarRecord w, bool live) p) => p.w == w));
	}

	public RosterMenu(NemesisMod mod)
	{
		_mod = mod;
	}

	public void Open()
	{
		if (!IsOpen)
		{
			Toggle();
		}
	}

	public void Close()
	{
		if (IsOpen)
		{
			Toggle();
		}
	}

	public void Toggle()
	{
		IsOpen = !IsOpen;
		if (IsOpen)
		{
			_mod.MarkYouInStoryGang();
		}
		_detail = null;
		_worldPage = false;
		_resetOpen = false;
		_worldPerson = -1;
		_row = -1;
		_confirmForget = false;
		Array.Clear(_offsets, 0, _offsets.Length);
	}

	private List<Row> BuildRows()
	{
		return BuildRows(Faction);
	}

	private List<Row> BuildRows(Gang gang)
	{
		List<NemesisRecord> source = MembersOf(gang);
		string succ = ((gang != null) ? _mod.SuccessorId(gang) : null);
		List<NemesisRecord> list = (from n in source
			where n.IsAlive && !n.PresumedDead && (succ == null || n.Id != succ)
			orderby n.Rank descending, n.Hate descending, n.Name
			select n).ToList();
		List<NemesisRecord> list2 = (from n in source.Where((NemesisRecord n) => !n.IsAlive || n.PresumedDead).Reverse()
			orderby (n.History == null || n.History.Count <= 0) ? DateTime.MinValue : n.History.Max((HistoryEntry h) => h.At) descending
			select n).ToList();
		List<Row> list3 = new List<Row>();
		if (gang != null)
		{
			if (!gang.StoryOnly)
			{
				(string name, string blurb, bool vacant) tuple = _mod.LeaderCard(gang);
				string item = tuple.name;
				string item2 = tuple.blurb;
				bool item3 = tuple.vacant;
				NemesisRecord nemesisRecord = (item3 ? null : _mod.LeaderPageOf(gang));
				list3.Add(new Row
				{
					Tier = Tier.Leader,
					Label = "LEADER",
					Slots = 1,
					FixedSeats = true,
					Items = ((nemesisRecord != null) ? new List<NemesisRecord> { nemesisRecord } : new List<NemesisRecord>()),
					LoreName = item,
					LoreBlurb = item2,
					LoreVacant = item3
				});
			}
			List<NemesisRecord> list4 = new List<NemesisRecord>(list);
			for (int num = 0; num < gang.Tiers.Length; num++)
			{
				(string, int, int) tuple2 = gang.Tiers[num];
				string item4 = tuple2.Item1;
				int minRank = tuple2.Item2;
				int item5 = tuple2.Item3;
				bool flag = num == gang.Tiers.Length - 1;
				List<NemesisRecord> list5 = (flag ? list4.ToList() : list4.Where((NemesisRecord n) => n.Rank >= minRank).Take(item5).ToList());
				list4.RemoveAll(list5.Contains);
				string standing = ((item4 == "BOYS") ? "One of the Boys" : (item4.Substring(0, 1) + item4.Substring(1, item4.Length - 2).ToLowerInvariant()));
				list3.Add(new Row
				{
					Tier = Tier.Members,
					Label = item4,
					Standing = standing,
					Slots = item5,
					FixedSeats = !flag,
					Items = list5
				});
			}
		}
		else
		{
			List<NemesisRecord> boss = list.Where((NemesisRecord n) => n.Rank >= 5).Take(1).ToList();
			List<NemesisRecord> lieutenants = list.Where((NemesisRecord n) => !boss.Contains(n) && n.Rank >= 3).Take(4).ToList();
			List<NemesisRecord> items = list.Where((NemesisRecord n) => !boss.Contains(n) && !lieutenants.Contains(n)).ToList();
			list3.Add(new Row
			{
				Tier = Tier.Boss,
				Label = "BOSS",
				Standing = "Boss",
				Slots = 1,
				FixedSeats = true,
				Items = boss
			});
			list3.Add(new Row
			{
				Tier = Tier.Members,
				Label = "CAPTAINS",
				Standing = "Captain",
				Slots = 4,
				FixedSeats = true,
				Items = lieutenants
			});
			list3.Add(new Row
			{
				Tier = Tier.Members,
				Label = "OUTLAWS",
				Standing = "Outlaw",
				Slots = 5,
				Items = items
			});
		}
		list3.Add(new Row
		{
			Tier = Tier.Fallen,
			Label = $"FALLEN ({list2.Count})",
			Standing = "Fallen",
			Slots = 5,
			Items = list2
		});
		Layout(list3);
		return list3;
	}

	internal string StandingOf(NemesisRecord rec)
	{
		Gang gang = Gangs.ById(rec.Gang);
		if (gang == null)
		{
			if (rec.IsAlive && !rec.PresumedDead)
			{
				return "Unaffiliated";
			}
			return "Fallen";
		}
		string key = rec.Id ?? rec.Name ?? "";
		if (_standingCache.TryGetValue(key, out (string, int) value) && UiDraw.RealMs - value.Item2 < 250)
		{
			return value.Item1;
		}
		float z = _z;
		float scrollY = _scrollY;
		string text = "Unknown";
		foreach (Row item in BuildRows(gang))
		{
			if (item.Items.Contains(rec))
			{
				text = ((item.Tier != Tier.Fallen) ? (item.Standing + " of the " + gang.Name) : item.Standing);
				break;
			}
		}
		_z = z;
		_scrollY = scrollY;
		if (_standingCache.Count > 200)
		{
			_standingCache.Clear();
		}
		_standingCache[key] = (text, UiDraw.RealMs);
		return text;
	}

	private float T(float scale)
	{
		return Math.Max(0.17f, scale * _z);
	}

	private int Shown(Row row)
	{
		if (row.Tier != Tier.Fallen)
		{
			if (!row.FixedSeats)
			{
				return row.Items.Count;
			}
			return Math.Max(row.Slots, row.Items.Count);
		}
		return Math.Min(PerLine, row.Items.Count);
	}

	private int LinesOf(Row row)
	{
		if (row.Tier != Tier.Fallen)
		{
			return Math.Max(1, (Shown(row) + PerLine - 1) / PerLine);
		}
		return 1;
	}

	private float RowHeight(Row row)
	{
		return (float)LinesOf(row) * CH + (float)(LinesOf(row) - 1) * LineGap;
	}

	private float Stack(List<Row> rows)
	{
		float num = 0.31299996f;
		float num2 = num;
		foreach (Row row in rows)
		{
			if (row.Tier == Tier.Fallen)
			{
				num += 0.015f;
			}
			row.Y = num;
			num2 = num + RowHeight(row);
			num = num2 + RowGap;
		}
		return num2;
	}

	private void Layout(List<Row> rows)
	{
		if (_zoomIndex < 0)
		{
			_z = 1f;
			while (_z > 0.5f && Stack(rows) > 0.848f)
			{
				_z -= 0.05f;
			}
		}
		else
		{
			_z = ZoomLevels[_zoomIndex];
		}
		Stack(rows);
	}

	private void Zoom(int dir)
	{
		if (_zoomIndex < 0)
		{
			_zoomIndex = 0;
			for (int i = 0; i < ZoomLevels.Length; i++)
			{
				if (ZoomLevels[i] <= _z + 0.001f)
				{
					_zoomIndex = i;
				}
			}
		}
		_zoomIndex = Math.Max(0, Math.Min(ZoomLevels.Length - 1, _zoomIndex + dir));
	}

	private float RowStartX(Row row)
	{
		int num = Math.Max(1, Math.Min(PerLine, Shown(row)));
		return 0.5f - ((float)num * CW + (float)(num - 1) * Gap) / 2f;
	}

	private float SeatX(Row row, int seat)
	{
		return RowStartX(row) + (float)((row.Tier == Tier.Fallen) ? seat : (seat % PerLine)) * (CW + Gap);
	}

	private float SeatY(Row row, int seat)
	{
		return row.Y + (float)((row.Tier != Tier.Fallen) ? (seat / PerLine) : 0) * (CH + LineGap);
	}

	private int SeatOf(Row row, int r, int item)
	{
		if (row.Tier != Tier.Fallen)
		{
			return item;
		}
		return item - _offsets[r];
	}

	private int LineOf(Row row, int seat)
	{
		if (row.Tier != Tier.Fallen)
		{
			return seat / PerLine;
		}
		return 0;
	}

	private float Sy(float y)
	{
		return y - _scrollY;
	}

	private static bool Visible(float y, float h)
	{
		if (y >= 0.27199998f)
		{
			return y + h <= 0.849f;
		}
		return false;
	}

	public bool HandleKey(Keys key)
	{
		if (_resetOpen)
		{
			return HandleResetKey(key);
		}
		if ((key == Keys.Home || key == Keys.A) && ShowArthur())
		{
			return true;
		}
		if (_detail != null)
		{
			return HandleDetailKey(key);
		}
		if (_worldPage)
		{
			return HandleWorldKey(key);
		}
		if (Faction == null)
		{
			return HandleListKey(key);
		}
		List<Row> list = BuildRows();
		EnsureSelection(list);
		switch (key)
		{
		case Keys.Left:
			if (_row >= 0)
			{
				_col = Math.Max(0, _col - 1);
			}
			break;
		case Keys.Right:
			if (_row >= 0)
			{
				_col = Math.Min(list[_row].Items.Count - 1, _col + 1);
			}
			break;
		case Keys.Up:
			MoveRow(list, -1);
			break;
		case Keys.Down:
			MoveRow(list, 1);
			break;
		case Keys.Return:
			if (_row >= 0)
			{
				_detail = list[_row].Items[_col];
				_action = 0;
				_confirmForget = false;
				_historyScroll = 0;
				_crimeScroll = 0;
			}
			break;
		case Keys.P:
			if (_row >= 0 && _mod.DebugOn && !list[_row].Items[_col].Story && !list[_row].Items[_col].LeaderPage)
			{
				PromoteAndFollow(list[_row].Items[_col]);
			}
			break;
		case Keys.H:
			if (_mod.DebugOn && Faction != null && !Faction.StoryOnly)
			{
				_mod.AddCampHere(Faction);
			}
			break;
		case Keys.R:
			if (_mod.DebugOn)
			{
				_mod.SetRandomAmbushes(!_mod.Config.RandomAmbushes);
			}
			else
			{
				_mod.OpenGuide();
			}
			break;
		case Keys.G:
			_mod.OpenGuide();
			break;
		case Keys.O:
			_mod.OpenSetupScreen();
			break;
		case Keys.N:
			_mod.CycleMessages();
			break;
		case Keys.C:
			_mod.CycleNameCards();
			break;
		case Keys.Add:
		case Keys.Oemplus:
			Zoom(1);
			Layout(list);
			break;
		case Keys.Subtract:
		case Keys.OemMinus:
			Zoom(-1);
			Layout(list);
			break;
		case Keys.Z:
			_zoomIndex = -1;
			_scrollY = 0f;
			Layout(list);
			break;
		case Keys.E:
		case Keys.Q:
			_faction = (_faction + ((key == Keys.E) ? 1 : (FactionCount - 1))) % FactionCount;
			_row = -1;
			_col = 0;
			_scrollY = 0f;
			Array.Clear(_offsets, 0, _offsets.Length);
			return true;
		case Keys.Back:
			Toggle();
			break;
		default:
			return false;
		}
		if (_row >= 0)
		{
			KeepVisible(list[_row], _row);
		}
		return true;
	}

	private List<ListEntry> Entries()
	{
		(bool, int) entriesKey = (_listAll, _mod.Roster.Count);
		if (_entriesCache != null && entriesKey.Equals(_entriesKey) && UiDraw.RealMs - _entriesAt < 250)
		{
			return _entriesCache;
		}
		_entriesKey = entriesKey;
		_entriesAt = UiDraw.RealMs;
		return _entriesCache = EntriesNow();
	}

	private List<ListEntry> EntriesNow()
	{
		List<NemesisRecord> source = Unaffiliated();
		if (!_listAll)
		{
			return source.Select((NemesisRecord r) => new ListEntry
			{
				Rec = r
			}).ToList();
		}
		List<(ListEntry e, DateTime at)> list = source.Select((NemesisRecord r) => (e: new ListEntry
		{
			Rec = r
		}, at: r.LastDealt)).ToList();
		list.AddRange(from a in _mod.PeopleForList()
			select (e: new ListEntry
			{
				Acq = a
			}, at: a.LastDealt));
		return (from t in list.OrderBy(delegate((ListEntry e, DateTime at) t)
			{
				if (t.e.Rec == null)
				{
					return t.e.Acq.Dead;
				}
				return !t.e.Rec.IsAlive || t.e.Rec.PresumedDead;
			}).ThenByDescending(((ListEntry e, DateTime at) t) => t.at).ThenBy(((ListEntry e, DateTime at) t) => t.e.Rec?.Name ?? t.e.Acq.Name, StringComparer.Ordinal)
			select t.e).ToList();
	}

	private int EntryIndexOf(NemesisRecord rec)
	{
		return Math.Max(0, Entries().FindIndex((ListEntry e) => e.Rec == rec));
	}

	private List<NemesisRecord> Unaffiliated()
	{
		return (from n in _mod.Roster
			where n.Gang == null
			orderby n.LastDealt descending, n.IsAlive && !n.PresumedDead descending, n.Name
			select n).ToList();
	}

	private bool HandleListKey(Keys key)
	{
		List<ListEntry> list = Entries();
		int num = Math.Max(0, list.Count - 1);
		if (_listKey != null)
		{
			int num2 = list.FindIndex((ListEntry e) => KeyOf(e) == _listKey);
			if (num2 >= 0)
			{
				_listIndex = num2;
			}
		}
		_listIndex = Math.Max(0, Math.Min(_listIndex, num));
		switch (key)
		{
		case Keys.F:
		case Keys.Add:
		case Keys.Subtract:
		case Keys.Oemplus:
		case Keys.OemMinus:
			_listAll = !_listAll;
			_listIndex = 0;
			_listScroll = 0;
			_listKey = null;
			break;
		case Keys.Up:
			_listIndex = Math.Max(0, _listIndex - 1);
			_listKey = ((list.Count > 0) ? KeyOf(list[_listIndex]) : null);
			break;
		case Keys.Down:
			_listIndex = Math.Min(num, _listIndex + 1);
			_listKey = ((list.Count > 0) ? KeyOf(list[_listIndex]) : null);
			break;
		case Keys.Prior:
			_listIndex = Math.Max(0, _listIndex - 16);
			_listKey = ((list.Count > 0) ? KeyOf(list[_listIndex]) : null);
			break;
		case Keys.Next:
			_listIndex = Math.Min(num, _listIndex + 16);
			_listKey = ((list.Count > 0) ? KeyOf(list[_listIndex]) : null);
			break;
		case Keys.Return:
			if (list.Count > 0 && list[_listIndex].Rec != null)
			{
				_detail = list[_listIndex].Rec;
				_action = 0;
				_confirmForget = false;
				_historyScroll = 0;
				_crimeScroll = 0;
			}
			else if (list.Count > 0 && list[_listIndex].Acq != null)
			{
				NemesisRecord nemesisRecord = _mod.PersonRecord(list[_listIndex].Acq);
				if (nemesisRecord != null)
				{
					_detail = nemesisRecord;
					_action = 0;
					_confirmForget = false;
					_historyScroll = 0;
					_crimeScroll = 0;
				}
			}
			break;
		case Keys.P:
			if (list.Count > 0 && _mod.DebugOn && list[_listIndex].Rec != null)
			{
				NemesisRecord rec = list[_listIndex].Rec;
				_mod.Promote(rec);
				_listIndex = EntryIndexOf(rec);
			}
			break;
		case Keys.R:
			if (_mod.DebugOn)
			{
				_mod.SetRandomAmbushes(!_mod.Config.RandomAmbushes);
			}
			else
			{
				_mod.OpenGuide();
			}
			break;
		case Keys.G:
			_mod.OpenGuide();
			break;
		case Keys.O:
			_mod.OpenSetupScreen();
			break;
		case Keys.N:
			_mod.CycleMessages();
			break;
		case Keys.C:
			_mod.CycleNameCards();
			break;
		case Keys.E:
		case Keys.Q:
			_faction = (_faction + ((key == Keys.E) ? 1 : (FactionCount - 1))) % FactionCount;
			_row = -1;
			_col = 0;
			Array.Clear(_offsets, 0, _offsets.Length);
			return true;
		case Keys.Back:
			Toggle();
			break;
		default:
			return false;
		}
		return true;
	}

	private void DrawUnaffiliatedList()
	{
		List<ListEntry> list = Entries();
		if (_listKey != null)
		{
			int num = list.FindIndex((ListEntry e) => KeyOf(e) == _listKey);
			if (num >= 0)
			{
				_listIndex = num;
			}
		}
		_listIndex = Math.Max(0, Math.Min(_listIndex, list.Count - 1));
		if (list.Count > 0)
		{
			_listKey = KeyOf(list[_listIndex]);
		}
		if (_listIndex < _listScroll)
		{
			_listScroll = _listIndex;
		}
		if (_listIndex >= _listScroll + 16)
		{
			_listScroll = _listIndex - 16 + 1;
		}
		DrawHeader("UNAFFILIATED", "Lone men with a grudge - no gang, no chain of command", UnaffiliatedGrey);
		DrawFactionBar(null);
		float num2 = 0.868f;
		Theme.PanelRect(0.07f, 0.26099998f, 0.86f, num2 - 0.26099998f);
		DrawFooter(num2, MainFooter(list: true));
		float num3 = 0.089999996f;
		float num4 = 0.37f;
		float num5 = 0.5f;
		float num6 = 0.64f;
		float num7 = 0.78f;
		(int r, int g, int b) labelColor = LabelColor;
		int item = labelColor.r;
		int item2 = labelColor.g;
		int item3 = labelColor.b;
		long num8 = _mod.GameMinutesNow();
		(string, float)[] array = new(string, float)[5]
		{
			("NAME", num3),
			("RANK  HATE", num4),
			("STATUS", num5),
			("WHERE IT HAPPENED", num6),
			("LAST SEEN", num7)
		};
		for (int num9 = 0; num9 < array.Length; num9++)
		{
			var (text, x) = array[num9];
			UiDraw.TextFont(text, UiDraw.TitleFont, x, 0.291f, 0.22f, item, item2, item3);
		}
		Theme.Divider(0.08f, 0.315f, 0.84000003f);
		if (list.Count == 0)
		{
			UiDraw.Text(_listAll ? "Nobody you know by name yet. Greet people to learn their names." : "Nobody holds a grudge yet. Rob, beat, shoot or insult a stranger - or let one kill you - and he may come back for you.", num3, 0.331f, 0.26f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		float num10 = 0.321f;
		float top = num10;
		int num11 = 5 * ((!UiDraw.GameFont) ? 1 : 2);
		int num12 = Math.Max(3, Math.Min(16, (UiDraw.TextSlotsLeft - 34) / num11));
		if (_listIndex >= _listScroll + num12)
		{
			_listScroll = _listIndex - num12 + 1;
		}
		float num13 = 0.81799996f - num10;
		_rowH = Math.Max(0.032f, Math.Min(0.044f, num13 / (float)Math.Max(1, num12)));
		_rowGrow = (_rowH - 0.032f) / 0.011999998f;
		int num14 = _listScroll;
		while (num14 < Math.Min(list.Count, _listScroll + num12))
		{
			NemesisRecord rec = list[num14].Rec;
			bool flag = num14 == _listIndex;
			int li = num14;
			string lk = KeyOf(list[num14]);
			UiMouse.Region(0.08f, num10, 0.84000003f, _rowH - 0.003f, delegate
			{
				bool result = _listIndex != li;
				_listIndex = li;
				_listKey = lk;
				return result;
			}, delegate
			{
				UiMouse.Send(Keys.Return);
			});
			if (rec == null)
			{
				DrawPersonRow(list[num14].Acq, num14, flag, num10, num3, num4, num5, num6, num7, num8);
			}
			else
			{
				bool flag2 = !rec.IsAlive || rec.PresumedDead;
				if (flag)
				{
					Theme.SelectBox(0.08f, num10, 0.84000003f, _rowH - 0.003f);
				}
				else if (num14 % 2 == 1)
				{
					UiDraw.Rect(0.08f, num10, 0.84000003f, _rowH - 0.003f, 255, 255, 255, 8);
				}
				int num15 = (flag2 ? 125 : (flag ? 250 : 228));
				float num16 = 0.27f + 0.04f * _rowGrow;
				float num17 = 0.25f + 0.035f * _rowGrow;
				float y = num10 + (_rowH - 0.003f) / 2f - num16 * 0.0375f;
				float y2 = num10 + (_rowH - 0.003f) / 2f - num17 * 0.0375f;
				UiDraw.Text(FitText(rec.FullName ?? "", num16, num4 - num3 - 0.008f, name: false), num3, y, num16, num15, num15, num15);
				int r;
				int g;
				int b;
				if (!flag2)
				{
					(r, g, b) = UiDraw.HateColor(rec.HateTier);
				}
				else
				{
					int num9 = num15;
					int num18 = num15;
					b = num18;
					g = num9;
					r = num15;
				}
				UiDraw.Text(string.Format("{0}/{1}   {2}", rec.Rank, 5, flag2 ? "-" : rec.HateTier.ToString()), num4, y2, num17, r, g, b);
				bool flag3 = !flag2 && _mod.Resting(rec);
				int r2;
				int g2;
				int b2;
				if (!(rec.Status == "Dead"))
				{
					if (!(rec.Status == "Presumed dead"))
					{
						if (!(rec.Status == "Paralyzed"))
						{
							if (!flag3)
							{
								r2 = 120;
								g2 = 190;
								b2 = 110;
							}
							else
							{
								r2 = 150;
								g2 = 170;
								b2 = 210;
							}
						}
						else
						{
							r2 = 150;
							g2 = 150;
							b2 = 170;
						}
					}
					else
					{
						r2 = 200;
						g2 = 150;
						b2 = 60;
					}
				}
				else
				{
					r2 = 190;
					g2 = 55;
					b2 = 45;
				}
				UiDraw.Text(FitText(flag3 ? $"Lying low - {Math.Max(1L, (rec.RestUntil - num8 + 59) / 60)}h" : (rec.Status + ((rec.Limp && !flag2) ? ", limps" : "")), num17, num6 - num5 - 0.008f, name: false), num5, y2, num17, r2, g2, b2);
				UiDraw.Text(FitText(string.IsNullOrEmpty(rec.KillLocation) ? "-" : rec.KillLocation, num17, num7 - num6 - 0.008f, name: false), num6, y2, num17, num15 - 20, num15 - 24, num15 - 32);
				UiDraw.Text(SeenText(rec.LastGameMin, rec.LastDealt, num8), num7, y2, num17, num15 - 30, num15 - 34, num15 - 40);
			}
			num14++;
			num10 += _rowH;
		}
		if (list.Count > 0)
		{
			NemesisRecord rec2 = list[_listIndex].Rec;
			float num19 = 0.82199997f;
			float num20 = (num19 + 0.868f) / 2f;
			Theme.HRule(0.08f, num19, 0.84000003f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 200);
			string text2 = ((rec2 != null) ? "GRUDGE" : "KNOWN");
			UiDraw.TextFont(text2, UiDraw.TitleFont, num3, num20 - 0.00975f, 0.26f, item, item2, item3);
			float num21 = num3 + Theme.CapsWidth(text2, 0.26f) + 0.016f;
			UiDraw.Text(FitText((rec2 != null) ? Pron.Her(rec2.GrudgeSummary, Models.Female(rec2.ModelHash)) : PersonLine(list[_listIndex].Acq), 0.28f, 0.92f - num21, name: false), num21, num20 - 0.010500001f, 0.28f, 200, 196, 186);
		}
		Theme.ScrollBar(0.919f, top, (float)num12 * _rowH, _listScroll, num12, list.Count);
	}

	private static string SeenText(long gameMin, DateTime real, long nowMin)
	{
		if (gameMin <= 0)
		{
			if (real.Year <= 1900)
			{
				return "-";
			}
			return real.ToString("MMM d");
		}
		DateTime dateTime = GameDate.FromMinutes(gameMin);
		long num = Math.Max(0L, (nowMin - gameMin) / 1440);
		return string.Format("{0} {1}  -  {2}", GameDate.Month(dateTime.Month), dateTime.Day, num switch
		{
			1L => "1 day ago", 
			0L => "today", 
			_ => $"{num} days ago", 
		});
	}

	private void DrawPersonRow(Acquaintance a, int i, bool sel, float y, float cName, float cRank, float cStatus, float cWhere, float cSeen, long nowMin)
	{
		float num = _rowH - 0.003f;
		if (sel)
		{
			Theme.SelectBox(0.08f, y, 0.84000003f, num);
		}
		else if (i % 2 == 1)
		{
			UiDraw.Rect(0.08f, y, 0.84000003f, num, 255, 255, 255, 8);
		}
		float num2 = 0.27f + 0.04f * _rowGrow;
		float num3 = 0.25f + 0.035f * _rowGrow;
		float y2 = y + num / 2f - num2 * 0.0375f;
		float y3 = y + num / 2f - num3 * 0.0375f;
		int num4 = (a.Dead ? 115 : (sel ? 235 : 190));
		UiDraw.Text(FitText(a.FullName ?? "", num2, cRank - cName - 0.008f, name: false), cName, y2, num2, num4, num4, num4);
		int r;
		int g;
		int b;
		if (!a.Dead && a.Grudge > 0)
		{
			if (a.Grudge < 40)
			{
				r = 205;
				g = 180;
				b = 110;
			}
			else
			{
				r = 215;
				g = 120;
				b = 60;
			}
		}
		else
		{
			int num5 = num4 - 40;
			int num6 = num4 - 40;
			int num7 = num4 - 40;
			b = num7;
			g = num6;
			r = num5;
		}
		UiDraw.Text((a.Grudge > 0) ? $"grudge {a.Grudge}" : "no grudge", cRank, y3, num3, r, g, b);
		string text = (a.Dead ? "Dead" : ((a.Id != null) ? (a.Met ? "Met" : "Seen") : (a.Met ? "Met - around you" : "Around you")));
		int r2;
		int g2;
		int b2;
		if (!a.Dead)
		{
			if (!a.Met)
			{
				r2 = 150;
				g2 = 150;
				b2 = 150;
			}
			else
			{
				r2 = 200;
				g2 = 196;
				b2 = 186;
			}
		}
		else
		{
			r2 = 190;
			g2 = 55;
			b2 = 45;
		}
		UiDraw.Text(text, cStatus, y3, num3, r2, g2, b2);
		UiDraw.Text(FitText(string.IsNullOrEmpty(a.Town) ? "-" : a.Town, num3, cSeen - cWhere - 0.008f, name: false), cWhere, y3, num3, num4 - 20, num4 - 24, num4 - 32);
		UiDraw.Text(SeenText(a.LastGameMin, a.LastSeen, nowMin), cSeen, y3, num3, num4 - 30, num4 - 34, num4 - 40);
	}

	private static string PersonLine(Acquaintance a)
	{
		List<string> list = new List<string>();
		list.Add(a.Met ? "you've met" : "you've only seen him");
		if (!string.IsNullOrEmpty(a.Town))
		{
			list.Add("in " + a.Town);
		}
		if (!string.IsNullOrEmpty(a.Note))
		{
			list.Add(a.Note);
		}
		if (a.Fear > 0)
		{
			list.Add(string.Format("afraid of you ({0}){1}", a.Fear, (a.Witnessed != null && a.Witnessed.Count > 0) ? (": " + a.Witnessed[a.Witnessed.Count - 1]) : ""));
		}
		if (a.Like > 0)
		{
			list.Add($"likes you ({a.Like})");
		}
		if (a.Favours > 0)
		{
			list.Add(string.Format("{0} favour{1}: {2}", a.Favours, (a.Favours == 1) ? "" : "s", string.Join(", ", a.FavourNotes ?? new List<string>())));
		}
		if (!string.IsNullOrEmpty(a.Temper))
		{
			list.Add(a.Temper.ToLowerInvariant());
		}
		if (a.TimesSeen > 1)
		{
			list.Add($"seen {a.TimesSeen} times");
		}
		if (a.Grudge > 0)
		{
			list.Add($"grudge {a.Grudge}");
		}
		if (a.Dead)
		{
			list.Add("dead");
		}
		return string.Join(", ", list);
	}

	private void PromoteAndFollow(NemesisRecord rec)
	{
		_mod.Promote(rec);
		List<Row> list = BuildRows();
		for (int i = 0; i < list.Count; i++)
		{
			int num = list[i].Items.IndexOf(rec);
			if (num >= 0)
			{
				_row = i;
				_col = num;
				KeepVisible(list[i], i);
				break;
			}
		}
	}

	private void EnsureSelection(List<Row> rows)
	{
		if (_row >= 0 && _row < rows.Count && rows[_row].Items.Count > 0)
		{
			_col = Math.Min(_col, rows[_row].Items.Count - 1);
			return;
		}
		_row = rows.FindIndex((Row r) => r.Items.Count > 0);
		_col = 0;
	}

	private void MoveRow(List<Row> rows, int dir)
	{
		if (_row < 0)
		{
			return;
		}
		Row row = rows[_row];
		int seat = SeatOf(row, _row, _col);
		float num = SeatX(row, seat) + CW / 2f;
		int num2 = _row;
		int num3 = LineOf(row, seat) + dir;
		int num4 = 0;
		while (num4 < 64)
		{
			if (num3 < 0 || num3 >= LinesOf(rows[num2]))
			{
				num2 += dir;
				if (num2 < 0 || num2 >= rows.Count)
				{
					break;
				}
				num3 = ((dir <= 0) ? (LinesOf(rows[num2]) - 1) : 0);
			}
			Row row2 = rows[num2];
			int num5 = -1;
			float num6 = float.MaxValue;
			for (int i = 0; i < row2.Items.Count; i++)
			{
				int num7 = SeatOf(row2, num2, i);
				int num8 = num7;
				if (row2.Tier == Tier.Fallen)
				{
					num8 = Math.Max(0, Math.Min(num7, PerLine - 1));
				}
				else if (LineOf(row2, num7) != num3)
				{
					continue;
				}
				float num9 = Math.Abs(SeatX(row2, num8) + CW / 2f - num) + ((num8 != num7) ? 1f : 0f);
				if (num9 < num6)
				{
					num6 = num9;
					num5 = i;
				}
			}
			if (num5 >= 0)
			{
				_row = num2;
				_col = num5;
				break;
			}
			num4++;
			num3 += dir;
		}
	}

	private void KeepVisible(Row row, int rowIndex)
	{
		if (row.Tier != Tier.Fallen)
		{
			_offsets[rowIndex] = 0;
			return;
		}
		if (_col < _offsets[rowIndex])
		{
			_offsets[rowIndex] = _col;
		}
		if (_col >= _offsets[rowIndex] + PerLine)
		{
			_offsets[rowIndex] = _col - PerLine + 1;
		}
	}

	private void ScrollToSelection(List<Row> rows)
	{
		float num = ((rows.Count == 0) ? 0f : (rows[rows.Count - 1].Y + RowHeight(rows[rows.Count - 1])));
		if (_row >= 0)
		{
			Row row = rows[_row];
			int seat = SeatOf(row, _row, _col);
			float num2 = SeatY(row, seat) - ((LineOf(row, seat) == 0) ? 0.035f : 0.005f);
			float num3 = SeatY(row, seat) + CH;
			if (num2 - _scrollY < 0.27299997f)
			{
				_scrollY = num2 - 0.27299997f;
			}
			if (num3 - _scrollY > 0.848f)
			{
				_scrollY = num3 - 0.848f;
			}
		}
		_scrollY = Math.Max(0f, Math.Min(_scrollY, Math.Max(0f, num - 0.848f)));
	}

	private bool HandleDetailKey(Keys key)
	{
		int count = DetailActions().Count;
		switch (key)
		{
		case Keys.Left:
		case Keys.Up:
			_action = (_action - 1 + count) % count;
			_confirmForget = false;
			return true;
		case Keys.Right:
		case Keys.Down:
			_action = (_action + 1) % count;
			_confirmForget = false;
			return true;
		case Keys.Return:
			ActivateDetail();
			return true;
		case Keys.Back:
			_detail = null;
			_confirmForget = false;
			return true;
		case Keys.E:
		case Keys.Q:
			StepPerson((key == Keys.E) ? 1 : (-1));
			return true;
		case Keys.M:
		case Keys.R:
			if (!_mindView && !_crimeView)
			{
				_mindView = true;
			}
			else if (_mindView)
			{
				_mindView = false;
				_crimeView = true;
			}
			else
			{
				_crimeView = false;
			}
			_crimeScroll = 0;
			return true;
		case Keys.Prior:
		case Keys.Subtract:
		case Keys.OemMinus:
			if (_crimeView)
			{
				_crimeScroll = Math.Max(0, _crimeScroll - 3);
				return true;
			}
			_historyScroll = Math.Max(0, _historyScroll - 3);
			return true;
		case Keys.Next:
		case Keys.Add:
		case Keys.Oemplus:
		{
			if (_crimeView)
			{
				_crimeScroll = Math.Min(Math.Max(0, _crimeTotal - 1), _crimeScroll + 3);
				return true;
			}
			_historyScroll = Math.Min(Math.Max(0, DetailLines(_detail, out var _, out var _).Count - 1), _historyScroll + 3);
			return true;
		}
		default:
			return false;
		}
	}

	private void StepPerson(int dir)
	{
		List<NemesisRecord> list = (_worldPage ? WorldPeople(SelectedEvent()) : ((Faction != null) ? (from n in BuildRows().SelectMany((Row r) => r.Items)
			where n != null
			select n).Distinct().ToList() : (from n in Entries().Select(delegate(ListEntry e)
			{
				NemesisRecord nemesisRecord = e.Rec;
				if (nemesisRecord == null)
				{
					if (!_listAll)
					{
						return (NemesisRecord)null;
					}
					nemesisRecord = _mod.PersonRecord(e.Acq);
				}
				return nemesisRecord;
			})
			where n != null
			select n).ToList()));
		if (list.Count < 2)
		{
			return;
		}
		int num = list.IndexOf(_detail);
		NemesisRecord next = list[(num >= 0) ? ((num + dir + list.Count) % list.Count) : 0];
		_detail = next;
		_action = 0;
		_confirmForget = false;
		_historyScroll = 0;
		_crimeScroll = 0;
		if (_worldPage)
		{
			_worldPerson = list.IndexOf(next);
			return;
		}
		if (Faction == null)
		{
			List<ListEntry> list2 = Entries();
			int num2 = list2.FindIndex((ListEntry e) => e.Rec == next || (e.Acq != null && next.Person && next.PersonAcq == e.Acq));
			if (num2 >= 0)
			{
				_listIndex = num2;
				_listKey = KeyOf(list2[num2]);
			}
			return;
		}
		List<Row> list3 = BuildRows();
		for (int num3 = 0; num3 < list3.Count; num3++)
		{
			int num4 = list3[num3].Items.IndexOf(next);
			if (num4 >= 0)
			{
				_row = num3;
				_col = num4;
				break;
			}
		}
	}

	private void ActivateDetail()
	{
		_action = Math.Min(_action, DetailActions().Count - 1);
		switch (DetailActions()[_action])
		{
		case "Promote":
			_mod.Promote(_detail);
			break;
		case "Summon Now":
			_mod.RequestSummon(_detail);
			Toggle();
			break;
		case "Track Camp":
			_mod.TrackCamp(_detail);
			Toggle();
			break;
		case "X-Rays":
			_mod.OpenXRays(_detail);
			break;
		case "Forget Him":
		case "Forget Her":
			if (!_confirmForget)
			{
				_confirmForget = true;
				break;
			}
			_mod.Forget(_detail);
			_detail = null;
			_confirmForget = false;
			break;
		default:
			_detail = null;
			break;
		}
	}

	private List<string> DetailActions()
	{
		List<string> list = new List<string>();
		if (_detail.Story || _detail.Person || _detail.LeaderPage)
		{
			list.Add("Back");
			return list;
		}
		if (_mod.DebugOn && _detail.IsAlive && _detail.Rank < 5)
		{
			list.Add("Promote");
		}
		if (_mod.DebugOn && _detail.CanHunt && _mod.ActiveRecord == null)
		{
			list.Add("Summon Now");
		}
		if (_mod.CanTrackCamp(_detail))
		{
			list.Add("Track Camp");
		}
		if (_mod.XRayShotsOn(_detail) > 0)
		{
			list.Add("X-Rays");
		}
		if (_mod.DebugOn)
		{
			list.Add(Models.Female(_detail.ModelHash) ? "Forget Her" : "Forget Him");
		}
		list.Add("Back");
		return list;
	}

	public void Draw()
	{
		if (_detail != null && !_detail.Person && !_detail.LeaderPage && !_mod.Roster.Contains(_detail) && !_mod.StoryRoster.Contains(_detail))
		{
			_detail = null;
		}
		Theme.Window(0.04f, ((_detail != null) ? 0.868f : 0.868f) + 0.034f);
		if (_detail != null)
		{
			DrawDetail();
		}
		else if (_worldPage)
		{
			DrawWorld();
		}
		else
		{
			DrawHierarchy();
		}
		if (_resetOpen)
		{
			DrawResetPanel();
		}
	}

	private void DrawHierarchy()
	{
		if (Faction == null)
		{
			DrawUnaffiliatedList();
			return;
		}
		List<Row> list = BuildRows();
		EnsureSelection(list);
		if (_row >= 0)
		{
			KeepVisible(list[_row], _row);
		}
		ScrollToSelection(list);
		Gang faction = Faction;
		List<NemesisRecord> list2 = MembersOf(faction);
		if (!faction.StoryOnly)
		{
			list2.Count((NemesisRecord n) => n.CanHunt && !n.PresumedDead);
		}
		if (_zoomIndex >= 0)
		{
			_ = $"  -  zoom {(int)Math.Round(_z * 100f)}%";
		}
		string text;
		bool flag;
		if (!faction.StoryOnly)
		{
			(text, _, flag) = _mod.LeaderCard(faction);
		}
		else
		{
			string leaderName = faction.LeaderName;
			flag = false;
			text = leaderName;
		}
		DrawHeader(FactionName(faction).ToUpperInvariant(), faction.Placeholder ? "Hierarchy  -  coming soon" : (flag ? "No leader - the seat is open" : ("Hierarchy under " + text)), AccentOf(faction));
		_mod.NotorietyOf(faction);
		DrawFactionBar(null);
		Theme.PanelRect(0.07f, 0.26099998f, 0.86f, 0.607f);
		if (faction.Placeholder)
		{
			UiDraw.Text("The " + faction.Name + " hierarchy is coming: their ranks will rise, fall and shift on their own. Nobody has joined yet.", 0.089999996f, 0.838f, 0.24f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		else if (list2.Count == 0)
		{
			UiDraw.Text("Nobody of the " + faction.Name + " has crossed you yet. Kill one - or let one kill you - and he takes his place here.", 0.089999996f, 0.838f, 0.24f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		DrawFooter(0.868f, MainFooter(list: false));
		DrawConnectors(list);
		int num = 0;
		foreach (Row item in list)
		{
			if (item.LoreName != null)
			{
				continue;
			}
			for (int num2 = 0; num2 < Shown(item); num2++)
			{
				if (((item.Tier == Tier.Fallen) ? (_offsets[list.IndexOf(item)] + num2) : num2) < item.Items.Count && Visible(Sy(SeatY(item, num2)), CH))
				{
					num++;
				}
			}
		}
		int num3 = UiDraw.TextSlotsLeft - 40;
		_leanStat = faction.OrdersNotFeelings || faction.StoryOnly || num3 < num * 6;
		_leanTitle = num3 < num * 4;
		_rawNames = num3 < num * 2;
		if ((double)UiDraw.RealMs - _textLogAt > 5000.0 || _textLogFor != faction.Id)
		{
			_textLogAt = UiDraw.RealMs;
			_textLogFor = faction.Id;
			_mod.MenuLog(string.Format("TEXT hierarchy {0}: last frame {1}/{2} slots, {3} texts cut, {4} rects ({5} before the cards) - now {6} used before the cards, {7} cards shown, lean{8}{9}{10}", faction.Id, UiDraw.LastFrameSlots, UiDraw.TextSlotLimit, UiDraw.LastFrameSkipped, UiDraw.LastFrameRects, UiDraw.RectsThisFrame, UiDraw.TextSlotsThisFrame, num, _leanStat ? " stat" : "", _leanTitle ? " title" : "", _rawNames ? " names" : ""));
		}
		for (int num4 = 0; num4 < 5; num4++)
		{
			for (int num5 = 0; num5 < list.Count; num5++)
			{
				DrawRow(list[num5], num5, num4);
			}
		}
		_ = Theme.Dim;
		float num6 = list[list.Count - 1].Y + RowHeight(list[list.Count - 1]) - 0.27299997f;
		float num7 = 0.57500005f;
		if (num6 > num7 + 0.001f)
		{
			Theme.ScrollBar(0.919f, 0.27299997f, num7, _scrollY / num6, num7 / num6);
		}
	}

	private void DrawRow(Row row, int r, int pass)
	{
		var (r2, g, b) = LabelColor;
		if (pass > 0)
		{
			if (row.LoreName != null || row.Items.Count == 0)
			{
				return;
			}
			for (int i = 0; i < Shown(row); i++)
			{
				int num = ((row.Tier == Tier.Fallen) ? (_offsets[r] + i) : i);
				float y = Sy(SeatY(row, i));
				if (num < row.Items.Count && Visible(y, CH))
				{
					DrawCard(row.Items[num], SeatX(row, i), y, r == _row && num == _col, row.Tier == Tier.Fallen, pass);
				}
			}
			return;
		}
		float num2 = Sy(row.Y) - 0.028f;
		if (num2 >= 0.26799998f && num2 + 0.02f <= 0.848f)
		{
			Theme.Section(row.Label, 0.085999995f, num2, 0.07f, (row.Tier == Tier.Fallen) ? Theme.Faint : AccentOf(Faction));
		}
		if (row.LoreName != null)
		{
			if (!Visible(Sy(row.Y), CH))
			{
				return;
			}
			float x = RowStartX(row);
			float y2 = Sy(row.Y);
			DrawLoreLeader(row, x, y2, r == _row && _col == 0 && row.Items.Count > 0);
			if (row.Items.Count > 0)
			{
				int rr = r;
				UiMouse.Region(x, y2, CW, CH, () => HoverCard(rr, 0), delegate
				{
					UiMouse.Send(Keys.Return);
				});
			}
			return;
		}
		if (!row.FixedSeats && row.Items.Count == 0)
		{
			if (Visible(Sy(row.Y), CH))
			{
				UiDraw.Text("None yet", 0.5f - Theme.MixedWidth("None yet", 0.26f, 0.0222f) / 2f, Sy(row.Y) + CH / 2f - 0.012f, 0.26f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b);
			}
			return;
		}
		int num3 = Shown(row);
		for (int num4 = 0; num4 < num3; num4++)
		{
			int num5 = ((row.Tier == Tier.Fallen) ? (_offsets[r] + num4) : num4);
			float x2 = SeatX(row, num4);
			float y3 = Sy(SeatY(row, num4));
			if (!Visible(y3, CH))
			{
				continue;
			}
			if (num5 < row.Items.Count)
			{
				DrawCard(row.Items[num5], x2, y3, r == _row && num5 == _col, row.Tier == Tier.Fallen, 0);
				int ci = num5;
				UiMouse.Region(x2, y3, CW, CH, () => HoverCard(r, ci), delegate
				{
					UiMouse.Send(Keys.Return);
				});
			}
			else
			{
				DrawVacant(x2, y3);
			}
		}
		if (row.Tier == Tier.Fallen && Visible(Sy(row.Y), CH))
		{
			if (_offsets[r] > 0)
			{
				Chevron(SeatX(row, 0) - 0.014f, Sy(row.Y) + CH / 2f, left: true, r2, g, b);
			}
			if (_offsets[r] + PerLine < row.Items.Count)
			{
				Chevron(SeatX(row, num3 - 1) + CW + 0.006f, Sy(row.Y) + CH / 2f, left: false, r2, g, b);
			}
		}
	}

	private static void Chevron(float x, float cy, bool left, int r, int g, int b)
	{
		if (!Theme.MenuArrow((!left) ? Theme.ArrowDir.Right : Theme.ArrowDir.Left, x + 0.0048f, cy, 0.02f, (r: r, g: g, b: b), 230))
		{
			float num = 0.0016f;
			float num2 = 0.026f;
			for (int i = 0; i < 6; i++)
			{
				float num3 = num2 * (float)(6 - i) / 6f;
				UiDraw.Rect(left ? (x + (float)i * num) : (x + (float)(5 - i) * num), cy - num3 / 2f, num, num3, r, g, b, 230);
			}
		}
	}

	private void DrawConnectors(List<Row> rows)
	{
		(int, int, int) lineColor = LineColor;
		int cr = lineColor.Item1;
		int cg = lineColor.Item2;
		int cb = lineColor.Item3;
		for (int i = 0; i + 1 < rows.Count; i++)
		{
			Row row = rows[i];
			Row row2 = rows[i + 1];
			if (row2.Tier == Tier.Fallen)
			{
				break;
			}
			int num = Shown(row2);
			int num2 = Math.Min(PerLine, num);
			if (num2 != 0)
			{
				float num3 = Sy(row2.Y) - 0.034f * Math.Max(_z, 0.75f);
				V(0.5f, Sy(row.Y + RowHeight(row)), num3);
				if (num3 >= 0.27299997f && num3 <= 0.848f)
				{
					UiDraw.HLine(SeatX(row2, 0) + CW / 2f, SeatX(row2, num2 - 1) + CW / 2f, num3, cr, cg, cb, 210);
				}
				for (int j = 0; j < num2; j++)
				{
					V(SeatX(row2, j) + CW / 2f, num3, Sy(row2.Y));
				}
				for (int k = PerLine; k < num; k++)
				{
					V(SeatX(row2, k) + CW / 2f, Sy(SeatY(row2, k - PerLine)) + CH, Sy(SeatY(row2, k)));
				}
			}
		}
		void V(float x, float y1, float y2)
		{
			y1 = Math.Max(y1, 0.27299997f);
			y2 = Math.Min(y2, 0.848f);
			if (y2 > y1)
			{
				UiDraw.VLine(x, y1, y2, cr, cg, cb, 210);
			}
		}
	}

	private static string FitText(string s, float scale, float width, bool name)
	{
		return Fit(s, scale, width, name, (string x) => Theme.MixedWidth(x, scale, 0.0222f));
	}

	private static string FitTitle(string s, float scale, float width, bool name = true)
	{
		return Fit(s, scale, width, name, (string x) => Theme.TitleWidth(x, scale));
	}

	private static string Fit(string s, float scale, float width, bool name, Func<string, float> w)
	{
		if (string.IsNullOrEmpty(s) || w(s) <= width)
		{
			return s ?? "";
		}
		int num = s.IndexOf(' ');
		if (name && num > 0)
		{
			string text = s[0] + ". " + s.Substring(num + 1);
			if (w(text) <= width)
			{
				return text;
			}
		}
		if (!name)
		{
			string text2 = Theme.Marquee(s, width, w);
			if (text2 != null)
			{
				return text2;
			}
		}
		string text3 = s;
		while (text3.Length > 4 && w(text3 + "...") > width)
		{
			int num2 = text3.LastIndexOf(' ');
			text3 = (((double)num2 > (double)text3.Length * 0.6) ? text3.Substring(0, num2) : text3.Substring(0, text3.Length - 1));
		}
		return text3.TrimEnd(' ', ',', '-', ';', ':') + "...";
	}

	private static List<string> WrapFit(string s, float scale, float width, int max)
	{
		List<string> list = WrapBody(s, scale, width);
		if (list.Count <= max)
		{
			return list;
		}
		List<string> list2 = list.Take(max).ToList();
		list2[max - 1] = FitText(list2[max - 1] + " " + string.Join(" ", list.Skip(max)), scale, width, name: false);
		return list2;
	}

	private static string GoneNote(NemesisRecord r)
	{
		if (r == null)
		{
			return null;
		}
		bool flag = r.IsAlive && r.PresumedDead && !NemesisRecord.ShowSecrets;
		if (r.IsAlive && !r.PresumedDead)
		{
			return null;
		}
		if (r.IsAlive && NemesisRecord.ShowSecrets)
		{
			return "presumed dead";
		}
		if (r.StoryGone != null)
		{
			return r.StoryGone.ToLowerInvariant();
		}
		if (r.Fate != null)
		{
			return r.Fate.ToLowerInvariant();
		}
		HistoryEntry historyEntry = r.History?.LastOrDefault((HistoryEntry h) => h.Text != null && h.Text.StartsWith("KILLED BY "));
		if ((flag || !r.KilledByPlayer) && (historyEntry == null || !historyEntry.Text.StartsWith("KILLED BY YOU")))
		{
			return "dead";
		}
		return "dead - killed by you";
	}

	internal static string RankText(NemesisRecord n)
	{
		Gang gang = Gangs.Of(n);
		if (gang == null)
		{
			return $"Rank {n.Rank}";
		}
		(string, int, int)[] tiers = gang.Tiers;
		for (int i = 0; i < tiers.Length; i++)
		{
			(string, int, int) tuple = tiers[i];
			if (n.Rank >= tuple.Item2)
			{
				return $"{Backstory.Singular(tuple.Item1)} ({n.Rank})";
			}
		}
		return $"Rank {n.Rank}";
	}

	private static string CardName(NemesisRecord n)
	{
		if (!(n.Id == "vdl_dutch"))
		{
			if (!(n.Id == "vdl_arthur"))
			{
				if (!(n.Id == "vdl_john"))
				{
					if (!(n.Id == "vdl_hosea"))
					{
						return n.Name;
					}
					return "Hosea";
				}
				return "John";
			}
			return "Arthur";
		}
		return "Dutch";
	}

	private void DrawCard(NemesisRecord n, float x, float y, bool selected, bool fallen, int pass)
	{
		float cW = CW;
		float cH = CH;
		float z = _z;
		float num = 0.009f * z;
		switch (pass)
		{
		case 0:
		{
			(int, int, int) card = Theme.Card;
			UiDraw.Rect(x, y, cW, cH, card.Item1, card.Item2, card.Item3, fallen ? 150 : 240);
			if (!fallen)
			{
				(int, int, int) tuple = (n.Paralyzed ? (150, 150, 175) : UiDraw.HateColor(n.HateTier));
				Theme.Hairline(x, y, cW, cH, tuple.Item1, tuple.Item2, tuple.Item3, 255, 2);
			}
			float num2 = CardIcons(n, x, y, cW, num, z, fallen, draw: false);
			(int, int, int) tuple2 = (fallen ? Theme.Dim : Theme.Title);
			UiDraw.TextFont(FitTitle(CardName(n), T(0.3f), cW - 2f * num - num2 - 0.004f), _rawNames ? null : UiDraw.TitleFont, x + num, y + 0.007f * z, T(0.3f), tuple2.Item1, tuple2.Item2, tuple2.Item3);
			return;
		}
		case 3:
			if (selected)
			{
				Theme.RedFrame(x - 0.00052083336f, y - (float)Theme.CardFrameUp * 0.0009259259f, cW + 0.0010416667f, cH, 0.95f, 5);
			}
			return;
		case 4:
			if (!fallen)
			{
				CardIcons(n, x, y, cW, num, z, fallen, draw: true);
				DrawPips(n, x, y, cW, cH, num, z);
			}
			return;
		}
		if (fallen ? (pass == 2) : (pass == 1))
		{
			if (!_leanTitle || fallen)
			{
				(int, int, int) tuple3 = (fallen ? Theme.Faint : Theme.Dim);
				UiDraw.Text(FitText(n.Title, T(0.21f), cW - 2f * num, name: false), x + num, y + 0.029f * z, T(0.21f), tuple3.Item1, tuple3.Item2, tuple3.Item3);
			}
		}
		else if (!(fallen ? (pass != 1) : (pass != 2)))
		{
			float y2 = y + cH - 0.021f * z;
			if (fallen)
			{
				bool flag = n.Status == "Presumed dead";
				UiDraw.Text(FitText((flag ? "Presumed dead" : (n.StoryGone ?? n.Fate ?? "Dead")) + " - " + RankText(n), T(0.21f), cW - 2f * num, name: false), x + num, y2, T(0.21f), flag ? 200 : 190, flag ? 150 : 60, flag ? 60 : 50);
			}
			else if (n.Paralyzed)
			{
				int r = 150;
				int g = 150;
				int b = 175;
				UiDraw.TextFont(FitTitle("PARALYZED", T(0.19f), cW - 2f * num - 0.05f * z), UiDraw.TitleFont, x + num, y2, T(0.19f), r, g, b);
			}
		}
	}

	private float CardIcons(NemesisRecord n, float x, float y, float w, float pad, float z, bool fallen, bool draw)
	{
		float num = 0f;
		if (fallen || !n.Personality.HasValue || (!n.TemperKnown && !_mod.DebugOn))
		{
			return 0f;
		}
		float num2 = 0.016f * z;
		float num3 = num2 / UiDraw.Aspect;
		float num4 = x + w - pad - num3;
		float y2 = y + 0.008f * z;
		List<(string, (int, int, int))> list = new List<(string, (int, int, int))> { (Theme.TemperIcon(n.Personality), NemesisMod.TemperColor(n.Personality.Value)) };
		if (n.TraitsRolled)
		{
			list.Add((Theme.TraitIcon(Traits.Aggr(n.Aggression)), Theme.Dim));
			string trait = ((n.Disposition == Disposition.Friendly || n.Disposition == Disposition.Surly) ? n.Disposition.ToString() : n.Sociability.ToString());
			list.Add((Theme.TraitIcon(trait), Theme.Dim));
		}
		for (int num5 = list.Count - 1; num5 >= 0; num5--)
		{
			if (list[num5].Item1 != null)
			{
				if (draw)
				{
					Theme.Icon(list[num5].Item1, num4, y2, num2, list[num5].Item2, 235);
				}
				num4 -= num3 + 0.003f;
				num += num3 + 0.003f;
			}
		}
		return num;
	}

	private void DrawPips(NemesisRecord n, float x, float y, float w, float h, float pad, float z)
	{
		float num = y + h - 0.021f * z;
		int num2 = Math.Max(0, Math.Min(5, n.Rank));
		float num3 = Math.Max(0.0115f, 0.016f * z);
		float num4 = num3 / UiDraw.Aspect;
		float num5 = Math.Max(0.0004f, 0.0012f * z);
		if (5f * num4 + 4f * num5 > w * 0.42f)
		{
			num5 = 0.0003f;
			num4 = Math.Min(num4, (w * 0.42f - 4f * num5) / 5f);
			num3 = num4 * UiDraw.Aspect;
		}
		float num6 = num + T(0.19f) * 0.0375f - 0.004f * z;
		float num7 = x + w - pad - 5f * num4 - 4f * num5;
		bool flag = UiDraw.EnsureTextureDict("generic_textures");
		for (int i = 0; i < 5; i++)
		{
			float num8 = num7 + (float)i * (num4 + num5) + num4 / 2f;
			bool flag2 = i < num2;
			if (flag)
			{
				UiDraw.Sprite("generic_textures", "star", num8, num6, num4, num3, 0f, flag2 ? Theme.Gold.r : 78, flag2 ? Theme.Gold.g : 75, flag2 ? Theme.Gold.b : 70, flag2 ? 255 : 235);
			}
			else
			{
				UiDraw.Rect(num8 - num4 * 0.3f, num6 - num3 * 0.3f, num4 * 0.6f, num3 * 0.6f, flag2 ? Theme.Gold.r : 70, flag2 ? Theme.Gold.g : 68, flag2 ? Theme.Gold.b : 64, 230);
			}
		}
	}

	private void DrawLoreLeader(Row row, float x, float y, bool selected = false)
	{
		(int, int, int) tuple = AccentOf(Faction);
		float z = _z;
		Theme.CardRect(x, y, CW, CH, 245);
		Theme.Hairline(x, y, CW, CH, tuple.Item1, tuple.Item2, tuple.Item3, 255, 2);
		Theme.Hairline(x + 0.0020833334f, y + 0.0037037036f, CW - 0.004166667f, CH - 1f / 135f, Theme.Border.r, Theme.Border.g, Theme.Border.b, 220);
		(int, int, int) tuple2 = (row.LoreVacant ? Theme.Faint : Theme.Title);
		UiDraw.TextFont(FitTitle(row.LoreName, T(0.3f), CW - 0.016f * z), UiDraw.TitleFont, x + 0.009f * z, y + 0.008f * z, T(0.3f), tuple2.Item1, tuple2.Item2, tuple2.Item3);
		if (row.LoreVacant)
		{
			UiDraw.TextFont("SEAT OPEN", UiDraw.TitleFont, x + 0.009f * z, y + 0.036f * z, T(0.2f), Theme.Warn.r, Theme.Warn.g, Theme.Warn.b);
		}
		if (selected)
		{
			Theme.RedFrame(x - 0.00052083336f, y - (float)Theme.CardFrameUp * 0.0009259259f, CW + 0.0010416667f, CH, 0.95f, 5);
		}
		float num = x + CW + 0.014f;
		UiDraw.Text(FitText(row.LoreBlurb ?? "", 0.22f, 0.908f - num, name: false), num, y + CH / 2f - 0.01f, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
	}

	private void DrawVacant(float x, float y)
	{
		Theme.Hairline(x, y, CW, CH, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 200);
		UiDraw.Text("Vacant", x + CW / 2f - Theme.MixedWidth("Vacant", T(0.24f), 0.0222f) / 2f, y + CH / 2f - 0.011f * _z, T(0.24f), Theme.Faint.r, Theme.Faint.g, Theme.Faint.b);
	}

	private void DrawDetail()
	{
		NemesisRecord n = _detail;
		Gang gang = Gangs.ById(n.Gang);
		DrawHeader(n.Name.ToUpperInvariant(), string.IsNullOrEmpty(n.Title) ? StandingOf(n) : n.Title, AccentOf(gang));
		Theme.PanelRect(0.07f, 0.13499999f, 0.86f, 0.733f);
		bool num = !_mindView && !_crimeView;
		UiMouse.WheelHandler = delegate(int d)
		{
			UiMouse.Send((d > 0) ? Keys.Prior : Keys.Next);
		};
		string text = (num ? "History" : (_crimeView ? "Scroll" : null));
		DrawFooter(0.868f, UsingPad ? ("D-pad  Navigate     A  Select     LB/RB  Prev/Next     Y  " + NextViewName + ((text != null) ? ("     LT/RT  " + text) : "") + "     B  Back") : ("Arrows  Navigate     Enter  Select     Q/E  Prev/Next     R  " + NextViewName + ((text != null) ? ("     PgUp/PgDn  " + text) : "") + "     Backspace  Back"));
		List<string> list = DetailActions();
		_action = Math.Min(_action, list.Count - 1);
		float num2 = 0.09f;
		float num3 = 0.81299996f;
		for (int num4 = 0; num4 < list.Count; num4++)
		{
			string text2 = ((list[num4].StartsWith("Forget ") && _confirmForget) ? "Confirm Forget" : list[num4]);
			bool num5 = num4 == _action;
			int ai = num4;
			UiMouse.Region(num2, num3, 0.115f, 0.036f, delegate
			{
				if (_action == ai)
				{
					return false;
				}
				_action = ai;
				_confirmForget = false;
				return true;
			}, delegate
			{
				UiMouse.Send(Keys.Return);
			});
			if (num5)
			{
				Theme.SelectBox(num2, num3, 0.115f, 0.036f);
			}
			else
			{
				UiDraw.Rect(num2, num3, 0.115f, 0.036f, Theme.Card.r, Theme.Card.g, Theme.Card.b, 230);
				Theme.Hairline(num2, num3, 0.115f, 0.036f, Theme.Border.r, Theme.Border.g, Theme.Border.b, 230);
			}
			int num6 = (num5 ? 245 : 200);
			UiDraw.TextFont(text2, UiDraw.TitleFont, num2 + 0.01f, num3 + 0.008f, 0.26f, num6, num6, num6);
			num2 += 0.127f;
		}
		(int r, int g, int b) acc = AccentOf(gang);
		float lx = 0.09f;
		float num7 = 0.52f;
		float y = 0.15499999f;
		var (lr, lg, lb) = LabelColor;
		if (_gapFor != n.Id)
		{
			_gapFor = n.Id;
			_rowGap = 0f;
			_secGap = 0f;
		}
		int lRows = 0;
		int lSecs = 0;
		float lExtra = 0f;
		float leftLimit = 0.801f;
		_histHold = ((!_mindView && !_crimeView) ? 44 : 0);
		UiDraw.FrameReserve += _histHold;
		int num8 = _mod.FollowerCount(n.Rank);
		Theme.Chip((gang != null) ? gang.Name.ToUpperInvariant() : "UNAFFILIATED", lx, y, acc, gang != null);
		y += 0.034f;
		long nowM;
		if (n.Story)
		{
			Sec(lx, "WITH THE GANG");
			Row(lx, "ROLE", string.IsNullOrEmpty(n.Title) ? StandingOf(n) : n.Title);
			PastEntry pastEntry = n.Past?.FirstOrDefault((PastEntry e) => e.Kind == "gang");
			if (pastEntry != null)
			{
				Row(lx, "SINCE", $"{pastEntry.Year}  -  {pastEntry.Text}", null, 0.21f);
			}
			if (n.BornYear > 0)
			{
				Row(lx, "AGE", $"{NemesisMod.GameYear() - n.BornYear} (born {n.BornYear})", null, 0.22f);
			}
			if (n.IsAlive && n.Id != NemesisMod.YouId)
			{
				int num9 = _mod.RegardOfRecord(n);
				(int, int, int) value = ((num9 >= 10) ? (110, 200, 120) : ((num9 <= -10) ? (215, 90, 70) : Theme.Dim));
				Row(lx, "TOWARD YOU", string.Format("{0}  ({1}{2})", NemesisMod.RegardWord(num9), (num9 > 0) ? "+" : "", num9), value, 0.23f);
				float num10 = lx + 0.095f;
				float num11 = 0.275f;
				float num12 = num10 + num11 / 2f;
				float num13 = 0.0046296297f;
				UiDraw.Rect(num10, y - 0.004f, num11, num13, 52, 50, 46, 235);
				float num14 = num11 / 2f * (float)Math.Abs(num9) / 100f;
				UiDraw.Rect((num9 >= 0) ? num12 : (num12 - num14), y - 0.004f, num14, num13, value.Item1, value.Item2, value.Item3, 255);
				UiDraw.Rect(num12 - 0.00052083336f, y - 0.004f - 0.0018518518f, 0.0010416667f, num13 + 0.0037037036f, 235, 230, 220, 230);
				y += 0.01f;
				string key = n.Id.Substring(4);
				int num15 = _mod.RegardBase(key);
				int num16 = _mod.RegardEarned(key);
				UiDraw.Text(string.Format("chapter {0}: the story has him at {1}{2},  your doing {3}{4}", _mod.RegardChapter, (num15 > 0) ? "+" : "", num15, (num16 > 0) ? "+" : "", num16), num10, y - 0.002f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				y += 0.018f;
			}
			y += 0.01f;
			Sec(lx, "TIES");
			foreach (Tie item2 in (n.Ties ?? new List<Tie>()).OrderByDescending((Tie t) => Math.Abs(t.Strength)).Take(6))
			{
				NemesisRecord nemesisRecord = _mod.FindRecord(item2.Id);
				if (nemesisRecord != null)
				{
					BadgeRow(item2.Type.ToUpperInvariant(), nemesisRecord.Name + "  -  " + item2.Note, (item2.Strength < 0) ? Theme.Warn : Theme.Dim, (item2.Strength < 0) ? Theme.Warn : Theme.Text);
				}
			}
		}
		else if (n.LeaderPage)
		{
			Sec(lx, "LEADS THE " + (gang?.Name ?? "").ToUpperInvariant().Replace("THE ", ""));
			Row(lx, "ROLE", n.Title);
			foreach (var (label, value2) in _mod.LeaderFacts(n))
			{
				Row(lx, label, value2, null, 0.21f);
			}
			y += 0.01f;
			if (gang != null && !string.IsNullOrEmpty(gang.LoreBlurb ?? gang.LeaderBlurb))
			{
				Sec(lx, "WHAT'S SAID OF HIM");
				Row(lx, "", gang.LoreBlurb ?? gang.LeaderBlurb, Theme.Dim, 0.21f);
			}
		}
		else if (n.Person && n.PersonAcq != null)
		{
			Acquaintance personAcq = n.PersonAcq;
			bool flag = Models.Female(personAcq.Model);
			Sec(lx, flag ? "WHO SHE IS TO YOU" : "WHO HE IS TO YOU");
			Row(lx, "STATUS", personAcq.Dead ? "Dead" : (personAcq.Met ? "You've met" : (flag ? "You know her by sight" : "You know him by sight")), personAcq.Dead ? new(int, int, int)?(Theme.Dim) : (((int, int, int)?)null));
			Row(lx, "TOWN", string.IsNullOrEmpty(personAcq.Town) ? "unknown" : personAcq.Town);
			string arg = ((personAcq.LastGameMin > 0) ? $"  -  last seen {GameDate.Month(GameDate.FromMinutes(personAcq.LastGameMin).Month)} {GameDate.FromMinutes(personAcq.LastGameMin).Day}" : "");
			Row(lx, "KNOWN", string.Format("seen {0} time{1}{2}", Math.Max(1, personAcq.TimesSeen), (personAcq.TimesSeen == 1) ? "" : "s", arg), null, 0.21f);
			if (!string.IsNullOrEmpty(personAcq.Note))
			{
				Row(lx, "NOTE", personAcq.Note, null, 0.21f);
			}
			if (!string.IsNullOrEmpty(personAcq.Affil) && (personAcq.Met || _mod.DebugOn))
			{
				Row(lx, "RUNS WITH", "friendly with the " + (Gangs.ById(personAcq.Affil)?.Name ?? personAcq.Affil) + (personAcq.Met ? "" : " (debug)"), Theme.Warn, 0.21f);
			}
			y += 0.01f;
			Sec(lx, "FEELINGS");
			Gauge("GRUDGE", (personAcq.Grudge >= 60) ? "BITTER" : ((personAcq.Grudge >= 30) ? "SORE" : ((personAcq.Grudge > 0) ? "MILD" : "NONE")), personAcq.Grudge, (personAcq.Grudge > 0) ? (r: 215, g: 120, b: 70) : Theme.Dim, (personAcq.Grudge > 0 && personAcq.GrudgeSettled) ? "let it go" : null, null);
			Gauge("FEAR", (personAcq.Fear >= 60) ? "TERRIFIED" : ((personAcq.Fear >= 30) ? "AFRAID" : ((personAcq.Fear > 0) ? "WARY" : "NONE")), personAcq.Fear, (r: 120, g: 150, b: 210), null, null);
			Gauge("LIKES YOU", (personAcq.Like >= 60) ? "FRIENDLY" : ((personAcq.Like >= 25) ? "WARM" : ((personAcq.Like > 0) ? "A LITTLE" : "NO")), personAcq.Like, (r: 110, g: 200, b: 120), null, null);
			if (personAcq.Favours > 0)
			{
				Row(lx, "FAVOURS", (personAcq.FavourNotes != null && personAcq.FavourNotes.Count > 0) ? string.Join(",  ", personAcq.FavourNotes.Take(3)) : $"{personAcq.Favours}", null, 0.21f);
			}
			if (personAcq.Witnessed != null && personAcq.Witnessed.Count > 0)
			{
				for (int num17 = 0; num17 < personAcq.Witnessed.Count && num17 < 3; num17++)
				{
					BadgeRow((num17 == 0) ? (personAcq.Witnessed[num17].StartsWith("saw") ? "SAW YOU" : "HEARD") : "", personAcq.Witnessed[num17], Theme.Warn);
				}
			}
			if (personAcq.Reported)
			{
				Row(lx, "", flag ? "She told the law about you." : "He told the law about you.", Theme.Warn, 0.21f);
			}
			if (personAcq.KeptQuiet)
			{
				Row(lx, "", flag ? "She's keeping quiet - you saw to that." : "He's keeping quiet - you saw to that.", Theme.Dim, 0.21f);
			}
		}
		else
		{
			Sec(lx, "STANDING");
			string text3 = RankText(n);
			int num18 = text3.IndexOf(" (");
			string word = ((num18 > 0) ? text3.Substring(0, num18) : $"Rank {n.Rank}").ToUpperInvariant();
			float num19 = WordRow("RANK", word, Theme.Title);
			if (num19 > 0f)
			{
				Stars(num19, y + 0.012f, n.Rank);
				if (n.Rank < 5)
				{
					string text4 = $"next rank {n.RankXp}/{NemesisMod.XpNeed(n.Rank)}";
					UiDraw.Text(text4, lx + 0.4f - 0.01f - Theme.MixedWidth(text4, 0.2f, 0.0222f), y + 0.004f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				}
				y += 0.03f;
			}
			if (n.Gang != null)
			{
				Row(lx, NemesisMod.IsOfficer(n) ? "CAMP" : "RIDES WITH", _mod.CampLine(n));
			}
			(int, int, int) c = ((!n.IsAlive) ? Theme.Dim : (n.PresumedDead ? Theme.Dim : (n.Paralyzed ? (130, 150, 175) : Theme.Good)));
			float num20 = WordRow("STATUS", n.Status.ToUpperInvariant(), c);
			if (num20 > 0f)
			{
				if (n.IsAlive && _mod.Resting(n))
				{
					num20 += Theme.Chip($"LYING LOW {Math.Max(1L, (n.RestUntil - _mod.GameMinutesNow() + 59) / 60)}H", num20, y + 0.002f, (r: 120, g: 150, b: 210)) + 0.006f;
				}
				if (n.Limp)
				{
					num20 += Theme.Chip("LIMP", num20, y + 0.002f, Theme.Warn) + 0.006f;
				}
				y += 0.03f;
			}
			if (num8 > 0)
			{
				Row(lx, "CREW", string.Format("rides with {0} {1}", num8, (num8 == 1) ? "man" : "men"));
			}
			y += 0.01f;
			Sec(lx, "FEELINGS");
			if (n.IsAlive)
			{
				List<Thought> item = _mod.MoodOf(n).thoughts;
				nowM = _mod.GameMinutesNow();
				if (item.Count == 0)
				{
					MoodRow("MOOD", "STEADY", Pron.Her("nothing much on his mind", Models.Female(n.ModelHash)), null, Theme.Dim);
				}
				for (int num21 = 0; num21 < item.Count && num21 < 3; num21++)
				{
					Thought thought = item[num21];
					MoodRow((num21 == 0) ? "MOOD" : "", thought.Mood.ToUpperInvariant(), thought.Text, Left(thought) + ((num21 == 2 && item.Count > 3) ? $"  +{item.Count - 3}" : ""), NemesisMod.MoodColor(thought.Mood));
				}
				y += 0.006f;
			}
			(int, int, int) c2 = UiDraw.HateColor(n.HateTier);
			Intent intent = _mod.IntentOf(n);
			bool flag2 = Gangs.Orders(n);
			Gauge("HATE", n.HateTier.ToString().ToUpperInvariant(), n.Hate, c2, Loadout.Describe(n), flag2 ? null : new int[2]
			{
				Loadout.BladeAt(n),
				Loadout.GunAt(n)
			});
			if (!flag2 && _mod.DebugOn)
			{
				Row(lx, "", $"blade at {Loadout.BladeAt(n)}, gun at {Loadout.GunAt(n)}  ({Loadout.ShiftWhy(n)})  -  hate gains x{_mod.HateGainOf(n):0.00}", Theme.Dim, 0.2f);
			}
			int num22 = _mod.EffectiveFear(n);
			Gauge("FEAR", (num22 >= 60) ? "TERRIFIED" : ((num22 >= 30) ? "AFRAID" : ((num22 > 0) ? "WARY" : "NONE")), num22, (r: 150, g: 170, b: 210), null, null);
			y += 0.004f;
			if (WordRow("INTENT", intent switch
			{
				Intent.ProvokedOnly => "ONLY IF PROVOKED", 
				Intent.WontHunt => "WAITS FOR YOU", 
				Intent.Hunts => "HUNTS YOU", 
				_ => "NONE", 
			}, intent switch
			{
				Intent.WontHunt => Theme.Warn, 
				Intent.Hunts => Theme.Blood, 
				_ => Theme.Dim, 
			}) > 0f)
			{
				y += 0.03f;
				if (intent == Intent.WontHunt || intent == Intent.ProvokedOnly)
				{
					string text5 = Traits.Describe(intent);
					text5 = char.ToUpperInvariant(text5[0]) + text5.Substring(1);
					if (y + 0.022f <= leftLimit)
					{
						UiDraw.Text(FitText(text5, 0.21f, 0.29500002f, name: false), lx + 0.095f, y - 0.004f, 0.21f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
						y += 0.022f;
					}
				}
			}
			Row(lx, "REACH", _mod.RoamText(n), Theme.Dim);
			y += 0.01f;
			if (y + 0.03f + 0.056f <= leftLimit)
			{
				Sec(lx, "RECORD");
			}
			List<(string, string, (int, int, int))> tiles = new List<(string, string, (int, int, int))>
			{
				T(n.TimesKilledPlayer, "KILLED YOU", Theme.Blood),
				T(n.TimesAmbushed, "CAME FOR YOU", Theme.Text),
				T((!n.PresumedDead) ? n.TimesCheatedDeath : 0, "CHEATED DEATH", Theme.Warn)
			};
			List<(string, string, (int, int, int))> tiles2 = new List<(string, string, (int, int, int))>
			{
				T(n.TimesKnockedOutByPlayer, "KO'D", Theme.Text),
				T(n.TimesHogtied, "TIED UP", Theme.Text),
				T(n.TimesRobbed, "ROBBED", Theme.Text),
				T(n.TimesInterrogated, "QUESTIONED", Theme.Text)
			};
			if (y + 0.056f <= leftLimit)
			{
				y = StatTiles(lx, 0.4f, y, tiles, 3);
			}
			if (y + 0.056f <= leftLimit)
			{
				y = StatTiles(lx, 0.4f, y, tiles2, 4);
			}
			if (y + 0.03f <= leftLimit)
			{
				Row(lx, "WORDS", $"antagonized {n.TimesAntagonized}   greeted {n.TimesGreeted}" + (_mod.DebugOn ? $"   fury {n.Fury}/{3}" : ""));
			}
			_leftEnd = y;
		}
		float num23 = y - lExtra;
		float num24 = leftLimit - 0.006f - num23;
		float num25 = ((num24 > 0f) ? Math.Min(0.006f, num24 * 0.35f / (float)Math.Max(1, lRows)) : 0f);
		float val = ((num24 > 0f) ? Math.Min(0.045f, (num24 - num25 * (float)lRows) / (float)Math.Max(1, lSecs - 1)) : 0f);
		_rowGap = Math.Max(0f, num25);
		_secGap = ((lSecs > 1) ? Math.Max(0f, val) : 0f);
		y = 0.15499999f;
		bool flag3 = n.TemperKnown || _mod.DebugOn;
		if (_mindView)
		{
			DrawMind(n, num7, 0.4f, 0.095f, 0.026f, ref y, 0.803f, acc, flag3);
			return;
		}
		if (_crimeView)
		{
			y = DrawCrimeSheet(n, num7, 0.4f, y, 0.798f, acc);
			return;
		}
		Sec(num7, "NATURE");
		if (flag3 && n.Personality.HasValue)
		{
			UiDraw.TextFont("TEMPER", UiDraw.TitleFont, num7 + 0.006f, y + 0.002f, 0.2f, lr, lg, lb);
			float num26 = num7 + 0.095f;
			num26 += Theme.Icon(Theme.TemperIcon(n.Personality), num26, y - 0.003f, 0.026f, NemesisMod.TemperColor(n.Personality.Value)) + 0.006f;
			UiDraw.Text(FitText(Personalities.Describe(n.Personality.Value), 0.23f, 0.26500002f, name: false), num26, y, 0.23f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			y += 0.030000001f;
		}
		else
		{
			Row(num7, "TEMPER", "Unknown", null, 0.23f);
		}
		if (flag3 && n.TraitsRolled)
		{
			UiDraw.TextFont("TRAITS", UiDraw.TitleFont, num7 + 0.006f, y + 0.002f, 0.2f, lr, lg, lb);
			float num27 = num7 + 0.095f;
			string[] array = new string[4]
			{
				n.Sociability.ToString(),
				n.Disposition.ToString(),
				Traits.Aggr(n.Aggression),
				n.Smarts.ToString()
			};
			foreach (string text6 in array)
			{
				num27 += Theme.IconChip(text6, Theme.TraitIcon(text6), num27, y - 0.003f, Theme.Dim) + 0.006f;
			}
			y += 0.030000001f;
		}
		else
		{
			Row(num7, "TRAITS", "Unknown - face him, or make him talk", Theme.Dim, 0.22f);
		}
		if (flag3)
		{
			List<string> quirks = n.Quirks;
			if (quirks != null && quirks.Count > 0)
			{
				UiDraw.TextFont("QUIRKS", UiDraw.TitleFont, num7 + 0.006f, y + 0.002f, 0.2f, lr, lg, lb);
				float num29 = num7 + 0.095f;
				foreach (string quirk in n.Quirks)
				{
					num29 += Theme.IconChip(Quirks.Label(quirk), Theme.QuirkIcon(quirk), num29, y - 0.003f, (r: 170, g: 150, b: 220)) + 0.006f;
				}
				y += 0.030000001f;
			}
		}
		List<string> skills = n.Skills;
		if (skills != null && skills.Count > 0)
		{
			Row(num7, "SKILLS", string.Join(",  ", n.Skills.Select(Quirks.SkillText)), Theme.Gold, 0.21f);
		}
		if (n.LiedToYou > 0 || n.Informant || n.OwesYou)
		{
			Row(num7, "WITH YOU", string.Join(",  ", new string[3]
			{
				(n.LiedToYou > 0) ? $"lied to you {n.LiedToYou}x" : null,
				n.Informant ? "your informer" : null,
				n.OwesYou ? "owes you his life" : null
			}.Where((string x) => x != null)), Theme.Warn, 0.21f);
		}
		if (!n.Story && !n.Person)
		{
			string text7 = _mod.KnownHabitText(n);
			if (text7 != null)
			{
				string[] array2 = text7.Split(new string[1] { ";  " }, StringSplitOptions.RemoveEmptyEntries);
				for (int num30 = 0; num30 < array2.Length; num30++)
				{
					Row(num7, (num30 == 0) ? "HABITS" : "", char.ToUpperInvariant(array2[num30][0]) + array2[num30].Substring(1), Theme.Gold, 0.21f);
				}
			}
		}
		y += 0.01f;
		Sec(num7, "BACKGROUND");
		if (flag3 && n.TraitsRolled)
		{
			if (n.BornYear > 0)
			{
				Row(num7, "BORN", string.Format("{0}, {1}  -  age {2}", n.BornYear, n.Birthplace ?? "who knows where", NemesisMod.GameYear() - n.BornYear), null, 0.22f);
			}
			string text8 = ((n.Wealth == Wealth.Rich) ? "Rich" : ((n.Wealth == Wealth.Comfortable) ? "Comfortable" : ((n.Wealth == Wealth.Working) ? "Working man" : "Poor")));
			Row(num7, "LIVING", (n.Trade ?? "Drifter") + "  -  " + text8);
			Row(num7, "FAITH", (n.Faith == "none") ? "None - godless" : (n.Faith + ", " + n.Devotion.ToString().ToLowerInvariant()));
			bool flag4 = Models.Female(n.ModelHash);
			Row(num7, "FAMILY", (n.Family == "single") ? "Single" : (((n.Family == "married") ? "Married" : (flag4 ? "Widow" : "Widower")) + ((n.Children > 0) ? string.Format(", {0} {1}", n.Children, (n.Children == 1) ? "child" : "children") : ", no children")));
			List<(NemesisRecord, string)> list2 = new List<(NemesisRecord, string)>();
			NemesisRecord nemesisRecord2 = ((n.KinId != null) ? _mod.FindRecord(n.KinId) : null);
			if (nemesisRecord2 != null)
			{
				list2.Add((nemesisRecord2, Traits.Word(n.KinRelation, flag4) + " of"));
			}
			foreach (NemesisRecord item3 in (n.Story ? _mod.StoryRoster : _mod.Roster).Where((NemesisRecord r) => r.KinId == n.Id))
			{
				list2.Add((item3, Traits.Word(Traits.Inverse(item3.KinRelation), flag4) + " of"));
			}
			for (int num31 = 0; num31 < list2.Count && num31 < 3; num31++)
			{
				string text9 = GoneNote(list2[num31].Item1);
				Row(num7, (num31 == 0) ? "KIN" : "", list2[num31].Item2 + " " + list2[num31].Item1.Name + ((text9 == null) ? "" : (" (" + text9 + ")")), (text9 == null) ? Theme.Text : Theme.Blood, 0.23f);
			}
			List<NemesisRecord> list3 = _mod.FriendsOf(n);
			if (list3.Count > 0)
			{
				Row(num7, "FRIENDS", string.Join(",  ", list3.Take(3).Select(delegate(NemesisRecord f)
				{
					string name = f.Name;
					string text11 = GoneNote(f);
					return name + ((text11 != null) ? (" (" + text11 + ")") : "") + ((f.Gang != n.Gang) ? (" [" + (Gangs.ById(f.Gang)?.Name ?? "loner") + "]") : "");
				})), null, 0.22f);
			}
			if (n.Pet != null)
			{
				Row(num7, "PET", n.Pet);
			}
		}
		else
		{
			Row(num7, "", "Unknown - learn more by facing him or making him talk", Theme.Dim, 0.22f);
		}
		y += 0.01f;
		if (!n.Story && !n.Person)
		{
			Sec(num7, "GRUDGE");
			Row(num7, "WHY", Pron.Her(n.GrudgeSummary, Models.Female(n.ModelHash)), null, 0.22f);
			List<Scar> list4 = (n.PresumedDead ? n.Scars.Take(n.Scars.Count - 1).ToList() : n.Scars);
			if (list4.Count == 0)
			{
				Row(num7, "SCARS", "None", Theme.Dim);
			}
			for (int num32 = 0; num32 < list4.Count && num32 < 3; num32++)
			{
				if (!(y < 0.783f))
				{
					break;
				}
				Row(num7, (num32 == 0) ? "SCARS" : "", list4[num32].Description + "  (" + list4[num32].Location + ")" + ((num32 == 2 && list4.Count > 3) ? $"  +{list4.Count - 3} more" : ""), null, 0.22f);
			}
			y += 0.01f;
		}
		UiDraw.FrameReserve = Math.Max(0, UiDraw.FrameReserve - _histHold);
		_histHold = 0;
		int pastCount;
		int hidden;
		List<HistoryEntry> list5 = DetailLines(n, out pastCount, out hidden);
		float val2 = ((n.Story || _leftEnd <= 0f) ? 0.803f : Math.Min(0.838f, Math.Max(_leftEnd, y + 0.15f)));
		val2 = Math.Max(val2, Math.Min(0.838f, y + 0.12f));
		_leftEnd = 0f;
		int num33 = 0;
		if (y >= val2 - 0.05f)
		{
			PageNote(n, $"history not drawn - no room (y {y:0.000}, bottom {val2:0.000})");
		}
		if (!(y < val2 - 0.05f))
		{
			return;
		}
		Theme.Section("HISTORY", num7, y, num7 + 0.4f, acc);
		y += 0.03f;
		int num34 = Math.Max(1, Math.Min((int)((val2 - y) / 0.0275f), (UiDraw.TextSlotsLeft - 30) / 4));
		int num35 = Math.Max(1, Math.Min(num34, list5.Count));
		float num36 = Math.Max(0.0275f, Math.Min(0.04f, (val2 - y) / (float)num35));
		float num37 = (num36 - 0.0275f) / 0.012499999f;
		float num38 = 0.205f + 0.03f * num37;
		float num39 = 0.235f + 0.04f * num37;
		_historyScroll = Math.Max(0, Math.Min(_historyScroll, Math.Max(0, list5.Count - 1)));
		float num40 = y;
		if (list5.Count == 0)
		{
			UiDraw.Text(Pron.Her((hidden > 0) ? "Nothing yet - he has a past, but you don't know it. Face him, or make him talk." : "Nothing yet - what you do to him is written here.", Models.Female(n.ModelHash)), num7 + 0.006f, y, 0.21f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		float num41 = num39 * 0.075f;
		float width = 0.312f;
		for (int num42 = _historyScroll; num42 < list5.Count && num42 < _historyScroll + num34; num42++)
		{
			HistoryEntry historyEntry = list5[num42];
			(int, int, int) color = historyEntry.Color;
			List<string> list6 = ((UiDraw.TextSlotsLeft > 80) ? WrapFit(historyEntry.Text ?? "", num39, width, 2) : new List<string> { FitText(historyEntry.Text ?? "", num39, width, name: false) });
			float num43 = ((list6.Count > 1) ? (num36 + num41) : num36);
			if (y + num43 > val2 + 0.002f || UiDraw.TextSlotsLeft < 24)
			{
				if (num33 == 0)
				{
					PageNote(n, "history rows not drawn - " + ((UiDraw.TextSlotsLeft < 24) ? $"text budget ({UiDraw.TextSlotsLeft} slots left)" : "no room"));
				}
				break;
			}
			num33++;
			float num44 = y + (num43 - 0.004f) / 2f;
			UiDraw.Rect(num7, y + 0.003f, 0.0015625001f, num43 - 0.008f, color.Item1, color.Item2, color.Item3, 255);
			string text10 = historyEntry.When ?? "";
			int num45 = text10.IndexOf("  ", StringComparison.Ordinal);
			UiDraw.Text((num45 > 0) ? (text10.Substring(0, num45) + " " + text10.Substring(num45 + 2)) : text10, num7 + 0.007f, num44 - num38 * 0.0375f, num38, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			float num46 = num44 - num39 * 0.0375f - (float)(list6.Count - 1) * num41 / 2f;
			for (int num47 = 0; num47 < list6.Count; num47++)
			{
				UiDraw.Text(list6[num47], num7 + 0.082f, num46 + (float)num47 * num41, num39, color.Item1, color.Item2, color.Item3);
			}
			if (num42 == list5.Count - 1 && hidden > 0 && !_mod.DebugOn && y + num43 + num36 < val2)
			{
				UiDraw.Text(Pron.Her("... and more of his past you don't know yet", Models.Female(n.ModelHash)), num7 + 0.082f, y + num43, 0.21f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b);
			}
			y += num43;
		}
		Theme.ScrollBar(num7 + 0.4f + 0.004f, num40, Math.Max(num36, y - num40), _historyScroll, num34, list5.Count);
		if (list5.Count > 0)
		{
			PageNote(n, string.Format("drew {0} of {1} history rows (scroll {2}, fit {3}, top {4:0.000}, bottom {5:0.000}, slots left {6}, texts cut last frame {7}, view {8})", num33, list5.Count, _historyScroll, num34, num40, val2, UiDraw.TextSlotsLeft, UiDraw.LastFrameSkipped, _mindView ? "mind" : (_crimeView ? "crimes" : "history")));
		}
		void BadgeRow(string badge, string s, (int r, int g, int b) bcol, (int r, int g, int b)? vcol = null)
		{
			if (!(y + 0.03f > leftLimit) && UiDraw.TextSlotsLeft >= 12)
			{
				LeftGap();
				if (!string.IsNullOrEmpty(badge))
				{
					Theme.Badge(badge, lx + 0.004f, y - 0.002f, 0.083f, bcol, filled: false);
				}
				(int, int, int) tuple3 = vcol ?? Theme.Text;
				UiDraw.Text(FitText(s, 0.245f, 0.29500002f, name: false), lx + 0.095f, y, 0.245f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
				y += 0.03f;
			}
		}
		void Gauge(string text11, string text12, int val3, (int r, int g, int b) tuple3, string note, int[] marks)
		{
			if (UiDraw.TextSlotsLeft < 14)
			{
				y += 0.046f;
			}
			else
			{
				LeftGap();
				float num48 = lx + 0.095f;
				float num49 = lx + 0.4f - 0.01f;
				float num50 = num49 - num48;
				UiDraw.TextFont(text11, UiDraw.TitleFont, lx + 0.006f, y + 0.002f, 0.2f, lr, lg, lb);
				UiDraw.TextFont(text12, UiDraw.TitleFont, num48, y + 0.001f, 0.22f, tuple3.r, tuple3.g, tuple3.b);
				string text13 = Math.Max(0, Math.Min(100, val3)).ToString();
				UiDraw.TextFont(text13, UiDraw.TitleFont, num49 - Theme.CapsWidth(text13, 0.22f), y + 0.001f, 0.22f, tuple3.r, tuple3.g, tuple3.b);
				y += 0.024f;
				float num51 = 0.0046296297f;
				float num52 = (float)Math.Max(0, Math.Min(100, val3)) / 100f;
				UiDraw.Rect(num48, y, num50, num51, 52, 50, 46, 235);
				UiDraw.Rect(num48, y, num50 * num52, num51, tuple3.r, tuple3.g, tuple3.b, 255);
				for (int i = 1; i < 4; i++)
				{
					UiDraw.Rect(num48 + num50 * (float)i / 4f, y, 0.00052083336f, num51, 12, 12, 12, 200);
				}
				if (marks != null)
				{
					foreach (int num53 in marks)
					{
						if (num53 > 0 && num53 < 100)
						{
							UiDraw.Rect(num48 + num50 * (float)num53 / 100f - 0.00052083336f, y - 0.0018518518f, 0.0010416667f, num51 + 0.0037037036f, 235, 230, 220, 230);
						}
					}
				}
				y += num51 + 0.006f;
				if (!string.IsNullOrEmpty(note))
				{
					UiDraw.Text(FitText(note, 0.2f, num50, name: false), num48, y - 0.001f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
					y += 0.02f;
				}
				y += 0.004f;
			}
		}
		string Left(Thought t)
		{
			long num48 = t.Until - nowM;
			if (num48 < 1440)
			{
				return $"{Math.Max(1L, num48 / 60)}h";
			}
			return $"{(num48 + 720) / 1440}d";
		}
		void LeftGap()
		{
			y += _rowGap;
			lRows++;
			lExtra += _rowGap;
		}
		// Unused in 0.9.5.3 (restored from IL - ILSpy drops uncalled local functions).
		void Meter(float x, int value, (int r, int g, int b) c)
		{
			float num = x + 0.095f;
			float num2 = 0.275f;
			UiDraw.Rect(num, y - 0.005f, num2, 0.0037037036f, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 230);
			UiDraw.Rect(num, y - 0.005f, num2 * (float)Math.Max(0, Math.Min(100, value)) / 100f, 0.0037037036f, c.r, c.g, c.b, 255);
			y += 0.008f;
		}
		void MoodRow(string text11, string text12, string text13, string left, (int r, int g, int b) tuple3)
		{
			if (UiDraw.TextSlotsLeft < 14)
			{
				y += 0.026f;
			}
			else
			{
				LeftGap();
				if (text11 != "")
				{
					UiDraw.TextFont(text11, UiDraw.TitleFont, lx + 0.006f, y + 0.002f, 0.2f, lr, lg, lb);
				}
				float num48 = lx + 0.095f;
				float num49 = lx + 0.4f - 0.01f;
				UiDraw.TextFont(text12, UiDraw.TitleFont, num48, y + 0.002f, 0.2f, tuple3.r, tuple3.g, tuple3.b);
				float num50 = num48 + Math.Max(0.062f, Theme.CapsWidth(text12, 0.2f) + 0.01f);
				float num51 = ((left != null) ? Theme.MixedWidth(left, 0.19f, 0.0222f) : 0f);
				if (left != null)
				{
					UiDraw.Text(left, num49 - num51, y + 0.002f, 0.19f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b);
				}
				UiDraw.Text(FitText(text13 ?? "", 0.21f, num49 - num50 - num51 - 0.01f, name: false), num50, y + 0.001f, 0.21f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
				y += 0.024f;
			}
		}
		void Row(float x, string text11, string s, (int r, int g, int b)? col = null, float scale = 0.25f)
		{
			bool flag5 = x == lx;
			float num48 = (flag5 ? 0.03f : 0.026f);
			if (!flag5 || !(y + num48 > leftLimit))
			{
				if (flag5)
				{
					LeftGap();
				}
				if (UiDraw.TextSlotsLeft < 12)
				{
					y += num48;
				}
				else
				{
					if (flag5)
					{
						scale = Math.Max(scale, 0.245f);
					}
					UiDraw.TextFont(text11, UiDraw.TitleFont, x + 0.006f, y + (flag5 ? 0.003f : 0.002f), flag5 ? 0.21f : 0.2f, lr, lg, lb);
					(int, int, int) tuple3 = col ?? Theme.Text;
					UiDraw.Text(FitText(s, scale, 0.29500002f, name: false), x + 0.095f, y, scale, tuple3.Item1, tuple3.Item2, tuple3.Item3);
					y += num48;
				}
			}
		}
		void Sec(float x, string title)
		{
			if (x == lx && lSecs++ > 0)
			{
				y += _secGap;
				lExtra += _secGap;
			}
			Theme.Section(title, x, y, x + 0.4f, acc);
			y += 0.03f;
		}
		static (string, string, (int r, int g, int b)) T(int v, string item2, (int r, int g, int b) on)
		{
			return (v.ToString(), item2, (v > 0) ? on : Theme.Dim);
		}
		float WordRow(string text11, string text12, (int r, int g, int b) tuple3)
		{
			if (y + 0.03f > leftLimit || UiDraw.TextSlotsLeft < 12)
			{
				return -1f;
			}
			LeftGap();
			UiDraw.TextFont(text11, UiDraw.TitleFont, lx + 0.006f, y + 0.003f, 0.21f, lr, lg, lb);
			UiDraw.TextFont(text12, UiDraw.TitleFont, lx + 0.095f, y + 0.001f, 0.24f, tuple3.r, tuple3.g, tuple3.b);
			return lx + 0.095f + Theme.CapsWidth(text12, 0.24f) + 0.012f;
		}
	}

	private void PageNote(NemesisRecord n, string why)
	{
		if (n != null && n.Name != null && _pageNoted.Add(n.Name + why.Split(' ')[0]))
		{
			_mod.MenuLog("PAGE " + n.Name + ": " + why);
		}
	}

	private List<HistoryEntry> DetailLines(NemesisRecord n, out int pastCount, out int hidden)
	{
		bool fem = Models.Female(n.ModelHash);
		List<HistoryEntry> list = (from h in (n.History ?? new List<HistoryEntry>()).Where((HistoryEntry h) => _mod.DebugOn || ((!n.PresumedDead) ? (!h.Cover) : (!h.Secret))).Reverse()
			select (!fem) ? h : new HistoryEntry
			{
				When = h.When,
				Text = Pron.Her(h.Text, female: true),
				Color = h.Color
			}).ToList();
		List<PastEntry> source = (from e in (n.Past ?? new List<PastEntry>()).Where(NemesisMod.PastShown)
			orderby e.Year descending, e.Month descending
			select e).ToList();
		hidden = source.Count((PastEntry e) => !Learned(e));
		List<PastEntry> list2 = source.Where((PastEntry e) => _mod.DebugOn || Learned(e)).ToList();
		pastCount = list2.Count;
		foreach (PastEntry item in list2)
		{
			list.Add(new HistoryEntry
			{
				When = item.WhenText,
				Text = Pron.Her(((!Learned(item)) ? "* " : "") + item.Text, fem),
				Color = Backstory.KindColor(item.Kind)
			});
		}
		return list;
		bool Learned(PastEntry e)
		{
			if (!e.Known)
			{
				if (e.Public)
				{
					return n.TemperKnown;
				}
				return false;
			}
			return true;
		}
	}

	private void DrawMind(NemesisRecord n, float rx, float colW, float labelW, float line, ref float y, float bottom, (int r, int g, int b) acc, bool known)
	{
		(int r, int g, int b) labelColor = LabelColor;
		int item = labelColor.r;
		int item2 = labelColor.g;
		int item3 = labelColor.b;
		float yy = y;
		bool fem = Models.Female(n.ModelHash);
		if (!n.MindRolled)
		{
			Sec("MIND");
			Text("Not read yet - give it a moment.", Theme.Dim);
			y = yy;
			return;
		}
		bool dbg = _mod.DebugOn;
		Sec("MIND");
		if (!known)
		{
			Text(Pron.Her("Unknown - face him, or make him talk.", fem), Theme.Dim);
		}
		else
		{
			Text(Mind.Describe(n), Theme.Title, 0.23f);
			for (int i = 0; i < Mind.FacetNames.Length; i += 2)
			{
				if (!(yy < bottom))
				{
					break;
				}
				for (int j = 0; j < 2 && i + j < Mind.FacetNames.Length; j++)
				{
					string text = Mind.FacetNames[i + j];
					int num = Mind.F(n, text);
					float num2 = rx + 0.006f + (float)j * (colW / 2f);
					float num3 = Theme.Icon(Theme.FacetIcon(text), num2, yy - 0.002f, 0.02f, Theme.Dim);
					UiDraw.TextFont(text.ToUpperInvariant(), UiDraw.TitleFont, num2 + num3 + 0.004f, yy + 0.001f, 0.18f, item, item2, item3);
					float num4 = num2 + 0.075f;
					float num5 = colW / 2f - 0.11f;
					UiDraw.Rect(num4, yy + 0.009f, num5, 0.0037037036f, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 230);
					(int, int, int) tuple = ((num >= 75 || num <= 25) ? Theme.Warn : Theme.Dim);
					UiDraw.Rect(num4, yy + 0.009f, num5 * (float)num / 100f, 0.0037037036f, tuple.Item1, tuple.Item2, tuple.Item3, 255);
					UiDraw.Text(num.ToString(), num4 + num5 + 0.005f, yy + 0.001f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				}
				yy += 0.021f;
			}
			yy += 0.004f;
			Text("Values:  " + string.Join("   ", Mind.ValueNames.Select((string v) => $"{v.ToLowerInvariant()} {Mind.V(n, v):+0;-0;0}")), Theme.Text, 0.21f);
		}
		yy += 0.008f;
		Sec("NEEDS AND STRESS");
		if (!known && !dbg)
		{
			Text("Unknown.", Theme.Dim);
		}
		else
		{
			(int, int, int) c = ((n.Stress >= 70) ? Theme.Blood : ((n.Stress >= 45) ? Theme.Warn : Theme.Good));
			Text(string.Format("Stress {0}{1}", n.Stress, (n.Stress >= 85) ? " - about to snap" : ((n.Stress >= 70) ? " - on edge" : ((n.Stress >= 45) ? " - strained" : " - steady"))), c);
			UiDraw.Rect(rx + 0.006f, yy - 0.004f, colW - 0.03f, 0.0037037036f, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 230);
			UiDraw.Rect(rx + 0.006f, yy - 0.004f, (colW - 0.03f) * (float)n.Stress / 100f, 0.0037037036f, c.Item1, c.Item2, c.Item3, 255);
			yy += 0.008f;
			Text("Needs:  " + string.Join("   ", Mind.NeedNames.Select((string x) => string.Format("{0} {1}{2}", x.ToLowerInvariant(), Mind.N(n, x), (Mind.N(n, x) >= 70) ? "!" : ""))), Theme.Text, 0.21f);
		}
		yy += 0.008f;
		Sec("WHAT HE WANTS");
		if (!(n.WantsKnown || dbg))
		{
			List<Goal> goals = n.Goals;
			Text((goals != null && goals.Count > 0) ? Pron.Her("He wants something - make him talk to find out what.", fem) : "Nothing you know of.", Theme.Dim);
		}
		else if (n.Goals == null || n.Goals.Count == 0)
		{
			Text("Nothing in particular - just to get by.", Theme.Dim);
		}
		else
		{
			foreach (Goal goal in n.Goals)
			{
				Text(goal.Type.ToUpperInvariant() + "  -  " + goal.Text, (goal.Target == "you") ? Theme.Blood : Theme.Gold);
			}
		}
		yy += 0.008f;
		Sec("WHO HE IS TO OTHERS");
		List<Tie> list = (n.Ties ?? new List<Tie>()).OrderByDescending((Tie t) => Math.Abs(t.Strength)).ToList();
		List<Tie> list2 = list.Where((Tie t) => dbg || known || t.Type == "kin").Take(5).ToList();
		if (list2.Count == 0)
		{
			Text((list.Count > 0) ? "Unknown." : "Nobody on your roster.", Theme.Dim);
		}
		foreach (Tie item4 in list2)
		{
			NemesisRecord nemesisRecord = _mod.FindRecord(item4.Id);
			if (nemesisRecord != null)
			{
				(int, int, int) c2 = ((item4.Strength >= 30) ? Theme.Good : ((item4.Strength <= -30) ? Theme.Blood : Theme.Text));
				Text(string.Format("{0}  {1}{2}  {3:+0;-0;0}{4}", item4.Type, nemesisRecord.Name, nemesisRecord.IsAlive ? "" : " (dead)", item4.Strength, string.IsNullOrEmpty(item4.Note) ? "" : ("  (" + item4.Note + ")")), c2);
			}
		}
		yy += 0.008f;
		Sec("WHAT HE REMEMBERS");
		List<Memory> list3 = (from m in n.Memories ?? new List<Memory>()
			where dbg || !n.PresumedDead || m.At < n.PresumedDeadAt
			where dbg || m.Cause == "you" || known
			orderby m.Intensity + (m.LongTerm ? 20 : 0) descending
			select m).ToList();
		if (list3.Count == 0)
		{
			Text("Nothing you know of.", Theme.Dim);
		}
		foreach (Memory item5 in list3)
		{
			if (yy > bottom)
			{
				break;
			}
			Text(string.Format("{0} {1}{2}:  {3}{4}", Mind.Felt(item5.Emotion), item5.Intensity, item5.LongTerm ? " - for good" : "", item5.Text, (item5.Cause == "you") ? "  (you)" : ""), Mind.EmotionColor(item5.Emotion));
		}
		y = yy;
		void Sec(string title)
		{
			Theme.Section(Pron.Her(title, fem), rx, yy, rx + colW, acc);
			yy += 0.03f;
		}
		void Text(string s, (int r, int g, int b) tuple2, float scale = 0.22f)
		{
			if (!(yy > bottom) && UiDraw.TextSlotsLeft >= 10)
			{
				UiDraw.Text(FitText(s, scale, colW - 0.012f, name: false), rx + 0.006f, yy, scale, tuple2.r, tuple2.g, tuple2.b);
				yy += 0.022f;
			}
		}
	}

	private void DrawHeader(string title, string subtitle, (int r, int g, int b) accent)
	{
		Theme.Header(title, subtitle, _worldPage ? MenuTab.World : MenuTab.Hierarchy, accent);
	}

	private WorldEvent SelectedEvent()
	{
		List<WorldEvent> list = _mod.ChronicleFor(_worldFilter);
		if (list.Count == 0)
		{
			return null;
		}
		_worldIndex = Math.Max(0, Math.Min(_worldIndex, list.Count - 1));
		return list[_worldIndex];
	}

	private List<NemesisRecord> WorldPeople(WorldEvent ev)
	{
		if (ev != null)
		{
			return (from p in ev.People
				select _mod.Roster.FirstOrDefault((NemesisRecord r) => r.Id == p.Id) into r
				where r != null
				select r).Distinct().ToList();
		}
		return new List<NemesisRecord>();
	}

	private bool HandleWorldKey(Keys key)
	{
		if (_worldFilter == WarsFilter && HandleWarsKey(key))
		{
			return true;
		}
		List<WorldEvent> list = _mod.ChronicleFor(_worldFilter);
		int val = Math.Max(0, list.Count - 1);
		List<NemesisRecord> list2 = WorldPeople(SelectedEvent());
		if (key == Keys.Up || key == Keys.Down || key == Keys.Prior || key == Keys.Next || key == Keys.Q || key == Keys.E)
		{
			_worldMouseTop = -1;
		}
		switch (key)
		{
		case Keys.Up:
			if (_worldPerson >= 0)
			{
				_worldPerson = Math.Max(0, _worldPerson - 1);
			}
			else
			{
				_worldIndex = Math.Max(0, _worldIndex - 1);
			}
			break;
		case Keys.Down:
			if (_worldPerson >= 0)
			{
				_worldPerson = Math.Min(Math.Max(0, list2.Count - 1), _worldPerson + 1);
			}
			else
			{
				_worldIndex = Math.Min(val, _worldIndex + 1);
			}
			break;
		case Keys.Prior:
			_worldIndex = Math.Max(0, _worldIndex - WorldRows);
			_worldPerson = -1;
			break;
		case Keys.Next:
			_worldIndex = Math.Min(val, _worldIndex + WorldRows);
			_worldPerson = -1;
			break;
		case Keys.Right:
			if (_worldPerson < 0 && list2.Count > 0)
			{
				_worldPerson = 0;
			}
			break;
		case Keys.Return:
			if (_worldPerson < 0)
			{
				if (list2.Count > 0)
				{
					_worldPerson = 0;
				}
			}
			else if (_worldPerson < list2.Count)
			{
				_detail = list2[_worldPerson];
				_action = 0;
				_confirmForget = false;
				_historyScroll = 0;
				_crimeScroll = 0;
			}
			break;
		case Keys.Left:
			_worldPerson = -1;
			break;
		case Keys.Back:
			if (_worldPerson >= 0)
			{
				_worldPerson = -1;
			}
			else
			{
				Toggle();
			}
			break;
		case Keys.E:
		case Keys.Q:
		{
			int num = Gangs.All.Length + 4;
			_worldFilter = FilterOf((DispOf(_worldFilter) + ((key == Keys.E) ? 1 : (num - 1))) % num);
			_worldIndex = 0;
			_worldPerson = -1;
			break;
		}
		case Keys.K:
			if (_mod.DebugOn)
			{
				_mod.CycleMarkerLook();
			}
			break;
		case Keys.M:
		case Keys.P:
		case Keys.R:
			_mod.ToggleMark(SelectedEvent());
			break;
		case Keys.Subtract:
		case Keys.OemMinus:
			if (_mod.DebugOn)
			{
				_mod.SimulateWorldEventNow();
				_worldIndex = 0;
				_worldPerson = -1;
			}
			break;
		case Keys.N:
			if (_mod.DebugOn && XPad.RecentlyUsed)
			{
				_resetOpen = true;
				_resetSel = 0;
				_resetConfirm = false;
			}
			else
			{
				_mod.CycleWorldNews();
			}
			break;
		case Keys.C:
			_mod.CycleNameCards();
			break;
		case Keys.Z:
			if (_mod.DebugOn)
			{
				_resetOpen = true;
				_resetSel = 0;
				_resetConfirm = false;
			}
			break;
		default:
			return false;
		}
		return true;
	}

	private bool HandleResetKey(Keys key)
	{
		switch (key)
		{
		case Keys.Up:
			_resetSel = (_resetSel + ResetRows.Length - 1) % ResetRows.Length;
			_resetConfirm = false;
			break;
		case Keys.Down:
			_resetSel = (_resetSel + 1) % ResetRows.Length;
			_resetConfirm = false;
			break;
		case Keys.Left:
		case Keys.Right:
		case Keys.E:
		case Keys.Q:
			if (_resetSel == 3)
			{
				int num = Gangs.All.Length + 1;
				_resetFaction = (_resetFaction + ((key == Keys.Right || key == Keys.E) ? 1 : (num - 1))) % num;
				_resetConfirm = false;
			}
			break;
		case Keys.Return:
			if (_resetSel == ResetRows.Length - 1)
			{
				_resetOpen = false;
				break;
			}
			if (!_resetConfirm)
			{
				_resetConfirm = true;
				break;
			}
			_resetConfirm = false;
			_resetOpen = false;
			if (_resetSel == 0)
			{
				_mod.DebugReset("all");
			}
			else if (_resetSel == 1)
			{
				_mod.DebugReset("xray");
			}
			else if (_resetSel == 2)
			{
				_mod.DebugReset("world");
			}
			else if (_resetSel == 4)
			{
				_mod.DebugReset("loners");
			}
			else if (_resetSel == 5)
			{
				_mod.DebugReset("safecamps");
			}
			else
			{
				_mod.DebugReset("faction", (_resetFaction < Gangs.All.Length) ? Gangs.All[_resetFaction] : null);
			}
			_worldIndex = 0;
			_worldPerson = -1;
			_row = -1;
			_listIndex = 0;
			break;
		case Keys.Back:
		case Keys.Z:
			_resetOpen = false;
			break;
		default:
			return true;
		}
		return true;
	}

	private void DrawResetPanel()
	{
		float num = 0.075f + (float)ResetRows.Length * 0.04f + 0.05f;
		float num2 = 0.3f;
		float num3 = 0.5f - num / 2f;
		UiDraw.Rect(num2, num3, 0.4f, num, 12, 11, 10, 248);
		Theme.Hairline(num2, num3, 0.4f, num, Theme.Blood.r, Theme.Blood.g, Theme.Blood.b, 255, 2);
		UiDraw.TextFont("DEBUG  -  RESET", UiDraw.TitleFont, num2 + 0.016f, num3 + 0.012f, 0.34f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.Text("Rosters are backed up first (nemeses.before-*.json).", num2 + 0.016f, num3 + 0.046f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		float num4 = num3 + 0.075f;
		for (int i = 0; i < ResetRows.Length; i++)
		{
			bool num5 = i == _resetSel;
			string text = ((i == 3) ? ("Reset one faction:   " + ResetFactionName + "   (Left / Right)") : ResetRows[i]);
			if (num5 && _resetConfirm)
			{
				text = ((i == 3) ? ("Reset " + ResetFactionName) : ResetRows[i]) + "  -  press again to confirm";
			}
			if (num5)
			{
				(int, int, int) tuple = (_resetConfirm ? Theme.Blood : Theme.Selected);
				UiDraw.Rect(num2 + 0.01f, num4, 0.38f, 0.033999998f, tuple.Item1, tuple.Item2, tuple.Item3, 245);
			}
			(int, int, int) tuple2 = (num5 ? (_resetConfirm ? Theme.Title : Theme.SelectedText) : ((i == 0) ? Theme.Blood : Theme.Text));
			UiDraw.TextFont(text, UiDraw.TitleFont, num2 + 0.02f, num4 + 0.007f, 0.25f, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			num4 += 0.04f;
		}
		UiDraw.Text(UsingPad ? "D-pad  Navigate     Left/Right  Faction     A  Select (twice)     B  Close" : "Up/Down  Navigate     Left/Right  Faction     Enter  Select (twice)     Backspace  Close", num2 + 0.016f, num3 + num - 0.034f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
	}

	private static string OutcomeLabel(WorldPart p)
	{
		if (!string.IsNullOrEmpty(p.Outcome))
		{
			return p.Outcome;
		}
		return "";
	}

	private static (int r, int g, int b) OutcomeColor(string o)
	{
		switch (o)
		{
		default:
			return Theme.Dim;
		case "MOURNS":
			return (r: 150, g: 170, b: 210);
		case "DISTINGUISHED":
		case "WON":
		case "PROMOTED":
		case "JOINED":
			return Theme.Good;
		case "SURRENDERED":
			return Theme.Good;
		case "WOUNDED":
		case "TALKED":
		case "CAMP LOST":
		case "GIVEN UP":
		case "KNOCKED OUT":
		case "ROBBED":
		case "HOGTIED":
			return Theme.Warn;
		case "KILLED":
		case "KILLED YOU":
		case "NEMESIS":
			return Theme.Blood;
		}
	}

	private static (int r, int g, int b) EffectColor(string fx)
	{
		(int, int, int) tuple = NemesisMod.MoodColor(fx.Split(' ')[0]);
		(int, int, int) tuple2 = tuple;
		(int, int, int) dim = Theme.Dim;
		if (tuple2.Item1 != dim.Item1 || tuple2.Item2 != dim.Item2 || tuple2.Item3 != dim.Item3)
		{
			return tuple;
		}
		if (fx.StartsWith("scar") || fx.StartsWith("limp"))
		{
			return Theme.Blood;
		}
		if (fx.StartsWith("fear"))
		{
			return (r: 150, g: 170, b: 210);
		}
		if (fx.StartsWith("hate"))
		{
			return Theme.Warn;
		}
		if (fx.StartsWith("laid up"))
		{
			return (r: 150, g: 170, b: 110);
		}
		if (fx.StartsWith("rank") || fx.StartsWith("married") || fx.Contains("child") || fx.StartsWith("friends with"))
		{
			return Theme.Good;
		}
		return Theme.Gold;
	}

	private void DrawWorld()
	{
		List<WorldEvent> list = _mod.ChronicleFor(_worldFilter);
		WorldEvent worldEvent = SelectedEvent();
		List<NemesisRecord> people = WorldPeople(worldEvent);
		if (_worldPerson >= people.Count)
		{
			_worldPerson = people.Count - 1;
		}
		DrawHeader("THE WORLD", "What happens while you're away", Theme.Gold);
		List<string> names = new List<string> { "All", "Wars" }.Concat(Gangs.All.Select((Gang g) => g.Name)).Concat(new string[2] { "Van der Linde", "Unaffiliated" }).ToList();
		(int, int, int) accent = ((_worldFilter == 0) ? Theme.Gold : ((_worldFilter <= Gangs.All.Length) ? Gangs.All[_worldFilter - 1].Accent : ((_worldFilter == Gangs.All.Length + 1) ? NemesisMod.VdlAccent : ((_worldFilter == WarsFilter) ? Theme.NativeRed : UnaffiliatedGrey))));
		Theme.FactionBar(0.13499999f, names, DispOf(_worldFilter), isGang: true, accent, null, GameDate.NowText(), chipAsText: true, delegate(int i)
		{
			int num53 = FilterOf(i);
			if (num53 != _worldFilter)
			{
				NemesisMod.UiSound((i > DispOf(_worldFilter)) ? "BUMPER_RIGHT" : "BUMPER_LEFT");
				_worldFilter = num53;
				_worldIndex = 0;
				_worldPerson = -1;
				_worldMouseTop = -1;
			}
		});
		float num = 0.17499998f;
		List<(string, Gang, Gang)> list2 = _mod.WarsNow();
		if (list2.Count > 0)
		{
			string text = "AT WAR";
			string text2 = "   -   ";
			int num2 = 0;
			foreach (var item in list2)
			{
				string text3 = item.Item1 + ": " + Short(item.Item2) + " vs " + Short(item.Item3);
				string text4 = text + ((num2 == 0) ? text2 : "     ") + text3;
				string text5 = ((list2.Count - num2 - 1 > 0) ? $"     +{list2.Count - num2 - 1} more" : "");
				if (Theme.MixedWidth(text4 + text5, 0.21f, 0.0222f) > 0.82f && num2 > 0)
				{
					text += $"     +{list2.Count - num2} more";
					num2 = -1;
					break;
				}
				text = text4;
				num2++;
			}
			Theme.NoteBand(num, text, Theme.Warn);
			num += 0.032f;
		}
		Theme.PanelRect(0.07f, num, 0.86f, 0.868f - num);
		if (_worldFilter == WarsFilter)
		{
			string text6 = NemesisSettings.WorldNewsShort[_mod.Config.WorldNews];
			DrawFooter(0.868f, UsingPad ? ("D-pad  Navigate     LT/RT  Events     LB/RB  Filter     LS  World News: " + text6 + "     B  Close") : ("Up/Down  Navigate     PgUp/PgDn  Events     Q/E  Filter     N  World News: " + text6 + "     Backspace  Close"));
			DrawWarsPage(num);
			return;
		}
		string text7 = ((worldEvent == null || !worldEvent.HasPos) ? null : (worldEvent.Marked ? "Unmark" : "Mark on Map"));
		string text8 = NemesisMod.NameCardModes[_mod.Config.NameCards];
		string text9 = NemesisSettings.WorldNewsShort[_mod.Config.WorldNews];
		string text10 = "     X  " + NemesisMod.YouShort + ((text7 != null) ? ("     Y  " + text7) : "") + (_mod.DebugOn ? "" : ("     LS  World News: " + text9)) + "     RS  Name Cards: " + text8 + (_mod.DebugOn ? "     LT  Event Now     LS  Reset" : "");
		string text11 = "     A  " + NemesisMod.YouShort + ((text7 != null) ? ("     P  " + text7) : "") + "     N  World News: " + text9 + "     C  Name Cards: " + text8 + (_mod.DebugOn ? "     -  Event Now     Z  Reset" : "");
		DrawFooter(0.868f, (!UsingPad) ? ((_worldPerson >= 0) ? "Up/Down  Navigate     Enter  Page     Backspace  Back" : ("Up/Down  Navigate     Enter  The People     Q/E  Filter" + text11 + "     Backspace  Close")) : ((_worldPerson >= 0) ? "D-pad  Navigate     A  Page     B  Back" : ("D-pad  Navigate     A  The People     LB/RB  Filter" + text10 + "     B  Close")));
		float num3 = 0.084f;
		float num4 = 0.455f;
		float num5 = num + 0.012f;
		float num6 = num5;
		float num7 = 0.85599995f;
		int num8 = Math.Max(4, Math.Min((int)((num7 - num6 - 0f) / 0.036f), (UiDraw.TextSlotsLeft - 70) / 4));
		float num9 = num7 - num6 - 0f;
		float num10 = Math.Max(0.032f, Math.Min(0.05f, num9 / (float)Math.Max(1, num8)));
		if (list.Count < num8)
		{
			num10 = Math.Max(0.032f, Math.Min(0.044f, num9 / (float)Math.Max(8, list.Count)));
		}
		float num11 = (num10 - 0.032f) / 0.018f;
		int num12 = Math.Min(num8, list.Count);
		float num13 = num6 + 0f + ((list.Count < num8) ? 0f : Math.Max(0f, (num9 - (float)num12 * num10) / 2f));
		float num14 = 0.19f + 0.035f * num11;
		float num15 = 0.22f + 0.05f * num11;
		if (list.Count == 0)
		{
			UiDraw.Text(_mod.Config.WorldEvents ? "Nothing yet - the world moves while you ride." : ("World events are switched off in the settings (" + KeyCombo.SettingsText + " - World - Events)."), num3 + 0.006f, num5 + 0.006f, 0.23f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			return;
		}
		int num16 = Math.Max(0, Math.Min(_worldIndex - num8 / 2, list.Count - num8));
		if (_worldMouseTop >= 0 && _worldIndex >= _worldMouseTop && _worldIndex < _worldMouseTop + num8)
		{
			num16 = Math.Max(0, Math.Min(_worldMouseTop, Math.Max(0, list.Count - num8)));
		}
		else
		{
			_worldMouseTop = -1;
		}
		Theme.ScrollBar(num3 + num4 + 0.004f, num13, (float)num12 * num10, num16, num8, list.Count);
		for (int num17 = num16; num17 < list.Count && num17 < num16 + num8; num17++)
		{
			WorldEvent worldEvent2 = list[num17];
			float num18 = num13 + (float)(num17 - num16) * num10;
			bool flag = num17 == _worldIndex;
			(int, int, int) tuple = NemesisMod.KindColor(worldEvent2.Kind);
			int wi = num17;
			int wtop = num16;
			UiMouse.Region(num3 - 0.0031250003f, num18, num4 + 0.0031250003f, num10 - 0.004f, delegate
			{
				if (_worldIndex == wi)
				{
					return false;
				}
				_worldIndex = wi;
				_worldPerson = -1;
				_worldMouseTop = wtop;
				return true;
			}, delegate
			{
				UiMouse.Send(Keys.Return);
			});
			if (flag && _worldPerson < 0)
			{
				Theme.SelectFill(num3 - 0.0031250003f, num18, num4 + 0.0031250003f, num10 - 0.004f);
			}
			else if (flag)
			{
				UiDraw.Rect(num3, num18, num4, num10 - 0.004f, Theme.CardHi.r, Theme.CardHi.g, Theme.CardHi.b, 240);
			}
			UiDraw.Rect(num3, num18, 0.0015625001f, num10 - 0.004f, tuple.Item1, tuple.Item2, tuple.Item3, 255);
			if (flag && _worldPerson < 0)
			{
				Theme.RedFrame(num3 - 0.0031250003f, num18, num4 + 0.0031250003f, num10 - 0.004f);
			}
			float num19 = num18 + (num10 - 0.004f) / 2f;
			(int, int, int) tuple2 = ((flag && _worldPerson < 0) ? Theme.SelectedText : Theme.Text);
			(int, int, int) tuple3 = ((flag && _worldPerson < 0) ? Theme.SelectedText : Theme.Dim);
			UiDraw.Text((worldEvent2.When ?? "").Replace("  ", " "), num3 + 0.01f, num19 - num14 * 0.0375f, num14, tuple3.Item1, tuple3.Item2, tuple3.Item3);
			int num20 = worldEvent2.People.Count((WorldPart worldPart) => worldPart.Outcome == "KILLED");
			string text12 = worldEvent2.Title + ((num20 > 0) ? $"  -  {num20} dead" : "");
			float num21 = num3 + 0.092f + 0.016f * num11;
			float num22 = num3 + num4 - 0.008f - num21;
			float num23 = num15;
			while (num23 > num15 * 0.85f && Theme.MixedWidth(text12, num23, 0.0222f) > num22)
			{
				num23 -= 0.005f;
			}
			float num24 = Math.Min(num15 * 0.85f, (num10 - 0.006f) / 0.15f);
			if (Theme.MixedWidth(text12, num23, 0.0222f) <= num22)
			{
				UiDraw.Text(text12, num21, num19 - num23 * 0.0375f, num23, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			}
			else if (num24 >= 0.165f)
			{
				List<string> list3 = WrapFit(text12, num24, num22, 2);
				float num25 = num24 * 0.075f;
				float num26 = num19 - num24 * 0.0375f - (float)(list3.Count - 1) * num25 / 2f;
				for (int num27 = 0; num27 < list3.Count; num27++)
				{
					UiDraw.Text(list3[num27], num21, num26 + (float)num27 * num25, num24, tuple2.Item1, tuple2.Item2, tuple2.Item3);
				}
			}
			else
			{
				UiDraw.Text(FitText(text12, num23, num22, name: false), num21, num19 - num23 * 0.0375f, num23, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			}
		}
		if (worldEvent == null)
		{
			return;
		}
		float num28 = 0.56f;
		float rw = 0.356f;
		float ry = num5;
		(int, int, int) tuple4 = NemesisMod.KindColor(worldEvent.Kind);
		float mx = num28 + rw / 2f;
		long num29 = _mod.GameMinutesNow();
		int num30 = Math.Min(2, Wrap(worldEvent.Title.ToUpperInvariant(), 0.3f, rw * 0.92f).Count);
		int num31 = ((worldEvent.UnnamedDead.Count > 0) ? 1 : 0) + (worldEvent.Marked ? 1 : 0) + ((worldEvent.AftermathUntil > 0) ? 1 : 0);
		List<List<string>> source = (from l in worldEvent.Lines.Take(4)
			select WrapFit(l, 0.24f, rw * 0.94f, 2)).ToList();
		int num32 = Math.Min(5, source.Sum((List<string> l) => l.Count));
		float num33 = 0.066f + (float)num30 * 0.031f + 0.018f + (float)num32 * 0.026f + (float)num31 * 0.023f + 0.01f;
		ry = num13 + 0.01f;
		float num34 = 0.85599995f - (ry - 0.01f);
		(WorldEvent, int, bool, long) storyKey = (worldEvent, (worldEvent?.People?.Count).GetValueOrDefault(), _mod.DebugOn, worldEvent?.AftermathUntil ?? 0);
		if (_storyFor != worldEvent || !storyKey.Equals(_storyKey))
		{
			_storyFor = worldEvent;
			_storyKey = storyKey;
			_story = WrapBody(string.Join(" ", EventStory.Build(worldEvent, _mod.FindRecord, num29, _mod.DebugOn)), 0.225f, rw - 0.04f);
		}
		float num35 = ((worldEvent.People.Count > 0) ? (0.044f + (float)Math.Min(2, worldEvent.People.Count) * 0.062f) : 0f);
		int val = Math.Max(0, Math.Min(_story.Count, (int)((num34 - num33 - 0.03f - num35 - 0.02f) / 0.0235f)));
		val = Math.Min(val, Math.Max(0, (UiDraw.TextSlotsLeft - 60) / 2));
		if (val > 0)
		{
			num33 += 0.012f + (float)val * 0.0235f + 0.006f;
		}
		float num36 = 0f;
		if (worldEvent.People.Count > 0)
		{
			num36 = 0.044f;
			foreach (WorldPart person in worldEvent.People)
			{
				float num37 = 0.052f + (((person.Effects?.Count ?? 0) > 0) ? 0.024f : 0f) + 0.008f;
				if (num33 + num36 + num37 > num34 - 0.03f)
				{
					num36 += 0.024f;
					break;
				}
				num36 += num37;
			}
		}
		float y = ry - 0.01f;
		UiDraw.Rect(num28, y, rw, num33 + 0.01f, 22, 21, 19, 200);
		Theme.Hairline(num28, y, rw, num33 + 0.01f, Theme.Border.r, Theme.Border.g, Theme.Border.b, 140);
		bool flag2 = !string.IsNullOrEmpty(worldEvent.Place) && (worldEvent.Title ?? "").IndexOf(worldEvent.Place, StringComparison.OrdinalIgnoreCase) >= 0;
		string s = (worldEvent.When ?? "").Replace("  ", " ") + ((string.IsNullOrEmpty(worldEvent.Place) || flag2) ? "" : ("  -  " + worldEvent.Place));
		string text13 = NemesisMod.KindLabel(worldEvent.Kind);
		float num38 = Theme.CapsWidth(text13, 0.21f) + 0.014f;
		Theme.Chip(text13, mx - num38 / 2f, ry, tuple4, filled: true);
		ry += 0.028f;
		Centre(s, 0.21f, Theme.Dim);
		ry += 0.038f;
		foreach (string item2 in Wrap(worldEvent.Title.ToUpperInvariant(), 0.3f, rw * 0.92f).Take(2))
		{
			Centre(item2, 0.3f, Theme.Title, title: true);
			ry += 0.031f;
		}
		ry += 0.004f;
		Theme.CenterRule(num28, ry, rw, 0.06f, 130);
		ry += 0.014f;
		List<string> list4 = source.SelectMany((List<string> l) => l).ToList();
		for (int num39 = 0; num39 < num32; num39++)
		{
			bool flag3 = num39 == num32 - 1 && list4.Count > num32;
			Centre(flag3 ? FitText(list4[num39] + " ...", 0.24f, rw * 0.94f, name: false) : list4[num39], 0.24f, Theme.Text);
			ry += 0.026f;
		}
		if (val > 0)
		{
			ry += 0.004f;
			UiDraw.Rect(num28 + 0.02f, ry, rw - 0.04f, 0.0009259259f, Theme.Border.r, Theme.Border.g, Theme.Border.b, 160);
			ry += 0.008f;
			for (int num40 = 0; num40 < val; num40++)
			{
				string text14 = _story[num40];
				if (num40 == val - 1 && val < _story.Count)
				{
					text14 = text14.TrimEnd('.', ',', ' ') + " ...";
				}
				UiDraw.Text(text14, num28 + 0.02f, ry, 0.225f, 214, 208, 194);
				ry += 0.0235f;
			}
			ry += 0.006f;
		}
		if (worldEvent.UnnamedDead.Count > 0)
		{
			string text15 = string.Join(",  ", worldEvent.UnnamedDead.Select((KeyValuePair<string, int> kv) => $"{Gangs.ById(kv.Key)?.Name ?? kv.Key} {kv.Value}"));
			Centre(FitText("Unnamed dead:  " + text15, 0.21f, rw * 0.94f, name: false), 0.21f, Theme.Dim);
			ry += 0.023f;
		}
		long num41 = num29;
		if (worldEvent.Marked)
		{
			Centre("Marked on your map.", 0.21f, Theme.Gold);
			ry += 0.023f;
		}
		if (worldEvent.AftermathUntil > 0)
		{
			string s2 = (worldEvent.Visited ? "You've seen the aftermath." : ((worldEvent.AftermathUntil > num41) ? "The bodies still lie there - mark it to find it (a ? shows when you're near)." : "The bodies are gone by now."));
			(int, int, int) c = ((!worldEvent.Visited && worldEvent.AftermathUntil > num41) ? Theme.Warn : Theme.Faint);
			Centre(FitText(s2, 0.21f, rw * 0.94f, name: false), 0.21f, c);
			ry += 0.023f;
		}
		ry += 0.024f;
		if (worldEvent.People.Count == 0)
		{
			return;
		}
		Theme.Section("PEOPLE", num28, ry, num28 + rw, tuple4);
		ry += 0.03f;
		float num42 = 0.83399993f;
		if (_peopleFor != worldEvent)
		{
			_peopleFor = worldEvent;
			_peopleStart = 0;
		}
		int num43 = ((_worldPerson >= 0 && _worldPerson < people.Count) ? worldEvent.People.FindIndex((WorldPart wp) => wp.Id == people[_worldPerson].Id) : (-1));
		if (num43 >= 0)
		{
			if (num43 < _peopleStart)
			{
				_peopleStart = num43;
			}
			while (_peopleStart < num43 && ry + 0.022f + worldEvent.People.Skip(_peopleStart).Take(num43 - _peopleStart + 1).Sum((Func<WorldPart, float>)CardH) > num42)
			{
				_peopleStart++;
			}
		}
		else
		{
			_peopleStart = 0;
		}
		int num44 = 0;
		float num45 = ry;
		foreach (WorldPart p in worldEvent.People.Skip(_peopleStart))
		{
			List<string> list5 = p.Effects ?? new List<string>();
			float num46 = 0.052f + ((list5.Count > 0) ? 0.024f : 0f);
			if (ry + num46 > num42 || UiDraw.TextSlotsLeft < 18)
			{
				break;
			}
			num44++;
			NemesisRecord nemesisRecord = _mod.Roster.FirstOrDefault((NemesisRecord r) => r.Id == p.Id);
			bool selected = nemesisRecord != null && _worldPerson >= 0 && people.IndexOf(nemesisRecord) == _worldPerson;
			string text16 = OutcomeLabel(p);
			(int, int, int) color = ((text16 != "") ? OutcomeColor(text16) : Theme.Faint);
			Theme.CardRect(num28, ry, rw, num46, 235, selected);
			if (nemesisRecord != null)
			{
				int pi = people.IndexOf(nemesisRecord);
				UiMouse.Region(num28, ry, rw, num46, delegate
				{
					if (_worldPerson == pi)
					{
						return false;
					}
					_worldPerson = pi;
					return true;
				}, delegate
				{
					UiMouse.Send(Keys.Return);
				});
			}
			UiDraw.Rect(num28 + 0.0010416667f, ry + 0.0018518518f, 0.0015625001f, num46 - 0.0037037036f, color.Item1, color.Item2, color.Item3, 255);
			float num47 = num28 + 0.012f;
			float width = rw - 0.024f - ((text16 != "") ? 0.11f : 0f);
			(int, int, int) tuple5 = ((nemesisRecord != null) ? Theme.Title : Theme.Dim);
			UiDraw.TextFont(FitTitle(p.Name + ((nemesisRecord != null && !string.IsNullOrEmpty(nemesisRecord.Title)) ? ("  -  " + nemesisRecord.Title) : ""), 0.24f, width), UiDraw.TitleFont, num47, ry + 0.006f, 0.24f, tuple5.Item1, tuple5.Item2, tuple5.Item3);
			string text17 = p.Note + ((_mod.DebugOn && p.SecretlyLived) ? "  (debug: secretly lived)" : "");
			if (nemesisRecord != null && nemesisRecord.IsAlive && !nemesisRecord.PresumedDead)
			{
				var (text18, list6) = _mod.MoodOf(nemesisRecord);
				if (list6.Count > 0)
				{
					text17 = text17 + ((text17 != "") ? "   -   " : "") + text18 + ": " + list6[0].Text;
				}
			}
			if (text17 != "")
			{
				UiDraw.Text(FitText(text17, 0.2f, width, name: false), num47, ry + 0.029f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			}
			if (text16 != "")
			{
				Theme.Badge(text16, num28 + rw - 0.01f - 0.1f, ry + (num46 - 0.024f) / 2f, 0.1f, color, text16 == "KILLED" || text16 == "KILLED YOU");
			}
			if (list5.Count > 0)
			{
				float num48 = num47;
				float num49 = ry + 0.052f - 0.002f;
				float num50 = num28 + rw - 0.012f;
				for (int num51 = 0; num51 < list5.Count; num51++)
				{
					string text19 = list5[num51];
					float num52 = Theme.MixedWidth(text19, 0.17f, 0.0222f) + 0.014f;
					if (num48 + num52 > num50)
					{
						if (num51 < list5.Count)
						{
							UiDraw.Text($"+{list5.Count - num51}", num48, num49 + 0.002f, 0.17f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
						}
						break;
					}
					num48 += Theme.Tag(text19, num48, num49, EffectColor(text19)) + 0.005f;
				}
			}
			ry += num46 + 0.008f;
		}
		Theme.ScrollBar(num28 + rw + 0.004f, num45, Math.Max(0.02f, ry - num45 - 0.008f), _peopleStart, num44, worldEvent.People.Count);
		static float CardH(WorldPart wp)
		{
			return 0.052f + (((wp.Effects?.Count ?? 0) > 0) ? 0.024f : 0f) + 0.008f;
		}
		void Centre(string text20, float scale, (int r, int g, int b) tuple7, bool title = false)
		{
			float num53 = (title ? Theme.CapsWidth(text20, scale) : Theme.MixedWidth(text20, scale, 0.0222f));
			if (title)
			{
				UiDraw.TextFont(text20, UiDraw.TitleFont, mx - num53 / 2f, ry, scale, tuple7.r, tuple7.g, tuple7.b);
			}
			else
			{
				UiDraw.Text(text20, mx - num53 / 2f, ry, scale, tuple7.r, tuple7.g, tuple7.b);
			}
		}
		static string Short(Gang g)
		{
			if (!g.Name.StartsWith("The "))
			{
				return g.Name;
			}
			return g.Name.Substring(4);
		}
	}

	private static List<string> WrapBody(string s, float scale, float width)
	{
		List<string> list = new List<string>();
		string text = "";
		string[] array = (s ?? "").Split(' ');
		foreach (string text2 in array)
		{
			if (text2.Length != 0)
			{
				string text3 = ((text.Length == 0) ? text2 : (text + " " + text2));
				if (text.Length > 0 && Theme.MixedWidth(text3, scale, 0.0222f) > width)
				{
					list.Add(text);
					text = text2;
				}
				else
				{
					text = text3;
				}
			}
		}
		if (text.Length > 0)
		{
			list.Add(text);
		}
		return list;
	}

	private static List<string> Wrap(string s, float scale, float width)
	{
		int num = Math.Max(8, (int)(width / (scale * 0.0175f)));
		List<string> list = new List<string>();
		string text = "";
		string[] array = (s ?? "").Split(' ');
		foreach (string text2 in array)
		{
			if (text.Length + text2.Length + 1 > num && text.Length > 0)
			{
				list.Add(text);
				text = text2;
			}
			else
			{
				text = ((text.Length == 0) ? text2 : (text + " " + text2));
			}
		}
		if (text.Length > 0)
		{
			list.Add(text);
		}
		return list;
	}

	private string MainFooter(bool list)
	{
		string text = (_mod.Config.RandomAmbushes ? "On" : "Off");
		_ = NemesisSettings.MessageLevelShort[_mod.Config.MessageLevel];
		_ = NemesisMod.NameCardModes[_mod.Config.NameCards];
		string text2 = "     X  " + NemesisMod.YouShort + (_mod.DebugOn ? ("     Y  Ambushes: " + text) : "");
		string text3 = "     A  " + NemesisMod.YouShort + (_mod.DebugOn ? ("     P  Promote     R  Ambushes: " + text) : "");
		string text4 = (_listAll ? "Everyone" : "Nemeses");
		string text5 = (_mod.DebugOn ? "" : "     Y  Guide");
		if (UsingPad)
		{
			if (!list)
			{
				return "D-pad  Navigate     A  Details     Start  Settings" + text2 + text5 + "     LT/RT  Zoom     B  Close";
			}
			return "D-pad  Navigate     A  Details     Start  Settings     LT/RT  Show: " + text4 + text2 + text5 + "     B  Close";
		}
		if (!list)
		{
			return "Arrows  Navigate     Enter  Details     O  Settings" + text3 + "     G  Guide     +/-  Zoom     Z  Fit     Backspace  Close";
		}
		return "Up/Down  Navigate     Enter  Details     O  Settings     F  Show: " + text4 + text3 + "     G  Guide     Backspace  Close";
	}

	private static void DrawFooter(float y, string help)
	{
		Theme.Footer(y, help);
	}

	private (string text, (int r, int g, int b) c) WarVerdict(WarRecord w, bool live, Gang a, Gang b, District d)
	{
		if (live)
		{
			int num = ((d != null) ? _mod.HoldOf(d, w.A) : w.HoldA0) - w.HoldA0;
			int num2 = ((d != null) ? _mod.HoldOf(d, w.B) : w.HoldB0) - w.HoldB0;
			if (num - num2 >= 3)
			{
				return (text: "ONGOING  -  " + Up(a) + " AHEAD", c: a.Accent);
			}
			if (num2 - num >= 3)
			{
				return (text: "ONGOING  -  " + Up(b) + " AHEAD", c: b.Accent);
			}
			return (text: "ONGOING  -  NEITHER SIDE AHEAD", c: Theme.Dim);
		}
		switch (w.Result)
		{
		case "met":
			return (text: "WON BY " + Up(a), c: a.Accent);
		case "defended":
			return (text: "WON BY " + Up(b) + "  -  THEY HELD", c: b.Accent);
		case "gave up":
			return (text: "WON BY " + Up(b) + "  -  THE ATTACKERS GAVE UP", c: b.Accent);
		case "broken":
			return (text: "WON BY " + Up(a) + "  -  " + Up(b) + " RAN OUT OF MEN", c: a.Accent);
		case "merged":
			return (text: "NO WINNER  -  JOINED THEIR OTHER WAR", c: Theme.Dim);
		case "spent":
			return (text: "WON BY " + Up(b) + "  -  THE ATTACKERS RAN OUT OF MEN", c: b.Accent);
		case "peace":
			return (text: "NO WINNER  -  PEACE WAS MADE", c: Theme.Dim);
		case "moot":
			return (text: "NO WINNER  -  NOTHING LEFT TO FIGHT FOR", c: Theme.Dim);
		default:
			if (_mod.IsDestroyed(b) && !_mod.IsDestroyed(a))
			{
				return (text: ("WON BY " + Up(a) + "  -  THE " + b.Name.ToUpperInvariant() + " ARE FINISHED").Replace("THE THE", "THE"), c: a.Accent);
			}
			if (_mod.IsDestroyed(a) && !_mod.IsDestroyed(b))
			{
				return (text: ("WON BY " + Up(b) + "  -  THE " + a.Name.ToUpperInvariant() + " ARE FINISHED").Replace("THE THE", "THE"), c: b.Accent);
			}
			if (w.EndHoldA >= 0 && w.EndHoldB >= 0)
			{
				int num = w.EndHoldA - w.HoldA0;
				int num2 = w.EndHoldB - w.HoldB0;
				if (num - num2 >= 3)
				{
					return (text: Up(a) + " CAME OUT AHEAD", c: a.Accent);
				}
				if (num2 - num >= 3)
				{
					return (text: Up(b) + " CAME OUT AHEAD", c: b.Accent);
				}
			}
			return (text: "NO CLEAR WINNER", c: Theme.Dim);
		}
		static string Up(Gang g)
		{
			return UiDraw.TheFix("the " + g.Name).ToUpperInvariant();
		}
	}

	private int DispOf(int f)
	{
		if (f != WarsFilter)
		{
			if (f != 0)
			{
				return f + 1;
			}
			return 0;
		}
		return 1;
	}

	private int FilterOf(int d)
	{
		return d switch
		{
			1 => WarsFilter, 
			0 => 0, 
			_ => d - 1, 
		};
	}

	private bool HandleWarsKey(Keys key)
	{
		int num = Math.Max(1, _mod.WarList().Count);
		switch (key)
		{
		case Keys.Up:
			_warSel = Math.Max(0, _warSel - 1);
			return true;
		case Keys.Down:
			_warSel = Math.Min(num - 1, _warSel + 1);
			return true;
		case Keys.Prior:
		case Keys.Subtract:
		case Keys.OemMinus:
			_warEvScroll = Math.Max(0, _warEvScroll - 3);
			return true;
		case Keys.Next:
		case Keys.Add:
		case Keys.Oemplus:
			_warEvScroll = Math.Min(Math.Max(0, _warEvCount - 1), _warEvScroll + 3);
			return true;
		case Keys.Return:
		case Keys.Left:
		case Keys.Right:
		case Keys.M:
		case Keys.P:
			return true;
		default:
			return false;
		}
	}

	private void DrawWarsPage(float bodyTop)
	{
		List<(WarRecord, bool)> list = _mod.WarList();
		float num = 0.084f;
		float num2 = 0.3f;
		float num3 = bodyTop + 0.014f;
		float bottom = 0.854f;
		if (list.Count == 0)
		{
			UiDraw.Text("No wars - not yet. When two outfits that hate each other share ground, one may come for the other.", num + 0.006f, num3 + 0.006f, 0.23f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			return;
		}
		_warSel = Math.Max(0, Math.Min(_warSel, list.Count - 1));
		if (_warEvFor != _warSel)
		{
			_warEvFor = _warSel;
			_warEvScroll = 0;
		}
		UiMouse.WheelHandler = delegate(int d)
		{
			UiMouse.Send((d > 0) ? Keys.Prior : Keys.Next);
		};
		int num4 = Math.Max(4, Math.Min((int)((bottom - num3) / 0.052f), (UiDraw.TextSlotsLeft - 150) / 4));
		int num5 = Math.Max(0, Math.Min(_warSel - num4 / 2, list.Count - num4));
		Theme.ScrollBar(num + num2 + 0.004f, num3, (float)Math.Min(num4, list.Count) * 0.052f, num5, num4, list.Count);
		for (int num6 = num5; num6 < list.Count && num6 < num5 + num4; num6++)
		{
			(WarRecord, bool) tuple = list[num6];
			WarRecord item = tuple.Item1;
			bool item2 = tuple.Item2;
			Gang g = Gangs.ById(item.A);
			Gang g2 = Gangs.ById(item.B);
			District district = _mod.DistrictByKey(item.District);
			float num7 = num3 + (float)(num6 - num5) * 0.052f;
			bool num8 = num6 == _warSel;
			int wi = num6;
			UiMouse.Region(num - 0.0031250003f, num7, num2 + 0.0031250003f, 0.048f, delegate
			{
				if (_warSel == wi)
				{
					return false;
				}
				_warSel = wi;
				return true;
			}, delegate
			{
			});
			(int, int, int) tuple2 = (item2 ? NemesisMod.HeatColor(item.Heat) : Theme.Faint);
			if (num8)
			{
				Theme.SelectFill(num - 0.0031250003f, num7, num2 + 0.0031250003f, 0.048f);
			}
			UiDraw.Rect(num, num7, 0.0015625001f, 0.048f, tuple2.Item1, tuple2.Item2, tuple2.Item3, 255);
			if (num8)
			{
				Theme.RedFrame(num - 0.0031250003f, num7, num2 + 0.0031250003f, 0.048f);
			}
			(int, int, int) tuple3 = (num8 ? Theme.SelectedText : (item2 ? Theme.Text : Theme.Dim));
			UiDraw.TextFont(FitTitle(_mod.WarName(item).ToUpperInvariant(), 0.24f, num2 - 0.024f), UiDraw.TitleFont, num + 0.012f, num7 + 0.005f, 0.24f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
			UiDraw.Text(FitText((district?.Name ?? item.District) + "  -  " + Short(g) + " vs " + Short(g2) + "  -  " + (item2 ? $"Day {_mod.WarDay(item)}" : "Over"), 0.2f, num2 - 0.024f, name: false), num + 0.012f, num7 + 0.028f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		(WarRecord, bool) tuple4 = list[_warSel];
		WarRecord item3 = tuple4.Item1;
		bool item4 = tuple4.Item2;
		Gang gang = Gangs.ById(item3.A);
		Gang gang2 = Gangs.ById(item3.B);
		District district2 = _mod.DistrictByKey(item3.District);
		if (gang == null || gang2 == null)
		{
			return;
		}
		float num9 = 0.415f;
		float num10 = 0.5f;
		float ry = num3 + 0.01f;
		float mx = num9 + num10 / 2f;
		string s = _mod.WarName(item3).ToUpperInvariant();
		float num11 = 0.38f;
		while (Theme.CapsWidth(s, num11) > num10 * 0.95f && num11 > 0.28f)
		{
			num11 -= 0.01f;
		}
		Centre(s, num11, Theme.Title, title: true);
		ry += num11 * 0.075f + 0.01f;
		var (s2, c) = WarVerdict(item3, item4, gang, gang2, district2);
		Centre(s2, 0.3f, c, title: true);
		ry += 0.032f;
		long num12 = Math.Max(1L, (((item3.EndMin > 0) ? item3.EndMin : _mod.GameMinutesNow()) - item3.StartMin) / 1440 + (item4 ? 1 : 0));
		string s3 = (item4 ? $"{district2?.Name ?? item3.District}  -  Day {_mod.WarDay(item3)}, since {NemesisMod.WarSince(item3)}" : string.Format("{0}  -  over {1:MMM d} after {2} day{3}", district2?.Name ?? item3.District, GameDate.FromMinutes(item3.EndMin), num12, (num12 == 1) ? "" : "s"));
		Centre(FitText(s3, 0.24f, num10 * 0.95f, name: false), 0.24f, Theme.Dim);
		ry += 0.028f;
		if (!item4 && !string.IsNullOrEmpty(item3.EndHow))
		{
			Centre(FitText(Cap(item3.EndHow), 0.22f, num10 * 0.95f, name: false), 0.22f, Theme.Text);
			ry += 0.026f;
		}
		if (!string.IsNullOrEmpty(item3.Cause))
		{
			Centre(FitText("Why: " + Cap(item3.Cause), 0.21f, num10 * 0.95f, name: false), 0.21f, Theme.Faint);
			ry += 0.026f;
		}
		ry += 0.006f;
		Rule(num9, ry, num10);
		ry += 0.012f;
		float num13 = 0.02f;
		float half = (num10 - num13) / 2f;
		float num14 = ry;
		float num15 = ry;
		int num16 = ((district2 != null) ? _mod.HoldOf(district2, item3.A) : item3.HoldA0);
		int num17 = ((district2 != null) ? _mod.HoldOf(district2, item3.B) : item3.HoldB0);
		float x;
		float y;
		for (int num18 = 0; num18 < 2; num18++)
		{
			Gang gang3 = ((num18 == 0) ? gang : gang2);
			int num19 = ((num18 == 0) ? num16 : num17);
			int num20 = ((num18 == 0) ? item3.HoldA0 : item3.HoldB0);
			x = num9 + (float)num18 * (half + num13);
			y = num14;
			UiDraw.Rect(x, y, half, 0.0027777776f, gang3.Accent.r, gang3.Accent.g, gang3.Accent.b, 255);
			y += 0.008f;
			UiDraw.TextFont(FitTitle(gang3.Name.ToUpperInvariant(), 0.27f, half - 0.09f), UiDraw.TitleFont, x + 0.002f, y, 0.27f, gang3.Accent.r, gang3.Accent.g, gang3.Accent.b);
			string text = ((num18 == 0) ? "ATTACKING" : "DEFENDING");
			UiDraw.TextFont(text, UiDraw.TitleFont, x + half - Theme.CapsWidth(text, 0.19f) - 0.002f, y + 0.004f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			y += 0.028f;
			if (item4)
			{
				Field("LEADER", _mod.WarLeader(gang3), Theme.Text);
				Field("AIM", _mod.WarAim(gang3), Theme.Text);
				Field("PURSE", _mod.WarPurse(gang3), (r: 212, g: 176, b: 96));
				Field("COSTS", _mod.WarCosts(gang3), Theme.Dim);
				Field("MEN", $"{_mod.MenOf(gang3)} of {_mod.MenStart(gang3)}  -  {NemesisMod.StrengthWord(_mod.Strength(gang3))}", (_mod.Strength(gang3) < 30) ? Theme.Warn : Theme.Text);
			}
			else
			{
				Field("LEADER", (num18 == 0) ? item3.EndLeaderA : item3.EndLeaderB, Theme.Text);
				Field("AIM", (num18 == 0) ? item3.EndAimA : item3.EndAimB, Theme.Text);
				Field("PURSE", ((num18 == 0) ? item3.EndPurseA : item3.EndPurseB) ?? "not recorded", (r: 212, g: 176, b: 96));
				Field("MEN", ((num18 == 0) ? item3.EndMenA : item3.EndMenB) ?? "not recorded", Theme.Text);
			}
			UiDraw.TextFont("GROUND", UiDraw.TitleFont, x + 0.002f, y, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			float num21 = x + 0.06f;
			float num22 = half - 0.064f;
			float num23 = y + 0.008f;
			UiDraw.Rect(num21, num23, num22, 0.006f, 40, 38, 35, 230);
			UiDraw.Rect(num21, num23, num22 * Math.Max(0.01f, Math.Min(1f, (float)num19 / 60f)), 0.006f, gang3.Accent.r, gang3.Accent.g, gang3.Accent.b, 255);
			UiDraw.Rect(num21 + num22 * Math.Min(1f, (float)num20 / 60f), num23 - 0.004f, 0.0010416667f, 0.014f, 230, 226, 216, 200);
			y += 0.024f;
			num15 = Math.Max(num15, y);
		}
		ry = num15 + 0.006f;
		Rule(num9, ry, num10);
		ry += 0.012f;
		float lx2 = num9;
		float cwid = half;
		float num24 = num9 + half + num13;
		float ly = ry;
		float ry2 = ry;
		List<(WorldEvent, int)> list2 = _mod.WarFights(item3);
		List<(string, Gang, string, string)> list3 = _mod.WarNamedDead(item3);
		int num25 = 6 + Math.Min(list3.Count, 4) * 4;
		if (RoomR(0.05f, 20))
		{
			Theme.Section($"FIGHTS ({list2.Count})", num24, ry2, num24 + cwid);
			ry2 += 0.028f;
			_warEvCount = list2.Count;
			_warEvScroll = Math.Max(0, Math.Min(_warEvScroll, Math.Max(0, list2.Count - 1)));
			float num26 = ry2;
			float num27 = (bottom - ry2) * 0.55f;
			int num28 = 0;
			for (int num29 = _warEvScroll; num29 < list2.Count; num29++)
			{
				if (ry2 - num26 + 0.04f > num27)
				{
					break;
				}
				if (!RoomR(0.04f, 66 + num25))
				{
					break;
				}
				(WorldEvent, int) tuple6 = list2[num29];
				WorldEvent item5 = tuple6.Item1;
				int item6 = tuple6.Item2;
				(int, int, int) tuple7 = NemesisMod.KindColor(item5.Kind);
				UiDraw.Rect(num24, ry2 + 0.003f, 0.0010416667f, 0.032f, tuple7.Item1, tuple7.Item2, tuple7.Item3, 255);
				UiDraw.Text(GameDate.FromMinutes(item5.GameMin).ToString("MMM d  h:mm tt"), num24 + 0.008f, ry2, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				string text2 = ((item6 == 0) ? "no dead" : $"{item6} dead");
				(int, int, int) tuple8 = ((item6 == 0) ? Theme.Dim : Theme.Blood);
				UiDraw.TextFont(text2.ToUpperInvariant(), UiDraw.TitleFont, num24 + cwid - Theme.CapsWidth(text2.ToUpperInvariant(), 0.18f) - 0.004f, ry2, 0.18f, tuple8.Item1, tuple8.Item2, tuple8.Item3);
				UiDraw.Text(FitText(item5.Title ?? "", 0.2f, cwid - 0.014f, name: false), num24 + 0.008f, ry2 + 0.017f, 0.2f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
				ry2 += 0.04f;
				num28++;
			}
			if (list2.Count == 0)
			{
				UiDraw.Text("No fights yet.", num24 + 0.008f, ry2, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				ry2 += 0.024f;
			}
			if (num28 > 0 && num28 < list2.Count)
			{
				Theme.ScrollBar(num24 + cwid - 0.002f, num26, (float)num28 * 0.04f, _warEvScroll, num28, list2.Count);
			}
			ry2 += 0.008f;
		}
		if (RoomR(0.05f, 70))
		{
			List<(string, Gang, string, string)> list4 = list3;
			Theme.Section($"NAMED MEN DEAD ({list4.Count})", num24, ry2, num24 + cwid);
			ry2 += 0.028f;
			int num30 = 0;
			foreach (var (text3, gang4, text4, text5) in list4)
			{
				if (!RoomR(0.024f, 66))
				{
					break;
				}
				(int, int, int) tuple10 = gang4?.Accent ?? Theme.Dim;
				UiDraw.Rect(num24, ry2 + 0.005f, 0.0026041667f, 0.0046296297f, tuple10.Item1, tuple10.Item2, tuple10.Item3, 255);
				UiDraw.Text(FitText(text3 + (string.IsNullOrEmpty(text4) ? "" : ("  -  " + text4)), 0.2f, cwid - 0.07f, name: false), num24 + 0.01f, ry2, 0.2f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
				UiDraw.Text(text5, num24 + cwid - Theme.MixedWidth(text5, 0.18f, 0.0222f) - 0.004f, ry2 + 0.001f, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				ry2 += 0.024f;
				num30++;
			}
			if (list4.Count == 0)
			{
				UiDraw.Text("None yet.", num24 + 0.008f, ry2, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				ry2 += 0.024f;
			}
			else if (num30 < list4.Count && RoomR(0.022f, 4))
			{
				UiDraw.Text($"and {list4.Count - num30} more", num24 + 0.01f, ry2, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				ry2 += 0.022f;
			}
		}
		if (item4 && RoomL(0.05f))
		{
			Theme.Section("THE FRONT", lx2, ly, lx2 + cwid);
			ly += 0.028f;
			(int, int, int) tuple11 = NemesisMod.HeatColor(item3.Heat);
			UiDraw.TextFont(NemesisMod.HeatWord(item3.Heat).ToUpperInvariant(), UiDraw.TitleFont, lx2 + 0.004f, ly, 0.22f, tuple11.Item1, tuple11.Item2, tuple11.Item3);
			if (_mod.DebugOn)
			{
				UiDraw.Text($"{item3.Heat}", lx2 + cwid - 0.024f, ly, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			}
			float x2 = lx2 + 0.115f;
			float num31 = cwid - 0.145f;
			float y2 = ly + 0.009f;
			UiDraw.Rect(x2, y2, num31, 0.006f, 40, 38, 35, 230);
			UiDraw.Rect(x2, y2, num31 * Math.Max(0.02f, (float)item3.Heat / 100f), 0.006f, tuple11.Item1, tuple11.Item2, tuple11.Item3, 255);
			ly += 0.028f;
			int num32 = _mod.WarCampsTaken(item3);
			LeftLine(string.Format("{0} fight{1}, {2} named m{3}n dead{4}", list2.Count, (list2.Count == 1) ? "" : "s", list3.Count, (list3.Count == 1) ? "a" : "e", (num32 > 0) ? string.Format(", {0} camp{1} overrun", num32, (num32 == 1) ? "" : "s") : ""), 0.2f, Theme.Dim);
			ly += 0.008f;
		}
		if (RoomL(0.05f))
		{
			Theme.Section("WHAT THE " + Short(gang).ToUpperInvariant() + " WANT", lx2, ly, lx2 + cwid);
			ly += 0.028f;
			LeftLine(Cap(item3.GoalText ?? "nobody's said"), 0.21f, Theme.Text);
			HashSet<string> seenAims = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { (item3.GoalText ?? "").Trim() };
			foreach (string item7 in (from l in (item3.GoalLog ?? new List<string>()).AsEnumerable().Reverse()
				where seenAims.Add(AimOf(l))
				select l).Take(2))
			{
				LeftLine("before - " + item7, 0.19f, Theme.Faint, 0.02f);
			}
			ly += 0.008f;
		}
		string text6 = _mod.WarDefenders(item3);
		if (text6 != null && RoomL(0.05f))
		{
			Theme.Section("THE " + Short(gang2).ToUpperInvariant(), lx2, ly, lx2 + cwid);
			ly += 0.028f;
			LeftLine(text6, 0.21f, Theme.Text);
			ly += 0.008f;
		}
		string text7 = (item4 ? _mod.WarOutlook(item3) : null);
		if (text7 != null && RoomL(0.05f))
		{
			Theme.Section("OUTLOOK", lx2, ly, lx2 + cwid);
			ly += 0.028f;
			LeftLine(text7, 0.21f, Theme.Text);
			ly += 0.008f;
		}
		if (RoomL(0.05f))
		{
			Theme.Section("YOUR PART", lx2, ly, lx2 + cwid);
			ly += 0.028f;
			string t = ((item3.YouKilledA + item3.YouKilledB == 0) ? "You've kept out of it." : ($"You've killed {item3.YouKilledA} of the {Short(gang)} and {item3.YouKilledB} of the {Short(gang2)}" + ((item3.OfficersA + item3.OfficersB > 0) ? $" - {item3.OfficersA + item3.OfficersB} of them senior officers." : ".")));
			LeftLine(t, 0.21f, Theme.Text);
			if (item3.FavourSaid)
			{
				Gang g3 = ((item3.YouKilledA > item3.YouKilledB) ? gang2 : gang);
				LeftLine("The " + Short(g3) + " know you've been helping them.", 0.2f, Theme.Good);
			}
		}
		static string AimOf(string l)
		{
			int num33 = l.IndexOf(": ");
			string text8 = ((num33 >= 0) ? l.Substring(num33 + 2) : l);
			int num34 = text8.LastIndexOf(" (");
			return ((num34 > 0) ? text8.Substring(0, num34) : text8).Trim();
		}
		void Centre(string text8, float sc, (int r, int g, int b) tuple12, bool title = false)
		{
			float num33 = (title ? Theme.CapsWidth(text8, sc) : Theme.MixedWidth(text8, sc, 0.0222f));
			if (title)
			{
				UiDraw.TextFont(text8, UiDraw.TitleFont, mx - num33 / 2f, ry, sc, tuple12.r, tuple12.g, tuple12.b);
			}
			else
			{
				UiDraw.Text(text8, mx - num33 / 2f, ry, sc, tuple12.r, tuple12.g, tuple12.b);
			}
		}
		void Field(string label, string value, (int r, int g, int b) tuple12)
		{
			if (value != null)
			{
				UiDraw.TextFont(label, UiDraw.TitleFont, x + 0.002f, y + 0.001f, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				UiDraw.Text(FitText(value, 0.2f, half - 0.064f, name: false), x + 0.06f, y, 0.2f, tuple12.r, tuple12.g, tuple12.b);
				y += 0.022f;
			}
		}
		void LeftLine(string s4, float sc, (int r, int g, int b) tuple12, float step = 0.022f)
		{
			foreach (string item8 in WrapBody(s4, sc, cwid - 0.006f).Take(3))
			{
				if (!RoomL(step, 6))
				{
					break;
				}
				UiDraw.Text(item8, lx2 + 0.004f, ly, sc, tuple12.r, tuple12.g, tuple12.b);
				ly += step;
			}
		}
		bool RoomL(float need, int slots = 12)
		{
			if (ly + need <= bottom)
			{
				return UiDraw.TextSlotsLeft >= slots;
			}
			return false;
		}
		bool RoomR(float need, int slots = 12)
		{
			if (ry2 + need <= bottom)
			{
				return UiDraw.TextSlotsLeft >= slots;
			}
			return false;
		}
		static void Rule(float x3, float y3, float w, int alpha = 120)
		{
			UiDraw.Rect(x3, y3, w, 0.0009259259f, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, alpha);
		}
	}

	private static string Short(Gang g)
	{
		if (g != null)
		{
			if (!g.Name.StartsWith("The "))
			{
				return g.Name;
			}
			return g.Name.Substring(4);
		}
		return "?";
	}

	private static string Cap(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1) + (s.EndsWith(".") ? "" : ".");
		}
		return s;
	}
}
