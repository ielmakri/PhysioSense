namespace CameraCapture
{
    partial class CameraCapture
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
            ReleaseData();
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.cboCamSelect = new System.Windows.Forms.ComboBox();
            this.btStart = new System.Windows.Forms.Button();
            this.btRecord = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.iboCapture = new Emgu.CV.UI.ImageBox();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.cboCamSelect);
            this.splitContainer1.Panel1.Controls.Add(this.btStart);
            this.splitContainer1.Panel1.Controls.Add(this.btRecord);

            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.iboCapture);
            this.splitContainer1.Panel2.Controls.Add(this.panel1);
            this.splitContainer1.Panel2.Controls.Add(this.label1);
            this.splitContainer1.Size = new System.Drawing.Size(632, 632);
            this.splitContainer1.SplitterDistance = 66;
            this.splitContainer1.TabIndex = 0;
            // 
            // cboCamSelect
            // 
            this.cboCamSelect.Location = new System.Drawing.Point(10, 12);
            this.cboCamSelect.Name = "cboCamSelect";
            this.cboCamSelect.Size = new System.Drawing.Size(152, 23);
            this.cboCamSelect.TabIndex = 2;
            this.cboCamSelect.Text = "cboCamSelect";
            // 
            // btStart
            // 
            this.btStart.Location = new System.Drawing.Point(182, 12);
            this.btStart.Name = "btStart";
            this.btStart.Size = new System.Drawing.Size(102, 23);
            this.btStart.TabIndex = 0;
            this.btStart.Text = "Start";
            this.btStart.UseVisualStyleBackColor = true;
            this.btStart.Click += new System.EventHandler(this.btStart_Click);
            // 
            // btRecord
            // 
            this.btRecord.Location = new System.Drawing.Point(302, 12);
            this.btRecord.Name = "btRecord";
            this.btRecord.Size = new System.Drawing.Size(102, 23);
            this.btRecord.TabIndex = 0;
            this.btRecord.Text = "Record";
            this.btRecord.UseVisualStyleBackColor = true;
            this.btRecord.Click += new System.EventHandler(this.btRecord_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.label1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(411, 33);
            this.panel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(85, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Captured Image:";
            // 
            // iboCapture
            // 
            this.iboCapture.Dock = System.Windows.Forms.DockStyle.Fill;
            this.iboCapture.Image = null;
            this.iboCapture.Location = new System.Drawing.Point(0, 33);
            this.iboCapture.Name = "iboCapture";
            this.iboCapture.Size = new System.Drawing.Size(411, 345);
            this.iboCapture.TabIndex = 1;
            // 
            // CameraCapture
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(632, 632);
            this.Controls.Add(this.splitContainer1);
            this.Name = "CameraCapture";
            this.Text = "Camera Capture";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();


            this.ResumeLayout(false);

        }

        #endregion
        
        private System.Windows.Forms.ComboBox cboCamSelect;
        private System.Windows.Forms.Button btStart;
        private System.Windows.Forms.Button btRecord;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label label1;
        private Emgu.CV.UI.ImageBox iboCapture;

    }
}

