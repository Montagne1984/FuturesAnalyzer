using System.Linq;
using FuturesAnalyzer.Models;
using FuturesAnalyzer.Models.States;
using FuturesAnalyzer.Services;
using FuturesAnalyzer.ViewModels;
using Microsoft.AspNet.Mvc;
using System;
using System.Collections.Generic;

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
            var report = GetReport(model);
            return Json(
                new
                {
                    Report = report
                }
                );
        }

        public JsonResult Optimize(ReportSettingViewModel model)
        {
            ReportSettingViewModel bestSettings = model.Clone();
            var bestPercentageBalance = 0m;
            var settings = bestSettings;

            var followTrends = new bool[] { true, false };
            
            for (var stopLoss = 0.01m; stopLoss <= 0.04m; stopLoss += 0.01m)
            {
                for (var startProfit = 0.02m; startProfit <= 0.2m; startProfit += 0.01m)
                {
                    for (var stopProfit = 0.1m; stopProfit <= 0.3m; stopProfit += 0.1m)
                    {
                        settings.StopLossCriteria = stopLoss;
                        settings.StartProfitCriteria = startProfit;
                        settings.StopProfitCriteria = stopProfit;

                        settings.NeverEnterAmbiguousState = true;
                        var result = GetReport(settings);
                        if (result.Last().PercentageBalance > bestPercentageBalance)
                        {
                            bestPercentageBalance = result.Last().PercentageBalance;
                            bestSettings = settings.Clone();
                        }

                        settings.NeverEnterAmbiguousState = false;
                        for (var openCriteria = 0.01m; openCriteria <= 0.06m; openCriteria += 0.01m)
                        {
                            settings.OpenCriteria = openCriteria;

                            foreach(var followTrend in followTrends)
                            {
                                settings.FollowTrend = followTrend;
                                result = GetReport(settings);
                                if (result.Last().PercentageBalance > bestPercentageBalance)
                                {
                                    bestPercentageBalance = result.Last().PercentageBalance;
                                    bestSettings = settings.Clone();
                                }
                            }
                        }
                    }
                }
            }
            return Json(
                new
                {
                    BestSettings = bestSettings,
                    Report = GetReport(bestSettings)
                });
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

        private IEnumerable<DailyAccountData> GetReport(ReportSettingViewModel model)
        {
            MarketState.StopLossUnit = model.StopLossUnit;
            MarketState.StopLossCriteria = model.StopLossCriteria;
            MarketState.StartProfitCriteria = model.StartProfitCriteria;
            MarketState.StopProfitCriteria = model.StopProfitCriteria;
            MarketState.StartProfitCriteriaForMultiUnits = model.StartProfitCriteriaForMultiUnits;
            MarketState.NeverEnterAmbiguousState = model.NeverEnterAmbiguousState;
            MarketState.AppendUnitCountAfterProfitStart = model.AppendUnitCountAfterProfitStart;
            MarketState.MinimumPriceUnit = model.MinimumPriceUnit;
            AmbiguousState.OpenCriteria = model.OpenCriteria;
            AmbiguousState.FollowTrend = model.FollowTrend;
            var dailyPrices = _reportService.LoadDailyPrices("Data/" + model.SelectedProductName + ".csv");
            var account = new Account { TransactionFeeRate = model.TransactionFeeRate };
            var dateRange = dailyPrices.Where(p => p.Date >= model.StartDate && p.Date <= model.EndDate).ToList();
            return _reportService.GenerateReport(account, dateRange).ToList();
        }
    }
}
