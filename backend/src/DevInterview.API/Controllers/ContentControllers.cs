using System.Text;
using Asp.Versioning;
using DevInterview.API.Infrastructure;
using DevInterview.Application.Abstractions;
using DevInterview.Application.Features.Companies;
using DevInterview.Application.Features.Library;
using DevInterview.Application.Features.Questions;
using DevInterview.Contracts.Common;
using DevInterview.Contracts.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInterview.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/technologies")]
public sealed class TechnologiesController : ApiControllerBase
{
    private readonly IQuestionService _questions;
    public TechnologiesController(IQuestionService questions) => _questions = questions;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TechnologyDto>>>> List(CancellationToken ct) => Success(await _questions.GetTechnologiesAsync(ct));

    [HttpGet("{id:guid}/categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryDto>>>> Categories(Guid id, CancellationToken ct) =>
        Success(await _questions.GetCategoriesAsync(id, ct));
}

/// <summary>Question bank: browsing, practice attempts, answers, bookmarks, notes and reports.</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/questions")]
public sealed class QuestionsController : ApiControllerBase
{
    private readonly IQuestionService _questions;
    private readonly ICurrentUser _user;

    public QuestionsController(IQuestionService questions, ICurrentUser user)
    {
        _questions = questions;
        _user = user;
    }

    /// <summary>Paged, filterable question list. Sort: difficulty (default), difficulty_desc, title, newest.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<QuestionListItemDto>>>> List([FromQuery] QuestionQuery query, CancellationToken ct) =>
        Success(await _questions.GetQuestionsAsync(_user.RequireUserId(), query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QuestionDetailDto>>> Get(Guid id, CancellationToken ct) =>
        Success(await _questions.GetQuestionAsync(_user.RequireUserId(), id, ct));

    [HttpGet("random")]
    public async Task<ActionResult<ApiResponse<QuestionDetailDto>>> Random([FromQuery] Guid? technologyId, [FromQuery] string? difficulty, CancellationToken ct) =>
        Success(await _questions.GetRandomAsync(_user.RequireUserId(), technologyId, difficulty, ct));

    /// <summary>Reveals the answer and detailed explanation (counts toward the free daily limit).</summary>
    [HttpGet("{id:guid}/answer")]
    public async Task<ActionResult<ApiResponse<QuestionAnswerDto>>> Answer(Guid id, CancellationToken ct) =>
        Success(await _questions.GetAnswerAsync(_user.RequireUserId(), id, ct));

    /// <summary>Submits a practice attempt; objective questions are graded server-side.</summary>
    [HttpPost("{id:guid}/attempt")]
    public async Task<ActionResult<ApiResponse<AttemptQuestionResult>>> Attempt(Guid id, AttemptQuestionRequest request, CancellationToken ct) =>
        Success(await _questions.AttemptAsync(_user.RequireUserId(), id, request, ct));

    [HttpPost("{id:guid}/bookmark")]
    public async Task<ActionResult<ApiResponse<object>>> Bookmark(Guid id, CancellationToken ct)
    {
        await _questions.SetBookmarkAsync(_user.RequireUserId(), id, true, ct);
        return Done("Bookmarked.");
    }

    [HttpDelete("{id:guid}/bookmark")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveBookmark(Guid id, CancellationToken ct)
    {
        await _questions.SetBookmarkAsync(_user.RequireUserId(), id, false, ct);
        return Done("Bookmark removed.");
    }

    [HttpGet("bookmarks")]
    public async Task<ActionResult<ApiResponse<PagedResult<QuestionListItemDto>>>> Bookmarks([FromQuery] QuestionQuery query, CancellationToken ct) =>
        Success(await _questions.GetBookmarksAsync(_user.RequireUserId(), query, ct));

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<ApiResponse<NoteDto>>> SaveNote(Guid id, SaveNoteRequest request, CancellationToken ct) =>
        Success(await _questions.SaveNoteAsync(_user.RequireUserId(), id, request, ct));

    [HttpDelete("{id:guid}/notes")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteNote(Guid id, CancellationToken ct)
    {
        await _questions.DeleteNoteAsync(_user.RequireUserId(), id, ct);
        return Done("Note deleted.");
    }

    [HttpGet("notes")]
    public async Task<ActionResult<ApiResponse<PagedResult<NoteDto>>>> Notes([FromQuery] PageQuery query, [FromQuery] Guid? technologyId, CancellationToken ct) =>
        Success(await _questions.GetNotesAsync(_user.RequireUserId(), query, technologyId, ct));

    /// <summary>Exports all notes as Markdown (default) or CSV.</summary>
    [HttpGet("notes/export")]
    public async Task<IActionResult> ExportNotes([FromQuery] string format = "markdown", CancellationToken ct = default)
    {
        var content = await _questions.ExportNotesAsync(_user.RequireUserId(), format, ct);
        var csv = format.Equals("csv", StringComparison.OrdinalIgnoreCase);
        return File(Encoding.UTF8.GetBytes(content), csv ? "text/csv" : "text/markdown", csv ? "devinterview-notes.csv" : "devinterview-notes.md");
    }

    [HttpPost("{id:guid}/report")]
    public async Task<ActionResult<ApiResponse<object>>> Report(Guid id, ReportQuestionRequest request, CancellationToken ct)
    {
        await _questions.ReportAsync(_user.RequireUserId(), id, request, ct);
        return Done("Thanks! Our content team will review this question.");
    }

    [HttpGet("report-reasons")]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> ReportReasons() => Success<IReadOnlyList<string>>(ReportQuestionRequestValidator.Reasons);
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/companies")]
public sealed class CompaniesController : ApiControllerBase
{
    private readonly ICompanyService _companies;
    private readonly ICurrentUser _user;

    public CompaniesController(ICompanyService companies, ICurrentUser user)
    {
        _companies = companies;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CompanyListItemDto>>>> List([FromQuery] string? search, CancellationToken ct) =>
        Success(await _companies.ListAsync(search, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CompanyDetailDto>>> Get(Guid id, CancellationToken ct) => Success(await _companies.GetAsync(_user.RequireUserId(), id, ct));

    [HttpGet("collections/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CompanyCollectionDetailDto>>> Collection(Guid id, CancellationToken ct) =>
        Success(await _companies.GetCollectionAsync(_user.RequireUserId(), id, ct));
}

/// <summary>Custom collections of saved questions, challenges and evaluated answers.</summary>
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/collections")]
public sealed class CollectionsController : ApiControllerBase
{
    private readonly ICollectionService _collections;
    private readonly ICurrentUser _user;

    public CollectionsController(ICollectionService collections, ICurrentUser user)
    {
        _collections = collections;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CollectionDto>>>> List(CancellationToken ct) => Success(await _collections.ListAsync(_user.RequireUserId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CollectionDetailDto>>> Get(Guid id, CancellationToken ct) => Success(await _collections.GetAsync(_user.RequireUserId(), id, ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CollectionDto>>> Create(CreateCollectionRequest request, CancellationToken ct) =>
        Created(await _collections.CreateAsync(_user.RequireUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CollectionDto>>> Rename(Guid id, CreateCollectionRequest request, CancellationToken ct) =>
        Success(await _collections.RenameAsync(_user.RequireUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _collections.DeleteAsync(_user.RequireUserId(), id, ct);
        return Done("Collection deleted.");
    }

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<ApiResponse<CollectionItemDto>>> AddItem(Guid id, AddCollectionItemRequest request, CancellationToken ct) =>
        Success(await _collections.AddItemAsync(_user.RequireUserId(), id, request, ct));

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveItem(Guid id, Guid itemId, CancellationToken ct)
    {
        await _collections.RemoveItemAsync(_user.RequireUserId(), id, itemId, ct);
        return Done("Item removed.");
    }
}

[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/search")]
public sealed class SearchController : ApiControllerBase
{
    private readonly ISearchService _search;
    private readonly ICurrentUser _user;

    public SearchController(ISearchService search, ICurrentUser user)
    {
        _search = search;
        _user = user;
    }

    /// <summary>Global search across questions, technologies, categories, challenges, learning paths, flashcards and the user's notes.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<SearchResultsDto>>> Search([FromQuery] SearchQuery query, CancellationToken ct) =>
        Success(await _search.SearchAsync(_user.RequireUserId(), query, ct));
}
