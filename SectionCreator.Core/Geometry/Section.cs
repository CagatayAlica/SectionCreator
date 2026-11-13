using System.Collections.Generic;

namespace SectionCreator.Core.Geometry
{
    public class Node
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public enum ElementKind
    {
        Lip,
        Corner,
        Flange,
        Web,
        Other
    }

    /// <summary>Kalınlık orta hattı üzerinde düz strip eleman.</summary>
    public class Element
    {
        public int Id { get; set; }
        public int StartNodeId { get; set; }
        public int EndNodeId { get; set; }
        public double Thickness { get; set; }
        public ElementKind Kind { get; set; }
    }

    public class Section
    {
        public string Name { get; set; }
        public double Thickness { get; set; }
        public string MaterialName { get; set; }
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<Element> Elements { get; set; } = new List<Element>();

        public int LinkNodeA { get; set; }
        public int LinkNodeB { get; set; }

        public Node AddNode(double x, double y)
        {
            var n = new Node { Id = Nodes.Count + 1, X = x, Y = y };
            Nodes.Add(n);
            return n;
        }

        public Element AddElement(Node a, Node b, ElementKind kind)
        {
            var e = new Element
            {
                Id = Elements.Count + 1,
                StartNodeId = a.Id,
                EndNodeId = b.Id,
                Thickness = Thickness,
                Kind = kind
            };
            Elements.Add(e);
            return e;
        }

        public Node GetNode(int id) => Nodes[id - 1];
    }
}
