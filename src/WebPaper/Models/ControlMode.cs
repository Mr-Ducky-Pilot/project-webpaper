namespace WebPaper.Models
{
    /// <summary>
    /// Control mode for input handling
    /// </summary>
    public enum ControlMode
    {
        /// <summary>
        /// WebPaper Control - Mouse and keyboard go to wallpaper (webpage), right-click shows webpage context menu
        /// Desktop icons can still be selected with single click
        /// </summary>
        WebPaperControl = 0,

        /// <summary>
        /// Desktop Control - Wallpaper is static, all input goes to Windows/desktop
        /// No interaction with webpage
        /// </summary>
        DesktopControl = 1
    }
}
