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
	private Dictionary<string, int> Grievances => Chronicle.Grievance ?? (Chronicle.Grievance = new Dictionary<string, int>());

	internal int GrievanceOf(string a, string b)
	{
		if (a == null || b == null || !Grievances.TryGetValue(PairKey(a, b), out var value))
		{
			return 0;
		}
		return value;
	}

	internal void AddGrievance(string a, string b, int amount, string why)
	{
		if (a != null && b != null && !(a == b) && amount > 0 && Gangs.ById(a) != null && Gangs.ById(b) != null)
		{
			string key = PairKey(a, b);
			int num = GrievanceOf(a, b);
			int num2 = Math.Min(80, num + amount);
			if (num2 != num)
			{
				Grievances[key] = num2;
				SaveWorld();
				GrudgeLog($"GRIEVANCE {Gangs.ById(a).Name} / {Gangs.ById(b).Name}: {num} -> {num2} ({why})");
			}
		}
	}
}
