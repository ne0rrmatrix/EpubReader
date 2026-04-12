namespace EpubReader.Interfaces;

public interface IFullScreenService
{
	bool IsFullScreen { get; set; }

	void SetFullScreen(bool enable);
	void EnterFullScreen();
	void ExitFullScreen();
}