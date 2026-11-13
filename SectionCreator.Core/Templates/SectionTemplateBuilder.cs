using System;
using SectionCreator.Core.Geometry;

namespace SectionCreator.Core.Templates
{
    public enum TemplateType
    {
        C,
        U
    }

    /// <summary>
    /// Dış yüz (out-to-out) ölçülerinden orta hat geometrisi üretir.
    /// Gövde orta hattı x = 0 doğrusunda, alt flanş orta hattı y = 0 doğrusundadır; kesit +x yönüne açılır.
    /// </summary>
    public static class SectionTemplateBuilder
    {
        public const int LipDivisions = 2;
        public const int CornerDivisions = 6;
        public const int FlangeDivisions = 3;
        public const int WebDivisions = 6;

        /// <param name="a">Gövde yüksekliği (dış yüz)</param>
        /// <param name="b">Flanş genişliği (dış yüz)</param>
        /// <param name="c">Dudak uzunluğu (dış yüz); U için kullanılmaz</param>
        /// <param name="t">Tasarım kalınlığı</param>
        /// <param name="r">İç radius</param>
        public static Section Build(TemplateType type, double a, double b, double c, double t, double r)
        {
            if (t <= 0) throw new ArgumentException("t sıfırdan büyük olmalı.");
            if (r < 0) throw new ArgumentException("R negatif olamaz.");

            double rc = r + t / 2.0;
            double h = a - t;
            double w = b - t / 2.0;
            double lip = c - t / 2.0;

            bool hasLip = type == TemplateType.C;

            double webStraight = h - 2 * rc;
            double flangeStraight = hasLip ? w - 2 * rc : w - rc;
            double lipStraight = lip - rc;

            if (webStraight < 0) throw new ArgumentException("Gövde yüksekliği (A) radiuslar için çok küçük.");
            if (flangeStraight < 0) throw new ArgumentException("Flanş genişliği (B) radiuslar için çok küçük.");
            if (hasLip && lipStraight < 0) throw new ArgumentException("Dudak uzunluğu (C) radius için çok küçük.");

            var s = new Section
            {
                Name = type + " " + a + "x" + b + (hasLip ? "x" + c : "") + "x" + t,
                Thickness = t
            };

            Node cur;
            if (hasLip)
            {
                cur = s.AddNode(w, h - lip);
                cur = Line(s, cur, w, h - rc, LipDivisions, ElementKind.Lip);
                cur = Arc(s, cur, w - rc, h - rc, rc, 0, 90);
            }
            else
            {
                cur = s.AddNode(w, h);
            }

            cur = Line(s, cur, rc, h, FlangeDivisions, ElementKind.Flange);
            cur = Arc(s, cur, rc, h - rc, rc, 90, 180);
            cur = Line(s, cur, 0, rc, WebDivisions, ElementKind.Web);
            cur = Arc(s, cur, rc, rc, rc, 180, 270);
            cur = Line(s, cur, hasLip ? w - rc : w, 0, FlangeDivisions, ElementKind.Flange);

            if (hasLip)
            {
                cur = Arc(s, cur, w - rc, rc, rc, 270, 360);
                Line(s, cur, w, lip, LipDivisions, ElementKind.Lip);
            }

            return s;
        }

        private static Node Line(Section s, Node from, double x, double y, int n, ElementKind kind)
        {
            Node prev = from;
            double x0 = from.X, y0 = from.Y;
            for (int i = 1; i <= n; i++)
            {
                double f = (double)i / n;
                Node next = s.AddNode(x0 + (x - x0) * f, y0 + (y - y0) * f);
                s.AddElement(prev, next, kind);
                prev = next;
            }
            return prev;
        }

        private static Node Arc(Section s, Node from, double cx, double cy, double rad, double deg0, double deg1)
        {
            Node prev = from;
            for (int i = 1; i <= CornerDivisions; i++)
            {
                double ang = (deg0 + (deg1 - deg0) * i / CornerDivisions) * Math.PI / 180.0;
                Node next = s.AddNode(cx + rad * Math.Cos(ang), cy + rad * Math.Sin(ang));
                s.AddElement(prev, next, ElementKind.Corner);
                prev = next;
            }
            return prev;
        }
    }
}
