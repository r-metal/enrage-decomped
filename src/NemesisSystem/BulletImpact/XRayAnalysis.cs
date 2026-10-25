using System;
using System.Collections.Generic;
using System.Linq;

namespace NemesisSystem.BulletImpact;

public sealed class XRayAnalysis
{
	public Anatomy.Body Body;

	public PathResult Main;

	public readonly List<PathResult> Pellets = new List<PathResult>();

	public readonly List<(string region, int blows)> Blows = new List<(string, int)>();

	public IEnumerable<DamageItem> AllDamage => from d in (from p in new PathResult[1] { Main }.Concat(Pellets)
			where p != null
			select p).SelectMany((PathResult p) => p.Damage)
		group d by d.Key into g
		select g.First() into d
		orderby d.Severity descending
		select d;

	public bool HitKey(string key)
	{
		if (Main == null || !Main.HitKeys.Contains(key))
		{
			return Pellets.Any((PathResult p) => p.HitKeys.Contains(key));
		}
		return true;
	}

	private static float RoundScale(uint weaponHash)
	{
		if (weaponHash != 3724000286u)
		{
			return 1f;
		}
		return 0.33f;
	}

	public static XRayAnalysis Build(XRayEntry e)
	{
		Dictionary<int, V3> bones = (from b in e.Bones
			group b by b.Id).ToDictionary((IGrouping<int, BonePoint> g) => g.Key, (IGrouping<int, BonePoint> g) => g.First().P);
		XRayAnalysis xRayAnalysis = new XRayAnalysis
		{
			Body = (e.Animal ? AnimalAnatomy.Build(bones) : Anatomy.Build(bones))
		};
		Random random = new Random(e.Id * 7919);
		if (e.WeaponClass == WeaponClass.Blunt)
		{
			BuildBeating(e, xRayAnalysis);
			return xRayAnalysis;
		}
		float num = ((e.WeaponClass == WeaponClass.Shotgun && e.Ammo != AmmoKind.Slug) ? Math.Max(0f, Math.Min(1f, (12f - e.Distance) / 10f)) : 0f);
		float num2 = 1f + 1.5f * num;
		AmmoKind ammo = Ammo.Of(e);
		if (e.ImpactOnVictim)
		{
			xRayAnalysis.Main = Anatomy.Trace(xRayAnalysis.Body, e.Entry - e.Dir * 0.6f, e.Dir, e.WeaponClass, 3f, ammo, RoundScale(e.WeaponHash) * num2);
		}
		if (xRayAnalysis.Main == null)
		{
			V3 value;
			V3 v = (xRayAnalysis.Body.Bones.TryGetValue(e.RecordedBoneId, out value) ? value : xRayAnalysis.Body.TorsoPoint(0.6f, 0f, 0f));
			V3 v2 = (v - e.Muzzle).Normalized();
			xRayAnalysis.Main = Anatomy.Trace(xRayAnalysis.Body, v - v2 * 1f, v2, e.WeaponClass, 3f, ammo, RoundScale(e.WeaponHash) * num2);
			if (e.Animal && e.StillUp && xRayAnalysis.Main != null && AnimalAnatomy.Drop(xRayAnalysis.Main.Damage) != null)
			{
				xRayAnalysis.Main = OffTheVitals(e, xRayAnalysis.Body, v, v2, ammo, RoundScale(e.WeaponHash) * num2) ?? xRayAnalysis.Main;
			}
			if (xRayAnalysis.Main != null)
			{
				xRayAnalysis.Main.Estimated = !e.ImpactOnVictim;
				xRayAnalysis.Main.Guessed = true;
			}
		}
		if (Anatomy.IsArea(e.WeaponClass) && xRayAnalysis.Main != null)
		{
			bool flag = e.WeaponClass == WeaponClass.Burn;
			V3 entry = xRayAnalysis.Main.Entry;
			V3 v3 = (entry - e.Muzzle).Normalized();
			V3 v4 = V3.Cross(v3, new V3(0f, 0f, 1f)).Normalized();
			if (v4.Length() < 0.5f)
			{
				v4 = new V3(1f, 0f, 0f);
			}
			V3 v5 = V3.Cross(v4, v3).Normalized();
			float num3 = (flag ? 0.45f : 0.35f);
			for (int num4 = 1; num4 < Math.Max(2, e.Pellets); num4++)
			{
				for (int num5 = 0; num5 < 15; num5++)
				{
					double num6 = random.NextDouble() * Math.PI * 2.0;
					double num7 = Math.Sqrt(random.NextDouble()) * (double)num3;
					V3 v6 = entry + v4 * (float)(Math.Cos(num6) * num7) + v5 * (float)(Math.Sin(num6) * num7 * 1.600000023841858);
					V3 v7 = (v6 - e.Muzzle).Normalized();
					PathResult pathResult = Anatomy.Trace(xRayAnalysis.Body, v6 - v7 * 1f, v7, e.WeaponClass, 3f);
					if (pathResult != null)
					{
						pathResult.Estimated = true;
						xRayAnalysis.Pellets.Add(pathResult);
						break;
					}
				}
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (PathResult item in new PathResult[1] { xRayAnalysis.Main }.Concat(xRayAnalysis.Pellets))
			{
				string key = Anatomy.RegionOf(xRayAnalysis.Body, item.Entry);
				dictionary[key] = ((!dictionary.TryGetValue(key, out var value2)) ? 1 : (value2 + 1));
			}
			foreach (KeyValuePair<string, int> item2 in dictionary.OrderByDescending((KeyValuePair<string, int> x) => x.Value))
			{
				Severity severity = ((item2.Value >= 5) ? Severity.Severe : ((item2.Value >= 2) ? Severity.Moderate : Severity.Minor));
				xRayAnalysis.Main.Damage.Add(flag ? new DamageItem
				{
					Key = "burn:" + item2.Key,
					Name = item2.Key.ToUpperInvariant() + " BURNED" + ((item2.Value >= 5) ? " - DEEP" : ""),
					Severity = severity
				} : new DamageItem
				{
					Key = "shrapnel:" + item2.Key,
					Name = $"SHRAPNEL IN THE {item2.Key.ToUpperInvariant()} ({item2.Value})",
					Severity = severity
				});
			}
			if (!flag && e.Distance < 2.5f)
			{
				xRayAnalysis.Main.Damage.Add(new DamageItem
				{
					Key = "concussion",
					Name = "CONCUSSION - THE BLAST WAVE",
					Severity = Severity.Moderate
				});
			}
			xRayAnalysis.Main.Damage.Sort((DamageItem x, DamageItem y) => y.Severity.CompareTo(x.Severity));
		}
		if (e.WeaponClass == WeaponClass.Shotgun && xRayAnalysis.Main != null && e.Pellets > 1 && e.Ammo != AmmoKind.Slug)
		{
			V3 entry2 = xRayAnalysis.Main.Entry;
			V3 v8 = (entry2 - e.Muzzle).Normalized();
			V3 v9 = V3.Cross(v8, new V3(0f, 0f, 1f)).Normalized();
			if (v9.Length() < 0.5f)
			{
				v9 = new V3(1f, 0f, 0f);
			}
			V3 v10 = V3.Cross(v9, v8).Normalized();
			float num8 = Math.Max(0.06f, Math.Min(0.35f, e.Distance * 0.03f));
			for (int num9 = 1; num9 < e.Pellets; num9++)
			{
				for (int num10 = 0; num10 < 15; num10++)
				{
					double num11 = random.NextDouble() * Math.PI * 2.0;
					double num12 = Math.Sqrt(random.NextDouble()) * (double)num8;
					V3 v11 = entry2 + v9 * (float)(Math.Cos(num11) * num12) + v10 * (float)(Math.Sin(num11) * num12);
					V3 v12 = (v11 - e.Muzzle).Normalized();
					PathResult pathResult2 = Anatomy.Trace(xRayAnalysis.Body, v11 - v12 * 1f, v12, WeaponClass.Shotgun, 3f, e.Ammo, num2);
					if (pathResult2 != null)
					{
						pathResult2.Estimated = true;
						xRayAnalysis.Pellets.Add(pathResult2);
						break;
					}
				}
			}
			if (num > 0.3f && !e.Animal)
			{
				Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
				foreach (PathResult item3 in new PathResult[1] { xRayAnalysis.Main }.Concat(xRayAnalysis.Pellets))
				{
					string key2 = Bare(Anatomy.RegionOf(xRayAnalysis.Body, item3.Entry));
					dictionary2[key2] = ((!dictionary2.TryGetValue(key2, out var value3)) ? 1 : (value3 + 1));
				}
				foreach (KeyValuePair<string, int> item4 in from x in dictionary2
					where x.Value >= 4
					orderby x.Value descending
					select x)
				{
					bool flag2 = item4.Key.Contains("head") || item4.Key.Contains("face") || item4.Key.Contains("neck");
					bool flag3 = item4.Key.Contains("arm") || item4.Key.Contains("leg") || item4.Key.Contains("thigh") || item4.Key.Contains("hand") || item4.Key.Contains("foot") || item4.Key.Contains("shoulder");
					xRayAnalysis.Main.Damage.Add(new DamageItem
					{
						Key = "buckshot:" + item4.Key,
						Name = (flag2 ? (item4.Key.ToUpperInvariant() + " BLOWN OPEN - BUCKSHOT") : (flag3 ? (item4.Key.ToUpperInvariant() + " TORN APART - BUCKSHOT") : (item4.Key.ToUpperInvariant() + " TORN OPEN - BUCKSHOT"))),
						Severity = (flag2 ? Severity.Fatal : Severity.Critical)
					});
				}
				xRayAnalysis.Main.Damage.Sort((DamageItem x, DamageItem y) => y.Severity.CompareTo(x.Severity));
			}
		}
		if (xRayAnalysis.Main != null)
		{
			ApplyAmmo(e, xRayAnalysis, random);
		}
		if (e.Animal && e.StillUp && xRayAnalysis.Main != null && xRayAnalysis.Main.Guessed)
		{
			xRayAnalysis.Pellets.RemoveAll((PathResult p) => p != null && (AnimalAnatomy.Drop(p.Damage) != null || p.Damage.Any((DamageItem d) => !d.Chipped && (d.Key == "lung_l" || d.Key == "lung_r"))));
		}
		if (e.Animal && xRayAnalysis.Main != null)
		{
			AnimalAnatomy.AddVerdicts(xRayAnalysis.Main, xRayAnalysis.Pellets, e.FatalShot && !e.StillUp && e.HitShare > 0f, e.HitShare);
		}
		return xRayAnalysis;
	}

	private static PathResult OffTheVitals(XRayEntry e, Anatomy.Body body, V3 target, V3 dir, AmmoKind ammo, float scale)
	{
		V3 v = V3.Cross(dir, new V3(0f, 0f, 1f)).Normalized();
		if (v.Length() < 0.5f)
		{
			v = new V3(1f, 0f, 0f);
		}
		V3 v2 = V3.Cross(v, dir).Normalized();
		List<V3> list = new List<V3>();
		if (e.ImpactOnVictim)
		{
			V3 a = e.Entry - target;
			a -= dir * V3.Dot(a, dir);
			if (a.Length() > 0.01f)
			{
				list.Add(a.Normalized());
			}
		}
		for (int i = 0; i < 8; i++)
		{
			list.Add((v * (float)Math.Cos((double)i * Math.PI / 4.0) + v2 * (float)Math.Sin((double)i * Math.PI / 4.0)).Normalized());
		}
		for (int j = 1; j <= 30; j++)
		{
			foreach (V3 item in list)
			{
				V3 v3 = target + item * ((float)j * 0.01f);
				PathResult pathResult = Anatomy.Trace(body, v3 - dir * 1f, dir, e.WeaponClass, 3f, ammo, scale);
				if (pathResult != null && AnimalAnatomy.Drop(pathResult.Damage) == null)
				{
					return pathResult;
				}
			}
		}
		return null;
	}

	private static void BuildBeating(XRayEntry e, XRayAnalysis a)
	{
		List<Blow> list = ((e.Blows != null && e.Blows.Count > 0) ? e.Blows : new List<Blow>
		{
			new Blow
			{
				Bone = e.RecordedBoneId,
				Dir = e.Dir
			}
		});
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		Dictionary<string, int> weight = new Dictionary<string, int>();
		int num = 0;
		int num2 = 0;
		PathResult pathResult = null;
		foreach (Blow item in list)
		{
			V3 v = ((item.Dir.Length() > 0.1f) ? item.Dir.Normalized() : new V3(0f, -1f, 0f));
			string text = BlowRegion(item.Bone, v);
			V3 value;
			V3 obj = (a.Body.Bones.TryGetValue(item.Bone, out value) ? value : a.Body.TorsoPoint(0.6f, 0f, 0f));
			float num3;
			switch (text)
			{
			default:
				num3 = 0.05f;
				break;
			case "chest":
			case "stomach":
			case "back":
				num3 = 0.13f;
				break;
			case "throat":
				num3 = 0.06f;
				break;
			case "face":
				num3 = 0.1f;
				break;
			}
			float num4 = num3;
			V3 v2 = obj - v * num4;
			if (text == "chest")
			{
				if (v2.X < 0f)
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
			dictionary[text] = ((!dictionary.TryGetValue(text, out var value2)) ? 1 : (value2 + 1));
			weight[text] = (weight.TryGetValue(text, out var value3) ? value3 : 0) + item.Weight;
			PathResult obj2 = new PathResult
			{
				Blunt = true,
				Blow = item.Kind,
				HasEntry = true,
				Start = v2 - v * 0.3f,
				Entry = v2,
				End = v2
			};
			if (pathResult != null)
			{
				a.Pellets.Add(pathResult);
			}
			pathResult = obj2;
		}
		a.Main = pathResult;
		foreach (KeyValuePair<string, int> item2 in dictionary.OrderByDescending((KeyValuePair<string, int> x) => x.Value))
		{
			a.Blows.Add((item2.Key, item2.Value));
		}
		int count = list.Count;
		int n = list.Count((Blow x) => x.Kind == BlowKind.Punch);
		int num5 = list.Count((Blow x) => x.Kind == BlowKind.Kick);
		int n2 = list.Count((Blow x) => x.Kind == BlowKind.Gun);
		int num6 = list.Count((Blow x) => x.Kind == BlowKind.Choke);
		int num7 = list.Where((Blow x) => x.Choke).Sum((Blow x) => x.HeldMs);
		int num8 = list.Count((Blow x) => x.Stomp);
		string text2 = ((num5 == 0) ? null : ((num8 == num5) ? Of(num8, "stomp", "stomps") : ((num8 > 0) ? (Of(num5, "kick", "kicks") + " (" + Of(num8, "stomp", "stomps") + ")") : Of(num5, "kick", "kicks"))));
		List<string> list2 = new string[4]
		{
			Of(n, "punch", "punches"),
			text2,
			Of(n2, "gun blow", "gun blows"),
			Of(num6, "choke", "chokes")
		}.Where((string x) => x != null).ToList();
		a.Main.EntryText = ((count == 1) ? (list2[0] + " - " + a.Blows[0].region) : string.Join(", ", list2));
		List<DamageItem> d = a.Main.Damage;
		int num9 = C("face");
		bool flag = e.KnockedOut && !e.Killed;
		if (e.LeftForDead)
		{
			Add("left_for_dead", "KICKED WHILE DOWN - LEFT FOR DEAD", Severity.Fatal);
		}
		if (num6 > 0)
		{
			if (e.FatalShot)
			{
				Add("strangled", "STRANGLED", Severity.Fatal);
			}
			else if (flag)
			{
				Add("choked_out", "CHOKED UNCONSCIOUS", Severity.Severe);
			}
			if (num7 >= 5000)
			{
				Add("windpipe", "WINDPIPE BRUISED - HOARSE", Severity.Moderate);
			}
			Add("bruise:choke", "BRUISED THROAT - FINGER MARKS", Severity.Minor);
		}
		else if (flag)
		{
			Add("ko", "KNOCKED OUT COLD", Severity.Severe);
		}
		if (num9 >= 8 || (flag && num9 >= 2))
		{
			Add("concussion", "CONCUSSION", Severity.Severe);
		}
		if (list.Count((Blow b) => b.Stomp && BlowRegion(b.Bone, b.Dir) == "face") >= 2)
		{
			Add("skull", "SKULL FRACTURED", Severity.Critical, bone: true);
		}
		if (num9 >= 6)
		{
			Add("jaw", "JAW CRACKED", Severity.Severe, bone: true);
		}
		if (num9 >= 4)
		{
			Add("nose", "BROKEN NOSE", Severity.Moderate, bone: true);
		}
		if (num9 >= 3)
		{
			Add("eye", "BLACK EYE", Severity.Moderate);
		}
		if (num9 >= 1)
		{
			Add("bruise:face", "BRUISED FACE", Severity.Minor);
		}
		if (C("throat") >= 3)
		{
			Add("throat", "THROAT SWOLLEN - HOARSE", Severity.Moderate);
		}
		else if (C("throat") >= 1)
		{
			Add("bruise:throat", "BRUISED THROAT", Severity.Minor);
		}
		if (C("chest") >= 4)
		{
			Add((num > num2) ? "ribs_l" : "ribs_r", (num > num2) ? "LEFT RIBS CRACKED" : "RIGHT RIBS CRACKED", Severity.Moderate, bone: true);
		}
		if (C("chest") >= 1)
		{
			Add("bruise:chest", "BRUISED CHEST", Severity.Minor);
		}
		if (C("stomach") >= 4)
		{
			Add("gut", "WINDED - BADLY BRUISED GUT", Severity.Moderate);
		}
		else if (C("stomach") >= 1)
		{
			Add("bruise:stomach", "BRUISED STOMACH", Severity.Minor);
		}
		if (C("back") >= 3)
		{
			Add("bruise:kidneys", "BRUISED KIDNEYS", Severity.Moderate);
		}
		else if (C("back") >= 1)
		{
			Add("bruise:back", "BRUISED BACK", Severity.Minor);
		}
		string[] array = new string[4] { "left arm", "right arm", "left leg", "right leg" };
		foreach (string text3 in array)
		{
			if (C(text3) >= 1)
			{
				Add("bruise:" + text3, "BRUISED " + text3.ToUpperInvariant(), Severity.Minor);
			}
		}
		if (C("body") >= 1 && d.Count == 0)
		{
			Add("bruise:body", "BRUISED", Severity.Minor);
		}
		d.Sort((DamageItem x, DamageItem y) => y.Severity.CompareTo(x.Severity));
		void Add(string key, string name, Severity s, bool bone = false)
		{
			d.Add(new DamageItem
			{
				Key = key,
				Name = name,
				Severity = s,
				IsBone = bone
			});
		}
		int C(string reg)
		{
			if (!weight.TryGetValue(reg, out var value4))
			{
				return 0;
			}
			return value4;
		}
		static string Of(int num11, string one, string many)
		{
			if (num11 != 0)
			{
				return $"{num11} {((num11 == 1) ? one : many)}";
			}
			return null;
		}
	}

	private static string BlowRegion(int bone, V3 dir)
	{
		string text = Anatomy.RegionOfBone(bone);
		bool flag = dir.Y > 0.3f;
		switch (text)
		{
		case "the head":
			return "face";
		case "the neck":
			return "throat";
		case "the chest":
			if (!flag)
			{
				return "chest";
			}
			return "back";
		case "the hips":
		case "the abdomen":
			if (!flag)
			{
				return "stomach";
			}
			return "back";
		case "the left arm":
		case "the left hand":
		case "the left shoulder":
			return "left arm";
		case "the right arm":
		case "the right hand":
		case "the right shoulder":
			return "right arm";
		case "the left leg":
		case "the left foot":
		case "the left thigh":
			return "left leg";
		case "the right leg":
		case "the right foot":
		case "the right thigh":
			return "right leg";
		default:
			return "body";
		}
	}

	private static void ApplyAmmo(XRayEntry e, XRayAnalysis a, Random rng)
	{
		PathResult main = a.Main;
		switch (Ammo.Of(e))
		{
		case AmmoKind.SplitPoint:
		{
			float num6 = Math.Max(0.02f, Math.Min(0.05f, (main.End - main.Entry).Length() * 0.5f));
			V3 v6 = main.Entry + e.Dir.Normalized() * num6;
			int num7 = 3 + rng.Next(2);
			int num8 = 0;
			for (int num9 = 0; num9 < num7; num9++)
			{
				V3 v7 = Jitter(e.Dir.Normalized(), 0.7f, rng);
				PathResult pathResult3 = Anatomy.Trace(a.Body, v6 - v7 * 0.01f, v7, WeaponClass.Shotgun, 1.5f, AmmoKind.Regular, 2f);
				if (pathResult3 != null)
				{
					pathResult3.Estimated = true;
					pathResult3.Entry = v6;
					pathResult3.Start = v6;
					a.Pellets.Add(pathResult3);
					num8++;
				}
			}
			main.Damage.Add(new DamageItem
			{
				Key = "ammo:split",
				Name = ((num8 > 0) ? $"BULLET BROKE APART - {num8} FRAGMENTS" : "BULLET FLATTENED"),
				Severity = Severity.Moderate
			});
			main.Damage.Add(new DamageItem
			{
				Key = "ammo:channel",
				Name = "WIDE WOUND CHANNEL",
				Severity = Severity.Moderate
			});
			break;
		}
		case AmmoKind.Explosive:
		{
			V3 entry = main.Entry;
			int num3 = 5 + rng.Next(3);
			int num4 = 0;
			for (int num5 = 0; num5 < num3; num5++)
			{
				V3 v5 = Jitter(e.Dir.Normalized(), 0.9f, rng);
				PathResult pathResult2 = Anatomy.Trace(a.Body, entry - v5 * 0.01f, v5, WeaponClass.Blast, 1.5f);
				if (pathResult2 != null)
				{
					pathResult2.Estimated = true;
					pathResult2.Entry = entry;
					pathResult2.Start = entry;
					a.Pellets.Add(pathResult2);
					num4++;
				}
			}
			string text = Bare(Anatomy.RegionOf(a.Body, entry));
			main.Damage.Add(new DamageItem
			{
				Key = "ammo:explode",
				Name = $"ROUND EXPLODED IN THE WOUND - {num4} FRAGMENTS",
				Severity = Severity.Severe
			});
			main.Damage.Add(new DamageItem
			{
				Key = "burn:" + text,
				Name = text.ToUpperInvariant() + " BURNED",
				Severity = Severity.Moderate
			});
			break;
		}
		case AmmoKind.Incendiary:
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (PathResult item in new PathResult[1] { main }.Concat(a.Pellets))
			{
				string key = Bare(Anatomy.RegionOf(a.Body, item.Entry));
				dictionary[key] = ((!dictionary.TryGetValue(key, out var value)) ? 1 : (value + 1));
			}
			foreach (KeyValuePair<string, int> item2 in dictionary.OrderByDescending((KeyValuePair<string, int> keyValuePair) => keyValuePair.Value))
			{
				main.Damage.Add(new DamageItem
				{
					Key = "burn:" + item2.Key,
					Name = item2.Key.ToUpperInvariant() + " BURNED",
					Severity = ((item2.Value < 3) ? Severity.Moderate : Severity.Severe)
				});
			}
			break;
		}
		case AmmoKind.Slug:
			main.Damage.Add(new DamageItem
			{
				Key = "ammo:slug",
				Name = "HIT BY A SLUG",
				Severity = Severity.Moderate
			});
			break;
		case AmmoKind.FireArrow:
		{
			string text2 = Bare(Anatomy.RegionOf(a.Body, main.Entry));
			main.Damage.Add(new DamageItem
			{
				Key = "burn:" + text2,
				Name = text2.ToUpperInvariant() + " BURNED - FIRE ARROW",
				Severity = Severity.Moderate
			});
			break;
		}
		case AmmoKind.PoisonArrow:
			main.Damage.Add(new DamageItem
			{
				Key = "ammo:poison",
				Name = "POISON IN THE BLOOD",
				Severity = Severity.Severe
			});
			break;
		case AmmoKind.MachineGun:
		{
			bool flag = e.Animal && e.StillUp && main.Guessed;
			V3 v = (main.Entry - main.Start).Normalized();
			V3 v2 = V3.Cross(v, new V3(0f, 0f, 1f)).Normalized();
			if (v2.Length() < 0.5f)
			{
				v2 = new V3(1f, 0f, 0f);
			}
			V3 v3 = V3.Cross(v2, v).Normalized();
			V3[] array = new V3[4]
			{
				v2 * 0.035f,
				v2 * -0.035f,
				v3 * 0.035f,
				v3 * -0.035f
			};
			foreach (V3 v4 in array)
			{
				PathResult pathResult = Anatomy.Trace(a.Body, main.Start + v4, v, e.WeaponClass, 3f, AmmoKind.MachineGun);
				if (pathResult == null)
				{
					continue;
				}
				foreach (DamageItem x in pathResult.Damage)
				{
					if (!(x.Key == "flesh") && (!flag || AnimalAnatomy.Drop(main.Damage.Concat(new DamageItem[1] { x })) == null))
					{
						int num = main.Damage.FindIndex((DamageItem y) => y.Key == x.Key);
						if (num < 0)
						{
							main.Damage.Add(x);
							main.HitKeys.Add(x.Key);
						}
						else if (main.Damage[num].Chipped && !x.Chipped)
						{
							main.Damage[num] = x;
						}
					}
				}
			}
			if (main.Damage.Any((DamageItem y) => y.Key != "flesh"))
			{
				main.Damage.RemoveAll((DamageItem y) => y.Key == "flesh");
			}
			int num2 = main.Damage.FindIndex((DamageItem y) => y.Key == "flesh");
			if (num2 >= 0)
			{
				main.Damage[num2] = new DamageItem
				{
					Key = "flesh",
					Name = "FLESH TORN OPEN - THE ROUND TUMBLED",
					Severity = Severity.Moderate
				};
			}
			else
			{
				main.Damage.Add(new DamageItem
				{
					Key = "ammo:tumble",
					Name = "ROUND TUMBLED - WIDE WOUND CHANNEL",
					Severity = Severity.Moderate
				});
			}
			break;
		}
		}
		main.Damage.Sort((DamageItem damageItem, DamageItem y) => y.Severity.CompareTo(damageItem.Severity));
	}

	private static string Bare(string region)
	{
		string text;
		if (region == null || !region.StartsWith("the "))
		{
			text = region;
			if (text == null)
			{
				return "body";
			}
		}
		else
		{
			text = region.Substring(4);
		}
		return text;
	}

	private static V3 Jitter(V3 d, float maxRad, Random rng)
	{
		V3 v = V3.Cross(d, new V3(0f, 0f, 1f)).Normalized();
		if (v.Length() < 0.5f)
		{
			v = new V3(1f, 0f, 0f);
		}
		V3 v2 = V3.Cross(v, d).Normalized();
		double num = rng.NextDouble() * Math.PI * 2.0;
		double num2 = Math.Tan(rng.NextDouble() * (double)maxRad);
		return (d + v * (float)(Math.Cos(num) * num2) + v2 * (float)(Math.Sin(num) * num2)).Normalized();
	}
}
