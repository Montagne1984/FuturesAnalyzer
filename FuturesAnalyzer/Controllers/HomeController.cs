using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FuturesAnalyzer.ViewModels;
using FuturesAnalyzer.Services;
using System.IO;
using FuturesAnalyzer.Models;
using CsvHelper;
using Microsoft.Extensions.Logging;

namespace FuturesAnalyzer.Controllers
{
    public class HomeController : Controller
    {
        private IReportService _reportService;
        private object lockObject = new object();
        private readonly ILogger _logger;

        public IActionResult Index()
        {
            var model = new ReportSettingViewModel();
            return View(model);
        }

        public JsonResult Report(ReportSettingViewModel model)
        {
            _reportService = model.UseCrossStarStrategy ? new CrossStarReportService() : new ReportService();
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
            var dailyPrices = _reportService.LoadDailyPrices("Data/" + model.SelectedProductName + ".csv");
            var report = GetReport(model, dailyPrices);
            return Json(
                new
                {
                    Report = report
                }
                );
        }

        public JsonResult Replay(string settings, string filePath, decimal transactionFeeRate, decimal minimumPriceUnit)
        {
            try
            {
                var fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1);
                var dateIndex = fileName.IndexOf("20");
                var productName = fileName.Substring(0, dateIndex);
                var index = dateIndex + 8;
                var notUseClosePrice = fileName[index] == 'T';
                index += notUseClosePrice ? 4 : 5;
                var onlyUseClosePrice = fileName[index] == 'T';
                index += onlyUseClosePrice ? 4 : 5;
                var closeAmbiguousStateToday = fileName[index] == 'T';

                var topSettings = new SortedList<decimal, List<SettingResult>>();
                var number = settings.Split("\n").Length;
                var dailyPrices = _reportService.LoadDailyPrices("Data/" + productName + ".csv");
                var startDate = new DateTime(2000, 1, 1);
                var endDate = DateTime.Now;
                using (var textReader = new StringReader(settings))
                using (var csvReader = new CsvReader(textReader))
                {
                    csvReader.Configuration.HasHeaderRecord = true;
                    csvReader.Read();
                    while (csvReader.Read())
                    {
                        var setting = new SettingResult
                        {
                            Setting = new ReportSettingViewModel
                            {
                                NotUseClosePrice = notUseClosePrice,
                                OnlyUseClosePrice = onlyUseClosePrice,
                                CloseAmbiguousStateToday = closeAmbiguousStateToday,
                                SelectedProductName = productName,
                                StartDate = startDate,
                                EndDate = endDate,
                                TransactionFeeRate = transactionFeeRate,
                                MinimumPriceUnit = minimumPriceUnit,
                                StopLossCriteria = csvReader.GetField<decimal>(0),
                                StopProfitCriteria = csvReader.GetField<decimal>(1),
                                StartProfitCriteria = csvReader.GetField<decimal>(2),
                                OpenCriteria = csvReader.GetField<decimal>(3),
                                FollowTrend = csvReader.GetField<bool>(4)
                            }
                        };
                        var report = GetReport(setting.Setting, dailyPrices);
                        setting.Result = report.Last().PercentageBalance;
                        UpdateTopThreeSettings(ref topSettings, setting, number);
                    }
                }

                using (var fileStream = new FileStream($"Results\\{productName}\\{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now.ToString("yyyyMMdd")}.csv", FileMode.Create))
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine("StopLoss,StopProfit,StartProfit,OpenCriteria,FollowTrend,Result");
                    for (var i = topSettings.Count - 1; i >= 0; i--)
                    {
                        foreach (var s in topSettings.ElementAt(i).Value)
                        {
                            streamWriter.WriteLine(
                                $"{s.Setting.StopLossCriteria},{s.Setting.StopProfitCriteria},{s.Setting.StartProfitCriteria},{s.Setting.OpenCriteria},{s.Setting.FollowTrend},{s.Result}");
                        }
                    }
                }

                var bestSettings = topSettings.Last().Value.First().Setting;
                return Json(
                    new
                    {
                        BestSettings = bestSettings,
                        Report = GetReport(bestSettings, dailyPrices)
                    });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return null;
            }
        }

        public JsonResult Optimize(ReportSettingViewModel model)
        {
            _reportService = model.UseCrossStarStrategy ? new CrossStarReportService() : new ReportService();
            ReportSettingViewModel bestSettings = model.Clone();
            var bestPercentageBalance = 0m;


            var startTime = DateTime.Now;

            var dailyPrices = _reportService.LoadDailyPrices("Data/" + model.SelectedProductName + ".csv");

            //var range = ranges.ContainsKey(model.SelectedProductName) ? ranges[model.SelectedProductName] : ranges["big"];
            var range = model.UseAverageMarketState ? ranges["average"] : ranges["big"];
            if (model.UseCrossStarStrategy)
            {
                range = ranges["crossstar"];
            }
            var followTrends = model.UseAverageMarketState ? new[] { true } : new[] { true, false };
            //var followTrends = new[] { true };

            var topSettingsDictionary = new Dictionary<string, SortedList<decimal, List<SettingResult>>>();

            var startProfitValue = range.BottomStartProfit;
            for (;
            startProfitValue <= range.TopStartProfit;
            startProfitValue += range.StartProfitStep)
            {
                var key = GetTopSettingListKey(startProfitValue, range);
                if (!topSettingsDictionary.ContainsKey(key))
                {
                    topSettingsDictionary.Add(key, new SortedList<decimal, List<SettingResult>>());
                }
            }
            if(startProfitValue - range.TopStartProfit < 0.01m)
            {
                var key = GetTopSettingListKey(startProfitValue, range);
                if (!topSettingsDictionary.ContainsKey(key))
                {
                    topSettingsDictionary.Add(key, new SortedList<decimal, List<SettingResult>>());
                }
            }

            //var topThreeSettings = new SortedList<decimal, List<SettingResult>>();
            for (var stopLoss = range.BottomStopLoss; stopLoss <= range.TopStopLoss; stopLoss += range.StopLossStep)
            {
                for (var startProfit = range.BottomStartProfit;
                startProfit <= range.TopStartProfit;
                startProfit += range.StartProfitStep)
                {
                    
                    var taskList = new List<Task>();
                    for (var stopProfit = range.BottomStopProfit;
                        stopProfit <= range.TopStopProfit;
                        stopProfit += range.StopProfitStep)
                    {
                        var settings = model.Clone();
                        settings.StopLossCriteria = stopLoss;
                        settings.StartProfitCriteria = startProfit;
                        settings.StopProfitCriteria = stopProfit;
                        settings.NeverEnterAmbiguousState = true;

                        taskList.Add(new Task(() =>
                        {
                            var result = GetReport(settings, dailyPrices);
                            var percentageBalance = result.Last().PercentageBalance;
                            if (percentageBalance > bestPercentageBalance)
                            {
                                bestPercentageBalance = percentageBalance;
                                bestSettings = settings;
                            }
                            var settingResult = new SettingResult { Result = percentageBalance, Setting = settings };
                            var topSettings = topSettingsDictionary[GetTopSettingListKey(startProfit, range)];
                            UpdateTopThreeSettings(ref topSettings, settingResult);
                        }
                            ));

                        if (range.NeverEnterAmbiguousState)
                        {
                            continue;
                        }

                        for (var openCriteria = range.BottomOpenCriteria;
                            openCriteria <= range.TopOpenCriteria;
                            openCriteria += range.OpenCriteriaStep)
                        {
                            foreach (var followTrend in followTrends)
                            {
                                var currentSettings = model.Clone();
                                currentSettings.StopLossCriteria = stopLoss;
                                currentSettings.StartProfitCriteria = startProfit;
                                currentSettings.StopProfitCriteria = stopProfit;
                                currentSettings.NeverEnterAmbiguousState = false;
                                currentSettings.OpenCriteria = openCriteria;
                                currentSettings.FollowTrend = followTrend;

                                taskList.Add(new Task(() =>
                                {
                                    var result = GetReport(currentSettings, dailyPrices);
                                    var percentageBalance = result.Last().PercentageBalance;
                                    if (percentageBalance > bestPercentageBalance)
                                    {
                                        bestPercentageBalance = percentageBalance;
                                        bestSettings = currentSettings;
                                    }
                                    var settingResult = new SettingResult
                                    {
                                        Result = percentageBalance,
                                        Setting = currentSettings
                                    };
                                    var topSettings = topSettingsDictionary[GetTopSettingListKey(startProfit, range)];
                                    UpdateTopThreeSettings(ref topSettings, settingResult);
                                }
                                    ));
                            }
                        }
                    }
                    Parallel.For(0, taskList.Count, (i) =>
                    {
                        taskList[i].Start();
                    });
                    Task.WaitAll(taskList.ToArray());
                }
            }

            var timeSpan = DateTime.Now.Subtract(startTime);
            var overallTopSettings = new SortedList<decimal, List<SettingResult>>();
            foreach (var key in topSettingsDictionary.Keys)
            {
                var topSettings = topSettingsDictionary[key];
                using (var fileStream = new FileStream($"Results\\{model.SelectedProductName}\\{model.SelectedProductName}{dailyPrices.Last().Date.ToString("yyyyMMdd")}{model.NotUseClosePrice}{model.OnlyUseClosePrice}{model.CloseAmbiguousStateToday}_{key}.csv", FileMode.Create))
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine("StopLoss,StopProfit,StartProfit,OpenCriteria,FollowTrend,Result");
                    for (var i = topSettings.Count - 1; i >= 0; i--)
                    {
                        foreach (var settings in topSettings.ElementAt(i).Value)
                        {
                            UpdateTopThreeSettings(ref overallTopSettings, settings);
                            streamWriter.WriteLine(
                                $"{settings.Setting.StopLossCriteria},{settings.Setting.StopProfitCriteria},{settings.Setting.StartProfitCriteria},{settings.Setting.OpenCriteria},{settings.Setting.FollowTrend},{settings.Result}");
                        }
                    }
                }
            }
            
            using (var fileStream = new FileStream($"Results\\{model.SelectedProductName}\\{model.SelectedProductName}{dailyPrices.Last().Date.ToString("yyyyMMdd")}{model.NotUseClosePrice}{model.OnlyUseClosePrice}{model.CloseAmbiguousStateToday}_{range.BottomStartProfit * 1000}_{range.TopStartProfit * 1000}.csv", FileMode.Create))
            using (var streamWriter = new StreamWriter(fileStream))
            {
                streamWriter.WriteLine("StopLoss,StopProfit,StartProfit,OpenCriteria,FollowTrend,Result");
                for (var i = overallTopSettings.Count - 1; i >= 0; i--)
                {
                    foreach (var settings in overallTopSettings.ElementAt(i).Value)
                    {
                        streamWriter.WriteLine(
                            $"{settings.Setting.StopLossCriteria},{settings.Setting.StopProfitCriteria},{settings.Setting.StartProfitCriteria},{settings.Setting.OpenCriteria},{settings.Setting.FollowTrend},{settings.Result}");
                    }
                }
            }

            for (var i = 0; i < 100; i++)
            {
                Console.Beep();
            }

            return Json(
                new
                {
                    BestSettings = bestSettings,
                    Report = GetReport(bestSettings, dailyPrices)
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

        private IEnumerable<DailyAccountData> GetReport(ReportSettingViewModel model, List<DailyPrice> dailyPrices)
        {
            var account = new Account
            {
                TransactionFeeRate = model.TransactionFeeRate,
                StopLossUnit = model.StopLossUnit,
                StopLossCriteria = model.StopLossCriteria,
                StartProfitCriteria = model.StartProfitCriteria,
                StopProfitCriteria = model.StopProfitCriteria,
                StartProfitCriteriaForMultiUnits = model.StartProfitCriteriaForMultiUnits,
                NeverEnterAmbiguousState = model.NeverEnterAmbiguousState,
                AppendUnitCountAfterProfitStart = model.AppendUnitCountAfterProfitStart,
                MinimumPriceUnit = model.MinimumPriceUnit,
                OpenCriteria = model.OpenCriteria,
                FollowTrend = model.FollowTrend,
                NotUseClosePrice = model.NotUseClosePrice,
                UseAverageMarketState = model.UseAverageMarketState,
                CloseAfterProfit = model.CloseAfterProfit,
                OnlyUseClosePrice = model.OnlyUseClosePrice,
                UseCrossStarStrategy = model.UseCrossStarStrategy,
                UseInternalProfit = model.UseInternalProfit,
                CloseAmbiguousStateToday = model.CloseAmbiguousStateToday
            };
            var dateRange = dailyPrices.Where(p => p.Date >= model.StartDate && p.Date <= model.EndDate).ToList();
            return _reportService.GenerateReport(account, dateRange).ToList();
        }

        private Dictionary<string, OptimizeRange> ranges;

        private void UpdateTopThreeSettings(
            ref SortedList<decimal, List<SettingResult>> topThreeSettings, SettingResult result, int number = 100)
        {
            lock (lockObject)
            {
                if (topThreeSettings.ContainsKey(result.Result))
                {
                    topThreeSettings[result.Result].Add(result);
                    return;
                }
                if (topThreeSettings.Count >= number && result.Result < topThreeSettings.First().Key)
                {
                    return;
                }
                topThreeSettings.Add(result.Result, new List<SettingResult> { result });
                if (topThreeSettings.Count > number)
                {
                    topThreeSettings.RemoveAt(0);
                }
            }
        }

        public HomeController(IReportService reportService, ILogger<HomeController> logger)
        {
            _reportService = reportService;
            _logger = logger;
            ranges = new Dictionary<string, OptimizeRange>();
            ranges.Add("big",
                new OptimizeRange
                {
                    BottomStopLoss = 0m,
                    TopStopLoss = 0.04m,
                    StopLossStep = 0.001m,
                    BottomStartProfit = 0.161m,
                    TopStartProfit = 0.2m,
                    StartProfitStep = 0.001m,
                    BottomStopProfit = 0m,
                    TopStopProfit = 0.3m,
                    StopProfitStep = 0.01m,
                    BottomOpenCriteria = 0.001m,
                    TopOpenCriteria = 0.04m,
                    OpenCriteriaStep = 0.001m,
                    NeverEnterAmbiguousState = false
                });
            ranges.Add("average",
                new OptimizeRange
                {
                    BottomStopLoss = 0.005m,
                    TopStopLoss = 0.04m,
                    StopLossStep = 0.001m,
                    BottomStartProfit = 0.08m,
                    TopStartProfit = 0.08m,
                    StartProfitStep = 0.001m,
                    BottomStopProfit = 0.3m,
                    TopStopProfit = 0.3m,
                    StopProfitStep = 0.01m,
                    BottomOpenCriteria = 0.005m,
                    TopOpenCriteria = 0.04m,
                    OpenCriteriaStep = 0.001m,
                    NeverEnterAmbiguousState = false
                });
            ranges.Add("crossstar",
                new OptimizeRange
                {
                    BottomStopLoss = 0.005m,
                    TopStopLoss = 0.005m,
                    StopLossStep = 0.001m,
                    BottomStartProfit = 0.08m,
                    TopStartProfit = 0.08m,
                    StartProfitStep = 0.001m,
                    BottomStopProfit = 0.3m,
                    TopStopProfit = 0.3m,
                    StopProfitStep = 0.01m,
                    BottomOpenCriteria = 0.001m,
                    TopOpenCriteria = 0.05m,
                    OpenCriteriaStep = 0.001m,
                    NeverEnterAmbiguousState = false
                });
            ranges.Add("small",
                new OptimizeRange
                {
                    BottomStopLoss = 0.001m,
                    TopStopLoss = 0.04m,
                    StopLossStep = 0.001m,
                    BottomStartProfit = 0.02m,
                    TopStartProfit = 0.2m,
                    StartProfitStep = 0.001m,
                    BottomStopProfit = 0.01m,
                    TopStopProfit = 0.3m,
                    StopProfitStep = 0.01m,
                    BottomOpenCriteria = 0.001m,
                    TopOpenCriteria = 0.06m,
                    OpenCriteriaStep = 0.001m,
                    NeverEnterAmbiguousState = true
                });
            //ranges.Add("热轧卷板",
            //    new OptimizeRange
            //    {
            //        BottomStopLoss = 0.005m,
            //        TopStopLoss = 0.02m,
            //        StopLossStep = 0.001m,
            //        BottomStartProfit = 0.09m,
            //        TopStartProfit = 0.2m,
            //        StartProfitStep = 0.001m,
            //        BottomStopProfit = 0.07m,
            //        TopStopProfit = 0.3m,
            //        StopProfitStep = 0.01m,
            //        BottomOpenCriteria = 0.001m,
            //        TopOpenCriteria = 0.06m,
            //        OpenCriteriaStep = 0.001m,
            //        NeverEnterAmbiguousState = true
            //    });
            //ranges.Add("螺纹钢",
            //    new OptimizeRange
            //    {
            //        BottomStopLoss = 0.005m,
            //        TopStopLoss = 0.025m,
            //        StopLossStep = 0.001m,
            //        BottomStartProfit = 0.11m,
            //        TopStartProfit = 0.17m,
            //        StartProfitStep = 0.001m,
            //        BottomStopProfit = 0.08m,
            //        TopStopProfit = 0.12m,
            //        StopProfitStep = 0.01m,
            //        BottomOpenCriteria = 0.008m,
            //        TopOpenCriteria = 0.02m,
            //        OpenCriteriaStep = 0.001m,
            //        NeverEnterAmbiguousState = false
            //    });
        }

        private string GetTopSettingListKey(decimal startProfit, OptimizeRange range)
        {
            var currentStartProfitLevelTop = Math.Ceiling(startProfit * 100) / 100m;
            var currentStartProfitLevelBottom = currentStartProfitLevelTop - 0.009m;
            if (currentStartProfitLevelTop > range.TopStartProfit)
            {
                currentStartProfitLevelTop = range.TopStartProfit;
            }
            if (currentStartProfitLevelBottom < range.BottomStartProfit)
            {
                currentStartProfitLevelBottom = range.BottomStartProfit;
            }
            var key = $"{currentStartProfitLevelBottom * 1000}_{currentStartProfitLevelTop * 1000}";
            return key;
        }
    }

    class OptimizeRange
    {
        public decimal BottomStopLoss { get; set; }
        public decimal TopStopLoss { get; set; }
        public decimal StopLossStep { get; set; }
        public decimal BottomStartProfit { get; set; }
        public decimal TopStartProfit { get; set; }
        public decimal StartProfitStep { get; set; }
        public decimal BottomStopProfit { get; set; }
        public decimal TopStopProfit { get; set; }
        public decimal StopProfitStep { get; set; }
        public decimal BottomOpenCriteria { get; set; }
        public decimal TopOpenCriteria { get; set; }
        public decimal OpenCriteriaStep { get; set; }
        public bool NeverEnterAmbiguousState { get; set; }
    }

    class SettingResult
    {
        public ReportSettingViewModel Setting { get; set; }
        public decimal Result { get; set; }
    }
}
