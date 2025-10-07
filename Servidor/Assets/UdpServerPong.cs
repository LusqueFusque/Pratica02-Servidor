using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UdpServerPong : MonoBehaviour
{
    UdpClient server;
    IPEndPoint anyEP;
    Thread receiveThread;

    public Dictionary<int, Vector2> playerPositions = new Dictionary<int, Vector2>();
    Dictionary<string, int> clientIds = new Dictionary<string, int>();

    public PongBall ballScript; // arraste a bolinha no Inspector
    public GameObject paddle1Obj; // paddle esquerda
    public GameObject paddle2Obj; // paddle direita

    public bool running = true;
    int nextId = 1; // próximo ID a ser atribuído

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);

        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("Servidor iniciado na porta 5001");
    }

    void Update()
    {
        // Atualiza posição física das raquetes no servidor
        if (playerPositions.ContainsKey(1) && paddle1Obj != null)
            paddle1Obj.GetComponent<Rigidbody2D>().MovePosition(playerPositions[1]);

        if (playerPositions.ContainsKey(2) && paddle2Obj != null)
            paddle2Obj.GetComponent<Rigidbody2D>().MovePosition(playerPositions[2]);

        // Passa posições atualizadas para o script da bola
        if (ballScript != null)
        {
            ballScript.paddle1Pos = playerPositions.ContainsKey(1) ? playerPositions[1] : paddle1Obj.transform.position;
            ballScript.paddle2Pos = playerPositions.ContainsKey(2) ? playerPositions[2] : paddle2Obj.transform.position;
        }
    }

    void ReceiveData()
    {
        while (running)
        {
            try
            {
                byte[] data = server.Receive(ref anyEP);
                string msg = Encoding.UTF8.GetString(data);
                string key = anyEP.Address + ":" + anyEP.Port;

                // ===== Atribui ID ao cliente novo =====
                if (!clientIds.ContainsKey(key))
                {
                    if (nextId > 2)
                    {
                        Debug.LogWarning("Mais de 2 jogadores tentando conectar. Ignorado: " + key);
                        continue;
                    }

                    clientIds[key] = nextId;
                    int assignedId = nextId;
                    nextId++;

                    string assignMsg = "ASSIGN:" + assignedId;
                    byte[] assignData = Encoding.UTF8.GetBytes(assignMsg);
                    server.Send(assignData, assignData.Length, anyEP);

                    Debug.Log($"Novo cliente → {key} recebeu ID {assignedId}");
                }

                int id = clientIds[key];

                // ===== Atualiza posição da raquete =====
                if (msg.StartsWith("POS:"))
                {
                    string[] parts = msg.Substring(4).Split(';');
                    if (parts.Length == 2)
                    {
                        float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

                        playerPositions[id] = new Vector2(x, y);

                        // Reenvia posição apenas aos outros clientes
                        string relayMsg = $"PLAYER:{id}:{x.ToString(System.Globalization.CultureInfo.InvariantCulture)};{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                        byte[] relayData = Encoding.UTF8.GetBytes(relayMsg);

                        foreach (var kvp in clientIds)
                        {
                            if (kvp.Key != key)
                            {
                                string[] ipPort = kvp.Key.Split(':');
                                IPEndPoint clientEP = new IPEndPoint(IPAddress.Parse(ipPort[0]), int.Parse(ipPort[1]));
                                server.Send(relayData, relayData.Length, clientEP);
                            }
                        }
                    }
                }
            }
            catch (SocketException ex)
            {
                Debug.LogWarning("Socket encerrado: " + ex.Message);
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Erro no servidor: " + ex.Message);
            }
        }
    }

    public void BroadcastToAllClients(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        foreach (var kvp in clientIds)
        {
            string[] ipPort = kvp.Key.Split(':');
            IPEndPoint clientEP = new IPEndPoint(IPAddress.Parse(ipPort[0]), int.Parse(ipPort[1]));
            server.Send(data, data.Length, clientEP);
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        receiveThread?.Join();
        server?.Close();
    }
}
