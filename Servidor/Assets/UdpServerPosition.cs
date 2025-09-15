using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class UdpServerPosition : MonoBehaviour
{

    UdpClient server;

    IPEndPoint clientEP;

    Thread receiveThread;

    void Start()
    {
        server = new UdpClient(5001);
        clientEP = new IPEndPoint(IPAddress.Any, 0);
        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();
        Debug.Log("Servidor iniciado na porta 5001");
    }
    void ReceiveData() {

    while (true)
    {
        byte[] data = server.Receive(ref clientEP);
        string msg = Encoding.UTF8.GetString(data);
        Debug.Log("Posição recebida: " + msg);
    }

}
    void OnApplicationQuit()
    {
        receiveThread.Abort();
        server.Close();
    }
    
}