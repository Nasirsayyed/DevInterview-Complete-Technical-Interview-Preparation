using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Application.Features.Coding;
using DevInterview.Application.Features.Questions;
using DevInterview.Application.Features.Subscriptions;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Content;
using DevInterview.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Companies;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyListItemDto>> ListAsync(string? search, CancellationToken ct);
    Task<CompanyDetailDto> GetAsync(Guid userId, Guid companyId, CancellationToken ct);
    Task<CompanyCollectionDetailDto> GetCollectionAsync(Guid userId, Guid collectionId, CancellationToken ct);
}

public sealed class CompanyService : ICompanyService
{
    public const string Disclaimer =
        "Practice content in this section is written by the DevInterview team to match each company's publicly described interview format. " +
        "Items marked 'Illustrative' or 'Community contributed' are not claims that a specific question was asked in a real interview. " +
        "Only items marked 'Verified' cite a public source.";

    private readonly IAppDbContext _db;
    private readonly IEntitlementService _entitlements;
    private readonly QuestionService _questions;
    private readonly CodingService _coding;

    public CompanyService(IAppDbContext db, IEntitlementService entitlements, QuestionService questions, CodingService coding)
    {
        _db = db;
        _entitlements = entitlements;
        _questions = questions;
        _coding = coding;
    }

    public async Task<IReadOnlyList<CompanyListItemDto>> ListAsync(string? search, CancellationToken ct)
    {
        var q = _db.Companies.AsNoTracking().Where(c => c.IsActive && !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(c => c.Name.Contains(search) || (c.Industry != null && c.Industry.Contains(search)));
        return await q.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new CompanyListItemDto(c.Id, c.Name, c.Slug, c.Industry, c.LogoUrl, c.DifficultyLevel.ToString(), c.IsPremium,
                c.Topics.Count, c.Collections.Count)).ToListAsync(ct);
    }

    public async Task<CompanyDetailDto> GetAsync(Guid userId, Guid companyId, CancellationToken ct)
    {
        var c = await _db.Companies.AsNoTracking().Include(x => x.Rounds).Include(x => x.Topics).ThenInclude(t => t.Technology)
            .Include(x => x.Collections).ThenInclude(col => col.Questions)
            .Include(x => x.Collections).ThenInclude(col => col.Challenges)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == companyId && x.IsActive && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Company", companyId);
        if (c.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "Company-specific preparation", ct);
        return new CompanyDetailDto(c.Id, c.Name, c.Slug, c.Overview, c.OverviewSource.ToString(), c.Industry, c.WebsiteUrl, c.DifficultyLevel.ToString(),
            c.IsPremium,
            c.Rounds.OrderBy(r => r.DisplayOrder).Select(r => new InterviewRoundDto(r.DisplayOrder, r.Name, r.Description, r.Source.ToString(), r.SourceReference)).ToList(),
            c.Topics.OrderBy(t => t.DisplayOrder).Select(t => new CompanyTopicDto(t.Id, t.Name, t.Description,
                t.Technology is null ? null : new RefDto(t.Technology.Id, t.Technology.Name, t.Technology.Slug))).ToList(),
            c.Collections.OrderBy(col => col.Kind).ThenBy(col => col.Title).Select(col => new CompanyCollectionDto(col.Id, col.Title, col.Description,
                col.Kind.ToString(), col.Source.ToString(), col.SourceReference, col.Questions.Count, col.Challenges.Count)).ToList(),
            Disclaimer);
    }

    public async Task<CompanyCollectionDetailDto> GetCollectionAsync(Guid userId, Guid collectionId, CancellationToken ct)
    {
        var col = await _db.CompanyQuestionCollections.AsNoTracking().Include(x => x.Company).Include(x => x.Questions).Include(x => x.Challenges)
            .FirstOrDefaultAsync(x => x.Id == collectionId && x.Company.IsActive && !x.Company.IsDeleted, ct)
            ?? throw new NotFoundException("Collection", collectionId);
        if (col.Company.IsPremium) await _entitlements.EnsurePremiumAsync(userId, "Company-specific preparation", ct);
        var qIds = col.Questions.OrderBy(q => q.DisplayOrder).Select(q => q.QuestionId).ToList();
        var cIds = col.Challenges.OrderBy(q => q.DisplayOrder).Select(q => q.ChallengeId).ToList();
        var questions = await _questions.ProjectList(_db.Questions.Where(q => qIds.Contains(q.Id) && q.Status == ContentStatus.Published && !q.IsDeleted), userId).ToListAsync(ct);
        var challenges = await _coding.Project(_db.CodingChallenges.Where(c => cIds.Contains(c.Id) && c.Status == ContentStatus.Published && !c.IsDeleted), userId).ToListAsync(ct);
        return new CompanyCollectionDetailDto(
            new CompanyCollectionDto(col.Id, col.Title, col.Description, col.Kind.ToString(), col.Source.ToString(), col.SourceReference, qIds.Count, cIds.Count),
            col.Company.Name, questions.OrderBy(q => qIds.IndexOf(q.Id)).ToList(), challenges.OrderBy(c => cIds.IndexOf(c.Id)).ToList(), Disclaimer);
    }
}
