using System;
using System.Collections.Generic;
using System.Linq;
using netDxf;
using netDxf.Entities;
using SectionCreator.Core.Geometry;

namespace SectionCreator.Core.Dxf
{
    /// <summary>
    /// DXF dosyasındaki 2B polyline'ı orta hat olarak okuyup Section'a çevirir.
    /// Bulge'lü segmentler yay olarak ele alınır ve 15 derecelik adımlarla bölünür (90 derece = 6 parça).
    /// </summary>
    public static class DxfSectionImporter
    {
        public const double ArcStepDegrees = 15.0;

        /// <param name="path">DXF dosya yolu</param>
        /// <param name="thickness">Tasarım kalınlığı t</param>
        /// <param name="targetElementLength">Düz segmentler için hedef eleman boyu; 0 ise otomatik (en büyük boyutun 1/30'u)</param>
        public static Section Import(string path, double thickness, double targetElementLength = 0)
        {
            if (thickness <= 0) throw new ArgumentException("t sıfırdan büyük olmalı.");

            DxfDocument doc = DxfDocument.Load(path);
            if (doc == null) throw new InvalidOperationException("DXF dosyası okunamadı.");

            Polyline2D poly = doc.Entities.Polylines2D.FirstOrDefault();
            if (poly == null) throw new InvalidOperationException("DXF içinde 2B polyline bulunamadı.");

            return FromPolyline(poly, System.IO.Path.GetFileNameWithoutExtension(path), thickness, targetElementLength);
        }

        public static Section FromPolyline(Polyline2D poly, string name, double thickness, double targetElementLength = 0)
        {
            var verts = poly.Vertexes.ToList();
            if (verts.Count < 2) throw new InvalidOperationException("Polyline en az 2 köşe içermeli.");

            if (targetElementLength <= 0)
            {
                double minX = verts.Min(v => v.Position.X), maxX = verts.Max(v => v.Position.X);
                double minY = verts.Min(v => v.Position.Y), maxY = verts.Max(v => v.Position.Y);
                targetElementLength = Math.Max(maxX - minX, maxY - minY) / 30.0;
            }

            var s = new Section { Name = name, Thickness = thickness };
            Node cur = s.AddNode(verts[0].Position.X, verts[0].Position.Y);

            int segCount = poly.IsClosed ? verts.Count : verts.Count - 1;
            for (int i = 0; i < segCount; i++)
            {
                var v0 = verts[i];
                var v1 = verts[(i + 1) % verts.Count];
                bool isLastClosing = poly.IsClosed && i == verts.Count - 1;
                cur = AddSegment(s, cur, v0, v1, targetElementLength, isLastClosing ? s.Nodes[0] : null);
            }
            return s;
        }

        private static Node AddSegment(Section s, Node from, Polyline2DVertex v0, Polyline2DVertex v1,
            double targetLength, Node closeTo)
        {
            double x0 = v0.Position.X, y0 = v0.Position.Y;
            double x1 = v1.Position.X, y1 = v1.Position.Y;
            double dx = x1 - x0, dy = y1 - y0;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-12) return from;

            var pts = new List<double[]>();
            ElementKind kind;
            double b = v0.Bulge;

            if (Math.Abs(b) < 1e-12)
            {
                int n = Math.Max(1, (int)Math.Ceiling(d / targetLength));
                for (int k = 1; k <= n; k++)
                    pts.Add(new[] { x0 + dx * k / n, y0 + dy * k / n });
                kind = ElementKind.Other;
            }
            else
            {
                double theta = 4.0 * Math.Atan(b);
                double off = (d / 2.0) * (1.0 - b * b) / (2.0 * b);
                double cx = (x0 + x1) / 2.0 + off * (-dy / d);
                double cy = (y0 + y1) / 2.0 + off * (dx / d);
                double a0 = Math.Atan2(y0 - cy, x0 - cx);
                int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(theta) * 180.0 / Math.PI / ArcStepDegrees - 1e-9));
                double rad = Math.Sqrt((x0 - cx) * (x0 - cx) + (y0 - cy) * (y0 - cy));
                for (int k = 1; k <= n; k++)
                {
                    double a = a0 + theta * k / n;
                    pts.Add(new[] { cx + rad * Math.Cos(a), cy + rad * Math.Sin(a) });
                }
                kind = ElementKind.Corner;
            }

            Node prev = from;
            for (int k = 0; k < pts.Count; k++)
            {
                bool last = k == pts.Count - 1;
                Node next;
                if (last && closeTo != null)
                    next = closeTo;
                else
                    next = s.AddNode(pts[k][0], pts[k][1]);
                s.AddElement(prev, next, kind);
                prev = next;
            }
            return prev;
        }
    }
}
