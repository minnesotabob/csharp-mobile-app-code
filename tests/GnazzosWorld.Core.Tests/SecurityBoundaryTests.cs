using GnazzosWorld.Core.Catalog;
using GnazzosWorld.Core.Identity;
using GnazzosWorld.Core.Networking;
using Xunit;

namespace GnazzosWorld.Core.Tests;

public sealed class SecurityBoundaryTests
{
    [Theory]
    [InlineData("https://johngnazzo.com@attacker.example/book")]
    [InlineData("https://johngnazzо.com/book")]
    [InlineData("https://johngnazzo.com./book")]
    public void Publisher_lookalikes_do_not_inherit_reviewed_host_permission(string value)
        => Assert.False(ContentLinks.IsAllowed(new(value)));

    [Theory]
    [InlineData("https://fixture.auth0.com.attacker.example/authorize", NativeIdentitySettings.Callback)]
    [InlineData("https://fixture.auth0.com@attacker.example/authorize", NativeIdentitySettings.Callback)]
    [InlineData("https://fixture.auth0.com/authorize/extra", NativeIdentitySettings.Callback)]
    [InlineData("https://fixture.auth0.com/authorize#code=secret", NativeIdentitySettings.Callback)]
    [InlineData("https://fixture.auth0.com/authorize", "com.johngnazzo.gnazzosworld://callback/extra")]
    public void Browser_destination_and_callback_must_match_the_configured_boundary(string start, string end)
    {
        var settings = new NativeIdentitySettings("fixture.auth0.com", "public-client-id");
        Assert.Throws<InvalidDataException>(() => settings.ValidateBrowserRequest(start, end));
    }
    [Theory]
    [InlineData("../customer")]
    [InlineData("https://attacker.example/")]
    [InlineData("customer?account=other")]
    [InlineData("customer%2fsecret")]
    [InlineData("customer.name")]
    [InlineData("reader\nlog")]
    public void Private_request_id_is_never_a_path_url_or_account_selector(string id)
        => Assert.False(RequestIdPolicy.IsValid(id));
    [Fact]
    public void Request_id_bound_accepts_exactly_the_documented_alphabet_and_length()
    {
        Assert.True(RequestIdPolicy.IsValid("A_z-019"));
        Assert.True(RequestIdPolicy.IsValid(new string('a', 64)));
        Assert.False(RequestIdPolicy.IsValid(new string('a', 65)));
        Assert.False(RequestIdPolicy.IsValid(null)); Assert.False(RequestIdPolicy.IsValid(""));
    }
}
