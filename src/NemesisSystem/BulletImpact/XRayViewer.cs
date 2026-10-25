using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NemesisSystem.BulletImpact;

internal sealed class XRayViewer
{
	private sealed class Group
	{
		public string Key;

		public string Name;

		public string Sub;

		public string NemesisId;

		public string FollowerOf;

		public List<XRayEntry> Shots;

		public DateTime Last;
	}

	private const float FilterBarH = 0.04f;

	private const float BodyTop = 0.18299998f;

	private const float BodyBottom = 0.85999995f;

	private const float FooterY = 0.868f;

	private const float ListX = 0.07f;

	private const float ListW = 0.21f;

	private const float ViewX = 0.286f;

	private const float ViewW = 0.335f;

	private const float InfoX = 0.62700003f;

	private const float InfoW = 0.30299997f;

	private const float RowH = 0.034f;

	private readonly XRayLog _log;

	private readonly XRayRenderer _renderer = new XRayRenderer();

	private readonly Func<string, NemesisFate> _fateOf;

	private readonly Action<XRayQuality> _saveQuality;

	private readonly Action<int> _saveBudget;

	private bool _animals;

	private static readonly string[] FilterNames = new string[2] { "Humans", "Animals" };

	private string _groupKey;

	private int _shot = -1;

	private int _scroll;

	private bool _confirmClear;

	private string _builtFor;

	private XRayComposite _composite;

	private XRayQuality _quality;

	public static bool DebugOn;

	private int _rectLimit;

	public static float GlyphCost = 0.8f;

	public static Action<string> Diag;

	private int _diagAt = -100000;

	private int _heldBudget;

	private string _diagFor;

	private bool _calibrating;

	private int _calRow;

	private int _calPage;

	private const int CalCols = 50;

	private const int CalRows = 40;

	private bool _dragOrbit;

	private List<Group> _groupsCache;

	private (bool animals, int count, int lastId) _groupsKey;

	private int _groupsAt;

	private const float ListHeadH = 0.032f;

	private const float CardH = 0.205f;

	private const float ListBottom = 0.64699996f;

	private float _lx;

	private float _ly;

	private static readonly string[][] SpriteCandidates = new string[8][]
	{
		new string[2] { "generic_textures", "weapon_stats_bar" },
		new string[2] { "generic_textures", "menu_bar" },
		new string[2] { "generic_textures", "list_item_h_line_narrow" },
		new string[2] { "generic_textures", "divider_line" },
		new string[2] { "generic_textures", "selection_box_square" },
		new string[2] { "generic_textures", "translate_bg_1a" },
		new string[2] { "generic_textures", "inkroller_1a" },
		new string[2] { "feeds", "toast_bg" }
	};

	private float _fx;

	private float _fvx;

	private float _fy;

	internal static readonly (int, int, int) PunchColor = (175, 100, 215);

	internal static readonly (int, int, int) KickColor = (215, 140, 60);

	internal static readonly (int, int, int) GunColor = (100, 170, 235);

	internal static readonly (int, int, int) ChokeColor = (225, 75, 85);

	private XRayQuality Quality
	{
		get
		{
			if (!DebugOn)
			{
				return XRayQuality.Auto;
			}
			return _quality;
		}
	}

	public bool IsOpen { get; private set; }

	private float RowW => 0.27899998f;

	public event Action SwitchToHierarchy;

	public XRayViewer(XRayLog log, XRayQuality quality, int rectBudget, Func<string, NemesisFate> fateOf, Action<int> saveBudget, Action<XRayQuality> saveQuality)
	{
		_saveBudget = saveBudget;
		_log = log;
		_quality = quality;
		_rectLimit = rectBudget;
		_fateOf = fateOf;
		_saveQuality = saveQuality;
	}

	public void Open(string nemesisId = null)
	{
		IsOpen = true;
		_confirmClear = false;
		_renderer.ReleaseAll();
		if (nemesisId != null && _animals)
		{
			SetFilter(animals: false);
		}
		List<Group> source = Groups();
		Group obj = ((nemesisId != null) ? source.FirstOrDefault((Group x) => x.NemesisId == nemesisId) : null);
		if (obj != null || _groupKey == null || source.All((Group x) => x.Key != _groupKey))
		{
			_groupKey = (obj ?? source.FirstOrDefault())?.Key;
			_shot = -1;
			_renderer.ResetView();
		}
	}

	public void Close()
	{
		IsOpen = false;
		_renderer.ReleaseAll();
	}

	public List<(int entryId, string victim, int shots, bool animal, bool killed)> SearchRows()
	{
		List<(int, string, int, bool, bool)> list = new List<(int, string, int, bool, bool)>();
		bool[] array = new bool[2] { false, true };
		foreach (bool flag in array)
		{
			using List<Group>.Enumerator enumerator = Groups(flag).GetEnumerator();
			int id;
			string name;
			int count;
			int item;
			for (; enumerator.MoveNext(); list.Add((id, name, count, flag, (byte)item != 0)))
			{
				Group current = enumerator.Current;
				id = current.Shots[current.Shots.Count - 1].Id;
				name = current.Name;
				count = current.Shots.Count;
				if (current.Shots.Any((XRayEntry s) => s.Killed))
				{
					string text = ManStatus(current);
					if (text != null && text != "CHEATED DEATH")
					{
						item = ((text != "HUNTING YOU") ? 1 : 0);
						continue;
					}
				}
				item = 0;
			}
		}
		return list;
	}

	public void OpenAt(int entryId)
	{
		XRayEntry e = _log.Entries.FirstOrDefault((XRayEntry x) => x.Id == entryId);
		if (e != null)
		{
			SetFilter(e.Animal);
		}
		Open();
		if (e != null)
		{
			Group obj = Groups().FirstOrDefault((Group x) => x.Shots.Contains(e));
			if (obj != null)
			{
				_groupKey = obj.Key;
				_shot = -1;
				_scroll = 0;
				_renderer.ResetView();
			}
		}
	}

	private List<Group> Groups(bool? animals = null)
	{
		bool flag = animals ?? _animals;
		(bool, int, int) groupsKey = (flag, _log.Entries.Count, (_log.Entries.Count > 0) ? _log.Entries[_log.Entries.Count - 1].Id : 0);
		if (_groupsCache != null && groupsKey.Equals(_groupsKey) && Environment.TickCount - _groupsAt < 250)
		{
			return _groupsCache;
		}
		_groupsKey = groupsKey;
		_groupsAt = Environment.TickCount;
		return _groupsCache = GroupsNow(flag);
	}

	private List<Group> GroupsNow(bool wantAnimals)
	{
		Dictionary<int, string> nemesisOfVictim = new Dictionary<int, string>();
		foreach (XRayEntry entry in _log.Entries)
		{
			if (entry.NemesisId != null && entry.VictimId > 0)
			{
				nemesisOfVictim[entry.VictimId] = entry.NemesisId;
			}
		}
		return (from g in _log.Entries.Where((XRayEntry e) => e.Animal == wantAnimals).GroupBy(KeyOf).Select(delegate(IGrouping<string, XRayEntry> g)
			{
				List<XRayEntry> list = g.OrderBy((XRayEntry e) => e.Time).ToList();
				XRayEntry xRayEntry = list[list.Count - 1];
				XRayEntry xRayEntry2 = list.LastOrDefault((XRayEntry s) => s.NemesisName != null);
				return new Group
				{
					Key = g.Key,
					Shots = list,
					Last = xRayEntry.Time,
					NemesisId = xRayEntry2?.NemesisId,
					FollowerOf = list.Select((XRayEntry s) => s.FollowerOf).FirstOrDefault((string f) => f != null),
					Name = (xRayEntry2?.NemesisName ?? ShownVictim(xRayEntry)),
					Sub = ((xRayEntry2 == null) ? xRayEntry.Location : (Gangs.ForModel(xRayEntry.VictimModel)?.Name ?? "Unaffiliated"))
				};
			})
			orderby g.Last descending
			select g).ToList();
		string KeyOf(XRayEntry e)
		{
			string value;
			string text = e.NemesisId ?? ((e.VictimId > 0 && nemesisOfVictim.TryGetValue(e.VictimId, out value)) ? value : null);
			if (text == null)
			{
				if (e.VictimId <= 0)
				{
					return "e:" + e.Id;
				}
				return "v:" + e.VictimId;
			}
			return "n:" + text;
		}
	}

	private static string ShownVictim(XRayEntry e)
	{
		if (!string.IsNullOrEmpty(e.Victim) && !(e.Victim == BulletImpactModule.ModelLabel(e.VictimModel)))
		{
			return e.Victim;
		}
		return BulletImpactModule.PrettyVictim(e.VictimModel);
	}

	private static string WhenText(XRayEntry e, bool seconds = false)
	{
		if (e.GameMin <= 0)
		{
			return e.Time.ToString(seconds ? "HH:mm:ss" : "HH:mm");
		}
		DateTime dateTime = GameDate.FromMinutes(e.GameMin);
		return $"{GameDate.Month(dateTime.Month)} {dateTime.Day}, {dateTime:HH:mm}";
	}

	private List<(Group g, int shot)> Rows(List<Group> groups)
	{
		List<(Group, int)> list = new List<(Group, int)>();
		foreach (Group group in groups)
		{
			list.Add((group, -1));
			if (group.Key == _groupKey)
			{
				for (int i = 0; i < group.Shots.Count; i++)
				{
					list.Add((group, i));
				}
			}
		}
		return list;
	}

	public bool HandleKey(Keys key)
	{
		if (_calibrating)
		{
			switch (key)
			{
			case Keys.Up:
				_calRow = Math.Max(0, _calRow - 1);
				break;
			case Keys.Down:
				_calRow = Math.Min(39, _calRow + 1);
				break;
			case Keys.Left:
			case Keys.Right:
				_calPage = 1 - _calPage;
				break;
			case Keys.Return:
				_rectLimit = (_calRow + 1) * 50;
				_saveBudget(_rectLimit);
				_calibrating = false;
				break;
			case Keys.Back:
			case Keys.K:
				_calibrating = false;
				break;
			}
			return true;
		}
		if (key == Keys.K && DebugOn)
		{
			_calibrating = true;
			_calRow = Math.Max(0, Math.Min(39, _rectLimit / 50 - 1));
			return true;
		}
		if (_renderer.SetHeld(key, down: true))
		{
			return true;
		}
		List<Group> groups = Groups();
		List<(Group, int)> list = Rows(groups);
		int num = list.FindIndex(((Group g, int shot) r) => r.g.Key == _groupKey && r.shot == _shot);
		if (key <= Keys.Down)
		{
			if (key <= Keys.Tab)
			{
				if (key != Keys.Back)
				{
					if (key != Keys.Tab)
					{
						goto IL_029b;
					}
					this.SwitchToHierarchy?.Invoke();
				}
				else
				{
					Close();
				}
			}
			else if (key != Keys.Space)
			{
				if (key != Keys.Up)
				{
					if (key != Keys.Down)
					{
						goto IL_029b;
					}
					Select(list, Math.Min(list.Count - 1, num + 1));
				}
				else
				{
					Select(list, Math.Max(0, num - 1));
				}
			}
			else
			{
				_renderer.AutoSpin = !_renderer.AutoSpin;
			}
		}
		else
		{
			if (key <= Keys.E)
			{
				if (key != Keys.Delete)
				{
					if (key == Keys.E)
					{
						goto IL_01f0;
					}
				}
				else if (DebugOn)
				{
					if (!_confirmClear)
					{
						_confirmClear = true;
						return true;
					}
					_log.Clear();
					_groupKey = null;
					_builtFor = null;
					goto IL_029d;
				}
				goto IL_029b;
			}
			if (key != Keys.F)
			{
				if (key == Keys.Q)
				{
					goto IL_01f0;
				}
				if (key != Keys.R)
				{
					goto IL_029b;
				}
				_renderer.ResetView();
			}
			else
			{
				if (!DebugOn)
				{
					goto IL_029b;
				}
				_quality = ((_quality == XRayQuality.Auto) ? XRayQuality.High : ((_quality == XRayQuality.Low) ? XRayQuality.Auto : (_quality - 1)));
				_builtFor = null;
				_saveQuality(_quality);
			}
		}
		goto IL_029d;
		IL_029b:
		return false;
		IL_029d:
		_confirmClear = false;
		return true;
		IL_01f0:
		SetFilter(!_animals);
		goto IL_029d;
	}

	public void HandleKeyUp(Keys key)
	{
		_renderer.SetHeld(key, down: false);
	}

	private void SetFilter(bool animals)
	{
		if (_animals != animals)
		{
			_animals = animals;
			_groupKey = null;
			_shot = -1;
			_scroll = 0;
			_builtFor = null;
			_confirmClear = false;
			_renderer.ResetView();
		}
	}

	public void Orbit(float dYaw, float dPitch)
	{
		_renderer.Orbit(dYaw, dPitch);
	}

	private void Select(List<(Group g, int shot)> rows, int index)
	{
		if (index >= 0 && index < rows.Count)
		{
			bool num = rows[index].g.Key != _groupKey;
			_groupKey = rows[index].g.Key;
			_shot = rows[index].shot;
			if (num)
			{
				_renderer.ResetView();
			}
		}
	}

	public void Draw()
	{
		if (_calibrating)
		{
			DrawCalibration();
			return;
		}
		List<Group> list = Groups();
		int num = _log.Entries.Count((XRayEntry x) => x.Animal == _animals);
		UiDraw.ScreenRect(0, 0, 0, 150);
		Theme.Window(0.04f, 0.90199995f);
		string text = ((!_animals) ? ((list.Count == 1) ? "man" : "men") : ((list.Count == 1) ? "animal" : "animals"));
		Theme.Header("X-RAY LOG", string.Format("{0} {1} shot  -  {2} wound{3} on record", list.Count, text, num, (num == 1) ? "" : "s"), MenuTab.XRay);
		Theme.FactionBar(0.13499999f, FilterNames, _animals ? 1 : 0, isGang: false, Theme.NativeRed, null, null, chipAsText: false, delegate(int i)
		{
			SetFilter(i == 1);
		});
		if (list.Count == 0)
		{
			Theme.PanelRect(0.07f, 0.18299998f, 0.86f, 0.677f);
			Theme.Body(_animals ? "No animals logged yet. Shoot, cut or blow up a four-legged animal and the wound is recorded here." : "No wounds logged yet. Every time you shoot, cut or blow up a person, the wound is recorded here.", 0.085f, 0.20299998f);
			DrawFooter();
			return;
		}
		Group obj = list.FirstOrDefault((Group x) => x.Key == _groupKey) ?? list[0];
		_groupKey = obj.Key;
		if (_shot >= obj.Shots.Count)
		{
			_shot = -1;
		}
		string text2 = $"{obj.Key}|{_shot}|{obj.Shots.Count}|{Quality}";
		if (text2 != _builtFor)
		{
			_composite = ((_shot < 0) ? XRayComposite.ForShots(obj.Shots) : XRayComposite.ForEntry(obj.Shots[_shot]));
			_renderer.Build(_composite.Body, _composite.Paths, _composite.Keys, Quality);
			_builtFor = text2;
		}
		HandleMouse();
		DrawFooter();
		DrawList(list);
		DrawFacts(obj);
		if (_shot < 0)
		{
			DrawManSummary(obj);
		}
		else
		{
			DrawShot(obj, _shot);
		}
		DrawXRay(obj);
	}

	private void Fact(string label, string value, (int r, int g, int b)? color = null)
	{
		if (!string.IsNullOrEmpty(value))
		{
			(int, int, int) tuple = color ?? Theme.Text;
			if (!(_ly > 0.83399993f) && UiDraw.TextSlotsLeft >= 8)
			{
				Theme.Label(label, _lx, _ly + 0.002f, 0.18f);
				float num = Theme.CapsWidth(label, 0.18f) + 0.012f;
				float width = 0.188f - num;
				string text = Fit(value, 0.23f, width);
				UiDraw.Text(text, 0.269f - Theme.MixedWidth(text, 0.23f, 0.0222f), _ly, 0.23f, tuple.Item1, tuple.Item2, tuple.Item3);
				UiDraw.Rect(_lx, _ly + 0.023f, 0.188f, 0.0009259259f, Theme.Border.r, Theme.Border.g, Theme.Border.b, 110);
				_ly += 0.027f;
			}
		}
	}

	private void DrawFacts(Group g)
	{
		float num = 0.655f;
		Theme.PanelRect(0.07f, num, 0.21f, 0.205f);
		bool num2 = _shot < 0;
		XRayEntry xRayEntry = (num2 ? null : g.Shots[_shot]);
		bool flag = xRayEntry != null && Anatomy.IsBlade(xRayEntry.WeaponClass);
		bool flag2 = xRayEntry != null && Anatomy.IsBlunt(xRayEntry.WeaponClass);
		Theme.Label((!num2) ? (flag2 ? "THIS BEATING" : (flag ? "THIS STRIKE" : "THIS SHOT")) : ((g.Shots.Count == 1) ? "THE SHOT" : "ALL SHOTS"), 0.078f, num + 0.007f, 0.25f);
		UiDraw.Rect(0.076f, num + 0.032f - 0.004f, 0.198f, 0.0015f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 200);
		_lx = 0.081f;
		_ly = num + 0.032f + 0.004f;
		if (num2)
		{
			string value = ((g.Shots.Count == 1) ? $"{g.Shots[0].Distance:0.0} m" : $"{g.Shots.Min((XRayEntry s) => s.Distance):0.0} - {g.Shots.Max((XRayEntry s) => s.Distance):0.0} m");
			string value2 = ((g.Shots.Count == 1) ? WhenText(g.Shots[0]) : $"{WhenText(g.Shots[0])}  ({(g.Last - g.Shots[0].Time).TotalSeconds:0} s)");
			Fact("WEAPONS", string.Join(", ", g.Shots.Select((XRayEntry s) => PrettyWeapon(s.WeaponName)).Distinct()));
			Fact("RANGE", value);
			Fact("WHERE", g.Shots[0].Location);
			Fact("WHEN", value2);
			string text = ManStatus(g);
			XRayEntry xRayEntry2 = g.Shots.LastOrDefault((XRayEntry s) => s.Effect != null);
			XRayEntry xRayEntry3 = g.Shots.LastOrDefault((XRayEntry s) => s.FatalShot);
			if (xRayEntry2 != null)
			{
				Fact("CONDITION", xRayEntry2.Effect, Theme.Warn);
			}
			else if (text == "KNOCKED OUT")
			{
				Fact("CONDITION", "Knocked out cold", Theme.Warn);
			}
			if (xRayEntry3 != null)
			{
				DamageItem damageItem = xRayEntry3.Analysis?.Main?.Damage.FirstOrDefault((DamageItem d) => !d.Chipped);
				string text2 = (Anatomy.IsBlunt(xRayEntry3.WeaponClass) ? "Beaten to death" : ((damageItem != null) ? Tidy(damageItem.Name) : "Blood loss"));
				if (text == "PRESUMED DEAD")
				{
					text2 = "Down with " + text2.ToLowerInvariant();
				}
				Fact((text == "PRESUMED DEAD") ? "LAST SEEN" : "CAUSE", text2, Theme.Blood);
			}
			return;
		}
		Fact("WEAPON", PrettyWeapon(xRayEntry.WeaponName));
		if (flag2)
		{
			Fact("BLOWS", BlowKinds(xRayEntry));
			if (xRayEntry.KnockedOut && !xRayEntry.Killed)
			{
				Fact("ENDED", "Knocked out cold", Theme.Warn);
			}
		}
		else
		{
			Fact(flag ? "STRIKE" : "ROUND", RoundName(xRayEntry.WeaponClass, Ammo.Of(xRayEntry)));
		}
		if (!flag2)
		{
			if (Anatomy.IsArea(xRayEntry.WeaponClass))
			{
				Fact("FROM IT", string.Format("{0:0.0} m  -  {1} {2} (est.)", xRayEntry.Distance, Math.Max(1, xRayEntry.Pellets), (xRayEntry.WeaponClass == WeaponClass.Burn) ? "burns" : "fragments"));
			}
			else if (xRayEntry.WeaponClass == WeaponClass.Shotgun)
			{
				Fact("RANGE", string.Format("{0:0.0} m  -  {1}", xRayEntry.Distance, (xRayEntry.Pellets > 1) ? $"{xRayEntry.Pellets} pellets ({xRayEntry.Pellets - 1} est.)" : "1 pellet"));
			}
			else
			{
				Fact("RANGE", $"{xRayEntry.Distance:0.0} m");
			}
		}
		Fact("WHEN", WhenText(xRayEntry, xRayEntry.GameMin <= 0));
		Fact("WHERE", xRayEntry.Location);
		if (xRayEntry.Effect != null)
		{
			Fact("EFFECT", xRayEntry.Effect, Theme.Warn);
		}
	}

	private static (int r, int g, int b) ShotChipColor(XRayEntry e)
	{
		List<DamageItem> list = e.Analysis?.AllDamage.Where((DamageItem d) => !d.Chipped).ToList();
		if (list == null || list.Count == 0)
		{
			return Theme.Dim;
		}
		return SeverityColor(list.Max((DamageItem d) => d.Severity));
	}

	private void DrawList(List<Group> groups)
	{
		Theme.PanelRect(0.07f, 0.18299998f, 0.21f, 0.464f);
		Theme.Label(_animals ? "ANIMALS" : "VICTIMS", 0.078f, 0.18999998f, 0.25f);
		UiDraw.Rect(0.076f, 0.211f, 0.198f, 0.0015f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 200);
		List<(Group, int)> list = Rows(groups);
		int num = list.FindIndex(((Group g, int shot) tuple7) => tuple7.g.Key == _groupKey && tuple7.shot == _shot);
		float num2 = 0.21499999f;
		float num3 = 0.64699996f - num2 - 0.022f;
		int num4 = Math.Max(4, Math.Min((int)(num3 / 0.034f), (UiDraw.TextSlotsLeft - 110) / 2));
		float num5 = Math.Max(0.034f, Math.Min(0.042f, num3 / (float)Math.Max(1, Math.Min(num4, Math.Max(list.Count, 8)))));
		float num6 = 1.18f * Math.Min(1.12f, num5 / 0.034f);
		if (num < _scroll)
		{
			_scroll = num;
		}
		if (num >= _scroll + num4)
		{
			_scroll = num - num4 + 1;
		}
		_scroll = Math.Max(0, Math.Min(_scroll, Math.Max(0, list.Count - num4)));
		float num7 = num2 + 0.002f;
		float num8 = -1f;
		float num9 = -1f;
		int num10 = _scroll;
		while (num10 < Math.Min(list.Count, _scroll + num4))
		{
			(Group, int) tuple = list[num10];
			Group item = tuple.Item1;
			int item2 = tuple.Item2;
			string gk = item.Key;
			int sh = item2;
			UiMouse.Region(0.073f, num7, 0.198f, num5, () => UiMouse.Moved && SelectRow(gk, sh), delegate
			{
				SelectRow(gk, sh);
			});
			bool flag = num10 == num;
			float num11 = num7 + (num5 - 0.002f) / 2f;
			if (item2 < 0)
			{
				(int, int, int) tuple2 = StatusColor(ManStatus(item));
				if (flag)
				{
					Theme.SelectFill(0.069875f, num7, 0.19812499f, num5 - 0.002f);
				}
				UiDraw.Rect(0.073f, num7, 0.003f, num5 - 0.002f, tuple2.Item1, tuple2.Item2, tuple2.Item3, 255);
				if (flag)
				{
					Theme.RedFrame(0.069875f, num7, 0.19812499f, num5 - 0.002f);
				}
				int r;
				int g;
				int b;
				if (flag)
				{
					(r, g, b) = Theme.SelectedText;
				}
				else if (item.NemesisId != null)
				{
					(r, g, b) = Theme.Gold;
				}
				else
				{
					(r, g, b) = Theme.Title;
				}
				string obj = ((item.NemesisId != null) ? "* " : "");
				float num12 = 0.25f * num6;
				UiDraw.Text(Fit(obj + item.Name, num12, 0.17799999f), 0.081f, num11 - num12 * 0.0375f, num12, r, g, b);
			}
			else
			{
				XRayEntry xRayEntry = item.Shots[item2];
				if (num8 < 0f)
				{
					num8 = num7;
				}
				num9 = num11;
				if (flag)
				{
					Theme.SelectBox(0.086f, num7 + 0.001f, 0.182f, num5 - 0.004f);
				}
				(int, int, int) tuple6 = ShotChipColor(xRayEntry);
				UiDraw.Rect(0.0825f, num11 - 0.0008f, 0.0035f, 0.0016f, Theme.Line.r, Theme.Line.g, Theme.Line.b, 220);
				UiDraw.Rect(0.091f, num11 - 0.005f, 0.004f, 0.01f, tuple6.Item1, tuple6.Item2, tuple6.Item3, 255);
				int num13 = (flag ? 245 : 200);
				float num14 = 0.22f * num6;
				string arg = Short(xRayEntry.Analysis?.Main?.EntryText ?? Anatomy.RegionOfBone(xRayEntry.RecordedBoneId));
				UiDraw.Text(Fit($"{item2 + 1}  {arg}", num14, 0.16399999f), 0.099f, num11 - num14 * 0.0375f, num14, num13, num13 - 5, num13 - 15);
			}
			num10++;
			num7 += num5;
		}
		if (num8 >= 0f)
		{
			UiDraw.Rect(0.0818f, num8 - 0.002f, 0.0014f, num9 - num8 + 0.002f, Theme.Line.r, Theme.Line.g, Theme.Line.b, 220);
		}
		Theme.ScrollBar(0.273f, num2 + 0.002f, (float)Math.Min(num4, list.Count) * num5, _scroll, num4, list.Count);
	}

	private void DrawXRay(Group g)
	{
		float num = 0.677f;
		UiDraw.Rect(0.286f, 0.18299998f, 0.335f, num, 5, 8, 11, 245);
		for (int i = 1; i < 6; i++)
		{
			UiDraw.Rect(0.286f, 0.18299998f + num * (float)i / 6f, 0.335f, 0.001f, 25, 40, 48, 140);
		}
		for (int j = 1; j < 4; j++)
		{
			UiDraw.Rect(0.286f + 0.335f * (float)j / 4f, 0.18299998f, 0.0007f, num, 25, 40, 48, 140);
		}
		UiDraw.Outline(0.286f, 0.18299998f, 0.335f, num, Theme.Line.r, Theme.Line.g, Theme.Line.b, 220);
		int num2 = (int)((float)UiDraw.GlyphsThisFrame * GlyphCost) + 25;
		UiMouse.DrawCursorNow();
		int num3 = Math.Max(350, (_rectLimit * 85 / 100 - UiDraw.RectsThisFrame - num2) / 50 * 50);
		if (_heldBudget > 0 && num3 >= _heldBudget - 40 && num3 <= _heldBudget + 100)
		{
			num3 = _heldBudget;
		}
		else
		{
			_heldBudget = num3;
		}
		int num4 = _renderer.Draw(0.29000002f, 0.21299998f, 0.32700002f, num - 0.06f, UiDraw.RealDt, UiDraw.RealMs, num3);
		string text = $"{g.Key}|{_shot}|{num3}|{_renderer.ActiveLevel}|{_renderer.LastCut > 0}";
		if (UiDraw.RealMs - _diagAt > 4000 && text != _diagFor)
		{
			_diagAt = UiDraw.RealMs;
			_diagFor = text;
			Diag?.Invoke($"XRAY frame: limit {_rectLimit}, before the skeleton {UiDraw.RectsThisFrame - num4} rects + {UiDraw.GlyphsThisFrame} letters (x{GlyphCost:0.0}) -> budget {num3}, drew {num4}, smoothness {_renderer.LastStepCap}, level {_renderer.ActiveLevel}, zoom {_renderer.Zoom:0.00}, cut {_renderer.LastCut}, plans so far {_renderer.PlanCount}");
		}
		if (DebugOn)
		{
			UiDraw.Text(string.Format("{0}/{1} rects   smoothness {2}{3}", num4, num3, _renderer.LastStepCap, (_renderer.LastDropped > 0) ? $"   {_renderer.LastDropped} extras skipped" : ""), 0.294f, 0.81399995f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		Theme.Label((_shot < 0) ? "ALL WOUNDS" : $"SHOT {_shot + 1} OF {g.Shots.Count}", 0.294f, 0.18899998f, 0.24f);
		if (DebugOn)
		{
			UiDraw.Text((_quality == XRayQuality.Auto) ? $"Auto detail: {_renderer.ActiveLevel}" : $"{_quality} detail", 0.5360001f, 0.18899998f, 0.21f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		}
		if (_composite.Paths.Any(((PathResult path, bool thin) p) => p.path.Estimated))
		{
			UiDraw.Text("thin / dimmed paths are estimated", 0.294f, 0.83599997f, 0.2f, 190, 160, 90);
		}
		UiDraw.Text(_renderer.AutoSpin ? "spinning" : "", 0.57600003f, 0.83599997f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
	}

	private void HandleMouse()
	{
		_renderer.PanX = (_renderer.PanY = 0f);
		if (!UiMouse.On)
		{
			_dragOrbit = false;
			return;
		}
		UiMouse.WheelHandler = delegate(int d)
		{
			_renderer.ZoomBy((d > 0) ? 1.15f : 0.86956525f);
		};
		if (UiMouse.Clicked && UiMouse.Over(0.286f, 0.18299998f, 0.335f, 0.677f))
		{
			_dragOrbit = true;
		}
		if (!UiMouse.Down)
		{
			_dragOrbit = false;
		}
		if (_dragOrbit && UiMouse.Moved)
		{
			_renderer.Orbit((0f - UiMouse.DX) * 300f, UiMouse.DY * 200f);
		}
	}

	private void DrawCalibration()
	{
		if (_calPage == 1)
		{
			DrawSpriteTest();
			return;
		}
		for (int i = 0; i < 40; i++)
		{
			for (int j = 0; j < 50; j++)
			{
				UiDraw.Rect(0.12f + (float)j * 0.0152f, 0.1f + (float)i * 0.02f, 0.01216f, 0.0139999995f, (i == _calRow) ? 235 : 110, (i == _calRow) ? 200 : 190, (i == _calRow) ? 90 : 110, 255);
			}
		}
		UiDraw.Text("PAGE 1/2  RECT LIMIT - find the FIRST row with any missing cell, put the arrows on the row ABOVE it, press Enter.", 0.03f, 0.035f, 0.3f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.Text("Backspace cancels.  Left/Right: page 2 (sprite test).", 0.62f, 0.065f, 0.24f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		UiDraw.Text($"Currently saved limit: {_rectLimit}.   Selected: {(_calRow + 1) * 50}", 0.03f, 0.065f, 0.26f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
		for (int k = 0; k < 40; k++)
		{
			bool num = k == _calRow;
			(int, int, int) tuple = (num ? Theme.Gold : Theme.Dim);
			UiDraw.Text($"{(k + 1) * 50}", 0.07f, 0.1f + (float)k * 0.02f - 0.003f, 0.22f, tuple.Item1, tuple.Item2, tuple.Item3);
			if (num)
			{
				UiDraw.Text(">>", 0.1f, 0.1f + (float)k * 0.02f - 0.004f, 0.25f, tuple.Item1, tuple.Item2, tuple.Item3);
				UiDraw.Text("<<", 0.885f, 0.1f + (float)k * 0.02f - 0.004f, 0.25f, tuple.Item1, tuple.Item2, tuple.Item3);
			}
		}
	}

	private bool SelectRow(string group, int shot)
	{
		if (group == _groupKey && shot == _shot)
		{
			return false;
		}
		if (group != _groupKey)
		{
			_renderer.ResetView();
		}
		_groupKey = group;
		_shot = shot;
		return true;
	}

	private void DrawSpriteTest()
	{
		bool flag = UiDraw.EnsureTextureDict("generic_textures");
		bool flag2 = UiDraw.EnsureTextureDict("feeds");
		UiDraw.Text("PAGE 2/2  SPRITE TEST - tell me which numbers show clean SOLID bars in columns A, B and C.  Left/Right: page 1.", 0.03f, 0.035f, 0.3f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.Text("textures loaded: generic_textures " + (flag ? "YES" : "no") + ", feeds " + (flag2 ? "YES" : "no"), 0.03f, 0.065f, 0.24f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
		float[] array = new float[3] { 0.32f, 0.56f, 0.8f };
		string[] array2 = new string[3] { "A: on its own", "B: over a dark box", "C: dark box + draw order" };
		for (int i = 0; i < 3; i++)
		{
			UiDraw.Text(array2[i], array[i] - 0.08f, 0.095f, 0.26f, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
		}
		for (int j = 0; j < SpriteCandidates.Length; j++)
		{
			float num = 0.16f + (float)j * 0.095f;
			UiDraw.Text($"{j + 1}  {SpriteCandidates[j][1]}", 0.03f, num - 0.012f, 0.25f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
			for (int k = 0; k < 3; k++)
			{
				if (k > 0)
				{
					UiDraw.Rect(array[k] - 0.09f, num - 0.04f, 0.18f, 0.08f, 25, 25, 30, 255);
				}
				if (k == 2)
				{
					UiDraw.SetDrawOrder(7);
				}
				float[] array3 = new float[3] { 0f, 45f, 90f };
				foreach (float num2 in array3)
				{
					float cx = array[k] + (num2 - 45f) / 45f * 0.05f;
					UiDraw.Sprite(SpriteCandidates[j][0], SpriteCandidates[j][1], cx, num, 0.045f, 0.008f, num2, 255, 255, 255, 255);
				}
				if (k == 2)
				{
					UiDraw.SetDrawOrder(4);
				}
			}
		}
	}

	private void FieldStart(float y)
	{
		_fx = 0.63900006f;
		_fvx = 0.712f;
		_fy = y;
	}

	private void Field(string label, string value, (int r, int g, int b)? color = null)
	{
		Theme.Label(label, _fx, _fy, 0.23f);
		(int, int, int) tuple = color ?? Theme.Text;
		UiDraw.Text(value, _fvx, _fy - 0.001f, 0.26f, tuple.Item1, tuple.Item2, tuple.Item3);
		_fy += 0.026f;
	}

	private void Section(string title)
	{
		_fy += 0.006f;
		UiDraw.Rect(_fx, _fy + 0.024f, 0.27899998f, 0.0015f, Theme.Line.r, Theme.Line.g, Theme.Line.b, 200);
		Theme.Label(title, 0.7785f - Theme.CapsWidth(title, 0.27f) / 2f - 0.0008f, _fy, 0.27f);
		_fy += 0.032f;
	}

	private static string Fit(string text, float scale, float width)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		int num = Math.Max(4, (int)(width / (scale * 0.0175f)));
		if (text.Length > num)
		{
			return text.Substring(0, num - 3).TrimEnd(' ', ',', '-') + "...";
		}
		return text;
	}

	private void Banner(string text, (int r, int g, int b) c, string right = null)
	{
		UiDraw.Rect(_fx, _fy, RowW, 0.038f, c.r / 4, c.g / 4, c.b / 4, 220);
		UiDraw.Rect(_fx, _fy, 0.004f, 0.038f, c.r, c.g, c.b, 255);
		if (right != null)
		{
			UiDraw.TextFont(text, UiDraw.TitleFont, _fx + 0.012f, _fy + 0.004f, 0.38f, c.r, c.g, c.b);
			UiDraw.Text(right, _fx + RowW - 0.01f - Theme.MixedWidth(right, 0.25f, 0.0222f), _fy + 0.01f, 0.25f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		}
		else
		{
			UiDraw.TextFont(text, UiDraw.TitleFont, _fx + RowW / 2f - Theme.CapsWidth(text, 0.38f) / 2f - 0.0008f, _fy + 0.004f, 0.38f, c.r, c.g, c.b);
		}
		_fy += 0.048f;
	}

	private void Pair(string l1, string v1, string l2, string v2, (int r, int g, int b)? c2 = null)
	{
		float num = RowW / 2f;
		Theme.Label(l1, _fx, _fy, 0.2f);
		UiDraw.Text(Fit(v1, 0.25f, (l2 != null) ? (num - 0.008f) : RowW), _fx, _fy + 0.015f, 0.25f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		if (l2 != null)
		{
			(int, int, int) tuple = c2 ?? Theme.Text;
			Theme.Label(l2, _fx + num, _fy, 0.2f);
			UiDraw.Text(Fit(v2, 0.25f, num), _fx + num, _fy + 0.015f, 0.25f, tuple.Item1, tuple.Item2, tuple.Item3);
		}
		_fy += 0.042f;
	}

	private void WoundTrack(PathResult m, bool blade)
	{
		Section(blade ? "WOUND TRACK" : "BULLET PATH");
		float num = (m.End - m.Entry).Length() * 100f;
		float markW = 0.008f / UiDraw.Aspect * 1.6f;
		float fy = _fy;
		Mark(255, 255, 255);
		Theme.Label(blade ? "IN" : "ENTRY", _fx + 0.012f, _fy, 0.2f);
		UiDraw.Text(Fit(Capitalize(m.EntryText) + (m.Estimated ? "  (est.)" : ""), 0.24f, RowW - 0.06f), _fx + 0.06f, _fy - 0.001f, 0.24f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		_fy += 0.022f;
		UiDraw.Text(string.Format("{0:0} cm through the body{1}", num, (!m.Exited) ? "" : (blade ? ", cut clean through" : ", over-penetrated")), _fx + 0.012f, _fy - 0.004f, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		_fy += 0.016f;
		float num2 = 0.0010416667f;
		float num3 = fy + 0.004f + 0.012f + 0.003f;
		float num4 = _fy + 0.004f - 0.003f;
		UiDraw.Rect(_fx + markW / 2f - num2 / 2f, num3, num2, num4 - num3, Theme.Line.r, Theme.Line.g, Theme.Line.b, 220);
		if (m.Exited)
		{
			Mark(240, 140, 50);
		}
		else
		{
			Mark(255, 215, 70);
		}
		Theme.Label(m.Exited ? "EXIT" : (blade ? "STOP" : "LODGED"), _fx + 0.012f, _fy, 0.2f);
		UiDraw.Text(Fit(Capitalize(m.EndText.Replace("Exited ", "").Replace("Lodged in ", "").Replace("Blade stopped in ", "")), 0.24f, RowW - 0.06f), _fx + 0.06f, _fy - 0.001f, 0.24f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
		_fy += 0.026f;
		void Mark(int r, int g, int b)
		{
			UiDraw.Rect(_fx, _fy + 0.004f, markW, 0.012f, r, g, b, 255);
		}
	}

	private static string BlowKinds(XRayEntry e)
	{
		List<Blow> list = e.Blows ?? new List<Blow>();
		if (list.Count == 0)
		{
			return $"{Math.Max(1, e.Pellets)}";
		}
		int num = list.Count((Blow x) => x.Stomp);
		List<string> list2 = new string[4]
		{
			Part(list.Count((Blow x) => x.Kind == BlowKind.Punch), "punch", "punches"),
			(num > 0) ? (Part(list.Count((Blow x) => x.Kind == BlowKind.Kick), "kick", "kicks") + " (" + Part(num, "stomp", "stomps") + ")") : Part(list.Count((Blow x) => x.Kind == BlowKind.Kick), "kick", "kicks"),
			Part(list.Count((Blow x) => x.Kind == BlowKind.Gun), "gun blow", "gun blows"),
			Part(list.Count((Blow x) => x.Kind == BlowKind.Choke), "choke", "chokes")
		}.Where((string p) => p != null).ToList();
		if (list2.Count != 1)
		{
			return string.Format("{0}: {1}", list.Count, string.Join(", ", list2));
		}
		return list2[0];
		static string Part(int n, string one, string many)
		{
			if (n != 0)
			{
				return $"{n} {((n == 1) ? one : many)}";
			}
			return null;
		}
	}

	private void BlowsByRegion(XRayEntry e, XRayAnalysis a)
	{
		Section("BLOWS");
		float w = 0.008f / UiDraw.Aspect * 1.6f;
		List<Blow> source = e.Blows ?? new List<Blow>();
		(BlowKind, string, (int, int, int))[] array = new(BlowKind, string, (int, int, int))[4]
		{
			(BlowKind.Punch, "Punches", PunchColor),
			(BlowKind.Kick, "Kicks", KickColor),
			(BlowKind.Gun, "With the gun", GunColor),
			(BlowKind.Choke, "Choked", ChokeColor)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(BlowKind, string, (int, int, int)) tuple = array[i];
			BlowKind kind = tuple.Item1;
			string item = tuple.Item2;
			(int, int, int) item2 = tuple.Item3;
			int num = source.Count((Blow x) => x.Kind == kind);
			if (num != 0)
			{
				UiDraw.Rect(_fx, _fy + 0.004f, w, 0.012f, item2.Item1, item2.Item2, item2.Item3, 255);
				int num2 = ((kind == BlowKind.Kick) ? source.Count((Blow x) => x.Stomp) : 0);
				UiDraw.Text((num2 > 0) ? string.Format("{0} ({1} {2})", item, num2, (num2 == 1) ? "stomp" : "stomps") : item, _fx + 0.012f, _fy - 0.001f, 0.24f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
				string text = $"{num}";
				UiDraw.Text(text, _fx + RowW - Theme.MixedWidth(text, 0.22f, 0.0222f), _fy, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
				_fy += 0.022f;
			}
		}
		_fy += 0.004f;
		Theme.Label("WHERE", _fx, _fy, 0.2f);
		_fy += 0.02f;
		foreach (var (s, num3) in a.Blows.Take(5))
		{
			UiDraw.Rect(_fx, _fy + 0.004f, w, 0.012f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b, 255);
			UiDraw.Text(Capitalize(s), _fx + 0.012f, _fy - 0.001f, 0.24f, Theme.Text.r, Theme.Text.g, Theme.Text.b);
			string text2 = ((num3 == 1) ? "1 blow" : $"{num3} blows");
			UiDraw.Text(text2, _fx + RowW - Theme.MixedWidth(text2, 0.22f, 0.0222f), _fy, 0.22f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			_fy += 0.022f;
		}
		_fy += 0.006f;
	}

	private void TraumaMeter(List<DamageItem> dmg)
	{
		dmg = dmg.Where((DamageItem d) => d.Key != "verdict").ToList();
		int num = Math.Min(100, dmg.Where((DamageItem d) => !d.Chipped).Sum((DamageItem d) => SeverityWeight(d.Severity)) + dmg.Count((DamageItem d) => d.Chipped) * 2);
		(int, int, int) tuple = SeverityColor((from d in dmg
			where !d.Chipped
			select d.Severity).DefaultIfEmpty(Severity.Minor).Max());
		Theme.Label("TRAUMA", _fx, _fy, 0.23f);
		float num2 = _fx + 0.066f;
		float num3 = RowW - 0.066f - 0.048f;
		UiDraw.Rect(num2, _fy + 0.005f, num3, 0.011f, 40, 34, 28, 230);
		UiDraw.Rect(num2, _fy + 0.005f, num3 * (float)num / 100f, 0.011f, tuple.Item1, tuple.Item2, tuple.Item3, 255);
		UiDraw.Text($"{num}", num2 + num3 + 0.008f, _fy - 0.001f, 0.27f, tuple.Item1, tuple.Item2, tuple.Item3);
		_fy += 0.03f;
	}

	private static int SeverityWeight(Severity s)
	{
		return s switch
		{
			Severity.Moderate => 8, 
			Severity.Severe => 18, 
			Severity.Critical => 35, 
			Severity.Fatal => 100, 
			_ => 3, 
		};
	}

	private static string SeverityTag(DamageItem d)
	{
		if (!d.Chipped)
		{
			if (d.Severity != Severity.Fatal)
			{
				if (d.Severity != Severity.Critical)
				{
					if (d.Severity != Severity.Severe)
					{
						if (d.Severity != Severity.Moderate)
						{
							return "MINOR";
						}
						return "MODERATE";
					}
					return "SEVERE";
				}
				return "CRITICAL";
			}
			return "FATAL";
		}
		if (!d.Name.Contains("NICKED"))
		{
			return "CHIPPED";
		}
		return "NICKED";
	}

	private static bool IsVessel(string key)
	{
		if (!key.StartsWith("carotid") && !key.StartsWith("femoral"))
		{
			return key == "windpipe";
		}
		return true;
	}

	private void InjuryList(IEnumerable<DamageItem> items, int max, bool notes)
	{
		List<DamageItem> list = items.ToList();
		if (list.Count == 0)
		{
			Theme.Body("Flesh wound only - nothing vital hit", _fx, _fy, 0.23f);
			_fy += 0.024f;
			return;
		}
		(string, List<DamageItem>)[] obj = new(string, List<DamageItem>)[5]
		{
			("HOW IT WENT DOWN", list.Where((DamageItem d) => d.Key == "verdict").ToList()),
			("SKELETAL", list.Where((DamageItem d) => d.IsBone).ToList()),
			("ORGANS", list.Where((DamageItem d) => !d.IsBone && !IsVessel(d.Key) && d.Key != "flesh" && d.Key != "verdict").ToList()),
			("VESSELS & AIRWAY", list.Where((DamageItem d) => !d.IsBone && IsVessel(d.Key)).ToList()),
			("SOFT TISSUE", list.Where((DamageItem d) => d.Key == "flesh").ToList())
		};
		int num = 0;
		(string, List<DamageItem>)[] array = obj;
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			var (arg, list2) = array[num2];
			if (list2.Count == 0 || num >= max)
			{
				continue;
			}
			Theme.Label($"{arg}  ({list2.Count})", _fx, _fy, 0.22f);
			_fy += 0.023f;
			foreach (DamageItem item in list2.OrderByDescending((DamageItem x) => (int)((!x.Chipped) ? x.Severity : ((Severity)(-1)))))
			{
				if (num >= max || UiDraw.TextSlotsLeft < 10 || _fy > 0.81499994f)
				{
					max = num;
					break;
				}
				num++;
				(int, int, int) tuple2 = (item.Chipped ? Theme.Dim : SeverityColor(item.Severity));
				UiDraw.Rect(_fx, _fy + 0.006f, 0.005f, 0.014f, tuple2.Item1, tuple2.Item2, tuple2.Item3, 255);
				string text = SeverityTag(item);
				float num3 = Theme.CapsWidth(text, 0.235f);
				UiDraw.Text(Fit(Tidy(item.Name), 0.275f, RowW - 0.022f - num3), _fx + 0.011f, _fy, 0.275f, item.Chipped ? Theme.Dim.r : Theme.Text.r, item.Chipped ? Theme.Dim.g : Theme.Text.g, item.Chipped ? Theme.Dim.b : Theme.Text.b);
				UiDraw.TextFont(text, UiDraw.TitleFont, _fx + RowW - num3 - 0.0008f, _fy + 0.003f, 0.235f, tuple2.Item1, tuple2.Item2, tuple2.Item3);
				_fy += 0.025f;
				string text2 = (notes ? Consequence(item) : null);
				if (text2 != null)
				{
					UiDraw.Text(text2, _fx + 0.009f, _fy - 0.002f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
					_fy += 0.016f;
				}
			}
			_fy += 0.004f;
		}
		if (list.Count > num)
		{
			UiDraw.Text($"+{list.Count - num} more", _fx, _fy, 0.2f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			_fy += 0.02f;
		}
	}

	private static string Consequence(DamageItem d)
	{
		if (d.Chipped)
		{
			if (!d.Key.StartsWith("spine") && !(d.Key == "cervical"))
			{
				return "Bone bruised, not broken - still works";
			}
			return "Bone bruised; cord intact - he can still move";
		}
		return ((d.Key.Length > 2 && d.Key[d.Key.Length - 2] == '_') ? d.Key.Substring(0, d.Key.Length - 2) : d.Key) switch
		{
			"brain" => "Death is instant", 
			"heart" => "Heart stops within seconds", 
			"carotid" => "Bleeds out in under a minute", 
			"femoral" => "Bleeds out in minutes without a tourniquet", 
			"windpipe" => "Choking on his own blood", 
			"lung" => "Lung collapses - drowning in blood", 
			"liver" => "Heavy internal bleeding", 
			"kidney" => "Internal bleeding, slow decline", 
			"stomach" => "Gut wound - infection, agonising death", 
			"intestines" => "Gut wound - sepsis within days", 
			"bladder" => "Painful, rarely fatal", 
			"skull" => "Concussion, likely brain damage", 
			"jaw" => "Can't speak or eat properly", 
			"cervical" => "Paralysed from the neck down", 
			"spine" => "Paralysed below the wound", 
			"ribs" => "Every breath hurts", 
			"sternum" => "Crushed chest, laboured breathing", 
			"scapula" => "Shoulder weak, arm hard to raise", 
			"clavicle" => "Can't raise the arm", 
			"humerus" => "Arm useless", 
			"forearm" => "Can't hold a weapon in that hand", 
			"hand" => "Can't grip", 
			"pelvis" => "Can't stand", 
			"femur" => "Leg can't bear weight", 
			"knee" => "Leg locked - can't stand", 
			"tibia" => "Can't put weight on the leg", 
			"foot" => "Walks with a limp", 
			"flesh" => "Bleeds, but nothing vital", 
			_ => null, 
		};
	}

	private void InfoHeader(string name, string sub, bool nemesis)
	{
		Theme.PanelRect(0.62700003f, 0.18299998f, 0.30299997f, 0.677f);
		(int, int, int) tuple = (nemesis ? Theme.Gold : Theme.Title);
		string text = name.ToUpperInvariant();
		float num = 0.44f;
		while (num > 0.3f && Theme.CapsWidth(text, num) > 0.27299997f)
		{
			num -= 0.02f;
		}
		float num2 = 0.7785f;
		UiDraw.TextFont(text, UiDraw.TitleFont, num2 - Theme.CapsWidth(text, num) / 2f - 0.0008f, 0.19099998f, num, tuple.Item1, tuple.Item2, tuple.Item3);
		sub = Fit(sub, 0.25f, 0.27299997f);
		UiDraw.Text(sub, num2 - Theme.MixedWidth(sub, 0.25f, 0.0222f) / 2f, 0.22899999f, 0.25f, Theme.Subtitle.r, Theme.Subtitle.g, Theme.Subtitle.b);
		FieldStart(0.26299998f);
	}

	private static string ManSub(Group g)
	{
		if (g.NemesisId == null)
		{
			if (g.FollowerOf == null)
			{
				return g.Sub;
			}
			return "Rode with " + g.FollowerOf;
		}
		return "Nemesis  -  " + g.Sub;
	}

	private void DrawManSummary(Group g)
	{
		InfoHeader(g.Name, ManSub(g), g.NemesisId != null);
		string text = ManStatus(g);
		g.Shots.Sum((XRayEntry s) => Math.Max(1, s.Pellets));
		g.Shots.Any((XRayEntry s) => s.WeaponClass == WeaponClass.Shotgun);
		Banner(text, StatusColor(text));
		List<DamageItem> list = _composite.Damage.ToList();
		Section("ALL INJURIES");
		TraumaMeter(list);
		InjuryList(list, 16, notes: false);
	}

	private void DrawShot(Group g, int index)
	{
		XRayEntry xRayEntry = g.Shots[index];
		XRayAnalysis analysis = xRayEntry.Analysis;
		bool blade = Anatomy.IsBlade(xRayEntry.WeaponClass);
		InfoHeader(g.Name, ManSub(g), g.NemesisId != null);
		string text = ShotVerdict(xRayEntry);
		Banner(text, StatusColor(text));
		if (analysis?.Main == null)
		{
			Section("WOUND");
			Theme.Body("Grazed " + Anatomy.RegionOfBone(xRayEntry.RecordedBoneId) + ".", _fx, _fy, 0.25f);
			return;
		}
		if (Anatomy.IsBlunt(xRayEntry.WeaponClass))
		{
			BlowsByRegion(xRayEntry, analysis);
		}
		else
		{
			WoundTrack(analysis.Main, blade);
		}
		List<DamageItem> list = analysis.AllDamage.ToList();
		Section("DAMAGE");
		TraumaMeter(list);
		InjuryList(list, 11, notes: false);
	}

	private string ManStatus(Group g)
	{
		if (g.NemesisId != null)
		{
			switch (_fateOf(g.NemesisId))
			{
			case NemesisFate.PresumedDead:
				return "PRESUMED DEAD";
			case NemesisFate.Dead:
				return "DEAD";
			case NemesisFate.CheatedDeath:
				if (!g.Shots.Any((XRayEntry s) => s.FatalShot))
				{
					return "HUNTING YOU";
				}
				return "CHEATED DEATH";
			case NemesisFate.Hunting:
				if (!g.Shots.Any((XRayEntry s) => s.FatalShot))
				{
					return "HUNTING YOU";
				}
				return "CHEATED DEATH";
			}
		}
		if (!g.Shots.Any((XRayEntry s) => s.Killed))
		{
			if (!g.Shots.Any((XRayEntry s) => s.KnockedOut))
			{
				return "SURVIVED";
			}
			return "KNOCKED OUT";
		}
		return "KILLED";
	}

	private string ShotVerdict(XRayEntry e)
	{
		if (e.NemesisId != null && (e.FatalShot || e.Killed))
		{
			NemesisFate nemesisFate = _fateOf(e.NemesisId);
			if (e.FatalShot && nemesisFate == NemesisFate.PresumedDead)
			{
				return "PRESUMED DEAD";
			}
			if (nemesisFate == NemesisFate.CheatedDeath || nemesisFate == NemesisFate.Hunting)
			{
				if (!e.FatalShot)
				{
					return "SURVIVED";
				}
				return "HE SURVIVED THIS";
			}
		}
		if (Anatomy.IsBlunt(e.WeaponClass))
		{
			if (e.Blows != null && e.Blows.Count > 0 && e.Blows.All((Blow b) => b.Choke))
			{
				if (!e.FatalShot)
				{
					if (!e.Killed)
					{
						if (!e.KnockedOut)
						{
							return "CHOKED";
						}
						return "CHOKED OUT";
					}
					return "DIED LATER";
				}
				return "STRANGLED";
			}
			if (e.LeftForDead)
			{
				return "STOMPED TO DEATH";
			}
			if (!e.FatalShot)
			{
				if (!e.Killed)
				{
					if (!e.KnockedOut)
					{
						return "BEATEN";
					}
					return "KNOCKED OUT";
				}
				return "DIED LATER";
			}
			return "BEATEN TO DEATH";
		}
		if (!e.FatalShot)
		{
			if (!e.Killed)
			{
				return "SURVIVED";
			}
			return "DIED LATER";
		}
		return "KILLING SHOT";
	}

	private static (int r, int g, int b) StatusColor(string status)
	{
		switch (status)
		{
		case "STOMPED TO DEATH":
		case "KILLING SHOT":
		case "KILLED":
		case "DEAD":
		case "BEATEN TO DEATH":
			return Theme.Blood;
		case "PRESUMED DEAD":
		case "KNOCKED OUT":
		case "DIED LATER":
			return Theme.Warn;
		case "HE SURVIVED THIS":
		case "CHEATED DEATH":
		case "HUNTING YOU":
			return Theme.Gold;
		default:
			return Theme.Good;
		}
	}

	private static (int r, int g, int b) SeverityColor(Severity s)
	{
		return s switch
		{
			Severity.Fatal => (r: 255, g: 75, b: 60), 
			Severity.Critical => (r: 240, g: 120, b: 60), 
			Severity.Severe => (r: 230, g: 175, b: 75), 
			Severity.Moderate => (r: 220, g: 210, b: 160), 
			_ => (r: 190, g: 185, b: 175), 
		};
	}

	private static string RoundName(WeaponClass w, AmmoKind a)
	{
		string text = Round(w);
		switch (a)
		{
		case AmmoKind.Slug:
			return "Shotgun Slug";
		case AmmoKind.Incendiary:
			return "Incendiary Buckshot";
		case AmmoKind.MachineGun:
			return "Machine-gun round";
		default:
		{
			string text2 = Ammo.Label(a);
			if (text2 == null)
			{
				return text;
			}
			if (text2.EndsWith("arrow"))
			{
				return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text2);
			}
			return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text2) + " " + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
		}
		}
	}

	private static string Round(WeaponClass w)
	{
		return w switch
		{
			WeaponClass.Pistol => "Pistol cartridge", 
			WeaponClass.Revolver => "Revolver cartridge", 
			WeaponClass.Repeater => "Repeater cartridge", 
			WeaponClass.Rifle => "Rifle cartridge", 
			WeaponClass.Sniper => "High-velocity rifle round", 
			WeaponClass.Shotgun => "Buckshot", 
			WeaponClass.Stab => "Blade - thrust", 
			WeaponClass.Slash => "Blade - slash", 
			WeaponClass.Chop => "Heavy blade - chop", 
			WeaponClass.Blast => "Explosion - shrapnel", 
			WeaponClass.Burn => "Fire", 
			WeaponClass.Blunt => "Fists", 
			_ => "Arrow", 
		};
	}

	internal static string PrettyWeapon(string enumName)
	{
		if (string.IsNullOrEmpty(enumName) || enumName.StartsWith("0x"))
		{
			return "Unknown weapon";
		}
		List<string> list = (from Match m in Regex.Matches(enumName, "[A-Z][a-z0-9]*")
			select m.Value).ToList();
		if (list.Count > 1 && list[0] == "Turret")
		{
			string text = string.Join(" ", list.Skip(1));
			if (!text.EndsWith("Cannon"))
			{
				return text + " gun";
			}
			return Tidy(text);
		}
		if (list.Count >= 2)
		{
			return string.Join(" ", list.Skip(1)) + " " + list[0];
		}
		return enumName;
	}

	private static string Tidy(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
		}
		return s;
	}

	private static string Capitalize(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return char.ToUpperInvariant(s[0]) + s.Substring(1);
		}
		return s;
	}

	private static string Short(string region)
	{
		return (region ?? "").Replace("the front of the ", "").Replace("the ", "");
	}

	private void DrawFooter()
	{
		if (_confirmClear)
		{
			Theme.Footer(0.868f, "Press Delete again to erase the whole X-ray log", warn: true);
		}
		else if (XPad.RecentlyUsed)
		{
			Theme.Footer(0.868f, "D-pad  Navigate     LB/RB  Humans / Animals     Right stick  Rotate     LT/RT  Zoom     Y  Spin     RS  Reset     X  Arthur     B  Close");
		}
		else
		{
			Theme.Footer(0.868f, "Up/Down  Navigate     Q/E  Humans / Animals     A/D W/S  Rotate     Z/X  Zoom     Space  Spin     R  Reset" + (DebugOn ? "     F  Detail     K  Calibrate     Del  Clear" : "") + "     Backspace  Close");
		}
	}
}
