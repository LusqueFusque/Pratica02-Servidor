using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using System.Threading;

public class UDPChatSystem : MonoBehaviour
{
    [Header("Configurações de Rede")]
    public bool isServer = false;
    public string serverIP = "192.168.1.100";
    public int chatPort = 5556;
    
    [Header("UI do Chat - TextMeshPro")]
    public TMP_InputField chatInput;
    public TextMeshProUGUI chatDisplay;
    public ScrollRect scrollRect;
    public Button sendButton;
    public int maxMessages = 50;
    
    [Header("Configurações do Jogador")]
    public string playerName = "Jogador";
    
    // Rede
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private List<IPEndPoint> connectedClients = new List<IPEndPoint>();
    private Thread receiveThread;
    private volatile bool isRunning = false;
    
    // Mensagens
    private List<string> chatMessages = new List<string>();
    private Queue<string> messageQueue = new Queue<string>();
    
    void Start()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendChatMessage);
        }
        
        // Limpa o display no início
        if (chatDisplay != null)
        {
            chatDisplay.text = "";
        }
        
        InitializeChat();
    }
    
    void InitializeChat()
    {
        try
        {
            isRunning = true;
            
            if (isServer)
            {
                udpClient = new UdpClient(chatPort);
                remoteEndPoint = new IPEndPoint(IPAddress.Any, chatPort);
            }
            else
            {
                udpClient = new UdpClient();
                udpClient.Client.ReceiveTimeout = 1000;
                remoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), chatPort);
                SendConnectionMessage();
            }
            
            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ERRO ao iniciar chat: {e.Message}");
        }
    }
    
    void ReceiveData()
    {
        while (isRunning)
        {
            try
            {
                if (udpClient == null || udpClient.Client == null)
                    break;
                
                IPEndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] receivedData = udpClient.Receive(ref senderEndPoint);
                
                if (receivedData != null && receivedData.Length > 0)
                {
                    string message = Encoding.UTF8.GetString(receivedData);
                    
                    lock (messageQueue)
                    {
                        messageQueue.Enqueue($"{senderEndPoint.Address}:{senderEndPoint.Port}|{message}");
                    }
                }
            }
            catch (SocketException)
            {
                // Timeout normal, continua
            }
            catch (System.Exception)
            {
                if (isRunning)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
    
    void SendConnectionMessage()
    {
        try
        {
            string connectMsg = $"CONNECT|{playerName}";
            byte[] data = Encoding.UTF8.GetBytes(connectMsg);
            udpClient.Send(data, data.Length, remoteEndPoint);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao conectar: {e.Message}");
        }
    }
    
    void Update()
    {
        lock (messageQueue)
        {
            while (messageQueue.Count > 0)
            {
                string data = messageQueue.Dequeue();
                ProcessMessage(data);
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (chatInput != null && chatInput.isFocused)
            {
                SendChatMessage();
            }
        }
    }
    
    void ProcessMessage(string data)
    {
        string[] parts = data.Split('|');
        
        if (parts.Length < 2) return;
        
        string[] addressParts = parts[0].Split(':');
        IPAddress senderIP = IPAddress.Parse(addressParts[0]);
        int senderPort = int.Parse(addressParts[1]);
        
        string messageType = parts[1];
        
        if (messageType == "CONNECT" && parts.Length >= 3)
        {
            HandleConnection(senderIP, senderPort, parts[2]);
        }
        else if (messageType == "CHAT" && parts.Length >= 4)
        {
            HandleChatMessage(parts[2], parts[3]);
        }
        else if (messageType == "DISCONNECT" && parts.Length >= 3)
        {
            HandleDisconnection(parts[2]);
        }
    }
    
    void HandleConnection(IPAddress ip, int port, string name)
    {
        if (isServer)
        {
            IPEndPoint newClient = new IPEndPoint(ip, port);
            
            if (!IsClientConnected(newClient))
            {
                connectedClients.Add(newClient);
                BroadcastMessage($"CHAT|SISTEMA|{name} entrou no chat", newClient);
            }
        }
    }
    
    void HandleChatMessage(string sender, string message)
    {
        AddChatMessage(sender, message);
        
        if (isServer)
        {
            BroadcastMessage($"CHAT|{sender}|{message}");
        }
    }
    
    void HandleDisconnection(string name)
    {
        // Apenas processa, não exibe nada
    }
    
    bool IsClientConnected(IPEndPoint client)
    {
        foreach (IPEndPoint c in connectedClients)
        {
            if (c.Address.Equals(client.Address) && c.Port == client.Port)
                return true;
        }
        return false;
    }
    
    public void SendChatMessage()
    {
        if (chatInput == null) return;
        
        string message = chatInput.text.Trim();
        
        if (string.IsNullOrEmpty(message)) return;
        
        AddChatMessage(playerName, message);
        
        string networkMessage = $"CHAT|{playerName}|{message}";
        byte[] data = Encoding.UTF8.GetBytes(networkMessage);
        
        try
        {
            if (isServer)
            {
                BroadcastMessage(networkMessage);
            }
            else
            {
                udpClient.Send(data, data.Length, remoteEndPoint);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Erro ao enviar: {e.Message}");
        }
        
        chatInput.text = "";
        chatInput.ActivateInputField();
    }
    
    void BroadcastMessage(string message, IPEndPoint exclude = null)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        
        foreach (IPEndPoint client in connectedClients)
        {
            if (exclude != null && client.Address.Equals(exclude.Address) && client.Port == exclude.Port)
                continue;
            
            try
            {
                udpClient.Send(data, data.Length, client);
            }
            catch { }
        }
    }
    
    void AddChatMessage(string sender, string message)
    {
        string formattedMessage = $"<color=#00FFFF>{sender}:</color> {message}";
        AddMessageToDisplay(formattedMessage);
    }
    
    void AddSystemMessage(string message)
    {
        string formattedMessage = $"<color=#FFFF00>[Sistema]</color> {message}";
        AddMessageToDisplay(formattedMessage);
    }
    
    void AddMessageToDisplay(string message)
    {
        chatMessages.Add(message);
        
        if (chatMessages.Count > maxMessages)
        {
            chatMessages.RemoveAt(0);
        }
        
        UpdateChatDisplay();
    }
    
    void UpdateChatDisplay()
    {
        if (chatDisplay != null)
        {
            chatDisplay.text = string.Join("\n", chatMessages.ToArray());
            
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
    
    void OnApplicationQuit()
    {
        CloseConnection();
    }
    
    void OnDisable()
    {
        CloseConnection();
    }
    
    void OnDestroy()
    {
        CloseConnection();
    }
    
    void CloseConnection()
    {
        if (!isRunning) return;
        
        isRunning = false;
        
        try
        {
            string disconnectMsg = $"DISCONNECT|{playerName}";
            byte[] data = Encoding.UTF8.GetBytes(disconnectMsg);
            
            if (isServer)
            {
                BroadcastMessage(disconnectMsg);
            }
            else if (udpClient != null)
            {
                udpClient.Send(data, data.Length, remoteEndPoint);
            }
        }
        catch { }
        
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch { }
            udpClient = null;
        }
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }
    }
}