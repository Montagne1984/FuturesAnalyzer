using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FuturesAnalyzer.Models.States
{
    public class AverageAmbiguousState: AmbiguousState
    {
        public override Transaction TryOpen(DailyPrice dailyPrice)
        {
            if (Account.Contract != null || dailyPrice == null || PreviousPrice == null)
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
                    newState = new AverageUpState();
                }
                else
                {
                    newState = new AverageDownState();
                }
                newState.StartPrice = Math.Max(dailyPrice.OpenPrice, ceilingOpenPrice);
            }
            else if (dailyPrice.LowestPrice <= floorOpenPrice)
            {
                if (Account.FollowTrend)
                {
                    newState = new AverageDownState();
                }
                else
                {
                    newState = new AverageUpState();
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

        public override decimal GetCeilingOpenPrice()
        {
            return Ceiling(Account.FiveDaysAveragePrice * (1 + Account.OpenCriteria));
        }

        public override decimal GetFloorOpenPrice()
        {
            return Floor(Account.FiveDaysAveragePrice * (1 - Account.OpenCriteria));
        }
    }
}
