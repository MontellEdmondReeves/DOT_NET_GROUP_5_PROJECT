using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Livestock_Health_App.Pages.Vets
{
    public class VetRow
    {
        public int VetId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public string ClinicName { get; set; } = "";
        public string District { get; set; } = "";
        public string Sector { get; set; } = "";
        public string Cell { get; set; } = "";
        public string Village { get; set; } = "";
    }

    public class AppointmentInput
    {
        public int VetId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select an animal.")]
        [Display(Name = "Animal")]
        public int AnimalId { get; set; }

        [Required(ErrorMessage = "Please choose a date and time.")]
        [Display(Name = "Preferred date and time")]
        public DateTime? AppointmentDate { get; set; }

        [Required(ErrorMessage = "Please describe the problem."), StringLength(500)]
        [Display(Name = "Reason for the visit")]
        public string Detail { get; set; } = "";
    }

    [Authorize(Roles = "Farmer")]
    public class IndexModel : PageModel
    {
        private readonly string _connString;

        public IndexModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        // filters (kept across requests)
        [BindProperty(SupportsGet = true)] public string? District { get; set; }
        [BindProperty(SupportsGet = true)] public string? Sector { get; set; }

        [BindProperty] public AppointmentInput Input { get; set; } = new();

        public List<VetRow> Vets { get; set; } = new();
        public List<string> DistrictOptions { get; set; } = new();
        public List<string> SectorOptions { get; set; } = new();
        public List<SelectListItem> AnimalOptions { get; set; } = new();

        public bool ShowModal { get; set; }
        public string SelectedVetName { get; set; } = "";

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public void OnGet()
        {
            LoadPage();
        }

        // ---------- Request an appointment ----------
        public IActionResult OnPostRequest()
        {
            if (Input.AppointmentDate.HasValue && Input.AppointmentDate.Value <= DateTime.Now)
                ModelState.AddModelError("Input.AppointmentDate", "The appointment must be in the future.");

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                if (ModelState.IsValid)
                {
                    // The vet must exist and be a veterinarian
                    if (!Exists(conn,
                        "SELECT COUNT(*) FROM Users WHERE user_ID = @Id AND role = 'Veterinarian'",
                        ("@Id", Input.VetId)))
                    {
                        TempData["ErrorMessage"] = "Veterinarian not found.";
                        return RedirectToPage(new { District, Sector });
                    }

                    // The animal must belong to the logged-in farmer
                    if (!Exists(conn, @"
                        SELECT COUNT(*) FROM Animals a
                        JOIN Farms f ON f.farm_ID = a.farm_ID
                        WHERE a.animal_ID = @Animal AND f.farmer_ID = @User",
                        ("@Animal", Input.AnimalId), ("@User", UserId)))
                    {
                        ModelState.AddModelError("Input.AnimalId", "Invalid animal.");
                    }
                    // No duplicate open request for the same animal and vet
                    else if (Exists(conn, @"
                        SELECT COUNT(*) FROM Appointments
                        WHERE animal_ID = @Animal AND veterinarian_ID = @Vet AND status = 'Pending'",
                        ("@Animal", Input.AnimalId), ("@Vet", Input.VetId)))
                    {
                        ModelState.AddModelError("Input.AnimalId",
                            "You already have a pending request for this animal with this veterinarian.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    LoadPage();
                    ShowModal = true;
                    SelectedVetName = Vets.FirstOrDefault(v => v.VetId == Input.VetId)?.FullName
                                      ?? GetVetName(conn, Input.VetId);
                    return Page();
                }

                const string sql = @"
                    INSERT INTO Appointments (animal_ID, veterinarian_ID, appointment_date, status, detail)
                    VALUES (@Animal, @Vet, @Date, 'Pending', @Detail)";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Animal", Input.AnimalId);
                cmd.Parameters.AddWithValue("@Vet", Input.VetId);
                cmd.Parameters.AddWithValue("@Date", Input.AppointmentDate!.Value);
                cmd.Parameters.AddWithValue("@Detail", Input.Detail.Trim());
                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] =
                    "Appointment requested. The veterinarian will accept or reject it; you can track it on your Appointments page.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Appointment request exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while requesting the appointment.";
            }

            return RedirectToPage(new { District, Sector });
        }

        // ---------- helpers ----------
        private static bool Exists(SqlConnection conn, string sql, params (string name, object value)[] ps)
        {
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static string GetVetName(SqlConnection conn, int vetId)
        {
            using var cmd = new SqlCommand("SELECT full_name FROM Users WHERE user_ID = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", vetId);
            return cmd.ExecuteScalar() as string ?? "";
        }

        private void LoadPage()
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                LoadDistricts(conn);
                if (!string.IsNullOrEmpty(District)) LoadSectors(conn);
                LoadVets(conn);
                LoadAnimals(conn);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Load vets exception: " + ex);
                TempData["ErrorMessage"] = "Could not load veterinarians.";
            }
        }

        private void LoadDistricts(SqlConnection conn)
        {
            // Only districts that actually have a veterinarian
            const string sql = @"
                SELECT DISTINCT c.district
                FROM Clinics c
                JOIN Users u ON u.clinic_ID = c.clinic_ID
                WHERE u.role = 'Veterinarian' AND u.is_verified = 1
                ORDER BY c.district";
            using var cmd = new SqlCommand(sql, conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) DistrictOptions.Add(r.GetString(0));
        }

        private void LoadSectors(SqlConnection conn)
        {
            const string sql = @"
                SELECT DISTINCT c.sector
                FROM Clinics c
                JOIN Users u ON u.clinic_ID = c.clinic_ID
                WHERE u.role = 'Veterinarian' AND u.is_verified = 1 AND c.district = @District
                ORDER BY c.sector";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@District", District!);
            using var r = cmd.ExecuteReader();
            while (r.Read()) SectorOptions.Add(r.GetString(0));
        }

        private void LoadVets(SqlConnection conn)
        {
            const string sql = @"
                SELECT u.user_ID, u.full_name, u.email, u.phone,
                       c.clinic_name, c.district, c.sector, c.cell, c.village
                FROM Users u
                JOIN Clinics c ON c.clinic_ID = u.clinic_ID
                WHERE u.role = 'Veterinarian' AND u.is_verified = 1
                  AND (@District = '' OR c.district = @District)
                  AND (@Sector = '' OR c.sector = @Sector)
                ORDER BY c.district, c.sector, u.full_name";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@District", District ?? "");
            cmd.Parameters.AddWithValue("@Sector", Sector ?? "");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Vets.Add(new VetRow
                {
                    VetId = r.GetInt32(0),
                    FullName = r.GetString(1),
                    Email = r.GetString(2),
                    Phone = r.IsDBNull(3) ? null : r.GetString(3),
                    ClinicName = r.GetString(4),
                    District = r.GetString(5),
                    Sector = r.GetString(6),
                    Cell = r.GetString(7),
                    Village = r.GetString(8)
                });
            }
        }

        private void LoadAnimals(SqlConnection conn)
        {
            const string sql = @"
                SELECT a.animal_ID, a.tag_number, a.species, f.farm_name
                FROM Animals a
                JOIN Farms f ON f.farm_ID = a.farm_ID
                WHERE f.farmer_ID = @UserId
                ORDER BY a.tag_number";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", UserId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                AnimalOptions.Add(new SelectListItem(
                    $"{r.GetString(1)} · {r.GetString(2)} ({r.GetString(3)})",
                    r.GetInt32(0).ToString()));
            }
        }
    }
}