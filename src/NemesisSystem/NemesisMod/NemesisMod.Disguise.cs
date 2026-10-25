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
	private int _nextMaskCheck;

	internal string WearingNote = "";

	internal int DisguiseSaves;

	private const ulong NumComponents = 10394376663872179841uL;

	private const ulong ShopItemAt = 8627268931001909819uL;

	private static uint _maskUp;

	private int _lastMount;

	private int _lastVehicle;

	internal unsafe bool PlayerMasked(Ped player)
	{
		if (Game.GameTime < _nextMaskCheck)
		{
			return _masked;
		}
		_nextMaskCheck = Game.GameTime + 400;
		if (_maskUp == 0)
		{
			_maskUp = MISC.GET_HASH_KEY("mask_up");
		}
		bool flag = false;
		int num = Function.Call<int>(10394376663872179841uL, new InputArgument[1] { player.Handle });
		ulong* ptr = stackalloc ulong[1];
		ulong* ptr2 = stackalloc ulong[1];
		int num2 = 0;
		for (int i = 1; i < 2; i++)
		{
			if (flag)
			{
				break;
			}
			for (int j = 0; j < num && j < 64; j++)
			{
				if (flag)
				{
					break;
				}
				*ptr = 0uL;
				*ptr2 = 0uL;
				if (Function.Call<uint>(8627268931001909819uL, new InputArgument[5]
				{
					player.Handle,
					j,
					i == 1,
					new InputArgument((ulong)ptr),
					new InputArgument((ulong)ptr2)
				}) != 0)
				{
					num2++;
					if ((uint)(*ptr2 & 0xFFFFFFFFu) == _maskUp)
					{
						flag = true;
					}
				}
			}
		}
		if (!flag)
		{
			HashSet<uint> hashSet = new HashSet<uint>(from c in Config.DisguiseCategories.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				select MISC.GET_HASH_KEY(c.Trim().ToUpperInvariant()));
			if (hashSet.Contains(1963323202u))
			{
				hashSet.Add(1249071452u);
			}
			int num3 = Function.Call<int>(11971384123627577485uL, new InputArgument[1] { player.Handle });
			for (int num4 = 0; num4 < num3 && num4 < 48; num4++)
			{
				if (flag)
				{
					break;
				}
				if (hashSet.Contains(Function.Call<uint>(14751957644553774639uL, new InputArgument[2] { player.Handle, num4 })))
				{
					flag = true;
				}
			}
			HashSet<uint> hashSet2 = new HashSet<uint>();
			for (int num5 = 0; num5 < num && num5 < 64; num5++)
			{
				uint num6 = Function.Call<uint>(11209604758464182439uL, new InputArgument[3] { player.Handle, num5, 0 });
				if (num6 != 0)
				{
					hashSet2.Add(num6);
					if (hashSet.Contains(num6))
					{
						flag = true;
					}
				}
			}
			if (num > 0 && num2 >= num - 1 && _lastPieceCats != null && !hashSet2.SetEquals(_lastPieceCats))
			{
				List<string> list = hashSet2.Except(_lastPieceCats).Select(CatName).ToList();
				List<string> list2 = _lastPieceCats.Except(hashSet2).Select(CatName).ToList();
				GrudgeLog(string.Format("DISGUISE clothes changed:{0}{1} - masked {2}", (list.Count > 0) ? (" on " + string.Join(", ", list)) : "", (list2.Count > 0) ? (" off " + string.Join(", ", list2)) : "", flag));
			}
			if (num > 0 && num2 >= num - 1)
			{
				_lastPieceCats = hashSet2;
			}
		}
		bool masked = _masked;
		_masked = flag && Config.DisguiseEnabled;
		if (_masked != masked)
		{
			GrudgeLog(string.Format("DISGUISE {0} ({1} components, {2} read)", _masked ? "ON" : "off", num, num2));
		}
		return _masked;
	}
}
