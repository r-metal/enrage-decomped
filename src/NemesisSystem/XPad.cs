using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RDR2;

namespace NemesisSystem;

internal static class XPad
{
	private struct Gamepad
	{
		public ushort Buttons;

		public byte LT;

		public byte RT;

		public short LX;

		public short LY;

		public short RX;

		public short RY;
	}

	private struct State
	{
		public uint Packet;

		public Gamepad Pad;
	}

	private struct RawDev
	{
		public IntPtr Handle;

		public uint Type;
	}

	[StructLayout(LayoutKind.Explicit, Size = 32)]
	private struct RidInfo
	{
		[FieldOffset(0)]
		public uint Size;

		[FieldOffset(4)]
		public uint Type;

		[FieldOffset(8)]
		public uint Vendor;

		[FieldOffset(12)]
		public uint Product;

		[FieldOffset(16)]
		public uint Version;

		[FieldOffset(20)]
		public ushort UsagePage;

		[FieldOffset(22)]
		public ushort Usage;
	}

	public const ushort Up = 1;

	public const ushort Down = 2;

	public const ushort Left = 4;

	public const ushort Right = 8;

	public const ushort Start = 16;

	public const ushort Back = 32;

	public const ushort LS = 64;

	public const ushort RS = 128;

	public const ushort LB = 256;

	public const ushort RB = 512;

	public const ushort A = 4096;

	public const ushort B = 8192;

	public const ushort X = 16384;

	public const ushort Y = 32768;

	private static volatile bool _use910;

	private static volatile bool _unavailable;

	private static volatile bool _probing;

	private static volatile bool _sonyChecking;

	private static volatile int _probeFound = -1;

	private static uint _slot;

	private static int _nextProbe = Environment.TickCount;

	private static ushort _now;

	private static ushort _prev;

	private static int _lastActivity = Environment.TickCount - 200000;

	public static float LX;

	public static float LY;

	public static float RX;

	public static float RY;

	public static float LT;

	public static float RT;

	private static int _lastKeyboard = Environment.TickCount - 100000;

	private static int _slowNotedAt;

	private static bool _slowNoted;

	private static bool _xinputUp;

	public static Action<string> Log;

	public static string IconsSetting = "Auto";

	private static volatile bool _sony;

	private static bool _iconsLogged;

	private static bool _fallback;

	private static int _nextSonyCheck = Environment.TickCount;

	private const uint RIDI_DEVICEINFO = 536870923u;

	private const uint RIM_TYPEHID = 2u;

	public static bool Connected { get; private set; }

	public static bool RecentlyUsed
	{
		get
		{
			if (Connected)
			{
				return _lastActivity - _lastKeyboard > 0;
			}
			return false;
		}
	}

	public static bool PlayStation { get; private set; }

	public static ushort Buttons => _now;

	[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
	private static extern uint Get14(uint user, out State s);

	[DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
	private static extern uint Get910(uint user, out State s);

	public static void NoteKeyboard()
	{
		_lastKeyboard = Environment.TickCount;
	}

	public static void Poll()
	{
		_prev = _now;
		long timestamp = Stopwatch.GetTimestamp();
		CheckIcons();
		long timestamp2 = Stopwatch.GetTimestamp();
		State s;
		bool num = PollXInput(out s);
		long timestamp3 = Stopwatch.GetTimestamp();
		if (num)
		{
			SetFallback(on: false);
			Connected = true;
			_now = s.Pad.Buttons;
			LX = Stick(s.Pad.LX);
			LY = Stick(s.Pad.LY);
			RX = Stick(s.Pad.RX);
			RY = Stick(s.Pad.RY);
			LT = (float)(int)s.Pad.LT / 255f;
			RT = (float)(int)s.Pad.RT / 255f;
		}
		else
		{
			if (!FallbackWanted())
			{
				SetFallback(on: false);
				Connected = false;
				_now = 0;
				LX = (LY = (RX = (RY = (LT = (RT = 0f)))));
				NoteSlow(timestamp, timestamp2, timestamp3);
				return;
			}
			SetFallback(on: true);
			Connected = true;
			ReadGameInputs();
		}
		NoteSlow(timestamp, timestamp2, timestamp3);
		if ((_now & ~_prev) != 0 || Math.Max(Math.Max(Math.Abs(LX), Math.Abs(LY)), Math.Max(Math.Abs(RX), Math.Abs(RY))) > 0.6f || Math.Max(LT, RT) > 0.5f)
		{
			_lastActivity = Environment.TickCount;
		}
	}

	private static void NoteSlow(long t0, long t1, long t2)
	{
		long timestamp = Stopwatch.GetTimestamp();
		double num = 1000.0 / (double)Stopwatch.Frequency;
		double num2 = (double)(timestamp - t0) * num;
		if (!(num2 < 12.0) && (!_slowNoted || Environment.TickCount - _slowNotedAt >= 60000))
		{
			_slowNoted = true;
			_slowNotedAt = Environment.TickCount;
			Log?.Invoke(string.Format("PADSLOW {0:0} ms: the Sony check {1:0.0}, XInput {2:0.0} (pad {3}), the rest {4:0.0}", num2, (double)(t1 - t0) * num, (double)(t2 - t1) * num, _xinputUp ? "up" : "none", (double)(timestamp - t2) * num));
		}
	}

	private static bool PollXInput(out State s)
	{
		s = default(State);
		if (_unavailable)
		{
			return false;
		}
		if (!_xinputUp)
		{
			int probeFound = _probeFound;
			if (probeFound < 0)
			{
				if (!_probing && Environment.TickCount - _nextProbe >= 0)
				{
					_probing = true;
					_nextProbe = Environment.TickCount + 2000;
					try
					{
						Task.Run((Action)ProbeSlots);
					}
					catch
					{
						_probing = false;
					}
				}
				return false;
			}
			_probeFound = -1;
			_slot = (uint)probeFound;
		}
		bool flag = false;
		try
		{
			flag = (_use910 ? Get910(_slot, out s) : Get14(_slot, out s)) == 0;
		}
		catch (DllNotFoundException)
		{
			if (_use910)
			{
				_unavailable = true;
			}
			else
			{
				_use910 = true;
			}
		}
		catch (EntryPointNotFoundException)
		{
			_unavailable = true;
		}
		catch
		{
		}
		if (!flag && _xinputUp)
		{
			_nextProbe = Environment.TickCount;
		}
		_xinputUp = flag;
		return flag;
	}

	private static void ProbeSlots()
	{
		try
		{
			for (uint num = 0u; num < 4; num++)
			{
				try
				{
					if ((_use910 ? Get910(num, out var _) : Get14(num, out var _)) == 0)
					{
						_probeFound = (int)num;
						break;
					}
				}
				catch (DllNotFoundException)
				{
					if (_use910)
					{
						_unavailable = true;
						break;
					}
					_use910 = true;
					num--;
				}
				catch (EntryPointNotFoundException)
				{
					_unavailable = true;
					break;
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		finally
		{
			_probing = false;
		}
	}

	private static void CheckIcons()
	{
		if (Environment.TickCount - _nextSonyCheck >= 0 && !_sonyChecking)
		{
			_nextSonyCheck = Environment.TickCount + 5000;
			_sonyChecking = true;
			try
			{
				Task.Run(delegate
				{
					try
					{
						_sony = FindSony();
					}
					catch
					{
					}
					finally
					{
						_sonyChecking = false;
					}
				});
			}
			catch
			{
				_sonyChecking = false;
			}
		}
		string text = (IconsSetting ?? "").Trim().ToLowerInvariant();
		int num;
		switch (text)
		{
		default:
			num = ((text == "ps5") ? 1 : 0);
			break;
		case "playstation":
		case "ps":
		case "ps4":
			num = 1;
			break;
		}
		bool flag = (byte)num != 0;
		bool flag2 = flag || (text != "xbox" && _sony);
		if (flag2 != PlayStation || !_iconsLogged)
		{
			PlayStation = flag2;
			_iconsLogged = true;
			Log?.Invoke((!flag2) ? ((text == "xbox") ? "PAD icons: Xbox (setting)" : "PAD icons: Xbox") : (flag ? "PAD icons: PlayStation (setting)" : "PAD icons: PlayStation (Sony controller found)"));
		}
	}

	private static bool FallbackWanted()
	{
		if (!_sony || Environment.TickCount - _lastKeyboard < 500)
		{
			return false;
		}
		try
		{
			return !Game.IsUsingKeyboardAndMouse;
		}
		catch
		{
			return false;
		}
	}

	private static void SetFallback(bool on)
	{
		if (on != _fallback)
		{
			_fallback = on;
			Log?.Invoke(on ? "PAD fallback: reading a PlayStation pad through the game's inputs" : "PAD fallback: off");
		}
	}

	private static void ReadGameInputs()
	{
		ushort b = 0;
		Bit(eInputType.FrontendUp, 1);
		Bit(eInputType.FrontendDown, 2);
		Bit(eInputType.FrontendLeft, 4);
		Bit(eInputType.FrontendRight, 8);
		Bit(eInputType.FrontendAccept, 4096);
		Bit(eInputType.FrontendCancel, 8192);
		Bit(eInputType.FrontendX, 16384);
		Bit(eInputType.FrontendY, 32768);
		Bit(eInputType.FrontendLB, 256);
		Bit(eInputType.FrontendRB, 512);
		Bit(eInputType.FrontendLS, 64);
		Bit(eInputType.FrontendRS, 128);
		Bit(eInputType.FrontendSelect, 32);
		Bit(eInputType.FrontendPause, 16);
		_now = b;
		LX = Dz(N(eInputType.FrontendAxisX));
		LY = Dz(0f - N(eInputType.FrontendAxisY));
		RX = Dz(N(eInputType.FrontendRightAxisX));
		RY = Dz(0f - N(eInputType.FrontendRightAxisY));
		bool flag = false;
		bool flag2 = false;
		try
		{
			flag = Game.IsDisabledControlPressed(eInputType.FrontendLT);
			flag2 = Game.IsDisabledControlPressed(eInputType.FrontendRT);
		}
		catch
		{
		}
		LT = Math.Max(flag ? 1f : 0f, Math.Abs(N(eInputType.FrontendLT)));
		RT = Math.Max(flag2 ? 1f : 0f, Math.Abs(N(eInputType.FrontendRT)));
		void Bit(eInputType c, ushort bit)
		{
			try
			{
				if (Game.IsDisabledControlPressed(c))
				{
					b |= bit;
				}
			}
			catch
			{
			}
		}
		static float Dz(float f)
		{
			if (!(Math.Abs(f) < 0.2f))
			{
				return Math.Max(-1f, Math.Min(1f, f));
			}
			return 0f;
		}
		static float N(eInputType c)
		{
			try
			{
				return Game.GetDisabledControlNormal(c);
			}
			catch
			{
				return 0f;
			}
		}
	}

	[DllImport("user32.dll")]
	private static extern uint GetRawInputDeviceList([Out] RawDev[] list, ref uint count, uint size);

	[DllImport("user32.dll")]
	private static extern uint GetRawInputDeviceInfo(IntPtr dev, uint cmd, ref RidInfo info, ref uint size);

	private static bool FindSony()
	{
		try
		{
			uint size = (uint)Marshal.SizeOf(typeof(RawDev));
			uint count = 0u;
			if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue || count == 0)
			{
				return false;
			}
			RawDev[] array = new RawDev[count + 8];
			count = (uint)array.Length;
			uint rawInputDeviceList = GetRawInputDeviceList(array, ref count, size);
			if (rawInputDeviceList == uint.MaxValue)
			{
				return false;
			}
			for (int i = 0; i < rawInputDeviceList && i < array.Length; i++)
			{
				if (array[i].Type == 2)
				{
					RidInfo info = new RidInfo
					{
						Size = 32u
					};
					uint size2 = 32u;
					if (GetRawInputDeviceInfo(array[i].Handle, 536870923u, ref info, ref size2) != uint.MaxValue && info.Type == 2 && info.Vendor == 1356 && info.UsagePage == 1 && (info.Usage == 4 || info.Usage == 5 || info.Usage == 8))
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static float Stick(short v)
	{
		float num = (float)v / 32767f;
		if (!(Math.Abs(num) < 0.2f))
		{
			return Math.Max(-1f, Math.Min(1f, num));
		}
		return 0f;
	}

	public static bool Held(ushort b)
	{
		return (_now & b) != 0;
	}

	public static bool Pressed(ushort b)
	{
		if ((_now & b) != 0)
		{
			return (_prev & b) == 0;
		}
		return false;
	}
}
