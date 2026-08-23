using Microsoft.Maui.Controls.Platform.Compatibility;
using UIKit;

namespace EpubReader.Handlers;

/// <summary>
/// Custom <see cref="ShellPageRendererTracker"/> that applies the secondary toolbar overflow menu fix
/// from <see href="https://github.com/dotnet/maui/pull/30480">dotnet/maui#30480</see>.
/// </summary>
/// <remarks>
/// On iOS/macOS, <see cref="ToolbarItem"/> objects with <see cref="ToolbarItem.Order"/>
/// set to <see cref="ToolbarItemOrder.Secondary"/> should appear in a "•••" overflow menu.
/// The base MAUI tracker (prior to the .NET 10 Preview 7 fix) silently drops them.
/// This subclass post-processes the toolbar to add the overflow menu button.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="CustomShellPageRendererTracker"/> class.
/// </remarks>
/// <param name="context">The shell context that owns this tracker.</param>
public class CustomShellPageRendererTracker(IShellContext context) : ShellPageRendererTracker(context)
{

	/// <inheritdoc />
	protected override void UpdateToolbarItems()
	{
		// Let the base implementation handle primary toolbar items and left toolbar items.
		base.UpdateToolbarItems();

		// Post-process: add secondary toolbar items inside a UIMenu overflow button.
		PostProcessSecondaryToolbarItems();
	}

	void PostProcessSecondaryToolbarItems()
	{
		if (Page is null)
		{
			return;
		}

		var navItem = ViewController?.NavigationItem;
		if (navItem is null)
		{
			return;
		}

		// Collect secondary items from the page toolbar items, then from Shell toolbar items
		// (the base renderer uses an if-else, but we merge both so programmatically-added
		// Shell items coexist with page-level items).
		var secondaries = CollectSecondaryItems(Page.ToolbarItems);

		// Always also scan Shell toolbar items so items added via Shell.Current.ToolbarItems.Add()
		// are captured even when the page defines its own primary toolbar items.
		secondaries.AddRange(CollectSecondaryItems(Shell.Current?.ToolbarItems));

		if (secondaries.Count == 0)
		{
			return;
		}

		ApplyOverflowMenu(navItem, secondaries);
	}

	static List<UIMenuElement> CollectSecondaryItems(IList<ToolbarItem>? items)
	{
		var secondaries = new List<UIMenuElement>();

		if (items is null || items.Count == 0)
		{
			return secondaries;
		}

		foreach (var item in items.OrderBy(x => x.Priority))
		{
			if (item.Order == ToolbarItemOrder.Secondary)
			{
				secondaries.Add(SecondaryToolbarHelper.CreateUIAction(item));
			}
		}

		return secondaries;
	}

	static void ApplyOverflowMenu(UINavigationItem navItem, List<UIMenuElement> secondaries)
	{
		var menuButton = SecondaryToolbarHelper.CreateOverflowMenuButton(secondaries);

		// Prepend the overflow menu button to the existing right bar button items.
		var existingItems = navItem.RightBarButtonItems ?? [];
		var newItems = new UIBarButtonItem[existingItems.Length + 1];
		newItems[0] = menuButton;
		if (existingItems.Length > 0)
		{
			Array.Copy(existingItems, 0, newItems, 1, existingItems.Length);
		}

		navItem.SetRightBarButtonItems(newItems, animated: false);
	}
}
