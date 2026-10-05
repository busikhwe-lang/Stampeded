using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Stampeded.Panes;

public partial class CommitsPaneView : UserControl
{
	public CommitsPaneView()
	{
		InitializeComponent();
	}

	void OnCommitSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is CommitsPaneViewModel vm && CommitList.SelectedItem is CommitRow row)
			vm.SelectCommit(row);
	}

	void OnFileOpened(object? sender, TappedEventArgs e)
	{
		if (DataContext is CommitsPaneViewModel vm && FilesList.SelectedItem is CommitFileRow row)
			vm.OpenFile(row);
	}

	void OnCommitMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		var row = CommitList.SelectedItem as CommitRow;
		bool hasCommit = row is { IsUncommitted: false } && row.Commit.Sha.Length > 0;
		CopyHashItem.IsEnabled = hasCommit;
		CopyShortHashItem.IsEnabled = hasCommit;
		CopySubjectItem.IsEnabled = row is not null;
		CopyMessageItem.IsEnabled = hasCommit && row!.Commit.Message.Length > 0;
		ReviewCommitItem.IsEnabled = hasCommit && DataContext is CommitsPaneViewModel { CanReviewCommits: true };
		OpenCommitOnHostItem.IsEnabled = hasCommit;
	}

	void OnCopyCommitHash(object? sender, RoutedEventArgs e)
	{
		if (CommitList.SelectedItem is CommitRow { IsUncommitted: false } row)
			CopyToClipboard(row.Commit.Sha);
	}

	void OnCopyShortHash(object? sender, RoutedEventArgs e)
	{
		if (CommitList.SelectedItem is CommitRow { IsUncommitted: false } row)
			CopyToClipboard(row.Commit.ShortSha);
	}

	void OnCopySubject(object? sender, RoutedEventArgs e)
	{
		if (CommitList.SelectedItem is CommitRow row)
			CopyToClipboard(row.Commit.Subject);
	}

	void OnCopyMessage(object? sender, RoutedEventArgs e)
	{
		if (CommitList.SelectedItem is CommitRow { IsUncommitted: false } row)
			CopyToClipboard(row.Commit.Message);
	}

	void OnReviewCommit(object? sender, RoutedEventArgs e)
	{
		if (DataContext is CommitsPaneViewModel vm && CommitList.SelectedItem is CommitRow row)
			vm.ReviewCommit(row);
	}

	void OnOpenCommitOnHost(object? sender, RoutedEventArgs e)
	{
		if (DataContext is CommitsPaneViewModel vm && CommitList.SelectedItem is CommitRow row)
			vm.OpenCommitOnHost(row);
	}

	void CopyToClipboard(string text)
	{
		if (text.Length > 0)
			TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(text).HandleExceptions();
	}
}
