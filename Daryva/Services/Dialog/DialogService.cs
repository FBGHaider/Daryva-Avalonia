using System.Windows;
using Microsoft.Win32;
using System.IO;
using WinForms = System.Windows.Forms;

namespace Daryva.Services.Dialog
{
    /// <summary>
    /// WPF implementation of IDialogService.
    /// </summary>
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string title = "Information")
        {
            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            var result = System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public string? ShowOpenFileDialog(string filter = "All Files|*.*", string title = "Select File")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? ShowSaveFileDialog(string defaultFileName, string filter = "All Files|*.*", string title = "Save File")
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? ShowFolderBrowserDialog(string description = "Select Folder")
        {
            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = description
            };

            return dialog.ShowDialog() == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        public string? ShowInputDialog(string prompt, string title = "Input", string defaultValue = "")
        {
            var inputDialog = new System.Windows.Forms.Form
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
                Text = title,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var label = new System.Windows.Forms.Label
            {
                Left = 20,
                Top = 20,
                Width = 350,
                Text = prompt
            };

            var textBox = new System.Windows.Forms.TextBox
            {
                Left = 20,
                Top = 50,
                Width = 350,
                Text = defaultValue
            };

            var okButton = new System.Windows.Forms.Button
            {
                Text = "OK",
                Left = 200,
                Width = 80,
                Top = 80,
                DialogResult = System.Windows.Forms.DialogResult.OK
            };

            var cancelButton = new System.Windows.Forms.Button
            {
                Text = "Cancel",
                Left = 290,
                Width = 80,
                Top = 80,
                DialogResult = System.Windows.Forms.DialogResult.Cancel
            };

            inputDialog.Controls.Add(label);
            inputDialog.Controls.Add(textBox);
            inputDialog.Controls.Add(okButton);
            inputDialog.Controls.Add(cancelButton);
            inputDialog.AcceptButton = okButton;
            inputDialog.CancelButton = cancelButton;

            if (inputDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return textBox.Text;
            }

            return null;
        }
    }
}
