using UnityEngine;

/// <summary>
/// MissingNo: patrulla aleatoriamente cerca de su punto de origen (homeX),
/// tiene vida y dibuja un corazón por cada punto de vida restante.
/// El pivote del sprite está en la base, así que transform.position.y son los "pies".
///
/// Tick, TakeHit y ContactDamage son virtuales para que Boss (el jefe) los reemplace.
/// </summary>
public class Enemy : MonoBehaviour
{
    public int Hp { get; protected set; }
    public float Width { get; protected set; }
    public float Height { get; protected set; }

    /// <summary>Cuánta vida le quita al jugador al tocarlo.</summary>
    public virtual int ContactDamage => 1;

    public Rect Bounds => new Rect(transform.position.x - Width / 2f, transform.position.y, Width, Height);

    private float homeX, leash, patrolSpeed;
    private float velocityX, changeTimer;
    private int maxHp;
    private SpriteRenderer[] hearts;
    private float heartSpacing;
    private float heartHeight;

    public void Init(Sprite body, float width, float height,
                     Sprite heart, float heartHeight,
                     int maxHp, float patrolSpeed, float leash,
                     float x, float groundY)
    {
        Width = width;
        Height = height;
        Hp = this.maxHp = maxHp;
        this.patrolSpeed = patrolSpeed;
        this.leash = leash;
        this.heartHeight = heartHeight;
        homeX = x;
        velocityX = Random.value < 0.5f ? -patrolSpeed : patrolSpeed;
        changeTimer = Random.Range(0.8f, 2f);

        transform.position = new Vector3(x, groundY, 0f);

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = body;
        sr.sortingOrder = 2;

        // Corazones (un SpriteRenderer por punto de vida máximo; se activan según Hp)
        float aspect = heart.rect.width / heart.rect.height;
        heartSpacing = heartHeight * aspect * 1.1f;
        hearts = new SpriteRenderer[maxHp];
        for (int i = 0; i < maxHp; i++)
        {
            var h = new GameObject("Heart" + i);
            h.transform.SetParent(transform, false);
            var hsr = h.AddComponent<SpriteRenderer>();
            hsr.sprite = heart;
            hsr.sortingOrder = 4;
            hearts[i] = hsr;
        }
        UpdateHearts();
    }

    private void UpdateHearts()
    {
        float total = heartSpacing * Hp;
        float y = Height + 0.15f + heartHeight / 2f;
        for (int i = 0; i < maxHp; i++)
        {
            bool visible = i < Hp;
            hearts[i].enabled = visible;
            if (visible)
                hearts[i].transform.localPosition = new Vector3(-total / 2f + heartSpacing * (i + 0.5f), y, 0f);
        }
    }

    public virtual void Tick(float dt)
    {
        changeTimer -= dt;
        if (changeTimer <= 0f)
        {
            int r = Random.Range(0, 3);
            velocityX = r == 0 ? -patrolSpeed : (r == 1 ? patrolSpeed : 0f);
            changeTimer = Random.Range(0.8f, 2f);
        }

        var p = transform.position;
        p.x += velocityX * dt;
        if (p.x < homeX - leash) { p.x = homeX - leash; velocityX = patrolSpeed; }
        if (p.x > homeX + leash) { p.x = homeX + leash; velocityX = -patrolSpeed; }
        transform.position = p;
    }

    /// <summary>Resta 1 de vida. Devuelve true si el enemigo murió.</summary>
    public virtual bool TakeHit()
    {
        Hp--;
        UpdateHearts();
        return Hp <= 0;
    }
}
