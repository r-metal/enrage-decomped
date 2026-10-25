using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace NemesisSystem;

public static class SafeFile
{
	public const long MaxLogBytes = 5242880L;

	public const int KeepLogs = 3;

	public const int KeepDaily = 7;

	private static readonly object _qLock = new object();

	private static readonly Queue<Action> _queue = new Queue<Action>();

	private static Thread _writer;

	private static int _busy;

	[ThreadStatic]
	private static DateTime _budgetUntil;

	[ThreadStatic]
	private static bool _budgetOn;

	private static readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public static Action<string> Log;

	public static string PendingNotice;

	public static bool Idle
	{
		get
		{
			lock (_qLock)
			{
				return _queue.Count == 0 && _busy == 0;
			}
		}
	}

	public static void WriteLater(string path, Func<string> make, bool keepBak, Action<Exception> done)
	{
		lock (_qLock)
		{
			_queue.Enqueue(delegate
			{
				Exception obj = null;
				try
				{
					Write(path, make(), keepBak);
				}
				catch (Exception ex)
				{
					obj = ex;
				}
				try
				{
					done?.Invoke(obj);
				}
				catch
				{
				}
			});
			if (_writer == null)
			{
				_writer = new Thread(WriterLoop)
				{
					IsBackground = true,
					Name = "enRAGE writer",
					Priority = ThreadPriority.BelowNormal
				};
				_writer.Start();
			}
			Monitor.PulseAll(_qLock);
		}
	}

	public static void WriteLater(string path, string text, bool keepBak, Action<Exception> done)
	{
		lock (_qLock)
		{
			_queue.Enqueue(delegate
			{
				Exception obj = null;
				try
				{
					Write(path, text, keepBak);
				}
				catch (Exception ex)
				{
					obj = ex;
				}
				try
				{
					done?.Invoke(obj);
				}
				catch
				{
				}
			});
			if (_writer == null)
			{
				_writer = new Thread(WriterLoop)
				{
					IsBackground = true,
					Name = "enRAGE writer",
					Priority = ThreadPriority.BelowNormal
				};
				_writer.Start();
			}
			Monitor.PulseAll(_qLock);
		}
	}

	private static void WriterLoop()
	{
		while (true)
		{
			Action action;
			lock (_qLock)
			{
				while (_queue.Count == 0)
				{
					Monitor.Wait(_qLock);
				}
				action = _queue.Dequeue();
				_busy++;
			}
			try
			{
				action();
			}
			catch
			{
			}
			lock (_qLock)
			{
				_busy--;
				Monitor.PulseAll(_qLock);
			}
		}
	}

	public static bool BeginWaitBudget(int ms)
	{
		if (_budgetOn)
		{
			return false;
		}
		_budgetOn = true;
		_budgetUntil = DateTime.UtcNow.AddMilliseconds(ms);
		return true;
	}

	public static void EndWaitBudget(bool started)
	{
		if (started)
		{
			_budgetOn = false;
		}
	}

	public static int BudgetMs(int ms)
	{
		if (_budgetOn)
		{
			return Math.Max(0, Math.Min(ms, (int)(_budgetUntil - DateTime.UtcNow).TotalMilliseconds));
		}
		return ms;
	}

	public static bool WaitWrites(int ms = 3000)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ms);
		if (_budgetOn && _budgetUntil < dateTime)
		{
			dateTime = _budgetUntil;
		}
		lock (_qLock)
		{
			while (_queue.Count > 0 || _busy > 0)
			{
				TimeSpan timeSpan = dateTime - DateTime.UtcNow;
				if (timeSpan <= TimeSpan.Zero)
				{
					return false;
				}
				Monitor.Wait(_qLock, timeSpan);
			}
		}
		return true;
	}

	public static bool IsBlocked(string path)
	{
		lock (_blocked)
		{
			return _blocked.Contains(path);
		}
	}

	public static void UnblockAll()
	{
		lock (_blocked)
		{
			_blocked.Clear();
		}
	}

	private static void Block(string path)
	{
		lock (_blocked)
		{
			if (!_blocked.Add(path))
			{
				return;
			}
		}
		Log?.Invoke("LOAD " + Path.GetFileName(path) + ": no copy could be read - running on an empty one, and it won't be saved over this session (the bad copies are kept as .unreadable-*)");
		PendingNotice = Path.GetFileName(path) + " couldn't be read - the mod won't save over it this session. The copies are kept beside it (.unreadable-*).";
	}

	public static void Write(string path, string text, bool keepBak = false)
	{
		if (!IsBlocked(path))
		{
			string text2 = path + ".tmp";
			File.WriteAllText(text2, text);
			if (File.Exists(path))
			{
				File.Replace(text2, path, keepBak ? (path + ".bak") : null);
			}
			else
			{
				File.Move(text2, path);
			}
		}
	}

	public static T Read<T>(string path, Action<string> log, out bool found, out string from) where T : class
	{
		found = false;
		from = null;
		string[] array = new string[3]
		{
			path,
			path + ".bak",
			path + ".tmp"
		};
		foreach (string text in array)
		{
			if (!File.Exists(text))
			{
				continue;
			}
			found = true;
			try
			{
				T val = JsonConvert.DeserializeObject<T>(File.ReadAllText(text));
				if (val == null)
				{
					throw new Exception("empty");
				}
				from = text;
				if (text != path)
				{
					log?.Invoke("LOAD " + Path.GetFileName(path) + " unreadable or missing - read " + Path.GetFileName(text) + " instead");
				}
				return val;
			}
			catch (Exception ex)
			{
				try
				{
					long len = new FileInfo(text).Length;
					if (!Directory.GetFiles(Path.GetDirectoryName(text) ?? ".", Path.GetFileName(text) + ".unreadable-*").Any((string f) => new FileInfo(f).Length == len))
					{
						File.Copy(text, text + $".unreadable-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
					}
				}
				catch
				{
				}
				log?.Invoke("LOAD " + Path.GetFileName(text) + " unreadable (" + ex.Message + ") - kept as .unreadable-*, trying the next copy");
			}
		}
		if (found)
		{
			Block(path);
		}
		return null;
	}

	public static T Read<T>(string path, Action<string> log = null) where T : class
	{
		bool found;
		string from;
		return Read<T>(path, log, out found, out from);
	}

	public static void RunLater(Action job)
	{
		lock (_qLock)
		{
			_queue.Enqueue(delegate
			{
				try
				{
					job();
				}
				catch
				{
				}
			});
			if (_writer == null)
			{
				_writer = new Thread(WriterLoop)
				{
					IsBackground = true,
					Name = "enRAGE writer",
					Priority = ThreadPriority.BelowNormal
				};
				_writer.Start();
			}
			Monitor.PulseAll(_qLock);
		}
	}

	public static void AppendLater(string path, string text)
	{
		lock (_qLock)
		{
			_queue.Enqueue(delegate
			{
				try
				{
					Append(path, text);
				}
				catch
				{
				}
			});
			if (_writer == null)
			{
				_writer = new Thread(WriterLoop)
				{
					IsBackground = true,
					Name = "enRAGE writer",
					Priority = ThreadPriority.BelowNormal
				};
				_writer.Start();
			}
			Monitor.PulseAll(_qLock);
		}
	}

	public static void Append(string path, string text)
	{
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			if (fileInfo.Exists && fileInfo.Length > 5242880)
			{
				Rotate(path);
			}
		}
		catch
		{
		}
		File.AppendAllText(path, text);
	}

	private static void Rotate(string path)
	{
		if (File.Exists(Old(3)))
		{
			File.Delete(Old(3));
		}
		for (int num = 2; num >= 1; num--)
		{
			if (File.Exists(Old(num)))
			{
				File.Move(Old(num), Old(num + 1));
			}
		}
		File.Move(path, Old(1));
		string Old(int i)
		{
			return $"{path}.{i}";
		}
	}

	public static bool Daily(string path, string backupDir)
	{
		try
		{
			if (!File.Exists(path))
			{
				return false;
			}
			Directory.CreateDirectory(backupDir);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
			string extension = Path.GetExtension(path);
			string text = Path.Combine(backupDir, $"{fileNameWithoutExtension}-{DateTime.Now:yyyyMMdd}{extension}");
			if (File.Exists(text))
			{
				return false;
			}
			File.Copy(path, text);
			foreach (string item in (from f in Directory.GetFiles(backupDir, fileNameWithoutExtension + "-????????" + extension)
				orderby f descending
				select f).Skip(7))
			{
				File.Delete(item);
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
