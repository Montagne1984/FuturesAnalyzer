﻿using System;
using System.Linq;

namespace FuturesAnalyzer.Models.States
{
    public class DownState : MarketState
    {
        public decimal StartProfitPoint => Account.Contract.Price*(1 - Account.StartProfitCriteria);

        public override bool CloseWithinStartProfitPrice(DailyPrice dailyPrice)
            => dailyPrice.ClosePrice > StartProfitPoint;

        public override bool HitStartProfitPrice(DailyPrice dailyPrice)
            => dailyPrice.LowestPrice <= StartProfitPoint - Account.Contract.Price*0.01m;

        public override Transaction TryClose(DailyPrice dailyPrice)
        {
            if (Account.Contract == null)
            {
                return null;
            }

            if (Account.Contract.AppendUnitPrice < Account.Contract.Price
                && dailyPrice.ClosePrice <= Account.Contract.Price*(1 - Account.StartProfitCriteria))
            {
                Account.Contract.AppendUnitPrice = Account.Contract.Price*(1 - Account.StartProfitCriteria);
                if (Account.Contract.AppendUnitPrice >=
                    Ceiling(dailyPrice.ClosePrice +
                            (Account.Contract.Price - dailyPrice.ClosePrice)*Account.StopProfitCriteria))
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
            else if (Account.Contract.Unit == 1 && dailyPrice.OpenPrice >= stopProfitPrice ||
                     Account.Contract.Unit > 1 && dailyPrice.OpenPrice <= stopProfitPrice)
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
                if (New)
                {
                    New = false;
                    return null;
                }
                if (!Account.UseInternalProfit) return null;
                if (!StopInternalProfit && HitStartProfitPrice(PreviousPrice))
                {
                    var hitPrice = StartProfitPoint - Account.Contract.Price*0.01m;
                    InternalProfit += dailyPrice.OpenPrice - hitPrice -
                                      (StartProfitPoint + dailyPrice.OpenPrice)*Account.TransactionFeeRate;
                }
                if (!CloseWithinStartProfitPrice(PreviousPrice))
                {
                    StopInternalProfit = true;
                }
                return null;
            }
            var transaction = new Transaction
            {
                Behavior = Behavior.Close,
                Date = dailyPrice.Date,
                Contract = Account.Contract,
                Price = closePrice,
                TransactionFee = closePrice*Account.TransactionFeeRate*Account.Contract.Unit,
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
            var contract = new Contract {Direction = Direction.Sell, Price = StartPrice};
            Account.Contract = contract;
            if (!CloseWithinStartProfitPrice(dailyPrice))
            {
                StopInternalProfit = true;
            }
            New = true;
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
            if (
                !(Account.CloseAfterProfit && Account.IsLastTransactionLoss.HasValue &&
                  !Account.IsLastTransactionLoss.Value))
            {
                var balanceDelta = (Account.Contract.Price - closePrice)*Account.Contract.Unit + InternalProfit;
                Account.Balance += balanceDelta;
                if (balanceDelta > 0 && Account.Contract.AppendUnitPrice != decimal.MaxValue)
                {
                    Account.Balance += (Account.Contract.AppendUnitPrice - closePrice)*
                                       Account.AppendUnitCountAfterProfitStart;
                }
            }
            Account.DeductTransactionFee(closePrice, Account.Contract.Unit);
            var newState = GetNewState(closePrice);
            newState.Account = Account;
            newState.StartPrice = closePrice;
            newState.HighestPrice = Account.NotUseClosePrice ? dailyPrice.HighestPrice : dailyPrice.ClosePrice;
            newState.LowestPrice = Account.NotUseClosePrice ? dailyPrice.LowestPrice : dailyPrice.ClosePrice;
            if (closePrice < Account.Contract.Price)
            {
                newState.TopPrice = newState.HighestPrice;
                newState.BottomPrice = closePrice;
            }
            else
            {
                newState.TopPrice = Math.Max(TopPrice, Account.NotUseClosePrice ? dailyPrice.HighestPrice : dailyPrice.ClosePrice);
                newState.BottomPrice = Math.Min(BottomPrice, Account.NotUseClosePrice ? dailyPrice.LowestPrice : dailyPrice.ClosePrice);
            }
            Account.MarketState = newState;
            Account.Contract = null;
        }

        protected override MarketState GetNewState(decimal closePrice)
        {
            MarketState newState;
            if (!Account.NeverReverse && (
                Account.NeverEnterAmbiguousState || closePrice < Account.Contract.Price ||
                !Account.BreakThroughStratgy && (!Account.IsLastTransactionLoss.HasValue || !Account.IsLastTransactionLoss.Value)))
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
                return Account.Contract.Price*(1 - Account.StartProfitCriteriaForMultiUnits);
            }
            var stopProfitPoint =
                Ceiling(LowestPrice + (Account.Contract.Price - LowestPrice)*Account.StopProfitCriteria);

            return LowestPrice <= StartProfitPoint &&
                   (!Account.OnlyUseClosePrice || Account.PreviousFiveDayPrices.Last() >= stopProfitPoint)
                ? stopProfitPoint
                : decimal.MaxValue;
        }

        public override decimal GetStopLossPrice()
        {
            //if (Account.Direction > 1)
            //{
            //    return decimal.MinValue;
            //}
            if (Account.BreakThroughStratgy)
            {
                return Ceiling(Math.Max(Account.Contract.Price * (1 + Account.StopLossCriteria), (TopPrice + BottomPrice) / 2));
            }
            return Ceiling(Account.Contract.Price * (1 + Account.StopLossCriteria));

            //var stopLossPrice = Math.Min(LowestPrice, Account.Contract.Price) * (1 + Account.StopLossCriteria);
            //if (stopLossPrice < Account.Contract.Price)
            //{
            //    stopLossPrice = Account.Contract.Price;
            //}
            //return Ceiling(stopLossPrice);
        }

        public override string GetNextTransaction()
        {
            string nextTransaction;
            var stopProfitPrice = GetStopProfitPrice();
            if (stopProfitPrice < decimal.MaxValue)
            {
                nextTransaction = $@"买反{stopProfitPrice}";
            }
            else
            {
                var stopLossPrice = GetStopLossPrice();
                nextTransaction =
                    $@"买{(Account.IsLastTransactionLoss.HasValue && Account.IsLastTransactionLoss.Value ? "平" : "反")}{stopLossPrice}";
            }
            return nextTransaction +
                   $" 合约价格{Account.Contract.Price} 当前价格{Account.PreviousFiveDayPrices.Last()} 目标盈利价{Floor(StartProfitPoint)}";
        }
    }
}