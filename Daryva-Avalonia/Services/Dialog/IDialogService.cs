using System;
using System.Threading.Tasks;

namespace Daryva.Services.Dialog
{
    /// <summary>
    /// Service for showing dialogs and getting user input.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a message box. Prefer <see cref="ShowMessageAsync"/> to avoid UI freezes.
        /// </summary>
        void ShowMessage(string message, string title = "Information");

        /// <summary>
        /// Shows a message box asynchronously.
        /// </summary>
        Task ShowMessageAsync(string message, string title = "Information");

        /// <summary>
        /// Shows a confirmation dialog. Prefer <see cref="ShowConfirmationAsync"/> to avoid deadlocks.
        /// </summary>
        bool ShowConfirmation(string message, string title = "Confirm");

        /// <summary>
        /// Shows a confirmation dialog asynchronously.
        /// </summary>
        Task<bool> ShowConfirmationAsync(string message, string title = "Confirm");

        /// <summary>
        /// Shows a file open dialog.
        /// </summary>
        string? ShowOpenFileDialog(string filter = "All Files|*.*", string title = "Select File");

        /// <summary>
        /// Shows a file open dialog asynchronously (use this to avoid UI deadlock when called from UI thread).
        /// </summary>
        Task<string?> ShowOpenFileDialogAsync(string filter = "All Files|*.*", string title = "Select File");

        /// <summary>
        /// Shows a file save dialog.
        /// </summary>
        string? ShowSaveFileDialog(string defaultFileName, string filter = "All Files|*.*", string title = "Save File");

        /// <summary>
        /// Shows a folder browser dialog.
        /// </summary>
        string? ShowFolderBrowserDialog(string description = "Select Folder");

        /// <summary>
        /// Shows a folder browser dialog asynchronously (use this when calling from the UI thread).
        /// </summary>
        Task<string?> ShowFolderBrowserDialogAsync(string description = "Select Folder");

        /// <summary>
        /// Shows an input dialog to get text from the user. Prefer <see cref="ShowInputDialogAsync"/>.
        /// </summary>
        string? ShowInputDialog(string prompt, string title = "Input", string defaultValue = "");

        /// <summary>
        /// Shows an input dialog asynchronously.
        /// </summary>
        Task<string?> ShowInputDialogAsync(string prompt, string title = "Input", string defaultValue = "");

        /// <summary>
        /// Runs an async action while showing a modal progress dialog with an indeterminate progress bar.
        /// The dialog closes automatically when the action completes.
        /// </summary>
        Task RunWithProgressAsync(string title, string message, Func<Task> action);
    }
}
