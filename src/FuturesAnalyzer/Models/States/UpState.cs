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
            MarketState newState;
            if (Account.NeverEnterAmbiguousState || closePrice > Account.Contract.Price || !Account.IsLastTransactionLoss.HasValue || !Account.IsLastTransactionLoss.Value)
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
            newState.HighestPrice = dailyPrice.ClosePrice;
            newState.LowestPrice = dailyPrice.ClosePrice;
            var balanceDelta = (closePrice - Account.Contract.Price) * Account.Contract.Unit;
            Account.Balance += balanceDelta;
            if (balanceDelta > 0 && Account.Contract.AppendUnitPrice != decimal.MaxValue)
            {
                Account.Balance += (closePrice - Account.Contract.AppendUnitPrice) * Account.AppendUnitCountAfterProfitStart;
            }
            Account.DeductTransactionFee(closePrice, Account.Contract.Unit);
            Account.MarketState = newState;
            Account.Contract = null;
        }

        protected override decimal GetStopLossPrice()
        {
            return Floor(Account.Contract.Price*(1 - Account.StopLossCriteria));
        }

        protected override decimal GetStopProfitPrice()
        {
            if (Account.Contract.Unit > 1)
            {
                return Account.Contract.Price*(1 + Account.StartProfitCriteriaForMultiUnits);
            }
            return HighestPrice <= Account.Contract.Price*(1 + Account.StartProfitCriteria)
                ? decimal.MinValue
                : Floor(HighestPrice - (HighestPrice - Account.Contract.Price)* Account.StopProfitCriteria);
        }
    }
}