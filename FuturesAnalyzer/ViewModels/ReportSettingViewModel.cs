using System;
using System.Collections.Generic;

namespace FuturesAnalyzer.ViewModels
{
    public class ReportSettingViewModel
    {
        public int StopLossUnit { get; set; } = 1;
        public decimal StopLossCriteria { get; set; } = 0.01m;
        public decimal OpenCriteria { get; set; } = 0.02m;
        public decimal StartProfitCriteria { get; set; } = 0.2m;
        public decimal StopProfitCriteria { get; set; } = 0.2m;
        public decimal StartProfitCriteriaForMultiUnits { get; set; } = 0.01m;
        public bool NeverEnterAmbiguousState { get; set; } = false;
        public bool FollowTrend { get; set; } = true;
        public int AppendUnitCountAfterProfitStart { get; set; } = 0;
        public string ProductNames { get; set; }
        public string SelectedProductName { get; set; }
        public decimal TransactionFeeRate { get; set; } = 0.0008m;
        public DateTime StartDate { get; set; } = DateTime.Now.AddYears(-1);
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(-1);
        public decimal MinimumPriceUnit { get; set; } = 1;
        public bool NotUseClosePrice { get; set; } = false;
        public bool UseAverageMarketState { get; set; }
        public bool CloseAfterProfit { get; set; }
        public bool OnlyUseClosePrice { get; set; }
        public bool UseCrossStarStrategy { get; set; }
        public bool UseInternalProfit { get; set; }
        public bool CloseAmbiguousStateToday { get; set; }
        public bool NeverReverse { get; set; }

        public ReportSettingViewModel Clone()
        {
            return new ReportSettingViewModel
            {
                StopLossUnit = StopLossUnit,
                StopLossCriteria = StopLossCriteria,
                OpenCriteria = OpenCriteria,
                StartProfitCriteria = StartProfitCriteria,
                StopProfitCriteria = StopProfitCriteria,
                StartProfitCriteriaForMultiUnits = StartProfitCriteriaForMultiUnits,
                NeverEnterAmbiguousState = NeverEnterAmbiguousState,
                FollowTrend = FollowTrend,
                AppendUnitCountAfterProfitStart = AppendUnitCountAfterProfitStart,
                ProductNames = ProductNames,
                SelectedProductName = SelectedProductName,
                TransactionFeeRate = TransactionFeeRate,
                StartDate = StartDate,
                EndDate = EndDate,
                MinimumPriceUnit = MinimumPriceUnit,
                NotUseClosePrice = NotUseClosePrice,
                UseAverageMarketState = UseAverageMarketState,
                CloseAfterProfit = CloseAfterProfit,
                OnlyUseClosePrice = OnlyUseClosePrice,
                CloseAmbiguousStateToday = CloseAmbiguousStateToday,
                NeverReverse = NeverReverse
            };
        }

        public override bool Equals(object obj)
        {
            return obj is ReportSettingViewModel settings
                && StopLossUnit == settings.StopLossUnit
                && StopLossCriteria == settings.StopLossCriteria
                && OpenCriteria == settings.OpenCriteria
                && StartProfitCriteria == settings.StartProfitCriteria
                && StopProfitCriteria == settings.StopProfitCriteria
                && StartProfitCriteriaForMultiUnits == settings.StartProfitCriteriaForMultiUnits
                && NeverEnterAmbiguousState == settings.NeverEnterAmbiguousState
                && FollowTrend == settings.FollowTrend
                && AppendUnitCountAfterProfitStart == settings.AppendUnitCountAfterProfitStart
                && ProductNames == settings.ProductNames
                && SelectedProductName == settings.SelectedProductName
                && TransactionFeeRate == settings.TransactionFeeRate
                && StartDate == settings.StartDate
                && EndDate == settings.EndDate
                && MinimumPriceUnit == settings.MinimumPriceUnit
                && CloseAmbiguousStateToday == settings.CloseAmbiguousStateToday
                && NeverReverse == settings.NeverReverse;
        }

        public override int GetHashCode()
        {
            var hashCode = -1306938562;
            hashCode = hashCode * -1521134295 + StopLossUnit.GetHashCode();
            hashCode = hashCode * -1521134295 + StopLossCriteria.GetHashCode();
            hashCode = hashCode * -1521134295 + OpenCriteria.GetHashCode();
            hashCode = hashCode * -1521134295 + StartProfitCriteria.GetHashCode();
            hashCode = hashCode * -1521134295 + StopProfitCriteria.GetHashCode();
            hashCode = hashCode * -1521134295 + StartProfitCriteriaForMultiUnits.GetHashCode();
            hashCode = hashCode * -1521134295 + NeverEnterAmbiguousState.GetHashCode();
            hashCode = hashCode * -1521134295 + FollowTrend.GetHashCode();
            hashCode = hashCode * -1521134295 + AppendUnitCountAfterProfitStart.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ProductNames);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SelectedProductName);
            hashCode = hashCode * -1521134295 + TransactionFeeRate.GetHashCode();
            hashCode = hashCode * -1521134295 + StartDate.GetHashCode();
            hashCode = hashCode * -1521134295 + EndDate.GetHashCode();
            hashCode = hashCode * -1521134295 + MinimumPriceUnit.GetHashCode();
            hashCode = hashCode * -1521134295 + NotUseClosePrice.GetHashCode();
            hashCode = hashCode * -1521134295 + UseAverageMarketState.GetHashCode();
            hashCode = hashCode * -1521134295 + CloseAfterProfit.GetHashCode();
            hashCode = hashCode * -1521134295 + OnlyUseClosePrice.GetHashCode();
            hashCode = hashCode * -1521134295 + UseCrossStarStrategy.GetHashCode();
            hashCode = hashCode * -1521134295 + UseInternalProfit.GetHashCode();
            hashCode = hashCode * -1521134295 + CloseAmbiguousStateToday.GetHashCode();
            hashCode = hashCode * -1521134295 + NeverReverse.GetHashCode();
            return hashCode;
        }
    }
}