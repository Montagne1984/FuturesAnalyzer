using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FuturesAnalyzer.Models;

namespace FuturesAnalyzer.Services
{
    public class CrossStarReportService: ReportService
    {
        public override IEnumerable<DailyAccountData> GenerateReport(Account account, List<DailyPrice> dailyPrices)
        {
            var report = new List<DailyAccountData>();
            var previousPrice = dailyPrices[0].ClosePrice;
            for (var i = 1; i < dailyPrices.Count; i++)
            {
                var dailyPrice = dailyPrices[i];
                if (!CheckDailyPrice(dailyPrice))
                {
                    throw new ArgumentException("数据异常：" + dailyPrice.Date);
                }
                var ceilingOpenPrice = Math.Ceiling(previousPrice * (1 + account.OpenCriteria) / account.MinimumPriceUnit) * account.MinimumPriceUnit;
                var floorOpenPrice = Math.Floor(previousPrice * (1 - account.OpenCriteria) / account.MinimumPriceUnit) * account.MinimumPriceUnit;

                decimal percentageBalanceDelta = 0;
                if (account.Contract != null)
                {
                    var profit = (dailyPrice.OpenPrice - account.Contract.Price)*(int) account.Contract.Direction -
                                 dailyPrice.OpenPrice*account.TransactionFeeRate;
                    account.Balance += profit;
                    percentageBalanceDelta += profit / account.Contract.Price;
                    account.Contract = null;
                }
                if (dailyPrice.HighestPrice >= ceilingOpenPrice && dailyPrice.LowestPrice <= floorOpenPrice)
                {
                    var profit = ceilingOpenPrice - floorOpenPrice -
                                       (ceilingOpenPrice + floorOpenPrice) * account.TransactionFeeRate * 2;
                    account.Balance += profit;
                    percentageBalanceDelta += profit/previousPrice;
                }
                else if (dailyPrice.HighestPrice >= ceilingOpenPrice)
                {
                    account.Balance -= ceilingOpenPrice * account.TransactionFeeRate;
                    account.Contract = new Contract
                    {
                        Price = ceilingOpenPrice,
                        Direction = Direction.Sell
                    };
                    percentageBalanceDelta -= account.TransactionFeeRate;
                }
                else if (dailyPrice.LowestPrice <= floorOpenPrice)
                {
                    account.Balance -= floorOpenPrice * account.TransactionFeeRate;
                    account.Contract = new Contract
                    {
                        Price = floorOpenPrice,
                        Direction = Direction.Buy
                    };
                    percentageBalanceDelta -= account.TransactionFeeRate;
                }
                var dailyAccountData = new DailyAccountData
                {
                    DailyPrice = dailyPrice,
                    Balance = account.Balance,
                    Contract = account.Contract,
                    PercentageBalance = report.Any() ? report.Last().PercentageBalance + percentageBalanceDelta : percentageBalanceDelta
                };
                report.Add(dailyAccountData);
                previousPrice = dailyPrice.ClosePrice;
            }
            return report;
        }
    }
}
