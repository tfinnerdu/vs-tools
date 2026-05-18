using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibGit2Sharp;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace DoaneDevTools.Editor
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(GitBlameMargin.MarginName)]
    [Order(After = PredefinedMarginNames.LineNumber, Before = PredefinedMarginNames.Spacer)]
    [MarginContainer(PredefinedMarginNames.Left)]
    [ContentType("CSharp")]
    [ContentType("Basic")]
    [ContentType("Python")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class GitBlameMarginProvider : IWpfTextViewMarginProvider
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService { get; set; } = null!;

        public IWpfTextViewMargin? CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin marginContainer)
        {
            if (!DoaneDevToolsPackage.Options.ShowGitBlameMargin) return null;
            if (!TextDocumentFactoryService.TryGetTextDocument(textViewHost.TextView.TextBuffer, out var doc))
                return null;
            return new GitBlameMargin(textViewHost.TextView, doc.FilePath);
        }
    }

    internal sealed class GitBlameMargin : Canvas, IWpfTextViewMargin
    {
        internal const string MarginName = "DoaneGitBlame";
        private const double MarginWidth = 220;

        private readonly IWpfTextView _view;
        private readonly string _filePath;
        private Dictionary<int, (string Author, string Date)> _blameLines = new();
        private bool _isDisposed;

        private static readonly FontFamily MonoFont = new FontFamily("Consolas");
        private static readonly Brush AuthorBrush = new SolidColorBrush(Color.FromRgb(0x85, 0x99, 0x00));
        private static readonly Brush DateBrush   = new SolidColorBrush(Color.FromRgb(0x65, 0x8B, 0xD0));
        private static readonly Brush BgBrush     = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));

        public GitBlameMargin(IWpfTextView view, string filePath)
        {
            _view     = view;
            _filePath = filePath;
            Background = BgBrush;
            Width = MarginWidth;
            ClipToBounds = true;

            LoadBlame();

            _view.LayoutChanged        += OnLayoutChanged;
            _view.ZoomLevelChanged     += OnZoomChanged;
        }

        private void LoadBlame()
        {
            _blameLines.Clear();
            try
            {
                var repoPath = FindRepoRoot(_filePath);
                if (repoPath == null) return;

                using var repo = new Repository(repoPath);
                var blame = repo.Blame(_filePath.Substring(repoPath.Length).TrimStart('/', '\\').Replace('\\', '/'));
                int line = 1;
                foreach (var hunk in blame)
                {
                    for (int i = 0; i < hunk.LineCount; i++, line++)
                    {
                        var sig  = hunk.FinalCommit?.Author;
                        var name = sig?.Name?.Split(' ')[0] ?? "?";
                        var date = sig?.When.ToString("MM/dd yy") ?? "";
                        _blameLines[line] = (name, date);
                    }
                }
            }
            catch { /* git repo unavailable or file untracked */ }
        }

        private static string? FindRepoRoot(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) => Render();
        private void OnZoomChanged(object sender, ZoomLevelChangedEventArgs e)         => Render();

        private void Render()
        {
            Children.Clear();
            if (_isDisposed || _blameLines.Count == 0) return;

            double scale = _view.ZoomLevel / 100.0;
            double fontSize = 11 * scale;

            foreach (ITextViewLine line in _view.TextViewLines)
            {
                if (!line.IsValid) continue;
                int lineNum = _view.TextSnapshot.GetLineNumberFromPosition(line.Start.Position) + 1;
                if (!_blameLines.TryGetValue(lineNum, out var blame)) continue;

                double top = line.TextTop - _view.ViewportTop;

                var tb = new TextBlock
                {
                    FontFamily  = MonoFont,
                    FontSize    = fontSize,
                    Height      = line.Height,
                    Width       = MarginWidth,
                    Padding     = new Thickness(2, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                tb.Inlines.Add(new System.Windows.Documents.Run(blame.Author.PadRight(10, ' ').Substring(0, Math.Min(10, blame.Author.Length)).PadRight(10))
                    { Foreground = AuthorBrush, FontSize = fontSize });
                tb.Inlines.Add(new System.Windows.Documents.Run(" " + blame.Date)
                    { Foreground = DateBrush, FontSize = fontSize });

                SetTop(tb, top);
                SetLeft(tb, 0);
                Children.Add(tb);
            }
        }

        // IWpfTextViewMargin
        public FrameworkElement VisualElement => this;
        public double MarginSize => MarginWidth;
        public bool Enabled => true;

        public ITextViewMargin? GetTextViewMargin(string marginName)
            => marginName == MarginName ? this : null;

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _view.LayoutChanged    -= OnLayoutChanged;
            _view.ZoomLevelChanged -= OnZoomChanged;
        }
    }
}
