using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NemesisSystem;

internal static class KeyCombo
{
	internal static Keys Current = Keys.F11;

	internal static int PadMode;

	internal static Func<string, Keys> Bound;

	internal static string MenuText => Name(Current);

	internal static (string, string) PadOpenHint
	{
		get
		{
			if (PadMode != 1)
			{
				if (PadMode != 2)
				{
					if (PadMode != 3)
					{
						if (PadMode != 4)
						{
							return ("DRight", "Double-tap: open");
						}
						return (MenuText, "Open (keyboard)");
					}
					return ("DRight", "Hold: open");
				}
				return ("DDown", "Double-tap: open");
			}
			return ("DLeft", "Double-tap: open");
		}
	}

	internal static string SettingsText
	{
		get
		{
			Keys item = Extras(Current).settings;
			if (item != Keys.None)
			{
				return Name(Current | item);
			}
			return "(none)";
		}
	}

	internal static string UndoText
	{
		get
		{
			Keys item = Extras(Current).undo;
			if (item != Keys.None)
			{
				return Name(Current | item);
			}
			return "(none)";
		}
	}

	internal static Keys Base(Keys k)
	{
		return k & Keys.KeyCode;
	}

	internal static Keys Mods(Keys k)
	{
		return k & Keys.Modifiers;
	}

	internal static string Name(Keys k)
	{
		List<string> list = new List<string>();
		if ((k & Keys.Control) != Keys.None)
		{
			list.Add("Ctrl");
		}
		if ((k & Keys.Shift) != Keys.None)
		{
			list.Add("Shift");
		}
		if ((k & Keys.Alt) != Keys.None)
		{
			list.Add("Alt");
		}
		list.Add(Base(k).ToString());
		return string.Join("+", list);
	}

	internal static string ForParse(string v)
	{
		return Regex.Replace(v ?? "", "ctrl", "Control", RegexOptions.IgnoreCase).Replace('+', ',');
	}

	private static bool Never(Keys b)
	{
		if (b != Keys.Insert && b != Keys.Escape)
		{
			return b == Keys.Back;
		}
		return true;
	}

	private static bool PlayKey(Keys b)
	{
		if (b != Keys.Return && b != Keys.Space && b != Keys.Tab && b != Keys.Up && b != Keys.Down && b != Keys.Left && b != Keys.Right && b != Keys.W && b != Keys.A && b != Keys.S)
		{
			return b == Keys.D;
		}
		return true;
	}

	internal static string Problem(string what, Keys combo)
	{
		Keys keys = Base(combo);
		Keys keys2 = Mods(combo);
		if (keys == Keys.Insert)
		{
			return "Insert reloads every script - pick another key";
		}
		if (Never(keys))
		{
			return $"{keys} closes things - pick another key";
		}
		if (keys >= Keys.BrowserBack && keys <= Keys.LaunchApplication2)
		{
			return $"{keys} is a media key - on many laptops the F-keys need Fn (or Fn Lock) to work as F-keys";
		}
		Keys k = Bound?.Invoke("ForceAmbush") ?? Keys.F10;
		Keys k2 = Bound?.Invoke("RosterMenu") ?? Keys.F11;
		string[] array;
		if (what == "RosterMenu")
		{
			if (keys == Base(k) && (keys2 == Keys.None || keys2 == Keys.Control))
			{
				return Name(combo) + " is the encounter spawner's - pick another";
			}
			if (PlayKey(keys))
			{
				return $"{keys} is used in play and in the menus, even with Shift / Ctrl / Alt - pick another";
			}
			if ((keys2 & Keys.Alt) != Keys.None && (keys == Keys.F4 || keys == Keys.Tab || keys == Keys.Escape))
			{
				return Name(combo) + " belongs to Windows - pick another";
			}
			if (keys2 == Keys.None)
			{
				if ((keys < Keys.F1 || keys > Keys.F24) && (keys < Keys.NumPad0 || keys > Keys.Divide) && keys != Keys.Apps && keys != Keys.Pause && keys != Keys.Scroll)
				{
					return $"{keys} on its own is used in play or in the menus - an F-key, a numpad key, or hold Shift / Ctrl / Alt with it";
				}
				array = new string[2] { "Interrogate", "Telegraph" };
				foreach (string text in array)
				{
					Keys? keys3 = Bound?.Invoke(text);
					if (keys3.HasValue)
					{
						Keys valueOrDefault = keys3.GetValueOrDefault();
						if (Base(valueOrDefault) == keys && Mods(valueOrDefault) == Keys.None)
						{
							return $"{keys} is the {text} key - pick another";
						}
					}
				}
			}
			return null;
		}
		if (keys2 != Keys.None)
		{
			return null;
		}
		if (PlayKey(keys))
		{
			return $"{keys} is used in play and in the menus - pick another";
		}
		if (keys == Base(k2) && Mods(k2) == Keys.None)
		{
			return $"{keys} is the menu key";
		}
		array = new string[3] { "Interrogate", "Telegraph", "ForceAmbush" };
		foreach (string text2 in array)
		{
			if (!(text2 != what))
			{
				continue;
			}
			Keys? keys3 = Bound?.Invoke(text2);
			if (keys3.HasValue)
			{
				Keys valueOrDefault2 = keys3.GetValueOrDefault();
				if (Base(valueOrDefault2) == keys)
				{
					return $"{keys} is the {text2} key - pick another";
				}
			}
		}
		return null;
	}

	internal static (Keys settings, Keys undo, Keys debug) Extras(Keys combo)
	{
		List<Keys> free = new List<Keys>
		{
			Keys.Alt,
			Keys.Shift,
			Keys.Control
		}.Where((Keys m) => (Mods(combo) & m) == 0).ToList();
		Keys item = Take(Keys.Alt);
		Keys item2 = Take(Keys.Shift);
		Keys item3 = Take(Keys.Control);
		return (settings: item, undo: item2, debug: item3);
		Keys Take(Keys want)
		{
			if (free.Remove(want))
			{
				return want;
			}
			if (free.Count != 0)
			{
				Keys result = free[0];
				free.RemoveAt(0);
				return result;
			}
			return Keys.None;
		}
	}

	internal static bool Is(KeyEventArgs e, Keys combo, Keys extra = Keys.None)
	{
		if (e != null && e.KeyCode == Base(combo))
		{
			return (e.Modifiers & Keys.Modifiers) == (Mods(combo) | extra);
		}
		return false;
	}

	internal static string Keyed(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		if (PadMode != 0)
		{
			string text = ((PadMode == 1) ? "double-tap D-pad Left" : ((PadMode == 2) ? "double-tap D-pad Down" : ((PadMode == 3) ? "hold D-pad Right" : "the keyboard key only")));
			s = s.Replace("double-tap Right on the D-pad", text).Replace("double-tap D-pad Right", text).Replace("Double-tap D-pad Right", char.ToUpperInvariant(text[0]) + text.Substring(1));
		}
		if (Current == Keys.F11)
		{
			return s;
		}
		(Keys, Keys, Keys) tuple = Extras(Current);
		string newValue = ((tuple.Item3 == Keys.None) ? "(none)" : Name(Current | tuple.Item3));
		return s.Replace("Alt+F11", "\u0001").Replace("Shift+F11", "\u0002").Replace("Ctrl+F11", "\u0003")
			.Replace("F11", "\u0004")
			.Replace("\u0001", SettingsText)
			.Replace("\u0002", UndoText)
			.Replace("\u0003", newValue)
			.Replace("\u0004", MenuText);
	}
}
