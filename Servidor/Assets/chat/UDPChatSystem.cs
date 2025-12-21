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
    public string serverIP = "192.168.1.100"; // IP do servidor
    public int chatPort = 5556; // Porta diferente do jogo
    
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
    private bool isRunning = false;
    
    // Mensagens
    private List<string> chatMessages = new List<string>();
    private Queue<string> messageQueue = new Queue<string>();
    
    void Start()
    {
        // Conecta o botão ao método de enviar
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendChatMessage);
        }
        
        InitializeChat();
        AddSystemMessage("=== CHAT INICIADO ===");
        
        if (isServer)
        {
            AddSystemMessage("Aguardando jogadores...");
        }
        else
        {
            AddSystemMessage($"Conectando ao servidor {serverIP}...");
        }
    }
    
    void InitializeChat()
    {
        try
        {
            isRunning = true;
            
            if (isServer)
            {
                // SERVIDOR: Escuta em qualquer IP na porta especificada
                udpClient = new UdpClient(chatPort);
                remoteEndPoint = new IPEndPoint(IPAddress.Any, chatPort);
                AddSystemMessage($"Servidor de chat iniciado na porta {chatPort}");
            }
            else
            {
                // CLIENTE: Cria cliente e define servidor remoto
                udpClient = new UdpClient();
                remoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), chatPort);
                
                // Envia mensagem de conexão
                SendConnectionMessage();
            }
            
            // Inicia thread separada para recebimento
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (System.Exception e)
        {
            AddSystemMessage($"ERRO ao iniciar chat: {e.Message}");
        }
    }
    
    void ReceiveData()
    {
        while (isRunning)
        {
            try
            {
                IPEndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] receivedData = udpClient.Receive(ref senderEndPoint);
                
                string message = Encoding.UTF8.GetString(receivedData);
                
                // Adiciona à fila para processar na thread principal
                lock (messageQueue)
                {
                    messageQueue.Enqueue($"{senderEndPoint.Address}:{senderEndPoint.Port}|{message}");
                }
            }
            catch (System.Exception e)
            {
                if (isRunning)
                {
                    Debug.LogError($"Erro ao receber: {e.Message}");
                }
            }
        }
    }
    
    void SendConnectionMessage()
    {
        string connectMsg = $"CONNECT|{playerName}";
        byte[] data = Encoding.UTF8.GetBytes(connectMsg);
        udpClient.Send(data, data.Length, remoteEndPoint);
    }
    
    void Update()
    {
        // Processa mensagens recebidas na thread principal
        lock (messageQueue)
        {
            while (messageQueue.Count > 0)
            {
                string data = messageQueue.Dequeue();
                string[] parts = data.Split('|');
                
                if (parts.Length >= 2)
                {
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
            }
        }
        
        // Envia mensagem ao pressionar Enter
        if (Input.GetKeyDown(KeyCode.Return) && chatInput.isFocused)
        {
            SendChatMessage();
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
                AddSystemMessage($"{name} entrou no chat");
                
                // Notifica outros clientes
                BroadcastMessage($"CHAT|SISTEMA|{name} entrou no chat", newClient);
            }
        }
        else
        {
            AddSystemMessage("Conectado ao servidor!");
        }
    }
    
    void HandleChatMessage(string sender, string message)
    {
        AddChatMessage(sender, message);
        
        // Se for servidor, faz broadcast para outros clientes
        if (isServer)
        {
            BroadcastMessage($"CHAT|{sender}|{message}");
        }
    }
    
    void HandleDisconnection(string name)
    {
        AddSystemMessage($"{name} saiu do chat");
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
        if (chatInput == null || string.IsNullOrEmpty(chatInput.text))
            return;
        
        string message = chatInput.text.Trim();
        
        if (message.Length > 0)
        {
            // Mostra localmente
            AddChatMessage(playerName, message);
            
            // Envia pela rede
            string networkMessage = $"CHAT|{playerName}|{message}";
            byte[] data = Encoding.UTF8.GetBytes(networkMessage);
            
            try
            {
                if (isServer)
                {
                    // Servidor envia para todos os clientes
                    BroadcastMessage(networkMessage);
                }
                else
                {
                    // Cliente envia para o servidor
                    udpClient.Send(data, data.Length, remoteEndPoint);
                }
            }
            catch (System.Exception e)
            {
                AddSystemMessage($"Erro ao enviar: {e.Message}");
            }
            
            chatInput.text = "";
            chatInput.ActivateInputField();
        }
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
            catch (System.Exception e)
            {
                Debug.LogError($"Erro ao enviar para {client.Address}: {e.Message}");
            }
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
        
        // Remove mensagens antigas
        if (chatMessages.Count > maxMessages)
        {
            chatMessages.RemoveAt(0);
        }
        
        // Atualiza display
        UpdateChatDisplay();
    }
    
    void UpdateChatDisplay()
    {
        if (chatDisplay != null)
        {
            chatDisplay.text = string.Join("\n", chatMessages.ToArray());
            
            // Auto-scroll para baixo
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
        if (udpClient != null && isRunning)
        {
            isRunning = false;
            
            // Envia mensagem de desconexão
            try
            {
                string disconnectMsg = $"DISCONNECT|{playerName}";
                byte[] data = Encoding.UTF8.GetBytes(disconnectMsg);
                
                if (isServer)
                {
                    BroadcastMessage(disconnectMsg);
                }
                else
                {
                    udpClient.Send(data, data.Length, remoteEndPoint);
                }
            }
            catch { }
            
            // Fecha cliente UDP
            try
            {
                udpClient.Close();
            }
            catch { }
            
            // Para a thread
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Abort();
            }
            
            udpClient = null;
        }
    }
}