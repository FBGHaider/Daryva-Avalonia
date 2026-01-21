using System;
using System.Collections.Generic;
using System.Linq;
using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public static class LedgerTotalsCalculator
    {
        public static LedgerTotals CalculateTotals(LedgerExportModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var totals = new LedgerTotals
            {
                TotalRent = model.Rows?.Sum(r => r.RentAmount) ?? 0m,
                TotalDeposit = model.Rows?.Sum(r => r.DepositAmount) ?? 0m,
                RentGivenToLandlord = model.RentGivenToLandlord
            };

            if (model.Collectors != null && model.Rows != null)
            {
                foreach (var collector in model.Collectors.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct())
                {
                    var collected = model.Rows
                        .Where(r => string.Equals(r.RentCollectedBy, collector, StringComparison.OrdinalIgnoreCase))
                        .Sum(r => r.RentAmount);

                    totals.RentCollectedBy[collector] = collected;
                }
            }

            return totals;
        }
    }
}
