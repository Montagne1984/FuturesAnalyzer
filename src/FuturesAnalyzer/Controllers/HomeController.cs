using System.Linq;
using FuturesAnalyzer.Models;
using FuturesAnalyzer.Models.States;
using FuturesAnalyzer.Services;
using FuturesAnalyzer.ViewModels;
using Microsoft.AspNet.Mvc;

namespace FuturesAnalyzer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IReportService _reportService;

        public HomeController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public IActionResult Index()
        {
            var model = new ReportSettingViewModel();
            return View(model);
        }

        public JsonResult Report(ReportSettingViewModel model)
        {
            MarketState.StopLossUnit = model.StopLossUnit;
            MarketState.StopLossCriteria = model.StopLossCriteria;
            MarketState.StartProfitCriteria = model.StartProfitCriteria;
            MarketState.StopProfitCriteria = model.StopProfitCriteria;
            MarketState.StartProfitCriteriaForMultiUnits = model.StartProfitCriteriaForMultiUnits;
            MarketState.NeverEnterAmbiguousState = model.NeverEnterAmbiguousState;
            AmbiguousState.OpenCriteria = model.OpenCriteria;
            AmbiguousState.FollowTrend = model.FollowTrend;
            var dailyPrices = _reportService.LoadDailyPrices("Data/" + model.SelectedProductName + ".csv");
            var account = new Account {TransactionFeeRate = model.TransactionFeeRate};
            var dateRange = dailyPrices.Where(p => p.Date >= model.StartDate && p.Date <= model.EndDate).ToList();
            var report = _reportService.GenerateReport(account, dateRange).ToList();
            //if (report.Any())
            //{
            //    var bestBalance = report.Last().Balance;
            //    for (var stopLoss = 0.01m; stopLoss <= 0.04m; stopLoss += 0.001m)
            //    {
            //        for (var startProfit = 0.02m; startProfit <= 0.2m; startProfit += 0.001m)
            //        {
            //            for (var stopProfit = 0.1m; stopProfit <= 0.3m; stopProfit += 0.01m)
            //            {
            //                for (var openCriteria = 0.01m; openCriteria <= 0.03m; openCriteria += 0.001m)
            //                {
            //                    MarketState.StopLossCriteria = stopLoss;
            //                    MarketState.StartProfitCriteria = startProfit;
            //                    MarketState.StopProfitCriteria = stopProfit;
            //                    AmbiguousState.OpenCriteria = openCriteria;
            //                    AmbiguousState.FollowTrend = true;
            //                    account = new Account { TransactionFeeRate = model.TransactionFeeRate };
            //                    var result = _reportService.GenerateReport(account, dateRange).ToList();
            //                    if (result.Last().Balance > bestBalance)
            //                    {
            //                        bestBalance = result.Last().Balance;
            //                    }
            //                    AmbiguousState.FollowTrend = false;
            //                    account = new Account { TransactionFeeRate = model.TransactionFeeRate };
            //                    result = _reportService.GenerateReport(account, dateRange).ToList();
            //                    if (result.Last().Balance > bestBalance)
            //                    {
            //                        bestBalance = result.Last().Balance;
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}
            return Json(
                new
                {
                    Report = report

                }
                );
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
