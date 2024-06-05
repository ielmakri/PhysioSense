namespace KeMoStreamer
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
            this.btLinkKeyboard = new System.Windows.Forms.Button();
            this.btLinkMouse = new System.Windows.Forms.Button();
            this.components = new System.ComponentModel.Container();

            var width = 600;
            var height = 240;

            // 
            // btLinkKeyboard
            // 
            this.btLinkKeyboard.Location = new System.Drawing.Point(width / 2 - 125, 40);
            this.btLinkKeyboard.Name = "btLinkKeyboard";
            this.btLinkKeyboard.Size = new System.Drawing.Size(250, 60);
            this.btLinkKeyboard.TabIndex = 1;
            this.btLinkKeyboard.Text = "Link Keyboard";
            this.btLinkKeyboard.UseVisualStyleBackColor = true;
            this.btLinkKeyboard.Click += new System.EventHandler(this.btLinkKeyboard_Click);
            // 
            // btLinkMouse
            // 
            this.btLinkMouse.Location = new System.Drawing.Point(width / 2 - 125, 140);
            this.btLinkMouse.Name = "btLinkMouse";
            this.btLinkMouse.Size = new System.Drawing.Size(250, 60);
            this.btLinkMouse.TabIndex = 1;
            this.btLinkMouse.Text = "Link Mouse";
            this.btLinkMouse.UseVisualStyleBackColor = true;
            this.btLinkMouse.Click += new System.EventHandler(this.btLinkMouse_Click);

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(width, height);
            this.Controls.Add(this.btLinkKeyboard);
            this.Controls.Add(this.btLinkMouse);
            this.Text = "KeMo Streamer";
        }

        #endregion

        private System.Windows.Forms.Button btLinkKeyboard;
        private System.Windows.Forms.Button btLinkMouse;

    }
}

