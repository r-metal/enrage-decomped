using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RDR2;

namespace NemesisSystem;

internal static class UiMouse
{
	private struct POINT
	{
		public int X;

		public int Y;
	}

	private struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private struct Hit
	{
		public float X;

		public float Y;

		public float W;

		public float H;

		public Func<bool> Hover;

		public Action Click;
	}

	public static float X;

	public static float Y;

	public static float DX;

	public static float DY;

	public static bool Moved;

	public static bool Down;

	public static bool Clicked;

	public static bool Released;

	public static bool RightClicked;

	public static bool WheelUp;

	public static bool WheelDown;

	public static Action<Keys> SendKey;

	public static Action<MenuTab> TabClick;

	public static Action<int> WheelHandler;

	private static IntPtr _hwnd;

	private static int _hwndAt = -100000;

	private static int _cx;

	private static int _cy;

	private static int _cw = 1920;

	private static int _ch = 1080;

	private static bool _havePrev;

	private static bool _lWas;

	private static bool _rWas;

	private static bool _clipped;

	private static bool _hadOldClip;

	private static bool _sending;

	private static int _lastMouseAt = -100000;

	private static int _lastKeyAt;

	private static int _clipAt = -100000;

	private static RECT _oldClip;

	private static readonly List<Hit> _hits = new List<Hit>();

	private static readonly eInputType[] LookControls = new eInputType[7]
	{
		eInputType.LookLR,
		eInputType.LookUD,
		eInputType.LookLeftOnly,
		eInputType.LookRightOnly,
		eInputType.LookUpOnly,
		eInputType.LookDownOnly,
		eInputType.PcFreeLook
	};

	private static bool _drawnEarly;

	public static bool On { get; private set; }

	public static bool Active { get; private set; }

	public static bool Sending => _sending;

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT p);

	[DllImport("user32.dll")]
	private static extern bool ScreenToClient(IntPtr h, ref POINT p);

	[DllImport("user32.dll")]
	private static extern bool ClientToScreen(IntPtr h, ref POINT p);

	[DllImport("user32.dll")]
	private static extern bool GetClientRect(IntPtr h, out RECT r);

	[DllImport("user32.dll")]
	private static extern bool ClipCursor(ref RECT r);

	[DllImport("user32.dll", EntryPoint = "ClipCursor")]
	private static extern bool ClipCursorOff(IntPtr none);

	[DllImport("user32.dll")]
	private static extern bool GetClipCursor(out RECT r);

	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int vk);

	public static bool KeyDownNow(Keys k)
	{
		try
		{
			return (GetAsyncKeyState((int)k) & 0x8000) != 0;
		}
		catch
		{
			return false;
		}
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	public static bool Over(float x, float y, float w, float h)
	{
		if (Active && X >= x && X <= x + w && Y >= y)
		{
			return Y <= y + h;
		}
		return false;
	}

	public static void Region(float x, float y, float w, float h, Func<bool> hover, Action click = null)
	{
		if (On && !(w <= 0f) && !(h <= 0f))
		{
			_hits.Add(new Hit
			{
				X = x,
				Y = y,
				W = w,
				H = h,
				Hover = hover,
				Click = click
			});
		}
	}

	public static void KeyRegion(float x, float y, float w, float h, Keys key)
	{
		if (On)
		{
			Region(x, y, w, h, null, delegate
			{
				Send(key);
			});
		}
	}

	public static void Send(Keys key)
	{
		if (SendKey == null)
		{
			return;
		}
		_sending = true;
		try
		{
			SendKey(key);
		}
		finally
		{
			_sending = false;
		}
	}

	public static void NoteKey(Keys k)
	{
		if (!_sending && k != Keys.LButton && k != Keys.RButton && k != Keys.MButton && k != Keys.XButton1 && k != Keys.XButton2)
		{
			_lastKeyAt = UiDraw.RealMs;
			Active = false;
		}
	}

	public static Keys? KeyOfLabel(string label)
	{
		if (string.IsNullOrEmpty(label))
		{
			return null;
		}
		switch (label)
		{
		case "Enter":
			return Keys.Return;
		case "Back":
		case "Backspace":
			return Keys.Back;
		case "Esc":
		case "Escape":
			return Keys.Escape;
		case "Tab":
			return Keys.Tab;
		case "Space":
			return Keys.Space;
		case "Del":
		case "Delete":
			return Keys.Delete;
		case "PgUp":
			return Keys.Prior;
		case "PgDn":
			return Keys.Next;
		case "-":
			return Keys.OemMinus;
		case "+":
			return Keys.Oemplus;
		default:
			if (label.Length == 1 && label[0] >= 'A' && label[0] <= 'Z')
			{
				return (Keys)label[0];
			}
			return null;
		}
	}

	private static IntPtr Window()
	{
		if (_hwnd == IntPtr.Zero || UiDraw.RealMs - _hwndAt > 2000)
		{
			_hwndAt = UiDraw.RealMs;
			try
			{
				_hwnd = Process.GetCurrentProcess().MainWindowHandle;
			}
			catch
			{
			}
		}
		return _hwnd;
	}

	public static void Update(bool open)
	{
		WheelUp = (WheelDown = (Moved = (Clicked = (Released = (RightClicked = false)))));
		DX = (DY = 0f);
		bool flag = (GetAsyncKeyState(1) & 0x8000) != 0;
		bool flag2 = (GetAsyncKeyState(2) & 0x8000) != 0;
		IntPtr intPtr = ((open && !XPad.RecentlyUsed) ? Window() : IntPtr.Zero);
		if (intPtr == IntPtr.Zero || GetForegroundWindow() != intPtr || !GetClientRect(intPtr, out var r) || r.Right <= 0 || r.Bottom <= 0)
		{
			On = false;
			Active = false;
			_havePrev = false;
			_lWas = flag;
			_rWas = flag2;
			Release();
			return;
		}
		On = true;
		_cw = r.Right;
		_ch = r.Bottom;
		if (!GetCursorPos(out var p))
		{
			Active = false;
			return;
		}
		ScreenToClient(intPtr, ref p);
		int num = Math.Max(0, Math.Min(_cw - 1, p.X));
		int num2 = Math.Max(0, Math.Min(_ch - 1, p.Y));
		float num3 = UiDraw.FromScreenX((float)num / (float)_cw);
		float num4 = UiDraw.FromScreenY((float)num2 / (float)_ch);
		if (_havePrev)
		{
			int num5 = Math.Abs(num - _cx) + Math.Abs(num2 - _cy);
			Moved = num5 >= 1;
			DX = num3 - X;
			DY = num4 - Y;
			if (num5 >= 3)
			{
				NoteMouse();
			}
		}
		_havePrev = true;
		_cx = num;
		_cy = num2;
		X = num3;
		Y = num4;
		bool flag3 = p.X >= 0 && p.Y >= 0 && p.X < _cw && p.Y < _ch;
		Clicked = flag && !_lWas && flag3;
		Released = !flag && _lWas;
		RightClicked = flag2 && !_rWas && flag3;
		Down = flag;
		_lWas = flag;
		_rWas = flag2;
		if (Clicked || RightClicked)
		{
			NoteMouse();
		}
		try
		{
			WheelUp = Game.IsDisabledControlJustPressed(eInputType.CursorScrollUp) || Game.IsDisabledControlJustPressed(eInputType.PrevWeapon);
			WheelDown = Game.IsDisabledControlJustPressed(eInputType.CursorScrollDown) || Game.IsDisabledControlJustPressed(eInputType.NextWeapon);
		}
		catch
		{
		}
		if (WheelUp || WheelDown)
		{
			_lastMouseAt = UiDraw.RealMs;
		}
		Active = UiDraw.RealMs - _lastMouseAt < 3000 && _lastMouseAt >= _lastKeyAt;
		if (!Active)
		{
			Moved = false;
		}
		Clip(intPtr);
		if (Active)
		{
			HoldLook();
		}
	}

	private static void NoteMouse()
	{
		_lastMouseAt = UiDraw.RealMs;
		XPad.NoteKeyboard();
	}

	private static void HoldLook()
	{
		eInputType[] lookControls = LookControls;
		foreach (eInputType control in lookControls)
		{
			try
			{
				Game.DisableControlThisFrame(control);
			}
			catch
			{
			}
		}
	}

	private static void Clip(IntPtr h)
	{
		if (UiDraw.RealMs - _clipAt < 250 && _clipped)
		{
			return;
		}
		_clipAt = UiDraw.RealMs;
		try
		{
			if (!_clipped)
			{
				_hadOldClip = GetClipCursor(out _oldClip);
			}
			POINT p = new POINT
			{
				X = 0,
				Y = 0
			};
			POINT p2 = new POINT
			{
				X = _cw,
				Y = _ch
			};
			ClientToScreen(h, ref p);
			ClientToScreen(h, ref p2);
			RECT r = new RECT
			{
				Left = p.X,
				Top = p.Y,
				Right = p2.X,
				Bottom = p2.Y
			};
			_clipped = ClipCursor(ref r) || _clipped;
		}
		catch
		{
		}
	}

	public static void Release()
	{
		if (!_clipped)
		{
			return;
		}
		_clipped = false;
		try
		{
			if (_hadOldClip)
			{
				RECT r = _oldClip;
				ClipCursor(ref r);
			}
			else
			{
				ClipCursorOff(IntPtr.Zero);
			}
		}
		catch
		{
			try
			{
				ClipCursorOff(IntPtr.Zero);
			}
			catch
			{
			}
		}
	}

	public static void EndFrame()
	{
		try
		{
			if (!On)
			{
				return;
			}
			if (Active)
			{
				HoldLook();
				Hit? hit = null;
				for (int num = _hits.Count - 1; num >= 0; num--)
				{
					Hit value = _hits[num];
					if (X >= value.X && X <= value.X + value.W && Y >= value.Y && Y <= value.Y + value.H)
					{
						hit = value;
						break;
					}
				}
				if (hit.HasValue && (Moved || Clicked) && hit.Value.Hover != null)
				{
					bool flag = false;
					try
					{
						flag = hit.Value.Hover();
					}
					catch
					{
					}
					if (flag && !Clicked)
					{
						NemesisMod.UiSound("NAV_DOWN");
					}
				}
				if (hit.HasValue && Clicked && hit.Value.Click != null)
				{
					hit.Value.Click();
				}
				if (RightClicked)
				{
					Send(Keys.Back);
				}
			}
			if (WheelUp || WheelDown)
			{
				int num2 = (WheelUp ? 1 : (-1));
				if (WheelHandler != null)
				{
					WheelHandler(num2);
				}
				else
				{
					Send((num2 > 0) ? Keys.Up : Keys.Down);
				}
			}
			if (Active && !_drawnEarly)
			{
				DrawCursor();
			}
			_drawnEarly = false;
		}
		finally
		{
			_hits.Clear();
			WheelHandler = null;
		}
	}

	public static void DrawCursorNow()
	{
		if (On && Active && !_drawnEarly)
		{
			DrawCursor();
			_drawnEarly = true;
		}
	}

	private static void DrawCursor()
	{
		float sx = (float)_cx / (float)_cw;
		float sy = (float)_cy / (float)_ch;
		float num = Math.Max(1f, (float)Math.Round((float)_ch / 1080f * 2f));
		float px = num / (float)_cw;
		float py = num / (float)_ch;
		for (int i = 0; i <= 9; i++)
		{
			int num2 = (int)Math.Round((float)i * 0.72f) + 2;
			Box(0f, i, num2, 1f, 12, 235);
			if (i >= 1 && i <= 8 && num2 - 2 > 0)
			{
				Box(1f, i, num2 - 2, 1f, 250, 255);
			}
		}
		for (int j = 0; j < 4; j++)
		{
			float num3 = 2.5f + (float)j * 0.8f;
			Box(num3, 8 + j, 3.4f, 1.2f, 12, 235);
			if (j < 3)
			{
				Box(num3 + 1f, 8.1f + (float)j, 1.4f, 1f, 250, 255);
			}
		}
		void Box(float x, float y, float w, float hh, int c, int a)
		{
			UiDraw.RealRect(sx + (x + w / 2f) * px, sy + (y + hh / 2f) * py, w * px, hh * py, c, c, c, a);
		}
	}
}
