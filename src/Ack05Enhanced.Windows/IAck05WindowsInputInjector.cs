using Ack05Enhanced.Core;

namespace Ack05Enhanced.Windows;

public interface IAck05WindowsInputInjector
{
    void KeyDown(Ack05KeyboardKey key);

    void KeyUp(Ack05KeyboardKey key);

    void MouseWheel(int steps);
}
