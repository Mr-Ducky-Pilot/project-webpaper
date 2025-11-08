using System;

namespace WebPaper.Models
{
    /// <summary>
    /// Represents a mouse input event
    /// </summary>
    public class MouseInputEvent
    {
        public int X { get; set; }
        public int Y { get; set; }
        public MouseEventType EventType { get; set; }
        public uint MouseData { get; set; }
        public DateTime Timestamp { get; set; }

        public MouseInputEvent()
        {
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"{EventType} at ({X}, {Y})";
        }
    }

    /// <summary>
    /// Types of mouse events
    /// </summary>
    public enum MouseEventType
    {
        LeftButtonDown,
        LeftButtonUp,
        RightButtonDown,
        RightButtonUp,
        MiddleButtonDown,
        MiddleButtonUp,
        MouseMove,
        MouseWheel,
        MouseHWheel
    }

    /// <summary>
    /// Represents a keyboard input event
    /// </summary>
    public class KeyboardInputEvent
    {
        public int VirtualKeyCode { get; set; }
        public uint ScanCode { get; set; }
        public KeyEventType EventType { get; set; }
        public bool IsExtendedKey { get; set; }
        public bool IsAltPressed { get; set; }
        public DateTime Timestamp { get; set; }

        public KeyboardInputEvent()
        {
            Timestamp = DateTime.Now;
        }

        public override string ToString()
        {
            return $"{EventType} - VK: {VirtualKeyCode} (0x{VirtualKeyCode:X2})";
        }
    }

    /// <summary>
    /// Types of keyboard events
    /// </summary>
    public enum KeyEventType
    {
        KeyDown,
        KeyUp,
        SystemKeyDown,
        SystemKeyUp
    }
}
