using System;
using System.Collections.Generic;
using System.Linq;
using FuturesAnalyzer.Models.States;

namespace FuturesAnalyzer.Models
{
    public class Account
    {
        public decimal Balance { get; set; }
        public Contract Contract { get; set; }
        public MarketState MarketState { get; set; }
        public bool? IsLastTransactionLoss { get; set; }
        public decimal TransactionFeeRate { get; set; }
        public int HitBothCriteriaInAmbiguousStateCount { get; set; } = 0;
        public Queue<decimal> PreviousFiveDayPrices = new Queue<decimal>();
        public Queue<int> PreviousFiveDayDirections = new Queue<int>(); 

        public decimal FiveDaysAveragePrice
        {
            get { return PreviousFiveDayPrices.Average(p => p); }
        }

        public int Direction
        {
            get { return PreviousFiveDayDirections.Sum(p => p); }
        }

        public decimal StopLossCriteria = 0.01m;
        public decimal StartProfitCriteria = 0.02m;
        public decimal StopProfitCriteria = 0.2m;
        public decimal StartProfitCriteriaForMultiUnits = 0.01m;
        public int StopLossUnit = 1;
        public bool NeverEnterAmbiguousState = false;
        public int AppendUnitCountAfterProfitStart = 0;
        public decimal MinimumPriceUnit = 1;
        public decimal OpenCriteria = 0.02m;
        public bool FollowTrend = true;
        public bool NotUseClosePrice = false;
        public bool UseAverageMarketState = false;
        public bool CloseAfterProfit = false;
        public bool OnlyUseClosePrice = false;
        public bool UseCrossStarStrategy = false;
        public bool UseInternalProfit = false;

        public decimal DeductTransactionFee(decimal price, int unit = 1)
        {
            var transactionFee = price * TransactionFeeRate * unit;
            Balance -= Math.Round(transactionFee, 2);
            return transactionFee;
        }
    }
}
