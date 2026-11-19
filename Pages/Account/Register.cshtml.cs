using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;

namespace Livestock_Health_App.Pages.Account
{
    public class RegisterModel : PageModel
    {
        [BindProperty]
        [Required]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        [Required]
        public string Role { get; set; } = string.Empty;

        [BindProperty]
        public string? ClinicId { get; set; }

        public string? RegistrationError { get; private set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {

            if (!ModelState.IsValid)
            {
                RegistrationError = "Registration failed. Please correct the highlighted fields and try again.";
                return Page();
            }

            try
            {
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(Password);
                String connString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=Livestock_Health_DB;Integrated Security=True;Encrypt=False";
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    conn.Open();
                    String sql = "INSERT INTO Users" + "(full_name,email,phone,passwordHash,role,clinic_ID) VALUES" + "(@FullName,@Email,@Phone,@Password,@Role,@ClinicId);";
                    using (SqlCommand command = new SqlCommand(sql, conn))
                    {
                        command.Parameters.AddWithValue("@FullName", FullName);
                        command.Parameters.AddWithValue("@Email", Email);
                        command.Parameters.AddWithValue("@Phone", Phone);
                        command.Parameters.AddWithValue("@Password", passwordHash);
                        command.Parameters.AddWithValue("@Role", Role);
                        command.Parameters.AddWithValue("@ClinicId", Role == "Veterinarian" ? ClinicId : DBNull.Value);
                        command.ExecuteNonQuery();
                    }
                }
                TempData["SuccessMessage"] = "Registration successful. You can now log in.";
                return RedirectToPage("/Account/Login");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex);
                RegistrationError = "Registration failed. Your account was not created. Please verify your information and try again.";
                ModelState.AddModelError(string.Empty, "An error occurred while registering the account.");
                return Page();
            }

        }
    }
}
