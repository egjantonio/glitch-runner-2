using UnityEngine;

/// <summary>
/// Jefe del nivel 2, piso 5. Hereda de Enemy, así que el ataque del jugador, la lista de
/// enemigos y la colisión de contacto lo tratan igual que a cualquier otro enemigo.
///
/// Comportamiento (máquina de estados):
///   Patrol    -> camina al azar dentro de su arena. Si el jugador está cerca y terminó el
///                enfriamiento, prepara una embestida.
///   Telegraph -> se detiene y parpadea en amarillo (aviso: es el momento de esquivar).
///   Charge    -> embiste en línea recta hacia donde estaba el jugador. Hace 2 de daño.
///   Recover   -> se queda aturdido (azulado). Es la mejor ventana para atacarlo.
/// </summary>
public class Boss : Enemy
{
    // --- Ajustes de combate (unidades de mundo y segundos): cámbialos para balancear ---
    private const float PatrolSpeed = 1.6f;
    private const float ChargeSpeed = 6.5f;
    private const float AggroRange = 10f;
    private const float TelegraphTime = 0.7f;
    private const float ChargeTime = 1.1f;
    private const float RecoverTime = 1.0f;
    private const float MinCooldown = 1.2f;
    private const float MaxCooldown = 2.4f;
    private const float HitFlashTime = 0.12f;

    public int MaxHp { get; private set; }
    public float HpFraction => MaxHp > 0 ? Mathf.Clamp01((float)Hp / MaxHp) : 0f;

    // La embestida duele el doble que el contacto normal
    public override int ContactDamage => state == State.Charge ? 2 : 1;

    private enum State { Patrol, Telegraph, Charge, Recover }
    private State state = State.Patrol;

    private PlayerController target;
    private SpriteRenderer sr;
    private Color baseColor = Color.white;

    private float arenaMin, arenaMax;
    private float velocityX, changeTimer, stateTimer, cooldown, flashTimer, chargeDir;

    public void InitBoss(Sprite body, float width, float height, int maxHp,
                         float x, float groundY, float arenaMin, float arenaMax,
                         PlayerController target, Color baseColor)
    {
        Width = width;
        Height = height;
        MaxHp = maxHp;
        Hp = maxHp;
        this.arenaMin = arenaMin;
        this.arenaMax = arenaMax;
        this.target = target;
        this.baseColor = baseColor;

        cooldown = Random.Range(MinCooldown, MaxCooldown);
        changeTimer = 0f;
        velocityX = 0f;

        transform.position = new Vector3(x, groundY, 0f);

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = body;
        sr.sortingOrder = 2;
        sr.color = baseColor;
    }

    public override void Tick(float dt)
    {
        if (flashTimer > 0f) flashTimer -= dt;

        var p = transform.position;

        switch (state)
        {
            case State.Patrol:
                changeTimer -= dt;
                if (changeTimer <= 0f)
                {
                    int r = Random.Range(0, 3);
                    velocityX = r == 0 ? -PatrolSpeed : (r == 1 ? PatrolSpeed : 0f);
                    changeTimer = Random.Range(0.8f, 2f);
                }
                p.x += velocityX * dt;

                cooldown -= dt;
                if (cooldown <= 0f && target != null && Mathf.Abs(target.Position.x - p.x) < AggroRange)
                {
                    state = State.Telegraph;
                    stateTimer = TelegraphTime;
                    chargeDir = target.Position.x >= p.x ? 1f : -1f;
                    velocityX = 0f;
                }
                break;

            case State.Telegraph:
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = State.Charge;
                    stateTimer = ChargeTime;
                }
                break;

            case State.Charge:
                stateTimer -= dt;
                p.x += chargeDir * ChargeSpeed * dt;
                if (stateTimer <= 0f) BeginRecover();
                break;

            case State.Recover:
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = State.Patrol;
                    cooldown = Random.Range(MinCooldown, MaxCooldown);
                    changeTimer = 0f;
                }
                break;
        }

        // Límites de la arena (x es el centro del jefe)
        float halfW = Width / 2f;
        float minC = arenaMin + halfW;
        float maxC = arenaMax - halfW;
        bool hitLeft = p.x < minC;
        bool hitRight = p.x > maxC;
        p.x = Mathf.Clamp(p.x, minC, maxC);
        if (hitLeft || hitRight)
        {
            if (state == State.Charge) BeginRecover();                       // chocó con la pared
            else if (state == State.Patrol) velocityX = hitLeft ? PatrolSpeed : -PatrolSpeed;
        }

        transform.position = p;
        UpdateColor();
    }

    private void BeginRecover()
    {
        state = State.Recover;
        stateTimer = RecoverTime;
    }

    private void UpdateColor()
    {
        Color c = baseColor;
        switch (state)
        {
            case State.Telegraph:
                // parpadeo amarillo
                if ((int)(stateTimer * 14f) % 2 == 0) c = new Color(1f, 1f, 0.3f);
                break;
            case State.Charge:
                c = new Color(1f, 0.4f, 0.4f);
                break;
            case State.Recover:
                c = new Color(0.65f, 0.8f, 1f);
                break;
        }
        if (flashTimer > 0f) c = new Color(0.35f, 0.35f, 0.35f); // destello oscuro al recibir un golpe
        sr.color = c;
    }

    public override bool TakeHit()
    {
        Hp--;
        flashTimer = HitFlashTime;
        return Hp <= 0;
    }
}
