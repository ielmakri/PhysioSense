namespace CaptivClient
{
    partial class MainForm
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
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tmFormUpdate = new System.Windows.Forms.Timer();
            this.btStart = new System.Windows.Forms.Button();
            this.btSettings = new System.Windows.Forms.Button();
            this.lbElapsedTime = new System.Windows.Forms.Label();
            this.lbPacketCount = new System.Windows.Forms.Label();
            this.lbStatus = new System.Windows.Forms.Label();
            this.lbShowDebugArea = new System.Windows.Forms.Label();
            this.cbEnableLog = new System.Windows.Forms.CheckBox();
            this.tbConsole = new System.Windows.Forms.TextBox();
            this.lbCommand = new System.Windows.Forms.Label();
            this.tbCommand = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            var width = 420;
            // 
            // tmFormUpdate
            // 
            this.tmFormUpdate.Interval = 1000;
            this.tmFormUpdate.Tick += new System.EventHandler(this.tmFormUpdate_Tick);
            // 
            // btStart
            // 
            this.btStart.Location = new System.Drawing.Point(10, 10);
            this.btStart.Name = "btStart";
            this.btStart.Size = new System.Drawing.Size(60, 20);
            this.btStart.TabIndex = 1;
            this.btStart.Text = "Start";
            this.btStart.UseVisualStyleBackColor = true;
            this.btStart.Click += new System.EventHandler(this.btStart_Click);
            // 
            // btSettings
            // 
            this.btSettings.Enabled = true;
            this.btSettings.Location = new System.Drawing.Point(width - 45, 10);
            this.btSettings.Name = "btSettings";
            this.btSettings.Size = new System.Drawing.Size(40, 20);
            this.btSettings.TabIndex = 2;
            this.btSettings.Text = "⚙";
            this.btSettings.UseVisualStyleBackColor = true;
            this.btSettings.Click += new System.EventHandler(this.btSettings_Click);
            // 
            // lbElapsedTime
            // 
            this.lbElapsedTime.Location = new System.Drawing.Point(10, 40);
            this.lbElapsedTime.Name = "lbElapsedTime";
            this.lbElapsedTime.Size = new System.Drawing.Size(width - 10, 15);
            this.lbElapsedTime.Text = "Elapsed: 00:00:00";
            // 
            // lbPacketCount
            // 
            this.lbPacketCount.Location = new System.Drawing.Point(10, 60);
            this.lbPacketCount.Name = "lbPacketCount";
            this.lbPacketCount.Size = new System.Drawing.Size(width - 10, 15);
            this.lbPacketCount.Text = "Data packets: 0.0kB";
            // 
            // lbStatus
            // 
            this.lbStatus.Location = new System.Drawing.Point(10, 80);
            this.lbStatus.Name = "lbStatus";
            this.lbStatus.Size = new System.Drawing.Size(width - 10, 15);
            this.lbStatus.Text = "Status: ";
            // 
            // lbShowDebugArea
            // 
            this.lbShowDebugArea.Location = new System.Drawing.Point(10, 110);
            this.lbShowDebugArea.Name = "lbShowDebugArea";
            this.lbShowDebugArea.Size = new System.Drawing.Size(60, 15);
            this.lbShowDebugArea.TabIndex = 3;
            this.lbShowDebugArea.Text = "Debug ⏬";
            this.lbShowDebugArea.BackColor = System.Drawing.Color.DarkGray;
            this.lbShowDebugArea.ForeColor = System.Drawing.Color.BlueViolet;
            this.lbShowDebugArea.Click += new System.EventHandler(this.lbShowDebugArea_Click);
            // 
            // cbEnableLog
            // 
            this.cbEnableLog.AutoSize = true;
            this.cbEnableLog.Location = new System.Drawing.Point(10, 135);
            this.cbEnableLog.Name = "cbEnableLog";
            this.cbEnableLog.Size = new System.Drawing.Size(60, 20);
            this.cbEnableLog.TabIndex = 4;
            this.cbEnableLog.Text = "Log Stream";
            this.cbEnableLog.UseVisualStyleBackColor = true;
            // 
            // tbConsole
            // 
            this.tbConsole.Location = new System.Drawing.Point(10, 155);
            this.tbConsole.Multiline = true;
            this.tbConsole.Name = "tbConsole";
            this.tbConsole.Size = new System.Drawing.Size(width - 10, 175);
            this.tbConsole.TabIndex = 5;
            this.tbConsole.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            // 
            // lbCommand
            //              
            this.lbCommand.Location = new System.Drawing.Point(10, 335);
            this.lbCommand.Name = "lbCommand";
            this.lbCommand.Size = new System.Drawing.Size(160, 15);
            this.lbCommand.Text = "Manual command:";
            // 
            // tbCommand
            //            
            this.tbCommand.Enabled = true;
            this.tbCommand.Location = new System.Drawing.Point(10, 350);
            this.tbCommand.Name = "tbCommand";
            this.tbCommand.Size = new System.Drawing.Size(width - 10, 20);
            this.tbCommand.TabIndex = 6;
            this.tbCommand.Text = "TEAStopRec";
            this.tbConsole.Multiline = true;
            this.tbCommand.AcceptsTab = true;
            this.tbCommand.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.tbCommand_PreviewKeyDownEvent);
            this.tbCommand.KeyDown += new System.Windows.Forms.KeyEventHandler(tbCommand_KeyDownEvent);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(width, 130);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Controls.Add(this.btStart);
            this.Controls.Add(this.btSettings);
            this.Controls.Add(this.lbElapsedTime);
            this.Controls.Add(this.lbPacketCount);
            this.Controls.Add(this.lbStatus);
            this.Controls.Add(this.lbShowDebugArea);
            this.Controls.Add(this.cbEnableLog);
            this.Controls.Add(this.tbConsole);
            this.Controls.Add(this.lbCommand);
            this.Controls.Add(this.tbCommand);
            this.Name = "MainForm";
            this.Text = "CAPTIV LSL App";
            this.ResumeLayout(false);
            this.PerformLayout();
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(MainForm_FormClosing);
        }

        #endregion

        private System.Windows.Forms.Timer tmFormUpdate;
        private System.Windows.Forms.Button btStart;
        private System.Windows.Forms.Button btSettings;
        private System.Windows.Forms.Label lbElapsedTime;
        private System.Windows.Forms.Label lbPacketCount;
        private System.Windows.Forms.Label lbStatus;
        private System.Windows.Forms.Label lbShowDebugArea;
        private System.Windows.Forms.CheckBox cbEnableLog;
        private System.Windows.Forms.TextBox tbConsole;
        private System.Windows.Forms.Label lbCommand;
        private System.Windows.Forms.TextBox tbCommand;


    }
}


