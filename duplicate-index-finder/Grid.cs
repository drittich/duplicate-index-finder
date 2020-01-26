using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace duplicate_index_finder
{
	public class Grid
	{
		public ObservableCollection<Row> Rows { get; private set; }
		public int ColumnCount { get; private set; }

		public Grid()
		{
			Rows = new ObservableCollection<Row>();
			Rows.CollectionChanged += new NotifyCollectionChangedEventHandler(
				delegate (object sender, NotifyCollectionChangedEventArgs e)
				{
					foreach (Row x in e.NewItems)
					{
						var columns = x.Cells.Sum(c => c.ColSpan);
						if (ColumnCount == 0)
							ColumnCount = columns;
						if (columns != ColumnCount)
							throw new Exception($"Invalid number of columns, column count is {ColumnCount}");
					}
				}
			);
		}

		public string Render()
		{
			var sb = new StringBuilder();
			sb.AppendLine(RenderRowSeparator("first"));
			var rowIndex = 0;
			foreach (var row in Rows)
			{
				sb.AppendLine(RenderRow(row));
				if (row != Rows.Last())
					sb.AppendLine(RenderRowSeparator("middle", rowIndex));
				rowIndex++;
			}
			sb.AppendLine(RenderRowSeparator("last", Rows.Count - 1));
			return sb.ToString();
		}

		private string RenderRowSeparator(string position, int? rowIndex = -1)
		{
			var left = position == "first" ? "┌" : (position == "last" ? "└" : "├");
			var right = position == "first" ? "┐" : (position == "last" ? "┘" : "┤");
			var space = '─';

			var sb = new StringBuilder();
			for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
			{
				if (columnIndex == 0)
					sb.Append(left);

				sb.Append($"{new string(space, GetMaxLengthForColumn(columnIndex))}");

				if (columnIndex == ColumnCount - 1)
					sb.Append(right);
				else
				{
					var prevRowCell = GetCellByColumnIndex(rowIndex.Value, columnIndex + 1);
					var nextRowCell = GetCellByColumnIndex(rowIndex.Value + 1, columnIndex + 1);
					var separator = GetInnerRowSeparator(prevRowCell, nextRowCell);
					sb.Append(separator);
				}
			}
			return sb.ToString();
		}

		private string GetInnerRowSeparator(Cell prevRowCell, Cell nextRowCell)
		{
			if (prevRowCell == null && nextRowCell == null)
				return "─";
			if (prevRowCell == null && nextRowCell != null)
				return "┬";
			if (prevRowCell != null && nextRowCell == null)
				return "┴";
			//if (prevRowCell != null && nextRowCell != null)
			return "┼";
		}

		private Cell GetCellByColumnIndex(int rowIndex, int columnIndex)
		{
			var row = Rows.Where(r => Rows.IndexOf(r) == rowIndex).FirstOrDefault();
			if (row == null)
				return null;

			int startColumn = 0;
			foreach (var cell in row.Cells)
			{
				if (startColumn == columnIndex)
					return cell;
				if (startColumn > columnIndex)
					return null;
				startColumn += cell.ColSpan;
			}
			return null;
		}

		private string RenderRow(Row row)
		{
			var sb = new StringBuilder();
			int columnIndex = 0;
			for (var i = 0; i < row.Cells.Count; i++)
			{
				var curCell = row.Cells[i];
				int cellMaxLength = 0;
				// a cell can have multiple colums, add the max lengths together
				for (var j = 0; j < curCell.ColSpan; j++)
					cellMaxLength += GetMaxLengthForColumn(columnIndex + j);
				// add an extra character for each separator when ColSpan > 1
				cellMaxLength += curCell.ColSpan - 1;

				sb.Append($"│{GetPaddedValueForCell(cellMaxLength, curCell)}");

				if (i == row.Cells.Count - 1)
					sb.Append("│");

				columnIndex += curCell.ColSpan;
			}
			return sb.ToString();
		}

		private string GetPaddedValueForCell(int maxLength, Cell cell)
		{
			var value = cell.Value.ToString();
			var paddingLength = maxLength - value.Length;

			if (cell.Align == Align.Left)
				return value + new string(' ', paddingLength);
			if (cell.Align == Align.Right)
				return new string(' ', paddingLength) + value;
			if (cell.Align == Align.Center)
			{
				if (paddingLength % 2 == 0)
				{
					var padding = new string(' ', paddingLength / 2);
					return padding + value + padding;
				}
				else
				{
					var padding = new string(' ', (int)Math.Floor(((decimal)paddingLength / 2)));
					return padding + value + padding + ' ';
				}
			}
			return null;
		}

		Dictionary<int, int> _maxlengths = new Dictionary<int, int>();
		private int GetMaxLengthForColumn(int colIndex)
		{
			if (!_maxlengths.ContainsKey(colIndex))
			{
				int maxLength = 0;
				for (var rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
				{
					var cell = GetCellByColumnIndex(rowIndex, colIndex);
					if (cell == null)
						continue;

					var length = cell.Value.ToString().Length;
					if (length > maxLength)
						maxLength = length;
				}
				_maxlengths[colIndex] = maxLength;
			}
			return _maxlengths[colIndex];
		}
	}

	public class Row
	{
		public Row(params Cell[] cells)
		{
			Cells = cells.ToList();
		}
		public bool IsHeader { get; set; }
		public List<Cell> Cells { get; set; }
	}

	public class Cell
	{
		public object Value { get; set; }

		public Align Align
		{
			get; set;
		}

		int? _colspan = null;
		public int ColSpan
		{
			get
			{
				if (_colspan == null || _colspan < 1)
					_colspan = 1;
				return _colspan.Value;
			}
			set
			{
				_colspan = value;
			}
		}

		public Cell(object value, int colspan = 1, Align align = Align.Left)
		{
			Value = value;
			Align = align;
			ColSpan = colspan;
		}
	}
	public enum Align
	{
		Left, Center, Right
	}

}
