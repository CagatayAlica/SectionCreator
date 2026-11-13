using System;
using System.Collections.Generic;
using SectionCreator.Core.Geometry;

namespace SectionCreator.Core.Properties
{
    /// <summary>
    /// Gross kesit özellikleri. Birimler girdi birimleriyle tutarlıdır (örn. mm, mm2, mm4, mm6).
    /// Eksen yönleri: x yatay, y düşey; tüm atalet değerleri ağırlık merkezi eksenlerine göredir.
    /// </summary>
    public class GrossProperties
    {
        public double Area { get; set; }
        public double Xc { get; set; }
        public double Yc { get; set; }

        public double Ix { get; set; }
        public double Iy { get; set; }
        public double Ixy { get; set; }

        /// <summary>Asal atalet momentleri (Imax >= Imin).</summary>
        public double Imax { get; set; }
        public double Imin { get; set; }

        /// <summary>x ekseninden (radyan) Imax'ın hesaplandığı eksene olan açı.</summary>
        public double ThetaMax { get; set; }

        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }

        /// <summary>Kesit modülleri (dış lif; orta hat uç noktalarına t/2 eklenir).</summary>
        public double Sx { get; set; }
        public double Sy { get; set; }

        public double Rx { get; set; }
        public double Ry { get; set; }

        /// <summary>Saint-Venant burulma sabiti, Σ(l t^3 / 3).</summary>
        public double J { get; set; }

        /// <summary>Çarpılma sabiti. Açık zincir değilse NaN.</summary>
        public double Cw { get; set; }

        /// <summary>Kayma merkezi (global koordinat). Açık zincir değilse NaN.</summary>
        public double Xs { get; set; }
        public double Ys { get; set; }

        /// <summary>Kayma merkezinin ağırlık merkezine göre konumu.</summary>
        public double Xo { get; set; }
        public double Yo { get; set; }

        /// <summary>Kayma merkezine göre polar atalet yarıçapı.</summary>
        public double Ro { get; set; }

        public double Perimeter { get; set; }
    }

    public static class GrossPropertiesCalculator
    {
        /// <summary>Kesit özelliklerini orta hat (çizgi eleman) yöntemiyle hesaplar.</summary>
        public static GrossProperties Calculate(Section s)
        {
            if (s == null || s.Elements.Count == 0)
                throw new ArgumentException("Kesit eleman içermiyor.");

            var g = new GrossProperties();

            double a = 0, qx = 0, qy = 0, ixx0 = 0, iyy0 = 0, ixy0 = 0, j = 0, per = 0;
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            double maxT = 0;

            foreach (var e in s.Elements)
            {
                Node n1 = s.GetNode(e.StartNodeId), n2 = s.GetNode(e.EndNodeId);
                double l = Dist(n1, n2), t = e.Thickness;
                double lt = l * t;
                a += lt;
                per += l;
                qx += lt * (n1.Y + n2.Y) / 2.0;
                qy += lt * (n1.X + n2.X) / 2.0;
                ixx0 += lt * (n1.Y * n1.Y + n1.Y * n2.Y + n2.Y * n2.Y) / 3.0;
                iyy0 += lt * (n1.X * n1.X + n1.X * n2.X + n2.X * n2.X) / 3.0;
                ixy0 += lt * (2 * n1.X * n1.Y + n1.X * n2.Y + n2.X * n1.Y + 2 * n2.X * n2.Y) / 6.0;
                j += l * t * t * t / 3.0;
                minX = Math.Min(minX, Math.Min(n1.X, n2.X));
                maxX = Math.Max(maxX, Math.Max(n1.X, n2.X));
                minY = Math.Min(minY, Math.Min(n1.Y, n2.Y));
                maxY = Math.Max(maxY, Math.Max(n1.Y, n2.Y));
                maxT = Math.Max(maxT, t);
            }

            g.Area = a;
            g.Perimeter = per;
            g.Xc = qy / a;
            g.Yc = qx / a;
            g.Ix = ixx0 - a * g.Yc * g.Yc;
            g.Iy = iyy0 - a * g.Xc * g.Xc;
            g.Ixy = ixy0 - a * g.Xc * g.Yc;
            g.J = j;

            double avg = (g.Ix + g.Iy) / 2.0;
            double rad = Math.Sqrt(Math.Pow((g.Ix - g.Iy) / 2.0, 2) + g.Ixy * g.Ixy);
            g.Imax = avg + rad;
            g.Imin = avg - rad;

            // Doğrultusu (c, s) olan eksen etrafındaki atalet: Ix c^2 + Iy s^2 - 2 Ixy s c
            double alpha = 0.5 * Math.Atan2(2 * g.Ixy, g.Iy - g.Ix);
            double c = Math.Cos(alpha), sn = Math.Sin(alpha);
            double iu = g.Ix * c * c + g.Iy * sn * sn - 2 * g.Ixy * sn * c;
            if (iu < avg) alpha += Math.PI / 2.0;
            if (Math.Abs(g.Ixy) <= 1e-9 * Math.Max(Math.Abs(g.Ix), Math.Abs(g.Iy)))
                alpha = g.Ix >= g.Iy ? 0 : Math.PI / 2.0;
            double deg = alpha * 180.0 / Math.PI;
            while (deg > 90.0) deg -= 180.0;
            while (deg <= -90.0) deg += 180.0;
            g.ThetaMax = deg;

            double half = maxT / 2.0;
            g.MinX = minX - half; g.MaxX = maxX + half;
            g.MinY = minY - half; g.MaxY = maxY + half;
            double cx = Math.Max(g.Xc - g.MinX, g.MaxX - g.Xc);
            double cy = Math.Max(g.Yc - g.MinY, g.MaxY - g.Yc);
            g.Sx = cy > 0 ? g.Ix / cy : double.NaN;
            g.Sy = cx > 0 ? g.Iy / cx : double.NaN;
            g.Rx = Math.Sqrt(g.Ix / a);
            g.Ry = Math.Sqrt(g.Iy / a);

            CalculateWarping(s, g);
            return g;
        }

        /// <summary>Birim uzunluk ağırlığı. Alan mm2, yoğunluk kg/m3 ise sonuç kg/m'dir.</summary>
        public static double WeightPerMeter(GrossProperties g, double densityKgPerM3)
        {
            return g.Area * 1e-6 * densityKgPerM3;
        }

        private static void CalculateWarpingLinked(Section s, GrossProperties g, System.Collections.Generic.List<Tuple<int, int>> chains)
        {
            int n = s.Elements.Count;
            var x1 = new double[n]; var y1 = new double[n];
            var x2 = new double[n]; var y2 = new double[n];
            var w1 = new double[n]; var w2 = new double[n];
            for (int i = 0; i < n; i++)
            {
                Node n1 = s.GetNode(s.Elements[i].StartNodeId), n2 = s.GetNode(s.Elements[i].EndNodeId);
                x1[i] = n1.X - g.Xc; y1[i] = n1.Y - g.Yc;
                x2[i] = n2.X - g.Xc; y2[i] = n2.Y - g.Yc;
            }

            // Her zincirde yerel ω0 hesaplanır; rijit bağ düğümlerinde ω eşitlenecek şekilde zincirler ötelenir.
            for (int c = 0; c < chains.Count; c++)
            {
                double w0 = 0;
                for (int i = chains[c].Item1; i <= chains[c].Item2; i++)
                {
                    w1[i] = w0;
                    w2[i] = w0 + (x1[i] * y2[i] - x2[i] * y1[i]);
                    w0 = w2[i];
                }
            }

            double WAt(int nodeId, out int chain)
            {
                for (int c = 0; c < chains.Count; c++)
                    for (int i = chains[c].Item1; i <= chains[c].Item2; i++)
                    {
                        if (s.Elements[i].StartNodeId == nodeId) { chain = c; return w1[i]; }
                        if (s.Elements[i].EndNodeId == nodeId) { chain = c; return w2[i]; }
                    }
                chain = -1; return 0;
            }

            double wa = WAt(s.LinkNodeA, out int ca);
            double wb = WAt(s.LinkNodeB, out int cb);
            if (ca < 0 || cb < 0 || ca == cb) return;
            // Bağ, A'dan B'ye sıfır kalınlıklı bir eleman gibi ele alınır: ω_B = ω_A + (xA*yB - xB*yA).
            Node na = s.GetNode(s.LinkNodeA), nb = s.GetNode(s.LinkNodeB);
            double xa = na.X - g.Xc, ya = na.Y - g.Yc, xb = nb.X - g.Xc, yb = nb.Y - g.Yc;
            double shift = wa - wb + (xa * yb - xb * ya);
            for (int i = chains[cb].Item1; i <= chains[cb].Item2; i++) { w1[i] += shift; w2[i] += shift; }

            double iwx = 0, iwy = 0, iw = 0;
            for (int i = 0; i < n; i++)
            {
                double l = Math.Sqrt(Math.Pow(x2[i] - x1[i], 2) + Math.Pow(y2[i] - y1[i], 2));
                double lt = l * s.Elements[i].Thickness;
                double wm = (w1[i] + w2[i]) / 2, xm = (x1[i] + x2[i]) / 2, ym = (y1[i] + y2[i]) / 2;
                iwx += lt / 6.0 * (w1[i] * x1[i] + 4 * wm * xm + w2[i] * x2[i]);
                iwy += lt / 6.0 * (w1[i] * y1[i] + 4 * wm * ym + w2[i] * y2[i]);
                iw += lt * wm;
            }

            double d = g.Ix * g.Iy - g.Ixy * g.Ixy;
            double a = (g.Iy * iwy - g.Ixy * iwx) / d;
            double b = (g.Ixy * iwy - g.Ix * iwx) / d;
            double wbar = iw / g.Area;

            double cw = 0;
            for (int i = 0; i < n; i++)
            {
                double l = Math.Sqrt(Math.Pow(x2[i] - x1[i], 2) + Math.Pow(y2[i] - y1[i], 2));
                double lt = l * s.Elements[i].Thickness;
                double f1 = w1[i] - a * y1[i] + b * x1[i] - wbar;
                double f2 = w2[i] - a * y2[i] + b * x2[i] - wbar;
                double fm = (f1 + f2) / 2;
                cw += lt / 6.0 * (f1 * f1 + 4 * fm * fm + f2 * f2);
            }

            g.Cw = cw;
            g.Xo = a;
            g.Yo = b;
            g.Xs = g.Xc + a;
            g.Ys = g.Yc + b;
            g.Ro = Math.Sqrt((g.Ix + g.Iy) / g.Area + a * a + b * b);
        }

        private static void CalculateWarping(Section s, GrossProperties g)
        {
            g.Cw = g.Xs = g.Ys = g.Xo = g.Yo = g.Ro = double.NaN;
            var chains = SplitChains(s);
            if (chains == null) return;
            if (s.LinkNodeA > 0 && s.LinkNodeB > 0 && chains.Count > 1)
            {
                CalculateWarpingLinked(s, g, chains);
                return;
            }

            int n = s.Elements.Count;
            // Kutup ağırlık merkezinde; her zincir kendi içinde ω0 = 0'dan başlar.
            var x1 = new double[n]; var y1 = new double[n];
            var x2 = new double[n]; var y2 = new double[n];
            var w1 = new double[n]; var w2 = new double[n];
            for (int i = 0; i < n; i++)
            {
                Node n1 = s.GetNode(s.Elements[i].StartNodeId), n2 = s.GetNode(s.Elements[i].EndNodeId);
                x1[i] = n1.X - g.Xc; y1[i] = n1.Y - g.Yc;
                x2[i] = n2.X - g.Xc; y2[i] = n2.Y - g.Yc;
            }

            // Zincir başına: ω0, alan, statik momentler ve yerel atalet (zincir ağırlık merkezine göre).
            var wbar = new double[chains.Count];
            var xbar = new double[chains.Count];
            var ybar = new double[chains.Count];
            double sxw = 0, syw = 0, sIx = 0, sIy = 0, sIxy = 0;
            for (int c = 0; c < chains.Count; c++)
            {
                int from = chains[c].Item1, to = chains[c].Item2;
                double ap = 0, qxp = 0, qyp = 0, ixx = 0, iyy = 0, ixy = 0, iw = 0, iwxp = 0, iwyp = 0;
                double w0 = 0;
                for (int i = from; i <= to; i++)
                {
                    w1[i] = w0;
                    w2[i] = w0 + (x1[i] * y2[i] - x2[i] * y1[i]);
                    w0 = w2[i];

                    double l = Math.Sqrt(Math.Pow(x2[i] - x1[i], 2) + Math.Pow(y2[i] - y1[i], 2));
                    double lt = l * s.Elements[i].Thickness;
                    double wm = (w1[i] + w2[i]) / 2, xm = (x1[i] + x2[i]) / 2, ym = (y1[i] + y2[i]) / 2;
                    ap += lt; qxp += lt * ym; qyp += lt * xm; iw += lt * wm;
                    ixx += lt * (y1[i] * y1[i] + y1[i] * y2[i] + y2[i] * y2[i]) / 3.0;
                    iyy += lt * (x1[i] * x1[i] + x1[i] * x2[i] + x2[i] * x2[i]) / 3.0;
                    ixy += lt * (2 * x1[i] * y1[i] + x1[i] * y2[i] + x2[i] * y1[i] + 2 * x2[i] * y2[i]) / 6.0;
                    iwxp += lt / 6.0 * (w1[i] * x1[i] + 4 * wm * xm + w2[i] * x2[i]);
                    iwyp += lt / 6.0 * (w1[i] * y1[i] + 4 * wm * ym + w2[i] * y2[i]);
                }
                xbar[c] = qyp / ap; ybar[c] = qxp / ap; wbar[c] = iw / ap;
                sxw += iwxp - wbar[c] * qyp;
                syw += iwyp - wbar[c] * qxp;
                sIx += ixx - ap * ybar[c] * ybar[c];
                sIy += iyy - ap * xbar[c] * xbar[c];
                sIxy += ixy - ap * xbar[c] * ybar[c];
            }

            double d = sIx * sIy - sIxy * sIxy;
            double a = (sIy * syw - sIxy * sxw) / d;
            double b = (sIxy * syw - sIx * sxw) / d;

            double cw = 0;
            for (int c = 0; c < chains.Count; c++)
            {
                double mean = wbar[c] - a * ybar[c] + b * xbar[c];
                for (int i = chains[c].Item1; i <= chains[c].Item2; i++)
                {
                    double l = Math.Sqrt(Math.Pow(x2[i] - x1[i], 2) + Math.Pow(y2[i] - y1[i], 2));
                    double lt = l * s.Elements[i].Thickness;
                    double f1 = w1[i] - a * y1[i] + b * x1[i] - mean;
                    double f2 = w2[i] - a * y2[i] + b * x2[i] - mean;
                    double fm = (f1 + f2) / 2;
                    cw += lt / 6.0 * (f1 * f1 + 4 * fm * fm + f2 * f2);
                }
            }

            g.Cw = cw;
            g.Xo = a;
            g.Yo = b;
            g.Xs = g.Xc + a;
            g.Ys = g.Yc + b;
            g.Ro = Math.Sqrt((g.Ix + g.Iy) / g.Area + a * a + b * b);
        }

        /// <summary>Ardışık bağlı elemanları açık zincirlere böler; kapalı zincir varsa null döner.</summary>
        private static System.Collections.Generic.List<Tuple<int, int>> SplitChains(Section s)
        {
            var els = s.Elements;
            var list = new System.Collections.Generic.List<Tuple<int, int>>();
            int start = 0;
            for (int i = 0; i < els.Count; i++)
            {
                bool last = i == els.Count - 1 || els[i].EndNodeId != els[i + 1].StartNodeId;
                if (!last) continue;
                if (els[start].StartNodeId == els[i].EndNodeId) return null;
                list.Add(Tuple.Create(start, i));
                start = i + 1;
            }
            return list;
        }

        private static double Dist(Node a, Node b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
