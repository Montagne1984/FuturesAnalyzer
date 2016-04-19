namespace FuturesAnalyzer.Models.States
{
    public abstract class MarketState
    {
        public static decimal StopLossCriteria = 0.01m;
        public static decimal StartProfitCriteria = 0.02m;
        public static decimal StopProfitCriteria = 0.2m;
        public static decimal StartProfitCriteriaForMultiUnits = 0.01m;
        public static int StopLossUnit = 2;
        public static bool NeverEnterAmbiguousState = false;

        public Account Account { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal StartPrice { get; set; }
        public decimal? PreviousPrice { get; set; }

        public abstract Transaction TryClose(DailyPrice dailyPrice);

        protected abstract void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice);

        public abstract Transaction TryOpen(DailyPrice dailyPrice);

        protected abstract decimal GetStopProfitPrice();
        protected abstract decimal GetStopLossPrice();
    }
}