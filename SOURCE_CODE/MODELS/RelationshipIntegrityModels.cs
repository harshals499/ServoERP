using System.Collections.Generic;
using System.Linq;

namespace HVAC_Pro_Desktop.Models
{
    public sealed class RelationshipHealthRow
    {
        public string Module { get; set; }
        public string RelationshipName { get; set; }
        public string ChildTable { get; set; }
        public string ChildColumn { get; set; }
        public string ParentTable { get; set; }
        public string ParentColumn { get; set; }
        public int ChildRows { get; set; }
        public int PopulatedKeys { get; set; }
        public int MatchedKeys { get; set; }
        public int OrphanKeys { get; set; }
        public int ContextViolations { get; set; }
        public bool IsForeignKeyEnforced { get; set; }
        public bool RequiresContextProtection { get; set; }
        public bool IsContextProtectionEnforced { get; set; }
        public string EnforcementStatus { get; set; }
        public string Severity { get; set; }
        public string Recommendation { get; set; }

        public decimal CoveragePercent => ChildRows == 0 ? 100m : decimal.Round(PopulatedKeys * 100m / ChildRows, 2);
        public decimal IntegrityPercent => PopulatedKeys == 0 ? 100m : decimal.Round(MatchedKeys * 100m / PopulatedKeys, 2);
        public decimal ContextConsistencyPercent => PopulatedKeys == 0 ? 100m : decimal.Round((PopulatedKeys - ContextViolations) * 100m / PopulatedKeys, 2);
        public bool IsFullyProtected => RequiresContextProtection ? IsContextProtectionEnforced : IsForeignKeyEnforced;
        public string ConnectionStatus => IsFullyProtected
            ? "Connected"
            : OrphanKeys > 0 || ContextViolations > 0 ? "Needs data review" : "Pending auto-connect";
    }

    public sealed class RelationshipHealthSnapshot
    {
        public List<RelationshipHealthRow> Rows { get; set; } = new List<RelationshipHealthRow>();
        public int RelationshipsChecked => Rows.Count;
        public int MissingForeignKeys => Rows.Count(row => !row.IsForeignKeyEnforced);
        public int ConnectedRelationships => Rows.Count(row => row.IsFullyProtected);
        public int MissingProtections => Rows.Count(row => !row.IsFullyProtected);
        public int RelationshipsNeedingReview => Rows.Count(row => !row.IsFullyProtected || row.OrphanKeys > 0 || row.ContextViolations > 0);
        public int OrphanKeys => Rows.Sum(row => row.OrphanKeys);
        public int ContextViolations => Rows.Sum(row => row.ContextViolations);
        public int PopulatedKeys => Rows.Sum(row => row.PopulatedKeys);
        public int MatchedKeys => Rows.Sum(row => row.MatchedKeys);
        public decimal WeightedIntegrityPercent => PopulatedKeys == 0 ? 100m : decimal.Round(MatchedKeys * 100m / PopulatedKeys, 2);
        public decimal ProtectionPercent => RelationshipsChecked == 0 ? 100m : decimal.Round(ConnectedRelationships * 100m / RelationshipsChecked, 2);
    }
}
