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

    public bool running = true;

    int nextId = 1;

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);
        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();
        Debug.Log("Servidor iniciado na porta 5001");

    }
    void Update()
    {
        if (ballScript != null)
        {
            if (playerPositions.ContainsKey(1))
                ballScript.paddle1Pos = playerPositions[1];

            if (playerPositions.ContainsKey(2))
                ballScript.paddle2Pos = playerPositions[2];
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
                string key = anyEP.Address.ToString() + ":" + anyEP.Port;

                // Atribui ID se for cliente novo
                if (!clientIds.ContainsKey(key))
                {
                    clientIds[key] = nextId++;
                    string assignMsg = "ASSIGN:" + clientIds[key];
                    byte[] assignData = Encoding.UTF8.GetBytes(assignMsg);
                    server.Send(assignData, assignData.Length, anyEP);
                    Debug.Log("Novo cliente → " + key + " recebeu ID " + clientIds[key]);
                }

                int id = clientIds[key];

                // Se for mensagem de posição
                if (msg.StartsWith("POS:"))
                {
                    string coords = msg.Substring(4); // Remove "POS:"
                    string[] parts = coords.Split(';');
                    if (parts.Length == 2)
                    {
                        float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        playerPositions[id] = new Vector2(x, y);


                        // Reenvia para os outros clientes
                        string relayMsg =
                            $"PLAYER:{id}:{x.ToString(System.Globalization.CultureInfo.InvariantCulture)};{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
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
                Debug.LogError("Erro geral no servidor: " + ex.Message);
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
        receiveThread?.Join(); // espera o thread encerrar
        server?.Close();
    }
}
    
