using System.Collections.Generic;

namespace Pilot.SDK
{
    /// <summary>
    /// A layout container for Pilot UI widgets.
    /// Supports vertical/horizontal arrangement and nesting.
    /// </summary>
    public sealed class PilotLayout
    {
        public enum Direction
        {
            Vertical,
            Horizontal
        }

        private readonly PilotUI m_ui;
        private readonly Direction m_direction;
        private readonly List<object> m_children = new List<object>();

        internal PilotLayout(PilotUI ui, Direction direction)
        {
            m_ui = ui;
            m_direction = direction;
        }

        public Direction GetDirection() => m_direction;

        // ── Sub-layouts ──

        public PilotLayout AddVertical()
        {
            var sub = new PilotLayout(m_ui, Direction.Vertical);
            m_children.Add(sub);
            m_ui.IncrementRevision();
            return sub;
        }

        public PilotLayout AddHorizontal()
        {
            var sub = new PilotLayout(m_ui, Direction.Horizontal);
            m_children.Add(sub);
            m_ui.IncrementRevision();
            return sub;
        }

        public PilotLayout AddCollapsible(string title)
        {
            var collapsible = new CollapsibleElement(m_ui, title);
            m_children.Add(collapsible);
            m_ui.IncrementRevision();
            return collapsible.Content;
        }

        // ── Padding ──

        public PilotLayout AddPadding(double weight)
        {
            m_children.Add(new PaddingElement(weight));
            m_ui.IncrementRevision();
            return this;
        }

        // ── Widgets ──

        public PilotButton AddButton(string label)
        {
            var w = new PilotButton(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotLabel AddLabel(string text)
        {
            var w = new PilotLabel(m_ui, text);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotStat AddStat(string label)
        {
            var w = new PilotStat(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotSwitch AddSwitch(string label)
        {
            var w = new PilotSwitch(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotInput AddInput(string label)
        {
            var w = new PilotInput(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotSelect AddSelect(string label)
        {
            var w = new PilotSelect(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotTextarea AddTextarea(string label)
        {
            var w = new PilotTextarea(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotTable AddTable(string label)
        {
            var w = new PilotTable(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        public PilotLogs AddLogs(string label)
        {
            var w = new PilotLogs(m_ui, label);
            m_children.Add(w);
            m_ui.IncrementRevision();
            return w;
        }

        // ── Serialization ──

        internal Dictionary<string, object> ToDict()
        {
            var childrenList = new List<object>();
            foreach (var child in m_children)
            {
                if (child is PilotLayout layout)
                    childrenList.Add(layout.ToDict());
                else if (child is PaddingElement padding)
                    childrenList.Add(padding.ToDict());
                else if (child is CollapsibleElement collapsible)
                    childrenList.Add(collapsible.ToDict());
                else
                {
                    // Use reflection-free approach: all widgets derive from PilotWidget<T>
                    var method = child.GetType().GetMethod("ToDict",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (method != null)
                        childrenList.Add(method.Invoke(child, null));
                }
            }

            return new Dictionary<string, object>
            {
                ["type"] = "layout",
                ["direction"] = m_direction == Direction.Vertical ? "vertical" : "horizontal",
                ["children"] = childrenList
            };
        }

        // ── Padding helper ──

        private sealed class PaddingElement
        {
            public readonly double Weight;

            public PaddingElement(double weight) { Weight = weight; }

            public Dictionary<string, object> ToDict()
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "padding",
                    ["weight"] = Weight
                };
            }
        }

        // ── Collapsible helper ──

        private sealed class CollapsibleElement
        {
            public readonly string Title;
            public readonly PilotLayout Content;

            public CollapsibleElement(PilotUI ui, string title)
            {
                Title = title;
                Content = new PilotLayout(ui, Direction.Vertical);
            }

            public Dictionary<string, object> ToDict()
            {
                var contentDict = Content.ToDict();
                return new Dictionary<string, object>
                {
                    ["type"] = "collapsible",
                    ["title"] = Title,
                    ["children"] = contentDict.ContainsKey("children") ? contentDict["children"] : new List<object>()
                };
            }
        }
    }
}
