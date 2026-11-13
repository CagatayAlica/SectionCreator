using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SectionCreator.Core.Dxf;
using SectionCreator.Core.Geometry;
using SectionCreator.Core.Materials;
using SectionCreator.Core.Properties;
using SectionCreator.Core.Storage;
using SectionCreator.Core.Templates;

namespace SectionCreator
{
    /// <summary>
    /// Geometri girişi (C / U / Custom Import), malzeme seçimi, Gross özellik hesabı ve kesit kütüphanesi paneli.
    /// </summary>
    public partial class SectionPanel : UserControl
    {
        private sealed class LibraryFile
        {
            public string Path { get; set; }
            public override string ToString() => Path == null ? "Browse..." : System.IO.Path.GetFileName(Path);
        }

        private readonly ToolTip _toolTip = new ToolTip();
        private MaterialLibrary _materials = new MaterialLibrary();
        private SectionStore _store = new SectionStore();
        private string _libraryPath;
        private string _dxfPath;
        private SectionRecord _current;
        private SectionRecord _editing;
        private ComboBox _unitBox;
        private double _shownFactor = 1.0;

        private const double MmPerInch = 25.4;
        private const double MpaPerKsi = 6.894757293168;

        private bool Imperial => _unitBox != null && _unitBox.SelectedIndex == 1;
        private double LenFactor => Imperial ? MmPerInch : 1.0;
        private double StressFactor => Imperial ? MpaPerKsi : 1.0;
        private string LenUnit => Imperial ? "in" : "mm";
        private string StressUnit => Imperial ? "ksi" : "MPa";

        private void InitUnitBox()
        {
            _unitBox = Controls.Find("comboBox4", true).OfType<ComboBox>().FirstOrDefault()
                ?? Controls.OfType<ComboBox>().FirstOrDefault(c => c != comboBox1 && c != comboBox2 && c != comboBox3);
            Label unitLabel = null;
            if (_unitBox == null)
            {
                unitLabel = new Label { Text = "Unit", Location = new Point(255, 165) };
                _unitBox = new ComboBox { Location = new Point(305, 162), Size = new Size(110, 21) };
                Controls.Add(unitLabel);
                Controls.Add(_unitBox);
            }
            else
            {
                unitLabel = Controls.OfType<Label>().FirstOrDefault(l => l.Text.Trim() == "Unit");
            }
            AlignLabels(unitLabel);

            _unitBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _unitBox.Items.Clear();
            _unitBox.Items.Add("Metric (mm, MPa)");
            _unitBox.Items.Add("Imperial (in, ksi)");
            _unitBox.SelectedIndex = 0;
            _unitBox.SelectedIndexChanged += (s, e) => UnitChanged();
            UpdateUnitLabels();
        }

        private void AlignLabels(Label unitLabel)
        {
            var pairs = new (Label label, Control box)[]
            {
                (label1, WebHeight), (label2, FlangeWidth), (label3, LipLength),
                (label4, Thickness), (label5, InnerRadius),
                (label7, comboBox2), (label8, comboBox3), (unitLabel, _unitBox)
            };

            foreach (var (label, box) in pairs)
            {
                if (label == null || box == null) continue;
                label.AutoSize = false;
                label.TextAlign = ContentAlignment.MiddleRight;
                label.Height = box.Height;
                label.Top = box.Top;
                label.Width = 96;
                label.Left = box.Left - label.Width - 4;
            }
        }

        private void UnitChanged()
        {
            double newF = LenFactor;
            if (newF == _shownFactor) return;

            foreach (var tb in new[] { WebHeight, FlangeWidth, LipLength, Thickness, InnerRadius, Gap })
            {
                string s = tb.Text.Trim().Replace(',', '.');
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    tb.Text = Math.Round(v * _shownFactor / newF, 4).ToString(CultureInfo.InvariantCulture);
            }

            _shownFactor = newF;
            UpdateUnitLabels();
            UpdatePreview();
        }

        private void UpdateUnitLabels()
        {
            string u = " (" + LenUnit + ")";
            label1.Text = "Web Height" + u;
            label2.Text = "Flange Width" + u;
            label3.Text = "Lip Length" + u;
            label4.Text = "Thickness" + u;
            label5.Text = "Inner Radius" + u;
            GapLabel.Text = "Gap" + u;
        }
        private void UpdatePreview()
        {
            DrawSection(_current);
            ShowGross(_current);
        }

        private void ShowGross(SectionRecord rec)
        {
            var g = rec?.Gross;
            if (g == null)
            {
                GrossProps.Text = string.Empty;
                return;
            }

            double f = LenFactor;
            string u = LenUnit;
            string N(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString("G6", CultureInfo.InvariantCulture);
            string Val(double v, int pow)
            {
                double x = v / Math.Pow(f, pow);
                if (double.IsNaN(x) || double.IsInfinity(x)) return "-";
                if (Imperial || pow > 4) return N(x);
                if (pow <= 2) return x.ToString("F2", CultureInfo.InvariantCulture);
                return Math.Round(x).ToString("F0", CultureInfo.InvariantCulture);
            }
            string L(string name, double v, int pow) => name + ": " + (pow == 1 && Math.Abs(v) < 0.1 ? "0.00" : Val(v, pow)) + " " + u + (pow > 1 ? "^" + pow : "");

            var lines = new System.Collections.Generic.List<string>
            {
                rec.Name,
                string.Empty,
                L("A", g.Area, 2),
                L("Perimeter", g.Perimeter, 1),
                L("Yc", g.Xc, 1),
                L("Zc", g.Yc, 1),
                L("Iy", g.Ix, 4),
                L("Iz", g.Iy, 4),
                L("Iyz", g.Ixy, 4),
                L("Imax", g.Imax, 4),
                L("Imin", g.Imin, 4),
                "ThetaMax: " + N(g.ThetaMax) + " deg",
                L("Sy", g.Sx, 3),
                L("Sz", g.Sy, 3),
                L("ry", g.Rx, 1),
                L("rz", g.Ry, 1),
                L("J", g.J, 4),
                L("Cw", g.Cw, 6),
                L("Ys", g.Xs, 1),
                L("Zs", g.Ys, 1),
                L("Yo", g.Xo, 1),
                L("Zo", g.Yo, 1),
                L("ro", g.Ro, 1)
            };

            if (rec.Material != null)
            {
                double kgPerM = GrossPropertiesCalculator.WeightPerMeter(g, rec.Material.Density);
                lines.Add(Imperial
                    ? "Weight: " + N(kgPerM * 0.671969) + " lb/ft"
                    : "Weight: " + N(kgPerM) + " kg/m");
            }

            GrossProps.Text = string.Join(Environment.NewLine, lines);
        }

        /// <summary>Bir kesit listeye eklendiğinde tetiklenir (FSA gibi sonraki aşamalar için).</summary>
        public event EventHandler<SectionRecord> SectionCalculated;

        public SectionRecord Current => _current;

        /// <summary>Kesit kütüphanesi JSON dosyalarının klasörü.</summary>
        public static string StorageFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sections");

        /// <summary>Malzeme kütüphanesi JSON dosyalarının klasörü.</summary>
        public static string MaterialsFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Materials");

        public SectionPanel()
        {
            InitializeComponent();
            InitChart();
            InitUnitBox();
            DoubleSection.CheckedChanged += (s, e) => UpdateMode();
            GapLabel.AutoSize = false;
            GapLabel.TextAlign = ContentAlignment.MiddleRight;
            GapLabel.Size = new Size(96, Gap.Height);
            GapLabel.Location = new Point(Gap.Left - 100, Gap.Top);
            UpdateMode();
            InitDxfLabel();

            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox2.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox3.DropDownStyle = ComboBoxStyle.DropDownList;
            SectionList.SelectedIndexChanged += (s, e) => SelectListItem();
            comboBox1.SelectionChangeCommitted += (s, e) => BrowseIfRequested(comboBox1, true);
            comboBox2.SelectionChangeCommitted += (s, e) => BrowseIfRequested(comboBox2, false);

            button8.Click += (s, e) => NewLibrary();
            button1.Click += (s, e) => AddSection();
            button2.Click += (s, e) => EditSection();
            button3.Click += (s, e) => RemoveSection();
            button6.Click += (s, e) => ImportDrawing();

            radioButton1.CheckedChanged += (s, e) => UpdateMode();
            radioButton2.CheckedChanged += (s, e) => UpdateMode();
            radioButton3.CheckedChanged += (s, e) => UpdateMode();
            radioButton1.Checked = true;
            UpdateMode();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignMode) return;

            RefreshLibraryFiles(comboBox1, StorageFolder);
            RefreshLibraryFiles(comboBox2, MaterialsFolder);
            if (comboBox2.SelectedItem is LibraryFile m && m.Path != null) LoadMaterials(m.Path);
            if (comboBox1.SelectedItem is LibraryFile s && s.Path != null) LoadSelectedLibrary();
        }

        // ---- Geometri modu ----

        private void UpdateMode()
        {
            bool c = radioButton1.Checked;
            bool custom = radioButton3.Checked;

            bool dims = !custom;
            WebHeight.Enabled = label1.Enabled = dims;
            FlangeWidth.Enabled = label2.Enabled = dims;
            InnerRadius.Enabled = label5.Enabled = dims;
            LipLength.Enabled = label3.Enabled = c;
            Thickness.Enabled = label4.Enabled = true;
            button6.Enabled = custom;
            Gap.Enabled = GapLabel.Enabled = DoubleSection.Checked;
        }

        // ---- Dosya listeleri (Storage / Material) ----

        private static void RefreshLibraryFiles(ComboBox box, string folder, string select = null)
        {
            string previous = select ?? (box.SelectedItem as LibraryFile)?.Path;
            box.Items.Clear();

            if (Directory.Exists(folder))
            {
                foreach (string f in Directory.GetFiles(folder, "*.json").OrderBy(x => x))
                    box.Items.Add(new LibraryFile { Path = f });
            }

            box.Items.Add(new LibraryFile { Path = null });

            if (previous != null)
            {
                var match = box.Items.Cast<LibraryFile>()
                    .FirstOrDefault(x => string.Equals(x.Path, previous, StringComparison.OrdinalIgnoreCase));
                if (match != null) box.SelectedItem = match;
            }
            if (box.SelectedIndex < 0 && box.Items.Count > 1) box.SelectedIndex = 0;
        }

        private static void SelectFile(ComboBox box, string path)
        {
            var match = box.Items.Cast<LibraryFile>()
                .FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                match = new LibraryFile { Path = path };
                box.Items.Insert(box.Items.Count - 1, match);
            }
            box.SelectedItem = match;
        }

        private void BrowseIfRequested(ComboBox box, bool storage)
        {
            if (!(box.SelectedItem is LibraryFile f)) return;
            if (f.Path != null)
            {
                if (storage) LoadSelectedLibrary(); else LoadMaterials(f.Path);
                return;
            }

            string path;
            using (var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    box.SelectedIndex = box.Items.Count > 1 ? 0 : -1;
                    return;
                }
                path = dlg.FileName;
            }

            SelectFile(box, path);
            if (storage) LoadSelectedLibrary(); else LoadMaterials(path);
        }

        private void InitChart()
        {
            GrossProps.ReadOnly = true;
            GrossProps.ScrollBars = ScrollBars.Vertical;
            GrossProps.Font = new Font(FontFamily.GenericMonospace, 8.25f);
            chart1.Legends.Clear();
            chart1.Series.Clear();
            chart1.Titles.Clear();
            chart1.Resize += (s, e) => UpdateChartScale();

            var area = chart1.ChartAreas[0];
            area.Position = new System.Windows.Forms.DataVisualization.Charting.ElementPosition(0, 0, 100, 100);
            area.InnerPlotPosition = new System.Windows.Forms.DataVisualization.Charting.ElementPosition(8, 4, 88, 90);
            foreach (var ax in new[] { area.AxisX, area.AxisY })
            {
                ax.LabelStyle.Enabled = false;
                ax.MajorGrid.Enabled = false;
                ax.MajorTickMark.Enabled = false;
                ax.LineWidth = 0;
                ax.Title = string.Empty;
            }
        }

        private double _chartW, _chartH;

        private void DrawSection(SectionRecord rec)
        {
            var section = rec?.Section;
            chart1.Series.Clear();
            chart1.Annotations.Clear();
            _chartW = _chartH = 0;
            var area = chart1.ChartAreas[0];

            if (section == null || section.Nodes.Count == 0 || section.Elements.Count == 0)
            {
                area.AxisX.Minimum = area.AxisX.Maximum = double.NaN;
                area.AxisY.Minimum = area.AxisY.Maximum = double.NaN;
                return;
            }

            var series = new System.Windows.Forms.DataVisualization.Charting.Series("Section")
            {
                ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line,
                Color = Color.SteelBlue,
                BorderWidth = 2,
                IsVisibleInLegend = false
            };

            // Elemanlar sırayla bağlıysa tek çizgi, kopukluk varsa boş noktayla ayrılır.
            int lastEnd = -1;
            foreach (var el in section.Elements)
            {
                var a = section.GetNode(el.StartNodeId);
                var b = section.GetNode(el.EndNodeId);
                if (el.StartNodeId != lastEnd)
                {
                    if (lastEnd != -1)
                    {
                        int gap = series.Points.AddXY(double.NaN, double.NaN);
                        series.Points[gap].IsEmpty = true;
                    }
                    series.Points.AddXY(a.X, a.Y);
                }
                series.Points.AddXY(b.X, b.Y);
                lastEnd = el.EndNodeId;
            }
            chart1.Series.Add(series);

            var nodeSeries = new System.Windows.Forms.DataVisualization.Charting.Series("Nodes")
            {
                ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Point,
                Color = Color.Red,
                MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle,
                MarkerSize = 4,
                IsVisibleInLegend = false
            };
            foreach (var nd in section.Nodes) nodeSeries.Points.AddXY(nd.X, nd.Y);
            chart1.Series.Add(nodeSeries);

            double t = section.Thickness;
            double minX = section.Nodes.Min(n => n.X) - t, maxX = section.Nodes.Max(n => n.X) + t;
            double minY = section.Nodes.Min(n => n.Y) - t, maxY = section.Nodes.Max(n => n.Y) + t;

            bool dims = rec.Kind == "C" || rec.Kind == "U";
            if (dims)
            {
                double off = 0.12 * Math.Max(maxX - minX, maxY - minY);
                double x0 = rec.DoubleSection ? rec.Gap / 2 : -t / 2, y0 = -t / 2;
                AddDimension("Web Height", x0 - off, y0, x0 - off, y0 + rec.WebHeight, rec.WebHeight, ContentAlignment.MiddleRight);
                AddDimension("Flange Width", x0, y0 - off, x0 + rec.FlangeWidth, y0 - off, rec.FlangeWidth, ContentAlignment.TopCenter);
                minX = Math.Min(minX, x0 - 2.2 * off);
                minY = Math.Min(minY, y0 - 1.8 * off);
                if (rec.Kind == "C" && rec.LipLength > 0)
                {
                    double lx = x0 + rec.FlangeWidth + off;
                    AddDimension("Lip Length", lx, y0, lx, y0 + rec.LipLength, rec.LipLength, ContentAlignment.MiddleLeft);
                    maxX = Math.Max(maxX, lx + 2.2 * off);
                }
            }
            else
            {
                // Custom Import: en büyük genişlik ve yükseklik (dış yüz, kalınlık dahil).
                double sx0 = section.Nodes.Min(n => n.X) - t / 2, sx1 = section.Nodes.Max(n => n.X) + t / 2;
                double sy0 = section.Nodes.Min(n => n.Y) - t / 2, sy1 = section.Nodes.Max(n => n.Y) + t / 2;
                double off = 0.12 * Math.Max(maxX - minX, maxY - minY);
                AddDimension("Max Height", sx0 - off, sy0, sx0 - off, sy1, sy1 - sy0, ContentAlignment.MiddleRight);
                AddDimension("Max Width", sx0, sy0 - off, sx1, sy0 - off, sx1 - sx0, ContentAlignment.TopCenter);
                minX = Math.Min(minX, sx0 - 2.2 * off);
                minY = Math.Min(minY, sy0 - 1.8 * off);
                _customW = sx1 - sx0;
                _customH = sy1 - sy0;
            }

            if (rec.DoubleSection)
            {
                int half = section.Nodes.Count / 2;
                double outerX = section.Nodes.Take(half).Min(n => n.X) - t / 2;
                double gy = (section.Nodes.Min(n => n.Y) + section.Nodes.Max(n => n.Y)) / 2;
                AddDimension("Gap", outerX - rec.Gap, gy, outerX, gy, rec.Gap, ContentAlignment.TopCenter);
            }

            if (rec.Gross != null)
            {
                double bx0 = section.Nodes.Min(n => n.X) - t, bx1 = section.Nodes.Max(n => n.X) + t;
                double by0 = section.Nodes.Min(n => n.Y) - t, by1 = section.Nodes.Max(n => n.Y) + t;
                double cx = rec.Gross.Xc, cy = rec.Gross.Yc;
                AddAxis("Axis Y", "Y", bx0, cy, bx1, cy, Color.Green, ContentAlignment.MiddleLeft);
                AddAxis("Axis Z", "Z", cx, by0, cx, by1, Color.Blue, ContentAlignment.BottomCenter);
                maxX = Math.Max(maxX, bx1 + 0.1 * (bx1 - bx0));
            }

            // Sol üstteki bilgi kutusu için
            maxY += 0.3 * (maxY - minY);

            AddInfoText(rec, dims);
            _chartW = maxX - minX;
            _chartH = maxY - minY;
            area.AxisX.Minimum = minX;
            area.AxisY.Minimum = minY;
            _chartMinX = minX;
            _chartMinY = minY;
            UpdateChartScale();

            series.BorderWidth = 2;
        }

        private double _chartMinX, _chartMinY;
        private double _customW, _customH;

        private void AddAxis(string name, string text, double x1, double y1, double x2, double y2, Color color, ContentAlignment labelSide)
        {
            var s = new System.Windows.Forms.DataVisualization.Charting.Series(name)
            {
                ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line,
                Color = color,
                BorderWidth = 1,
                IsVisibleInLegend = false
            };
            s.Points.AddXY(x1, y1);
            s.Points.AddXY(x2, y2);
            chart1.Series.Add(s);

            var area = chart1.ChartAreas[0];
            chart1.Annotations.Add(new System.Windows.Forms.DataVisualization.Charting.TextAnnotation
            {
                Text = text,
                AxisX = area.AxisX,
                AxisY = area.AxisY,
                AnchorX = x2,
                AnchorY = y2,
                IsSizeAlwaysRelative = false,
                AnchorAlignment = labelSide,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = color
            });
        }

        private void AddDimension(string name, double x1, double y1, double x2, double y2, double value, ContentAlignment labelSide)
        {
            var s = new System.Windows.Forms.DataVisualization.Charting.Series(name)
            {
                ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line,
                Color = Color.Firebrick,
                BorderWidth = 1,
                BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Dash,
                IsVisibleInLegend = false
            };
            s.Points.AddXY(x1, y1);
            s.Points.AddXY(x2, y2);
            chart1.Series.Add(s);

            var area = chart1.ChartAreas[0];
            var label = new System.Windows.Forms.DataVisualization.Charting.TextAnnotation
            {
                Text = (value / LenFactor).ToString("0.###", CultureInfo.InvariantCulture),
                AxisX = area.AxisX,
                AxisY = area.AxisY,
                AnchorX = (x1 + x2) / 2,
                AnchorY = (y1 + y2) / 2,
                IsSizeAlwaysRelative = false,
                AnchorAlignment = labelSide,
                Font = new Font(Font.FontFamily, 8f),
                ForeColor = Color.Firebrick
            };
            chart1.Annotations.Add(label);
        }

        private void AddInfoText(SectionRecord rec, bool dims)
        {
            string F(double mm) => (mm / LenFactor).ToString("0.###", CultureInfo.InvariantCulture) + " " + LenUnit;
            var lines = new System.Collections.Generic.List<string>();
            if (dims)
            {
                lines.Add("Web Height: " + F(rec.WebHeight));
                lines.Add("Flange Width: " + F(rec.FlangeWidth));
                if (rec.Kind == "C") lines.Add("Lip Length: " + F(rec.LipLength));
                lines.Add("Inner Radius: " + F(rec.InnerRadius));
            }
            else
            {
                lines.Add("Max Width: " + F(_customW));
                lines.Add("Max Height: " + F(_customH));
            }
            lines.Add("Thickness: " + F(rec.Thickness));
            if (rec.Material != null)
                lines.Add("fy: " + (rec.Material.YieldStress / StressFactor).ToString("0.##", CultureInfo.InvariantCulture) + " " + StressUnit);

            var ta = new System.Windows.Forms.DataVisualization.Charting.TextAnnotation
            {
                Text = string.Join("\n", lines),
                IsSizeAlwaysRelative = true,
                X = 1,
                Y = 1,
                Font = new Font(Font.FontFamily, 8f),
                ForeColor = Color.DimGray,
                Alignment = ContentAlignment.TopLeft,
                AnchorAlignment = ContentAlignment.TopLeft
            };
            chart1.Annotations.Add(ta);
        }

        /// <summary>X ve Y eksenlerinde piksel başına mm aynı olacak şekilde eksen aralıklarını ayarlar (ölçekli çizim).</summary>
        private void UpdateChartScale()
        {
            if (_chartW <= 0 || _chartH <= 0) return;

            var area = chart1.ChartAreas[0];
            double plotW = chart1.ClientSize.Width * 0.88;
            double plotH = chart1.ClientSize.Height * 0.90;
            if (plotW <= 0 || plotH <= 0) return;

            double mmPerPx = Math.Max(_chartW / plotW, _chartH / plotH);
            double spanX = mmPerPx * plotW, spanY = mmPerPx * plotH;

            area.AxisX.Minimum = _chartMinX - (spanX - _chartW) / 2;
            area.AxisX.Maximum = area.AxisX.Minimum + spanX;
            area.AxisY.Minimum = _chartMinY - (spanY - _chartH) / 2;
            area.AxisY.Maximum = area.AxisY.Minimum + spanY;
            area.AxisX.Interval = area.AxisY.Interval = NiceInterval(mmPerPx * 50);
        }

        private static double NiceInterval(double raw)
        {
            double pow = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double f = raw / pow;
            return (f < 1.5 ? 1 : f < 3.5 ? 2 : f < 7.5 ? 5 : 10) * pow;
        }

        private void SelectListItem()
        {
            int i = SectionList.SelectedIndex;
            if (i < 0 || i >= _store.Records.Count) return;
            _current = _store.Records[i];
            UpdatePreview();
            if (_editing != null && !ReferenceEquals(_editing, _current)) EndEdit();
            if (_current.Material != null && comboBox3.Items.Contains(_current.Material.Name))
                comboBox3.SelectedItem = _current.Material.Name;
            FillFields(_current);
            if (_current.Kind != "Custom")
            {
                _dxfLabel.Visible = false;
            }
            else
            {
                UpdateDxfLabel();
            }
        }

        private bool FillFields(SectionRecord rec)
        {
            if (rec.Kind == null && !TryRecoverDimensions(rec)) return false;

            radioButton1.Checked = rec.Kind == "C";
            radioButton2.Checked = rec.Kind == "U";
            radioButton3.Checked = rec.Kind == "Custom";
            WebHeight.Text = Fmt(rec.WebHeight);
            FlangeWidth.Text = Fmt(rec.FlangeWidth);
            LipLength.Text = Fmt(rec.LipLength);
            InnerRadius.Text = Fmt(rec.InnerRadius);
            Thickness.Text = Fmt(rec.Thickness);
            SectionName.Text = rec.CustomName ?? string.Empty;
            DoubleSection.Checked = rec.DoubleSection;
            Gap.Text = Fmt(rec.Gap);
            if (rec.Kind == "Custom" && rec.Source != null && rec.Source.StartsWith("DXF: "))
            {
                _dxfPath = rec.Source.Substring(5);
                UpdateDxfLabel();
            }
            return true;
        }

        private Label _dxfLabel;

        private void InitDxfLabel()
        {
            _dxfLabel = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(button6.Left, button6.Bottom + 3),
                Size = new Size(300, 18),
                Visible = false
            };
            Controls.Add(_dxfLabel);
        }

        private void UpdateDxfLabel()
        {
            bool has = !string.IsNullOrEmpty(_dxfPath);
            _dxfLabel.Text = has ? Path.GetFileName(_dxfPath) : string.Empty;
            _toolTip.SetToolTip(_dxfLabel, has ? _dxfPath : string.Empty);
            _dxfLabel.Visible = has;
        }

        private static bool TryRecoverDimensions(SectionRecord rec)
        {
            var m = System.Text.RegularExpressions.Regex.Match(rec.Name ?? "",
                @"^(C|U) ([\d.,]+)x([\d.,]+)(?:x([\d.,]+))?x([\d.,]+)(?: R([\d.,]+))?$");
            if (!m.Success) return false;

            double P(string s) => string.IsNullOrEmpty(s) ? 0
                : double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

            rec.Kind = m.Groups[1].Value;
            rec.WebHeight = P(m.Groups[2].Value);
            rec.FlangeWidth = P(m.Groups[3].Value);
            rec.LipLength = P(m.Groups[4].Value);
            rec.Thickness = P(m.Groups[5].Value);

            if (m.Groups[6].Success) rec.InnerRadius = P(m.Groups[6].Value);
            else if (rec.Section != null && rec.Section.Nodes.Count > 0)
            {
                double top = rec.Section.Nodes.Max(n => n.Y);
                double rc = rec.Section.Nodes.Where(n => Math.Abs(n.Y - top) < 1e-9).Min(n => n.X);
                rec.InnerRadius = Math.Round(rc - rec.Thickness / 2.0, 6);
            }
            return true;
        }

        // ---- Malzeme ----

        private void LoadSelectedMaterialLibrary()
        {
            string path = (comboBox2.SelectedItem as LibraryFile)?.Path;
            if (path == null)
            {
                using (var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    path = dlg.FileName;
                }
                SelectFile(comboBox2, path);
            }
            LoadMaterials(path);
        }

        private void LoadMaterials(string path)
        {
            try
            {
                _materials = MaterialLibrary.Load(path);
                comboBox3.Items.Clear();
                comboBox3.Items.AddRange(_materials.Materials.Select(m => (object)m.Name).ToArray());
                if (comboBox3.Items.Count > 0) comboBox3.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Malzeme dosyası okunamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- Kesit kütüphanesi ----

        private void NewLibrary()
        {
            Directory.CreateDirectory(StorageFolder);
            using (var dlg = new SaveFileDialog
            {
                Title = "Yeni kesit kütüphanesi",
                Filter = "JSON (*.json)|*.json",
                InitialDirectory = StorageFolder,
                FileName = "sections.json"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _store = new SectionStore();
                    _store.Save(dlg.FileName);
                    _libraryPath = dlg.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Kütüphane oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            RefreshLibraryFiles(comboBox1, StorageFolder, _libraryPath);
            SelectFile(comboBox1, _libraryPath);
            ShowList();
        }

        private void LoadSelectedLibrary()
        {
            var file = comboBox1.SelectedItem as LibraryFile;
            if (file == null || file.Path == null)
            {
                MessageBox.Show(this, "Önce listeden bir kütüphane seçin veya New ile oluşturun.", "Kütüphane",                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _store = SectionStore.Load(file.Path);
                _libraryPath = file.Path;
                ShowList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Kesit kütüphanesi okunamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveLibrary()
        {
            if (_libraryPath == null)
            {
                MessageBox.Show(this, "Önce bir kütüphane seçip Load yapın veya New ile oluşturun.", "Kütüphane",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _store.Save(_libraryPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Kütüphane kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowList()
        {
            SectionList.Items.Clear();
            SectionList.Items.AddRange(_store.Records
                .Select(r => (object)(r.Name + (r.Material != null ? "  [" + r.Material.Name + "]" : "")))
                .ToArray());
        }

        // ---- Geometri girişi ve hesap ----

        private void ImportDrawing()
        {
            using (var dlg = new OpenFileDialog { Filter = "DXF (*.dxf)|*.dxf" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _dxfPath = dlg.FileName;
                _toolTip.SetToolTip(button6, _dxfPath);
                UpdateDxfLabel();
            }
        }

        private double Parse(TextBox tb, string name)
        {
            string s = tb.Text.Trim().Replace(',', '.');
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new FormatException(name + " sayısal olmalı.");
            return v * LenFactor;
        }

        private static void MirrorSection(Section s, double gap)
        {
            double xm = s.Nodes.Min(n => n.X) - s.Thickness / 2 - gap / 2;
            int count = s.Nodes.Count;
            int elCount = s.Elements.Count;
            var web = s.Nodes.OrderBy(n => n.X).ThenBy(n => Math.Abs(n.Y - (s.Nodes.Min(m => m.Y) + s.Nodes.Max(m => m.Y)) / 2)).First();
            s.LinkNodeA = web.Id;
            s.LinkNodeB = web.Id + count;
            for (int i = 0; i < count; i++)
                s.AddNode(2 * xm - s.Nodes[i].X, s.Nodes[i].Y);
            for (int i = 0; i < elCount; i++)
            {
                var el = s.Elements[i];
                var e = s.AddElement(s.GetNode(el.StartNodeId + count), s.GetNode(el.EndNodeId + count), el.Kind);
                e.Thickness = el.Thickness;
            }
            foreach (var nd in s.Nodes) nd.X -= xm;
        }

        private SectionRecord Calculate()
        {
            Material mat = _materials.Find(comboBox3.SelectedItem as string);
            if (mat == null) throw new InvalidOperationException("Malzeme seçilmedi.");

            double t = Parse(Thickness, "Thickness");
            Section section;
            string source;
            string kind;
            double a = 0, b = 0, c = 0, r = 0;

            if (radioButton3.Checked)
            {
                if (_dxfPath == null) throw new InvalidOperationException("Önce Import Drawing ile bir DXF dosyası seçin.");
                section = DxfSectionImporter.Import(_dxfPath, t);
                section.Name += " t" + t.ToString(CultureInfo.InvariantCulture);
                source = "DXF: " + _dxfPath;
                kind = "Custom";
            }
            else
            {
                bool isC = radioButton1.Checked;
                a = Parse(WebHeight, "Web Height");
                b = Parse(FlangeWidth, "Flange Width");
                c = isC ? Parse(LipLength, "Lip Length") : 0;
                r = Parse(InnerRadius, "Inner Radius");

                section = SectionTemplateBuilder.Build(isC ? TemplateType.C : TemplateType.U, a, b, c, t, r);
                section.Name += " R" + r.ToString(CultureInfo.InvariantCulture);
                source = isC ? "Template C" : "Template U";
                kind = isC ? "C" : "U";
            }

            section.MaterialName = mat.Name;
            bool dbl = DoubleSection.Checked;
            double gap = 0;
            if (dbl)
            {
                gap = Parse(Gap, "Gap");
                if (gap < 0) throw new FormatException("Gap negatif olamaz.");
                MirrorSection(section, gap);
            }
            string custom = SectionName.Text.Trim();
            if (custom.Length > 0) section.Name = custom + " " + mat.Name;
            if (dbl) section.Name += " Double Gap" + gap.ToString(CultureInfo.InvariantCulture);
            return new SectionRecord
            {
                DoubleSection = dbl,
                Gap = gap,
                Name = section.Name,
                CustomName = custom.Length > 0 ? custom : null,
                Source = source,
                Kind = kind,
                WebHeight = a,
                FlangeWidth = b,
                LipLength = c,
                InnerRadius = r,
                Thickness = t,
                Material = mat,
                Section = section,
                Gross = GrossPropertiesCalculator.Calculate(section)
            };
        }

        private bool IsDuplicate(SectionRecord rec, SectionRecord ignore)
        {
            return _store.Records.Any(x => !ReferenceEquals(x, ignore)
                && string.Equals(x.Name, rec.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Material?.Name, rec.Material?.Name, StringComparison.OrdinalIgnoreCase));
        }

        private void WarnDuplicate()
        {
            MessageBox.Show(this, "Aynı kesit ve malzeme listede zaten mevcut.", "Tekrar eden kesit",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void EditSection()
        {
            if (_editing == null)
            {
                int i = SectionList.SelectedIndex;
                if (i < 0 || i >= _store.Records.Count)
                {
                    MessageBox.Show(this, "Listeden bir kesit seçin.", "Edit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var rec = _store.Records[i];
                if (!FillFields(rec))
                {
                    MessageBox.Show(this, "Bu kayıt eski biçimde, düzenlenemez. Silip yeniden ekleyin.", "Edit",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (rec.Material != null && comboBox3.Items.Contains(rec.Material.Name))
                    comboBox3.SelectedItem = rec.Material.Name;

                _editing = rec;
                button2.Text = "Apply";
                return;
            }

            try
            {
                var updated = Calculate();
                if (IsDuplicate(updated, _editing))
                {
                    WarnDuplicate();
                    return;
                }

                int idx = _store.Records.IndexOf(_editing);
                _store.Records[idx] = updated;
                _current = updated;
                UpdatePreview();
                EndEdit();
                ShowList();
                SaveLibrary();
                SectionList.SelectedIndex = idx;
                SectionCalculated?.Invoke(this, _current);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Hesap hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EndEdit()
        {
            _editing = null;
            button2.Text = "Edit";
        }

        private void RemoveSection()
        {
            int i = SectionList.SelectedIndex;
            if (i < 0 || i >= _store.Records.Count)
            {
                MessageBox.Show(this, "Listeden bir kesit seçin.", "Remove", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ReferenceEquals(_store.Records[i], _editing)) EndEdit();
            _current = null;
                        UpdatePreview();
            _store.Records.RemoveAt(i);
            ShowList();
            SaveLibrary();
        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private string Fmt(double mm) => Math.Round(mm / LenFactor, 4).ToString(CultureInfo.InvariantCulture);

        private void AddSection()
        {
            try
            {
                var rec = Calculate();
                if (IsDuplicate(rec, null))
                {
                    WarnDuplicate();
                    return;
                }

                _current = rec;
                UpdatePreview();
                _store.Records.Add(_current);
                ShowList();
                SaveLibrary();
                SectionCalculated?.Invoke(this, _current);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Hesap hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
