using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace duplicate_index_finder
{
	class Program
	{
		static void Main(string[] arguments)
		{
			CommandLineArguments args = GetArguments(arguments);

			if (args.Help)
			{
				Console.Write(CommandLineArguments.GetUsageString());
				ExitWithCode(ExitCode.Success);
			}

			var indexes = Index.GetIndexes(args.Schema, args.Table).Where(i => !i.IsDisabled);
			Console.WriteLine($"Found {indexes.Count().ToString("N0")} indexes");
			var analyzedIndexPairs = GetAnalyzedIndexPairs(indexes, args.Index);

			var identical = analyzedIndexPairs.Where(i => i.ComparisonInfo.Equivalency == IndexEquivalency.Overlap);
			Console.WriteLine($"Found {identical.Count()} index pair{(identical.Count() == 1 ? "" : "s")} that are identical or overlap");

			var canBeMerged = analyzedIndexPairs.Where(i => i.ComparisonInfo.Equivalency == IndexEquivalency.CanBeMerged);
			Console.WriteLine($"Found {canBeMerged.Count()} index pair{(canBeMerged.Count() == 1 ? "" : "s")} that can be merged");

			var canPossiblyBeMerged = analyzedIndexPairs.Where(i => i.ComparisonInfo.Equivalency == IndexEquivalency.CanPossiblyBeMerged);
			Console.WriteLine($"Found {canPossiblyBeMerged.Count()} index pair{(canPossiblyBeMerged.Count() == 1 ? "" : "s")} that can possibly be merged");

			DisplayAnalysis(identical, "Indexes that are identical or overlap");
			DisplayAnalysis(canBeMerged, "Indexes that can be merged");
			DisplayAnalysis(canPossiblyBeMerged, "Indexes that can possibly be merged");

			ExitWithCode(ExitCode.Success);
		}

		private static void DisplayAnalysis(IEnumerable<IndexPair> indexPairs, string label)
		{
			if (!indexPairs.Any())
				return;

			Console.WriteLine("");
			Console.WriteLine($"{label}:");
			//foreach (var indexPair in indexPairs)
			//{
			//	var sb = new StringBuilder();
			//	sb.AppendLine(indexPair.Index1.GetDetailInfo());
			//	sb.AppendLine(indexPair.Index2.GetDetailInfo());
			//	//sb.AppendLine(indexPair.GetSummaryInfo());
			//	sb.AppendLine($"Recommendation: {indexPair.ComparisonInfo.AnalysisSummary}");
			//	sb.Append("-----------------------------------------------------------");
			//	Console.WriteLine(sb.ToString());
			//}

			foreach (var ip in indexPairs)
			{
				var t = new Grid();
				t.Rows.Add(new Row(
						new Cell($"Table"),
						new Cell($"{ip.Index1.SchemaName}.{ip.Index1.TableName}", colspan:2)
					));
				t.Rows.Add(new Row(
						new Cell("Index"),
						new Cell($"{ip.Index1.IndexName}"),
						new Cell($"{ip.Index2.IndexName}")
					));
				t.Rows.Add(new Row(
						new Cell("Indexed Columns"),
						new Cell(string.Join(", ", ip.Index1.IndexedColumns.Select(c => c.ColumnName + (c.IsDescendingKey ? " (desc)" : "")))),
						new Cell(string.Join(", ", ip.Index2.IndexedColumns.Select(c => c.ColumnName + (c.IsDescendingKey ? " (desc)" : ""))))
					));
				t.Rows.Add(new Row(
						new Cell("Included Columns"),
						new Cell($"{ string.Join(", ", ip.Index1.IncludedColumns.Select(c => c.ColumnName))}"),
						new Cell($"{ string.Join(", ", ip.Index2.IncludedColumns.Select(c => c.ColumnName))}")
					));
				t.Rows.Add(new Row(
						new Cell("Attributes"),
						new Cell($"{ string.Join(", ", ip.Index1.GetAttributes())}"),
						new Cell($"{ string.Join(", ", ip.Index2.GetAttributes())}")
					));
				t.Rows.Add(new Row(
						new Cell("Filter"),
						new Cell($"{ string.Join(", ", ip.Index1.FilterDefinition)}"),
						new Cell($"{ string.Join(", ", ip.Index2.FilterDefinition)}")
					));
				t.Rows.Add(new Row(
						new Cell("Recommendation"),
						new Cell(ip.ComparisonInfo.AnalysisSummary, colspan: 2)
					));

				Console.WriteLine(t.Render());
			}
		}

		// make unique pairings of indexes (ignoring order in pair) and compare them
		private static List<IndexPair> GetAnalyzedIndexPairs(IEnumerable<Index> indexes, string index = null)
		{
			var distinctTables = indexes.GroupBy(i => new { i.SchemaName, i.TableName }).Select(g => g.Select(gs => new { gs.SchemaName, gs.TableName }).First()).ToList();
			var analyzedIndexPairs = new List<IndexPair>();
			foreach (var table in distinctTables)
			{
				var tableIndexes = indexes.Where(i => i.SchemaName == table.SchemaName && i.TableName == table.TableName).ToArray();
				for (var i = 0; i < tableIndexes.Count(); i++)
				{
					for (var j = i + 1; j < tableIndexes.Count(); j++)
					{
						// filter for requested index if necessary
						if (index != null && tableIndexes[i].IndexName != index && tableIndexes[j].IndexName != index)
							continue;

						var indexPair = new IndexPair(tableIndexes[i].Clone(), tableIndexes[j].Clone());
						indexPair.Compare();
						analyzedIndexPairs.Add(indexPair);
					}
				}
			}

			return analyzedIndexPairs;
		}

		static CommandLineArguments GetArguments(string[] arguments)
		{
			CommandLineArguments args = null;
			try
			{
				args = CommandLineArguments.ParseArguments(arguments);
			}
			catch (DotArguments.Exceptions.MandatoryArgumentMissingException)
			{
				ExitWithCode(ExitCode.Error);
			}

			return args;
		}

		static void ExitWithCode(ExitCode exitCode, string message = null)
		{
			if (message != null)
				Console.WriteLine(message);
			if (Debugger.IsAttached)
				Console.ReadKey();
			Environment.Exit((int)exitCode);
		}
	}

	public enum ExitCode
	{
		Success,
		Error
	}
}
