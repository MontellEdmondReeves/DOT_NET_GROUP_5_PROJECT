using System.Data;
using System.Data.SqlClient;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages.Reports
{
    public class BreakdownItem
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
    }

    public class CaseRow
    {
        public DateTime Date { get; set; }
        public int AnimalId { get; set; }
        public string TagNumber { get; set; } = "";
        public string Species { get; set; } = "";
        public string District { get; set; } = "";
        public string? Diagnosis { get; set; }
        public string? Treatment { get; set; }
        public string? CaseStatus { get; set; }
        public string? VetName { get; set; }
    }

    [Authorize(Roles = "Veterinarian,Admin")]
    public class IndexModel : PageModel
    {
        public static readonly string[] SpeciesList = { "Cattle", "Goat", "Sheep", "Pig", "Poultry" };
        public static readonly string[] CaseStatuses = { "Open", "Under Treatment", "Resolved" };
        private const int MaxListed = 200;

        // ---- shared SQL pieces (built only from constants, never from user input) ----
        private const string HrFrom = @"
            FROM HealthRecords hr
            JOIN Animals an ON an.animal_ID = hr.animal_ID
            JOIN Farms f ON f.farm_ID = an.farm_ID";

        private const string HrWhere = @"
            (@IsAdmin = 1 OR hr.veterinarian_ID = @UserId)
            AND (@From IS NULL OR hr.record_date >= @From)
            AND (@To IS NULL OR hr.record_date < DATEADD(day, 1, @To))
            AND (@Species = '' OR an.species = @Species)
            AND (@District = '' OR f.district = @District)
            AND (@Diagnosis = '' OR hr.diagnosis LIKE @DiagLike)
            AND (@Status = '' OR hr.case_status = @Status)";

        private const string VcFrom = @"
            FROM Vaccinations vc
            JOIN Animals an ON an.animal_ID = vc.animal_ID
            JOIN Farms f ON f.farm_ID = an.farm_ID";

        // A vet's vaccination scope = animals they have treated or had an appointment with
        private const string VcWhere = @"
            (@IsAdmin = 1
              OR EXISTS (SELECT 1 FROM HealthRecords h WHERE h.animal_ID = vc.animal_ID AND h.veterinarian_ID = @UserId)
              OR EXISTS (SELECT 1 FROM Appointments p WHERE p.animal_ID = vc.animal_ID AND p.veterinarian_ID = @UserId))
            AND (@From IS NULL OR vc.vaccination_date >= @From)
            AND (@To IS NULL OR vc.vaccination_date <= @To)
            AND (@Species = '' OR an.species = @Species)
            AND (@District = '' OR f.district = @District)";

        private const string ApFrom = @"
            FROM Appointments ap
            JOIN Animals an ON an.animal_ID = ap.animal_ID
            JOIN Farms f ON f.farm_ID = an.farm_ID";

        private const string ApWhere = @"
            (@IsAdmin = 1 OR ap.veterinarian_ID = @UserId)
            AND (@From IS NULL OR ap.appointment_date >= @From)
            AND (@To IS NULL OR ap.appointment_date < DATEADD(day, 1, @To))
            AND (@Species = '' OR an.species = @Species)
            AND (@District = '' OR f.district = @District)";

        private readonly string _connString;

        public IndexModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        // ---- filters ----
        [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
        [BindProperty(SupportsGet = true)] public string? Species { get; set; }
        [BindProperty(SupportsGet = true)] public string? District { get; set; }
        [BindProperty(SupportsGet = true)] public string? Diagnosis { get; set; }
        [BindProperty(SupportsGet = true)] public string? Status { get; set; }

        // ---- results ----
        public int TotalCases { get; set; }
        public int AnimalsAffected { get; set; }
        public int OpenCases { get; set; }
        public int UnderTreatment { get; set; }
        public int ResolvedCases { get; set; }

        public int VaccinationsGiven { get; set; }
        public int AnimalsVaccinated { get; set; }
        public int OverdueVaccinations { get; set; }

        public int TotalAppointments { get; set; }

        public List<BreakdownItem> BySpecies { get; set; } = new();
        public List<BreakdownItem> ByDiagnosis { get; set; } = new();
        public List<BreakdownItem> ByDistrict { get; set; } = new();
        public List<BreakdownItem> ByStatus { get; set; } = new();
        public List<BreakdownItem> ByVet { get; set; } = new();
        public List<BreakdownItem> ByVaccine { get; set; } = new();
        public List<BreakdownItem> ByAppointmentStatus { get; set; } = new();

        public List<CaseRow> Cases { get; set; } = new();
        public bool CasesTruncated { get; set; }
        public List<string> DistrictOptions { get; set; } = new();

        public bool IsAdmin => User.IsInRole("Admin");
        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public void OnGet()
        {
            NormalizeFilters();
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                LoadDistricts(conn);
                LoadHealthSummary(conn);
                LoadBreakdowns(conn);
                LoadVaccinationStats(conn);
                LoadCases(conn, MaxListed + 1);
                if (Cases.Count > MaxListed)
                {
                    CasesTruncated = true;
                    Cases.RemoveAt(Cases.Count - 1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Reports exception: " + ex);
                TempData["ErrorMessage"] = "Could not generate the report.";
            }
        }

        // ---------- CSV export of the case list (all rows matching the filters) ----------
        public IActionResult OnGetExport()
        {
            NormalizeFilters();
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                LoadCases(conn, int.MaxValue);

                var sb = new StringBuilder();
                sb.AppendLine("Date,Tag number,Species,District,Diagnosis,Treatment,Case status,Veterinarian");
                foreach (var c in Cases)
                {
                    sb.AppendLine(string.Join(",",
                        Csv(c.Date.ToString("yyyy-MM-dd")), Csv(c.TagNumber), Csv(c.Species), Csv(c.District),
                        Csv(c.Diagnosis), Csv(c.Treatment), Csv(c.CaseStatus), Csv(c.VetName)));
                }

                // BOM so Excel opens the file as UTF-8
                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
                return File(bytes, "text/csv", $"health-report-{DateTime.Today:yyyyMMdd}.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Report export exception: " + ex);
                TempData["ErrorMessage"] = "Could not export the report.";
                return RedirectToPage();
            }
        }

        // ---------- helpers ----------
        private void NormalizeFilters()
        {
            if (From.HasValue && To.HasValue && From > To) (From, To) = (To, From);
            if (!SpeciesList.Contains(Species ?? "")) Species = "";
            if (!CaseStatuses.Contains(Status ?? "")) Status = "";
            District = District?.Trim() ?? "";
            Diagnosis = Diagnosis?.Trim() ?? "";
        }

        private void AddFilters(SqlCommand cmd)
        {
            cmd.Parameters.Add("@IsAdmin", SqlDbType.Bit).Value = IsAdmin;
            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = UserId;
            cmd.Parameters.Add("@From", SqlDbType.DateTime).Value = (object?)From?.Date ?? DBNull.Value;
            cmd.Parameters.Add("@To", SqlDbType.DateTime).Value = (object?)To?.Date ?? DBNull.Value;
            cmd.Parameters.Add("@Species", SqlDbType.NVarChar, 50).Value = Species ?? "";
            cmd.Parameters.Add("@District", SqlDbType.NVarChar, 100).Value = District ?? "";
            cmd.Parameters.Add("@Diagnosis", SqlDbType.NVarChar, 200).Value = Diagnosis ?? "";
            cmd.Parameters.Add("@DiagLike", SqlDbType.NVarChar, 210).Value = "%" + (Diagnosis ?? "") + "%";
            cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = Status ?? "";
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // Stop spreadsheet programs from running cell content as a formula
            if ("=+-@".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void LoadDistricts(SqlConnection conn)
        {
            using var cmd = new SqlCommand("SELECT DISTINCT district FROM Farms WHERE district IS NOT NULL ORDER BY district", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) DistrictOptions.Add(r.GetString(0));
        }

        private void LoadHealthSummary(SqlConnection conn)
        {
            using var cmd = new SqlCommand($"SELECT COUNT(*), COUNT(DISTINCT hr.animal_ID), SUM(CASE WHEN hr.case_status='Open' THEN 1 ELSE 0 END), SUM(CASE WHEN hr.case_status='Under Treatment' THEN 1 ELSE 0 END), SUM(CASE WHEN hr.case_status='Resolved' THEN 1 ELSE 0 END) {HrFrom} WHERE {HrWhere}", conn);
            AddFilters(cmd); using var r = cmd.ExecuteReader();
            if (r.Read()) { TotalCases = r.GetInt32(0); AnimalsAffected = r.GetInt32(1); OpenCases = r.IsDBNull(2) ? 0 : r.GetInt32(2); UnderTreatment = r.IsDBNull(3) ? 0 : r.GetInt32(3); ResolvedCases = r.IsDBNull(4) ? 0 : r.GetInt32(4); }
        }

        private void LoadBreakdowns(SqlConnection conn)
        {
            LoadBreakdown(conn, $"SELECT an.species, COUNT(*) {HrFrom} WHERE {HrWhere} GROUP BY an.species", BySpecies);
            LoadBreakdown(conn, $"SELECT hr.diagnosis, COUNT(*) {HrFrom} WHERE {HrWhere} GROUP BY hr.diagnosis", ByDiagnosis);
            LoadBreakdown(conn, $"SELECT f.district, COUNT(*) {HrFrom} WHERE {HrWhere} GROUP BY f.district", ByDistrict);
            LoadBreakdown(conn, $"SELECT hr.case_status, COUNT(*) {HrFrom} WHERE {HrWhere} GROUP BY hr.case_status", ByStatus);
        }

        private void LoadBreakdown(SqlConnection conn, string sql, List<BreakdownItem> target)
        {
            using var cmd = new SqlCommand(sql, conn); AddFilters(cmd); using var r = cmd.ExecuteReader();
            while (r.Read()) target.Add(new BreakdownItem { Label = r.IsDBNull(0) ? "(none)" : r.GetString(0), Count = r.GetInt32(1) });
        }

        private void LoadVaccinationStats(SqlConnection conn)
        {
            using var cmd = new SqlCommand($"SELECT COUNT(*), COUNT(DISTINCT vc.animal_ID), SUM(CASE WHEN vc.next_due_date < CAST(GETDATE() AS date) THEN 1 ELSE 0 END) {VcFrom} WHERE {VcWhere}", conn);
            AddFilters(cmd); using var r = cmd.ExecuteReader();
            if (r.Read()) { VaccinationsGiven = r.GetInt32(0); AnimalsVaccinated = r.GetInt32(1); OverdueVaccinations = r.IsDBNull(2) ? 0 : r.GetInt32(2); }
        }

        private void LoadCases(SqlConnection conn, int limit)
        {
            using var cmd = new SqlCommand($"SELECT TOP (@Limit) hr.record_date, an.animal_ID, an.tag_number, an.species, f.district, hr.diagnosis, hr.treatment, hr.case_status, u.full_name {HrFrom} LEFT JOIN Users u ON u.user_ID = hr.veterinarian_ID WHERE {HrWhere} ORDER BY hr.record_date DESC", conn);
            cmd.Parameters.AddWithValue("@Limit", limit); AddFilters(cmd); using var r = cmd.ExecuteReader();
            while (r.Read()) Cases.Add(new CaseRow { Date = r.GetDateTime(0), AnimalId = r.GetInt32(1), TagNumber = r.GetString(2), Species = r.GetString(3), District = r.GetString(4), Diagnosis = r.IsDBNull(5) ? null : r.GetString(5), Treatment = r.IsDBNull(6) ? null : r.GetString(6), CaseStatus = r.IsDBNull(7) ? null : r.GetString(7), VetName = r.IsDBNull(8) ? null : r.GetString(8) });
        }
    }
}
