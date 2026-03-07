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

public class LoginPageTests : TestContext
{
    public LoginPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddScoped(_ => new AuthStateService());
        Services.AddScoped<IAuthStorage>(_ => Mock.Of<IAuthStorage>());
        Services.AddScoped<IHttpClientFactory>(_ => new StubHttpClientFactory(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new LoginResponse(
                    Guid.NewGuid(),
                    "ion@example.com",
                    "Ion Popescu",
                    "token-1",
                    DateTime.UtcNow.AddHours(1)))
            }));
        Services.AddScoped<AuthService>();
    }

    [Fact]
    public void Given_LoginPage_When_Rendered_Then_ShouldShowLoginHeading()
    {
        var cut = RenderComponent<Login>();

        Assert.Contains("Autentificare", cut.Markup);
    }

    [Fact]
    public void Given_EmptyEmail_When_ValidateEmail_Then_ShouldReturnRequiredMessage()
    {
        var cut = RenderComponent<Login>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidateEmail", "");

        Assert.Equal("Email-ul este obligatoriu", result);
    }

    [Fact]
    public void Given_InvalidEmail_When_ValidateEmail_Then_ShouldReturnInvalidMessage()
    {
        var cut = RenderComponent<Login>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidateEmail", "invalid-email");

        Assert.Equal("Email invalid", result);
    }

    [Fact]
    public void Given_ValidEmail_When_ValidateEmail_Then_ShouldReturnNull()
    {
        var cut = RenderComponent<Login>();

        var result = InvokePrivate<string?>(cut.Instance, "ValidateEmail", "ion@example.com");

        Assert.Null(result);
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        return (T)method.Invoke(instance, args)!;
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
