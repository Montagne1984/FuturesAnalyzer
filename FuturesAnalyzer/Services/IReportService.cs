using System.Collections.Generic;
using FuturesAnalyzer.Models;

namespace FuturesAnalyzer.Services
{
    public interface IReportService
    {
        List<DailyPrice> LoadDailyPrices(string fileName);
        IEnumerable<DailyAccountData> GenerateReport(Account account, List<DailyPrice> dailyPrices);
        decimal GetMaxLossRange(IEnumerable<DailyAccountData> report);
        decimal GetBestBudgetChangeFactor(IEnumerable<DailyAccountData> report, int bondPercentage);
        IEnumerable<DailyAccountData> GetReportWithBudgetFactor(DailyAccountData[] report, decimal budgetChangeFactor, int bondPercentage);
    }
}