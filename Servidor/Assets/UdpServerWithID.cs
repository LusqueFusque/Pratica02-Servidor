using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UdpServerWithID : MonoBehaviour
{

    UdpClient server;
    IPEndPoint anyEP;
    Thread receiveThread;
    Dictionary<string, int> clientIds = new
        Dictionary<string, int>();

    int nextId = 1;

    void Start()
    {
        server = new UdpClient(5001);
        anyEP = new IPEndPoint(IPAddress.Any, 0);
        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();
        Debug.Log("Servidor iniciado na porta 5001");
    }
    
    void ReceiveData()
    {
        while (true)
        { 
            byte[] data = server.Receive(ref anyEP);
            string msg = Encoding.UTF8.GetString(data);
            string key = anyEP.Address.ToString() + ":" + anyEP.Port;
            
            // Se o cliente é novo, atribui ID
            if (!clientIds.ContainsKey(key))
            {
                clientIds[key] = nextId++;
                string assignMsg = "ASSIGN:" + clientIds[key];
                byte[] assignData = Encoding.UTF8.GetBytes(assignMsg);
                server.Send(assignData, assignData.Length, anyEP);
                Debug.Log("Novo cliente → " + key + "recebeu ID " + clientIds[key]);
            }
            
            int id = clientIds[key];

            // Se for mensagem de posição
            if (msg.StartsWith("POS:"))
            {
                string coords = msg.Substring(4); //remove "POS:"
                string[] parts = coords.Split(';');
                if (parts.Length == 2)
                {
                    float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                    Debug.Log($"[Servidor] Recebido do ID{id} → x={x}, y={y}");
                }
            }
        }
    }

    void OnApplicationQuit() {
        receiveThread.Abort();
        server.Close();
    }
}