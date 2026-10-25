using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using RDR2;
using RDR2.Native;

namespace NemesisSystem;

internal static class UiFeed
{
	private const ulong PostSampleToast = 2803616216400160553uL;

	public static bool Enabled = false;

	public static Action<string> Breadcrumb;

	public static int Posted;

	public static int Failed;

	public static string LastError = "";

	private static readonly Queue<(string title, string text, string dict, string icon)> _queue = new Queue<(string, string, string, string)>();

	private static readonly List<(IntPtr a, IntPtr b, int at)> _live = new List<(IntPtr, IntPtr, int)>();

	private static int _nextAt;

	public static void Toast(string title, string text, string dict = "generic_textures", string icon = "tick")
	{
		if (Enabled)
		{
			if (_queue.Count >= 5)
			{
				_queue.Dequeue();
			}
			_queue.Enqueue((title, text, dict, icon));
		}
	}

	public static void Tick()
	{
		for (int num = _live.Count - 1; num >= 0; num--)
		{
			if (Game.GameTime - _live[num].at > 15000 || Game.GameTime < _live[num].at)
			{
				Marshal.FreeHGlobal(_live[num].a);
				Marshal.FreeHGlobal(_live[num].b);
				_live.RemoveAt(num);
			}
		}
		if (!Enabled || _queue.Count == 0 || Game.GameTime < _nextAt)
		{
			return;
		}
		(string, string, string, string) tuple = _queue.Dequeue();
		_nextAt = Game.GameTime + 4000;
		Breadcrumb?.Invoke("FEED toast posting: " + tuple.Item1 + " / " + tuple.Item2);
		IntPtr intPtr = IntPtr.Zero;
		IntPtr intPtr2 = IntPtr.Zero;
		try
		{
			intPtr = Marshal.AllocHGlobal(56);
			intPtr2 = Marshal.AllocHGlobal(64);
			for (int i = 0; i < 7; i++)
			{
				Marshal.WriteInt64(intPtr, i * 8, 0L);
			}
			for (int j = 0; j < 8; j++)
			{
				Marshal.WriteInt64(intPtr2, j * 8, 0L);
			}
			Marshal.WriteInt64(intPtr, 0, 4000L);
			Marshal.WriteInt64(intPtr2, 8, (long)UiDraw.ForeverVarString(UiDraw.Safe(tuple.Item1)));
			Marshal.WriteInt64(intPtr2, 16, (long)UiDraw.ForeverVarString(UiDraw.Safe(tuple.Item2)));
			Marshal.WriteInt64(intPtr2, 32, MISC.GET_HASH_KEY(tuple.Item3));
			Marshal.WriteInt64(intPtr2, 40, MISC.GET_HASH_KEY(tuple.Item4));
			Marshal.WriteInt64(intPtr2, 48, MISC.GET_HASH_KEY("COLOR_WHITE"));
			Function.Call(2803616216400160553uL, (ulong)intPtr.ToInt64(), (ulong)intPtr2.ToInt64(), 1, 1);
			_live.Add((intPtr, intPtr2, Game.GameTime));
			Posted++;
		}
		catch (Exception ex)
		{
			Failed++;
			LastError = ex.Message;
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
			if (intPtr2 != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr2);
			}
		}
	}
}
