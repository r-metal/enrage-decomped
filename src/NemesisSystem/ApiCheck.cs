using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NemesisSystem;

internal static class ApiCheck
{
	internal static List<string> Missing(Assembly mod)
	{
		int checkedApi;
		return Missing(mod, out checkedApi);
	}

	internal static List<string> Missing(Assembly mod, out int checkedApi)
	{
		return Missing(mod, out checkedApi, null);
	}

	internal static List<string> Missing(Assembly mod, out int checkedApi, ICollection<string> found, ICollection<string> sigs = null)
	{
		checkedApi = 0;
		SortedSet<string> sortedSet = new SortedSet<string>();
		HashSet<int> hashSet = new HashSet<int>();
		Module manifestModule = mod.ManifestModule;
		Type[] array;
		try
		{
			array = mod.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			array = ex.Types.Where((Type t) => t != null).ToArray();
		}
		Type[] array2 = array;
		foreach (Type type in array2)
		{
			foreach (MethodBase item in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
			{
				byte[] array3;
				try
				{
					array3 = item.GetMethodBody()?.GetILAsByteArray();
				}
				catch
				{
					continue;
				}
				if (array3 == null)
				{
					continue;
				}
				for (int num2 = 0; num2 + 4 < array3.Length; num2++)
				{
					byte b = array3[num2];
					if (b != 40 && b != 111 && b != 115 && (b != 254 || array3[num2 + 1] != 6))
					{
						continue;
					}
					int num3 = ((b == 254) ? (num2 + 2) : (num2 + 1));
					if (num3 + 4 > array3.Length)
					{
						continue;
					}
					int num4 = BitConverter.ToInt32(array3, num3);
					if (num4 >> 24 != 10 || !hashSet.Add(num4))
					{
						continue;
					}
					try
					{
						MemberInfo memberInfo = manifestModule.ResolveMember(num4);
						Type declaringType = memberInfo.DeclaringType;
						if ((object)declaringType != null && declaringType.Namespace?.StartsWith("RDR2") == true)
						{
							checkedApi++;
							found?.Add(memberInfo.DeclaringType.Name + "." + memberInfo.Name);
							sigs?.Add(memberInfo.DeclaringType.FullName + "." + memberInfo);
						}
					}
					catch (MissingMemberException ex2)
					{
						if (ex2.Message.Contains("RDR2."))
						{
							sortedSet.Add(Strip(ex2.Message));
						}
					}
					catch
					{
					}
				}
			}
		}
		return sortedSet.ToList();
	}

	private static string Strip(string message)
	{
		int num = message.IndexOf('\'');
		int num2 = message.LastIndexOf('\'');
		if (num < 0 || num2 <= num)
		{
			return message;
		}
		return message.Substring(num + 1, num2 - num - 1);
	}
}
