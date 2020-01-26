namespace duplicate_index_finder
{
    class Column
	{
		public string ColumnName { get; set; }
		public int ColumnPosition { get; set; }
		public bool IsIncludedColumn { get; set; }
		public bool IsDescendingKey { get; set; }
	}
}
