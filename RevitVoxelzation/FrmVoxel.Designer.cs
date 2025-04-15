namespace RevitVoxelzation
{
    partial class FrmVoxel
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.txtVoxSize = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.btnGenerate = new System.Windows.Forms.Button();
            this.prog = new System.Windows.Forms.ProgressBar();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnLoad = new System.Windows.Forms.Button();
            this.btnVisualize = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.btnSaveCSV = new System.Windows.Forms.Button();
            this.btnCompressVoxes = new System.Windows.Forms.Button();
            this.chkShowTriangle = new System.Windows.Forms.CheckBox();
            this.chkDebug = new System.Windows.Forms.CheckBox();
            this.btnGenerateByMesh = new System.Windows.Forms.Button();
            this.txtExportSolid = new System.Windows.Forms.Button();
            this.chkShowBaseVoxel = new System.Windows.Forms.CheckBox();
            this.btnConvert = new System.Windows.Forms.Button();
            this.txtSmallVoxH = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.chkConvertSldByElems = new System.Windows.Forms.CheckBox();
            this.btnVoxGenValidation = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.dgvResult = new System.Windows.Forms.DataGridView();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResult)).BeginInit();
            this.SuspendLayout();
            // 
            // txtVoxSize
            // 
            this.txtVoxSize.Location = new System.Drawing.Point(130, 38);
            this.txtVoxSize.Name = "txtVoxSize";
            this.txtVoxSize.Size = new System.Drawing.Size(81, 28);
            this.txtVoxSize.TabIndex = 1;
            this.txtVoxSize.Text = "200";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(217, 41);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(26, 18);
            this.label2.TabIndex = 0;
            this.label2.Text = "mm";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 41);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(98, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "Voxel Size";
            // 
            // btnGenerate
            // 
            this.btnGenerate.Location = new System.Drawing.Point(5, 76);
            this.btnGenerate.Name = "btnGenerate";
            this.btnGenerate.Size = new System.Drawing.Size(188, 35);
            this.btnGenerate.TabIndex = 2;
            this.btnGenerate.Text = "Generate(Model)";
            this.btnGenerate.UseVisualStyleBackColor = true;
            this.btnGenerate.Click += new System.EventHandler(this.btnGenerate_Click);
            // 
            // prog
            // 
            this.prog.Location = new System.Drawing.Point(6, 828);
            this.prog.Name = "prog";
            this.prog.Size = new System.Drawing.Size(733, 42);
            this.prog.TabIndex = 3;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(413, 76);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(132, 35);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.button1_Click);
            // 
            // btnLoad
            // 
            this.btnLoad.Location = new System.Drawing.Point(5, 117);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(188, 35);
            this.btnLoad.TabIndex = 4;
            this.btnLoad.Text = "Load Voxels";
            this.btnLoad.UseVisualStyleBackColor = true;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);
            // 
            // btnVisualize
            // 
            this.btnVisualize.Location = new System.Drawing.Point(199, 117);
            this.btnVisualize.Name = "btnVisualize";
            this.btnVisualize.Size = new System.Drawing.Size(289, 35);
            this.btnVisualize.TabIndex = 5;
            this.btnVisualize.Text = "Visualize Voxel elements";
            this.btnVisualize.UseVisualStyleBackColor = true;
            this.btnVisualize.Click += new System.EventHandler(this.btnVisualize_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.btnSaveCSV);
            this.groupBox3.Controls.Add(this.btnCompressVoxes);
            this.groupBox3.Controls.Add(this.chkShowTriangle);
            this.groupBox3.Controls.Add(this.chkDebug);
            this.groupBox3.Controls.Add(this.btnLoad);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.txtVoxSize);
            this.groupBox3.Controls.Add(this.btnSave);
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Controls.Add(this.btnGenerateByMesh);
            this.groupBox3.Controls.Add(this.btnGenerate);
            this.groupBox3.Controls.Add(this.btnVisualize);
            this.groupBox3.Location = new System.Drawing.Point(21, 21);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(718, 161);
            this.groupBox3.TabIndex = 9;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Voxelize element";
            // 
            // btnSaveCSV
            // 
            this.btnSaveCSV.Location = new System.Drawing.Point(551, 76);
            this.btnSaveCSV.Name = "btnSaveCSV";
            this.btnSaveCSV.Size = new System.Drawing.Size(148, 35);
            this.btnSaveCSV.TabIndex = 9;
            this.btnSaveCSV.Text = "Save as CSV";
            this.btnSaveCSV.UseVisualStyleBackColor = true;
            this.btnSaveCSV.Click += new System.EventHandler(this.btnSaveCSV_Click);
            // 
            // btnCompressVoxes
            // 
            this.btnCompressVoxes.Location = new System.Drawing.Point(494, 117);
            this.btnCompressVoxes.Name = "btnCompressVoxes";
            this.btnCompressVoxes.Size = new System.Drawing.Size(205, 35);
            this.btnCompressVoxes.TabIndex = 8;
            this.btnCompressVoxes.Text = "Compress Voxels";
            this.btnCompressVoxes.UseVisualStyleBackColor = true;
            this.btnCompressVoxes.Click += new System.EventHandler(this.btnCompressVoxes_Click);
            // 
            // chkShowTriangle
            // 
            this.chkShowTriangle.AutoSize = true;
            this.chkShowTriangle.Location = new System.Drawing.Point(514, 37);
            this.chkShowTriangle.Name = "chkShowTriangle";
            this.chkShowTriangle.Size = new System.Drawing.Size(151, 22);
            this.chkShowTriangle.TabIndex = 7;
            this.chkShowTriangle.Text = "Show Triangle";
            this.chkShowTriangle.UseVisualStyleBackColor = true;
            // 
            // chkDebug
            // 
            this.chkDebug.AutoSize = true;
            this.chkDebug.Location = new System.Drawing.Point(280, 37);
            this.chkDebug.Name = "chkDebug";
            this.chkDebug.Size = new System.Drawing.Size(214, 22);
            this.chkDebug.TabIndex = 6;
            this.chkDebug.Text = "Output single voxels";
            this.chkDebug.UseVisualStyleBackColor = true;
            this.chkDebug.CheckedChanged += new System.EventHandler(this.chkDebug_CheckedChanged);
            // 
            // btnGenerateByMesh
            // 
            this.btnGenerateByMesh.Location = new System.Drawing.Point(199, 76);
            this.btnGenerateByMesh.Name = "btnGenerateByMesh";
            this.btnGenerateByMesh.Size = new System.Drawing.Size(208, 35);
            this.btnGenerateByMesh.TabIndex = 2;
            this.btnGenerateByMesh.Text = "Generate(Load Mesh)";
            this.btnGenerateByMesh.UseVisualStyleBackColor = true;
            this.btnGenerateByMesh.Click += new System.EventHandler(this.btnGenerateByMesh_Click);
            // 
            // txtExportSolid
            // 
            this.txtExportSolid.Location = new System.Drawing.Point(283, 23);
            this.txtExportSolid.Name = "txtExportSolid";
            this.txtExportSolid.Size = new System.Drawing.Size(162, 32);
            this.txtExportSolid.TabIndex = 6;
            this.txtExportSolid.Text = "Convert model";
            this.txtExportSolid.UseVisualStyleBackColor = true;
            this.txtExportSolid.Click += new System.EventHandler(this.txtExportSolid_Click);
            // 
            // chkShowBaseVoxel
            // 
            this.chkShowBaseVoxel.AutoSize = true;
            this.chkShowBaseVoxel.Location = new System.Drawing.Point(463, 109);
            this.chkShowBaseVoxel.Name = "chkShowBaseVoxel";
            this.chkShowBaseVoxel.Size = new System.Drawing.Size(178, 22);
            this.chkShowBaseVoxel.TabIndex = 5;
            this.chkShowBaseVoxel.Text = "Show Base Voxels";
            this.chkShowBaseVoxel.UseVisualStyleBackColor = true;
            // 
            // btnConvert
            // 
            this.btnConvert.Location = new System.Drawing.Point(328, 104);
            this.btnConvert.Name = "btnConvert";
            this.btnConvert.Size = new System.Drawing.Size(129, 30);
            this.btnConvert.TabIndex = 2;
            this.btnConvert.Text = "Run";
            this.btnConvert.UseVisualStyleBackColor = true;
            this.btnConvert.Click += new System.EventHandler(this.btnConvert_Click);
            // 
            // txtSmallVoxH
            // 
            this.txtSmallVoxH.Location = new System.Drawing.Point(176, 104);
            this.txtSmallVoxH.Name = "txtSmallVoxH";
            this.txtSmallVoxH.Size = new System.Drawing.Size(146, 28);
            this.txtSmallVoxH.TabIndex = 1;
            this.txtSmallVoxH.Text = "200";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(18, 107);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(161, 18);
            this.label3.TabIndex = 0;
            this.label3.Text = "Detection Inteval";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.groupBox5);
            this.groupBox4.Controls.Add(this.btnExport);
            this.groupBox4.Controls.Add(this.dgvResult);
            this.groupBox4.Controls.Add(this.chkShowBaseVoxel);
            this.groupBox4.Controls.Add(this.btnConvert);
            this.groupBox4.Controls.Add(this.label3);
            this.groupBox4.Controls.Add(this.txtSmallVoxH);
            this.groupBox4.Location = new System.Drawing.Point(21, 188);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(718, 598);
            this.groupBox4.TabIndex = 14;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Validation";
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.chkConvertSldByElems);
            this.groupBox5.Controls.Add(this.txtExportSolid);
            this.groupBox5.Controls.Add(this.btnVoxGenValidation);
            this.groupBox5.Location = new System.Drawing.Point(12, 27);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(700, 66);
            this.groupBox5.TabIndex = 15;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Convert Model for Validation";
            // 
            // chkConvertSldByElems
            // 
            this.chkConvertSldByElems.AutoSize = true;
            this.chkConvertSldByElems.Location = new System.Drawing.Point(9, 30);
            this.chkConvertSldByElems.Name = "chkConvertSldByElems";
            this.chkConvertSldByElems.Size = new System.Drawing.Size(268, 22);
            this.chkConvertSldByElems.TabIndex = 7;
            this.chkConvertSldByElems.Text = "Genreate Element By Solids";
            this.chkConvertSldByElems.UseVisualStyleBackColor = true;
            // 
            // btnVoxGenValidation
            // 
            this.btnVoxGenValidation.Location = new System.Drawing.Point(451, 23);
            this.btnVoxGenValidation.Name = "btnVoxGenValidation";
            this.btnVoxGenValidation.Size = new System.Drawing.Size(241, 32);
            this.btnVoxGenValidation.TabIndex = 2;
            this.btnVoxGenValidation.Text = "Generate Voxels For Test";
            this.btnVoxGenValidation.UseVisualStyleBackColor = true;
            this.btnVoxGenValidation.Click += new System.EventHandler(this.btnVoxGenValidation_Click);
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(6, 551);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(201, 41);
            this.btnExport.TabIndex = 8;
            this.btnExport.Text = "Export Result";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // dgvResult
            // 
            this.dgvResult.AllowUserToAddRows = false;
            this.dgvResult.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResult.Location = new System.Drawing.Point(9, 152);
            this.dgvResult.Name = "dgvResult";
            this.dgvResult.RowHeadersWidth = 62;
            this.dgvResult.RowTemplate.Height = 30;
            this.dgvResult.Size = new System.Drawing.Size(695, 393);
            this.dgvResult.TabIndex = 7;
            this.dgvResult.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvResult_CellContentClick);
            this.dgvResult.RowEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvResult_RowEnter);
            this.dgvResult.RowHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dgvResult_RowHeaderMouseClick);
            // 
            // FrmVoxel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(749, 882);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.prog);
            this.Name = "FrmVoxel";
            this.Text = "FrmVoxel";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FrmVoxel_FormClosed);
            this.Load += new System.EventHandler(this.FrmVoxel_Load);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResult)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.TextBox txtVoxSize;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnGenerate;
        private System.Windows.Forms.ProgressBar prog;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnLoad;
        private System.Windows.Forms.Button btnVisualize;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button btnConvert;
        private System.Windows.Forms.TextBox txtSmallVoxH;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox chkDebug;
        private System.Windows.Forms.CheckBox chkShowBaseVoxel;
        private System.Windows.Forms.CheckBox chkShowTriangle;
        private System.Windows.Forms.Button txtExportSolid;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.DataGridView dgvResult;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button btnVoxGenValidation;
        private System.Windows.Forms.Button btnGenerateByMesh;
        private System.Windows.Forms.Button btnCompressVoxes;
        private System.Windows.Forms.Button btnSaveCSV;
        private System.Windows.Forms.CheckBox chkConvertSldByElems;
    }
}