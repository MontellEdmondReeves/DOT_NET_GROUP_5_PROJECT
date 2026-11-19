using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages
{
    public class AppointmentRow
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "";
        public string TagNumber { get; set; } = "";
        public string Species { get; set; } = "";
        public string PersonName { get; set; } = ""; // vet name (farmer view) or farmer name (vet view)
    }

    public class VaccinationRow
    {
        public int AnimalId { get; set; }
        public string TagNumber { get; set; } = "";
        public string Species { get; set; } = "";
        public string VaccineName { get; set; } = "";
        public DateTime DueDate { get; set; }
    }

    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly string _connString;

        public IndexModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public string? ErrorMessage { get; set; }

        // Farmer
        public int FarmCount { get; set; }
        public int AnimalCount { get; set; }

        // Vet
        public int PendingRequests { get; set; }

        // Admin
        public int TotalUsers { get; set; }
        public int FarmerCount { get; set; }
        public int VetCount { get; set; }
        public int ClinicCount { get; set; }
        public int TotalFarms { get; set; }
        public int TotalAnimals { get; set; }

        // Lists (farmer: upcoming appointments; vet: today's appointments)
        public List<AppointmentRow> Appointments { get; set; } = new();
        public List<VaccinationRow> DueVaccinations { get; set; } = new();

        // Vaccinations due within the next 14 days, or already overdue.
        // A later dose of the same vaccine for the same animal supersedes an older due date.
        private const string DueFilter = @"
            vc.next_due_date IS NOT NULL
            AND vc.next_due_date <= DATEADD(day, 14, CAST(GETDATE() AS date))
            AND NOT EXISTS (
                SELECT 1 FROM Vaccinations v2
                WHERE v2.animal_ID = vc.animal_ID
                  AND v2.vaccine_name = vc.vaccine_name
                  AND v2.vaccination_date > vc.vaccination_date)";

        public void OnGet()
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            FullName = User.Identity?.Name ?? "";
            Role = User.FindFirstValue(ClaimTypes.Role) ?? "";

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                switch (Role)
                {
                    case "Farmer": LoadFarmer(conn, userId); break;
                    case "Veterinarian": LoadVet(conn, userId); break;
                    case "Admin": LoadAdmin(conn); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Dashboard exception: " + ex);
                ErrorMessage = "Could not load dashboard data.";
            }
        }

        private void LoadFarmer(SqlConnection conn, int userId)
        {
            FarmCount = Scalar(conn,
                "SELECT COUNT(*) FROM Farms WHERE farmer_ID = @UserId", userId);

            AnimalCount = Scalar(conn, @"
                SELECT COUNT(*) FROM Animals an
                JOIN Farms f ON f.farm_ID = an.farm_ID
                WHERE f.farmer_ID = @UserId", userId);

            Appointments = ReadAppointments(conn, @"
                SELECT TOP 5 ap.appointment_ID, ap.appointment_date, ap.status,
                       an.tag_number, an.species, vet.full_name
                FROM Appointments ap
                JOIN Animals an ON an.animal_ID = ap.animal_ID
                JOIN Farms f ON f.farm_ID = an.farm_ID
                JOIN Users vet ON vet.user_ID = ap.veterinarian_ID
                WHERE f.farmer_ID = @UserId
                  AND ap.appointment_date >= GETDATE()
                  AND ap.status NOT IN ('Rejected', 'Cancelled', 'Completed')
                ORDER BY ap.appointment_date", userId);

            DueVaccinations = ReadVaccinations(conn, @"
                SELECT TOP 10 an.animal_ID, an.tag_number, an.species, vc.vaccine_name, vc.next_due_date
                FROM Vaccinations vc
                JOIN Animals an ON an.animal_ID = vc.animal_ID
                JOIN Farms f ON f.farm_ID = an.farm_ID
                WHERE f.farmer_ID = @UserId AND " + DueFilter + @"
                ORDER BY vc.next_due_date", userId);
        }

        private void LoadVet(SqlConnection conn, int userId)
        {
            PendingRequests = Scalar(conn, @"
                SELECT COUNT(*) FROM Appointments
                WHERE veterinarian_ID = @UserId AND status = 'Pending'", userId);

            Appointments = ReadAppointments(conn, @"
                SELECT ap.appointment_ID, ap.appointment_date, ap.status,
                       an.tag_number, an.species, farmer.full_name
                FROM Appointments ap
                JOIN Animals an ON an.animal_ID = ap.animal_ID
                JOIN Farms f ON f.farm_ID = an.farm_ID
                JOIN Users farmer ON farmer.user_ID = f.farmer_ID
                WHERE ap.veterinarian_ID = @UserId
                  AND CAST(ap.appointment_date AS date) = CAST(GETDATE() AS date)
                  AND ap.status NOT IN ('Rejected', 'Cancelled')
                ORDER BY ap.appointment_date", userId);

            // Follow-ups for animals this vet has had appointments with
            DueVaccinations = ReadVaccinations(conn, @"
                SELECT TOP 10 an.animal_ID, an.tag_number, an.species, vc.vaccine_name, vc.next_due_date
                FROM Vaccinations vc
                JOIN Animals an ON an.animal_ID = vc.animal_ID
                WHERE EXISTS (SELECT 1 FROM Appointments ap
                              WHERE ap.animal_ID = vc.animal_ID AND ap.veterinarian_ID = @UserId)
                  AND " + DueFilter + @"
                ORDER BY vc.next_due_date", userId);
        }

        private void LoadAdmin(SqlConnection conn)
        {
            TotalUsers = Scalar(conn, "SELECT COUNT(*) FROM Users");
            FarmerCount = Scalar(conn, "SELECT COUNT(*) FROM Users WHERE role = 'Farmer'");
            VetCount = Scalar(conn, "SELECT COUNT(*) FROM Users WHERE role = 'Veterinarian'");
            ClinicCount = Scalar(conn, "SELECT COUNT(*) FROM Clinics");
            TotalFarms = Scalar(conn, "SELECT COUNT(*) FROM Farms");
            TotalAnimals = Scalar(conn, "SELECT COUNT(*) FROM Animals");
        }

        // ---------- helpers ----------

        private static int Scalar(SqlConnection conn, string sql, int userId = 0)
        {
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static List<AppointmentRow> ReadAppointments(SqlConnection conn, string sql, int userId)
        {
            var list = new List<AppointmentRow>();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new AppointmentRow
                {
                    Id = r.GetInt32(0),
                    Date = r.GetDateTime(1),
                    Status = r.GetString(2),
                    TagNumber = r.GetString(3),
                    Species = r.GetString(4),
                    PersonName = r.GetString(5)
                });
            }
            return list;
        }

        private static List<VaccinationRow> ReadVaccinations(SqlConnection conn, string sql, int userId)
        {
            var list = new List<VaccinationRow>();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new VaccinationRow
                {
                    AnimalId = r.GetInt32(0),
                    TagNumber = r.GetString(1),
                    Species = r.GetString(2),
                    VaccineName = r.GetString(3),
                    DueDate = r.GetDateTime(4)
                });
            }
            return list;
        }
    }
}