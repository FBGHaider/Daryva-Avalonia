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
        private readonly ITenancyService? _tenancyService;
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;

        private string _ownerFilter = "All"; // All, Tenant, Tenancy, House
        private int? _selectedTenantId;
        private int? _selectedTenancyId;
        private int? _selectedHouseId;
        private string _typeFilter = "All";
        private string _allDocumentsTypeFilter = "All";
        private string _selectedDocumentTypeForUpload = "Other"; // Document type selected from dropdown for upload
        private DocumentStatusItem? _selectedDocumentStatus;
        private Document? _selectedDocument;

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

            // Try to get ITenancyService if available
            _tenancyService = serviceProvider.GetService<ITenancyService>();

            Documents = new ObservableCollection<Document>();
            DocumentStatusChecklist = new ObservableCollection<DocumentStatusItem>();
            Tenants = new ObservableCollection<Tenant>();
            Houses = new ObservableCollection<House>();
            var allTypes = new[] { "StudentConfirmationLetter", "PhotoId", "RightToRent", "TenancyAgreementSigned", "GuarantorAgreement", "InventoryCheckIn", "DepositProtectionCertificate", "NoticeToLeave", "Contract", "CouncilTax", "Bills", "Insurance", "GasSafety", "Epc", "Other" };
            AvailableDocumentTypes = new ObservableCollection<string>(allTypes);
            DocumentTypesForUpload = new ObservableCollection<string>();
            OwnerTypeOptions = new ObservableCollection<string> { "All", "Tenant", "House" };
            TypeFilterOptions = new ObservableCollection<string> { "All", "Active", "Expired", "ExpiringSoon", "Missing" };
            AllDocumentsTypeFilterOptions = new ObservableCollection<string> { "All" };
            foreach (var t in allTypes)
                AllDocumentsTypeFilterOptions.Add(t);

            LoadDocumentsCommand = new RelayCommand(async _ => await LoadDocumentsAsync());
            LoadDocumentStatusCommand = new RelayCommand(async _ => await LoadDocumentStatusAsync());
            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            UploadDocumentCommand = new RelayCommand(async _ => await ShowUploadDocumentAsync(), _ => CanUploadDocument());
            ViewDocumentCommand = new RelayCommand(_ => ViewDocumentAsync(), _ => SelectedDocumentStatus != null && SelectedDocumentStatus.DocumentId.HasValue);
            DownloadDocumentCommand = new RelayCommand(_ => DownloadDocumentAsync(), _ => SelectedDocumentStatus != null && SelectedDocumentStatus.DocumentId.HasValue);
            DeleteDocumentCommand = new RelayCommand(async _ => await DeleteDocumentAsync(), _ => SelectedDocumentStatus != null && SelectedDocumentStatus.DocumentId.HasValue);
            DeleteSelectedDocumentCommand = new RelayCommand(async _ => await DeleteSelectedDocumentAsync(), _ => SelectedDocument != null);
            DeleteAllDocumentsCommand = new RelayCommand(async _ => await DeleteAllDocumentsAsync());
            ViewHistoryCommand = new RelayCommand(async _ => await ViewDocumentHistoryAsync(), _ => SelectedDocumentStatus != null);

            RefreshDocumentTypesForUpload();

            // Load initial data
            LoadHousesCommand.Execute(null);
            LoadTenantsCommand.Execute(null);
            LoadDocumentsCommand.Execute(null);
            LoadDocumentStatusCommand.Execute(null);
        }

        public ICommand LoadDocumentsCommand { get; }
        public ICommand LoadDocumentStatusCommand { get; }
        public ICommand LoadTenantsCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand UploadDocumentCommand { get; }
        public ICommand ViewDocumentCommand { get; }
        public ICommand DownloadDocumentCommand { get; }
        public ICommand DeleteDocumentCommand { get; }
        public ICommand DeleteSelectedDocumentCommand { get; }
        public ICommand DeleteAllDocumentsCommand { get; }
        public ICommand ViewHistoryCommand { get; }

        public ObservableCollection<Document> Documents { get; }
        public ObservableCollection<DocumentStatusItem> DocumentStatusChecklist { get; }
        public ObservableCollection<Tenant> Tenants { get; }
        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<string> AvailableDocumentTypes { get; }
        public ObservableCollection<string> DocumentTypesForUpload { get; }
        public ObservableCollection<string> OwnerTypeOptions { get; }
        public ObservableCollection<string> TypeFilterOptions { get; }
        public ObservableCollection<string> AllDocumentsTypeFilterOptions { get; }

        public string OwnerFilter
        {
            get => _ownerFilter;
            set
            {
                var v = value ?? "All";
                if (SetProperty(ref _ownerFilter, v))
                {
                    // Reset only the selections that don't match the new filter (to avoid circular updates)
                    if (v != "Tenant" && _selectedTenantId.HasValue)
                    {
                        _selectedTenantId = null;
                        OnPropertyChanged(nameof(SelectedTenantId));
                    }
                    if (v != "House" && _selectedHouseId.HasValue)
                    {
                        _selectedHouseId = null;
                        OnPropertyChanged(nameof(SelectedHouseId));
                    }

                    OnPropertyChanged(nameof(IsHouseFilterVisible));
                    OnPropertyChanged(nameof(IsTenantFilterVisible));
                    OnPropertyChanged(nameof(IsTypeFilterVisible));
                    RefreshDocumentTypesForUpload();
                    ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
                    LoadDocumentStatusCommand.Execute(null);
                }
            }
        }

        public bool IsHouseFilterVisible => string.Equals(OwnerFilter, "House", StringComparison.OrdinalIgnoreCase);
        public bool IsTenantFilterVisible => string.Equals(OwnerFilter, "Tenant", StringComparison.OrdinalIgnoreCase);
        public bool IsTypeFilterVisible => !string.Equals(OwnerFilter, "All", StringComparison.OrdinalIgnoreCase);

        private void RefreshDocumentTypesForUpload()
        {
            DocumentTypesForUpload.Clear();
            if (string.Equals(OwnerFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                _selectedDocumentTypeForUpload = "Other";
                OnPropertyChanged(nameof(SelectedDocumentTypeForUpload));
                ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
                return;
            }
            var types = _documentService.GetDocumentTypesForOwner(OwnerFilter).ToList();
            foreach (var t in types)
                DocumentTypesForUpload.Add(t);
            var preferred = types.FirstOrDefault(x => string.Equals(x, "Other", StringComparison.OrdinalIgnoreCase)) ?? types.FirstOrDefault();
            _selectedDocumentTypeForUpload = preferred ?? "Other";
            OnPropertyChanged(nameof(SelectedDocumentTypeForUpload));
            ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
        }

        public int? SelectedTenantId
        {
            get => _selectedTenantId;
            set
            {
                if (SetProperty(ref _selectedTenantId, value))
                {
                    // Only clear tenancy when setting tenant (same owner-type dimension). Do not clear house.
                    if (value.HasValue && _selectedTenancyId.HasValue)
                    {
                        _selectedTenancyId = null;
                        OnPropertyChanged(nameof(SelectedTenancyId));
                    }
                    LoadDocumentStatusCommand.Execute(null);
                    ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int? SelectedTenancyId
        {
            get => _selectedTenancyId;
            set
            {
                if (SetProperty(ref _selectedTenancyId, value))
                {
                    // Only clear tenant when setting tenancy (same owner-type dimension). Do not clear house.
                    if (value.HasValue && _selectedTenantId.HasValue)
                    {
                        _selectedTenantId = null;
                        OnPropertyChanged(nameof(SelectedTenantId));
                    }
                    LoadDocumentStatusCommand.Execute(null);
                    ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int? SelectedHouseId
        {
            get => _selectedHouseId;
            set
            {
                if (SetProperty(ref _selectedHouseId, value))
                {
                    // Do not clear tenant or tenancy when selecting house; filters are independent.
                    LoadDocumentStatusCommand.Execute(null);
                    ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string TypeFilter
        {
            get => _typeFilter;
            set
            {
                var v = value ?? "All";
                if (SetProperty(ref _typeFilter, v))
                    LoadDocumentStatusCommand.Execute(null);
            }
        }

        public string AllDocumentsTypeFilter
        {
            get => _allDocumentsTypeFilter;
            set
            {
                var v = value ?? "All";
                if (SetProperty(ref _allDocumentsTypeFilter, v))
                    LoadDocumentsCommand.Execute(null);
            }
        }

        public string SelectedDocumentTypeForUpload
        {
            get => _selectedDocumentTypeForUpload;
            set
            {
                if (SetProperty(ref _selectedDocumentTypeForUpload, value))
                    ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
            }
        }

        public DocumentStatusItem? SelectedDocumentStatus
        {
            get => _selectedDocumentStatus;
            set
            {
                if (SetProperty(ref _selectedDocumentStatus, value))
                {
                    ((RelayCommand)ViewDocumentCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DownloadDocumentCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteDocumentCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ViewHistoryCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public Document? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (SetProperty(ref _selectedDocument, value))
                {
                    ((RelayCommand)DeleteSelectedDocumentCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool CanUploadDocument()
        {
            if (OwnerFilter == "All" || string.IsNullOrWhiteSpace(SelectedDocumentTypeForUpload))
                return false;
            if (OwnerFilter == "Tenant")
                return SelectedTenantId.HasValue;
            if (OwnerFilter == "House")
                return SelectedHouseId.HasValue;
            return false;
        }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                Daryva.Services.DateTimeFormatProvider.DateFormat = dateFormat;

                string? typeFilter = (string.IsNullOrEmpty(AllDocumentsTypeFilter) || string.Equals(AllDocumentsTypeFilter, "All", StringComparison.OrdinalIgnoreCase))
                    ? null
                    : AllDocumentsTypeFilter;
                var documents = await _documentService.GetDocumentsAsync(null, null, null, typeFilter);

                // Load tenant information for documents to populate TenantName
                var tenantIds = documents.Where(d => d.TenantId.HasValue).Select(d => d.TenantId!.Value).Distinct().ToList();
                
                var tenantsDict = new Dictionary<int, Tenant>();
                if (tenantIds.Any())
                {
                    var tenants = await _tenantService.GetAllTenantsAsync();
                    tenantsDict = tenants.Where(t => tenantIds.Contains(t.TenantId)).ToDictionary(t => t.TenantId);
                }

                // Populate Tenant navigation property
                foreach (var doc in documents)
                {
                    if (doc.TenantId.HasValue && tenantsDict.TryGetValue(doc.TenantId.Value, out var tenant))
                    {
                        doc.Tenant = tenant;
                    }
                }

                // Ensure UI updates happen on the UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Documents.Clear();
                    foreach (var doc in documents.OrderByDescending(d => d.UploadedAt))
                    {
                        Documents.Add(doc);
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading documents: {ex.Message}", "Error");
            }
        }

        private async Task LoadDocumentStatusAsync()
        {
            try
            {
                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                Daryva.Services.DateTimeFormatProvider.DateFormat = dateFormat;

                // Use OwnerFilter to decide which owner drives the checklist (one owner only).
                // This prevents checklist from "resetting" when house is selected after tenant.
                int? tenantId = null;
                int? tenancyId = null;
                int? houseId = null;

                if (OwnerFilter == "Tenant" && SelectedTenantId.HasValue)
                    tenantId = SelectedTenantId;
                else if (OwnerFilter == "House" && SelectedHouseId.HasValue)
                    houseId = SelectedHouseId;
                else
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DocumentStatusChecklist.Clear();
                    });
                    return;
                }

                var checklist = await _documentService.GetDocumentStatusChecklistAsync(tenantId, tenancyId, houseId);

                // Apply status filter (TypeFilter: All, Active, Expired, ExpiringSoon, Missing)
                var filtered = checklist.AsEnumerable();
                if (!string.IsNullOrEmpty(TypeFilter) && !string.Equals(TypeFilter, "All", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(i => string.Equals(i.Status, TypeFilter, StringComparison.OrdinalIgnoreCase));

                // Ensure UI updates happen on the UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DocumentStatusChecklist.Clear();
                    foreach (var item in filtered.OrderBy(i => i.DisplayName))
                    {
                        DocumentStatusChecklist.Add(item);
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading document status: {ex.Message}", "Error");
            }
        }

        private async Task LoadTenantsAsync()
        {
            try
            {
                var tenants = await _tenantService.GetAllTenantsAsync();
                // Ensure UI updates happen on the UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Tenants.Clear();
                    foreach (var tenant in tenants)
                    {
                        Tenants.Add(tenant);
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading tenants: {ex.Message}", "Error");
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                // Ensure UI updates happen on the UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Houses.Clear();
                    foreach (var house in houses)
                    {
                        Houses.Add(house);
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading houses: {ex.Message}", "Error");
            }
        }

        private static readonly Dictionary<string, string> FormatToExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PDF"] = "*.pdf",
            ["JPG"] = "*.jpg",
            ["JPEG"] = "*.jpeg",
            ["PNG"] = "*.png",
            ["GIF"] = "*.gif",
            ["DOC"] = "*.doc",
            ["DOCX"] = "*.docx",
            ["XLS"] = "*.xls",
            ["XLSX"] = "*.xlsx",
        };

        private static string BuildFileFilterFromAllowedFormats(string allowedFormats)
        {
            if (string.IsNullOrWhiteSpace(allowedFormats))
                return "All Files (*.*)|*.*";
            var tokens = allowedFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var patterns = new List<string>();
            var names = new List<string>();
            foreach (var t in tokens)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                var ext = FormatToExtension.TryGetValue(t, out var e) ? e : "*." + t.TrimStart('.');
                if (!patterns.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    patterns.Add(ext);
                    names.Add(t.ToUpperInvariant());
                }
            }
            if (patterns.Count == 0)
                return "All Files (*.*)|*.*";
            var display = "Allowed (" + string.Join(", ", names) + ")";
            var patternStr = string.Join(";", patterns);
            return $"{display}|{patternStr}";
        }

        private async System.Threading.Tasks.Task ShowUploadDocumentAsync()
        {
            try
            {
                var allowedFormats = await _settingsService.GetSettingAsync("AllowedFormats", "PDF,JPG,JPEG,PNG,DOCX") ?? "PDF,JPG,JPEG,PNG,DOCX";
                var maxFileSizeMB = await _settingsService.GetSettingAsync<int>("MaxFileSizeMB", 10) ?? 10;
                var filter = BuildFileFilterFromAllowedFormats(allowedFormats);

                var filePath = await _dialogService.ShowOpenFileDialogAsync(filter, "Select Document to Upload");

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileBytes = await File.ReadAllBytesAsync(filePath);
                    var sizeMB = fileBytes.Length / (1024.0 * 1024.0);
                    if (sizeMB > maxFileSizeMB)
                    {
                        _dialogService.ShowMessage(
                            $"File size ({sizeMB:F1} MB) exceeds the maximum allowed ({maxFileSizeMB} MB). Adjust Settings → Documents if needed.",
                            "File Too Large");
                        return;
                    }

                    string docType = !string.IsNullOrWhiteSpace(SelectedDocumentTypeForUpload)
                        ? SelectedDocumentTypeForUpload
                        : SelectedDocumentStatus?.Type ?? "Other";

                    await UploadDocumentAsync(docType, fileName, fileBytes);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error selecting file: {ex.Message}", "Error");
            }
        }

        private async System.Threading.Tasks.Task UploadDocumentAsync(string docType, string fileName, byte[] fileBytes)
        {
            try
            {
                // Validate same as CanUploadDocument: Owner Type + corresponding owner + Type
                if (OwnerFilter == "All" || string.IsNullOrWhiteSpace(docType))
                {
                    _dialogService.ShowMessage("Please select Owner Type, the corresponding owner, and Document Type before uploading.", "Validation");
                    return;
                }
                if (OwnerFilter == "Tenant" && !SelectedTenantId.HasValue)
                {
                    _dialogService.ShowMessage("Please select a tenant before uploading a document.", "No Tenant Selected");
                    return;
                }
                if (OwnerFilter == "House" && !SelectedHouseId.HasValue)
                {
                    _dialogService.ShowMessage("Please select a house before uploading a document.", "No House Selected");
                    return;
                }

                var document = new Document
                {
                    TenantId = OwnerFilter == "Tenant" ? SelectedTenantId : null,
                    TenancyId = null,
                    HouseId = OwnerFilter == "House" ? SelectedHouseId : null,
                    Type = docType,
                    FileName = fileName,
                    Source = "Uploaded"
                };

                // Set valid dates for StudentConfirmationLetter
                if (docType == "StudentConfirmationLetter")
                {
                    var currentYear = DateTime.Now.Year;
                    document.ValidFrom = new DateTime(currentYear, 9, 1); // Academic year start
                    document.ValidTo = new DateTime(currentYear + 1, 8, 31); // Academic year end
                }

                var storageRootPath = await _settingsService.GetSettingAsync("DocumentStoragePath", string.Empty);
                var pathToUse = string.IsNullOrWhiteSpace(storageRootPath) ? null : storageRootPath.Trim();
                var uploadedDoc = await _documentService.UploadDocumentAsync(document, fileBytes, pathToUse);

                _dialogService.ShowMessage($"Document '{uploadedDoc.DisplayName}' uploaded successfully.", "Success");
                
                // Refresh lists
                LoadDocumentStatusCommand.Execute(null);
                LoadDocumentsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error uploading document: {ex.Message}", "Error");
            }
        }

        private async void ViewDocumentAsync()
        {
            if (SelectedDocumentStatus?.DocumentId == null)
                return;

            try
            {
                var document = await _documentService.GetDocumentByIdAsync(SelectedDocumentStatus.DocumentId.Value);
                if (document == null)
                {
                    _dialogService.ShowMessage("Document not found.", "Error");
                    return;
                }

                var fileBytes = await _documentService.GetDocumentFileBytesAsync(document.DocumentId);
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _dialogService.ShowMessage("Document file not found.", "Error");
                    return;
                }

                // Save to temp file and open
                var tempPath = Path.Combine(Path.GetTempPath(), document.FileName);
                await File.WriteAllBytesAsync(tempPath, fileBytes);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error viewing document: {ex.Message}", "Error");
            }
        }

        private async void DownloadDocumentAsync()
        {
            if (SelectedDocumentStatus?.DocumentId == null)
                return;

            try
            {
                var document = await _documentService.GetDocumentByIdAsync(SelectedDocumentStatus.DocumentId.Value);
                if (document == null)
                {
                    _dialogService.ShowMessage("Document not found.", "Error");
                    return;
                }

                var fileBytes = await _documentService.GetDocumentFileBytesAsync(document.DocumentId);
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _dialogService.ShowMessage("Document file not found.", "Error");
                    return;
                }

                var filePath = _dialogService.ShowSaveFileDialog(document.FileName, "All Files (*.*)|*.*", "Save Document");

                if (!string.IsNullOrEmpty(filePath))
                {
                    await File.WriteAllBytesAsync(filePath, fileBytes);
                    _dialogService.ShowMessage($"Document saved to {filePath}", "Success");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error downloading document: {ex.Message}", "Error");
            }
        }

        private async Task DeleteDocumentAsync()
        {
            if (SelectedDocumentStatus?.DocumentId == null)
                return;

            var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
            var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                $"Are you sure you want to delete '{SelectedDocumentStatus.DisplayName}'?", 
                "Confirm Delete");

            if (!confirmed)
                return;

            try
            {
                var deleted = await _documentService.DeleteDocumentAsync(SelectedDocumentStatus.DocumentId.Value);
                if (deleted)
                {
                    _dialogService.ShowMessage("Document deleted successfully.", "Success");
                    LoadDocumentStatusCommand.Execute(null);
                    LoadDocumentsCommand.Execute(null);
                }
                else
                {
                    _dialogService.ShowMessage("Failed to delete document.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting document: {ex.Message}", "Error");
            }
        }

        private async Task DeleteSelectedDocumentAsync()
        {
            if (SelectedDocument == null)
                return;

            var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
            var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                $"Are you sure you want to delete '{SelectedDocument.DisplayName}'?", 
                "Confirm Delete");

            if (!confirmed)
                return;

            try
            {
                var deleted = await _documentService.DeleteDocumentAsync(SelectedDocument.DocumentId);
                if (deleted)
                {
                    _dialogService.ShowMessage("Document deleted successfully.", "Success");
                    LoadDocumentStatusCommand.Execute(null);
                    LoadDocumentsCommand.Execute(null);
                    SelectedDocument = null; // Clear selection after deletion
                }
                else
                {
                    _dialogService.ShowMessage("Failed to delete document.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting document: {ex.Message}", "Error");
            }
        }

        private async Task ViewDocumentHistoryAsync()
        {
            if (SelectedDocumentStatus == null)
                return;

            try
            {
                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                Daryva.Services.DateTimeFormatProvider.DateFormat = dateFormat;

                int? tenantId = OwnerFilter == "Tenant" ? SelectedTenantId : null;
                int? tenancyId = null;
                int? houseId = OwnerFilter == "House" ? SelectedHouseId : null;

                var history = await _documentService.GetDocumentHistoryAsync(
                    tenantId, tenancyId, houseId, SelectedDocumentStatus.Type);

                var historyText = string.Join("\n", history.Select(d =>
                    $"v{d.Version} - {d.DisplayName} - {Daryva.Services.DateTimeFormatProvider.FormatDate(d.UploadedAt)} - {(d.IsActive ? "Active" : "Inactive")}"));

                _dialogService.ShowMessage(historyText, $"History: {SelectedDocumentStatus.DisplayName}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading history: {ex.Message}", "Error");
            }
        }

        private async Task DeleteAllDocumentsAsync()
        {
            try
            {
                var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
                var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                    "Are you sure you want to delete ALL documents? This action cannot be undone!",
                    "Delete All Documents");

                if (!confirmed)
                    return;

                // Get all documents
                var allDocuments = await _documentService.GetDocumentsAsync(null, null, null, null);
                
                int deletedCount = 0;
                int failedCount = 0;

                foreach (var doc in allDocuments)
                {
                    try
                    {
                        var deleted = await _documentService.DeleteDocumentAsync(doc.DocumentId);
                        if (deleted)
                            deletedCount++;
                        else
                            failedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        System.Diagnostics.Debug.WriteLine($"Error deleting document {doc.DocumentId}: {ex.Message}");
                    }
                }

                _dialogService.ShowMessage(
                    $"Deleted {deletedCount} document(s).{(failedCount > 0 ? $" Failed to delete {failedCount} document(s)." : "")}",
                    "Delete All Documents");

                // Refresh the lists
                LoadDocumentStatusCommand.Execute(null);
                LoadDocumentsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting all documents: {ex.Message}", "Error");
            }
        }
    }

    /// <summary>
    /// Service interface placeholder for TenancyService (may not exist yet).
    /// </summary>
    public interface ITenancyService
    {
        // Add methods as needed
    }
}
