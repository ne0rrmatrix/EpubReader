namespace EpubReader.Service;

class FullScreenService : IFullScreenService
{
	public bool IsFullScreen { get; set; }

	public void SetFullScreen(bool enable)
	{
		// No implementation needed for Windows.
	}
	public void EnterFullScreen()
	{
		// No implementation needed for Windows.
	}
	public void ExitFullScreen()
	{
		// No implementation needed for Windows.
	}
}