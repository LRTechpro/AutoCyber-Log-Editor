using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AutoCyber_Log_Editor
{
    public partial class Form1 : Form
    {
        #region ========== FIELD DECLARATIONS: STATE MANAGEMENT ==========
        // This region contains all private fields that track the application state.
        // These include file path, dirty flag, search state, color settings, UI elements,
        // and flags for preventing re-entrancy during operations.
        // Most fields are initialized here and updated throughout the application lifecycle.

        // File management state
        /// <summary>Full path to the currently open file. Empty string if new/unsaved document.</summary>
        private string currentFilePath = string.Empty;
        
        /// <summary>Tracks whether document has unsaved changes. Set to true on text modification, false after save.</summary>
        private bool isDirty = false;

        // Search and highlighting state
        /// <summary>Stores the last text searched for, used to pre-populate Find dialog.</summary>
        private string lastSearchText = string.Empty;
        
        /// <summary>Background color for standard Find highlighting (yellow by default).</summary>
        private Color highlightColor = Color.Yellow;
        
        /// <summary>Background color for triage keyword highlighting (orange by default).</summary>
        private Color triageHighlightColor = Color.FromArgb(255, 200, 100);
        
        /// <summary>Background color for user-marked lines (light blue by default).</summary>
        private Color markedLineColor = Color.FromArgb(173, 216, 230);
        
        /// <summary>Unused auto-highlight color (kept for potential future use).</summary>
        private Color autoHighlightColor = Color.FromArgb(200, 255, 200);
        
        /// <summary>Stores text that was last auto-highlighted (currently unused).</summary>
        private string lastAutoHighlightedText = string.Empty;

        // Triage mode state
        /// <summary>When true, triage mode is active and keywords are highlighted automatically.</summary>
        private bool isLogTriageMode = false;
        
        /// <summary>Keywords to highlight in triage mode. Searched case-insensitively.</summary>
        private readonly string[] triageKeywords = { "FAIL", "ERROR", "DENIED", "0x27", "0x7F", "NRC" };
        
        /// <summary>Set of line indexes (0-based) that contain triage keywords. Updated when triage mode toggles or text changes.</summary>
        private HashSet<int> triagedLines = new HashSet<int>();

        // UI controls
        /// <summary>Panel on left side that displays line numbers. Manually painted in PnlLineNumbers_Paint.</summary>
        private Panel pnlLineNumbers = new Panel();

        // View settings
        /// <summary>Current zoom level as a percentage (100 = normal). Updated when user changes zoom.</summary>
        private float zoomPercentField = 100;
        
        /// <summary>True when dark mode theme is active, false for light mode.</summary>
        private bool isDarkMode = false;

        // Dark mode colors
        /// <summary>Background color for editor in dark mode.</summary>
        private Color darkModeBackground = Color.FromArgb(30, 30, 30);
        
        /// <summary>Text color for editor in dark mode.</summary>
        private Color darkModeForeground = Color.FromArgb(230, 230, 230);
        
        /// <summary>Background color for line number panel in dark mode.</summary>
        private Color darkModeLinePanel = Color.FromArgb(37, 37, 37);
        
        /// <summary>Text color for line numbers in dark mode.</summary>
        private Color darkModeLineText = Color.FromArgb(169, 169, 169);

        // Line marking state
        /// <summary>List of (start, length) tuples representing user-marked lines. Used to apply light blue backgrounds.</summary>
        private List<(int start, int length)> markedRanges = new List<(int, int)>();

        // Guard flags to prevent re-entrancy
        /// <summary>Set to true during HighlightAllOccurrences to prevent SelectionChanged from re-triggering highlighting.</summary>
        private bool isHighlightingInProgress = false;
        
        /// <summary>Set to true during RepaintAllHighlights to prevent SelectionChanged from re-triggering repaint.</summary>
        private bool isInternalRepaint = false;

        // Template undo state
        /// <summary>Stores document content before template insertion, allowing one-time undo of template insertion.</summary>
        private string contentBeforeTemplate = string.Empty;

        /// <summary>
        /// Returns the appropriate editor background color based on the current theme mode.
        /// Used throughout to ensure consistency when resetting backgrounds during highlighting.
        /// </summary>
        /// <returns>Dark background if dark mode is enabled, white otherwise.</returns>
        private Color GetDefaultEditorBackColor() => isDarkMode ? darkModeBackground : Color.White;

        // Win32 API constant for retrieving the first visible line in a RichTextBox
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

        /// <summary>
        /// Win32 API import to send messages to window handles.
        /// Used to query the first visible line for line number rendering.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        #endregion

        #region ========== CONSTRUCTOR & INITIALIZATION ==========
        // This region contains the Form1 constructor which is called when the application starts.
        // It initializes all UI components including the line number panel, editor control,
        // and wires up event handlers for text changes, drag-drop, and form lifecycle events.

        /// <summary>
        /// Initializes the main form and sets up all UI components.
        /// This constructor creates the line number panel, editor control, and wires up all events.
        /// Called once at application startup before the form is shown.
        /// </summary>
        public Form1()
        {
            // Initialize Windows Forms designer-generated components (menus, status bar, etc.)
            InitializeComponent();

            // Create and configure the line number gutter on the left side
            BuildLineNumberPanel();

            // Create and configure the main RichTextBox editor
            BuildEditor();

            // Wire up all event handlers for text changes, drag-drop, and form lifecycle
            HookEvents();

            // Set up the right-click context menu for the editor
            WireContextMenu();
        }

        #endregion

        #region ========== UI CONSTRUCTION: BUILD LINE NUMBER PANEL ==========
        // This region contains code for creating and configuring the line number panel.
        // The panel appears on the left side of the editor and shows line numbers.
        // It is manually painted in the PnlLineNumbers_Paint event handler.

        /// <summary>
        /// Creates and configures the line number panel that appears on the left side of the editor.
        /// This panel displays line numbers and highlights lines that contain triage keywords when triage mode is active.
        /// Also highlights the current line with a light blue background when in triage mode.
        /// </summary>
        private void BuildLineNumberPanel()
        {
            // Dock the panel to the left edge so it stays fixed during resizing
            pnlLineNumbers.Dock = DockStyle.Left;

            // Set width to accommodate typical line numbers (up to 5-6 digits)
            pnlLineNumbers.Width = 50;

            // Light gray background provides visual separation from editor
            pnlLineNumbers.BackColor = Color.FromArgb(240, 240, 240);

            // Darker gray text for subtle line numbers
            pnlLineNumbers.ForeColor = Color.FromArgb(100, 100, 100);

            // Subscribe to Paint event to manually draw line numbers
            pnlLineNumbers.Paint += PnlLineNumbers_Paint;

            // Add to form's control collection
            this.Controls.Add(pnlLineNumbers);

            // Bring to front so it appears above the editor
            pnlLineNumbers.BringToFront();
        }

        #endregion

        #region ========== UI CONSTRUCTION: BUILD EDITOR ==========
        // This region creates the main RichTextBox editor control.
        // The editor is the primary text editing surface where users view and modify log files.
        // It also sets up Ctrl+Click handling for marking lines.

        /// <summary>
        /// Creates and configures the main RichTextBox editor control.
        /// This is the primary text editing surface where users view and modify log files.
        /// Also sets up Ctrl+Click handling for marking lines.
        /// Side effects: Adds rtbEditor to form's control collection, wires MouseDown event.
        /// </summary>
        private void BuildEditor()
        {
            // Create the RichTextBox instance
            rtbEditor = new RichTextBox();

            // Fill remaining space after line number panel
            rtbEditor.Dock = DockStyle.Fill;

            // Use Consolas font for clear monospace display (important for log alignment)
            rtbEditor.Font = new Font("Consolas", 11f);

            // Start with default white background
            rtbEditor.BackColor = Color.White;

            // Start with default black text
            rtbEditor.ForeColor = Color.Black;

            // Enable word wrap for easier reading of long lines
            rtbEditor.WordWrap = true;

            // Allow Tab key to insert tabs rather than moving focus
            rtbEditor.AcceptsTab = true;

            // Enable drag-drop of files into the editor
            rtbEditor.AllowDrop = true;

            // Add to form's control collection
            this.Controls.Add(rtbEditor);

            // Bring to front so it layers above other controls
            rtbEditor.BringToFront();

            // Wire up Ctrl+Click handler for marking lines
            // Example scenario: User Ctrl+Clicks on line 45 to mark it for review
            rtbEditor.MouseDown += (s, e) =>
            {
                // Check if Control key is held during mouse click
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    // Convert mouse position to character index in the text
                    int charIndex = rtbEditor.GetCharIndexFromPosition(e.Location);

                    // Mark or unmark the clicked line
                    HandleControlClick(charIndex);
                }
            };
        }

        /// <summary>
        /// Handles Ctrl+Click on a line to toggle its marked state.
        /// Marked lines are highlighted with a light blue background color.
        /// This allows users to visually flag important lines for review.
        /// Side effects: Updates markedRanges list, triggers RepaintAllHighlights to apply/remove colors.
        /// </summary>
        /// <param name="charIndex">The character index where the user clicked.</param>
        private void HandleControlClick(int charIndex)
        {
            // Convert character index to line number
            int line = rtbEditor.GetLineFromCharIndex(charIndex);

            // Get the full line range including newline characters
            GetLineRange(line, out int lineStart, out int lineLength);

            // Check if this line is already marked
            var existing = markedRanges.FindIndex(r => r.start == lineStart && r.length == lineLength);

            if (existing >= 0)
            {
                // Line is already marked, so unmark it by removing from list
                markedRanges.RemoveAt(existing);
            }
            else
            {
                // Line is not marked, so add it to the marked ranges list
                markedRanges.Add((lineStart, lineLength));
            }

            // Repaint all highlights to correctly apply or remove the background color
            // This ensures the base background is reset and all highlight layers are reapplied
            // in the correct order: marked lines -> triage -> search
            RepaintAllHighlights();
        }

        #endregion

        #region ========== UI CONSTRUCTION: CONTEXT MENU ==========
        // This region creates and wires up the right-click context menu for the editor.
        // The menu provides quick access to clipboard operations, line marking,
        // hex/UDS conversion tools, template insertion, and text analysis.

        /// <summary>
        /// Creates and wires up the right-click context menu for the editor.
        /// Provides quick access to common editing, marking, and tool functions.
        /// Side effects: Creates and attaches ContextMenuStrip to rtbEditor.
        /// </summary>
        private void WireContextMenu()
        {
            // Create new context menu strip
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            // Standard clipboard operations - updated to use plain text methods
            contextMenu.Items.Add("Cu&t", null, (s, e) => {
                if (rtbEditor.SelectionLength > 0)
                {
                    CopySelectionAsPlainText();
                    rtbEditor.SelectedText = "";
                    isDirty = true;
                    UpdateTitle();
                }
            });
            contextMenu.Items.Add("&Copy", null, (s, e) => CopySelectionAsPlainText());
            contextMenu.Items.Add("&Paste", null, (s, e) => PasteAndMaintainHighlights());
            contextMenu.Items.Add("Select &All", null, (s, e) => rtbEditor.SelectAll());

            // Separator for visual grouping
            contextMenu.Items.Add(new ToolStripSeparator());

            // Line marking operations (for flagging important lines)
            contextMenu.Items.Add("&Toggle Mark Line", null, (s, e) => ToggleMarkLine());
            contextMenu.Items.Add("C&lear Marked Lines", null, (s, e) => ClearMarkedLines());

            // Separator before conversion tools
            contextMenu.Items.Add(new ToolStripSeparator());

            // Automotive and hex conversion tools
            contextMenu.Items.Add("Decode &UDS Service", null, (s, e) => MenuTools_DecodeUDS_Click(this, e));
            contextMenu.Items.Add("&Hex to ASCII", null, (s, e) => MenuTools_HexToAscii_Click(this, e));
            contextMenu.Items.Add("&ASCII to Hex", null, (s, e) => MenuTools_AsciiToHex_Click(this, e));

            // Separator before template operations
            contextMenu.Items.Add(new ToolStripSeparator());

            // Template insertion tools
            contextMenu.Items.Add("&Insert Template", null, (s, e) => MenuTools_InsertTemplate_Click(this, e));
            contextMenu.Items.Add("&Undo Template Insertion", null, (s, e) => MenuTools_UndoTemplateInsertion_Click(this, e));

            // Separator before analysis tools
            contextMenu.Items.Add(new ToolStripSeparator());

            // Text analysis tools
            contextMenu.Items.Add("&Count Occurrences of Selection", null, (s, e) => MenuTools_CountOccurrences_Click(this, e));

            // Attach context menu to the editor
            rtbEditor.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Converts selected hex bytes to ASCII text representation.
        /// Non-printable characters (below 0x20 or above 0x7E) are replaced with dots.
        /// Handles multiple hex formats: "0xFF", "FF", or space-separated hex values.
        /// Prompts user to confirm before applying changes to the document.
        /// Side effects: May replace selected text and set isDirty=true if user confirms.
        /// Example scenario: User selects "48 65 6C 6C 6F" and converts to "Hello"
        /// </summary>
        /// <param name="form1">Reference to the main form (unused, kept for consistency).</param>
        /// <param name="e">Event arguments (unused).</param>
        private void MenuTools_HexToAscii_Click(Form1 form1, EventArgs e)
        {
            // Check if user has selected text to convert
            if (rtbEditor.SelectedText.Length == 0)
            {
                MessageBox.Show(this, "Please select hex text to convert.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Get the selected hex text
                string input = rtbEditor.SelectedText;

                // Parse all recognizable hex bytes from the input string
                // This handles various formats: 0xFF, FF, or space-separated
                List<byte> bytes = ExtractHexBytes(input);

                // If no valid hex bytes were found, notify user
                if (bytes.Count == 0)
                {
                    MessageBox.Show(this, "Could not find any recognizable hex bytes.", "Invalid Hex", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Build ASCII representation
                StringBuilder sb = new StringBuilder();
                foreach (byte byteValue in bytes)
                {
                    // Check if byte is in printable ASCII range (space to tilde)
                    if (byteValue < 0x20 || byteValue > 0x7E)
                    {
                        // Non-printable character: replace with dot for clarity
                        sb.Append('.');
                    }
                    else
                    {
                        // Printable character: add as-is
                        sb.Append((char)byteValue);
                    }
                }

                // Get final result string
                string result = sb.ToString();

                // Show preview and ask user to confirm replacement
                DialogResult dr = MessageBox.Show(this, $"Converted Result:\n\n{result}\n\nApply this change to the document?",
                    "Hex to ASCII Conversion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                // If user confirms, replace selected text with ASCII result
                if (dr == DialogResult.Yes)
                {
                    rtbEditor.SelectedText = result;

                    // Mark document as modified
                    isDirty = true;

                    // Update title to show unsaved changes indicator
                    UpdateTitle();
                }
            }
            catch (Exception ex)
            {
                // Handle any parsing errors gracefully
                MessageBox.Show(this, $"Invalid hex input: {ex.Message}", "Conversion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Extracts all recognizable hex byte values from a string.
        /// Supports multiple formats: "0xFF" prefix notation and plain "FF" pairs.
        /// Skips invalid or incomplete hex sequences gracefully.
        /// Used for parsing hex dumps, log entries, and diagnostic data.
        /// </summary>
        /// <param name="input">The input string containing hex values in various formats.</param>
        /// <returns>A list of parsed byte values.</returns>
        private static List<byte> ExtractHexBytes(string input)
        {
            // Initialize result list
            List<byte> bytes = new List<byte>();

            // Handle empty or whitespace input
            if (string.IsNullOrWhiteSpace(input))
                return bytes;

            // Convert to uppercase for case-insensitive parsing
            string upper = input.Trim().ToUpperInvariant();

            // Scan through the string character by character
            for (int i = 0; i < upper.Length; i++)
            {
                // Check for "0x" prefix format
                if (upper[i] == '0' && i + 1 < upper.Length && upper[i + 1] == 'X')
                {
                    // Accumulate up to 2 hex digits after "0x"
                    StringBuilder sb = new StringBuilder(2);
                    for (int j = i + 2; j < upper.Length && sb.Length < 2; j++)
                    {
                        if (Uri.IsHexDigit(upper[j]))
                        {
                            sb.Append(upper[j]);
                        }
                        else if (sb.Length > 0)
                        {
                            // Stop if we hit non-hex character after finding some digits
                            break;
                        }
                    }

                    // If we collected exactly 2 hex digits, parse as byte
                    if (sb.Length == 2)
                    {
                        try
                        {
                            bytes.Add(Convert.ToByte(sb.ToString(), 16));
                        }
                        catch
                        {
                            // Ignore invalid byte conversions (shouldn't happen with valid hex)
                        }
                    }

                    // Skip past the "0x" and hex digits we just processed
                    i = Math.Min(i + 1 + sb.Length, upper.Length - 1);
                    continue;
                }

                // Check for plain hex digit pairs (e.g., "FF" or "0F")
                if (Uri.IsHexDigit(upper[i]) && i + 1 < upper.Length && Uri.IsHexDigit(upper[i + 1]))
                {
                    try
                    {
                        // Parse two consecutive hex digits as a byte
                        bytes.Add(Convert.ToByte($"{upper[i]}{upper[i + 1]}", 16));
                    }
                    catch
                    {
                        // Ignore invalid byte conversions
                    }

                    // Skip the second digit since we already processed both
                    i++;
                }
            }

            return bytes;
        }

        /// <summary>
        /// Converts selected ASCII text to hexadecimal representation.
        /// Each character is converted to its hex byte value (e.g., 'A' becomes "41").
        /// Prompts user to confirm before applying changes to the document.
        /// Side effects: May replace selected text and set isDirty=true if user confirms.
        /// Example scenario: User selects "Hello" and converts to "48 65 6C 6C 6F"
        /// </summary>
        /// <param name="form1">Reference to the main form (unused, kept for consistency).</param>
        /// <param name="e">Event arguments (unused).</param>
        private void MenuTools_AsciiToHex_Click(Form1 form1, EventArgs e)
        {
            // Check if user has selected text to convert
            if (rtbEditor.SelectedText.Length == 0)
            {
                MessageBox.Show(this, "Please select text to convert to hex.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Get the selected ASCII text
                string ascii = rtbEditor.SelectedText;

                // Build hex representation with spaces between bytes
                StringBuilder sb = new StringBuilder();
                foreach (char c in ascii)
                {
                    // Convert character to its integer value, then format as 2-digit hex
                    sb.Append(((int)c).ToString("X2"));

                    // Add space for readability
                    sb.Append(" ");
                }
                
                // Remove trailing space
                string result = sb.ToString().TrimEnd();

                // Show preview and ask user to confirm replacement
                DialogResult dr = MessageBox.Show(this, $"Converted Result:\n\n{result}\n\nApply this change to the document?", 
                    "ASCII to Hex Conversion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                // If user confirms, replace selected text with hex result
                if (dr == DialogResult.Yes)
                {
                    rtbEditor.SelectedText = result;

                    // Mark document as modified
                    isDirty = true;

                    // Update title to show unsaved changes indicator
                    UpdateTitle();
                }
            }
            catch (Exception ex)
            {
                // Handle any conversion errors gracefully
                MessageBox.Show(this, $"Conversion error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Decodes a UDS (Unified Diagnostic Services) service code to its human-readable name.
        /// UDS is an automotive diagnostic protocol used in vehicle ECUs.
        /// Supports common service codes like 0x10 (DiagnosticSessionControl), 0x27 (SecurityAccess), etc.
        /// Returns "Unknown Service" if the code is not recognized.
        /// </summary>
        /// <param name="code">The hex service code string to decode (e.g., "0x10" or "10").</param>
        /// <returns>The human-readable service name or "Unknown Service" if not recognized.</returns>
        private string DecodeUDSService(string code)
        {
            // Extract the first hex byte from the input string
            string? hexByte = ExtractFirstHexByte(code);

            // If no valid hex byte found, return unknown
            if (string.IsNullOrEmpty(hexByte))
            {
                return "Unknown Service";
            }

            // Match hex byte to known UDS service codes
            // These are standard codes from ISO 14229 (UDS protocol)
            return hexByte switch
            {
                "10" => "DiagnosticSessionControl",
                "11" => "ECUReset",
                "14" => "ClearDiagnosticInformation",
                "19" => "ReadDTCInformation",
                "22" => "ReadDataByIdentifier",
                "27" => "SecurityAccess",
                "3E" => "TesterPresent",
                _ => "Unknown Service"
            };
        }

        /// <summary>
        /// Extracts the first valid hex byte from a string.
        /// Looks for "0x" prefix format first, then falls back to plain hex digit pairs.
        /// Used by UDS decoding to parse service codes from log entries.
        /// Returns null if no valid hex byte is found.
        /// </summary>
        /// <param name="input">The input string to parse.</param>
        /// <returns>A 2-character hex string (e.g., "10" or "FF"), or null if no valid byte found.</returns>
        private static string? ExtractFirstHexByte(string input)
        {
            // Handle empty or whitespace input
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // Convert to uppercase for case-insensitive parsing
            string upper = input.Trim().ToUpperInvariant();

            // First, try to find "0x" prefix format
            int prefixIndex = upper.IndexOf("0X", StringComparison.Ordinal);
            if (prefixIndex >= 0)
            {
                // Accumulate up to 2 hex digits after "0x"
                StringBuilder sb = new StringBuilder(2);
                for (int i = prefixIndex + 2; i < upper.Length && sb.Length < 2; i++)
                {
                    char c = upper[i];
                    if (Uri.IsHexDigit(c))
                    {
                        sb.Append(c);
                    }
                    else if (sb.Length > 0)
                    {
                        // Stop if we hit non-hex character after finding some digits
                        break;
                    }
                }

                // Return if we found exactly 2 hex digits
                if (sb.Length == 2)
                    return sb.ToString();
            }

            // Fallback: look for any 2 consecutive hex digits
            StringBuilder buffer = new StringBuilder(2);
            foreach (char c in upper)
            {
                if (Uri.IsHexDigit(c))
                {
                    buffer.Append(c);

                    // Return as soon as we have 2 hex digits
                    if (buffer.Length == 2)
                        return buffer.ToString();
                }
                else
                {
                    // Reset buffer if we hit non-hex character
                    buffer.Clear();
                }
            }

            // No valid hex byte found
            return null;
        }

        #endregion

        #region ========== EVENT HANDLERS: EDITOR EVENTS ==========
        // This region handles all events related to the editor control itself.
        // Includes TextChanged (triggers on typing/paste), SelectionChanged (triggers on cursor movement),
        // and the Paint event for the line number panel.
        // Guard flags prevent re-entrancy during highlighting operations.

        /// <summary>
        /// Handles text changes in the editor.
        /// Triggered whenever the user types, pastes, or programmatically modifies text.
        /// Updates the dirty flag, title bar, status bar, and redraws line numbers.
        /// If triage mode is active, rescans document for triage keywords.
        /// Side effects: Sets isDirty=true, updates title/status, invalidates line panel, may update triagedLines.
        /// </summary>
        /// <param name="sender">The RichTextBox that raised the event.</param>
        /// <param name="e">Event arguments (unused).</param>
        private void RichTextBox_TextChanged(object? sender, EventArgs e)
        {
            // Mark document as modified (unsaved changes exist)
            isDirty = true;

            // Update title bar to show asterisk for unsaved changes
            UpdateTitle();

            // Update status bar with new line/char counts
            UpdateStatusBar();

            // Redraw line numbers panel to reflect new line count
            if (pnlLineNumbers != null)
            {
                pnlLineNumbers.Invalidate();
            }

            // If triage mode is active, rescan document for keywords
            // This ensures new/modified text is checked for triage keywords
            if (isLogTriageMode)
            {
                UpdateTriagedLines();
            }
        }

        /// <summary>
        /// Handles selection changes in the editor.
        /// Triggered when the user moves the cursor or changes the selected text.
        /// Updates the status bar with new cursor position and selection length.
        /// Guard flags prevent re-entrancy during highlight operations.
        /// Side effects: Updates status bar, invalidates line panel.
        /// </summary>
        /// <param name="sender">The RichTextBox that raised the event.</param>
        /// <param name="e">Event arguments (unused).</param>
        private void RichTextBox_SelectionChanged(object? sender, EventArgs e)
        {
            // Prevent re-entrancy during highlighting operations
            // Without these guards, highlighting would trigger selection changes,
            // which would trigger more highlighting, causing infinite recursion
            if (isHighlightingInProgress || isInternalRepaint)
                return;

            // Update status bar with new cursor position and selection info
            UpdateStatusBar();

            // Redraw line numbers panel to highlight current line (if triage mode is on)
            if (pnlLineNumbers != null)
            {
                pnlLineNumbers.Invalidate();
            }
        }

        /// <summary>
        /// Handles custom painting of the line number panel.
        /// Draws line numbers aligned to the right, corresponding to visible editor lines.
        /// When triage mode is active, highlights line numbers that contain triage keywords.
        /// Also highlights the current line with a light blue background when in triage mode.
        /// Uses Win32 API to query which lines are visible to optimize rendering.
        /// Side effects: Draws to Graphics object, creates/disposes GDI objects.
        /// </summary>
        /// <param name="sender">The Panel that raised the Paint event.</param>
        /// <param name="e">Paint event arguments containing the Graphics object.</param>
        private void PnlLineNumbers_Paint(object? sender, PaintEventArgs e)
        {
            // Ensure we have valid paint event arguments
            if (e == null) return;

            // Clear panel with background color before drawing
            e.Graphics.Clear(pnlLineNumbers.BackColor);

            // Query the first visible line using Win32 API (for performance)
            // This lets us only draw line numbers for visible lines, not all lines
            int firstVisibleLine = SendMessage(rtbEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);

            // Get total number of lines in the document
            int totalLines = rtbEditor.Lines.Length;

            // Create font slightly smaller than editor font for subtlety
            Font lineNumberFont = new Font(rtbEditor.Font.FontFamily, rtbEditor.Font.Size - 2);

            // Create brush matching panel foreground color
            Brush lineNumberBrush = new SolidBrush(pnlLineNumbers.ForeColor);

            // Determine which line contains the cursor (1-based for display)
            int currentLine = rtbEditor.GetLineFromCharIndex(rtbEditor.SelectionStart) + 1;

            // Track vertical position as we draw line numbers
            int yPosition = 0;

            // Draw line numbers for all visible lines
            for (int lineNumber = firstVisibleLine + 1; lineNumber <= totalLines && yPosition < pnlLineNumbers.Height; lineNumber++)
            {
                // Check if this line should be highlighted in triage mode
                // Triage lines contain keywords like FAIL, ERROR, DENIED, etc.
                bool isTriageLine = isLogTriageMode && triagedLines.Contains(lineNumber - 1);

                // Highlight background if this is a triaged line
                if (isTriageLine)
                {
                    // Draw orange background for triaged lines
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 230, 200)), 0, yPosition, pnlLineNumbers.Width, (int)lineNumberFont.Height + 2);
                }
                else if (lineNumber == currentLine && isLogTriageMode)
                {
                    // Draw light blue background rectangle for current line (when not a triage line)
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(200, 220, 255)), 0, yPosition, pnlLineNumbers.Width, (int)lineNumberFont.Height + 2);
                }

                // Convert line number to string for display
                string lineText = lineNumber.ToString();

                // Measure text to calculate right-alignment position
                SizeF textSize = e.Graphics.MeasureString(lineText, lineNumberFont);

                // Draw line number right-aligned with 5px padding from edge
                e.Graphics.DrawString(lineText, lineNumberFont, lineNumberBrush, pnlLineNumbers.Width - textSize.Width - 5, yPosition);

                // Move to next line position (font height + 2px padding)
                yPosition += (int)(lineNumberFont.Height + 2);
            }

            // Clean up GDI resources
            lineNumberFont.Dispose();
            lineNumberBrush.Dispose();
        }

        #endregion

        #region ========== EVENT HANDLERS: DRAG AND DROP ==========
        // This region handles drag-and-drop operations for opening files.
        // Includes DragEnter (shows appropriate cursor) and DragDrop (opens the file).
        // Works for both the editor control and the form itself.

        /// <summary>
        /// Handles the DragEnter event when a file is dragged over the editor.
        /// Shows a "copy" cursor if the dragged data is a file, otherwise shows "no drop" cursor.
        /// This provides visual feedback to the user that file drops are supported.
        /// </summary>
        /// <param name="sender">The RichTextBox that raised the event.</param>
        /// <param name="e">Drag event arguments containing information about the dragged data.</param>
        private void RichTextBox_DragEnter(object? sender, DragEventArgs e)
        {
            // Check if dragged data is a file
            if (e != null && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Show copy cursor to indicate drop is allowed
                e.Effect = DragDropEffects.Copy;
            }
            else if (e != null)
            {
                // Show no-drop cursor for non-file data
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// Handles the DragDrop event when a file is dropped onto the editor.
        /// Prompts to save current document if modified, then opens the dropped file.
        /// Only the first file is opened if multiple files are dropped.
        /// Side effects: May save current file, opens new file, updates UI.
        /// </summary>
        /// <param name="sender">The RichTextBox that raised the event.</param>
        /// <param name="e">Drag event arguments containing the dropped file paths.</param>
        private void RichTextBox_DragDrop(object? sender, DragEventArgs e)
        {
            // Check if dropped data is a file
            if (e != null && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Get array of dropped file paths
                object? droppedData = e.Data.GetData(DataFormats.FileDrop);
                string[]? files = droppedData as string[];

                // If at least one file was dropped
                if (files != null && files.Length > 0)
                {
                    // Prompt to save current document if it has unsaved changes
                    PromptSaveIfDirty();

                    // Open the first dropped file
                    OpenFileFromPath(files[0]);
                }
            }
        }

        #endregion

        #region ========== EVENT HANDLERS: FORM-LEVEL EVENTS ==========
        // This region hooks up event handlers for the form and its controls.
        // Includes text changes, selection changes, drag-drop, and form closing events.
        // Also handles the save prompt when closing with unsaved changes.

        /// <summary>
        /// Hooks up all event handlers for the form and editor control.
        /// This includes text changes, selection changes, drag-drop, and form lifecycle events.
        /// Called once during form initialization.
        /// Side effects: Wires up multiple event handlers.
        /// </summary>
        private void HookEvents()
        {
            // Editor text modification events
            rtbEditor.TextChanged += RichTextBox_TextChanged;
            rtbEditor.SelectionChanged += RichTextBox_SelectionChanged;

            // Editor drag-drop events
            rtbEditor.DragEnter += RichTextBox_DragEnter;
            rtbEditor.DragDrop += RichTextBox_DragDrop;

            // Form lifecycle events
            this.FormClosing += Form1_FormClosing;

            // Form-level drag-drop support (for files dropped on the form itself)
            this.DragEnter += (s, e) =>
            {
                // Check if dragged data is a file
                if (e != null && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Show copy cursor to indicate drop is allowed
                    e.Effect = DragDropEffects.Copy;
                }
            };

            // Handle files dropped on the form (not just the editor)
            this.DragDrop += (s, e) =>
            {
                // Check if dropped data is a file
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Get array of dropped file paths
                    string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];

                    if (files != null && files.Length > 0)
                    {
                        // Prompt to save current document if it has unsaved changes
                        // If user cancels, abort the file open operation
                        if (!PromptSaveIfDirty())
                            return;

                        // Store the file path for future save operations
                        currentFilePath = files[0];

                        // Load the dropped file into the editor
                        OpenFileFromPath(currentFilePath);
                    }
                }
            };
        }

        /// <summary>
        /// Handles the form closing event.
        /// Prompts user to save if document has unsaved changes.
        /// Cancels the close operation if user clicks Cancel in the save prompt.
        /// Side effects: May cancel form close by setting e.Cancel=true.
        /// </summary>
        /// <param name="sender">The Form that raised the event.</param>
        /// <param name="e">Form closing event arguments that can be used to cancel the close.</param>
        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Prompt to save if document is dirty
            if (!PromptSaveIfDirty())
            {
                // User clicked Cancel, so abort the close operation
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Prompts the user to save changes if the document has been modified.
        /// Shows a Yes/No/Cancel dialog: Yes saves and continues, No discards changes,
        /// Cancel aborts the operation that triggered the prompt.
        /// Side effects: May save file and clear isDirty flag.
        /// </summary>
        /// <returns>True if the operation should proceed, false if it should be cancelled.</returns>
        private bool PromptSaveIfDirty()
        {
            // If document hasn't been modified, no need to prompt
            if (!isDirty)
            {
                return true;
            }

            // Show save prompt with Yes/No/Cancel options
            DialogResult result = MessageBox.Show(
                this,
                "The document has unsaved changes. Do you want to save before proceeding?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                // User wants to save: trigger save operation
                SaveFile();
                return true;
            }

            if (result == DialogResult.No)
            {
                // User wants to discard changes: proceed without saving
                return true;
            }

            // User clicked Cancel: abort the operation
            return false;
        }

        /// <summary>
        /// Toggles the marked state of the line containing the cursor.
        /// Marked lines are highlighted with a light blue background for visual reference.
        /// This is useful for flagging important log entries during analysis.
        /// Side effects: Updates markedRanges, triggers RepaintAllHighlights.
        /// </summary>
        private void ToggleMarkLine()
        {
            // Get current cursor position
            int cursorPos = rtbEditor.SelectionStart;

            // Convert cursor position to line number
            int line = rtbEditor.GetLineFromCharIndex(cursorPos);

            // Get the full line range including newline characters
            GetLineRange(line, out int lineStart, out int lineLength);

            // Check if this line is already marked
            var existing = markedRanges.FindIndex(r => r.start == lineStart && r.length == lineLength);

            if (existing >= 0)
            {
                // Line is already marked, so unmark it by removing from list
                markedRanges.RemoveAt(existing);
            }
            else
            {
                // Line is not marked, so add it to the marked ranges list
                markedRanges.Add((lineStart, lineLength));
            }

            // Repaint all highlights to reflect the change
            RepaintAllHighlights();
        }

        /// <summary>
        /// Clears all marked lines from the document.
        /// Removes all light blue background highlights that were added by marking lines.
        /// Repaints the document to show the cleared state.
        /// Side effects: Clears markedRanges, triggers RepaintAllHighlights.
        /// </summary>
        private void ClearMarkedLines()
        {
            // Remove all entries from the marked ranges list
            markedRanges.Clear();

            // Repaint document to remove all background highlights
            RepaintAllHighlights();
        }

        /// <summary>
        /// Decodes the selected UDS service code and displays a human-readable name.
        /// UDS is an automotive diagnostic protocol with standard service codes.
        /// Shows a message box with the decoded service name.
        /// </summary>
        /// <param name="form1">Reference to the main form (unused, kept for consistency).</param>
        /// <param name="e">Event arguments (unused).</param>
        private void MenuTools_DecodeUDS_Click(Form1 form1, EventArgs e)
        {
            // Check if user has selected text to decode
            if (rtbEditor.SelectedText.Length == 0)
            {
                MessageBox.Show(this, "Please select UDS service text to decode.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Get the selected text and trim whitespace
            string udsCode = rtbEditor.SelectedText.Trim();

            // Decode the UDS service code to human-readable name
            string decoded = DecodeUDSService(udsCode);

            // Display the result in a message box
            MessageBox.Show(this, $"UDS Service {udsCode}: {decoded}", "UDS Decode", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Counts how many times the selected text appears in the entire document.
        /// Search is case-insensitive.
        /// Displays the count in a message box.
        /// </summary>
        /// <param name="form1">Reference to the main form (unused, kept for consistency).</param>
        /// <param name="e">Event arguments (unused).</param>
        private void MenuTools_CountOccurrences_Click(Form1 form1, EventArgs e)
        {
            // Check if user has selected text to count
            if (rtbEditor.SelectedText.Length == 0)
            {
                MessageBox.Show(this, "Please select text to count occurrences.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Get the selected text
            string selectedText = rtbEditor.SelectedText;

            // Count how many times it appears in the document
            int count = CountOccurrences(selectedText);

            // Display the count result
            MessageBox.Show(this, $"Found {count} occurrence(s) of '{selectedText}'.", "Count Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Undoes the last template insertion by restoring the document content from before the insertion.
        /// This provides a quick way to revert if a template was inserted by mistake.
        /// Only works immediately after a template insertion (before other text changes).
        /// Side effects: Restores text, clears contentBeforeTemplate, sets isDirty=true.
        /// </summary>
        /// <param name="form1">Reference to the main form (unused, kept for consistency).</param>
        /// <param name="e">Event arguments (unused).</param>
        private void MenuTools_UndoTemplateInsertion_Click(Form1 form1, EventArgs e)
        {
            // Check if there's a saved state to restore
            if (string.IsNullOrEmpty(contentBeforeTemplate))
            {
                MessageBox.Show(this, "No template insertion to undo.", "No History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Restore the document to its state before template insertion
            rtbEditor.Text = contentBeforeTemplate;

            // Clear the saved state (can only undo once)
            contentBeforeTemplate = string.Empty;

            // Mark document as modified
            isDirty = true;

            // Update title bar to show unsaved changes
            UpdateTitle();

            // Update status bar with restored document stats
            UpdateStatusBar();

            // Confirm the undo operation
            MessageBox.Show(this, "Template insertion undone.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Shows a dialog for inserting pre-defined text templates into the document.
        /// Templates include common automotive log structures like UDS sessions and CAN frames.
        /// Saves the current document state before insertion to allow undo.
        /// Side effects: Appends template text, sets isDirty=true, saves contentBeforeTemplate.
        /// </summary>
        /// <param name="form1">Reference to the main form (unused, kept for consistency).</param>
        /// <param name="e">Event arguments (unused).</param>
        private void MenuTools_InsertTemplate_Click(Form1 form1, EventArgs e)
        {
            // Create template selection dialog
            using (Form templateDialog = new Form())
            {
                // Configure dialog appearance
                templateDialog.Text = "Insert Template";
                templateDialog.Width = 350;
                templateDialog.Height = 200;
                templateDialog.StartPosition = FormStartPosition.CenterParent;
                templateDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                templateDialog.MaximizeBox = false;
                templateDialog.MinimizeBox = false;

                // Create prompt label
                Label promptLabel = new Label() { Left = 20, Top = 20, Text = "Select a template:", Width = 300, AutoSize = false };

                // Create listbox with template options
                ListBox templateListBox = new ListBox() { Left = 20, Top = 50, Width = 300, Height = 80 };
                templateListBox.Items.Add("UDS Diagnostic Session");
                templateListBox.Items.Add("CAN Frame Template");
                templateListBox.Items.Add("Error Log Template");
                templateListBox.Items.Add("Automotive Report");

                // Create dialog buttons
                Button insertButton = new Button() { Text = "&Insert", Left = 150, Top = 140, Width = 70, DialogResult = DialogResult.OK };
                Button cancelButton = new Button() { Text = "Cancel", Left = 230, Top = 140, Width = 70, DialogResult = DialogResult.Cancel };

                // Add controls to dialog
                templateDialog.Controls.Add(promptLabel);
                templateDialog.Controls.Add(templateListBox);
                templateDialog.Controls.Add(insertButton);
                templateDialog.Controls.Add(cancelButton);

                // Set default buttons for Enter/Escape keys
                templateDialog.AcceptButton = insertButton;
                templateDialog.CancelButton = cancelButton;

                // Show dialog and wait for user response
                if (templateDialog.ShowDialog(this) == DialogResult.OK && templateListBox.SelectedIndex >= 0)
                {
                    // Save current document state for undo functionality
                    contentBeforeTemplate = rtbEditor.Text;

                    // Get selected template name
                    string template = templateListBox.SelectedItem?.ToString() ?? "";

                    // Map template name to actual template text
                    string templateText = template switch
                    {
                        "UDS Diagnostic Session" => "=== UDS Diagnostic Session ===\nService: 0x10\nSubFunction: 0x01\nSession Type: Default\n\n",
                        "CAN Frame Template" => "CAN ID: 0x000\nDLC: 8\nData: [00 00 00 00 00 00 00 00]\n\n",
                        "Error Log Template" => "=== Error Log ===\nTimestamp: \nError Code: \nDescription: \nSeverity: \n\n",
                        "Automotive Report" => "=== Automotive Diagnostics Report ===\nVehicle: \nDate: \nMileage: \nIssues Found: \n\n",
                        _ => ""
                    };

                    // Append template to end of document
                    rtbEditor.Text += templateText;

                    // Mark document as modified
                    isDirty = true;

                    // Update title bar to show unsaved changes
                    UpdateTitle();

                    // Update status bar with new document stats
                    UpdateStatusBar();
                }
            }
        }

        /// <summary>
        /// Helper method to calculate the full range of a line including newline characters.
        /// Returns the starting character index and the length from line start to next line start (or end of document).
        /// This ensures marked lines include the entire line with newline characters for consistent highlighting.
        /// </summary>
        /// <param name="line">The 0-based line number.</param>
        /// <param name="start">Output: starting character index of the line.</param>
        /// <param name="length">Output: length from line start to next line start (or document end).</param>
        private void GetLineRange(int line, out int start, out int length)
        {
            // Get the starting character index of this line
            start = rtbEditor.GetFirstCharIndexFromLine(line);

            // Calculate where the next line starts (or end of document if last line)
            int nextStart = (line + 1 < rtbEditor.Lines.Length)
                ? rtbEditor.GetFirstCharIndexFromLine(line + 1)
                : rtbEditor.TextLength;

            // Length includes the text on the line plus any newline characters
            length = Math.Max(0, nextStart - start);
        }

        /// <summary>
        /// Updates the form title to show the current file name, modification status, and triage mode.
        /// Displays "[Untitled]" for new documents and an asterisk for unsaved changes.
        /// Appends " [TRIAGE: ON]" when triage mode is active.
        /// Side effects: Updates this.Text property.
        /// </summary>
        private void UpdateTitle()
        {
            // Get display name: file name if saved, "[Untitled]" if new
            string displayName = string.IsNullOrEmpty(currentFilePath) ? "[Untitled]" : Path.GetFileName(currentFilePath);

            // Add asterisk if document has unsaved changes
            string dirtyIndicator = isDirty ? " *" : string.Empty;

            // Build base title with application name, file name, and dirty indicator
            string baseTitle = $"AutoCyber Log Editor - {displayName}{dirtyIndicator}";
            
            // Append triage mode indicator only if triage mode is active
            if (isLogTriageMode)
            {
                this.Text = baseTitle + " [TRIAGE: ON]";
            }
            else
            {
                this.Text = baseTitle;
            }
        }

        /// <summary>
        /// Updates the status bar labels with current document statistics.
        /// Shows cursor position, total lines, character count, selection length,
        /// word wrap status, and zoom level.
        /// Handles null checks for status bar labels (may not exist in designer yet).
        /// Side effects: Updates status bar label Text properties only (no longer modifies window title).
        /// </summary>
        private void UpdateStatusBar()
        {
            // Calculate current line number (1-based)
            int currentLine = rtbEditor.GetLineFromCharIndex(rtbEditor.SelectionStart) + 1;

            // Get start position of current line
            int lineStart = rtbEditor.GetFirstCharIndexFromLine(currentLine - 1);

            // Calculate column number within current line (1-based)
            int currentColumn = rtbEditor.SelectionStart - lineStart + 1;

            // Get total line count
            int totalLines = rtbEditor.Lines.Length;

            // Get total character count
            int charCount = rtbEditor.TextLength;

            // Get selected text length
            int selectionLength = rtbEditor.SelectedText.Length;

            // Get word wrap status as ON/OFF string
            string wrapStatus = rtbEditor.WordWrap ? "ON" : "OFF";

            // Update status bar labels (with null checks for designer compatibility)
            if (LabelLineCol != null)
                LabelLineCol.Text = $"Line {currentLine}, Col {currentColumn}";
            if (LabelLineCount != null)
                LabelLineCount.Text = $"Lines: {totalLines}";
            if (LabelCharCount != null)
                LabelCharCount.Text = $"Chars: {charCount}";
            if (LabelSelection != null)
                LabelSelection.Text = $"Selection: {selectionLength}";
            if (LabelWordWrap != null)
                LabelWordWrap.Text = $"Word Wrap: {wrapStatus}";
            if (LabelZoom != null)
                LabelZoom.Text = $"Zoom: {zoomPercentField}%";

            // Note: Triage mode status is now handled in UpdateTitle() only
        }

        /// <summary>
        /// Opens and loads a file from the specified path into the editor.
        /// Resets zoom level and dirty flag.
        /// Updates title and status bar to reflect the loaded file.
        /// Shows error message if file cannot be read.
        /// Side effects: Loads file into editor, resets zoom, clears isDirty, updates UI.
        /// </summary>
        /// <param name="filePath">The full path to the file to open.</param>
        private void OpenFileFromPath(string filePath)
        {
            try
            {
                // Read entire file contents into a string
                string fileContents = File.ReadAllText(filePath);

                // Load file contents into editor
                rtbEditor.Text = fileContents;

                // Store file path for future save operations
                currentFilePath = filePath;

                // Mark document as not modified (freshly loaded)
                isDirty = false;

                // Reset zoom to 100%
                zoomPercentField = 100;
                rtbEditor.ZoomFactor = 1.0f;

                // Update title bar with new file name
                UpdateTitle();

                // Update status bar with document statistics
                UpdateStatusBar();

                // If triage mode is active, scan new document for keywords
                if (isLogTriageMode)
                {
                    UpdateTriagedLines();
                    RepaintAllHighlights();
                }
            }
            catch (Exception ex)
            {
                // Show error message if file cannot be opened
                MessageBox.Show(this, $"Error opening file:\n{ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Clear file path since open failed
                currentFilePath = string.Empty;
            }
        }

        /// <summary>
        /// Saves the current document to disk.
        /// If no file path is set (new document), prompts for save location using Save As dialog.
        /// Shows success or error message after save attempt.
        /// Side effects: Writes file to disk, clears isDirty flag.
        /// </summary>
        private void SaveFile()
        {
            // If no file path set, this is a new document: prompt for location
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveFileAs();
                return;
            }

            try
            {
                // Write editor contents to file
                File.WriteAllText(currentFilePath, rtbEditor.Text);

                // Clear dirty flag (no unsaved changes)
                isDirty = false;

                // Update title to remove asterisk
                UpdateTitle();

                // Show success confirmation
                MessageBox.Show(this, "File saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // Show error message if save fails
                MessageBox.Show(this, $"Error saving file:\n{ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Prompts user to choose a file location and saves the document.
        /// Uses a standard Windows Save File dialog.
        /// Updates the current file path after successful save.
        /// Shows success or error message after save attempt.
        /// Side effects: Writes file to disk, updates currentFilePath, clears isDirty.
        /// </summary>
        private void SaveFileAs()
        {
            // Create Save File dialog
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Title = "Save File As";

            // Set file type filters
            saveDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

            // Start in My Documents by default
            saveDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // If current file has a path, pre-populate the filename and directory
            if (!string.IsNullOrEmpty(currentFilePath))
            {
                saveDialog.FileName = Path.GetFileName(currentFilePath);
                saveDialog.InitialDirectory = Path.GetDirectoryName(currentFilePath);
            }

            // Show dialog and wait for user response
            if (saveDialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    // Write editor contents to selected file
                    File.WriteAllText(saveDialog.FileName, rtbEditor.Text);

                    // Update current file path for future saves
                    currentFilePath = saveDialog.FileName;

                    // Clear dirty flag (no unsaved changes)
                    isDirty = false;

                    // Update title with new file name
                    UpdateTitle();

                    // Show success confirmation
                    MessageBox.Show(this, "File saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    // Show error message if save fails
                    MessageBox.Show(this, $"Error saving file:\n{ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Counts how many times a search string appears in the document.
        /// Search is case-insensitive.
        /// Used by Count Occurrences menu item.
        /// </summary>
        /// <param name="searchText">The text to search for.</param>
        /// <returns>The number of occurrences found.</returns>
        private int CountOccurrences(string searchText)
        {
            // Initialize occurrence counter
            int count = 0;

            // Start searching from beginning
            int pos = 0;

            // Get full document text
            string documentText = rtbEditor.Text;

            // Loop through document finding all occurrences
            while ((pos = documentText.IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                // Found an occurrence: increment counter
                count++;

                // Move past this occurrence to continue searching
                pos += searchText.Length;
            }

            return count;
        }

        #endregion

        #region ========== TRIAGE MODE: TOGGLE & KEYWORD SCANNING ==========
        // This region implements Log Triage Mode functionality.
        // When enabled, it automatically highlights lines containing error keywords
        // like FAIL, ERROR, DENIED, 0x27, 0x7F, NRC.
        // Maintains a HashSet of triaged line indexes for efficient rendering.

        /// <summary>
        /// Toggles Log Triage Mode on or off.
        /// When enabled, automatically highlights all lines containing triage keywords.
        /// Keywords include: FAIL, ERROR, DENIED, 0x27, 0x7F, NRC.
        /// Updates line number panel to highlight triaged lines.
        /// Can be called from menu item or keyboard shortcut (Ctrl+T).
        /// Side effects: Toggles isLogTriageMode, updates triagedLines, repaints highlights and line panel.
        /// Example scenario: User presses Ctrl+T to enable triage mode. All lines with "ERROR" or "FAIL"
        /// are immediately highlighted in orange, making it easy to spot problems in large log files.
        /// </summary>
        public void ToggleLogTriageMode()
        {
            // Toggle the triage mode flag
            isLogTriageMode = !isLogTriageMode;

            if (isLogTriageMode)
            {
                // Triage mode activated: scan document for keywords
                UpdateTriagedLines();

                // Apply triage highlighting to document
                RepaintAllHighlights();
            }
            else
            {
                // Triage mode deactivated: clear triaged lines set
                triagedLines.Clear();

                // Remove triage highlights (return to normal highlighting)
                RepaintAllHighlights();
            }

            // Update status bar to show triage mode status
            UpdateStatusBar();

            // Redraw line number panel to update highlighting
            if (pnlLineNumbers != null)
            {
                pnlLineNumbers.Invalidate();
            }
        }

        /// <summary>
        /// Scans all lines in the document and identifies which ones contain triage keywords.
        /// Updates the triagedLines HashSet with 0-based line indexes.
        /// Called when triage mode is toggled on or when text changes in triage mode.
        /// Search is case-insensitive for text keywords (FAIL, ERROR, DENIED, NRC).
        /// Hex values (0x27, 0x7F) are matched as typed.
        /// Side effects: Updates triagedLines HashSet.
        /// </summary>
        private void UpdateTriagedLines()
        {
            // Clear existing set
            triagedLines.Clear();

            // Get all lines from editor
            string[] lines = rtbEditor.Lines;

            // Scan each line for triage keywords
            for (int i = 0; i < lines.Length; i++)
            {
                // Get line text in uppercase for case-insensitive comparison
                string lineUpper = lines[i].ToUpperInvariant();

                // Check if line contains any triage keyword
                foreach (string keyword in triageKeywords)
                {
                    // For hex values, check as-is (case-sensitive)
                    // For text keywords, already uppercased
                    if (lineUpper.Contains(keyword.ToUpperInvariant()))
                    {
                        // Add line index to set (0-based)
                        triagedLines.Add(i);
                        break; // No need to check other keywords for this line
                    }
                }
            }
        }

        #endregion

        #region ========== HIGHLIGHTING: REPAINT & BASE HIGHLIGHTS ==========
        // This region contains the core highlighting engine that applies background colors
        // to the editor. It handles marked lines (user-flagged), triage lines (auto-detected errors),
        // and search results (yellow Find highlights). Uses BeginUpdate/EndUpdate to prevent flicker,
        // and guard flags to prevent re-entrancy during selection changes.

        /// <summary>
        /// Repaints all background highlights in the document.
        /// Applies highlights in order: marked lines → triage lines → search highlights.
        /// This layering ensures search highlights (yellow) are most visible.
        /// Saves and restores the current selection to avoid disrupting the user's cursor position.
        /// Uses BeginUpdate/EndUpdate to prevent flicker and improve performance.
        /// Sets isInternalRepaint flag to prevent re-entrancy in SelectionChanged event.
        /// Side effects: Changes background colors, uses BeginUpdate/EndUpdate, sets guard flag.
        /// Example scenario: User marks line 10, toggles triage mode (highlights line 15 with ERROR),
        /// then searches for "test". Line 10 is light blue, line 15 is orange, "test" is yellow.
        /// </summary>
        private void RepaintAllHighlights()
        {
            // Save current selection state before modifying colors
            // This prevents the cursor from jumping during repaint
            int originalSelectionStart = rtbEditor.SelectionStart;
            int originalSelectionLength = rtbEditor.SelectionLength;

            // Set flag to prevent SelectionChanged from firing during repaint
            // Without this, each selection change would trigger another SelectionChanged event,
            // causing unnecessary repaints and potential infinite recursion
            isInternalRepaint = true;

            // Suspend redrawing to prevent flicker and improve performance
            // BeginUpdate tells Windows not to repaint until EndUpdate is called
            rtbEditor.BeginUpdate();
            
            try
            {
                // Get appropriate background color based on current theme
                Color defaultBackColor = GetDefaultEditorBackColor();

                // Reset all text to default background color first
                // This clears any existing highlights
                rtbEditor.SelectAll();
                rtbEditor.SelectionBackColor = defaultBackColor;

                // Apply marked line backgrounds (light blue)
                // These are user-marked lines that should always be visible
                foreach (var range in markedRanges)
                {
                    // Validate range is within document bounds (prevents crash on invalid ranges)
                    if (range.start >= 0 && range.start < rtbEditor.TextLength && range.length > 0)
                    {
                        // Select the marked line range
                        rtbEditor.SelectionStart = range.start;

                        // Clamp length to prevent selecting past end of document
                        rtbEditor.SelectionLength = Math.Min(range.length, rtbEditor.TextLength - range.start);

                        // Apply light blue background to marked line
                        rtbEditor.SelectionBackColor = markedLineColor;
                    }
                }

                // Apply triage keyword backgrounds (orange) if triage mode is active
                // Triage highlights are applied after marked lines so they're visible
                if (isLogTriageMode)
                {
                    ApplyTriageHighlights();
                }

                // Restore original selection so user's cursor doesn't move
                rtbEditor.SelectionStart = originalSelectionStart;
                rtbEditor.SelectionLength = originalSelectionLength;
            }
            finally
            {
                // Resume redrawing and force repaint
                // This applies all color changes in one visual update
                rtbEditor.EndUpdate();

                // Clear flag to allow SelectionChanged to fire normally again
                isInternalRepaint = false;
            }
        }

        /// <summary>
        /// Applies triage keyword highlighting to the document.
        /// Highlights all occurrences of each keyword in triageKeywords array.
        /// Uses triageHighlightColor (orange) for triage highlights.
        /// Search is case-insensitive for text keywords.
        /// Must be called within a BeginUpdate/EndUpdate block for performance.
        /// Side effects: Changes SelectionBackColor for keyword matches.
        /// </summary>
        private void ApplyTriageHighlights()
        {
            // Get full document text for searching
            string text = rtbEditor.Text;

            // Highlight each triage keyword
            foreach (string keyword in triageKeywords)
            {
                // Track current search position for this keyword
                int pos = 0;

                // Find and highlight all occurrences of this keyword
                while ((pos = text.IndexOf(keyword, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    // Select the matched keyword
                    rtbEditor.SelectionStart = pos;
                    rtbEditor.SelectionLength = keyword.Length;

                    // Apply orange triage highlight
                    rtbEditor.SelectionBackColor = triageHighlightColor;

                    // Move past this occurrence to continue searching
                    pos += keyword.Length;
                }
            }
        }

        /// <summary>
        /// Shows the Find dialog and searches for the specified text.
        /// Highlights all occurrences in yellow and selects the first/next occurrence.
        /// Wraps around to beginning if search reaches end of document.
        /// Side effects: Updates lastSearchText, changes selection, applies highlights.
        /// Example scenario: User searches for "timeout". First occurrence is selected and
        /// scrolled into view, and all occurrences are highlighted in yellow for visual context.
        /// </summary>
        /// <param name="sender">The menu item or button that triggered the search.</param>
        /// <param name="e">Event arguments (unused).</param>
        public void MenuEdit_Find_Click(object sender, EventArgs e)
        {
            // Show input dialog and get search text from user
            if (!TryGetTextInput("Find", "Find what:", lastSearchText, out string searchText))
                return;

            // Ignore empty or whitespace-only search
            if (string.IsNullOrWhiteSpace(searchText))
                return;

            // Remember search text for next Find operation
            lastSearchText = searchText;

            // Start searching from current selection end (to find next occurrence)
            int startIndex = rtbEditor.SelectionStart + rtbEditor.SelectionLength;

            // Search forward from start index
            int index = rtbEditor.Text.IndexOf(searchText, startIndex, StringComparison.OrdinalIgnoreCase);

            // If not found and we're not at the beginning, wrap around to start
            if (index < 0 && startIndex > 0)
                index = rtbEditor.Text.IndexOf(searchText, 0, StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                // Found: select the matched text
                rtbEditor.SelectionStart = index;
                rtbEditor.SelectionLength = searchText.Length;

                // Scroll to make selection visible
                rtbEditor.ScrollToCaret();

                // Highlight all occurrences in yellow for visual feedback
                HighlightAllOccurrences(searchText);
            }
            else
            {
                // Not found: notify user
                MessageBox.Show(this, $"'{searchText}' was not found.", "Find", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Shows the Replace dialog and replaces text.
        /// Can replace one occurrence or all occurrences depending on user's choice.
        /// Highlights all occurrences if Replace All is used.
        /// Side effects: May modify document text, sets isDirty=true, applies highlights.
        /// </summary>
        /// <param name="sender">The menu item or button that triggered the replace.</param>
        /// <param name="e">Event arguments (unused).</param>
        public void MenuEdit_Replace_Click(object sender, EventArgs e)
        {
            // Show replace dialog and get find/replace text from user
            if (!TryGetReplaceInput(out string findText, out string replaceText, out bool replaceAll))
                return;

            // Ignore empty or whitespace-only search
            if (string.IsNullOrWhiteSpace(findText))
                return;

            if (replaceAll)
            {
                // Replace All mode: replace every occurrence in document
                string updated = ReplaceAllInsensitive(rtbEditor.Text, findText, replaceText, out int count);

                // If nothing was found, notify user
                if (count == 0)
                {
                    MessageBox.Show(this, $"'{findText}' was not found.", "Replace", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Update editor with replaced text
                rtbEditor.Text = updated;

                // Mark document as modified
                isDirty = true;

                // Update title and status bar
                UpdateTitle();
                UpdateStatusBar();

                // Highlight all occurrences (of the original find text, to show what was replaced)
                HighlightAllOccurrences(findText);
            }
            else
            {
                // Replace One mode: find and replace next occurrence only
                int index = rtbEditor.Text.IndexOf(findText, rtbEditor.SelectionStart, StringComparison.OrdinalIgnoreCase);

                // If not found, notify user
                if (index < 0)
                {
                    MessageBox.Show(this, $"'{findText}' was not found.", "Replace", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Select the found text
                rtbEditor.SelectionStart = index;
                rtbEditor.SelectionLength = findText.Length;

                // Replace selected text with replacement
                rtbEditor.SelectedText = replaceText;

                // Mark document as modified
                isDirty = true;

                // Update title and status bar
                UpdateTitle();
                UpdateStatusBar();
            }
        }

        /// <summary>
        /// Shows the Go To Line dialog and jumps to the specified line number.
        /// Validates that line number is within document bounds.
        /// Scrolls to make the target line visible.
        /// Side effects: Changes selection position, scrolls editor.
        /// </summary>
        /// <param name="sender">The menu item or button that triggered the action.</param>
        /// <param name="e">Event arguments (unused).</param>
        public void MenuEdit_GoToLine_Click(object sender, EventArgs e)
        {
            // Get current line number to pre-populate dialog
            int currentLine = rtbEditor.GetLineFromCharIndex(rtbEditor.SelectionStart) + 1;

            // Show input dialog and get line number from user
            if (!TryGetTextInput("Go To Line", "Line number:", currentLine.ToString(), out string input))
                return;

            // Try to parse input as integer
            if (!int.TryParse(input, out int lineNumber))
            {
                MessageBox.Show(this, "Please enter a valid line number.", "Go To Line", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate line number is within document bounds
            if (lineNumber < 1 || lineNumber > rtbEditor.Lines.Length)
            {
                MessageBox.Show(this, $"Line number must be between 1 and {rtbEditor.Lines.Length}.", "Go To Line", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Get character index of the target line (convert from 1-based to 0-based)
            int index = rtbEditor.GetFirstCharIndexFromLine(lineNumber - 1);

            // Move cursor to start of target line
            rtbEditor.SelectionStart = index;
            rtbEditor.SelectionLength = 0;

            // Scroll to make cursor visible
            rtbEditor.ScrollToCaret();

            // Update status bar to show new position
            UpdateStatusBar();
        }

        /// <summary>
        /// Shows a live search dialog where typing immediately highlights matching text.
        /// As the user types, all matches are highlighted in yellow in real-time.
        /// When dialog is closed, highlights are cleared if search box is empty.
        /// Side effects: Applies/removes highlights in real-time, updates lastSearchText.
        /// </summary>
        private void ShowLiveSearchDialog()
        {
            // Create live search dialog
            using Form dialog = new Form();
            dialog.Text = "Live Search";
            dialog.Width = 360;
            dialog.Height = 140;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.StartPosition = FormStartPosition.CenterParent;

            // Create label prompt
            Label label = new Label { Left = 20, Top = 15, Width = 300, Text = "Search text:" };

            // Create search input box with last search text
            TextBox input = new TextBox { Left = 20, Top = 40, Width = 300, Text = lastSearchText };

            // Create close button
            Button closeButton = new Button { Text = "Close", Left = 250, Top = 70, Width = 70, DialogResult = DialogResult.OK };

            // Wire up TextChanged event for live highlighting as user types
            input.TextChanged += (s, e) =>
            {
                // // Store search text for future use
                lastSearchText = input.Text;

                // Highlight all occurrences in real-time
                HighlightAllOccurrences(lastSearchText);
            };

            // Add controls to dialog
            dialog.Controls.Add(label);
            dialog.Controls.Add(input);
            dialog.Controls.Add(closeButton);
            dialog.AcceptButton = closeButton;

            // Show dialog (blocks until user closes it)
            dialog.ShowDialog(this);

            // If search box was cleared, remove all highlights
            if (string.IsNullOrWhiteSpace(lastSearchText))
            {
                RepaintAllHighlights();
            }
        }

        /// <summary>
        /// Toggles between light and dark color themes.
        /// Switches editor, line numbers, and menu colors.
        /// Repaints highlights to use correct background colors.
        /// Side effects: Toggles isDarkMode, applies new colors, repaints highlights.
        /// </summary>
        private void ToggleDarkMode()
        {
            // Toggle dark mode flag
            isDarkMode = !isDarkMode;

            // Apply new theme colors to all UI elements
            ApplyTheme();

            // Repaint highlights with new theme colors
            RepaintAllHighlights();

            // Repaint line numbers panel with new colors
            if (pnlLineNumbers != null)
                pnlLineNumbers.Invalidate();
        }

        /// <summary>
        /// Applies the current theme (light or dark) to all UI elements.
        /// Updates editor, line numbers panel, toolstrips, and context menus.
        /// Side effects: Changes BackColor and ForeColor of multiple controls.
        /// </summary>
        private void ApplyTheme()
        {
            // Get appropriate colors based on current theme mode
            Color editorBack = GetDefaultEditorBackColor();
            Color editorFore = isDarkMode ? darkModeForeground : Color.Black;
            Color chromeBack = isDarkMode ? darkModeLinePanel : SystemColors.Control;
            Color chromeFore = editorFore;

            // Apply theme to editor
            rtbEditor.BackColor = editorBack;
            rtbEditor.ForeColor = editorFore;

            // Apply theme to line numbers panel
            pnlLineNumbers.BackColor = isDarkMode ? darkModeLinePanel : Color.FromArgb(240, 240, 240);
            pnlLineNumbers.ForeColor = isDarkMode ? darkModeLineText : Color.FromArgb(100, 100, 100);

            // Apply theme to form background
            this.BackColor = editorBack;

            // Apply theme to all toolstrips (menu bar, toolbar, etc.)
            foreach (Control control in this.Controls)
            {
                if (control is ToolStrip toolStrip)
                {
                    ApplyToolStripTheme(toolStrip, chromeBack, chromeFore);
                }
            }

            // Apply theme to context menu
            if (rtbEditor.ContextMenuStrip != null)
            {
                rtbEditor.ContextMenuStrip.BackColor = chromeBack;
                rtbEditor.ContextMenuStrip.ForeColor = chromeFore;

                // Apply theme to each menu item
                foreach (ToolStripItem item in rtbEditor.ContextMenuStrip.Items)
                {
                    item.BackColor = chromeBack;
                    item.ForeColor = chromeFore;
                }
            }
        }

        /// <summary>
        /// Applies theme colors to a toolstrip and all its items.
        /// Helper method for ApplyTheme.
        /// </summary>
        /// <param name="toolStrip">The toolstrip to theme.</param>
        /// <param name="backColor">Background color to apply.</param>
        /// <param name="foreColor">Foreground (text) color to apply.</param>
        private static void ApplyToolStripTheme(ToolStrip toolStrip, Color backColor, Color foreColor)
        {
            // Apply colors to toolstrip itself
            toolStrip.BackColor = backColor;
            toolStrip.ForeColor = foreColor;

            // Apply colors to each item in the toolstrip
            foreach (ToolStripItem item in toolStrip.Items)
            {
                item.BackColor = backColor;
                item.ForeColor = foreColor;
            }
        }

        /// <summary>
        /// Highlights all occurrences of the search text in yellow.
        /// First applies base highlights (marked lines and triage), then applies search highlights on top.
        /// Saves and restores selection to avoid disrupting cursor position.
        /// Uses BeginUpdate/EndUpdate to prevent flicker during highlight operations.
        /// Sets isHighlightingInProgress flag to prevent re-entrancy in SelectionChanged event.
        /// Side effects: Changes background colors, uses BeginUpdate/EndUpdate, sets guard flag.
        /// Example scenario: User marks line 10, toggles triage mode (highlights line 15 with ERROR),
        /// then searches for "test". Line 10 is light blue, line 15 is orange, "test" is yellow.
        /// </summary>
        /// <param name="searchText">The text to highlight. If empty, only base highlights are applied.</param>
        private void HighlightAllOccurrences(string searchText)
        {
            // Save current selection state before modifying colors
            int originalSelectionStart = rtbEditor.SelectionStart;
            int originalSelectionLength = rtbEditor.SelectionLength;

            // Set flag to prevent SelectionChanged from firing during highlighting
            // This prevents infinite recursion: highlight -> selection change -> highlight -> ...
            isHighlightingInProgress = true;

            // Suspend redrawing for performance and to prevent flicker
            rtbEditor.BeginUpdate();

            try
            {
                // First, apply base highlights (marked lines and triage keywords)
                ApplyBaseHighlights();

                // If search text is empty, we're done (only base highlights shown)
                if (string.IsNullOrWhiteSpace(searchText))
                    return;

                // Track current search position
                int pos = 0;

                // Get full document text for searching
                string text = rtbEditor.Text;

                // Find and highlight all occurrences of search text in yellow
                // Yellow is applied last so it's most visible
                while ((pos = text.IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    // Select the matched text
                    rtbEditor.SelectionStart = pos;
                    rtbEditor.SelectionLength = searchText.Length;

                    // Apply yellow highlight to this occurrence
                    rtbEditor.SelectionBackColor = highlightColor;

                    // Move past this occurrence to continue searching
                    pos += searchText.Length;
                }
            }
            finally
            {
                // Restore original selection so user's cursor doesn't move
                rtbEditor.SelectionStart = originalSelectionStart;
                rtbEditor.SelectionLength = originalSelectionLength;

                // Resume redrawing and force repaint
                rtbEditor.EndUpdate();

                // Clear flag to allow SelectionChanged to fire normally again
                isHighlightingInProgress = false;
            }
        }

        /// <summary>
        /// Applies base background highlights to the document.
        /// First clears all backgrounds to default color, then applies marked line backgrounds,
        /// then applies triage keyword backgrounds if triage mode is active.
        /// This is called before applying search highlights to establish a clean base state.
        /// Must be called within a BeginUpdate/EndUpdate block for performance.
        /// Side effects: Changes SelectionBackColor for marked lines and triage keywords.
        /// </summary>
        private void ApplyBaseHighlights()
        {
            // Get appropriate background color based on current theme
            Color defaultBackColor = GetDefaultEditorBackColor();

            // Reset all text to default background color first
            rtbEditor.SelectAll();
            rtbEditor.SelectionBackColor = defaultBackColor;

            // Apply marked line backgrounds (light blue)
            // These are user-marked lines for review
            foreach (var range in markedRanges)
            {
                // Validate range is within document bounds
                if (range.start >= 0 && range.start < rtbEditor.TextLength && range.length > 0)
                {
                    // Select the marked line range
                    rtbEditor.SelectionStart = range.start;

                    // Clamp length to prevent selecting past end of document
                    rtbEditor.SelectionLength = Math.Min(range.length, rtbEditor.TextLength - range.start);

                    // Apply light blue background to marked line
                    rtbEditor.SelectionBackColor = markedLineColor;
                }
            }

            // Apply triage keyword backgrounds (orange) if triage mode is active
            if (isLogTriageMode)
            {
                ApplyTriageHighlights();
            }
        }

        #endregion

        #region ========== DIALOGS: INPUT & REPLACE ==========
        // This region contains helper methods for showing input dialogs.
        // Used by Find, Replace, and Go To Line functions.

        /// <summary>
        /// Shows a simple input dialog with OK/Cancel buttons.
        /// Used for Find, Go To Line, and other single-input prompts.
        /// </summary>
        /// <param name="title">Dialog window title.</param>
        /// <param name="prompt">Label text prompting user what to enter.</param>
        /// <param name="defaultValue">Pre-populated value in the input box.</param>
        /// <param name="value">Output parameter containing the user's input if OK was clicked.</param>
        /// <returns>True if user clicked OK, false if cancelled.</returns>
        private bool TryGetTextInput(string title, string prompt, string defaultValue, out string value)
        {
            // Initialize output parameter with default
            value = defaultValue;

            // Create input dialog
            using Form dialog = new Form();
            dialog.Text = title;
            dialog.Width = 360;
            dialog.Height = 140;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.StartPosition = FormStartPosition.CenterParent;

            // Create label prompt
            Label label = new Label { Left = 20, Top = 15, Width = 300, Text = prompt };

            // Create input box with default value
            TextBox input = new TextBox { Left = 20, Top = 40, Width = 300, Text = defaultValue };

            // Create buttons
            Button okButton = new Button { Text = "OK", Left = 170, Top = 70, Width = 70, DialogResult = DialogResult.OK };
            Button cancelButton = new Button { Text = "Cancel", Left = 250, Top = 70, Width = 70, DialogResult = DialogResult.Cancel };

            // Add controls to dialog
            dialog.Controls.Add(label);
            dialog.Controls.Add(input);
            dialog.Controls.Add(okButton);
            dialog.Controls.Add(cancelButton);

            // Set default buttons for Enter/Escape keys
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            // Show dialog and check result
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // User clicked OK: return the input value
                value = input.Text;
                return true;
            }

            // User cancelled
            return false;
        }

        /// <summary>
        /// Shows the Replace dialog with separate find/replace inputs and Replace All checkbox.
        /// Used by the Replace functionality.
        /// </summary>
        /// <param name="findText">Output parameter for the text to find.</param>
        /// <param name="replaceText">Output parameter for the replacement text.</param>
        /// <param name="replaceAll">Output parameter indicating if Replace All checkbox was checked.</param>
        /// <returns>True if user clicked OK, false if cancelled.</returns>
        private bool TryGetReplaceInput(out string findText, out string replaceText, out bool replaceAll)
        {
            // Initialize output parameters
            findText = string.Empty;
            replaceText = string.Empty;
            replaceAll = false;

            // Create replace dialog
            using Form dialog = new Form();
            dialog.Text = "Replace";
            dialog.Width = 380;
            dialog.Height = 190;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.StartPosition = FormStartPosition.CenterParent;

            // Create "Find what" controls
            Label findLabel = new Label { Left = 20, Top = 15, Width = 320, Text = "Find what:" };
            TextBox findBox = new TextBox { Left = 20, Top = 35, Width = 320, Text = lastSearchText };

            // Create "Replace with" controls
            Label replaceLabel = new Label { Left = 20, Top = 65, Width = 320, Text = "Replace with:" };
            TextBox replaceBox = new TextBox { Left = 20, Top = 85, Width = 320 };

            // Create "Replace all" checkbox
            CheckBox replaceAllBox = new CheckBox { Left = 20, Top = 115, Width = 120, Text = "Replace all" };

            // Create buttons
            Button okButton = new Button { Text = "OK", Left = 190, Top = 115, Width = 70, DialogResult = DialogResult.OK };
            Button cancelButton = new Button { Text = "Cancel", Left = 270, Top = 115, Width = 70, DialogResult = DialogResult.Cancel };

            // Add controls to dialog
            dialog.Controls.Add(findLabel);
            dialog.Controls.Add(findBox);
            dialog.Controls.Add(replaceLabel);
            dialog.Controls.Add(replaceBox);
            dialog.Controls.Add(replaceAllBox);
            dialog.Controls.Add(okButton);
            dialog.Controls.Add(cancelButton);

            // Set default buttons for Enter/Escape keys
            dialog.AcceptButton = okButton;
            dialog.CancelButton = cancelButton;

            // Show dialog and check result
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // User clicked OK: return the input values
                findText = findBox.Text;
                replaceText = replaceBox.Text;
                replaceAll = replaceAllBox.Checked;

                // Remember find text for future operations
                lastSearchText = findText;

                return true;
            }

            // User cancelled
            return false;
        }

        /// <summary>
        /// Replaces all occurrences of a string in text (case-insensitive).
        /// Uses StringBuilder for efficient string building with multiple replacements.
        /// </summary>
        /// <param name="input">The input text to search and replace in.</param>
        /// <param name="find">The text to find.</param>
        /// <param name="replace">The text to replace with.</param>
        /// <param name="count">Output parameter containing the number of replacements made.</param>
        /// <returns>The text with all replacements applied.</returns>
        private static string ReplaceAllInsensitive(string input, string find, string replace, out int count)
        {
            // Initialize replacement counter
            count = 0;

            // Handle empty find string (nothing to replace)
            if (string.IsNullOrEmpty(find))
                return input;

            // Use StringBuilder for efficient string building
            StringBuilder sb = new StringBuilder();

            // Track current search position
            int pos = 0;

            // Loop through input finding and replacing all occurrences
            while (true)
            {
                // Search for next occurrence (case-insensitive)
                int index = input.IndexOf(find, pos, StringComparison.OrdinalIgnoreCase);

                // If not found, we're done
                if (index < 0)
                    break;

                // Append text before the match
                sb.Append(input, pos, index - pos);

                // Append replacement text
                sb.Append(replace);

                // Move past the matched text
                pos = index + find.Length;

                // Increment replacement counter
                count++;
            }

            // Append remaining text after last match
            sb.Append(input, pos, input.Length - pos);

            // Return final result
            return sb.ToString();
        }

        #endregion

        /// <summary>
        /// Copies selected text to clipboard as plain text without any formatting.
        /// This ensures highlights don't carry over when pasting into other applications.
        /// </summary>
        private void CopyAsPlainText()
        {
            if (rtbEditor.SelectionLength > 0)
            {
                Clipboard.SetText(rtbEditor.SelectedText);
            }
        }

        /// <summary>
        /// Overrides the default copy behavior to always copy as plain text.
        /// This prevents background color formatting from being copied to clipboard.
        /// </summary>
        private void CopySelectionAsPlainText()
        {
            if (rtbEditor.SelectionLength > 0)
            {
                try
                {
                    // Clear clipboard first
                    Clipboard.Clear();
                    
                    // Copy only the plain text
                    string plainText = rtbEditor.SelectedText;
                    Clipboard.SetText(plainText, TextDataFormat.UnicodeText);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Copy failed: {ex.Message}", "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts for copy operations.
        /// Intercepts Ctrl+C to ensure plain text copying.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Handle Ctrl+C - copy as plain text
            if (keyData == (Keys.Control | Keys.C))
            {
                CopySelectionAsPlainText();
                return true; // Indicate we handled this key
            }
            
            // Handle Ctrl+X - cut as plain text
            if (keyData == (Keys.Control | Keys.X))
            {
                if (rtbEditor.SelectionLength > 0)
                {
                    CopySelectionAsPlainText();
                    rtbEditor.SelectedText = ""; // Delete selected text
                    isDirty = true;
                    UpdateTitle();
                }
                return true;
            }
            
            // Handle Ctrl+V - paste and reapply highlights
            if (keyData == (Keys.Control | Keys.V))
            {
                PasteAndMaintainHighlights();
                return true;
            }
            
            // Let base class handle all other keys
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// Pastes text from clipboard and maintains highlight state.
        /// </summary>
        private void PasteAndMaintainHighlights()
        {
            if (Clipboard.ContainsText())
            {
                try
                {
                    // Get plain text from clipboard
                    string textToPaste = Clipboard.GetText(TextDataFormat.UnicodeText);
                    
                    // Insert at current position
                    int startPos = rtbEditor.SelectionStart;
                    rtbEditor.SelectedText = textToPaste;
                    
                    // Mark as dirty
                    isDirty = true;
                    UpdateTitle();
                    UpdateStatusBar();
                    
                    // Reapply all highlights
                    RepaintAllHighlights();
                    
                    // If there's an active search, re-highlight it
                    if (!string.IsNullOrWhiteSpace(lastSearchText))
                    {
                        HighlightAllOccurrences(lastSearchText);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Paste failed: {ex.Message}", "Paste Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    } // End of Form1 class

    /// <summary>
    /// Extension methods for RichTextBox control.
    /// Provides BeginUpdate/EndUpdate methods to suspend redrawing during bulk operations.
    /// This prevents flicker and improves performance when making many formatting changes.
    /// Uses Win32 API WM_SETREDRAW message to control redrawing.
    /// </summary>
    public static class RichTextBoxExtensions
    {
        /// <summary>
        /// Win32 API import to send messages to window handles.
        /// Used to control redrawing of the RichTextBox.
        /// </summary>
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // Win32 message constant for enabling/disabling window redrawing
        private const int WM_SETREDRAW = 0x0B;

        /// <summary>
        /// Suspends redrawing of the RichTextBox.
        /// Call this before making many formatting changes to prevent flicker.
        /// Must be paired with EndUpdate() to resume redrawing.
        /// Example: Used before applying multiple background color changes during highlighting.
        /// </summary>
        /// <param name="box">The RichTextBox to suspend redrawing for.</param>
        public static void BeginUpdate(this RichTextBox box)
        {
            // Send WM_SETREDRAW message with wParam=0 to suspend redrawing
            SendMessage(box.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Resumes redrawing of the RichTextBox and forces a repaint.
        /// Call this after BeginUpdate() and after making formatting changes.
        /// The control will repaint once with all changes applied, preventing flicker.
        /// </summary>
        /// <param name="box">The RichTextBox to resume redrawing for.</param>
        public static void EndUpdate(this RichTextBox box)
        {
            // Send WM_SETREDRAW message with wParam=1 to resume redrawing
            SendMessage(box.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);

            // Force control to repaint now
            box.Invalidate();
        }
    } // End of RichTextBoxExtensions class

} // End of namespace AutoCyber_Log_Editor