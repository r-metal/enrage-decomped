using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace NemesisSystem.Tests;

public class RecruitsTests
{
	private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

	// the recruit API the F11 "Your Men" page is built on: internal, these parameters, this return type
	[Theory]
	[InlineData("OfferRecruit", typeof(void), typeof(NemesisRecord), typeof(string))]
	[InlineData("SendForRecruit", typeof(void), typeof(NemesisRecord))]
	[InlineData("SendRecruitHome", typeof(void), typeof(NemesisRecord))]
	[InlineData("LetRecruitGo", typeof(void), typeof(NemesisRecord))]
	[InlineData("RecruitStatus", typeof(string), typeof(NemesisRecord))]
	[InlineData("RecruitsKept", typeof(IReadOnlyList<NemesisRecord>))]
	public void MenuApi(string name, Type returns, params Type[] args)
	{
		MethodInfo m = typeof(NemesisMod).GetMethod(name, Inst, null, args, null);
		Assert.NotNull(m);
		Assert.True(m.IsAssembly, name + " is not internal");
		Assert.Equal(returns, m.ReturnType);
	}

	[Fact]
	public void TickedEachFrame()
	{
		MethodInfo m = typeof(NemesisMod).GetMethod("UpdateRecruits", Inst);
		Assert.NotNull(m);
		Assert.True(m.IsPrivate);
	}

	[Fact]
	public void RecruitsRideWithoutACamp()
	{
		Type c = typeof(NemesisMod).GetNestedType("Companion", BindingFlags.NonPublic);
		Assert.NotNull(c);
		Assert.Equal(typeof(bool), c.GetField("NoCamp")?.FieldType);
	}
}
