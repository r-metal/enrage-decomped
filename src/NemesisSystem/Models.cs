using System;
using System.Collections.Generic;
using RDR2;

namespace NemesisSystem;

public static class Models
{
	private static readonly Dictionary<uint, string> _raw = new Dictionary<uint, string>();

	private static readonly Dictionary<uint, string> _lower = new Dictionary<uint, string>();

	public static string Raw(uint model)
	{
		if (_raw.TryGetValue(model, out var value))
		{
			return value;
		}
		object obj;
		if (!Enum.IsDefined(typeof(PedHash), model))
		{
			obj = "";
		}
		else
		{
			PedHash pedHash = (PedHash)model;
			obj = pedHash.ToString();
		}
		value = (string)obj;
		_raw[model] = value;
		return value;
	}

	public static string Name(uint model)
	{
		if (_lower.TryGetValue(model, out var value))
		{
			return value;
		}
		value = Raw(model).ToLowerInvariant();
		_lower[model] = value;
		return value;
	}

	public static string Name(Ped p)
	{
		if (!(p == null))
		{
			return Name((uint)p.Model.Hash);
		}
		return "";
	}

	public static bool Female(uint model)
	{
		if (model != 0)
		{
			return Name(model).Contains("_f_");
		}
		return false;
	}
}
