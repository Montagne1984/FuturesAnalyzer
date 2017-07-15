using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FuturesAnalyzer.Models.States
{
    public class AverageUpState: UpState
    {
        protected override MarketState GetNewState(decimal closePrice)
        {
            MarketState newState;
            if (Account.Direction < -1)
            {
                newState = new AverageDownState();
                Account.IsLastTransactionLoss = closePrice < Account.Contract.Price;
            }
            else
            {
                newState = new AverageAmbiguousState();
                Account.IsLastTransactionLoss = null;
            }
            return newState;
        }

        public override decimal GetStopProfitPrice()
        {
            return Account.Direction < -1 ? decimal.MaxValue : Account.FiveDaysAveragePrice*(1 - Account.StopLossCriteria);
        }

        public override decimal GetStopLossPrice()
        {
            return GetStopProfitPrice();
        }

        public override string GetNextTransaction()
        {
            var stopProfitPrice = GetStopProfitPrice();
            return stopProfitPrice < decimal.MaxValue ? $@"卖反{stopProfitPrice}" : "开盘卖平";
        }
    }
}
