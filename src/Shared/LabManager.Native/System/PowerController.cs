using System.Diagnostics;
using LabManager.Core.Enums;

namespace LabManager.Native.System;

public static class PowerController
{
    public static bool ExecuteCommand(PowerAction action, bool force = true, int delaySeconds = 0)
    {
        try
        {
            var (fileName, args) = GetCommandArguments(action, force, delaySeconds);
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static (string FileName, string Arguments) GetCommandArguments(PowerAction action, bool force, int delaySeconds)
    {
        string fFlag = force ? "/f " : "";
        int delay = Math.Max(0, delaySeconds);

        return action switch
        {
            PowerAction.Shutdown => ("shutdown.exe", $"/s {fFlag}/t {delay}"),
            PowerAction.Restart => ("shutdown.exe", $"/r {fFlag}/t {delay}"),
            PowerAction.Logoff => ("shutdown.exe", $"/l {fFlag}"),
            PowerAction.Sleep => ("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"),
            _ => ("shutdown.exe", $"/s {fFlag}/t {delay}")
        };
    }
}
