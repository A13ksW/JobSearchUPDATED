using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JobSearch.Data;

namespace JobSearch.Services
{
    public enum EmploymentType
    {
        FullTime,
        PartTime,
        Contract,
        Internship,
        Temporary,
        Freelance
    }
    public enum JobType
    {
        Remote,
        OnSite,
        Hybrid
    }

    public enum OfferStatus
    {
        [Display(Name = "Weryfikacja")]
        Weryfikacja,
        [Display(Name = "Opublikowana")]
        Opublikowana,
        [Display(Name = "Odrzucona")]
        Odrzucona
    }

    public class JobOffer
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Location { get; set; } = string.Empty;

        [Required, StringLength(250)]
        [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
        public string ContactInfo { get; set; } = string.Empty;

        [Column(TypeName = "datetime2")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = false)]
        public DateTime DatePosted { get; set; }

        public DateTime? ApplicationDeadline { get; set; }

        [Required]
        public EmploymentType EmplType { get; set; }

        [Required]
        public JobType JobType { get; set; }

        [NotMapped]
        public string SalaryRange
        {
            get
            {
                if (SalaryMin.HasValue && SalaryMax.HasValue)
                    return $"{SalaryMin.Value:0.##} - {SalaryMax.Value:0.##}";
                if (SalaryMin.HasValue)
                    return $"{SalaryMin.Value:0.##} - ?";
                if (SalaryMax.HasValue)
                    return $"? - {SalaryMax.Value:0.##}";
                return string.Empty;
            }
        }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? SalaryMin { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? SalaryMax { get; set; }

        [Required]
        public string Requirements { get; set; } = string.Empty;

        public string? Benefits { get; set; }

        [Required]
        public OfferStatus Status { get; set; } = OfferStatus.Weryfikacja;

        [Column(TypeName = "nvarchar(1000)")]
        public string? ModerationComment { get; set; }

        // --- POPRAWKA PONIŻEJ ---

        // Usunęliśmy [Required] i zmieniliśmy 'string' na 'string?'
        public string? CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual ApplicationUser? CreatedByUser { get; set; }

        public virtual ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    }
}