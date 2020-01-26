using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace duplicate_index_finder
{
    class IndexPairComparisonInfo
    {

        public string PkIndex { get; set; }
        public bool HasPkIndex
        {
            get
            {
                return PkIndex != null;
            }
        }
        public string ClusteredIndex { get; set; }
        public bool HasClusteredIndex
        {
            get
            {
                return ClusteredIndex != null;
            }
        }
        public bool HasClusteredUniqueIndex
        {
            get
            {
                return HasClusteredIndex && ClusteredIndex == UniqueIndex;
            }
        }
        public bool HasClusteredPkIndex
        {
            get
            {
                return PkIndex != null && PkIndex == ClusteredIndex;
            }
        }
        public string UniqueIndex { get; set; }
        public bool HasUniqueIndex
        {
            get
            {
                return UniqueIndex != null;
            }
        }

        public bool HasSameIndexedColumnNames { get; set; }
        public bool HasOrderedProperSubsetOfIndexedColumns
        {
            get
            {
                return IndexWithOrderedProperSubsetOfIndexedColumns != null;
            }
        }
        public string IndexWithOrderedProperSubsetOfIndexedColumns { get; set; }
        public bool HasSameIndexedColumnOrder { get; set; }
        public bool HasSameIndexedColumnDescending { get; set; }

        public bool HasSameIncludedColumns { get; set; }
        public string IndexWithProperSubsetOfIncludedColumns { get; set; }
        public bool HasProperSubsetOfIncludedColumns
        {
            get
            {
                return IndexWithProperSubsetOfIncludedColumns != null;
            }
        }
        public bool HasSameFilter { get; set; }

        // Whether or not the indexed columns of the two indexes are identical.
        public bool HasIdenticalIndexedColumns
        {
            get
            {
                return HasSameIndexedColumnNames
                    && HasSameIndexedColumnOrder
                    && HasSameIndexedColumnDescending;
            }
        }
        public bool PkIndexHasMoreColumns { get; set; }
        public bool UniqueIndexHasMoreColumns { get; set; }
        public bool ClusteredIndexHasMoreColumns { get; set; }

        public IndexEquivalency Equivalency { get; set; }
        public string AnalysisSummary { get; set; }
    }

    enum IndexEquivalency {
        Unknown,
        Overlap,
        CanBeMerged,
        CanPossiblyBeMerged
    }
}
