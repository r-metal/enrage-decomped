using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using RDR2.Native;

namespace NemesisSystem;

internal static class Theme
{
	internal enum ArrowDir
	{
		Left,
		Right,
		Down
	}

	public const float PanelX = 0.07f;

	public const float PanelW = 0.86f;

	public const float Top = 0.07f;

	public const float HeaderH = 0.065f;

	public const float Pad = 0.01f;

	public const float CenterX = 0.5f;

	public const float FooterY = 0.868f;

	public const float Px = 0.00052083336f;

	public const float PyU = 0.0009259259f;

	public static readonly (int r, int g, int b) Gold = (r: 206, g: 200, b: 188);

	public static readonly (int r, int g, int b) Title = (r: 245, g: 244, b: 240);

	public static readonly (int r, int g, int b) Subtitle = (r: 196, g: 193, b: 186);

	public static readonly (int r, int g, int b) Text = (r: 226, g: 224, b: 218);

	public static readonly (int r, int g, int b) Dim = (r: 146, g: 144, b: 138);

	public static readonly (int r, int g, int b) Faint = (r: 98, g: 96, b: 91);

	public static readonly (int r, int g, int b) Panel = (r: 10, g: 10, b: 9);

	public static readonly (int r, int g, int b) Card = (r: 27, g: 26, b: 24);

	public static readonly (int r, int g, int b) CardHi = (r: 52, g: 50, b: 46);

	public static readonly (int r, int g, int b) Border = (r: 84, g: 81, b: 76);

	public static readonly (int r, int g, int b) Selected = (r: 40, g: 38, b: 35);

	public static readonly (int r, int g, int b) SelectedText = (r: 245, g: 244, b: 240);

	public static readonly (int r, int g, int b) NativeRed = (r: 176, g: 26, b: 24);

	public static readonly (int r, int g, int b) KeyCap = (r: 214, g: 212, b: 206);

	public static readonly (int r, int g, int b) Rule = (r: 190, g: 187, b: 180);

	public static readonly (int r, int g, int b) Line = (r: 120, g: 117, b: 110);

	public static readonly (int r, int g, int b) Blood = (r: 205, g: 55, b: 45);

	public static readonly (int r, int g, int b) Warn = (r: 215, g: 135, b: 50);

	public static readonly (int r, int g, int b) Good = (r: 130, g: 190, b: 120);

	public static readonly (int r, int g, int b) Ink = (r: 22, g: 21, b: 19);

	public static readonly (int r, int g, int b) InkSoft = (r: 78, g: 74, b: 68);

	public static readonly (int r, int g, int b) InkFaded = (r: 112, g: 108, b: 102);

	private static bool _backdrop;

	private const ulong AnimPostFxPlay = 4684433203668451423uL;

	private const ulong AnimPostFxStop = 13041707943063647124uL;

	public static string InkDict = "pausemenu_textures";

	public static string InkTexture = "menu_ink_large1";

	public static bool InkOn = true;

	private static int _windowFrame = -1;

	public static bool FrameOn = true;

	public static bool FrameTint = false;

	public static int FrameSize = 16;

	public static int FrameOut = 4;

	public static int FrameEdge = 16;

	public static int FrameInsetX = 28;

	public static int FrameInsetY = 12;

	public static int CardFrameUp = 1;

	private const string FrameDict = "menu_textures";

	public const float TitleBearing = 0.0008f;

	public const float BodyUnit = 0.0222f;

	private static readonly Dictionary<string, (int first, int last)> _marquee = new Dictionary<string, (int, int)>();

	private static readonly Dictionary<string, int> _marqueeEnd = new Dictionary<string, int>();

	private const int MarqueeWait = 2000;

	private const int MarqueeStep = 110;

	private const int MarqueeHold = 2000;

	public static bool MarqueeOn = true;

	private static readonly Dictionary<string, (int off, string text)> _marqueeCut = new Dictionary<string, (int, string)>();

	public const string ModName = "enRAGE";

	public static readonly string ModVersion = (typeof(Theme).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?").Split('+')[0];

	internal const float ArrowDownHeading = 90f;

	public const float NotorietyBandH = 0.032f;

	public const float StrengthRowH = 0.028f;

	public const float GroundRowH = 0.026f;

	public const string IconDict = "itemtype_textures";

	private static readonly HashSet<string> PadFace = new HashSet<string> { "A", "B", "X", "Y" };

	private static readonly HashSet<string> PadPill = new HashSet<string> { "LB", "RB", "LT", "RT", "View", "Menu", "LS", "RS" };

	private static readonly HashSet<string> PadStick = new HashSet<string> { "LS", "RS" };

	private const float GlyphH = 0.022f;

	private const float FaceD = 0.029f;

	private static readonly HashSet<string> PadDir = new HashSet<string> { "DUp", "DDown", "DLeft", "DRight" };

	private static readonly Dictionary<string, string> PsNames = new Dictionary<string, string>
	{
		{ "LB", "L1" },
		{ "RB", "R1" },
		{ "LT", "L2" },
		{ "RT", "R2" },
		{ "LS", "L3" },
		{ "RS", "R3" },
		{ "View", "Share" },
		{ "Menu", "Options" }
	};

	private static readonly Dictionary<string, (float w, float s)> _keyFit = new Dictionary<string, (float, float)>();

	public static bool BackdropOn => _backdrop;

	private static bool InWindow => _windowFrame == UiDraw.FrameNo;

	public static (int r, int g, int b) InkHate(HateTier t)
	{
		return t switch
		{
			HateTier.Vengeful => (r: 160, g: 82, b: 12), 
			HateTier.Murderous => (r: 150, g: 24, b: 18), 
			_ => (r: 112, g: 92, b: 22), 
		};
	}

	public static void SetBackdrop(bool on)
	{
		if (on != _backdrop)
		{
			_backdrop = on;
			Function.Call(on ? 4684433203668451423uL : 13041707943063647124uL, new InputArgument(UiDraw.CString("PauseMenuIn")));
		}
	}

	public static void HRule(float x, float y, float w, int r, int g, int b, int a, int px = 1)
	{
		UiDraw.Rect(x, y, w, 0.0009259259f * (float)px, r, g, b, a);
	}

	public static void VRule(float x, float y, float h, int r, int g, int b, int a, int px = 1)
	{
		UiDraw.Rect(x, y, 0.00052083336f * (float)px, h, r, g, b, a);
	}

	public static void Hairline(float x, float y, float w, float h, int r, int g, int b, int a, int px = 1)
	{
		HRule(x, y, w, r, g, b, a, px);
		HRule(x, y + h - 0.0009259259f * (float)px, w, r, g, b, a, px);
		VRule(x, y, h, r, g, b, a, px);
		VRule(x + w - 0.00052083336f * (float)px, y, h, r, g, b, a, px);
	}

	public static void Corners(float x, float y, float w, float h, int a = 230, float len = 0.012f)
	{
		float num = len * UiDraw.Aspect;
		var (r, g, b) = Gold;
		HRule(x, y, len, r, g, b, a, 2);
		VRule(x, y, num, r, g, b, a, 2);
		HRule(x + w - len, y, len, r, g, b, a, 2);
		VRule(x + w - 0.0010416667f, y, num, r, g, b, a, 2);
		HRule(x, y + h - 0.0018518518f, len, r, g, b, a, 2);
		VRule(x, y + h - num, num, r, g, b, a, 2);
		HRule(x + w - len, y + h - 0.0018518518f, len, r, g, b, a, 2);
		VRule(x + w - 0.0010416667f, y + h - num, num, r, g, b, a, 2);
	}

	public static void Disc(float cx, float cy, float d, int r, int g, int b, int a)
	{
		float num = d / 2f;
		float num2 = d / 24f;
		for (int i = 0; i < 24; i++)
		{
			float num3 = 0f - num + num2 * ((float)i + 0.5f);
			float num4 = (float)Math.Sqrt(Math.Max(0f, num * num - num3 * num3)) / UiDraw.Aspect;
			UiDraw.Rect(cx - num4, cy - num + num2 * (float)i, num4 * 2f, num2 + 0.00046296295f, r, g, b, a);
		}
	}

	private static bool InkReady()
	{
		if (InkOn && !string.IsNullOrEmpty(InkDict))
		{
			return UiDraw.EnsureTextureDict(InkDict);
		}
		return false;
	}

	public static void InkBehind(float x, float y, float w, float h, int alpha = 255, bool tight = false)
	{
		if (InkReady())
		{
			float w2 = (tight ? (w * 1.05f + 0.012f) : (w * 1.1f + 0.03f));
			float h2 = (tight ? (h * 1.02f + 0.006f) : (h * 1.16f + 0.045f));
			UiDraw.Sprite(InkDict, InkTexture, x + w / 2f, y + h / 2f, w2, h2, 0f, 0, 0, 0, alpha);
		}
	}

	public static bool Window(float y0, float y1)
	{
		if (!InkReady())
		{
			return false;
		}
		InkBehind(0.07f, y0, 0.86f, y1 - y0);
		_windowFrame = UiDraw.FrameNo;
		return true;
	}

	private static void Bg(float x, float y, float w, float h, int alpha)
	{
		if (!InWindow)
		{
			UiDraw.Rect(x, y, w, h, 0, 0, 0, alpha);
		}
	}

	public static void DebugPanel(float x, float y, float w, float h, int alpha = 215)
	{
		UiDraw.Rect(x, y, w, h, Panel.r, Panel.g, Panel.b, Math.Max(alpha, 205));
		Hairline(x, y, w, h, Border.r, Border.g, Border.b, 230);
		Corners(x, y, w, h);
	}

	public static void PanelRect(float x, float y, float w, float h, int alpha = 215)
	{
		if (!InWindow)
		{
			if (InkReady())
			{
				InkBehind(x, y, w, h);
			}
			else
			{
				UiDraw.Rect(x, y, w, h, 0, 0, 0, Math.Max(alpha, 228));
			}
		}
	}

	public static void Brush(float x, float y, float w, float h, int alpha)
	{
		if (InkReady())
		{
			InkBehind(x, y, w, h, alpha, tight: true);
			if (UiDraw.EnsureTextureDict(InkDict))
			{
				UiDraw.Sprite(InkDict, InkTexture, x + w / 2f, y + h / 2f, w * 1.01f + 0.004f, h * 1.04f + 0.01f, 0f, 0, 0, 0, alpha);
			}
		}
		else
		{
			UiDraw.Rect(x, y, w, h, 0, 0, 0, alpha * 232 / 255);
		}
	}

	public static void CardRect(float x, float y, float w, float h, int alpha = 240, bool selected = false)
	{
		(int, int, int) tuple = (selected ? CardHi : Card);
		UiDraw.Rect(x, y, w, h, tuple.Item1, tuple.Item2, tuple.Item3, alpha);
		if (selected)
		{
			RedFrame(x, y, w, h);
		}
		else
		{
			Hairline(x, y, w, h, Border.r, Border.g, Border.b, alpha * 3 / 5);
		}
	}

	public static void SelectBox(float x, float y, float w, float h, int alpha = 235)
	{
		UiDraw.Rect(x, y, w, h, Selected.r, Selected.g, Selected.b, alpha);
		RedFrame(x, y, w, h);
	}

	public static void SelectFill(float x, float y, float w, float h, int alpha = 235)
	{
		UiDraw.Rect(x, y, w, h, Selected.r, Selected.g, Selected.b, alpha);
	}

	public static void RedFrame(float x, float y, float w, float h, float scale = 1f, int extraOut = 0)
	{
		if (!FrameOn || !UiDraw.EnsureTextureDict("menu_textures"))
		{
			Hairline(x, y, w, h, NativeRed.r, NativeRed.g, NativeRed.b, 255, (scale > 1.2f) ? 3 : 2);
			return;
		}
		float num = ((float)(FrameOut + extraOut) + (float)FrameEdge * scale * (float)FrameInsetX / 100f) * 0.00052083336f;
		float num2 = (float)(FrameOut + extraOut) * 0.0009259259f;
		float num3 = x - num;
		float num4 = y - num2;
		float num5 = x + w + num;
		float num6 = y + h + num2 + (float)FrameEdge * scale * (float)FrameInsetY / 100f * 0.0009259259f;
		float val = (float)FrameSize * scale * 0.00052083336f;
		float val2 = (float)FrameSize * scale * 0.0009259259f;
		val = Math.Min(val, (num5 - num3) / 2f);
		val2 = Math.Min(val2, (num6 - num4) / 2f);
		float num7 = (float)FrameEdge * scale * 0.00052083336f;
		float num8 = (float)FrameEdge * scale * 0.0009259259f;
		int r = 255;
		int g = 255;
		int b = 255;
		if (FrameTint)
		{
			r = NativeRed.r;
			g = NativeRed.g;
			b = NativeRed.b;
		}
		float num9 = num5 - num3 - val;
		float num10 = num6 - num4 - val2;
		if (num9 > 0f)
		{
			S("crafting_highlight_t", (num3 + num5) / 2f, num4 + num8 / 2f, num9, num8, r, g, b);
			S("crafting_highlight_b", (num3 + num5) / 2f, num6 - num8 / 2f, num9, num8, r, g, b);
		}
		if (num10 > 0f)
		{
			S("crafting_highlight_l", num3 + num7 / 2f, (num4 + num6) / 2f, num7, num10, r, g, b);
			S("crafting_highlight_r", num5 - num7 / 2f, (num4 + num6) / 2f, num7, num10, r, g, b);
		}
		S("crafting_highlight_tl", num3 + val / 2f, num4 + val2 / 2f, val, val2, r, g, b);
		S("crafting_highlight_tr", num5 - val / 2f, num4 + val2 / 2f, val, val2, r, g, b);
		S("crafting_highlight_bl", num3 + val / 2f, num6 - val2 / 2f, val, val2, r, g, b);
		S("crafting_highlight_br", num5 - val / 2f, num6 - val2 / 2f, val, val2, r, g, b);
	}

	private static void S(string n, float cx, float cy, float w, float h, int r, int g, int b)
	{
		UiDraw.Sprite("menu_textures", n, cx, cy, w, h, 0f, r, g, b, 255);
	}

	public static void CenterRule(float x, float y, float w, float inset = 0.03f, int alpha = 170)
	{
		UiDraw.Rect(x + inset, y, w - inset * 2f, 0.0018518518f, Rule.r, Rule.g, Rule.b, alpha);
	}

	public static void Divider(float x, float y, float w)
	{
		HRule(x, y, w, Line.r, Line.g, Line.b, 200);
		HRule(x, y + 0.0027777776f, w, Faint.r, Faint.g, Faint.b, 160);
	}

	public static void Label(string text, float x, float y, float scale = 0.26f)
	{
		UiDraw.TextFont(text, UiDraw.TitleFont, x, y, scale, Gold.r, Gold.g, Gold.b);
	}

	public static void Body(string text, float x, float y, float scale = 0.28f)
	{
		UiDraw.Text(text, x, y, scale, Text.r, Text.g, Text.b);
	}

	public static void Section(string text, float x, float y, float right, (int r, int g, int b)? accent = null)
	{
		UiDraw.TextFont(text, UiDraw.TitleFont, x + 0.004f, y, 0.25f, Title.r, Title.g, Title.b);
		float num = x + 0.004f + CapsWidth(text, 0.25f) + 0.01f;
		if (right > num)
		{
			HRule(num, y + 0.011f, right - num, Rule.r, Rule.g, Rule.b, 110);
		}
	}

	public static float Chip(string text, float x, float y, (int r, int g, int b) color, bool filled = false)
	{
		float num = CapsWidth(text, 0.21f);
		float num2 = num + 0.014f;
		float num3 = 0.022f;
		if (filled)
		{
			UiDraw.Rect(x, y, num2, num3, color.r, color.g, color.b, 235);
		}
		else
		{
			Hairline(x, y, num2, num3, color.r, color.g, color.b, 235);
		}
		(int, int, int) tuple = (filled ? (14, 14, 13) : color);
		UiDraw.TextFont(text, UiDraw.TitleFont, x + (num2 - num) / 2f - 0.0008f, y + num3 / 2f - 0.007875f + 0.0004f, 0.21f, tuple.Item1, tuple.Item2, tuple.Item3);
		return num2;
	}

	public static float Tag(string text, float x, float y, (int r, int g, int b) color)
	{
		float num = MixedWidth(text, 0.17f, 0.0222f);
		float num2 = num + 0.014f;
		UiDraw.Rect(x, y, num2, 0.02f, color.r / 7, color.g / 7, color.b / 7, 220);
		Hairline(x, y, num2, 0.02f, color.r, color.g, color.b, 200);
		UiDraw.Text(text, x + (num2 - num) / 2f - 0.0015f, y + 0.01f - 0.0063750003f - 0.0012f, 0.17f, color.r, color.g, color.b);
		return num2;
	}

	public static void Badge(string text, float x, float y, float w, (int r, int g, int b) color, bool filled)
	{
		if (filled)
		{
			UiDraw.Rect(x, y, w, 0.024f, color.r, color.g, color.b, 235);
		}
		else
		{
			UiDraw.Rect(x, y, w, 0.024f, color.r / 6, color.g / 6, color.b / 6, 220);
			Hairline(x, y, w, 0.024f, color.r, color.g, color.b, 235);
		}
		(int, int, int) tuple = (filled ? (14, 14, 13) : color);
		float num = CapsWidth(text, 0.2f);
		UiDraw.TextFont(text, UiDraw.TitleFont, x + (w - num) / 2f - 0.0008f, y + 0.012f - 0.0075000003f + 0.0008f, 0.2f, tuple.Item1, tuple.Item2, tuple.Item3);
	}

	public static float CapsWidth(string s, float scale)
	{
		if (string.IsNullOrEmpty(s))
		{
			return 0f;
		}
		float num = 0f;
		foreach (char c in s)
		{
			switch (char.ToUpperInvariant(c))
			{
			case 'I':
				num += 0.55f;
				break;
			case 'J':
				num += 0.6f;
				break;
			case 'L':
				num += 0.8f;
				break;
			case 'F':
				num += 0.85f;
				break;
			case 'E':
			case 'S':
			case 'T':
			case 'Z':
				num += 0.85f;
				break;
			case 'B':
			case 'P':
			case 'R':
				num += 0.92f;
				break;
			case 'C':
			case 'K':
			case 'Y':
				num += 0.97f;
				break;
			case 'D':
			case 'G':
				num += 1.07f;
				break;
			case 'O':
			case 'Q':
				num += 1.06f;
				break;
			case 'M':
				num += 1.3f;
				break;
			case 'W':
				num += 1.45f;
				break;
			case ' ':
				num += 0.45f;
				break;
			case '-':
				num += 0.55f;
				break;
			case ',':
			case '.':
			case ':':
				num += 0.35f;
				break;
			case '\'':
				num += 0.18f;
				break;
			default:
				num += (char.IsDigit(c) ? 0.9f : 1f);
				break;
			}
		}
		return num * scale * 0.0205f;
	}

	public static string Marquee(string s, float width, Func<string, float> w)
	{
		if (!MarqueeOn)
		{
			return null;
		}
		int realMs = UiDraw.RealMs;
		if (_marquee.Count > 400)
		{
			_marquee.Clear();
			_marqueeEnd.Clear();
			_marqueeCut.Clear();
		}
		string key = s + "|" + (int)(width * 10000f);
		if (!_marquee.TryGetValue(key, out (int, int) value) || realMs - value.Item2 > 500)
		{
			value = (realMs, realMs);
		}
		value.Item2 = realMs;
		if (!_marqueeEnd.TryGetValue(key, out var i))
		{
			for (i = 1; i < s.Length - 1 && w("..." + s.Substring(i)) > width; i++)
			{
			}
			_marqueeEnd[key] = i;
		}
		int num = realMs - value.Item1;
		if (num > 2000 + i * 110 + 2000)
		{
			value.Item1 = realMs;
			num = 0;
		}
		_marquee[key] = value;
		int num2 = Math.Min(i, Math.Max(0, (num - 2000) / 110));
		if (num2 <= 0)
		{
			return null;
		}
		if (_marqueeCut.TryGetValue(key, out (int, string) value2) && value2.Item1 == num2)
		{
			return value2.Item2;
		}
		string text = s.Substring(num2);
		while (text.Length > 1 && w("..." + text) > width)
		{
			text = text.Substring(0, text.Length - 1);
		}
		_marqueeCut[key] = (num2, "..." + text);
		return "..." + text;
	}

	public static float MixedWidth(string s, float scale, float unit)
	{
		if (string.IsNullOrEmpty(s))
		{
			return 0f;
		}
		float num = 0f;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (char.IsUpper(c))
			{
				num += CapsWidth(c.ToString(), 1f) / 0.0205f;
				continue;
			}
			switch (c)
			{
			case '\'':
			case ',':
			case '.':
			case ':':
			case 'i':
			case 'j':
			case 'l':
			case '|':
				num += 0.36f;
				break;
			case ' ':
			case '(':
			case ')':
			case '-':
			case 'f':
			case 'r':
			case 't':
				num += 0.5f;
				break;
			case 'm':
			case 'w':
				num += 1.15f;
				break;
			default:
				num += (char.IsDigit(c) ? 0.82f : 0.78f);
				break;
			}
		}
		return num * scale * unit;
	}

	public static float TitleWidth(string s, float scale)
	{
		if (string.IsNullOrEmpty(s))
		{
			return 0f;
		}
		float num = 0f;
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			num += (char.IsLower(c) ? (CapsWidth(c.ToString(), 1f) * 0.8f) : CapsWidth(c.ToString(), 1f));
		}
		return num * scale;
	}

	public static float TextWidth(string s, float scale)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return (float)s.Length * scale * 0.0185f;
		}
		return 0f;
	}

	public static void Header(string title, string subtitle, MenuTab active, (int r, int g, int b)? accent = null)
	{
		float num = 0.07f;
		float y = 0.04f;
		float h = 0.095f;
		Bg(num, y, 0.86f, h, 232);
		string text = title.ToUpperInvariant();
		float num2 = 0.54f;
		while (num2 > 0.36f && CapsWidth(text, num2) > 0.30960003f)
		{
			num2 -= 0.02f;
		}
		float num3 = 0.5f - CapsWidth(text, num2) / 2f;
		float num4 = 0.060000002f;
		UiDraw.TextFont(text, UiDraw.TitleFont, num3 + 0.00052083336f, num4, num2, Title.r, Title.g, Title.b);
		UiDraw.TextFont(text, UiDraw.TitleFont, num3, num4, num2, Title.r, Title.g, Title.b);
		if (!string.IsNullOrEmpty(subtitle))
		{
			float num5 = 0.344f;
			string text2 = subtitle;
			while (text2.Length > 4 && MixedWidth(text2, 0.22f, 0.0222f) > num5)
			{
				text2 = text2.Substring(0, text2.Length - 4).TrimEnd(' ', '-') + "...";
			}
			UiDraw.Text(text2, 0.5f - MixedWidth(text2, 0.22f, 0.0222f) / 2f, num4 + num2 * 0.075f + 0.001f, 0.22f, Subtitle.r, Subtitle.g, Subtitle.b);
		}
		string key = (XPad.RecentlyUsed ? "View" : "Tab");
		float num6 = num + 0.016f;
		float num7 = 0.06725f;
		num6 += Glyph(key, num6, num7 + 0.002f) + 0.01f;
		(string, MenuTab)[] array = new(string, MenuTab)[3]
		{
			("HIERARCHY", MenuTab.Hierarchy),
			("WORLD", MenuTab.World),
			("X-RAY LOG", MenuTab.XRay)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, MenuTab) tuple = array[i];
			string item = tuple.Item1;
			MenuTab item2 = tuple.Item2;
			DrawTab(item, num6, num7, 0.34f, active == item2);
			MenuTab t2 = item2;
			UiMouse.Region(num6 - 0.005f, num7 - 0.004f, CapsWidth(item, 0.34f) + 0.01f, 0.034f, null, delegate
			{
				UiMouse.TabClick?.Invoke(t2);
			});
			num6 += CapsWidth(item, 0.34f) + 0.022f;
		}
		string key2 = (XPad.RecentlyUsed ? "View" : "Tab");
		string text3 = "HOLD  SEARCH";
		float num8 = GlyphWidth(key2) + 0.008f + CapsWidth(text3, 0.26f);
		float num9 = num + 0.86f - 0.016f - num8;
		float num10 = 0.07025f;
		num9 += Glyph(key2, num9, num10 + 0.002f) + 0.008f;
		UiDraw.TextFont(text3, UiDraw.TitleFont, num9, num10 + 0.004f, 0.26f, Dim.r, Dim.g, Dim.b);
		CenterRule(num, 0.131f, 0.86f, 0.012f, 190);
	}

	private static void DrawTab(string text, float x, float y, float s, bool on)
	{
		(int, int, int) tuple = (on ? Title : Dim);
		bool flag = on && UiDraw.MarkupUnderline && UiDraw.GameFont;
		UiDraw.TextFont(text, UiDraw.TitleFont, x, y, s, tuple.Item1, tuple.Item2, tuple.Item3, 255, flag);
		if (on && !flag)
		{
			float num = CapsWidth(text, s);
			float num2 = num * 0.86f;
			HRule(x + (num - num2) / 2f, y + 0.028f, num2, NativeRed.r, NativeRed.g, NativeRed.b, 255, 2);
		}
	}

	internal static bool MenuArrow(ArrowDir dir, float cx, float cy, float h, (int r, int g, int b) c, int alpha = 255)
	{
		if (!UiDraw.EnsureTextureDict("menu_textures"))
		{
			return false;
		}
		float w = h / UiDraw.Aspect;
		string name = ((dir == ArrowDir.Left) ? "selection_arrow_left" : "selection_arrow_right");
		UiDraw.Sprite("menu_textures", name, cx, cy, w, h, (dir == ArrowDir.Down) ? 90f : 0f, c.r, c.g, c.b, alpha);
		return true;
	}

	private static void BarChevron(float x, float cy, bool left)
	{
		float num = 0.0015f;
		float num2 = 0.018f;
		for (int i = 0; i < 5; i++)
		{
			float num3 = num2 * (float)(5 - i) / 5f;
			UiDraw.Rect(left ? (x + (float)i * num) : (x + (float)(4 - i) * num), cy - num3 / 2f, num, num3, Dim.r, Dim.g, Dim.b, 220);
		}
	}

	public static void FactionBar(float y, IList<string> names, int active, bool isGang, (int r, int g, int b) accent, string status, string chipText = null, bool chipAsText = false, Action<int> pick = null)
	{
		float num = 0.07f;
		float num2 = 0.04f;
		Bg(num, y, 0.86f, num2, 232);
		CenterRule(num, y + num2 - 0.0018518518f, 0.86f, 0.012f, 120);
		bool recentlyUsed = XPad.RecentlyUsed;
		string key = (recentlyUsed ? "LB" : "Q");
		string key2 = (recentlyUsed ? "RB" : "E");
		float ty = y + 0.008f;
		List<string> list = names.Select((string n) => n.ToUpperInvariant()).ToList();
		float num3 = num + 0.86f - 0.014f;
		string text = chipText ?? (isGang ? "GANG HIERARCHY" : "LONE NEMESES");
		float num4 = (chipAsText ? MixedWidth(text, 0.22f, 0.0205f) : 0f);
		float num5 = num + 0.016f;
		float num6 = (chipAsText ? (num3 - num4 - 0.02f) : (num + 0.86f - 0.016f)) - GlyphWidth(key2);
		float num7 = num5 + GlyphWidth(key) + 0.02f;
		float num8 = num6 - 0.02f;
		float num9 = num8 - num7;
		int num10 = 0;
		int num11 = list.Count - 1;
		while (num11 > num10 && W(list.GetRange(num10, num11 - num10 + 1)) + 0.016f * (float)(num11 - num10) > num9)
		{
			if (active - num10 > num11 - active)
			{
				num10++;
			}
			else
			{
				num11--;
			}
		}
		List<string> range = list.GetRange(num10, num11 - num10 + 1);
		active -= num10;
		float num12 = ((range.Count > 1) ? Math.Max(0.016f, (num9 - W(range)) / (float)range.Count) : 0f);
		Glyph(key, num5, y + 0.006f);
		Glyph(key2, num6, y + 0.006f);
		UiMouse.KeyRegion(num5 - 0.004f, y + 0.004f, GlyphWidth(key) + 0.008f, num2 - 0.008f, Keys.Q);
		UiMouse.KeyRegion(num6 - 0.004f, y + 0.004f, GlyphWidth(key2) + 0.008f, num2 - 0.008f, Keys.E);
		float num13 = num7 + ((range.Count > 1) ? (num12 / 2f) : ((num9 - W(range)) / 2f));
		if (num10 > 0)
		{
			Arrow(left: true, num5 + GlyphWidth(key), num7);
		}
		for (int num14 = 0; num14 < range.Count; num14++)
		{
			bool num15 = num14 == active;
			(int, int, int) tuple = (num15 ? Title : Dim);
			bool flag = num15 && UiDraw.MarkupUnderline && UiDraw.GameFont;
			UiDraw.TextFont(range[num14], UiDraw.TitleFont, num13, ty, 0.25f, tuple.Item1, tuple.Item2, tuple.Item3, 255, flag);
			if (pick != null)
			{
				int fi = num10 + num14;
				UiMouse.Region(num13 - num12 / 2f, y + 0.002f, CapsWidth(range[num14], 0.25f) + num12, num2 - 0.004f, null, delegate
				{
					pick(fi);
				});
			}
			if (num15 && !flag)
			{
				float num16 = CapsWidth(range[num14], 0.25f);
				float num17 = num16 * 0.86f;
				UiDraw.Rect(num13 + (num16 - num17) / 2f, y + num2 - 0.0046296297f, num17, 0.0027777776f, accent.r, accent.g, accent.b, 255);
			}
			num13 += CapsWidth(range[num14], 0.25f) + num12;
		}
		if (num11 < list.Count - 1)
		{
			Arrow(left: false, num8, num6);
		}
		GlyphWidth(key2);
		if (chipAsText)
		{
			UiDraw.TextFont(text, UiDraw.TitleFont, num3 - num4, y + 0.008f, 0.22f, Title.r, Title.g, Title.b);
		}
		void Arrow(bool left, float gapL, float gapR)
		{
			float num18 = (gapL + gapR) / 2f;
			float cy = ty + 0.01f;
			if (!MenuArrow((!left) ? ArrowDir.Right : ArrowDir.Left, num18, cy, 0.016f, Dim))
			{
				BarChevron(num18 - 0.0037f, cy, left);
			}
		}
		static float W(List<string> l)
		{
			return l.Sum((string n) => CapsWidth(n, 0.25f));
		}
	}

	public static void NotorietyBand(float y, int value, string killValues = null, string purse = null, (int r, int g, int b)? purseCol = null, string aim = null)
	{
		if (purse != null || aim != null)
		{
			float num = y + 0.008f;
			if (purse != null)
			{
				(int, int, int) tuple = purseCol ?? Text;
				UiDraw.TextFont("PURSE", UiDraw.TitleFont, 0.092f, num - 0.0018f, 0.2f, Gold.r, Gold.g, Gold.b);
				UiDraw.TextFont(purse, UiDraw.TitleFont, 0.092f + CapsWidth("PURSE", 0.2f) + 0.01f, num - 0.002f, 0.22f, tuple.Item1, tuple.Item2, tuple.Item3);
			}
			if (aim != null)
			{
				string text = aim.ToUpperInvariant();
				float num2 = 0.908f - CapsWidth(text, 0.22f);
				UiDraw.TextFont(text, UiDraw.TitleFont, num2, num - 0.002f, 0.22f, Text.r, Text.g, Text.b);
				UiDraw.TextFont("AIM", UiDraw.TitleFont, num2 - 0.01f - CapsWidth("AIM", 0.2f), num - 0.0018f, 0.2f, Gold.r, Gold.g, Gold.b);
			}
		}
		float num3 = 0.032f;
		Bg(0.07f, y, 0.86f, num3, 232);
		CenterRule(0.07f, y + num3 - 0.0018518518f, 0.86f, 0.012f, 90);
		int num4 = Notoriety.Level(value);
		(int, int, int) tuple2 = Notoriety.LevelColors[num4];
		string text2 = Notoriety.LevelNames[num4].ToUpperInvariant();
		string text3 = ((num4 < 4) ? $"{value} / 100  -  {Notoriety.LevelNames[num4 + 1]} at {Notoriety.LevelStarts[num4 + 1]}" : $"{value} / 100  -  they will never forget you");
		float num5 = CapsWidth("NOTORIETY", 0.2f);
		float num6 = CapsWidth(text2, 0.24f);
		float num7 = MixedWidth(text3, 0.21f, 0.0222f);
		float num8 = num5 + 0.012f + num6 + 0.012f + 0.24f + 0.012f + num7;
		float num9 = 0.5f - num8 / 2f;
		float num10 = y + 0.008f;
		UiDraw.TextFont("NOTORIETY", UiDraw.TitleFont, num9, num10, 0.2f, Gold.r, Gold.g, Gold.b);
		num9 += num5 + 0.012f;
		UiDraw.TextFont(text2, UiDraw.TitleFont, num9, num10 - 0.002f, 0.24f, tuple2.Item1, tuple2.Item2, tuple2.Item3);
		num9 += num6 + 0.012f;
		float num11 = 0.0064814813f;
		float num12 = y + num3 / 2f - num11 / 2f;
		UiDraw.Rect(num9 - 0.0010416667f, num12 - 0.0018518518f, 0.24208333f, num11 + 0.0037037036f, 0, 0, 0, 230);
		for (int i = 0; i < 5; i++)
		{
			int num13 = Notoriety.LevelStarts[i];
			int num14 = ((i < 4) ? Notoriety.LevelStarts[i + 1] : 100);
			float x = num9 + 0.24f * (float)num13 / 100f;
			float num15 = 0.24f * (float)(num14 - num13) / 100f - ((i < 4) ? 0.0010416667f : 0f);
			UiDraw.Rect(x, num12, num15, num11, 40, 38, 35, 255);
			if (value > num13)
			{
				float w = num15 * Math.Min(1f, (float)(value - num13) / (float)(num14 - num13));
				(int, int, int) tuple3 = Notoriety.LevelColors[i];
				UiDraw.Rect(x, num12, w, num11, tuple3.Item1, tuple3.Item2, tuple3.Item3, 255);
				UiDraw.Rect(x, num12, w, 0.0009259259f, Math.Min(255, tuple3.Item1 + 40), Math.Min(255, tuple3.Item2 + 40), Math.Min(255, tuple3.Item3 + 40), 200);
			}
			if (i > 0)
			{
				UiDraw.Rect(num9 + 0.24f * (float)num13 / 100f - 0.00052083336f - 0.00052083336f, num12 - 0.0027777776f, 0.0015625001f, num11 + 0.005555555f, Gold.r, Gold.g, Gold.b, 200);
			}
		}
		num9 += 0.252f;
		UiDraw.Text(text3, num9, num10, 0.21f, Text.r, Text.g, Text.b);
	}

	public static void StrengthRow(float y, int strength, string word, (string label, double v)[] parts)
	{
		float num = 0.028f;
		Bg(0.07f, y, 0.86f, num, 232);
		CenterRule(0.07f, y + num - 0.0018518518f, 0.86f, 0.012f, 70);
		if (strength < 0 || parts == null)
		{
			return;
		}
		(int, int, int) tuple = ((strength >= 75) ? Good : ((strength >= 50) ? Text : ((strength >= 30) ? Warn : Blood)));
		string text = (word ?? "").ToUpperInvariant();
		float num2 = CapsWidth("STRENGTH:", 0.18f) + 0.01f + CapsWidth(text, 0.21f) + 0.022f;
		(string, double)[] array = parts;
		for (int i = 0; i < array.Length; i++)
		{
			(string, double) tuple2 = array[i];
			num2 += CapsWidth(tuple2.Item1, 0.18f) + 0.01f + 0.055f + 0.022f;
		}
		num2 -= 0.022f;
		float num3 = 0.5f - num2 / 2f;
		float num4 = y + 0.006f;
		UiDraw.TextFont("STRENGTH:", UiDraw.TitleFont, num3, num4 + 0.001f, 0.18f, Gold.r, Gold.g, Gold.b);
		num3 += CapsWidth("STRENGTH:", 0.18f) + 0.01f;
		UiDraw.TextFont(text, UiDraw.TitleFont, num3, num4 - 0.001f, 0.21f, tuple.Item1, tuple.Item2, tuple.Item3);
		num3 += CapsWidth(text, 0.21f) + 0.022f;
		float num5 = 0.0046296297f;
		float num6 = y + num / 2f - num5 / 2f - 0.0009259259f;
		array = parts;
		for (int i = 0; i < array.Length; i++)
		{
			(string, double) tuple3 = array[i];
			UiDraw.TextFont(tuple3.Item1, UiDraw.TitleFont, num3, num4 + 0.001f, 0.18f, Dim.r, Dim.g, Dim.b);
			num3 += CapsWidth(tuple3.Item1, 0.18f) + 0.01f;
			UiDraw.Rect(num3 - 0.00052083336f, num6 - 0.0009259259f, 0.056041665f, num5 + 0.0018518518f, 0, 0, 0, 230);
			UiDraw.Rect(num3, num6, 0.055f, num5, 40, 38, 35, 255);
			(int, int, int) tuple4 = Col(tuple3.Item2);
			float num7 = 0.055f * (float)Math.Max(0.0, Math.Min(1.0, tuple3.Item2));
			if (num7 > 0f)
			{
				UiDraw.Rect(num3, num6, num7, num5, tuple4.Item1, tuple4.Item2, tuple4.Item3, 255);
			}
			num3 += 0.077f;
		}
		static (int r, int g, int b) Col(double v)
		{
			if (!(v < 0.25))
			{
				if (!(v < 0.5))
				{
					return Good;
				}
				return Warn;
			}
			return Blood;
		}
	}

	public static void GroundRow(float y, (string label, List<string> names, (int r, int g, int b) c)[] groups, string none = null)
	{
		float num = 0.026f;
		Bg(0.07f, y, 0.86f, num, 232);
		CenterRule(0.07f, y + num - 0.0018518518f, 0.86f, 0.012f, 70);
		if (groups == null)
		{
			return;
		}
		float num2 = 0.81f;
		List<(string label, List<string> names, (int r, int g, int b) c)> gs = (from gr in groups
			select (label: gr.label, names: new List<string>(gr.names), c: gr.c) into gr
			where gr.names.Count > 0
			select gr).ToList();
		int dropped = 0;
		while (gs.Count > 0 && Width() > num2)
		{
			(string, List<string>, (int, int, int)) tuple = gs[gs.Count - 1];
			tuple.Item2.RemoveAt(tuple.Item2.Count - 1);
			dropped++;
			if (tuple.Item2.Count == 0)
			{
				gs.RemoveAt(gs.Count - 1);
			}
		}
		float num3 = y + 0.0055f;
		if (gs.Count == 0)
		{
			string text = ((dropped > 0) ? $"GROUND: in {dropped} districts" : (none ?? "No ground of their own"));
			UiDraw.Text(text, 0.5f - MixedWidth(text, 0.205f, 0.0222f) / 2f, num3, 0.205f, Dim.r, Dim.g, Dim.b);
			return;
		}
		float num4 = 0.5f - Width() / 2f;
		UiDraw.TextFont("GROUND:", UiDraw.TitleFont, num4, num3 + 0.001f, 0.18f, Gold.r, Gold.g, Gold.b);
		num4 += CapsWidth("GROUND:", 0.18f) + 0.022f;
		for (int num5 = 0; num5 < gs.Count; num5++)
		{
			(string, List<string>, (int, int, int)) tuple2 = gs[num5];
			UiDraw.TextFont(tuple2.Item1, UiDraw.TitleFont, num4, num3 + 0.001f, 0.18f, Dim.r, Dim.g, Dim.b);
			num4 += CapsWidth(tuple2.Item1, 0.18f) + 0.008f;
			UiDraw.Text(Joined(tuple2.Item2) + ((num5 == gs.Count - 1) ? Tail() : ""), num4, num3 - 0.001f, 0.205f, tuple2.Item3.Item1, tuple2.Item3.Item2, tuple2.Item3.Item3);
			num4 += MixedWidth(Joined(tuple2.Item2), 0.205f, 0.0222f) + 0.022f;
		}
		static string Joined(List<string> n)
		{
			return string.Join(", ", n);
		}
		string Tail()
		{
			if (dropped <= 0)
			{
				return "";
			}
			return $"  +{dropped} more";
		}
		float Width()
		{
			float num6 = CapsWidth("GROUND:", 0.18f) + 0.022f;
			foreach (var item in gs)
			{
				num6 += CapsWidth(item.label, 0.18f) + 0.008f + MixedWidth(Joined(item.names), 0.205f, 0.0222f) + 0.022f;
			}
			return num6 - 0.022f + ((dropped > 0) ? MixedWidth(Tail(), 0.205f, 0.0222f) : 0f);
		}
	}

	public static void NoteBand(float y, string text, (int r, int g, int b)? color = null)
	{
		float num = 0.032f;
		(int, int, int) tuple = color ?? Dim;
		Bg(0.07f, y, 0.86f, num, 232);
		CenterRule(0.07f, y + num - 0.0018518518f, 0.86f, 0.012f, 90);
		UiDraw.Text(text, 0.5f - MixedWidth(text, 0.21f, 0.0222f) / 2f, y + 0.0065f, 0.21f, tuple.Item1, tuple.Item2, tuple.Item3);
	}

	public static float Icon(string name, float x, float y, float h, (int r, int g, int b) c, int a = 255)
	{
		if (string.IsNullOrEmpty(name))
		{
			return 0f;
		}
		float num = h / UiDraw.Aspect;
		if (UiDraw.EnsureTextureDict("itemtype_textures"))
		{
			UiDraw.Sprite("itemtype_textures", name, x + num / 2f, y + h / 2f, num, h, 0f, c.r, c.g, c.b, a);
		}
		return num;
	}

	public static string TemperIcon(Personality? p)
	{
		return p switch
		{
			Personality.Hothead => "itemtype_weapons", 
			Personality.Coward => "itemtype_player_stamina", 
			Personality.Proud => "itemtype_gold", 
			Personality.Cold => "itemtype_player_deadeye", 
			Personality.Greedy => "itemtype_cash", 
			Personality.Loyal => "itemtype_camp", 
			Personality.Drifter => "itemtype_horse", 
			_ => null, 
		};
	}

	public static string FacetIcon(string facet)
	{
		return facet switch
		{
			"Anger" => "itemtype_weapons", 
			"Bravery" => "itemtype_player_health", 
			"Greed" => "itemtype_cash", 
			"Loyalty" => "itemtype_camp", 
			"Pride" => "itemtype_gold", 
			"Honesty" => "transaction_honor_good", 
			"Piety" => "transaction_journal", 
			"Cruelty" => "transaction_honor_bad", 
			"Humour" => "itemtype_collectable", 
			"Ambition" => "transaction_xp", 
			_ => null, 
		};
	}

	public static float IconChip(string text, string icon, float x, float y, (int r, int g, int b) color)
	{
		string text2 = text.ToUpperInvariant();
		float num = 0.018f / UiDraw.Aspect;
		float num2 = CapsWidth(text2, 0.2f);
		float num3 = 0.008f + num + 0.006f + num2 + 0.01f;
		UiDraw.Rect(x, y, num3, 0.026f, color.r / 8, color.g / 8, color.b / 8, 200);
		Hairline(x, y, num3, 0.026f, color.r, color.g, color.b, 220);
		if (icon != null)
		{
			Icon(icon, x + 0.008f, y + 0.0040000007f, 0.018f, color);
		}
		float x2 = x + 0.008f + num + 0.006f;
		UiDraw.TextFont(text2, UiDraw.TitleFont, x2, y + 0.013f - 0.0075000003f - 0.001f, 0.2f, color.r, color.g, color.b);
		return num3;
	}

	public static string QuirkIcon(string q)
	{
		return q switch
		{
			"drunk" => "itemtype_herbs", 
			"gambler" => "itemtype_collectable", 
			"braggart" => "transaction_xp", 
			"traitor" => "transaction_honor_bad", 
			"honest" => "transaction_honor_good", 
			"superstitious" => "itemtype_animal", 
			"sadist" => "itemtype_weapons", 
			"family man" => "itemtype_camp", 
			_ => null, 
		};
	}

	public static string TraitIcon(string trait)
	{
		switch (trait)
		{
		case "Outgoing":
			return "transaction_camp_gang";
		case "Restless":
			return "itemtype_player_stamina";
		case "Violent":
			return "itemtype_weapons";
		case "Calm":
			return "itemtype_player_health";
		case "Introvert":
			return "transaction_journal";
		case "Friendly":
			return "transaction_honor_good";
		case "Surly":
			return "transaction_honor_bad";
		case "Neutral":
			return "itemtype_player_deadeye";
		case "Sharp":
			return "itemtype_documents";
		case "Dim":
			return "itemtype_loot";
		default:
			if (trait.Contains("Violent") || trait.Contains("Aggressive"))
			{
				break;
			}
			goto case null;
		case null:
			return null;
		}
		return "itemtype_weapons";
	}

	private static string Canon(string key)
	{
		return key switch
		{
			"Dpad" => "D-pad", 
			"Left stick" => "LS", 
			"Right stick" => "RS", 
			_ => key, 
		};
	}

	public static float GlyphWidth(string key)
	{
		float num = 0f;
		foreach (string item in Parts(key))
		{
			num += SingleWidth(item) + 0.003f;
		}
		return Math.Max(0f, num - 0.003f);
	}

	private static IEnumerable<string> Parts(string key)
	{
		key = Canon(key.Trim());
		if (key == "+/-")
		{
			yield return "+";
			yield return "-";
			yield break;
		}
		string[] array = key.Split(' ');
		foreach (string text in array)
		{
			string[] array2 = text.Split('/');
			foreach (string text2 in array2)
			{
				if (text2.Length > 0)
				{
					yield return Canon(text2);
				}
			}
		}
	}

	private static bool PadStyle(string k)
	{
		if (XPad.RecentlyUsed)
		{
			if (!PadFace.Contains(k) && !PadPill.Contains(k) && !(k == "D-pad"))
			{
				return PadDir.Contains(k);
			}
			return true;
		}
		return false;
	}

	private static float KeyboardLabelW(string k, float s)
	{
		return MixedWidth((k == null) ? null : new string(k.Select((char c) => ("iljtfr".IndexOf(c) < 0) ? c : 'o').ToArray()), s, 0.0205f);
	}

	private static float KeyLabelW(string k, float s)
	{
		return MixedWidth(k, s, 0.0205f);
	}

	private static string PadLabel(string k)
	{
		if (!XPad.PlayStation || !PsNames.TryGetValue(k, out var value))
		{
			return k;
		}
		return value;
	}

	private static void PsSymbol(string k, float cx, float cy, float d)
	{
		float aspect = UiDraw.Aspect;
		float num = 0.0024074074f;
		float num2 = d * 0.44f;
		switch (k)
		{
		case "A":
		{
			float num11 = num2 * 0.46f;
			UiDraw.Line(cx - num11 / aspect, cy - num11, cx + num11 / aspect, cy + num11, num, 100, 150, 230);
			UiDraw.Line(cx - num11 / aspect, cy + num11, cx + num11 / aspect, cy - num11, num, 100, 150, 230);
			break;
		}
		case "B":
		{
			(int r, int g, int b) keyCap = KeyCap;
			int item = keyCap.r;
			int item2 = keyCap.g;
			int item3 = keyCap.b;
			float num9 = num2 * 1.05f;
			float num10 = num9 - 2f * num;
			if (UiDraw.EnsureTextureDict("menu_textures"))
			{
				UiDraw.Sprite("menu_textures", "menu_icon_circle", cx, cy, num9 / aspect, num9, 0f, 220, 80, 80, 255);
				UiDraw.Sprite("menu_textures", "menu_icon_circle", cx, cy, num10 / aspect, num10, 0f, item, item2, item3, 255);
			}
			else
			{
				Disc(cx, cy, num9, 220, 80, 80, 255);
				Disc(cx, cy, num10, item, item2, item3, 255);
			}
			break;
		}
		case "X":
		{
			float num6 = num2 * 0.44f;
			float num7 = num6 / aspect;
			float num8 = num / aspect;
			UiDraw.Rect(cx - num7, cy - num6, 2f * num7, num, 220, 130, 190, 255);
			UiDraw.Rect(cx - num7, cy + num6 - num, 2f * num7, num, 220, 130, 190, 255);
			UiDraw.Rect(cx - num7, cy - num6, num8, 2f * num6, 220, 130, 190, 255);
			UiDraw.Rect(cx + num7 - num8, cy - num6, num8, 2f * num6, 220, 130, 190, 255);
			break;
		}
		case "Y":
		{
			float num3 = cy - num2 * 0.5f;
			float num4 = cy + num2 * 0.4f;
			float num5 = num2 * 0.54f / aspect;
			UiDraw.Rect(cx - num5, num4 - num, 2f * num5, num, 80, 190, 140, 255);
			UiDraw.Line(cx, num3 + num * 0.5f, cx - num5 + num * 0.5f / aspect, num4 - num * 0.5f, num, 80, 190, 140);
			UiDraw.Line(cx, num3 + num * 0.5f, cx + num5 - num * 0.5f / aspect, num4 - num * 0.5f, num, 80, 190, 140);
			break;
		}
		}
	}

	private static float KeyLabelY(float y, float h, float s)
	{
		return y + h / 2f - s * 0.0375f;
	}

	private static float SingleWidth(string k)
	{
		if (PadStyle(k))
		{
			if (PadFace.Contains(k) || PadStick.Contains(k))
			{
				return 0.029f / UiDraw.Aspect;
			}
			if (k == "D-pad" || PadDir.Contains(k))
			{
				return 0.022f / UiDraw.Aspect;
			}
			return KeyLabelW(PadLabel(k), 0.19f) + 0.012f;
		}
		return KeyFit(k).w;
	}

	private static (float w, float s) KeyFit(string k)
	{
		if (k != null && _keyFit.TryGetValue(k, out (float, float) value))
		{
			return value;
		}
		float num = 0.022f / UiDraw.Aspect;
		float num2 = 0.004f;
		float num3 = 0.21f;
		while (num3 > 0.15f && KeyboardLabelW(k, num3) + num2 > num)
		{
			num3 -= 0.01f;
		}
		float num4 = Math.Max(num, KeyboardLabelW(k, num3) + num2);
		if (num4 > num)
		{
			num4 = KeyboardLabelW(k, num3) + 0.008f;
		}
		(float, float) tuple = (num4, num3);
		if (k != null && UiDraw.Aspect > 0f)
		{
			_keyFit[k] = tuple;
		}
		return tuple;
	}

	public static float Glyph(string key, float x, float y)
	{
		float num = x;
		foreach (string item in Parts(key))
		{
			x += Single(item, x, y) + 0.003f;
		}
		return Math.Max(0f, x - num - 0.003f);
	}

	private static float Single(string k, float x, float y)
	{
		float num = SingleWidth(k);
		float num2 = 0.022f;
		var (r, g, b) = KeyCap;
		if (PadStyle(k))
		{
			if (PadStick.Contains(k))
			{
				float num3 = 0.029f;
				float num4 = x + num / 2f;
				float num5 = y + 0.011f;
				if (UiDraw.EnsureTextureDict("menu_textures"))
				{
					UiDraw.Sprite("menu_textures", "menu_icon_circle", num4, num5, num3 / UiDraw.Aspect, num3, 0f, r, g, b, 255);
				}
				else
				{
					Disc(num4, num5, num3, r, g, b, 255);
				}
				string text = PadLabel(k);
				UiDraw.TextFont(text, UiDraw.TitleFont, num4 - KeyLabelW(text, 0.18f) / 2f - 0.0011f, num5 - 0.0067500006f + 0.0006f, 0.18f, 18, 18, 17);
			}
			else if (PadFace.Contains(k))
			{
				float num6 = 0.029f;
				float num7 = x + num / 2f;
				float num8 = y + 0.011f;
				if (UiDraw.EnsureTextureDict("menu_textures"))
				{
					UiDraw.Sprite("menu_textures", "menu_icon_circle", num7, num8, num6 / UiDraw.Aspect, num6, 0f, r, g, b, 255);
				}
				else
				{
					Disc(num7, num8, num6, r, g, b, 255);
				}
				if (XPad.PlayStation)
				{
					PsSymbol(k, num7, num8, num6);
					return num;
				}
				UiDraw.TextFont(k, UiDraw.TitleFont, num7 - KeyLabelW(k, 0.23f) / 2f - 0.0007f, num8 - 0.008625001f + 0.0008f, 0.23f, 18, 18, 17);
			}
			else if (PadDir.Contains(k))
			{
				float num9 = num * 0.34f;
				float num10 = num2 * 0.34f;
				UiDraw.Rect(x + (num - num9) / 2f, y, num9, num2, 110, 108, 104, 255);
				UiDraw.Rect(x, y + (num2 - num10) / 2f, num, num10, 110, 108, 104, 255);
				float num11 = x + (num - num9) / 2f;
				float num12 = y + (num2 - num10) / 2f;
				switch (k)
				{
				case "DUp":
					UiDraw.Rect(num11, y, num9, (num2 - num10) / 2f + num10 * 0.5f, r, g, b, 255);
					break;
				case "DDown":
					UiDraw.Rect(num11, num12 + num10 * 0.5f, num9, (num2 - num10) / 2f + num10 * 0.5f, r, g, b, 255);
					break;
				case "DLeft":
					UiDraw.Rect(x, num12, (num - num9) / 2f + num9 * 0.5f, num10, r, g, b, 255);
					break;
				default:
					UiDraw.Rect(num11 + num9 * 0.5f, num12, (num - num9) / 2f + num9 * 0.5f, num10, r, g, b, 255);
					break;
				}
			}
			else if (k == "D-pad")
			{
				float num13 = num * 0.34f;
				float num14 = num2 * 0.34f;
				UiDraw.Rect(x + (num - num13) / 2f, y, num13, num2, r, g, b, 255);
				UiDraw.Rect(x, y + (num2 - num14) / 2f, num, num14, r, g, b, 255);
			}
			else
			{
				UiDraw.Rect(x, y + 0.001f, num, num2 - 0.002f, r, g, b, 255);
				float num15;
				switch (k)
				{
				default:
					num15 = 0f;
					break;
				case "View":
					num15 = -0.001f;
					break;
				case "RB":
				case "LB":
					num15 = -0.0007f;
					break;
				case "RT":
				case "LT":
					num15 = -0.0009f;
					break;
				case "RS":
				case "LS":
					num15 = -0.0011f;
					break;
				}
				float num16 = num15;
				string text2 = PadLabel(k);
				UiDraw.TextFont(text2, UiDraw.TitleFont, x + num / 2f - KeyLabelW(text2, 0.19f) / 2f + num16, KeyLabelY(y, num2, 0.19f), 0.19f, 18, 18, 17);
			}
			return num;
		}
		UiDraw.Rect(x, y, num, num2, r, g, b, 255);
		HRule(x, y + num2 - 0.0018518518f, num, 150, 148, 142, 255, 2);
		float item = KeyFit(k).s;
		UiDraw.TextFont(k, UiDraw.TitleFont, x + num / 2f - KeyboardLabelW(k, item) / 2f - 0.0010416667f, KeyLabelY(y, num2 - 0.0018518518f, item), item, 14, 14, 13);
		return num;
	}

	public static void Footer(float y, string help, bool warn = false)
	{
		Bg(0.07f, y, 0.86f, 0.034f, 232);
		CenterRule(0.07f, y, 0.86f, 0.012f);
		if (warn)
		{
			UiDraw.Text(help, 0.5f - MixedWidth(help, 0.25f, 0.0222f) / 2f, y + 0.012f, 0.25f, Warn.r, Warn.g, Warn.b);
			return;
		}
		List<(string, string, float)> list = new List<(string, string, float)>();
		string[] array = help.Split(new string[1] { "     " }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			int num = text.IndexOf("  ", StringComparison.Ordinal);
			string text2 = ((num > 0) ? text.Substring(0, num).Trim() : null);
			string text3 = ((num > 0) ? text.Substring(num).Trim() : text);
			float item = ((text2 != null) ? (GlyphWidth(text2) + 0.006f) : 0f) + MixedWidth(text3, 0.23f, 0.0205f);
			list.Add((text2, text3, item));
		}
		HintRow(0.084f, 0.916f, y + 0.01f, list.Select<(string, string, float), (string, string)>(((string key, string label, float w) hh) => (key: hh.key, label: hh.label)).ToList(), 0.23f, Text);
	}

	public static void ScrollBar(float x, float top, float h, int first, int shown, int total)
	{
		if (total > shown && shown > 0 && !(h <= 0f))
		{
			ScrollBar(x, top, h, (float)first / (float)total, (float)shown / (float)total);
		}
	}

	public static void ScrollBar(float x, float top, float h, float startFrac, float shownFrac)
	{
		if (!(shownFrac >= 0.999f) && !(h <= 0f))
		{
			float w = 0.0015625001f;
			float num = Math.Max(0.02f, h * Math.Max(0f, shownFrac));
			float y = top + (h - num) * Math.Max(0f, Math.Min(1f, startFrac / Math.Max(0.0001f, 1f - shownFrac)));
			UiDraw.Rect(x, top, w, h, Faint.r, Faint.g, Faint.b, 70);
			UiDraw.Rect(x, y, w, num, Text.r, Text.g, Text.b, 210);
		}
	}

	public static void HintRow(float left, float right, float y, IList<(string key, string label)> hints, float scale, (int r, int g, int b) color)
	{
		if (hints == null || hints.Count == 0)
		{
			return;
		}
		List<float> list = hints.Select(((string key, string label) hh) => ((hh.key != null) ? (GlyphWidth(hh.key) + 0.006f) : 0f) + MixedWidth(hh.label, scale, 0.0205f)).ToList();
		float num = right - left;
		float num2 = list.Sum();
		List<int> list2 = Enumerable.Range(0, hints.Count).ToList();
		while (list2.Count > 1 && num2 + (float)(list2.Count - 1) * 0.012f > num)
		{
			int index = ((list2.Count >= 2) ? (list2.Count - 2) : 0);
			num2 -= list[list2[index]];
			list2.RemoveAt(index);
		}
		int count = list2.Count;
		float num3 = ((count > 1) ? Math.Min(0.04f, (num - num2) / (float)(count - 1)) : 0f);
		float num4 = left + (num - (num2 + num3 * (float)(count - 1))) / 2f;
		for (int num5 = 0; num5 < count; num5++)
		{
			(string key, string label) tuple = hints[list2[num5]];
			string item = tuple.key;
			string item2 = tuple.label;
			Keys? keys = UiMouse.KeyOfLabel(item);
			if (keys.HasValue)
			{
				UiMouse.KeyRegion(num4 - 0.004f, y - 0.004f, list[list2[num5]] + 0.008f, 0.03f, keys.Value);
			}
			if (item != null)
			{
				num4 += Glyph(item, num4, y) + 0.006f;
			}
			UiDraw.TextFont(item2, UiDraw.TitleFont, num4, y + 0.003f, scale, color.r, color.g, color.b);
			num4 += MixedWidth(item2, scale, 0.0205f) + num3;
		}
	}
}
