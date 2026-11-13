using System.Collections.Generic;
using SectionCreator.Core.Geometry;
using SectionCreator.Core.Materials;
using SectionCreator.Core.Properties;

namespace SectionCreator.Core.Fsa
{
    /// <summary>Sonlu şerit analizi (FSA) girdisi; ayrı FSA projesi bu modeli tüketir.</summary>
    public class FsaInput
    {
        public Section Section { get; set; }
        public Material Material { get; set; }
        public GrossProperties Gross { get; set; }

        /// <summary>Taranacak yarım dalga boyları (girdi uzunluk biriminde).</summary>
        public IList<double> HalfWavelengths { get; set; } = new List<double>();
    }

    public class FsaPoint
    {
        public double HalfWavelength { get; set; }
        public double LoadFactor { get; set; }
    }

    public class FsaResult
    {
        public IList<FsaPoint> Curve { get; set; } = new List<FsaPoint>();
    }

    /// <summary>FSA projesinin uygulayacağı arayüz.</summary>
    public interface IFiniteStripAnalyzer
    {
        FsaResult Analyze(FsaInput input);
    }
}
