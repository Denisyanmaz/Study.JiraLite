using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JiraLite.Web.Pages
{
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGet()
        {
            await HttpContext.SignOutAsync("Cookies");   // ✅ clears auth cookie
            Response.Cookies.Delete("jiralite_jwt");
            Response.Cookies.Delete("jiralite_email");
            return RedirectToPage("/Index");
        }


        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync("Cookies");
            Response.Cookies.Delete("jiralite_jwt");
            Response.Cookies.Delete("jiralite_email");

            return RedirectToPage("/Index");
        }

    }
}
