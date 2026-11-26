using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages.Animals
{
    public class AnimalInfo
    {
        public int AnimalId { get; set; }
        public string Species { get; set; } = "";
        public string TagNumber { get; set; } = "";
        public string? Breed { get; set; }
        public string? Sex { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string FarmName { get; set; } = "";
        public string District { get; set; } = "";
        public string Sector { get; set; } = "";
        public string Cell { get; set; } = "";
        public string Village { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public string? OwnerPhone { get; set; }
    }

    public class RecordEntry
    {
        public int RecordId { get; set; }
        public DateTime Date { get; set; }
        public string? Diagnosis { get; set; }
        public string? Treatment { get; set; }
        public string? CaseStatus { get; set; }
        public string? Description { get; set; }
        public int? VetId { get; set; }
        public string? VetName { get; set; }
    }

    public class VaccinationEntry
    {
        public int Id { get; set; }
        public string VaccineName { get; set; } = "";
        public DateTime VaccinationDate { get; set; }
        public DateTime? NextDueDate { get; set; }
        public bool IsLatest { get; set; }   // newest dose of this vaccine for this animal
    }

    public class AnimalAppointment
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = "";
        public string? Detail { get; set; }
        public string VetName { get; set; } = "";
    }

    public class RecordInput
    {
        public int RecordId { get; set; }   // 0 = new record

        [Required(ErrorMessage = "Please enter a diagnosis."), StringLength(200)]
        public string Diagnosis { get; set; } = "";

        [StringLength(200)]
        public string? Treatment { get; set; }

        [Display(Name = "Case status")]
        public string CaseStatus { get; set; } = "Open";

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Description { get; set; }
    }

    public class VaccinationInput
    {
        public int VaccinationId { get; set; }   // 0 = new

        [Required(ErrorMessage = "Please enter the vaccine name."), StringLength(100)]
        [Display(Name = "Vaccine")]
        public string VaccineName { get; set; } = "";

        [Required(ErrorMessage = "Please enter the vaccination date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date given")]
        public DateTime? VaccinationDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Next due date")]
        public DateTime? NextDueDate { get; set; }
    }

    [Authorize(Roles = "Farmer,Veterinarian")]
    public class DetailsModel : PageModel
    {
        public static readonly string[] CaseStatuses = { "Open", "Under Treatment", "Resolved" };
        private static readonly string[] Tabs = { "records", "vaccinations", "appointments" };

        private readonly string _connString;

        public DetailsModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        [BindProperty(SupportsGet = true)] public string? Tab { get; set; }

        [BindProperty] public RecordInput Record { get; set; } = new();
        [BindProperty] public VaccinationInput Vaccination { get; set; } = new();

        public AnimalInfo Animal { get; set; } = new();
        public List<RecordEntry> Records { get; set; } = new();
        public List<VaccinationEntry> Vaccinations { get; set; } = new();
        public List<AnimalAppointment> Appointments { get; set; } = new();

        public bool ShowRecordModal { get; set; }
        public bool ShowVaccModal { get; set; }

        public int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        public bool IsVet => User.IsInRole("Veterinarian");
        public bool IsFarmer => User.IsInRole("Farmer");

        public IActionResult OnGet(int id)
        {
            if (!LoadPage(id)) return RedirectToPage("Index");
            return Page();
        }

        // ---------- Vet: add / edit a health record ----------
        public IActionResult OnPostSaveRecord(int id)
        {
            if (!IsVet) return Forbid();

            // Both forms are bound on every post; ignore the one that wasn't submitted
            ModelState.ClearValidationState(nameof(Vaccination));
            if (!ModelState.IsValid) return Reshow(id, "records", recordModal: true);

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                if (!AnimalExists(conn, id))
                {
                    TempData["ErrorMessage"] = "Animal not found.";
                    return RedirectToPage("Index");
                }

                if (Record.RecordId == 0)
                {
                    const string sql = @"
                        INSERT INTO HealthRecords
                            (animal_ID, veterinarian_ID, diagnosis, treatment, reported_by_user_ID, case_status, description)
                        VALUES (@Animal, @Vet, @Diagnosis, @Treatment, @Vet, @Status, @Description)";
                    using var cmd = new SqlCommand(sql, conn);
                    AddRecordParams(cmd, id);
                    cmd.ExecuteNonQuery();
                    TempData["SuccessMessage"] = "Health record added.";
                }
                else
                {
                    // A vet can only edit records they created
                    const string sql = @"
                        UPDATE HealthRecords
                        SET diagnosis = @Diagnosis, treatment = @Treatment,
                            case_status = @Status, description = @Description
                        WHERE record_ID = @RecordId AND animal_ID = @Animal AND veterinarian_ID = @Vet";
                    using var cmd = new SqlCommand(sql, conn);
                    AddRecordParams(cmd, id);
                    cmd.Parameters.AddWithValue("@RecordId", Record.RecordId);

                    if (cmd.ExecuteNonQuery() == 0)
                        TempData["ErrorMessage"] = "Record not found, or it was created by another veterinarian.";
                    else
                        TempData["SuccessMessage"] = "Health record updated.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Record save exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while saving the record.";
            }

            return RedirectToPage(new { id, tab = "records" });
        }

        // ---------- Vet: add / edit a vaccination ----------
        public IActionResult OnPostSaveVaccination(int id)
        {
            if (!IsVet) return Forbid();

            ModelState.ClearValidationState(nameof(Record));

            if (Vaccination.VaccinationDate.HasValue && Vaccination.VaccinationDate.Value.Date > DateTime.Today)
                ModelState.AddModelError("Vaccination.VaccinationDate", "The date given cannot be in the future.");
            if (Vaccination.VaccinationDate.HasValue && Vaccination.NextDueDate.HasValue &&
                Vaccination.NextDueDate.Value.Date <= Vaccination.VaccinationDate.Value.Date)
                ModelState.AddModelError("Vaccination.NextDueDate", "The next due date must be after the date given.");

            if (!ModelState.IsValid) return Reshow(id, "vaccinations", recordModal: false);

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                if (!AnimalExists(conn, id))
                {
                    TempData["ErrorMessage"] = "Animal not found.";
                    return RedirectToPage("Index");
                }

                if (Vaccination.VaccinationId == 0)
                {
                    const string sql = @"
                        INSERT INTO Vaccinations (animal_ID, vaccine_name, vaccination_date, next_due_date)
                        VALUES (@Animal, @Vaccine, @Date, @Next)";
                    using var cmd = new SqlCommand(sql, conn);
                    AddVaccinationParams(cmd, id);
                    cmd.ExecuteNonQuery();
                    TempData["SuccessMessage"] = "Vaccination registered.";
                }
                else
                {
                    const string sql = @"
                        UPDATE Vaccinations
                        SET vaccine_name = @Vaccine, vaccination_date = @Date, next_due_date = @Next
                        WHERE vaccination_ID = @VaccId AND animal_ID = @Animal";
                    using var cmd = new SqlCommand(sql, conn);
                    AddVaccinationParams(cmd, id);
                    cmd.Parameters.AddWithValue("@VaccId", Vaccination.VaccinationId);

                    if (cmd.ExecuteNonQuery() == 0)
                        TempData["ErrorMessage"] = "Vaccination not found.";
                    else
                        TempData["SuccessMessage"] = "Vaccination updated.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Vaccination save exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while saving the vaccination.";
            }

            return RedirectToPage(new { id, tab = "vaccinations" });
        }

        // ---------- Vet: delete a vaccination (to correct a mistake) ----------
        public IActionResult OnPostDeleteVaccination(int id, int vaccinationId)
        {
            if (!IsVet) return Forbid();

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "DELETE FROM Vaccinations WHERE vaccination_ID = @V AND animal_ID = @A", conn);
                cmd.Parameters.AddWithValue("@V", vaccinationId);
                cmd.Parameters.AddWithValue("@A", id);

                if (cmd.ExecuteNonQuery() == 0)
                    TempData["ErrorMessage"] = "Vaccination not found.";
                else
                    TempData["SuccessMessage"] = "Vaccination deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Vaccination delete exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while deleting the vaccination.";
            }

            return RedirectToPage(new { id, tab = "vaccinations" });
        }

        // ---------- helpers ----------
        private IActionResult Reshow(int id, string tab, bool recordModal)
        {
            Tab = tab;
            if (!LoadPage(id)) return RedirectToPage("Index");
            ShowRecordModal = recordModal;
            ShowVaccModal = !recordModal;
            return Page();
        }

        private static object Db(string? s) =>
            string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

        private void AddRecordParams(SqlCommand cmd, int animalId)
        {
            cmd.Parameters.AddWithValue("@Animal", animalId);
            cmd.Parameters.AddWithValue("@Vet", CurrentUserId);
            cmd.Parameters.AddWithValue("@Diagnosis", Record.Diagnosis.Trim());
            cmd.Parameters.AddWithValue("@Treatment", Db(Record.Treatment));
            cmd.Parameters.AddWithValue("@Status", Db(Record.CaseStatus));
            cmd.Parameters.AddWithValue("@Description", Db(Record.Description));
        }

        private void AddVaccinationParams(SqlCommand cmd, int animalId)
        {
            cmd.Parameters.AddWithValue("@Animal", animalId);
            cmd.Parameters.AddWithValue("@Vaccine", Vaccination.VaccineName.Trim());
            cmd.Parameters.AddWithValue("@Date", Vaccination.VaccinationDate!.Value.Date);
            cmd.Parameters.AddWithValue("@Next",
                Vaccination.NextDueDate.HasValue ? (object)Vaccination.NextDueDate.Value.Date : DBNull.Value);
        }

        private static bool AnimalExists(SqlConnection conn, int id)
        {
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM Animals WHERE animal_ID = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private bool LoadPage(int id)
        {
            if (string.IsNullOrEmpty(Tab) || !Tabs.Contains(Tab)) Tab = "records";

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                if (!LoadAnimal(conn, id))
                {
                    TempData["ErrorMessage"] = "Animal not found.";
                    return false;
                }
                LoadRecords(conn, id);
                LoadVaccinations(conn, id);
                LoadAppointments(conn, id);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Load animal details exception: " + ex);
                TempData["ErrorMessage"] = "Could not load the animal.";
                return false;
            }
        }

        private bool LoadAnimal(SqlConnection conn, int id)
        {
            // A farmer only gets a result for animals on their own farms
            const string sql = @"
                SELECT a.animal_ID, a.species, a.tag_number, a.breed, a.sex, a.date_of_birth,
                       f.farm_name, f.district, f.sector, f.cell, f.village,
                       o.full_name, o.phone
                FROM Animals a
                JOIN Farms f ON f.farm_ID = a.farm_ID
                JOIN Users o ON o.user_ID = f.farmer_ID
                WHERE a.animal_ID = @Id AND (@IsVet = 1 OR f.farmer_ID = @UserId)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@IsVet", IsVet);
            cmd.Parameters.AddWithValue("@UserId", CurrentUserId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return false;

            Animal = new AnimalInfo
            {
                AnimalId = r.GetInt32(0),
                Species = r.GetString(1),
                TagNumber = r.GetString(2),
                Breed = r.IsDBNull(3) ? null : r.GetString(3),
                Sex = r.IsDBNull(4) ? null : r.GetString(4),
                DateOfBirth = r.IsDBNull(5) ? null : r.GetDateTime(5),
                FarmName = r.GetString(6),
                District = r.GetString(7),
                Sector = r.GetString(8),
                Cell = r.GetString(9),
                Village = r.GetString(10),
                OwnerName = r.GetString(11),
                OwnerPhone = r.IsDBNull(12) ? null : r.GetString(12)
            };
            return true;
        }

        private void LoadRecords(SqlConnection conn, int id)
        {
            const string sql = @"
                SELECT hr.record_ID, hr.record_date, hr.diagnosis, hr.treatment, hr.case_status,
                       hr.description, hr.veterinarian_ID, v.full_name
                FROM HealthRecords hr
                LEFT JOIN Users v ON v.user_ID = hr.veterinarian_ID
                WHERE hr.animal_ID = @Id
                ORDER BY hr.record_date DESC, hr.record_ID DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Records.Add(new RecordEntry
                {
                    RecordId = r.GetInt32(0),
                    Date = r.GetDateTime(1),
                    Diagnosis = r.IsDBNull(2) ? null : r.GetString(2),
                    Treatment = r.IsDBNull(3) ? null : r.GetString(3),
                    CaseStatus = r.IsDBNull(4) ? null : r.GetString(4),
                    Description = r.IsDBNull(5) ? null : r.GetString(5),
                    VetId = r.IsDBNull(6) ? null : r.GetInt32(6),
                    VetName = r.IsDBNull(7) ? null : r.GetString(7)
                });
            }
        }

        private void LoadVaccinations(SqlConnection conn, int id)
        {
            const string sql = @"
                SELECT vaccination_ID, vaccine_name, vaccination_date, next_due_date
                FROM Vaccinations
                WHERE animal_ID = @Id
                ORDER BY vaccination_date DESC, vaccination_ID DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var r = cmd.ExecuteReader();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (r.Read())
            {
                var name = r.GetString(1);
                Vaccinations.Add(new VaccinationEntry
                {
                    Id = r.GetInt32(0),
                    VaccineName = name,
                    VaccinationDate = r.GetDateTime(2),
                    NextDueDate = r.IsDBNull(3) ? null : r.GetDateTime(3),
                    IsLatest = seen.Add(name)   // first row per vaccine = newest dose
                });
            }
        }

        private void LoadAppointments(SqlConnection conn, int id)
        {
            // Vets only see their own appointments with this animal
            const string sql = @"
                SELECT ap.appointment_date, ap.status, ap.detail, v.full_name
                FROM Appointments ap
                JOIN Users v ON v.user_ID = ap.veterinarian_ID
                WHERE ap.animal_ID = @Id AND (@IsVet = 0 OR ap.veterinarian_ID = @UserId)
                ORDER BY ap.appointment_date DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@IsVet", IsVet);
            cmd.Parameters.AddWithValue("@UserId", CurrentUserId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                Appointments.Add(new AnimalAppointment
                {
                    Date = r.GetDateTime(0),
                    Status = r.GetString(1),
                    Detail = r.IsDBNull(2) ? null : r.GetString(2),
                    VetName = r.GetString(3)
                });
            }
        }
    }
}