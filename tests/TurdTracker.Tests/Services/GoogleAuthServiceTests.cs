using Bunit;
using FluentAssertions;
using TurdTracker.Services;
using Xunit;

namespace TurdTracker.Tests.Services;

public class GoogleAuthServiceTests : IDisposable
{
    private readonly BunitContext _ctx;
    private readonly GoogleAuthService _sut;

    public GoogleAuthServiceTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _sut = new GoogleAuthService(_ctx.JSInterop.JSRuntime);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _ctx.Dispose();
    }

    [Fact]
    public async Task InitializeAsync_CallsJsInitializeOnce_WhenCalledMultipleTimes()
    {
        await _sut.InitializeAsync();
        await _sut.InitializeAsync();

        var invocations = _ctx.JSInterop.Invocations["googleAuth.initialize"];
        invocations.Should().HaveCount(1, "initialize should be idempotent");
        invocations[0].Arguments[0].Should().Be("101133685796-p43f4uqejgt9lgce4lvibrnoam8oegb1.apps.googleusercontent.com");
    }

    [Fact]
    public async Task SignInAsync_CallsEnsureInitializedThenJsSignIn()
    {
        _ctx.JSInterop.Setup<string?>("googleAuth.signIn").SetResult("test-token");

        var result = await _sut.SignInAsync();

        _ctx.JSInterop.Invocations["googleAuth.initialize"].Should().HaveCount(1);
        _ctx.JSInterop.Invocations["googleAuth.signIn"].Should().HaveCount(1);
        result.Should().Be("test-token");
    }

    [Fact]
    public async Task SignOutAsync_CallsJsSignOut()
    {
        await _sut.SignOutAsync();

        _ctx.JSInterop.Invocations["googleAuth.signOut"].Should().HaveCount(1);
    }

    [Fact]
    public async Task IsSignedInAsync_CallsEnsureInitializedThenJsIsSignedIn()
    {
        _ctx.JSInterop.Setup<bool>("googleAuth.isSignedIn").SetResult(true);

        var result = await _sut.IsSignedInAsync();

        _ctx.JSInterop.Invocations["googleAuth.initialize"].Should().HaveCount(1);
        _ctx.JSInterop.Invocations["googleAuth.isSignedIn"].Should().HaveCount(1);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsTokenFromJs()
    {
        _ctx.JSInterop.Setup<string?>("googleAuth.getAccessToken").SetResult("access-token-123");

        var result = await _sut.GetAccessTokenAsync();

        result.Should().Be("access-token-123");
    }

    [Fact]
    public async Task TrySilentSignInAsync_ReturnsTrue_WhenTokenObtained()
    {
        _ctx.JSInterop.Setup<string?>("googleAuth.trySilentSignIn").SetResult("silent-token");

        var result = await _sut.TrySilentSignInAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TrySilentSignInAsync_ReturnsFalse_WhenNoToken()
    {
        _ctx.JSInterop.Setup<string?>("googleAuth.trySilentSignIn").SetResult((string?)null);

        var result = await _sut.TrySilentSignInAsync();

        result.Should().BeFalse();
    }
}
