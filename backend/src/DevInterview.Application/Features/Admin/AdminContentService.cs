using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Common;
using DevInterview.Contracts.Admin;
using DevInterview.Contracts.Common;
using DevInterview.Domain.Entities;
using DevInterview.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DevInterview.Application.Features.Admin;

public interface IAdminContentService
{
    Task<IReadOnlyList<AdminTechnologyDto>> GetTechnologiesAsync(CancellationToken ct);
    Task<AdminTechnologyDto> UpsertTechnologyAsync(Guid? id, TechnologyUpsertRequest request, CancellationToken ct);
    Task DeleteTechnologyAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(Guid? technologyId, CancellationToken ct);
    Task<AdminCategoryDto> UpsertCategoryAsync(Guid? id, CategoryUpsertRequest request, CancellationToken ct);
    Task DeleteCategoryAsync(Guid id, CancellationToken ct);
    Task<AdminTopicDto> UpsertTopicAsync(Guid? id, TopicUpsertRequest request, CancellationToken ct);
    Task DeleteTopicAsync(Guid id, CancellationToken ct);

    Task<PagedResult<AdminQuestionListItemDto>> GetQuestionsAsync(AdminQuestionQuery query, CancellationToken ct);
    Task<AdminQuestionDto> GetQuestionAsync(Guid id, CancellationToken ct);
    Task<AdminQuestionDto> UpsertQuestionAsync(Guid? id, QuestionUpsertRequest request, Guid adminId, CancellationToken ct);
    Task DeleteQuestionAsync(Guid id, CancellationToken ct);
    Task<int> BulkSetStatusAsync(BulkStatusRequest request, CancellationToken ct);
    Task<ImportResultDto> ImportQuestionsAsync(Stream file, string fileName, bool dryRun, Guid adminId, CancellationToken ct);
    Task<byte[]> ExportQuestionsAsync(Guid? technologyId, string format, CancellationToken ct);
    byte[] GetImportTemplate(string format);

    Task<PagedResult<QuestionReportDto>> GetReportsAsync(PageQuery query, string? status, CancellationToken ct);
    Task ResolveReportAsync(Guid id, ResolveReportRequest request, CancellationToken ct);
}

public sealed class AdminContentService : IAdminContentService
{
    public static readonly string[] ImportColumns =
    {
        "ExternalKey", "Technology", "Category", "Topic", "Title", "Body", "CodeLanguage", "CodeSnippet", "Type", "Difficulty",
        "EstimatedSeconds", "IsPremium", "Status", "Tags", "Option1", "Option2", "Option3", "Option4", "Option5", "Option6",
        "CorrectOptions", "ShortAnswer", "DetailedExplanation", "ReferenceAnswer"
    };
    public const int MaxImportRows = 5000;

    private readonly IAppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public AdminContentService(IAppDbContext db, IAuditLogger audit, IClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    // ---------------- Taxonomy ----------------
    public async Task<IReadOnlyList<AdminTechnologyDto>> GetTechnologiesAsync(CancellationToken ct) =>
        await _db.Technologies.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Select(t => new AdminTechnologyDto(t.Id, t.Name, t.Slug, t.Description, t.Group, t.IconKey, t.DisplayOrder, t.IsActive,
                t.Categories.Count(c => !c.IsDeleted), _db.Questions.Count(q => q.TechnologyId == t.Id && !q.IsDeleted))).ToListAsync(ct);

    public async Task<AdminTechnologyDto> UpsertTechnologyAsync(Guid? id, TechnologyUpsertRequest r, CancellationToken ct)
    {
        var slug = Slug.From(string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug);
        if (await _db.Technologies.AnyAsync(t => t.Slug == slug && t.Id != id, ct)) throw new ConflictException($"A technology with slug '{slug}' already exists.");
        var t = id is null ? new Technology() : await _db.Technologies.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Technology", id);
        if (id is null) _db.Technologies.Add(t);
        t.Name = r.Name.Trim(); t.Slug = slug; t.Description = r.Description?.Trim(); t.Group = r.Group.Trim(); t.IconKey = r.IconKey; t.DisplayOrder = r.DisplayOrder; t.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(Technology), t.Id.ToString(), new { t.Name, t.IsActive }, ct);
        return (await GetTechnologiesAsync(ct)).First(x => x.Id == t.Id);
    }

    public async Task DeleteTechnologyAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Technologies.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Technology", id);
        if (await _db.Questions.AnyAsync(q => q.TechnologyId == id && !q.IsDeleted, ct))
            throw new ConflictException("Delete or move the technology's questions first, or deactivate it instead.");
        t.IsDeleted = true; t.DeletedAt = _clock.UtcNow; t.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", nameof(Technology), id.ToString(), null, ct);
    }

    public async Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(Guid? technologyId, CancellationToken ct)
    {
        var q = _db.Categories.AsNoTracking().Where(c => !c.IsDeleted);
        if (technologyId is { } t) q = q.Where(c => c.TechnologyId == t);
        return await q.OrderBy(c => c.Technology.DisplayOrder).ThenBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new AdminCategoryDto(c.Id, c.TechnologyId, c.Technology.Name, c.Name, c.Slug, c.Description, c.DisplayOrder, c.IsActive,
                _db.Questions.Count(x => x.CategoryId == c.Id && !x.IsDeleted),
                c.Topics.Where(tp => !tp.IsDeleted).OrderBy(tp => tp.DisplayOrder).Select(tp => new AdminTopicDto(tp.Id, tp.CategoryId, tp.Name, tp.Slug, tp.DisplayOrder, tp.IsActive)).ToList()))
            .ToListAsync(ct);
    }

    public async Task<AdminCategoryDto> UpsertCategoryAsync(Guid? id, CategoryUpsertRequest r, CancellationToken ct)
    {
        if (!await _db.Technologies.AnyAsync(t => t.Id == r.TechnologyId && !t.IsDeleted, ct)) throw new NotFoundException("Technology", r.TechnologyId);
        var slug = Slug.From(string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug);
        if (await _db.Categories.AnyAsync(c => c.TechnologyId == r.TechnologyId && c.Slug == slug && c.Id != id, ct))
            throw new ConflictException($"Category '{slug}' already exists for this technology.");
        var c = id is null ? new Category() : await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Category", id);
        if (id is null) _db.Categories.Add(c);
        if (id is not null && c.TechnologyId != r.TechnologyId)
            await _db.Questions.Where(q => q.CategoryId == c.Id).ExecuteUpdateAsync(s => s.SetProperty(q => q.TechnologyId, r.TechnologyId), ct);
        c.TechnologyId = r.TechnologyId; c.Name = r.Name.Trim(); c.Slug = slug; c.Description = r.Description?.Trim(); c.DisplayOrder = r.DisplayOrder; c.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(Category), c.Id.ToString(), new { c.Name }, ct);
        return (await GetCategoriesAsync(r.TechnologyId, ct)).First(x => x.Id == c.Id);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct)
    {
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Category", id);
        if (await _db.Questions.AnyAsync(q => q.CategoryId == id && !q.IsDeleted, ct)) throw new ConflictException("The category still has questions.");
        c.IsDeleted = true; c.DeletedAt = _clock.UtcNow; c.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", nameof(Category), id.ToString(), null, ct);
    }

    public async Task<AdminTopicDto> UpsertTopicAsync(Guid? id, TopicUpsertRequest r, CancellationToken ct)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == r.CategoryId && !c.IsDeleted, ct)) throw new NotFoundException("Category", r.CategoryId);
        var t = id is null ? new Topic() : await _db.Topics.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Topic", id);
        if (id is null) _db.Topics.Add(t);
        t.CategoryId = r.CategoryId; t.Name = r.Name.Trim(); t.Slug = Slug.From(string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug); t.DisplayOrder = r.DisplayOrder; t.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        return new AdminTopicDto(t.Id, t.CategoryId, t.Name, t.Slug, t.DisplayOrder, t.IsActive);
    }

    public async Task DeleteTopicAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Topics.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Topic", id);
        await _db.Questions.Where(q => q.TopicId == id).ExecuteUpdateAsync(s => s.SetProperty(q => q.TopicId, (Guid?)null), ct);
        t.IsDeleted = true; t.DeletedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Questions ----------------
    public async Task<PagedResult<AdminQuestionListItemDto>> GetQuestionsAsync(AdminQuestionQuery query, CancellationToken ct)
    {
        var q = _db.Questions.AsNoTracking().Where(x => !x.IsDeleted);
        if (query.TechnologyId is { } t) q = q.Where(x => x.TechnologyId == t);
        if (query.CategoryId is { } c) q = q.Where(x => x.CategoryId == c);
        if (EnumParser.ParseOptional<ContentStatus>(query.Status, "status") is { } s) q = q.Where(x => x.Status == s);
        if (EnumParser.ParseOptional<Difficulty>(query.Difficulty, "difficulty") is { } d) q = q.Where(x => x.Difficulty == d);
        if (EnumParser.ParseOptional<QuestionType>(query.Type, "type") is { } ty) q = q.Where(x => x.Type == ty);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(x => x.Title.Contains(query.Search) || x.Body.Contains(query.Search));
        q = query.Sort switch { "title" => q.OrderBy(x => x.Title), _ => q.OrderByDescending(x => x.UpdatedAt) };
        return await q.Select(x => new AdminQuestionListItemDto(x.Id, x.Title, x.Technology.Name, x.Category.Name, x.Type.ToString(), x.Difficulty.ToString(),
            x.Status.ToString(), x.IsPremium, x.UpdatedAt)).ToPagedAsync(query, ct);
    }

    public async Task<AdminQuestionDto> GetQuestionAsync(Guid id, CancellationToken ct)
    {
        var q = await _db.Questions.AsNoTracking().Include(x => x.Technology).Include(x => x.Category).Include(x => x.Options).Include(x => x.Answer)
            .Include(x => x.Tags).ThenInclude(t => t.Tag).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Question", id);
        return new AdminQuestionDto(q.Id, q.TechnologyId, q.Technology.Name, q.CategoryId, q.Category.Name, q.TopicId, q.Title, q.Body, q.CodeSnippet,
            q.CodeLanguage, q.Type.ToString(), q.Difficulty.ToString(), q.EstimatedSeconds, q.IsPremium, q.Status.ToString(),
            q.Tags.Select(t => t.Tag.Name).ToList(),
            q.Options.OrderBy(o => o.DisplayOrder).Select(o => new AdminOptionDto(o.Id, o.Text, o.IsCorrect, o.Explanation, o.DisplayOrder)).ToList(),
            q.Answer?.ShortAnswer ?? "", q.Answer?.DetailedExplanation ?? "", q.Answer?.ReferenceAnswer, q.CreatedAt, q.UpdatedAt, q.PublishedAt);
    }

    /// <summary>Validates content rules that depend on the question type. Returns human-readable errors.</summary>
    public static List<string> ValidateQuestionContent(QuestionType type, ContentStatus status, IReadOnlyList<OptionUpsertDto> options, string shortAnswer)
    {
        var errors = new List<string>();
        var correct = options.Count(o => o.IsCorrect);
        switch (type)
        {
            case QuestionType.MultipleChoice or QuestionType.SingleChoice:
                if (options.Count < 2) errors.Add("Choice questions need at least 2 options.");
                if (correct != 1) errors.Add("Choice questions need exactly one correct option.");
                break;
            case QuestionType.MultipleSelect:
                if (options.Count < 3) errors.Add("Multiple-select questions need at least 3 options.");
                if (correct < 1) errors.Add("Mark at least one correct option.");
                break;
            case QuestionType.TrueFalse:
                if (options.Count != 2 || correct != 1) errors.Add("True/False questions need exactly 2 options with one correct.");
                break;
            default:
                if (options.Count > 0 && correct == 0) errors.Add("Options were provided but none is marked correct.");
                break;
        }
        if (options.Select(o => o.Text.Trim().ToLowerInvariant()).Distinct().Count() != options.Count) errors.Add("Options must be unique.");
        if (status == ContentStatus.Published && string.IsNullOrWhiteSpace(shortAnswer)) errors.Add("A short answer is required before publishing.");
        return errors;
    }

    public async Task<AdminQuestionDto> UpsertQuestionAsync(Guid? id, QuestionUpsertRequest r, Guid adminId, CancellationToken ct)
    {
        var question = id is null ? null :
            await _db.Questions.Include(x => x.Options).Include(x => x.Answer).Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Question", id);
        question = await ApplyAsync(question, r, adminId, null, ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "Create" : "Update", nameof(Question), question.Id.ToString(), new { question.Title, Status = question.Status.ToString() }, ct);
        return await GetQuestionAsync(question.Id, ct);
    }

    private async Task<Question> ApplyAsync(Question? question, QuestionUpsertRequest r, Guid adminId, string? externalKey, CancellationToken ct)
    {
        var type = EnumParser.Parse<QuestionType>(r.Type, "type");
        var difficulty = EnumParser.Parse<Difficulty>(r.Difficulty, "difficulty");
        var status = EnumParser.Parse<ContentStatus>(r.Status, "status");
        var options = r.Options ?? Array.Empty<OptionUpsertDto>();
        var errors = ValidateQuestionContent(type, status, options, r.ShortAnswer);
        if (errors.Count > 0) throw new ValidationAppException(errors[0], new Dictionary<string, string[]> { ["question"] = errors.ToArray() });
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == r.CategoryId && !c.IsDeleted, ct) ?? throw new NotFoundException("Category", r.CategoryId);
        if (category.TechnologyId != r.TechnologyId) throw ValidationAppException.For("categoryId", "The category does not belong to the selected technology.");
        if (r.TopicId is { } topicId && !await _db.Topics.AnyAsync(t => t.Id == topicId && t.CategoryId == category.Id, ct))
            throw ValidationAppException.For("topicId", "The topic does not belong to the selected category.");

        if (question is null)
        {
            question = new Question { CreatedByUserId = adminId, ExternalKey = externalKey };
            _db.Questions.Add(question);
        }
        question.TechnologyId = r.TechnologyId; question.CategoryId = r.CategoryId; question.TopicId = r.TopicId;
        question.Title = r.Title.Trim(); question.Body = r.Body.Trim();
        question.CodeSnippet = string.IsNullOrWhiteSpace(r.CodeSnippet) ? null : r.CodeSnippet;
        question.CodeLanguage = string.IsNullOrWhiteSpace(r.CodeLanguage) ? null : r.CodeLanguage.Trim().ToLowerInvariant();
        question.Type = type; question.Difficulty = difficulty; question.EstimatedSeconds = r.EstimatedSeconds; question.IsPremium = r.IsPremium;
        if (status == ContentStatus.Published && question.Status != ContentStatus.Published) question.PublishedAt = _clock.UtcNow;
        question.Status = status;

        // Options are replaced wholesale. Existing ids are reused by position so quiz answers keep pointing at valid ids.
        var existing = question.Options.OrderBy(o => o.DisplayOrder).ToList();
        for (var i = 0; i < options.Count; i++)
        {
            var o = i < existing.Count ? existing[i] : new QuestionOption();
            if (i >= existing.Count) question.Options.Add(o);
            o.Text = options[i].Text.Trim(); o.IsCorrect = options[i].IsCorrect; o.Explanation = options[i].Explanation?.Trim(); o.DisplayOrder = i;
        }
        foreach (var extra in existing.Skip(options.Count)) _db.QuestionOptions.Remove(extra);

        question.Answer ??= new QuestionAnswer();
        question.Answer.ShortAnswer = r.ShortAnswer.Trim();
        question.Answer.DetailedExplanation = r.DetailedExplanation.Trim();
        question.Answer.ReferenceAnswer = string.IsNullOrWhiteSpace(r.ReferenceAnswer) ? null : r.ReferenceAnswer.Trim();

        var tagNames = (r.Tags ?? Array.Empty<string>()).Select(t => t.Trim()).Where(t => t.Length > 0).DistinctBy(Slug.From).Take(10).ToList();
        var slugs = tagNames.Select(Slug.From).ToList();
        var tags = await _db.Tags.Where(t => slugs.Contains(t.Slug)).ToListAsync(ct);
        tags.AddRange(_db.Tags.Local.Where(t => slugs.Contains(t.Slug) && tags.All(x => x.Slug != t.Slug)));
        foreach (var name in tagNames.Where(n => tags.All(t => t.Slug != Slug.From(n))))
        {
            var tag = new Tag { Name = name, Slug = Slug.From(name) };
            _db.Tags.Add(tag);
            tags.Add(tag);
        }
        foreach (var qt in question.Tags.Where(qt => !tags.Any(t => t.Id == qt.TagId)).ToList()) question.Tags.Remove(qt);
        foreach (var tag in tags.Where(t => question.Tags.All(qt => qt.TagId != t.Id))) question.Tags.Add(new QuestionTag { Tag = tag, TagId = tag.Id });
        return question;
    }

    public async Task DeleteQuestionAsync(Guid id, CancellationToken ct)
    {
        var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct) ?? throw new NotFoundException("Question", id);
        q.IsDeleted = true; q.DeletedAt = _clock.UtcNow; q.Status = ContentStatus.Archived;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Delete", nameof(Question), id.ToString(), new { q.Title }, ct);
    }

    public async Task<int> BulkSetStatusAsync(BulkStatusRequest request, CancellationToken ct)
    {
        var status = EnumParser.Parse<ContentStatus>(request.Status, "status");
        var questions = await _db.Questions.Include(q => q.Options).Include(q => q.Answer).Where(q => request.Ids.Contains(q.Id) && !q.IsDeleted).ToListAsync(ct);
        var changed = 0;
        foreach (var q in questions)
        {
            if (status == ContentStatus.Published)
            {
                var errors = ValidateQuestionContent(q.Type, status, q.Options.Select(o => new OptionUpsertDto(o.Text, o.IsCorrect, o.Explanation)).ToList(), q.Answer?.ShortAnswer ?? "");
                if (errors.Count > 0) continue; // invalid drafts stay drafts
                q.PublishedAt ??= _clock.UtcNow;
            }
            q.Status = status;
            changed++;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("BulkStatus", nameof(Question), null, new { Count = changed, Status = status.ToString() }, ct);
        return changed;
    }

    // ---------------- Import / export ----------------
    private sealed record ImportRow(int Row, IReadOnlyDictionary<string, string> Values)
    {
        public string Get(string key) => Values.TryGetValue(key, out var v) ? v.Trim() : string.Empty;
    }

    private static List<ImportRow> ReadRows(Stream file, string fileName)
    {
        var rows = new List<ImportRow>();
        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            using var workbook = new XLWorkbook(file);
            var sheet = workbook.Worksheets.First();
            var header = sheet.Row(1).CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => c.GetString().Trim());
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                var values = header.ToDictionary(h => h.Value, h => row.Cell(h.Key).GetString(), StringComparer.OrdinalIgnoreCase);
                rows.Add(new ImportRow(row.RowNumber(), values));
                if (rows.Count > MaxImportRows) break;
            }
            return rows;
        }
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw ValidationAppException.For("file", "Upload a .csv or .xlsx file using the import template.");
        using var reader = new StreamReader(file);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null, BadDataFound = null, TrimOptions = TrimOptions.Trim });
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        while (csv.Read())
        {
            var values = headers.ToDictionary(h => h, h => csv.GetField(h) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            rows.Add(new ImportRow(csv.Parser.Row, values));
            if (rows.Count > MaxImportRows) break;
        }
        return rows;
    }

    public async Task<ImportResultDto> ImportQuestionsAsync(Stream file, string fileName, bool dryRun, Guid adminId, CancellationToken ct)
    {
        List<ImportRow> rows;
        try { rows = ReadRows(file, fileName); }
        catch (Exception ex) when (ex is not AppException) { throw ValidationAppException.For("file", "The file could not be read. Make sure it follows the import template."); }
        if (rows.Count > MaxImportRows) throw ValidationAppException.For("file", $"Import at most {MaxImportRows} rows per file.");

        var technologies = await _db.Technologies.Where(t => !t.IsDeleted).ToListAsync(ct);
        var categories = await _db.Categories.Where(c => !c.IsDeleted).ToListAsync(ct);
        var errors = new List<ImportErrorDto>();
        int created = 0, updated = 0, skipped = 0;

        await using var tx = dryRun ? null : await _db.Database.BeginTransactionAsync(ct);
        foreach (var row in rows)
        {
            try
            {
                var techKey = row.Get("Technology");
                var tech = technologies.FirstOrDefault(t => t.Slug == Slug.From(techKey) || t.Name.Equals(techKey, StringComparison.OrdinalIgnoreCase))
                           ?? throw ValidationAppException.For("Technology", $"Unknown technology '{techKey}'.");
                var catName = row.Get("Category");
                if (catName.Length == 0) throw ValidationAppException.For("Category", "Category is required.");
                var category = categories.FirstOrDefault(c => c.TechnologyId == tech.Id && (c.Slug == Slug.From(catName) || c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase)));
                if (category is null)
                {
                    category = new Category { TechnologyId = tech.Id, Name = catName, Slug = Slug.From(catName), DisplayOrder = 100 };
                    categories.Add(category);
                    if (!dryRun) _db.Categories.Add(category);
                }
                var optionTexts = Enumerable.Range(1, 6).Select(i => row.Get($"Option{i}")).ToList();
                var correct = row.Get("CorrectOptions").Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var n) ? n : -1).ToHashSet();
                var options = optionTexts.Select((text, i) => (text, i)).Where(x => x.text.Length > 0)
                    .Select(x => new OptionUpsertDto(x.text, correct.Contains(x.i + 1), null)).ToList();
                var request = new QuestionUpsertRequest(tech.Id, category.Id, null, row.Get("Title"), row.Get("Body"), NullIfEmpty(row.Get("CodeSnippet")),
                    NullIfEmpty(row.Get("CodeLanguage")), Default(row.Get("Type"), "MultipleChoice"), Default(row.Get("Difficulty"), "Beginner"),
                    int.TryParse(row.Get("EstimatedSeconds"), out var secs) ? Math.Clamp(secs, 10, 3600) : 90,
                    row.Get("IsPremium").Equals("true", StringComparison.OrdinalIgnoreCase) || row.Get("IsPremium") == "1",
                    Default(row.Get("Status"), "Draft"), row.Get("Tags").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    options, row.Get("ShortAnswer"), row.Get("DetailedExplanation"), NullIfEmpty(row.Get("ReferenceAnswer")));
                var validation = await new QuestionUpsertRequestValidator().ValidateAsync(request, ct);
                if (!validation.IsValid) throw new ValidationAppException(validation.Errors[0].ErrorMessage);

                var key = NullIfEmpty(row.Get("ExternalKey"));
                Question? existing = null;
                if (key is not null)
                    existing = await _db.Questions.Include(q => q.Options).Include(q => q.Answer).Include(q => q.Tags).FirstOrDefaultAsync(q => q.ExternalKey == key, ct);
                else if (await _db.Questions.AnyAsync(q => q.CategoryId == category.Id && q.Title == request.Title && !q.IsDeleted, ct))
                {
                    skipped++; // duplicate title in category without an external key
                    continue;
                }
                if (dryRun)
                {
                    var contentErrors = ValidateQuestionContent(EnumParser.Parse<QuestionType>(request.Type, "Type"), EnumParser.Parse<ContentStatus>(request.Status, "Status"), options, request.ShortAnswer);
                    if (contentErrors.Count > 0) throw new ValidationAppException(contentErrors[0]);
                }
                else
                {
                    await ApplyAsync(existing, request, adminId, key, ct);
                    await _db.SaveChangesAsync(ct);
                }
                if (existing is null) created++; else updated++;
            }
            catch (AppException ex)
            {
                errors.Add(new ImportErrorDto(row.Row, ex.Message));
                if (!dryRun) _db.ResetTracking();
            }
        }
        if (tx is not null)
        {
            await tx.CommitAsync(ct);
            await _audit.LogAsync("Import", nameof(Question), null, new { created, updated, skipped, Errors = errors.Count, fileName }, ct);
        }
        return new ImportResultDto(created, updated, skipped, errors.Take(200).ToList(), dryRun);
    }

    private static string? NullIfEmpty(string v) => string.IsNullOrWhiteSpace(v) ? null : v;
    private static string Default(string v, string d) => string.IsNullOrWhiteSpace(v) ? d : v;

    public async Task<byte[]> ExportQuestionsAsync(Guid? technologyId, string format, CancellationToken ct)
    {
        var q = _db.Questions.AsNoTracking().Where(x => !x.IsDeleted);
        if (technologyId is { } t) q = q.Where(x => x.TechnologyId == t);
        var questions = await q.Include(x => x.Technology).Include(x => x.Category).Include(x => x.Topic).Include(x => x.Options).Include(x => x.Answer)
            .Include(x => x.Tags).ThenInclude(x => x.Tag).AsSplitQuery().OrderBy(x => x.Technology.Name).ThenBy(x => x.Category.Name).ThenBy(x => x.Title).ToListAsync(ct);
        var rows = questions.Select(x =>
        {
            var opts = x.Options.OrderBy(o => o.DisplayOrder).ToList();
            var values = new List<string>
            {
                x.ExternalKey ?? x.Id.ToString(), x.Technology.Slug, x.Category.Name, x.Topic?.Name ?? "", x.Title, x.Body, x.CodeLanguage ?? "", x.CodeSnippet ?? "",
                x.Type.ToString(), x.Difficulty.ToString(), x.EstimatedSeconds.ToString(CultureInfo.InvariantCulture), x.IsPremium ? "true" : "false", x.Status.ToString(),
                string.Join(';', x.Tags.Select(tg => tg.Tag.Name))
            };
            for (var i = 0; i < 6; i++) values.Add(i < opts.Count ? opts[i].Text : "");
            values.Add(string.Join(';', opts.Select((o, i) => (o, i)).Where(p => p.o.IsCorrect).Select(p => p.i + 1)));
            values.Add(x.Answer?.ShortAnswer ?? ""); values.Add(x.Answer?.DetailedExplanation ?? ""); values.Add(x.Answer?.ReferenceAnswer ?? "");
            return values;
        }).ToList();
        return format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? ToXlsx(rows) : ToCsv(rows);
    }

    public byte[] GetImportTemplate(string format)
    {
        var sample = new List<string>
        {
            "csharp-value-vs-reference-001", "csharp", "C# Fundamentals", "", "Value types vs reference types",
            "Which statement about `struct` in C# is correct?", "", "", "MultipleChoice", "Beginner", "60", "false", "Draft", "types;memory",
            "A struct is a value type and is copied on assignment", "A struct is always allocated on the heap", "A struct can inherit from another struct", "", "", "",
            "1", "Structs are value types.", "Assigning a struct copies its value; classes are reference types.", ""
        };
        var rows = new List<List<string>> { sample };
        return format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? ToXlsx(rows) : ToCsv(rows);
    }

    private static byte[] ToCsv(IEnumerable<List<string>> rows)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, new System.Text.UTF8Encoding(true)))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var h in ImportColumns) csv.WriteField(h);
            csv.NextRecord();
            foreach (var row in rows)
            {
                foreach (var v in row) csv.WriteField(v);
                csv.NextRecord();
            }
        }
        return ms.ToArray();
    }

    private static byte[] ToXlsx(IEnumerable<List<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Questions");
        for (var i = 0; i < ImportColumns.Length; i++) sheet.Cell(1, i + 1).Value = ImportColumns[i];
        sheet.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Count; i++) sheet.Cell(r, i + 1).Value = row[i];
            r++;
        }
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ---------------- Reports ----------------
    public async Task<PagedResult<QuestionReportDto>> GetReportsAsync(PageQuery query, string? status, CancellationToken ct)
    {
        var q = _db.QuestionReports.AsNoTracking();
        if (EnumParser.ParseOptional<QuestionReportStatus>(status, "status") is { } s) q = q.Where(r => r.Status == s);
        return await q.OrderByDescending(r => r.CreatedAt)
            .Select(r => new QuestionReportDto(r.Id, r.QuestionId, r.Question.Title, r.Reason, r.Details, r.Status.ToString(), r.ResolutionNote,
                _db.Users.Where(u => u.Id == r.UserId).Select(u => u.Email!).FirstOrDefault() ?? "", r.CreatedAt))
            .ToPagedAsync(query, ct);
    }

    public async Task ResolveReportAsync(Guid id, ResolveReportRequest request, CancellationToken ct)
    {
        var report = await _db.QuestionReports.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Report", id);
        report.Status = EnumParser.Parse<QuestionReportStatus>(request.Status, "status");
        report.ResolutionNote = request.ResolutionNote?.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Resolve", nameof(QuestionReport), id.ToString(), new { Status = report.Status.ToString() }, ct);
    }
}

public sealed class QuestionUpsertRequestValidator : AbstractValidator<QuestionUpsertRequest>
{
    public QuestionUpsertRequestValidator()
    {
        RuleFor(x => x.TechnologyId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.CodeSnippet).MaximumLength(10000);
        RuleFor(x => x.CodeLanguage).MaximumLength(30);
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.Difficulty).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
        RuleFor(x => x.EstimatedSeconds).InclusiveBetween(10, 3600);
        RuleFor(x => x.Options).Must(o => o is null || o.Count <= 8).WithMessage("At most 8 options.");
        RuleForEach(x => x.Options).ChildRules(o =>
        {
            o.RuleFor(x => x.Text).NotEmpty().MaximumLength(1000);
            o.RuleFor(x => x.Explanation).MaximumLength(2000);
        });
        RuleFor(x => x.Tags).Must(t => t is null || t.Count <= 10);
        RuleFor(x => x.ShortAnswer).MaximumLength(4000);
        RuleFor(x => x.DetailedExplanation).MaximumLength(20000);
        RuleFor(x => x.ReferenceAnswer).MaximumLength(10000);
    }
}

public sealed class TechnologyUpsertRequestValidator : AbstractValidator<TechnologyUpsertRequest>
{
    public TechnologyUpsertRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Group).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CategoryUpsertRequestValidator : AbstractValidator<CategoryUpsertRequest>
{
    public CategoryUpsertRequestValidator()
    {
        RuleFor(x => x.TechnologyId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
