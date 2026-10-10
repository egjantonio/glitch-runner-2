using System.Collections.Generic;
using UnityEngine;


public class PlayerController : MonoBehaviour
{
    [Header("Hitbox (unidades de mundo)")]
    public float width = 0.9f;
    public float height = 1.8f;

    [Header("Física (unidades por segundo)")]
    public float gravity = 42f;
    public float jumpVelocity = 14f;
    public float maxFallSpeed = 19f;
    public float jumpHoldGravityMultiplier = 0.45f;
    public float maxJumpHoldTime = 0.25f;

    [Header("Movimiento")]
    public float walkSpeed = 3.8f;
    public float runSpeed = 7.6f;   

    [Header("Ataque")]
    public float attackReach = 1.6f;
    private const float AttackDuration = 7 * 0.045f;  
    private const float AttackActiveStart = 0.09f;    
    private const float AttackActiveEnd = 0.23f;

    [Header("Vida")]
    public int maxHealth = 4;
    public float invulnerableTime = 1.2f;   
    public float hurtStunTime = 0.25f;      
    public float knockbackSpeed = 7f;       
    public float knockbackUp = 8f;          

    public int Health { get; private set; }

    [HideInInspector] public List<Rect> Platforms;
    [HideInInspector] public List<Enemy> Enemies;
    [HideInInspector] public float minX = 0f;
    [HideInInspector] public float maxX = 1000f;

    public Vector2 Position => pos;   
    public Rect Bounds => new Rect(pos.x - width / 2f, pos.y, width, height);

    private SpriteRenderer sr;
    private SpriteAnim idle, walk, run, jump, fall, attack, current;

    private Vector2 pos;
    private float vy;
    private bool airborne = true;
    private bool facingRight = true;

    private bool running;

    private bool boostActive;
    private float boostTimer;

    private bool attacking;
    private float attackTime;
    private readonly HashSet<Enemy> hitThisAttack = new HashSet<Enemy>();

    private float invulTimer;
    private float stunTimer;
    private float knockVx;

    private bool prevJump, prevAttack;

    private void Awake()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 3;
        Health = maxHealth;
    }

    public void Init(SpriteAnim idle, SpriteAnim walk, SpriteAnim run,
                     SpriteAnim jump, SpriteAnim fall, SpriteAnim attack)
    {
        this.idle = idle; this.walk = walk; this.run = run;
        this.jump = jump; this.fall = fall; this.attack = attack;
        current = idle;
    }

    public void Place(float x, float feetY)
    {
        pos = new Vector2(x, feetY);
        vy = 0f;
        airborne = true;
        running = false;
        boostActive = false;
        attacking = false;
        facingRight = true;
        invulTimer = 0f;
        stunTimer = 0f;
        knockVx = 0f;
        sr.color = Color.white;
        transform.position = new Vector3(pos.x, pos.y, 0f);
    }

    
    public void ResetHealth()
    {
        Health = maxHealth;
        invulTimer = 0f;
        stunTimer = 0f;
    }

    
    public bool TakeDamage(int amount, float sourceX)
    {
        if (invulTimer > 0f || Health <= 0) return false;

        Health = Mathf.Max(0, Health - amount);
        invulTimer = invulnerableTime;
        stunTimer = hurtStunTime;

        float dir = pos.x >= sourceX ? 1f : -1f;
        knockVx = dir * knockbackSpeed;
        vy = knockbackUp;
        airborne = true;
        boostActive = false;
        attacking = false;
        return true;
    }

    public void Tick(float dt)
    {
        if (invulTimer > 0f) invulTimer -= dt;
        bool stunned = stunTimer > 0f;
        if (stunned) stunTimer -= dt;

        bool left = !stunned && GameInput.Left;
        bool right = !stunned && GameInput.Right;
        bool jumpHeld = !stunned && GameInput.JumpHeld;
        bool attackHeld = !stunned && GameInput.AttackHeld;
        bool jumpDown = jumpHeld && !prevJump;
        bool attackDown = attackHeld && !prevAttack;
        prevJump = jumpHeld;
        prevAttack = attackHeld;

        bool moving = left || right;
        running = moving && GameInput.RunHeld;   
        float speed = running ? runSpeed : walkSpeed;

        if (left) { pos.x -= speed * dt; facingRight = false; }
        if (right) { pos.x += speed * dt; facingRight = true; }
        if (stunned) pos.x += knockVx * dt;   
        pos.x = Mathf.Clamp(pos.x, minX, maxX);

        // --- Salto ---
        if (jumpDown && !airborne)
        {
            vy = jumpVelocity;
            airborne = true;
            jump.Reset();
            boostActive = true;
            boostTimer = 0f;
        }
        if (!jumpHeld) boostActive = false;

        // --- Gravedad (con salto de altura variable) ---
        if (boostActive && vy > 0f && boostTimer < maxJumpHoldTime)
        {
            boostTimer += dt;
            vy -= gravity * jumpHoldGravityMultiplier * dt;
        }
        else
        {
            vy -= gravity * dt;
        }
        if (vy < -maxFallSpeed) vy = -maxFallSpeed;

        float prevBottom = pos.y;
        pos.y += vy * dt;

        
        bool landed = false;
        if (vy <= 0f && Platforms != null)
        {
            float halfW = width / 2f;
            float bestTop = float.NegativeInfinity;
            foreach (var p in Platforms)
            {
                if (pos.x + halfW <= p.xMin || pos.x - halfW >= p.xMax) continue;
                float top = p.yMax;
                if (prevBottom >= top - 0.1f && pos.y <= top && top > bestTop)
                {
                    bestTop = top;
                    landed = true;
                }
            }
            if (landed)
            {
                pos.y = bestTop;
                vy = 0f;
            }
        }
        airborne = !landed;

        
        if (attackDown && !attacking)
        {
            attacking = true;
            attackTime = 0f;
            attack.Reset();
            hitThisAttack.Clear();
        }
        if (attacking)
        {
            attackTime += dt;
            if (attackTime >= AttackDuration) attacking = false;
            else if (attackTime >= AttackActiveStart && attackTime <= AttackActiveEnd) DoAttackHit();
        }

       
        if (attacking) current = attack;
        else if (airborne && vy > 0f) current = jump;
        else if (airborne) current = fall;
        else if (running && moving) current = run;
        else if (moving) current = walk;
        else current = idle;

        current.Update(dt);
        sr.sprite = current.Current;
        sr.flipX = !facingRight;

        float alpha = (invulTimer > 0f && (int)(invulTimer * 14f) % 2 == 0) ? 0.3f : 1f;
        sr.color = new Color(1f, 1f, 1f, alpha);

        transform.position = new Vector3(pos.x, pos.y, 0f);
    }

    private void DoAttackHit()
    {
        if (Enemies == null) return;
        float x0 = facingRight ? pos.x : pos.x - attackReach;
        var hit = new Rect(x0, pos.y, attackReach, height);

        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var e = Enemies[i];
            if (hitThisAttack.Contains(e)) continue;
            if (hit.Overlaps(e.Bounds))
            {
                hitThisAttack.Add(e);
                if (e.TakeHit())
                {
                    Enemies.RemoveAt(i);
                    Destroy(e.gameObject);
                }
            }
        }
    }
}
