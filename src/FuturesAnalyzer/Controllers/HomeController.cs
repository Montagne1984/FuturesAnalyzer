using System.Linq;
using FuturesAnalyzer.Models;
using FuturesAnalyzer.Models.States;
using FuturesAnalyzer.Services;
using FuturesAnalyzer.ViewModels;
using Microsoft.AspNet.Mvc;

namespace FuturesAnalyzer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IReportService _reportService;

        public HomeController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public IActionResult Index()
        {
            var model = new ReportSettingViewModel();
            return View(model);
        }

        public JsonResult Report(ReportSettingViewModel model)
        {
            MarketState.StopLossCriteria = model.StopLossCriteria;
            MarketState.StartProfitCriteria = model.StartProfitCriteria;
            MarketState.StopProfitCriteria = model.StopProfitCriteria;
            AmbiguousState.OpenCriteria = model.OpenCriteria;
            AmbiguousState.FollowTrend = model.FollowTrend;
            var dailyPrices = _reportService.LoadDailyPrices("Data/" + model.SelectedProductName + ".csv");
            var account = new Account {TransactionFeeRate = model.TransactionFeeRate};
            return Json(_reportService.GenerateReport(account, dailyPrices.Where(p => p.Date >= model.StartDate && p.Date <= model.EndDate).ToList()));
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
