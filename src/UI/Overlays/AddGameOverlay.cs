using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.UI.Helpers;

namespace NintendoDiscordStatus.UI.Overlays;

public static class AddGameOverlay
{
    public static Control Build(
        Window OwnerWindow,
        IReadOnlyList<string> ExistingConsoles,
        string DefaultConsole,
        string AccentHex,
        Func<string, string, string, bool> OnAddGame,
        Action<string, string> OnGameAddedSelect,
        Action OnClose)
    {
        AutoCompleteBox ConsoleBox = new AutoCompleteBox
        {
            ItemsSource = ExistingConsoles,
            Text = string.IsNullOrWhiteSpace(DefaultConsole) ? AppConstants.DefaultConsoleName : DefaultConsole,
            Watermark = string.Empty,
            MinimumPrefixLength = 0,
            FilterMode = AutoCompleteFilterMode.Custom,
            TextFilter = (SearchText, ItemText) =>
                string.IsNullOrWhiteSpace(SearchText)
                || (ItemText?.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? false),
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            CornerRadius = new CornerRadius(6)
        };

        TextBox GameNameBox = UiStyleHelper.CreateStyledTextBox(string.Empty);
        TextBox ImageBox = UiStyleHelper.CreateStyledTextBox(string.Empty, "", 512);

        TextBlock ErrorText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.WarningOrangeHex),
            IsVisible = false
        };

        Button BrowseButton = UiStyleHelper.CreateSecondaryButton("Browse...");
        BrowseButton.Margin = new Thickness(8, 0, 0, 0);
        BrowseButton.Click += async (Sender, Args) =>
        {
            TopLevel? HostTopLevel = TopLevel.GetTopLevel(OwnerWindow);
            if (HostTopLevel == null)
            {
                return;
            }

            IReadOnlyList<IStorageFile> Files = await HostTopLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Select Game Cover Image",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
                });

            if (Files.Count > 0)
            {
                ImageBox.Text = Files[0].Path.LocalPath;
            }
        };

        Grid ImageRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        ImageRow.Children.Add(ImageBox);
        Grid.SetColumn(ImageBox, 0);
        ImageRow.Children.Add(BrowseButton);
        Grid.SetColumn(BrowseButton, 1);

        Button CancelButton = UiStyleHelper.CreateSecondaryButton("Cancel");
        CancelButton.Margin = new Thickness(0, 0, 8, 0);
        CancelButton.Click += (Sender, Args) => OnClose();

        Button AddButton = UiStyleHelper.CreatePrimaryButton("Add Game", AccentHex);
        AddButton.Click += (Sender, Args) =>
        {
            string ConsoleName = ConsoleBox.Text?.Trim() ?? string.Empty;
            string GameName = GameNameBox.Text?.Trim() ?? string.Empty;
            string ImageKeyOrPath = ImageBox.Text?.Trim() ?? string.Empty;

            if (!OnAddGame(ConsoleName, GameName, ImageKeyOrPath))
            {
                ErrorText.Text = "Both Console and Game Title are required.";
                ErrorText.IsVisible = true;
                return;
            }

            OnGameAddedSelect(ConsoleName, GameName);
            OnClose();
        };

        StackPanel FooterRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };
        FooterRow.Children.Add(CancelButton);
        FooterRow.Children.Add(AddButton);

        StackPanel CardContent = new StackPanel { Spacing = 10 };
        CardContent.Children.Add(UiStyleHelper.CreateLabel("ADD CUSTOM GAME", 14, true, ThemeConstants.TextWhiteHex));
        CardContent.Children.Add(UiStyleHelper.CreateLabel("CONSOLE"));
        CardContent.Children.Add(ConsoleBox);
        CardContent.Children.Add(UiStyleHelper.CreateLabel("GAME TITLE"));
        CardContent.Children.Add(GameNameBox);
        CardContent.Children.Add(UiStyleHelper.CreateLabel("COVER IMAGE (URL FOR DISCORD, LOCAL FILE FOR PREVIEW)"));
        CardContent.Children.Add(ImageRow);
        CardContent.Children.Add(ErrorText);
        CardContent.Children.Add(FooterRow);

        return UiStyleHelper.WrapModalCard(CardContent, 420);
    }
}
