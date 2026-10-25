using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;

namespace NemesisSystem;

internal static class Crumbs
{
	private const int Line = 128;

	private const int RingSize = 64;

	private const int Size = 8320;

	private static MemoryMappedFile _map;

	private static MemoryMappedViewAccessor _view;

	private static readonly byte[] Buf = new byte[128];

	private static long _seq;

	public static string Previous;

	public static string PreviousCalls;

	private static string _dir;

	private static string _previousAll;

	private static string _lastWhat;

	private static int _repeat;

	public static void Init(string dir)
	{
		_dir = dir;
		string path = Path.Combine(dir, "lastframe.txt");
		try
		{
			if (File.Exists(path))
			{
				string text = File.ReadAllText(path).Replace("\0", " ");
				List<string> list = (from i in Enumerable.Range(0, (text.Length + 128 - 1) / 128)
					select text.Substring(i * 128, Math.Min(128, text.Length - i * 128)).Trim()).ToList();
				Previous = ((list.Count > 0) ? list[0] : text.Trim());
				List<(long seq, string what)> list2 = new List<(long seq, string what)>();
				foreach (string item in list.Skip(1))
				{
					int num = item.IndexOf(' ');
					if (num > 0 && long.TryParse(item.Substring(0, num), out var result))
					{
						list2.Add((result, item.Substring(num + 1)));
					}
				}
				if (list2.Count > 0)
				{
					PreviousCalls = string.Join("  |  ", from c in list2.OrderBy(((long seq, string what) c) => c.seq).Skip(Math.Max(0, list2.Count - 12))
						select c.what);
				}
				_previousAll = string.Join(Environment.NewLine, new string[1] { Previous }.Concat(from c in list2
					orderby c.seq
					select c.what));
			}
		}
		catch
		{
		}
		try
		{
			using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
			{
				fileStream.SetLength(8320L);
			}
			_map = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 8320L, MemoryMappedFileAccess.ReadWrite);
			_view = _map.CreateViewAccessor(0L, 8320L);
		}
		catch
		{
			_view = null;
			_map = null;
		}
	}

	private static void Put(int slot, string s)
	{
		if (_view == null)
		{
			return;
		}
		try
		{
			for (int i = Encoding.ASCII.GetBytes((s.Length > 127) ? s.Substring(0, 127) : s, 0, Math.Min(s.Length, 127), Buf, 0); i < 127; i++)
			{
				Buf[i] = 32;
			}
			Buf[127] = 10;
			_view.WriteArray(slot * 128, Buf, 0, 128);
		}
		catch
		{
		}
	}

	public static void Mark(string what)
	{
		Put(0, $"{DateTime.Now:HH:mm:ss.fff} {what}");
	}

	public static void Native(string what)
	{
		if (what == _lastWhat && _seq > 0)
		{
			_repeat++;
			Put(1 + (int)(_seq % 64), $"{_seq} {DateTime.Now:HH:mm:ss.fff} {what} x{_repeat}");
		}
		else
		{
			_lastWhat = what;
			_repeat = 1;
			long num = ++_seq;
			Put(1 + (int)(num % 64), $"{num} {DateTime.Now:HH:mm:ss.fff} {what}");
		}
	}

	public static void SaveCrash(string header)
	{
		if (_dir == null || _previousAll == null)
		{
			return;
		}
		try
		{
			File.WriteAllText(Path.Combine(_dir, "lastcrash.txt"), header + Environment.NewLine + Environment.NewLine + _previousAll + Environment.NewLine);
		}
		catch
		{
		}
	}

	public static void Close(string why)
	{
		if (_view != null)
		{
			Mark("CLEAN EXIT: " + why);
			try
			{
				_view?.Flush();
				_view?.Dispose();
				_map?.Dispose();
			}
			catch
			{
			}
			_view = null;
			_map = null;
		}
	}
}
