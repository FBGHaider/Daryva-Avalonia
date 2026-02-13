using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class UploadDocumentViewModel : BaseViewModel
    {
        private readonly IDocumentService _documentService;
        private readonly IHouseService _houseService;
        private readonly ITenantService _tenantService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;

        private string _ownerType = "Tenant"; // Tenant | House
        private House? _selectedHouse;
        private Tenant? _selectedTenant;
        private string _documentType = "Other";
        private DateTime? _validTo;
        private string _selectedFilePath = string.Empty;
        private byte[]? _selectedFileBytes;

        public UploadDocumentViewModel(
            IDocumentService documentService,
            IHouseService houseService,
            ITenantService tenantService,
            IDialogService dialogService,
            ISettingsService settingsService)
        {
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            Houses = new ObservableCollection<House>();
            Tenants = new ObservableCollection<Tenant>();
            var allTypes = new[] { "StudentConfirmationLetter", "PhotoId", "RightToRent", "TenancyAgreementSigned", "GuarantorAgreement", "InventoryCheckIn", "DepositProtectionCertificate", "NoticeToLeave", "Contract", "CouncilTax", "Bills", "Insurance", "GasSafety", "Epc", "Other" };
            DocumentTypes = new ObservableCollection<string>(allTypes);

            SelectFileCommand = new RelayCommand(async _ => await SelectFileAsync());
            UploadCommand = new RelayCommand(async _ => await UploadAsync(), _ => CanUpload());
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, false));

            _ = LoadHousesAndTenantsAsync();
        }

        public ICommand SelectFileCommand { get; }
        public ICommand UploadCommand { get; }
        public ICommand CancelCommand { get; }

        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<Tenant> Tenants { get; }
        public ObservableCollection<string> DocumentTypes { get; }
        public ObservableCollection<string> OwnerTypeOptions { get; } = new ObservableCollection<string> { "Tenant", "House" };

        public event EventHandler<bool>? CloseRequested;

        public string OwnerType
        {
            get => _ownerType;
            set
            {
                if (SetProperty(ref _ownerType, value ?? "Tenant"))
                {
                    OnPropertyChanged(nameof(IsTenantOwner));
                    OnPropertyChanged(nameof(IsHouseOwner));
                    ((RelayCommand)UploadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsTenantOwner => string.Equals(OwnerType, "Tenant", StringComparison.OrdinalIgnoreCase);
        public bool IsHouseOwner => string.Equals(OwnerType, "House", StringComparison.OrdinalIgnoreCase);

        public House? SelectedHouse
        {
            get => _selectedHouse;
            set
            {
                if (SetProperty(ref _selectedHouse, value))
                {
                    ((RelayCommand)UploadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public Tenant? SelectedTenant
        {
            get => _selectedTenant;
            set
            {
                if (SetProperty(ref _selectedTenant, value))
                {
                    ((RelayCommand)UploadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string DocumentType
        {
            get => _documentType;
            set
            {
                if (SetProperty(ref _documentType, value ?? "Other"))
                {
                    ((RelayCommand)UploadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public DateTime? ValidTo
        {
            get => _validTo;
            set => SetProperty(ref _validTo, value);
        }

        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set
            {
                if (SetProperty(ref _selectedFilePath, value ?? string.Empty))
                {
                    ((RelayCommand)UploadCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private async Task LoadHousesAndTenantsAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                var tenants = await _tenantService.GetAllTenantsAsync();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Houses.Clear();
                    foreach (var h in houses) Houses.Add(h);
                    Tenants.Clear();
                    foreach (var t in tenants) Tenants.Add(t);
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading data: {ex.Message}", "Error");
            }
        }

        private async Task SelectFileAsync()
        {
            try
            {
                var allowedFormats = await _settingsService.GetSettingAsync("AllowedFormats", "PDF,JPG,JPEG,PNG,DOCX") ?? "PDF,JPG,JPEG,PNG,DOCX";
                var filter = BuildFileFilter(allowedFormats);
                var filePath = await _dialogService.ShowOpenFileDialogAsync(filter, "Select Document to Upload");
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return;

                var maxFileSizeMB = await _settingsService.GetSettingAsync<int>("MaxFileSizeMB", 10) ?? 10;
                _selectedFileBytes = await File.ReadAllBytesAsync(filePath);
                var sizeMB = _selectedFileBytes.Length / (1024.0 * 1024.0);
                if (sizeMB > maxFileSizeMB)
                {
                    _dialogService.ShowMessage($"File size ({sizeMB:F1} MB) exceeds maximum ({maxFileSizeMB} MB).", "File Too Large");
                    _selectedFileBytes = null;
                    return;
                }
                SelectedFilePath = Path.GetFileName(filePath);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error selecting file: {ex.Message}", "Error");
            }
        }

        private static string BuildFileFilter(string allowedFormats)
        {
            if (string.IsNullOrWhiteSpace(allowedFormats))
                return "All Files (*.*)|*.*";
            var tokens = allowedFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var patterns = tokens.Select(t => "*." + t.TrimStart('.')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (patterns.Count == 0)
                return "All Files (*.*)|*.*";
            return "Documents|" + string.Join(";", patterns);
        }

        private bool CanUpload()
        {
            if (string.IsNullOrWhiteSpace(DocumentType)) return false;
            if (_selectedFileBytes == null || _selectedFileBytes.Length == 0) return false;
            if (OwnerType == "Tenant") return SelectedTenant != null;
            if (OwnerType == "House") return SelectedHouse != null;
            return false;
        }

        private async Task UploadAsync()
        {
            if (!CanUpload() || _selectedFileBytes == null)
            {
                _dialogService.ShowMessage("Please fill all required fields and select a file.", "Validation");
                return;
            }

            try
            {
                var document = new Document
                {
                    TenantId = OwnerType == "Tenant" ? SelectedTenant!.TenantId : null,
                    TenancyId = null,
                    HouseId = OwnerType == "House" ? SelectedHouse!.HouseId : null,
                    Type = DocumentType,
                    FileName = Path.GetFileName(SelectedFilePath),
                    Source = "Uploaded",
                    ValidTo = ValidTo
                };
                if (DocumentType == "StudentConfirmationLetter")
                {
                    var currentYear = DateTime.Now.Year;
                    document.ValidFrom = new DateTime(currentYear, 9, 1);
                    document.ValidTo = document.ValidTo ?? new DateTime(currentYear + 1, 8, 31);
                }

                var storageRootPath = await _settingsService.GetSettingAsync("DocumentStoragePath", string.Empty);
                var pathToUse = string.IsNullOrWhiteSpace(storageRootPath) ? null : storageRootPath.Trim();
                await _documentService.UploadDocumentAsync(document, _selectedFileBytes, pathToUse);

                _dialogService.ShowMessage("Document uploaded successfully.", "Success");
                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error uploading: {ex.Message}", "Error");
            }
        }
    }
}
