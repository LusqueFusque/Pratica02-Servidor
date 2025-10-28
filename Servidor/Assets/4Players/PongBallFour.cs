using UnityEngine;
using System.Globalization;

[RequireComponent(typeof(Rigidbody2D))]
public class PongBallFour : MonoBehaviour
{
    public float speed = 5f;
    private Vector2 direction;
    
    // Posições dos 4 paddles (atualizadas pelo servidor)
    public Vector2 paddle1Pos;
    public Vector2 paddle2Pos;
    public Vector2 paddle3Pos;
    public Vector2 paddle4Pos;
    
    public UdpServerPongFour server;
    Rigidbody2D rb;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        direction = new Vector2(Random.value < 0.5f ? -1 : 1, Random.Range(-0.5f, 0.5f)).normalized;
        rb.linearVelocity = direction * speed;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }
    
    void FixedUpdate()
    {
        SendBallPosition();
    }
    
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            if (collision.contacts[0].normal.y != 0)
                direction = new Vector2(direction.x, -direction.y);
            
            if (collision.contacts[0].normal.x != 0)
                direction = new Vector2(-direction.x, direction.y);
            
            rb.linearVelocity = direction * speed;
        }
        else if (collision.gameObject.CompareTag("Paddle"))
        {
            direction = new Vector2(-direction.x, direction.y);
            float offset = transform.position.y - collision.transform.position.y;
            direction.y = offset * 2f;
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