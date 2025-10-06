using UnityEngine;
using System.Globalization;

[RequireComponent(typeof(Rigidbody2D))]
public class PongBall : MonoBehaviour
{
    public float speed = 5f;
    private Vector2 direction;

    public float topLimit = 4.5f;
    public float bottomLimit = -4.5f;

    public Vector2 paddle1Pos;
    public Vector2 paddle2Pos;
    public float paddleWidth = 0.5f;
    public float paddleHeight = 1.5f;

    public UdpServerPong server;

    Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Direção inicial aleatória
        direction = new Vector2(Random.value < 0.5f ? -1 : 1, Random.Range(-0.5f, 0.5f)).normalized;

        // Velocidade inicial
        rb.velocity = direction * speed;
    }

    void FixedUpdate()
    {
        // Envia posição atual para os clientes
        SendBallPosition();

        // Verifica se saiu dos limites verticais
        if (transform.position.y > topLimit || transform.position.y < bottomLimit)
        {
            direction = new Vector2(direction.x, -direction.y);
            rb.velocity = direction * speed;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Rebate horizontalmente
        direction = new Vector2(-direction.x, direction.y);
        rb.velocity = direction * speed;
    }

    void SendBallPosition()
    {
        string msg = $"BALL:{transform.position.x.ToString("F2", CultureInfo.InvariantCulture)};{transform.position.y.ToString("F2", CultureInfo.InvariantCulture)}";
        server.BroadcastToAllClients(msg);
    }
}