using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Investments.Api.Tests;

public sealed class InvestmentsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ---------- owners ----------

    [Fact]
    public async Task Registered_people_are_listed()
    {
        var id = await CreateOwnerAsync();

        var owners = await GetJsonAsync("/api/owners");

        Assert.Contains(owners.EnumerateArray(), o => o.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Duplicated_email_is_a_conflict_even_with_different_casing()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/owners", new { name = "Ana", email });

        var response = await _client.PostAsJsonAsync("/api/owners", new { name = "Other", email = email.ToUpperInvariant() });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "owner.email_in_use");
    }

    // ---------- creation ----------

    [Fact]
    public async Task Create_returns_the_investment_and_its_location_even_when_the_email_fails()
    {
        var ownerId = await CreateOwnerAsync();

        var response = await PostInvestmentAsync(ownerId, 1000m, "2025-01-10");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith($"/api/investments/{body.GetProperty("id").GetGuid()}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Active", body.GetProperty("status").GetString());
        Assert.Equal(ownerId, body.GetProperty("owner").GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("-1", "2025-01-10", "investment.amount_not_positive")]
    [InlineData("0", "2025-01-10", "investment.amount_not_positive")]
    [InlineData("10.001", "2025-01-10", "investment.amount_precision")]
    [InlineData("1000", "2026-06-16", "investment.creation_in_future")] // tomorrow in São Paulo, already "today" in UTC
    [InlineData("1000", "0001-01-01", "investment.creation_too_old")]
    public async Task Create_rejects_business_rule_violations(string amount, string date, string code)
    {
        var ownerId = await CreateOwnerAsync();

        var response = await PostInvestmentAsync(ownerId, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), date);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Create_accepts_today()
    {
        var response = await PostInvestmentAsync(await CreateOwnerAsync(), 1000m, "2026-06-15");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("""{"ownerId":"abc","amount":1,"creationDate":"2025-01-10"}""")]
    public async Task Create_rejects_malformed_bodies(string body)
    {
        var response = await _client.PostAsync("/api/investments", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_for_an_unknown_owner_is_not_found()
    {
        var response = await PostInvestmentAsync(Guid.NewGuid(), 1000m, "2025-01-10");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "owner.not_found");
    }

    // ---------- view ----------

    [Fact]
    public async Task View_shows_initial_amount_gains_and_expected_balance()
    {
        // From 2025-06-15 to 2026-06-15: exactly 12 monthly payments.
        var id = await CreateInvestmentAsync(1000m, "2025-06-15");

        var investment = await GetJsonAsync($"/api/investments/{id}");

        Assert.Equal(1000m, investment.GetProperty("initialAmount").GetDecimal());
        Assert.Equal(64.22m, investment.GetProperty("gains").GetDecimal());
        Assert.Equal(1064.22m, investment.GetProperty("expectedBalance").GetDecimal());
        Assert.Equal(JsonValueKind.Null, investment.GetProperty("withdrawal").ValueKind);
    }

    [Fact]
    public async Task View_of_an_unknown_investment_is_not_found()
    {
        var response = await _client.GetAsync($"/api/investments/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "investment.not_found");
    }

    [Fact]
    public async Task View_shows_the_next_gain_and_a_withdrawal_today_while_active()
    {
        var id = await CreateInvestmentAsync(1000m, "2025-06-15");

        var accrual = (await GetJsonAsync($"/api/investments/{id}")).GetProperty("accrual");

        Assert.Equal(0.0052m, accrual.GetProperty("monthlyGainRate").GetDecimal());
        Assert.Equal(12, accrual.GetProperty("completedMonths").GetInt32());
        Assert.Equal("2026-07-15", accrual.GetProperty("nextGainDate").GetString());
        Assert.Equal(5.53m, accrual.GetProperty("nextGainAmount").GetDecimal());
        Assert.Equal(1052.34m, accrual.GetProperty("withdrawalToday").GetProperty("netAmount").GetDecimal());
    }

    [Fact]
    public async Task View_has_no_accrual_after_the_withdrawal()
    {
        var id = await CreateInvestmentAsync(1000m, "2025-01-10");

        var withdrawn = await (await WithdrawAsync(id, "2026-01-10")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Null, withdrawn.GetProperty("accrual").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await GetJsonAsync($"/api/investments/{id}")).GetProperty("accrual").ValueKind);
    }

    [Fact]
    public async Task Growth_has_the_paid_months_and_a_one_year_projection()
    {
        var id = await CreateInvestmentAsync(1000m, "2026-03-15");

        var points = (await GetJsonAsync($"/api/investments/{id}/growth")).EnumerateArray().ToList();

        Assert.Equal(4 + 12, points.Count);
        Assert.Equal(4, points.Count(p => !p.GetProperty("projected").GetBoolean()));
        Assert.Equal("2026-03-15", points[0].GetProperty("date").GetString());
        Assert.Equal(1000m, points[0].GetProperty("balance").GetDecimal());
        Assert.Equal("2027-06-15", points[^1].GetProperty("date").GetString());
    }

    [Fact]
    public async Task Growth_of_an_unknown_investment_is_not_found()
    {
        await AssertProblemAsync(await _client.GetAsync($"/api/investments/{Guid.NewGuid()}/growth"), HttpStatusCode.NotFound, "investment.not_found");
    }

    // ---------- rules and simulation ----------

    [Fact]
    public async Task Rules_expose_the_configured_rates()
    {
        var rules = await GetJsonAsync("/api/rules");

        Assert.Equal(0.0052m, rules.GetProperty("monthlyGainRate").GetDecimal());
        Assert.Equal(0.225m, rules.GetProperty("taxRateUnderOneYear").GetDecimal());
        Assert.Equal(0.185m, rules.GetProperty("taxRateOneToTwoYears").GetDecimal());
        Assert.Equal(0.15m, rules.GetProperty("taxRateOverTwoYears").GetDecimal());
    }

    [Fact]
    public async Task Simulation_returns_the_taxed_values_and_creates_nothing()
    {
        var before = (await GetJsonAsync("/api/investments")).GetProperty("totalItems").GetInt32();

        var result = await GetJsonAsync("/api/investments/simulation?amount=1000.00&creationDate=2026-06-15&months=12");

        Assert.Equal("2027-06-15", result.GetProperty("date").GetString());
        Assert.Equal(1064.22m, result.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(0.185m, result.GetProperty("taxRate").GetDecimal());
        Assert.Equal(1052.34m, result.GetProperty("netAmount").GetDecimal());
        Assert.Equal(before, (await GetJsonAsync("/api/investments")).GetProperty("totalItems").GetInt32());
    }

    [Theory]
    [InlineData("amount=1000&creationDate=2026-06-15&months=0", "simulation.months_out_of_range")]
    [InlineData("amount=1000&creationDate=2026-06-15&months=601", "simulation.months_out_of_range")]
    [InlineData("amount=-1&creationDate=2026-06-15&months=12", "investment.amount_not_positive")]
    [InlineData("amount=1000&creationDate=2026-06-16&months=12", "investment.creation_in_future")]
    public async Task Simulation_rejects_broken_rules(string query, string code)
    {
        await AssertProblemAsync(await _client.GetAsync($"/api/investments/simulation?{query}"), HttpStatusCode.BadRequest, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("amount=abc&creationDate=2026-06-15&months=12")]
    [InlineData("amount=1000&months=12")]
    public async Task Simulation_rejects_missing_or_malformed_parameters(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/investments/simulation?{query}")).StatusCode);
    }

    // ---------- withdrawal ----------

    [Fact]
    public async Task Preview_then_withdraw_then_view_keeps_the_same_frozen_values()
    {
        var id = await CreateInvestmentAsync(1000m, "2025-01-10");

        var preview = await GetJsonAsync($"/api/investments/{id}/withdrawal-preview?date=2026-01-10");
        Assert.Equal(11.88m, preview.GetProperty("taxAmount").GetDecimal());
        Assert.Equal(1052.34m, preview.GetProperty("netAmount").GetDecimal());
        Assert.Equal("Active", (await GetJsonAsync($"/api/investments/{id}")).GetProperty("status").GetString());

        var response = await WithdrawAsync(id, "2026-01-10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var investment = await GetJsonAsync($"/api/investments/{id}");
        Assert.Equal("Withdrawn", investment.GetProperty("status").GetString());
        Assert.Equal(1064.22m, investment.GetProperty("expectedBalance").GetDecimal()); // 17 months have passed, only 12 count
        Assert.Equal(JsonSerializer.Serialize(preview), JsonSerializer.Serialize(investment.GetProperty("withdrawal")));
    }

    [Theory]
    [InlineData("2025-01-09", "withdrawal.before_creation")]
    [InlineData("2026-06-16", "withdrawal.in_future")]
    public async Task Invalid_dates_are_rejected_by_preview_and_withdrawal(string date, string code)
    {
        var id = await CreateInvestmentAsync(1000m, "2025-01-10");

        await AssertProblemAsync(await _client.GetAsync($"/api/investments/{id}/withdrawal-preview?date={date}"), HttpStatusCode.BadRequest, code);
        await AssertProblemAsync(await WithdrawAsync(id, date), HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Withdrawal_date_is_mandatory()
    {
        var id = await CreateInvestmentAsync(1000m, "2025-01-10");

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/investments/{id}/withdrawal-preview")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync($"/api/investments/{id}/withdrawal", new { })).StatusCode);
    }

    [Fact]
    public async Task Second_withdrawal_is_a_conflict()
    {
        var id = await CreateInvestmentAsync(1000m, "2025-01-10");
        await WithdrawAsync(id, "2026-01-10");

        await AssertProblemAsync(await WithdrawAsync(id, "2026-01-11"), HttpStatusCode.Conflict, "investment.already_withdrawn");
    }

    [Fact]
    public async Task Only_one_of_many_simultaneous_withdrawals_succeeds()
    {
        var id = await CreateInvestmentAsync(1000m, "2025-01-10");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => WithdrawAsync(id, "2026-01-10")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    // ---------- list ----------

    [Fact]
    public async Task List_is_paginated_and_filtered_by_person()
    {
        var ownerId = await CreateOwnerAsync();
        for (var day = 1; day <= 5; day++)
        {
            await PostInvestmentAsync(ownerId, 100m * day, $"2025-03-0{day}");
        }

        await CreateInvestmentAsync(1m, "2025-03-01"); // someone else's

        var first = await GetJsonAsync($"/api/investments?ownerId={ownerId}&page=1&pageSize=2");
        var last = await GetJsonAsync($"/api/investments?ownerId={ownerId}&page=3&pageSize=2");
        var beyond = await GetJsonAsync($"/api/investments?ownerId={ownerId}&page=4&pageSize=2");

        Assert.Equal(5, first.GetProperty("totalItems").GetInt32());
        Assert.Equal(3, first.GetProperty("totalPages").GetInt32());
        Assert.Equal([500m, 400m], Amounts(first)); // newest first
        Assert.Equal([100m], Amounts(last));
        Assert.Empty(Amounts(beyond));
    }

    [Theory]
    [InlineData("Active", new[] { 300, 100 })]
    [InlineData("Withdrawn", new[] { 200 })]
    public async Task List_is_filtered_by_status(string status, int[] expected)
    {
        var ownerId = await CreateOwnerAsync();
        await PostInvestmentAsync(ownerId, 100m, "2025-03-01");
        var second = await PostInvestmentAsync(ownerId, 200m, "2025-03-02");
        await PostInvestmentAsync(ownerId, 300m, "2025-03-03");
        await WithdrawAsync((await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), "2026-01-10");

        var list = await GetJsonAsync($"/api/investments?ownerId={ownerId}&status={status}");

        Assert.Equal(expected.Select(v => (decimal)v), Amounts(list));
        Assert.Equal(expected.Length, list.GetProperty("totalItems").GetInt32());
    }

    [Theory]
    [InlineData("", new[] { 1000, 9, 20 })] // newest first
    [InlineData("&sortBy=CreationDate&sortDirection=Asc", new[] { 20, 9, 1000 })]
    [InlineData("&sortBy=Amount&sortDirection=Asc", new[] { 9, 20, 1000 })] // as numbers, not as text
    [InlineData("&sortBy=Amount&sortDirection=Desc", new[] { 1000, 20, 9 })]
    public async Task List_is_sorted(string sort, int[] expected)
    {
        var ownerId = await CreateOwnerAsync();
        await PostInvestmentAsync(ownerId, 20m, "2025-03-01");
        await PostInvestmentAsync(ownerId, 9m, "2025-03-02");
        await PostInvestmentAsync(ownerId, 1000m, "2025-03-03");

        var list = await GetJsonAsync($"/api/investments?ownerId={ownerId}{sort}");

        Assert.Equal(expected.Select(v => (decimal)v), Amounts(list));
    }

    [Fact]
    public async Task List_is_sorted_by_owner_name_ignoring_case()
    {
        using var isolated = new ApiFactory();
        var client = isolated.CreateClient();
        foreach (var name in new[] { "Carla", "bruno", "Ana" })
        {
            var owner = await (await client.PostAsJsonAsync("/api/owners", new { name, email = $"{name}@example.com" })).Content.ReadFromJsonAsync<JsonElement>();
            await client.PostAsJsonAsync("/api/investments", new { ownerId = owner.GetProperty("id").GetGuid(), amount = 1m, creationDate = "2025-03-01" });
        }

        var list = await client.GetFromJsonAsync<JsonElement>("/api/investments?sortBy=Owner&sortDirection=Asc");

        Assert.Equal(["Ana", "bruno", "Carla"], list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("owner").GetProperty("name").GetString()));
    }

    [Fact]
    public async Task List_without_filter_includes_everyone()
    {
        var a = await CreateInvestmentAsync(10m, "2026-06-15");
        var b = await CreateInvestmentAsync(20m, "2026-06-15");

        var list = await GetJsonAsync("/api/investments?pageSize=100");

        var ids = list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(a, ids);
        Assert.Contains(b, ids);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    [InlineData("status=Unknown")]
    [InlineData("status=7")]
    [InlineData("sortBy=Balance")]
    [InlineData("sortDirection=Up")]
    public async Task List_rejects_invalid_parameters(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/investments?{query}")).StatusCode);
    }

    // ---------- platform ----------

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/docs")]
    public async Task Documentation_is_available(string path)
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Cors_allows_only_the_configured_frontend()
    {
        Assert.True(await IsOriginAllowedAsync("http://localhost:4200"));
        Assert.False(await IsOriginAllowedAsync("http://evil.example"));
    }

    // ---------- helpers ----------

    private async Task<bool> IsOriginAllowedAsync(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/owners");
        request.Headers.Add("Origin", origin);
        var response = await _client.SendAsync(request);

        return response.Headers.Contains("Access-Control-Allow-Origin");
    }

    private static decimal[] Amounts(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("initialAmount").GetDecimal())];

    private async Task<Guid> CreateOwnerAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/owners", new { name = "Ana Souza", email = $"{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PostInvestmentAsync(Guid ownerId, decimal amount, string creationDate) =>
        _client.PostAsJsonAsync("/api/investments", new { ownerId, amount, creationDate });

    private async Task<Guid> CreateInvestmentAsync(decimal amount, string creationDate)
    {
        var response = await PostInvestmentAsync(await CreateOwnerAsync(), amount, creationDate);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> WithdrawAsync(Guid id, string date) =>
        _client.PostAsJsonAsync($"/api/investments/{id}/withdrawal", new { date });

    private Task<JsonElement> GetJsonAsync(string url) => _client.GetFromJsonAsync<JsonElement>(url);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
