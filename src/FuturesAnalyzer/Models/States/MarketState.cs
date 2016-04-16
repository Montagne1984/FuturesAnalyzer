namespace FuturesAnalyzer.Models.States
{
    public abstract class MarketState
    {
        public static decimal StopLossCriteria = 0.01m;
        public static decimal StartProfitCriteria = 0.02m;
        public static decimal StopProfitCriteria = 0.2m;

        public Account Account { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal StartPrice { get; set; }

        public Transaction TryClose(DailyPrice dailyPrice)
        {
            if (Account.Contract == null)
            {
                return null;
            }

            var floorClosePrice = GetFloorClosePrice();
            var ceilingClosePrice = GetCeilingClosePrice();

            var closePrice = 0m;
            if (dailyPrice.OpenPrice <= floorClosePrice || dailyPrice.OpenPrice >= ceilingClosePrice)
            {
                closePrice = dailyPrice.OpenPrice;
            }
            else if (dailyPrice.LowesetPrice <= floorClosePrice)
            {
                closePrice = floorClosePrice;
            }
            else if (dailyPrice.HighestPrice >= ceilingClosePrice)
            {
                closePrice = ceilingClosePrice;
            }
            if (closePrice == 0)
            {
                return null;
            }
            var transaction = new Transaction
            {
                Behavior = Behavior.Close,
                Date = dailyPrice.Date,
                Contract = Account.Contract,
                Price = closePrice,
                TransactionFee = closePrice * Account.TransactionFeeRate
            };
            ActionAfterClose(closePrice, dailyPrice);
            return transaction;
        }

        protected abstract void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice);

        public abstract Transaction TryOpen(DailyPrice dailyPrice, DailyPrice previousDailyPrice = null);

        protected abstract decimal GetFloorClosePrice();
        protected abstract decimal GetCeilingClosePrice();
    }
}