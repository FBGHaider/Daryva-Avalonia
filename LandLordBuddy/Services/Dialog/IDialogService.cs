namespace LandLordBuddy.Services.Dialog
{
    /// <summary>
    /// Service for showing dialogs and getting user input.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a message box.
        /// </summary>
        void ShowMessage(string message, string title = "Information");

        /// <summary>
        /// Shows a confirmation dialog.
        /// </summary>
        bool ShowConfirmation(string message, string title = "Confirm");

        /// <summary>
        /// Shows a file open dialog.
        /// </summary>
        string? ShowOpenFileDialog(string filter = "All Files|*.*", string title = "Select File");

        /// <summary>
        /// Shows a file save dialog.
        /// </summary>
        string? ShowSaveFileDialog(string defaultFileName, string filter = "All Files|*.*", string title = "Save File");

        /// <summary>
        /// Shows a folder browser dialog.
        /// </summary>
        string? ShowFolderBrowserDialog(string description = "Select Folder");

        /// <summary>
        /// Shows an input dialog to get text from the user.
        /// </summary>
        string? ShowInputDialog(string prompt, string title = "Input", string defaultValue = "");
    }
}
