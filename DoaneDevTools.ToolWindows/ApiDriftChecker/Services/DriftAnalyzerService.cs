using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DoaneDevTools.ToolWindows.ApiDriftChecker.Models;

namespace DoaneDevTools.ToolWindows.ApiDriftChecker.Services
{
    /// <summary>Compares controller endpoints against spec endpoints and produces a DriftReport.</summary>
    public class DriftAnalyzerService
    {
        public DriftReport Analyze(
            List<ControllerEndpoint> codeEndpoints,
            List<SpecEndpoint> specEndpoints,
            string controllerFile,
            string specFile)
        {
            var report = new DriftReport
            {
                ControllerFile = controllerFile,
                SpecFile = specFile
            };

            var specByKey = specEndpoints.ToDictionary(
                e => NormalizeRoute(e.HttpMethod, e.RouteTemplate),
                StringComparer.OrdinalIgnoreCase);

            var codeByKey = codeEndpoints.ToDictionary(
                e => NormalizeRoute(e.HttpMethod, e.RouteTemplate),
                StringComparer.OrdinalIgnoreCase);

            // Check code endpoints against spec
            foreach (var codeEndpoint in codeEndpoints)
            {
                var key = NormalizeRoute(codeEndpoint.HttpMethod, codeEndpoint.RouteTemplate);

                if (!specByKey.TryGetValue(key, out var specEndpoint))
                {
                    report.Items.Add(new DriftItem
                    {
                        Status = DriftStatus.MissingFromSpec,
                        HttpMethod = codeEndpoint.HttpMethod,
                        Route = codeEndpoint.RouteTemplate,
                        ControllerName = codeEndpoint.ControllerName,
                        ActionName = codeEndpoint.ActionName,
                        FilePath = codeEndpoint.FilePath,
                        LineNumber = codeEndpoint.LineNumber
                    });
                    continue;
                }

                // Check parameter drift
                var paramMismatch = FindParameterMismatch(codeEndpoint.Parameters, specEndpoint.Parameters);
                if (paramMismatch != null)
                {
                    report.Items.Add(new DriftItem
                    {
                        Status = DriftStatus.ParameterMismatch,
                        HttpMethod = codeEndpoint.HttpMethod,
                        Route = codeEndpoint.RouteTemplate,
                        ControllerName = codeEndpoint.ControllerName,
                        ActionName = codeEndpoint.ActionName,
                        MismatchDetail = paramMismatch,
                        FilePath = codeEndpoint.FilePath,
                        LineNumber = codeEndpoint.LineNumber
                    });
                }
                else
                {
                    report.Items.Add(new DriftItem
                    {
                        Status = DriftStatus.InSync,
                        HttpMethod = codeEndpoint.HttpMethod,
                        Route = codeEndpoint.RouteTemplate,
                        ControllerName = codeEndpoint.ControllerName,
                        ActionName = codeEndpoint.ActionName,
                        FilePath = codeEndpoint.FilePath,
                        LineNumber = codeEndpoint.LineNumber
                    });
                }
            }

            // Check spec endpoints not in code
            foreach (var specEndpoint in specEndpoints)
            {
                var key = NormalizeRoute(specEndpoint.HttpMethod, specEndpoint.RouteTemplate);
                if (!codeByKey.ContainsKey(key))
                {
                    report.Items.Add(new DriftItem
                    {
                        Status = DriftStatus.MissingFromCode,
                        HttpMethod = specEndpoint.HttpMethod,
                        Route = specEndpoint.RouteTemplate
                    });
                }
            }

            // Sort: issues first, then in-sync
            report.Items = report.Items
                .OrderBy(i => i.Status == DriftStatus.InSync ? 1 : 0)
                .ThenBy(i => i.Route)
                .ToList();

            return report;
        }

        private static string NormalizeRoute(string method, string route)
        {
            var normalized = Regex.Replace(route, @"\{[^}:]+(?::[^}]*)?\}", "{id}");
            return $"{method.ToUpper()} {normalized.ToLower()}";
        }

        private static string? FindParameterMismatch(
            List<EndpointParameter> codeParams,
            List<SpecParameter> specParams)
        {
            var codeByName = codeParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var specByName = specParams.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var specParam in specParams)
            {
                if (!codeByName.TryGetValue(specParam.Name, out var codeParam))
                    return $"spec expects '{specParam.Name}' ({specParam.TypeName}), not in code";

                if (!TypesCompatible(codeParam.TypeName, specParam.TypeName))
                    return $"spec expects '{specParam.Name}' ({specParam.TypeName}), code has ({codeParam.TypeName})";
            }

            return null;
        }

        private static bool TypesCompatible(string codeType, string specType)
        {
            // Simple heuristic — proper type mapping would be more thorough
            if (specType == "integer" && (codeType.Contains("int") || codeType.Contains("long")))
                return true;
            if (specType == "string" && codeType.Contains("string", StringComparison.OrdinalIgnoreCase))
                return true;
            if (specType == "boolean" && codeType.Contains("bool"))
                return true;
            if (specType == "number" && (codeType.Contains("decimal") || codeType.Contains("double") || codeType.Contains("float")))
                return true;
            return string.Equals(codeType, specType, StringComparison.OrdinalIgnoreCase);
        }
    }
}
