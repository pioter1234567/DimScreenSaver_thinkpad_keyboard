using System.Windows.Forms;
using System.Drawing;

namespace DimScreenSaver
{
    partial class FormOptions
    {
        private System.ComponentModel.IContainer components = null;



        private void InitializeComponent()
        {
            this.lblKeyboardBacklight = new System.Windows.Forms.Label();
            this.groupBoxTest = new System.Windows.Forms.GroupBox();
            this.btnSet0 = new System.Windows.Forms.Button();
            this.btnSet1 = new System.Windows.Forms.Button();
            this.btnSet2 = new System.Windows.Forms.Button();
            this.groupBoxMode = new System.Windows.Forms.GroupBox();
            this.panel_z_regulacja = new System.Windows.Forms.Panel();
            this.rbForceLevel = new System.Windows.Forms.RadioButton();
            this.rbUseBrightnessMap = new System.Windows.Forms.RadioButton();
            this.cmbForceLevel = new System.Windows.Forms.ComboBox();
            this.btnResetDefaults = new System.Windows.Forms.Button();
            this.lblHeaderOpis = new System.Windows.Forms.Label();
            this.lblHeaderOd = new System.Windows.Forms.Label();
            this.lblHeaderDo = new System.Windows.Forms.Label();
            this.lbl0 = new System.Windows.Forms.Label();
            this.nud0min = new System.Windows.Forms.NumericUpDown();
            this.nud0max = new System.Windows.Forms.NumericUpDown();
            this.lbl1 = new System.Windows.Forms.Label();
            this.nud1min = new System.Windows.Forms.NumericUpDown();
            this.nud1max = new System.Windows.Forms.NumericUpDown();
            this.lbl2 = new System.Windows.Forms.Label();
            this.nud2min = new System.Windows.Forms.NumericUpDown();
            this.nud2max = new System.Windows.Forms.NumericUpDown();
            this.lblCurrentBrightness = new System.Windows.Forms.Label();
            this.groupBoxBacklight = new System.Windows.Forms.GroupBox();
            this.groupBoxCurrentBrightness = new System.Windows.Forms.GroupBox();
            this.btnClose = new System.Windows.Forms.Button();
            this.chkAutoClose = new System.Windows.Forms.CheckBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.brightnessEditor = new DimScreenSaver.BrightnessLevelEditor();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.groupBoxTest.SuspendLayout();
            this.groupBoxMode.SuspendLayout();
            this.panel_z_regulacja.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nud0min)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud0max)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud1min)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud1max)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud2min)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud2max)).BeginInit();
            this.groupBoxBacklight.SuspendLayout();
            this.groupBoxCurrentBrightness.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblKeyboardBacklight
            // 
            this.lblKeyboardBacklight.AutoSize = true;
            this.lblKeyboardBacklight.Font = new System.Drawing.Font("Segoe UI", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.lblKeyboardBacklight.Location = new System.Drawing.Point(48, 16);
            this.lblKeyboardBacklight.Name = "lblKeyboardBacklight";
            this.lblKeyboardBacklight.Size = new System.Drawing.Size(39, 45);
            this.lblKeyboardBacklight.TabIndex = 31;
            this.lblKeyboardBacklight.Text = "X";
            // 
            // groupBoxTest
            // 
            this.groupBoxTest.BackColor = System.Drawing.Color.Transparent;
            this.groupBoxTest.Controls.Add(this.btnSet0);
            this.groupBoxTest.Controls.Add(this.btnSet1);
            this.groupBoxTest.Controls.Add(this.btnSet2);
            this.groupBoxTest.Location = new System.Drawing.Point(267, 6);
            this.groupBoxTest.Name = "groupBoxTest";
            this.groupBoxTest.Size = new System.Drawing.Size(123, 66);
            this.groupBoxTest.TabIndex = 4;
            this.groupBoxTest.TabStop = false;
            this.groupBoxTest.Text = "Test podświetlenia";
            // 
            // btnSet0
            // 
            this.btnSet0.BackColor = System.Drawing.Color.Black;
            this.btnSet0.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSet0.FlatAppearance.BorderSize = 0;
            this.btnSet0.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSet0.Location = new System.Drawing.Point(12, 20);
            this.btnSet0.Name = "btnSet0";
            this.btnSet0.Size = new System.Drawing.Size(32, 32);
            this.btnSet0.TabIndex = 0;
            this.btnSet0.UseVisualStyleBackColor = false;
            this.btnSet0.Click += new System.EventHandler(this.BtnSet0_Click);
            // 
            // btnSet1
            // 
            this.btnSet1.BackColor = System.Drawing.Color.Black;
            this.btnSet1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSet1.FlatAppearance.BorderSize = 0;
            this.btnSet1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSet1.Location = new System.Drawing.Point(51, 20);
            this.btnSet1.Name = "btnSet1";
            this.btnSet1.Size = new System.Drawing.Size(32, 32);
            this.btnSet1.TabIndex = 1;
            this.btnSet1.UseVisualStyleBackColor = false;
            this.btnSet1.Click += new System.EventHandler(this.BtnSet1_Click);
            // 
            // btnSet2
            // 
            this.btnSet2.BackColor = System.Drawing.Color.Black;
            this.btnSet2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.btnSet2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSet2.FlatAppearance.BorderSize = 0;
            this.btnSet2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSet2.Location = new System.Drawing.Point(90, 20);
            this.btnSet2.Name = "btnSet2";
            this.btnSet2.Size = new System.Drawing.Size(32, 32);
            this.btnSet2.TabIndex = 2;
            this.btnSet2.UseVisualStyleBackColor = false;
            this.btnSet2.Click += new System.EventHandler(this.BtnSet2_Click);
            // 
            // groupBoxMode
            // 
            this.groupBoxMode.Controls.Add(this.panel_z_regulacja);
            this.groupBoxMode.Controls.Add(this.rbForceLevel);
            this.groupBoxMode.Controls.Add(this.rbUseBrightnessMap);
            this.groupBoxMode.Controls.Add(this.cmbForceLevel);
            this.groupBoxMode.Location = new System.Drawing.Point(9, 88);
            this.groupBoxMode.Name = "groupBoxMode";
            this.groupBoxMode.Size = new System.Drawing.Size(381, 163);
            this.groupBoxMode.TabIndex = 5;
            this.groupBoxMode.TabStop = false;
            this.groupBoxMode.Text = "Tryb sterowania klawiaturą";
            // 
            // panel_z_regulacja
            // 
            this.panel_z_regulacja.BackColor = System.Drawing.Color.Black;
            this.panel_z_regulacja.Controls.Add(this.brightnessEditor);
            this.panel_z_regulacja.Location = new System.Drawing.Point(31, 83);
            this.panel_z_regulacja.Name = "panel_z_regulacja";
            this.panel_z_regulacja.Size = new System.Drawing.Size(319, 63);
            this.panel_z_regulacja.TabIndex = 3;
            // 
            // rbForceLevel
            // 
            this.rbForceLevel.BackColor = System.Drawing.Color.Transparent;
            this.rbForceLevel.Location = new System.Drawing.Point(10, 20);
            this.rbForceLevel.Name = "rbForceLevel";
            this.rbForceLevel.Size = new System.Drawing.Size(222, 24);
            this.rbForceLevel.TabIndex = 0;
            this.rbForceLevel.Text = "Zawsze podświetlaj klawiaturę na poziom:";
            this.rbForceLevel.UseVisualStyleBackColor = false;
            // 
            // rbUseBrightnessMap
            // 
            this.rbUseBrightnessMap.BackColor = System.Drawing.Color.Transparent;
            this.rbUseBrightnessMap.Checked = true;
            this.rbUseBrightnessMap.Location = new System.Drawing.Point(10, 50);
            this.rbUseBrightnessMap.Name = "rbUseBrightnessMap";
            this.rbUseBrightnessMap.Size = new System.Drawing.Size(274, 24);
            this.rbUseBrightnessMap.TabIndex = 1;
            this.rbUseBrightnessMap.TabStop = true;
            this.rbUseBrightnessMap.Text = "Podświetlaj klawiaturę na podstawie progów jasności";
            this.rbUseBrightnessMap.UseVisualStyleBackColor = false;
            this.rbUseBrightnessMap.CheckedChanged += new System.EventHandler(this.RbUseBrightnessMap_CheckedChanged);
            // 
            // cmbForceLevel
            // 
            this.cmbForceLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbForceLevel.Items.AddRange(new object[] {
            "0 – wyłączaj",
            "1 – lekkie",
            "2 – pełne"});
            this.cmbForceLevel.Location = new System.Drawing.Point(251, 20);
            this.cmbForceLevel.Name = "cmbForceLevel";
            this.cmbForceLevel.Size = new System.Drawing.Size(115, 21);
            this.cmbForceLevel.TabIndex = 2;
            this.cmbForceLevel.Visible = false;
            // 
            // btnResetDefaults
            // 
            this.btnResetDefaults.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnResetDefaults.Location = new System.Drawing.Point(169, 258);
            this.btnResetDefaults.Name = "btnResetDefaults";
            this.btnResetDefaults.Size = new System.Drawing.Size(104, 28);
            this.btnResetDefaults.TabIndex = 5;
            this.btnResetDefaults.Text = "Domyślne";
            this.btnResetDefaults.Click += new System.EventHandler(this.BtnResetDefaults_Click);
            // 
            // lblHeaderOpis
            // 
            this.lblHeaderOpis.Location = new System.Drawing.Point(0, 0);
            this.lblHeaderOpis.Name = "lblHeaderOpis";
            this.lblHeaderOpis.Size = new System.Drawing.Size(100, 23);
            this.lblHeaderOpis.TabIndex = 0;
            // 
            // lblHeaderOd
            // 
            this.lblHeaderOd.Location = new System.Drawing.Point(0, 0);
            this.lblHeaderOd.Name = "lblHeaderOd";
            this.lblHeaderOd.Size = new System.Drawing.Size(100, 23);
            this.lblHeaderOd.TabIndex = 0;
            // 
            // lblHeaderDo
            // 
            this.lblHeaderDo.Location = new System.Drawing.Point(0, 0);
            this.lblHeaderDo.Name = "lblHeaderDo";
            this.lblHeaderDo.Size = new System.Drawing.Size(100, 23);
            this.lblHeaderDo.TabIndex = 0;
            // 
            // lbl0
            // 
            this.lbl0.Location = new System.Drawing.Point(0, 0);
            this.lbl0.Name = "lbl0";
            this.lbl0.Size = new System.Drawing.Size(100, 23);
            this.lbl0.TabIndex = 0;
            // 
            // nud0min
            // 
            this.nud0min.Location = new System.Drawing.Point(0, 0);
            this.nud0min.Name = "nud0min";
            this.nud0min.Size = new System.Drawing.Size(120, 20);
            this.nud0min.TabIndex = 0;
            // 
            // nud0max
            // 
            this.nud0max.Location = new System.Drawing.Point(0, 0);
            this.nud0max.Name = "nud0max";
            this.nud0max.Size = new System.Drawing.Size(120, 20);
            this.nud0max.TabIndex = 0;
            // 
            // lbl1
            // 
            this.lbl1.Location = new System.Drawing.Point(0, 0);
            this.lbl1.Name = "lbl1";
            this.lbl1.Size = new System.Drawing.Size(100, 23);
            this.lbl1.TabIndex = 0;
            // 
            // nud1min
            // 
            this.nud1min.Location = new System.Drawing.Point(0, 0);
            this.nud1min.Name = "nud1min";
            this.nud1min.Size = new System.Drawing.Size(120, 20);
            this.nud1min.TabIndex = 0;
            // 
            // nud1max
            // 
            this.nud1max.Location = new System.Drawing.Point(0, 0);
            this.nud1max.Name = "nud1max";
            this.nud1max.Size = new System.Drawing.Size(120, 20);
            this.nud1max.TabIndex = 0;
            // 
            // lbl2
            // 
            this.lbl2.Location = new System.Drawing.Point(0, 0);
            this.lbl2.Name = "lbl2";
            this.lbl2.Size = new System.Drawing.Size(100, 23);
            this.lbl2.TabIndex = 0;
            // 
            // nud2min
            // 
            this.nud2min.Location = new System.Drawing.Point(0, 0);
            this.nud2min.Name = "nud2min";
            this.nud2min.Size = new System.Drawing.Size(120, 20);
            this.nud2min.TabIndex = 0;
            // 
            // nud2max
            // 
            this.nud2max.Location = new System.Drawing.Point(0, 0);
            this.nud2max.Name = "nud2max";
            this.nud2max.Size = new System.Drawing.Size(120, 20);
            this.nud2max.TabIndex = 0;
            // 
            // lblCurrentBrightness
            // 
            this.lblCurrentBrightness.AutoSize = true;
            this.lblCurrentBrightness.BackColor = System.Drawing.Color.Transparent;
            this.lblCurrentBrightness.Font = new System.Drawing.Font("Segoe UI", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.lblCurrentBrightness.Location = new System.Drawing.Point(16, 16);
            this.lblCurrentBrightness.Name = "lblCurrentBrightness";
            this.lblCurrentBrightness.Size = new System.Drawing.Size(84, 45);
            this.lblCurrentBrightness.TabIndex = 32;
            this.lblCurrentBrightness.Text = "XX%";
            // 
            // groupBoxBacklight
            // 
            this.groupBoxBacklight.BackColor = System.Drawing.Color.Transparent;
            this.groupBoxBacklight.Controls.Add(this.lblKeyboardBacklight);
            this.groupBoxBacklight.Location = new System.Drawing.Point(138, 6);
            this.groupBoxBacklight.Name = "groupBoxBacklight";
            this.groupBoxBacklight.Size = new System.Drawing.Size(123, 66);
            this.groupBoxBacklight.TabIndex = 5;
            this.groupBoxBacklight.TabStop = false;
            this.groupBoxBacklight.Text = "Aktualne podświetlenie";
            // 
            // groupBoxCurrentBrightness
            // 
            this.groupBoxCurrentBrightness.BackColor = System.Drawing.Color.Transparent;
            this.groupBoxCurrentBrightness.Controls.Add(this.lblCurrentBrightness);
            this.groupBoxCurrentBrightness.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.groupBoxCurrentBrightness.Location = new System.Drawing.Point(9, 6);
            this.groupBoxCurrentBrightness.Name = "groupBoxCurrentBrightness";
            this.groupBoxCurrentBrightness.Size = new System.Drawing.Size(123, 66);
            this.groupBoxCurrentBrightness.TabIndex = 32;
            this.groupBoxCurrentBrightness.TabStop = false;
            this.groupBoxCurrentBrightness.Text = "Aktualna jasność";
            // 
            // btnClose
            // 
            this.btnClose.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnClose.Location = new System.Drawing.Point(284, 258);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(104, 28);
            this.btnClose.TabIndex = 33;
            this.btnClose.Text = "Zamknij";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.Button1_Click);
            // 
            // chkAutoClose
            // 
            this.chkAutoClose.AutoSize = true;
            this.chkAutoClose.BackColor = System.Drawing.SystemColors.ControlLight;
            this.chkAutoClose.Checked = true;
            this.chkAutoClose.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAutoClose.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.chkAutoClose.Location = new System.Drawing.Point(56, 4);
            this.chkAutoClose.Name = "chkAutoClose";
            this.chkAutoClose.Size = new System.Drawing.Size(312, 17);
            this.chkAutoClose.TabIndex = 35;
            this.chkAutoClose.Text = "Automatycznie zamknij to okno po 3 minutach nieaktywności";
            this.chkAutoClose.UseVisualStyleBackColor = false;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panel1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.panel1.Controls.Add(this.chkAutoClose);
            this.panel1.Location = new System.Drawing.Point(-7, 292);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(416, 27);
            this.panel1.TabIndex = 36;
            // 
            // brightnessEditor
            // 
            this.brightnessEditor.BackColor = System.Drawing.Color.Black;
            this.brightnessEditor.Location = new System.Drawing.Point(9, 11);
            this.brightnessEditor.Margin = new System.Windows.Forms.Padding(10);
            this.brightnessEditor.Name = "brightnessEditor";
            this.brightnessEditor.Padding = new System.Windows.Forms.Padding(10);
            this.brightnessEditor.Size = new System.Drawing.Size(300, 46);
            this.brightnessEditor.TabIndex = 6;
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.BackColor = System.Drawing.Color.Transparent;
            this.linkLabel1.Location = new System.Drawing.Point(12, 258);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(124, 13);
            this.linkLabel1.TabIndex = 34;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "Buy author a coffee :) ☕";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel1_LinkClicked);
            // 
            // FormOptions
            // 
            this.ClientSize = new System.Drawing.Size(400, 315);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.btnResetDefaults);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.groupBoxCurrentBrightness);
            this.Controls.Add(this.groupBoxBacklight);
            this.Controls.Add(this.groupBoxTest);
            this.Controls.Add(this.groupBoxMode);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormOptions";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Opcje...";
            this.Load += new System.EventHandler(this.FormOptions_Load);
            this.groupBoxTest.ResumeLayout(false);
            this.groupBoxMode.ResumeLayout(false);
            this.panel_z_regulacja.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.nud0min)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud0max)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud1min)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud1max)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud2min)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nud2max)).EndInit();
            this.groupBoxBacklight.ResumeLayout(false);
            this.groupBoxBacklight.PerformLayout();
            this.groupBoxCurrentBrightness.ResumeLayout(false);
            this.groupBoxCurrentBrightness.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        private System.Windows.Forms.GroupBox groupBoxTest;
        private System.Windows.Forms.Button btnSet0;
        private System.Windows.Forms.Button btnSet1;
        private System.Windows.Forms.Button btnSet2;
        private System.Windows.Forms.GroupBox groupBoxMode;
        private System.Windows.Forms.RadioButton rbForceLevel;
        private System.Windows.Forms.RadioButton rbUseBrightnessMap;
        private System.Windows.Forms.ComboBox cmbForceLevel;
       // private System.Windows.Forms.TableLayoutPanel tblBrightnessRanges;
        private System.Windows.Forms.NumericUpDown nud0min, nud0max, nud1min, nud1max, nud2min, nud2max;
        private System.Windows.Forms.Label lbl0;
        private System.Windows.Forms.Label lbl1;
        private System.Windows.Forms.Label lbl2;
        private Button btnResetDefaults;
        private Label lblHeaderOpis;
        private Label lblHeaderOd;
        private Label lblHeaderDo;
        private System.Windows.Forms.Label lblKeyboardBacklight;
        private Label lblCurrentBrightness;
        private GroupBox groupBoxBacklight;
        private GroupBox groupBoxCurrentBrightness;
        private Button btnClose;
        private Panel panel_z_regulacja;
        private BrightnessLevelEditor brightnessEditor;
        private CheckBox chkAutoClose;
        private Panel panel1;
        private LinkLabel linkLabel1;
    }
}
