namespace SectionCreator
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.sectionPanel1 = new SectionCreator.SectionPanel();
            this.SuspendLayout();
            // 
            // sectionPanel1
            // 
            this.sectionPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sectionPanel1.Location = new System.Drawing.Point(0, 0);
            this.sectionPanel1.Name = "sectionPanel1";
            this.sectionPanel1.Size = new System.Drawing.Size(1118, 545);
            this.sectionPanel1.TabIndex = 0;
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1118, 545);
            this.Controls.Add(this.sectionPanel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Section Creator";
            this.ResumeLayout(false);

        }

        #endregion

        private SectionPanel sectionPanel1;
    }
}
