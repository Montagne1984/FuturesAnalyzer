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

        public IEnumerable<DailyAccountData> GenerateReport(Account account, List<DailyPrice> dailyPrices)
        {
            var report = new List<DailyAccountData>();
            if (dailyPrices.Count <= WarmUpLength)
            {
                return report;
            }
            var startIndex = WarmUp(account, dailyPrices);
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
                    CloseTransaction = account.MarketState.TryClose(dailyPrice),
                    OpenTransaction = account.MarketState.TryOpen(dailyPrice),
                    Balance = account.Balance,
                    Contract = account.Contract
                };
                report.Add(dailyAccountData);
                var currentState = account.MarketState;
                currentState.HighestPrice = Math.Max(currentState.HighestPrice, account.NotUseClosePrice && dailyAccountData.OpenTransaction == null ? dailyPrice.HighestPrice : dailyPrice.ClosePrice);
                currentState.LowestPrice = Math.Min(currentState.LowestPrice, account.NotUseClosePrice && dailyAccountData.OpenTransaction == null ? dailyPrice.LowestPrice : dailyPrice.ClosePrice);
                currentState.PreviousPrice = dailyPrice.ClosePrice;
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
                }
            }
            if (report.Count > 0 && account.Contract != null)
            {
                var finalPrice = dailyPrices.Last().ClosePrice;
                report.Last().Balance += (finalPrice - account.Contract.Price) *
                                        (int)account.Contract.Direction * account.Contract.Unit;
                report.Last().PercentageBalance += (finalPrice - account.Contract.Price) *
                                        (int)account.Contract.Direction / account.Contract.Price;
            }
            return report;
        }

        private static int WarmUp(Account account, List<DailyPrice> dailyPrices)
        {
            var direction = 0;
            for (var i = 1; i < WarmUpLength && i < dailyPrices.Count; i++)
            {
                direction += Math.Sign(dailyPrices[i].ClosePrice - dailyPrices[i - 1].ClosePrice);
            }
            if (direction > 1)
            {
                account.MarketState = new UpState();
            }
            else if (direction < -1)
            {
                account.MarketState = new DownState();
            }
            else
            {
                account.MarketState = new AmbiguousState();
            }
            account.MarketState.HighestPrice = dailyPrices[WarmUpLength].OpenPrice;
            account.MarketState.LowestPrice = dailyPrices[WarmUpLength].OpenPrice;
            account.MarketState.StartPrice = dailyPrices[WarmUpLength].OpenPrice;
            account.MarketState.Account = account;
            account.MarketState.PreviousPrice = dailyPrices[WarmUpLength - 1].ClosePrice;
            if (account.MarketState is UpState || account.MarketState is DownState)
            {
                account.MarketState.TryOpen(dailyPrices[WarmUpLength]);
            }
            return WarmUpLength;
        }

        private bool CheckDailyPrice(DailyPrice dailyPrice)
        {
            return dailyPrice.ClosePrice > 0 && dailyPrice.OpenPrice > 0 && dailyPrice.HighestPrice > 0 &&
                   dailyPrice.LowestPrice > 0 
                   && dailyPrice.ClosePrice >= dailyPrice.LowestPrice && dailyPrice.ClosePrice <= dailyPrice.HighestPrice
                   && dailyPrice.OpenPrice >= dailyPrice.LowestPrice && dailyPrice.OpenPrice <= dailyPrice.HighestPrice;
        }
    }
}