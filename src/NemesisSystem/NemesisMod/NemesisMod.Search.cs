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
	private sealed class SearchHit
	{
		public string Kind;

		public string Label;

		public string Sub;

		public (int r, int g, int b) Color;

		public int Rank;

		public Action Go;
	}

	private sealed class SearchPanel
	{
		public string Query = "";

		public int Sel;

		public int Top;

		public List<SearchHit> Hits = new List<SearchHit>();

		public bool PadKb;

		public int PadKbAt;

		public bool OverXRay;

		public bool Pad;

		public bool OnKeys;

		public int GX;

		public int GY;
	}

	private SearchPanel _search;

	private static readonly eInputType[] PadKeyboardInputs = new eInputType[20]
	{
		eInputType.FrontendUp,
		eInputType.FrontendDown,
		eInputType.FrontendLeft,
		eInputType.FrontendRight,
		eInputType.FrontendAccept,
		eInputType.FrontendCancel,
		eInputType.FrontendX,
		eInputType.FrontendY,
		eInputType.FrontendLB,
		eInputType.FrontendRB,
		eInputType.FrontendLT,
		eInputType.FrontendRT,
		eInputType.FrontendLS,
		eInputType.FrontendRS,
		eInputType.FrontendPause,
		eInputType.FrontendSelect,
		eInputType.FrontendAxisX,
		eInputType.FrontendAxisY,
		eInputType.FrontendRightAxisX,
		eInputType.FrontendRightAxisY
	};

	private int _tabDownAt;

	private const int SelfCheckFirstMs = 60000;

	private const int SelfCheckEveryMs = 600000;

	internal bool SearchOpen => _search != null;

	private void OpenSearch(bool pad)
	{
		if (_menu.IsOpen || _bulletImpact.ViewerOpen)
		{
			_search = new SearchPanel
			{
				OverXRay = _bulletImpact.ViewerOpen,
				Pad = pad,
				OnKeys = pad
			};
			UiSound("MENU_ENTER");
			RunSearch();
		}
	}

	private void CloseSearch()
	{
		if (_search == null)
		{
			return;
		}
		if (_search.PadKb)
		{
			try
			{
				MISC.CANCEL_ONSCREEN_KEYBOARD();
			}
			catch
			{
			}
		}
		_search = null;
		UiSound("MENU_CLOSE");
	}

	private void StartPadKeyboard()
	{
		try
		{
			Crumbs.Native("search onscreen keyboard");
			MISC.DISPLAY_ONSCREEN_KEYBOARD(0, "", "", _search.Query ?? "", "", "", "", 40);
			_search.PadKb = true;
			_search.PadKbAt = Environment.TickCount;
		}
		catch (Exception ex)
		{
			GrudgeLog("SEARCH the game's keyboard failed: " + ex.Message);
			_search.PadKb = false;
		}
	}

	private void UpdateTabHold()
	{
		if (_tabDownAt == 0)
		{
			return;
		}
		if (!UiMouse.KeyDownNow(Keys.Tab))
		{
			bool num = _tabDownAt != -1;
			_tabDownAt = 0;
			if (num && _search == null && (_menu.IsOpen || _bulletImpact.ViewerOpen))
			{
				Dispatch(Keys.Tab);
			}
		}
		else if (_tabDownAt != -1 && Environment.TickCount - _tabDownAt > 600)
		{
			_tabDownAt = -1;
			if (_search == null)
			{
				OpenSearch(pad: false);
			}
		}
	}

	private void UpdateSearch()
	{
		if (_search == null)
		{
			return;
		}
		if (!_menu.IsOpen && !_bulletImpact.ViewerOpen)
		{
			_search = null;
			return;
		}
		Game.DisableAllControlsThisFrame();
		if (!_search.PadKb)
		{
			return;
		}
		eInputType[] padKeyboardInputs = PadKeyboardInputs;
		foreach (eInputType action in padKeyboardInputs)
		{
			PAD.ENABLE_CONTROL_ACTION(0, (uint)action, enableRelatedActions: true);
		}
		switch (MISC.UPDATE_ONSCREEN_KEYBOARD())
		{
		case 1:
		{
			string text = null;
			try
			{
				text = MISC.GET_ONSCREEN_KEYBOARD_RESULT();
			}
			catch
			{
			}
			_search.PadKb = false;
			_search.Query = (text ?? "").Trim();
			_search.Sel = 0;
			_search.Top = 0;
			RunSearch();
			GrudgeLog($"SEARCH pad keyboard: '{_search.Query}' - {_search.Hits.Count} found");
			break;
		}
		case 2:
			_search.PadKb = false;
			if (string.IsNullOrEmpty(_search.Query))
			{
				CloseSearch();
			}
			break;
		case 3:
			if (Environment.TickCount - _search.PadKbAt > 1500)
			{
				_search.PadKb = false;
				GrudgeLog("SEARCH the game's on-screen keyboard didn't come up - type with the keyboard");
				ShowNotification("enRAGE: the game's keyboard didn't open - type the name on the keyboard.");
			}
			break;
		}
	}

	private void PadSearchKey(Keys key)
	{
		SearchPanel s = _search;
		if (s == null)
		{
			return;
		}
		if (!s.Pad)
		{
			s.Pad = true;
			s.OnKeys = true;
		}
		if (!s.OnKeys)
		{
			if (key == Keys.Escape || key == Keys.Q || key == Keys.Tab || (key == Keys.Up && s.Sel == 0))
			{
				s.OnKeys = true;
				UiSound("NAV_UP");
			}
			else if (key == Keys.Up || key == Keys.Down || key == Keys.Return)
			{
				SearchKey(key, shift: false);
			}
			return;
		}
		int num = PadRowLen(s.GY);
		int num2 = PadGridRows.Length + 1;
		switch (key)
		{
		case Keys.Left:
			s.GX = (s.GX - 1 + num) % num;
			UiSound("NAV_LEFT");
			break;
		case Keys.Right:
			s.GX = (s.GX + 1) % num;
			UiSound("NAV_RIGHT");
			break;
		case Keys.Down:
			if (s.GY == num2 - 1 && s.Hits.Count > 0)
			{
				ToResults();
				break;
			}
			goto case Keys.Up;
		case Keys.Up:
		{
			int num3 = PadRowLen(s.GY);
			s.GY = (s.GY + ((key == Keys.Down) ? 1 : (num2 - 1))) % num2;
			int num4 = PadRowLen(s.GY);
			s.GX = Math.Min(num4 - 1, (int)Math.Round((float)(s.GX * (num4 - 1)) / (float)Math.Max(1, num3 - 1)));
			UiSound((key == Keys.Down) ? "NAV_DOWN" : "NAV_UP");
			break;
		}
		case Keys.Return:
			if (s.GY < PadGridRows.Length)
			{
				AddChar(PadGridRows[s.GY][s.GX]);
			}
			else if (s.GX == 0)
			{
				AddChar(' ');
			}
			else if (s.GX == 1)
			{
				Delete();
			}
			else
			{
				ToResults();
			}
			UiSound("SELECT");
			break;
		case Keys.Back:
			Delete();
			UiSound("BACK");
			break;
		case Keys.Space:
			if (s.Query.Length > 0 && !s.Query.EndsWith(" "))
			{
				AddChar(' ');
			}
			break;
		case Keys.Tab:
			ToResults();
			break;
		case Keys.Escape:
			CloseSearch();
			break;
		}
		void AddChar(char c)
		{
			if (s.Query.Length < 40)
			{
				bool flag = s.Query.Length == 0 || s.Query.EndsWith(" ");
				s.Query += (flag ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
				s.Sel = 0;
				s.Top = 0;
				RunSearch();
			}
		}
		void Delete()
		{
			if (s.Query.Length > 0)
			{
				s.Query = s.Query.Substring(0, s.Query.Length - 1);
				s.Sel = 0;
				s.Top = 0;
				RunSearch();
			}
		}
		void ToResults()
		{
			if (s.Hits.Count > 0)
			{
				s.OnKeys = false;
				s.Sel = 0;
				s.Top = 0;
				UiSound("NAV_DOWN");
			}
		}
	}

	private bool SearchKey(Keys key, bool shift)
	{
		SearchPanel search = _search;
		if (search == null)
		{
			return false;
		}
		if (search.PadKb)
		{
			return true;
		}
		switch (key)
		{
		case Keys.Escape:
			CloseSearch();
			return true;
		case Keys.Back:
			if (search.Query.Length == 0)
			{
				CloseSearch();
				return true;
			}
			search.Query = search.Query.Substring(0, search.Query.Length - 1);
			search.Sel = 0;
			search.Top = 0;
			RunSearch();
			return true;
		case Keys.Up:
			if (search.Hits.Count > 0)
			{
				search.Sel = (search.Sel - 1 + search.Hits.Count) % search.Hits.Count;
				UiSound("NAV_UP");
			}
			return true;
		case Keys.Down:
			if (search.Hits.Count > 0)
			{
				search.Sel = (search.Sel + 1) % search.Hits.Count;
				UiSound("NAV_DOWN");
			}
			return true;
		case Keys.Return:
		{
			if (search.Hits.Count == 0)
			{
				return true;
			}
			SearchHit searchHit = search.Hits[Math.Max(0, Math.Min(search.Sel, search.Hits.Count - 1))];
			GrudgeLog("SEARCH '" + search.Query + "' -> " + searchHit.Kind + " " + searchHit.Label);
			_search = null;
			UiSound("SELECT");
			try
			{
				searchHit.Go();
			}
			catch (Exception ex)
			{
				GrudgeLog("SEARCH jump failed: " + ex.Message);
			}
			return true;
		}
		default:
		{
			char c = KeyChar(key, shift);
			if (c != 0 && search.Query.Length < 40)
			{
				search.Query += c;
				search.Sel = 0;
				search.Top = 0;
				RunSearch();
			}
			return true;
		}
		}
	}

	private static char KeyChar(Keys k, bool shift)
	{
		if (k >= Keys.A && k <= Keys.Z)
		{
			return (char)((shift ? 65 : 97) + (k - 65));
		}
		if (k >= Keys.D0 && k <= Keys.D9 && !shift)
		{
			return (char)(48 + (k - 48));
		}
		if (k >= Keys.NumPad0 && k <= Keys.NumPad9)
		{
			return (char)(48 + (k - 96));
		}
		switch (k)
		{
		case Keys.Space:
			return ' ';
		case Keys.OemQuotes:
			return '\'';
		case Keys.Subtract:
		case Keys.OemMinus:
			return '-';
		case Keys.Decimal:
		case Keys.OemPeriod:
			return '.';
		default:
			return '\0';
		}
	}

	private static int MatchRank(string text, string q)
	{
		if (string.IsNullOrEmpty(text))
		{
			return -1;
		}
		int num = text.IndexOf(q, StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return -1;
		}
		if (num == 0)
		{
			return 0;
		}
		if (text[num - 1] != ' ' && text[num - 1] != '\'' && text[num - 1] != '-')
		{
			return 2;
		}
		return 1;
	}

	private void RunSearch()
	{
		SearchPanel search = _search;
		search.Hits.Clear();
		string text = (search.Query ?? "").Trim();
		if (text.Length == 0)
		{
			return;
		}
		List<SearchHit> hits = new List<SearchHit>();
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Gang[] all = Gangs.All;
		foreach (Gang gang in all)
		{
			NemesisRecord nemesisRecord = LeaderPageOf(gang);
			if (nemesisRecord != null && nemesisRecord.LeaderPage)
			{
				hashSet.Add(nemesisRecord.Name);
				Gang gg = gang;
				Add("LEADER", nemesisRecord.Name, "Leader of the " + gang.Name, Theme.Gold, MatchRank(nemesisRecord.Name, text), 0, delegate
				{
					ShowRecordPage(LeaderPageOf(gg));
				});
			}
		}
		foreach (NemesisRecord item3 in Roster)
		{
			if (item3 == null || string.IsNullOrEmpty(item3.Name))
			{
				continue;
			}
			int num = Math.Max(MatchRank(item3.Name, text), (item3.Title != null && item3.Title.Length > 0) ? MatchRank(item3.FullName, text) : (-1));
			if (num >= 0)
			{
				hashSet.Add(item3.Name);
				Gang gang2 = Gangs.ById(item3.Gang);
				bool flag = !item3.IsAlive || (item3.PresumedDead && !NemesisRecord.ShowSecrets);
				string sub = ((gang2 != null) ? gang2.Name : "Loner") + "  -  " + RosterMenu.RankText(item3) + "  -  " + (flag ? item3.Status : item3.HateTier.ToString());
				NemesisRecord rr = item3;
				Add(flag ? "FALLEN" : "NEMESIS", item3.FullName, sub, flag ? Theme.Dim : UiDraw.HateColor(item3.HateTier), num, (!flag) ? 1 : 4, delegate
				{
					ShowRecordPage(rr);
				});
			}
		}
		foreach (NemesisRecord item4 in StoryRoster)
		{
			int num2 = MatchRank(item4.Name, text);
			if (num2 >= 0)
			{
				hashSet.Add(item4.Name);
				NemesisRecord rr2 = item4;
				Add("THE GANG", item4.Name, string.IsNullOrEmpty(item4.Title) ? "Van der Linde gang" : ("Van der Linde gang  -  " + item4.Title), VdlAccent, num2, 2, delegate
				{
					ShowRecordPage(rr2);
				});
			}
		}
		foreach (Acquaintance person in People)
		{
			if (person == null || string.IsNullOrEmpty(person.Name) || hashSet.Contains(person.Name))
			{
				continue;
			}
			int num3 = MatchRank(person.Name, text);
			if (num3 < 0)
			{
				continue;
			}
			Acquaintance aa = person;
			string sub2 = (person.Dead ? "Dead" : (person.Met ? "Met" : "Seen")) + (string.IsNullOrEmpty(person.Town) ? "" : ("  -  " + person.Town)) + (string.IsNullOrEmpty(person.Temper) ? "" : ("  -  " + person.Temper));
			Add("PERSON", person.FullName, sub2, (r: 170, g: 200, b: 150), num3, 3, delegate
			{
				NemesisRecord nemesisRecord2 = PersonRecord(aa);
				if (nemesisRecord2 != null)
				{
					ShowRecordPage(nemesisRecord2);
				}
			});
		}
		foreach (var item5 in WarList())
		{
			WarRecord item = item5.w;
			bool item2 = item5.live;
			string text2 = WarName(item);
			Gang gang3 = Gangs.ById(item.A);
			Gang gang4 = Gangs.ById(item.B);
			int num4 = Math.Max(MatchRank(text2, text), Math.Max(MatchRank(gang3?.Name, text), MatchRank(gang4?.Name, text)));
			if (num4 >= 0)
			{
				WarRecord ww = item;
				Add("WAR", text2, (DistrictByKey(item.District)?.Name ?? item.District) + "  -  " + gang3?.Name + " vs " + gang4?.Name + "  -  " + (item2 ? $"Day {WarDay(item)}" : "Over"), Theme.NativeRed, num4, 5, delegate
				{
					ShowWarPage(ww);
				});
			}
		}
		int num5 = 0;
		foreach (WorldEvent item6 in ChronicleFor(0))
		{
			if (num5 >= 25)
			{
				break;
			}
			int num6 = MatchRank(item6.Title, text);
			if (num6 < 0 && item6.People != null)
			{
				foreach (WorldPart person2 in item6.People)
				{
					num6 = Math.Max(num6, MatchRank(person2.Name, text));
				}
			}
			if (num6 >= 0)
			{
				num5++;
				WorldEvent ee = item6;
				Add("EVENT", item6.Title, (string.IsNullOrEmpty(item6.When) ? "" : (item6.When + "  -  ")) + KindLabel(item6.Kind) + (string.IsNullOrEmpty(item6.Place) ? "" : ("  -  " + item6.Place)), KindColor(item6.Kind), num6, 6, delegate
				{
					ShowEventPage(ee);
				});
			}
		}
		foreach (var item7 in _bulletImpact.SearchXRays())
		{
			int num7 = MatchRank(item7.victim, text);
			if (num7 >= 0)
			{
				var (id, _, _, _, _) = item7;
				Add("X-RAY", item7.victim, string.Format("{0} {1}{2}{3}", item7.shots, (item7.shots == 1) ? "wound" : "wounds", item7.animal ? "  -  animal" : "", item7.killed ? "  -  killed" : ""), (r: 120, g: 150, b: 210), num7, 7, delegate
				{
					ShowXRayOf(id);
				});
			}
		}
		search.Hits = (from h in hits
			orderby h.Rank, h.Label
			select h).Take(60).ToList();
		search.Sel = Math.Min(search.Sel, Math.Max(0, search.Hits.Count - 1));
		void Add(string kind, string label, string sub3, (int r, int g, int b) c, int rank, int kindOrder, Action go)
		{
			if (rank >= 0)
			{
				hits.Add(new SearchHit
				{
					Kind = kind,
					Label = label,
					Sub = sub3,
					Color = c,
					Rank = rank * 10 + kindOrder,
					Go = go
				});
			}
		}
	}

	private void ShowRecordPage(NemesisRecord r)
	{
		if (r != null)
		{
			if (_bulletImpact.ViewerOpen)
			{
				_bulletImpact.CloseViewer();
			}
			_menu.Open();
			_menu.ShowRecord(r);
		}
	}

	private void ShowEventPage(WorldEvent e)
	{
		if (_bulletImpact.ViewerOpen)
		{
			_bulletImpact.CloseViewer();
		}
		_menu.Open();
		_menu.ShowEvent(e);
	}

	private void ShowWarPage(WarRecord w)
	{
		if (_bulletImpact.ViewerOpen)
		{
			_bulletImpact.CloseViewer();
		}
		_menu.Open();
		_menu.ShowWar(w);
	}

	private void ShowXRayOf(int entryId)
	{
		if (_menu.IsOpen)
		{
			_menu.Close();
		}
		_bulletImpact.OpenViewerAt(entryId);
	}

	private void DrawSearch()
	{
		SearchPanel search = _search;
		if (search == null)
		{
			return;
		}
		int num = (search.Pad ? 6 : 10);
		int num2 = Math.Min(num, Math.Max(1, search.Hits.Count));
		if (search.Sel < search.Top)
		{
			search.Top = search.Sel;
		}
		if (search.Sel >= search.Top + num)
		{
			search.Top = search.Sel - num + 1;
		}
		float num3 = 0.028f * UiDraw.Aspect;
		float num4 = 0.004f * UiDraw.Aspect;
		float num5 = (search.Pad ? ((float)(PadGridRows.Length + 1) * (num3 + num4) + 0.012f) : 0f);
		float num6 = 0.08f + num5 + 0.01f + (float)num2 * 0.034f + 0.04f;
		float num7 = 0.1f;
		Theme.Brush(0.22f, num7, 0.56f, num6, 255);
		string text = "SEARCH";
		UiDraw.TextFont(text, UiDraw.TitleFont, 0.5f - Theme.CapsWidth(text, 0.26f) / 2f, num7 + 0.008f, 0.26f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		float num8 = num7 + 0.04f;
		float num9 = 0.244f;
		float w = 0.512f;
		UiDraw.Rect(num9, num8, w, 0.032f, 20, 19, 17, 235);
		Theme.Hairline(num9, num8, w, 0.032f, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 160);
		bool flag = UiDraw.RealMs / 500 % 2 == 0;
		string text2 = (search.PadKb ? "typing on the game's keyboard..." : ((search.Query.Length != 0) ? (search.Query + (flag ? "|" : "")) : (flag ? "|" : "")));
		(int, int, int) tuple = ((search.PadKb || search.Query.Length == 0) ? Theme.Dim : (250, 246, 236));
		UiDraw.Text(text2, num9 + 0.01f, num8 + 0.006f, 0.25f, tuple.Item1, tuple.Item2, tuple.Item3);
		if (search.Query.Length == 0 && !search.PadKb)
		{
			UiDraw.Text("A name - nemeses, people, leaders, wars, world events, X-rays", num9 + 0.022f, num8 + 0.007f, 0.21f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b);
		}
		float num10 = num8 + 0.04f + 0.006f;
		if (search.Pad)
		{
			int num11 = PadGridRows.Max((string r0) => r0.Length);
			float num12 = (float)num11 * 0.028f + (float)(num11 - 1) * 0.004f;
			for (int num13 = 0; num13 <= PadGridRows.Length; num13++)
			{
				int num14 = PadRowLen(num13);
				float num15 = ((num13 >= PadGridRows.Length) ? ((num12 - (float)(num14 - 1) * 0.004f) / (float)num14) : 0.028f);
				float num16 = (float)num14 * num15 + (float)(num14 - 1) * 0.004f;
				float num17 = 0.5f - num16 / 2f;
				for (int num18 = 0; num18 < num14; num18++)
				{
					string text3 = ((num13 < PadGridRows.Length) ? PadGridRows[num13][num18].ToString() : PadGridKeys[num18]);
					bool num19 = search.OnKeys && num13 == search.GY && num18 == search.GX;
					float num20 = num17 + (float)num18 * (num15 + 0.004f);
					if (num19)
					{
						Theme.SelectBox(num20, num10, num15, num3);
					}
					else
					{
						UiDraw.Rect(num20, num10, num15, num3, 30, 28, 25, 235);
						Theme.Hairline(num20, num10, num15, num3, Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, search.OnKeys ? 170 : 110);
					}
					bool flag2 = text3 == "RESULTS" && search.Hits.Count == 0;
					(int, int, int) tuple2 = (num19 ? Theme.SelectedText : (flag2 ? Theme.Faint : (search.OnKeys ? Theme.Text : Theme.Dim)));
					float num21 = ((text3.Length == 1) ? 0.28f : 0.21f);
					UiDraw.TextFont(text3, UiDraw.TitleFont, num20 + num15 / 2f - Theme.CapsWidth(text3, num21) / 2f, num10 + num3 / 2f - num21 * 0.0375f, num21, tuple2.Item1, tuple2.Item2, tuple2.Item3);
				}
				num10 += num3 + num4;
			}
			num10 += 0.012f;
		}
		if (search.Query.Length > 0 && search.Hits.Count == 0 && !search.PadKb)
		{
			string text4 = "Nothing found";
			UiDraw.Text(text4, 0.5f - Theme.MixedWidth(text4, 0.22f, 0.0222f) / 2f, num10 + 0.006f, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		for (int num22 = search.Top; num22 < Math.Min(search.Hits.Count, search.Top + num); num22++)
		{
			SearchHit searchHit = search.Hits[num22];
			bool num23 = num22 == search.Sel && (!search.Pad || !search.OnKeys);
			int hi = num22;
			UiMouse.Region(0.232f, num10, 0.536f, 0.030000001f, delegate
			{
				if (_search == null || _search.Sel == hi)
				{
					return false;
				}
				_search.Sel = hi;
				return true;
			}, delegate
			{
				UiMouse.Send(Keys.Return);
			});
			if (num23)
			{
				Theme.SelectBox(0.232f, num10, 0.536f, 0.030000001f);
			}
			float num24 = 0.242f;
			float val = Theme.Tag(searchHit.Kind, num24, num10 + 0.006f, searchHit.Color);
			float num25 = num24 + Math.Max(val, 0.07f) + 0.01f;
			(int, int, int) tuple3 = (num23 ? Theme.SelectedText : Theme.Text);
			float num26 = Math.Min(0.24f, Theme.MixedWidth(searchHit.Sub ?? "", 0.19f, 0.0222f));
			UiDraw.Text(FitW(searchHit.Label ?? "", 0.24f, 0.75f - num26 - 0.014f - num25), num25, num10 + 0.005f, 0.24f, tuple3.Item1, tuple3.Item2, tuple3.Item3);
			string text5 = FitW(searchHit.Sub ?? "", 0.19f, 0.24f);
			UiDraw.Text(text5, 0.75f - Theme.MixedWidth(text5, 0.19f, 0.0222f), num10 + 0.009f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			num10 += 0.034f;
		}
		if (search.Hits.Count > num)
		{
			Theme.ScrollBar(0.77f, num10 - (float)Math.Min(num, search.Hits.Count - search.Top) * 0.034f, (float)num * 0.034f, search.Top, num, search.Hits.Count);
		}
		List<(string, string)> hints = ((search.Pad && search.OnKeys) ? new List<(string, string)>
		{
			("A", "Type"),
			("X", "Delete"),
			("Y", "Space"),
			("B", "Close")
		} : (search.Pad ? new List<(string, string)>
		{
			("D-pad", "Pick"),
			("A", "Go"),
			("B", "Letters")
		} : new List<(string, string)>
		{
			("Up/Down", "Navigate"),
			("Enter", "Go"),
			("Esc", "Close")
		}));
		Theme.HintRow(0.236f, 0.764f, num7 + num6 - 0.034f, hints, 0.21f, Theme.Dim);
	}
}
