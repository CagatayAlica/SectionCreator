using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SectionCreator.Core.Geometry;
using SectionCreator.Core.Materials;
using SectionCreator.Core.Properties;

namespace SectionCreator.Core.Storage
{
    public class SectionRecord
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string CustomName { get; set; }
        public string Kind { get; set; }
        public double WebHeight { get; set; }
        public double FlangeWidth { get; set; }
        public double LipLength { get; set; }
        public double InnerRadius { get; set; }
        public double Thickness { get; set; }
        public bool DoubleSection { get; set; }
        public double Gap { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public Material Material { get; set; }
        public Section Section { get; set; }
        public GrossProperties Gross { get; set; }
    }

    /// <summary>Kesit kayıtlarını tek bir JSON dosyasında saklar.</summary>
    public class SectionStore
    {
        public List<SectionRecord> Records { get; set; } = new List<SectionRecord>();

        public SectionRecord Find(string name)
        {
            return Records.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Aynı adlı kayıt varsa değiştirir.</summary>
        public void AddOrReplace(SectionRecord record)
        {
            Records.RemoveAll(r => string.Equals(r.Name, record.Name, StringComparison.OrdinalIgnoreCase));
            Records.Add(record);
        }

        public static SectionStore Load(string path)
        {
            if (!File.Exists(path)) return new SectionStore();
            var store = JsonConvert.DeserializeObject<SectionStore>(File.ReadAllText(path), Settings);
            return store ?? new SectionStore();
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented, Settings));
        }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            // NaN (açık zincir olmayan kesitlerde Cw vb.) JSON'a yazılabilsin.
            FloatFormatHandling = FloatFormatHandling.String
        };
    }
}
