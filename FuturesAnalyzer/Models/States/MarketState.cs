using System;

namespace FuturesAnalyzer.Models.States
{
    public abstract class MarketState
    {

        public Account Account { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal StartPrice { get; set; }
        public DailyPrice PreviousPrice { get; set; }
        public bool StopInternalProfit { get; set; }
        public bool New { get; set; }
        public decimal InternalProfit { get; set; }

        public virtual bool CloseWithinStartProfitPrice(DailyPrice dailyPrice) => false;
        public virtual bool HitStartProfitPrice(DailyPrice dailyPrice) => false;

        public abstract Transaction TryClose(DailyPrice dailyPrice);

        protected abstract void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice);

        protected virtual MarketState GetNewState(decimal closePrice)
        {
            return null;
        }

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