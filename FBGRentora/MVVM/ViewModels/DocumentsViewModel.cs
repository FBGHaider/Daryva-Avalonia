using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using FBGRentora.MVVM.Commands;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Business;
using FBGRentora.Services.Dialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace FBGRentora.MVVM.ViewModels
{
    public class DocumentsViewModel : BaseViewModel
    {
        private readonly IDocumentService _documentService;
        private readonly ITenantService _tenantService;
        private readonly ITenancyService? _tenancyService;
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;

        private string _ownerFilter = "All"; // All, Tenant, Tenancy, House
        private int? _selectedTenantId;
        private int? _selectedTenancyId;
        private int? _selectedHouseId;
        private string _typeFilter = "All";
        private string _selectedDocumentTypeForUpload = "Other"; // Document type selected from dropdown for upload
        private DocumentStatusItem? _selectedDocumentStatus;
        private Document? _selectedDocument;

        public DocumentsViewModel(
            IDocumentService documentService,
            ITenantService tenantService,
            IHouseService houseService,
            IDialogService dialogService,
            IServiceProvider serviceProvider)
        {
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Try to get ITenancyService if available
            _tenancyService = serviceProvider.GetService<ITenancyService>();

            Documents = new ObservableCollection<Document>();
            DocumentStatusChecklist = new ObservableCollection<DocumentStatusItem>();
            Tenants = new ObservableCollection<Tenant>();
            Houses = new ObservableCollection<House>();
            AvailableDocumentTypes = new ObservableCollection<string>
            {
                "StudentConfirmationLetter",
                "PhotoId",
                "RightToRent",
                "TenancyAgreementSigned",
                "GuarantorAgreement",
                "InventoryCheckIn",
                "DepositProtectionCertificate",
                "NoticeToLeave",
                "Other"
            };

            LoadDocumentsCommand = new RelayCommand(async _ => await LoadDocumentsAsync());
            LoadDocumentStatusCommand = new RelayCommand(async _ => await LoadDocumentStatusAsync());
            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            UploadDocumentCommand = new RelayCommand(_ => ShowUploadDocumentDialog(), _ => CanUploadDocument());
            ViewDocumentCommand = new RelayCommand(_ => ViewDocumentAsync(), _ => SelectedDocumentStatus != null && SelectedDocumentStatus.DocumentId.HasValue);
            DownloadDocumentCommand = new RelayCommand(_ => DownloadDocumentAsync(), _ => SelectedDocumentStatus != null && SelectedDocumentStatus.DocumentId.HasValue);
            DeleteDocumentCommand = new RelayCommand(async _ => await DeleteDocumentAsync(), _ => SelectedDocumentStatus != null && SelectedDocumentStatus.DocumentId.HasValue);
            DeleteSelectedDocumentCommand = new RelayCommand(async _ => await DeleteSelectedDocumentAsync(), _ => SelectedDocument != null);
            DeleteAllDocumentsCommand = new RelayCommand(async _ => await DeleteAllDocumentsAsync());
            ViewHistoryCommand = new RelayCommand(async _ => await ViewDocumentHistoryAsync(), _ => SelectedDocumentStatus != null);

            // Load initial data
            LoadHousesCommand.Execute(null);
            LoadTenantsCommand.Execute(null);
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

        public string OwnerFilter
        {
            get => _ownerFilter;
            set
            {
                if (SetProperty(ref _ownerFilter, value))
                {
                    // Reset only the selections that don't match the new filter (to avoid circular updates)
                    if (value != "Tenant" && _selectedTenantId.HasValue)
                    {
                        _selectedTenantId = null;
                        OnPropertyChanged(nameof(SelectedTenantId));
                    }
                    if (value != "Tenancy" && _selectedTenancyId.HasValue)
                    {
                        _selectedTenancyId = null;
                        OnPropertyChanged(nameof(SelectedTenancyId));
                    }
                    if (value != "House" && _selectedHouseId.HasValue)
                    {
                        _selectedHouseId = null;
                        OnPropertyChanged(nameof(SelectedHouseId));
                    }
                    
                    ((RelayCommand)UploadDocumentCommand).RaiseCanExecuteChanged();
                    LoadDocumentStatusCommand.Execute(null);
                }
            }
        }

        public int? SelectedTenantId
        {
            get => _selectedTenantId;
            set
            {
                if (SetProperty(ref _selectedTenantId, value))
                {
                    // Only clear other selections if we're setting a value (not clearing)
                    if (value.HasValue)
                    {
                        if (_selectedTenancyId.HasValue)
                        {
                            _selectedTenancyId = null;
                            OnPropertyChanged(nameof(SelectedTenancyId));
                        }
                        if (_selectedHouseId.HasValue)
                        {
                            _selectedHouseId = null;
                            OnPropertyChanged(nameof(SelectedHouseId));
                        }
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
                    // Only clear other selections if we're setting a value (not clearing)
                    if (value.HasValue)
                    {
                        if (_selectedTenantId.HasValue)
                        {
                            _selectedTenantId = null;
                            OnPropertyChanged(nameof(SelectedTenantId));
                        }
                        if (_selectedHouseId.HasValue)
                        {
                            _selectedHouseId = null;
                            OnPropertyChanged(nameof(SelectedHouseId));
                        }
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
                    // Only clear other selections if we're setting a value (not clearing)
                    if (value.HasValue)
                    {
                        if (_selectedTenantId.HasValue)
                        {
                            _selectedTenantId = null;
                            OnPropertyChanged(nameof(SelectedTenantId));
                        }
                        if (_selectedTenancyId.HasValue)
                        {
                            _selectedTenancyId = null;
                            OnPropertyChanged(nameof(SelectedTenancyId));
                        }
                    }
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
                if (SetProperty(ref _typeFilter, value))
                {
                    LoadDocumentsCommand.Execute(null);
                }
            }
        }

        public string SelectedDocumentTypeForUpload
        {
            get => _selectedDocumentTypeForUpload;
            set => SetProperty(ref _selectedDocumentTypeForUpload, value);
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
            // Must have an owner type selected AND the corresponding owner selected
            if (OwnerFilter == "Tenant")
                return SelectedTenantId.HasValue;
            if (OwnerFilter == "Tenancy")
                return SelectedTenancyId.HasValue;
            if (OwnerFilter == "House")
                return SelectedHouseId.HasValue;
            return false; // "All" or invalid filter means no upload allowed
        }

        private async Task LoadDocumentsAsync()
        {
            try
            {
                int? tenantId = OwnerFilter == "Tenant" ? SelectedTenantId : null;
                int? tenancyId = OwnerFilter == "Tenancy" ? SelectedTenancyId : null;
                int? houseId = OwnerFilter == "House" ? SelectedHouseId : null;
                string? type = TypeFilter != "All" ? TypeFilter : null;

                var documents = await _documentService.GetDocumentsAsync(tenantId, tenancyId, houseId, type);

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
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
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
                int? tenantId = OwnerFilter == "Tenant" ? SelectedTenantId : null;
                int? tenancyId = OwnerFilter == "Tenancy" ? SelectedTenancyId : null;
                int? houseId = OwnerFilter == "House" ? SelectedHouseId : null;

                // Only load checklist if an owner is selected
                if (!tenantId.HasValue && !tenancyId.HasValue && !houseId.HasValue)
                {
                    DocumentStatusChecklist.Clear();
                    return;
                }

                var checklist = await _documentService.GetDocumentStatusChecklistAsync(tenantId, tenancyId, houseId);

                // Ensure UI updates happen on the UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    DocumentStatusChecklist.Clear();
                    foreach (var item in checklist.OrderBy(i => i.DisplayName))
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
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
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
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
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

        private void ShowUploadDocumentDialog()
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "All Files (*.*)|*.*|PDF Files (*.pdf)|*.pdf|Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|Word Documents (*.doc;*.docx)|*.doc;*.docx",
                    Title = "Select Document to Upload"
                };

                if (openDialog.ShowDialog() == true)
                {
                    var filePath = openDialog.FileName;
                    var fileName = Path.GetFileName(filePath);
                    var fileBytes = File.ReadAllBytes(filePath);

                    // Use selected document type from dropdown, or fallback to SelectedDocumentStatus, or "Other"
                    string docType = !string.IsNullOrWhiteSpace(SelectedDocumentTypeForUpload) 
                        ? SelectedDocumentTypeForUpload 
                        : SelectedDocumentStatus?.Type ?? "Other";

                    UploadDocumentAsync(docType, fileName, fileBytes);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error selecting file: {ex.Message}", "Error");
            }
        }

        private async void UploadDocumentAsync(string docType, string fileName, byte[] fileBytes)
        {
            try
            {
                // Validate that an owner is selected
                int? tenantId = null;
                int? tenancyId = null;
                int? houseId = null;

                if (OwnerFilter == "Tenant")
                {
                    if (!SelectedTenantId.HasValue)
                    {
                        _dialogService.ShowMessage("Please select a tenant before uploading a document.", "No Tenant Selected");
                        return;
                    }
                    tenantId = SelectedTenantId;
                }
                else if (OwnerFilter == "Tenancy")
                {
                    if (!SelectedTenancyId.HasValue)
                    {
                        _dialogService.ShowMessage("Please select a tenancy before uploading a document.", "No Tenancy Selected");
                        return;
                    }
                    tenancyId = SelectedTenancyId;
                }
                else if (OwnerFilter == "House")
                {
                    if (!SelectedHouseId.HasValue)
                    {
                        _dialogService.ShowMessage("Please select a house before uploading a document.", "No House Selected");
                        return;
                    }
                    houseId = SelectedHouseId;
                }
                else
                {
                    _dialogService.ShowMessage("Please select an owner type (Tenant, Tenancy, or House) and the specific owner before uploading a document.", "No Owner Selected");
                    return;
                }

                var document = new Document
                {
                    TenantId = tenantId,
                    TenancyId = tenancyId,
                    HouseId = houseId,
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

                var uploadedDoc = await _documentService.UploadDocumentAsync(document, fileBytes);

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

                var saveDialog = new SaveFileDialog
                {
                    FileName = document.FileName,
                    Filter = "All Files (*.*)|*.*"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    await File.WriteAllBytesAsync(saveDialog.FileName, fileBytes);
                    _dialogService.ShowMessage($"Document saved to {saveDialog.FileName}", "Success");
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

            var confirmed = _dialogService.ShowConfirmation(
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

            var confirmed = _dialogService.ShowConfirmation(
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
                int? tenantId = OwnerFilter == "Tenant" ? SelectedTenantId : null;
                int? tenancyId = OwnerFilter == "Tenancy" ? SelectedTenancyId : null;
                int? houseId = OwnerFilter == "House" ? SelectedHouseId : null;

                var history = await _documentService.GetDocumentHistoryAsync(
                    tenantId, tenancyId, houseId, SelectedDocumentStatus.Type);

                var historyText = string.Join("\n", history.Select(d => 
                    $"v{d.Version} - {d.DisplayName} - {d.UploadedAt:yyyy-MM-dd} - {(d.IsActive ? "Active" : "Inactive")}"));

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
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to delete ALL documents? This action cannot be undone!",
                    "Delete All Documents",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
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
