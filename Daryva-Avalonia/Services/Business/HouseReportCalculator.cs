using System;
using System.Collections.Generic;
using System.Linq;
using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public static class HouseReportCalculator
    {
        public static void CalculateTotals(HouseReportExportModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            // Calculate occupancy rate
            if (model.TotalRooms > 0)
            {
                model.OccupancyRate = (decimal)model.ActiveTenantsCount / model.TotalRooms * 100;
            }

            // Calculate net income
            model.NetIncome = model.TotalRentCollected - model.TotalExpenses;

            // Calculate expenses by category
            model.ExpensesByCategory.Clear();
            foreach (var expense in model.Expenses)
            {
                if (!model.ExpensesByCategory.ContainsKey(expense.Category))
                {
                    model.ExpensesByCategory[expense.Category] = 0m;
                }
                model.ExpensesByCategory[expense.Category] += expense.Amount;
            }

            // Calculate rent collected by collector
            model.RentCollectedByCollector.Clear();
            foreach (var row in model.MonthlyLedger)
            {
                if (!string.IsNullOrWhiteSpace(row.RentCollectedBy))
                {
                    if (!model.RentCollectedByCollector.ContainsKey(row.RentCollectedBy))
                    {
                        model.RentCollectedByCollector[row.RentCollectedBy] = 0m;
                    }
                    model.RentCollectedByCollector[row.RentCollectedBy] += row.RentAmount;
                }
            }

            // Extract unique collectors
            model.Collectors = model.MonthlyLedger
                .Where(r => !string.IsNullOrWhiteSpace(r.RentCollectedBy))
                .Select(r => r.RentCollectedBy!)
                .Distinct()
                .ToList();
        }
    }
}
