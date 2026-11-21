using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NintendoDiscordStatus.Core.Constants;

namespace NintendoDiscordStatus.UI.Helpers;

public sealed class ModernToggleSwitch : Border
{
    private readonly Border Thumb;
    private string AccentHex;
    private bool IsCheckedValue;

    public ModernToggleSwitch(bool InitialValue, string AccentHex = ThemeConstants.DefaultAccentHex)
    {
        this.AccentHex = AccentHex;
        IsCheckedValue = InitialValue;

        Width = 44;
        Height = 24;
        CornerRadius = new CornerRadius(12);
        BorderThickness = new Thickness(1);
        Cursor = new Cursor(StandardCursorType.Hand);
        VerticalAlignment = VerticalAlignment.Center;

        Thumb = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(3, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        Child = Thumb;

        PointerPressed += (Sender, Args) =>
        {
            IsChecked = !IsChecked;
            Args.Handled = true;
        };

        UpdateVisualState();
    }

    public event EventHandler<bool>? CheckedChanged;

    public bool IsChecked
    {
        get => IsCheckedValue;
        set
        {
            if (IsCheckedValue == value)
            {
                return;
            }

            IsCheckedValue = value;
            UpdateVisualState();
            CheckedChanged?.Invoke(this, IsCheckedValue);
        }
    }

    public void UpdateAccent(string NewAccentHex)
    {
        AccentHex = NewAccentHex;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (IsCheckedValue)
        {
            Background = UiStyleHelper.ParseBrush(AccentHex);
            BorderBrush = UiStyleHelper.ParseBrush(AccentHex);
            Thumb.Background = Brushes.White;
            Thumb.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            Background = UiStyleHelper.ParseBrush("#2E3038");
            BorderBrush = UiStyleHelper.ParseBrush("#484B57");
            Thumb.Background = UiStyleHelper.ParseBrush("#D4D6DC");
            Thumb.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }
}
