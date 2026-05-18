using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using DoaneDevTools.ToolWindows.ApiDriftChecker.Services;

namespace DoaneDevTools.ToolWindows.ApiDriftChecker.Services
{
    /// <summary>Parses OpenAPI 3.x YAML/JSON spec files into endpoint descriptors.</summary>
    public class OpenApiParserService
    {
        public List<SpecEndpoint> ParseSpec(string specFilePath)
        {
            var endpoints = new List<SpecEndpoint>();

            using var stream = File.OpenRead(specFilePath);
            var reader = new OpenApiStreamReader();
            var doc = reader.Read(stream, out var diagnostic);

            if (doc?.Paths == null) return endpoints;

            foreach (var (path, pathItem) in doc.Paths)
            {
                foreach (var (operationType, operation) in pathItem.Operations)
                {
                    endpoints.Add(new SpecEndpoint
                    {
                        HttpMethod = operationType.ToString().ToUpper(),
                        RouteTemplate = path,
                        OperationId = operation.OperationId,
                        Parameters = operation.Parameters?
                            .Select(p => new SpecParameter
                            {
                                Name = p.Name,
                                TypeName = p.Schema?.Type ?? "string",
                                IsRequired = p.Required,
                                Location = p.In?.ToString() ?? "query"
                            }).ToList() ?? new List<SpecParameter>(),
                        ResponseCodes = operation.Responses?.Keys.ToList() ?? new List<string>()
                    });
                }
            }

            return endpoints;
        }
    }

    public class SpecEndpoint
    {
        public string HttpMethod { get; set; } = string.Empty;
        public string RouteTemplate { get; set; } = string.Empty;
        public string? OperationId { get; set; }
        public List<SpecParameter> Parameters { get; set; } = new();
        public List<string> ResponseCodes { get; set; } = new();

        public string RouteKey => $"{HttpMethod.ToUpper()} {NormalizeRoute(RouteTemplate)}";

        private static string NormalizeRoute(string route) =>
            System.Text.RegularExpressions.Regex.Replace(route, @"\{[^}]+\}", "{id}");
    }

    public class SpecParameter
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public string Location { get; set; } = "query";
    }
}
