using System;
using System.Collections.Generic;

namespace Project_Creation.DTO
{
    public class SalesComparisonViewModel
    {
        // Date inputs for the form
        public DateTime? Period1StartDate { get; set; }
        public DateTime? Period1EndDate { get; set; }
        public DateTime? Period2StartDate { get; set; }
        public DateTime? Period2EndDate { get; set; }

        // Summaries for each period
        public SalesPeriodSummaryDto Period1Summary { get; set; }
        public SalesPeriodSummaryDto Period2Summary { get; set; }

        // Comparison Metrics (can be calculated in the view or controller)
        public decimal RevenueDifference => (Period2Summary?.TotalRevenue ?? 0) - (Period1Summary?.TotalRevenue ?? 0);
        public double RevenueChangePercentage
        {
            get
            {
                if (Period1Summary?.TotalRevenue == 0 || Period1Summary?.TotalRevenue == null) return (Period2Summary?.TotalRevenue > 0) ? 100.0 : 0.0; // Avoid division by zero or if P1 is 0 and P2 is positive
                return (double)(((Period2Summary?.TotalRevenue ?? 0) - Period1Summary.TotalRevenue) / Period1Summary.TotalRevenue * 100);
            }
        }

        public int SalesCountDifference => (Period2Summary?.TotalSalesCount ?? 0) - (Period1Summary?.TotalSalesCount ?? 0);
        public double SalesCountChangePercentage
        {
            get
            {
                if (Period1Summary?.TotalSalesCount == 0 || Period1Summary?.TotalSalesCount == null) return (Period2Summary?.TotalSalesCount > 0) ? 100.0 : 0.0;
                return (double)(((Period2Summary?.TotalSalesCount ?? 0) - Period1Summary.TotalSalesCount) / (decimal)Period1Summary.TotalSalesCount * 100);
            }
        }

        public bool ShowResults { get; set; } = false; // To control visibility of the results section

        public SalesComparisonViewModel()
        {
            Period1Summary = new SalesPeriodSummaryDto { PeriodName = "Period 1" };
            Period2Summary = new SalesPeriodSummaryDto { PeriodName = "Period 2" };
        }
    }
}