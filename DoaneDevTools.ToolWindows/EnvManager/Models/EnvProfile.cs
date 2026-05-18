using System.Collections.Generic;
using System.Linq;

namespace DoaneDevTools.ToolWindows.EnvManager.Models
{
    /// <summary>
    /// A named collection of environment variables (e.g., DEV, QA, PROD).
    /// Profiles are persisted to <c>devtools.profiles.json</c> via
    /// <see cref="Services.ProfileStorageService"/>.
    /// </summary>
    public class EnvProfile
    {
        public string Name { get; set; } = string.Empty;

        public List<EnvVariable> Variables { get; set; } = new List<EnvVariable>();

        /// <summary>
        /// Returns a deep copy of this profile with a new name.
        /// </summary>
        public EnvProfile Duplicate(string newName) => new EnvProfile
        {
            Name = newName,
            Variables = Variables.Select(v => v.Clone()).ToList()
        };

        /// <summary>
        /// Looks up a variable by key (case-insensitive).
        /// </summary>
        public EnvVariable? GetVariable(string key) =>
            Variables.FirstOrDefault(v =>
                string.Equals(v.Key, key, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns a flat dictionary of all key→value pairs.
        /// </summary>
        public Dictionary<string, string> ToDictionary() =>
            Variables.ToDictionary(v => v.Key, v => v.Value);

        public override string ToString() => $"{Name} ({Variables.Count} variables)";
    }
}
