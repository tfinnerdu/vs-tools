using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DoaneDevTools.Analyzers.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DS007_ConductorTaskResultAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticIds.DS007,
            title: "Conductor Task Result Missing Required Properties",
            messageFormat: "Class '{0}' is a Conductor worker but returns an object missing '{1}' property/properties. Task results must include both 'Status' and 'Output'.",
            category: "DoaneStack",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Classes marked with [MotorTaskWorker] or [WorkerTask] or implementing IWorker must return objects with Status and Output properties.");

        private static readonly ImmutableHashSet<string> WorkerAttributes = ImmutableHashSet.Create(
            "MotorTaskWorker",
            "MotorTaskWorkerAttribute",
            "WorkerTask",
            "WorkerTaskAttribute");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;

            // Check if the class is a Conductor worker
            if (!IsWorkerClass(classDecl))
                return;

            // Find methods that return non-void, non-Task objects (result objects)
            var returningMethods = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => !IsVoidOrTaskReturn(m))
                .ToList();

            if (!returningMethods.Any())
                return;

            // For each return type, check if it has Status and Output properties
            foreach (var method in returningMethods)
            {
                var returnTypeName = method.ReturnType.ToString();

                // Try to find the return type in the semantic model
                var returnTypeInfo = context.SemanticModel.GetTypeInfo(method.ReturnType, context.CancellationToken);
                var returnType = returnTypeInfo.Type;

                if (returnType == null)
                    continue;

                // Skip Task<T> — unwrap generics
                ITypeSymbol effectiveType = returnType;
                if (returnType is INamedTypeSymbol named && named.IsGenericType &&
                    named.Name == "Task")
                {
                    effectiveType = named.TypeArguments.FirstOrDefault() ?? returnType;
                }

                // Check for Status and Output properties
                bool hasStatus = HasPropertyNamed(effectiveType, "Status", "status");
                bool hasOutput = HasPropertyNamed(effectiveType, "Output", "output");

                if (!hasStatus || !hasOutput)
                {
                    var missing = !hasStatus && !hasOutput ? "Status, Output" :
                                  !hasStatus ? "Status" : "Output";

                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        classDecl.Identifier.GetLocation(),
                        classDecl.Identifier.Text,
                        missing));
                    break; // Report once per class
                }
            }
        }

        private static bool IsWorkerClass(ClassDeclarationSyntax classDecl)
        {
            // Check for worker attributes
            foreach (var attributeList in classDecl.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attrName = attribute.Name.ToString();
                    if (WorkerAttributes.Contains(attrName))
                        return true;
                }
            }

            // Check for IWorker interface
            if (classDecl.BaseList != null)
            {
                foreach (var baseType in classDecl.BaseList.Types)
                {
                    var typeName = baseType.Type.ToString();
                    if (typeName == "IWorker" || typeName.EndsWith(".IWorker"))
                        return true;
                }
            }

            return false;
        }

        private static bool IsVoidOrTaskReturn(MethodDeclarationSyntax method)
        {
            var returnType = method.ReturnType;

            if (returnType is PredefinedTypeSyntax predefined &&
                predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
                return true;

            var typeName = returnType.ToString();
            return typeName == "Task" || typeName == "void";
        }

        private static bool HasPropertyNamed(ITypeSymbol type, params string[] names)
        {
            if (type == null)
                return false;

            var members = type.GetMembers();
            foreach (var member in members)
            {
                if (member is IPropertySymbol)
                {
                    foreach (var name in names)
                    {
                        if (member.Name == name)
                            return true;
                    }
                }
            }

            // Check base types
            if (type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
                return HasPropertyNamed(type.BaseType, names);

            return false;
        }
    }
}
