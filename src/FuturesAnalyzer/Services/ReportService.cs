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
            var dailyPrices = new List<DailyPrice>();
            while (csvReader.Read())
            {
                var dailyPrice = new DailyPrice
                {
                    Date = csvReader.GetField<DateTime>(0),
                    AveragePrice = csvReader.GetField<decimal>(1),
                    OpenPrice = csvReader.GetField<decimal>(2),
                    HighestPrice = csvReader.GetField<decimal>(3),
                    LowesetPrice = csvReader.GetField<decimal>(4)
                };
                dailyPrices.Add(dailyPrice);
            }
            return dailyPrices.OrderBy(p => p.Date).ToList();
        }

        public IEnumerable<DailyAccountData> GenerateReport(Account account, List<DailyPrice> dailyPrices)
        {
            var report = new List<DailyAccountData>();
            if (dailyPrices.Count <= WarmUpLength)
            {
                return report;
            }
            WarmUp(account, dailyPrices);
            for (var i = 1; i < dailyPrices.Count; i++)
            {
                if (dailyPrices[i].Date > dailyPrices[i - 1].Date.AddDays(15) || dailyPrices[i].Date == dailyPrices[i - 1].Date)
                {
                    throw new ArgumentException("日期异常：" + dailyPrices[i - 1].Date + " - " + dailyPrices[i].Date);
                }
            }
            for (var i = WarmUpLength; i < dailyPrices.Count; i++)
            {
                var dailyPrice = dailyPrices[i];
                if (!CheckDailyPrice(dailyPrice))
                {
                    throw new ArgumentException("数据异常：" + dailyPrice.Date);
                }
                report.Add(new DailyAccountData
                {
                    Date = dailyPrice.Date,
                    CloseTransaction = account.MarketState.TryClose(dailyPrice),
                    OpenTransaction = account.MarketState.TryOpen(dailyPrice),
                    Balance = account.Balance,
                    Contract = account.Contract
                });
                var currentState = account.MarketState;
                currentState.HighestPrice = Math.Max(currentState.HighestPrice, dailyPrice.AveragePrice);
                currentState.LowestPrice = Math.Min(currentState.LowestPrice, dailyPrice.AveragePrice);
                currentState.PreviousPrice = dailyPrice.AveragePrice;
            }
            return report;
        }

        private static void WarmUp(Account account, List<DailyPrice> dailyPrices)
        {
            var direction = 0;
            for (var i = 1; i < WarmUpLength && i < dailyPrices.Count; i++)
            {
                direction += Math.Sign(dailyPrices[i].AveragePrice - dailyPrices[i - 1].AveragePrice);
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
            account.MarketState.HighestPrice = dailyPrices[WarmUpLength - 1].AveragePrice;
            account.MarketState.LowestPrice = dailyPrices[WarmUpLength - 1].AveragePrice;
            account.MarketState.StartPrice = dailyPrices[WarmUpLength - 1].AveragePrice;
            account.MarketState.Account = account;
            account.MarketState.PreviousPrice = dailyPrices[WarmUpLength - 1].AveragePrice;
        }

        private bool CheckDailyPrice(DailyPrice dailyPrice)
        {
            return dailyPrice.AveragePrice > 0 && dailyPrice.OpenPrice > 0 && dailyPrice.HighestPrice > 0 &&
                   dailyPrice.LowesetPrice > 0 
                   && dailyPrice.AveragePrice >= dailyPrice.LowesetPrice && dailyPrice.AveragePrice <= dailyPrice.HighestPrice
                   && dailyPrice.OpenPrice >= dailyPrice.LowesetPrice && dailyPrice.OpenPrice <= dailyPrice.HighestPrice;
        }
    }
}