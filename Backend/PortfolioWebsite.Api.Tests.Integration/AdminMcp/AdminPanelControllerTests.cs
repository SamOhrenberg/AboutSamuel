using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using PortfolioWebsite.Api.Tests.Integration.Infrastructure;
using static PortfolioWebsite.Api.Tests.Integration.Infrastructure.PortfolioSeed;

namespace PortfolioWebsite.Api.Tests.Integration.AdminMcp;

/// <summary>
/// The admin panel's controllers now go through the same services as the admin MCP tools.
/// These check the panel still behaves as before, plus the two fixes that came with the move.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminPanelControllerTests(ApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _http = factory.CreateClient();

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminPanelToken.Create());
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Information_update_replaces_keywords()
    {
        using var create = await _http.PostAsJsonAsync("/admin/information", new { text = "Likes Postgres.", keywords = new[] { "db" } });
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("informationId").GetGuid();

        using var update = await _http.PutAsJsonAsync($"/admin/information/{id}", new { text = "Likes PostgreSQL.", keywords = new[] { "sql", "postgres" } });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        using var add = await _http.PostAsJsonAsync($"/admin/information/{id}/keywords", new { text = "pg" });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        using var doc = JsonDocument.Parse(await _http.GetStringAsync($"/admin/information/{id}"));
        Assert.Equal(["pg", "postgres", "sql"], doc.RootElement.GetProperty("keywords").EnumerateArray().Select(e => e.GetString()).Order());
    }

    [Fact]
    public async Task Project_responses_keep_their_shapes()
    {
        using var missing = await _http.PutAsJsonAsync($"/admin/projects/{Guid.NewGuid()}", new { title = "t", role = "r", summary = "s" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var badLink = await _http.PostAsJsonAsync("/admin/projects", new { title = "t", role = "r", summary = "s", workExperienceIds = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.BadRequest, badLink.StatusCode);
        Assert.Contains("WorkExperienceIds", await badLink.Content.ReadAsStringAsync());

        using var delete = await _http.DeleteAsync($"/admin/projects/{BillingRewrite}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Work_experience_create_returns_201_with_a_location()
    {
        using var response = await _http.PostAsJsonAsync("/admin/Work-experience", new { employer = "Initech", title = "Engineer" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Work_experience_needs_the_admin_role_like_every_other_admin_controller()
    {
        using var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenWithoutRole());

        using var response = await http.GetAsync("/admin/Work-experience");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A validly signed panel token with no Admin role.</summary>
    private static string TokenWithoutRole()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AdminPanelToken.Secret));
        var token = new JwtSecurityToken(
            claims:
            [
                new(JwtRegisteredClaimNames.Iss, "aboutsamuel.com"),
                new(JwtRegisteredClaimNames.Aud, "aboutsamuel-admin"),
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
