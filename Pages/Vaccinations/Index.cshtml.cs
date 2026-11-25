using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Livestock_Health_App.Pages.Vaccinations
{
    public class VaccRow
    {
        public int Id { get; set; }
        public int AnimalId { get; set; }
        public string TagNumber { get; set; } = "";
        public string Species { get; set; } = "";
        public string FarmName { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public string VaccineName { get; set; } = "";
        public DateTime VaccinationDate { get; set; }
        public DateTime? NextDueDate { get; set; }
        public bool IsLatest { get; set; }   // newest dose of this vaccine for this animal

        // history | completed | overdue | soon | ok
        public string Status =>
            !IsLatest ? "history" :
            NextDueDate == null ? "completed" :
            DaysUntilDue < 0 ? "overdue" :
            DaysUntilDue <= 14 ? "soon" : "ok";

        public int DaysUntilDue => NextDueDate == null ? 0 : (NextDueDate.Value.Date - DateTime.Today).Days;
    }

    public class VaccInput
    {
        public int VaccinationId { get; set; }   // 0 = new

        [Display(Name = "Animal tag number")]
        public string? TagNumber { get; set; }   // required for new entries only (checked in the handler)

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
    public class IndexModel : PageModel
    {
        public static readonly string[] SpeciesList = { "Cattle", "Goat", "Sheep", "Pig", "Poultry" };

        public static readonly (string Key, string Label)[] TabList =
        {
            ("attention", "Needs action"),
            ("overdue", "Overdue"),
            ("soon", "Due soon"),
            ("ok", "Up to date"),
            ("completed", "Completed"),
            ("all", "All current"),
            ("history", "Previous doses")
        };

        // Scope: a farmer sees their own animals. A vet sees animals they have treated or had an
        // appointment with, plus any animal whose exact tag number they searched for.
        private const string FarmerScope = "f.farmer_ID = @UserId";
        private const string VetScope = @"
            (EXISTS (SELECT 1 FROM HealthRecords h WHERE h.animal_ID = vc.animal_ID AND h.veterinarian_ID = @UserId)
             OR EXISTS (SELECT 1 FROM Appointments p WHERE p.animal_ID = vc.animal_ID AND p.veterinarian_ID = @UserId)
             OR (@Search <> '' AND an.tag_number = @Search))";

        private readonly string _connString;

        public IndexModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        // filters
        [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
        [BindProperty(SupportsGet = true)] public string? Species { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }

        // form
        [BindProperty] public VaccInput Input { get; set; } = new();

        public List<VaccRow> Rows { get; set; } = new();
        public Dictionary<string, int> Counts { get; set; } = new();
        public bool ShowModal { get; set; }

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        public bool IsVet => User.IsInRole("Veterinarian");
        public bool IsFarmer => User.IsInRole("Farmer");

        public void OnGet()
        {
            LoadPage();
        }

        // ---------- Vet: register / edit ----------
        public IActionResult OnPostSave()
        {
            if (!IsVet) return Forbid();

            bool isNew = Input.VaccinationId == 0;
            string tag = (Input.TagNumber ?? "").Trim();

            if (isNew && tag == "")
                ModelState.AddModelError("Input.TagNumber", "Please enter the animal's tag number.");
            if (Input.VaccinationDate.HasValue && Input.VaccinationDate.Value.Date > DateTime.Today)
                ModelState.AddModelError("Input.VaccinationDate", "The date given cannot be in the future.");
            if (Input.VaccinationDate.HasValue && Input.NextDueDate.HasValue &&
                Input.NextDueDate.Value.Date <= Input.VaccinationDate.Value.Date)
                ModelState.AddModelError("Input.NextDueDate", "The next due date must be after the date given.");

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                int animalId = 0;
                if (isNew && ModelState.IsValid)
                {
                    using var find = new SqlCommand("SELECT animal_ID FROM Animals WHERE tag_number = @Tag", conn);
                    find.Parameters.AddWithValue("@Tag", tag);
                    var result = find.ExecuteScalar();
                    if (result == null)
                        ModelState.AddModelError("Input.TagNumber", "No animal with this tag number was found.");
                    else
                        animalId = Convert.ToInt32(result);
                }

                // Same vaccine, same animal, same day = duplicate entry
                if (isNew && ModelState.IsValid)
                {
                    using var dup = new SqlCommand(@"
                        SELECT COUNT(*) FROM Vaccinations
                        WHERE animal_ID = @A AND vaccine_name = @V AND vaccination_date = @D", conn);
                    dup.Parameters.AddWithValue("@A", animalId);
                    dup.Parameters.AddWithValue("@V", Input.VaccineName.Trim());
                    dup.Parameters.AddWithValue("@D", Input.VaccinationDate!.Value.Date);
                    if (Convert.ToInt32(dup.ExecuteScalar()) > 0)
                        ModelState.AddModelError("Input.VaccineName",
                            "This vaccine is already registered for this animal on that date.");
                }

                if (!ModelState.IsValid)
                {
                    LoadPage();
                    ShowModal = true;
                    return Page();
                }

                using var cmd = new SqlCommand(isNew
                    ? "INSERT INTO Vaccinations (animal_ID, vaccine_name, vaccination_date, next_due_date) VALUES (@A, @V, @D, @N)"
                    : "UPDATE Vaccinations SET vaccine_name = @V, vaccination_date = @D, next_due_date = @N WHERE vaccination_ID = @Id", conn);
                cmd.Parameters.AddWithValue("@A", animalId);
                cmd.Parameters.AddWithValue("@V", Input.VaccineName.Trim());
                cmd.Parameters.AddWithValue("@D", Input.VaccinationDate!.Value.Date);
                cmd.Parameters.AddWithValue("@N", (object?)Input.NextDueDate?.Date ?? DBNull.Value);
                if (!isNew) cmd.Parameters.AddWithValue("@Id", Input.VaccinationId);
                cmd.ExecuteNonQuery();
                TempData["SuccessMessage"] = isNew ? "Vaccination registered." : "Vaccination updated.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Vaccination save exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while saving the vaccination.";
            }

            return RedirectToPage(new { Tab, Species, Search });
        }

        public IActionResult OnPostDelete(int id)
        {
            if (!IsVet) return Forbid();
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                using var cmd = new SqlCommand("DELETE FROM Vaccinations WHERE vaccination_ID = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
                TempData["SuccessMessage"] = "Vaccination deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Vaccination delete exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while deleting the vaccination.";
            }
            return RedirectToPage(new { Tab, Species, Search });
        }

        private void LoadPage()
        {
            Rows.Clear(); Counts.Clear();
            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();
                const string sql = @"
                    SELECT vc.vaccination_ID, an.animal_ID, an.tag_number, an.species,
                           f.farm_name, u.full_name, vc.vaccine_name, vc.vaccination_date, vc.next_due_date,
                           CASE WHEN vc.vaccination_date = (SELECT MAX(v2.vaccination_date) FROM Vaccinations v2
                               WHERE v2.animal_ID = vc.animal_ID AND v2.vaccine_name = vc.vaccine_name) THEN 1 ELSE 0 END
                    FROM Vaccinations vc JOIN Animals an ON an.animal_ID = vc.animal_ID
                    JOIN Farms f ON f.farm_ID = an.farm_ID
                    LEFT JOIN Users u ON u.user_ID = f.farmer_ID
                    WHERE (@Species = '' OR an.species = @Species)
                      AND (@Search = '' OR an.tag_number LIKE @SearchLike OR vc.vaccine_name LIKE @SearchLike)
                      AND (@IsVet = 1 OR f.farmer_ID = @UserId)
                    ORDER BY vc.vaccination_date DESC";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Species", Species ?? "");
                cmd.Parameters.AddWithValue("@Search", Search ?? "");
                cmd.Parameters.AddWithValue("@SearchLike", "%" + (Search ?? "") + "%");
                cmd.Parameters.AddWithValue("@IsVet", IsVet);
                cmd.Parameters.AddWithValue("@UserId", UserId);
                using var r = cmd.ExecuteReader();
                while (r.Read()) Rows.Add(new VaccRow { Id = r.GetInt32(0), AnimalId = r.GetInt32(1), TagNumber = r.GetString(2), Species = r.GetString(3), FarmName = r.GetString(4), OwnerName = r.IsDBNull(5) ? "" : r.GetString(5), VaccineName = r.GetString(6), VaccinationDate = r.GetDateTime(7), NextDueDate = r.IsDBNull(8) ? null : r.GetDateTime(8), IsLatest = Convert.ToBoolean(r.GetInt32(9)) });
                foreach (var row in Rows) Counts[row.Status] = Counts.GetValueOrDefault(row.Status) + 1;
                Counts["all"] = Rows.Count(r => r.IsLatest); Counts["history"] = Rows.Count(r => !r.IsLatest);
            }
            catch (Exception ex) { Console.WriteLine("Load vaccinations exception: " + ex); TempData["ErrorMessage"] = "Could not load vaccinations."; }
        }
    }
}
