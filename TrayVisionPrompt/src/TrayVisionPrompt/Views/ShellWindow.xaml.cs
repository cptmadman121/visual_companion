using System;
using System.ComponentModel;
using System.Windows;
using TrayVisionPrompt.ViewModels;

namespace TrayVisionPrompt.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.Initialize();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
