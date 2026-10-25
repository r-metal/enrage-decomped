using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NemesisSystem.BulletImpact;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;

namespace NemesisSystem;

public partial class NemesisMod
{
	private const double RunningLineMinutes = 10.0;

	private static readonly string[] RepPrefixes = new string[13]
	{
		"WITNESSES", "UNEASY", "RECOGNISED", "REPORTED", "KEPT QUIET", "SILENCED", "GRUDGE", "WOUNDED", "SHOT", "FLED",
		"SOCIAL", "FAVOUR", "KNOWN"
	};

	internal void Feed(string text, (int r, int g, int b)? color, NemesisRecord rec)
	{
		if (rec != null)
		{
			text = Pron.Her(text, Models.Female(rec.ModelHash));
		}
		Feed(text, color);
		if ((rec == null || _interro == null || _interro.Rec != rec || text.StartsWith("INTEL") || text.StartsWith("PAST") || text.StartsWith("WANTS")) && rec != null)
		{
			History(rec, text, color);
		}
	}

	private static string[] Months => GameDate.Months;

	internal void History(NemesisRecord rec, string text, (int r, int g, int b)? color = null)
	{
		if (rec == null)
		{
			return;
		}
		text = UiDraw.TheFix(text);
		if (rec.Name != null)
		{
			text = text.Replace(" " + rec.Name + " ", " ").Replace(" " + rec.Name + ":", ":").Replace(" " + rec.Name, "");
		}
		text = Pron.Her(text, Models.Female(rec.ModelHash));
		if (rec.History == null)
		{
			rec.History = new List<HistoryEntry>();
		}
		int cLOCK_MONTH = CLOCK.GET_CLOCK_MONTH();
		string when = $"{Months[Math.Max(0, Math.Min(11, cLOCK_MONTH))]} {CLOCK.GET_CLOCK_DAY_OF_MONTH()}  {CLOCK.GET_CLOCK_HOURS():00}:{CLOCK.GET_CLOCK_MINUTES():00}";
		if (_historyAt.HasValue)
		{
			DateTime dateTime = GameDate.FromMinutes(_historyAt.Value);
			when = $"{Months[dateTime.Month - 1]} {dateTime.Day}  {dateTime.Hour:00}:{dateTime.Minute:00}";
		}
		HistoryEntry historyEntry = ((rec.History.Count > 0) ? rec.History[rec.History.Count - 1] : null);
		if (historyEntry != null && IsRunningLine(text) && IsRunningLine(historyEntry.Text) && historyEntry.Secret == rec.PresumedDead && (DateTime.Now - historyEntry.At).TotalMinutes < 10.0 && HitsOf(text) > HitsOf(historyEntry.Text))
		{
			historyEntry.Text = text;
			historyEntry.At = DateTime.Now;
			return;
		}
		rec.History.Add(new HistoryEntry
		{
			At = DateTime.Now,
			When = when,
			Text = text,
			Color = (color ?? Theme.Text),
			Secret = rec.PresumedDead
		});
		if (rec.History.Count > 60)
		{
			rec.History.RemoveAt(0);
		}
	}

	private static bool IsRunningLine(string t)
	{
		return t?.StartsWith("PUNCHED (", StringComparison.Ordinal) ?? false;
	}

	private static int HitsOf(string t)
	{
		int num = t.IndexOf('(');
		int num2 = ((num < 0) ? (-1) : t.IndexOf(' ', num));
		if (num < 0 || num2 <= num || !int.TryParse(t.Substring(num + 1, num2 - num - 1), out var result))
		{
			return 0;
		}
		return result;
	}

	private void CollapsePunchRuns()
	{
		int num = 0;
		foreach (NemesisRecord item in Roster)
		{
			List<HistoryEntry> history = item.History;
			if (history == null || history.Count < 2)
			{
				continue;
			}
			List<HistoryEntry> list = new List<HistoryEntry>(history.Count);
			foreach (HistoryEntry item2 in history)
			{
				HistoryEntry historyEntry = ((list.Count > 0) ? list[list.Count - 1] : null);
				if (historyEntry != null && IsRunningLine(historyEntry.Text) && IsRunningLine(item2.Text) && historyEntry.Secret == item2.Secret && Math.Abs((item2.At - historyEntry.At).TotalMinutes) < 10.0 && HitsOf(item2.Text) > HitsOf(historyEntry.Text))
				{
					historyEntry.Text = item2.Text;
					historyEntry.At = item2.At;
					num++;
				}
				else
				{
					list.Add(item2);
				}
			}
			if (list.Count != history.Count)
			{
				item.History = list;
			}
		}
		if (num != 0)
		{
			SaveRoster();
			GrudgeLog($"HISTORY {num} repeated PUNCHED lines folded into one a fight");
		}
	}

	internal void Feed(string text, (int r, int g, int b)? color = null)
	{
		text = UiDraw.TheFix(text);
		_feed.Add((UiDraw.RealMs, text, color ?? Theme.Text));
		if (_feed.Count > 30)
		{
			_feed.RemoveAt(0);
		}
		GrudgeLog("FEED " + text);
		string[] repPrefixes = RepPrefixes;
		foreach (string value in repPrefixes)
		{
			if (text.StartsWith(value))
			{
				RepNote(text);
				break;
			}
		}
	}

	internal IEnumerable<(int at, string text, (int r, int g, int b) c)> FeedLines => _feed;

	private NemesisRecord RecordAt(int handle)
	{
		if (_activeNemesisPed != null && handle == _activeNemesisPed.Handle)
		{
			return ActiveRecord;
		}
		if (_worldNemeses.TryGetValue(handle, out var value))
		{
			return value;
		}
		TownMan townMan = TownManFor(handle);
		if (townMan != null)
		{
			return townMan.Rec;
		}
		if (_sightingPed != null && handle == _sightingPed.Handle)
		{
			return _sightingRec;
		}
		return null;
	}

	private string WhoIs(int handle)
	{
		NemesisRecord nemesisRecord = RecordAt(handle);
		if (nemesisRecord != null)
		{
			return nemesisRecord.Name;
		}
		if (_named.TryGetValue(handle, out var value))
		{
			if (!value.Met)
			{
				return value.Name + " (stranger)";
			}
			return value.Name;
		}
		return "a stranger";
	}

	private static string Delta(int before, int after)
	{
		if (before != after)
		{
			return $"{before} -> {after}";
		}
		return $"{after}";
	}
}
