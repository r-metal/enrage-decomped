using System;
using System.Collections.Generic;

namespace NemesisSystem;

internal class SavePointMeta
{
	public long GameMin { get; set; }

	public string When { get; set; }

	public DateTime Real { get; set; }

	public string Kind { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public Dictionary<string, int> Notoriety { get; set; } = new Dictionary<string, int>();

	public string SeededGangs { get; set; }

	public bool ArmyTopUp { get; set; }

	public int LastSuccessionDay { get; set; }

	public int NextSuccessionDay { get; set; }

	public int LastDecayDay { get; set; }

	public string ClearedCamps { get; set; }

	public string Slot { get; set; }
}
