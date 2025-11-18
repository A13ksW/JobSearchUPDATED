using JobSearch.Data;
using JobSearch.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class JobOfferService : IJobOfferService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotificationService _notificationService;

    public JobOfferService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        INotificationService notificationService)
    {
        _contextFactory = contextFactory;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _notificationService = notificationService;
    }

    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private void LogAudit(ApplicationDbContext context, string actionType, int entityId, string changes)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;

        var log = new AuditLog
        {
            UserId = userId,
            ActionType = actionType,
            EntityId = entityId,
            EntityType = "JobOffer",
            Changes = changes,
            Timestamp = DateTime.UtcNow
        };
        context.AuditLogs.Add(log);
    }

    public async Task<List<JobOffer>> GetJobOffersAsync(string? location, decimal? minSalary, string? sortBy, EmploymentType? employmentType, JobType? jobType, IndustryCategory? industryCategory, bool onlyWithExperience, bool onlyOnlineRecruitment, bool onlyWithSanitaryBook, bool onlyWithStudentStatus, bool onlyWithDisabilityCertificate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.JobOffer.AsQueryable();
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null || !httpContext.User.IsInRole("Admin") && !httpContext.User.IsInRole("Moderator"))
        {
            query = query.Where(o => o.Status == OfferStatus.Opublikowana);
        }
        if (!string.IsNullOrEmpty(location))
        {
            query = query.Where(o => o.Location.ToLower().Contains(location.ToLower()));
        }
        if (minSalary.HasValue && minSalary.Value > 0)
        {
            query = query.Where(o => o.SalaryMin >= minSalary.Value);
        }
        if (employmentType.HasValue)
        {
            query = query.Where(o => o.EmplType == employmentType.Value);

        }
        if (industryCategory.HasValue)
        {
            query = query.Where(o => o.IndustryCategory == industryCategory.Value);
        }
        if (jobType.HasValue)
        {
            query = query.Where(o => o.JobType == jobType.Value);
        }
        
        if (onlyWithExperience)
        {
            query = query.Where(o => o.RequiresExperience);
        }

       
        if (onlyOnlineRecruitment)
        {
            query = query.Where(o => o.IsOnlineRecruitment);
        }

       
        if (onlyWithSanitaryBook)
        {
            query = query.Where(o => o.RequiresSanitaryBook);
        }

        if (onlyWithStudentStatus)
        {
            query = query.Where(o => o.RequiresStudentStatus);
        }

        if (onlyWithDisabilityCertificate)
        {
            query = query.Where(o => o.RequiresDisabilityCertificate);
        }

        switch (sortBy)
        {
            case "salary_desc":
                query = query.OrderByDescending(o => o.SalaryMin);
                break;
            case "salary_asc":
                query = query.OrderBy(o => o.SalaryMin);
                break;
            default:
                query = query.OrderByDescending(o => o.DatePosted);
                break;
        }
        return await query.ToListAsync();
    }

    public async Task<JobOffer?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.JobOffer.FindAsync(id);
    }

    public async Task CreateAsync(JobOffer jobOffer)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var currentUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(currentUserId))
        {
            throw new InvalidOperationException("Nie można utworzyć oferty bez zalogowanego użytkownika.");
        }
        jobOffer.DatePosted = DateTime.Now;
        jobOffer.Status = OfferStatus.Weryfikacja;
        jobOffer.CreatedByUserId = currentUserId;
        context.JobOffer.Add(jobOffer);
        await context.SaveChangesAsync();
        LogAudit(context, "OfferCreated", jobOffer.Id, $"Oferta '{jobOffer.Title}' została utworzona i oczekuje na weryfikację.");
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(JobOffer jobOffer)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var originalOffer = await context.JobOffer.AsNoTracking().FirstOrDefaultAsync(o => o.Id == jobOffer.Id);
        var currentUserId = GetCurrentUserId();
        var isOwner = originalOffer?.CreatedByUserId == currentUserId;
        bool isAdmin = _httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;
        bool isMod = _httpContextAccessor.HttpContext?.User.IsInRole("Moderator") ?? false;
        var isModerator = isAdmin || isMod;
        if (!isOwner && !isModerator)
        {
            throw new InvalidOperationException("Nie masz uprawnień do edycji tej oferty.");
        }
        string changes = "Oferta została zedytowana.";
        if (originalOffer != null && originalOffer.Status != jobOffer.Status)
        {
            if (!isModerator)
            {
                jobOffer.Status = originalOffer.Status;
            }
            else
            {
                changes = $"Zmieniono status z '{originalOffer.Status}' na '{jobOffer.Status}'.";
                if (!string.IsNullOrEmpty(jobOffer.ModerationComment))
                {
                    changes += $" Komentarz: {jobOffer.ModerationComment}";
                }
            }
        }
        else if (isOwner && !isModerator && originalOffer?.Status != OfferStatus.Weryfikacja)
        {
            jobOffer.Status = OfferStatus.Weryfikacja;
            changes = "Oferta została zaktualizowana przez właściciela i ponownie przesunięta do weryfikacji.";
        }
        context.Entry(jobOffer).State = EntityState.Modified;
        LogAudit(context, "OfferEdit", jobOffer.Id, changes);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var jobOffer = await context.JobOffer.FindAsync(id);
        if (jobOffer != null)
        {
            var currentUserId = GetCurrentUserId();
            var isOwner = jobOffer.CreatedByUserId == currentUserId;
            bool isAdmin = _httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;
            bool isMod = _httpContextAccessor.HttpContext?.User.IsInRole("Moderator") ?? false;
            var isModerator = isAdmin || isMod;
            if (!isOwner && !isModerator)
            {
                throw new InvalidOperationException("Nie masz uprawnień do usunięcia tej oferty.");
            }
            LogAudit(context, "OfferDelete", jobOffer.Id, $"Oferta '{jobOffer.Title}' została usunięta.");
            context.JobOffer.Remove(jobOffer);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<JobOffer>> GetMyOffersAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.JobOffer
            .Where(o => o.CreatedByUserId == userId)
            .OrderByDescending(o => o.DatePosted)
            .ToListAsync();
    }

    public async Task<bool> ApplyForOfferAsync(int offerId, string applicantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var existingApplication = await context.JobApplications
            .AnyAsync(a => a.JobOfferId == offerId && a.ApplicantId == applicantId);

        if (existingApplication) return false;

        var offer = await context.JobOffer.FindAsync(offerId);
        if (offer?.CreatedByUserId == applicantId) return false;
        if (offer == null) return false;

        var application = new JobApplication
        {
            JobOfferId = offerId,
            ApplicantId = applicantId,
            ApplicationDate = DateTime.UtcNow,
            Status = ApplicationStatus.Aplikowano
        };

        context.JobApplications.Add(application);
        await context.SaveChangesAsync();

        // --- POPRAWKA CS8604: Sprawdzamy, czy CreatedByUserId nie jest nullem ---
        if (!string.IsNullOrEmpty(offer.CreatedByUserId))
        {
            await _notificationService.CreateNotificationAsync(
                userId: offer.CreatedByUserId,
                message: $"Masz nową aplikację na stanowisko: {offer.Title}",
                linkUrl: $"/my-offers/details/{offer.Id}"
            );
        }
        return true;
    }

    public async Task<List<JobApplication>> GetApplicationsForOfferAsync(int offerId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.JobApplications
            .Where(a => a.JobOfferId == offerId)
            .Include(a => a.Applicant)
                .ThenInclude(u => u.UserProfileCV)
                    .ThenInclude(cv => cv.Skills)
            .Include(a => a.Applicant)
                .ThenInclude(u => u.UserProfileCV)
                    .ThenInclude(cv => cv.Languages)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync();
    }

    public async Task<bool> HasUserAppliedAsync(int offerId, string applicantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.JobApplications
            .AnyAsync(a => a.JobOfferId == offerId && a.ApplicantId == applicantId);
    }

    public async Task ApproveOfferAsync(int offerId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var offer = await context.JobOffer.FindAsync(offerId);
        if (offer == null || offer.Status != OfferStatus.Weryfikacja) return;

        offer.Status = OfferStatus.Opublikowana;
        offer.ModerationComment = null;
        LogAudit(context, "OfferApproved", offer.Id, "Oferta zatwierdzona i opublikowana.");
        await context.SaveChangesAsync();

        // --- POPRAWKA CS8604: Sprawdzamy, czy CreatedByUserId nie jest nullem ---
        if (!string.IsNullOrEmpty(offer.CreatedByUserId))
        {
            await _notificationService.CreateNotificationAsync(
                userId: offer.CreatedByUserId,
                message: $"Twoja oferta '{offer.Title}' została zatwierdzona i jest publiczna!",
                linkUrl: $"/my-offers/details/{offer.Id}"
            );
        }
    }

    public async Task RejectOfferAsync(int offerId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var offer = await context.JobOffer.FindAsync(offerId);
        if (offer == null || offer.Status != OfferStatus.Weryfikacja) return;

        offer.Status = OfferStatus.Odrzucona;
        LogAudit(context, "OfferRejected", offer.Id, "Oferta odrzucona (szybka akcja).");
        await context.SaveChangesAsync();

        // --- POPRAWKA CS8604: Sprawdzamy, czy CreatedByUserId nie jest nullem ---
        if (!string.IsNullOrEmpty(offer.CreatedByUserId))
        {
            await _notificationService.CreateNotificationAsync(
                userId: offer.CreatedByUserId,
                message: $"Twoja oferta '{offer.Title}' została odrzucona przez moderację.",
                linkUrl: $"/my-offers/edit/{offer.Id}"
            );
        }
    }

    public async Task<List<JobApplication>> GetMyApplicationsAsync(string applicantId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.JobApplications
            .Where(a => a.ApplicantId == applicantId)
            .Include(a => a.JobOffer)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync();
    }

    // --- METODA DO ZASTĄPIENIA ---
    public async Task UpdateApplicationStatusAsync(int applicationId, ApplicationStatus newStatus, string ownerUserId, string? employerPhone = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var application = await context.JobApplications
            .Include(a => a.JobOffer)
            .Include(a => a.Applicant)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null)
        {
            throw new InvalidOperationException("Nie znaleziono aplikacji.");
        }

        if (application.JobOffer?.CreatedByUserId != ownerUserId)
        {
            throw new InvalidOperationException("Brak uprawnień do zmiany statusu tej aplikacji.");
        }

        application.Status = newStatus;

        // --- DODANA LOGIKA ZAPIUS NUMERU ---
        string message = string.Empty;
        if (newStatus == ApplicationStatus.Zaproszony)
        {
            application.EmployerContactPhone = employerPhone; // Zapisz numer telefonu
            message = $"Gratulacje! Zostałeś zaproszony na rozmowę w sprawie: {application.JobOffer.Title}. Skontaktuj się pod numerem: {employerPhone}";
        }
        else if (newStatus == ApplicationStatus.Odrzucony)
        {
            application.EmployerContactPhone = null; // Wyczyść numer, jeśli odrzucono
            message = $"Niestety, Twoja aplikacja na stanowisko: {application.JobOffer.Title} została odrzucona.";
        }
        // --- KONIEC LOGIKI ZAPISU NUMERU ---

        await context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(message))
        {
            await _notificationService.CreateNotificationAsync(
                userId: application.ApplicantId,
                message: message,
                linkUrl: $"/my-applications"
            );
        }
    }
}