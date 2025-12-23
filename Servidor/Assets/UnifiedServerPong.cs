using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

public class UnifiedServerPong : MonoBehaviour
{
    [Header("Portas de Rede")]
    public int udpGamePort = 5001;
    public int tcpChatPort = 5556;
    
    [Header("Objetos do Jogo - SERVIDOR É AUTORIDADE")]
    public GameObject paddle1;
    public GameObject paddle2;
    public GameObject ball;
    
    [Header("Configurações do Jogo")]
    public float paddleSpeed = 25f;
    public float ballSpeed = 10f;
    
    // ========== UDP GAME (AUTHORITATIVE SERVER) ==========
    private UdpClient udpServer;
    private IPEndPoint udpAnyEP;
    private Thread udpThread;
    
    // Posições enviadas pelos clientes (input apenas)
    private Dictionary<int, float> playerInputs = new Dictionary<int, float>(); // ID -> input vertical
    private Dictionary<string, int> udpClients = new Dictionary<string, int>(); // address:port -> playerID
    private Dictionary<string, IPEndPoint> udpEndpoints = new Dictionary<string, IPEndPoint>();
    private int nextPlayerId = 1;
    
    // Componentes físicos (AUTORIDADE)
    private Rigidbody2D paddle1Rb;
    private Rigidbody2D paddle2Rb;
    private Rigidbody2D ballRb;
    
    // ========== TCP CHAT (RELAY SERVER) ==========
    private TcpListener tcpListener;
    private List<TcpClient> chatClients = new List<TcpClient>();
    private List<string> chatNames = new List<string>();
    private Thread tcpListenThread;
    
    // ========== CONTROLE ==========
    private volatile bool isRunning = false;
    
    void Start()
    {
        // Pega componentes físicos
        if (paddle1 != null) paddle1Rb = paddle1.GetComponent<Rigidbody2D>();
        if (paddle2 != null) paddle2Rb = paddle2.GetComponent<Rigidbody2D>();
        if (ball != null) ballRb = ball.GetComponent<Rigidbody2D>();
        
        isRunning = true;
        StartUdpGameServer();
        StartTcpChatServer();
        
        // Inicia movimento da bola
        if (ballRb != null)
        {
            ballRb.linearVelocity = new Vector2(ballSpeed, ballSpeed);
        }
    }
    
    // ===================================================================
    // UDP GAME SERVER - AUTHORITATIVE (Física roda aqui)
    // ===================================================================
    
    void StartUdpGameServer()
    {
        try
        {
            udpServer = new UdpClient(udpGamePort);
            udpAnyEP = new IPEndPoint(IPAddress.Any, 0);
            
            udpThread = new Thread(ReceiveGameInput);
            udpThread.IsBackground = true;
            udpThread.Start();
            
            Debug.Log($"[UDP Game] Servidor AUTORITATIVO iniciado na porta {udpGamePort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UDP] Erro: {e.Message}");
        }
    }
    
    void ReceiveGameInput()
    {
        while (isRunning)
        {
            try
            {
                byte[] data = udpServer.Receive(ref udpAnyEP);
                string msg = Encoding.UTF8.GetString(data);
                string clientKey = udpAnyEP.Address + ":" + udpAnyEP.Port;
                
                // HELLO - Atribui ID ao novo jogador
                if (msg == "HELLO")
                {
                    if (!udpClients.ContainsKey(clientKey))
                    {
                        if (nextPlayerId > 2)
                        {
                            Debug.LogWarning($"[UDP] Sala cheia. Rejeitando: {clientKey}");
                            continue;
                        }
                        
                        int playerId = nextPlayerId++;
                        udpClients[clientKey] = playerId;
                        udpEndpoints[clientKey] = new IPEndPoint(udpAnyEP.Address, udpAnyEP.Port);
                        
                        // Envia ID do jogador
                        string assignMsg = "ASSIGN:" + playerId;
                        byte[] assignData = Encoding.UTF8.GetBytes(assignMsg);
                        udpServer.Send(assignData, assignData.Length, udpAnyEP);
                        
                        Debug.Log($"[UDP] Jogador {playerId} conectado: {clientKey}");
                    }
                }
                // INPUT - Recebe input vertical do cliente
                else if (msg.StartsWith("INPUT:"))
                {
                    if (udpClients.ContainsKey(clientKey))
                    {
                        int playerId = udpClients[clientKey];
                        float input = float.Parse(msg.Substring(6), CultureInfo.InvariantCulture);
                        
                        lock (playerInputs)
                        {
                            playerInputs[playerId] = input;
                        }
                    }
                }
            }
            catch (SocketException) { Thread.Sleep(10); }
            catch (System.Exception e)
            {
                Debug.LogError($"[UDP] Erro: {e.Message}");
            }
        }
    }
    
    // ===================================================================
    // TCP CHAT SERVER - RELAY (Apenas retransmite mensagens)
    // ===================================================================
    
    void StartTcpChatServer()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, tcpChatPort);
            tcpListener.Start();
            
            tcpListenThread = new Thread(ListenForChatClients);
            tcpListenThread.IsBackground = true;
            tcpListenThread.Start();
            
            Debug.Log($"[TCP Chat] Servidor RELAY iniciado na porta {tcpChatPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TCP] Erro: {e.Message}");
        }
    }
    
    void ListenForChatClients()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                
                lock (chatClients)
                {
                    chatClients.Add(client);
                    chatNames.Add("Desconhecido");
                }
                
                Thread clientThread = new Thread(() => HandleChatClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();
                
                Debug.Log("[TCP Chat] Cliente conectado");
            }
            catch (SocketException) { Thread.Sleep(100); }
            catch (System.Exception e)
            {
                Debug.LogError($"[TCP] Erro: {e.Message}");
            }
        }
    }
    
    void HandleChatClient(TcpClient client)
    {
        NetworkStream stream = null;
        StreamReader reader = null;
        int clientIndex = -1;
        
        try
        {
            stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.UTF8);
            
            lock (chatClients)
            {
                clientIndex = chatClients.IndexOf(client);
            }
            
            while (isRunning && client.Connected)
            {
                if (stream.DataAvailable)
                {
                    string message = reader.ReadLine();
                    if (!string.IsNullOrEmpty(message))
                    {
                        ProcessChatMessage(message, clientIndex);
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }
        catch { }
        finally
        {
            string name = "Desconhecido";
            
            lock (chatClients)
            {
                if (clientIndex >= 0 && clientIndex < chatNames.Count)
                {
                    name = chatNames[clientIndex];
                    chatNames.RemoveAt(clientIndex);
                }
                chatClients.Remove(client);
            }
            
            reader?.Close();
            stream?.Close();
            client?.Close();
            
            RelayChatMessage($"SYSTEM|{name} saiu do chat");
        }
    }
    
    void ProcessChatMessage(string message, int clientIndex)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 2) return;
        
        if (parts[0] == "CONNECT" && parts.Length >= 2)
        {
            lock (chatClients)
            {
                if (clientIndex >= 0 && clientIndex < chatNames.Count)
                {
                    chatNames[clientIndex] = parts[1];
                }
            }
            RelayChatMessage($"SYSTEM|{parts[1]} entrou no chat");
        }
        else if (parts[0] == "CHAT" && parts.Length >= 3)
        {
            // RELAY - Apenas retransmite sem processar
            RelayChatMessage(message);
        }
    }
    
    void RelayChatMessage(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n");
        
        lock (chatClients)
        {
            foreach (TcpClient client in chatClients.ToArray())
            {
                try
                {
                    if (client.Connected)
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                    }
                }
                catch { }
            }
        }
    }
    
    // ===================================================================
    // UPDATE - SIMULAÇÃO AUTORITATIVA DO JOGO
    // ===================================================================
    
    void FixedUpdate()
    {
        // AUTORIDADE: Move raquetes baseado no input dos clientes
        lock (playerInputs)
        {
            if (playerInputs.ContainsKey(1) && paddle1Rb != null)
            {
                Vector2 pos = paddle1Rb.position;
                pos.y += playerInputs[1] * paddleSpeed * Time.fixedDeltaTime;
                pos.y = Mathf.Clamp(pos.y, -4.5f, 4.5f);
                paddle1Rb.MovePosition(pos);
            }
            
            if (playerInputs.ContainsKey(2) && paddle2Rb != null)
            {
                Vector2 pos = paddle2Rb.position;
                pos.y += playerInputs[2] * paddleSpeed * Time.fixedDeltaTime;
                pos.y = Mathf.Clamp(pos.y, -4.5f, 4.5f);
                paddle2Rb.MovePosition(pos);
            }
        }
        
        // AUTORIDADE: Física da bola roda aqui (colisões, velocidade)
        // O Rigidbody2D já cuida disso automaticamente
        
        // Broadcast estado do jogo para todos os clientes
        BroadcastGameState();
    }
    
    void BroadcastGameState()
    {
        // Envia posições das raquetes
        if (paddle1 != null)
        {
            Vector2 pos = paddle1.transform.position;
            string msg = $"PLAYER:1:{pos.x.ToString("F2", CultureInfo.InvariantCulture)};{pos.y.ToString("F2", CultureInfo.InvariantCulture)}";
            SendToAllUdp(msg);
        }
        
        if (paddle2 != null)
        {
            Vector2 pos = paddle2.transform.position;
            string msg = $"PLAYER:2:{pos.x.ToString("F2", CultureInfo.InvariantCulture)};{pos.y.ToString("F2", CultureInfo.InvariantCulture)}";
            SendToAllUdp(msg);
        }
        
        // Envia posição da bola
        if (ball != null)
        {
            Vector2 pos = ball.transform.position;
            string msg = $"BALL:{pos.x.ToString("F2", CultureInfo.InvariantCulture)};{pos.y.ToString("F2", CultureInfo.InvariantCulture)}";
            SendToAllUdp(msg);
        }
    }
    
    void SendToAllUdp(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        
        foreach (var kvp in udpEndpoints)
        {
            try
            {
                udpServer.Send(data, data.Length, kvp.Value);
            }
            catch { }
        }
    }
    
    // ===================================================================
    // CLEANUP
    // ===================================================================
    
    void OnApplicationQuit()
    {
        Shutdown();
    }
    
    void OnDestroy()
    {
        Shutdown();
    }
    
    void Shutdown()
    {
        if (!isRunning) return;
        isRunning = false;
        
        // UDP
        try { udpServer?.Close(); } catch { }
        if (udpThread != null && udpThread.IsAlive) udpThread.Join(500);
        
        // TCP
        try { tcpListener?.Stop(); } catch { }
        lock (chatClients)
        {
            foreach (var client in chatClients) 
            {
                try { client.Close(); } catch { }
            }
            chatClients.Clear();
        }
        if (tcpListenThread != null && tcpListenThread.IsAlive) tcpListenThread.Join(500);
        
        Debug.Log("[Servidor] Desligado");
    }
}