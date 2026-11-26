using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly string _connString;

        public LoginModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        [BindProperty]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                int userId;
                string fullName, role, passwordHash;

                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();
                    const string sql =
                        "SELECT user_ID, full_name, role, passwordHash FROM Users WHERE email = @Email";

                    using var command = new SqlCommand(sql, conn);
                    command.Parameters.AddWithValue("@Email", Email.Trim());

                    using var reader = command.ExecuteReader();
                    if (!reader.Read())
                    {
                        ModelState.AddModelError(string.Empty, "Invalid email or password.");
                        return Page();
                    }

                    userId = reader.GetInt32(0);
                    fullName = reader.GetString(1);
                    role = reader.GetString(2);
                    passwordHash = reader.GetString(3);
                }

                if (!BCrypt.Net.BCrypt.Verify(Password, passwordHash))
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return Page();
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, userId.ToString()),
                    new(ClaimTypes.Name, fullName),
                    new(ClaimTypes.Role, role)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity));

                TempData["SuccessMessage"] = "Login successful.";
                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex);
                ModelState.AddModelError(string.Empty, "An error occurred while logging in.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login");
        }
    }
}