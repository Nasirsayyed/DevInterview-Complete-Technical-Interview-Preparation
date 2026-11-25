using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;

namespace DevInterview.IntegrationTests;

/// <summary>Runs only when DEVINTERVIEW_TEST_SQLSERVER holds a SQL Server connection string (server-level; a database is created per run).</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVINTERVIEW_TEST_SQLSERVER")))
            Skip = "Set DEVINTERVIEW_TEST_SQLSERVER to run the SQL Server migration tests.";
    }
}

public sealed class SqlServerFactory : ApiFactory
{
    private readonly string _database = $"DevInterviewTest_{Guid.NewGuid():N}";
    private string ConnectionString => new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DEVINTERVIEW_TEST_SQLSERVER") ?? "") { InitialCatalog = _database }.ConnectionString;

    protected override IDictionary<string, string?> Overrides => new Dictionary<string, string?>
    {
        ["Database:Provider"] = "SqlServer",
        ["ConnectionStrings:Default"] = ConnectionString
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVINTERVIEW_TEST_SQLSERVER"))) return;
        SqlConnection.ClearAllPools();
        using var conn = new SqlConnection(new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" }.ConnectionString);
        conn.Open();
        using var cmd = new SqlCommand($"ALTER DATABASE [{_database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_database}];", conn);
        cmd.ExecuteNonQuery();
    }
}

public class SqlServerTests : IClassFixture<SqlServerFactory>
{
    private readonly SqlServerFactory _factory;
    public SqlServerTests(SqlServerFactory factory) => _factory = factory;

    [SqlServerFact]
    public async Task Migrations_apply_and_core_flows_work_on_sql_server()
    {
        var (client, _) = await _factory.NewUserAsync();
        var questions = await (await client.GetAsync("/api/v1/questions?pageSize=5&sort=difficulty")).DataAsync();
        Assert.True(questions.GetProperty("totalCount").GetInt32() > 200);
        Assert.Equal("Beginner", questions.GetProperty("items")[0].GetProperty("difficulty").GetString());

        var challenges = await (await client.GetAsync("/api/v1/challenges?pageSize=5")).DataAsync();
        Assert.Equal("Easy", challenges.GetProperty("items")[0].GetProperty("difficulty").GetString());

        var quiz = await (await client.GetAsync("/api/v1/quizzes/daily")).DataAsync();
        var attempt = await (await client.PostAsync($"/api/v1/quizzes/{quiz.GetProperty("id")}/start", null)).DataAsync();
        var submit = await client.PostAsJsonAsync($"/api/v1/quizzes/{quiz.GetProperty("id")}/submit", new { attemptId = attempt.GetProperty("attemptId"), answers = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/dashboard/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/analytics/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/search?q=join")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/flashcards/decks")).StatusCode);
        var admin = await _factory.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/reports/content")).StatusCode);
    }
}
