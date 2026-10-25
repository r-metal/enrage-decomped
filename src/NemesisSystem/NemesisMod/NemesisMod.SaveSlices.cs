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
	private sealed class SliceJob
	{
		public string Name;

		public string Path;

		public SlicedJson Json;

		public int Gen;

		public int Frames;

		public double Ms;

		public double MaxMs;

		public Action<Exception> Done;
	}

	private static readonly string[] PadGridRows = new string[3] { "ABCDEFGHIJ", "KLMNOPQRST", "UVWXYZ'-." };

	private static readonly string[] PadGridKeys = new string[3] { "SPACE", "DELETE", "RESULTS" };

	private bool Slicing(string name)
	{
		return _slices.Any((SliceJob j) => j.Name == name);
	}

	private void StartSlice(string name, string path, object root, int gen, Action<Exception> done)
	{
		_slices.Add(new SliceJob
		{
			Name = name,
			Path = path,
			Json = new SlicedJson(root),
			Gen = gen,
			Done = done
		});
	}

	private bool SliceStillGood(SliceJob j)
	{
		if (!(j.Name == "roster"))
		{
			if (j.Json.Root == _chron)
			{
				return j.Gen == _worldGen;
			}
			return false;
		}
		if (j.Json.Root == Roster)
		{
			return j.Gen == _rosterGen;
		}
		return false;
	}

	private void RunSlices(double budgetMs)
	{
		long t0;
		if (_slices.Count > 0)
		{
			t0 = _saveClock.ElapsedTicks;
			foreach (SliceJob item in _slices.ToList())
			{
				if (!SliceStillGood(item))
				{
					SetAside(item);
					continue;
				}
				double num = Spent();
				bool flag;
				try
				{
					do
					{
						flag = item.Json.Step();
					}
					while (!flag && Spent() < budgetMs);
				}
				catch (Exception ex)
				{
					SliceFailed(item, ex);
					continue;
				}
				double num2 = Spent() - num;
				item.Ms += num2;
				item.Frames++;
				if (num2 > item.MaxMs)
				{
					item.MaxMs = num2;
				}
				if (flag)
				{
					CompleteSlice(item);
				}
				if (!(Spent() >= budgetMs))
				{
					continue;
				}
				break;
			}
		}
		RunAfterSlices();
		double Spent()
		{
			return (double)(_saveClock.ElapsedTicks - t0) * 1000.0 / (double)Stopwatch.Frequency;
		}
	}

	private void CompleteSlice(SliceJob j)
	{
		_slices.Remove(j);
		if (!SliceStillGood(j))
		{
			SetAside(j);
			return;
		}
		SafeFile.WriteLater(j.Path, j.Json.Text, keepBak: true, j.Done);
		if (j.MaxMs >= 15.0)
		{
			GrudgeLog($"SAVETIME {j.Name} took {j.MaxMs:0} ms in one frame (in pieces: {j.Ms:0} ms over {j.Frames} frames)");
		}
	}

	private void SetAside(SliceJob j)
	{
		_slices.Remove(j);
		GrudgeLog("SAVE " + j.Name + " in pieces set aside - " + ((!(j.Name == "roster")) ? ((j.Json.Root == _chron) ? "saved whole meanwhile" : "the world was put back / reset") : ((j.Json.Root == Roster) ? "saved whole meanwhile" : "the roster was put back / reset")));
	}

	private void SliceFailed(SliceJob j, Exception ex)
	{
		_slices.Remove(j);
		if (j.Name == "roster")
		{
			_rosterDirty = true;
		}
		else
		{
			_worldDirty = true;
		}
		GrudgeLog("SAVE " + j.Name + " in pieces failed (" + ex.GetType().Name + ": " + ex.Message + ") - saved whole on the next pass");
	}

	private void FinishSlices(string why)
	{
		if (_slices.Count == 0)
		{
			RunAfterSlices();
			return;
		}
		foreach (SliceJob item in _slices.ToList())
		{
			if (!SliceStillGood(item))
			{
				SetAside(item);
				continue;
			}
			try
			{
				item.Json.Finish();
			}
			catch (Exception ex)
			{
				SliceFailed(item, ex);
				continue;
			}
			CompleteSlice(item);
		}
		RunAfterSlices();
	}

	private void AfterSlices(Action a)
	{
		if (_slices.Count == 0)
		{
			a();
		}
		else
		{
			_afterSlices.Add(a);
		}
	}

	private void RunAfterSlices()
	{
		if (_slices.Count > 0 || _afterSlices.Count == 0)
		{
			return;
		}
		List<Action> list = _afterSlices.ToList();
		_afterSlices.Clear();
		foreach (Action item in list)
		{
			try
			{
				item();
			}
			catch (Exception ex)
			{
				GrudgeLog("SAVE after the pieces failed: " + ex.Message);
			}
		}
	}

	private void NoteSaveTime(string name, double ms)
	{
		if (!(ms < 15.0))
		{
			bool flag = _combatants.Values.Any((int t) => Game.GameTime - t < 3000) || (_enc != null && _enc.Engaged);
			GrudgeLog(string.Format("SAVETIME {0} took {1:0} ms{2}", name, ms, flag ? " - IN A FIGHT" : ""));
		}
	}

	private void FlushPrints()
	{
		if (_printsDirty && Game.GameTime >= _nextPrintsSave)
		{
			_nextPrintsSave = Game.GameTime + 60000;
			SavePrints(later: true);
		}
	}

	private static int PadRowLen(int gy)
	{
		if (gy >= PadGridRows.Length)
		{
			return PadGridKeys.Length;
		}
		return PadGridRows[gy].Length;
	}
}
