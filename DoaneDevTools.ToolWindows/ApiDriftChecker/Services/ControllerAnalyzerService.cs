using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using DoaneDevTools.ToolWindows.ApiDriftChecker.Models;

namespace DoaneDevTools.ToolWindows.ApiDriftChecker.Services
{
    /// <summary>Extracts route/parameter info from C# ControllerBase subclasses via Roslyn.</summary>
    public class ControllerAnalyzerService
    {
        /// <summary>Scans all C# projects in the solution for controller action methods.</summary>
        public async Task<List<ControllerEndpoint>> GetEndpointsAsync(string solutionPath)
        {
            var endpoints = new List<ControllerEndpoint>();

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath);

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                foreach (var tree in compilation.SyntaxTrees)
                {
                    var model = compilation.GetSemanticModel(tree);
                    var root = await tree.GetRootAsync();

                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                    foreach (var cls in classes)
                    {
                        var clsSymbol = model.GetDeclaredSymbol(cls) as INamedTypeSymbol;
                        if (clsSymbol == null) continue;
                        if (!InheritsFromControllerBase(clsSymbol)) continue;

                        var classRoute = GetRouteTemplate(cls);

                        foreach (var method in cls.Members.OfType<MethodDeclarationSyntax>())
                        {
                            var httpMethod = GetHttpMethod(method);
                            if (httpMethod == null) continue;

                            var actionRoute = GetRouteTemplate(method);
                            var fullRoute = CombineRoutes(classRoute, actionRoute, clsSymbol.Name);
                            var methodSymbol = model.GetDeclaredSymbol(method) as IMethodSymbol;

                            endpoints.Add(new ControllerEndpoint
                            {
                                HttpMethod = httpMethod,
                                RouteTemplate = fullRoute,
                                ControllerName = clsSymbol.Name,
                                ActionName = method.Identifier.Text,
                                FilePath = tree.FilePath,
                                LineNumber = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                                Parameters = GetParameters(methodSymbol),
                                ReturnType = methodSymbol?.ReturnType.ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }

            return endpoints;
        }

        private bool InheritsFromControllerBase(INamedTypeSymbol symbol)
        {
            var current = symbol.BaseType;
            while (current != null)
            {
                if (current.Name == "ControllerBase" || current.Name == "Controller")
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        private string? GetHttpMethod(MemberDeclarationSyntax member)
        {
            foreach (var attr in member.AttributeLists.SelectMany(a => a.Attributes))
            {
                var name = attr.Name.ToString();
                if (name.Contains("HttpGet")) return "GET";
                if (name.Contains("HttpPost")) return "POST";
                if (name.Contains("HttpPut")) return "PUT";
                if (name.Contains("HttpDelete")) return "DELETE";
                if (name.Contains("HttpPatch")) return "PATCH";
            }
            return null;
        }

        private string GetRouteTemplate(SyntaxNode node)
        {
            foreach (var attrList in ((MemberDeclarationSyntax)node).AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (name.Contains("Route") || name.Contains("Http"))
                    {
                        var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
                        if (arg?.Expression is LiteralExpressionSyntax lit)
                            return lit.Token.ValueText;
                    }
                }
            }
            return string.Empty;
        }

        private string CombineRoutes(string classRoute, string actionRoute, string controllerName)
        {
            classRoute = classRoute.Replace("[controller]",
                controllerName.Replace("Controller", string.Empty).ToLower());
            if (string.IsNullOrEmpty(actionRoute)) return "/" + classRoute.TrimStart('/');
            if (string.IsNullOrEmpty(classRoute)) return "/" + actionRoute.TrimStart('/');
            return "/" + classRoute.TrimEnd('/') + "/" + actionRoute.TrimStart('/');
        }

        private List<EndpointParameter> GetParameters(IMethodSymbol? method)
        {
            if (method == null) return new();
            return method.Parameters
                .Where(p => p.Name != "cancellationToken")
                .Select(p => new EndpointParameter
                {
                    Name = p.Name,
                    TypeName = p.Type.ToString(),
                    IsRequired = !p.HasExplicitDefaultValue && !p.Type.IsReferenceType
                })
                .ToList();
        }
    }

    public class ControllerEndpoint
    {
        public string HttpMethod { get; set; } = string.Empty;
        public string RouteTemplate { get; set; } = string.Empty;
        public string ControllerName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public List<EndpointParameter> Parameters { get; set; } = new();
        public string ReturnType { get; set; } = string.Empty;
    }

    public class EndpointParameter
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
    }
}
