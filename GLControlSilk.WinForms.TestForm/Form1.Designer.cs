namespace GLControlSilk.WinForms.TestForm
{
    partial class Form1
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
            glControl = new GLControl();
            SuspendLayout();
            // 
            // glControl
            // 
            glControl.API = Silk.NET.Windowing.ContextAPI.OpenGL;
            glControl.APIVersion = new Silk.NET.Windowing.APIVersion(3, 3);
            glControl.Dock = DockStyle.Fill;
            glControl.Flags = Silk.NET.Windowing.ContextFlags.Default;
            glControl.IsEventDriven = true;
            glControl.Location = new Point(0, 0);
            glControl.Name = "glControl";
            glControl.Profile = Silk.NET.Windowing.ContextProfile.Compatability;
            glControl.Size = new Size(800, 450);
            glControl.TabIndex = 0;
            glControl.Text = "glControl1";
            glControl.Load += glControl_Load;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(glControl);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
        }

        #endregion

        private GLControl glControl;
    }
}