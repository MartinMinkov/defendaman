﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Runtime.InteropServices;
using UnityEngine.UI;

/*Carson
Being used to denote what type of data we are sending/receiving for a given JSON object.
e.g. Player is valued at 1. If we receive a JSON object for type Player ID 1, that is "Player 1's" data.
     Projectile is defined at 2. If we receive a JSON object for type Projectile ID 3, that is "Projectile 3's" data
    
Enviroment does not have an ID associated with it, since it is one entity. The ID we use for it will always default to 0

Note: Does not start at value 0. Reason being, if JSON parser fails, it returns 0 for fail, so checking
for fail does not work 
*/
public enum DataType
{
    Player = 1, Trigger = 2, Environment = 3, StartGame = 4, ControlInformation = 5, Lobby = 6
}

public enum Protocol
{
    TCP, UDP, NA
}

/*Carson
Class used for handling sending/receiving data. The class has 2 uses:
* To send/receive data from the Networking Team's clientside code, and
* Notifying subscribed objects when new data is updated

To subscribe for an objects updates from server, you would call the public Subscribe method.
This method takes in three things:
    Callback method, which is a void method that takes in a JSONClass as a parameter
    DataType you want to receive, e.g. DataType.Player for data of a player
    int ID of which of the DataType you want to receive info from, e.g. ID 1 on DataType.Player is Player 1's data

e.g. NetworkingManager.Subscribe((JSONClass json) => {Debug.Log("Got Player 1's Data");}, DataType.Player, 1);
*/
public class NetworkingManager : MonoBehaviour
{
    
    #region Variables
    // Game object to send data of
    public Transform playerType;
    public GameObject player;

    //Holds the subscriber data
    private static Dictionary<Pair<DataType, int>, List<Action<JSONClass>>> _subscribedActions = new Dictionary<Pair<DataType, int>, List<Action<JSONClass>>>();

    //List of JSON strings to be sent on the next available TCP packet 
    private static List<string> jsonTCPObjectsToSend = new List<string>();

    //List of JSON strings to be sent on the next available UDP packet
    private static List<string> jsonUDPObjectsToSend = new List<string>();
    
    public static IntPtr TCPClient { get; private set; }
    public static IntPtr UDPClient { get; private set; }

    #endregion

    void Start()
    {
        try {
            //TCPClient = TCP_CreateClient();
            UDPClient = Game_CreateClient();
            UDP_ConnectToServer("192.168.0.14", 7000);
            UDP_StartReadThread();
        } catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    // Update is called once per frame
    void Update()
    {
        update_data(receive_data());
        send_data();

        if (Input.GetKeyDown(KeyCode.Space))
            StartOfGame();
    }

    ////Code for subscribing to updates from client-server system////
    #region SubscriberSystem
    /*
    To subscribe for an objects updates from server, you would call the public Subscribe method.
    This method takes in three things:
    Callback method, which is a void method that takes in a JSONClass as a parameter
    DataType you want to receive, e.g. DataType.Player for data of a player
    int ID of which of the DataType you want to receive info from, e.g. ID 1 on DataType.Player is Player 1's data

    e.g. NetworkingManager.Subscribe((JSONClass json) => {Debug.Log("Got Player 1's Data");}, DataType.Player, 1);
    */
    public static void Subscribe(Action<JSONClass> callback, DataType dataType, int id = 0)
    {
        Pair<DataType, int> pair = new Pair<DataType, int>(dataType, id);

        if (!(_subscribedActions.ContainsKey(pair)))
        {
            _subscribedActions.Add(pair, new List<Action<JSONClass>>());
        }
        List<Action<JSONClass>> val = null;
        _subscribedActions.TryGetValue(pair, out val);
        if (val != null)
        {
            //Add our callback to the list of entries under that pair of datatype and ID.
            _subscribedActions[pair].Add(callback);
        }
    }

    private void update_data(string JSONGameState)
    {
        JSONArray gameObjects = null;
        try {
            gameObjects = JSON.Parse(JSONGameState).AsArray;
        } catch (Exception e) {
            Debug.Log(e.ToString());
            return;
        }
        foreach (var node in gameObjects.Children)
        {
            var obj = node.AsObject;
            int dataType = obj["DataType"].AsInt;
            int id = obj["ID"].AsInt;

            if (dataType == (int)DataType.Environment)
            {
                gameObject.GetComponent<MapManager>().handle_event(id, obj);// receive_message(obj, id);
            }

            if (id != 0 || (dataType == (int)DataType.Environment || dataType == (int)DataType.StartGame))
            {
                Pair<DataType, int> pair = new Pair<DataType, int>((DataType)dataType, id);
                if (_subscribedActions.ContainsKey(pair))
                {
                    foreach (Action<JSONClass> callback in _subscribedActions[pair])
                    {
                        callback(obj);
                    }
                }
            }
        }
    }

    #endregion

    ////Code for communicating with client-server system////
    #region CommunicationWithClientSystem
    [DllImport("ClientLibrary.so")]
    public static extern IntPtr TCP_CreateClient();

	[DllImport("ClientLibrary.so")]
	public static extern void TCP_DisposeClient(IntPtr client);
	public static void TCP_DisposeClient(){
		TCP_DisposeClient(TCPClient);
	}
    [DllImport("ClientLibrary.so")]
    private static extern int TCP_ConnectToServer(IntPtr client, string ipAddress, short port);
    public static int TCP_ConnectToServer(string ipAddress, short port) {
        return TCP_ConnectToServer(TCPClient, ipAddress, port);
    }

    [DllImport("ClientLibrary.so")]
    private static extern int TCP_Send(IntPtr client, string message, int size);
    public static int TCP_Send(string message, int size) {
        return TCP_Send(TCPClient, message, size);
    }

    [DllImport("ClientLibrary.so")]
    private static extern IntPtr TCP_GetData(IntPtr client);
    public static IntPtr TCP_GetData() {
        return TCP_GetData(TCPClient);
    }

    [DllImport("ClientLibrary.so")]
    private static extern int TCP_StartReadThread(IntPtr client);
    public static int TCP_StartReadThread() {
        return TCP_StartReadThread(TCPClient);
    }

    [DllImport("ClientLibrary.so")]
    public static extern IntPtr Game_CreateClient();

    [DllImport("ClientLibrary.so")]
    private static extern void Game_DisposeClient(IntPtr client);
    public static void UDP_DisposeClient() {
        Game_DisposeClient(UDPClient);
    }

    [DllImport("ClientLibrary.so")]
    private static extern int Game_ConnectToServer(IntPtr client, string ipAddress, short port);
    public static int UDP_ConnectToServer(string ipAddress, short port) {
        return Game_ConnectToServer(UDPClient, ipAddress, port);
    }

    [DllImport("ClientLibrary.so")]
    private static extern int Game_Send(IntPtr client, string message, int size);
    public static int UDP_SendData(string message, int size) {
        return Game_Send(UDPClient, message, 512);
    }

    [DllImport("ClientLibrary.so")]
    private static extern IntPtr Game_GetData(IntPtr client);
    public static IntPtr UDP_GetData() {
        return Game_GetData(UDPClient);
    }

    [DllImport("ClientLibrary.so")]
    private static extern int Game_StartReadThread(IntPtr client);
    public static int UDP_StartReadThread() {
        return Game_StartReadThread(UDPClient);
    }

    [DllImport("MapGeneration.so")]
    private static extern string GenerateMap(int seed);

    /*// Imported function from C++ library for receiving data
    [DllImport("NetworkingLibrary.so")]
    public static extern IntPtr receiveData();

    // Imported function from C++ library for sending data
    [DllImport("NetworkingLibrary.so")]
    public static extern void sendDataUDP(string data);*/

    //On Linux, send data to C++ file
    private void send_data()
    {
        var tcp = create_sending_json(Protocol.TCP);
        var udp = create_sending_json(Protocol.UDP);
        if (Application.platform == RuntimePlatform.LinuxPlayer)
        {
            if (tcp != "[]")
                TCP_Send(tcp, tcp.Length);
            UDP_SendData(udp, udp.Length);
        }
        if (tcp != "[]")
            lastTCP = tcp;
        if (udp != "[]")
            lastUDP = udp;
    }

    //Receive a packet from C++ networking client code
    private string receive_data()
    {
        //On Linux, get a proper packet
        if (Application.platform == RuntimePlatform.LinuxPlayer) {
            result = Marshal.PtrToStringAnsi(UDP_GetData());
        } else {
            //On Windows, return whatever JSON data we want to generate/test for
            result = create_test_json();
        }

        return result;
    }

    //Generate the JSON file to send to C++ networking client code
    private string create_sending_json(Protocol protocol)
    {
        //Open JSON array
        string sending = "[";

        if (protocol == Protocol.UDP)
        {
            if (player != null)
            {
                //Add player data
                var memberItems = new List<Pair<string, string>>();
                memberItems.Add(new Pair<string, string>("x", player.transform.position.x.ToString()));
                memberItems.Add(new Pair<string, string>("y", player.transform.position.y.ToString()));
                memberItems.Add(new Pair<string, string>("rotationZ", player.GetComponent<PlayerRotation>().curRotation.z.ToString()));
                memberItems.Add(new Pair<string, string>("rotationW", player.GetComponent<PlayerRotation>().curRotation.w.ToString()));
                send_next_packet(DataType.Player, player.GetComponent<BaseClass>().playerID, memberItems, protocol);
            }
        }

        //Add data that external sources want to send
        foreach (var item in protocol == Protocol.TCP ? jsonTCPObjectsToSend : jsonUDPObjectsToSend)
        {
            sending += item;
        }

        if (protocol == Protocol.TCP)
            jsonTCPObjectsToSend.Clear();
        else
            jsonUDPObjectsToSend.Clear();

        //Close json array
        if (sending.Length > 2)
            sending = sending.Remove(sending.Length - 1, 1);
        sending += "]";

        return sending;
    }

    //Add data to be sent
    public static string send_next_packet(DataType type, int id, List<Pair<string, string>> memersToSend, Protocol protocol)
    {
        string sending = "";
        if (protocol == Protocol.NA)
            sending += "[";
        sending = "{";
        sending += " DataType : " + (int)type + ", ID : " + id + ",";

        foreach (var pair in memersToSend)
        {
            sending += " " + pair.first + " : " + pair.second + ",";
        }

        sending = sending.Remove(1, 1);
        sending = sending.Remove(sending.Length - 1, 1);
        sending += "},";
        switch (protocol)
        {
            case Protocol.UDP:
                jsonUDPObjectsToSend.Add(sending);
                break;
            case Protocol.TCP:
                jsonTCPObjectsToSend.Add(sending);
                break;
        }

        return sending;
    }
    #endregion 

    ////Game creation code
    #region StartOfGame

    void StartGame(JSONClass data)
    {
        int myPlayer = GameData.MyPlayerID;
        int myTeam = 0;
        List<Pair<int, int>> kings = new List<Pair<int, int>>();

        update_data(GenerateMap(data["Seed"].AsInt));

        //foreach (JSONClass playerData in data["playersData"].AsArray)
        foreach (var playerData in GameData.LobbyData) {
            Debug.Log("Player Data: " + playerData.ToString());

            var createdPlayer = ((Transform)Instantiate(playerType, new Vector3(GameData.TeamSpawnPoints[playerData.TeamID-1].first, GameData.TeamSpawnPoints[playerData.TeamID-1].second, -10), Quaternion.identity)).gameObject;

            switch(playerData.ClassType)
            {
                case ClassType.Ninja:
                    createdPlayer.AddComponent<NinjaClass>();
                    break;
                case ClassType.Gunner:
                    createdPlayer.AddComponent<GunnerClass>();
                    break;
                case ClassType.Wizard:
                    createdPlayer.AddComponent<WizardClass>();
                    break;
                default:
                    Debug.Log("Player " + playerData.PlayerID + " has not selected a valid class. Defaulting to Gunner");
                    createdPlayer.AddComponent<GunnerClass>();
                    break;
            }


            createdPlayer.GetComponent<BaseClass>().team = playerData.TeamID;
            createdPlayer.GetComponent<BaseClass>().playerID = playerData.PlayerID;

            //if (playerData.King) //Uncomment this one line when kings are in place
                kings.Add(new Pair<int, int>(playerData.TeamID, playerData.PlayerID));

            if (myPlayer == playerData.PlayerID)
            {
                myTeam = playerData.TeamID;
                player = createdPlayer;
                GameObject.Find("Main Camera").GetComponent<FollowCamera>().target = player.transform;
                if (GameObject.Find("Minimap Camera") != null)
                    GameObject.Find("Minimap Camera").GetComponent<FollowCamera>().target = player.transform;
                player.AddComponent<Movement>();
                player.AddComponent<PlayerRotation>();
                player.AddComponent<Attack>();
                //Created our player
            }
            else {
                createdPlayer.AddComponent<NetworkingManager_test1>();
                createdPlayer.GetComponent<NetworkingManager_test1>().playerID = playerData.PlayerID;
                //Created another player
            }
        }

        foreach (var king in kings)
        {
            if (king.first == myTeam)
                GameData.AllyKingID = king.second;
            else
                GameData.EnemyKingID = king.second;
        }
    }

    #endregion

    ////Code for Carson's testing purposes////
    #region DummyTestingCode
    //Dummy data for the sake of testing.
    string result = "receiving failed";
    string lastUDP = "Last UDP";
    string lastTCP = "Last TCP";
    
    void OnGUI()
    {
        GUI.Label(new Rect(8, 0, Screen.width, Screen.height), "Last Received: " + result);
        GUI.Label(new Rect(8, 20, Screen.width, Screen.height), "UDP Sending: " + lastUDP);
        GUI.Label(new Rect(8, 40, Screen.width, Screen.height), "TCP Sending: " + lastTCP);
    }

    string create_test_json()
    {
        if (player != null)
        {
            return "[]";
            var rot = player.GetComponent<PlayerRotation>().curRotation;
            return "[{\"DataType\" : 1, \"ID\" : 1, \"x\" : 51.0, \"y\" : 51.0, \"rotationZ\" : " + rot.z + ", \"rotationW\" : " + rot.w + "}]";
        }
        else
        {
            return "[]";
        }
    }

    public void StartOfGame()
    {
        Subscribe(StartGame, DataType.StartGame);
        update_data("[{DataType : 3, ID : 0, mapWidth : 100, mapHeight : 100, mapIDs : [[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 0, 0, 0, 0, 105, 105, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 104, 104, 104, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 105, 106, 104, 0, 0, 104, 104, 105, 104, 104, 104, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 105, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 105, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 105, 104, 105, 103, 104, 104, 104, 104, 105, 104, 104, 103, 104, 103, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 105, 105, 105, 105, 0, 0, 0, 0, 104, 104, 104, 0, 0, 0, 0], [0, 104, 104, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 105, 105, 104, 0, 0, 0, 0, 0, 103, 104, 105, 104, 105, 105, 103, 104, 104, 104, 104, 105, 105, 104, 104, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 105, 104, 104, 104, 0, 0, 105, 105, 105, 105, 105, 0, 0, 0], [0, 104, 105, 105, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 103, 104, 104, 105, 0, 0, 0, 0, 0, 105, 103, 104, 104, 104, 0, 0, 0, 103, 105, 106, 104, 103, 104, 104, 104, 105, 104, 104, 104, 105, 105, 105, 103, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 103, 104, 104, 105, 104, 104, 105, 103, 104, 105, 105, 105, 104, 0, 0, 0], [0, 0, 103, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 104, 104, 104, 104, 0, 0, 0, 0, 0, 104, 104, 105, 105, 104, 103, 104, 104, 104, 104, 104, 103, 102, 105, 103, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 105, 104, 104, 105, 104, 104, 104, 105, 105, 104, 105, 105, 104, 104, 0, 0, 0], [0, 0, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 0, 0, 104, 104, 0, 0, 0, 0, 0, 105, 104, 104, 104, 104, 103, 104, 104, 105, 0, 0, 0, 0, 105, 104, 104, 105, 104, 104, 104, 104, 103, 104, 105, 103, 103, 104, 104, 104, 105, 104, 104, 103, 105, 104, 105, 103, 105, 103, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 104, 104, 104, 105, 105, 105, 103, 104, 104, 105, 105, 104, 105, 104, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 104, 104, 104, 104, 105, 104, 104, 104, 105, 104, 105, 104, 103, 103, 104, 105, 104, 105, 105, 0, 0, 105, 105, 104, 104, 104, 104, 105, 105, 104, 104, 104, 105, 103, 104, 104, 104, 105, 105, 105, 104, 102, 104, 104, 104, 105, 104, 103, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 103, 103, 105, 104, 105, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 104, 104, 104, 104, 105, 105, 103, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 105, 105, 105, 105, 105, 105, 105, 104, 105, 105, 104, 104, 105, 105, 103, 104, 104, 104, 104, 104, 105, 105, 105, 105, 105, 104, 103, 103, 105, 104, 104, 104, 105, 104, 0, 0, 0, 0, 0, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 105, 103, 105, 105, 105, 104, 104, 104, 103, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 104, 104, 104, 104, 105, 104, 105, 104, 104, 104, 105, 104, 105, 105, 104, 103, 104, 105, 105, 105, 106, 105, 106, 105, 103, 104, 105, 105, 106, 105, 105, 104, 105, 105, 104, 103, 105, 104, 104, 104, 104, 105, 105, 104, 105, 104, 105, 103, 105, 105, 105, 104, 0, 0, 0, 0, 104, 104, 104, 105, 0, 0, 0, 0, 0, 0, 104, 103, 104, 105, 105, 105, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 102, 103, 105, 103, 105, 104, 104, 105, 105, 104, 104, 104, 104, 104, 104, 103, 105, 105, 105, 105, 105, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 106, 105, 106, 104, 105, 105, 105, 105, 103, 105, 105, 104, 104, 104, 105, 105, 104, 105, 104, 105, 104, 105, 105, 104, 0, 0, 0, 0, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 105, 103, 103, 104, 104, 104, 105, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 105, 105, 104, 104, 105, 104, 104, 105, 105, 104, 104, 105, 105, 105, 105, 105, 105, 105, 105, 104, 105, 105, 105, 105, 105, 105, 106, 106, 105, 104, 105, 105, 104, 105, 105, 105, 104, 104, 104, 105, 104, 105, 104, 105, 105, 105, 105, 104, 105, 103, 0, 0, 0, 0, 0, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 103, 103, 104, 104, 104, 105, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 104, 105, 105, 104, 106, 105, 104, 104, 104, 104, 104, 0, 0, 103, 104, 105, 105, 104, 104, 104, 104, 105, 103, 105, 106, 104, 104, 104, 104, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 105, 104, 104, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 104, 104, 104, 104, 105, 104, 106, 105, 105, 104, 105, 104, 105, 0, 0, 0, 0, 103, 104, 105, 104, 104, 105, 105, 104, 103, 105, 104, 105, 103, 102, 104, 105, 104, 105, 105, 104, 103, 105, 105, 104, 105, 103, 103, 104, 104, 104, 104, 103, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 103, 104, 104, 104, 105, 105, 105, 105, 105, 102, 104, 104, 0, 0, 0, 0, 104, 103, 104, 104, 105, 105, 105, 104, 103, 104, 105, 102, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 105, 0, 0, 105, 105, 104, 104, 104, 104, 104, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 103, 104, 104, 103, 0, 0, 105, 105, 105, 104, 104, 104, 104, 105, 104, 104, 103, 104, 103, 104, 104, 104, 105, 105, 104, 104, 104, 104, 105, 0, 0, 0, 0, 105, 104, 105, 104, 104, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 104, 104, 105, 104, 105, 105, 105, 105, 105, 104, 105, 103, 104, 105, 105, 105, 105, 105, 103, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 103, 104, 104, 104, 0, 0, 0, 0, 105, 105, 104, 104, 104, 103, 103, 103, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 104, 104, 105, 105, 103, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 104, 105, 104, 104, 105, 105, 105, 105, 105, 104, 104, 104, 105, 104, 105, 104, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 103, 104, 103, 103, 105, 104, 105, 105, 105, 105, 0, 0, 0, 0, 105, 105, 105, 104, 105, 104, 105, 102, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 103, 104, 105, 105, 104, 104, 105, 104, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 105, 104, 105, 104, 104, 104, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 103, 104, 105, 104, 104, 104, 103, 104, 105, 103, 104, 105, 104, 104, 102, 104, 104, 104, 104, 104, 104, 105, 105, 104, 0, 0, 0, 105, 105, 104, 105, 105, 104, 104, 104, 103, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 104, 104, 105, 104, 105, 104, 105, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 104, 104, 105, 105, 104, 103, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 103, 104, 104, 103, 105, 103, 104, 102, 104, 102, 104, 104, 104, 104, 104, 104, 105, 105, 105, 104, 103, 0, 105, 105, 105, 105, 105, 105, 105, 104, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 104, 105, 105, 104, 104, 103, 103, 104, 105, 0, 0, 0, 0, 0, 0], [0, 0, 0, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 104, 0, 0, 104, 103, 103, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 103, 104, 104, 104, 104, 103, 104, 104, 104, 105, 103, 103, 104, 104, 104, 105, 105, 105, 105, 105, 105, 105, 103, 104, 104, 104, 104, 101, 105, 104, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 105, 104, 104, 105, 104, 104, 104, 104, 104, 105, 105, 0, 0, 0, 0, 0], [0, 0, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 0, 0, 0, 0, 105, 104, 0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 104, 105, 105, 104, 104, 106, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 104, 104, 104, 106, 104, 104, 104, 104, 104, 104, 104, 104, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 105, 104, 105, 105, 104, 104, 104, 104, 104, 103, 104, 0, 0, 0, 0, 0], [0, 0, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 103, 105, 105, 105, 105, 0, 0, 0, 0, 104, 103, 102, 0, 0, 0, 0, 0, 0, 105, 105, 104, 105, 105, 105, 106, 106, 105, 105, 104, 104, 105, 0, 0, 104, 105, 104, 106, 104, 103, 104, 104, 102, 104, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 103, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 104, 104, 104, 104, 105, 105, 104, 103, 103, 103, 105, 104, 104, 104, 0, 0, 0, 0, 0], [0, 0, 106, 105, 103, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 104, 104, 105, 105, 0, 0, 104, 104, 105, 104, 105, 0, 0, 0, 0, 105, 104, 104, 104, 104, 105, 104, 104, 105, 104, 104, 104, 104, 0, 0, 0, 0, 104, 105, 104, 105, 104, 104, 103, 104, 104, 104, 104, 105, 104, 104, 103, 104, 104, 103, 104, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 103, 104, 105, 104, 104, 104, 105, 105, 105, 104, 105, 0, 0, 0, 0, 0], [0, 0, 104, 105, 105, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 105, 105, 105, 105, 105, 104, 104, 104, 103, 104, 105, 105, 105, 104, 105, 103, 105, 103, 105, 104, 105, 104, 104, 104, 105, 104, 103, 103, 0, 0, 0, 0, 0, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 103, 105, 104, 104, 103, 103, 105, 105, 105, 104, 104, 0, 0, 0, 0, 0, 0, 0, 104, 104, 0, 0, 0, 104, 104, 104, 105, 104, 104, 104, 105, 105, 105, 105, 104, 105, 103, 0, 0, 0, 0, 0], [0, 0, 104, 104, 104, 0, 0, 0, 0, 0, 0, 105, 106, 105, 104, 104, 105, 104, 104, 104, 105, 104, 104, 104, 104, 104, 105, 105, 104, 103, 104, 104, 104, 104, 104, 104, 104, 103, 105, 105, 103, 102, 103, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 105, 104, 104, 105, 104, 0, 0, 0, 0, 104, 104, 104, 104, 0, 0, 104, 104, 104, 105, 104, 104, 105, 105, 105, 103, 105, 105, 105, 105, 0, 0, 0, 0, 0], [0, 0, 105, 105, 105, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 104, 104, 104, 105, 104, 104, 103, 104, 104, 105, 104, 105, 104, 105, 103, 103, 103, 105, 104, 103, 104, 104, 104, 105, 105, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 105, 104, 104, 103, 105, 104, 104, 103, 105, 105, 105, 104, 104, 103, 104, 104, 104, 0, 0, 0, 105, 103, 103, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 104, 104, 104, 105, 105, 104, 0, 0, 0, 0, 0], [0, 0, 105, 104, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 105, 104, 104, 105, 105, 104, 104, 104, 104, 104, 105, 105, 104, 105, 104, 104, 104, 105, 105, 105, 105, 104, 104, 103, 105, 104, 104, 103, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 104, 105, 105, 105, 104, 105, 104, 104, 103, 104, 105, 105, 104, 104, 104, 104, 104, 105, 104, 105, 105, 105, 103, 105, 104, 104, 103, 104, 104, 105, 105, 105, 103, 104, 104, 105, 104, 104, 105, 105, 104, 104, 0, 0, 0, 0, 0], [0, 0, 105, 105, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 103, 104, 105, 104, 105, 104, 104, 104, 104, 105, 106, 105, 105, 105, 105, 105, 105, 105, 105, 105, 103, 104, 104, 104, 103, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 104, 103, 104, 105, 104, 104, 105, 105, 104, 105, 103, 105, 103, 104, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 105, 104, 105, 104, 104, 104, 104, 105, 104, 104, 105, 105, 104, 105, 0, 0, 0, 0], [0, 103, 106, 104, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 105, 104, 104, 105, 104, 104, 104, 104, 104, 103, 105, 105, 105, 104, 105, 105, 106, 104, 105, 104, 103, 103, 104, 103, 104, 104, 104, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 104, 104, 104, 104, 104, 105, 104, 104, 105, 104, 104, 105, 104, 104, 104, 105, 104, 105, 105, 105, 105, 104, 105, 105, 104, 105, 104, 104, 104, 103, 104, 105, 105, 104, 104, 104, 104, 105, 105, 104, 0, 0, 0, 0], [0, 104, 105, 105, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 104, 105, 105, 105, 103, 104, 104, 104, 105, 104, 105, 105, 104, 105, 105, 104, 104, 103, 104, 104, 103, 104, 104, 105, 105, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 104, 104, 103, 104, 104, 103, 105, 104, 104, 103, 104, 104, 104, 104, 105, 105, 105, 105, 104, 105, 104, 105, 105, 103, 105, 104, 105, 104, 105, 103, 104, 104, 104, 105, 105, 105, 104, 104, 104, 104, 105, 105, 103, 0, 0, 0], [0, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 103, 105, 104, 105, 104, 105, 106, 104, 104, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 105, 103, 103, 104, 104, 105, 105, 105, 104, 104, 104, 0, 0, 0, 0, 0, 105, 105, 105, 103, 105, 104, 104, 105, 105, 105, 104, 104, 104, 105, 105, 104, 104, 103, 104, 104, 103, 105, 105, 105, 105, 104, 105, 105, 105, 104, 103, 104, 105, 105, 103, 105, 105, 104, 104, 104, 103, 104, 105, 104, 104, 105, 104, 105, 105, 0, 0, 0], [0, 103, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 105, 104, 103, 104, 104, 103, 103, 104, 105, 105, 105, 105, 105, 105, 105, 105, 0, 0, 0, 0, 104, 105, 105, 105, 104, 104, 105, 105, 105, 105, 104, 104, 105, 0, 0, 105, 105, 104, 104, 104, 105, 104, 105, 105, 105, 103, 105, 104, 105, 105, 0, 0, 103, 103, 103, 105, 104, 104, 104, 104, 104, 103, 105, 104, 105, 105, 105, 105, 104, 105, 104, 0, 0], [0, 104, 105, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 106, 105, 105, 105, 105, 104, 105, 105, 105, 104, 104, 105, 105, 103, 104, 103, 104, 104, 104, 105, 105, 105, 105, 106, 105, 105, 105, 0, 0, 0, 0, 104, 104, 105, 104, 104, 104, 105, 105, 104, 104, 105, 104, 0, 0, 0, 0, 104, 105, 104, 104, 105, 104, 105, 105, 104, 104, 105, 105, 105, 0, 0, 0, 0, 103, 104, 105, 104, 105, 104, 104, 104, 105, 105, 105, 105, 105, 105, 104, 104, 105, 104, 104, 0], [0, 103, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 104, 105, 106, 106, 105, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 104, 104, 105, 104, 104, 104, 104, 104, 104, 105, 105, 105, 104, 104, 0, 0, 0, 0, 0, 104, 104, 104, 104, 105, 106, 105, 104, 105, 105, 105, 0, 0, 0, 0, 105, 104, 104, 104, 104, 105, 105, 104, 104, 105, 104, 104, 105, 0, 0, 0, 0, 104, 103, 104, 104, 104, 105, 103, 104, 104, 104, 104, 104, 104, 105, 105, 105, 105, 106, 104, 0], [0, 104, 104, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 103, 104, 105, 104, 104, 105, 105, 105, 104, 103, 104, 104, 105, 103, 105, 104, 104, 104, 104, 105, 105, 104, 104, 104, 105, 104, 105, 105, 104, 105, 104, 0, 0, 0, 0, 104, 103, 104, 105, 105, 106, 105, 105, 105, 104, 105, 105, 0, 0, 0, 0, 104, 104, 104, 105, 105, 104, 104, 104, 104, 105, 105, 0, 0, 0, 0, 0, 105, 104, 105, 105, 104, 104, 105, 105, 104, 104, 104, 103, 102, 104, 105, 105, 105, 105, 105, 0], [0, 0, 104, 105, 103, 104, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 104, 106, 103, 104, 105, 105, 104, 105, 104, 104, 104, 104, 104, 103, 105, 105, 105, 105, 104, 105, 105, 105, 105, 104, 104, 103, 0, 0, 0, 0, 104, 104, 105, 105, 105, 105, 105, 104, 105, 105, 104, 105, 0, 0, 0, 0, 104, 104, 105, 104, 105, 104, 104, 104, 104, 105, 0, 0, 0, 0, 0, 0, 104, 103, 104, 104, 105, 105, 105, 105, 105, 104, 104, 104, 104, 103, 104, 105, 105, 106, 104, 0], [0, 0, 0, 105, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 104, 103, 104, 104, 105, 104, 104, 104, 103, 104, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 104, 104, 104, 105, 104, 104, 105, 104, 103, 0, 0, 0, 0, 0, 106, 103, 104, 105, 105, 104, 106, 106, 105, 105, 105, 0, 0, 0, 0, 104, 104, 105, 104, 105, 104, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 104, 104, 105, 104, 102, 104, 103, 104, 104, 104, 104, 104, 105, 104, 105, 0], [0, 0, 0, 105, 104, 105, 105, 0, 0, 0, 0, 0, 0, 104, 105, 104, 105, 104, 103, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 103, 104, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 105, 104, 105, 105, 104, 103, 0, 0, 0, 0, 0, 103, 104, 105, 104, 104, 104, 104, 104, 103, 104, 0, 0, 0, 0, 0, 104, 104, 103, 103, 104, 105, 105, 105, 104, 103, 104, 105, 105, 104, 104, 106, 105, 105, 103, 0], [0, 0, 0, 103, 105, 105, 105, 0, 0, 0, 0, 0, 105, 103, 103, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 104, 103, 104, 104, 104, 103, 104, 104, 104, 103, 105, 104, 105, 104, 104, 104, 0, 0, 0, 0, 0, 0, 106, 105, 105, 104, 104, 104, 104, 105, 104, 104, 104, 0, 0, 0, 0, 0, 104, 105, 105, 105, 104, 104, 105, 103, 104, 105, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 104, 105, 105, 104, 104, 104, 105, 104, 105, 104, 105, 105, 104, 0], [0, 0, 0, 104, 105, 104, 105, 104, 0, 0, 0, 0, 104, 104, 104, 104, 104, 104, 105, 104, 105, 104, 103, 103, 104, 104, 105, 104, 104, 104, 104, 105, 104, 103, 104, 104, 104, 104, 103, 104, 105, 104, 104, 104, 104, 0, 0, 0, 0, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 104, 0, 0, 0, 0, 0, 105, 105, 105, 104, 104, 103, 105, 104, 104, 105, 104, 0, 0, 0, 0, 104, 105, 105, 104, 104, 105, 105, 104, 105, 105, 104, 105, 105, 104, 105, 104, 104, 104, 0], [0, 0, 104, 104, 104, 105, 104, 105, 0, 0, 0, 0, 104, 104, 104, 104, 104, 104, 104, 105, 104, 105, 104, 104, 104, 105, 104, 104, 105, 104, 104, 105, 105, 104, 104, 103, 103, 103, 103, 104, 104, 105, 105, 104, 104, 0, 0, 0, 0, 105, 105, 105, 105, 105, 104, 104, 104, 104, 103, 104, 105, 104, 0, 0, 0, 105, 105, 105, 105, 105, 103, 103, 104, 105, 105, 104, 103, 0, 0, 0, 105, 105, 105, 105, 104, 104, 102, 104, 105, 104, 104, 104, 105, 0, 0, 103, 102, 103, 104, 0], [0, 103, 104, 104, 104, 104, 105, 104, 0, 0, 0, 0, 105, 105, 105, 104, 104, 104, 104, 105, 105, 104, 104, 103, 104, 104, 105, 104, 105, 104, 104, 104, 105, 104, 104, 105, 103, 104, 104, 104, 105, 105, 105, 104, 104, 104, 0, 0, 0, 0, 104, 104, 104, 105, 104, 104, 105, 105, 104, 105, 105, 105, 105, 103, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 104, 105, 105, 104, 105, 105, 105, 105, 105, 105, 104, 105, 104, 104, 104, 104, 104, 105, 0, 0, 0, 0, 104, 104, 0, 0], [0, 104, 104, 105, 105, 103, 104, 104, 0, 0, 0, 0, 104, 104, 103, 104, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 104, 104, 105, 105, 105, 104, 103, 105, 104, 105, 105, 104, 104, 103, 104, 0, 0, 0, 0, 104, 103, 104, 105, 105, 104, 105, 105, 105, 105, 103, 105, 104, 103, 105, 104, 104, 105, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 103, 105, 104, 104, 105, 105, 105, 105, 104, 104, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0], [0, 104, 104, 105, 104, 104, 104, 104, 104, 0, 0, 104, 104, 102, 104, 105, 104, 105, 105, 103, 104, 104, 104, 105, 104, 104, 104, 104, 105, 104, 104, 104, 103, 104, 105, 105, 105, 104, 105, 104, 104, 104, 104, 102, 104, 104, 0, 0, 0, 0, 105, 105, 104, 104, 105, 105, 104, 104, 105, 105, 105, 104, 103, 104, 104, 104, 104, 104, 105, 104, 105, 103, 104, 105, 105, 104, 105, 105, 104, 104, 104, 104, 104, 105, 105, 105, 104, 105, 105, 105, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0], [0, 104, 105, 105, 105, 104, 104, 104, 104, 103, 103, 104, 104, 104, 103, 104, 104, 105, 104, 104, 104, 104, 104, 104, 103, 103, 104, 104, 104, 103, 104, 104, 105, 105, 105, 105, 105, 106, 105, 104, 105, 105, 103, 104, 104, 104, 104, 0, 0, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 103, 102, 104, 105, 104, 104, 104, 104, 103, 104, 104, 105, 104, 105, 104, 104, 105, 105, 103, 104, 105, 104, 104, 105, 104, 105, 105, 105, 105, 104, 103, 105, 0, 0, 0, 0, 0, 0], [0, 104, 105, 104, 105, 104, 103, 104, 105, 104, 104, 103, 105, 104, 103, 104, 105, 105, 104, 104, 104, 104, 104, 104, 103, 104, 105, 104, 104, 104, 104, 104, 103, 103, 105, 105, 105, 105, 0, 105, 105, 103, 104, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 104, 103, 104, 104, 105, 104, 104, 104, 104, 103, 103, 104, 104, 104, 104, 104, 104, 104, 104, 105, 104, 104, 105, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 104, 105, 105, 104, 105, 104, 105, 105, 0, 0, 0, 0, 0], [0, 104, 104, 105, 105, 104, 104, 105, 105, 104, 103, 104, 105, 103, 103, 104, 105, 105, 104, 104, 104, 104, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 105, 104, 105, 104, 0, 0, 0, 104, 105, 105, 105, 105, 105, 104, 104, 105, 105, 105, 105, 104, 105, 104, 105, 104, 104, 104, 104, 105, 104, 105, 104, 103, 104, 105, 104, 102, 104, 104, 106, 105, 105, 105, 104, 105, 105, 105, 105, 104, 104, 105, 105, 105, 105, 104, 105, 104, 105, 105, 104, 105, 105, 105, 0, 0, 0, 0, 0], [0, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 105, 105, 105, 105, 105, 105, 104, 104, 104, 103, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 104, 0, 0, 0, 104, 105, 105, 105, 104, 104, 104, 105, 105, 105, 105, 104, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 105, 104, 103, 104, 103, 104, 104, 105, 102, 104, 105, 103, 105, 104, 104, 105, 104, 105, 104, 104, 104, 105, 105, 104, 105, 104, 104, 105, 105, 105, 106, 105, 0, 0, 0, 0, 0], [0, 0, 105, 105, 105, 104, 104, 105, 105, 105, 104, 104, 104, 104, 105, 105, 104, 105, 104, 104, 105, 106, 105, 104, 105, 105, 105, 105, 105, 103, 105, 104, 105, 104, 104, 104, 105, 104, 0, 0, 0, 104, 105, 104, 104, 105, 105, 105, 105, 104, 104, 104, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 105, 104, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 103, 105, 104, 104, 104, 104, 104, 105, 105, 105, 105, 104, 104, 104, 105, 106, 105, 106, 105, 0, 0, 0, 0], [0, 0, 104, 104, 105, 105, 104, 105, 104, 104, 105, 104, 103, 104, 105, 104, 104, 105, 104, 104, 105, 105, 106, 105, 105, 105, 105, 105, 103, 103, 104, 105, 104, 104, 104, 104, 104, 105, 104, 0, 0, 0, 0, 0, 104, 105, 105, 105, 105, 104, 104, 105, 105, 104, 104, 105, 104, 104, 104, 104, 104, 104, 105, 104, 102, 104, 105, 105, 104, 105, 104, 104, 105, 105, 105, 104, 105, 104, 105, 104, 104, 104, 105, 105, 104, 104, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 105, 0, 0, 0], [0, 0, 104, 104, 104, 105, 105, 104, 104, 103, 104, 104, 105, 105, 104, 104, 104, 104, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 103, 104, 105, 0, 0, 0, 104, 104, 105, 105, 105, 105, 0, 0, 0, 0, 0, 105, 104, 105, 105, 105, 105, 104, 104, 104, 102, 105, 105, 104, 105, 104, 105, 104, 105, 104, 105, 105, 105, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 104, 105, 104, 105, 104, 104, 105, 105, 103, 104, 105, 105, 105, 105, 105, 104, 105, 105, 105, 104, 104, 0, 0], [0, 0, 104, 105, 104, 104, 105, 105, 104, 104, 103, 104, 105, 105, 105, 104, 104, 104, 105, 103, 105, 104, 104, 104, 104, 105, 105, 104, 104, 104, 0, 0, 0, 0, 0, 105, 105, 105, 105, 104, 103, 0, 0, 0, 0, 105, 104, 105, 104, 105, 105, 104, 104, 105, 105, 105, 105, 105, 105, 104, 105, 103, 104, 105, 103, 105, 105, 104, 104, 104, 105, 105, 105, 105, 104, 104, 104, 105, 105, 105, 104, 104, 104, 105, 104, 105, 105, 106, 105, 105, 105, 104, 104, 104, 104, 104, 105, 103, 0, 0], [0, 0, 105, 104, 104, 105, 104, 105, 105, 104, 104, 104, 105, 104, 104, 104, 104, 105, 104, 104, 104, 103, 104, 0, 0, 0, 105, 104, 104, 0, 0, 0, 0, 0, 0, 0, 105, 104, 103, 104, 104, 0, 0, 0, 0, 0, 104, 105, 104, 104, 103, 105, 104, 104, 104, 105, 105, 104, 104, 104, 103, 104, 104, 104, 105, 105, 104, 104, 104, 0, 0, 104, 104, 104, 104, 104, 104, 105, 105, 105, 105, 104, 104, 104, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 105, 103, 103, 0], [0, 0, 0, 105, 104, 104, 105, 104, 104, 104, 105, 105, 105, 104, 102, 104, 105, 104, 104, 104, 103, 103, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 104, 104, 105, 0, 0, 0, 0, 105, 105, 105, 105, 104, 103, 104, 103, 104, 104, 105, 104, 103, 102, 103, 104, 103, 104, 104, 105, 104, 104, 0, 0, 0, 0, 104, 104, 105, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 105, 105, 105, 104, 104, 105, 104, 105, 105, 104, 104, 104, 0], [0, 0, 0, 0, 0, 105, 104, 104, 104, 104, 104, 105, 105, 103, 104, 104, 105, 105, 105, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 105, 104, 105, 104, 105, 105, 0, 105, 105, 104, 104, 105, 105, 104, 102, 104, 104, 104, 103, 104, 104, 103, 103, 104, 105, 104, 105, 104, 104, 104, 0, 0, 0, 0, 104, 104, 104, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 103, 104, 105, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 0, 0], [0, 0, 0, 0, 0, 0, 105, 104, 105, 104, 104, 104, 105, 103, 104, 104, 104, 105, 103, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 104, 104, 105, 105, 105, 103, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 0, 0, 105, 104, 104, 0, 0, 0, 0, 105, 104, 104, 105, 105, 104, 105, 104, 105, 104, 104, 103, 103, 104, 105, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 104, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 104, 105, 104, 104, 104, 104, 104, 104, 105, 105, 105, 105, 105, 104, 104, 104, 104, 105, 104, 104, 104, 104, 104, 0, 0, 0, 0, 104, 104, 104, 0, 0, 105, 104, 104, 104, 104, 105, 105, 104, 104, 105, 104, 104, 104, 104, 103, 104, 105, 105, 105, 105, 104, 104, 104, 104, 103, 104, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 105, 105, 105, 103, 104, 104, 104, 104, 104, 104, 105, 106, 106, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 104, 103, 104, 104, 105, 104, 105, 104, 105, 104, 103, 105, 104, 104, 103, 105, 105, 104, 103, 104, 104, 104, 103, 104, 103, 105, 105, 0, 0, 0, 0, 104, 104, 104, 105, 104, 104, 104, 104, 104, 103, 105, 104, 105, 104, 104, 105, 104, 104, 103, 103, 102, 104, 105, 104, 105, 105, 104, 104, 104, 104, 104, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 103, 104, 104, 103, 104, 104, 105, 104, 105, 104, 104, 104, 105, 105, 0, 0, 0, 0, 0, 0, 0, 104, 105, 105, 105, 105, 105, 104, 103, 104, 104, 104, 104, 104, 104, 104, 103, 104, 104, 105, 106, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 105, 0, 0, 104, 104, 104, 104, 104, 104, 104, 104, 104, 103, 104, 104, 104, 104, 105, 104, 105, 104, 104, 104, 104, 104, 105, 106, 105, 105, 105, 104, 104, 104, 104, 104, 104, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 104, 105, 104, 104, 105, 105, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 103, 104, 105, 105, 104, 104, 105, 104, 104, 104, 103, 105, 104, 104, 105, 104, 104, 105, 105, 105, 104, 105, 104, 104, 104, 105, 106, 105, 104, 105, 104, 104, 105, 105, 105, 104, 105, 105, 104, 104, 104, 104, 103, 103, 104, 104, 104, 104, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 104, 104, 103, 104, 104, 104, 104, 104, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 103, 105, 105, 104, 104, 105, 104, 103, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 104, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 103, 0, 0, 105, 104, 105, 105, 105, 105, 103, 104, 105, 104, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 104, 104, 105, 105, 103, 105, 105, 104, 104, 104, 104, 105, 105, 103, 104, 0, 0], [0, 0, 0, 0, 0, 0, 0, 105, 103, 104, 105, 104, 105, 105, 105, 105, 104, 104, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 105, 104, 104, 105, 104, 105, 105, 104, 105, 105, 104, 104, 104, 105, 105, 0, 0, 0, 0, 104, 105, 104, 105, 105, 104, 105, 104, 105, 105, 105, 105, 102, 103, 105, 104, 105, 105, 105, 105, 104, 104, 105, 105, 105, 104, 104, 105, 104, 104, 105, 105, 105, 105, 104, 104, 104, 104, 104, 104, 105, 105, 104, 103, 0], [0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 105, 106, 104, 105, 105, 104, 105, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 104, 105, 105, 105, 105, 105, 104, 105, 104, 104, 104, 104, 103, 104, 103, 0, 0, 0, 0, 104, 104, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 103, 104, 103, 105, 105, 105, 105, 105, 105, 104, 105, 105, 105, 105, 105, 104, 104, 104, 103, 104, 104, 104, 104, 105, 104, 105, 105, 105, 103, 104, 105, 104, 0], [0, 0, 0, 0, 0, 0, 104, 103, 104, 105, 105, 104, 104, 105, 104, 104, 104, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 105, 105, 104, 105, 105, 104, 104, 104, 104, 104, 105, 105, 105, 104, 104, 104, 103, 103, 0, 0, 104, 104, 104, 105, 105, 105, 104, 104, 104, 105, 105, 105, 104, 104, 104, 104, 105, 105, 105, 105, 104, 104, 104, 105, 105, 104, 105, 105, 105, 104, 104, 104, 104, 105, 105, 105, 104, 104, 105, 104, 103, 103, 103, 104, 105, 0], [0, 0, 0, 0, 0, 0, 103, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 104, 104, 105, 104, 105, 104, 105, 104, 104, 104, 104, 104, 104, 105, 105, 103, 104, 104, 104, 104, 105, 104, 105, 104, 105, 105, 104, 105, 105, 103, 103, 104, 104, 104, 104, 105, 105, 104, 105, 103, 104, 104, 104, 104, 105, 106, 104, 103, 104, 104, 104, 103, 103, 104, 105, 0], [0, 0, 0, 0, 0, 0, 105, 103, 105, 105, 105, 104, 103, 104, 104, 105, 104, 105, 105, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 104, 104, 104, 103, 104, 104, 104, 104, 104, 103, 104, 105, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 105, 105, 105, 105, 105, 104, 104, 103, 103, 104, 105, 105, 104, 104, 104, 105, 104, 104, 104, 104, 105, 104, 103, 105, 104, 104, 104, 105, 104, 0], [0, 0, 0, 0, 0, 0, 105, 104, 105, 105, 103, 104, 104, 104, 104, 105, 105, 104, 104, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 105, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 104, 105, 105, 104, 104, 104, 104, 104, 105, 105, 104, 104, 105, 103, 104, 104, 104, 104, 104, 104, 103, 105, 104, 105, 105, 105, 105, 105, 104, 103, 105, 105, 105, 105, 103, 104, 104, 104, 103, 104, 103, 105, 105, 105, 104, 105, 104, 104, 105, 104, 104, 0], [0, 0, 0, 0, 0, 0, 105, 104, 104, 104, 105, 105, 105, 104, 105, 104, 104, 103, 103, 104, 105, 0, 0, 0, 0, 0, 0, 0, 104, 105, 105, 103, 104, 103, 103, 104, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 104, 105, 104, 104, 105, 105, 104, 105, 105, 105, 105, 104, 104, 104, 104, 103, 104, 104, 104, 104, 104, 104, 105, 105, 105, 105, 104, 105, 104, 103, 103, 103, 104, 105, 105, 105, 104, 104, 104, 104, 105, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 0, 0], [0, 0, 0, 0, 0, 0, 104, 104, 103, 104, 105, 105, 105, 104, 105, 104, 104, 103, 104, 104, 104, 0, 0, 0, 0, 0, 0, 105, 105, 104, 105, 104, 103, 104, 104, 103, 105, 105, 105, 104, 104, 103, 104, 104, 104, 105, 105, 105, 104, 105, 105, 105, 105, 105, 104, 103, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 104, 105, 105, 105, 104, 105, 103, 104, 104, 104, 105, 105, 105, 104, 104, 105, 105, 0, 0, 105, 105, 105, 103, 104, 104, 105, 104, 0, 0], [0, 0, 0, 0, 0, 105, 105, 103, 104, 105, 105, 105, 105, 105, 104, 103, 103, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 105, 104, 104, 104, 104, 105, 105, 104, 104, 104, 103, 103, 105, 104, 105, 105, 105, 105, 104, 105, 105, 104, 105, 105, 104, 104, 104, 104, 103, 103, 104, 105, 103, 104, 104, 104, 104, 104, 104, 105, 104, 105, 103, 105, 105, 105, 105, 104, 104, 105, 103, 104, 105, 105, 105, 105, 105, 0, 0, 0, 0, 105, 105, 103, 103, 105, 104, 104, 104, 0], [0, 0, 0, 0, 104, 105, 105, 105, 106, 105, 105, 105, 103, 104, 104, 103, 105, 105, 105, 104, 0, 0, 0, 0, 0, 0, 0, 105, 104, 105, 105, 105, 105, 104, 105, 105, 105, 104, 104, 104, 103, 104, 105, 105, 105, 105, 105, 105, 104, 104, 105, 105, 105, 104, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 105, 105, 104, 104, 105, 104, 104, 104, 104, 105, 104, 103, 104, 103, 104, 104, 105, 105, 105, 105, 104, 0, 0, 0, 0, 105, 105, 103, 104, 104, 104, 104, 104, 0], [0, 0, 0, 0, 104, 105, 105, 104, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 105, 105, 104, 106, 104, 105, 105, 104, 105, 104, 104, 104, 105, 105, 105, 105, 105, 104, 104, 104, 104, 105, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 105, 105, 103, 104, 103, 104, 104, 103, 104, 103, 104, 105, 0, 0, 0, 104, 104, 103, 104, 105, 104, 104, 104, 0], [0, 0, 0, 0, 0, 105, 103, 105, 104, 105, 104, 104, 104, 105, 105, 105, 105, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 105, 105, 105, 104, 104, 104, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 104, 105, 104, 104, 104, 105, 104, 104, 105, 105, 105, 105, 104, 105, 103, 104, 104, 104, 105, 104, 105, 104, 103, 104, 104, 102, 103, 103, 104, 105, 0, 0, 0, 0, 103, 104, 104, 105, 105, 104, 103, 0], [0, 0, 0, 0, 0, 104, 104, 104, 104, 105, 104, 104, 105, 105, 105, 105, 104, 103, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 104, 102, 105, 105, 103, 105, 105, 105, 105, 105, 105, 104, 104, 105, 105, 104, 105, 105, 104, 104, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 105, 105, 105, 105, 104, 105, 104, 105, 104, 106, 105, 104, 105, 104, 104, 105, 104, 105, 104, 105, 104, 104, 103, 104, 105, 0, 0, 0, 0, 0, 104, 104, 104, 105, 105, 104, 0], [0, 0, 0, 0, 0, 105, 105, 104, 105, 105, 104, 105, 105, 104, 103, 105, 104, 104, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 103, 104, 105, 104, 105, 104, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 106, 105, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 101, 104, 104, 105, 103, 104, 104, 104, 104, 104, 105, 106, 106, 104, 105, 104, 104, 104, 104, 105, 105, 105, 104, 104, 104, 105, 105, 104, 0, 0, 0, 0, 0, 104, 105, 105, 105, 102, 0], [0, 0, 0, 0, 0, 0, 104, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 104, 105, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 105, 104, 104, 104, 104, 105, 105, 104, 104, 0, 0, 0, 106, 104, 104, 105, 104, 105, 105, 104, 104, 105, 105, 104, 105, 105, 105, 105, 104, 105, 104, 103, 104, 104, 103, 104, 105, 104, 104, 105, 105, 103, 105, 104, 104, 105, 105, 105, 105, 105, 105, 104, 105, 104, 104, 0, 0, 0, 0, 0, 104, 106, 105, 105, 0, 0], [0, 0, 0, 0, 0, 0, 104, 104, 105, 104, 105, 105, 106, 105, 105, 105, 105, 105, 103, 105, 104, 103, 0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 105, 105, 104, 104, 0, 0, 105, 104, 106, 105, 0, 0, 0, 0, 0, 104, 104, 105, 104, 103, 105, 105, 104, 104, 105, 105, 105, 105, 104, 104, 105, 105, 105, 104, 105, 104, 104, 103, 105, 105, 105, 105, 102, 104, 104, 104, 104, 104, 104, 105, 104, 104, 105, 105, 104, 104, 104, 104, 0, 0, 0, 0, 104, 104, 105, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 105, 105, 104, 103, 104, 105, 105, 105, 105, 104, 103, 105, 105, 104, 103, 104, 0, 0, 0, 0, 0, 0, 105, 104, 105, 105, 105, 103, 0, 0, 0, 0, 105, 104, 105, 0, 0, 0, 0, 0, 104, 104, 104, 105, 103, 103, 105, 104, 104, 105, 105, 104, 105, 105, 104, 105, 105, 105, 104, 105, 105, 104, 105, 105, 105, 105, 105, 105, 103, 104, 104, 0, 104, 105, 104, 104, 104, 105, 105, 104, 105, 105, 105, 104, 0, 0, 104, 105, 104, 104, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 104, 106, 105, 105, 106, 105, 105, 105, 104, 104, 103, 104, 103, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 0, 0, 0, 0, 105, 104, 104, 105, 0, 0, 0, 0, 103, 105, 105, 105, 105, 105, 104, 104, 104, 105, 105, 104, 104, 104, 104, 104, 104, 104, 104, 105, 105, 105, 106, 105, 104, 105, 105, 105, 105, 104, 0, 0, 0, 104, 104, 103, 104, 105, 105, 105, 105, 103, 104, 104, 104, 105, 105, 104, 105, 105, 104, 0, 0], [0, 0, 0, 0, 0, 0, 105, 104, 104, 103, 105, 105, 105, 104, 105, 105, 104, 105, 104, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 105, 104, 105, 0, 0, 105, 104, 104, 105, 105, 103, 0, 0, 104, 104, 105, 104, 105, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 105, 104, 104, 104, 104, 105, 104, 104, 104, 104, 103, 105, 105, 105, 104, 105, 0, 0, 0, 105, 105, 105, 104, 104, 106, 105, 102, 103, 104, 104, 105, 105, 104, 104, 105, 105, 105, 0, 0], [0, 0, 0, 104, 104, 105, 104, 105, 104, 104, 105, 105, 104, 104, 104, 104, 104, 104, 105, 105, 104, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 105, 104, 105, 105, 105, 105, 105, 105, 105, 104, 105, 104, 104, 104, 104, 104, 104, 102, 105, 105, 104, 105, 105, 104, 105, 104, 106, 104, 105, 105, 105, 105, 104, 104, 105, 104, 103, 104, 104, 105, 105, 105, 104, 104, 0, 0, 104, 105, 105, 104, 104, 104, 104, 105, 103, 103, 104, 104, 104, 103, 105, 104, 105, 105, 104, 104, 0], [0, 0, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 103, 104, 104, 104, 105, 104, 105, 104, 104, 104, 104, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 104, 105, 105, 104, 104, 105, 105, 105, 104, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 105, 104, 105, 105, 105, 104, 105, 105, 105, 105, 104, 103, 103, 104, 104, 105, 104, 103, 104, 104, 105, 104, 104, 103, 0, 0, 104, 103, 104, 105, 105, 104, 104, 105, 105, 104, 104, 104, 104, 103, 104, 105, 105, 105, 105, 103, 0], [0, 0, 104, 104, 105, 105, 105, 105, 105, 104, 105, 105, 105, 105, 105, 104, 104, 104, 105, 105, 105, 103, 105, 104, 104, 104, 104, 104, 0, 0, 0, 105, 105, 105, 105, 104, 105, 104, 104, 104, 105, 105, 105, 103, 105, 104, 105, 104, 104, 104, 104, 104, 105, 105, 105, 103, 104, 103, 105, 105, 104, 103, 104, 105, 104, 104, 105, 104, 105, 105, 105, 105, 105, 104, 104, 104, 104, 0, 0, 105, 105, 105, 104, 104, 104, 105, 104, 105, 105, 104, 104, 104, 105, 104, 104, 104, 105, 104, 105, 0], [0, 0, 104, 105, 105, 105, 104, 104, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 105, 105, 104, 105, 105, 104, 104, 104, 103, 105, 104, 103, 104, 104, 105, 105, 104, 105, 105, 105, 104, 105, 105, 104, 105, 103, 104, 104, 105, 104, 104, 105, 105, 105, 105, 105, 103, 105, 104, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 105, 105, 106, 105, 105, 104, 102, 105, 105, 105, 105, 104, 105, 106, 104, 104, 103, 105, 105, 104, 104, 105, 104, 104, 103, 105, 105, 104, 104, 105, 104, 104, 0], [0, 0, 104, 104, 104, 104, 104, 104, 104, 104, 104, 102, 105, 104, 104, 104, 104, 105, 105, 104, 104, 104, 105, 105, 104, 105, 104, 104, 104, 104, 105, 104, 104, 105, 105, 105, 104, 104, 105, 105, 104, 104, 104, 105, 105, 104, 104, 104, 104, 103, 105, 104, 105, 104, 104, 104, 105, 104, 105, 105, 105, 105, 105, 104, 105, 105, 105, 105, 103, 105, 105, 104, 103, 104, 105, 104, 105, 105, 106, 104, 105, 103, 103, 104, 104, 105, 105, 105, 105, 104, 104, 105, 104, 105, 104, 104, 105, 105, 105, 0], [0, 0, 0, 104, 105, 103, 104, 104, 104, 104, 104, 103, 105, 104, 104, 105, 104, 104, 105, 105, 104, 105, 104, 104, 104, 105, 105, 105, 104, 104, 105, 105, 106, 104, 104, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 105, 104, 104, 104, 104, 104, 105, 104, 104, 104, 104, 105, 105, 105, 105, 105, 104, 105, 104, 104, 105, 104, 105, 105, 105, 104, 105, 105, 104, 104, 105, 105, 105, 106, 105, 104, 104, 105, 104, 105, 104, 104, 103, 104, 104, 105, 105, 105, 105, 104, 105, 105, 106, 0, 0], [0, 0, 0, 103, 103, 104, 104, 105, 104, 103, 104, 105, 105, 104, 104, 104, 103, 104, 105, 105, 105, 105, 105, 105, 104, 104, 105, 105, 104, 104, 104, 105, 105, 104, 105, 105, 105, 105, 0, 0, 0, 103, 104, 105, 105, 105, 105, 105, 103, 104, 104, 105, 104, 104, 104, 104, 104, 105, 105, 105, 106, 105, 105, 105, 105, 104, 105, 105, 103, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 105, 105, 105, 105, 105, 104, 105, 105, 105, 105, 104, 105, 105, 104, 104, 105, 105, 0, 0], [0, 0, 0, 104, 102, 103, 105, 104, 103, 104, 104, 105, 104, 104, 104, 0, 104, 104, 105, 105, 105, 104, 104, 104, 103, 104, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 105, 0, 0, 0, 0, 0, 103, 105, 105, 104, 105, 105, 103, 103, 104, 105, 105, 103, 104, 105, 105, 105, 105, 105, 105, 106, 105, 105, 105, 104, 105, 104, 105, 104, 104, 105, 105, 105, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 104, 104, 105, 105, 105, 105, 104, 104, 104, 104, 104, 104, 104, 105, 0, 0], [0, 0, 0, 104, 104, 103, 104, 105, 105, 105, 104, 104, 105, 106, 0, 0, 0, 104, 104, 104, 105, 104, 104, 104, 103, 104, 103, 104, 104, 105, 105, 104, 104, 105, 104, 105, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 105, 105, 104, 103, 104, 104, 103, 103, 104, 105, 105, 105, 104, 105, 105, 106, 104, 104, 103, 104, 104, 104, 105, 104, 105, 104, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 105, 104, 105, 104, 105, 105, 105, 105, 105, 105, 104, 104, 105, 105, 0, 0, 0], [0, 0, 0, 104, 104, 104, 104, 105, 105, 105, 104, 105, 104, 104, 0, 0, 0, 103, 104, 104, 104, 105, 105, 104, 104, 104, 104, 104, 103, 104, 104, 104, 104, 104, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 105, 104, 104, 105, 105, 105, 105, 104, 104, 104, 104, 104, 105, 104, 105, 104, 104, 104, 103, 105, 104, 103, 104, 104, 105, 105, 105, 104, 104, 105, 105, 104, 104, 104, 103, 105, 105, 105, 104, 105, 105, 104, 105, 104, 104, 105, 105, 104, 104, 104, 105, 105, 105, 0, 0, 0], [0, 0, 0, 104, 104, 104, 104, 105, 104, 104, 105, 104, 103, 103, 0, 0, 0, 105, 104, 105, 105, 105, 105, 104, 104, 104, 104, 103, 103, 104, 104, 104, 105, 105, 105, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105, 105, 105, 104, 105, 105, 104, 104, 104, 104, 105, 103, 104, 105, 105, 104, 104, 105, 104, 105, 104, 104, 105, 105, 104, 104, 104, 103, 104, 104, 104, 103, 104, 104, 104, 104, 105, 105, 104, 104, 104, 104, 105, 0, 105, 105, 104, 105, 104, 105, 104, 103, 0, 0, 0], [0, 0, 0, 0, 105, 104, 105, 0, 0, 0, 104, 103, 104, 0, 0, 0, 0, 0, 106, 105, 104, 105, 0, 0, 104, 105, 104, 104, 104, 105, 104, 0, 0, 104, 105, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 104, 105, 104, 104, 104, 104, 104, 104, 104, 104, 104, 104, 105, 104, 104, 104, 103, 104, 0, 0, 105, 104, 105, 104, 104, 103, 104, 102, 104, 104, 104, 103, 105, 105, 106, 104, 104, 103, 104, 0, 0, 0, 104, 105, 105, 105, 103, 103, 104, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 105, 0, 0, 0, 0, 105, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 103, 103, 102, 104, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 104, 104, 0, 0, 0, 0, 104, 104, 104, 103, 105, 105, 104, 104, 103, 104, 0, 0, 0, 0, 0, 105, 105, 105, 104, 104, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]]}," +
                     "{DataType : 4, ID : 0, playerID : 2, playersData : [{ID : 1, Team : 1, x : 50, y : 50, King : 1},{ID : 2, Team : 2, x : 53, y : 53, King : 1},{ID : 3, Team : 1, x : 54, y : 54, King : 0}]}]");
    }

    #endregion
}