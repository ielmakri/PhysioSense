namespace DataManager
{
    partial class FormPlotter
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            timerUpdate.Dispose();

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lbInfo = new System.Windows.Forms.Label();
            this.formsPlot1 = new ScottPlot.FormsPlot();
            this.lbStatus = new System.Windows.Forms.Label();
            this.timerUpdate = new System.Windows.Forms.Timer();
            this.SuspendLayout();
            // 
            // lbInfo
            // 
            this.lbInfo.Location = new System.Drawing.Point(0, 0);
            this.lbInfo.Name = "lbInfo";
            this.lbInfo.Size = new System.Drawing.Size(600, 0);
            this.lbInfo.AutoSize = true;
            this.lbInfo.Text = "";
            //this.lbInfo.BackColor = System.Drawing.Color.FromArgb(0,163,175);
            this.lbInfo.BackColor = System.Drawing.Color.DarkBlue;
            this.lbInfo.ForeColor = System.Drawing.Color.WhiteSmoke;
            //this.lbInfo.Font = new System.Drawing.Font(this.lbInfo.Font.FontFamily, 10);
            // 
            // formsPlot1
            // 
            this.formsPlot1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.formsPlot1.Location = new System.Drawing.Point(10, 55);
            this.formsPlot1.Name = "formsPlot";
            this.formsPlot1.Size = new System.Drawing.Size(600, 400);
            this.formsPlot1.Text = "plotter";
            this.formsPlot1.DoubleClick += new System.EventHandler(this.formsPlot1_DoubleClick);
            // 
            // lbStatus
            // 
            this.lbStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lbStatus.Visible = false;
            this.lbStatus.Location = new System.Drawing.Point(300, 470);
            this.lbStatus.Name = "lbStatus";
            this.lbStatus.Size = new System.Drawing.Size(100, 0);
            this.lbStatus.AutoSize = true;
            this.lbStatus.Text = "⏸";
            this.lbStatus.ForeColor = System.Drawing.Color.Red;
            this.lbStatus.Font = new System.Drawing.Font(this.lbInfo.Font.FontFamily, 24);
            // 
            // 
            // timerUpdate
            this.timerUpdate.Tick += new System.EventHandler(this.timerUpdate_Tick);
            //          
            // 
            // FormPlotter
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(620, 520);
            this.Controls.Add(this.formsPlot1);
            this.Controls.Add(this.lbInfo);
            this.Controls.Add(this.lbStatus);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(FormPlotter_FormClosing);
            this.Name = "FormPlotter";
            this.Text = "Plotter";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private ScottPlot.FormsPlot formsPlot1;
        private System.Windows.Forms.Label lbInfo;
        private System.Windows.Forms.Label lbStatus;
        private System.Windows.Forms.Timer timerUpdate;
    }
}

