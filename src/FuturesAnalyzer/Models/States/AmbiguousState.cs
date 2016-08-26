using System;

namespace FuturesAnalyzer.Models.States
{
    public class AmbiguousState : MarketState
    {
        private decimal _ceilingOpenPrice;
        private decimal _floorOpenPrice;
        private bool _hitBothCriteria;

        public override Transaction TryOpen(DailyPrice dailyPrice)
        {
            if (Account.Contract != null || dailyPrice == null || PreviousPrice == null)
            {
                return null;
            }

            MarketState newState = null;

            if (_hitBothCriteria)
            {
                if (dailyPrice.OpenPrice > _ceilingOpenPrice)
                {
                    var closeContractPrice = Account.FollowTrend ? _floorOpenPrice : _ceilingOpenPrice;
                    var loss = dailyPrice.OpenPrice - closeContractPrice +
                                       (dailyPrice.OpenPrice + closeContractPrice) * Account.TransactionFeeRate;
                    newState = new UpState {StartPrice = Account.FollowTrend ? _ceilingOpenPrice : _floorOpenPrice, InternalProfit = -loss};
                }
                else if (dailyPrice.OpenPrice < _floorOpenPrice)
                {
                    var closeContractPrice = Account.FollowTrend ? _ceilingOpenPrice : _floorOpenPrice;
                    var loss = closeContractPrice - dailyPrice.OpenPrice +
                                       (dailyPrice.OpenPrice + closeContractPrice) * Account.TransactionFeeRate;
                    newState = new DownState { StartPrice = Account.FollowTrend ? _floorOpenPrice : _ceilingOpenPrice, InternalProfit = -loss};
                }
                else
                {
                    return null;
                }
            }
            else
            {
                var ceilingOpenPrice = GetCeilingOpenPrice();
                var floorOpenPrice = GetFloorOpenPrice();
                _hitBothCriteria = false;
                if (dailyPrice.HighestPrice >= ceilingOpenPrice && dailyPrice.LowestPrice <= floorOpenPrice)
                {
                    Account.HitBothCriteriaInAmbiguousStateCount++;
                    _hitBothCriteria = true;
                }

                if (_hitBothCriteria)
                {
                    _ceilingOpenPrice = Math.Max(ceilingOpenPrice, dailyPrice.OpenPrice);
                    _floorOpenPrice = Math.Min(floorOpenPrice, dailyPrice.OpenPrice);
                    return null;
                }

                if (dailyPrice.HighestPrice >= ceilingOpenPrice && !(_hitBothCriteria && dailyPrice.HitLowPriceFirst.HasValue && dailyPrice.HitLowPriceFirst.Value))
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
            }
            if (newState == null)
            {
                return null;
            }

            newState.HighestPrice = Math.Max(newState.StartPrice, dailyPrice.ClosePrice);
            newState.LowestPrice = Math.Min(newState.StartPrice, dailyPrice.ClosePrice);
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

        public virtual decimal GetCeilingOpenPrice()
        {
            return Ceiling(PreviousPrice.ClosePrice*(1 + Account.OpenCriteria));
        }

        public virtual decimal GetFloorOpenPrice()
        {
            return Floor(PreviousPrice.ClosePrice*(1 - Account.OpenCriteria));
        }

        public override string GetNextTransaction()
        {
            if (_hitBothCriteria)
            {
                return $"开盘大于{_ceilingOpenPrice}买平 开盘小于{_floorOpenPrice}卖平";
            }
            return $@"{(Account.FollowTrend ? "卖" : "买")}开{GetFloorOpenPrice()} {(Account.FollowTrend ? "买" : "卖")}开{GetCeilingOpenPrice()}";
        }
    }
}