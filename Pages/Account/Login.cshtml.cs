using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Livestock_Health_App.Pages.Account
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public void OnGet()
        {
        }

        public void OnPost()
        {
        }
    }
}
