using System.Linq;

namespace FuturesAnalyzer.Models.States
{
    public class UpState : MarketState
    {
        public decimal StartProfitPoint => Account.Contract.Price*(1 + Account.StartProfitCriteria);
        public override bool CloseWithinStartProfitPrice(DailyPrice dailyPrice) => dailyPrice.ClosePrice < StartProfitPoint;
        public override bool HitStartProfitPrice(DailyPrice dailyPrice) => dailyPrice.HighestPrice >= StartProfitPoint + Account.Contract.Price * 0.01m;

        public decimal InternalProfit { get; set; }

        public override Transaction TryClose(DailyPrice dailyPrice)
        {
            if (Account.Contract == null)
            {
                return null;
            }

            if (Account.Contract.AppendUnitPrice < Account.Contract.Price 
                && dailyPrice.ClosePrice >= Account.Contract.Price * (1 + Account.StartProfitCriteria))
            {
                Account.Contract.AppendUnitPrice = Account.Contract.Price * (1 + Account.StartProfitCriteria);
                if (Account.Contract.AppendUnitPrice <= Floor(dailyPrice.ClosePrice - (dailyPrice.ClosePrice - Account.Contract.Price) * Account.StopProfitCriteria))
                {
                    Account.Contract.AppendUnitPrice = decimal.MaxValue;
                }
            }

            var stopLossPrice = GetStopLossPrice();
            var stopProfitPrice = GetStopProfitPrice();

            var closePrice = 0m;
            if (dailyPrice.OpenPrice <= stopLossPrice)
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
            else if (Account.Contract.Unit == 1 && dailyPrice.OpenPrice <= stopProfitPrice || Account.Contract.Unit > 1 && dailyPrice.OpenPrice >= stopProfitPrice)
            {
                closePrice = dailyPrice.OpenPrice;
            }
            else if (dailyPrice.LowestPrice <= stopProfitPrice && dailyPrice.HighestPrice >= stopProfitPrice)
            {
                closePrice = stopProfitPrice;
            }
            else if (dailyPrice.LowestPrice <= stopLossPrice)
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
                        Price = stopLossPrice,
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
                    var hitPrice = StartProfitPoint + Account.Contract.Price * 0.01m;
                    InternalProfit += hitPrice - dailyPrice.OpenPrice -
                                      (StartProfitPoint + dailyPrice.OpenPrice) * Account.TransactionFeeRate;
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
            var contract = new Contract {Direction = Direction.Buy, Price = StartPrice};
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
            if (!(Account.CloseAfterProfit && Account.IsLastTransactionLoss.HasValue && !Account.IsLastTransactionLoss.Value))
            {
                var balanceDelta = (closePrice - Account.Contract.Price)*Account.Contract.Unit + InternalProfit;
                Account.Balance += balanceDelta;
                if (balanceDelta > 0 && Account.Contract.AppendUnitPrice != decimal.MaxValue)
                {
                    Account.Balance += (closePrice - Account.Contract.AppendUnitPrice)*
                                       Account.AppendUnitCountAfterProfitStart;
                }
            }
            Account.DeductTransactionFee(closePrice, Account.Contract.Unit);
            MarketState newState = GetNewState(closePrice);
            newState.Account = Account;
            newState.StartPrice = closePrice;
            newState.HighestPrice = dailyPrice.ClosePrice;
            newState.LowestPrice = dailyPrice.ClosePrice;
            Account.MarketState = newState;
            Account.Contract = null;
        }

        protected override MarketState GetNewState(decimal closePrice)
        {
            MarketState newState;
            if (Account.NeverEnterAmbiguousState || closePrice > Account.Contract.Price ||
                !Account.IsLastTransactionLoss.HasValue || !Account.IsLastTransactionLoss.Value)
            {
                newState = new DownState();
                Account.IsLastTransactionLoss = closePrice < Account.Contract.Price;
            }
            else
            {
                newState = new AmbiguousState();
                Account.IsLastTransactionLoss = null;
            }
            return newState;
        }

        public override decimal GetStopLossPrice()
        {
            //if (Account.Direction < -1)
            //{
            //    return decimal.MaxValue;
            //}
            return Floor(Account.Contract.Price*(1 - Account.StopLossCriteria));
        }

        public override decimal GetStopProfitPrice()
        {
            //if (Account.Direction < -1)
            //{
            //    return decimal.MaxValue;
            //}
            if (Account.Contract.Unit > 1)
            {
                return Account.Contract.Price*(1 + Account.StartProfitCriteriaForMultiUnits);
            }
            var stopProfitPoint = Floor(HighestPrice - (HighestPrice - Account.Contract.Price) * Account.StopProfitCriteria);

            return HighestPrice >= StartProfitPoint &&
                   (!Account.OnlyUseClosePrice || Account.PreviousFiveDayPrices.Last() <= stopProfitPoint)
                ? stopProfitPoint
                : decimal.MinValue;
        }

        public override string GetNextTransaction()
        {
            var stopProfitPrice = GetStopProfitPrice();
            if (stopProfitPrice > decimal.MinValue)
            {
                return $@"卖反{stopProfitPrice}";
            }
            var stopLossPrice = GetStopLossPrice();
            return $@"卖{(Account.IsLastTransactionLoss.HasValue && Account.IsLastTransactionLoss.Value ? "平" : "反")}{stopLossPrice}";
        }
    }
}