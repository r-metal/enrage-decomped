using System.Collections.Generic;

namespace NemesisSystem.BulletImpact;

public sealed class XRayComposite
{
	public Anatomy.Body Body;

	public readonly List<(PathResult path, bool thin)> Paths = new List<(PathResult, bool)>();

	public readonly HashSet<string> Keys = new HashSet<string>();

	public readonly List<DamageItem> Damage = new List<DamageItem>();

	public static XRayComposite ForEntry(XRayEntry e)
	{
		if (e.Analysis == null)
		{
			e.Analysis = XRayAnalysis.Build(e);
		}
		XRayComposite xRayComposite = new XRayComposite();
		xRayComposite.Body = e.Analysis.Body;
		xRayComposite.AddAnalysis(e.Analysis, null);
		return xRayComposite;
	}

	public static XRayComposite ForShots(IList<XRayEntry> shots)
	{
		foreach (XRayEntry shot in shots)
		{
			if (shot.Analysis == null)
			{
				shot.Analysis = XRayAnalysis.Build(shot);
			}
		}
		XRayComposite xRayComposite = new XRayComposite
		{
			Body = shots[0].Analysis.Body
		};
		foreach (XRayEntry shot2 in shots)
		{
			xRayComposite.AddAnalysis(shot2.Analysis, (shot2.Analysis.Body == xRayComposite.Body) ? null : shot2.Analysis.Body);
		}
		return xRayComposite;
	}

	private void AddAnalysis(XRayAnalysis a, Anatomy.Body from)
	{
		if (a.Main != null)
		{
			Paths.Add((Remap(a.Main, from), false));
		}
		foreach (PathResult pellet in a.Pellets)
		{
			Paths.Add((Remap(pellet, from), true));
		}
		foreach (DamageItem item in a.AllDamage)
		{
			if (Keys.Add(item.Key))
			{
				Damage.Add(item);
			}
		}
		Damage.Sort((DamageItem x, DamageItem y) => y.Severity.CompareTo(x.Severity));
	}

	private PathResult Remap(PathResult p, Anatomy.Body from)
	{
		if (from == null)
		{
			return p;
		}
		PathResult pathResult = new PathResult
		{
			Start = Map(p.Start, from),
			Entry = Map(p.Entry, from),
			End = Map(p.End, from),
			HasEntry = p.HasEntry,
			Exited = p.Exited,
			Estimated = p.Estimated,
			Blunt = p.Blunt,
			EntryText = p.EntryText,
			EndText = p.EndText
		};
		pathResult.Damage.AddRange(p.Damage);
		foreach (string hitKey in p.HitKeys)
		{
			pathResult.HitKeys.Add(hitKey);
		}
		return pathResult;
	}

	private V3 Map(V3 q, Anatomy.Body src)
	{
		int num = -1;
		float num2 = float.MaxValue;
		foreach (KeyValuePair<int, V3> bone in src.Bones)
		{
			if (Body.Has(bone.Key))
			{
				float num3 = (q - bone.Value).Length();
				if (num3 < num2)
				{
					num2 = num3;
					num = bone.Key;
				}
			}
		}
		if (num >= 0)
		{
			return Body.Get(num) + (q - src.Get(num));
		}
		return q;
	}
}
