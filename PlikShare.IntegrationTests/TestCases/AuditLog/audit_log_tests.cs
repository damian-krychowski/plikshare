using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.AuditLog;

[Collection(IntegrationTestsCollection.Name)]
public class audit_log_tests : TestFixture
{
    public audit_log_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task signing_in_should_create_audit_log_entry()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when
        var result = await GetLogsWithRetry(
            request: new GetAuditLogRequestDto
            {
                PageSize = 50,
                EventTypes = [AuditLogEventTypes.Auth.SignedIn]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery,
            predicate: r => r.Items.Any(i => i.EventType == AuditLogEventTypes.Auth.SignedIn));

        //then
        result.Items.Should().Contain(item =>
            item.EventType == AuditLogEventTypes.Auth.SignedIn &&
            item.ActorEmail == Users.AppOwner.Email);
    }

    [Fact]
    public async Task audit_log_stats_should_return_db_info()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        await Task.Delay(500);

        //when
        var stats = await Api.AuditLog.GetStats(
            cookie: appOwner.Cookie);

        //then
        stats.DbSizeInBytes.Should().BeGreaterThan(0);
        stats.TotalLogCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task audit_log_should_support_cursor_pagination()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        await SignIn(Users.AppOwner);
        await SignIn(Users.AppOwner);
        await Task.Delay(500);

        //when
        var firstPage = await Api.AuditLog.GetLogs(
            request: new GetAuditLogRequestDto { PageSize = 1 },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then
        firstPage.Items.Should().HaveCount(1);
        firstPage.HasMore.Should().BeTrue();

        var secondPage = await Api.AuditLog.GetLogs(
            request: new GetAuditLogRequestDto
            {
                PageSize = 1,
                Cursor = firstPage.NextCursor
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        secondPage.Items.Should().HaveCount(1);
        secondPage.Items[0].ExternalId.Should().NotBe(firstPage.Items[0].ExternalId);
    }

    [Fact]
    public async Task audit_log_should_filter_by_event_category()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when
        var result = await GetLogsWithRetry(
            request: new GetAuditLogRequestDto
            {
                PageSize = 50,
                EventCategories = [AuditLogEventCategories.Auth]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery,
            predicate: r => r.Items.Count > 0);

        //then
        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(item => item.EventCategory == AuditLogEventCategories.Auth);
    }

    [Fact]
    public async Task audit_log_should_filter_by_severity()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when
        var result = await GetLogsWithRetry(
            request: new GetAuditLogRequestDto
            {
                PageSize = 50,
                Severities = [AuditLogSeverities.Info]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery,
            predicate: r => r.Items.Count > 0);

        //then
        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(item => item.EventSeverity == AuditLogSeverities.Info);
    }

    [Fact]
    public async Task failed_sign_in_should_create_warning_audit_log_entry()
    {
        //given
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        await Api.Auth.SignIn(
            email: Users.AppOwner.Email,
            password: "wrong-password",
            antiforgeryCookies: anonymousAntiforgeryCookies);

        var appOwner = await SignIn(Users.AppOwner);

        //when
        var result = await GetLogsWithRetry(
            request: new GetAuditLogRequestDto
            {
                PageSize = 50,
                EventTypes = [AuditLogEventTypes.Auth.SignInFailed]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery,
            predicate: r => r.Items.Any(i => i.EventType == AuditLogEventTypes.Auth.SignInFailed));

        //then
        result.Items.Should().Contain(item =>
            item.EventType == AuditLogEventTypes.Auth.SignInFailed &&
            item.EventSeverity == AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task delete_old_logs_should_remove_entries()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        await Task.Delay(500);

        var statsBefore = await Api.AuditLog.GetStats(cookie: appOwner.Cookie);
        statsBefore.TotalLogCount.Should().BeGreaterThan(0);

        //when
        var futureDate = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var deleteResult = await Api.AuditLog.DeleteOldLogs(
            request: new DeleteOldAuditLogsRequestDto { OlderThanDate = futureDate },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then
        deleteResult.DeletedCount.Should().BeGreaterThan(0);

        var statsAfter = await Api.AuditLog.GetStats(cookie: appOwner.Cookie);
        statsAfter.TotalLogCount.Should().Be(0);
    }

    [Fact]
    public async Task archive_logs_should_create_file()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        await Task.Delay(500);

        //when
        var archiveResult = await Api.AuditLog.ArchiveLogs(
            request: new ArchiveAuditLogsRequestDto(),
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then
        archiveResult.ArchivedCount.Should().BeGreaterThanOrEqualTo(0);
        archiveResult.FileName.Should().StartWith("audit-log-archive-");
        archiveResult.FileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task non_admin_should_not_access_audit_log()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        var regularUser = await InviteAndRegisterUser(appOwner);

        //when / then
        var act = async () => await Api.AuditLog.GetLogs(
            request: new GetAuditLogRequestDto { PageSize = 10 },
            cookie: regularUser.Cookie,
            antiforgery: regularUser.Antiforgery);

        await act.Should().ThrowAsync<TestApiCallException>()
            .Where(e => e.StatusCode == 403);
    }

    private async Task<GetAuditLogResponseDto> GetLogsWithRetry(
        GetAuditLogRequestDto request,
        SessionAuthCookie cookie,
        AntiforgeryCookies antiforgery,
        Func<GetAuditLogResponseDto, bool>? predicate = null,
        int maxRetries = 20)
    {
        for (var i = 0; i < maxRetries; i++)
        {
            var result = await Api.AuditLog.GetLogs(
                request, 
                cookie, 
                antiforgery);

            if (predicate == null || predicate(result))
                return result;

            await Task.Delay(100);
        }

        return await Api.AuditLog.GetLogs(
            request, 
            cookie, 
            antiforgery);
    }
}
