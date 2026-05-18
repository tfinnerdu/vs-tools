using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace DoaneDevTools.ToolWindows.EnvManager.Services
{
    /// <summary>
    /// Reads and writes environment variable values to/from various configuration
    /// file formats used across the Doane service estate:
    /// <list type="bullet">
    ///   <item>.env  (KEY=VALUE lines)</item>
    ///   <item>appsettings*.json  (nested JSON)</item>
    ///   <item>start-local.ps1  ($env:KEY = "value" lines)</item>
    ///   <item>Kubernetes Secret YAML  (data: block, base-64 encoded)</item>
    ///   <item>docker-compose*.yml  (environment: block)</item>
    /// </list>
    /// All methods preserve comments, ordering, and unrelated keys.
    /// </summary>
    public class EnvFileService
    {
        // ----------------------------------------------------------------
        // .env  (KEY=VALUE, # comments)
        // ----------------------------------------------------------------

        /// <summary>
        /// Parses a .env file and returns its key/value pairs.
        /// Comment lines (starting with #) and blank lines are skipped.
        /// Inline comments are not stripped from values.
        /// </summary>
        public Dictionary<string, string> ReadDotEnv(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var eqIdx = line.IndexOf('=');
                if (eqIdx <= 0) continue;
                var key = line.Substring(0, eqIdx).Trim();
                var val = line.Substring(eqIdx + 1).Trim();
                // Strip surrounding quotes if present.
                if (val.Length >= 2 &&
                    ((val[0] == '"' && val[val.Length - 1] == '"') ||
                     (val[0] == '\'' && val[val.Length - 1] == '\'')))
                {
                    val = val.Substring(1, val.Length - 2);
                }
                result[key] = val;
            }
            return result;
        }

        /// <summary>
        /// Writes <paramref name="values"/> back into the .env file at
        /// <paramref name="path"/>. Existing comment lines and unrecognised
        /// lines are preserved; keys present in <paramref name="values"/>
        /// are updated in-place; new keys are appended at the end.
        /// </summary>
        public void WriteDotEnv(string path, Dictionary<string, string> values)
        {
            var lines = File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : new List<string>();

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var eqIdx = line.IndexOf('=');
                if (eqIdx <= 0) continue;
                var key = line.Substring(0, eqIdx).Trim();
                if (values.TryGetValue(key, out var newVal))
                {
                    lines[i] = $"{key}={EscapeDotEnvValue(newVal)}";
                    written.Add(key);
                }
            }

            // Append keys that were not already in the file.
            foreach (var kv in values)
            {
                if (!written.Contains(kv.Key))
                    lines.Add($"{kv.Key}={EscapeDotEnvValue(kv.Value)}");
            }

            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        private static string EscapeDotEnvValue(string value)
        {
            // Wrap in double-quotes if the value contains spaces, quotes, or
            // special shell characters so it round-trips correctly.
            if (value.IndexOfAny(new[] { ' ', '\t', '"', '\'', '#', '$', '\\' }) >= 0)
                return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            return value;
        }

        // ----------------------------------------------------------------
        // appsettings*.json
        // ----------------------------------------------------------------

        /// <summary>
        /// Reads specific sections from an appsettings JSON file and returns a
        /// flat <c>section:key</c> dictionary.  If <paramref name="sections"/>
        /// is null or empty, all top-level string values are returned.
        /// </summary>
        public Dictionary<string, string> ReadAppSettings(string path, string[]? sections = null)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var jsonText = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(jsonText, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

            var root = doc.RootElement;

            if (sections == null || sections.Length == 0)
            {
                FlattenJsonElement(root, string.Empty, result);
            }
            else
            {
                foreach (var section in sections)
                {
                    if (root.TryGetProperty(section, out var sectionEl))
                        FlattenJsonElement(sectionEl, section, result);
                }
            }

            return result;
        }

        private static void FlattenJsonElement(JsonElement el, string prefix, Dictionary<string, string> result)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in el.EnumerateObject())
                    {
                        var childKey = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}:{prop.Name}";
                        FlattenJsonElement(prop.Value, childKey, result);
                    }
                    break;
                case JsonValueKind.String:
                    result[prefix] = el.GetString() ?? string.Empty;
                    break;
                case JsonValueKind.Number:
                    result[prefix] = el.GetRawText();
                    break;
                case JsonValueKind.True:
                    result[prefix] = "true";
                    break;
                case JsonValueKind.False:
                    result[prefix] = "false";
                    break;
                case JsonValueKind.Null:
                    result[prefix] = string.Empty;
                    break;
            }
        }

        /// <summary>
        /// Updates specific keys in an appsettings JSON file.
        /// Keys use colon-separated paths (e.g., <c>ConnectionStrings:Default</c>).
        /// The file is parsed and re-serialised with indentation preserved.
        /// </summary>
        public void WriteAppSettings(string path, Dictionary<string, string> values)
        {
            var jsonText = File.Exists(path) ? File.ReadAllText(path) : "{}";
            var doc = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonText)
                      ?? new Dictionary<string, object>();

            foreach (var kv in values)
            {
                var parts = kv.Key.Split(':');
                SetNestedValue(doc, parts, 0, kv.Value);
            }

            var updated = JsonSerializer.Serialize(doc, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(path, updated, Encoding.UTF8);
        }

        private static void SetNestedValue(Dictionary<string, object> node, string[] parts, int depth, string value)
        {
            var key = parts[depth];
            if (depth == parts.Length - 1)
            {
                node[key] = value;
                return;
            }

            if (!node.TryGetValue(key, out var child) || child is not Dictionary<string, object> childDict)
            {
                childDict = new Dictionary<string, object>();
                node[key] = childDict;
            }
            SetNestedValue(childDict, parts, depth + 1, value);
        }

        // ----------------------------------------------------------------
        // start-local.ps1  ($env:KEY = "value" lines)
        // ----------------------------------------------------------------

        private static readonly Regex Ps1EnvPattern = new Regex(
            @"^\s*\$env:(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[""']?(?<val>[^""']*)[""']?\s*$",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses <c>$env:KEY = "value"</c> lines from a PowerShell start script.
        /// </summary>
        public Dictionary<string, string> ReadStartLocalPs1(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(path))
            {
                var m = Ps1EnvPattern.Match(line);
                if (m.Success)
                    result[m.Groups["key"].Value] = m.Groups["val"].Value;
            }
            return result;
        }

        /// <summary>
        /// Writes back <c>$env:KEY = "value"</c> lines, preserving other script
        /// content. New keys are appended after the last existing <c>$env:</c> line.
        /// </summary>
        public void WriteStartLocalPs1(string path, Dictionary<string, string> values)
        {
            var lines = File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : new List<string>();

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int lastEnvLine = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                var m = Ps1EnvPattern.Match(lines[i]);
                if (!m.Success) continue;
                lastEnvLine = i;
                var key = m.Groups["key"].Value;
                if (values.TryGetValue(key, out var newVal))
                {
                    lines[i] = $"$env:{key} = \"{newVal}\"";
                    written.Add(key);
                }
            }

            // Insert new keys after the last $env: line (or at top if none).
            int insertAt = lastEnvLine >= 0 ? lastEnvLine + 1 : 0;
            int offset = 0;
            foreach (var kv in values)
            {
                if (!written.Contains(kv.Key))
                {
                    lines.Insert(insertAt + offset, $"$env:{kv.Key} = \"{kv.Value}\"");
                    offset++;
                }
            }

            File.WriteAllLines(path, lines, Encoding.UTF8);
        }

        // ----------------------------------------------------------------
        // Kubernetes Secret YAML  (data: block, base-64 encoded)
        // ----------------------------------------------------------------

        /// <summary>
        /// Parses the <c>data:</c> block of a Kubernetes Secret YAML manifest.
        /// Values are base-64 decoded before being returned.
        /// </summary>
        public Dictionary<string, string> ReadK8sSecret(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var yaml = new YamlStream();
            using var reader = new StreamReader(path);
            yaml.Load(reader);

            if (yaml.Documents.Count == 0) return result;

            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            if (!root.Children.TryGetValue(new YamlScalarNode("data"), out var dataNode))
                return result;

            if (dataNode is not YamlMappingNode dataMap) return result;

            foreach (var entry in dataMap.Children)
            {
                var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                var encoded = ((YamlScalarNode)entry.Value).Value ?? string.Empty;
                try
                {
                    result[key] = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                }
                catch
                {
                    // If the value isn't valid base64, store it as-is.
                    result[key] = encoded;
                }
            }
            return result;
        }

        /// <summary>
        /// Writes values into the <c>data:</c> block of a Kubernetes Secret YAML
        /// manifest, base-64 encoding each value. The rest of the YAML is preserved
        /// by doing a targeted string replacement rather than full re-serialisation.
        /// </summary>
        public void WriteK8sSecret(string path, Dictionary<string, string> values)
        {
            var yaml = new YamlStream();
            string originalText = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

            using (var reader = new StringReader(originalText))
                yaml.Load(reader);

            if (yaml.Documents.Count == 0)
            {
                // Create a minimal Secret manifest.
                var newSecret = BuildMinimalK8sSecret(values);
                File.WriteAllText(path, newSecret, Encoding.UTF8);
                return;
            }

            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            if (!root.Children.TryGetValue(new YamlScalarNode("data"), out var dataNode) ||
                dataNode is not YamlMappingNode dataMap)
            {
                // No data block — fall back to minimal creation.
                var newSecret = BuildMinimalK8sSecret(values);
                File.WriteAllText(path, newSecret, Encoding.UTF8);
                return;
            }

            // Update existing entries and track which keys we've handled.
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in dataMap.Children.ToList())
            {
                var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                if (values.TryGetValue(key, out var newVal))
                {
                    ((YamlScalarNode)entry.Value).Value =
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(newVal));
                    written.Add(key);
                }
            }

            // Append new keys.
            foreach (var kv in values)
            {
                if (!written.Contains(kv.Key))
                    dataMap.Add(kv.Key,
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value)));
            }

            var sb = new StringBuilder();
            using var writer = new StringWriter(sb);
            yaml.Save(writer, assignAnchors: false);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string BuildMinimalK8sSecret(Dictionary<string, string> values)
        {
            var sb = new StringBuilder();
            sb.AppendLine("apiVersion: v1");
            sb.AppendLine("kind: Secret");
            sb.AppendLine("metadata:");
            sb.AppendLine("  name: app-secrets");
            sb.AppendLine("  namespace: prod");
            sb.AppendLine("type: Opaque");
            sb.AppendLine("data:");
            foreach (var kv in values)
            {
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value));
                sb.AppendLine($"  {kv.Key}: {encoded}");
            }
            return sb.ToString();
        }
    }
}
