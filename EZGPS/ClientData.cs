using System;
using ProtoBuf;


namespace EZGPS
{

    [ProtoContract]
    public class ClientData
    {
        [ProtoMember(1)]
        public long playerID;

        [ProtoMember(2)]
        public String text;

        public ClientData(long playerID, String text)
        {
            this.playerID = playerID;
            this.text = text;
        }

        public ClientData() { }

    }
}
