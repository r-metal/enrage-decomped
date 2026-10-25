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
	internal static int XpNeed(int rank)
	{
		return 2 * Math.Max(1, rank);
	}

	internal void GainRankXp(NemesisRecord r, int amount, string why)
	{
		if (r == null || !r.IsAlive || amount <= 0 || r.Rank >= 5)
		{
			return;
		}
		if (Mind.Has(r, "Settle"))
		{
			GrudgeLog("RANK " + r.Name + ": no xp for " + why + " - he wants out");
			return;
		}
		double num = 0.5 + (double)Mind.F(r, "Ambition") / 100.0;
		if (Mind.Has(r, "Rise"))
		{
			num *= 1.5;
		}
		int num2 = Math.Max(1, (int)Math.Round((double)amount * num));
		r.RankXp = Math.Min(r.RankXp + num2, XpNeed(r.Rank));
		GrudgeLog($"RANK {r.Name} +{num2} xp ({why}, x{num:0.00}) -> {r.RankXp}/{XpNeed(r.Rank)} at rank {r.Rank}");
		if (r.Gang == null)
		{
			TryRankUp(r, why);
		}
		SaveRoster();
	}

	private bool TryRankUp(NemesisRecord r, string why)
	{
		if (r.Rank >= 5 || r.RankXp < XpNeed(r.Rank) || Mind.Has(r, "Settle"))
		{
			return false;
		}
		if (r.PresumedDead)
		{
			return false;
		}
		r.RankXp -= XpNeed(r.Rank);
		int rank = r.Rank;
		r.Rank++;
		History(r, $"ROSE TO RANK {r.Rank} - {why}", Theme.Gold);
		Remember(r, "Pride", 45, $"made a name for himself - rank {r.Rank}", null);
		Feed($"RANK {r.Name}: {rank} -> {r.Rank} ({why})", Theme.Gold, r);
		if (r == ActiveRecord && _activeNemesisPed != null && _activeNemesisPed.Exists())
		{
			ApplyRankStats(_activeNemesisPed, r, fullHeal: false);
		}
		GrudgeNotice($"{r.FullName} is getting a name for himself: rank {r.Rank}.");
		return true;
	}

	private int PromoteWithinTiers(Gang gang, List<NemesisRecord> living, List<(string, (int r, int g, int b))> lines, List<WorldPart> rankParts)
	{
		int num = 0;
		for (int t = 0; t < gang.Tiers.Length; t++)
		{
			int max = ((t == 0) ? 5 : (gang.Tiers[t - 1].MinRank - 1));
			foreach (NemesisRecord item in living.Where((NemesisRecord n) => n.Rank >= gang.Tiers[t].MinRank && n.Rank <= max && n.Rank < max && n.RankXp >= XpNeed(n.Rank) && !Mind.Has(n, "Settle")).ToList())
			{
				int rank = item.Rank;
				item.RankXp -= XpNeed(item.Rank);
				item.Rank++;
				string text = Backstory.Singular(gang.Tiers[t].Label);
				History(item, $"RANK {rank} -> {item.Rank} as {text} in the {gang.Name} - earned it", gang.Accent);
				Remember(item, "Pride", 40, $"moved up to rank {item.Rank}", null);
				lines.Add(($"{item.Name} earns rank {item.Rank} ({text})", Theme.Text));
				WorldPart worldPart = PartFor(item, "PROMOTED", "earned it");
				worldPart.Effects.Add($"rank {rank} -> {item.Rank}");
				rankParts.Add(worldPart);
				GrudgeLog($"RANK {gang.Id} {item.Name} {rank} -> {item.Rank} within {text} (xp left {item.RankXp})");
				num++;
			}
		}
		return num;
	}
}
