using LabManager.Native.Input;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class RemoteControlHandler
{
    public void HandleInput(RemoteInputPacket packet)
    {
        switch (packet.InputType)
        {
            case RemoteInputType.MouseMove:
                InputSimulator.MoveMouse(packet.X, packet.Y);
                break;
            case RemoteInputType.MouseDown:
                InputSimulator.MouseButton(packet.X, packet.Y, packet.Button, isDown: true);
                break;
            case RemoteInputType.MouseUp:
                InputSimulator.MouseButton(packet.X, packet.Y, packet.Button, isDown: false);
                break;
            case RemoteInputType.MouseWheel:
                InputSimulator.MouseWheel(packet.WheelDelta);
                break;
            case RemoteInputType.KeyDown:
                InputSimulator.KeyEvent(packet.VirtualKey, isDown: true);
                break;
            case RemoteInputType.KeyUp:
                InputSimulator.KeyEvent(packet.VirtualKey, isDown: false);
                break;
        }
    }
}
