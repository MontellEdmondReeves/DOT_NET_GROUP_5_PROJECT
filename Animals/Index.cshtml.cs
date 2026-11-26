using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Livestock_Health_App.Pages.Animals
{
    public class AnimalRow
    {
        public int AnimalId { get; set; }
        public int FarmId { get; set; }
        public string FarmName { get; set; } = "";
        public string Species { get; set; } = "";
        public string TagNumber { get; set; } = "";
        public string? Breed { get; set; }
        public string? Sex { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int RecordCount { get; set; }      // appointments + health records + vaccinations (farmer view)
        public string OwnerName { get; set; } = ""; // vet view
        public string District { get; set; } = "";  // vet view
    }

    public class AnimalInput
    {
        public int AnimalId { get; set; }   // 0 = new animal

        [Required(ErrorMessage = "Please select a farm.")]
        [Display(Name = "Farm")]
        public int FarmId { get; set; }

        [Required(ErrorMessage = "Please select a species.")]
        public string Species { get; set; } = "";

        [Required, StringLength(50)]
        [Display(Name = "Tag number")]
        public string TagNumber { get; set; } = "";

        [StringLength(50)]
        public string? Breed { get; set; }

        public string? Sex { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }
    }

    [Authorize(Roles = "Farmer,Veterinarian")]
    public class IndexModel : PageModel
    {
        public static readonly string[] SpeciesList = { "Cattle", "Goat", "Sheep", "Pig", "Poultry" };
        public static readonly string[] SexList = { "Male", "Female" };

        private readonly string _connString;

        public IndexModel(IConfiguration config)
        {
            _connString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing connection string.");
        }

        // filters (GET)
        [BindProperty(SupportsGet = true)] public int FarmFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? SpeciesFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }   // vet: tag number search

        // form (POST)
        [BindProperty] public AnimalInput Input { get; set; } = new();

        public List<AnimalRow> Animals { get; set; } = new();
        public List<SelectListItem> FarmOptions { get; set; } = new();
        public bool ShowModal { get; set; }

        private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        public bool IsFarmer => User.IsInRole("Farmer");

        public void OnGet()
        {
            LoadPage();
        }

        // ---------- Add / Update (farmer only) ----------
        public IActionResult OnPostSave()
        {
            if (!IsFarmer) return Forbid();

            if (Input.DateOfBirth.HasValue && Input.DateOfBirth.Value.Date > DateTime.Today)
                ModelState.AddModelError("Input.DateOfBirth", "Date of birth cannot be in the future.");
            if (!SpeciesList.Contains(Input.Species))
                ModelState.AddModelError("Input.Species", "Invalid species.");
            if (!string.IsNullOrEmpty(Input.Sex) && !SexList.Contains(Input.Sex))
                ModelState.AddModelError("Input.Sex", "Invalid sex.");

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                // The chosen farm must belong to this farmer
                if (ModelState.IsValid && !FarmBelongsToUser(conn, Input.FarmId))
                    ModelState.AddModelError("Input.FarmId", "Invalid farm.");

                // Tag numbers must be unique (FR03)
                if (ModelState.IsValid && TagExists(conn, Input.TagNumber.Trim(), Input.AnimalId))
                    ModelState.AddModelError("Input.TagNumber", "This tag number is already registered.");

                if (!ModelState.IsValid)
                {
                    LoadPage();
                    ShowModal = true;
                    return Page();
                }

                if (Input.AnimalId == 0)
                {
                    const string sql = @"
                        INSERT INTO Animals (farm_ID, species, tag_number, breed, sex, date_of_birth)
                        VALUES (@FarmId, @Species, @Tag, @Breed, @Sex, @Dob)";
                    using var cmd = new SqlCommand(sql, conn);
                    AddParams(cmd);
                    cmd.ExecuteNonQuery();
                    TempData["SuccessMessage"] = "Animal registered.";
                }
                else
                {
                    // Join to Farms so a farmer can only edit animals on their own farms
                    const string sql = @"
                        UPDATE a
                        SET a.farm_ID = @FarmId, a.species = @Species, a.tag_number = @Tag,
                            a.breed = @Breed, a.sex = @Sex, a.date_of_birth = @Dob
                        FROM Animals a
                        JOIN Farms f ON f.farm_ID = a.farm_ID
                        WHERE a.animal_ID = @AnimalId AND f.farmer_ID = @UserId";
                    using var cmd = new SqlCommand(sql, conn);
                    AddParams(cmd);
                    cmd.Parameters.AddWithValue("@AnimalId", Input.AnimalId);
                    cmd.Parameters.AddWithValue("@UserId", UserId);

                    if (cmd.ExecuteNonQuery() == 0)
                        TempData["ErrorMessage"] = "Animal not found.";
                    else
                        TempData["SuccessMessage"] = "Animal updated.";
                }
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                // unique tag number violated by a simultaneous request
                TempData["ErrorMessage"] = "This tag number is already registered.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Animal save exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while saving the animal.";
            }

            return RedirectToPage();
        }

        // ---------- Delete (farmer only) ----------
        public IActionResult OnPostDelete(int id)
        {
            if (!IsFarmer) return Forbid();

            try
            {
                using var conn = new SqlConnection(_connString);
                conn.Open();

                // How many records reference this animal? NULL result = not found / not yours
                const string checkSql = @"
                    SELECT (SELECT COUNT(*) FROM Appointments  WHERE animal_ID = a.animal_ID)
                         + (SELECT COUNT(*) FROM HealthRecords WHERE animal_ID = a.animal_ID)
                         + (SELECT COUNT(*) FROM Vaccinations  WHERE animal_ID = a.animal_ID)
                    FROM Animals a
                    JOIN Farms f ON f.farm_ID = a.farm_ID
                    WHERE a.animal_ID = @AnimalId AND f.farmer_ID = @UserId";

                object? result;
                using (var checkCmd = new SqlCommand(checkSql, conn))
                {
                    checkCmd.Parameters.AddWithValue("@AnimalId", id);
                    checkCmd.Parameters.AddWithValue("@UserId", UserId);
                    result = checkCmd.ExecuteScalar();
                }

                if (result == null)
                {
                    TempData["ErrorMessage"] = "Animal not found.";
                    return RedirectToPage();
                }
                if (Convert.ToInt32(result) > 0)
                {
                    TempData["ErrorMessage"] =
                        "This animal has health records, vaccinations or appointments and cannot be deleted.";
                    return RedirectToPage();
                }

                const string delSql = "DELETE FROM Animals WHERE animal_ID = @AnimalId";
                using var cmd = new SqlCommand(delSql, conn);
                cmd.Parameters.AddWithValue("@AnimalId", id);
                cmd.ExecuteNonQuery();
                TempData["SuccessMessage"] = "Animal deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Animal delete exception: " + ex);
                TempData["ErrorMessage"] = "An error occurred while deleting the animal.";
            }

            return RedirectToPage();
        }

        // ---------- helpers ----------
        private void AddParams(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@FarmId", Input.FarmId);
            cmd.Parameters.AddWithValue("@Species", Input.Species);
            cmd.Parameters.AddWithValue("@Tag", Input.TagNumber.Trim());
            cmd.Parameters.AddWithValue("@Breed", string.IsNullOrWhiteSpace(Input.Breed) ? DBNull.Value : Input.Breed.Trim());
            cmd.Parameters.AddWithValue("@Sex", string.IsNullOrEmpty(Input.Sex) ? DBNull.Value : Input.Sex);
            cmd.Parameters.AddWithValue("@Dob", Input.DateOfBirth.HasValue ? Input.DateOfBirth.Value : DBNull.Value);
        }

        private bool FarmBelongsToUser(SqlConnection conn, int farmId)
        {
            const string sql = "SELECT COUNT(*) FROM Farms WHERE farm_ID = @FarmId AND farmer_ID = @UserId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@FarmId", farmId);
            cmd.Parameters.AddWithValue("@UserId", UserId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private bool TagExists(SqlConnection conn, string tagNumber, int animalId)
        {
            const string sql = "SELECT COUNT(*) FROM Animals WHERE tag_number = @Tag AND animal_ID <> @AnimalId";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Tag", tagNumber);
            cmd.Parameters.AddWithValue("@AnimalId", animalId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private void LoadPage()
        {
            Animals.Clear();
            FarmOptions.Clear();
            using var conn = new SqlConnection(_connString);
            conn.Open();

            if (IsFarmer)
            {
                const string farmsSql = "SELECT farm_ID, farm_name FROM Farms WHERE farmer_ID = @UserId ORDER BY farm_name";
                using (var farmCmd = new SqlCommand(farmsSql, conn))
                {
                    farmCmd.Parameters.AddWithValue("@UserId", UserId);
                    using var farmReader = farmCmd.ExecuteReader();
                    while (farmReader.Read())
                        FarmOptions.Add(new SelectListItem(farmReader.GetString(1), farmReader.GetInt32(0).ToString()));
                }

                const string animalSql = @"SELECT a.animal_ID, a.farm_ID, f.farm_name, a.species,
                    a.tag_number, a.breed, a.sex, a.date_of_birth,
                    (SELECT COUNT(*) FROM Appointments x WHERE x.animal_ID = a.animal_ID)
                    + (SELECT COUNT(*) FROM HealthRecords x WHERE x.animal_ID = a.animal_ID)
                    + (SELECT COUNT(*) FROM Vaccinations x WHERE x.animal_ID = a.animal_ID)
                    FROM Animals a JOIN Farms f ON f.farm_ID = a.farm_ID
                    WHERE f.farmer_ID = @UserId
                      AND (@FarmFilter = 0 OR a.farm_ID = @FarmFilter)
                      AND (@SpeciesFilter = '' OR a.species = @SpeciesFilter)
                    ORDER BY a.tag_number";
                using var animalCmd = new SqlCommand(animalSql, conn);
                animalCmd.Parameters.AddWithValue("@UserId", UserId);
                animalCmd.Parameters.AddWithValue("@FarmFilter", FarmFilter);
                animalCmd.Parameters.AddWithValue("@SpeciesFilter", SpeciesFilter ?? string.Empty);
                using var reader = animalCmd.ExecuteReader();
                while (reader.Read()) Animals.Add(ReadAnimal(reader));
            }
            else if (!string.IsNullOrWhiteSpace(Search))
            {
                const string animalSql = @"SELECT a.animal_ID, a.farm_ID, f.farm_name, a.species,
                    a.tag_number, a.breed, a.sex, a.date_of_birth, u.full_name, f.district
                    FROM Animals a JOIN Farms f ON f.farm_ID = a.farm_ID
                    JOIN Users u ON u.user_ID = f.farmer_ID
                    WHERE a.tag_number LIKE @Search ORDER BY a.tag_number";
                using var animalCmd = new SqlCommand(animalSql, conn);
                animalCmd.Parameters.AddWithValue("@Search", $"%{Search.Trim()}%");
                using var reader = animalCmd.ExecuteReader();
                while (reader.Read()) Animals.Add(ReadAnimal(reader, true));
            }
        }

        private static AnimalRow ReadAnimal(SqlDataReader reader, bool veterinarian = false)
        {
            var animal = new AnimalRow
            {
                AnimalId = reader.GetInt32(0), FarmId = reader.GetInt32(1), FarmName = reader.GetString(2),
                Species = reader.GetString(3), TagNumber = reader.GetString(4),
                Breed = reader.IsDBNull(5) ? null : reader.GetString(5),
                Sex = reader.IsDBNull(6) ? null : reader.GetString(6),
                DateOfBirth = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
            };
            if (veterinarian)
            {
                animal.OwnerName = reader.GetString(8);
                animal.District = reader.GetString(9);
            }
            else
            {
                animal.RecordCount = reader.GetInt32(8);
            }
            return animal;
        }
    }
}