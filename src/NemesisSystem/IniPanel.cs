using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NemesisSystem;

internal sealed class IniPanel
{
	private sealed class Entry
	{
		public string Key;

		public string Value;

		public string Help;

		public int Line;
	}

	private sealed class Section
	{
		public string Name;

		public readonly List<Entry> Entries = new List<Entry>();
	}

	private readonly string _path;

	private readonly Action _reload;

	private List<string> _lines = new List<string>();

	private List<Section> _sections = new List<Section>();

	private int _sec;

	private int _row;

	private int _scroll;

	private string _note = "";

	private static readonly HashSet<string> Bindable = new HashSet<string> { "RosterMenu", "ForceAmbush", "Interrogate", "Telegraph" };

	private Entry _binding;

	internal static bool Dev;

	private int _clickRow = -1;

	public bool IsOpen { get; private set; }

	public bool Capturing
	{
		get
		{
			if (IsOpen)
			{
				return _binding != null;
			}
			return false;
		}
	}

	public IniPanel(string path, Action reload)
	{
		_path = path;
		_reload = reload;
	}

	public void Toggle()
	{
		IsOpen = !IsOpen;
		_binding = null;
		if (IsOpen)
		{
			Read();
			_note = "";
		}
	}

	public void Capture(Keys code, Keys mods)
	{
		if (_binding == null || code == Keys.ShiftKey || code == Keys.ControlKey || code == Keys.Menu || code == Keys.LShiftKey || code == Keys.RShiftKey || code == Keys.LControlKey || code == Keys.RControlKey || code == Keys.LMenu || code == Keys.RMenu)
		{
			return;
		}
		Entry binding = _binding;
		if (code == Keys.Escape || code == Keys.Back)
		{
			_binding = null;
			_note = binding.Key + ": unchanged";
			return;
		}
		bool flag = binding.Key == "RosterMenu";
		string text = KeyCombo.Problem(binding.Key, flag ? (code | mods) : code);
		if (text != null)
		{
			_note = text;
			return;
		}
		if (!flag && mods != Keys.None)
		{
			_note = $"{binding.Key} takes one key - {code} it is";
		}
		string nv = (flag ? KeyCombo.Name(code | mods) : code.ToString());
		_binding = null;
		Write(_sections[_sec], binding, nv);
	}

	private void Write(Section sec, Entry e, string nv)
	{
		try
		{
			List<string> list = File.ReadAllLines(_path).ToList();
			int num = FindLine(list, sec.Name, e.Key);
			if (num < 0)
			{
				_note = e.Key + " moved - reopen the panel";
				return;
			}
			list[num] = e.Key + "=" + nv;
			SafeFile.Write(_path, string.Join("\r\n", list) + "\r\n");
			_reload();
			int sec2 = _sec;
			int row = _row;
			Read();
			_sec = Math.Min(sec2, _sections.Count - 1);
			_row = Math.Min(row, _sections[_sec].Entries.Count - 1);
			Entry entry = _sections[_sec].Entries[_row];
			_note = ((entry.Value == nv) ? (e.Key + " = " + nv + "  (applied)") : (e.Key + ": the mod kept " + entry.Value + " (out of range?)"));
		}
		catch (Exception ex)
		{
			_note = "could not save: " + ex.Message;
		}
	}

	private void Read()
	{
		try
		{
			_lines = File.ReadAllLines(_path).ToList();
		}
		catch
		{
			_lines = new List<string>();
		}
		_sections = new List<Section>();
		Section section = null;
		List<string> list = new List<string>();
		string help = "";
		for (int i = 0; i < _lines.Count; i++)
		{
			string text = _lines[i].Trim();
			if (text.StartsWith("[") && text.EndsWith("]"))
			{
				section = new Section
				{
					Name = text.Substring(1, text.Length - 2)
				};
				_sections.Add(section);
				list.Clear();
				help = "";
				continue;
			}
			if (text.StartsWith(";"))
			{
				list.Add(text.TrimStart(';').Trim());
				continue;
			}
			int num = text.IndexOf('=');
			if (section == null || num <= 0)
			{
				if (text.Length == 0)
				{
					list.Clear();
					help = "";
				}
				continue;
			}
			if (list.Count > 0)
			{
				help = string.Join(" ", list);
			}
			if (Internal(section.Name, text.Substring(0, num).Trim()))
			{
				list.Clear();
				continue;
			}
			section.Entries.Add(new Entry
			{
				Key = text.Substring(0, num).Trim(),
				Value = text.Substring(num + 1).Trim(),
				Help = help,
				Line = i
			});
			list.Clear();
		}
		_sections.RemoveAll((Section s) => s.Entries.Count == 0);
		_sec = Math.Max(0, Math.Min(_sec, _sections.Count - 1));
		_row = 0;
		_scroll = 0;
	}

	private static string Step(string v, int dir, bool big)
	{
		if (bool.TryParse(v, out var result))
		{
			return (!result).ToString();
		}
		if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
		{
			int num = Math.Abs(result2);
			int num2 = ((num >= 1000) ? 50 : ((num >= 200) ? 10 : ((num < 50) ? 1 : 5)));
			return (result2 + dir * num2 * ((!big) ? 1 : 10)).ToString(CultureInfo.InvariantCulture);
		}
		if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
		{
			float num3 = Math.Abs(result3);
			float num4 = ((num3 >= 100f) ? 5f : ((num3 >= 10f) ? 0.5f : ((num3 >= 2f) ? 0.1f : 0.05f)));
			return ((float)Math.Round(result3 + (float)dir * num4 * (float)((!big) ? 1 : 10), 3)).ToString("0.###", CultureInfo.InvariantCulture);
		}
		return null;
	}

	private static int FindLine(List<string> lines, string section, string key)
	{
		bool flag = false;
		for (int i = 0; i < lines.Count; i++)
		{
			string text = lines[i].Trim();
			if (text.StartsWith("[") && text.EndsWith("]"))
			{
				flag = text.Substring(1, text.Length - 2) == section;
			}
			else if (flag && text.StartsWith(key + "="))
			{
				return i;
			}
		}
		return -1;
	}

	private static bool Internal(string section, string key)
	{
		if (!(section == "Meta") && !key.EndsWith("Day"))
		{
			switch (key)
			{
			default:
				if (section == "Notoriety")
				{
					break;
				}
				if (!Dev)
				{
					if (!(section == "Debug") && !(key == "Music") && !(key == "GameFeed"))
					{
						if (section == "Encounters")
						{
							return key == "Force";
						}
						return false;
					}
					return true;
				}
				return false;
			case "Seeded":
			case "Cleared":
			case "GangCamps":
			case "ArmyTopUp":
				break;
			}
		}
		return true;
	}

	public bool HandleKey(Keys key, bool shift)
	{
		if (!IsOpen)
		{
			return false;
		}
		if (_binding != null)
		{
			_note = _binding.Key + ": unchanged";
			_binding = null;
			return true;
		}
		if (_sections.Count == 0)
		{
			if (key == Keys.Back)
			{
				IsOpen = false;
			}
			return true;
		}
		Section section = _sections[_sec];
		key = key switch
		{
			Keys.D => Keys.Right, 
			Keys.A => Keys.Left, 
			Keys.S => Keys.Down, 
			Keys.W => Keys.Up, 
			_ => key, 
		};
		switch (key)
		{
		case Keys.Up:
			_row = Math.Max(0, _row - 1);
			break;
		case Keys.Down:
			_row = Math.Min(section.Entries.Count - 1, _row + 1);
			break;
		case Keys.Prior:
		case Keys.Q:
			_sec = (_sec + _sections.Count - 1) % _sections.Count;
			_row = 0;
			_scroll = 0;
			break;
		case Keys.Next:
		case Keys.E:
			_sec = (_sec + 1) % _sections.Count;
			_row = 0;
			_scroll = 0;
			break;
		case Keys.Return:
		{
			Entry entry2 = section.Entries[_row];
			if (section.Name == "Keys" && Bindable.Contains(entry2.Key))
			{
				_binding = entry2;
				_note = "Press the new key for " + entry2.Key + ((entry2.Key == "RosterMenu") ? " (a combination works: hold Shift / Ctrl / Alt)" : "") + " - Escape cancels";
			}
			else
			{
				_note = "Enter changes keys ([Keys] section) - Left / Right change the rest";
			}
			break;
		}
		case Keys.Left:
		case Keys.Right:
		{
			Entry entry = section.Entries[_row];
			string text = Step(entry.Value, (key == Keys.Right) ? 1 : (-1), shift);
			if (text == null)
			{
				_note = ((section.Name == "Keys" && Bindable.Contains(entry.Key)) ? ("Enter, then press the new key for " + entry.Key) : (entry.Key + " is text - edit it in NemesisMod.ini"));
			}
			else
			{
				Write(section, entry, text);
			}
			break;
		}
		case Keys.Back:
		case Keys.Escape:
			IsOpen = false;
			break;
		default:
			return true;
		}
		return true;
	}

	public void Draw()
	{
		if (!IsOpen)
		{
			return;
		}
		int num = 22;
		float num2 = 0.09f + (float)num * 0.024f + 0.09f;
		Theme.PanelRect(0.3f, 0.1f, 0.4f, num2, 240);
		UiDraw.TextFont("ALL SETTINGS", UiDraw.TitleFont, 0.312f, 0.108f, 0.32f, Theme.Title.r, Theme.Title.g, Theme.Title.b);
		UiDraw.Text("NemesisMod.ini - changes apply at once", 0.312f, 0.14f, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		if (_sections.Count == 0)
		{
			UiDraw.Text("Could not read NemesisMod.ini.", 0.312f, 0.17f, 0.2f, Theme.Blood.r, Theme.Blood.g, Theme.Blood.b);
			return;
		}
		Section section = _sections[_sec];
		float num3 = 0.24f;
		float num4 = 0.16f;
		float num5 = 0.016f;
		float num6 = num5 / UiDraw.Aspect;
		float cy = num4 + num3 * 0.04f;
		float num7 = 0.312f;
		Theme.MenuArrow(Theme.ArrowDir.Left, num7 + num6 / 2f, cy, num5, Theme.Gold);
		float num8 = num7 + num6 + 0.006f;
		UiDraw.TextFont(section.Name, UiDraw.TitleFont, num8, num4, num3, Theme.Gold.r, Theme.Gold.g, Theme.Gold.b);
		float num9 = Theme.TitleWidth(section.Name, num3);
		float num10 = num8 + num9 + 0.006f;
		Theme.MenuArrow(Theme.ArrowDir.Right, num10 + num6 / 2f, cy, num5, Theme.Gold);
		UiMouse.KeyRegion(num7 - 0.002f, num4, num6 + 0.004f, 0.022f, Keys.Q);
		UiMouse.KeyRegion(num10 - 0.002f, num4, num6 + 0.004f, 0.022f, Keys.E);
		UiDraw.Text($"{_sec + 1} / {_sections.Count}", num10 + num6 + 0.012f, num4 + 0.004f, 0.19f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		string name = _sections[(_sec + _sections.Count - 1) % _sections.Count].Name;
		string name2 = _sections[(_sec + 1) % _sections.Count].Name;
		string text = "Q  " + name + "     E  " + name2;
		UiDraw.Text(text, 0.688f - Theme.MixedWidth(text, 0.17f, 0.0222f), 0.164f, 0.17f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
		float num11 = Theme.MixedWidth(text, 0.17f, 0.0222f);
		float num12 = 0.688f - num11;
		float num13 = Theme.MixedWidth("Q  " + name + "  ", 0.17f, 0.0222f);
		UiMouse.KeyRegion(num12 - 0.004f, 0.16f, num13 + 0.004f, 0.022f, Keys.Q);
		UiMouse.KeyRegion(num12 + num13, 0.16f, num11 - num13 + 0.004f, 0.022f, Keys.E);
		if (_row < _scroll)
		{
			_scroll = _row;
		}
		if (_row >= _scroll + num)
		{
			_scroll = _row - num + 1;
		}
		float num14 = 0.19f;
		for (int i = _scroll; i < section.Entries.Count && i < _scroll + num; i++)
		{
			Entry entry = section.Entries[i];
			bool num15 = i == _row;
			int ri = i;
			UiMouse.Region(0.308f, num14 - 0.002f, 0.192f, 0.024f, delegate
			{
				if (_row == ri)
				{
					return false;
				}
				_row = ri;
				return true;
			}, delegate
			{
				if (_clickRow != ri)
				{
					_clickRow = ri;
					_row = ri;
				}
				else
				{
					UiMouse.Send(Keys.Left);
				}
			});
			UiMouse.Region(0.5f, num14 - 0.002f, 0.192f, 0.024f, delegate
			{
				if (_row == ri)
				{
					return false;
				}
				_row = ri;
				return true;
			}, delegate
			{
				if (_clickRow != ri)
				{
					_clickRow = ri;
					_row = ri;
				}
				else
				{
					UiMouse.Send(Keys.Right);
				}
			});
			if (num15)
			{
				Theme.SelectBox(0.308f, num14 - 0.002f, 0.384f, 0.024f);
			}
			(int, int, int) tuple = (num15 ? Theme.SelectedText : Theme.Text);
			UiDraw.Text(entry.Key, 0.314f, num14 + 0.003f, 0.19f, tuple.Item1, tuple.Item2, tuple.Item3);
			string text2 = ((entry.Value.Length > 30) ? (entry.Value.Substring(0, 27) + "...") : entry.Value);
			bool result;
			(int, int, int) tuple2 = (num15 ? Theme.SelectedText : (bool.TryParse(entry.Value, out result) ? (result ? Theme.Good : Theme.Dim) : Theme.Gold));
			UiDraw.Text(text2, 0.68600005f - Theme.MixedWidth(text2, 0.19f, 0.0222f), num14 + 0.003f, 0.19f, tuple2.Item1, tuple2.Item2, tuple2.Item3);
			num14 += 0.024f;
		}
		Theme.ScrollBar(0.69400007f, 0.19f, (float)Math.Min(num, section.Entries.Count) * 0.024f, _scroll, num, section.Entries.Count);
		float num16 = 0.19f + (float)num * 0.024f + 0.008f;
		Theme.HRule(0.308f, num16, 0.384f, Theme.Faint.r, Theme.Faint.g, Theme.Faint.b, 200);
		Entry entry2 = section.Entries[Math.Min(_row, section.Entries.Count - 1)];
		string obj = (string.IsNullOrEmpty(entry2.Help) ? "(no description)" : entry2.Help);
		int num17 = 117;
		string[] array = obj.Split(' ');
		List<string> list = new List<string>();
		string text3 = "";
		string[] array2 = array;
		foreach (string text4 in array2)
		{
			if (text3.Length + text4.Length + 1 > num17 && text3.Length > 0)
			{
				list.Add(text3);
				text3 = text4;
			}
			else
			{
				text3 = ((text3.Length == 0) ? text4 : (text3 + " " + text4));
			}
		}
		if (text3.Length > 0)
		{
			list.Add(text3);
		}
		float num19 = num16 + 0.008f;
		foreach (string item in list.Take(3))
		{
			UiDraw.Text(item, 0.312f, num19, 0.18f, Theme.Dim.r, Theme.Dim.g, Theme.Dim.b);
			num19 += 0.018f;
		}
		if (_note != "")
		{
			UiDraw.Text(_note, 0.312f, 0.1f + num2 - 0.04f, 0.18f, Theme.Warn.r, Theme.Warn.g, Theme.Warn.b);
		}
		List<(string, string)> hints = (XPad.RecentlyUsed ? new List<(string, string)>
		{
			("D-pad", "Navigate"),
			("LB/RB", "Section"),
			("X", "Hold x10"),
			("B", "Close")
		} : new List<(string, string)>
		{
			("Up/Down", "Navigate"),
			("Left/Right", "Change"),
			("Shift", "x10"),
			("Q/E", "Section"),
			("Backspace", "Close")
		});
		Theme.HintRow(0.31f, 0.69000006f, 0.1f + num2 - 0.026f, hints, 0.19f, Theme.Dim);
	}
}
