using System;
using System.Linq;

namespace FuturesAnalyzer.Models.States
{
    public class AmbiguousState : MarketState
    {
        private decimal _ceilingOpenPrice;
        private decimal _floorOpenPrice;
        private bool _hitBothCriteria;
        private decimal _internalProfit;

        public override Transaction TryOpen(DailyPrice dailyPrice)
        {
            if (Account.Contract != null || dailyPrice == null || PreviousPrice == null)
            {
                return null;
            }

            MarketState newState = null;

            if (_hitBothCriteria)
            {
                var lastPrice = Account.PreviousFiveDayPrices.Last();
                if (lastPrice > _ceilingOpenPrice && dailyPrice.HighestPrice > _ceilingOpenPrice)
                {
                    var closeContractPrice = Account.FollowTrend ? _floorOpenPrice : _ceilingOpenPrice;
                    var loss = (dailyPrice.OpenPrice > _ceilingOpenPrice ? dailyPrice.OpenPrice : _ceilingOpenPrice + Account.MinimumPriceUnit) - closeContractPrice +
                                       (dailyPrice.OpenPrice + closeContractPrice) * Account.TransactionFeeRate;
                    newState = new UpState {StartPrice = Account.FollowTrend ? _ceilingOpenPrice : _floorOpenPrice, InternalProfit = -loss};
                }
                else if (lastPrice < _floorOpenPrice && dailyPrice.LowestPrice < _floorOpenPrice)
                {
                    var closeContractPrice = Account.FollowTrend ? _ceilingOpenPrice : _floorOpenPrice;
                    var loss = closeContractPrice - (dailyPrice.OpenPrice < _floorOpenPrice ? dailyPrice.OpenPrice : _floorOpenPrice - Account.MinimumPriceUnit) +
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
                    if (Account.CloseAmbiguousStateToday)
                    {
                        _internalProfit += (floorOpenPrice - ceilingOpenPrice)* (Account.FollowTrend ? 1 : -1) -
                                           (ceilingOpenPrice + floorOpenPrice)*Account.TransactionFeeRate;
                        return null;
                    }
                    _hitBothCriteria = true;
                }

                if (_hitBothCriteria)
                {
                    _ceilingOpenPrice = Math.Max(ceilingOpenPrice, dailyPrice.OpenPrice);
                    _floorOpenPrice = Math.Min(floorOpenPrice, dailyPrice.OpenPrice);
                    InternalProfit = (_floorOpenPrice - _ceilingOpenPrice)*(Account.FollowTrend ? 1 : -1);
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
                    newState.InternalProfit = _internalProfit;
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
                    newState.InternalProfit = _internalProfit;
                }
            }
            if (newState == null)
            {
                return null;
            }

            newState.HighestPrice = Math.Max(newState.StartPrice, Account.FollowTrend && Account.NotUseClosePrice ? dailyPrice.HighestPrice : dailyPrice.ClosePrice);
            newState.LowestPrice = Math.Min(newState.StartPrice, Account.FollowTrend && Account.NotUseClosePrice ? dailyPrice.LowestPrice : dailyPrice.ClosePrice);
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
                var lastPrice = Account.PreviousFiveDayPrices.Last();
                if (lastPrice > _ceilingOpenPrice)
                {
                    return $"大于{_ceilingOpenPrice}买平";
                }
                if (lastPrice < _floorOpenPrice)
                {
                    return $"小于{_floorOpenPrice}卖平";
                }
                return "不交易";
            }
            return $@"{(Account.FollowTrend ? "卖" : "买")}开{GetFloorOpenPrice()} {(Account.FollowTrend ? "买" : "卖")}开{GetCeilingOpenPrice()}";
        }
    }
}