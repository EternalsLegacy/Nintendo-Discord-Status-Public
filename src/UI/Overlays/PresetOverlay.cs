using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.UI.Helpers;

namespace NintendoDiscordStatus.UI.Overlays;

public static class PresetOverlay
{
    public static Control Build(
        ObservableCollection<PresetModel> Presets,
        string SuggestedName,
        string AccentHex,
        Action<PresetModel> OnLoadPreset,
        Func<string, bool> OnSaveCurrentPreset,
        Func<string, bool> OnDeletePreset,
        Action OnClose)
    {
        TextBox PresetNameBox = UiStyleHelper.CreateStyledTextBox("Custom preset name...", SuggestedName, 60);
        TextBlock CountLabel = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeight.Medium,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock FeedbackText = new TextBlock
        {
            FontSize = 11,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.WarningOrangeHex),
            IsVisible = false
        };

        StackPanel ListPanel = new StackPanel { Spacing = 8 };

        void RebuildList()
        {
            ListPanel.Children.Clear();
            CountLabel.Text = Presets.Count == 1 ? "1 saved" : $"{Presets.Count} saved";

            if (Presets.Count == 0)
            {
                ListPanel.Children.Add(new Border
                {
                    Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
                    BorderBrush = UiStyleHelper.ParseBrush("#2E2F36"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 16),
                    Child = new TextBlock
                    {
                        Text = "No saved presets yet. Enter a name above and save your current setup!",
                        FontSize = 12,
                        Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                });
                return;
            }

            foreach (PresetModel Item in Presets)
            {
                ListPanel.Children.Add(BuildPresetCard(Item, OnLoadPreset, OnDeletePreset, OnClose, RebuildList));
            }
        }

        RebuildList();

        Button SaveCurrentButton = UiStyleHelper.CreatePrimaryButton("Save Current", AccentHex);
        SaveCurrentButton.Margin = new Thickness(8, 0, 0, 0);
        SaveCurrentButton.Click += (Sender, Args) =>
        {
            string EnteredName = PresetNameBox.Text?.Trim() ?? string.Empty;
            if (!OnSaveCurrentPreset(EnteredName))
            {
                FeedbackText.Text = "Please enter a preset name first.";
                FeedbackText.IsVisible = true;
                return;
            }

            FeedbackText.IsVisible = false;
            PresetNameBox.Text = string.Empty;
            RebuildList();
        };

        Grid SaveRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        SaveRow.Children.Add(PresetNameBox);
        Grid.SetColumn(PresetNameBox, 0);
        SaveRow.Children.Add(SaveCurrentButton);
        Grid.SetColumn(SaveCurrentButton, 1);

        ScrollViewer Scroller = new ScrollViewer
        {
            MaxHeight = 270,
            Content = ListPanel
        };

        Button CloseButton = UiStyleHelper.CreateSecondaryButton("Close");
        CloseButton.HorizontalAlignment = HorizontalAlignment.Right;
        CloseButton.Click += (Sender, Args) => OnClose();

        Grid HeaderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        TextBlock TitleLabel = UiStyleHelper.CreateLabel("PRESETS", 14, true, ThemeConstants.TextWhiteHex);
        TitleLabel.VerticalAlignment = VerticalAlignment.Center;
        HeaderGrid.Children.Add(TitleLabel);
        Grid.SetColumn(TitleLabel, 0);
        HeaderGrid.Children.Add(CountLabel);
        Grid.SetColumn(CountLabel, 1);

        StackPanel CardContent = new StackPanel { Spacing = 12 };
        CardContent.Children.Add(HeaderGrid);
        CardContent.Children.Add(SaveRow);
        CardContent.Children.Add(FeedbackText);
        CardContent.Children.Add(Scroller);
        CardContent.Children.Add(CloseButton);

        return UiStyleHelper.WrapModalCard(CardContent, 424);
    }

    private static Border BuildPresetCard(
        PresetModel Item,
        Action<PresetModel> OnLoadPreset,
        Func<string, bool> OnDeletePreset,
        Action OnClose,
        Action RebuildList)
    {
        string ConsoleAccentHex = ThemeHelper.GetAccentHexForConsole(Item.ConsoleName, true);
        string BadgeCode = ThemeHelper.GetConsoleBadge(Item.ConsoleName);

        Border ConsoleBadge = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = UiStyleHelper.ParseBrush(ConsoleAccentHex),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = BadgeCode,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        StackPanel TextStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        TextStack.Children.Add(new TextBlock
        {
            Text = Item.PresetName,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            TextWrapping = TextWrapping.Wrap
        });

        string SubtitleText = BuildSubtitleText(Item);
        if (!string.IsNullOrEmpty(SubtitleText))
        {
            TextStack.Children.Add(new TextBlock
            {
                Text = SubtitleText,
                FontSize = 11.5,
                Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                TextWrapping = TextWrapping.Wrap
            });
        }

        Button LoadButton = new Button
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 2),
            CornerRadius = new CornerRadius(6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = TextStack
        };
        LoadButton.Click += (Sender, Args) =>
        {
            OnLoadPreset(Item);
            OnClose();
        };

        Button DeleteButton = new Button
        {
            Content = UiStyleHelper.CreateVectorIcon(UiStyleHelper.TrashIconData, ThemeConstants.TextGrayHex, 16),
            Background = Brushes.Transparent,
            Width = 32,
            Height = 32,
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(DeleteButton, "Delete preset");
        DeleteButton.Click += (Sender, Args) =>
        {
            OnDeletePreset(Item.PresetName);
            RebuildList();
        };

        Grid RowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto")
        };
        RowGrid.Children.Add(ConsoleBadge);
        Grid.SetColumn(ConsoleBadge, 0);
        RowGrid.Children.Add(LoadButton);
        Grid.SetColumn(LoadButton, 1);
        RowGrid.Children.Add(DeleteButton);
        Grid.SetColumn(DeleteButton, 2);

        return new Border
        {
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            BorderBrush = UiStyleHelper.ParseBrush("#30313A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8),
            Child = RowGrid
        };
    }

    private static string BuildSubtitleText(PresetModel Item)
    {
        List<string> MetadataParts = new List<string>();

        string DefaultGeneratedName = $"{Item.ConsoleName} - {Item.GameName}".Trim(' ', '-');
        bool IsCustomName = !string.Equals(Item.PresetName, DefaultGeneratedName, StringComparison.OrdinalIgnoreCase);

        if (IsCustomName)
        {
            if (!string.IsNullOrWhiteSpace(Item.ConsoleName))
            {
                MetadataParts.Add(Item.ConsoleName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(Item.GameName))
            {
                MetadataParts.Add(Item.GameName.Trim());
            }
        }

        if (!string.IsNullOrWhiteSpace(Item.Details))
        {
            MetadataParts.Add(Item.Details.Trim());
        }

        if (!string.IsNullOrWhiteSpace(Item.State))
        {
            MetadataParts.Add(Item.State.Trim());
        }

        return MetadataParts.Count > 0
            ? string.Join(" • ", MetadataParts)
            : string.Empty;
    }
}
