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
	internal static bool IsEpiloguePlayer()
	{
		try
		{
			Ped character = Game.Player.Character;
			return character != null && character.Exists() && Models.Name(character) == "player_three";
		}
		catch
		{
			return false;
		}
	}

	internal static string YouId
	{
		get
		{
			if (!IsEpiloguePlayer())
			{
				return "vdl_arthur";
			}
			return "vdl_john";
		}
	}

	internal static string YouShort
	{
		get
		{
			if (!IsEpiloguePlayer())
			{
				return "Arthur";
			}
			return "John";
		}
	}
}
