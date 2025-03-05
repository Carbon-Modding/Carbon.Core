using Mono.Cecil;

public sealed class Publicizer
{
	private readonly DefaultAssemblyResolver _resolver;
	private readonly string _file, _location, _normalized;
	private readonly AssemblyDefinition _assembly;

	public Publicizer(string path)
	{
		try
		{
			_normalized = Path.GetFullPath(path);
			_file = Path.GetFileName(_normalized);
			_location = Path.GetDirectoryName(_normalized);

			if (!File.Exists(_normalized))
				throw new Exception($"Assembly file '{_file}' was not found");

			_resolver = new DefaultAssemblyResolver();
			_resolver.AddSearchDirectory(_location);

			_assembly = AssemblyDefinition.ReadAssembly(_normalized,
				parameters: new ReaderParameters { AssemblyResolver = _resolver })
				?? throw new Exception($"Assembly file '{_file}' could not be read from disk");
		}
		catch (System.Exception ex)
		{
			Console.WriteLine(ex.Message);
		}

		Console.WriteLine($"Found {_assembly.MainModule.Types.Count} types on '{_file}'");

		Start();
		SaveToDisk();
	}

	private void Start()
	{
		AssemblyNameReference scope = _assembly.MainModule.AssemblyReferences.OrderByDescending(a => a.Version).FirstOrDefault(a => a.Name == "mscorlib");

		MethodReference ctor = new MethodReference(".ctor", _assembly.MainModule.TypeSystem.Void, declaringType: new TypeReference("System", "NonSerializedAttribute", _assembly.MainModule, scope))
		{
			HasThis = true
		};

		Console.Write("Publicize process start.. ");

		foreach (TypeDefinition type in _assembly.MainModule.Types)
			Publicize(type, ctor);

		Console.WriteLine("Done");
	}

	private static void Publicize(TypeDefinition type, MethodReference ctor)
	{
		foreach (TypeDefinition childType in type.NestedTypes)
			Publicize(childType, ctor);

		try
		{
			if (type.IsNested)
			{
				type.IsNestedPublic = true;
			}
			else
			{
				type.IsPublic = true;
			}

			foreach (MethodDefinition Method in type.Methods)
				Method.IsPublic = true;

			foreach (FieldDefinition Field in type.Fields)
			{
				// Prevent publicize auto-generated fields
				if (type.Events.Any(x => x.Name == Field.Name)) continue;

				if (ctor != null && !Field.IsPublic && !Field.CustomAttributes.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeField"))
				{
					Field.IsNotSerialized = true;
					Field.CustomAttributes.Add(item: new CustomAttribute(ctor));
				}

				Field.IsPublic = true;
			}
		}
		catch (System.Exception ex)
		{
			Console.WriteLine("Failed:");
			Console.WriteLine(ex.Message);
		}
	}

	private void SaveToDisk()
	{
		try
		{
			Console.Write("Modified assembly in-memory validation.. ");

			using MemoryStream memoryStream = new MemoryStream();
			_assembly.Write(memoryStream);
			memoryStream.Position = 0;
			_assembly.Dispose();

			Console.WriteLine("Done");
			Console.Write("Writting publicized assembly to disk.. ");

			using FileStream outputStream = File.Open(_normalized, FileMode.Create);
			memoryStream.CopyTo(outputStream);
			Console.WriteLine("Done");
		}
		catch (System.Exception ex)
		{
			Console.WriteLine("Failed:");
			Console.WriteLine(ex.Message);
		}
	}
}
