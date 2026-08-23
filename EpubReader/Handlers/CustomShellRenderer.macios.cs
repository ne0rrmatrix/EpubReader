using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace EpubReader.Handlers;

/// <summary>
/// Custom <see cref="ShellRenderer"/> that injects the secondary toolbar overflow menu fix
/// from <see href="https://github.com/dotnet/maui/pull/30480">dotnet/maui#30480</see>.
/// </summary>
public class CustomShellRenderer : ShellRenderer
{
	/// <inheritdoc />
	protected override IShellPageRendererTracker CreatePageRendererTracker()
	{
		return new CustomShellPageRendererTracker(this);
	}
}
