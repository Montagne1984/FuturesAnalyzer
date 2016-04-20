namespace FuturesAnalyzer.Models
{
    public class Contract
    {
        public decimal Price { get; set; }
        public Direction Direction { get; set; }
        public int Unit { get; set; } = 1;
        public decimal AppendUnitPrice { get; set; }

        public void Add(decimal price, int unit)
        {
            Price = (Price*Unit + price*unit)/(Unit + unit);
            Unit += unit;
        }
    }
}