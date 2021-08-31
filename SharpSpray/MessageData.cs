using System;

namespace A
{
    public class MessageData
    {
        public enum MessageType
        {
            Red,
            Warning,
            Blue,
            Good,
            General
        }

        public string Text { get; }
        public ConsoleColor ForegroundColor { get; }
        public ConsoleColor BackgroundColor { get; } = ConsoleColor.Black;
        public MessageType Type { get; set; }


        public MessageData(string text, ConsoleColor backgroundColor, ConsoleColor foregroundColor, MessageType t)
        {
            Text = text;
            ForegroundColor = foregroundColor;
            BackgroundColor = backgroundColor;
            Type = t;
        }

        public MessageData(string text, ConsoleColor foregroundColor, MessageType t)
        {
            Text = text;
            ForegroundColor = foregroundColor;
            Type = t;
        }

        public MessageData(string text)
        {
            Text = text;
            Type = MessageType.General;
            ForegroundColor = ConsoleColor.White;
        }
    }
}