using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages.Admin
{
    public class ClinicRow
    {
        public int ClinicId { get; set; }
        public string ClinicName { get; set; } = "";
        public string District { get; set; } = "";
        public string Sector { get; set; } = "";
        public string Cell { get; set; } = "";
        public string Village { get; set; } = "";
        public int VetCount { get; set; }
        public string? VetNames { get; set; }
    }

    public class ClinicInput
    {
        public int ClinicId { get; set; }   // 0 = new clinic

        [Required, StringLength(100)]
        [Display(Name = "Clinic name")]
        public string ClinicName { get; set; } = "";

        [Required, StringLength(100)]
        public string District { get; set; } = "";

        [Required, StringLength(100)]
        public string Sector { get; set; } = "";

        [Required, StringLength(100)]
        public string Cell { get; set; } = "";

        [Required, StringLength(100)]
        public string Village { get; set; } = "";
    }

    [Authorize(Roles = "Admin")]
    public class ClinicsModel : PageModel
    {
        private readonly string _connString;

        public ClinicsModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty]
        public ClinicInput Input { get; set; } = new();

        public List<ClinicRow> Clinics { get; set; } = new();
        public bool ShowModal { get; set; }

        public void OnGet()
        {
            LoadClinics();
        }

        // ---------- Add / Update ----------
        public IActionResult OnPostSave()
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                // Same name in the same district would be a duplicate
                if (ModelState.IsValid && DuplicateExists(conn))
                    ModelState.AddModelError("Input.ClinicName",
                        "A clinic with this name already exists in this district.");

                if (!ModelState.IsValid)
                {
                    LoadClinics();
                    ShowModal = true;
                    return Page();
                }

                if (Input.ClinicId == 0)
                {
                    const string sql = @"
                        INSERT INTO Clinics (clinic_name, district, sector, cell, village)
                        VALUES (@Name, @District, @Sector, @Cell, @Village)";
                    using var cmd = new SqlCommand(sql, conn);
                    AddParams(cmd);
                    cmd.ExecuteNonQuery();
                    TempData["SuccessMessage"] = "Clinic added.";
                }
                else
                {
                    const string sql = @"
                        UPDATE Clinics
                        SET clinic_name = @Name, district = @District, sector = @Sector,
                            cell = @Cell, village = @Village
                        WHERE clinic_ID = @ClinicId";
                    using var cmd = new SqlCommand(sql, conn);
                    AddParams(cmd);
                    cmd.Parameters.AddWithValue("@ClinicId", Input.ClinicId);

                    if (cmd.ExecuteNonQuery() == 0)
                        TempData["ErrorMessage"] = "Clinic not found.";
                    else
                        TempData["SuccessMessage"] = "Clinic updated.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Clinic save exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while saving the clinic.";
            }

            return RedirectToPage(new { Search });
        }

        // ---------- Delete ----------
        public IActionResult OnPostDelete(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                // Users.clinic_ID references Clinics, so a clinic with vets cannot be deleted
                using (var check = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE clinic_ID = @Id", conn))
                {
                    check.Parameters.AddWithValue("@Id", id);
                    if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    {
                        TempData["ErrorMessage"] =
                            "This clinic still has veterinarians assigned to it. Move them to another clinic first.";
                        return RedirectToPage(new { Search });
                    }
                }

                using var cmd = new SqlCommand("DELETE FROM Clinics WHERE clinic_ID = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);

                if (cmd.ExecuteNonQuery() == 0)
                    TempData["ErrorMessage"] = "Clinic not found.";
                else
                    TempData["SuccessMessage"] = "Clinic deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Clinic delete exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while deleting the clinic.";
            }

            return RedirectToPage(new { Search });
        }

        // ---------- helpers ----------
        private void LoadClinics()
        {
            Clinics.Clear();

            using var conn = new SqlConnection(_connString);
            conn.Open();

            const string sql = @"
                SELECT c.clinic_ID, c.clinic_name, c.district, c.sector, c.cell, c.village,
                       COUNT(u.user_ID) AS vet_count,
                       STRING_AGG(u.full_name, ', ') WITHIN GROUP (ORDER BY u.full_name) AS vet_names
                FROM Clinics c
                LEFT JOIN Users u ON u.clinic_ID = c.clinic_ID AND u.role = 'Veterinarian'
                WHERE @Search = ''
                   OR c.clinic_name LIKE @Like
                   OR c.district LIKE @Like
                   OR c.sector LIKE @Like
                GROUP BY c.clinic_ID, c.clinic_name, c.district, c.sector, c.cell, c.village
                ORDER BY c.district, c.clinic_name";

            using var cmd = new SqlCommand(sql, conn);
            var search = (Search ?? "").Trim();
            cmd.Parameters.AddWithValue("@Search", search);
            cmd.Parameters.AddWithValue("@Like", "%" + search + "%");

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Clinics.Add(new ClinicRow
                {
                    ClinicId = r.GetInt32(0),
                    ClinicName = r.GetString(1),
                    District = r.GetString(2),
                    Sector = r.GetString(3),
                    Cell = r.GetString(4),
                    Village = r.GetString(5),
                    VetCount = r.GetInt32(6),
                    VetNames = r.IsDBNull(7) ? null : r.GetString(7)
                });
            }
        }

        private void AddParams(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@Name", Input.ClinicName.Trim());
            cmd.Parameters.AddWithValue("@District", Input.District.Trim());
            cmd.Parameters.AddWithValue("@Sector", Input.Sector.Trim());
            cmd.Parameters.AddWithValue("@Cell", Input.Cell.Trim());
            cmd.Parameters.AddWithValue("@Village", Input.Village.Trim());
        }

        private bool DuplicateExists(SqlConnection conn)
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM Clinics
                WHERE clinic_name = @Name AND district = @District AND clinic_ID <> @Id", conn);
            cmd.Parameters.AddWithValue("@Name", Input.ClinicName.Trim());
            cmd.Parameters.AddWithValue("@District", Input.District.Trim());
            cmd.Parameters.AddWithValue("@Id", Input.ClinicId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }
}
