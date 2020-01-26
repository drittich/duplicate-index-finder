using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace duplicate_index_finder
{
    class IndexComparisonInfo
    {
        // Whether or not this index contains all the indexed columns in the other index (in any order).
        public bool ContainsAllIndexedColumns { get; set; }

        // Whether or not this index contains all the included columns in the other index.
        public bool ContainsAllIncludedColumns { get; set; }

        // Whether or not the included columns in this index are in the same order in the other index.
        public bool IndexedColumnOrderMatches { get; set; }

        // For all the columns in this one that exist in the other index,
        // the IsDescending value matches.
        public bool IsDescendingMatches { get; set; }

        // Whether or not this index contains additional indexed columns compare to the other index.
        public bool ContainsAdditionalIndexedColumns { get; set; }

        // Whether or not this index contains additional included columns compare to the other index.
        public bool ContainsAdditionalIncludedColumns { get; set; }
    }
}
