using FuturesAnalyzer.Models.States;

namespace FuturesAnalyzer.Models
{
    public class Account
    {
        public decimal Balance { get; set; }
        public Contract Contract { get; set; }
        public MarketState MarketState { get; set; }
        public bool? IsLastTransactionLoss { get; set; }
        public decimal TransactionFeeRate { get; set; }
        public int HitBothCriteriaInAmbiguousStateCount { get; set; } = 0;

        public decimal StopLossCriteria = 0.01m;
        public decimal StartProfitCriteria = 0.02m;
        public decimal StopProfitCriteria = 0.2m;
        public decimal StartProfitCriteriaForMultiUnits = 0.01m;
        public int StopLossUnit = 1;
        public bool NeverEnterAmbiguousState = false;
        public int AppendUnitCountAfterProfitStart = 0;
        public decimal MinimumPriceUnit = 1;
        public decimal OpenCriteria = 0.02m;
        public bool FollowTrend = true;

        public decimal DeductTransactionFee(decimal price, int unit)
        {
            var transactionFee = price * TransactionFeeRate * unit;
            Balance -= transactionFee;
            return transactionFee;
        }
    }
}
