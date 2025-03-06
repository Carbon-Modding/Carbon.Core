using System.Text;
using Carbon.Pooling;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using HarmonyLib;

namespace Carbon.Generator;

public class InternalCallHook
{
	public static void Generate(CompilationUnitSyntax input, out CompilationUnitSyntax output, out MethodDeclarationSyntax generatedMethod, out bool isPartial, bool baseCall = false, string baseName = "plugin", List<ClassDeclarationSyntax> classList = null)
	{
		var methodContents = $"\n\tvar length = args?.Length;\ntry {{ switch(hook) {{ ";

		var @namespace = (BaseNamespaceDeclarationSyntax)null;
		var namespaceIndex = 0;
		var classIndex = 0;

		if (classList == null)
		{
			classList = new List<ClassDeclarationSyntax>();
			FindPluginInfo(input, out @namespace, out _, out _, classList);
		}
		else
		{
			FindPluginInfo(input, out @namespace, out _, out _, null);

			namespaceIndex = classIndex = 0;
		}

		if (classList.Count == 0)
		{
			output = null;
			generatedMethod = null;
			isPartial = default;
			return;
		}

		var @class = classList[0];

		if (@namespace == null)
		{
			@namespace = @class.Parent as BaseNamespaceDeclarationSyntax;
		}

		isPartial = @class.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword));

		var methodDeclarations = new List<MethodDeclarationSyntax>();
		methodDeclarations.AddRange(classList.SelectMany(x => x.ChildNodes()).OfType<MethodDeclarationSyntax>());

		var hookableMethods = new Dictionary<uint, List<MethodDeclarationSyntax>>();
		var privateMethods0 = methodDeclarations.Where(md => (md.Modifiers.Count == 0 || md.Modifiers.All(modifier => !modifier.IsKind(SyntaxKind.PublicKeyword) && !modifier.IsKind(SyntaxKind.StaticKeyword)) || md.AttributeLists.Any(x => x.Attributes.Any(y => y.Name.ToString() == "HookMethod"))) && md.TypeParameterList == null);
		var privateMethods = privateMethods0.OrderBy(x => x.Identifier.ValueText);
		privateMethods0 = null;

		foreach (var method in privateMethods)
		{
			var hookMethod = method.AttributeLists.Select(x => x.Attributes.FirstOrDefault(x => x.Name.ToString() == "HookMethod")).FirstOrDefault();
			var methodName = hookMethod != null && hookMethod.ArgumentList.Arguments.Count > 0 ? hookMethod.ArgumentList.Arguments[0].ToString().Replace("\"", string.Empty) : method.Identifier.ValueText;

			if (hookMethod != null)
			{
				var context = hookMethod.ArgumentList.Arguments[0];
				var contextString = context.ToString();

				if (contextString.Contains("nameof"))
				{
					methodName = contextString
						.Replace("nameof", string.Empty)
						.Replace("(", string.Empty)
						.Replace(")", string.Empty);

					if (methodName.Contains("."))
					{
						var temp = methodName.Split('.');
						methodName = temp[^1];
					}
				}
				else if (contextString.Contains("."))
				{
					var argument = context.Expression as MemberAccessExpressionSyntax;
					var expression = argument.Expression.ToString();
					var name = argument.Name.ToString();

					var value = AccessTools.Field(AccessTools.TypeByName(expression), name)?.GetValue(null)?.ToString();

					if (!string.IsNullOrEmpty(value))
					{
						methodName = value;
					}
				}
				else if (context.ToString().Contains("\""))
				{
					var value = AccessTools
						.Field(AccessTools.TypeByName(classList.FirstOrDefault().Identifier.Text),
							context.ToString().Replace("\"", string.Empty))?.GetValue(null)?.ToString();

					if (!string.IsNullOrEmpty(value))
					{
						methodName = value;
					}
				}
			}

			var id = HookStringPool.GetOrAdd(methodName);

			if (!hookableMethods.TryGetValue(id, out var list))
			{
				hookableMethods[id] = list = new();
			}

			list.Add(method);
		}

		foreach (var group in hookableMethods)
		{
			methodContents += $"\t\t\t\n// {group.Value[0].Identifier.ValueText} aka {group.Key}\n\t\t\tcase {group.Key}:\n\t\t\t{{";

			var overrideCount = 1;

			for (int i = 0; i < group.Value.Count; i++)
			{
				var parameterIndex = -1;
				var method = group.Value[i];
				var conditional = method.AttributeLists.Select(x => x.Attributes.FirstOrDefault(x => ((IdentifierNameSyntax)x.Name).Identifier.Text == "Conditional"))?.FirstOrDefault()?.ArgumentList?.Arguments[0].ToString().Replace("\"", string.Empty);
				var methodName = method.Identifier.ValueText;
				var parameters0 = method.ParameterList.Parameters.Select(x =>
				{
					var type = x.Type.ToString().Replace("?", string.Empty);
					parameterIndex++;

					if (x.Modifiers.Any(x => x.IsKind(SyntaxKind.OutKeyword)))
					{
						return $"out var arg{parameterIndex}_{i}";
					}

					if (x.Default != null || x.Type is NullableTypeSyntax)
					{
						return $"length > {parameterIndex} && args[{parameterIndex}] is {type} arg{parameterIndex}_{i} ? arg{parameterIndex}_{i} : ({type})default";
					}

					if (x.Modifiers.Any(x => x.IsKind(SyntaxKind.RefKeyword)))
					{
						return $"ref arg{parameterIndex}_{i}";
					}

					return $"arg{parameterIndex}_{i}";
				});

				var parameters = parameters0.ToArray();
				var requiredParameters = method.ParameterList.Parameters.Where(x => x.Default == null && x.Type is not NullableTypeSyntax);

				var refSets = string.Empty;
				parameterIndex = 0;
				foreach (var @ref in method.ParameterList.Parameters)
				{
					if (@ref.Modifiers.Any(x => x.IsKind(SyntaxKind.RefKeyword) || x.IsKind(SyntaxKind.OutKeyword)))
					{
						refSets += $"args[{parameterIndex}] = arg{parameterIndex}_{i}; ";
					}

					parameterIndex++;
				}

				parameterIndex = -1;
				var parameterText = string.Empty;
				var varText = string.Empty;
				for (int o = 0; o < method.ParameterList.Parameters.Count; o++)
				{
					var parameter = method.ParameterList.Parameters[o];
					parameterIndex++;

					if (parameter.Default == null && !parameter.Modifiers.Any(y => y.IsKind(SyntaxKind.OutKeyword)) && parameter.Type is not NullableTypeSyntax && !(parameter.Type is ITypeSymbol symbol && symbol.IsValueType))
					{
						var type = parameter.Type.ToString().Replace("global::", string.Empty);

						varText += $"var narg{parameterIndex}_{i} = length > {parameterIndex} ? args[{parameterIndex}] is {type} or null : true;\nvar arg{parameterIndex}_{i} = length > {parameterIndex} && narg{parameterIndex}_{i} ? ({type})(args[{parameterIndex}] ?? ({type})default) : ({type})default;\n";
						parameterText += !IsUnmanagedType(parameter.Type) ? $"narg{parameterIndex}_{i} && " : $"(narg{parameterIndex}_{i} || args[{parameterIndex}] == null) && ";
					}
				}

				if (!string.IsNullOrEmpty(parameterText))
				{
					parameterText = parameterText[..^3];
				}

				methodContents += $"{(string.IsNullOrEmpty(conditional) ? string.Empty : $"\n#if {conditional}")}\t\t\t\n\t\t\t\t" +
					$"{varText}{(string.IsNullOrEmpty(parameterText) ? string.Empty : $"if({parameterText}) {{")} {(method.ReturnType.ToString() != "void" ? $"return " : string.Empty)}" +
					$"{methodName}({string.Join(", ", parameters)}); {refSets} " +
					$"{(string.IsNullOrEmpty(parameterText) ? string.Empty : $"}}")}{(string.IsNullOrEmpty(conditional) ? string.Empty : $"\n#endif")}\n";

				Array.Clear(parameters, 0, parameters.Length);
				parameters = null;
				parameters0 = null;
				requiredParameters = null;

				overrideCount++;
			}

			methodContents += "\t\t\t\tbreak;\n\t\t\t}";
		}

		methodContents += "}\n}\ncatch (System.Exception ex)\n{\nCarbon.Logger.Error($\"Failed to call internal hook '{Carbon.Pooling.HookStringPool.GetOrAdd(hook)}' on " + baseName + " '{" + (baseName == "plugin" ? "base.Name" : "this.Name") + "} v{ " + (baseName == "plugin" ? "base.Version" : "this.Version") + "}' [{hook}]\", ex);\n" +
						  "\nOnException(hook);\n}\n" +
			$"return {(baseCall ? "base.InternalCallHook(hook, args)" : "(object)null")};";

		generatedMethod = SyntaxFactory.MethodDeclaration(
			SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword).WithTrailingTrivia(SyntaxFactory.Space)),
			"InternalCallHook").AddParameterListParameters(
				SyntaxFactory.Parameter(SyntaxFactory.Identifier("hook")).WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.UIntKeyword)).WithTrailingTrivia(SyntaxFactory.Space)),
				SyntaxFactory.Parameter(SyntaxFactory.Identifier("args")).WithType(SyntaxFactory.ArrayType(
				SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)),
				SyntaxFactory.SingletonList(
						SyntaxFactory.ArrayRankSpecifier(
							SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
								SyntaxFactory.OmittedArraySizeExpression()
							)
						)
					)
				).WithTrailingTrivia(SyntaxFactory.Space)))
				.WithTrailingTrivia(SyntaxFactory.LineFeed)
			.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(SyntaxFactory.Space), SyntaxFactory.Token(SyntaxKind.OverrideKeyword).WithTrailingTrivia(SyntaxFactory.Space))
			.AddBodyStatements(SyntaxFactory.ParseStatement(methodContents)).WithTrailingTrivia(SyntaxFactory.LineFeed);

		output = input.WithMembers(input.Members.RemoveAt(namespaceIndex).Insert(namespaceIndex, @namespace.WithMembers(@namespace.Members.RemoveAt(classIndex).Insert(classIndex, @class.WithMembers(@class.Members.Insert(@class.Members.Count, generatedMethod))))));

		#region Cleanup

		methodDeclarations.Clear();
		methodDeclarations = null;
		foreach (var hookableMethod in hookableMethods)
		{
			hookableMethod.Value.Clear();
		}
		hookableMethods.Clear();
		hookableMethods = null;

		#endregion
	}

	public static void GeneratePartial(CompilationUnitSyntax input, out CompilationUnitSyntax output, CSharpParseOptions options, string fileName, List<ClassDeclarationSyntax> classes = null, string debugOutputPath = null)
	{
		Generate(input, out _, out var method, out var isPartial, classList: classes);

		if (method == null)
		{
			output = null;
			return;
		}

		var @namespace = (BaseNamespaceDeclarationSyntax)null;
		var @class = (ClassDeclarationSyntax)null;

		if (classes == null)
		{
			classes = new List<ClassDeclarationSyntax>();
			FindPluginInfo(input, out @namespace, out _, out _, classes);
		}
		else
		{
			@namespace = classes[0].Parent as BaseNamespaceDeclarationSyntax;
		}

		@class = classes[0];

		var usings = input.Usings;
		var subUsings = @namespace.Usings;

		var source = @$"{string.Join("\n", usings.Select(x => x.ToString()))}

namespace {@namespace.Name};
{(subUsings.Any() ? $"\n{string.Join("\n", subUsings.Select(x => x.ToString()))}" : string.Empty)}
partial class {@class.Identifier.ValueText}
{{
	{method}
}}";

		string path;

#if DEBUG
		if (isPartial)
		{
			path = Path.Combine(debugOutputPath, $"{Path.GetFileNameWithoutExtension(fileName)}.Internal.cs");
			output = CSharpSyntaxTree.ParseText(source, options, path, Encoding.UTF8).GetCompilationUnitRoot().NormalizeWhitespace();
			File.WriteAllText(path, output.ToFullString());
		}
		else
		{
			path = $"{fileName}/Internal";
			output = CSharpSyntaxTree.ParseText(source, options, path, Encoding.UTF8).GetCompilationUnitRoot();
		}
#else
		path = $"{fileName}/Internal";
		output = CSharpSyntaxTree.ParseText(source, options, path, Encoding.UTF8).GetCompilationUnitRoot();
#endif
	}

	public static bool FindPluginInfo(CompilationUnitSyntax input, out BaseNamespaceDeclarationSyntax @namespace, out int namespaceIndex, out int classIndex, List<ClassDeclarationSyntax> classes)
	{
		var @class = (ClassDeclarationSyntax)null;
		@namespace = null;
		namespaceIndex = 0;
		classIndex = 0;

		for (int n = 0; n < input.Members.Count; n++)
		{
			var memberA = input.Members[n];

			if (memberA is not BaseNamespaceDeclarationSyntax ns)
			{
				continue;
			}

			for (int c = 0; c < ns.Members.Count; c++)
			{
				var memberB = ns.Members[c];

				if (memberB is not ClassDeclarationSyntax cls)
				{
					continue;
				}

				if (cls.AttributeLists.Count > 0)
				{
					foreach (var attribute in cls.AttributeLists)
					{
						if (attribute.Attributes[0].Name is IdentifierNameSyntax nameSyntax && nameSyntax.Identifier.Text.Equals("Info"))
						{
							@namespaceIndex = n;
							@namespace = ns;
							classIndex = c;
							@class = cls;
							classes?.Insert(0, @class);
						}
					}
				}
				else if (cls.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword)))
				{
					classes?.Add(cls);
				}
			}
		}

		return @class != null;
	}

	public static bool IsUnmanagedType(TypeSyntax type)
	{
		return type is ITypeSymbol symbol && symbol.IsUnmanagedType;
	}
}
