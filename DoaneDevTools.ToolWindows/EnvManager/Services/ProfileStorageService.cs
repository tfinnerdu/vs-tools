using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DoaneDevTools.ToolWindows.EnvManager.Models;

namespace DoaneDevTools.ToolWindows.EnvManager.Services
{
    /// <summary>
    /// Persists <see cref="EnvProfile"/> collections to
    /// <c>devtools.profiles.json</c> alongside the solution file.
    ///
    /// Secret values are encrypted using Windows DPAPI
    /// (<see cref="ProtectedData"/> with <c>DataProtectionScope.CurrentUser</c>)
    /// when running on Windows.  On non-Windows platforms a plaintext fallback
    /// is used and a warning is included in the stored JSON.
    /// </summary>
    public class ProfileStorageService
    {
        private const string ProfilesFileName = "devtools.profiles.json";
        private const string SchemaFileName = "devtools.profiles.schema.json";

        private readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        // ----------------------------------------------------------------
        // Load / Save
        // ----------------------------------------------------------------

        /// <summary>
        /// Loads profiles from <c>devtools.profiles.json</c> in the same
        /// directory as <paramref name="solutionFilePath"/>.
        /// Returns an empty list if the file does not exist.
        /// </summary>
        public List<EnvProfile> LoadProfiles(string solutionFilePath)
        {
            var dir = Path.GetDirectoryName(solutionFilePath) ?? Directory.GetCurrentDirectory();
            var profilesPath = Path.Combine(dir, ProfilesFileName);

            if (!File.Exists(profilesPath))
                return new List<EnvProfile>();

            var json = File.ReadAllText(profilesPath);
            var dto = JsonSerializer.Deserialize<ProfilesFileDto>(json, JsonOptions);

            if (dto?.Profiles == null)
                return new List<EnvProfile>();

            return dto.Profiles.Select(p => new EnvProfile
            {
                Name = p.Name ?? string.Empty,
                Variables = (p.Variables ?? new List<EnvVariableDto>()).Select(v =>
                {
                    var variable = new EnvVariable
                    {
                        Key = v.Key ?? string.Empty,
                        IsSecret = v.IsSecret
                    };
                    variable.Value = v.IsSecret && v.EncryptedValue != null
                        ? Decrypt(v.EncryptedValue)
                        : v.PlainValue ?? string.Empty;
                    return variable;
                }).ToList()
            }).ToList();
        }

        /// <summary>
        /// Saves <paramref name="profiles"/> to <c>devtools.profiles.json</c>
        /// next to <paramref name="solutionFilePath"/>.
        /// </summary>
        public void SaveProfiles(string solutionFilePath, IEnumerable<EnvProfile> profiles)
        {
            var dir = Path.GetDirectoryName(solutionFilePath) ?? Directory.GetCurrentDirectory();
            var profilesPath = Path.Combine(dir, ProfilesFileName);

            var dto = new ProfilesFileDto
            {
                SchemaVersion = "1.0",
                Encryption = _isWindows ? "dpapi-currentuser" : "plaintext-warning",
                Profiles = profiles.Select(p => new EnvProfileDto
                {
                    Name = p.Name,
                    Variables = p.Variables.Select(v => new EnvVariableDto
                    {
                        Key = v.Key,
                        IsSecret = v.IsSecret,
                        EncryptedValue = v.IsSecret && _isWindows ? Encrypt(v.Value) : null,
                        PlainValue = !v.IsSecret || !_isWindows ? v.Value : null
                    }).ToList()
                }).ToList()
            };

            var json = JsonSerializer.Serialize(dto, JsonOptions);
            File.WriteAllText(profilesPath, json, Encoding.UTF8);
        }

        /// <summary>
        /// Exports a JSON Schema document describing the variable keys defined
        /// across all profiles.  The schema file is used by IDE language servers
        /// for .env validation.
        /// </summary>
        public void ExportSchema(string solutionFilePath, IEnumerable<EnvProfile> profiles)
        {
            var dir = Path.GetDirectoryName(solutionFilePath) ?? Directory.GetCurrentDirectory();
            var schemaPath = Path.Combine(dir, SchemaFileName);

            // Collect all distinct keys across all profiles.
            var allKeys = profiles
                .SelectMany(p => p.Variables)
                .Select(v => v.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k)
                .ToList();

            var properties = new Dictionary<string, object>();
            foreach (var key in allKeys)
            {
                properties[key] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = $"Environment variable: {key}"
                };
            }

            var schema = new Dictionary<string, object>
            {
                ["$schema"] = "http://json-schema.org/draft-07/schema#",
                ["title"] = "DoaneDevTools Environment Schema",
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = new Dictionary<string, object> { ["type"] = "string" }
            };

            var json = JsonSerializer.Serialize(schema, JsonOptions);
            File.WriteAllText(schemaPath, json, Encoding.UTF8);
        }

        // ----------------------------------------------------------------
        // Encryption helpers (DPAPI on Windows, plaintext elsewhere)
        // ----------------------------------------------------------------

        private string Encrypt(string plaintext)
        {
            if (!_isWindows) return plaintext;
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private string Decrypt(string ciphertext)
        {
            if (!_isWindows) return ciphertext;
            try
            {
                var encrypted = Convert.FromBase64String(ciphertext);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                // If decryption fails (e.g., loaded on a different machine),
                // return the raw value so the user can correct it.
                return ciphertext;
            }
        }

        // ----------------------------------------------------------------
        // JSON serialisation options and DTOs
        // ----------------------------------------------------------------

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private class ProfilesFileDto
        {
            public string? SchemaVersion { get; set; }
            public string? Encryption { get; set; }
            public List<EnvProfileDto>? Profiles { get; set; }
        }

        private class EnvProfileDto
        {
            public string? Name { get; set; }
            public List<EnvVariableDto>? Variables { get; set; }
        }

        private class EnvVariableDto
        {
            public string? Key { get; set; }
            public bool IsSecret { get; set; }
            /// <summary>Set for secret values when DPAPI is available.</summary>
            public string? EncryptedValue { get; set; }
            /// <summary>Set for non-secret values (or when DPAPI is unavailable).</summary>
            public string? PlainValue { get; set; }
        }
    }
}
