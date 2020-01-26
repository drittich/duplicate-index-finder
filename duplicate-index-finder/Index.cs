using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json;

namespace duplicate_index_finder
{
	class Index
	{
		public string SchemaName { get; set; }
		public string TableName { get; set; }
		public string IndexName { get; set; }
		public bool HasFilter { get; set; }
		public string FilterDefinition { get; set; }
		public bool IsUnique { get; set; }
		public bool IsUniqueConstraint { get; set; }
		public int? UserSeeks { get; set; }
		public int? UserScans { get; set; }
		public int? UserLookups { get; set; }
		public int? UserReads { get; set; }
		public int? UserWrites { get; set; }
		public float IndexSizeMB { get; set; }
		public bool IsDisabled { get; set; }
		public bool IsPrimaryKey { get; set; }
		public bool IsClustered { get; set; }
		public List<Column> Columns { get; set; }

		public IndexComparisonInfo ComparisonInfo = new IndexComparisonInfo();

		public List<Column> IncludedColumns
		{
			get
			{
				return Columns.Where(c => c.IsIncludedColumn).ToList();
			}
		}

		public List<Column> IndexedColumns
		{
			get
			{
				return Columns.Where(c => !c.IsIncludedColumn).ToList();
			}
		}

		public string GetDetailInfo()
		{
			var sb = new StringBuilder();
			var name = $"[{SchemaName}].[{TableName}].[{IndexName}]";
			sb.AppendLine($"Name: {name} (Reads:{NotAvailableIfNull(UserReads)} Writes:{NotAvailableIfNull(UserWrites)} Size:{IndexSizeMB.ToString("N2")} MB)");
			sb.AppendLine($"Indexed Columns: {string.Join(", ", IndexedColumns.Select(c => c.ColumnName + (c.IsDescendingKey ? " (desc)" : "")))}");
			sb.AppendLine($"Included Columns: {string.Join(", ", IncludedColumns.Select(c => c.ColumnName))}");

			List<string> attribs = GetAttributes();
			sb.AppendLine($"Attributes: {string.Join(", ", attribs)}");

			sb.AppendLine($"Filter: {FilterDefinition}");

			return sb.ToString();
		}

		private List<string> _attributes;
		public List<string> GetAttributes()
		{
			if (_attributes == null)
			{
				_attributes = new List<string>();
				if (IsPrimaryKey) _attributes.Add("Primary Key");
				if (IsClustered) _attributes.Add("Clustered");
				if (IsUnique) _attributes.Add("Unique");
				return _attributes;

			}
			return _attributes;
		}

		static string NotAvailableIfNull(int? i)
		{
			return i.HasValue ? i.Value.ToString("N0") : "N/A";
		}

		public string GetSummaryInfo()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"{SchemaName}.{TableName}.{IndexName} (Reads:{NotAvailableIfNull(UserReads)} Writes:{NotAvailableIfNull(UserWrites)} Size:{IndexSizeMB.ToString("N2")} MB)");
			return sb.ToString();
		}

		public static IEnumerable<Index> GetIndexes(string schema, string table)
		{
			var filterClause = string.IsNullOrWhiteSpace(schema) ? "" : @"and schema_name(t.schema_id) = @schema";
			filterClause += string.IsNullOrWhiteSpace(table) ? "" : @" and t.name = @table";

			string indexSql = $@"
				SELECT schema_name(t.schema_id) as SchemaName,
					t.name as TableName,
					ind.name as IndexName,
					ind.has_filter as HasFilter,
					ind.filter_definition as FilterDefinition,
					ind.is_unique as IsUnique,
					ind.is_unique_constraint as IsUniqueConstraint,
					s.USER_SEEKS as UserSeeks, 
					s.USER_SCANS as UserScans, 
					s.USER_LOOKUPS as UserLookups, 
					s.USER_SEEKS + s.USER_SCANS + s.USER_LOOKUPS as UserReads,
					s.USER_UPDATES as UserWrites,
					size.IndexSizeMB,
					ind.is_disabled as IsDisabled,
                    ind.is_primary_key as IsPrimaryKey,
                    case when ind.type_desc = 'CLUSTERED' then 1 else 0 end as IsClustered
				FROM sys.indexes ind 
				inner join sys.tables t ON ind.object_id = t.object_id 
				left join SYS.DM_DB_INDEX_USAGE_STATS S ON ind.[OBJECT_ID] = S.[OBJECT_ID] 
					AND ind.INDEX_ID = S.INDEX_ID 
					and s.database_id = db_id()
				inner JOIN (
					SELECT schema_name(tn.schema_id) as SchemaName, 
						tn.[name] AS TableName, 
						ix.[name] AS IndexName,
						(SUM(sz.[used_page_count]) * 8) / 1024.0 AS [IndexSizeMB]
					FROM sys.dm_db_partition_stats AS sz
					INNER JOIN sys.indexes AS ix ON sz.[object_id] = ix.[object_id] 
						AND sz.[index_id] = ix.[index_id]
					INNER JOIN sys.tables tn ON tn.OBJECT_ID = ix.object_id
					where ix.name is not null
					GROUP BY schema_name(tn.schema_id), tn.[name], ix.[name]
				) size on schema_name(t.schema_id) = size.SchemaName
					and t.name = size.TableName
					and ind.name = size.IndexName
				where t.is_ms_shipped = 0 {filterClause}";
			//indexSql += @" and ind.name in ('IX_MonitorThresholdFailure_FailedUntil', 'IX_MonitorThresholdFailure_FailedUntil_includes')";

			indexSql += @"

                order by schema_name(t.schema_id), t.name, ind.name";

			string columnSql = $@"
				SELECT t.name as TableName,
					schema_name(t.schema_id) as SchemaName,
					ind.name as IndexName,
					col.name as ColumnName,
					ic.index_column_id as ColumnPosition,
					ic.is_descending_key as IsDescendingKey,
					ic.is_included_column as IsIncludedColumn
				FROM sys.indexes ind 
				INNER JOIN sys.index_columns ic ON  ind.object_id = ic.object_id and ind.index_id = ic.index_id 
				INNER JOIN sys.columns col ON ic.object_id = col.object_id and ic.column_id = col.column_id 
				INNER JOIN sys.tables t ON ind.object_id = t.object_id 
				where t.is_ms_shipped = 0 {filterClause}";

			columnSql += @"
				order by ColumnPosition";

			IEnumerable<Index> indexes;
			List<dynamic> columns;
			var parms = new { schema, table };

			using (var cn = Sql.GetConnection())
			{
				indexes = cn.Query<Index>(indexSql, parms).ToList();
				columns = cn.Query(columnSql, parms).ToList();
			}

			foreach (var idx in indexes)
			{
				idx.Columns = columns.Where(c => c.SchemaName == idx.SchemaName
					&& c.TableName == idx.TableName
					&& c.IndexName == idx.IndexName).Select(c => new Column
					{
						ColumnName = c.ColumnName,
						ColumnPosition = c.ColumnPosition,
						IsIncludedColumn = c.IsIncludedColumn,
						IsDescendingKey = c.IsDescendingKey
					}).ToList();
			}

			return indexes;
		}

		public Index Clone()
		{
			return JsonConvert.DeserializeObject<Index>(JsonConvert.SerializeObject(this));
		}

		public override string ToString()
		{
			return $"{IndexName} Cols:{string.Join(", ", IndexedColumns.Select(c => c.ColumnName))} Includes:{string.Join(", ", IncludedColumns.Select(c => c.ColumnName))}";
		}
	}
}
