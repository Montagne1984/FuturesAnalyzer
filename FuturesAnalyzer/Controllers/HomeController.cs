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

        public JsonResult Replay(FileInfo[] fileInfos, DateTime startDate, DateTime endDate, DateTime replayStartDate, bool getBestChangeDates = false, bool passive = false)
        {
            var topOfTopSettings = new SortedList<decimal, List<SettingResult>>();
            var topSettingsArray = new SortedList<decimal, List<SettingResult>>[fileInfos.Length];
            var everyDayBest = new SortedList<DateTime, SortedList<decimal, List<SettingResult>>>();
            var taskList = new List<Task>();
            for (var i = 0; i < fileInfos.Length; i++)
            {
                var fileName = fileInfos[i].FileName;
                var settings = fileInfos[i].Settings;
                var transactionFeeRate = fileInfos[i].TransactionFeeRate;
                var minimumPriceUnit = fileInfos[i].MinimumPriceUnit;
                try
                {
                    var fileDateIndex = fileName.IndexOf("20");
                    var productName = fileName.Substring(0, fileDateIndex);
                    var dailyPrices = _reportService.LoadDailyPrices("Data/" + productName + ".csv");
                    var index = fileName.IndexOf("20") + 8;
                    var notUseClosePrice = fileName[index] == 'T';
                    index += notUseClosePrice ? 4 : 5;
                    var onlyUseClosePrice = fileName[index] == 'T';
                    index += onlyUseClosePrice ? 4 : 5;
                    var closeAmbiguousStateToday = fileName[index] == 'T';

                    var topSettings = new SortedList<decimal, List<SettingResult>>();
                    topSettingsArray[i] = topSettings;
                    var number = settings.Split("\n").Length;
                    using (var textReader = new StringReader(settings))
                    using (var csvReader = new CsvReader(textReader))
                    {
                        csvReader.Configuration.HasHeaderRecord = true;
                        csvReader.Read();
                        while (csvReader.Read())
                        {
                            var stopLossCriteria = csvReader.GetField<decimal>(0);
                            var stopProfitCriteria = csvReader.GetField<decimal>(1);
                            var startProfitCriteria = csvReader.GetField<decimal>(2);
                            var openCriteria = csvReader.GetField<decimal>(3);
                            var followTrend = csvReader.GetField<bool>(4);
                            taskList.Add(new Task(() => {
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
                                        StopLossCriteria = stopLossCriteria,
                                        StopProfitCriteria = stopProfitCriteria,
                                        StartProfitCriteria = startProfitCriteria,
                                        OpenCriteria = openCriteria,
                                        FollowTrend = followTrend
                                    }
                                };
                                var report = GetReport(setting.Setting, dailyPrices);
                                if (getBestChangeDates)
                                {
                                    for (var reportIndex = 250; reportIndex < report.Count(); reportIndex++)
                                    {
                                        var reportDay = report.ElementAt(reportIndex);
                                        var reportDate = reportDay.DailyPrice.Date;
                                        lock (everyDayBest)
                                        {
                                            if (!everyDayBest.ContainsKey(reportDate))
                                            {
                                                everyDayBest.Add(reportDate, new SortedList<decimal, List<SettingResult>>());
                                            }
                                            var reportDateBest = everyDayBest[reportDate];
                                            var cloneSetting = new SettingResult
                                            {
                                                Setting = setting.Setting,
                                                Result = reportDay.RealTimePercentageBalance
                                            };
                                            UpdateTopThreeSettings(ref reportDateBest, cloneSetting, number);
                                        }
                                    }
                                }
                                setting.Result = report.Last().RealTimePercentageBalance;
                                UpdateTopThreeSettings(ref topSettings, setting, number);
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                    return Json(
                        new
                        {
                            ex.Message
                        });
                }
            }

            Parallel.For(0, taskList.Count, (i) =>
            {
                taskList[i].Start();
            });
            Task.WaitAll(taskList.ToArray());

            for (var i = 0; i < fileInfos.Length; i++)
            {
                var fileName = fileInfos[i].FileName;
                var settings = fileInfos[i].Settings;
                var transactionFeeRate = fileInfos[i].TransactionFeeRate;
                var minimumPriceUnit = fileInfos[i].MinimumPriceUnit;
                var topSettings = topSettingsArray[i];
                var fileDateIndex = fileName.IndexOf("20");
                var productName = fileName.Substring(0, fileDateIndex);
                if (!getBestChangeDates)
                {
                    var resultFileName = $"{endDate.ToString("yyyyMMdd")}_{Math.Round(topSettings.Last().Value.First().Result, 4)}_{Path.GetFileNameWithoutExtension(fileName)}.csv";
                    if (fileName.Contains("Detail") || fileName.Contains("Summary"))
                    {
                        var parts = fileName.Split("_");
                        var category = parts[0].Substring(productName.Length + 8);
                        resultFileName = $"{endDate.ToString("yyyyMMdd")}_{category}_{Math.Round(topSettings.Last().Value.First().Result, 4)}_{parts[1]}_{parts[2]}_{productName}_{(fileName.Contains("Summary") ? "Summary" : string.Empty)}.csv";
                    }
                    using (var fileStream = new FileStream($"Results\\{productName}\\{resultFileName}", FileMode.Create))
                    using (var streamWriter = new StreamWriter(fileStream))
                    {
                        streamWriter.WriteLine("StopLoss,StopProfit,StartProfit,OpenCriteria,FollowTrend,Result");
                        for (var j = topSettings.Count - 1; j >= 0; j--)
                        {
                            foreach (var s in topSettings.ElementAt(j).Value)
                            {
                                streamWriter.WriteLine(
                                    $"{s.Setting.StopLossCriteria},{s.Setting.StopProfitCriteria},{s.Setting.StartProfitCriteria},{s.Setting.OpenCriteria},{s.Setting.FollowTrend},{s.Result}");
                            }
                        }
                    }
                }

                foreach (var topSetting in topSettings.Last().Value)
                {
                    UpdateTopThreeSettings(ref topOfTopSettings, topSetting, fileInfos.Length);
                }
            }

            var bestSettings = topOfTopSettings.Last().Value.First().Setting;
            var bestDailyPrices = _reportService.LoadDailyPrices("Data/" + bestSettings.SelectedProductName + ".csv").Where(d => d.Date >= startDate && d.Date <= endDate).ToList();

            if (getBestChangeDates && bestDailyPrices.First().Date <= replayStartDate)
            {
                var settingResults = new Dictionary<DateTime, SettingResult>();
                var startIndex = 5;
                while (everyDayBest.ElementAt(startIndex).Key < replayStartDate)
                {
                    startIndex++;
                }
                for (var ii = startIndex; ii < everyDayBest.Count; ii++)
                {
                    var currentDayBestResult = everyDayBest.ElementAt(ii).Value.Last().Key;
                    var currentDayBestSetting = everyDayBest.ElementAt(ii).Value[currentDayBestResult];
                    everyDayBest.ElementAt(ii).Value[currentDayBestResult] = 
                        currentDayBestSetting
                        .OrderBy(s => s.Setting.StartProfitCriteria)
                        .OrderBy(s => s.Setting.StopLossCriteria)
                        .OrderByDescending(s => s.Setting.CloseAmbiguousStateToday)
                        .OrderBy(s => s.Setting.OpenCriteria)
                        .OrderBy(s => s.Setting.StopProfitCriteria)
                        .ToList();
                }
                var previousDayBest = everyDayBest.ElementAt(startIndex).Value.Last().Value[0];
                var productName = previousDayBest.Setting.SelectedProductName;
                settingResults.Add(everyDayBest.ElementAt(startIndex).Key, previousDayBest);
                for (var dayIndex = startIndex + 1; dayIndex < everyDayBest.Count; dayIndex++)
                {
                    var thisDayBest = everyDayBest.ElementAt(dayIndex).Value.Last().Value[0];
                    if (previousDayBest.Setting != thisDayBest.Setting)
                    {
                        thisDayBest.Result = everyDayBest.ElementAt(dayIndex).Value.Last().Key;
                        settingResults.Add(everyDayBest.ElementAt(dayIndex).Key, thisDayBest);
                        previousDayBest = thisDayBest;
                    }
                }

                var content = new List<string>();
                var resultFileName = GenerateChangeReport(fileInfos, endDate, replayStartDate, bestDailyPrices, settingResults, content, passive);
                
                using (var fileStream = new FileStream($"Results\\{productName}\\{resultFileName}", FileMode.Create))
                using (var streamWriter = new StreamWriter(fileStream))
                {
                    streamWriter.WriteLine("Date,StopLoss,StopProfit,StartProfit,OpenCriteria,FollowTrend,NotUseClosePrice,OnlyUseClosePrice,CloseAmbiguousStateToday,Result,PreviousResult,Delta");
                    foreach(var c in content)
                    {
                        streamWriter.WriteLine(c);
                    }
                }
            }

            return Json(
                new
                {
                    BestSettings = bestSettings,
                    Report = GetReport(bestSettings, bestDailyPrices)
                });
        }

        private string GenerateChangeReport(FileInfo[] fileInfos, DateTime endDate, DateTime replayStartDate, List<DailyPrice> dailyPrices, Dictionary<DateTime, SettingResult> settingResults, List<string> content, bool passive)
        {
            string resultFileName = null;
            IEnumerable<DailyAccountData> previousDayBestReport = null;
            if (passive)
            {
                var currentSetting = settingResults.First().Value;
                IEnumerable<DailyAccountData> targetReport = null;
                IEnumerable<DailyAccountData> currentReport = null;
                decimal totalDelta = 0m;

                for (var i = 0; i < settingResults.Count; i++)
                {
                    var settingResult = settingResults.ElementAt(i);
                    var s = settingResult.Value;
                    if (currentReport == null)
                    {
                        currentSetting = s;
                        currentReport = GetReport(s.Setting, dailyPrices);
                        content.Add(
                            $"{settingResult.Key.ToString("MM/dd/yy")},{s.Setting.StopLossCriteria},{s.Setting.StopProfitCriteria},{s.Setting.StartProfitCriteria},{s.Setting.OpenCriteria},{s.Setting.FollowTrend},{s.Setting.NotUseClosePrice},{s.Setting.OnlyUseClosePrice},{s.Setting.CloseAmbiguousStateToday},{s.Result},{s.Result},0");
                        continue;
                    }
                    if (settingResult.Value == currentSetting)
                    {
                        continue;
                    }

                    var currentDate = settingResult.Key;
                    DateTime nextChangeDate;
                    if (i < settingResults.Count - 1)
                    {
                        nextChangeDate = settingResults.ElementAt(i + 1).Key;
                    }
                    else
                    {
                        nextChangeDate = dailyPrices.Last().Date;
                    }

                    var currentDateIndex = dailyPrices.IndexOf(dailyPrices.First(d => d.Date == currentDate));
                    targetReport = GetReport(settingResult.Value.Setting, dailyPrices);
                    var currentDailyAccountData = currentReport.First(r => r.DailyPrice.Date == currentDate);
                    var targetDailyAccountData = targetReport.First(r => r.DailyPrice.Date == currentDate);
                    for (var j = currentDateIndex + 1; j < dailyPrices.Count && dailyPrices[j].Date <= nextChangeDate; j++)
                    {
                        var currentDailyPrice = dailyPrices[j];
                        var currentContractResult = GetCurrentContractResult(currentDailyPrice, currentDailyAccountData, currentSetting.Setting, targetDailyAccountData, settingResult.Value.Setting);
                        if (currentContractResult.HasValue)
                        {
                            var delta = currentContractResult.Value - s.Result;
                            totalDelta += delta;
                            var targetResult = targetReport.First(r => r.DailyPrice.Date == currentDailyPrice.Date).PercentageBalance;
                            content.Add(
                                $"{currentDailyPrice.Date.ToString("MM/dd/yy")},{s.Setting.StopLossCriteria},{s.Setting.StopProfitCriteria},{s.Setting.StartProfitCriteria},{s.Setting.OpenCriteria},{s.Setting.FollowTrend},{s.Setting.NotUseClosePrice},{s.Setting.OnlyUseClosePrice},{s.Setting.CloseAmbiguousStateToday},{targetResult},{currentContractResult},{delta}");
                            currentReport = targetReport;
                            currentSetting = settingResult.Value;
                            break;
                        }
                    }
                }
                var lastSetting = currentSetting.Setting;
                var lastResult = currentReport.Last().RealTimePercentageBalance;
                content.Add(
                    $"{endDate.ToString("MM/dd/yy")},{lastSetting.StopLossCriteria},{lastSetting.StopProfitCriteria},{lastSetting.StartProfitCriteria},{lastSetting.OpenCriteria},{lastSetting.FollowTrend},{lastSetting.NotUseClosePrice},{lastSetting.OnlyUseClosePrice},{lastSetting.CloseAmbiguousStateToday},{lastResult},{lastResult},0");
                var totalGain = currentReport.Last().RealTimePercentageBalance - settingResults.First().Value.Result;
                var finalResult = totalGain + totalDelta;
                resultFileName = $"PassiveChange_{DateTime.Now.ToString("yyMMddHHmm")}_{Math.Round(finalResult, 4)}_{Math.Round(totalGain, 4)}_{Math.Round(totalDelta, 4)}_{replayStartDate.ToString("yyMMdd")}_{Path.GetFileNameWithoutExtension(fileInfos[0].FileName)}_{fileInfos.Length}.csv";
            }
            else
            {
                decimal totalLoss = 0;
                decimal previousDayBestResultToday = 0;
                foreach (var settingResult in settingResults)
                {
                    var date = settingResult.Key;
                    var s = settingResult.Value;
                    if (previousDayBestReport != null)
                    {
                        previousDayBestResultToday = previousDayBestReport.First(r => r.DailyPrice.Date == date).RealTimePercentageBalance;
                    }
                    else
                    {
                        previousDayBestResultToday = s.Result;
                    }
                    var loss = previousDayBestResultToday - s.Result;
                    totalLoss += loss;
                    content.Add(
                        $"{date.ToString("MM/dd/yy")},{s.Setting.StopLossCriteria},{s.Setting.StopProfitCriteria},{s.Setting.StartProfitCriteria},{s.Setting.OpenCriteria},{s.Setting.FollowTrend},{s.Setting.NotUseClosePrice},{s.Setting.OnlyUseClosePrice},{s.Setting.CloseAmbiguousStateToday},{s.Result},{previousDayBestResultToday},{loss}");
                    previousDayBestReport = GetReport(s.Setting, dailyPrices);
                }
                var lastSetting = settingResults.Last().Value.Setting;
                var lastResult = GetReport(lastSetting, dailyPrices).Last().RealTimePercentageBalance;
                content.Add(
                    $"{endDate.ToString("MM/dd/yy")},{lastSetting.StopLossCriteria},{lastSetting.StopProfitCriteria},{lastSetting.StartProfitCriteria},{lastSetting.OpenCriteria},{lastSetting.FollowTrend},{lastSetting.NotUseClosePrice},{lastSetting.OnlyUseClosePrice},{lastSetting.CloseAmbiguousStateToday},{lastResult},{lastResult},0");

                var totalGain = lastResult - settingResults.First().Value.Result;
                var finalResult = totalGain + totalLoss;
                resultFileName = $"Change_{DateTime.Now.ToString("yyMMddHHmm")}_{Math.Round(finalResult, 4)}_{Math.Round(totalGain, 4)}_{Math.Round(totalLoss, 4)}_{replayStartDate.ToString("yyMMdd")}_{Path.GetFileNameWithoutExtension(fileInfos[0].FileName)}_{fileInfos.Length}.csv";
            }
            return resultFileName;
        }

        private decimal? GetCurrentContractResult(DailyPrice dailyPrice, DailyAccountData currentDailyAccountData, ReportSettingViewModel currentSetting, DailyAccountData targetDailyAccountData, ReportSettingViewModel targetSetting)
        {
            if (currentDailyAccountData.Contract == null)
            {
                return 0;
            }
            else if (currentDailyAccountData.Contract.Direction == Direction.Buy)
            {
                return 1;
            }
            else
            {
                return -1;
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
                            var percentageBalance = result.Last().RealTimePercentageBalance;
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
                                    var percentageBalance = result.Last().RealTimePercentageBalance;
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
                if (!topSettings.Any())
                {
                    continue;
                }
                using (var fileStream = new FileStream($"Results\\{model.SelectedProductName}\\Details\\{model.SelectedProductName}{dailyPrices.Last().Date.ToString("yyyyMMdd")}{model.NotUseClosePrice}{model.OnlyUseClosePrice}{model.CloseAmbiguousStateToday}_{key}_Detail.csv", FileMode.Create))
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

            var filePath = $"Results\\{model.SelectedProductName}\\Summary\\{model.SelectedProductName}{dailyPrices.Last().Date.ToString("yyyyMMdd")}{model.NotUseClosePrice}{model.OnlyUseClosePrice}{model.CloseAmbiguousStateToday}_{range.BottomStartProfit * 1000}_{range.TopStartProfit * 1000}.csv";
            using (var fileStream = new FileStream(filePath, FileMode.Create))
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
            System.IO.File.Copy(filePath, $"Results\\{model.SelectedProductName}\\Details\\{model.SelectedProductName}{dailyPrices.Last().Date.ToString("yyyyMMdd")}{model.NotUseClosePrice}{model.OnlyUseClosePrice}{model.CloseAmbiguousStateToday}_{range.BottomStartProfit * 1000}_{range.TopStartProfit * 1000}_Summary.csv");

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
                    BottomStartProfit = 0.081m,
                    TopStartProfit = 0.16m,
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

    public class FileInfo
    {
        public string FileName { get; set; }
        public string Settings { get; set; }
        public decimal TransactionFeeRate { get; set; }
        public decimal MinimumPriceUnit { get; set; }
    }
}
