using Foundation;
using Microsoft.Maui.Controls.Platform;
using UIKit;

namespace EpubReader.Handlers;

/// <summary>
/// Helper for creating native iOS UIMenu elements from MAUI <see cref="ToolbarItem"/> objects.
/// Used to backport the secondary toolbar overflow menu fix from
/// <see href="https://github.com/dotnet/maui/pull/30480">dotnet/maui#30480</see>.
/// </summary>
static class SecondaryToolbarHelper
{
	/// <summary>
	/// Creates a <see cref="UIAction"/> for a secondary toolbar item.
	/// </summary>
	public static UIAction CreateUIAction(ToolbarItem item)
	{
		ArgumentNullException.ThrowIfNull(item);

		var weakItem = new WeakReference<ToolbarItem>(item);

		var action = UIAction.Create(
			item.Text ?? string.Empty,
			image: null,
			identifier: null,
			handler: _ =>
			{
				if (weakItem.TryGetTarget(out var targetItem))
				{
					((IMenuItemController)targetItem).Activate();
				}
			});

		// Load icon asynchronously if present
		if (item.IconImageSource is not null && !item.IconImageSource.IsEmpty)
		{
			var mauiContext = Application.Current?.Handler?.MauiContext;
			if (mauiContext is not null)
			{
				item.IconImageSource.LoadImage(mauiContext, result =>
				{
					action.Image = result?.Value;
				});
			}
		}

		return action;
	}

	/// <summary>
	/// Creates the overflow menu button (<see cref="UIBarButtonItem"/>) that hosts secondary toolbar items
	/// inside a <see cref="UIMenu"/>, using the ellipsis.circle SF Symbol per iOS HIG.
	/// </summary>
	public static UIBarButtonItem CreateOverflowMenuButton(IReadOnlyList<UIMenuElement> menuElements)
	{
		ArgumentNullException.ThrowIfNull(menuElements);

		var icon = UIImage.GetSystemImage("ellipsis.circle")
			?? UIImage.GetSystemImage("ellipsis"); // fallback

		var menu = UIMenu.Create(
			title: string.Empty,
			image: null,
			identifier: UIMenuIdentifier.Edit,
			options: UIMenuOptions.DisplayInline,
			children: [.. menuElements]);

		return new UIBarButtonItem(icon, menu)
		{
			AccessibilityIdentifier = "SecondaryToolbarMenuButton"
		};
	}
}
