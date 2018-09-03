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

        public decimal StopLossCriteria { get; set; } = 0.01m;
        public decimal StartProfitCriteria { get; set; } = 0.02m;
        public decimal StopProfitCriteria { get; set; } = 0.2m;
        public decimal StartProfitCriteriaForMultiUnits { get; set; } = 0.01m;
        public int StopLossUnit { get; set; } = 1;
        public bool NeverEnterAmbiguousState { get; set; } = false;
        public int AppendUnitCountAfterProfitStart { get; set; } = 0;
        public decimal MinimumPriceUnit { get; set; } = 1;
        public decimal OpenCriteria { get; set; } = 0.02m;
        public bool FollowTrend { get; set; } = true;
        public decimal BudgetFactor { get; set; } = 1;
        public bool NotUseClosePrice { get; set; } = false;
        public bool UseAverageMarketState { get; set; } = false;
        public bool CloseAfterProfit { get; set; } = false;
        public bool OnlyUseClosePrice { get; set; } = false;
        public bool UseCrossStarStrategy { get; set; } = false;
        public bool UseInternalProfit { get; set; } = false;
        public bool CloseAmbiguousStateToday { get; set; } = false;
        public bool NeverReverse { get; set; } = false;
        public bool BreakThroughStratgy { get; set; } = false;

        public decimal DeductTransactionFee(decimal price, int unit = 1)
        {
            var transactionFee = price * TransactionFeeRate * unit;
            Balance -= Math.Round(transactionFee, 2);
            return transactionFee;
        }
    }
}
