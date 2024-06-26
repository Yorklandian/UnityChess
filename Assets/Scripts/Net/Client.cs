using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using Unity.VisualScripting;
using UnityEngine;

public class Client : MonoBehaviour
{
    #region Singleton implementation
    public static Client Instance { set; get; }
    private void Awake()
    {
        Instance = this;
    }
    #endregion
    
    public NetworkDriver driver;
    private NetworkConnection connection;

    private bool isActive = false;
    private const float keepAliveTickRate = 20.0f;
    private float lastKeepAlive;

    public Action connectionDropped;
    
    
    // methods
    public void Init(string ip, ushort port)
    {
        driver = NetworkDriver.Create();
        NetworkEndPoint endPoint = NetworkEndPoint.Parse(ip, port);

        connection = driver.Connect(endPoint);
        Debug.Log("Attempting to connect to Server on " + endPoint.Address);
        isActive = true;
        RegisterToEvent();
    }

    public void ShutDown()
    {
        if (isActive)
        {
            UnregisterToEvent();
            driver.Dispose();
            isActive = false;
            connection = default(NetworkConnection);
        }
    }

    private void OnDestroy()
    {
        ShutDown();
    }
    
    
    private void Update()
    {
        if (!isActive)
        {
            return;
        }
        
        driver.ScheduleUpdate().Complete();
        CheckAlive();
        UpdateMessagePump();
        
    }

    private void CheckAlive()
    {
        if (!connection.IsCreated && isActive)
        {
            Debug.Log("Something went wrong, lost connection to server");
            connectionDropped?.Invoke();
            ShutDown();
        }   
    }

    private void UpdateMessagePump()
    {
        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = connection.PopEvent(driver, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                SendToServer(new NetWelcome());
                Debug.Log("We are connected!");
            }
            else if(cmd == NetworkEvent.Type.Data)
            {
                NetUtility.OnData(stream, default(NetworkConnection));
            }
            else if(cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("client got disconnected from server");
                connection = default(NetworkConnection);
                connectionDropped?.Invoke();
                ShutDown();
            }
        }
    }

    public void SendToServer(NetMessage msg)
    {
        DataStreamWriter writer;
        driver.BeginSend(connection, out writer);
        msg.Serialize(ref writer);
        driver.EndSend(writer);
    }
    
    // Event parsing
    private void RegisterToEvent()
    {
        NetUtility.C_KEEP_ALIVE += OnKeepAlive;
    }

    private void UnregisterToEvent()
    {
        NetUtility.C_KEEP_ALIVE -= OnKeepAlive;
    }

    private void OnKeepAlive(NetMessage nm)
    {
        SendToServer(nm);
    }
}
