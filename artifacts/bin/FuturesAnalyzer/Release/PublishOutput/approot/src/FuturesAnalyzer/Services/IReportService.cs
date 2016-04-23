using System.Collections.Generic;
using FuturesAnalyzer.Models;

namespace FuturesAnalyzer.Services
{
    public interface IReportService
    {
        List<DailyPrice> LoadDailyPrices(string fileName);
        IEnumerable<DailyAccountData> GenerateReport(Account account, List<DailyPrice> dailyPrices);
    }
}