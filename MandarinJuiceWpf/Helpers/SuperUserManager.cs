using System.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MandarinJuiceCore.Helpers;

namespace MandarinJuiceWpf.Helpers;

/// <summary>
/// Manages the "Super User" feature, which unlocks specific functionality after a certain number of user interactions within a specified time frame.
/// </summary>
public partial class SuperUserManager : ObservableObject
{
    private readonly ProgressReporter _progressReporter;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.DataBind); // setting a higher priority is important!
    private readonly uint _superUserThreshold;
    private uint _superUserClicks;

    [ObservableProperty] private bool _isSuperUser;

    public SuperUserManager(ProgressReporter progressReporter, long timeSpanMs = 500, uint superUserThreshold = 3)
    {
        _progressReporter = progressReporter;
        _superUserThreshold = superUserThreshold;
        _timer.Interval = TimeSpan.FromMilliseconds(timeSpanMs);
        _timer.Tick += (_, _) => ResetCounter();
    }

    [RelayCommand]
    public void SuperUserTriggerClick()
    {
        if (IsSuperUser) return;

        _superUserClicks++;
        _timer.Stop();

        if (_superUserClicks < _superUserThreshold) _timer.Start();
        else EnableSuperUser();
    }

    private void ResetCounter()
    {
        _superUserClicks = 0;
        _timer.Stop();
    }

    private void EnableSuperUser()
    {
        IsSuperUser = true;
        _progressReporter.Report("You're a SuperUser now! 🎉");
        // play sound
        SystemSounds.Beep.Play();
    }
}