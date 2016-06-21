using System;

namespace FuturesAnalyzer.Models.States
{
    public class AmbiguousState : MarketState
    {

        public override Transaction TryOpen(DailyPrice dailyPrice)
        {
            if (Account.Contract != null || dailyPrice == null || !PreviousPrice.HasValue)
            {
                return null;
            }
            var ceilingOpenPrice = GetCeilingOpenPrice();
            var floorOpenPrice = GetFloorOpenPrice();
            MarketState newState = null;
            var hitBothCriteria = false;
            if (dailyPrice.HighestPrice >= ceilingOpenPrice && dailyPrice.LowestPrice <= floorOpenPrice)
            {
                Account.HitBothCriteriaInAmbiguousStateCount++;
                hitBothCriteria = true;
            }
            if (dailyPrice.HighestPrice >= ceilingOpenPrice && !(hitBothCriteria && dailyPrice.HitLowPriceFirst.HasValue && dailyPrice.HitLowPriceFirst.Value))
            {
                if (Account.FollowTrend)
                {
                    newState = new UpState();
                }
                else
                {
                    newState = new DownState();
                }
                newState.StartPrice = Math.Max(dailyPrice.OpenPrice, ceilingOpenPrice);
            }
            else if (dailyPrice.LowestPrice <= floorOpenPrice)
            {
                if (Account.FollowTrend)
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

        public override decimal GetStopLossPrice()
        {
            return 0;
        }

        public override decimal GetStopProfitPrice()
        {
            return 0;
        }

        public decimal GetCeilingOpenPrice()
        {
            return Ceiling(PreviousPrice.Value*(1 + Account.OpenCriteria));
        }

        public decimal GetFloorOpenPrice()
        {
            return Floor(PreviousPrice.Value*(1 - Account.OpenCriteria));
        }

        public override string GetNextTransaction()
        {
            return $@"买开{GetFloorOpenPrice()} 卖开{GetCeilingOpenPrice()}";
        }
    }
}