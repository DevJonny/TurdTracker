using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TurdTracker.Tests.Fakes;

#pragma warning disable CS0067 // Event never used

public class FakeDialogService : IDialogService
{
    public bool? MessageBoxResult { get; set; } = true;
    public List<string> MethodCalls { get; } = [];

    public event Func<IDialogReference, Task>? DialogInstanceAddedAsync;
    public event Action<IDialogReference, DialogResult?>? OnDialogCloseRequested;

    public Task<bool?> ShowMessageBoxAsync(
        string title,
        string message,
        string yesText = "OK",
        string? noText = null,
        string? cancelText = null,
        DialogOptions? options = null)
    {
        MethodCalls.Add(nameof(ShowMessageBoxAsync));
        return Task.FromResult(MessageBoxResult);
    }

    public Task<bool?> ShowMessageBoxAsync(
        string title,
        MarkupString markupMessage,
        string yesText = "OK",
        string? noText = null,
        string? cancelText = null,
        DialogOptions? options = null)
    {
        MethodCalls.Add(nameof(ShowMessageBoxAsync));
        return Task.FromResult(MessageBoxResult);
    }

    public Task<bool?> ShowMessageBoxAsync(MessageBoxOptions messageBoxOptions, DialogOptions? options = null)
    {
        MethodCalls.Add(nameof(ShowMessageBoxAsync));
        return Task.FromResult(MessageBoxResult);
    }

    public Task<IDialogReference> ShowAsync<TComponent>() where TComponent : IComponent
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync<TComponent>(string? title) where TComponent : IComponent
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync<TComponent>(string? title, DialogOptions options) where TComponent : IComponent
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync<TComponent>(string? title, DialogParameters parameters) where TComponent : IComponent
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync<TComponent>(string? title, DialogParameters parameters, DialogOptions? options) where TComponent : IComponent
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync(Type component)
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync(Type component, string? title)
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync(Type component, string? title, DialogOptions options)
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync(Type component, string? title, DialogParameters parameters)
        => throw new NotImplementedException();
    public Task<IDialogReference> ShowAsync(Type component, string? title, DialogParameters parameters, DialogOptions options)
        => throw new NotImplementedException();
    public IDialogReference CreateReference()
        => throw new NotImplementedException();
    public void Close(IDialogReference dialog) { }
    public void Close(IDialogReference dialog, DialogResult? result) { }
}
