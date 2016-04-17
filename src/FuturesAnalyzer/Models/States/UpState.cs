using System;

namespace FuturesAnalyzer.Models.States
{
    public class UpState : MarketState
    {
        public override Transaction TryClose(DailyPrice dailyPrice)
        {
            if (Account.Contract == null)
            {
                return null;
            }

            var stopLossPrice = GetStopLossPrice();
            var stopProfitPrice = GetStopProfitPrice();

            var closePrice = 0m;
            if (dailyPrice.OpenPrice <= stopLossPrice || stopProfitPrice < decimal.MaxValue && dailyPrice.OpenPrice <= stopProfitPrice)
            {
                closePrice = dailyPrice.OpenPrice;
            }
            else if (dailyPrice.LowesetPrice <= stopProfitPrice && dailyPrice.HighestPrice >= stopProfitPrice)
            {
                closePrice = stopProfitPrice;
            }
            else if (dailyPrice.LowesetPrice <= stopLossPrice)
            {
                closePrice = stopLossPrice;
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

        public override Transaction TryOpen(DailyPrice dailyPrice)
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
                Account.IsLastTransactionLoss = closePrice < Account.Contract.Price;
            }
            else
            {
                newState = new AmbiguousState();
                Account.IsLastTransactionLoss = null;
            }
            newState.Account = Account;
            newState.StartPrice = closePrice;
            newState.HighestPrice = dailyPrice.AveragePrice;
            newState.LowestPrice = dailyPrice.LowesetPrice;
            Account.Balance += closePrice - Account.Contract.Price - closePrice * Account.TransactionFeeRate;
            Account.MarketState = newState;
            Account.Contract = null;
        }

        protected override decimal GetStopLossPrice()
        {
            return Math.Floor(Account.Contract.Price*(1 - StopLossCriteria));
        }

        protected override decimal GetStopProfitPrice()
        {
            return HighestPrice <= Account.Contract.Price*(1 + StartProfitCriteria)
                ? decimal.MaxValue
                : Math.Floor(HighestPrice - (HighestPrice - Account.Contract.Price)*StopProfitCriteria);
        }
    }
}