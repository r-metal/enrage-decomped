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
	private readonly Dictionary<int, int> _restrainedAt = new Dictionary<int, int>();

	private readonly HashSet<int> _tiedCounted = new HashSet<int>();

	internal List<NemesisRecord> FriendsOf(NemesisRecord r)
	{
		if (r?.FriendIds != null)
		{
			return (from id in r.FriendIds
				select Roster.FirstOrDefault((NemesisRecord x) => x.Id == id) into x
				where x != null
				select x).ToList();
		}
		return new List<NemesisRecord>();
	}

	private void RollFriends()
	{
		bool flag = false;
		foreach (NemesisRecord r in Roster.Where((NemesisRecord x) => !x.FriendsRolled && x.Id != null).ToList())
		{
			r.FriendsRolled = true;
			flag = true;
			if (r.FriendIds == null)
			{
				r.FriendIds = new List<string>();
			}
			if (!r.IsAlive || _rng.Next(100) >= ((r.Gang != null) ? Traits.FriendChanceGang : Traits.FriendChanceLoner))
			{
				continue;
			}
			int num = ((_rng.Next(100) >= 30) ? 1 : 2);
			for (int num2 = 0; num2 < num; num2++)
			{
				if (r.FriendIds.Count >= 3)
				{
					break;
				}
				bool other = _rng.Next(100) < Traits.FriendOtherFaction;
				List<NemesisRecord> list = Roster.Where(delegate(NemesisRecord x)
				{
					if (x != r && x.IsAlive && x.Id != null && !r.FriendIds.Contains(x.Id) && (x.FriendIds?.Count ?? 0) < 3 && x.KinId != r.Id && r.KinId != x.Id)
					{
						if (!other)
						{
							return x.Gang == r.Gang;
						}
						if (x.Gang != r.Gang)
						{
							Gang gang = Gangs.Of(x);
							if (gang == null || !gang.KeepsToItsOwn)
							{
								Gang gang2 = Gangs.Of(r);
								if (gang2 == null)
								{
									return true;
								}
								return !gang2.KeepsToItsOwn;
							}
						}
						return false;
					}
					return false;
				}).ToList();
				if (list.Count != 0)
				{
					NemesisRecord nemesisRecord = list[_rng.Next(list.Count)];
					if (nemesisRecord.FriendIds == null)
					{
						nemesisRecord.FriendIds = new List<string>();
					}
					r.FriendIds.Add(nemesisRecord.Id);
					nemesisRecord.FriendIds.Add(r.Id);
					GrudgeLog("FRIENDS " + r.Name + " (" + (r.Gang ?? "loner") + ") <-> " + nemesisRecord.Name + " (" + (nemesisRecord.Gang ?? "loner") + ")");
				}
			}
		}
		if (flag)
		{
			SaveRoster();
		}
	}
}
