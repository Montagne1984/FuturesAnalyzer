using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using FuturesAnalyzer.Models;
using FuturesAnalyzer.Models.States;

namespace FuturesAnalyzer.Services
{
    public class ReportService: IReportService
    {
        public static int WarmUpLength = 6;

        public List<DailyPrice> LoadDailyPrices(string fileName)
        {
            var fileText = File.ReadAllText(fileName);
            var stringReader = new StringReader(fileText);
            var csvReader = new CsvReader(stringReader);
            csvReader.Configuration.HasHeaderRecord = false;
            var dailyPrices = new List<DailyPrice>();
            while (csvReader.Read())
            {
                var dailyPrice = new DailyPrice
                {
                    Date = csvReader.GetField<DateTime>(0),
                    ClosePrice = csvReader.GetField<decimal>(1),
                    OpenPrice = csvReader.GetField<decimal>(2),
                    HighestPrice = csvReader.GetField<decimal>(3),
                    LowestPrice = csvReader.GetField<decimal>(4),
                    Turnover = csvReader.GetField<int>(5)
                };
                bool hitLowPriceFirst;
                if(csvReader.TryGetField(6, out hitLowPriceFirst))
                {
                    dailyPrice.HitLowPriceFirst = hitLowPriceFirst;
                }
                dailyPrices.Add(dailyPrice);
            }
            return dailyPrices.Where(p => p.Turnover > 0).OrderBy(p => p.Date).ToList();
        }

        public virtual IEnumerable<DailyAccountData> GenerateReport(Account account, List<DailyPrice> dailyPrices)
        {
            var report = new List<DailyAccountData>();
            if (dailyPrices.Count <= WarmUpLength)
            {
                return report;
            }
            Transaction firstDayOpenTransaction;
            var startIndex = WarmUp(account, dailyPrices, out firstDayOpenTransaction);
            for (var i = 1; i < dailyPrices.Count; i++)
            {
                if (dailyPrices[i].Date > dailyPrices[i - 1].Date.AddDays(15) || dailyPrices[i].Date == dailyPrices[i - 1].Date)
                {
                    throw new ArgumentException("日期异常：" + dailyPrices[i - 1].Date + " - " + dailyPrices[i].Date);
                }
            }
            for (var i = startIndex; i < dailyPrices.Count; i++)
            {
                var dailyPrice = dailyPrices[i];
                if (!CheckDailyPrice(dailyPrice))
                {
                    throw new ArgumentException("数据异常：" + dailyPrice.Date);
                }
                var dailyAccountData = new DailyAccountData
                {
                    DailyPrice = dailyPrice,
                    CloseTransaction = i == startIndex ? null : account.MarketState.TryClose(dailyPrice),
                    OpenTransaction = i == startIndex ? firstDayOpenTransaction : account.MarketState.TryOpen(dailyPrice),
                    Balance = account.Balance,
                    Contract = account.Contract,
                    InternalProfit = account.MarketState.InternalProfit
                };
                report.Add(dailyAccountData);
                var currentState = account.MarketState;
                currentState.HighestPrice = Math.Max(currentState.HighestPrice, account.NotUseClosePrice && dailyAccountData.OpenTransaction == null ? dailyPrice.HighestPrice : dailyPrice.ClosePrice);
                currentState.LowestPrice = Math.Min(currentState.LowestPrice, account.NotUseClosePrice && dailyAccountData.OpenTransaction == null ? dailyPrice.LowestPrice : dailyPrice.ClosePrice);
                currentState.TopPrice = Math.Max(currentState.TopPrice, account.NotUseClosePrice && dailyAccountData.OpenTransaction == null ? dailyPrice.HighestPrice : dailyPrice.ClosePrice);
                currentState.BottomPrice = Math.Min(currentState.BottomPrice, account.NotUseClosePrice && dailyAccountData.OpenTransaction == null ? dailyPrice.LowestPrice : dailyPrice.ClosePrice);
                currentState.PreviousPrice = dailyPrice;
                account.PreviousFiveDayPrices.Dequeue();
                account.PreviousFiveDayPrices.Enqueue(dailyPrice.ClosePrice);
                account.PreviousFiveDayDirections.Dequeue();
                account.PreviousFiveDayDirections.Enqueue(Math.Sign(dailyPrices[i].ClosePrice - dailyPrices[i - 1].ClosePrice));
                dailyAccountData.NextTransaction = account.MarketState.GetNextTransaction();
            }
            if(report.Count > 1)
            {
                for(var i = 1; i < report.Count; i++)
                {
                    report[i].PercentageBalance = report[i - 1].PercentageBalance;
                    if (report[i - 1].Contract != null && report[i - 1].Contract != report[i].Contract)
                    {
                        report[i].PercentageBalance += (report[i].Balance - report[i - 1].Balance) / report[i - 1].Contract.Price;
                    }
                    if (report[i].Contract != null)
                    {
                        report[i].RealTimePercentageBalance = report[i].PercentageBalance + ((report[i].DailyPrice.ClosePrice - report[i].Contract.Price) * (int)report[i].Contract.Direction + report[i].InternalProfit) / report[i].Contract.Price;
                    }
                    else
                    {
                        report[i].RealTimePercentageBalance = report[i].PercentageBalance + report[i].InternalProfit / report[i - 1].DailyPrice.ClosePrice;
                    }
                }
            }
            if (report.Count > 0)
            {
                if (account.Contract != null)
                {
                    var finalPrice = dailyPrices.Last().ClosePrice;
                    report.Last().Balance += ((finalPrice - account.Contract.Price) * (int)account.Contract.Direction + account.MarketState.InternalProfit) * account.Contract.Unit;
                    report.Last().PercentageBalance += ((finalPrice - account.Contract.Price) * (int)account.Contract.Direction + account.MarketState.InternalProfit) / account.Contract.Price;
                }
                else
                {
                    report.Last().Balance += account.MarketState.InternalProfit;
                    report.Last().PercentageBalance += account.MarketState.InternalProfit / report[report.Count - 2].DailyPrice.ClosePrice;
                }
                report.Last().RealTimePercentageBalance = report.Last().PercentageBalance;
            }
            return report;
        }
        
        private static int WarmUp(Account account, List<DailyPrice> dailyPrices, out Transaction firstDayOpenTransaction)
        {
            var direction = 0;
            for (var i = 1; i < WarmUpLength && i < dailyPrices.Count; i++)
            {
                var d = Math.Sign(dailyPrices[i].ClosePrice - dailyPrices[i - 1].ClosePrice);
                direction += d;
                account.PreviousFiveDayPrices.Enqueue(dailyPrices[i].ClosePrice);
                account.PreviousFiveDayDirections.Enqueue(d);
            }
            if(account.Direction != direction)
                throw new ArgumentException();
            if (direction > 1)
            {
                account.MarketState = account.UseAverageMarketState ? new AverageUpState() : new UpState();
            }
            else if (direction < -1)
            {
                account.MarketState = account.UseAverageMarketState ? new AverageDownState() : new DownState();
            }
            else
            {
                account.MarketState = account.UseAverageMarketState ? new AverageAmbiguousState() : new AmbiguousState();
                var firstDailyPrices = dailyPrices.Take(WarmUpLength);
                account.MarketState.TopPrice = account.NotUseClosePrice ? firstDailyPrices.Max(p => p.HighestPrice) : firstDailyPrices.Max(p => p.ClosePrice);
                account.MarketState.BottomPrice = account.NotUseClosePrice ? firstDailyPrices.Min(p => p.LowestPrice) : firstDailyPrices.Min(p => p.ClosePrice);
            }
            account.MarketState.HighestPrice = dailyPrices[WarmUpLength].OpenPrice;
            account.MarketState.LowestPrice = dailyPrices[WarmUpLength].OpenPrice;
            account.MarketState.StartPrice = dailyPrices[WarmUpLength].OpenPrice;
            account.MarketState.Account = account;
            account.MarketState.PreviousPrice = dailyPrices[WarmUpLength - 1];
            if (account.MarketState is UpState || account.MarketState is DownState)
            {
                firstDayOpenTransaction = account.MarketState.TryOpen(dailyPrices[WarmUpLength]);
            }
            else
            {
                firstDayOpenTransaction = null;
            }
            return WarmUpLength;
        }

        protected bool CheckDailyPrice(DailyPrice dailyPrice)
        {
            return dailyPrice.ClosePrice > 0 && dailyPrice.OpenPrice > 0 && dailyPrice.HighestPrice > 0 &&
                   dailyPrice.LowestPrice > 0 
                   && dailyPrice.ClosePrice >= dailyPrice.LowestPrice && dailyPrice.ClosePrice <= dailyPrice.HighestPrice
                   && dailyPrice.OpenPrice >= dailyPrice.LowestPrice && dailyPrice.OpenPrice <= dailyPrice.HighestPrice;
        }

        public decimal GetMaxLossRange(IEnumerable<DailyAccountData> report)
        {
            decimal max = 0;
            DateTime maxDate = report.First().DailyPrice.Date;
            DateTime maxLossRangeStartDate;
            DateTime maxLossRangeEndDate;
            decimal maxLossRange = 0;
            foreach(var dailyData in report)
            {
                if (dailyData.PercentageBalance > max)
                {
                    max = dailyData.PercentageBalance;
                    maxDate = dailyData.DailyPrice.Date;
                }
                else if (max - dailyData.PercentageBalance > maxLossRange)
                {
                    maxLossRangeStartDate = maxDate;
                    maxLossRangeEndDate = dailyData.DailyPrice.Date;
                    maxLossRange = max - dailyData.PercentageBalance;
                }
            }
            return maxLossRange;
        }

        public decimal GetBestBudgetChangeFactor(IEnumerable<DailyAccountData> report, int bondPercentage)
        {
            var bestResult = 0m;
            var bestFactor = 1m;
            for (var i = 100; i < 300; i++)
            {
                var factor = i / 100m;
                var result = GetReportWithBudgetFactor(report.ToArray(), factor, bondPercentage).Last().PercentageBalance;
                if (result > bestResult)
                {
                    bestResult = result;
                    bestFactor = factor;
                }
            }
            return bestFactor;
        }

        public IEnumerable<DailyAccountData> GetReportWithBudgetFactor(DailyAccountData[] report, decimal budgetChangeFactor, int bondPercentage)
        {
            var budgetFactor = 1m;
            var budgetFactorLevel = 0m;
            //var profitFactor = 100m / bondPercentage;
            var bondPercentageValue = bondPercentage / 100m;
            decimal percentageBalance = 0m;
            var newReport = new DailyAccountData[report.Length];
            for (var index = 0; index < report.Length; index++)
            {
                if (index > 0)
                {
                    var percentageDelta = report[index].PercentageBalance - report[index - 1].PercentageBalance;
                    percentageDelta *= budgetFactor;
                    percentageBalance += percentageDelta;
                    if (percentageDelta > 0)
                    {
                        if ((percentageBalance + bondPercentageValue) * 0.8m >= bondPercentageValue * budgetFactor * budgetChangeFactor)
                        {
                            if (budgetChangeFactor != 1)
                            {
                                budgetFactorLevel++;
                            }
                            budgetFactor *= budgetChangeFactor;
                        }
                    }
                    else
                    {
                        while ((percentageBalance + bondPercentageValue) * 0.8m < bondPercentageValue * budgetFactor && budgetFactor > 1)
                        {
                            if (budgetChangeFactor != 1)
                            {
                                budgetFactorLevel--;
                            }
                            budgetFactor /= budgetChangeFactor;
                        }
                    }
                }
                else if (index == report.Length - 1)
                {
                    var percentageDelta = report[index].PercentageBalance - report[index - 1].PercentageBalance;
                    percentageDelta *= budgetFactor;
                    percentageBalance += percentageDelta;
                }
                newReport[index] = report[index].Clone();
                newReport[index].PercentageBalance = percentageBalance;
                newReport[index].RealTimePercentageBalance = percentageBalance + (report[index].RealTimePercentageBalance - report[index].PercentageBalance) * budgetFactor;
            }
            return newReport;
        }
    }
}