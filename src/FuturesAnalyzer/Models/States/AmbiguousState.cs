using System;

namespace FuturesAnalyzer.Models.States
{
    public class AmbiguousState : MarketState
    {
        public static decimal OpenCriteria = 0.02m;
        public static bool FollowTrend = true;

        public override Transaction TryOpen(DailyPrice dailyPrice)
        {
            if (Account.Contract != null || dailyPrice == null || !PreviousPrice.HasValue)
            {
                return null;
            }
            var ceilingOpenPrice = Math.Ceiling(PreviousPrice.Value * (1 + OpenCriteria));
            var floorOpenPrice = Math.Floor(PreviousPrice.Value * (1 - OpenCriteria));
            //var ceilingOpenPrice = Math.Ceiling(LowestPrice * (1 + OpenCriteria));
            //var floorOpenPrice = Math.Floor(HighestPrice * (1 - OpenCriteria));
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

            newState.HighestPrice = dailyPrice.ClosePrice;
            newState.LowestPrice = dailyPrice.ClosePrice;
            newState.Account = Account;
            var transaction = newState.TryOpen(dailyPrice);
            Account.MarketState = newState;
            Account.Contract = transaction.Contract;
            return transaction;
        }

        public override Transaction TryClose(DailyPrice dailyPrice)
        {
            return null;
        }

        protected override void ActionAfterClose(decimal closePrice, DailyPrice dailyPrice)
        {
        }

        protected override decimal GetStopLossPrice()
        {
            return 0;
        }

        protected override decimal GetStopProfitPrice()
        {
            return 0;
        }
    }
}