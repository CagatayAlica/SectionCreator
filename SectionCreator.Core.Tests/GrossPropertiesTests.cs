using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SectionCreator.Core.Geometry;
using SectionCreator.Core.Properties;
using SectionCreator.Core.Templates;

namespace SectionCreator.Core.Tests
{
    [TestClass]
    public class GrossPropertiesTests
    {
        [TestMethod]
        public void FlatPlate_MatchesClosedForm()
        {
            const double l = 100, t = 2;
            var s = new Section { Thickness = t };
            var n1 = s.AddNode(0, 0);
            var n2 = s.AddNode(0, l);
            s.AddElement(n1, n2, ElementKind.Web);

            var g = GrossPropertiesCalculator.Calculate(s);

            Assert.AreEqual(l * t, g.Area, 1e-9);
            Assert.AreEqual(l / 2, g.Yc, 1e-9);
            Assert.AreEqual(t * l * l * l / 12.0, g.Ix, 1e-6);
            Assert.AreEqual(0, g.Iy, 1e-9);
            Assert.AreEqual(l * t * t * t / 3.0, g.J, 1e-9);
        }

        [TestMethod]
        public void CTemplate_ElementCount_And_Divisions()
        {
            var s = SectionTemplateBuilder.Build(TemplateType.C, 200, 70, 20, 2, 3);
            // 2 dudak x 2 + 4 köşe x 6 + 2 flanş x 3 + gövde 6
            Assert.AreEqual(4 + 24 + 6 + 6, s.Elements.Count);
            Assert.AreEqual(s.Elements.Count + 1, s.Nodes.Count);
        }

        [TestMethod]
        public void UTemplate_ElementCount()
        {
            var s = SectionTemplateBuilder.Build(TemplateType.U, 200, 70, 0, 2, 3);
            // 2 köşe x 6 + 2 flanş x 3 + gövde 6
            Assert.AreEqual(12 + 6 + 6, s.Elements.Count);
        }

        [TestMethod]
        public void CTemplate_Area_And_Symmetry()
        {
            const double a = 200, b = 70, c = 20, t = 2, r = 3;
            var s = SectionTemplateBuilder.Build(TemplateType.C, a, b, c, t, r);
            var g = GrossPropertiesCalculator.Calculate(s);

            double rc = r + t / 2;
            double h = a - t, w = b - t / 2, lip = c - t / 2;
            double length = (h - 2 * rc) + 2 * (w - 2 * rc) + 2 * (lip - rc) + 4 * (Math.PI / 2 * rc);

            Assert.AreEqual(length * t, g.Area, length * t * 0.005);
            Assert.AreEqual(h / 2, g.Yc, 1e-6);
            Assert.AreEqual(0, g.Ixy, g.Ix * 1e-9);
            Assert.IsTrue(g.Ix > g.Iy);
            Assert.AreEqual(g.Ix, g.Imax, g.Ix * 1e-9);
        }

        [TestMethod]
        public void CTemplate_ShearCenter_LiesOnSymmetryAxis_BehindWeb()
        {
            var s = SectionTemplateBuilder.Build(TemplateType.C, 200, 70, 20, 2, 3);
            var g = GrossPropertiesCalculator.Calculate(s);

            Assert.AreEqual(g.Yc, g.Ys, 1e-6);
            Assert.IsTrue(g.Xs < 0, "Kayma merkezi gövdenin açıklık tarafının tersinde olmalı.");
            Assert.IsTrue(g.Cw > 0);
            Assert.IsTrue(g.Ro > 0);
        }

        [TestMethod]
        public void Build_InvalidInput_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => SectionTemplateBuilder.Build(TemplateType.C, 200, 70, 20, 0, 3));
            Assert.ThrowsException<ArgumentException>(() => SectionTemplateBuilder.Build(TemplateType.C, 9, 70, 20, 2, 3));
        }
    }
}
