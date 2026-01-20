using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class DocumentSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private int _maxFileSizeMB = 10;
        private string _allowedFormats = "PDF,JPG,JPEG,PNG,DOCX";
        private bool _autoVersionDocuments = true;
        private int _studentLetterValidityMonths = 12;
        private int _notifyBeforeExpiryDays = 30;
        private string _documentStoragePath = string.Empty;
        private bool _openDocumentsInApp = false;

        public DocumentSettingsViewModel(ISettingsService settingsService, IDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());
            BrowseStoragePathCommand = new RelayCommand(_ => BrowseStoragePath());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand BrowseStoragePathCommand { get; }

        public int MaxFileSizeMB
        {
            get => _maxFileSizeMB;
            set => SetProperty(ref _maxFileSizeMB, value);
        }

        public string AllowedFormats
        {
            get => _allowedFormats;
            set => SetProperty(ref _allowedFormats, value);
        }

        public bool AutoVersionDocuments
        {
            get => _autoVersionDocuments;
            set => SetProperty(ref _autoVersionDocuments, value);
        }

        public int StudentLetterValidityMonths
        {
            get => _studentLetterValidityMonths;
            set => SetProperty(ref _studentLetterValidityMonths, value);
        }

        public int NotifyBeforeExpiryDays
        {
            get => _notifyBeforeExpiryDays;
            set => SetProperty(ref _notifyBeforeExpiryDays, value);
        }

        public string DocumentStoragePath
        {
            get => _documentStoragePath;
            set => SetProperty(ref _documentStoragePath, value);
        }

        public bool OpenDocumentsInApp
        {
            get => _openDocumentsInApp;
            set => SetProperty(ref _openDocumentsInApp, value);
        }

        private void BrowseStoragePath()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder for document storage",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrWhiteSpace(DocumentStoragePath) && Directory.Exists(DocumentStoragePath))
            {
                dialog.SelectedPath = DocumentStoragePath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DocumentStoragePath = dialog.SelectedPath;
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                MaxFileSizeMB = await _settingsService.GetSettingAsync<int>("MaxFileSizeMB", 10) ?? 10;
                AllowedFormats = await _settingsService.GetSettingAsync("AllowedFormats", "PDF,JPG,JPEG,PNG,DOCX") ?? "PDF,JPG,JPEG,PNG,DOCX";
                AutoVersionDocuments = await _settingsService.GetSettingAsync<bool>("AutoVersionDocuments", true) ?? true;
                StudentLetterValidityMonths = await _settingsService.GetSettingAsync<int>("StudentLetterValidityMonths", 12) ?? 12;
                NotifyBeforeExpiryDays = await _settingsService.GetSettingAsync<int>("NotifyBeforeExpiryDays", 30) ?? 30;
                DocumentStoragePath = await _settingsService.GetSettingAsync("DocumentStoragePath", string.Empty) ?? string.Empty;
                OpenDocumentsInApp = await _settingsService.GetSettingAsync<bool>("OpenDocumentsInApp", false) ?? false;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading settings: {ex.Message}", "Error");
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                if (MaxFileSizeMB < 1 || MaxFileSizeMB > 100)
                {
                    _dialogService.ShowMessage("Max file size must be between 1 and 100 MB.", "Validation Error");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(DocumentStoragePath) && !Directory.Exists(DocumentStoragePath))
                {
                    _dialogService.ShowMessage("Document storage path does not exist. Please select a valid folder.", "Validation Error");
                    return;
                }

                await _settingsService.SetSettingAsync("MaxFileSizeMB", MaxFileSizeMB);
                await _settingsService.SetSettingAsync("AllowedFormats", AllowedFormats);
                await _settingsService.SetSettingAsync("AutoVersionDocuments", AutoVersionDocuments);
                await _settingsService.SetSettingAsync("StudentLetterValidityMonths", StudentLetterValidityMonths);
                await _settingsService.SetSettingAsync("NotifyBeforeExpiryDays", NotifyBeforeExpiryDays);
                await _settingsService.SetSettingAsync("DocumentStoragePath", DocumentStoragePath);
                await _settingsService.SetSettingAsync("OpenDocumentsInApp", OpenDocumentsInApp);

                _dialogService.ShowMessage("Settings saved successfully. Changes will apply to future actions only.", "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }
    }
}
