namespace LandLordBuddy.MVVM.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private byte _defaultPaymentDueDay = 1;
        private string _exportFolderPath = string.Empty;

        public SettingsViewModel()
        {
            // Settings will be loaded from configuration in future version
        }

        public byte DefaultPaymentDueDay
        {
            get => _defaultPaymentDueDay;
            set => SetProperty(ref _defaultPaymentDueDay, value);
        }

        public string ExportFolderPath
        {
            get => _exportFolderPath;
            set => SetProperty(ref _exportFolderPath, value);
        }
    }
}
