using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Livestock_Health_App.Pages.Admin
{
    public class UserRow
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string Role { get; set; } = "";
        public int? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public bool IsVerified { get; set; }
    }

    public class UserInput
    {
        public int UserId { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [StringLength(20)]
        public string? Phone { get; set; }

        [Display(Name = "Clinic")]
        public int? ClinicId { get; set; }

        // display only (re-read from the database on save)
        public string? Email { get; set; }
        public string? Role { get; set; }
    }

    [Authorize(Roles = "Admin")]
    public class UsersModel : PageModel
    {
        public static readonly string[] Filters = { "All", "Unverified", "Farmer", "Veterinarian", "Admin" };

        private readonly string _connString;

        public UsersModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        [BindProperty(SupportsGet = true)] public string? Filter { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }

        [BindProperty] public UserInput Input { get; set; } = new();

        public List<UserRow> Users { get; set; } = new();
        public List<SelectListItem> ClinicOptions { get; set; } = new();
        public Dictionary<string, int> Counts { get; set; } = new();
        public bool ShowModal { get; set; }

        public int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public void OnGet()
        {
            LoadPage();
        }

        // ---------- Verify / revoke a veterinarian ----------
        public IActionResult OnPostVerify(int id) =>
            SetVerified(id, true, "Veterinarian verified. They now appear in the farmers' vet search.");

        public IActionResult OnPostRevoke(int id) =>
            SetVerified(id, false, "Verification revoked. This veterinarian no longer appears in the vet search.");

        private IActionResult SetVerified(int id, bool verified, string okMessage)
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "UPDATE Users SET is_verified = @V WHERE user_ID = @Id AND role = 'Veterinarian'", conn);
                cmd.Parameters.AddWithValue("@V", verified);
                cmd.Parameters.AddWithValue("@Id", id);
                
                int rows = cmd.ExecuteNonQuery();
                TempData[rows == 0 ? "ErrorMessage" : "SuccessMessage"] =
                    rows == 0 ? "Veterinarian not found." : okMessage;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Verify exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while updating the user.";
            }
            return RedirectToPage(new { Filter, Search });
        }

        // ---------- Edit ----------
        public IActionResult OnPostSave()
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                // The role comes from the database, never from the form
                string? role;
                using (var cmd = new SqlCommand("SELECT role FROM Users WHERE user_ID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", Input.UserId);
                    role = cmd.ExecuteScalar() as string;
                }
                if (role == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToPage(new { Filter, Search });
                }

                // Veterinarians must have a valid clinic (FR05)
                if (role == "Veterinarian")
                {
                    if (Input.ClinicId == null)
                        ModelState.AddModelError("Input.ClinicId", "Please select a clinic for this veterinarian.");
                    else
                    {
                        using var check = new SqlCommand("SELECT COUNT(*) FROM Clinics WHERE clinic_ID = @C", conn);
                        check.Parameters.AddWithValue("@C", Input.ClinicId);
                        if (Convert.ToInt32(check.ExecuteScalar()) == 0)
                            ModelState.AddModelError("Input.ClinicId", "Invalid clinic.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    LoadPage();
                    ShowModal = true;
                    return Page();
                }

                using var upd = new SqlCommand(@"
                    UPDATE Users SET full_name = @Name, phone = @Phone, clinic_ID = @Clinic
                    WHERE user_ID = @Id", conn);
                upd.Parameters.AddWithValue("@Name", Input.FullName.Trim());
                upd.Parameters.AddWithValue("@Phone",
                    string.IsNullOrWhiteSpace(Input.Phone) ? DBNull.Value : Input.Phone.Trim());
                upd.Parameters.AddWithValue("@Clinic",
                    role == "Veterinarian" ? Input.ClinicId!.Value : DBNull.Value);
                upd.Parameters.AddWithValue("@Id", Input.UserId);
                upd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "User updated.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("User save exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while saving the user.";
            }
            return RedirectToPage(new { Filter, Search });
        }

        // ---------- Delete ----------
        public IActionResult OnPostDelete(int id)
        {
            if (id == CurrentUserId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToPage(new { Filter, Search });
            }

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                // Farms, appointments and health records reference users
                using (var check = new SqlCommand(@"
                    SELECT (SELECT COUNT(*) FROM Farms        WHERE farmer_ID = @Id)
                         + (SELECT COUNT(*) FROM Appointments WHERE veterinarian_ID = @Id)
                         + (SELECT COUNT(*) FROM HealthRecords WHERE veterinarian_ID = @Id
                                                                  OR reported_by_user_ID = @Id)", conn))
                {
                    check.Parameters.AddWithValue("@Id", id);
                    if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    {
                        TempData["ErrorMessage"] =
                            "This user has farms, appointments or health records and cannot be deleted.";
                        return RedirectToPage(new { Filter, Search });
                    }
                }

                using var cmd = new SqlCommand("DELETE FROM Users WHERE user_ID = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                TempData[cmd.ExecuteNonQuery() == 0 ? "ErrorMessage" : "SuccessMessage"] =
                    "User deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("User delete exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while deleting the user.";
            }
            return RedirectToPage(new { Filter, Search });
        }

        // ---------- loading ----------
        private void LoadPage()
        {
            if (string.IsNullOrEmpty(Filter) || !Filters.Contains(Filter)) Filter = "All";

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                LoadCounts(conn);
                LoadClinics(conn);
                LoadUsers(conn);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Load users exception: " + ex);
                TempData["ErrorMessage"] = "Could not load users.";
            }
        }

        private void LoadCounts(SqlConnection conn)
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(*),
                       SUM(CASE WHEN role = 'Veterinarian' AND is_verified = 0 THEN 1 ELSE 0 END),
                       SUM(CASE WHEN role = 'Farmer' THEN 1 ELSE 0 END),
                       SUM(CASE WHEN role = 'Veterinarian' THEN 1 ELSE 0 END),
                       SUM(CASE WHEN role = 'Admin' THEN 1 ELSE 0 END)
                FROM Users", conn);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                Counts["All"] = r.GetInt32(0);
                Counts["Unverified"] = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                Counts["Farmer"] = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                Counts["Veterinarian"] = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                Counts["Admin"] = r.IsDBNull(4) ? 0 : r.GetInt32(4);
            }
        }

        private void LoadClinics(SqlConnection conn)
        {
            using var cmd = new SqlCommand(
                "SELECT clinic_ID, clinic_name, district FROM Clinics ORDER BY district, clinic_name", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                ClinicOptions.Add(new SelectListItem(
                    $"{r.GetString(1)} ({r.GetString(2)})", r.GetInt32(0).ToString()));
        }

        private void LoadUsers(SqlConnection conn)
        {
            const string sql = @"
                SELECT u.user_ID, u.full_name, u.email, u.phone, u.role,
                       u.clinic_ID, c.clinic_name, u.is_verified
                FROM Users u
                LEFT JOIN Clinics c ON c.clinic_ID = u.clinic_ID
                WHERE (   @Filter = 'All'
                       OR (@Filter = 'Unverified' AND u.role = 'Veterinarian' AND u.is_verified = 0)
                       OR (@Filter IN ('Farmer', 'Veterinarian', 'Admin') AND u.role = @Filter))
                  AND (@Search = '' OR u.full_name LIKE @Like OR u.email LIKE @Like)
                ORDER BY CASE WHEN u.role = 'Veterinarian' AND u.is_verified = 0 THEN 0 ELSE 1 END,
                         u.role, u.full_name";

            using var cmd = new SqlCommand(sql, conn);
            var search = (Search ?? "").Trim();
            cmd.Parameters.AddWithValue("@Filter", Filter!);
            cmd.Parameters.AddWithValue("@Search", search);
            cmd.Parameters.AddWithValue("@Like", "%" + search + "%");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Users.Add(new UserRow
                {
                    UserId = r.GetInt32(0),
                    FullName = r.GetString(1),
                    Email = r.GetString(2),
                    Phone = r.IsDBNull(3) ? null : r.GetString(3),
                    Role = r.GetString(4),
                    ClinicId = r.IsDBNull(5) ? null : r.GetInt32(5),
                    ClinicName = r.IsDBNull(6) ? null : r.GetString(6),
                    IsVerified = r.GetBoolean(7)
                });
            }
        }
    }
}