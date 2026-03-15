using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Bunit;
using LegalDoc.Frontend.Components.Pages;
using LegalDoc.Frontend.Models;
using LegalDoc.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;

namespace LegalDoc.Frontend.Test.Components.Pages;

public class RegisterPageTests : TestContext
{
    public RegisterPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddScoped(_ => new AuthStateService());
        Services.AddScoped<IAuthStorage>(_ => Mock.Of<IAuthStorage>());
        Services.AddScoped<IHttpClientFactory>(_ => new StubHttpClientFactory(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new RegisterResponse(
                    Guid.NewGuid(),
                    "ion@example.com",
                    "Ion Popescu",
                    DateTime.UtcNow))
            }));
        Services.AddScoped<AuthService>();
    }

    [Fact]
    public void Given_RegisterPage_When_Rendered_Then_ShouldShowRegisterHeading()
    {
        var cut = RenderComponent<Register>();

        Assert.Contains("Create Account", cut.Markup);
    }

    [Fact]
    public void Given_EmptyEmail_When_ValidateEmail_Then_ShouldReturnRequiredMessage()
    {
        var cut = RenderComponent<Register>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidateEmail", "");

        Assert.Equal("Email is required", result);
    }

    [Fact]
    public void Given_InvalidEmail_When_ValidateEmail_Then_ShouldReturnInvalidMessage()
    {
        var cut = RenderComponent<Register>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidateEmail", "invalid-email");

        Assert.Equal("Invalid email address", result);
    }

    [Fact]
    public void Given_ValidEmail_When_ValidateEmail_Then_ShouldReturnNull()
    {
        var cut = RenderComponent<Register>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidateEmail", "ion@example.com");

        Assert.Null(result);
    }

    [Fact]
    public void Given_EmptyPassword_When_ValidatePassword_Then_ShouldReturnRequiredMessage()
    {
        var cut = RenderComponent<Register>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidatePassword", "");

        Assert.Equal("Password is required", result);
    }

    [Fact]
    public void Given_ShortPassword_When_ValidatePassword_Then_ShouldReturnMinLengthMessage()
    {
        var cut = RenderComponent<Register>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidatePassword", "123");

        Assert.Equal("Password must be at least 6 characters", result);
    }

    [Fact]
    public void Given_ValidPassword_When_ValidatePassword_Then_ShouldReturnNull()
    {
        var cut = RenderComponent<Register>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidatePassword", "123456");

        Assert.Null(result);
    }

    [Fact]
    public void Given_EmptyConfirmPassword_When_ValidateConfirmPassword_Then_ShouldReturnRequiredMessage()
    {
        var cut = RenderComponent<Register>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidateConfirmPassword", "");

        Assert.Equal("Please confirm your password", result);
    }

    [Fact]
    public void Given_MismatchedConfirmPassword_When_ValidateConfirmPassword_Then_ShouldReturnMismatchMessage()
    {
        var cut = RenderComponent<Register>();
        SetPrivateField(cut.Instance, "_password", "123456");

        var result = InvokePrivate<string?>(cut.Instance, "ValidateConfirmPassword", "654321");

        Assert.Equal("Passwords do not match", result);
    }

    [Fact]
    public void Given_MatchingConfirmPassword_When_ValidateConfirmPassword_Then_ShouldReturnNull()
    {
        var cut = RenderComponent<Register>();
        SetPrivateField(cut.Instance, "_password", "123456");

        var result = InvokePrivate<string?>(cut.Instance, "ValidateConfirmPassword", "123456");

        Assert.Null(result);
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        return (T)method.Invoke(instance, args)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        field.SetValue(instance, value);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new StubHttpMessageHandler(_handler))
            {
                BaseAddress = new Uri("https://localhost")
            };
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
