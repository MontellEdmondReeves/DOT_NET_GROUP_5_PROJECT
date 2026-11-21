using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages.Appointments
{
    public class AppointmentItem
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "";
        public string? Detail { get; set; }

        public int AnimalId { get; set; }
        public string TagNumber { get; set; } = "";
        public string Species { get; set; } = "";

        // shown to the vet
        public string FarmerName { get; set; } = "";
        public string? FarmerPhone { get; set; }
        public string FarmName { get; set; } = "";
        public string District { get; set; } = "";

        // shown to the farmer
        public string VetName { get; set; } = "";
        public string? VetPhone { get; set; }
        public string? ClinicName { get; set; }
    }

    [Authorize(Roles = "Farmer,Veterinarian")]
    public class IndexModel : PageModel
    {
        public static readonly string[] Statuses =
            { "Pending", "Accepted", "Completed", "Rejected", "Cancelled" };

        private readonly string _connString;

        public IndexModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        // "All" or one of Statuses. Vets default to Pending, farmers to All.
        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public List<AppointmentItem> Appointments { get; set; } = new();
        public Dictionary<string, int> StatusCounts { get; set; } = new();
        public int TotalCount => StatusCounts.Values.Sum();

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        public bool IsFarmer => User.IsInRole("Farmer");
        public bool IsVet => User.IsInRole("Veterinarian");

        // Which appointments belong to the logged-in user (no user input is concatenated here)
        private string ScopeSql => IsVet
            ? "ap.veterinarian_ID = @UserId"
            : @"ap.animal_ID IN (SELECT a.animal_ID FROM Animals a
                                 JOIN Farms fm ON fm.farm_ID = a.farm_ID
                                 WHERE fm.farmer_ID = @UserId)";

        public void OnGet()
        {
            NormalizeFilter();
            LoadPage();
        }

        // ---------- Farmer: cancel a pending request ----------
        public IActionResult OnPostCancel(int id)
        {
            if (!IsFarmer) return Forbid();
            return ChangeStatus(id, "Cancelled", "Pending",
                "Appointment cancelled.",
                "This appointment can no longer be cancelled.");
        }

        // ---------- Vet: accept / reject / complete ----------
        public IActionResult OnPostAccept(int id)
        {
            if (!IsVet) return Forbid();
            return ChangeStatus(id, "Accepted", "Pending",
                "Appointment accepted.",
                "Could not accept: it was already handled, or its date has passed.",
                futureOnly: true);
        }

        public IActionResult OnPostReject(int id)
        {
            if (!IsVet) return Forbid();
            return ChangeStatus(id, "Rejected", "Pending",
                "Appointment rejected.",
                "This appointment was already handled.");
        }

        public IActionResult OnPostComplete(int id)
        {
            if (!IsVet) return Forbid();
            return ChangeStatus(id, "Completed", "Accepted",
                "Appointment marked as completed.",
                "Only accepted appointments can be completed.");
        }

        // ---------- helpers ----------
        // The WHERE clause checks the current status and ownership, so a request that
        // is stale or belongs to someone else changes nothing (0 rows).
        private IActionResult ChangeStatus(int id, string newStatus, string requiredStatus,
                                           string okMessage, string failMessage, bool futureOnly = false)
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                var sql = $@"
                    UPDATE ap SET ap.status = @New
                    FROM Appointments ap
                    WHERE ap.appointment_ID = @Id
                      AND ap.status = @Required
                      AND {ScopeSql}"
                    + (futureOnly ? " AND ap.appointment_date >= GETDATE()" : "");

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@New", newStatus);
                cmd.Parameters.AddWithValue("@Required", requiredStatus);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@UserId", UserId);

                if (cmd.ExecuteNonQuery() == 0)
                    TempData["ErrorMessage"] = failMessage;
                else
                    TempData["SuccessMessage"] = okMessage;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Appointment status exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while updating the appointment.";
            }

            return RedirectToPage(new { StatusFilter });
        }

        private void NormalizeFilter()
        {
            if (string.IsNullOrEmpty(StatusFilter) ||
                (StatusFilter != "All" && !Statuses.Contains(StatusFilter)))
            {
                StatusFilter = IsVet ? "Pending" : "All";
            }
        }

        private void LoadPage()
        {
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                LoadCounts(conn);
                LoadAppointments(conn);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Load appointments exception: " + ex);
                TempData["ErrorMessage"] = "Could not load appointments.";
            }
        }

        private void LoadCounts(SqlConnection conn)
        {
            var sql = $"SELECT ap.status, COUNT(*) FROM Appointments ap WHERE {ScopeSql} GROUP BY ap.status";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", UserId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) StatusCounts[r.GetString(0)] = r.GetInt32(1);
        }

        private void LoadAppointments(SqlConnection conn)
        {
            // Pending first, then accepted (soonest first), then everything else (newest first)
            var sql = $@"
                SELECT ap.appointment_ID, ap.appointment_date, ap.status, ap.detail,
                       an.animal_ID, an.tag_number, an.species,
                       farmer.full_name, farmer.phone, f.farm_name, f.district,
                       vet.full_name, vet.phone, c.clinic_name
                FROM Appointments ap
                JOIN Animals an ON an.animal_ID = ap.animal_ID
                JOIN Farms f ON f.farm_ID = an.farm_ID
                JOIN Users farmer ON farmer.user_ID = f.farmer_ID
                JOIN Users vet ON vet.user_ID = ap.veterinarian_ID
                LEFT JOIN Clinics c ON c.clinic_ID = vet.clinic_ID
                WHERE {ScopeSql}
                  AND (@Status = '' OR ap.status = @Status)
                ORDER BY CASE ap.status WHEN 'Pending' THEN 0 WHEN 'Accepted' THEN 1 ELSE 2 END,
                         CASE WHEN ap.status IN ('Pending', 'Accepted') THEN ap.appointment_date END ASC,
                         ap.appointment_date DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", UserId);
            cmd.Parameters.AddWithValue("@Status", StatusFilter == "All" ? "" : StatusFilter!);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Appointments.Add(new AppointmentItem
                {
                    Id = r.GetInt32(0),
                    Date = r.GetDateTime(1),
                    Status = r.GetString(2),
                    Detail = r.IsDBNull(3) ? null : r.GetString(3),
                    AnimalId = r.GetInt32(4),
                    TagNumber = r.GetString(5),
                    Species = r.GetString(6),
                    FarmerName = r.GetString(7),
                    FarmerPhone = r.IsDBNull(8) ? null : r.GetString(8),
                    FarmName = r.GetString(9),
                    District = r.GetString(10),
                    VetName = r.GetString(11),
                    VetPhone = r.IsDBNull(12) ? null : r.GetString(12),
                    ClinicName = r.IsDBNull(13) ? null : r.GetString(13)
                });
            }
        }
    }
}