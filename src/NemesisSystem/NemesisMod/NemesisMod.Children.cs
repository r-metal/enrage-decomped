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
	private sealed class Kid
	{
		public string Name;

		public string Home;

		public string Want;

		public string Quirk;

		public string Temper;

		public bool Boy;

		public bool Met;

		public int Age;

		public int SeenAt;

		public string Short => string.Format("{0}, {1}", Boy ? "Boy" : "Girl", Age);
	}

	private readonly Dictionary<int, Kid> _kids = new Dictionary<int, Kid>();

	private const int KidsCap = 60;

	private static readonly string[] BoyNames = new string[28]
	{
		"Tommy", "Billy", "Jimmy", "Johnny", "Eddie", "Charlie", "Frankie", "Georgie", "Harry", "Joey",
		"Sammy", "Danny", "Bobby", "Freddie", "Walter", "Albert", "Elmer", "Otis", "Homer", "Clyde",
		"Wesley", "Luther", "Amos", "Silas", "Willie", "Ollie", "Abe", "Ned"
	};

	private static readonly string[] GirlNames = new string[26]
	{
		"Annie", "Lizzie", "Mary", "Clara", "Ellie", "Hattie", "Lottie", "Minnie", "Nellie", "Rosie",
		"Daisy", "Ida", "Pearl", "Ruby", "Mabel", "Edna", "Effie", "Lula", "Bessie", "Grace",
		"Josie", "Cora", "Etta", "Maggie", "Winnie", "Polly"
	};

	private static readonly Dictionary<string, string[]> TownWork = new Dictionary<string, string[]>
	{
		["Valentine"] = new string[3] { "the stockyards", "the livery", "the doctor's" },
		["Saint Denis"] = new string[4] { "the docks", "the factory", "a laundry", "the trolley line" },
		["Rhodes"] = new string[3] { "the cotton fields", "the gunsmith's", "the general store" },
		["Annesburg"] = new string[3] { "the mine", "the coal tipple", "the fishery" },
		["Strawberry"] = new string[3] { "the lumber yard", "the hotel", "the doctor's" },
		["Blackwater"] = new string[4] { "the railroad", "the bank", "the stables", "the photo studio" },
		["Van Horn"] = new string[3] { "the trading post", "the saloon", "the lighthouse" },
		["Armadillo"] = new string[3] { "the railroad", "the general store", "the undertaker's" },
		["Tumbleweed"] = new string[3] { "the stables", "the saloon", "the mine up the hill" }
	};

	private static readonly string[] AnyWork = new string[5] { "the railroad", "a farm outside town", "the general store", "the saloon", "a ranch nearby" };

	private Kid KidFor(Ped p)
	{
		if (p == null || !p.Exists())
		{
			return null;
		}
		if (_kids.TryGetValue(p.Handle, out var value))
		{
			value.SeenAt = Game.GameTime;
			return value;
		}
		Random random = new Random(p.Handle ^ 0x5EED17);
		bool flag = !Models.Female((uint)p.Model.Hash);
		string text = null;
		try
		{
			text = TownAt(p.Position);
		}
		catch
		{
		}
		string[] value2;
		string[] array = ((text != null && TownWork.TryGetValue(text, out value2)) ? value2 : AnyWork);
		string text2 = array[random.Next(array.Length)];
		string text3 = (flag ? "his" : "her");
		string text4 = (flag ? "he" : "she");
		string text5 = NameBank.AmericanLast[random.Next(NameBank.AmericanLast.Length)];
		Kid kid = new Kid();
		kid.Boy = flag;
		kid.Age = 5 + random.Next(10);
		kid.SeenAt = Game.GameTime;
		kid.Temper = (new string[7] { "Hothead", "Proud", "Cold", "Coward", "Greedy", "Loyal", "Drifter" })[random.Next(7)];
		kid.Name = (flag ? BoyNames[random.Next(BoyNames.Length)] : GirlNames[random.Next(GirlNames.Length)]) + " " + text5;
		kid.Home = (new string[9]
		{
			Cap(text3) + " pa works " + text2,
			Cap(text3) + " ma works " + text2,
			"Lives above " + text2 + " with " + text3 + " folks",
			Cap(text3) + " folks farm outside " + (text ?? "town"),
			"An orphan - sleeps in the hayloft at the livery",
			"Lives with " + text3 + " grandmother by the river",
			Cap(text3) + " pa is a deputy",
			"Helps " + text3 + " ma take in washing",
			Cap(text3) + " pa went west for work and hasn't written"
		})[random.Next(9)];
		kid.Quirk = (new string[10]
		{
			"Keeps a frog in " + text3 + " pocket",
			"Sells newspapers on the corner",
			"Can't read yet - pretends " + text4 + " can",
			"Collects arrowheads",
			"Knows every horse in town by name",
			"Never wears shoes",
			"Has a slingshot " + text4 + " isn't supposed to have",
			"Draws on everything with charcoal",
			"Saves every penny " + text4 + " finds",
			"Talks to the dogs more than to people"
		})[random.Next(10)];
		kid.Want = (new string[10]
		{
			"wants to be a cowboy",
			"wants to be a lawman",
			"wants to drive a train",
			"wants to be a gunslinger like in the dime novels",
			"wants to be a doctor",
			"wants to see the ocean",
			"wants a horse of " + text3 + " own",
			"is saving up for a harmonica",
			"wants to be a trick rider in a show",
			"wants to go to school in the city"
		})[random.Next(10)];
		value = kid;
		if (_kids.Count >= 60)
		{
			int num = (from kv in _kids
				orderby kv.Value.Met ? 1 : 0, kv.Value.SeenAt
				select kv.Key).FirstOrDefault();
			if (num != 0)
			{
				_kids.Remove(num);
			}
		}
		_kids[p.Handle] = value;
		return value;
	}

	private void MeetKid(int handle, Kid k)
	{
		if (k == null)
		{
			return;
		}
		k.SeenAt = Game.GameTime;
		if (!k.Met)
		{
			k.Met = true;
			try
			{
				SetPromptName(handle, k.Name);
			}
			catch
			{
			}
			Feed($"CHILD {k.Name}, {k.Age} ({k.Temper.ToLowerInvariant()}). {k.Home}. {k.Quirk}. {Cap(k.Want)}.", Theme.Dim);
			GrudgeLog("CHILD met " + k.Name + " (" + k.Short + "): " + k.Home + "; " + k.Quirk + "; " + k.Want);
		}
	}

	private bool ChildPed(int handle, out Ped p)
	{
		p = Entity.FromHandle(handle) as Ped;
		if (p != null && p.Exists() && p.IsHuman && !p.IsPlayer && Kids.IsChild(p))
		{
			return !IsStoryPed(p);
		}
		return false;
	}

	private void ForgetKids()
	{
		foreach (KeyValuePair<int, Kid> item in _kids.ToList())
		{
			bool num = !ENTITY.DOES_ENTITY_EXIST(item.Key) || ENTITY.IS_ENTITY_DEAD(item.Key);
			int num2 = (item.Value.Met ? 1800000 : 120000);
			if (num || Game.GameTime - item.Value.SeenAt > num2)
			{
				_kids.Remove(item.Key);
			}
		}
	}
}
