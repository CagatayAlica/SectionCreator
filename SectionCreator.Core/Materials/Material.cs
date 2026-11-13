using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SectionCreator.Core.Materials
{
    /// <summary>Birimler: gerilim ve modüller MPa; Density kg/m3 (JSON'da yoksa 7850).</summary>
    public class Material
    {
        public string Name { get; set; }
        public double ElasticModulus { get; set; } = 203000;
        public double PoissonRatio { get; set; } = 0.3;
        public double? ShearModulus { get; set; }
        public double YieldStress { get; set; }
        public double UltimateStress { get; set; }
        public double Density { get; set; } = 7850;

        /// <summary>JSON'da ShearModulus yoksa E / (2(1+nu)) ile hesaplanır.</summary>
        [JsonIgnore]
        public double G => ShearModulus ?? ElasticModulus / (2.0 * (1.0 + PoissonRatio));
    }

    public class MaterialLibrary
    {
        public List<Material> Materials { get; set; } = new List<Material>();

        public Material Find(string name)
        {
            return Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public static MaterialLibrary Load(string path)
        {
            string text = File.ReadAllText(path);
            MaterialLibrary lib;
            if (text.TrimStart().StartsWith("["))
                lib = new MaterialLibrary { Materials = JsonConvert.DeserializeObject<List<Material>>(text) };
            else
                lib = JsonConvert.DeserializeObject<MaterialLibrary>(text);
            if (lib == null || lib.Materials == null)
                throw new InvalidDataException("Malzeme dosyası geçersiz: " + path);
            return lib;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
