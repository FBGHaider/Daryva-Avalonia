using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Daryva.Services;

namespace Daryva.Services.Dialog
{
    /// <summary>
    /// Avalonia implementation of IDialogService.
    /// </summary>
    public class DialogService : IDialogService
    {
        private Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }

        public void ShowMessage(string message, string title = "Information")
        {
            _ = ShowMessageAsync(message, title);
        }

        public async Task ShowMessageAsync(string message, string title = "Information")
        {
            if (title.Contains("Error", StringComparison.OrdinalIgnoreCase))
                AppLogger.LogError("Dialog", $"[{title}] {message}");
            else
                AppLogger.Log("Dialog", $"[{title}] {message}");

            var owner = GetMainWindow();
            if (owner == null) return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var okButton = new Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Right };
                var msgBox = new Window
                {
                    Title = title,
                    Width = 450,
                    MinHeight = 120,
                    MaxHeight = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = true,
                    ShowInTaskbar = false,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 16,
                        Children =
                        {
                            new ScrollViewer
                            {
                                MaxHeight = 400,
                                Content = new TextBlock
                                {
                                    Text = message,
                                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                                }
                            },
                            okButton
                        }
                    }
                };
                okButton.Click += (s, e) => msgBox.Close();
                await msgBox.ShowDialog(owner);
            });
        }

        public bool ShowConfirmation(string message, string title = "Confirm")
        {
            return ShowConfirmationAsync(message, title).GetAwaiter().GetResult();
        }

        public async Task<bool> ShowConfirmationAsync(string message, string title = "Confirm")
        {
            var owner = GetMainWindow();
            if (owner == null) return false;

            var tcs = new TaskCompletionSource<bool>();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var yesButton = new Button { Content = "Yes", Width = 80 };
                var noButton = new Button { Content = "No", Width = 80 };

                var msgBox = new Window
                {
                    Title = title,
                    Width = 450,
                    MinHeight = 120,
                    MaxHeight = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = true,
                    ShowInTaskbar = false,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 16,
                        Children =
                        {
                            new ScrollViewer
                            {
                                MaxHeight = 400,
                                Content = new TextBlock
                                {
                                    Text = message,
                                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                                }
                            },
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 10,
                                Children = { yesButton, noButton }
                            }
                        }
                    }
                };

                yesButton.Click += (s, e) => { tcs.TrySetResult(true); msgBox.Close(); };
                noButton.Click += (s, e) => { tcs.TrySetResult(false); msgBox.Close(); };

                await msgBox.ShowDialog(owner);
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(false);
            });

            return await tcs.Task;
        }

        public string? ShowInputDialog(string prompt, string title = "Input", string defaultValue = "")
        {
            return ShowInputDialogAsync(prompt, title, defaultValue).GetAwaiter().GetResult();
        }

        public async Task<string?> ShowInputDialogAsync(string prompt, string title = "Input", string defaultValue = "")
        {
            var owner = GetMainWindow();
            if (owner == null) return null;

            var tcs = new TaskCompletionSource<string?>();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var tb = new Avalonia.Controls.TextBox
                {
                    Text = defaultValue ?? "",
                    MinWidth = 280,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var okButton = new Button { Content = "OK", Width = 80 };
                var cancelButton = new Button { Content = "Cancel", Width = 80 };

                var msgBox = new Window
                {
                    Title = title,
                    Width = 400,
                    MinHeight = 160,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    ShowInTaskbar = false,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 16,
                        Children =
                        {
                            new TextBlock { Text = prompt, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            tb,
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Spacing = 10,
                                Children = { okButton, cancelButton }
                            }
                        }
                    }
                };

                void Finish(string? value)
                {
                    tcs.TrySetResult(value);
                    msgBox.Close();
                }

                okButton.Click += (s, e) => Finish(tb.Text?.Trim());
                cancelButton.Click += (s, e) => Finish(null);

                await msgBox.ShowDialog(owner);
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(null);
            });

            return await tcs.Task;
        }

        public string? ShowOpenFileDialog(string filter = "All Files|*.*", string title = "Select File")
        {
            return Task.Run(() => ShowOpenFileDialogAsync(filter, title).GetAwaiter().GetResult()).GetAwaiter().GetResult();
        }

        public async Task<string?> ShowOpenFileDialogAsync(string filter = "All Files|*.*", string title = "Select File")
        {
            var window = GetMainWindow();
            if (window == null) return null;

            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var fileType = new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } };
                if (!string.IsNullOrEmpty(filter) && filter.Contains('|'))
                {
                    var parts = filter.Split('|');
                    if (parts.Length >= 2)
                    {
                        var patterns = parts[1].Split(';');
                        fileType = new FilePickerFileType(parts[0].Trim()) { Patterns = patterns };
                    }
                }

                var options = new FilePickerOpenOptions
                {
                    Title = title,
                    FileTypeFilter = new[] { fileType }
                };

                var files = await window.StorageProvider.OpenFilePickerAsync(options);
                return files.Count >= 1 ? files[0].Path.LocalPath : null;
            });
        }

        public string? ShowSaveFileDialog(string defaultFileName, string filter = "All Files|*.*", string title = "Save File")
        {
            return Task.Run(() => ShowSaveFileDialogAsync(defaultFileName, filter, title).GetAwaiter().GetResult()).GetAwaiter().GetResult();
        }

        private async Task<string?> ShowSaveFileDialogAsync(string defaultFileName, string filter, string title)
        {
            var window = GetMainWindow();
            if (window == null) return null;

            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var fileType = new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } };
                if (!string.IsNullOrEmpty(filter) && filter.Contains('|'))
                {
                    var parts = filter.Split('|');
                    if (parts.Length >= 2)
                    {
                        var patterns = parts[1].Split(';');
                        fileType = new FilePickerFileType(parts[0].Trim()) { Patterns = patterns };
                    }
                }

                IStorageFolder? suggestedLocation = null;
                if (!string.IsNullOrEmpty(defaultFileName))
                {
                    try
                    {
                        var defaultPath = Path.GetDirectoryName(defaultFileName);
                        if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
                            suggestedLocation = await window.StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(defaultPath)));
                    }
                    catch { }
                }

                var options = new FilePickerSaveOptions
                {
                    Title = title,
                    SuggestedFileName = Path.GetFileName(defaultFileName ?? ""),
                    DefaultExtension = Path.GetExtension(defaultFileName ?? "").TrimStart('.'),
                    FileTypeChoices = new[] { fileType },
                    SuggestedStartLocation = suggestedLocation
                };

                var file = await window.StorageProvider.SaveFilePickerAsync(options);
                return file?.Path.LocalPath;
            });
        }

        public string? ShowFolderBrowserDialog(string description = "Select Folder")
        {
            // Synchronous wrapper for legacy callers. Prefer ShowFolderBrowserDialogAsync.
            return ShowFolderBrowserDialogAsync(description).GetAwaiter().GetResult();
        }

        public async Task<string?> ShowFolderBrowserDialogAsync(string description)
        {
            var window = GetMainWindow();
            if (window == null) return null;

            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var options = new FolderPickerOpenOptions { Title = description };
                var folders = await window.StorageProvider.OpenFolderPickerAsync(options);
                return folders.Count >= 1 ? folders[0].Path.LocalPath : null;
            });
        }

        public async Task RunWithProgressAsync(string title, string message, Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var owner = GetMainWindow();
            if (owner == null)
            {
                // No window available – just run the action.
                await action();
                return;
            }

            Exception? capturedException = null;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var progressBar = new ProgressBar
                {
                    IsIndeterminate = true,
                    Height = 16,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var window = new Window
                {
                    Title = title,
                    Width = 400,
                    MinHeight = 140,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    ShowInTaskbar = false,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = message,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap
                            },
                            progressBar
                        }
                    }
                };

                // Start the work on a background thread
                var workTask = Task.Run(async () =>
                {
                    try
                    {
                        await action();
                    }
                    catch (Exception ex)
                    {
                        capturedException = ex;
                    }
                    finally
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => window.Close());
                    }
                });

                await window.ShowDialog(owner);
                await workTask;
            });

            if (capturedException != null)
            {
                throw capturedException;
            }
        }

        public async Task<bool> CopyToClipboardAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var window = GetMainWindow();
            if (window?.Clipboard == null) return false;
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await window.Clipboard.SetTextAsync(text);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
