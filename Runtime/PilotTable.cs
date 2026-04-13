using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// Table widget. Displays data in rows and columns.
    /// </summary>
    public sealed class PilotTable : PilotWidget<PilotTable>
    {
        internal PilotTable(PilotUI ui, string label) : base(ui, "table")
        {
            Put("label", label);
        }

        public PilotTable Columns(string[][] columns)
        {
            var list = new List<object>();
            foreach (var col in columns)
            {
                list.Add(new Dictionary<string, object>
                {
                    ["key"] = col[0],
                    ["label"] = col[1]
                });
            }
            Put("columns", list);
            return this;
        }

        public PilotTable Rows(List<Dictionary<string, object>> rows)
        {
            var list = new List<object>();
            foreach (var row in rows)
            {
                list.Add(row);
            }
            Put("rows", list);
            return this;
        }
    }
}
