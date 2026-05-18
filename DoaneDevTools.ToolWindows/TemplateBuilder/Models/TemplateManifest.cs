using System.Collections.Generic;

namespace DoaneDevTools.ToolWindows.TemplateBuilder.Models
{
    /// <summary>
    /// Describes a scaffolding template loaded from a <c>template.json</c> manifest.
    /// </summary>
    public class TemplateManifest
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<TemplateVariable> Variables { get; set; } = new List<TemplateVariable>();
        public List<string> OutputFiles { get; set; } = new List<string>();
        /// <summary>Absolute path to the directory that contains the template files.</summary>
        public string TemplateDirectory { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    /// <summary>
    /// A single variable slot within a <see cref="TemplateManifest"/>.
    /// </summary>
    public class TemplateVariable
    {
        public string Name { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        /// <summary>"string" | "int" | "bool"</summary>
        public string Type { get; set; } = "string";
        public object? Default { get; set; }

        public override string ToString() => $"{Name} ({Type})";
    }

    // -----------------------------------------------------------------------
    // Tree-view node types used by the TemplateBuilder UI
    // -----------------------------------------------------------------------

    /// <summary>
    /// Base node for the template-category TreeView.
    /// </summary>
    public abstract class TemplateTreeNode
    {
        public string Name { get; set; } = string.Empty;
        public bool IsCategory { get; protected set; }
    }

    /// <summary>
    /// Category node (e.g., "Python/Flask").  Contains child <see cref="TemplateLeafNode"/> instances.
    /// </summary>
    public class TemplateCategoryNode : TemplateTreeNode
    {
        public TemplateCategoryNode() => IsCategory = true;
        public List<TemplateTreeNode> Templates { get; set; } = new List<TemplateTreeNode>();
    }

    /// <summary>
    /// Leaf node representing a single concrete template.
    /// </summary>
    public class TemplateLeafNode : TemplateTreeNode
    {
        public TemplateLeafNode() => IsCategory = false;
        public TemplateManifest Manifest { get; set; } = new TemplateManifest();
        /// <summary>Templates have no children in the TreeView.</summary>
        public List<TemplateTreeNode> Templates { get; } = new List<TemplateTreeNode>();
    }

    // -----------------------------------------------------------------------
    // ViewModel-side variable input item
    // -----------------------------------------------------------------------

    /// <summary>
    /// Wraps a <see cref="TemplateVariable"/> with bindable input values for the
    /// DataGrid in the Template Builder.
    /// </summary>
    public class TemplateVariableInputItem : Infrastructure.ViewModelBase
    {
        private string _stringValue = string.Empty;
        private bool   _boolValue;

        public TemplateVariable Variable { get; }

        public TemplateVariableInputItem(TemplateVariable variable)
        {
            Variable = variable;

            // Initialise from default value.
            if (variable.Default != null)
            {
                var raw = variable.Default.ToString() ?? string.Empty;
                if (variable.Type == "bool")
                    _boolValue = raw.Equals("true", System.StringComparison.OrdinalIgnoreCase);
                else
                    _stringValue = raw;
            }
        }

        public string Name   => Variable.Name;
        public string Prompt => Variable.Prompt;
        public bool   IsBool => string.Equals(Variable.Type, "bool", System.StringComparison.OrdinalIgnoreCase);
        public bool   IsText => !IsBool;

        public string StringValue
        {
            get => _stringValue;
            set => SetProperty(ref _stringValue, value);
        }

        public bool BoolValue
        {
            get => _boolValue;
            set => SetProperty(ref _boolValue, value);
        }

        /// <summary>Returns the current value as a plain string for template substitution.</summary>
        public string ResolvedValue => IsBool ? (_boolValue ? "true" : "false") : _stringValue;
    }
}
