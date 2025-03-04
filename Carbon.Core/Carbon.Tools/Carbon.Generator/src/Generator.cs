using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Carbon;
using Carbon.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public sealed class Generator
{
	public static void Generate()
	{
		try
		{
			var options = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None, preprocessorSymbols: new string[]
			{
				"DEBUG"
			});

			var syntaxTrees = Directory.GetFiles(Program.Arguments.PluginInput, "*.cs", SearchOption.TopDirectoryOnly).Select(file =>
			{
				Console.Write($"  {Path.GetFileName(file)}");
				var code = CSharpSyntaxTree.ParseText(File.ReadAllText(file), options);
				Console.Write(" done.\n");

				return code;
			});
			var nameSpace = (NameSyntax)null;
			var usings = new List<UsingDirectiveSyntax>();
			var groupedMembers = new Dictionary<string, List<MemberDeclarationSyntax>>();

			foreach (var syntaxTree in syntaxTrees)
			{
				var root = syntaxTree.GetCompilationUnitRoot();
				usings.AddRange(root.Usings);
				foreach (var member in root.Members)
				{
					if (member is FileScopedNamespaceDeclarationSyntax namespaceSyntax)
					{
						foreach (var type in namespaceSyntax.Members)
						{
							if (type is ClassDeclarationSyntax classSyntax)
							{
								var key = $"{namespaceSyntax.Name}_{classSyntax.Identifier}";
								nameSpace = namespaceSyntax.Name;

								if (!groupedMembers.TryGetValue(key, out var list))
								{
									groupedMembers.Add(key, list = new List<MemberDeclarationSyntax>());
								}

								list.AddRange(classSyntax.Members);
							}
						}
					}
				}
			}

			var mergedMembers = new List<MemberDeclarationSyntax>();
			foreach (var group in groupedMembers)
			{
				var typeNameParts = group.Key.Split('_');
				var classSyntax = SyntaxFactory.ClassDeclaration($" {typeNameParts[1]}").WithMembers(SyntaxFactory.List(group.Value));
				var namespaceSyntax = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(typeNameParts[0].Replace("_", "."))).AddMembers(classSyntax);
				mergedMembers.Add(namespaceSyntax.Members[namespaceSyntax.Members.Count - 1]);
			}

			var compilationUnit = SyntaxFactory.CompilationUnit();
			var nameSpace2 = SyntaxFactory.NamespaceDeclaration(nameSpace, default, SyntaxFactory.List(usings), SyntaxFactory.List(mergedMembers));
			compilationUnit = compilationUnit.WithMembers(compilationUnit.Members.Add(nameSpace2));

			var classes = new List<ClassDeclarationSyntax>();

			classes.AddRange(nameSpace2.Members.OfType<ClassDeclarationSyntax>());

			HookCaller.GenerateInternalCallHook(compilationUnit, out var output, out var method, out var isPartial, Program.Arguments.BaseCall, Program.Arguments.BaseName, classList: classes);

			var prettyFormat = $@"{usings.Select(x => x.ToString()).Distinct().ToString("\n")}

namespace {Program.Arguments.PluginNamespace};

public partial class {Program.Arguments.PluginName}
{{
{method.ToFullString()}
}}";

			OsEx.File.Create(Program.Arguments.PluginOutput, CSharpSyntaxTree.ParseText(prettyFormat, options, string.Empty, Encoding.UTF8).GetCompilationUnitRoot().NormalizeWhitespace().ToFullString());

		}
		catch (Exception ex)
		{
			Console.WriteLine($"** CorePlugin - {ex}");
		}
	}
}
