using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace NemesisSystem.BulletImpact;

internal sealed class XRayLog
{
	private const int ShootoutGapSeconds = 90;

	private readonly string _path;

	private readonly int _maxEntries;

	private bool _dirty;

	private DateTime _lastSave = DateTime.MinValue;

	internal static long LastSaveMs;

	internal static int SaveCount;

	internal static bool Hold;

	public List<XRayEntry> Entries { get; private set; } = new List<XRayEntry>();

	public XRayLog(string path, int maxEntries)
	{
		_path = path;
		_maxEntries = Math.Max(1, maxEntries);
		Entries = Clean(SafeFile.Read<List<XRayEntry>>(path));
	}

	private static List<XRayEntry> Clean(List<XRayEntry> list)
	{
		if (list != null)
		{
			return list.Where((XRayEntry x) => x != null && x.Bones != null).ToList();
		}
		return new List<XRayEntry>();
	}

	public void Add(XRayEntry e)
	{
		XRayEntry xRayEntry = Entries.LastOrDefault();
		e.Id = (xRayEntry?.Id ?? 0) + 1;
		e.ShootoutId = ((xRayEntry == null) ? 1 : (((e.Time - xRayEntry.Time).TotalSeconds > 90.0) ? (xRayEntry.ShootoutId + 1) : xRayEntry.ShootoutId));
		Entries.Add(e);
		int num = Entries.Count((XRayEntry x) => x.Animal == e.Animal);
		if (num > _maxEntries)
		{
			int drop = num - _maxEntries;
			Entries.RemoveAll((XRayEntry x) => x.Animal == e.Animal && drop-- > 0);
		}
		_dirty = true;
	}

	public void MarkDirty()
	{
		_dirty = true;
	}

	public void Replace(XRayEntry old, XRayEntry now)
	{
		int num = Entries.IndexOf(old);
		if (num >= 0)
		{
			Entries[num] = now;
			_dirty = true;
		}
	}

	public void Reload()
	{
		Entries = Clean(SafeFile.Read<List<XRayEntry>>(_path));
		_dirty = false;
	}

	public void Clear()
	{
		Entries.Clear();
		_dirty = true;
		SaveIfDirty(force: true);
	}

	public void SaveInBackground()
	{
		if (!_dirty)
		{
			return;
		}
		List<XRayEntry> snap = Entries.ToList();
		SafeFile.WriteLater(_path, () => JsonConvert.SerializeObject(snap), keepBak: true, delegate(Exception err)
		{
			if (err != null)
			{
				_dirty = true;
			}
		});
		_dirty = false;
		_lastSave = DateTime.Now;
	}

	public void SaveIfDirty(bool force = false)
	{
		if (!_dirty || (!force && ((DateTime.Now - _lastSave).TotalSeconds < 10.0 || Hold)))
		{
			return;
		}
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			if (force)
			{
				if (SafeFile.WaitWrites())
				{
					SafeFile.Write(_path, JsonConvert.SerializeObject(Entries), keepBak: true);
				}
				else
				{
					List<XRayEntry> snap = Entries.ToList();
					SafeFile.WriteLater(_path, () => JsonConvert.SerializeObject(snap), keepBak: true, delegate(Exception err)
					{
						if (err != null)
						{
							_dirty = true;
						}
					});
				}
			}
			else
			{
				List<XRayEntry> snap2 = Entries.ToList();
				SafeFile.WriteLater(_path, () => JsonConvert.SerializeObject(snap2), keepBak: true, delegate(Exception err)
				{
					if (err != null)
					{
						_dirty = true;
					}
				});
			}
			_dirty = false;
		}
		catch
		{
		}
		finally
		{
			_lastSave = DateTime.Now;
			LastSaveMs = stopwatch.ElapsedMilliseconds;
			SaveCount++;
		}
	}
}
