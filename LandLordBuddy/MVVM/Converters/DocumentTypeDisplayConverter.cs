using System.Globalization;
using System.Windows.Data;

namespace LandLordBuddy.MVVM.Converters
{
    /// <summary>
    /// Converter that converts document type keys to display names.
    /// </summary>
    public class DocumentTypeDisplayConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> DocumentTypeDisplayNames = new()
        {
            ["StudentConfirmationLetter"] = "Student Confirmation Letter",
            ["PhotoId"] = "Photo ID",
            ["RightToRent"] = "Right to Rent",
            ["TenancyAgreementSigned"] = "Tenancy Agreement (Signed)",
            ["GuarantorAgreement"] = "Guarantor Agreement",
            ["InventoryCheckIn"] = "Inventory & Check-in",
            ["DepositProtectionCertificate"] = "Deposit Protection Certificate",
            ["NoticeToLeave"] = "Notice to Leave",
            ["Other"] = "Other"
        };

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string docType && !string.IsNullOrWhiteSpace(docType))
            {
                return DocumentTypeDisplayNames.GetValueOrDefault(docType, docType);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
