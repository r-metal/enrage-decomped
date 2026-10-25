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
	private readonly HashSet<(int, string)> _affilTold = new HashSet<(int, string)>();

	private string RollAffiliation(Ped p)
	{
		if (p == null || Gangs.ForModel((uint)p.Model.Hash) != null || IsLawman(p) || Kids.IsChild(p) || IsStoryPed(p))
		{
			return null;
		}
		if ((float)_rng.Next(1000) >= Config.AffiliatedPercent * 10f)
		{
			return null;
		}
		if (South)
		{
			if (!KlanCountry() || _rng.Next(100) >= 30)
			{
				return Gangs.LemoyneRaiders.Id;
			}
			return Gangs.Klan.Id;
		}
		string result = (Gangs.InEra(Gangs.ODriscolls) ? Gangs.ODriscolls.Id : Gangs.LaramieGang.Id);
		if (North)
		{
			return result;
		}
		if (_rng.Next(2) != 0)
		{
			if (!Gangs.InEra(Gangs.ODriscolls))
			{
				return Gangs.DelLobos.Id;
			}
			return Gangs.LemoyneRaiders.Id;
		}
		return result;
	}

	private bool KlanCountry()
	{
		return KlanDistricts.Contains(_regionDistrict);
	}

	internal string AffiliationOf(int handle)
	{
		if (_named.TryGetValue(handle, out var value) && value.Affil != null)
		{
			return value.Affil;
		}
		if (_acqOf.TryGetValue(handle, out var value2) && value2.Affil != null)
		{
			return value2.Affil;
		}
		return RecordAt(handle)?.Affiliation;
	}

	private void AffiliateWronged(int handle, string crime)
	{
		string text = AffiliationOf(handle);
		if (text != null && crime != null && _affilTold.Add((handle, crime)))
		{
			Gang gang = Gangs.ById(text);
			if (gang != null)
			{
				string text2 = WhoIs(handle);
				AddNotoriety(gang, (!(crime == "kill") && !(crime == "gore")) ? 1 : 2, text2 + " was a friend of theirs (" + crime + ")");
				Feed("FRIEND OF THE " + gang.Name.ToUpperInvariant() + ": " + text2 + " - word of it will reach them", gang.Accent);
				GrudgeLog("AFFILIATE " + text2 + " (" + gang.Id + ") wronged: " + crime);
			}
		}
	}
}
