using System;

namespace FuturesAnalyzer.Models.States
{
    public class UpState : MarketState
    {
        public override Transaction TryOpen(DailyPrice dailyPrice, DailyPrice previousDailyPrice = null)
        {
            if (Account.Contract != null)
            {
                return null;
            }
            var contract = new Contract {Direction = Direction.Buy, Price = StartPrice};
            Account.Contract = contract;
            return new Transaction
            {
                Behavior = Behavior.Open,
                Date = dailyPrice.Date,
                Contract = contract,
                Price = StartPrice,
                TransactionFee = StartPrice*Account.TransactionFeeRate
            };
        }

        protected override void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice)
        {
            MarketState newState;
            if (closePrice > Account.Contract.Price || !Account.IsLastTransactionLoss.HasValue || !Account.IsLastTransactionLoss.Value)
            {
                newState = new DownState();
            }
            else
            {
                newState = new AmbiguousState();
            }
            newState.Account = Account;
            newState.StartPrice = closePrice;
            newState.HighestPrice = dailyPrice.AveragePrice;
            newState.LowestPrice = dailyPrice.LowesetPrice;
            Account.Balance += closePrice - Account.Contract.Price - closePrice * Account.TransactionFeeRate;
            Account.MarketState = newState;
            Account.IsLastTransactionLoss = closePrice < Account.Contract.Price;
            Account.Contract = null;
        }

        protected override decimal GetFloorClosePrice()
        {
            return Math.Floor(Account.Contract.Price*(1 - StopLossCriteria));
        }

        protected override decimal GetCeilingClosePrice()
        {
            return HighestPrice <= Account.Contract.Price*(1 + StartProfitCriteria)
                ? decimal.MaxValue
                : Math.Floor(HighestPrice - (HighestPrice - Account.Contract.Price)*StopProfitCriteria);
        }
    }
}