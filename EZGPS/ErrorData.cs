using System;
using ProtoBuf;


namespace EZGPS
{

    [ProtoContract]
    public class ErrorData
    {
        [ProtoMember(1)]
        public long playerID;

        [ProtoMember(2)]
        public String message;

        public ErrorData(long playerID, String message)
        {
            this.playerID = playerID;
            this.message = message;
        }

        public ErrorData() { }

    }
}
