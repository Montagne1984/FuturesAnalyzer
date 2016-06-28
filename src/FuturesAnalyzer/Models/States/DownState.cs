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
                && dailyPrice.ClosePrice <= Account.Contract.Price * (1 - Account.StartProfitCriteria))
            {
                Account.Contract.AppendUnitPrice = Account.Contract.Price * (1 - Account.StartProfitCriteria);
                if (Account.Contract.AppendUnitPrice >= Ceiling(dailyPrice.ClosePrice + (Account.Contract.Price - dailyPrice.ClosePrice) * Account.StopProfitCriteria))
                {
                    Account.Contract.AppendUnitPrice = decimal.MaxValue;
                }
            }

            var stopProfitPrice = GetStopProfitPrice();
            var stopLossPrice = GetStopLossPrice();

            var closePrice = 0m;
            if (dailyPrice.OpenPrice >= stopLossPrice)
            {
                if (Account.Contract.Unit >= Account.StopLossUnit)
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
                if (Account.Contract.Unit >= Account.StopLossUnit)
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
            return new Transaction
            {
                Behavior = Behavior.Open,
                Date = dailyPrice.Date,
                Contract = contract,
                Price = StartPrice,
                TransactionFee = Account.DeductTransactionFee(StartPrice, 1)
            };
        }

        protected override void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice)
        {
            var newState = GetNewState(closePrice);
            newState.Account = Account;
            newState.StartPrice = closePrice;
            newState.HighestPrice = dailyPrice.ClosePrice;
            newState.LowestPrice = dailyPrice.ClosePrice;
            var balanceDelta = (Account.Contract.Price - closePrice) * Account.Contract.Unit;
            Account.Balance += balanceDelta;
            if (balanceDelta > 0 && Account.Contract.AppendUnitPrice != decimal.MaxValue)
            {
                Account.Balance += (Account.Contract.AppendUnitPrice - closePrice) * Account.AppendUnitCountAfterProfitStart;
            }
            Account.DeductTransactionFee(closePrice, Account.Contract.Unit);
            Account.MarketState = newState;
            Account.Contract = null;
        }

        protected override MarketState GetNewState(decimal closePrice)
        {
            MarketState newState;
            if (Account.NeverEnterAmbiguousState || closePrice < Account.Contract.Price ||
                !Account.IsLastTransactionLoss.HasValue || !Account.IsLastTransactionLoss.Value)
            {
                newState = new UpState();
                Account.IsLastTransactionLoss = closePrice > Account.Contract.Price;
            }
            else
            {
                newState = new AmbiguousState();
                Account.IsLastTransactionLoss = null;
            }
            return newState;
        }

        public override decimal GetStopProfitPrice()
        {
            //if (Account.Direction > 1)
            //{
            //    return decimal.MinValue;
            //}
            if (Account.Contract.Unit > 1)
            {
                return Account.Contract.Price * (1 - Account.StartProfitCriteriaForMultiUnits);
            }
            return LowestPrice >= Account.Contract.Price * (1 - Account.StartProfitCriteria)
                ? decimal.MaxValue
                : Ceiling(LowestPrice + (Account.Contract.Price - LowestPrice) * Account.StopProfitCriteria);
        }

        public override decimal GetStopLossPrice()
        {
            //if (Account.Direction > 1)
            //{
            //    return decimal.MinValue;
            //}
            return Ceiling(Account.Contract.Price * (1 + Account.StopLossCriteria));
        }

        public override string GetNextTransaction()
        {
            var stopProfitPrice = GetStopProfitPrice();
            if (stopProfitPrice < decimal.MaxValue)
            {
                return $@"买反{stopProfitPrice}";
            }
            var stopLossPrice = GetStopLossPrice();
            return $@"买{(Account.IsLastTransactionLoss.HasValue && Account.IsLastTransactionLoss.Value ? "平" : "反")}{stopLossPrice}";
        }
    }
}