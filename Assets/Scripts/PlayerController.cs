using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.U2D;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;
    public Rigidbody2D playerRb;
    public GameObject SnowBall;
    private Animator anime;
    // private Animator Anime;
    public float speed;

    //bool run = false;

    // Start is called before the first frame update
    void Start()
    {
        GameObject.Find("Snowman").GetComponentInChildren<Camera>().enabled = true;
        playerRb = GetComponent<Rigidbody2D>();
        anime = GetComponent<Animator>();
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        //DontDestroyOnLoad(gameObject);
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void FixedUpdate()
    {
        Movement();
    }

    void Movement()
    {
        float Hmove = Input.GetAxis("Horizontal");
        float Vmove = Input.GetAxis("Vertical");
        float Move = Mathf.Abs(Hmove) < Mathf.Abs(Vmove) ? Vmove : Hmove;
        float direction = Input.GetAxisRaw("Horizontal");
        SnowBall.transform.Rotate(0, 0, -Move * speed);

        /*if (Input.GetButton("Horizontal") || Input.GetButton("Vertical"))
        {
            run = true;
        }
        else
        {
            run = false;
        }*/

        playerRb.velocity = new Vector2(Hmove * speed, Vmove * speed);
        //anime.SetBool("running", run);

        if (direction != 0)
        {
            if (direction > 0)
            {
                GetComponent<SpriteRenderer>().flipX = true;
            }
            else
            {
                GetComponent<SpriteRenderer>().flipX = false;
            }
        }
    }
}