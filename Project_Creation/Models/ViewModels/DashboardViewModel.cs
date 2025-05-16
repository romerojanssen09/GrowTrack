using System;
using System.Collections.Generic;

namespace Project_Creation.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalSales { get; set; }
        public decimal TotalRevenue { get; set; }
        public int LowStockCount { get; set; }
        public List<RecentSaleViewModel> RecentSales { get; set; } = new List<RecentSaleViewModel>();
        public List<RecentActivityViewModel> RecentActivities { get; set; } = new List<RecentActivityViewModel>();
        public List<SalesTrendViewModel> SalesTrend { get; set; } = new List<SalesTrendViewModel>();
        public List<StockDistributionViewModel> StockDistribution { get; set; } = new List<StockDistributionViewModel>();
    }

    public class RecentSaleViewModel
    {
        public int SaleId { get; set; }
        public string CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime SaleDate { get; set; }
        public int ItemCount { get; set; }
        public List<SaleItemInfo> Products { get; set; } = new List<SaleItemInfo>();
    }

    public class SaleItemInfo
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class RecentActivityViewModel
    {
        public string ProductName { get; set; }
        public string Type { get; set; }
        public int Quantity { get; set; }
        public DateTime Timestamp { get; set; }
        public string Notes { get; set; }
    }

    public class SalesTrendViewModel
    {
        public DateTime Date { get; set; }
        public decimal TotalSales { get; set; }
        public int OrderCount { get; set; }
    }

    public class StockDistributionViewModel
    {
        public string Category { get; set; }
        public int TotalItems { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalValue { get; set; }
    }
} 