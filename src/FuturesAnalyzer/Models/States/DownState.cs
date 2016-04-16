using System;

namespace FuturesAnalyzer.Models.States
{
    public class DownState : MarketState
    {
        public override Transaction TryOpen(DailyPrice dailyPrice, DailyPrice previousDailyPrice = null)
        {
            if (Account.Contract != null)
            {
                return null;
            }
            var contract = new Contract { Direction = Direction.Sell, Price = StartPrice };
            Account.Contract = contract;
            return new Transaction
            {
                Behavior = Behavior.Open,
                Date = dailyPrice.Date,
                Contract = contract,
                Price = StartPrice,
                TransactionFee = StartPrice * Account.TransactionFeeRate
            };
        }

        protected override void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice)
        {
            MarketState newState;
            if (closePrice < Account.Contract.Price || !Account.IsLastTransactionLoss.HasValue || !Account.IsLastTransactionLoss.Value)
            {
                newState = new UpState();
            }
            else
            {
                newState = new AmbiguousState();
            }
            newState.Account = Account;
            newState.StartPrice = closePrice;
            newState.HighestPrice = dailyPrice.AveragePrice;
            newState.LowestPrice = dailyPrice.AveragePrice;
            Account.Balance += Account.Contract.Price - closePrice - closePrice * Account.TransactionFeeRate;
            Account.MarketState = newState;
            Account.IsLastTransactionLoss = closePrice > Account.Contract.Price;
            Account.Contract = null;
        }

        protected override decimal GetFloorClosePrice()
        {
            return LowestPrice >= Account.Contract.Price * (1 - StartProfitCriteria)
                ? 0
                : Math.Ceiling(LowestPrice + (Account.Contract.Price - LowestPrice) * StopProfitCriteria);
        }

        protected override decimal GetCeilingClosePrice()
        {
            return Math.Ceiling(Account.Contract.Price * (1 + StopLossCriteria));
        }
    }
}