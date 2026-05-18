using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DoaneDevTools.ToolWindows.EnvManager.Models
{
    /// <summary>
    /// The type of configuration file a <see cref="ProjectFileNode"/> represents.
    /// </summary>
    public enum ConfigFileType
    {
        /// <summary>A directory / project folder node (not a file).</summary>
        Folder,

        /// <summary>Python-style <c>.env</c> file (<c>KEY=VALUE</c> lines).</summary>
        DotEnv,

        /// <summary>ASP.NET Core <c>appsettings*.json</c> file.</summary>
        AppSettings,

        /// <summary>PowerShell <c>start-local.ps1</c> dev-runner script.</summary>
        StartLocalPs1,

        /// <summary>Kubernetes <c>Secret</c> YAML manifest.</summary>
        K8sSecret,

        /// <summary>Docker Compose <c>docker-compose*.yml</c> file.</summary>
        DockerCompose
    }

    /// <summary>
    /// Node in the Projects &amp; Files TreeView.  Folder nodes have
    /// <see cref="Children"/> populated; file nodes have <see cref="FullPath"/> set.
    /// </summary>
    public class ProjectFileNode : INotifyPropertyChanged
    {
        private bool _isLinked;

        /// <summary>Display name shown in the TreeView.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Absolute path to the file. Empty/null for folder nodes.
        /// </summary>
        public string? FullPath { get; set; }

        /// <summary>
        /// The kind of configuration file this node represents.
        /// </summary>
        public ConfigFileType FileType { get; set; } = ConfigFileType.Folder;

        /// <summary>
        /// True when this file has been associated with the active profile
        /// (i.e., the profile's variables will be written to it on Apply).
        /// </summary>
        public bool IsLinked
        {
            get => _isLinked;
            set
            {
                if (_isLinked == value) return;
                _isLinked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LinkStatus));
            }
        }

        /// <summary>Text label shown in the "linked" column of the TreeView.</summary>
        public string LinkStatus => IsLinked ? "✓ linked" : string.Empty;

        /// <summary>Child nodes (populated for folder/project nodes).</summary>
        public List<ProjectFileNode> Children { get; set; } = new List<ProjectFileNode>();

        public bool IsFolder => FileType == ConfigFileType.Folder;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => Name;
    }
}
