namespace RevitVoxelzation
{
    partial class FrmCompressMesh
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
            this.progCompress = new System.Windows.Forms.ProgressBar();
            this.btnCompress = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.btnGenARect = new System.Windows.Forms.Button();
            this.btnSaveAR = new System.Windows.Forms.Button();
            this.btnPathPlanning = new System.Windows.Forms.Button();
            this.btnLoadRegion = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // progCompress
            // 
            this.progCompress.Location = new System.Drawing.Point(12, 115);
            this.progCompress.Name = "progCompress";
            this.progCompress.Size = new System.Drawing.Size(1027, 49);
            this.progCompress.TabIndex = 0;
            // 
            // btnCompress
            // 
            this.btnCompress.Location = new System.Drawing.Point(12, 12);
            this.btnCompress.Name = "btnCompress";
            this.btnCompress.Size = new System.Drawing.Size(186, 44);
            this.btnCompress.TabIndex = 1;
            this.btnCompress.Text = "Compress Voxels";
            this.btnCompress.UseVisualStyleBackColor = true;
            this.btnCompress.Click += new System.EventHandler(this.btnCompress_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(204, 12);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(235, 44);
            this.button1.TabIndex = 1;
            this.button1.Text = "Load Compressed Voxels";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // btnGenARect
            // 
            this.btnGenARect.Location = new System.Drawing.Point(445, 13);
            this.btnGenARect.Name = "btnGenARect";
            this.btnGenARect.Size = new System.Drawing.Size(315, 43);
            this.btnGenARect.TabIndex = 2;
            this.btnGenARect.Text = "Generate AccessibleRectangle";
            this.btnGenARect.UseVisualStyleBackColor = true;
            this.btnGenARect.Click += new System.EventHandler(this.btnGenARect_Click);
            // 
            // btnSaveAR
            // 
            this.btnSaveAR.Location = new System.Drawing.Point(766, 13);
            this.btnSaveAR.Name = "btnSaveAR";
            this.btnSaveAR.Size = new System.Drawing.Size(273, 43);
            this.btnSaveAR.TabIndex = 3;
            this.btnSaveAR.Text = "Save Accessible Region";
            this.btnSaveAR.UseVisualStyleBackColor = true;
            this.btnSaveAR.Click += new System.EventHandler(this.btnSaveAR_Click);
            // 
            // btnPathPlanning
            // 
            this.btnPathPlanning.Location = new System.Drawing.Point(204, 62);
            this.btnPathPlanning.Name = "btnPathPlanning";
            this.btnPathPlanning.Size = new System.Drawing.Size(235, 47);
            this.btnPathPlanning.TabIndex = 4;
            this.btnPathPlanning.Text = "Path Planning";
            this.btnPathPlanning.UseVisualStyleBackColor = true;
            this.btnPathPlanning.Click += new System.EventHandler(this.btnPathPlanning_Click);
            // 
            // btnLoadRegion
            // 
            this.btnLoadRegion.Location = new System.Drawing.Point(12, 62);
            this.btnLoadRegion.Name = "btnLoadRegion";
            this.btnLoadRegion.Size = new System.Drawing.Size(186, 47);
            this.btnLoadRegion.TabIndex = 5;
            this.btnLoadRegion.Text = "Load Region";
            this.btnLoadRegion.UseVisualStyleBackColor = true;
            this.btnLoadRegion.Click += new System.EventHandler(this.btnLoadRegion_Click);
            // 
            // FrmCompressMesh
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1051, 175);
            this.Controls.Add(this.btnLoadRegion);
            this.Controls.Add(this.btnPathPlanning);
            this.Controls.Add(this.btnSaveAR);
            this.Controls.Add(this.btnGenARect);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.btnCompress);
            this.Controls.Add(this.progCompress);
            this.Name = "FrmCompressMesh";
            this.Text = "FrmCompressMesh";
            this.Load += new System.EventHandler(this.FrmCompressMesh_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ProgressBar progCompress;
        private System.Windows.Forms.Button btnCompress;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button btnGenARect;
        private System.Windows.Forms.Button btnSaveAR;
        private System.Windows.Forms.Button btnPathPlanning;
        private System.Windows.Forms.Button btnLoadRegion;
    }
}