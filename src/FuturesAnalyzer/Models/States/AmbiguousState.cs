using System;

namespace FuturesAnalyzer.Models.States
{
    public class AmbiguousState : MarketState
    {
        public static decimal OpenCriteria = 0.02m;
        public static bool FollowTrend = true;

        public override Transaction TryOpen(DailyPrice dailyPrice, DailyPrice previousDailyPrice = null)
        {
            if (Account.Contract != null || dailyPrice == null || previousDailyPrice == null)
            {
                return null;
            }
            var ceilingOpenPrice = Math.Ceiling(previousDailyPrice.AveragePrice*(1 + OpenCriteria));
            var floorOpenPrice = Math.Floor(previousDailyPrice.AveragePrice*(1 - OpenCriteria));
            MarketState newState = null;
            if (dailyPrice.HighestPrice >= ceilingOpenPrice)
            {
                if (FollowTrend)
                {
                    newState = new UpState();
                }
                else
                {
                    newState = new DownState();
                }
                newState.StartPrice = Math.Max(dailyPrice.OpenPrice, ceilingOpenPrice);
            }
            else if (dailyPrice.LowesetPrice <= floorOpenPrice)
            {
                if (FollowTrend)
                {
                    newState = new DownState();
                }
                else
                {
                    newState = new UpState();
                }
                newState.StartPrice = Math.Min(dailyPrice.OpenPrice, floorOpenPrice);
            }
            if (newState == null)
            {
                return null;
            }

            newState.HighestPrice = dailyPrice.AveragePrice;
            newState.LowestPrice = dailyPrice.AveragePrice;
            newState.Account = Account;
            var transaction = newState.TryOpen(dailyPrice);
            Account.MarketState = newState;
            Account.Contract = transaction.Contract;
            return transaction;
        }

        protected override void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice)
        {
        }

        protected override decimal GetFloorClosePrice()
        {
            return decimal.MinValue;
        }

        protected override decimal GetCeilingClosePrice()
        {
            return decimal.MaxValue;
        }
    }
}