using JobSearch.Data;
using JobSearch.Services;

public interface IJobOfferService
{
    // --- Istniejące metody ---
    Task<List<JobOffer>> GetJobOffersAsync(string? location, decimal? minSalary, string? sortBy, EmploymentType? employmentType, JobType? jobType, IndustryCategory? industryCategory);
    Task<JobOffer?> GetByIdAsync(int id);
    Task CreateAsync(JobOffer jobOffer);
    Task UpdateAsync(JobOffer jobOffer);
    Task DeleteAsync(int id);
    Task<List<JobOffer>> GetMyOffersAsync(string userId);
    Task<bool> ApplyForOfferAsync(int offerId, string applicantId);
    Task<List<JobApplication>> GetApplicationsForOfferAsync(int offerId);
    Task<bool> HasUserAppliedAsync(int offerId, string applicantId);
    Task ApproveOfferAsync(int offerId);
    Task RejectOfferAsync(int offerId);
    Task<List<JobApplication>> GetMyApplicationsAsync(string applicantId);

    // --- ZMIANA SYGNATURY METODY ---
    Task UpdateApplicationStatusAsync(int applicationId, ApplicationStatus newStatus, string ownerUserId, string? employerPhone = null);
}