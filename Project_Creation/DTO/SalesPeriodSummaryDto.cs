using System;

namespace Project_Creation.DTO
{
    public class SalesPeriodSummaryDto
    {
        public string PeriodName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalProductsSold { get; set; }
        public decimal AverageSaleValue { get; set; }
        public bool IsDataAvailable { get; set; } = false;
    }
}