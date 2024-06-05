namespace DataManager
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        public const int MAIN_WIDTH = 500;
        public const int MAIN_HEIGHT1 = 400;
        public const int MAIN_HEIGHT2 = 100;

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
            this.btSettings = new System.Windows.Forms.Button();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageMain = new System.Windows.Forms.TabPage();
            this.btListStreams = new System.Windows.Forms.Button();
            this.btCheckStreams = new System.Windows.Forms.Button();
            this.btSubscribeStreams = new System.Windows.Forms.Button();
            this.btRecord = new System.Windows.Forms.Button();
            this.cbUdpStream = new System.Windows.Forms.CheckBox();
            this.lbElapsedTime = new System.Windows.Forms.Label();
            this.lbPacketCount = new System.Windows.Forms.Label();
            this.lbWarning = new System.Windows.Forms.Label();
            this.clbStreams = new System.Windows.Forms.CheckedListBox();
            this.tabPageRecord = new System.Windows.Forms.TabPage();
            this.lbDataPath = new System.Windows.Forms.Label();
            this.tbDataPath = new System.Windows.Forms.TextBox();
            this.btDataPath = new System.Windows.Forms.Button();
            this.lbProject = new System.Windows.Forms.Label();
            this.tbProject = new System.Windows.Forms.TextBox();
            this.lbExperiment = new System.Windows.Forms.Label();
            this.tbExperiment = new System.Windows.Forms.TextBox();
            this.lbSession = new System.Windows.Forms.Label();
            this.tbSession = new System.Windows.Forms.TextBox();
            this.lbSubject = new System.Windows.Forms.Label();
            this.tbSubject = new System.Windows.Forms.TextBox();
            this.btConfirm = new System.Windows.Forms.Button();
            this.tabPageMultiplot = new System.Windows.Forms.TabPage();
            this.btMultiplot = new System.Windows.Forms.Button();
            this.clbMultiplotStreams = new System.Windows.Forms.CheckedListBox();
            this.lbStatus = new System.Windows.Forms.Label();
            this.lbShowDebugArea = new System.Windows.Forms.Label();
            this.tbConsole = new System.Windows.Forms.TextBox();
            this.tmFormUpdate = new System.Windows.Forms.Timer();
            this.SuspendLayout();

            #region variables

            var tabPageWidth = MAIN_WIDTH - 15;
            var tabPageHeight = MAIN_HEIGHT1 - 55;

            #endregion
            // 
            // btSettings
            // 
            this.btSettings.Enabled = true;
            this.btSettings.Location = new System.Drawing.Point(MAIN_WIDTH - 40, 5);
            this.btSettings.Name = "btSettings";
            this.btSettings.TabIndex = 1;
            this.btSettings.Text = "⚙";
            this.btSettings.Size = new System.Drawing.Size(35, 20);
            this.btSettings.UseVisualStyleBackColor = true;
            this.btSettings.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btSettings.Click += new System.EventHandler(this.btSettings_Click);
            // 
            // tabControlMain
            //
            this.tabControlMain.Location = new System.Drawing.Point(10, 10);
            this.tabControlMain.Size = new System.Drawing.Size(tabPageWidth, tabPageHeight);
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.TabIndex = 0;
            // 
            // tabPageMain
            //     
            this.tabPageMain.Text = "Main";
            this.tabPageMain.Size = new System.Drawing.Size(tabPageWidth, tabPageHeight);
            this.tabPageMain.TabIndex = 0;
            // 
            // btListStreams
            // 
            this.btListStreams.Location = new System.Drawing.Point(10, 10);
            this.btListStreams.Name = "btListStreams";
            this.btListStreams.Size = new System.Drawing.Size(75, 20);
            this.btListStreams.TabIndex = 1;
            this.btListStreams.Text = "List";
            this.btListStreams.UseVisualStyleBackColor = true;
            this.btListStreams.Click += new System.EventHandler(this.btListStreams_Click);
            // 
            // btCheckStreams
            // 
            this.btCheckStreams.Enabled = true;
            this.btCheckStreams.Location = new System.Drawing.Point(10, 40);
            this.btCheckStreams.Name = "btCheckStreams";
            this.btCheckStreams.Size = new System.Drawing.Size(75, 20);
            this.btCheckStreams.TabIndex = 2;
            this.btCheckStreams.Text = "Check all";
            this.btCheckStreams.UseVisualStyleBackColor = true;
            this.btCheckStreams.Click += new System.EventHandler(this.btCheckStreams_Click);
            // 
            // btSubscribeStreams
            // 
            this.btSubscribeStreams.Enabled = true;
            this.btSubscribeStreams.Location = new System.Drawing.Point(10, 70);
            this.btSubscribeStreams.Name = "btSubscribeStreams";
            this.btSubscribeStreams.Size = new System.Drawing.Size(75, 20);
            this.btSubscribeStreams.TabIndex = 3;
            this.btSubscribeStreams.Text = "Subscribe";
            this.btSubscribeStreams.UseVisualStyleBackColor = true;
            this.btSubscribeStreams.Click += new System.EventHandler(this.btSubscribe_Click);
            // 
            // btRecord
            // 
            this.btRecord.Enabled = false;
            this.btRecord.Location = new System.Drawing.Point(10, 100);
            this.btRecord.Name = "btRecord";
            this.btRecord.Size = new System.Drawing.Size(75, 20);
            this.btRecord.TabIndex = 4;
            this.btRecord.Text = "Record";
            this.btRecord.UseVisualStyleBackColor = true;
            this.btRecord.Click += new System.EventHandler(this.btRecord_Click);
            // 
            // cbUdpStream
            // 
            this.cbUdpStream.Enabled = false;
            this.cbUdpStream.Location = new System.Drawing.Point(10, 130);
            this.cbUdpStream.Name = "cbUdpStream";
            this.cbUdpStream.Size = new System.Drawing.Size(110, 20);
            this.cbUdpStream.TabIndex = 5;
            this.cbUdpStream.Text = "Transmit metrics";
            this.cbUdpStream.UseVisualStyleBackColor = true;
            this.cbUdpStream.CheckedChanged += new System.EventHandler(this.cbUdpStream_CheckedChanged);
            this.cbUdpStream.AutoSize = true;
            // 
            // lbElapsedTime
            // 
            this.lbElapsedTime.Location = new System.Drawing.Point(0, 170);
            this.lbElapsedTime.Name = "lbElapsedTime";
            this.lbElapsedTime.Size = new System.Drawing.Size(100, 15);
            this.lbElapsedTime.Text = "Elapsed: 00:00:00";
            this.lbElapsedTime.AutoSize = true;
            // 
            // lbPacketCount
            // 
            this.lbPacketCount.Location = new System.Drawing.Point(0, 190);
            this.lbPacketCount.Name = "lbPacketCount";
            this.lbPacketCount.Size = new System.Drawing.Size(100, 15);
            this.lbPacketCount.Text = "Data packets: 0.0kB";
            this.lbPacketCount.AutoSize = true;
            // 
            // lbWarning
            // 
            this.lbWarning.Visible = false;
            this.lbWarning.Location = new System.Drawing.Point(25, 200);
            this.lbWarning.Name = "lbWarning";
            this.lbWarning.Size = new System.Drawing.Size(25, 15);
            this.lbWarning.Text = "🔗";
            this.lbWarning.BackColor = System.Drawing.Color.Crimson;
            this.lbWarning.Font = new System.Drawing.Font(this.lbWarning.Font.FontFamily, 22);
            this.lbWarning.AutoSize = true;
            // 
            // clbStreams
            // 
            this.clbStreams.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.clbStreams.Location = new System.Drawing.Point(125, 10);
            this.clbStreams.Name = "clbStreams";
            this.clbStreams.Size = new System.Drawing.Size(tabPageWidth - 125 - 10, tabPageHeight - 10);
            this.clbStreams.TabIndex = 6;
            this.clbStreams.HorizontalScrollbar = true;
            //this.clbStreams.ScrollAlwaysVisible = true;
            // 
            // tabPageRecord
            //     
            this.tabPageRecord.Text = "Record";
            this.tabPageRecord.Size = new System.Drawing.Size(tabPageWidth, tabPageHeight);
            this.tabPageRecord.TabIndex = 1;
            // 
            // lbDataPath
            // 
            this.lbDataPath.Location = new System.Drawing.Point(10, 10);
            this.lbDataPath.Name = "lbDataPath";
            this.lbDataPath.Size = new System.Drawing.Size(60, 15);
            this.lbDataPath.Text = "Data Path:";
            this.lbDataPath.BackColor = System.Drawing.Color.DarkGray;
            this.lbDataPath.ForeColor = System.Drawing.Color.White;
            // 
            // tbDataPath
            // 
            this.tbDataPath.Location = new System.Drawing.Point(lbDataPath.Size.Width + 10, 10);
            this.tbDataPath.Name = "tbDataPath";
            this.tbDataPath.Size = new System.Drawing.Size(tabPageWidth - lbDataPath.Size.Width - 40, 20);
            this.tbDataPath.TabIndex = 1;
            this.tbDataPath.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbDataPath.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.tbDataPath.ReadOnly = true;
            // 
            // btDataPath
            // 
            this.btDataPath.Enabled = true;
            this.btDataPath.Location = new System.Drawing.Point(tabPageWidth - 30, tbDataPath.Location.Y);
            this.btDataPath.Name = "btDataPath";
            this.btDataPath.Size = new System.Drawing.Size(20, 15);
            this.btDataPath.TabIndex = 2;
            this.btDataPath.Text = "...";
            this.btDataPath.UseVisualStyleBackColor = true;
            this.btDataPath.Click += new System.EventHandler(this.btDataPath_Click);
            // 
            // lbProject
            // 
            this.lbProject.Location = new System.Drawing.Point(lbDataPath.Location.X, lbDataPath.Location.Y + 20);
            this.lbProject.Name = "lbProject";
            this.lbProject.Size = new System.Drawing.Size(60, 15);
            this.lbProject.Text = "Project:";
            this.lbProject.BackColor = System.Drawing.Color.DarkGray;
            this.lbProject.ForeColor = System.Drawing.Color.White;
            // 
            // tbProject
            // 
            this.tbProject.Enabled = true;
            this.tbProject.Location = new System.Drawing.Point(tbDataPath.Location.X, lbProject.Location.Y);
            this.tbProject.Name = "tbProject";
            this.tbProject.Size = tbDataPath.Size;
            this.tbProject.TabIndex = 3;
            this.tbProject.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.tbProject.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.tbProject.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tabRecord_KeyPress);
            this.tbProject.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.tabRecord_PreviewKeyDown);
            this.tbProject.GotFocus += new System.EventHandler(this.tabRecord_GotFocus);
            this.tbProject.LostFocus += new System.EventHandler(this.tabRecord_LostFocus);
            // 
            // lbExperiment
            // 
            this.lbExperiment.Location = new System.Drawing.Point(lbDataPath.Location.X, lbDataPath.Location.Y + 40);
            this.lbExperiment.Name = "lbExperiment";
            this.lbExperiment.Size = new System.Drawing.Size(60, 15);
            this.lbExperiment.Text = "Experiment:";
            this.lbExperiment.BackColor = System.Drawing.Color.DarkGray;
            this.lbExperiment.ForeColor = System.Drawing.Color.White;
            // 
            // tbExperiment
            // 
            this.tbExperiment.Enabled = false;
            this.tbExperiment.Location = new System.Drawing.Point(tbDataPath.Location.X, lbExperiment.Location.Y);
            this.tbExperiment.Name = "tbExperiment";
            this.tbExperiment.Size = tbDataPath.Size;
            this.tbExperiment.TabIndex = 4;
            this.tbExperiment.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.tbExperiment.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.tbExperiment.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tabRecord_KeyPress);
            this.tbExperiment.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.tabRecord_PreviewKeyDown);
            this.tbExperiment.GotFocus += new System.EventHandler(this.tabRecord_GotFocus);
            this.tbExperiment.LostFocus += new System.EventHandler(this.tabRecord_LostFocus);
            // 
            // lbSession
            // 
            this.lbSession.Location = new System.Drawing.Point(lbDataPath.Location.X, lbDataPath.Location.Y + 60);
            this.lbSession.Name = "lbSession";
            this.lbSession.Size = new System.Drawing.Size(60, 15);
            this.lbSession.Text = "Session:";
            this.lbSession.BackColor = System.Drawing.Color.DarkGray;
            this.lbSession.ForeColor = System.Drawing.Color.White;
            // 
            // tbSession
            // 
            this.tbSession.Enabled = false;
            this.tbSession.Location = new System.Drawing.Point(tbDataPath.Location.X, lbSession.Location.Y);
            this.tbSession.Name = "tbSession";
            this.tbSession.Size = tbDataPath.Size;
            this.tbSession.TabIndex = 5;
            this.tbSession.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.tbSession.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.tbSession.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tabRecord_KeyPress);
            this.tbSession.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.tabRecord_PreviewKeyDown);
            this.tbSession.GotFocus += new System.EventHandler(this.tabRecord_GotFocus);
            this.tbSession.LostFocus += new System.EventHandler(this.tabRecord_LostFocus);
            // 
            // lbSubject
            // 
            this.lbSubject.Location = new System.Drawing.Point(lbDataPath.Location.X, lbDataPath.Location.Y + 80);
            this.lbSubject.Name = "lbSubject";
            this.lbSubject.Size = new System.Drawing.Size(60, 15);
            this.lbSubject.Text = "Subject:";
            this.lbSubject.BackColor = System.Drawing.Color.DarkGray;
            this.lbSubject.ForeColor = System.Drawing.Color.White;
            // 
            // tbSubject
            // 
            this.tbSubject.Enabled = false;
            this.tbSubject.Location = new System.Drawing.Point(tbDataPath.Location.X, lbSubject.Location.Y);
            this.tbSubject.Name = "tbSubject";
            this.tbSubject.Size = tbDataPath.Size;
            this.tbSubject.TabIndex = 6;
            this.tbSubject.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.CustomSource;
            this.tbSubject.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.tbSubject.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.tabRecord_KeyPress);
            this.tbSubject.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.tabRecord_PreviewKeyDown);
            this.tbSubject.GotFocus += new System.EventHandler(this.tabRecord_GotFocus);
            this.tbSubject.LostFocus += new System.EventHandler(this.tabRecord_LostFocus);
            // 
            // btConfirm
            // 
            this.btConfirm.Enabled = false;
            this.btConfirm.Location = new System.Drawing.Point(tabPageWidth / 2 - 32, lbSubject.Location.Y + 25);
            this.btConfirm.Name = "btConfirm";
            this.btConfirm.Size = new System.Drawing.Size(75, 20);
            this.btConfirm.TabIndex = 7;
            this.btConfirm.Text = "Confirm";
            this.btConfirm.UseVisualStyleBackColor = true;
            this.btConfirm.Click += new System.EventHandler(this.btConfirm_Click);
            //
            // tabPageMultiplot
            //     
            this.tabPageMultiplot.Text = "Multiplot";
            this.tabPageMultiplot.Size = new System.Drawing.Size(tabPageWidth, tabPageHeight);
            this.tabPageMultiplot.TabIndex = 2;
            // 
            // btMultiplot
            // 
            this.btMultiplot.Enabled = true;
            this.btMultiplot.Location = new System.Drawing.Point(10, 10);
            this.btMultiplot.Name = "btMultiplot";
            this.btMultiplot.Size = new System.Drawing.Size(75, 20);
            this.btMultiplot.TabIndex = 0;
            this.btMultiplot.Text = "Plot 📉";
            this.btMultiplot.UseVisualStyleBackColor = true;
            this.btMultiplot.Click += new System.EventHandler(this.btMultiplot_Click);
            //this.btMultiplot.Font = new System.Drawing.Font(this.btMultiplot.Font.FontFamily, 33);
            // 
            // clbMultiplotStreams
            // 
            this.clbMultiplotStreams.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.clbMultiplotStreams.Location = new System.Drawing.Point(115, 10);
            this.clbMultiplotStreams.Name = "clbMultiplotStreams";
            this.clbMultiplotStreams.Size = new System.Drawing.Size(tabPageWidth - 115 - 10, tabPageHeight - 10);
            this.clbMultiplotStreams.TabIndex = 6;
            //this.clbMultiplotStreams.ScrollAlwaysVisible = true;
            this.clbMultiplotStreams.HorizontalScrollbar = true;

            //
            // lbStatus
            // 
            this.lbStatus.Location = new System.Drawing.Point(10, MAIN_HEIGHT1 - 40);
            this.lbStatus.Name = "lbStatus";
            this.lbStatus.Size = new System.Drawing.Size(MAIN_WIDTH - 10, 0);
            this.lbStatus.AutoSize = true;
            this.lbStatus.Text = "Status: ";
            // 
            // lbShowDebugArea
            // 
            this.lbShowDebugArea.Location = new System.Drawing.Point(10, MAIN_HEIGHT1 - 20);
            this.lbShowDebugArea.Name = "lbShowDebugArea";
            this.lbShowDebugArea.Size = new System.Drawing.Size(60, 15);
            this.lbShowDebugArea.Text = "Debug ⏬";
            this.lbShowDebugArea.BackColor = System.Drawing.Color.DarkGray;
            this.lbShowDebugArea.ForeColor = System.Drawing.Color.BlueViolet;
            this.lbShowDebugArea.Click += new System.EventHandler(this.lbShowDebugArea_Click);
            // 
            // tbConsole
            // 
            this.tbConsole.Location = new System.Drawing.Point(10, this.lbShowDebugArea.Location.Y + 25);
            this.tbConsole.Multiline = true;
            this.tbConsole.Name = "tbConsole";
            this.tbConsole.Size = new System.Drawing.Size(MAIN_WIDTH - 15, MAIN_HEIGHT2 - 10);
            this.tbConsole.TabIndex = 2;
            this.tbConsole.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            
            // 
            // tmFormUpdate
            // 
            this.tmFormUpdate.Interval = 1000;
            this.tmFormUpdate.Tick += new System.EventHandler(this.tmFormUpdate_Tick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);       
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(MAIN_WIDTH, MAIN_HEIGHT1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            this.Controls.Add(this.btSettings);
            this.Controls.Add(this.tabControlMain);
            this.tabControlMain.Controls.Add(this.tabPageMain);
            this.tabPageMain.Controls.Add(this.btListStreams);
            this.tabPageMain.Controls.Add(this.btCheckStreams);
            this.tabPageMain.Controls.Add(this.btSubscribeStreams);
            this.tabPageMain.Controls.Add(this.btRecord);
            this.tabPageMain.Controls.Add(this.cbUdpStream);
            this.tabPageMain.Controls.Add(this.lbElapsedTime);
            this.tabPageMain.Controls.Add(this.lbPacketCount);
            this.tabPageMain.Controls.Add(this.lbWarning);
            this.tabPageMain.Controls.Add(this.clbStreams);
            this.tabControlMain.Controls.Add(this.tabPageRecord);
            this.tabPageRecord.Controls.Add(this.lbDataPath);
            this.tabPageRecord.Controls.Add(this.tbDataPath);
            this.tabPageRecord.Controls.Add(this.btDataPath);
            this.tabPageRecord.Controls.Add(this.lbProject);
            this.tabPageRecord.Controls.Add(this.tbProject);
            this.tabPageRecord.Controls.Add(this.lbExperiment);
            this.tabPageRecord.Controls.Add(this.tbExperiment);
            this.tabPageRecord.Controls.Add(this.lbSession);
            this.tabPageRecord.Controls.Add(this.tbSession);
            this.tabPageRecord.Controls.Add(this.lbSubject);
            this.tabPageRecord.Controls.Add(this.tbSubject);
            this.tabPageRecord.Controls.Add(this.btConfirm);
            this.tabControlMain.Controls.Add(this.tabPageMultiplot);
            this.tabPageMultiplot.Controls.Add(this.btMultiplot);
            this.tabPageMultiplot.Controls.Add(this.clbMultiplotStreams);
            this.Controls.Add(this.lbStatus);
            this.Controls.Add(this.lbShowDebugArea);
            this.Controls.Add(this.tbConsole);
            this.Name = "MainForm";
            this.Text = "AugmentX Data Manager";
            this.ResumeLayout(false);
            this.PerformLayout();
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(MainForm_FormClosing);
        }

        #endregion
        private System.Windows.Forms.Button btSettings;
        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageMain;
        private System.Windows.Forms.Button btListStreams;
        private System.Windows.Forms.Button btCheckStreams;
        private System.Windows.Forms.Button btSubscribeStreams;
        private System.Windows.Forms.Button btRecord;
        private System.Windows.Forms.CheckBox cbUdpStream;
        private System.Windows.Forms.Label lbElapsedTime;
        private System.Windows.Forms.Label lbPacketCount;
        private System.Windows.Forms.Label lbWarning;
        private System.Windows.Forms.CheckedListBox clbStreams;
        private System.Windows.Forms.TabPage tabPageRecord;
        private System.Windows.Forms.Label lbDataPath;
        private System.Windows.Forms.TextBox tbDataPath;
        private System.Windows.Forms.Button btDataPath;
        private System.Windows.Forms.Label lbProject;
        private System.Windows.Forms.TextBox tbProject;
        private System.Windows.Forms.Label lbExperiment;
        private System.Windows.Forms.TextBox tbExperiment;
        private System.Windows.Forms.Label lbSession;
        private System.Windows.Forms.TextBox tbSession;
        private System.Windows.Forms.Label lbSubject;
        private System.Windows.Forms.TextBox tbSubject;
        private System.Windows.Forms.Button btConfirm;
        private System.Windows.Forms.TabPage tabPageMultiplot;
        private System.Windows.Forms.Button btMultiplot;
        private System.Windows.Forms.CheckedListBox clbMultiplotStreams;
        private System.Windows.Forms.Label lbStatus;
        private System.Windows.Forms.Label lbShowDebugArea;
        private System.Windows.Forms.TextBox tbConsole;
        private System.Windows.Forms.Timer tmFormUpdate;

    }
}


