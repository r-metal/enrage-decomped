using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace NemesisSystem.Tests;

// The mod references System.Windows.Forms, which .NET 10 on Linux doesn't have, so a type with
// a Keys field (NemesisSettings, KeyCombo) fails to load. This stands in an assembly holding
// only the Keys enum, its values read from the .NET Framework 4.8 reference assembly.
// Anything else from WinForms still fails to load.
internal static class WinFormsShim
{
	private static Assembly _shim;

	[ModuleInitializer]
	internal static void Install()
	{
		AssemblyLoadContext.Default.Resolving += (AssemblyLoadContext ctx, AssemblyName name) => (name.Name == "System.Windows.Forms") ? Shim(ctx) : null;
	}

	private static Assembly Shim(AssemblyLoadContext ctx)
	{
		lock (typeof(WinFormsShim))
		{
			if (_shim != null)
			{
				return _shim;
			}
			PersistedAssemblyBuilder asm = new PersistedAssemblyBuilder(new AssemblyName("System.Windows.Forms")
			{
				Version = new Version(4, 0, 0, 0)
			}, typeof(object).Assembly);
			EnumBuilder keys = asm.DefineDynamicModule("System.Windows.Forms.dll").DefineEnum("System.Windows.Forms.Keys", TypeAttributes.Public, typeof(int));
			using (PEReader pe = new PEReader(File.OpenRead((string)AppContext.GetData("NemesisMod.Tests.WinFormsRef"))))
			{
				MetadataReader md = pe.GetMetadataReader();
				foreach (TypeDefinitionHandle th in md.TypeDefinitions)
				{
					TypeDefinition type = md.GetTypeDefinition(th);
					if (md.GetString(type.Name) != "Keys" || md.GetString(type.Namespace) != "System.Windows.Forms")
					{
						continue;
					}
					foreach (FieldDefinitionHandle fh in type.GetFields())
					{
						FieldDefinition field = md.GetFieldDefinition(fh);
						if (!field.GetDefaultValue().IsNil)
						{
							keys.DefineLiteral(md.GetString(field.Name), md.GetBlobReader(md.GetConstant(field.GetDefaultValue()).Value).ReadInt32());
						}
					}
				}
			}
			keys.CreateType();
			MemoryStream ms = new MemoryStream();
			asm.Save(ms);
			ms.Position = 0L;
			_shim = ctx.LoadFromStream(ms);
			return _shim;
		}
	}
}
