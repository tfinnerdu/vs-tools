using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace DoaneDevTools.Editor
{
    // ── Classification type definitions ─────────────────────────────────────

    internal static class HotspotClassificationTypes
    {
        internal const string Warning  = "DoaneHotspotWarning";
        internal const string Critical = "DoaneHotspotCritical";

        [Export, Name(Warning)]
        [BaseDefinition("identifier")]
        internal static ClassificationTypeDefinition? WarningType;

        [Export, Name(Critical)]
        [BaseDefinition("identifier")]
        internal static ClassificationTypeDefinition? CriticalType;
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = HotspotClassificationTypes.Warning)]
    [Name(HotspotClassificationTypes.Warning)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class HotspotWarningFormat : ClassificationFormatDefinition
    {
        internal HotspotWarningFormat()
        {
            DisplayName = "Doane Hotspot Warning (CC > 10)";
            BackgroundColor = System.Windows.Media.Color.FromArgb(40, 255, 200, 0);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = HotspotClassificationTypes.Critical)]
    [Name(HotspotClassificationTypes.Critical)]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class HotspotCriticalFormat : ClassificationFormatDefinition
    {
        internal HotspotCriticalFormat()
        {
            DisplayName = "Doane Hotspot Critical (CC > 15)";
            BackgroundColor = System.Windows.Media.Color.FromArgb(50, 255, 80, 60);
        }
    }

    // ── Tagger provider ──────────────────────────────────────────────────────

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IClassificationTag))]
    [ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class PerformanceHotspotTaggerProvider : ITaggerProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; } = null!;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return (ITagger<T>)(object)buffer.Properties.GetOrCreateSingletonProperty(
                () => new PerformanceHotspotTagger(buffer, ClassificationRegistry));
        }
    }

    // ── Tagger ───────────────────────────────────────────────────────────────

    internal sealed class PerformanceHotspotTagger : ITagger<IClassificationTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly IClassificationType _warningType;
        private readonly IClassificationType _criticalType;

        private List<(SnapshotSpan Span, IClassificationType Type)> _tags = new();
        private CancellationTokenSource _cts = new();
        private Timer? _debounce;

        private static readonly int[] BranchTokenKinds = {
            (int)SyntaxKind.IfKeyword, (int)SyntaxKind.WhileKeyword,
            (int)SyntaxKind.ForKeyword, (int)SyntaxKind.ForEachKeyword,
            (int)SyntaxKind.CaseKeyword, (int)SyntaxKind.CatchKeyword,
            (int)SyntaxKind.ConditionalExpression,
            (int)SyntaxKind.AmpersandAmpersandToken, (int)SyntaxKind.BarBarToken,
            (int)SyntaxKind.QuestionQuestionToken,
            (int)SyntaxKind.SwitchExpressionArm,
        };

        public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

        public PerformanceHotspotTagger(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer       = buffer;
            _warningType  = registry.GetClassificationType(HotspotClassificationTypes.Warning);
            _criticalType = registry.GetClassificationType(HotspotClassificationTypes.Critical);

            buffer.Changed += OnBufferChanged;
            ScheduleAnalysis();
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
            => ScheduleAnalysis();

        private void ScheduleAnalysis()
        {
            _debounce?.Dispose();
            _debounce = new Timer(_ => _ = AnalyzeAsync(), null, 800, Timeout.Infinite);
        }

        private async Task AnalyzeAsync()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                var snapshot = _buffer.CurrentSnapshot;
                var text     = snapshot.GetText();
                var tree     = await Task.Run(() => CSharpSyntaxTree.ParseText(text, cancellationToken: ct), ct);
                var root     = await tree.GetRootAsync(ct);

                var newTags = new List<(SnapshotSpan, IClassificationType)>();

                foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (ct.IsCancellationRequested) return;

                    int cc = ComputeCC(method);
                    var opts = DoaneDevToolsPackage.Options;
                    if (!opts.HighlightHotspots || cc < opts.HotspotWarningThreshold) continue;

                    var nameToken = method.Identifier;
                    int start = nameToken.SpanStart;
                    int end   = nameToken.Span.End;
                    if (start < 0 || end > snapshot.Length) continue;

                    var span = new SnapshotSpan(snapshot, start, end - start);
                    newTags.Add((span, cc >= opts.HotspotCriticalThreshold ? _criticalType : _warningType));
                }

                if (ct.IsCancellationRequested) return;
                _tags = newTags;

                if (TagsChanged != null)
                {
                    var full = new SnapshotSpan(snapshot, 0, snapshot.Length);
                    TagsChanged(this, new SnapshotSpanEventArgs(full));
                }
            }
            catch (OperationCanceledException) { }
            catch { /* swallow parse errors */ }
        }

        private static int ComputeCC(MethodDeclarationSyntax method)
        {
            int cc = 1;
            foreach (var token in method.DescendantTokens())
            {
                switch (token.Kind())
                {
                    case SyntaxKind.IfKeyword:
                    case SyntaxKind.WhileKeyword:
                    case SyntaxKind.ForKeyword:
                    case SyntaxKind.ForEachKeyword:
                    case SyntaxKind.CaseKeyword:
                    case SyntaxKind.CatchKeyword:
                    case SyntaxKind.AmpersandAmpersandToken:
                    case SyntaxKind.BarBarToken:
                    case SyntaxKind.QuestionQuestionToken:
                        cc++;
                        break;
                }
            }
            foreach (var _ in method.DescendantNodes().OfType<SwitchExpressionArmSyntax>())
                cc++;
            foreach (var cond in method.DescendantNodes().OfType<ConditionalExpressionSyntax>())
                cc++;
            return cc;
        }

        public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0) yield break;
            var snap = spans[0].Snapshot;
            foreach (var (tagSpan, type) in _tags)
            {
                var mapped = tagSpan.TranslateTo(snap, SpanTrackingMode.EdgeExclusive);
                if (spans.IntersectsWith(new NormalizedSnapshotSpanCollection(mapped)))
                    yield return new TagSpan<IClassificationTag>(mapped, new ClassificationTag(type));
            }
        }
    }
}
