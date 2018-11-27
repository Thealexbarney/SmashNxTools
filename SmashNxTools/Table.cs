using System;
using System.Collections.Generic;
using System.Text;

namespace SmashNxTools
{
    public class Table
    {
        private List<string[]> Rows { get; } = new List<string[]>();
        private int ColumnCount { get; set; }

        public Table(params string[] header)
        {
            ColumnCount = header.Length;
            Rows.Add(header);
        }

        public void AddRow(params string[] row)
        {
            if (row.Length != ColumnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "All rows must have the same number of columns");
            }

            Rows.Add(row);
        }

        public string Print()
        {
            var sb = new StringBuilder();
            var indexWidth = Math.Max(Rows.Count.ToString("X").Length, "idx".Length);
            string indexFormat = $"X{indexWidth}";
            var width = new int[ColumnCount];

            foreach (string[] row in Rows)
            {
                for (int i = 0; i < ColumnCount; i++)
                {
                    width[i] = Math.Max(width[i], row[i].Length);
                }
            }

            for (int r = 0; r < Rows.Count; r++)
            {
                var first = r == 0 ? "idx" : (r - 1).ToString(indexFormat);
                sb.Append($"{first.PadLeft(indexWidth + 1, ' ')}");

                for (int i = 0; i < ColumnCount; i++)
                {
                    sb.Append($"{Rows[r][i].PadLeft(width[i] + 1, ' ')}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
