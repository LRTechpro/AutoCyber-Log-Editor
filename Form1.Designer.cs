using System;
using System.Windows.Forms;
using System.Drawing;

namespace AutoCyber_Log_Editor
{
    /// <summary>
    /// Form1.Designer.cs - Designer-generated partial class for Form1.
    /// This file contains InitializeComponent() which creates and configures all UI controls.
    /// DO NOT manually edit this section in the Visual Studio designer to avoid conflicts.
    /// </summary>
    partial class Form1 : System.Windows.Forms.Form
    {
        /// <summary>
        /// components - The IContainer that manages all components on this form.
        /// Required by the designer for proper disposal of resources when the form closes.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Dispose - Properly cleans up all resources used by the form and its controls.
        /// Called automatically when the form is closed.
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
        /// InitializeComponent - Creates and configures all UI controls for the form.
        /// This method is called automatically from the Form1 constructor.
        /// It sets up: MenuStrip, ToolStrip, RichTextBox editor, and StatusStrip.
        /// The order of adding controls is CRITICAL: top controls first, fill control last.
        /// </summary>
        private void InitializeComponent()
        {
            // Create the IContainer to manage component lifetime and disposal.
            components = new System.ComponentModel.Container();

            // === CONFIGURE BASIC FORM PROPERTIES ===
            // Set form to use the system default font for a native Windows appearance.
            this.AutoScaleMode = AutoScaleMode.Font;
            // Set the initial window size when the form first appears.
            this.ClientSize = new Size(1000, 700);
            // Set the initial window title (will be updated at runtime to show filename).
            this.Text = "AutoCyber Log Editor";
            // Allow files to be dragged onto the form window to open them.
            this.AllowDrop = true;

            // === CREATE AND CONFIGURE MENUSTRIP ===
            // The MenuStrip provides the menu bar (File, Edit, View, Tools, Help).
            // We create this FIRST and add it FIRST so it appears at the top of the form.
            MenuStrip menuStrip = new MenuStrip();
            menuStrip.Dock = DockStyle.Top;  // Dock to top so it doesn't take up space elsewhere.

            // FILE MENU: Contains New, Open, Save, Save As, and Exit commands.
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add("&New", null, MenuFile_New_Click);
            fileMenu.DropDownItems.Add("&Open", null, MenuFile_Open_Click);
            fileMenu.DropDownItems.Add("&Save", null, MenuFile_Save_Click);
            fileMenu.DropDownItems.Add("Save &As", null, MenuFile_SaveAs_Click);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, new EventHandler(MenuFile_Exit_Click));
            menuStrip.Items.Add(fileMenu);

            // EDIT MENU: Contains Undo, Redo, Cut, Copy, Paste, Find, Replace, Go To Line.
            ToolStripMenuItem editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add("&Undo", null, (s, e) => { if (rtbEditor != null) rtbEditor.Undo(); });
            editMenu.DropDownItems.Add("&Redo", null, (s, e) => { if (rtbEditor != null) rtbEditor.Redo(); });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("Cu&t", null, (s, e) => { if (rtbEditor != null) rtbEditor.Cut(); });
            editMenu.DropDownItems.Add("&Copy", null, (s, e) => { if (rtbEditor != null) rtbEditor.Copy(); });
            editMenu.DropDownItems.Add("&Paste", null, (s, e) => { if (rtbEditor != null) rtbEditor.Paste(); });
            editMenu.DropDownItems.Add("&Delete", null, (s, e) => { if (rtbEditor != null && rtbEditor.SelectionLength > 0) rtbEditor.SelectedText = ""; });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("Select &All", null, (s, e) => { if (rtbEditor != null) rtbEditor.SelectAll(); });
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("&Find", null, new EventHandler(MenuEdit_Find_Click));
            editMenu.DropDownItems.Add("&Live Search", null, new EventHandler(MenuEdit_LiveSearch_Click));  // ADD THIS LINE
            editMenu.DropDownItems.Add("&Replace", null, new EventHandler(MenuEdit_Replace_Click));
            editMenu.DropDownItems.Add("&Go To Line", null, new EventHandler(MenuEdit_GoToLine_Click));
            menuStrip.Items.Add(editMenu);

            // VIEW MENU: Contains Word Wrap, Zoom, Font, and Log Triage Mode options.
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("&View");
            viewMenu.DropDownItems.Add("&Word Wrap", null, new EventHandler(MenuView_WordWrap_Click));
            viewMenu.DropDownItems.Add("Zoom &In", null, (s, e) => SetZoom(zoomPercent + 10));
            viewMenu.DropDownItems.Add("Zoom &Out", null, (s, e) => SetZoom(zoomPercent - 10));
            viewMenu.DropDownItems.Add("&Reset Zoom", null, (s, e) => SetZoom(100));
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add("&Font", null, new EventHandler(MenuView_Font_Click));
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add("&Log Triage Mode", null, new EventHandler(MenuView_LogTriageMode_Click));
            viewMenu.DropDownItems.Add("&Highlight Similar Text", null, new EventHandler(MenuView_ToggleAutoHighlight_Click));  // ADD THIS LINE
            ToolStripMenuItem darkModeToolStripMenuItem = new ToolStripMenuItem();
            darkModeToolStripMenuItem.Name = "darkModeToolStripMenuItem";
            darkModeToolStripMenuItem.Text = "&Dark Mode";
            darkModeToolStripMenuItem.Click += MenuView_DarkMode_Click;
            viewMenu.DropDownItems.Add(darkModeToolStripMenuItem);
            menuStrip.Items.Add(viewMenu);

            // TOOLS MENU: Contains automotive cybersecurity analysis tools.
            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("&Tools");
            toolsMenu.DropDownItems.Add("&Decode UDS Service", null, new EventHandler(MenuTools_DecodeUDS_Click));
            toolsMenu.DropDownItems.Add("&Hex to ASCII", null, new EventHandler(MenuTools_HexToAscii_Click));
            toolsMenu.DropDownItems.Add("&ASCII to Hex", null, new EventHandler(MenuTools_AsciiToHex_Click));
            menuStrip.Items.Add(toolsMenu);

            // HELP MENU: Contains About information.
            ToolStripMenuItem helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add("&About", null, new EventHandler(MenuHelp_About_Click));
            menuStrip.Items.Add(helpMenu);

            // Add the MenuStrip to the form.
            // Order matters: MenuStrip must be added BEFORE ToolStrip so it stays on top.
            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;

            // === CREATE AND CONFIGURE TOOLSTRIP ===
            // The ToolStrip provides quick-access buttons below the menu bar.
            // Docked to top so it appears below the MenuStrip and above the editor.
            ToolStrip toolStrip = new ToolStrip();
            toolStrip.Dock = DockStyle.Top;

            // Add buttons for common operations: New, Open, Save, Find.
            toolStrip.Items.Add("New", null, new EventHandler(MenuFile_New_Click));
            toolStrip.Items.Add("Open", null, new EventHandler(MenuFile_Open_Click));
            toolStrip.Items.Add("Save", null, new EventHandler(MenuFile_Save_Click));
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add("Find", null, new EventHandler(MenuEdit_Find_Click));

            // Add the ToolStrip to the form.
            // ToolStrip must be added AFTER MenuStrip (so it doesn't hide the menu).
            this.Controls.Add(toolStrip);

            // === CREATE AND CONFIGURE STATUSSTRIP ===
            // The StatusStrip appears at the bottom and shows document state info.
            // Docked to bottom so it doesn't interfere with the fill control.
            StatusStrip statusStrip = new StatusStrip();
            statusStrip.Dock = DockStyle.Bottom;

            // Create labels for different status indicators (line, column, count, zoom, word wrap).
            ToolStripStatusLabel labelLineCol = new ToolStripStatusLabel("Line 1, Col 1");
            statusStrip.Items.Add(labelLineCol);
            statusStrip.Items.Add(new ToolStripSeparator());
            ToolStripStatusLabel labelLineCount = new ToolStripStatusLabel("Lines: 1");
            statusStrip.Items.Add(labelLineCount);
            ToolStripStatusLabel labelCharCount = new ToolStripStatusLabel("Chars: 0");
            statusStrip.Items.Add(labelCharCount);
            ToolStripStatusLabel labelSelection = new ToolStripStatusLabel("Selection: 0");
            statusStrip.Items.Add(labelSelection);
            statusStrip.Items.Add(new ToolStripSeparator());
            ToolStripStatusLabel labelWordWrap = new ToolStripStatusLabel("Word Wrap: ON");
            statusStrip.Items.Add(labelWordWrap);
            ToolStripStatusLabel labelZoom = new ToolStripStatusLabel("Zoom: 100%");
            statusStrip.Items.Add(labelZoom);

            // Store references to status labels in fields so they can be updated by event handlers.
            // This allows us to update the status bar display from anywhere in the code.
            LabelLineCol = labelLineCol;
            LabelLineCount = labelLineCount;
            LabelCharCount = labelCharCount;
            LabelSelection = labelSelection;
            LabelWordWrap = labelWordWrap;
            LabelZoom = labelZoom;

            // Add the StatusStrip to the form.
            // StatusStrip must be added BEFORE the RichTextBox (so it appears on top of the fill area).
            this.Controls.Add(statusStrip);

            // === CREATE AND CONFIGURE RICHTEXTBOX (MAIN EDITOR) ===
            // The RichTextBox is where the user types and edits text.
            // We create this LAST and add it LAST because it uses DockStyle.Fill.
            // DockStyle.Fill makes the RichTextBox expand to fill all remaining space in the form.
            // It MUST be added after all Dock.Top and Dock.Bottom controls, or it will cover them.
            rtbEditor = new RichTextBox();
            rtbEditor.Dock = DockStyle.Fill;  // Fill all remaining space (critical for visibility!).
            rtbEditor.Font = new Font("Consolas", 11);  // Consolas is a monospace font perfect for code/logs.
            rtbEditor.BackColor = Color.White;  // White background for good contrast and readability.
            rtbEditor.ForeColor = Color.Black;  // Black text is easy on the eyes.
            rtbEditor.WordWrap = true;  // Start with word wrap enabled.
            rtbEditor.AcceptsTab = true;  // Allow Tab key to insert tab characters instead of moving focus.
            rtbEditor.AllowDrop = true;  // Allow drag-and-drop to open files.

            // Hook up event handlers for the RichTextBox.
            // TextChanged fires whenever the user types, pastes, or deletes.
            rtbEditor.TextChanged += new EventHandler(RichTextBox_TextChanged);
            // SelectionChanged fires when the cursor moves or text is selected.
            rtbEditor.SelectionChanged += new EventHandler(RichTextBox_SelectionChanged);
            // DragEnter fires when the user drags a file over the editor.
            rtbEditor.DragEnter += new DragEventHandler(RichTextBox_DragEnter);
            // DragDrop fires when the user drops a file onto the editor.
            rtbEditor.DragDrop += new DragEventHandler(RichTextBox_DragDrop);

            // Add the RichTextBox to the form LAST.
            // This is CRITICAL: if you add it before the StatusStrip, it will cover the status bar.
            // The order of Controls.Add() matters because it determines the Z-order (layering).
            this.Controls.Add(rtbEditor);

            // Hook up the form-level event for when the form is being closed.
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
        }

        private void MenuView_LogTriageMode_Click(object sender, EventArgs e)
        {
            // Toggle Log Triage Mode
            isLogTriageMode = !isLogTriageMode;
            
            if (isLogTriageMode)
            {
                // Enable triage mode: use Consolas 11pt, disable word wrap, highlight keywords
                rtbEditor.Font = new Font("Consolas", 11f);
                rtbEditor.WordWrap = false;
            }
            else
            {
                // Disable triage mode: revert to default settings
                rtbEditor.Font = new Font("Consolas", 11f);
                this.RepaintAllHighlights();
            }
            
            // Repaint highlights with triage keywords if enabled
            RepaintAllHighlights();
            
            // Update status bar
            if (LabelWordWrap != null)
                LabelWordWrap.Text = $"Word Wrap: {(rtbEditor.WordWrap ? "ON" : "OFF")}";
        }

        private void MenuView_Font_Click(object sender, EventArgs e)
        {
            // Create and show the font dialog
            FontDialog fontDialog = new FontDialog();
            fontDialog.Font = rtbEditor.Font;
            fontDialog.ShowColor = false;
            
            if (fontDialog.ShowDialog(this) == DialogResult.OK)
            {
                rtbEditor.Font = fontDialog.Font;
            }
        }

        private void MenuFile_Exit_Click(object sender, EventArgs e) => this.Close();

        private void MenuFile_SaveAs_Click(object sender, EventArgs e) => SaveFileAs();

        private void MenuFile_Save_Click(object sender, EventArgs e) => SaveFile();

        private void MenuFile_Open_Click(object sender, EventArgs e)
        {
            // Prompt to save current document if it has unsaved changes
            if (!PromptSaveIfDirty())
                return;

            // Create and configure the open file dialog
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Title = "Open File";
            openDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            openDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // If user selects a file, open it
            if (openDialog.ShowDialog(this) == DialogResult.OK)
            {
                OpenFileFromPath(openDialog.FileName);
            }
        }

        private void MenuFile_New_Click(object sender, EventArgs e)
        {
            // Prompt to save current document if it has unsaved changes
            if (!PromptSaveIfDirty())
                return;

            // Clear the editor for a new document
            rtbEditor.Clear();
            currentFilePath = string.Empty;
            isDirty = false;

            // Update UI
            UpdateTitle();
            UpdateStatusBar();
        }

        #endregion

        #region ========== PROPERTIES TO HOLD STATUS BAR REFERENCES ==========
        // These properties store references to the status bar labels so they can be updated at runtime.

        /// <summary>
        /// LabelLineCol - Reference to the status label showing current line and column numbers.
        /// Stored here so UpdateStatusBar() and other methods can update it.
        /// </summary>
        public ToolStripStatusLabel LabelLineCol { get; set; }

        /// <summary>
        /// LabelLineCount - Reference to the status label showing total line count.
        /// </summary>
        public ToolStripStatusLabel LabelLineCount { get; set; }

        /// <summary>
        /// LabelCharCount - Reference to the status label showing total character count.
        /// </summary>
        public ToolStripStatusLabel LabelCharCount { get; set; }

        /// <summary>
        /// LabelSelection - Reference to the status label showing selection length.
        /// </summary>
        public ToolStripStatusLabel LabelSelection { get; set; }

        /// <summary>
        /// LabelWordWrap - Reference to the status label showing word wrap status.
        /// </summary>
        public ToolStripStatusLabel LabelWordWrap { get; set; }

        /// <summary>
        /// LabelZoom - Reference to the status label showing zoom percentage.
        /// </summary>
        public ToolStripStatusLabel LabelZoom { get; set; }

        #endregion

        // ===== FIELD AND METHOD DECLARATIONS =====
        // These are declared here in the partial class so Form1.cs can implement them.

        /// <summary>
        /// rtbEditor - The main RichTextBox control where users type and edit text.
        /// Initialized in InitializeComponent() and fully configured in BuildEditor() in Form1.cs.
        /// </summary>
        private RichTextBox rtbEditor;

        /// <summary>
        /// zoomPercent - Stores the current zoom level as a percentage (100 = 100%, etc.).
        /// Used by SetZoom() to adjust rtbEditor.ZoomFactor.
        /// </summary>
        private int zoomPercent = 100;

        /// <summary>
        /// SetZoom - Sets the zoom level of the editor to the specified percentage.
        /// Clamps the value between 10% and 500% to prevent invalid zoom levels.
        /// Updates the status bar label with the new zoom percentage.
        /// </summary>
        private void SetZoom(int percent)
        {
            // Clamp the zoom percentage to a reasonable range (10-500)
            if (percent < 10) percent = 10;
            if (percent > 500) percent = 500;

            // Store the new zoom percentage
            zoomPercent = percent;

            // Apply the zoom to the editor if it exists
            if (rtbEditor != null)
            {
                try
                {
                    // Convert percentage to zoom factor (100% = 1.0f)
                    rtbEditor.ZoomFactor = zoomPercent / 100.0f;
                }
                catch
                {
                    // If zoom fails, reset to 100%
                    rtbEditor.ZoomFactor = 1.0f;
                    zoomPercent = 100;
                }
            }

            // Update the status bar to show the new zoom level
            if (LabelZoom != null)
                LabelZoom.Text = $"Zoom: {zoomPercent}%";
        }

        /// <summary>
        /// MenuView_WordWrap_Click - Handler for View > Word Wrap menu item.
        /// Toggles word wrap on/off and updates the status bar accordingly.
        /// When word wrap is ON, long lines wrap to the next line.
        /// When word wrap is OFF, long lines extend off-screen and require horizontal scrolling.
        /// </summary>
        private void MenuView_WordWrap_Click(object sender, EventArgs e)
        {
            // Toggle the word wrap setting
            if (rtbEditor != null)
            {
                rtbEditor.WordWrap = !rtbEditor.WordWrap;

                // Update the status bar to show the new word wrap state
                if (LabelWordWrap != null)
                    LabelWordWrap.Text = $"Word Wrap: {(rtbEditor.WordWrap ? "ON" : "OFF")}";
            }
        }

        // Add this method to the partial class Form1
        private void MenuEdit_LiveSearch_Click(object sender, EventArgs e) =>
            ShowLiveSearchDialog();

        private void MenuView_ToggleAutoHighlight_Click(object sender, EventArgs e) =>
            // TODO: Implement highlight similar text functionality here.
            MessageBox.Show("Highlight Similar Text clicked (not yet implemented).");

        private void MenuHelp_About_Click(object sender, EventArgs e) => MessageBox.Show("AutoCyber Log Editor\nVersion 1.0\n\nCreated by Your Name", "About");

        private void MenuTools_AsciiToHex_Click(object sender, EventArgs e) =>
            MenuTools_AsciiToHex_Click(this, e);

        private void MenuView_DarkMode_Click(object sender, EventArgs e) =>
            ToggleDarkMode();

        private void MenuTools_DecodeUDS_Click(object sender, EventArgs e) =>
            MenuTools_DecodeUDS_Click(this, e);

        private void MenuTools_HexToAscii_Click(object sender, EventArgs e) =>
            MenuTools_HexToAscii_Click(this, e);
    }
}