﻿//************************************************************************************************
// Copyright © 2020 Steven M Cohn.  All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Models
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Xml.Linq;


	internal enum TableSelectionRange
	{
		Single,
		Columns,
		Rows,
		Rectangular
	}


	/// <summary>
	/// Helper class to construct a OneNote table
	/// </summary>
	internal class Table : TableProperties
	{
		private readonly XElement columns;
		private readonly List<TableRow> rows;
		private int numCells;


		/// <summary>
		/// Initialize a new instance with an optional shading color
		/// </summary>
		/// <param name="ns"></param>
		/// <param name="shadingColor"></param>
		public Table(XNamespace ns) : base(ns)
		{
			columns = new XElement(ns + "Columns");
			Root = new XElement(ns + "Table", columns);

			rows = new List<TableRow>();
			numCells = 0;
		}


		public Table(XNamespace ns, int rows, int cols) : this(ns)
		{
			for (int i = 0; i < cols; i++)
			{
				AddColumn(1f, false);
			}

			for (int i = 0; i < rows; i++)
			{
				AddRow();
			}
		}


		public Table(XElement root) : base(root)
		{
			columns = root.Element(ns + "Columns");
			numCells = columns.Elements(ns + "Column").Count();

			rows = new List<TableRow>();
			var elements = root.Elements(ns + "Row");
			if (elements?.Any() == true)
			{
				int r = 1;
				foreach (var element in elements)
				{
					rows.Add(new TableRow(element, r));
					r++;
				}
			}
		}


		public bool BordersVisible
		{
			get { return GetBooleanAttribute("bordersVisible"); }
			set { SetAttribute("bordersVisible", value.ToString().ToLower()); }
		}


		public bool HasHeaderRow
		{
			get { return GetBooleanAttribute("hasHeaderRow"); }
			set { SetAttribute("hasHeaderRow", value.ToString().ToLower()); }
		}


		/// <summary>
		/// Gets the rows in this table.
		/// </summary>
		public IEnumerable<TableRow> Rows => rows;


		/// <summary>
		/// Gets the indexed row.
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		public TableRow this[int i] => rows[i];


		/// <summary>
		/// Adds a column definition to the table. If the table has any rows then append
		/// each row with a new cell
		/// </summary>
		/// <param name="width">Required width of the column</param>
		/// <param name="locked">True if the column width is locked</param>
		public void AddColumn(float width, bool locked = false)
		{
			var column = new XElement(ns + "Column",
				new XAttribute("index", numCells),
				new XAttribute("width", width.ToString("0.0#", CultureInfo.InvariantCulture))
				);

			if (locked)
			{
				column.Add(new XAttribute("isLocked", "true"));
			}

			columns.Add(column);
			numCells++;

			if (rows.Any())
			{
				foreach (var row in rows)
				{
					row.AddCell();
				}
			}
		}


		/// <summary>
		/// Adds a new blank row to the table
		/// </summary>
		/// <returns>A TableRow that can be used to add content</returns>
		public TableRow AddRow()
		{
			var row = new TableRow(ns, numCells);
			rows.Add(row);

			var last = columns.NodesAfterSelf().OfType<XElement>()
				.LastOrDefault(e => e.Name.LocalName == "Row");

			if (last == null)
			{
				columns.AddAfterSelf(row.Root);
			}
			else
			{
				last.AddAfterSelf(row.Root);
			}

			return row;
		}


		public TableCell GetCell(string coord)
		{
			return rows.SelectMany(
				r => r.Cells.Where(e => e.Coordinates == coord), (row, cell) => cell)
				.FirstOrDefault();
		}


		public IEnumerable<TableCell> GetSelectedCells(out TableSelectionRange range)
		{
			var selections =
				from r in rows
				let cells = r.Cells
				from c in cells
				where c.Selected == Selection.all || c.Selected == Selection.partial
				select c;

			range = InferRangeType(selections);

			return selections;
		}


		private TableSelectionRange InferRangeType(IEnumerable<TableCell> cells)
		{
			if (cells.Count() == 1)
			{
				return TableSelectionRange.Single;
			}

			var col = -1;
			var row = -1;

			// cells should be in order, from A1, A2, .. B1, B2, .. C1, C2, ..
			foreach (var cell in cells)
			{
				// record first column and then notice when there are multiple cols selected
				if (col < 0)
					col = cell.ColNum;
				else if (col != int.MaxValue && col != cell.ColNum)
					col = int.MaxValue;

				// record first row and then notice when there are multiple rows selected
				if (row < 0)
					row = cell.RowNum;
				else if (row != int.MaxValue && row != cell.RowNum)
					row = int.MaxValue;

				// if both multi cols and multi rows then must be rectangular
				if (col == int.MaxValue && row == int.MaxValue)
					break;
			}

			if (col == int.MaxValue && row == int.MaxValue)
			{
				return TableSelectionRange.Rectangular;
			}
			else if (col == int.MaxValue)
			{
				return TableSelectionRange.Columns;
			}

			return TableSelectionRange.Rows;
		}


		public void SetColumnWidth(int index, float width)
		{
			var column = columns.Elements(ns + "Column").Skip(index)?.FirstOrDefault();
			if (column != null)
			{
				column.SetAttributeValue("width", width.ToString("0.0#", CultureInfo.InvariantCulture));
				column.SetAttributeValue("isLocked", "true");
			}
		}
	}
}
