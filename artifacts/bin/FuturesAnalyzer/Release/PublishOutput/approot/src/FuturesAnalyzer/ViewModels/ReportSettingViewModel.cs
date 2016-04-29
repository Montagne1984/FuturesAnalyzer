using System;
using FuturesAnalyzer.Models.States;

namespace FuturesAnalyzer.ViewModels
{
    public class ReportSettingViewModel
    {
        public int StopLossUnit { get; set; } = MarketState.StopLossUnit;
        public decimal StopLossCriteria { get; set; } = MarketState.StopLossCriteria;
        public decimal OpenCriteria { get; set; } = AmbiguousState.OpenCriteria;
        public decimal StartProfitCriteria { get; set; } = MarketState.StartProfitCriteria;
        public decimal StopProfitCriteria { get; set; } = MarketState.StopProfitCriteria;
        public decimal StartProfitCriteriaForMultiUnits { get; set; } = MarketState.StartProfitCriteriaForMultiUnits;
        public bool NeverEnterAmbiguousState { get; set; } = MarketState.NeverEnterAmbiguousState;
        public bool FollowTrend { get; set; } = AmbiguousState.FollowTrend;
        public int AppendUnitCountAfterProfitStart { get; set; } = MarketState.AppendUnitCountAfterProfitStart;
        public string ProductNames { get; set; }
        public string SelectedProductName { get; set; }
        public decimal TransactionFeeRate { get; set; } = 0.0008m;
        public DateTime StartDate { get; set; } = DateTime.Now.AddYears(-1);
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(-1);
        public bool Optimize { get; set; }
        public decimal MinimumPriceUnit { get; set; } = MarketState.MinimumPriceUnit;
    }
}