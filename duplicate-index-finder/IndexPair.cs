using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace duplicate_index_finder
{
	class IndexPair
	{
		public Index Index1 { get; set; }
		public Index Index2 { get; set; }
		public List<Index> Indexes { get; set; }
		public IndexPairComparisonInfo ComparisonInfo = new IndexPairComparisonInfo();

		public IndexPair(Index index1, Index index2)
		{
			Indexes = new List<Index> { index1, index2 };
			Index1 = index1;
			Index2 = index2;
		}

		public Index GetById(string id, bool getOther = false)
		{
			if (id == "Index1")
				return getOther ? Index2 : Index1;
			if (id == "Index2")
				return getOther ? Index1 : Index2;
			return null;
		}

		public Index GetOtherById(string id)
		{
			return GetById(id, true);
		}

		// This method needs work, only covers one unusual case for now
		// TODO: handle case where indexed columns are a subset, but include columns are different
		private string GetCanPossiblyBeMergedAnalysisSummary(IndexPairComparisonInfo ci)
		{
			// indexes that might be consolidated (pairs where, with some changes to one, the other may be dropped, but analysis is needed to be certain)
			if (ci.HasSameFilter && ci.HasOrderedProperSubsetOfIndexedColumns && ci.HasSameIndexedColumnDescending && ci.HasPkIndex && !ci.HasClusteredPkIndex && ci.HasClusteredIndex && !ci.HasUniqueIndex && ci.PkIndexHasMoreColumns)
			{
				return $"Consider making {GetById(ci.PkIndex).IndexName} clustered and removing {GetOtherById(ci.PkIndex).IndexName}";
			}
			return null;
		}

		private string GetCanBeMergedAnalysisSummary(IndexPairComparisonInfo ci)
		{
			// indexes that can be consolidated (pairs where, with minor changes to one, the other may be dropped)
			if (ci.HasSameFilter && ci.HasSameIndexedColumnNames && ci.HasSameIndexedColumnOrder && ci.HasSameIndexedColumnDescending && ci.HasSameIncludedColumns)
			{
				if (ci.HasPkIndex && !ci.HasClusteredPkIndex && ci.HasClusteredIndex)
				{
					return $"Make {GetById(ci.PkIndex).IndexName} clustered and remove {GetOtherById(ci.PkIndex).IndexName}";
				}
				else if (!ci.HasPkIndex && ci.HasClusteredIndex && ci.HasUniqueIndex)
				{
					return $"Make {GetById(ci.ClusteredIndex).IndexName} unique and remove {GetOtherById(ci.ClusteredIndex).IndexName}";
				}
			}

			if (ci.HasSameFilter && ci.HasSameIndexedColumnNames && ci.HasSameIndexedColumnOrder && ci.HasSameIndexedColumnDescending && !ci.HasSameIncludedColumns && !ci.HasProperSubsetOfIncludedColumns)
			{
				return $"Merge the included columns into one index and remove the other";
			}
			return null;
		}

		private string GetIdenticalOrOverlapAnalysisSummary(IndexPairComparisonInfo ci)
		{
			// We're not analyzing indexes with different filters for now, we'll consider them unrelated, because usually if you are using filters you are addressing
			// a performance concern.
			if (!ci.HasSameFilter)
				return null;


			// indexes that are identical (pairs where one of them can be safely dropped
			if (ci.HasSameFilter && ci.HasSameIndexedColumnNames && ci.HasSameIndexedColumnOrder && ci.HasSameIndexedColumnDescending && ci.HasSameIncludedColumns)
			{
				if (ci.HasClusteredPkIndex)
				{
					return $"Remove {GetOtherById(ci.PkIndex).IndexName}";
				}
				else if (!ci.HasPkIndex && ci.HasClusteredUniqueIndex)
				{
					return $"Remove {GetOtherById(ci.ClusteredIndex).IndexName}";
				}
				else if (!ci.HasPkIndex && ci.HasClusteredIndex && !ci.HasUniqueIndex)
				{
					return $"Remove {GetOtherById(ci.ClusteredIndex).IndexName}";
				}
				else if (!ci.HasPkIndex && !ci.HasClusteredIndex && ci.HasUniqueIndex)
				{
					return $"Remove {GetOtherById(ci.UniqueIndex).IndexName}";
				}
				else if (!ci.HasPkIndex && !ci.HasClusteredIndex && !ci.HasUniqueIndex)
				{
					return "Remove either index";
				}
			}

			if (ci.HasSameFilter && ci.HasSameIndexedColumnNames && ci.HasSameIndexedColumnOrder && ci.HasSameIndexedColumnDescending && ci.HasProperSubsetOfIncludedColumns)
			{
				if (!ci.HasPkIndex || (ci.PkIndex != ci.IndexWithProperSubsetOfIncludedColumns))
				{
					return $"Remove {GetById(ci.IndexWithProperSubsetOfIncludedColumns).IndexName}";
				}
			}

			if (ci.HasSameFilter && ci.HasOrderedProperSubsetOfIndexedColumns && ci.HasSameIndexedColumnDescending && ci.HasSameIncludedColumns && !ci.HasPkIndex && !ci.HasClusteredIndex && !ci.HasUniqueIndex)
			{

				return $"Remove {GetById(ci.IndexWithOrderedProperSubsetOfIndexedColumns).IndexName}";
			}

			if (ci.HasSameFilter && ci.HasOrderedProperSubsetOfIndexedColumns && ci.HasSameIndexedColumnDescending && !ci.HasPkIndex && !ci.HasClusteredIndex && ci.HasUniqueIndex && ci.UniqueIndexHasMoreColumns)
			{
				return $"Remove {GetOtherById(ci.UniqueIndex).IndexName}";
			}

			if (ci.HasSameFilter && ci.HasOrderedProperSubsetOfIndexedColumns && ci.HasSameIndexedColumnDescending && !ci.HasPkIndex && ci.HasClusteredIndex && !ci.HasUniqueIndex && ci.ClusteredIndexHasMoreColumns)
			{
				return $"Remove {GetOtherById(ci.ClusteredIndex).IndexName}";
			}

			return null;
		}

		/// <summary>
		/// Compare two indexes. Note that the comparison is done from the perspective of comparing the other index to this one.
		/// </summary>
		/// <param name="otherIndex"></param>
		/// <returns></returns>
		public void Compare()
		{
			CompareFirstPass();
			CompareSecondPass();
			CompareThirdPass();
		}

		// initial pass at Index level
		private void CompareFirstPass()
		{
			for (var i = 1; i <= 2; i++)
			{
				CheckContainsAllIndexedColumns(i);
				CheckContainsAdditionalIndexedColumns(i);
				CheckIndexedColumnOrderMatches(i);
				CheckIsDescendingMatches(i);
				CheckContainsAllIncludedColumns(i);
				CheckContainsAdditionalIncludedColums(i);
			}
		}

		// final pass at IndexPair level
		private void CompareSecondPass()
		{
			ComparisonInfo.ClusteredIndex = GetClusteredIndex();
			ComparisonInfo.HasSameFilter = GetHasSameFilter();
			ComparisonInfo.HasSameIncludedColumns = GetHasSameIncludedColumns();
			ComparisonInfo.HasSameIndexedColumnDescending = GetHasSameIndexedColumnDescending();
			ComparisonInfo.HasSameIndexedColumnNames = GetHasSameIndexedColumnNames();
			ComparisonInfo.HasSameIndexedColumnOrder = GetHasSameIndexedColumnOrder();
			ComparisonInfo.IndexWithOrderedProperSubsetOfIndexedColumns = GetHasOrderedProperSubsetOfIndexedColumns();
			ComparisonInfo.IndexWithProperSubsetOfIncludedColumns = GetHasSubsetOfIncludedColumns();
			ComparisonInfo.PkIndex = GetPkIndex();
			ComparisonInfo.UniqueIndex = GetUniqueIndex();
			ComparisonInfo.UniqueIndexHasMoreColumns = GetUniqueIndexHasMoreColumns();
			ComparisonInfo.PkIndexHasMoreColumns = GetPkIndexHasMoreColumns();
			ComparisonInfo.ClusteredIndexHasMoreColumns = GetClusteredIndexHasMoreColumns();
		}

		public void CompareThirdPass()
		{
			var ci = ComparisonInfo;

			// identical
			var analysisSummary = GetIdenticalOrOverlapAnalysisSummary(ci);
			if (analysisSummary != null)
			{
				ci.AnalysisSummary = analysisSummary;
				ci.Equivalency = IndexEquivalency.Overlap;
			}
			if (analysisSummary == null)
			{
				analysisSummary = GetCanBeMergedAnalysisSummary(ci);
				if (analysisSummary != null)
				{
					ci.AnalysisSummary = analysisSummary;
					ci.Equivalency = IndexEquivalency.CanBeMerged;
				}
			}
			if (analysisSummary == null)
			{
				analysisSummary = GetCanPossiblyBeMergedAnalysisSummary(ci);
				if (analysisSummary != null)
				{
					ci.AnalysisSummary = analysisSummary;
					ci.Equivalency = IndexEquivalency.CanPossiblyBeMerged;
				}
			}
			if (analysisSummary == null)
			{
				ci.Equivalency = IndexEquivalency.Unknown;
			}
		}

		private bool GetClusteredIndexHasMoreColumns()
		{
			if (!Index1.IsClustered && !Index1.IsClustered)
				return false;
			if (Index1.IsClustered && Index1.Columns.Count > Index2.Columns.Count)
				return true;
			if (Index2.IsClustered && Index2.Columns.Count > Index1.Columns.Count)
				return true;
			return false;
		}

		private bool GetPkIndexHasMoreColumns()
		{
			if (!Index1.IsPrimaryKey && !Index1.IsPrimaryKey)
				return false;
			if (Index1.IsPrimaryKey && Index1.Columns.Count > Index2.Columns.Count)
				return true;
			if (Index2.IsPrimaryKey && Index2.Columns.Count > Index1.Columns.Count)
				return true;
			return false;
		}

		private bool GetUniqueIndexHasMoreColumns()
		{
			if (!Index1.IsUnique && !Index2.IsUnique)
				return false;
			if (Index1.IsUnique && Index2.IsUnique)
				return false;
			if (Index1.IsUnique && Index1.Columns.Count > Index2.Columns.Count)
				return true;
			if (Index2.IsUnique && Index2.Columns.Count > Index1.Columns.Count)
				return true;
			return false;
		}

		private bool GetHasSameFilter()
		{
			return Index1.FilterDefinition == Index2.FilterDefinition;
		}

		private string GetHasSubsetOfIncludedColumns()
		{
			if (Index1.ComparisonInfo.ContainsAllIncludedColumns && Index1.ComparisonInfo.ContainsAdditionalIncludedColumns)
				return "Index2";
			if (Index2.ComparisonInfo.ContainsAllIncludedColumns && Index2.ComparisonInfo.ContainsAdditionalIncludedColumns)
				return "Index1";
			return null;
		}

		private bool GetHasSameIncludedColumns()
		{
			return Index1.ComparisonInfo.ContainsAllIncludedColumns && Index2.ComparisonInfo.ContainsAllIncludedColumns;
		}

		private bool GetHasSameIndexedColumnDescending()
		{
			return Index1.ComparisonInfo.IsDescendingMatches && Index2.ComparisonInfo.IsDescendingMatches;
		}

		private bool GetHasSameIndexedColumnOrder()
		{
			return Index1.ComparisonInfo.IndexedColumnOrderMatches && Index2.ComparisonInfo.IndexedColumnOrderMatches;
		}

		private string GetHasOrderedProperSubsetOfIndexedColumns()
		{
			if (Index1.ComparisonInfo.ContainsAdditionalIndexedColumns && Index2.ComparisonInfo.IndexedColumnOrderMatches)
				return "Index2";
			if (Index2.ComparisonInfo.ContainsAdditionalIndexedColumns && Index1.ComparisonInfo.IndexedColumnOrderMatches)
				return "Index1";
			return null;
		}

		private bool GetHasSameIndexedColumnNames()
		{
			return Index1.ComparisonInfo.ContainsAllIndexedColumns && Index2.ComparisonInfo.ContainsAllIndexedColumns;
		}

		private string GetUniqueIndex()
		{
			if (Index1.IsUnique && Index2.IsUnique)
				return "Both";
			if (Index1.IsUnique)
				return "Index1";
			if (Index2.IsUnique)
				return "Index2";
			return null;
		}

		private string GetClusteredIndex()
		{
			if (Index1.IsClustered && Index2.IsClustered)
				return "Both";
			if (Index1.IsClustered)
				return "Index1";
			if (Index2.IsClustered)
				return "Index2";
			return null;
		}

		private string GetPkIndex()
		{
			if (Index1.IsPrimaryKey)
				return "Index1";
			if (Index2.IsPrimaryKey)
				return "Index2";
			return null;
		}

		private void CheckContainsAllIndexedColumns(int indexNumber)
		{
			var thisIndex = indexNumber == 1 ? Index1 : Index2;
			var otherIndex = indexNumber == 1 ? Index2 : Index1;

			foreach (var otherIndexCol in otherIndex.IndexedColumns)
			{
				if (!thisIndex.IndexedColumns.Any(c => c.ColumnName == otherIndexCol.ColumnName))
				{
					thisIndex.ComparisonInfo.ContainsAllIndexedColumns = false;
					return;
				}
			}

			thisIndex.ComparisonInfo.ContainsAllIndexedColumns = true;
		}

		private void CheckContainsAdditionalIndexedColumns(int indexNumber)
		{
			var thisIndex = indexNumber == 1 ? Index1 : Index2;
			var otherIndex = indexNumber == 1 ? Index2 : Index1;

			// short-circuit: if length is different, no need to examine individual columns
			if (thisIndex.IndexedColumns.Count > otherIndex.IndexedColumns.Count)
			{
				thisIndex.ComparisonInfo.ContainsAdditionalIndexedColumns = true;
				return;
			}

			foreach (var col in thisIndex.IndexedColumns)
			{
				if (!otherIndex.IndexedColumns.Any(c => c.ColumnName == col.ColumnName))
				{
					thisIndex.ComparisonInfo.ContainsAdditionalIndexedColumns = true;
					return;
				}
			}

			thisIndex.ComparisonInfo.ContainsAdditionalIndexedColumns = false;
		}

		private void CheckIndexedColumnOrderMatches(int indexNumber)
		{
			var thisIndex = indexNumber == 1 ? Index1 : Index2;
			var otherIndex = indexNumber == 1 ? Index2 : Index1;

			for (var i = 0; i < otherIndex.IndexedColumns.Count; i++)
			{
				if (i > thisIndex.IndexedColumns.Count - 1)
					break;

				if (thisIndex.IndexedColumns[i].ColumnName != otherIndex.IndexedColumns[i].ColumnName)
				{
					thisIndex.ComparisonInfo.IndexedColumnOrderMatches = false;
					return;
				}
			}

			thisIndex.ComparisonInfo.IndexedColumnOrderMatches = true;
		}

		private void CheckIsDescendingMatches(int indexNumber)
		{
			var thisIndex = indexNumber == 1 ? Index1 : Index2;
			var otherIndex = indexNumber == 1 ? Index2 : Index1;

			foreach (var col in thisIndex.IndexedColumns)
			{
				var otherIndexedColumn = otherIndex.IndexedColumns.Where(oc => col.ColumnName == oc.ColumnName).FirstOrDefault();
				if (otherIndexedColumn != null && col.IsDescendingKey != otherIndexedColumn.IsDescendingKey)
				{
					thisIndex.ComparisonInfo.IsDescendingMatches = false;
					return;
				}
			}

			thisIndex.ComparisonInfo.IsDescendingMatches = true;
		}

		// TODO: Consider that the PK is going to be a hidden column in includes on any index, so I need to
		// factor that in. Probably easiest best to manually add it if it is not present explicitly.
		private void CheckContainsAllIncludedColumns(int indexNumber)
		{
			var thisIndex = indexNumber == 1 ? Index1 : Index2;
			var otherIndex = indexNumber == 1 ? Index2 : Index1;

			foreach (var otherIndexCol in otherIndex.IncludedColumns)
			{
				if (!thisIndex.IncludedColumns.Any(c => c.ColumnName == otherIndexCol.ColumnName))
				{
					thisIndex.ComparisonInfo.ContainsAllIncludedColumns = false;
					return;
				}
			}

			thisIndex.ComparisonInfo.ContainsAllIncludedColumns = true;
		}

		// TODO: Consider that the PK is going to be a hidden column in includes on any index, so I need to
		// factor that in. Probably easiest best to manually add it if it is not present explicitly.
		private void CheckContainsAdditionalIncludedColums(int indexNumber)
		{
			var thisIndex = indexNumber == 1 ? Index1 : Index2;
			var otherIndex = indexNumber == 1 ? Index2 : Index1;

			// short-circuit: if length is different, no need to examine individual columns
			if (thisIndex.IncludedColumns.Count > otherIndex.IncludedColumns.Count)
			{
				thisIndex.ComparisonInfo.ContainsAdditionalIncludedColumns = true;
				return;
			}

			foreach (var col in thisIndex.IncludedColumns)
			{
				if (!otherIndex.IncludedColumns.Any(c => c.ColumnName == col.ColumnName))
				{
					thisIndex.ComparisonInfo.ContainsAdditionalIncludedColumns = true;
					return;
				}
			}

			thisIndex.ComparisonInfo.ContainsAdditionalIncludedColumns = false;
		}
	}
}
