using System;

namespace FuturesAnalyzer.Models.States
{
    public abstract class MarketState
    {

        public Account Account { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal StartPrice { get; set; }
        public decimal? PreviousPrice { get; set; }

        public abstract Transaction TryClose(DailyPrice dailyPrice);

        protected abstract void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice);

        public abstract Transaction TryOpen(DailyPrice dailyPrice);

        public abstract decimal GetStopProfitPrice();
        public abstract decimal GetStopLossPrice();

        protected virtual decimal Ceiling(decimal price)
        {
            return Math.Ceiling(price/ Account.MinimumPriceUnit)* Account.MinimumPriceUnit;
        }

        protected virtual decimal Floor(decimal price)
        {
            return Math.Floor(price/ Account.MinimumPriceUnit)* Account.MinimumPriceUnit;
        }

        public abstract string GetNextTransaction();
    }
}