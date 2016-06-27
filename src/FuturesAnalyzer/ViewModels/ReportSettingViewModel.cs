using System;

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
                UseAverageMarketState = UseAverageMarketState
            };
        }

        public override bool Equals(object obj)
        {
            var settings = obj as ReportSettingViewModel;
            return settings != null
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
                && MinimumPriceUnit == settings.MinimumPriceUnit;
        }
    }
}