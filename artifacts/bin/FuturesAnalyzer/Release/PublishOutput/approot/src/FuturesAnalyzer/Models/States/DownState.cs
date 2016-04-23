using System;

namespace FuturesAnalyzer.Models.States
{
    public class DownState : MarketState
    {
        public override Transaction TryClose(DailyPrice dailyPrice)
        {
            if (Account.Contract == null)
            {
                return null;
            }

            if (Account.Contract.AppendUnitPrice < Account.Contract.Price
                && dailyPrice.ClosePrice <= Account.Contract.Price * (1 - StartProfitCriteria))
            {
                Account.Contract.AppendUnitPrice = Account.Contract.Price * (1 - StartProfitCriteria);
                if (Account.Contract.AppendUnitPrice >= Math.Ceiling(dailyPrice.ClosePrice + (Account.Contract.Price - dailyPrice.ClosePrice) * StopProfitCriteria))
                {
                    Account.Contract.AppendUnitPrice = decimal.MaxValue;
                }
            }

            var stopProfitPrice = GetStopProfitPrice();
            var stopLossPrice = GetStopLossPrice();

            var closePrice = 0m;
            if (dailyPrice.OpenPrice >= stopLossPrice)
            {
                if (Account.Contract.Unit >= StopLossUnit)
                {
                    closePrice = dailyPrice.OpenPrice;
                }
                else
                {
                    Account.Contract.Add(dailyPrice.OpenPrice, 1);
                    var transactionFee = Account.DeductTransactionFee(dailyPrice.OpenPrice, 1);
                    return new Transaction
                    {
                        Behavior = Behavior.Open,
                        Date = dailyPrice.Date,
                        Contract = Account.Contract,
                        Price = dailyPrice.OpenPrice,
                        TransactionFee = transactionFee
                    };
                }
            }
            else if (Account.Contract.Unit == 1 && dailyPrice.OpenPrice >= stopProfitPrice || Account.Contract.Unit > 1 && dailyPrice.OpenPrice <= stopProfitPrice)
            {
                closePrice = dailyPrice.OpenPrice;
            }
            else if (dailyPrice.LowestPrice <= stopProfitPrice && dailyPrice.HighestPrice >= stopProfitPrice)
            {
                closePrice = stopProfitPrice;
            }
            else if (dailyPrice.HighestPrice >= stopLossPrice)
            {
                if (Account.Contract.Unit >= StopLossUnit)
                {
                    closePrice = stopLossPrice;
                }
                else
                {
                    Account.Contract.Add(stopLossPrice, 1);
                    var transactionFee = Account.DeductTransactionFee(stopLossPrice, 1);
                    return new Transaction
                    {
                        Behavior = Behavior.Open,
                        Date = dailyPrice.Date,
                        Contract = Account.Contract,
                        Price = dailyPrice.OpenPrice,
                        TransactionFee = transactionFee
                    };
                }
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
                TransactionFee = closePrice * Account.TransactionFeeRate * Account.Contract.Unit, 
                Unit = Account.Contract.Unit
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
            var contract = new Contract { Direction = Direction.Sell, Price = StartPrice };
            Account.Contract = contract;
            var transactionFee = StartPrice * Account.TransactionFeeRate;
            Account.Balance -= transactionFee;
            return new Transaction
            {
                Behavior = Behavior.Open,
                Date = dailyPrice.Date,
                Contract = contract,
                Price = StartPrice,
                TransactionFee = transactionFee
            };
        }

        protected override void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice)
        {
            MarketState newState;
            if (NeverEnterAmbiguousState || closePrice < Account.Contract.Price || !Account.IsLastTransactionLoss.HasValue || !Account.IsLastTransactionLoss.Value)
            {
                newState = new UpState();
                Account.IsLastTransactionLoss = closePrice > Account.Contract.Price;
            }
            else
            {
                newState = new AmbiguousState();
                Account.IsLastTransactionLoss = null;
            }
            newState.Account = Account;
            newState.StartPrice = closePrice;
            newState.HighestPrice = dailyPrice.ClosePrice;
            newState.LowestPrice = dailyPrice.ClosePrice;
            var balanceDelta = (Account.Contract.Price - closePrice) * Account.Contract.Unit;
            Account.Balance += balanceDelta;
            if (balanceDelta > 0 && Account.Contract.AppendUnitPrice != decimal.MaxValue)
            {
                Account.Balance += (Account.Contract.AppendUnitPrice - closePrice) * AppendUnitCountAfterProfitStart;
            }
            Account.DeductTransactionFee(closePrice, Account.Contract.Unit);
            Account.MarketState = newState;
            Account.Contract = null;
        }

        protected override decimal GetStopProfitPrice()
        {
            if (Account.Contract.Unit > 1)
            {
                return Account.Contract.Price * (1 - StartProfitCriteriaForMultiUnits);
            }
            return LowestPrice >= Account.Contract.Price * (1 - StartProfitCriteria)
                ? decimal.MaxValue
                : Math.Ceiling(LowestPrice + (Account.Contract.Price - LowestPrice) * StopProfitCriteria);
        }

        protected override decimal GetStopLossPrice()
        {
            return Math.Ceiling(Account.Contract.Price * (1 + StopLossCriteria));
        }
    }
}