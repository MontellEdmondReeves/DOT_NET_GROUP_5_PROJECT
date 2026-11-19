using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages.Farms
{
    public class FarmRow
    {
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string District { get; set; } = "";
        public string Sector { get; set; } = "";
        public string Cell { get; set; } = "";
        public string Village { get; set; } = "";
        public int AnimalCount { get; set; }
    }

    public class FarmInput
    {
        public int FarmId { get; set; }   // 0 = new farm

        [Required, StringLength(100)]
        [Display(Name = "Farm name")]
        public string FarmName { get; set; } = "";

        [Required, StringLength(100)]
        public string District { get; set; } = "";

        [Required, StringLength(100)]
        public string Sector { get; set; } = "";

        [Required, StringLength(100)]
        public string Cell { get; set; } = "";

        [Required, StringLength(100)]
        public string Village { get; set; } = "";
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

        public List<FarmRow> Farms { get; set; } = new();

        [BindProperty]
        public FarmInput Input { get; set; } = new();

        // true when validation fails, so the modal reopens with the errors
        public bool ShowModal { get; set; }

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public void OnGet()
        {
            LoadFarms();
        }

        // ---------- Add / Update ----------
        public IActionResult OnPostSave()
        {
            if (!ModelState.IsValid)
            {
                LoadFarms();
                ShowModal = true;
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                if (Input.FarmId == 0)
                {
                    const string sql = @"
                        INSERT INTO Farms (farmer_ID, farm_name, district, sector, cell, village)
                        VALUES (@UserId, @Name, @District, @Sector, @Cell, @Village)";
                    using var cmd = new SqlCommand(sql, conn);
                    AddParams(cmd);
                    cmd.ExecuteNonQuery();
                    TempData["SuccessMessage"] = "Farm added.";
                }
                else
                {
                    // farmer_ID in the WHERE clause stops users editing other people's farms
                    const string sql = @"
                        UPDATE Farms
                        SET farm_name = @Name, district = @District, sector = @Sector,
                            cell = @Cell, village = @Village
                        WHERE farm_ID = @FarmId AND farmer_ID = @UserId";
                    using var cmd = new SqlCommand(sql, conn);
                    AddParams(cmd);
                    cmd.Parameters.AddWithValue("@FarmId", Input.FarmId);

                    if (cmd.ExecuteNonQuery() == 0)
                        TempData["ErrorMessage"] = "Farm not found.";
                    else
                        TempData["SuccessMessage"] = "Farm updated.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Farm save exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while saving the farm.";
            }

            return RedirectToPage();
        }

        // ---------- Delete ----------
        public IActionResult OnPostDelete(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                // A farm that still has animals cannot be deleted (Animals references Farms)
                const string countSql = @"
                    SELECT COUNT(*) FROM Animals a
                    JOIN Farms f ON f.farm_ID = a.farm_ID
                    WHERE f.farm_ID = @FarmId AND f.farmer_ID = @UserId";
                using (var countCmd = new SqlCommand(countSql, conn))
                {
                    countCmd.Parameters.AddWithValue("@FarmId", id);
                    countCmd.Parameters.AddWithValue("@UserId", UserId);
                    if (Convert.ToInt32(countCmd.ExecuteScalar()) > 0)
                    {
                        TempData["ErrorMessage"] =
                            "This farm still has animals. Remove or move them before deleting the farm.";
                        return RedirectToPage();
                    }
                }

                const string delSql = "DELETE FROM Farms WHERE farm_ID = @FarmId AND farmer_ID = @UserId";
                using var cmd = new SqlCommand(delSql, conn);
                cmd.Parameters.AddWithValue("@FarmId", id);
                cmd.Parameters.AddWithValue("@UserId", UserId);

                if (cmd.ExecuteNonQuery() == 0)
                    TempData["ErrorMessage"] = "Farm not found.";
                else
                    TempData["SuccessMessage"] = "Farm deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Farm delete exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while deleting the farm.";
            }

            return RedirectToPage();
        }

        // ---------- helpers ----------
        private void AddParams(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@UserId", UserId);
            cmd.Parameters.AddWithValue("@Name", Input.FarmName.Trim());
            cmd.Parameters.AddWithValue("@District", Input.District.Trim());
            cmd.Parameters.AddWithValue("@Sector", Input.Sector.Trim());
            cmd.Parameters.AddWithValue("@Cell", Input.Cell.Trim());
            cmd.Parameters.AddWithValue("@Village", Input.Village.Trim());
        }

        private void LoadFarms()
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                const string sql = @"
                    SELECT f.farm_ID, f.farm_name, f.district, f.sector, f.cell, f.village,
                           (SELECT COUNT(*) FROM Animals a WHERE a.farm_ID = f.farm_ID)
                    FROM Farms f
                    WHERE f.farmer_ID = @UserId
                    ORDER BY f.farm_name";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", UserId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    Farms.Add(new FarmRow
                    {
                        FarmId = r.GetInt32(0),
                        FarmName = r.GetString(1),
                        District = r.GetString(2),
                        Sector = r.GetString(3),
                        Cell = r.GetString(4),
                        Village = r.GetString(5),
                        AnimalCount = r.GetInt32(6)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Load farms exception: " + ex);
                TempData["ErrorMessage"] = "Could not load your farms.";
            }
        }
    }
}