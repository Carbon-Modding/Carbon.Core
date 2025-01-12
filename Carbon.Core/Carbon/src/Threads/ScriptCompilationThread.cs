using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Carbon.Base;
using Carbon.Components;
using Carbon.Contracts;
using Carbon.Core;
using Carbon.Extensions;
using Carbon.Pooling;
using Carbon.Profiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;

namespace Carbon.Jobs;

public class ScriptCompilationThread : BaseThreadedJob
{
	public ISource InitialSource => Sources != null && Sources.Count > 0 ? Sources[0] : null;
	public List<ISource> Sources;
	public string[] References;
	public string[] Requires;
	public string InternalCallHookSource;
	public bool IsExtension;
	public List<string> Usings = new();
	public Dictionary<Type, List<uint>> Hooks = new();
	public Dictionary<Type, List<HookMethodAttribute>> HookMethods = new();
	public Dictionary<Type, List<PluginReferenceAttribute>> PluginReferences = new();
	public TimeSpan CompileTime;
	public TimeSpan InternalCallHookGenTime;
	public Assembly Assembly;
	public List<CompilerException> Exceptions = new();
	public List<CompilerException> Warnings = new();

	#region Internals

	internal const string _internalCallHookPattern = @"override object InternalCallHook";
	internal const string _partialPattern = @" partial ";
	internal Stopwatch _stopwatch;
	internal List<ClassDeclarationSyntax> ClassList = new();
	internal static EmitOptions _emitOptions = new(debugInformationFormat: DebugInformationFormat.Embedded);
	internal static ConcurrentDictionary<string, byte[]> _compilationCache = new();
	internal static ConcurrentDictionary<string, byte[]> _extensionCompilationCache = new();
	internal static Dictionary<string, PortableExecutableReference> _referenceCache = new();
	internal static Dictionary<string, PortableExecutableReference> _extensionReferenceCache = new();
	internal static readonly string[] _libraryDirectories =
	[
		Defines.GetLibFolder(),
		Defines.GetManagedFolder(),
		Defines.GetRustManagedFolder(),
		Defines.GetManagedModulesFolder(),
		Defines.GetExtensionsFolder()
	];

	internal static byte[] _getPlugin(string name)
	{
		name = name.Replace(" ", string.Empty);

		foreach (var plugin in _compilationCache)
		{
			if (plugin.Key == name)
			{
				return plugin.Value;
			}
		}

		return null;
	}
	internal static byte[] _getExtensionPlugin(string name)
	{
		foreach (var extension in _extensionCompilationCache)
		{
			if (extension.Key == name)
			{
				return extension.Value;
			}
		}

		return null;
	}
	internal static void _overridePlugin(string name, byte[] pluginAssembly)
	{
		name = name.Replace(" ", "");

		if (pluginAssembly == null) return;

		var plugin = _getPlugin(name);
		if (plugin == null)
		{
			try { _compilationCache.AddOrUpdate(name, pluginAssembly, (a, v) => pluginAssembly); } catch { }
			return;
		}

		Array.Clear(plugin, 0, plugin.Length);
		try { _compilationCache[name] = pluginAssembly; } catch { }
	}
	internal static void _overrideExtensionPlugin(string name, byte[] pluginAssembly)
	{
		if (pluginAssembly == null) return;

		var plugin = _getExtensionPlugin(name);
		if (plugin == null)
		{
			try { _extensionCompilationCache.AddOrUpdate(name, pluginAssembly, (a, v) => pluginAssembly); } catch { }
			return;
		}

		Array.Clear(plugin, 0, plugin.Length);
		try { _extensionCompilationCache[name] = pluginAssembly; } catch { }
	}
	internal static void _clearExtensionPlugin(string name)
	{
		if (_extensionCompilationCache.ContainsKey(name)) _extensionCompilationCache.TryRemove(name, out _);
		if (_extensionReferenceCache.ContainsKey(name)) _extensionReferenceCache.Remove(name);
	}
	internal static void _injectPatchedReferences()
	{
		foreach (var assembly in Community.Runtime.Config.Publicizer.PublicizedAssemblies)
		{
			var correctedName = assembly.Replace(".dll", string.Empty);
			using var stream = new MemoryStream(API.Assembly.PatchedAssemblies.AssemblyCache[correctedName]);
			_referenceCache[correctedName] = PortableExecutableReference.CreateFromStream(stream);
		}
	}
	internal void _injectReference(string id, string name, List<MetadataReference> references, string[] directories, bool direct = false, bool allowCache = true)
	{
		if (allowCache && _referenceCache.TryGetValue(name, out var reference))
		{
			Logger.Debug(id, $"Added common references from cache '{name}'", 4);
			references.Add(reference);
		}
		else
		{
			var raw = (byte[])null;

			if (direct)
			{
				var found = false;
				foreach (var directory in directories)
				{
					foreach (var file in OsEx.Folder.GetFilesWithExtension(directory, "dll"))
					{
						if (!file.Contains(name)) continue;
						raw = OsEx.File.ReadBytes(file);
						found = true;
						break;
					}

					if (found) break;
				}
			}
			else
			{
				raw = Community.Runtime.AssemblyEx.Read(name, directories);
			}

			if (raw == null) return;

			using var mem = new MemoryStream(raw);
			var processedReference = MetadataReference.CreateFromStream(mem);

			references.Add(processedReference);
			_referenceCache[name] = processedReference;
			Logger.Debug(id, $"Added common reference '{name}'", 4);
		}
	}
	internal void _injectExtensionReference(string name, List<MetadataReference> references)
	{
		if (_extensionReferenceCache.TryGetValue(name, out var reference))
		{
			references.Add(reference);
		}
		else
		{
			var raw = Community.Runtime.AssemblyEx.Read(name, _libraryDirectories);
			if (raw == null) return;

			using var mem = new MemoryStream(raw);
			var processedReference = MetadataReference.CreateFromStream(mem);

			references.Add(processedReference);
			_extensionReferenceCache.Add(name, processedReference);
		}
	}
	internal List<MetadataReference> _addReferences()
	{
		var references = new List<MetadataReference>();
		var id = Path.GetFileNameWithoutExtension(InitialSource.FilePath);

		_injectReference(id, "0Harmony", references, _libraryDirectories);

		foreach (var item in Community.Runtime.AssemblyEx.RefWhitelist)
		{
			try
			{
				_injectReference(id, item, references, _libraryDirectories);
			}
			catch (System.Exception ex)
			{
				Logger.Debug(id, $"Error loading common reference '{item}': {ex}", 4);
			}
		}

		foreach (var item in Community.Runtime.AssemblyEx.Modules.Loaded)
		{
			try
			{
				var name = Path.GetFileName(item.Value.Key);
				using var mem = new MemoryStream(item.Value.Value);
				var processedReference = MetadataReference.CreateFromStream(mem);

				references.Add(processedReference);
				_referenceCache[name] = processedReference;
			}
			catch (System.Exception ex)
			{
				Logger.Debug(id, $"Error loading module reference '{item}': {ex}", 4);
			}
		}

		foreach (var item in Community.Runtime.AssemblyEx.Extensions.Loaded)
		{
			try
			{
				_injectExtensionReference(Path.GetFileName(item.Value.Key), references);
			}
			catch (System.Exception ex)
			{
				Logger.Debug(id, $"Error loading extension reference '{item}': {ex}", 4);
			}
		}

		return references;
	}

	#endregion

	public class CompilerException : Exception
	{
		public string FilePath;
		public CompilerError Error;

		public CompilerException(string filePath, CompilerError error)
		{
			FilePath = filePath;
			Error = error;
		}

		public override string ToString()
		{
			return $"{Error.ErrorText}\n ({FilePath} {Error.Column} line {Error.Line})";
		}
	}

	private List<MetadataReference> references;

	public override void Start()
	{
		references = _addReferences();

		foreach (var require in Requires)
		{
			try
			{
				var requiredPlugin = _getPlugin(require);

				using var dllStream = new MemoryStream(requiredPlugin);
				references.Add(MetadataReference.CreateFromStream(dllStream));
			}
			catch (Exception exception)
			{
				Logger.Error($"Failed loading required plugin for '{InitialSource.ContextFileName}': {require}", exception);
			}
		}

		foreach (var reference in References)
		{
			try
			{
				var extensionFile = Path.Combine(Defines.GetExtensionsFolder(), $"{reference}.dll");
				if (OsEx.File.Exists(extensionFile))
				{
					_injectExtensionReference(extensionFile, references);
					continue;
				}

				var libFile = Path.Combine(Defines.GetLibFolder(), $"{reference}.dll");
				if (OsEx.File.Exists(libFile))
				{
					_injectReference(reference, libFile, references, _libraryDirectories);
					continue;
				}

				var managedFile = Path.Combine(Defines.GetRustManagedFolder(), $"{reference}.dll");
				if (OsEx.File.Exists(managedFile))
				{
					_injectReference(reference, managedFile, references, _libraryDirectories);
					continue;
				}
			}
			catch (Exception exception)
			{
				Logger.Error($"Failed loading reference for '{InitialSource.ContextFileName}': {reference}", exception);
			}
		}

		base.Start();
	}
	public override void ThreadFunction()
	{
		if (Sources.TrueForAll(x => string.IsNullOrEmpty(x.Content)))
		{
			Dispose();
			return;
		}

		try
		{
			Exceptions.Clear();
			Warnings.Clear();

			var trees = Facepunch.Pool.Get<List<SyntaxTree>>();
			var conditionals = Facepunch.Pool.Get<List<string>>();

			_stopwatch = Facepunch.Pool.Get<Stopwatch>();

			try
			{
				conditionals.AddRange(Community.Runtime.Config.Compiler.ConditionalCompilationSymbols);
			}
			catch (Exception ex)
			{
				Logger.Error($"Failed referencing conditional compilation symbols", ex);
			}
#if WIN
			conditionals.Add("WIN");
#else
			conditionals.Add("UNIX");
#endif

#if MINIMAL
			conditionals.Add("MINIMAL");
#endif

#if RUST_STAGING
			conditionals.Add("RUST_STAGING");
#elif RUST_RELEASE
			conditionals.Add("RUST_RELEASE");
#elif RUST_AUX01
			conditionals.Add("RUST_AUX01");
#elif RUST_AUX02
			conditionals.Add("RUST_AUX02");
#endif

			string pdbFilename =
#if DEBUG
				Debugger.IsAttached
					? (string.IsNullOrEmpty(Community.Runtime.Config.Debugging.ScriptDebuggingOrigin)
						? InitialSource.ContextFilePath
						: Path.Combine(Community.Runtime.Config.Debugging.ScriptDebuggingOrigin, InitialSource.ContextFileName))
					: InitialSource.ContextFileName;
#else
				InitialSource.ContextFileName;
#endif

			var parseOptions = new CSharpParseOptions(LanguageVersion.Preview)
				.WithPreprocessorSymbols(conditionals);

			var containsInternalCallHookOverride = Sources.Any(x =>
				!string.IsNullOrEmpty(x.Content) && x.Content.Contains(_internalCallHookPattern));

			foreach (var source in Sources)
			{
				var tree = CSharpSyntaxTree.ParseText(
					source.Content, options: parseOptions, source.FilePath, Encoding.UTF8);

				var root = tree.GetCompilationUnitRoot();

				HookCaller.HandleVersionConditionals(root, conditionals);

				parseOptions = parseOptions.WithPreprocessorSymbols(conditionals);

				tree = tree.WithRootAndOptions(root, parseOptions);

				if (HookCaller.FindPluginInfo(root, out var @namespace, out var namespaceIndex, out var classIndex, ClassList))
				{
					var @class = ClassList[0];

					if (!@class.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword)))
					{
						@class = @class.WithModifiers(@class.Modifiers.Add(SyntaxFactory.ParseToken(_partialPattern)));
					}

					root = root.WithMembers(root.Members.RemoveAt(namespaceIndex).Insert(namespaceIndex,
						@namespace.WithMembers(@namespace.Members.RemoveAt(classIndex).Insert(classIndex, @class))));
					trees.Insert(0, CSharpSyntaxTree.ParseText(
						root.ToFullString(), options: parseOptions, source.FilePath, Encoding.UTF8));
				}
				else
				{
					trees.Add(tree);
				}

				foreach (var name in root.Usings.Select(element => element.Name.ToString())
					         .Where(name => !Usings.Contains(name)))
				{
					Usings.Add(name);
				}
			}

			if (!containsInternalCallHookOverride)
			{
				_stopwatch.Start();

				var completeBody = CSharpSyntaxTree.ParseText(
					Sources.Select(x => x.Content).ToString("\n"), options: parseOptions, pdbFilename, Encoding.UTF8);

				HookCaller.GeneratePartial(completeBody.GetCompilationUnitRoot(), out var partialTree, parseOptions,
					pdbFilename, ClassList);

				InternalCallHookGenTime = _stopwatch.Elapsed;

				if (partialTree != null)
				{
					InternalCallHookSource = partialTree.NormalizeWhitespace().ToFullString();
					trees.Add(partialTree.SyntaxTree);
				}
			}

			var options = new CSharpCompilationOptions(
				OutputKind.DynamicallyLinkedLibrary,
				optimizationLevel:
#if DEBUG
				Debugger.IsAttached ? OptimizationLevel.Debug : OptimizationLevel.Release,
#else
				OptimizationLevel.Release,
#endif
				deterministic: true, warningLevel: 4,
				allowUnsafe: true
			);

			_stopwatch.Restart();

			var compilation = CSharpCompilation.Create(
				$"Script.{InitialSource.FileName}.{Guid.NewGuid():N}", trees, references, options);

			using (var dllStream = new MemoryStream())
			{
				var emit = compilation.Emit(dllStream, options: _emitOptions);

				var errors = Facepunch.Pool.Get<List<string>>();
				var warnings = Facepunch.Pool.Get<List<string>>();

				foreach (var error in emit.Diagnostics)
				{
					if (errors.Contains(error.Id) || warnings.Contains(error.Id))
					{
						continue;
					}

					var span = error.Location.GetMappedLineSpan().Span;

					var filePath = error?.Location?.SourceTree?.FilePath;
					var fileName = Path.GetFileNameWithoutExtension(filePath);

					switch (error.Severity)
					{
						case DiagnosticSeverity.Error:
							errors.Add(error.Id);
							Exceptions.Add(new CompilerException(filePath,
								new CompilerError(fileName, span.Start.Line + 1, span.Start.Character + 1, error.Id,
									error.GetMessage(CultureInfo.InvariantCulture))));

							break;

						case DiagnosticSeverity.Warning:
							if (error.GetMessage(CultureInfo.InvariantCulture).Contains("Assuming assembly reference"))
								continue;

							errors.Add(error.Id);
							Warnings.Add(new CompilerException(filePath,
								new CompilerError(fileName, span.Start.Line + 1, span.Start.Character + 1, error.Id,
									error.GetMessage(CultureInfo.InvariantCulture))));
							break;
					}
				}

				Facepunch.Pool.FreeUnmanaged(ref errors);
				Facepunch.Pool.FreeUnmanaged(ref warnings);

				if (emit.Success)
				{
					var assembly = dllStream.ToArray();
					if (assembly != null)
					{
						if (IsExtension)
						{
							_overrideExtensionPlugin(InitialSource.ContextFilePath, assembly);
						}

						_overridePlugin(Path.GetFileNameWithoutExtension(InitialSource.ContextFilePath), assembly);
						Assembly = Assembly.Load(assembly);

						try
						{
							var name = Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(InitialSource.ContextFileName)
								? InitialSource.FileName
								: InitialSource.ContextFileName);

							var isProfiled = MonoProfiler.TryStartProfileFor(MonoProfilerConfig.ProfileTypes.Plugin, Assembly, name, true);
							Assemblies.Plugins.Update(name, Assembly, string.IsNullOrEmpty(InitialSource.ContextFilePath) ? InitialSource.FilePath : InitialSource.ContextFilePath, isProfiled);
						}
						catch (Exception ex)
						{
							Logger.Error($"Couldn't cache assembly in Carbon's global database", ex);
						}
					}
				}
			}

			references.Clear();
			references = null;
			Facepunch.Pool.FreeUnmanaged(ref conditionals);
			Facepunch.Pool.FreeUnmanaged(ref trees);

			CompileTime = _stopwatch.Elapsed;
			_stopwatch.Reset();
			Facepunch.Pool.FreeUnsafe(ref _stopwatch);

			if (Assembly == null) return;

			foreach (var type in Assembly.GetTypes())
			{
				var hooks = new List<uint>();
				var hookMethods = new List<HookMethodAttribute>();
				var pluginReferences = new List<PluginReferenceAttribute>();
				Hooks.Add(type, hooks);
				HookMethods.Add(type, hookMethods);
				PluginReferences.Add(type, pluginReferences);

				foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance |
				                                       BindingFlags.NonPublic))
				{

					if (Community.Runtime.HookManager.IsHook(method.Name))
					{
						var hash = HookStringPool.GetOrAdd(method.Name);

						if (!hooks.Contains(hash)) hooks.Add(hash);
					}
					else
					{
						var attribute = method.GetCustomAttribute<HookMethodAttribute>();
						if (attribute == null) continue;

						attribute.Method = method;
						hookMethods.Add(attribute);
					}
				}

				foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic |
				                                     BindingFlags.Public))
				{
					var attribute = field.GetCustomAttribute<PluginReferenceAttribute>();
					if (attribute == null) continue;

					attribute.Field = field;
					pluginReferences.Add(attribute);
				}
			}

			if (Exceptions.Count > 0) throw null;
		}
		catch (Exception ex)
		{
			Logger.Error($"Threading compilation failed for '{InitialSource?.ContextFilePath}'", ex);
			Analytics.plugin_native_compile_fail(InitialSource, ex);
		}
	}
	public override void Dispose()
	{
		ClassList?.Clear();

		Exceptions?.Clear();
		Warnings?.Clear();

		Hooks?.Clear();
		HookMethods?.Clear();
		PluginReferences?.Clear();

		ClassList = null;
		Hooks = null;
		HookMethods = null;
		PluginReferences = null;
		Exceptions = null;
		Warnings = null;
		InternalCallHookSource = null;
	}
}
