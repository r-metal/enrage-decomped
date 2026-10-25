using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NemesisSystem.Tests;

public class WorldStateRecruitsTests
{
	// what 0.9.5.3's WorldState reads a newer world.json into: it has no Recruits
	public class OldWorld
	{
		public int Era { get; set; }

		public bool HistorySeeded { get; set; }

		public Dictionary<string, long> CompanionHurt { get; set; }
	}

	// world.json is read by SafeFile.Read (JsonConvert defaults) and written by SaveWorldNow,
	// either whole (JsonConvert.SerializeObject, defaults) or sliced (SlicedJson)
	private static string Save(WorldState world, bool sliced)
	{
		if (sliced)
		{
			return new SlicedJson(world).Finish();
		}
		return JsonConvert.SerializeObject(world);
	}

	private static T Load<T>(string json) where T : class
	{
		string dir = Path.Combine(Path.GetTempPath(), "nemesis-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			string path = Path.Combine(dir, "world.json");
			File.WriteAllText(path, json);
			T val = SafeFile.Read<T>(path, null, out bool found, out string from);
			Assert.True(found);
			Assert.Equal(path, from);
			return val;
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}

	[Fact]
	public void LegacyWorldLoadsWithNoRecruits()
	{
		Assert.Null(new WorldState().Recruits);
		WorldState world = Load<WorldState>("{\"Era\":0,\"HistorySeeded\":true,\"CompanionHurt\":{\"pearson\":1200}}");
		Assert.True(world.HistorySeeded);
		Assert.Equal(1200L, world.CompanionHurt["pearson"]);
		Assert.Null(world.Recruits);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RecruitsRoundTrip(bool sliced)
	{
		WorldState world = new WorldState
		{
			Recruits = new List<NemesisRecord>
			{
				new NemesisRecord
				{
					Id = "rec_1",
					Name = "Ezra Pike",
					Gang = "odriscoll",
					ModelHash = 0xC9F7C2A3u,
					OutfitHash = 7u,
					Rank = 3,
					Personality = Personality.Coward
				},
				new NemesisRecord
				{
					Id = "rec_2",
					Name = "Abner Cole",
					Gang = "lemoyneraiders",
					ModelHash = 0x1A2B3C4Du,
					OutfitHash = 0u,
					Rank = 1,
					Personality = Personality.Drifter
				}
			}
		};
		List<NemesisRecord> back = Load<WorldState>(Save(world, sliced)).Recruits;
		Assert.Equal(2, back.Count);
		for (int i = 0; i < 2; i++)
		{
			NemesisRecord a = world.Recruits[i];
			NemesisRecord b = back[i];
			Assert.Equal(a.Id, b.Id);
			Assert.Equal(a.Name, b.Name);
			Assert.Equal(a.Gang, b.Gang);
			Assert.Equal(a.ModelHash, b.ModelHash);
			Assert.Equal(a.OutfitHash, b.OutfitHash);
			Assert.Equal(a.Rank, b.Rank);
			Assert.Equal(a.Personality, b.Personality);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void FreshWorldWritesNoRecruitList(bool sliced)
	{
		// null until first use, like CompanionHurt: no "Recruits": [] in every save
		Assert.Equal(JTokenType.Null, JObject.Parse(Save(new WorldState(), sliced))["Recruits"].Type);
	}

	[Fact]
	public void OldReaderIgnoresRecruitsKey()
	{
		Assert.NotEqual(MissingMemberHandling.Error, JsonSerializer.CreateDefault().MissingMemberHandling);
		JsonSerializer ser = (JsonSerializer)typeof(SlicedJson).GetField("_ser", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(new SlicedJson(new WorldState()));
		Assert.NotEqual(MissingMemberHandling.Error, ser.MissingMemberHandling);
		WorldState world = new WorldState
		{
			Era = 1,
			HistorySeeded = true,
			Recruits = new List<NemesisRecord>
			{
				new NemesisRecord
				{
					Id = "rec_1",
					Name = "Ezra Pike",
					Gang = "odriscoll"
				}
			}
		};
		string json = Save(world, sliced: false);
		Assert.Contains("\"Recruits\":[", json);
		OldWorld old = Load<OldWorld>(json);
		Assert.Equal(1, old.Era);
		Assert.True(old.HistorySeeded);
	}
}
