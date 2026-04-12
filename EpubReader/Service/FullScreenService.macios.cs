namespace EpubReader.Service;

class FullScreenService : IFullScreenService
{
	public bool IsFullScreen { get; set; }

	public void SetFullScreen(bool enable)
	{
		// No implementation needed for macOS, as full-screen mode is managed by the system and can be toggled by the user.
	}
	public void EnterFullScreen()
	{
		// No implementation needed for macOS, as full-screen mode is managed by the system and can be toggled by the user.
	}
	public void ExitFullScreen()
	{
		// No implementation needed for macOS, as full-screen mode is managed by the system and can be toggled by the user.
	}
}
