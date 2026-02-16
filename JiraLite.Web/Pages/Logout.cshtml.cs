using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JiraLite.Web.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            Response.Cookies.Delete("jiralite_jwt");
            Response.Cookies.Delete("jiralite_email");
            return RedirectToPage("/Login");
        }

        public IActionResult OnPost()
        {
            Response.Cookies.Delete("jiralite_jwt");
            Response.Cookies.Delete("jiralite_email");

            return RedirectToPage("/Login");
        }

    }
}
