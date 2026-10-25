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
	internal void InitHeat(WarRecord w)
	{
		if (w != null && !w.HeatSet)
		{
			int num = Relation(w.A, w.B);
			w.Heat = Math.Max(25, Math.Min(80, 35 + Math.Min(25, Math.Max(0, (-num - 35) / 2)) + GrievanceOf(w.A, w.B) / 4));
			w.HeatSet = true;
			if (w.GoalLog == null)
			{
				w.GoalLog = new List<string>();
			}
			if (w.GoalLog.Count == 0 && w.GoalText != null)
			{
				w.GoalLog.Add($"{GameDate.FromMinutes(w.StartMin):MMM d}: {w.GoalText}");
			}
		}
	}

	internal void AddHeat(WarRecord w, int n, string why)
	{
		if (w != null && n != 0)
		{
			InitHeat(w);
			int heat = w.Heat;
			w.Heat = Math.Max(0, Math.Min(100, w.Heat + n));
			if (w.Heat != heat)
			{
				GrudgeLog($"WAR heat {w.District}: {heat} -> {w.Heat} ({why})");
				SaveWorld();
			}
		}
	}

	internal static string HeatWord(int h)
	{
		if (h >= 20)
		{
			if (h >= 40)
			{
				if (h >= 60)
				{
					if (h >= 80)
					{
						return "at its worst";
					}
					return "raging";
				}
				return "burning";
			}
			return "smouldering";
		}
		return "dying down";
	}

	internal static (int r, int g, int b) HeatColor(int h)
	{
		if (h >= 20)
		{
			if (h >= 40)
			{
				if (h >= 60)
				{
					return Theme.Blood;
				}
				return Theme.Warn;
			}
			return (r: 190, g: 160, b: 90);
		}
		return Theme.Dim;
	}

	private float HeatPeaceMult(WarRecord w)
	{
		InitHeat(w);
		if (w.Heat >= 20)
		{
			if (w.Heat >= 35)
			{
				if (w.Heat <= 75)
				{
					return 1f;
				}
				return 0.5f;
			}
			return 1.6f;
		}
		return 2.5f;
	}
}
