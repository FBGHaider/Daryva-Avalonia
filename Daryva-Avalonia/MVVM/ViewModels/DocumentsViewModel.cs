using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Threading;

namespace Daryva.MVVM.ViewModels
{
    public class DocumentsViewModel : BaseViewModel
    {
        private readonly IDocumentService _documentService;
        private readonly ITenantService _tenantService;
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;

        private string _searchTerm = string.Empty;
        private Document? _selectedDocument;
        private List<Document> _allDocuments = new List<Document>();

        public DocumentsViewModel(
            IDocumentService documentService,
            ITenantService tenantService,
            IHouseService houseService,
            IDialogService dialogService,
            IServiceProvider serviceProvider,
            ISettingsService settingsService)
        {
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            Documents = new ObservableCollection<Document>();

            LoadDocumentsCommand = new RelayCommand(async _ => await LoadDocumentsAsync());
            UploadDocumentCommand = new RelayCommand(async _ => await ShowUploadDialogAsync());
            ViewDocumentCommand = new RelayCommand(p => ViewDocument(p as Document), p => p is Document);
            DownloadDocumentCommand = new RelayCommand(p => DownloadDocument(p as Document), p => p is Document);
            DeleteDocumentCommand = new RelayCommand(async p => await DeleteDocumentAsync(p as Document), p => p is Document);

            LoadDocumentsCommand.Execute(null);
        }

        public ICommand LoadDocumentsCommand { get; }
        public ICommand UploadDocumentCommand { get; }
        public ICommand ViewDocumentCommand { get; }
        public ICommand DownloadDocumentCommand { get; }
        public ICommand DeleteDocumentCommand { get; }

        public ObservableCollection<Document> Documents { get; }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value ?? string.Empty))
                    ApplySearch();
            }
        }

        public Document? SelectedDocument
        {
            get => _selectedDocument;
            set => SetProperty(ref _selectedDocument, value);
        }

        private void ApplySearch()
        {
            var term = (SearchTerm ?? "").Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(term)
                ? _allDocuments
                : _allDocuments.Where(d =>
                    (d.DisplayName?.ToLowerInvariant().Contains(term) == true) ||
                    (d.FileName?.ToLowerInvariant().Contains(term) == true) ||
                    (d.Type?.ToLowerInvariant().Contains(term) == true) ||
                    (d.TenantName?.ToLowerInvariant().Contains(term) == true) ||
                    (d.TenantOrHouseDisplay?.ToLowerInvariant().Contains(term) == true)).ToList();

            Dispatcher.UIThread.Post(() =>
            {
                Documents.Clear();
                foreach (var doc in filtered.OrderByDescending(d => d.UploadedAt))
                    Documents.Add(doc);
            });
        }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                Daryva.Services.DateTimeFormatProvider.DateFormat = dateFormat;

                var list = (await _documentService.GetDocumentsAsync(null, null, null, null)).ToList();

                var tenantIds = list.Where(d => d.TenantId.HasValue).Select(d => d.TenantId!.Value).Distinct().ToList();
                var tenantsDict = new Dictionary<int, Tenant>();
                if (tenantIds.Any())
                {
                    var tenants = await _tenantService.GetAllTenantsAsync();
                    tenantsDict = tenants.Where(t => tenantIds.Contains(t.TenantId)).ToDictionary(t => t.TenantId);
                }

                var houseIds = list.Where(d => d.HouseId.HasValue).Select(d => d.HouseId!.Value).Distinct().ToList();
                var housesDict = new Dictionary<int, House>();
                if (houseIds.Any())
                {
                    var houses = await _houseService.GetAllHousesAsync();
                    housesDict = houses.Where(h => houseIds.Contains(h.HouseId)).ToDictionary(h => h.HouseId);
                }

                foreach (var doc in list)
                {
                    if (doc.TenantId.HasValue && tenantsDict.TryGetValue(doc.TenantId.Value, out var tenant))
                        doc.Tenant = tenant;
                    if (doc.HouseId.HasValue && housesDict.TryGetValue(doc.HouseId.Value, out var house))
                        doc.House = house;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _allDocuments = list;
                    ApplySearch();
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading documents: {ex.Message}", "Error");
            }
        }

        private async Task ShowUploadDialogAsync()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<UploadDocumentViewModel>();
                var dialog = new MVVM.Views.UploadDocumentDialog(viewModel);
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;
                if (mainWindow != null)
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                    await dialog.ShowDialog(null!);
                }
                await LoadDocumentsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening upload dialog: {ex.Message}", "Error");
            }
        }

        private async void ViewDocument(Document? document)
        {
            if (document == null) return;
            try
            {
                var doc = await _documentService.GetDocumentByIdAsync(document.DocumentId);
                if (doc == null) { _dialogService.ShowMessage("Document not found.", "Error"); return; }
                var fileBytes = await _documentService.GetDocumentFileBytesAsync(doc.DocumentId);
                if (fileBytes == null || fileBytes.Length == 0) { _dialogService.ShowMessage("Document file not found.", "Error"); return; }
                var tempPath = Path.Combine(Path.GetTempPath(), doc.FileName);
                await File.WriteAllBytesAsync(tempPath, fileBytes);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error viewing document: {ex.Message}", "Error");
            }
        }

        private async void DownloadDocument(Document? document)
        {
            if (document == null) return;
            try
            {
                var doc = await _documentService.GetDocumentByIdAsync(document.DocumentId);
                if (doc == null) { _dialogService.ShowMessage("Document not found.", "Error"); return; }
                var fileBytes = await _documentService.GetDocumentFileBytesAsync(doc.DocumentId);
                if (fileBytes == null || fileBytes.Length == 0) { _dialogService.ShowMessage("Document file not found.", "Error"); return; }
                var filePath = _dialogService.ShowSaveFileDialog(doc.FileName, "All Files (*.*)|*.*", "Save Document");
                if (!string.IsNullOrEmpty(filePath))
                {
                    await File.WriteAllBytesAsync(filePath, fileBytes);
                    _dialogService.ShowMessage($"Saved to {filePath}", "Success");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error downloading document: {ex.Message}", "Error");
            }
        }

        private async Task DeleteDocumentAsync(Document? document)
        {
            if (document == null) return;
            var confirmed = await _dialogService.ShowConfirmationAsync($"Delete '{document.DisplayName}'?", "Confirm Delete");
            if (!confirmed) return;
            try
            {
                var storageRootPath = await _settingsService.GetSettingAsync("DocumentStoragePath", string.Empty);
                var pathToUse = string.IsNullOrWhiteSpace(storageRootPath) ? null : storageRootPath.Trim();
                var deleted = await _documentService.DeleteDocumentAsync(document.DocumentId, pathToUse);
                if (deleted)
                {
                    _dialogService.ShowMessage("Document deleted.", "Success");
                    await LoadDocumentsAsync();
                }
                else
                    _dialogService.ShowMessage("Failed to delete document.", "Error");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting document: {ex.Message}", "Error");
            }
        }
    }
}
