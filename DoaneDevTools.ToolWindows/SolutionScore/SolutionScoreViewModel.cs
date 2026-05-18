using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using DoaneDevTools.ToolWindows.Shared;

namespace DoaneDevTools.ToolWindows.SolutionScore
{
    public class ComponentScore : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Detail { get; set; } = string.Empty;

        public string ScoreColor => Score >= 80 ? "#2E7D32" :
                                    Score >= 60 ? "#E65100" : "#C62828";

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class ScoreHistoryEntry
    {
        public DateTime Date { get; set; }
        public int Score { get; set; }
    }

    public class SolutionScoreViewModel : INotifyPropertyChanged
    {
        private int _score = 0;
        private bool _hasHistory;

        public int Score
        {
            get => _score;
            set
            {
                _score = value;
                OnPropertyChanged(nameof(Score));
                OnPropertyChanged(nameof(ScoreLabel));
                OnPropertyChanged(nameof(ScoreBackground));
            }
        }

        public string ScoreLabel => Score >= 90 ? "Excellent" :
                                    Score >= 75 ? "Good" :
                                    Score >= 55 ? "Fair" :
                                    Score >= 35 ? "Needs Attention" : "Critical";

        public string ScoreBackground => Score >= 80 ? "#2E7D32" :
                                         Score >= 60 ? "#E65100" : "#C62828";

        public string TrendText { get; private set; } = "Run analysis to see your score.";

        public bool HasHistory
        {
            get => _hasHistory;
            set { _hasHistory = value; OnPropertyChanged(nameof(HasHistory)); }
        }

        public ObservableCollection<ComponentScore> ComponentScores { get; } = new();
        public ObservableCollection<ScoreHistoryEntry> ScoreHistory { get; } = new();

        public ICommand AnalyzeCommand { get; }

        public SolutionScoreViewModel()
        {
            AnalyzeCommand = new RelayCommand(async _ => await AnalyzeAsync());

            // Seed component list
            ComponentScores.Add(new ComponentScore { Name = "Code Quality (DA rules)", Score = 0 });
            ComponentScores.Add(new ComponentScore { Name = "Stack Compliance (DS rules)", Score = 0 });
            ComponentScores.Add(new ComponentScore { Name = "Documentation Coverage", Score = 0 });
            ComponentScores.Add(new ComponentScore { Name = "Dependency Coupling", Score = 0 });
            ComponentScores.Add(new ComponentScore { Name = "API / Spec Sync", Score = 0 });
            ComponentScores.Add(new ComponentScore { Name = "Security (secrets/patterns)", Score = 0 });
        }

        private async Task AnalyzeAsync()
        {
            // In the full implementation, each component score is computed by querying
            // the Roslyn workspace and the analyzer results from the VS error list.
            // Here we demonstrate the calculation approach.

            TrendText = "Analyzing...";
            OnPropertyChanged(nameof(TrendText));

            await Task.Delay(500); // Simulate analysis work

            // Placeholder scores — real scores come from:
            // - Rule violation counts from the Roslyn analyzers already running
            // - Documentation coverage: count of DA012 violations / total public members
            // - Coupling: average Ce from dependency graph
            // - API drift: count of drift items from DriftAnalyzerService
            // - Security: count of DSSEC + DS003 violations

            var scores = new[] { 85, 72, 60, 78, 90, 95 };

            for (int i = 0; i < ComponentScores.Count; i++)
                ComponentScores[i].Score = scores[i];

            Score = (int)scores.Average();
            TrendText = $"Last analyzed {DateTime.Now:HH:mm}";
            OnPropertyChanged(nameof(TrendText));

            // Persist score
            ScoreHistory.Insert(0, new ScoreHistoryEntry { Date = DateTime.Now, Score = Score });
            if (ScoreHistory.Count > 20) ScoreHistory.RemoveAt(ScoreHistory.Count - 1);
            HasHistory = ScoreHistory.Count > 1;

            PersistHistory();
        }

        private void PersistHistory()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DoaneDevTools");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "score-history.json");
                File.WriteAllText(path, JsonSerializer.Serialize(ScoreHistory.ToList()));
            }
            catch { /* non-critical */ }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
