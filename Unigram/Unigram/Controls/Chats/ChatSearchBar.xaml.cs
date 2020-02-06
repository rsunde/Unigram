﻿using LinqToVisualTree;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Dialogs;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using static Unigram.Controls.Chats.ChatTextBox;

namespace Unigram.Controls.Chats
{
    public sealed partial class ChatSearchBar : UserControl
    {
        public ChatSearchViewModel ViewModel => DataContext as ChatSearchViewModel;

        public ChatSearchBar()
        {
            InitializeComponent();

            if (ApiInformation.IsEventPresent("Windows.UI.Xaml.UIElement", "PreviewKeyDown"))
            {
                Field.PreviewKeyDown += OnKeyDown;
            }
            else
            {
                Field.KeyDown += OnKeyDown;
            }

            RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        }

        public void Update(ChatSearchViewModel viewModel)
        {
            DataContext = viewModel;
            Bindings.Update();

            Field.Text = string.Empty;
            Field.From = null;
            Field.Filter = null;
            Field.State = ChatSearchState.Text;
        }

        private async void OnVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Visibility == Visibility.Visible)
            {
                await Task.Delay(100);
                Field.Focus(FocusState.Keyboard);
            }
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            if (args.Item is User user)
            {
                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.GetFullName();
                username.Text = string.IsNullOrEmpty(user.Username) ? string.Empty : $" @{user.Username}";

                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }
            else if (args.Item is ChatSearchMediaFilter filter)
            {
                var child = content.Children[0] as Border;
                var glyph = child.Child as TextBlock;
                var title = content.Children[1] as TextBlock;

                glyph.Text = filter.Glyph;
                title.Text = filter.Text;

                if (filter.Filter is SearchMessagesFilterVideoNote)
                {
                    glyph.FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily;
                }
                else
                {
                    glyph.FontFamily = App.Current.Resources["SymbolThemeFontFamily"] as FontFamily;
                }
            }
        }

        #endregion

        #region Binding

        private string ConvertOf(int index, int count)
        {
            if (count == 0)
            {
                return Strings.Resources.NoResult;
            }

            return string.Format(Strings.Resources.Of, index + 1, count);
        }

        #endregion

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.Equals(Field.Text, Strings.Resources.SearchFrom) && ViewModel.IsFromEnabled && Field.State == ChatSearchState.Text)
            {
                SetState(ChatSearchState.Members);
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (Field.State == ChatSearchState.Members)
            {
                ViewModel.Autocomplete = new UsernameCollection(ViewModel.ProtoService, ViewModel.Dialog.Chat.Id, Field.Text, false, true);
            }

            DeleteButton.Visibility = string.IsNullOrEmpty(Field.Text) && Field.State == ChatSearchState.Text ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var shift = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            if (e.Key == Windows.System.VirtualKey.Enter && !shift && Field.State != ChatSearchState.Members)
            {
                ViewModel.Search(Field.Text, Field.From, Field.Filter?.Filter);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter && shift && Field.State != ChatSearchState.Members)
            {
                ViewModel.NextCommand.Execute();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(Field.Text))
            {
                Delete(false);
                e.Handled = true;
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Search(Field.Text, Field.From, Field.Filter?.Filter);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            Delete(true);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequested();
        }

        private void FilterByMember_Click(object sender, RoutedEventArgs e)
        {
            SetState(ChatSearchState.Members);
            Field.Focus(FocusState.Keyboard);
        }

        private void FilterByMedia_Click(object sender, RoutedEventArgs e)
        {
            SetState(ChatSearchState.Media);
            Field.Focus(FocusState.Keyboard);
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is User from)
            {
                SetState(ChatSearchState.TextByMember, from);
            }
            else if (e.ClickedItem is ChatSearchMediaFilter filter)
            {
                SetState(ChatSearchState.TextByMedia, null, filter);
            }
        }

        private void SetState(ChatSearchState state, User from = null, ChatSearchMediaFilter filter = null)
        {
            if (from != null)
            {
                Field.Filter = null;
                Field.From = from;
            }
            else
            {
                Field.From = null;
                Field.Filter = filter;
            }

            Field.Text = string.Empty;
            Field.State = state;

            switch (state)
            {
                case ChatSearchState.Members:
                    ToolsPanel.Visibility = ToolsMiniPanel.Visibility = Visibility.Collapsed;
                    ViewModel.Autocomplete = new UsernameCollection(ViewModel.ProtoService, ViewModel.Dialog.Chat.Id, string.Empty, false, true);
                    break;
                case ChatSearchState.Media:
                    ToolsPanel.Visibility = ToolsMiniPanel.Visibility = Visibility.Collapsed;
                    ViewModel.Autocomplete = ViewModel.Filters;
                    break;
                case ChatSearchState.TextByMember:
                case ChatSearchState.TextByMedia:
                    ToolsPanel.Visibility = ToolsMiniPanel.Visibility = Visibility.Collapsed;
                    ViewModel.Autocomplete = null;
                    break;
                default:
                    ToolsPanel.Visibility = ToolsMiniPanel.Visibility = Visibility.Visible;
                    ViewModel.Autocomplete = null;
                    break;
            }

            DeleteButton.Visibility = string.IsNullOrEmpty(Field.Text) && state == ChatSearchState.Text ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Delete(bool allowDispose)
        {
            if (!string.IsNullOrEmpty(Field.Text))
            {
                Field.Text = string.Empty;
            }
            else if (Field.State == ChatSearchState.TextByMember)
            {
                SetState(ChatSearchState.Members);
            }
            else if (Field.State == ChatSearchState.TextByMedia)
            {
                SetState(ChatSearchState.Media);
            }
            else if (Field.State == ChatSearchState.Members || Field.State == ChatSearchState.Media)
            {
                SetState(ChatSearchState.Text);
            }
            else if (ViewModel?.Dialog?.Search != null && allowDispose)
            {
                ViewModel.Dialog.DisposeSearch();
            }
        }

        public bool OnBackRequested()
        {
            SetState(ChatSearchState.Text);

            if (ViewModel?.Dialog?.Search != null)
            {
                ViewModel.Dialog.DisposeSearch();
            }

            return true;
        }

        private void Field_GotFocus(object sender, RoutedEventArgs e)
        {
            DeleteButton.RequestedTheme = QueryButton.RequestedTheme = ElementTheme.Light;
        }

        private void Field_LostFocus(object sender, RoutedEventArgs e)
        {
            DeleteButton.RequestedTheme = QueryButton.RequestedTheme = ElementTheme.Default;
        }
    }
}
