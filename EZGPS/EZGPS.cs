#region pre_script
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;


using System.Text;
using System.Threading.Tasks;
using Sandbox.Common;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;

using Sandbox.ModAPI.Interfaces;

using VRage.Game.Entity;

using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;


#endregion pre_script

namespace EZGPS
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class GPSUtilityCommands : MySessionComponentBase
    {

        public static ushort NetworkId = 19972;
        public static ushort ErrorNetworkId = 19971;

        public bool init = false;
        public static bool doRange = false;
        public static double range = 0.0;



        public override void UpdateAfterSimulation()
        {
            //start up mod
            if (!init)
            {
                if (MyAPIGateway.Session == null)
                    return;
                Init();
                
            }


            //update HUD with gps marks that are in range if enabled
            if (doRange)
            {
                //get player gps points 
                long playerID = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
                List<IMyGps> gps = MyAPIGateway.Session.GPS.GetGpsList(playerID).ToList();

                //for each gps, if it is farther than the range, hide it, else show it
                foreach (VRage.Game.ModAPI.IMyGps g in gps)
                {
                    VRageMath.Vector3D playerPos = MyAPIGateway.Session.Player.GetPosition();

                    double distance = VRageMath.Vector3D.Distance(playerPos, g.Coords);

                    if (distance > range)
                    {
                        MyAPIGateway.Session.GPS.SetShowOnHud(playerID, g.GetHashCode(), false);
                    }
                    else
                    {
                        MyAPIGateway.Session.GPS.SetShowOnHud(playerID, g.GetHashCode(), true);
                    }
                }
            }
        }

        public void Init()
        {


            MyAPIGateway.Utilities.ShowMessage("EZ GPS", "EZ GPS Loaded (/ez help)");
            init = true;

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NetworkId, NetworkHandler);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ErrorNetworkId, ErrorNetworkHandler);
            log(string.Format("initialized+ {0}", DateTime.Now));
            MyAPIGateway.Utilities.MessageEntered += ProcessMessage;


        }


        //Network stuff based on material from Meridius_IX / Lucas

        public void ErrorNetworkHandler(ushort errorNetworkId, byte[] receivedData,ulong playerID, bool isFromServer)
        {
            var data = MyAPIGateway.Utilities.SerializeFromBinary<ErrorData>(receivedData);
            if (MyAPIGateway.Session.LocalHumanPlayer.IdentityId == data.playerID)
            {
                MyAPIGateway.Utilities.ShowMessage("EZ GPS", data.message);
                
            }

        }


        public void NetworkHandler(ushort networkId, byte[] receivedData, ulong playerID, bool isFromServer)
        {
            var data = MyAPIGateway.Utilities.SerializeFromBinary<ClientData>(receivedData);
            log("Message Received: " + data.playerID);
            //process gps sharing
            char[] separators = new char[] { ' ', '_' };
            List<String> words = new List<string>(data.text.Split(separators, StringSplitOptions.RemoveEmptyEntries));
            gpsProcess(data.playerID, words);
        }

        protected override void UnloadData()
        {


            MyAPIGateway.Utilities.MessageEntered -= ProcessMessage;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NetworkId, NetworkHandler);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ErrorNetworkId, ErrorNetworkHandler);

            base.UnloadData();
            init = false;
        }

        public void WriteErrorMessage(long playerID, String message)
        {

            log("Write Error message " + playerID +" "+ message);

            if (MyAPIGateway.Session.LocalHumanPlayer != null &&
               MyAPIGateway.Session.LocalHumanPlayer.IdentityId == playerID)
            {
                log("Error Message sent to self");
                MyAPIGateway.Utilities.ShowMessage("EZ GPS", message);
            }

            else
            {
                var data = new ErrorData(playerID, message);
                var sendData = MyAPIGateway.Utilities.SerializeToBinary<ErrorData>(data);
                var sendRequest = MyAPIGateway.Multiplayer.SendMessageToOthers(ErrorNetworkId, sendData);
                log("Error message sent back");
            }

        }

        public void WriteMessage(long playerID, string message)
        {
            if (MyAPIGateway.Session.LocalHumanPlayer.IdentityId == playerID)
            {
                MyAPIGateway.Utilities.ShowMessage("EZ GPS", message);
                MyAPIGateway.Utilities.ShowNotification(message, 2000, MyFontEnum.Blue);
            }
            else
            {
                var data = new ClientData(playerID, message);
                var sendData = MyAPIGateway.Utilities.SerializeToBinary<ClientData>(data);
                var sendRequest = MyAPIGateway.Multiplayer.SendMessageToOthers(NetworkId, sendData);
            }            
        }

        // gps help
        // gps hide|h <tag> GOOD
        // gps show|s <tag> GOOD
        // gps hideall|ha GOOD
        // gps showall|sa GOOD
        // gps showonly|so <tag> GOOD
        // gps cardinals GOOD
        // gps add|a [name] GOOD
        // gps range [num]/off GOOD
        //
        //
        //faction share gps points
        //share all
        //share [name]
        //fix add [name], allowing for spaces GOOD
        //allow tag to be multiple words, using or search, GOOD
        //gps remove [name]
        //add with colour
        //change colour
        //dist
        //vector line
        //
        void ProcessMessage(string messageText, ref bool echo)
        {
            if (!messageText.StartsWith("/", StringComparison.InvariantCultureIgnoreCase))
            {
                return;

            }//if message does not have slash ignore

            else
            {
                echo = false;
                String text = messageText.Trim().Replace("/", "");
                char[] separators = new char[] { ' ', '_' };
                var words = new List<string>(text.Split(separators, StringSplitOptions.RemoveEmptyEntries));

                if ((words[0] == "ez" || words[0] == "gps2") && words.Count >= 1)
                {
                    //get player
                    long playerID = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;

                    if (words.Count == 1)
                    {
                        WriteErrorMessage(playerID, "Need to contain command, use /ez help to see commands");
                        return;
                    }
                    if (MyAPIGateway.Multiplayer.IsServer)
                    {
                        gpsProcess(playerID, words);
                    }
                   
                    else
                    {
                        //if not a local command, send data to server to do
                        switch (words[1])
                        {
                            case "range":
                            case "r":
                            case "help":
                            case "export":
                            case "e":
                            case "dist":
                                gpsProcess(playerID, words);
                                break;
                            default:
                                var data = new ClientData(playerID, text);
                                var sendData = MyAPIGateway.Utilities.SerializeToBinary<ClientData>(data);
                                var sendRequest = MyAPIGateway.Multiplayer.SendMessageToServer(NetworkId, sendData);
                                log("Message sent");

                                break;
                        }
                    }
                }
            }

        }




        private void gpsProcess(long playerID, List<string> words)
        {

            IMyPlayer player = getPlayerFromID(playerID);
            if (words.Count == 1)
            {//thanks to sagepourpre for finding this error
                words.Add("help");
            }

            String name = "";

            for (int i = 0; i < words.Count; i++)
            {
                if (i > 1)
                {
                    if (i == words.Count - 1)
                    {
                        name += words[i];
                    }
                    else
                    {
                        name += words[i] + " ";
                    }

                }
            }

            var gps = MyAPIGateway.Session.GPS.GetGpsList(player.IdentityId).ToList();

            switch (words[1])
            {
                case "range":
                case "r":
                    if (words.Count == 3)
                    {
                        if (words[2] == "off")
                        {
                            doRange = false;
                            filterGPS(false, true, false, false, "", playerID);
                        }
                        else
                        {
                            decimal r;
                            if (decimal.TryParse(words[2], out r))
                            {
                                doRange = true;
                                range = (double)r * 1000;
                            }
                            else
                            {
                                WriteErrorMessage(player.IdentityId, "Input " + words[2] + " is not a valid range");
                            }
                        }
                    }
                    break;
                case "dist":
                    
                    //split input into 2 names
                    var gpss = new List<string>(name.Trim().Split(':'));
                    
                    //only 2 to switch
                    if (gpss.Count == 2)
                    {
                        var gpsList = new List<VRage.Game.ModAPI.IMyGps>();
                        foreach (String gpsName in gpss)
                        {//find matching gps and add to list 
                            try
                            {
                                gpsList.Add(getGpsByName(gpsName, gps));
                            }
                            catch
                            {
                                WriteErrorMessage(player.IdentityId, "Error-GPS does not exist");
                                break;
                            }
                        }
                    
                        try
                        {
                            //get the 2 gps, edit the old to have the new loc
                            VRage.Game.ModAPI.IMyGps firstGps = gpsList.ElementAt(0);
                            VRage.Game.ModAPI.IMyGps secondGps = gpsList.ElementAt(1);

                            Vector3D firstPos = firstGps.Coords;
                            Vector3D secondPos = secondGps.Coords;
                            Double distance = Math.Round( Vector3D.Distance(firstPos, secondPos),1);
                            Double distKM = Math.Round(distance / 1000, 1);
                            WriteErrorMessage(player.IdentityId, "Distance between GPS " + firstGps.Name + " and " + secondGps.Name + " is " + distance+"m or "+distKM+"km");

                        }
                        catch
                        {
                            WriteErrorMessage(player.IdentityId, "One of those GPS points do not exist");
                        }

                    }
                    else
                    {
                        WriteErrorMessage(player.IdentityId, "Too few/many gps points listed. Separate them by :");
                    }
                    break;


                case "extend":
                    //split input into 2 names
                    var extendText = new List<string>(name.Trim().Split(':'));

                    //only 4 parameters
                    if (extendText.Count == 4)
                    {
                        
                        String newGPSName = extendText.ElementAt(0);
                        String gpsNameTail = extendText.ElementAt(1);
                        String gpsNameHead = extendText.ElementAt(2);

                        String distString = extendText.ElementAt(3);
                        decimal extDist;
                        if (decimal.TryParse(distString, out extDist))
                        {
                            log("successful conversion");
                            //convert to km
                            extDist *= 1000;
                        }
                        else
                        {
                            WriteErrorMessage(player.IdentityId, "Input " + distString + " is not a distance");
                            break;
                        }



                        VRage.Game.ModAPI.IMyGps firstGps = getGpsByName(gpsNameTail, gps);
                        VRage.Game.ModAPI.IMyGps secondGps = getGpsByName(gpsNameHead, gps);
                        
                        if(firstGps == null || secondGps == null)
                        {
                            WriteErrorMessage(player.IdentityId, "Error-GPS does not exist");
                            break;
                        }

                        
                        Vector3D firstPos = firstGps.Coords;
                        Vector3D secondPos = secondGps.Coords;

                        //get distance between 2 points
                        Double distance = Vector3D.Distance(firstPos, secondPos);

                        //scale extension to fraction of distance
                        Double scaledExtDist = ((Double)extDist )/ distance;

                        //lerp is linear interpolation. It will find the position partway between first and second, based on fraction of 3rd param
                        //So this is finding the point between the first and second at 100% + scaledExtDist% beyond.
                        Vector3D newPos = Vector3D.Lerp(firstPos, secondPos, 1 + scaledExtDist);

                        //Double distance = Math.Round(Vector3D.Distance(firstPos, secondPos), 1);
                        //Double distKM = Math.Round(distance / 1000, 1);

                        VRage.Game.ModAPI.IMyGps newExtendGPS = createGPSFromCommand(newGPSName, newPos);
                        MyAPIGateway.Session.GPS.AddGps(player.IdentityId, newExtendGPS);


                    }
                    else
                    {
                        WriteErrorMessage(player.IdentityId, "Too few/many parameters listed. Separate them by :");
                    }
                    break;
                case "add":
                case "a":
                    VRage.Game.ModAPI.IMyGps addGps = createGPSFromCommand(name, player.GetPosition());

                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, addGps);

                    break;
                case "delete":
                case "d":
                    
                    List<IMyGps> toDelete = new List<IMyGps>();
                    //prevent deleting everything
                    if (name.Equals(""))
                    {
                        break;
                    }
                    foreach (VRage.Game.ModAPI.IMyGps g in gps)
                    {
                        if(gpsContains(g, name)){
                            toDelete.Add(g);
                        }
                    }

                    foreach (VRage.Game.ModAPI.IMyGps g in toDelete)
                    {
                        MyAPIGateway.Session.GPS.RemoveGps(player.IdentityId, g);
                    }
                    break;

                case "deleteall":
                    foreach(VRage.Game.ModAPI.IMyGps g in gps)
                    {
                        MyAPIGateway.Session.GPS.RemoveGps(player.IdentityId, g);
                    }
                    break;

                case "recolour":
                case "recolor":
                case "rc":
                    Color newColour = new Color();
                    if (!name.Equals(""))
                    {
                        //get gps colour tag if it exists
                        String lastWord = name.Split(' ').Last();


                        //if the last word is of the format <hhhhhh> where h is a digit of a hexadecimal, then it is colour
                        if (Regex.IsMatch(lastWord, "<[A-f0-9]{6}>", RegexOptions.IgnoreCase))
                        {
                            newColour = stringToColour(lastWord);
                            name = Regex.Replace(name, "<[A-f0-9]{6}>", "");
                        }
                    }


                    
                    try
                    {

                        //for each gps, if it contains string, replace colour in gps

                        foreach (VRage.Game.ModAPI.IMyGps g in gps)
                        {
                            if (gpsContains(g, name))
                            {
                                g.GPSColor = newColour;
                                MyAPIGateway.Session.GPS.ModifyGps(player.IdentityId, g);
                            }
                        }

                    }
                    catch
                    {
                        WriteErrorMessage(player.IdentityId, "Error-GPS does not exist");
                    }
                    

                    
                    break;
                //added by 97CWEB
                case "copy":
                    //split input into 2 names
                    gpss = new List<string>(name.Trim().Split(':'));

                    //only 2 to switch
                    if (gpss.Count == 2)
                    {
                        var gpsList = new List<VRage.Game.ModAPI.IMyGps>();
                        foreach (String gpsName in gpss)
                        {//find matching gps and add to list 
                            try
                            {
                                gpsList.Add(getGpsByName(gpsName, gps));
                            }
                            catch
                            {
                                WriteErrorMessage(player.IdentityId, "Error-GPS does not exist");
                                break;
                            }
                        }

                        try
                        {
                            //get the 2 gps, edit the old to have the new loc
                            VRage.Game.ModAPI.IMyGps newPointGps = gpsList.ElementAt(0);
                            VRage.Game.ModAPI.IMyGps oldGps = gpsList.ElementAt(1);

                            //copy co-ordinates from one to the other  
                            oldGps.Coords = newPointGps.Coords;


                            MyAPIGateway.Session.GPS.ModifyGps(player.IdentityId, oldGps);

                        }
                        catch
                        {
                            WriteErrorMessage(player.IdentityId, "One of those GPS points do not exist");
                        }

                    }
                    else
                    {
                        WriteErrorMessage(player.IdentityId, "Too few/many gps points listed. Separate them by :");
                    }
                    break;

                case "moveHere":
                case "mh":

                    try
                    {
                        VRage.Game.ModAPI.IMyGps g = getGpsByName(name, gps);
                        g.Coords = player.GetPosition();
                        MyAPIGateway.Session.GPS.ModifyGps(player.IdentityId, g);
                    }
                    catch
                    {
                        WriteErrorMessage(player.IdentityId, "Error-GPS does not exist");
                    }
                    //get gps from name
                    //create gps at new spot
                    //copy from 1 to another
                    //delete new gps
                    break;



                case "factionShareAll":
                case "fsa":


                    //get player faction members, this line does all faction ones


                    KeyValuePair<long, MyFactionMember>[] members = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId).Members.ToArray();
                    //for each member, give them all of the player's gps's


                    foreach (KeyValuePair<long, MyFactionMember> member in members)
                    {


                        long memberID = member.Value.PlayerId;


                        foreach (IMyGps g in gps)
                        {
                            if (!MyAPIGateway.Session.GPS.GetGpsList(memberID).Contains(g))
                            {
                                MyAPIGateway.Session.GPS.AddGps(memberID, MyAPIGateway.Session.GPS.Create(g.Name, g.Description, g.Coords, true));
                            }

                        }
                    }
                    break;

                case "addFactionShare":
                case "afs":
                    //get gps
                    addGps = createGPSFromCommand(name, player.GetPosition());

                    //add local
                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, addGps);

                    //get member list
                    members = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId).Members.ToArray();

                    //add to each member in faction
                    foreach (KeyValuePair<long, MyFactionMember> member in members)
                    {
                        long memberID = member.Value.PlayerId;
                        if (!MyAPIGateway.Session.GPS.GetGpsList(memberID).Contains(addGps))
                        {
                            MyAPIGateway.Session.GPS.AddGps(memberID, addGps);

                        }
                    }

                    break;
                case "factionShare":
                case "fs":
                    //get player faction members, this line does all faction ones
                    

                    members = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId).Members.ToArray();

                    //get name of GPS to share, exact match
                    List<IMyGps> toAdd = new List<IMyGps>();
                    foreach (VRage.Game.ModAPI.IMyGps g in gps)
                    {
                        if (gpsContains(g, name))
                        {
                            toAdd.Add(g);
                        }
                    }

                    //get gpss to add to members


                    
                    //for each member, give them all of the player's gps's with this tag
                    foreach (KeyValuePair<long, MyFactionMember> member in members)
                    {
                        long memberID = member.Value.PlayerId;
                        foreach (IMyGps g in toAdd)
                        {
                            if (!MyAPIGateway.Session.GPS.GetGpsList(memberID).Contains(g))
                            {
                                MyAPIGateway.Session.GPS.AddGps(memberID, MyAPIGateway.Session.GPS.Create(g.Name, g.Description, g.Coords, true));

                            }
                        }
                    }


                    break;
                case "factionShareVisible":
                case "fsv":
                    members = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId).Members.ToArray();

                    //for each member, give them all of the player's gps's
                    foreach (KeyValuePair<long, MyFactionMember> member in members)
                    {
                        foreach (VRage.Game.ModAPI.IMyGps g in gps)
                        {
                            long memberID = member.Value.PlayerId;
                            if (g.ShowOnHud && !MyAPIGateway.Session.GPS.GetGpsList(memberID).Contains(g))
                            {
                                MyAPIGateway.Session.GPS.AddGps(memberID, MyAPIGateway.Session.GPS.Create(g.Name, g.Description, g.Coords, true));
                            }
                        }
                    }
                    break;

                case "hide":
                case "h":
                    if (!name.Equals(""))
                    {
                        filterGPS(false, false, true, false, name, playerID);
                    }
                    break;
                case "show":
                case "s":
                    if (!name.Equals(""))
                    {
                        filterGPS(false, false, false, true, name, playerID);
                    }
                    break;
                case "hideall":
                case "ha":
                    filterGPS(true, false, false, false, "", playerID);
                    break;
                case "showall":
                case "sa":
                    filterGPS(false, true, false, false, "", playerID);
                    break;
                case "showonly":
                case "so":
                    if (!name.Equals(""))
                    {
                        filterGPS(true, false, false, false, "", playerID);           // Hide them all then show the filtered ones.
                        filterGPS(false, false, false, true, name, playerID);
                    }
                    break;
                case "cardinals":
                case "c":
                    const double maxD = 1000000000;

                    // Check the existing list, if there is a GPS coord at origin then niavely assume they have already run this command ;)
                    foreach (VRage.Game.ModAPI.IMyGps g in gps)
                    {
                        if (Math.Abs(g.Coords.X) < double.Epsilon && g.Coords.Y < double.Epsilon && g.Coords.Z < double.Epsilon)
                        {
                            WriteErrorMessage(player.IdentityId, "GPS: ERROR: Cardinals already in GPS. Delete coord at origin to make new ones.");
                            return;
                        }
                    }
                    VRage.Game.ModAPI.IMyGps origin = MyAPIGateway.Session.GPS.Create("Nav Origin", "", new VRageMath.Vector3D(0.0, 0.0, 0.0), true);
                    VRage.Game.ModAPI.IMyGps zplus = MyAPIGateway.Session.GPS.Create("Nav Down", "", new VRageMath.Vector3D(0.0, 0.0, maxD), true);
                    VRage.Game.ModAPI.IMyGps zminus = MyAPIGateway.Session.GPS.Create("Nav Up", "", new VRageMath.Vector3D(0.0, 0.0, -maxD), true);
                    VRage.Game.ModAPI.IMyGps xplus = MyAPIGateway.Session.GPS.Create("Nav North", "", new VRageMath.Vector3D(maxD, 0.0, 0.0), true);
                    VRage.Game.ModAPI.IMyGps xminus = MyAPIGateway.Session.GPS.Create("Nav South", "", new VRageMath.Vector3D(-maxD, 0.0, 0.0), true);
                    VRage.Game.ModAPI.IMyGps yplus = MyAPIGateway.Session.GPS.Create("Nav East", "", new VRageMath.Vector3D(0.0, maxD, 0.0), true);
                    VRage.Game.ModAPI.IMyGps yminus = MyAPIGateway.Session.GPS.Create("Nav West", "", new VRageMath.Vector3D(0.0, -maxD, 0.0), true);

                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, origin);
                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, zplus);
                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, zminus);
                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, xplus);
                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, xminus);
                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, yplus);
                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, yminus);

                    break;
                case "shownearest":
                case "sn":

                    if (name.Equals(""))
                    {
                        break;  
                    }

                    //for each gps, save if shorter

                    Vector3D playerPos = player.GetPosition();

                    double minDist = Double.MaxValue;
                    VRage.Game.ModAPI.IMyGps minGps=null;

                    foreach (VRage.Game.ModAPI.IMyGps g in gps)
                    {
                        double dist = VRageMath.Vector3D.Distance(playerPos, g.Coords);

                        if (dist<minDist) 
                        {
                            if(gpsContains(g, name)){
                                minDist = dist;
                                minGps = g;
                            }
                            
                        }
                    }

                    //hide all
                    filterGPS(false, false, true, false, name, playerID);

                    //show min
                    if(minGps != null)
                    {
                        MyAPIGateway.Session.GPS.SetShowOnHud(playerID, minGps.GetHashCode(), true);
                    }
                    break;

                case "export":
                case "e":
                    var clipboard = "";


                    //if no name passed in, save all gps points
                    if (name.Equals(""))
                    {
                        foreach (VRage.Game.ModAPI.IMyGps g in gps)
                        {
                            clipboard += g.ToString() + "`\n";
                        }
                    }

                    //export gps points with matching tags
                    else
                    {
                        foreach (VRage.Game.ModAPI.IMyGps g in gps)
                        {
                            if (gpsContains(g, name))
                                clipboard += g.ToString() + "`\n";
                        }
                    }
                    
                    
                    log(clipboard);
                    VRage.Utils.MyClipboardHelper.SetClipboard(clipboard);
                    MyAPIGateway.Utilities.ShowNotification("Tagged GPS List Saved To Clipboard", 5000, "White");
                    break;
                case "import":
                case "i":

                    //regex for finding anything in { } 
                    Regex regx = new Regex("{.+}");
                    Match match= Regex.Match(name, regx.ToString());

                    //import params
                    bool rename = false;
                    bool recolour = false;
                    bool update = false;

                    //if import name contains { } at spot 0
                    if(match.Success && match.Index == 0)
                    {
                        //remove from name
                        name = regx.Replace(name, "", 1);


                        //remove { }
                        String importParamsString = match.Value.Trim('{', '}');

                        log(importParamsString);

                        //split contents into individual words
                        char[] separators = new char[] { ' ', '_' };
                        String[] importParams = importParamsString.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                        for(int i =0; i < importParams.Length; i++)
                        {
                            switch (importParams[i])
                            {
                                case "rename":
                                    rename= true;
                                    break;
                                case "update":
                                    update = true;
                                    break;
                                case "rc":
                                    recolour = true;
                                    break;
                            }
                        }
                    }



                    //split gps list on `
                    String[] newGpss = name.Split('`');

                    //for each gps string
                    for (int i = 0; i < newGpss.Length; i++)
                    {

                        String newGPS = newGpss[i];

                        //if string not empty
                        if (newGPS == null || newGPS.Length == 0)
                        {
                            break;
                        }

                        //split string on :
                        String[] gpsParts = newGPS.Split(':');

                        //if first word is GPS
                        if (gpsParts[0].Trim().Equals("GPS"))
                        {
                            //get name
                            String gpsName = gpsParts[1];

                            float x = 0;
                            float y = 0;
                            float z = 0;

                            //parse gps position
                            if (float.TryParse(gpsParts[2], out x) && float.TryParse(gpsParts[3], out y) && float.TryParse(gpsParts[4], out z))
                            {

                                VRage.Game.ModAPI.IMyGps foundGPSByName= getGpsByExactName(gpsName, gps);
                                VRage.Game.ModAPI.IMyGps foundGPSByCoord = getGpsByCoord(x,y,z, gps);

                                bool nameMatch = foundGPSByName != null;
                                bool coordMatch = foundGPSByCoord != null;

                                log(nameMatch +" " + coordMatch);

                                //get colour info
                                int start = gpsParts[5].Length - 6;
                                Color c = ColorExtensions.HexToColor("75C9F1");
                                if (start >= 0)
                                {
                                    String hexCode = '#' + gpsParts[5].Substring(start, 6);
                                    c = ColorExtensions.HexToColor(hexCode);
                                }

                                //if match by name, not by coord, and wanting update, relocate
                                if (nameMatch && !coordMatch && update){
                                    //update coords based on new location
                                    foundGPSByName.Coords = new Vector3D(x, y, z);

                                    if (recolour)
                                    {
                                        foundGPSByName.GPSColor = c;
                                    }

                                    MyAPIGateway.Session.GPS.ModifyGps(player.IdentityId, foundGPSByName);
                                }

                                /* 
                                //if match by name, no coord match and no update, add with bonus text
                                else if(nameMatch && !coordMatch && !update)
                                {
                                    //add with bonus text?
                                }
                                */

                                //if no match name, match coords and wanting to rename, rename
                                else if (!nameMatch && coordMatch && rename)
                                {
                                    //rename based on new gps
                                    foundGPSByCoord.Name = gpsName;

                                    if (recolour)
                                    {
                                        foundGPSByCoord.GPSColor = c;
                                    }

                                    MyAPIGateway.Session.GPS.ModifyGps(player.IdentityId, foundGPSByCoord);
                                }

                                //if no match at all, or (no match name and no rename) or (no coord match and no update), create new
                                else if((!nameMatch && !coordMatch) || (!nameMatch && !rename) || (!coordMatch &&!update))
                                { 
                                    //create new GPS
                                    VRage.Game.ModAPI.IMyGps createdGPS = MyAPIGateway.Session.GPS.Create(gpsName, "", new Vector3(x, y, z), true);


                                    createdGPS.GPSColor = c;
                                    //add gps
                                    MyAPIGateway.Session.GPS.AddGps(player.IdentityId, createdGPS);
                                }

                                // rest is do nothing, includes when found by name and coord
                                
                            }
                        }
                    }
                    break;
                case "help":
                    const string helpMessage =
                        "hideall|ha:                           Hide all GPS coords.\n\n" +

                        "showall|sa:                         Show all GPS coords.\n\n" +

                        "hide|h <tag>:                       Hide all GPS containing <tag>.\n\n" +

                        "show|s <tag>:                     Show all GPS containing <tag>.\n\n" +

                        "showonly|so <tag>:            Show only GPS containing <tag>.\n\n" +

                        "shownearest|sn <tag>:      Show nearest GPS containing <tag>.\n\n" +

                        "add|a [<name>][colour]:     Add GPS with optional name and colour.\n\n" +

                        "delete|d [<name>]:             delete GPS containing <name>.\n\n" +

                        "deleteall:                            delete all GPS points.\n\n" +

                        "copy <name1>:<name2>:		   copy the coordinates of name1 into name2\n\n" +

                        "recolour|rc <tag> <colour>:		      recolour the given gps point to the new colour\n\n" +

                        "moveHere|mh <name>:	   	 	  move gps with name name to where you are\n\n" +

                        "range|r [<distance>|off]:     Hide all GPS beyond distance.\n" +
                        "                                            distance in km. \"off\" turns off filter.\n\n" +

                        "factionShareAll|fsa:            Share all GPS coords with your faction.\n\n" +

                        "factionShare|fs:                  Share all GPS coords containing <tag>\n" +
                        "                                            with your faction.\n\n" +

                        "addFactionShare|afs:          Create and share a new GPS \n\n" +

                        "factionShareVisible|fsv:     Share only visible GPS coords\n" +
                        "                                            with your faction.\n\n" +

                        "cardinals|c:                         Add cardinal GPS coords.\n\n" +

                        "export|e <tag>:                    Stores a list in the clipboard of all GPS\n" +
                        "                                                       containing <tag>\n\n" +

                       "import|i {params} (GPS list):      Copies the GPSs pasted in after " +
                       "                                                                    command into the gps.\n" +
                       "                                                    OPTIONAL params are: \n" +
                       "                                                    \"rename\" to change matching points to\n" +
                       "                                                       new name\n" +
                       "                                                    \"update\" to change matching name to \n" +
                       "                                                       new location\n" +
                       "                                                    \"rc\" to recolour gps of either update or\n" +
                       "                                                       renamed gps\n\n" +

                       "dist: <name1>:<name2>     Calculates distance between 2 points.\n\n" +

                       "extend: <name>:<name1>:<name2>:<distance>       Creates GPS <name>\n" +
                       "                                                                                    on <name1> <name2> line\n" +
                       "                                                                                     offset by <distance>.\n\n";



                    MyAPIGateway.Utilities.ShowMissionScreen("GPS Utility Commands", "/ez <command>", " ", helpMessage, null, "OK");
                    
                    break;

                default:
                    //WriteErrorMessage(player.IdentityId, "Input is not a valid command");
                    WriteErrorMessage(playerID, "Input is not a valid command");
                    break;
            }
        }

        private VRage.Game.ModAPI.IMyGps createGPSFromCommand(string name, Vector3D playerPos)
        {
            bool changeColour = false;
            Color newColour = new Color();

            //get gps colour tag if it exists
            char[] separators = new char[] { ' ', '_' };
            String lastWord = name.Split(separators, StringSplitOptions.RemoveEmptyEntries).Last();


            //if the last word is of the format <hhhhhh> where h is a digit of a hexadecimal, then it is colour
            if (Regex.IsMatch(lastWord, "<[A-f0-9]{6}>", RegexOptions.IgnoreCase))
            {
                newColour = stringToColour(lastWord);
                changeColour = true;
                name = Regex.Replace(name, "<[A-f0-9]{6}>", "");
            }


            var addGps = MyAPIGateway.Session.GPS.Create(name, "", playerPos, true);

            if (changeColour)
            {
                addGps.GPSColor = newColour;
            }

            return addGps;
        }

        private Color stringToColour(string lastWord)
        {
            String hexColour = lastWord.Replace("<", "").Replace(">", "");

            int r = int.Parse(hexColour.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hexColour.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hexColour.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            return new Color(r,g,b);
        }

        private IMyPlayer getPlayerFromID(long playerID)
        {
            List<IMyPlayer> allPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(allPlayers);
            for (int i = 0; i < allPlayers.Count; i++)
            {
                if (allPlayers[i].IdentityId == playerID)
                {
                    return allPlayers[i];
                }
            }
            return null;
        }

        // Filter out the appropriate GPS coords.
        void filterGPS(bool hideAll, bool showAll, bool hide, bool show, string tag, long playerID)
        {
            log("filtering");
            List<VRage.Game.ModAPI.IMyGps> gps = MyAPIGateway.Session.GPS.GetGpsList(playerID).ToList();
            foreach (VRage.Game.ModAPI.IMyGps g in gps)
            {

                if (hideAll == true || (hide == true && (gpsContains(g, tag))))
                {
                    MyAPIGateway.Session.GPS.ModifyGps(playerID, g);
                    MyAPIGateway.Session.GPS.SetShowOnHud(playerID, g.GetHashCode(), false);
                }
                if (showAll == true || (show == true && (gpsContains(g, tag))))
                {
                    MyAPIGateway.Session.GPS.ModifyGps(playerID, g);
                    MyAPIGateway.Session.GPS.SetShowOnHud(playerID, g.GetHashCode(), true);
                }
            }
        }



        VRage.Game.ModAPI.IMyGps getGpsByExactName(String gpsName, List<VRage.Game.ModAPI.IMyGps> gpsList)
        {
            foreach (VRage.Game.ModAPI.IMyGps gps in gpsList)
            {

                if (gps.Name.Equals(gpsName))
                {
                    return gps;
                }
            }
            return null;

        }



        VRage.Game.ModAPI.IMyGps getGpsByName(String gpsName, List<VRage.Game.ModAPI.IMyGps> gpsList)
        {
            foreach (VRage.Game.ModAPI.IMyGps gps in gpsList)
            {
                bool doesContain = gpsContains(gps, gpsName);
                
                if (doesContain)
                {
                    return gps;
                }
            }
            return null;

        }

        VRage.Game.ModAPI.IMyGps getGpsByCoord(float x, float y, float z, List<VRage.Game.ModAPI.IMyGps> gpsList)
        {
            foreach (VRage.Game.ModAPI.IMyGps gps in gpsList)
            {

                if (doubleEquivalent(gps.Coords.X,x,0.1) && doubleEquivalent(gps.Coords.Y, y, 0.1) && doubleEquivalent(gps.Coords.Z, z, 0.1)) {
                    return gps;
                }
                
            }
            return null;

        }

        bool gpsContains(VRage.Game.ModAPI.IMyGps g, String tag)
        {
            char[] separators = new char[] { ' ', '_' };
            var tagSplit = new List<string>(tag.Trim().Split(separators, StringSplitOptions.RemoveEmptyEntries));
            
            bool doesContain = true;
            foreach (String word in tagSplit)
            {
                String cleanedWord = Regex.Escape(word);
                try
                {
                    doesContain &= Regex.IsMatch(g.Name, @"(?:^|[\s_])" + cleanedWord + @"(?:\b|_)", RegexOptions.IgnoreCase) && cleanedWord.Length > 0;
                }
                catch(ArgumentNullException e)
                {
                    log("Regex word failure: \""+cleanedWord+"\" Taglist: "+tag);
                }
            }
            return doesContain;
        }

        bool doubleEquivalent(double a, double b, double precision)
        {
            return Math.Abs(a - b) < precision;
        }

        // Writes to SpaceEngineers/Storage
        void log(string text)
        {
            using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage("EZGPS Log.txt"))
            {
                writer.WriteLine(text);
            }
        }

        #region post_script
    }
}
#endregion post_script