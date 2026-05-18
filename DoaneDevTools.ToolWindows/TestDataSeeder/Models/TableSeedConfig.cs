using System.Collections.Generic;
using System.ComponentModel;

namespace DoaneDevTools.ToolWindows.TestDataSeeder.Models
{
    public enum SeedStrategy
    {
        FakerRealistic,
        FakerLinked,
        Sequence,
        Fixed,
        CopyFromProd
    }

    public class TableSeedConfig : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private int _seedCount = 100;
        private SeedStrategy _strategy = SeedStrategy.FakerRealistic;

        public string SchemaName { get; set; } = "dbo";
        public string TableName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{TableName}";
        public List<string> ForeignKeyDependencies { get; set; } = new();
        public List<ColumnInfo> Columns { get; set; } = new();

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public int SeedCount
        {
            get => _seedCount;
            set { _seedCount = value; OnPropertyChanged(nameof(SeedCount)); }
        }

        public SeedStrategy Strategy
        {
            get => _strategy;
            set { _strategy = value; OnPropertyChanged(nameof(Strategy)); }
        }

        public string StrategyDisplay => Strategy switch
        {
            SeedStrategy.FakerRealistic => "Faker (realistic)",
            SeedStrategy.FakerLinked => "Faker (linked)",
            SeedStrategy.Sequence => "Sequence",
            SeedStrategy.Fixed => "Fixed CSV",
            SeedStrategy.CopyFromProd => "Copy from prod",
            _ => "Unknown"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public string? ReferencedTable { get; set; }
        public string? ReferencedColumn { get; set; }
        public int? MaxLength { get; set; }
    }
}
