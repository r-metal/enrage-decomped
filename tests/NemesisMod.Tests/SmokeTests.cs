using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Xunit;

namespace NemesisSystem.Tests;

public class SmokeTests
{
	// nemeses.json is the roster list, serialized with default settings (RosterIO)
	private static NemesisRecord RoundTrip(NemesisRecord rec)
	{
		List<NemesisRecord> list = JsonConvert.DeserializeObject<List<NemesisRecord>>(JsonConvert.SerializeObject(new List<NemesisRecord> { rec }));
		Assert.Single(list);
		return list[0];
	}

	[Fact]
	public void RecordSurvivesNemesesJson()
	{
		NemesisRecord back = RoundTrip(new NemesisRecord
		{
			Id = "smoke_1",
			Name = "Ezra Pike",
			ModelHash = 0xC9F7C2A3u,
			OutfitHash = 7u,
			Rank = 4
		});
		Assert.Equal("smoke_1", back.Id);
		Assert.Equal("Ezra Pike", back.Name);
		Assert.Equal(0xC9F7C2A3u, back.ModelHash);
		Assert.Equal(7u, back.OutfitHash);
		Assert.Equal(4, back.Rank);
	}

	[Fact]
	public void OldRecordLoadsWithDefaults()
	{
		NemesisRecord rec = JsonConvert.DeserializeObject<List<NemesisRecord>>("[{\"Id\":\"smoke_2\",\"Name\":\"Abner Cole\"}]")[0];
		Assert.Equal("smoke_2", rec.Id);
		Assert.Equal(1, rec.Rank);
		Assert.Equal(0u, rec.ModelHash);
		Assert.True(rec.IsAlive);
	}

	[Fact]
	public void InternalsAreVisible()
	{
		// WorldState is internal: this compiles only through InternalsVisibleTo("NemesisMod.Tests")
		Assert.Empty(new WorldState().Events);
	}

	[Fact]
	public void KeysLoadThroughShim()
	{
		// KeyCombo.Current is a WinForms Keys; without WinFormsShim the type fails to load
		object key = typeof(NemesisRecord).Assembly.GetType("NemesisSystem.KeyCombo", throwOnError: true).GetField("Current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
		Assert.Equal("F11", key.ToString());
		Assert.Equal(122, Convert.ToInt32(key));
	}
}
