using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using RDR2;
using RDR2.Native;

namespace NemesisSystem;

internal static class UiDraw
{
	private sealed class NativeString
	{
		public IntPtr Ptr;

		public int LastUsedFrame;
	}

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const ulong VarStringHash = 18055593637008912569uL;

	private const ulong BgDisplayTextHash = 1619411321388727128uL;

	private static readonly Dictionary<string, NativeString> Strings = new Dictionary<string, NativeString>();

	private static int _nextSweepFrame;

	private static readonly Dictionary<string, IntPtr> Forever = new Dictionary<string, IntPtr>();

	private static readonly char[] MarkupChars = new char[3] { '~', '<', '>' };

	public static int TextSlotLimit = 230;

	public static int FrameReserve;

	private static int _skipped;

	public static float ScreenAspect = 1.7777778f;

	public static bool FitTo169 = true;

	private static float _kx = 1f;

	private static float _ky = 1f;

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	private static double _lastFrameAt;

	private static int _nextAspectCheck;

	private const ulong DrawSpriteHash = 14521943631680817972uL;

	private static readonly Regex DoubleThe = new Regex("\\b(the)\\s+the\\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex ArticleThe = new Regex("\\b(an?)\\s+the\\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static bool GameFont = true;

	public static string BodyFont = "$body";

	public static string TitleFont = "$title";

	public static bool MarkupUnderline = true;

	private const int MaxLinePieces = 40;

	public static int RectsThisFrame { get; private set; }

	public static int TextSlotsThisFrame { get; private set; }

	public static int FrameNo { get; private set; }

	public static int TextSlotsLeft => TextSlotLimit - FrameReserve - TextSlotsThisFrame;

	public static int LastFrameSlots { get; private set; }

	public static int LastFrameSkipped { get; private set; }

	public static int LastFrameRects { get; private set; }

	public static int GlyphsThisFrame { get; private set; }

	public static bool CountOnly { get; set; }

	public static float Aspect
	{
		get
		{
			if (!FitTo169)
			{
				return ScreenAspect;
			}
			return 1.7777778f;
		}
	}

	public static float RealDt { get; private set; } = 0.016f;

	public static int RealMs => (int)Clock.ElapsedMilliseconds;

	public static Action<float, float, float, float, int, int, int, int> RectHook { get; set; }

	private static ulong Pin(string text)
	{
		int frame = Game.FrameCount;
		if (!Strings.TryGetValue(text, out var value))
		{
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			IntPtr intPtr = Marshal.AllocHGlobal(bytes.Length + 1);
			Marshal.Copy(bytes, 0, intPtr, bytes.Length);
			Marshal.WriteByte(intPtr, bytes.Length, 0);
			Dictionary<string, NativeString> strings = Strings;
			NativeString obj = new NativeString
			{
				Ptr = intPtr
			};
			value = obj;
			strings[text] = obj;
		}
		value.LastUsedFrame = frame;
		if (frame >= _nextSweepFrame)
		{
			_nextSweepFrame = frame + 600;
			foreach (string item in (from kv in Strings
				where frame - kv.Value.LastUsedFrame > 300
				select kv.Key).ToList())
			{
				Marshal.FreeHGlobal(Strings[item].Ptr);
				Strings.Remove(item);
			}
		}
		return (ulong)value.Ptr.ToInt64();
	}

	public static ulong CString(string text)
	{
		if (!CountOnly)
		{
			return Pin(text);
		}
		return 0uL;
	}

	public static ulong ForeverString(string text)
	{
		if (CountOnly)
		{
			return 0uL;
		}
		if (!Forever.TryGetValue(text, out var value))
		{
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			value = Marshal.AllocHGlobal(bytes.Length + 1);
			Marshal.Copy(bytes, 0, value, bytes.Length);
			Marshal.WriteByte(value, bytes.Length, 0);
			Forever[text] = value;
		}
		return (ulong)value.ToInt64();
	}

	public static ulong ForeverVarString(string text)
	{
		if (CountOnly)
		{
			return 0uL;
		}
		text = TheFix(text);
		return Function.Call<ulong>(18055593637008912569uL, new InputArgument[3]
		{
			10,
			new InputArgument(ForeverString("LITERAL_STRING")),
			new InputArgument(ForeverString(text))
		});
	}

	public static ulong VarString(string text)
	{
		if (CountOnly)
		{
			return 0uL;
		}
		TextSlotsThisFrame++;
		text = TheFix(text);
		return Function.Call<ulong>(18055593637008912569uL, new InputArgument[3]
		{
			10,
			new InputArgument(Pin("LITERAL_STRING")),
			new InputArgument(Pin(text))
		});
	}

	private static void CountGlyphs(string s)
	{
		for (int i = 0; i < s.Length; i++)
		{
			if (!char.IsWhiteSpace(s[i]))
			{
				GlyphsThisFrame++;
			}
		}
	}

	private static bool TakeSlots(int n)
	{
		if (TextSlotsThisFrame + n > TextSlotLimit - FrameReserve)
		{
			_skipped++;
			return false;
		}
		TextSlotsThisFrame += n;
		return true;
	}

	private static void UpdateFit()
	{
		if (!FitTo169 || CountOnly)
		{
			_kx = (_ky = 1f);
		}
		else
		{
			_kx = (_ky = Math.Min(1f, ScreenAspect / 1.7777778f)) * 1.7777778f / ScreenAspect;
		}
	}

	private static float MX(float x)
	{
		return 0.5f + (x - 0.5f) * _kx;
	}

	private static float MY(float y)
	{
		return 0.5f + (y - 0.5f) * _ky;
	}

	public static float FromScreenX(float x)
	{
		if (_kx != 0f)
		{
			return 0.5f + (x - 0.5f) / _kx;
		}
		return x;
	}

	public static float FromScreenY(float y)
	{
		if (_ky != 0f)
		{
			return 0.5f + (y - 0.5f) / _ky;
		}
		return y;
	}

	public static void ScreenRect(int r, int g, int b, int a)
	{
		RectsThisFrame++;
		if (CountOnly)
		{
			RectHook?.Invoke(0f, 0f, 1f, 1f, r, g, b, a);
		}
		else
		{
			GRAPHICS.DRAW_RECT(0.5f, 0.5f, 1f, 1f, r, g, b, a, p8: true, p9: false);
		}
	}

	public static void RealRect(float cx, float cy, float w, float h, int r, int g, int b, int a)
	{
		RectsThisFrame++;
		if (!CountOnly)
		{
			GRAPHICS.DRAW_RECT(cx, cy, w, h, r, g, b, a, p8: true, p9: false);
		}
	}

	public static void BeginFrame()
	{
		double totalSeconds = Clock.Elapsed.TotalSeconds;
		RealDt = (float)Math.Min(0.1, Math.Max(0.0, totalSeconds - _lastFrameAt));
		_lastFrameAt = totalSeconds;
		LastFrameRects = RectsThisFrame;
		RectsThisFrame = 0;
		FrameNo++;
		LastFrameSlots = TextSlotsThisFrame;
		LastFrameSkipped = _skipped;
		_skipped = 0;
		TextSlotsThisFrame = 0;
		GlyphsThisFrame = 0;
		FrameReserve = 0;
		if (!CountOnly)
		{
			if (_nextAspectCheck-- <= 0)
			{
				_nextAspectCheck = 120;
				ScreenAspect = WindowAspect() ?? ScreenAspect;
			}
			UpdateFit();
		}
	}

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(IntPtr hWnd, out RECT r);

	private static float? WindowAspect()
	{
		try
		{
			IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
			if (mainWindowHandle != IntPtr.Zero && GetClientRect(mainWindowHandle, out var r) && r.Right > 0 && r.Bottom > 0)
			{
				float num = (float)r.Right / (float)r.Bottom;
				if (num > 0.9f && num < 4f)
				{
					return num;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static bool EnsureTextureDict(string dict)
	{
		if (CountOnly)
		{
			return true;
		}
		if (TXD.HAS_STREAMED_TEXTURE_DICT_LOADED(dict))
		{
			return true;
		}
		TXD.REQUEST_STREAMED_TEXTURE_DICT(dict, p1: false);
		return false;
	}

	public static void Sprite(string dict, string name, float cx, float cy, float w, float h, float heading, int r, int g, int b, int a)
	{
		RectsThisFrame++;
		if (!CountOnly)
		{
			Function.Call(14521943631680817972uL, new InputArgument(Pin(dict)), new InputArgument(Pin(name)), MX(cx), MY(cy), w * _kx, h * _ky, heading, r, g, b, a, false);
		}
	}

	public static void SetDrawOrder(int order)
	{
		if (!CountOnly)
		{
			GRAPHICS.SET_SCRIPT_GFX_DRAW_ORDER(order);
		}
	}

	public static void Rect(float x, float y, float w, float h, int r, int g, int b, int a)
	{
		RectsThisFrame++;
		if (CountOnly)
		{
			RectHook?.Invoke(x, y, w, h, r, g, b, a);
		}
		else
		{
			GRAPHICS.DRAW_RECT(MX(x + w / 2f), MY(y + h / 2f), w * _kx, h * _ky, r, g, b, a, p8: true, p9: false);
		}
	}

	public static void Text(string text, float x, float y, float scale, int r = 235, int g = 230, int b = 220, int a = 255)
	{
		if (GameFont)
		{
			TextFont(text, BodyFont, x, y, scale, r, g, b, a);
		}
		else
		{
			RawText(text, x, y, scale, r, g, b, a);
		}
	}

	private static void RawText(string text, float x, float y, float scale, int r, int g, int b, int a)
	{
		if (!string.IsNullOrEmpty(text) && TakeSlots(1))
		{
			text = Safe(text);
			CountGlyphs(text);
			UIDEBUG._BG_SET_TEXT_SCALE(scale * _ky, scale * _ky);
			UIDEBUG._BG_SET_TEXT_COLOR(r, g, b, a);
			ulong value = Function.Call<ulong>(18055593637008912569uL, new InputArgument[3]
			{
				10,
				new InputArgument(Pin("LITERAL_STRING")),
				new InputArgument(Pin(text))
			});
			Function.Call(1619411321388727128uL, new InputArgument(value), MX(x), MY(y));
		}
	}

	internal static string TheFix(string s)
	{
		if (s == null)
		{
			return s;
		}
		if (s.IndexOf("the the", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("the  the", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			s = DoubleThe.Replace(s, "$1 ");
		}
		if (s.IndexOf("a the ", StringComparison.OrdinalIgnoreCase) >= 0 || s.IndexOf("an the ", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			s = ArticleThe.Replace(s, "$1 ");
		}
		return s;
	}

	internal static string Safe(string s)
	{
		s = TheFix(s);
		if (s.IndexOfAny(MarkupChars) < 0)
		{
			return s;
		}
		return s.Replace("->", "to").Replace("<-", "from").Replace(">=", "at least")
			.Replace("<=", "at most")
			.Replace(" > ", " over ")
			.Replace(" < ", " under ")
			.Replace("~", "")
			.Replace("<", "(")
			.Replace(">", ")");
	}

	public static void Glyph(string ch, string font, float x, float y, float scale, int r, int g, int b, int a = 255)
	{
		if (!string.IsNullOrEmpty(ch) && TakeSlots((!GameFont) ? 1 : 2))
		{
			CountGlyphs(ch);
			UIDEBUG._BG_SET_TEXT_SCALE(scale * _ky, scale * _ky);
			UIDEBUG._BG_SET_TEXT_COLOR(r, g, b, a);
			ulong value2;
			if (GameFont && !string.IsNullOrEmpty(font))
			{
				ulong value = Function.Call<ulong>(18055593637008912569uL, new InputArgument[3]
				{
					10,
					new InputArgument(Pin("LITERAL_STRING")),
					new InputArgument(Pin("<FONT FACE='" + font + "'>" + ch + "</FONT>"))
				});
				value2 = Function.Call<ulong>(18055593637008912569uL, new InputArgument[4]
				{
					42,
					new InputArgument(Pin("COLOR_STRING")),
					0,
					new InputArgument(value)
				});
			}
			else
			{
				value2 = Function.Call<ulong>(18055593637008912569uL, new InputArgument[3]
				{
					10,
					new InputArgument(Pin("LITERAL_STRING")),
					new InputArgument(Pin(ch))
				});
			}
			Function.Call(1619411321388727128uL, new InputArgument(value2), MX(x), MY(y));
		}
	}

	public static void TextFont(string text, string font, float x, float y, float scale, int r = 235, int g = 230, int b = 220, int a = 255, bool underline = false)
	{
		if (!GameFont || string.IsNullOrEmpty(font))
		{
			RawText(text, x, y, scale, r, g, b, a);
		}
		else if (!string.IsNullOrEmpty(text) && TakeSlots(2))
		{
			text = Safe(text);
			CountGlyphs(text);
			if (underline)
			{
				text = "<U>" + text + "</U>";
			}
			UIDEBUG._BG_SET_TEXT_SCALE(scale * _ky, scale * _ky);
			UIDEBUG._BG_SET_TEXT_COLOR(r, g, b, a);
			ulong value = Function.Call<ulong>(18055593637008912569uL, new InputArgument[3]
			{
				10,
				new InputArgument(Pin("LITERAL_STRING")),
				new InputArgument(Pin("<FONT FACE='" + font + "'>" + text + "</FONT>"))
			});
			ulong value2 = Function.Call<ulong>(18055593637008912569uL, new InputArgument[4]
			{
				42,
				new InputArgument(Pin("COLOR_STRING")),
				0,
				new InputArgument(value)
			});
			Function.Call(1619411321388727128uL, new InputArgument(value2), MX(x), MY(y));
		}
	}

	public static void Outline(float x, float y, float w, float h, int r, int g, int b, int a, float t = 0.0012f)
	{
		float num = t * Aspect;
		Rect(x, y, w, num, r, g, b, a);
		Rect(x, y + h - num, w, num, r, g, b, a);
		Rect(x, y, t, h, r, g, b, a);
		Rect(x + w - t, y, t, h, r, g, b, a);
	}

	public static void HLine(float x1, float x2, float y, int r, int g, int b, int a, float t = 0.0012f)
	{
		Rect(Math.Min(x1, x2), y - t * Aspect / 2f, Math.Abs(x2 - x1), t * Aspect, r, g, b, a);
	}

	public static void VLine(float x, float y1, float y2, int r, int g, int b, int a, float t = 0.0012f)
	{
		Rect(x - t / 2f, Math.Min(y1, y2), t, Math.Abs(y2 - y1), r, g, b, a);
	}

	public static void Line(float x1, float y1, float x2, float y2, float t, int r, int g, int b, int a = 255, int maxPieces = 40)
	{
		float aspect = Aspect;
		float num = x1 * aspect;
		float num2 = y1;
		float num3 = (x2 - x1) * aspect;
		float num4 = y2 - y1;
		float num5 = (float)Math.Sqrt(num3 * num3 + num4 * num4);
		if (num5 < 1E-05f)
		{
			Rect((num - t / 2f) / aspect, num2 - t / 2f, t / aspect, t, r, g, b, a);
			return;
		}
		float num6 = num3 / num5 * t / 2f;
		float num7 = num4 / num5 * t / 2f;
		num -= num6;
		num2 -= num7;
		num3 += 2f * num6;
		num4 += 2f * num7;
		bool flag = Math.Abs(num3) >= Math.Abs(num4);
		float num8 = (flag ? Math.Abs(num4) : Math.Abs(num3));
		int num9 = Math.Max(1, Math.Min(maxPieces, (int)Math.Ceiling(num8 / t)));
		for (int i = 0; i < num9; i++)
		{
			float num10 = (float)i / (float)num9;
			float num11 = (float)(i + 1) / (float)num9;
			float num12 = (num10 + num11) / 2f;
			float val = num + num3 * num10;
			float val2 = num + num3 * num11;
			float val3 = num2 + num4 * num10;
			float val4 = num2 + num4 * num11;
			float num13;
			float num14;
			float num17;
			float num18;
			if (flag)
			{
				num13 = Math.Min(val, val2);
				num14 = Math.Max(val, val2);
				float num15 = num2 + num4 * num12;
				float num16 = t / 2f;
				num17 = num15 - num16;
				num18 = num15 + num16;
			}
			else
			{
				num17 = Math.Min(val3, val4);
				num18 = Math.Max(val3, val4);
				float num19 = num + num3 * num12;
				float num20 = t / 2f;
				num13 = num19 - num20;
				num14 = num19 + num20;
			}
			Rect(num13 / aspect, num17, (num14 - num13) / aspect, num18 - num17, r, g, b, a);
		}
	}

	public static int LineCost(float x1, float y1, float x2, float y2, float t, int maxPieces = 40)
	{
		float num = (x2 - x1) * Aspect;
		float num2 = y2 - y1;
		float num3 = (float)Math.Sqrt(num * num + num2 * num2);
		if (num3 < 1E-05f)
		{
			return 1;
		}
		num = Math.Abs(num) + Math.Abs(num) / num3 * t;
		num2 = Math.Abs(num2) + Math.Abs(num2) / num3 * t;
		return Math.Max(1, Math.Min(maxPieces, (int)Math.Ceiling(Math.Min(num, num2) / t)));
	}

	public static bool Clip(ref float x1, ref float y1, ref float x2, ref float y2, float minX, float minY, float maxX, float maxY)
	{
		float num = x2 - x1;
		float num2 = y2 - y1;
		float num3 = 0f;
		float num4 = 1f;
		float[] array = new float[4]
		{
			0f - num,
			num,
			0f - num2,
			num2
		};
		float[] array2 = new float[4]
		{
			x1 - minX,
			maxX - x1,
			y1 - minY,
			maxY - y1
		};
		for (int i = 0; i < 4; i++)
		{
			if (Math.Abs(array[i]) < 1E-09f)
			{
				if (array2[i] < 0f)
				{
					return false;
				}
				continue;
			}
			float num5 = array2[i] / array[i];
			if (array[i] < 0f)
			{
				if (num5 > num4)
				{
					return false;
				}
				if (num5 > num3)
				{
					num3 = num5;
				}
			}
			else
			{
				if (num5 < num3)
				{
					return false;
				}
				if (num5 < num4)
				{
					num4 = num5;
				}
			}
		}
		float num6 = x1 + num * num3;
		float num7 = y1 + num2 * num3;
		x2 = x1 + num * num4;
		y2 = y1 + num2 * num4;
		x1 = num6;
		y1 = num7;
		return true;
	}

	public static void Blob(float cx, float cy, float radius, int r, int g, int b, int a)
	{
		float aspect = Aspect;
		Rect(cx - radius * 0.7f / aspect, cy - radius, radius * 1.4f / aspect, radius * 2f, r, g, b, a);
		Rect(cx - radius / aspect, cy - radius * 0.7f, radius * 2f / aspect, radius * 1.4f, r, g, b, a);
	}

	public static (int r, int g, int b) HateColor(HateTier tier)
	{
		return tier switch
		{
			HateTier.Vengeful => (r: 215, g: 135, b: 50), 
			HateTier.Murderous => (r: 205, g: 55, b: 45), 
			_ => (r: 205, g: 185, b: 100), 
		};
	}
}
