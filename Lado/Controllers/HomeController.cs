using Microsoft.AspNetCore.Mvc;

namespace Lado.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // MOSTRAR LANDING PAGE A TODOS (autenticados o no)
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Cookies()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }
    }
}