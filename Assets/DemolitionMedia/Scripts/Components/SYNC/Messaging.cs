namespace DemolitionStudios.DemolitionMedia
{
    using ClockType = System.Double;

    class MessageBase
    {
        public Protocol.PacketId type;
    }

    class SyncMessage : MessageBase
    {
        public SyncMessage()
        {
            type = Protocol.PacketId.Sync;
        }

        public ulong timestamp;
        public ClockType position;
    }

    class SpeedMessage : MessageBase
    {
        public SpeedMessage()
        {
            type = Protocol.PacketId.Speed;
        }

        public ClockType speed;
    }

    class PauseMessage : MessageBase
    {
        public PauseMessage()
        {
            type = Protocol.PacketId.Pause;
        }

        public bool pause;
    }

    class ChangeVideoMessage : MessageBase
    {
        public ChangeVideoMessage()
        {
            type = Protocol.PacketId.ChangeVideo;
        }

        public int playlistIndex;
    }

    class CustomCommandMessage : MessageBase
    {
        public CustomCommandMessage()
        {
            type = Protocol.PacketId.CustomCommand;
        }

        public string command;
    }

    class CustomCommandWithDataMessage : MessageBase
    {
        public CustomCommandWithDataMessage()
        {
            type = Protocol.PacketId.CustomCommandWithData;
        }

        public string command;
        public byte[] data;
    }
}
