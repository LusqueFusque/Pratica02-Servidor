using UnityEngine;
using System.Globalization;

[RequireComponent(typeof(Rigidbody2D))]
public class PongBall : MonoBehaviour
{
    public float speed = 5f;                // Velocidade da bola
    private Vector2 direction;              // Direção da bola

    public Vector2 paddle1Pos;              // Posição do paddle1 (atualizada pelo servidor)
    public Vector2 paddle2Pos;              // Posição do paddle2 (atualizada pelo servidor)

    public UdpServerPong server;            // Referência ao servidor para enviar posição

    Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Direção inicial aleatória
        direction = new Vector2(Random.value < 0.5f ? -1 : 1, Random.Range(-0.5f, 0.5f)).normalized;

        // Define velocidade inicial usando Rigidbody2D
        rb.linearVelocity = direction * speed;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // evita atravessar colliders
    }

    void FixedUpdate()
    {
        // Atualiza posição dos paddles no servidor (para cálculos de colisão ou lógica)
        // Opcional, caso você queira usar para outras coisas
        // paddle1Pos e paddle2Pos já são atualizados no Update do servidor

        // Envia posição atual para todos os clientes
        SendBallPosition();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Se bater em parede (parede vertical ou horizontal)
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Inverte a direção vertical se for parede horizontal (topo/baixo)
            if (collision.contacts[0].normal.y != 0)
                direction = new Vector2(direction.x, -direction.y);

            // Inverte a direção horizontal se for parede lateral (esquerda/direita)
            if (collision.contacts[0].normal.x != 0)
                direction = new Vector2(-direction.x, direction.y);

            rb.linearVelocity = direction * speed;
        }
        // Se bater em um paddle
        else if (collision.gameObject.CompareTag("Paddle"))
        {
            // Inverte a direção horizontal
            direction = new Vector2(-direction.x, direction.y);

            // Opcional: acrescenta efeito baseado na posição do paddle
            float offset = transform.position.y - collision.transform.position.y;
            direction.y = offset * 2f; // quanto maior o offset, mais inclinado o rebote
            direction.Normalize();

            rb.linearVelocity = direction * speed;
        }
    }

    void SendBallPosition()
    {
        string msg = $"BALL:{transform.position.x.ToString("F2", CultureInfo.InvariantCulture)};" +
                     $"{transform.position.y.ToString("F2", CultureInfo.InvariantCulture)}";
        server.BroadcastToAllClients(msg);
    }
}